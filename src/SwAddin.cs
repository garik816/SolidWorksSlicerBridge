using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using Environment = System.Environment;

namespace SolidWorksSlicerBridge
{
    // SOLIDWORKS resolves toolbar callbacks by name through IDispatch.
    // ISwAddin is the lifecycle interface, not the toolbar callback contract.
    // Keep existing member signatures and DISPIDs stable; append new members.
    [ComVisible(true)]
    [Guid("74DE7382-4B9D-4D50-A028-84754928FD6A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ISlicerCallbacks
    {
        [DispId(1)] int CanExport();
        [DispId(2)] int AlwaysEnabled();
        [DispId(3)] void OpenInOrca();
        [DispId(4)] void OpenInBambu();
        [DispId(5)] void OpenInPrusa();
        [DispId(6)] void ShowSettings();
        [DispId(7)] void AddToOpenOrca();
        [DispId(8)] void AddToOpenBambu();
        [DispId(9)] void AddToOpenPrusa();
    }

    [ComVisible(true)]
    [Guid("D51D3347-A8E7-4892-A8BD-391203C2E8A4")]
    [ProgId("SolidWorksSlicerBridge.Addin")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(ISlicerCallbacks))]
    public class SwAddin : ISwAddin, ISlicerCallbacks
    {
        private const int CommandGroupId = 73191;
        private const string CommandTabName = "3D Print";
        private const string AddinGuid = "{D51D3347-A8E7-4892-A8BD-391203C2E8A4}";
        private const string BridgeVersion = "1.0.9";

        private ISldWorks swApp;
        private ICommandManager commandManager;
        private ICommandGroup commandGroup;
        private int addinCookie;
        private string startupStage = "ConnectToSW";
        private string runtimeLogPath;
        private bool refreshCommandTabs;
        private string uiRevision;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                InitializeRuntimeLog();
                WriteRuntimeLog("Starting " + BridgeVersion + "; cookie=" + Cookie);
                WriteRuntimeLog("Add-in: " + Assembly.GetExecutingAssembly().Location);
                WriteRuntimeLog("API: " + typeof(ISldWorks).Assembly.FullName);
                WriteRuntimeLog("API path: " + typeof(ISldWorks).Assembly.Location);
                WriteRuntimeLog("Runtime: " + Environment.Version + "; x64=" + Environment.Is64BitProcess);

                SetStartupStage("Cast SOLIDWORKS application to ISldWorks");
                swApp = (ISldWorks)ThisSW;
                addinCookie = Cookie;
                try { WriteRuntimeLog("SOLIDWORKS revision: " + swApp.RevisionNumber()); } catch { }

                // Release only the pointer acquired here, not SOLIDWORKS-owned RCWs.
                SetStartupStage("Verify callback IDispatch");
                IntPtr dispatch = Marshal.GetIDispatchForObject(this);
                try
                {
                    if (dispatch == IntPtr.Zero)
                        throw new InvalidOperationException("Callback IDispatch is unavailable.");
                }
                finally { if (dispatch != IntPtr.Zero) Marshal.Release(dispatch); }

                SetStartupStage("SetAddinCallbackInfo2");
                if (!swApp.SetAddinCallbackInfo2(0, this, addinCookie))
                    throw new InvalidOperationException("SOLIDWORKS rejected the callback object.");

                SetStartupStage("GetCommandManager");
                commandManager = swApp.GetCommandManager(addinCookie);
                if (commandManager == null)
                    throw new InvalidOperationException("SOLIDWORKS returned no CommandManager.");

                AddCommandManager();
                SetStartupStage("Connected");
                return true;
            }
            catch (Exception ex)
            {
                string failedStage = startupStage;
                WriteRuntimeLog("FAILED at " + failedStage + Environment.NewLine + ex.ToString());
                try { RemoveCommandManager(); } catch { }
                commandGroup = null;
                commandManager = null;
                swApp = null;
                MessageBox.Show("Не удалось загрузить SolidWorks Slicer Bridge " + BridgeVersion + ".\r\n\r\n" +
                    "Этап: " + failedStage + "\r\n" + ex.GetType().Name + " (0x" + ex.HResult.ToString("X8") + "): " + ex.Message +
                    "\r\n\r\nЛог: " + (runtimeLogPath ?? "недоступен"),
                    "SolidWorks Slicer Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void InitializeRuntimeLog()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SolidWorksSlicerBridge", "logs");
                Directory.CreateDirectory(folder);
                runtimeLogPath = Path.Combine(folder, "addin.log");
                if (File.Exists(runtimeLogPath) && new FileInfo(runtimeLogPath).Length > 2 * 1024 * 1024)
                {
                    File.Copy(runtimeLogPath, runtimeLogPath + ".previous", true);
                    File.WriteAllText(runtimeLogPath, String.Empty, Encoding.UTF8);
                }
            }
            catch { runtimeLogPath = null; }
        }

