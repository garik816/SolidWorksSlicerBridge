// Windows-only test support. Limited API stubs, not vendor interop assemblies.
// Never packaged. The actual add-in source and embedded artwork are tested here.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using DISPPARAMS = System.Runtime.InteropServices.ComTypes.DISPPARAMS;
using EXCEPINFO = System.Runtime.InteropServices.ComTypes.EXCEPINFO;
using SolidWorksSlicerBridge;

namespace SolidWorks.Interop.swpublished
{
    [ComVisible(true)]
    [Guid("2D8F77FD-897B-4A87-9B2B-BDB4E804C7D2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ISwAddin
    {
        bool ConnectToSW(object host, int cookie);
        bool DisconnectFromSW();
    }
}

namespace SolidWorks.Interop.swconst
{
    public enum swCommandItemType_e { swMenuItem = 1, swToolbarItem = 2 }
    public enum swDocumentTypes_e { swDocPART = 1, swDocASSEMBLY = 2 }
    public enum swCommandTabButtonTextDisplay_e { swCommandTabButton_TextHorizontal = 1 }
    public enum swUserPreferenceToggle_e {
        sw3MFShowInfoOnSave = 1, swSTLPreview = 2, swSTLBinaryFormat = 3,
        swSTLComponentsIntoOneFile = 4, swSTLShowInfoOnSave = 5, swSTLCheckForInterference = 6
    }
    public enum swUserPreferenceIntegerValue_e { swExportStlUnits = 1 }
    public enum swLengthUnit_e { swMM = 0 }
    public enum swSaveAsVersion_e { swSaveAsCurrentVersion = 0 }
    public enum swSaveAsOptions_e { swSaveAsOptions_Silent = 1 }
}

namespace SolidWorks.Interop.sldworks
{
    public class Environment { }
    public interface ISldWorks
    {
        bool SetAddinCallbackInfo2(long handle, object callbacks, int cookie);
        ICommandManager GetCommandManager(int cookie);
        object ActiveDoc { get; }
        string RevisionNumber();
        bool GetUserPreferenceToggle(int pref);
        // ISldWorks (system options) returns void; do not substitute the
        // bool signature of IModelDocExtension.SetUserPreferenceToggle.
        void SetUserPreferenceToggle(int pref, bool value);
        int GetUserPreferenceIntegerValue(int pref);
        bool SetUserPreferenceIntegerValue(int pref, int value);
    }
    public interface IModelDoc2
    {
        int GetType();
        string GetTitle();
        void ClearSelection2(bool all);
        IModelDocExtension Extension { get; }
    }
    public interface IModelDocExtension
    {
        bool SaveAs3(string file, int version, int options, object exportData,
            object advancedOptions, ref int errors, ref int warnings);
    }
    public class ICommandManager
    {
        public object Callbacks;
        public int TabCount;
        public int RemovedCount;
        public bool IgnorePrevious;
        public ICommandGroup Group;
        private readonly Dictionary<int, CommandTab> tabs = new Dictionary<int, CommandTab>();
        public ICommandManager() { tabs[1] = new CommandTab(); tabs[2] = new CommandTab(); }
        public bool GetGroupDataFromRegistry(int id, out object data)
        {
            data = Group == null ? new int[] { 1001, 1002, 1003, 1004 } : Group.UserIds.ToArray();
            return true;
        }
        public ICommandGroup CreateCommandGroup2(int id, string title, string tooltip,
            string hint, int position, bool ignore, ref int errors)
        { errors = 0; IgnorePrevious = ignore; Group = new ICommandGroup { Callbacks = Callbacks }; return Group; }
        // Match the vendor API signatures; ICommandTab would hide CS1503.
        public CommandTab GetCommandTab(int type, string name)
        { CommandTab tab; return tabs.TryGetValue(type, out tab) ? tab : null; }
        public CommandTab AddCommandTab(int type, string name)
        { TabCount++; CommandTab tab = new CommandTab(); tabs[type] = tab; return tab; }
        public bool RemoveCommandTab(CommandTab tab)
        {
            foreach (int type in new List<int>(tabs.Keys))
                if (Object.ReferenceEquals(tabs[type], tab)) { tabs.Remove(type); RemovedCount++; return true; }
            return false;
        }
        public bool RemoveCommandGroup2(int id, bool runtimeOnly) { return true; }
    }
    public class ICommandGroup
    {
        public object Callbacks;
        public int CommandCount;
        public readonly List<int> UserIds = new List<int>();
        public object IconList { get; set; }
        public object MainIconList { get; set; }
        public bool HasToolbar { get; set; }
        public bool HasMenu { get; set; }
        public int AddCommandItem2(string name, int position, string hint, string tooltip,
            int image, string callback, string enable, int userId, int flags)
        {
            DispatchProbe.FindId(Callbacks, callback);
            DispatchProbe.FindId(Callbacks, enable);
            int[] expectedImages = { 0, 1, 2, 3, 0, 1, 2 };
            if (image != expectedImages[CommandCount] || userId != 1001 + CommandCount)
                throw new Exception("Incorrect icon or stable command ID for " + name);
            UserIds.Add(userId);
            return CommandCount++;
        }
        public int get_CommandID(int index) { return 1000 + index; }
        public bool Activate() { return true; }
    }
    public interface ICommandTab { CommandTabBox AddCommandTabBox(); }
    public class CommandTab : ICommandTab
    {
        public CommandTabBox AddCommandTabBox() { return new CommandTabBox(); }
    }
    public class CommandTabBox
    {
        public bool AddCommands(object ids, object styles)
        {
            int[] expected = { 1000, 1001, 1002, 1004, 1005, 1006, 1003 };
            int[] actual = (int[])ids;
            if (actual.Length != expected.Length) throw new Exception("Missing commands in the tab.");
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i]) throw new Exception("Wrong command order in the tab.");
            return true;
        }
    }
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
public class LegacyCallbackShape : SolidWorks.Interop.swpublished.ISwAddin
{
    public bool ConnectToSW(object host, int cookie) { return true; }
    public bool DisconnectFromSW() { return true; }
    public int AlwaysEnabled() { return 1; }
}

public static class DispatchProbe
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetIds(IntPtr self, ref Guid iid, IntPtr names, uint count, uint locale, IntPtr ids);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int InvokeMethod(IntPtr self, int id, ref Guid iid, uint locale, ushort flags,
        ref DISPPARAMS arguments, [MarshalAs(UnmanagedType.Struct)] out object result,
        ref EXCEPINFO exception, out uint argumentError);

