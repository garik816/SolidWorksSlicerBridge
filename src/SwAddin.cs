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

namespace SolidWorksSlicerBridge
{
    [ComVisible(true)]
    [Guid("D51D3347-A8E7-4892-A8BD-391203C2E8A4")]
    [ProgId("SolidWorksSlicerBridge.Addin")]
    [ClassInterface(ClassInterfaceType.None)]
    public class SwAddin : ISwAddin
    {
        private const int CommandGroupId = 73191;
        private const string CommandTabName = "3D Print";
        private const string AddinGuid = "{D51D3347-A8E7-4892-A8BD-391203C2E8A4}";

        private ISldWorks swApp;
        private ICommandManager commandManager;
        private ICommandGroup commandGroup;
        private int addinCookie;

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                swApp = (ISldWorks)ThisSW;
                addinCookie = Cookie;
                swApp.SetAddinCallbackInfo2(0, this, addinCookie);
                commandManager = swApp.GetCommandManager(addinCookie);
                AddCommandManager();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить SolidWorks Slicer Bridge.\r\n\r\n" + ex.Message,
                    "SolidWorks Slicer Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try { RemoveCommandManager(); } catch { }
            commandGroup = null;
            commandManager = null;
            swApp = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        private void AddCommandManager()
        {
            int createErrors = 0;
            object previousIds = null;
            bool hasPrevious = commandManager.GetGroupDataFromRegistry(CommandGroupId, out previousIds);

            commandGroup = commandManager.CreateCommandGroup2(
                CommandGroupId,
                "3D Print Slicers",
                "Экспортировать активную модель в 3MF и открыть в слайсере",
                "",
                -1,
                !hasPrevious,
                ref createErrors);

            if (commandGroup == null)
                throw new InvalidOperationException("SOLIDWORKS не создал CommandGroup. Код: " + createErrors);

            string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string iconDir = Path.Combine(baseDir, "Icons");
            int[] sizes = new int[] { 20, 32, 40, 64, 96, 128 };
            string[] strips = new string[sizes.Length];
            string[] mains = new string[sizes.Length];

            for (int i = 0; i < sizes.Length; i++)
            {
                strips[i] = Path.Combine(iconDir, "toolbar_" + sizes[i] + ".png");
                mains[i] = Path.Combine(iconDir, "main_" + sizes[i] + ".png");
            }

            commandGroup.IconList = strips;
            commandGroup.MainIconList = mains;

            int menuAndToolbar = (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem;

            int orcaIndex = commandGroup.AddCommandItem2(
                "OrcaSlicer", -1, "Экспорт 3MF и открыть в OrcaSlicer", "Open in OrcaSlicer", 0,
                "OpenInOrca", "CanExport", 1001, menuAndToolbar);

            int bambuIndex = commandGroup.AddCommandItem2(
                "Bambu Studio", -1, "Экспорт 3MF и открыть в Bambu Studio", "Open in Bambu Studio", 1,
                "OpenInBambu", "CanExport", 1002, menuAndToolbar);

            int prusaIndex = commandGroup.AddCommandItem2(
                "PrusaSlicer", -1, "Экспорт 3MF и открыть в PrusaSlicer", "Open in PrusaSlicer", 2,
                "OpenInPrusa", "CanExport", 1003, menuAndToolbar);

            int settingsIndex = commandGroup.AddCommandItem2(
                "Slicer Settings", -1, "Настроить пути к слайсерам", "Slicer Settings", 3,
                "ShowSettings", "AlwaysEnabled", 1004, menuAndToolbar);

            commandGroup.HasToolbar = true;
            commandGroup.HasMenu = true;
            commandGroup.Activate();

            int[] ids = new int[] {
                commandGroup.get_CommandID(orcaIndex),
                commandGroup.get_CommandID(bambuIndex),
                commandGroup.get_CommandID(prusaIndex),
                commandGroup.get_CommandID(settingsIndex)
            };

            AddCommandTab((int)swDocumentTypes_e.swDocPART, ids);
            AddCommandTab((int)swDocumentTypes_e.swDocASSEMBLY, ids);
        }

        private void AddCommandTab(int docType, int[] commandIds)
        {
            ICommandTab tab = commandManager.GetCommandTab(docType, CommandTabName);
            if (tab != null) return;

            tab = commandManager.AddCommandTab(docType, CommandTabName);
            if (tab == null) return;

            CommandTabBox box = tab.AddCommandTabBox();
            int[] styles = new int[commandIds.Length];
            for (int i = 0; i < styles.Length; i++)
                styles[i] = (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal;

            box.AddCommands(commandIds, styles);
        }

        private void RemoveCommandManager()
        {
            if (commandManager == null) return;
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

        public void ShowSettings()
        {
            using (SettingsForm form = new SettingsForm()) form.ShowDialog();
        }

        private void ExportAndOpen(SlicerKind kind)
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

                string exe = SlicerPaths.Resolve(kind, true);
                if (String.IsNullOrEmpty(exe)) return;

                string output = CreateOutputPath(model);
                Export3Mf(model, output);
                LaunchSlicer(exe, output);
                CleanupOldExports();
            }
            catch (Exception ex)
            {
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
                    out errors,
                    out warnings);

                if (!ok || errors != 0 || !File.Exists(outputPath))
                    throw new InvalidOperationException("SOLIDWORKS не смог сохранить 3MF. Error=" + errors + ", Warning=" + warnings);
            }
            finally
            {
                if (haveShowInfo) { try { swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.sw3MFShowInfoOnSave, oldShowInfo); } catch { } }
                if (havePreview) { try { swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview, oldPreview); } catch { } }
            }
        }

        private static string CreateOutputPath(IModelDoc2 model)
        {
            string folder = Path.Combine(Path.GetTempPath(), "SolidWorksSlicerBridge");
            Directory.CreateDirectory(folder);
            string title = model.GetTitle();
            int dot = title.LastIndexOf('.');
            if (dot > 0) title = title.Substring(0, dot);
            title = MakeSafeFileName(title);
            return Path.Combine(folder, title + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".3mf");
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
                string[] files = Directory.GetFiles(folder, "*.3mf");
                DateTime cutoff = DateTime.Now.AddDays(-7);
                for (int i = 0; i < files.Length; i++)
                {
                    try { if (File.GetLastWriteTime(files[i]) < cutoff) File.Delete(files[i]); } catch { }
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
                key.SetValue("Description", "One-click 3MF export to OrcaSlicer, Bambu Studio and PrusaSlicer", RegistryValueKind.String);
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
