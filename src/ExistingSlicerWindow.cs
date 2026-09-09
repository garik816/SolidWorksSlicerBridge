using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SolidWorksSlicerBridge
{
    [ComVisible(false)]
    internal sealed class SlicerWindow
    {
        public IntPtr Handle;
        public int ProcessId;
        public long StartTimeUtcTicks;
        public string ExecutablePath;
        public string Title;
        public SlicerKind Kind;
        public override string ToString() { return Title + "  [PID " + ProcessId + "]"; }
    }

    // Uses the slicers' existing single-instance message handlers. It does not
    // launch a process, send keystrokes, change preferences or touch the clipboard.
    [ComVisible(false)]
    internal static class ExistingSlicerWindows
    {
        private const uint WmCopyData = 0x004A;
        private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);
        [StructLayout(LayoutKind.Sequential)]
        private struct CopyData { public UIntPtr Id; public int ByteCount; public IntPtr Data; }

        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProp(IntPtr window, string name);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("kernel32.dll", EntryPoint = "SetLastError")] private static extern void ClearLastError(uint error);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeoutW(IntPtr window, uint message, IntPtr sender,
            ref CopyData data, uint flags, uint timeout, out UIntPtr result);

        internal static bool MatchesExecutable(SlicerKind kind, string path, string configuredPath)
        {
            string expected = kind == SlicerKind.Orca ? "orca-slicer.exe" :
                kind == SlicerKind.Bambu ? "bambu-studio.exe" : "prusa-slicer.exe";
            return String.Equals(Path.GetFileName(path), expected, StringComparison.OrdinalIgnoreCase) ||
                (!String.IsNullOrWhiteSpace(configuredPath) &&
                 String.Equals(Path.GetFullPath(path), Path.GetFullPath(configuredPath), StringComparison.OrdinalIgnoreCase));
        }

        public static List<SlicerWindow> Find(SlicerKind kind)
        {
            List<SlicerWindow> found = new List<SlicerWindow>();
            string configured = SlicerPaths.ReadSaved(kind);
            int session;
            using (Process current = Process.GetCurrentProcess()) session = current.SessionId;
            EnumWindows(delegate(IntPtr window, IntPtr ignored)
            {
                try
                {
                    if (!IsWindowVisible(window)) return true;
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(window, className, className.Capacity);
                    if (className.ToString() != "wxWindowNR") return true;
                    // These properties distinguish an initialized slicer main window
                    // from splash screens, settings dialogs and unrelated wx programs.
                    if (GetProp(window, "Instance_Hash_Minor") == IntPtr.Zero &&
                        GetProp(window, "Instance_Hash_Major") == IntPtr.Zero) return true;
                    uint pid;
                    GetWindowThreadProcessId(window, out pid);
                    using (Process process = Process.GetProcessById(checked((int)pid)))
                    {
                        if (process.SessionId != session) return true;
                        string exe = process.MainModule.FileName;
                        if (!MatchesExecutable(kind, exe, configured)) return true;
                        StringBuilder title = new StringBuilder(2048);
                        GetWindowText(window, title, title.Capacity);
                        found.Add(new SlicerWindow { Handle = window, ProcessId = process.Id,
                            StartTimeUtcTicks = process.StartTime.ToUniversalTime().Ticks,
                            ExecutablePath = exe, Title = title.ToString(), Kind = kind });
                    }
                }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
                // A process may exit while Windows enumerates its windows.
                return true;
            }, IntPtr.Zero);
            return found;
        }

        public static SlicerWindow Choose(SlicerKind kind)
        {
            List<SlicerWindow> windows = Find(kind);
            if (windows.Count == 0)
            {
                MessageBox.Show("Сначала открой " + SlicerPaths.DisplayName(kind) + ".\r\n\r\n" +
                    "Подходящее открытое окно не найдено. Новый экземпляр не запускается.\r\n" +
                    "Запускай SOLIDWORKS и слайсер с одинаковыми правами доступа.",
                    "Добавить в открытое окно", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            if (windows.Count == 1) return windows[0];
            using (Form form = new Form())
            using (ListBox list = new ListBox())
            using (Button ok = new Button())
            using (Button cancel = new Button())
            {
                form.Text = "Добавить модель — " + SlicerPaths.DisplayName(kind);
                form.ClientSize = new Size(680, 270);
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false; form.MaximizeBox = false;
                list.SetBounds(12, 12, 656, 195);
                list.HorizontalScrollbar = true;
                foreach (SlicerWindow window in windows) list.Items.Add(window);
                list.SelectedIndex = 0;
                ok.Text = "Добавить"; ok.SetBounds(448, 223, 105, 30); ok.DialogResult = DialogResult.OK;
                cancel.Text = "Отмена"; cancel.SetBounds(563, 223, 105, 30); cancel.DialogResult = DialogResult.Cancel;
                form.Controls.AddRange(new Control[] { list, ok, cancel });
                form.AcceptButton = ok; form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? list.SelectedItem as SlicerWindow : null;
            }
        }

        internal static Version ReadPrusaVersion(string exe)
        {
            FileVersionInfo info = FileVersionInfo.GetVersionInfo(exe);
            Match match = Regex.Match(info.ProductVersion ?? String.Empty, @"(?<!\d)(\d+)\.(\d+)\.(\d+)");
            if (match.Success)
                return new Version(Int32.Parse(match.Groups[1].Value), Int32.Parse(match.Groups[2].Value), Int32.Parse(match.Groups[3].Value));
            if (info.FileMajorPart > 0)
                return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
            throw new InvalidOperationException("Не удалось определить версию PrusaSlicer. Модель не отправлена.");
        }

        // The receiver skips argument zero. Always quote each C-style argument,
        // including semicolons in valid Windows filenames, then JSON-escape the
        // complete list for PrusaSlicer's protocol introduced in 2.9.1.
        internal static string BuildMessage(SlicerKind kind, string exe, string model, Version prusaVersion)
        {
            if (String.IsNullOrWhiteSpace(exe) || String.IsNullOrWhiteSpace(model) || exe.IndexOf('\0') >= 0 || model.IndexOf('\0') >= 0)
                throw new ArgumentException("Invalid IPC file path.");
            string arguments = QuoteCStyle(exe) + ";" + QuoteCStyle(model);
            if (kind != SlicerKind.Prusa) return arguments;
            if (prusaVersion == null || prusaVersion.Major < 2)
                throw new InvalidOperationException("Unsupported or unknown PrusaSlicer version.");
            return prusaVersion.CompareTo(new Version(2, 9, 1)) >= 0
                ? "{\"type\":\"CLI\",\"data\":" + QuoteJson(arguments) + "}" : arguments;
        }

        private static string QuoteCStyle(string value)
        {
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }

        private static string QuoteJson(string value)
        {
            StringBuilder output = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '\\' || c == '"') output.Append('\\').Append(c);
                else if (c < 32) output.Append("\\u").Append(((int)c).ToString("x4"));
                else output.Append(c);
            }
            return output.Append('"').ToString();
        }

        internal static void ValidateTarget(SlicerWindow target)
        {
            uint pid;
            if (target == null || !IsWindow(target.Handle)) throw new InvalidOperationException("Окно слайсера уже закрыто.");
            GetWindowThreadProcessId(target.Handle, out pid);
            if (pid != target.ProcessId) throw new InvalidOperationException("Окно слайсера изменилось. Модель не отправлена.");
            using (Process process = Process.GetProcessById(target.ProcessId))
            {
                if (process.StartTime.ToUniversalTime().Ticks != target.StartTimeUtcTicks ||
                    !String.Equals(process.MainModule.FileName, target.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Экземпляр слайсера изменился. Модель не отправлена.");
            }
            if (!IsWindowEnabled(target.Handle))
                throw new InvalidOperationException("Закрой диалоговое окно в слайсере и повтори добавление модели.");
        }

        public static void SendModel(SlicerWindow target, string model, Action<string> log)
        {
            ValidateTarget(target);
            if (!String.Equals(Path.GetExtension(model), ".stl", StringComparison.OrdinalIgnoreCase) || !File.Exists(model))
                throw new InvalidOperationException("Для добавления требуется существующий STL-файл с геометрией.");
            Version version = target.Kind == SlicerKind.Prusa ? ReadPrusaVersion(target.ExecutablePath) : null;
            string message = BuildMessage(target.Kind, target.ExecutablePath, Path.GetFullPath(model), version);
            if (log != null) log("Append target: " + target.Title + "; PID=" + target.ProcessId + "; exe=" + target.ExecutablePath +
                "; protocol=" + (version == null ? "legacy CLI" : version.ToString()) + "; file=" + model);
            SendPayload(target, message, 5000);
            // Successful dispatch is not a synchronous acknowledgment of model import.
            if (log != null) log("WM_COPYDATA dispatched. Import is performed asynchronously by the slicer.");
            if (IsIconic(target.Handle)) ShowWindowAsync(target.Handle, 9);
            SetForegroundWindow(target.Handle);
        }

        internal static void SendPayload(SlicerWindow target, string message, uint timeoutMilliseconds)
        {
            ValidateTarget(target);
            if (message == null || message.Length > 65535 || message.IndexOf('\0') >= 0)
                throw new ArgumentException("Invalid IPC message.");
            IntPtr buffer = Marshal.StringToHGlobalUni(message);
            try
            {
                CopyData data = new CopyData { Id = new UIntPtr(1), ByteCount = checked((message.Length + 1) * 2), Data = buffer };
                UIntPtr result;
                ClearLastError(0);
                IntPtr sent = SendMessageTimeoutW(target.Handle, WmCopyData, IntPtr.Zero, ref data,
                    0x0001 | 0x0002 | 0x0020, timeoutMilliseconds, out result);
                if (sent == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error == 5)
                        throw new InvalidOperationException("Windows запретила передачу. Запусти SOLIDWORKS и слайсер с одинаковыми правами доступа.");
                    throw new InvalidOperationException("Слайсер не подтвердил получение сообщения (Win32=" + error + ").\r\n" +
                        "Перед повторной попыткой проверь окно: модель могла уже попасть в очередь импорта. Автоматического повтора нет.");
                }
                // wxWidgets handlers may return zero even after consuming the message.
                // The SendMessageTimeout return value, not the WndProc result, indicates dispatch.
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }
}