    public static int FindId(object instance, string name)
    {
        IntPtr dispatch = Marshal.GetIDispatchForObject(instance);
        IntPtr text = IntPtr.Zero, names = IntPtr.Zero, ids = IntPtr.Zero;
        try
        {
            text = Marshal.StringToCoTaskMemUni(name);
            names = Marshal.AllocHGlobal(IntPtr.Size);
            ids = Marshal.AllocHGlobal(4);
            Marshal.WriteIntPtr(names, text);
            IntPtr vtable = Marshal.ReadIntPtr(dispatch);
            GetIds getIds = (GetIds)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtable, 5 * IntPtr.Size), typeof(GetIds));
            Guid iid = Guid.Empty;
            Marshal.ThrowExceptionForHR(getIds(dispatch, ref iid, names, 1, 0, ids));
            return Marshal.ReadInt32(ids);
        }
        finally
        {
            if (text != IntPtr.Zero) Marshal.FreeCoTaskMem(text);
            if (names != IntPtr.Zero) Marshal.FreeHGlobal(names);
            if (ids != IntPtr.Zero) Marshal.FreeHGlobal(ids);
            Marshal.Release(dispatch);
        }
    }

    public static int InvokeInt(object instance, string name)
    {
        int id = FindId(instance, name);
        IntPtr dispatch = Marshal.GetIDispatchForObject(instance);
        try
        {
            IntPtr vtable = Marshal.ReadIntPtr(dispatch);
            InvokeMethod invoke = (InvokeMethod)Marshal.GetDelegateForFunctionPointer(
                Marshal.ReadIntPtr(vtable, 6 * IntPtr.Size), typeof(InvokeMethod));
            Guid iid = Guid.Empty;
            DISPPARAMS args = new DISPPARAMS();
            EXCEPINFO exception = new EXCEPINFO();
            object result;
            uint argumentError;
            Marshal.ThrowExceptionForHR(invoke(dispatch, id, ref iid, 0, 1,
                ref args, out result, ref exception, out argumentError));
            return Convert.ToInt32(result);
        }
        finally { Marshal.Release(dispatch); }
    }
}

