// Disposable Windows fixtures, NOT real slicers or vendor API DLLs.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using SolidWorksSlicerBridge;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using Environment = System.Environment;
[assembly: AssemblyFileVersion("2.9.1.0")]
[assembly: AssemblyInformationalVersion("2.9.1-test-fixture")]

public static class ExistingWindowSmoke
{
    [StructLayout(LayoutKind.Sequential)]
    private struct CopyData { public UIntPtr Id; public int ByteCount; public IntPtr Data; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size, Style;
        public IntPtr Procedure;
        public int ClassExtra, WindowExtra;
        public IntPtr Instance, Icon, Cursor, Background;
        public string Menu, Name;
        public IntPtr SmallIcon;
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate IntPtr WndProc(IntPtr window, uint message, IntPtr wparam, IntPtr lparam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClassEx(ref WindowClass value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint extended, string className, string title, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr window, uint message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool SetProp(IntPtr window, string name, IntPtr value);
    [DllImport("user32.dll")] private static extern bool EnableWindow(IntPtr window, bool enable);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);
    private static readonly WndProc ReceiverProcedure = Receive;
    private static string receiverDirectory;
    private static string receiverMode;

    private static IntPtr Receive(IntPtr window, uint message, IntPtr wp, IntPtr lp)
    {
        if (message != 0x004A) return DefWindowProc(window, message, wp, lp);
        CopyData data = (CopyData)Marshal.PtrToStructure(lp, typeof(CopyData));
        if (data.Id.ToUInt64() != 1 || data.ByteCount < 2 || data.ByteCount % 2 != 0 ||
            Marshal.ReadInt16(data.Data, data.ByteCount - 2) != 0)
        {
            File.WriteAllText(Path.Combine(receiverDirectory, "error"), "Invalid COPYDATA layout or UTF16 terminator");
            return IntPtr.Zero;
        }
        if (receiverMode == "slow") Thread.Sleep(1500);
        string text = Marshal.PtrToStringUni(data.Data, data.ByteCount / 2 - 1);
        File.WriteAllText(Path.Combine(receiverDirectory, "message"), text, Encoding.UTF8);
        return receiverMode == "zero" ? IntPtr.Zero : new IntPtr(1);
    }

    private static int RunReceiver(string directory, string mode)
    {
        receiverDirectory = directory; receiverMode = mode;
        WindowClass wc = new WindowClass { Size = (uint)Marshal.SizeOf(typeof(WindowClass)),
            Instance = GetModuleHandle(null), Name = "wxWindowNR",
            Procedure = Marshal.GetFunctionPointerForDelegate(ReceiverProcedure) };
        if (RegisterClassEx(ref wc) == 0) throw new Exception("Fixture window registration failed.");
        IntPtr window = CreateWindowEx(0, wc.Name, "Slicer IPC TEST FIXTURE", 0x10CF0000,
            10, 10, 240, 100, IntPtr.Zero, IntPtr.Zero, wc.Instance, IntPtr.Zero);
        if (window == IntPtr.Zero) throw new Exception("Fixture window creation failed.");
        SetProp(window, "Instance_Hash_Minor", new IntPtr(54321));
        SetProp(window, "Instance_Hash_Major", new IntPtr(12345));
        if (mode == "disabled") EnableWindow(window, false);
        File.WriteAllText(Path.Combine(directory, "ready"), window.ToInt64().ToString());
        Application.Run();
        return 0;
    }

    private sealed class Receiver : IDisposable
    {
        public Process Process;
        public SlicerWindow Target;
        public string DirectoryPath;
        public Receiver(string root, string exeName, SlicerKind kind, string mode)
        {
            DirectoryPath = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            string executable = Path.Combine(DirectoryPath, exeName);
            File.Copy(Assembly.GetExecutingAssembly().Location, executable);
            ProcessStartInfo start = new ProcessStartInfo(executable,
                "--receiver \"" + DirectoryPath + "\" " + mode);
            start.UseShellExecute = false; start.CreateNoWindow = true;
            Process = Process.Start(start);
            try
            {
                string ready = Path.Combine(DirectoryPath, "ready");
                Stopwatch wait = Stopwatch.StartNew();
                while (!File.Exists(ready) && wait.ElapsedMilliseconds < 10000 && !Process.HasExited) Thread.Sleep(25);
                if (!File.Exists(ready)) throw new Exception("Fixture receiver did not become ready.");
                long handle = 0;
                while (!Int64.TryParse(File.ReadAllText(ready), out handle) && wait.ElapsedMilliseconds < 10000) Thread.Sleep(10);
                if (handle == 0) throw new Exception("Invalid fixture HWND.");
                Target = new SlicerWindow { Handle = new IntPtr(handle), Kind = kind, ExecutablePath = executable,
                    ProcessId = Process.Id, StartTimeUtcTicks = Process.StartTime.ToUniversalTime().Ticks, Title = "Fixture" };
            }
            catch { Dispose(); throw; }
        }
        public string ReadMessage() { return File.ReadAllText(Path.Combine(DirectoryPath, "message"), Encoding.UTF8); }
        public bool HasMessage { get { return File.Exists(Path.Combine(DirectoryPath, "message")); } }
        public void Dispose()
        {
            if (Process != null)
            {
                if (!Process.HasExited) { Process.Kill(); Process.WaitForExit(5000); }
                Process.Dispose(); Process = null;
            }
        }
    }

    private static void Assert(bool condition, string text) { if (!condition) throw new Exception(text); }
    private static void MustFail(Action action, string reason)
    {
        bool failed = false;
        try { action(); } catch (InvalidOperationException) { failed = true; }
        Assert(failed, reason);
    }

    // Independent parser for the quoted argument subset used by production.
    private static List<string> DecodeArguments(string text)
    {
        List<string> result = new List<string>();
        int i = 0;
        while (i < text.Length)
        {
            Assert(text[i++] == '"', "Argument must be quoted.");
            StringBuilder value = new StringBuilder();
            bool closed = false;
            while (i < text.Length)
            {
                char c = text[i++];
                if (c == '"') { closed = true; break; }
                if (c == '\\')
                {
                    Assert(i < text.Length, "Incomplete escape.");
                    c = text[i++];
                    if (c == 'r') c = '\r'; else if (c == 'n') c = '\n';
                }
                value.Append(c);
            }
            Assert(closed, "Unclosed argument.");
            result.Add(value.ToString());
            if (i < text.Length) Assert(text[i++] == ';', "Invalid argument separator.");
        }
        return result;
    }

    private static void CheckMessage(SlicerKind kind, string message, string exe, string model, bool modern)
    {
        if (modern)
        {
            Dictionary<string, object> json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(message);
            Assert((string)json["type"] == "CLI", "Wrong JSON message type.");
            message = (string)json["data"];
        }
        List<string> args = DecodeArguments(message);
        Assert(args.Count == 2 && args[0] == exe && args[1] == model, "IPC path round-trip failed: " + kind);
    }

    private sealed class ModelStub : IModelDoc2, IModelDocExtension
    {
        private readonly HostStub host;
        public bool Fail;
        public bool Throw;
        public bool SelectionCleared;
        public int SaveCalls;
        public Action AfterSave;
        public ModelStub(HostStub app) { host = app; }
        public new int GetType() { return (int)swDocumentTypes_e.swDocPART; }
        public string GetTitle() { return "test.SLDPRT"; }
        public void ClearSelection2(bool all) { SelectionCleared = all; }
        public IModelDocExtension Extension { get { return this; } }
        public bool SaveAs3(string file, int version, int options, object data, object advanced, ref int errors, ref int warnings)
        {
            SaveCalls++;
            Assert(host.Units == (int)swLengthUnit_e.swMM, "STL units are not mm.");
            Assert(host.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLBinaryFormat), "Binary STL not selected.");
            Assert(host.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile), "Assembly must export to one file.");
            Assert(!host.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLShowInfoOnSave) &&
                !host.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLPreview) &&
                !host.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSTLCheckForInterference), "Unexpected export dialog preference.");
            Assert(SelectionCleared, "Selection was not cleared.");
            if (Throw) throw new InvalidOperationException("Injected export exception.");
            if (Fail) { errors = 2; return false; }
            using (BinaryWriter writer = new BinaryWriter(File.Create(file)))
            { writer.Write(new byte[80]); writer.Write((uint)1); writer.Write(new byte[50]); }
            if (AfterSave != null) AfterSave();
            return true;
        }
    }

    private static void CheckExport(string root)
    {
        HostStub app = new HostStub();
        for (int pref = 1; pref <= 6; pref++) app.Toggles[pref] = (pref % 2 == 0);
        ModelStub model = new ModelStub(app);
        string path = Path.Combine(root, "export.stl");
        int binary = (int)swUserPreferenceToggle_e.swSTLBinaryFormat;
        Action checkRestored = delegate
        {
            Assert(app.Units == 3, "Original STL units not restored.");
            for (int pref = 1; pref <= 6; pref++) Assert(app.Toggles[pref] == (pref % 2 == 0), "STL toggle not restored.");
        };
        AppendExport.Save(app, model, path); checkRestored();
        AppendExport.ValidateBinaryStl(path);
        model.Fail = true;
        MustFail(delegate { AppendExport.Save(app, model, path); }, "Failed export was accepted."); checkRestored();
        model.Fail = false; model.Throw = true;
        MustFail(delegate { AppendExport.Save(app, model, path); }, "Export exception was ignored."); checkRestored();
        model.Throw = false;

        // The void setter can silently ignore a requested change. Binary starts
        // false and must become true: read-back must stop export on a mismatch.
        int before = model.SaveCalls;
        app.RejectNextToggle = binary;
        MustFail(delegate { AppendExport.Save(app, model, path); }, "Ignored toggle change was not detected by read-back.");
        Assert(model.SaveCalls == before, "Export ran after a rejected toggle change.");
        checkRestored();
        app.ThrowAfterSetToggle = binary;
        MustFail(delegate { AppendExport.Save(app, model, path); }, "Setter exception after partial mutation was ignored.");
        Assert(model.SaveCalls == before, "Export ran after a setter exception.");
        checkRestored();
        Console.WriteLine("PASS: void setter read-back rejects ignored changes; partial mutations are restored before export.");

        // A no-op is valid when the saved value already equals the request.
        app.RejectNextToggle = (int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile;
        AppendExport.Save(app, model, path); checkRestored();
        Console.WriteLine("PASS: an already-correct toggle value does not cause a false failure.");

        // Fail specifically during restoration after a successful file write.
        // Save must fail rather than let the caller dispatch the new STL.
        model.AfterSave = delegate { app.RejectNextToggle = binary; };
        MustFail(delegate { AppendExport.Save(app, model, path); }, "Ignored restoration was accepted.");
        Assert(app.Toggles[binary], "The restoration mismatch fixture did not remain changed.");
        app.Toggles[binary] = false;
        checkRestored(); // All the other settings were still restored.
        model.AfterSave = delegate { app.ThrowAfterSetToggle = binary; };
        MustFail(delegate { AppendExport.Save(app, model, path); }, "Restoration exception was ignored.");
        checkRestored();
        model.AfterSave = null;
        Console.WriteLine("PASS: restoration mismatches and exceptions block dispatch without skipping other settings.");
        Console.WriteLine("PASS: STL mm/binary/whole model; preferences restored after success, error, exception and rejected setting.");
    }

    [STAThread]
    public static int Main(string[] arguments)
    {
        if (arguments.Length == 3 && arguments[0] == "--receiver") return RunReceiver(arguments[1], arguments[2]);
        string root = Path.Combine(Path.GetTempPath(), "SWSB IPC tests " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string model = Path.Combine(root, "\u043c\u043e\u0434\u0435\u043b\u044c; box with space.stl");
            File.WriteAllText(model, "fixture");
            string exampleExe = @"C:\Program Files\Prusa3D\prusa-slicer.exe";
            CheckMessage(SlicerKind.Prusa, ExistingSlicerWindows.BuildMessage(SlicerKind.Prusa, exampleExe, model, new Version(2, 9, 0)), exampleExe, model, false);
            CheckMessage(SlicerKind.Prusa, ExistingSlicerWindows.BuildMessage(SlicerKind.Prusa, exampleExe, model, new Version(2, 9, 1)), exampleExe, model, true);
            MustFail(delegate { ExistingSlicerWindows.BuildMessage(SlicerKind.Prusa, exampleExe, model, null); }, "Unknown Prusa protocol was guessed.");
            Assert(!ExistingSlicerWindows.MatchesExecutable(SlicerKind.Orca, @"C:\Other\notepad.exe", ""), "Unrelated executable matched.");
            Console.WriteLine("PASS: legacy / Prusa 2.9.1 JSON protocol boundary, Unicode, spaces and semicolon path escaping.");

            SlicerKind[] kinds = { SlicerKind.Orca, SlicerKind.Bambu, SlicerKind.Prusa };
            string[] names = { "orca-slicer.exe", "bambu-studio.exe", "prusa-slicer.exe" };
            for (int i = 0; i < kinds.Length; i++)
            {
                using (Receiver receiver = new Receiver(root, names[i], kinds[i], "normal"))
                {
                    List<SlicerWindow> windows = ExistingSlicerWindows.Find(kinds[i]);
                    Assert(windows.Exists(w => w.Handle == receiver.Target.Handle && w.ProcessId == receiver.Target.ProcessId), "Main window discovery missed " + kinds[i]);
                    ExistingSlicerWindows.SendModel(receiver.Target, model, null);
                    CheckMessage(kinds[i], receiver.ReadMessage(), receiver.Target.ExecutablePath, model, kinds[i] == SlicerKind.Prusa);
                    Console.WriteLine("PASS: " + kinds[i] + " discovery and real cross-process WM_COPYDATA delivery to fixture.");
                }
            }
            using (Receiver first = new Receiver(root, "orca-slicer.exe", SlicerKind.Orca, "zero"))
            using (Receiver second = new Receiver(root, "orca-slicer.exe", SlicerKind.Orca, "normal"))
            {
                Assert(ExistingSlicerWindows.Find(SlicerKind.Orca).Count >= 2, "Multiple windows were not enumerated.");
                ExistingSlicerWindows.SendModel(first.Target, model, null);
                Assert(first.HasMessage && !second.HasMessage, "Message was broadcast or delivered to the wrong window.");
                int oldPid = first.Target.ProcessId;
                first.Target.ProcessId = second.Target.ProcessId;
                MustFail(delegate { ExistingSlicerWindows.ValidateTarget(first.Target); }, "PID mismatch was accepted.");
                first.Target.ProcessId = oldPid;
                Console.WriteLine("PASS: selected window only, zero WndProc result accepted, stale PID rejected.");
            }
            using (Receiver disabled = new Receiver(root, "bambu-studio.exe", SlicerKind.Bambu, "disabled"))
            {
                MustFail(delegate { ExistingSlicerWindows.SendModel(disabled.Target, model, null); }, "Disabled main window was accepted.");
                Assert(!disabled.HasMessage, "Disabled window received an import.");
            }
            using (Receiver slow = new Receiver(root, "orca-slicer.exe", SlicerKind.Orca, "slow"))
            {
                Stopwatch elapsed = Stopwatch.StartNew();
                MustFail(delegate { ExistingSlicerWindows.SendPayload(slow.Target, "fixture", 100); }, "Timeout was not reported.");
                Assert(elapsed.ElapsedMilliseconds < 2500, "Message send was not bounded.");
            }
            using (Receiver unrelated = new Receiver(root, "NotASlicer.exe", SlicerKind.Orca, "normal"))
                Assert(!ExistingSlicerWindows.Find(SlicerKind.Orca).Exists(w => w.ProcessId == unrelated.Target.ProcessId), "Unrelated wx application was mistaken for a slicer.");
            Console.WriteLine("PASS: modal window rejection, bounded timeout, no unrelated recipients.");
            CheckExport(root);
            Console.WriteLine("NOTE: these are real Windows IPC fixtures and simulated CAD APIs, NOT real SOLIDWORKS/slicer end-to-end tests.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
        finally { Directory.Delete(root, true); }
    }
}
