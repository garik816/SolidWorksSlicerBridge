using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace SolidWorksSlicerBridge
{
    [ComVisible(false)]
    internal static class ToolbarIcons
    {
        private const string Revision = "official-icons-1";
        private const string ResourcePrefix = "SolidWorksSlicerBridge.Icons.";
        private static readonly int[] Sizes = { 20, 32, 40, 64, 96, 128 };

        // SOLIDWORKS accepts file paths, not streams. Keep the originals inside
        // the DLL and materialize them in our own cache, never in SOLIDWORKS folders.
        public static string Extract()
        {
            Assembly assembly = typeof(ToolbarIcons).Assembly;
            string directory = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "SolidWorksSlicerBridge", "icons", Revision, assembly.ManifestModule.ModuleVersionId.ToString("N"));
            Directory.CreateDirectory(directory);
            foreach (int size in Sizes)
            {
                WriteImage(assembly, directory, "toolbar_" + size + ".png", size * 4, size);
                WriteImage(assembly, directory, "main_" + size + ".png", size, size);
            }
            return directory;
        }

        private static void WriteImage(Assembly assembly, string directory, string name, int width, int height)
        {
            byte[] bytes;
            using (Stream resource = assembly.GetManifestResourceStream(ResourcePrefix + name))
            {
                if (resource == null) throw new InvalidDataException("Missing embedded toolbar image: " + name);
                using (MemoryStream buffer = new MemoryStream())
                {
                    resource.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }
            }
            using (MemoryStream buffer = new MemoryStream(bytes))
            using (Bitmap bitmap = new Bitmap(buffer))
            {
                if (bitmap.Width != width || bitmap.Height != height)
                    throw new InvalidDataException("Invalid embedded toolbar image dimensions: " + name);
            }
            string target = Path.Combine(directory, name);
            if (File.Exists(target) && Equal(File.ReadAllBytes(target), bytes)) return;
            string temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(target)) File.Delete(target);
                File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static string SettingsKey(string solidWorksRevision)
        {
            string major = String.IsNullOrEmpty(solidWorksRevision) ? "unknown" : solidWorksRevision.Split('.')[0];
            return @"Software\SolidWorksSlicerBridge\UI\" + major;
        }

        public static bool NeedsRefresh(string solidWorksRevision)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SettingsKey(solidWorksRevision)))
                    return key == null || !String.Equals(key.GetValue("IconRevision") as string, Revision, StringComparison.Ordinal);
            }
            catch { return true; }
        }

        public static void MarkCurrent(string solidWorksRevision)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsKey(solidWorksRevision)))
                key.SetValue("IconRevision", Revision, RegistryValueKind.String);
        }
    }
}