public class HostStub : SolidWorks.Interop.sldworks.ISldWorks
{
    public readonly SolidWorks.Interop.sldworks.ICommandManager Manager = new SolidWorks.Interop.sldworks.ICommandManager();
    public readonly Dictionary<int, bool> Toggles = new Dictionary<int, bool>();
    public int Units = 3;
    public int RejectNextToggle;
    public int ThrowAfterSetToggle;
    private readonly string revision = "ICON-TEST-" + Guid.NewGuid().ToString("N");
    public bool SetAddinCallbackInfo2(long handle, object callbacks, int cookie)
    {
        if (DispatchProbe.InvokeInt(callbacks, "AlwaysEnabled") != 1) return false;
        Manager.Callbacks = callbacks;
        return true;
    }
    public SolidWorks.Interop.sldworks.ICommandManager GetCommandManager(int cookie) { return Manager; }
    public object ActiveDoc { get { return null; } }
    public string RevisionNumber() { return revision; }
    public bool GetUserPreferenceToggle(int pref) { bool value; return Toggles.TryGetValue(pref, out value) && value; }
    public void SetUserPreferenceToggle(int pref, bool value)
    {
        if (RejectNextToggle == pref) { RejectNextToggle = 0; return; }
        Toggles[pref] = value;
        if (ThrowAfterSetToggle == pref)
        {
            ThrowAfterSetToggle = 0;
            throw new InvalidOperationException("Injected exception after applying toggle " + pref);
        }
    }
    public int GetUserPreferenceIntegerValue(int pref) { return Units; }
    public bool SetUserPreferenceIntegerValue(int pref, int value) { Units = value; return true; }
}

