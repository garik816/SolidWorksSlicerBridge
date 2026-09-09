// Windows-only test support. These are deliberately limited API stubs, NOT
// vendor interop assemblies. This file is never packaged or used by the add-in.
// The real src/SwAddin.cs and SettingsForm.cs are compiled into this test.
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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
    public enum swUserPreferenceToggle_e { sw3MFShowInfoOnSave = 1, swSTLPreview = 2 }
    public enum swSaveAsVersion_e { swSaveAsCurrentVersion = 0 }
    public enum swSaveAsOptions_e { swSaveAsOptions_Silent = 1 }
}

namespace SolidWorks.Interop.sldworks
{
    // Deliberately reproduce the namespace collision in the vendor API.
    public class Environment { }
    public interface ISldWorks
    {
        bool SetAddinCallbackInfo2(long handle, object callbacks, int cookie);
        ICommandManager GetCommandManager(int cookie);
        object ActiveDoc { get; }
        string RevisionNumber();
        bool GetUserPreferenceToggle(int pref);
        bool SetUserPreferenceToggle(int pref, bool value);
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
        public ICommandGroup Group;
        public bool GetGroupDataFromRegistry(int id, out object data) { data = null; return false; }
        public ICommandGroup CreateCommandGroup2(int id, string title, string tooltip,
            string hint, int position, bool ignore, ref int errors)
        { errors = 0; Group = new ICommandGroup { Callbacks = Callbacks }; return Group; }
        public ICommandTab GetCommandTab(int type, string name) { return null; }
        public ICommandTab AddCommandTab(int type, string name) { TabCount++; return new ICommandTab(); }
        public bool RemoveCommandGroup2(int id, bool runtimeOnly) { return true; }
    }
    public class ICommandGroup
    {
        public object Callbacks;
        public int CommandCount;
        public object IconList { get; set; }
        public object MainIconList { get; set; }
        public bool HasToolbar { get; set; }
        public bool HasMenu { get; set; }
        public int AddCommandItem2(string name, int position, string hint, string tooltip,
            int image, string callback, string enable, int userId, int flags)
        {
            DispatchProbe.FindId(Callbacks, callback);
            DispatchProbe.FindId(Callbacks, enable);
            return CommandCount++;
        }
        public int get_CommandID(int index) { return 1000 + index; }
        public bool Activate() { return true; }
    }
    public class ICommandTab { public CommandTabBox AddCommandTabBox() { return new CommandTabBox(); } }
    public class CommandTabBox { public bool AddCommands(object ids, object styles) { return true; } }
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
    public bool SetAddinCallbackInfo2(long handle, object callbacks, int cookie)
    {
        // Same dispatch requirement as a native COM callback argument.
        if (DispatchProbe.InvokeInt(callbacks, "AlwaysEnabled") != 1) return false;
        Manager.Callbacks = callbacks;
        return true;
    }
    public SolidWorks.Interop.sldworks.ICommandManager GetCommandManager(int cookie) { return Manager; }
    public object ActiveDoc { get { return null; } }
    public string RevisionNumber() { return "TEST STUB - NOT SOLIDWORKS"; }
    public bool GetUserPreferenceToggle(int pref) { return false; }
    public bool SetUserPreferenceToggle(int pref, bool value) { return true; }
}

public static class CallbackInteropSmoke
{
    [STAThread]
    public static int Main()
    {
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
            string[] methods = { "CanExport", "AlwaysEnabled", "OpenInOrca", "OpenInBambu", "OpenInPrusa", "ShowSettings" };
            for (int i = 0; i < methods.Length; i++)
            {
                int id = DispatchProbe.FindId(addin, methods[i]);
                if (id != i + 1) throw new Exception("Unexpected DISPID for " + methods[i]);
                Console.WriteLine("PASS native GetIDsOfNames: " + methods[i] + " -> " + id);
            }
            if (DispatchProbe.InvokeInt(addin, "AlwaysEnabled") != 1) throw new Exception("AlwaysEnabled dispatch result is incorrect.");
            if (DispatchProbe.InvokeInt(addin, "CanExport") != 0) throw new Exception("Export should be disabled without a document.");
            Console.WriteLine("PASS native IDispatch.Invoke: both enable callbacks.");

            HostStub host = new HostStub();
            if (!addin.ConnectToSW(host, 123)) throw new Exception("ConnectToSW failed against the test host.");
            if (host.Manager.Group.CommandCount != 4 || host.Manager.TabCount != 2)
                throw new Exception("Expected four commands and two document tabs.");
            if (!addin.DisconnectFromSW()) throw new Exception("DisconnectFromSW failed.");
            Console.WriteLine("PASS production ConnectToSW and DisconnectFromSW with API stubs.");
            Console.WriteLine("NOTE: this verifies Windows COM callbacks, not operation inside SOLIDWORKS.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
}