        private void WriteRuntimeLog(string text)
        {
            try
            {
                if (runtimeLogPath != null)
                    File.AppendAllText(runtimeLogPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                        " [" + Process.GetCurrentProcess().Id + "] " + text + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        private void SetStartupStage(string stage)
        {
            startupStage = stage;
            WriteRuntimeLog("STAGE: " + stage);
        }

        public bool DisconnectFromSW()
        {
            WriteRuntimeLog("DisconnectFromSW");
            try { RemoveCommandManager(); } catch { }
            commandGroup = null;
            commandManager = null;
            swApp = null;
            return true;
        }

        private static bool HasCurrentCommands(object previousIds)
        {
            Array ids = previousIds as Array;
            if (ids == null || ids.Length != 7) return false;
            try
            {
                for (int i = 0; i < 7; i++)
                    if (Convert.ToInt32(ids.GetValue(i)) != 1001 + i) return false;
                return true;
            }
            catch { return false; }
        }

        private void AddCommandManager()
        {
            SetStartupStage("GetGroupDataFromRegistry");
            int createErrors = 0;
            object previousIds = null;
            bool hasPrevious = commandManager.GetGroupDataFromRegistry(CommandGroupId, out previousIds);
            uiRevision = swApp.RevisionNumber();
            refreshCommandTabs = !hasPrevious || !HasCurrentCommands(previousIds) || ToolbarIcons.NeedsRefresh(uiRevision);

            SetStartupStage("CreateCommandGroup2");
            commandGroup = commandManager.CreateCommandGroup2(
                CommandGroupId,
                "3D Print Slicers",
                "Экспортировать активную модель и открыть или добавить в слайсер",
                "",
                -1,
                refreshCommandTabs,
                ref createErrors);

            if (commandGroup == null)
                throw new InvalidOperationException("SOLIDWORKS не создал CommandGroup. Код: " + createErrors);

            SetStartupStage("Extract embedded application icons");
            string iconDir = ToolbarIcons.Extract();
            WriteRuntimeLog("Embedded toolbar icons: " + iconDir);
            int[] sizes = new int[] { 20, 32, 40, 64, 96, 128 };
            string[] strips = new string[sizes.Length];
            string[] mains = new string[sizes.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                strips[i] = Path.Combine(iconDir, "toolbar_" + sizes[i] + ".png");
                mains[i] = Path.Combine(iconDir, "main_" + sizes[i] + ".png");
            }

            SetStartupStage("Set CommandGroup.IconList");
            commandGroup.IconList = strips;
            SetStartupStage("Set CommandGroup.MainIconList");
            commandGroup.MainIconList = mains;
            int menuAndToolbar = (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swMenuItem;

            SetStartupStage("AddCommandItem2: OrcaSlicer");
            int orcaIndex = commandGroup.AddCommandItem2(
                "OrcaSlicer", -1, "Экспорт 3MF и открыть в OrcaSlicer", "Open in OrcaSlicer", 0,
                "OpenInOrca", "CanExport", 1001, menuAndToolbar);
            SetStartupStage("AddCommandItem2: Bambu Studio");
            int bambuIndex = commandGroup.AddCommandItem2(
                "Bambu Studio", -1, "Экспорт 3MF и открыть в Bambu Studio", "Open in Bambu Studio", 1,
                "OpenInBambu", "CanExport", 1002, menuAndToolbar);
            SetStartupStage("AddCommandItem2: PrusaSlicer");
            int prusaIndex = commandGroup.AddCommandItem2(
                "PrusaSlicer", -1, "Экспорт 3MF и открыть в PrusaSlicer", "Open in PrusaSlicer", 2,
                "OpenInPrusa", "CanExport", 1003, menuAndToolbar);
            SetStartupStage("AddCommandItem2: Settings");
            int settingsIndex = commandGroup.AddCommandItem2(
                "Slicer Settings", -1, "Настроить пути к слайсерам", "Slicer Settings", 3,
                "ShowSettings", "AlwaysEnabled", 1004, menuAndToolbar);

            // Reuse the embedded original program icons. Existing command IDs stay intact.
            SetStartupStage("AddCommandItem2: append commands");
            int addOrcaIndex = commandGroup.AddCommandItem2(
                "Add to open OrcaSlicer", -1, "Добавить геометрию в открытое окно OrcaSlicer", "Add to open OrcaSlicer", 0,
                "AddToOpenOrca", "CanExport", 1005, menuAndToolbar);
            int addBambuIndex = commandGroup.AddCommandItem2(
                "Add to open Bambu Studio", -1, "Добавить геометрию в открытое окно Bambu Studio", "Add to open Bambu Studio", 1,
                "AddToOpenBambu", "CanExport", 1006, menuAndToolbar);
            int addPrusaIndex = commandGroup.AddCommandItem2(
                "Add to open PrusaSlicer", -1, "Добавить геометрию в открытое окно PrusaSlicer", "Add to open PrusaSlicer", 2,
                "AddToOpenPrusa", "CanExport", 1007, menuAndToolbar);

            if (orcaIndex < 0 || bambuIndex < 0 || prusaIndex < 0 || settingsIndex < 0 ||
                addOrcaIndex < 0 || addBambuIndex < 0 || addPrusaIndex < 0)
                throw new InvalidOperationException("SOLIDWORKS could not create all toolbar commands.");

            SetStartupStage("Activate CommandGroup");
            commandGroup.HasToolbar = true;
            commandGroup.HasMenu = true;
            if (!commandGroup.Activate())
                throw new InvalidOperationException("SOLIDWORKS could not activate the command group.");

            SetStartupStage("Read command IDs");
            int[] ids = new int[] {
                commandGroup.get_CommandID(orcaIndex),
                commandGroup.get_CommandID(bambuIndex),
                commandGroup.get_CommandID(prusaIndex),
                commandGroup.get_CommandID(addOrcaIndex),
                commandGroup.get_CommandID(addBambuIndex),
                commandGroup.get_CommandID(addPrusaIndex),
                commandGroup.get_CommandID(settingsIndex)
            };
            AddCommandTab((int)swDocumentTypes_e.swDocPART, ids);
            AddCommandTab((int)swDocumentTypes_e.swDocASSEMBLY, ids);
            try { ToolbarIcons.MarkCurrent(uiRevision); }
            catch (Exception ex) { WriteRuntimeLog("Could not record toolbar icon revision: " + ex.Message); }
        }

        private void AddCommandTab(int docType, int[] commandIds)
        {
            SetStartupStage("GetCommandTab: document type " + docType);
            // Preserve the API's CommandTab type: RemoveCommandTab does not accept ICommandTab.
            CommandTab tab = commandManager.GetCommandTab(docType, CommandTabName);
            if (tab != null && refreshCommandTabs)
            {
                SetStartupStage("Refresh 3D Print icons: document type " + docType);
                if (!commandManager.RemoveCommandTab(tab))
                    throw new InvalidOperationException("Could not refresh the 3D Print command tab.");
                tab = null;
            }
            if (tab != null) return;
            SetStartupStage("AddCommandTab: document type " + docType);
            tab = commandManager.AddCommandTab(docType, CommandTabName);
            if (tab == null) return;
            SetStartupStage("AddCommandTabBox: document type " + docType);
            CommandTabBox box = tab.AddCommandTabBox();
            int[] styles = new int[commandIds.Length];
            for (int i = 0; i < styles.Length; i++)
                styles[i] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;
            SetStartupStage("AddCommands: document type " + docType);
            box.AddCommands(commandIds, styles);
        }

        private void RemoveCommandManager()
        {
            if (commandManager == null || commandGroup == null) return;
            try { commandManager.RemoveCommandGroup2(CommandGroupId, true); } catch { }
        }

        public int CanExport()
        {
            try
            {
                IModelDoc2 model = swApp == null ? null : (IModelDoc2)swApp.ActiveDoc;
                if (model == null) return 0;
                int type = model.GetType();
                return (type == (int)swDocumentTypes_e.swDocPART || type == (int)swDocumentTypes_e.swDocASSEMBLY) ? 1 : 0;
            }
            catch { return 0; }
        }

        public int AlwaysEnabled() { return 1; }
        public void OpenInOrca() { ExportAndOpen(SlicerKind.Orca); }
        public void OpenInBambu() { ExportAndOpen(SlicerKind.Bambu); }
        public void OpenInPrusa() { ExportAndOpen(SlicerKind.Prusa); }
        public void AddToOpenOrca() { ExportAndOpen(SlicerKind.Orca, true); }
        public void AddToOpenBambu() { ExportAndOpen(SlicerKind.Bambu, true); }
        public void AddToOpenPrusa() { ExportAndOpen(SlicerKind.Prusa, true); }

        public void ShowSettings()
        {
            using (SettingsForm form = new SettingsForm()) form.ShowDialog();
        }

        private void ExportAndOpen(SlicerKind kind) { ExportAndOpen(kind, false); }

        private void ExportAndOpen(SlicerKind kind, bool append)
        {
            try
            {
                IModelDoc2 model = swApp == null ? null : (IModelDoc2)swApp.ActiveDoc;
                if (model == null)
                {
                    MessageBox.Show("Открой деталь или сборку.", "3D Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int docType = model.GetType();
                if (docType != (int)swDocumentTypes_e.swDocPART && docType != (int)swDocumentTypes_e.swDocASSEMBLY)
                {
                    MessageBox.Show("Экспорт поддерживается для деталей и сборок.", "3D Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (append)
                {
                    SlicerWindow target = ExistingSlicerWindows.Choose(kind);
                    if (target == null) return;
                    ExistingSlicerWindows.ValidateTarget(target);
                    string output = CreateOutputPath(model, ".stl");
                    AppendExport.Save(swApp, model, output);
                    ExistingSlicerWindows.SendModel(target, output, WriteRuntimeLog);
                }
                else
                {
                    string exe = SlicerPaths.Resolve(kind, true);
                    if (String.IsNullOrEmpty(exe)) return;
                    string output = CreateOutputPath(model, ".3mf");
                    Export3Mf(model, output);
                    LaunchSlicer(exe, output);
                }
                CleanupOldExports();
            }
            catch (Exception ex)
            {
                WriteRuntimeLog("Export failure: " + ex.ToString());
                MessageBox.Show("Ошибка экспорта/запуска:\r\n\r\n" + ex.Message,
                    "3D Print", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Export3Mf(IModelDoc2 model, string outputPath)
        {
            bool oldShowInfo = false;
            bool oldPreview = false;
            bool haveShowInfo = false;
            bool havePreview = false;
            try
            {
                oldShowInfo = swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.sw3MFShowInfoOnSave);
                haveShowInfo = true;
            }
            catch { }
            try
            {
                oldPreview = swApp.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview);
                havePreview = true;
            }
            catch { }
            try
            {
                model.ClearSelection2(true);
                try { swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.sw3MFShowInfoOnSave, false); } catch { }
                try { swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, false); } catch { }
                int errors = 0;
                int warnings = 0;
                bool ok = model.Extension.SaveAs3(
                    outputPath,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null,
                    null,
                    ref errors,
                    ref warnings);
                if (!ok || errors != 0 || !File.Exists(outputPath))
                    throw new InvalidOperationException("SOLIDWORKS не смог сохранить 3MF. Error=" + errors + ", Warning=" + warnings);
            }
            finally
            {
                if (haveShowInfo) { try { swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.sw3MFShowInfoOnSave, oldShowInfo); } catch { } }
                if (havePreview) { try { swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, oldPreview); } catch { } }
            }
        }

        private static string CreateOutputPath(IModelDoc2 model, string extension)
        {
            string folder = Path.Combine(Path.GetTempPath(), "SolidWorksSlicerBridge");
            Directory.CreateDirectory(folder);
            string title = model.GetTitle();
            int dot = title.LastIndexOf('.');
            if (dot > 0) title = title.Substring(0, dot);
            title = MakeSafeFileName(title);
            return Path.Combine(folder, title + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + extension);
        }

        private static string MakeSafeFileName(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "SolidWorksModel";
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
                sb.Append(Array.IndexOf(invalid, value[i]) >= 0 ? '_' : value[i]);
            return sb.ToString();
        }

        private static void LaunchSlicer(string exe, string modelPath)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exe;
            psi.Arguments = "\"" + modelPath + "\"";
            psi.WorkingDirectory = Path.GetDirectoryName(exe);
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        private static void CleanupOldExports()
        {
            try
            {
                string folder = Path.Combine(Path.GetTempPath(), "SolidWorksSlicerBridge");
                if (!Directory.Exists(folder)) return;
                DateTime cutoff = DateTime.Now.AddDays(-7);
                foreach (string pattern in new string[] { "*.3mf", "*.stl" })
                {
                    foreach (string file in Directory.GetFiles(folder, pattern))
                    {
                        try { if (File.GetLastWriteTime(file) < cutoff) File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        [ComRegisterFunction]
        public static void Register(Type t)
        {
            using (RegistryKey lm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key = lm.CreateSubKey(@"SOFTWARE\SolidWorks\Addins\" + AddinGuid))
            {
                key.SetValue(null, 0, RegistryValueKind.DWord);
                key.SetValue("Title", "SolidWorks Slicer Bridge", RegistryValueKind.String);
                key.SetValue("Description", "One-click export to OrcaSlicer, Bambu Studio and PrusaSlicer; add to an existing window", RegistryValueKind.String);
            }
            using (RegistryKey cu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (RegistryKey key = cu.CreateSubKey(@"Software\SolidWorks\AddInsStartup\" + AddinGuid))
                key.SetValue(null, 1, RegistryValueKind.DWord);
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                using (RegistryKey lm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    lm.DeleteSubKeyTree(@"SOFTWARE\SolidWorks\Addins\" + AddinGuid, false);
            }
            catch { }
            try
            {
                using (RegistryKey cu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
                    cu.DeleteSubKeyTree(@"Software\SolidWorks\AddInsStartup\" + AddinGuid, false);
            }
            catch { }
        }
    }

    internal enum SlicerKind { Orca, Bambu, Prusa }

    internal static class SlicerPaths
    {
        private const string RegistrySettings = @"Software\SolidWorksSlicerBridge";

        public static string Resolve(SlicerKind kind, bool allowPrompt)
        {
            string saved = ReadSaved(kind);
            if (File.Exists(saved)) return saved;
            string detected = AutoDetect(kind);
            if (!String.IsNullOrEmpty(detected))
            {
                Save(kind, detected);
                return detected;
            }
            if (!allowPrompt) return "";
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Укажи " + DisplayName(kind) + ".exe";
                dialog.Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Save(kind, dialog.FileName);
                    return dialog.FileName;
                }
            }
            return "";
        }

        public static string ReadSaved(SlicerKind kind)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistrySettings, false))
                {
                    if (key == null) return "";
                    object value = key.GetValue(ValueName(kind), "");
                    return value == null ? "" : value.ToString();
                }
            }
            catch { return ""; }
        }

        public static void Save(SlicerKind kind, string path)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistrySettings))
                key.SetValue(ValueName(kind), path == null ? "" : path, RegistryValueKind.String);
        }

        private static string AutoDetect(SlicerKind kind)
        {
            string exeName;
            string[] candidates;
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (kind == SlicerKind.Orca)
            {
                exeName = "orca-slicer.exe";
                candidates = new string[] {
                    Path.Combine(pf, @"OrcaSlicer\orca-slicer.exe"),
                    Path.Combine(pf, @"Orca Slicer\orca-slicer.exe"),
                    Path.Combine(local, @"Programs\OrcaSlicer\orca-slicer.exe"),
                    Path.Combine(local, @"Programs\Orca Slicer\orca-slicer.exe")
                };
            }
            else if (kind == SlicerKind.Bambu)
            {
                exeName = "bambu-studio.exe";
                candidates = new string[] {
                    Path.Combine(pf, @"Bambu Studio\bambu-studio.exe"),
                    Path.Combine(local, @"Programs\Bambu Studio\bambu-studio.exe"),
                    Path.Combine(local, @"BambuStudio\bambu-studio.exe")
                };
            }
            else
            {
                exeName = "prusa-slicer.exe";
                candidates = new string[] {
                    Path.Combine(pf, @"Prusa3D\PrusaSlicer\prusa-slicer.exe"),
                    Path.Combine(pf, @"PrusaSlicer\prusa-slicer.exe"),
                    Path.Combine(local, @"Programs\PrusaSlicer\prusa-slicer.exe")
                };
            }
            string fromRegistry = FindAppPath(exeName);
            if (File.Exists(fromRegistry)) return fromRegistry;
            for (int i = 0; i < candidates.Length; i++) if (File.Exists(candidates[i])) return candidates[i];
            return "";
        }

        private static string FindAppPath(string exeName)
        {
            string sub = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exeName;
            RegistryHive[] hives = new RegistryHive[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 };
            for (int h = 0; h < hives.Length; h++)
            {
                for (int v = 0; v < views.Length; v++)
                {
                    try
                    {
                        using (RegistryKey root = RegistryKey.OpenBaseKey(hives[h], views[v]))
                        using (RegistryKey key = root.OpenSubKey(sub, false))
                        {
                            if (key != null)
                            {
                                object value = key.GetValue(null);
                                if (value != null && File.Exists(value.ToString())) return value.ToString();
                            }
                        }
                    }
                    catch { }
                }
            }
            return "";
        }

        public static string DisplayName(SlicerKind kind)
        {
            if (kind == SlicerKind.Orca) return "OrcaSlicer";
            if (kind == SlicerKind.Bambu) return "Bambu Studio";
            return "PrusaSlicer";
        }

        private static string ValueName(SlicerKind kind)
        {
            if (kind == SlicerKind.Orca) return "OrcaPath";
            if (kind == SlicerKind.Bambu) return "BambuPath";
            return "PrusaPath";
        }
    }
}