public static class CallbackInteropSmoke
{
    private static void CheckIcons(object stripsObject, object mainsObject)
    {
        string[] strips = (string[])stripsObject;
        string[] mains = (string[])mainsObject;
        int[] sizes = { 20, 32, 40, 64, 96, 128 };
        if (strips.Length != 6 || mains.Length != 6) throw new Exception("Missing icon sizes.");
        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            using (Bitmap strip = new Bitmap(strips[i]))
            using (Bitmap main = new Bitmap(mains[i]))
            {
                if (strip.Width != size * 4 || strip.Height != size || main.Width != size || main.Height != size)
                    throw new Exception("Incorrect icon dimensions.");
                if (strip.GetPixel(0, 0).A != 0) throw new Exception("Icon background must be transparent.");
                HashSet<int> hashes = new HashSet<int>();
                int green = 0, orange = 0;
                for (int tile = 0; tile < 4; tile++)
                {
                    int hash = 17;
                    for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            Color c = strip.GetPixel(tile * size + x, y);
                            hash = unchecked(hash * 31 + c.ToArgb());
                            if (tile == 1 && c.A > 200 && c.G > 130 && c.R < 60 && c.B < 110) green++;
                            if (tile == 2 && c.A > 200 && c.R > 180 && c.G > 60 && c.G < 160 && c.B < 80) orange++;
                        }
                    hashes.Add(hash);
                }
                if (hashes.Count != 4 || green < size * size / 5 || orange < size * size / 10)
                    throw new Exception("Wrong application glyphs or icon order.");
            }
        }
        Console.WriteLine("PASS: twelve embedded PNGs, six sizes, transparent background and original app colors/order.");
    }

    [STAThread]
    public static int Main()
    {
        HostStub host = null;
        string iconCache = null;
        try
        {
            bool rejected = false;
            try { DispatchProbe.FindId(new LegacyCallbackShape(), "AlwaysEnabled"); }
            catch (InvalidCastException ex)
            {
                rejected = true;
                Console.WriteLine("PASS negative control: old callback shape throws " + ex.GetType().Name + ": " + ex.Message);
            }
            catch (COMException ex)
            {
                if (ex.ErrorCode != unchecked((int)0x80004002) && ex.ErrorCode != unchecked((int)0x80020006)) throw;
                rejected = true;
                Console.WriteLine("PASS negative control: old callback shape rejects IDispatch/name lookup, HRESULT=0x" + ex.ErrorCode.ToString("X8"));
            }
            if (!rejected) throw new Exception("Negative control did not reproduce inaccessible COM callbacks.");
            SwAddin addin = new SwAddin();
            string[] methods = { "CanExport", "AlwaysEnabled", "OpenInOrca", "OpenInBambu", "OpenInPrusa", "ShowSettings",
                "AddToOpenOrca", "AddToOpenBambu", "AddToOpenPrusa" };
            for (int i = 0; i < methods.Length; i++)
            {
                int id = DispatchProbe.FindId(addin, methods[i]);
                if (id != i + 1) throw new Exception("Unexpected DISPID for " + methods[i]);
                Console.WriteLine("PASS native GetIDsOfNames: " + methods[i] + " -> " + id);
            }
            if (DispatchProbe.InvokeInt(addin, "AlwaysEnabled") != 1) throw new Exception("AlwaysEnabled dispatch result is incorrect.");
            if (DispatchProbe.InvokeInt(addin, "CanExport") != 0) throw new Exception("Export should be disabled without a document.");
            Console.WriteLine("PASS native IDispatch.Invoke: both enable callbacks.");
            host = new HostStub();
            if (!addin.ConnectToSW(host, 123)) throw new Exception("ConnectToSW failed against the test host.");
            if (host.Manager.Group.CommandCount != 7 || host.Manager.TabCount != 2 || host.Manager.RemovedCount != 2 || !host.Manager.IgnorePrevious)
                throw new Exception("Expected seven commands and a one-time replacement of the two old tabs.");
            CheckIcons(host.Manager.Group.IconList, host.Manager.Group.MainIconList);
            string damaged = ((string[])host.Manager.Group.IconList)[0];
            iconCache = Path.GetDirectoryName(damaged);
            File.WriteAllText(damaged, "damaged cache test");
            ToolbarIcons.Extract();
            CheckIcons(host.Manager.Group.IconList, host.Manager.Group.MainIconList);
            Console.WriteLine("PASS: damaged icon cache repaired from embedded resources.");
            if (!addin.DisconnectFromSW()) throw new Exception("DisconnectFromSW failed.");
            if (!addin.ConnectToSW(host, 123)) throw new Exception("Second ConnectToSW failed.");
            if (host.Manager.TabCount != 2 || host.Manager.RemovedCount != 2 || host.Manager.IgnorePrevious)
                throw new Exception("Toolbar customization was reset again after migration.");
            if (!addin.DisconnectFromSW()) throw new Exception("Second DisconnectFromSW failed.");
            Console.WriteLine("PASS: seven commands, stable IDs, one-time migration and preservation of customized tabs.");
            Console.WriteLine("NOTE: API stubs validate Windows COM and embedded artwork, not a SOLIDWORKS host session.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
        finally
        {
            if (host != null) Registry.CurrentUser.DeleteSubKeyTree(@"Software\SolidWorksSlicerBridge\UI\" + host.RevisionNumber(), false);
            if (iconCache != null && Directory.Exists(iconCache)) Directory.Delete(iconCache, true);
        }
    }
}
