using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SolidWorksSlicerBridge
{
    [ComVisible(false)]
    internal static class AppendExport
    {
        // An STL carries geometry, not printer settings or a replacement project.
        // Keep the user's tessellation quality; restore every temporary preference.
        public static void Save(ISldWorks app, IModelDoc2 model, string path)
        {
            int unitsPreference = (int)swUserPreferenceIntegerValue_e.swExportStlUnits;
            int oldUnits = app.GetUserPreferenceIntegerValue(unitsPreference);
            int[] toggles = {
                (int)swUserPreferenceToggle_e.swSTLBinaryFormat,
                (int)swUserPreferenceToggle_e.swSTLComponentsIntoOneFile,
                (int)swUserPreferenceToggle_e.swSTLShowInfoOnSave,
                (int)swUserPreferenceToggle_e.swSTLPreview,
                (int)swUserPreferenceToggle_e.swSTLCheckForInterference
            };
            bool[] requested = { true, true, false, false, false };
            bool[] saved = new bool[toggles.Length];
            for (int i = 0; i < toggles.Length; i++) saved[i] = app.GetUserPreferenceToggle(toggles[i]);
            Exception failure = null;
            List<string> restoreFailures = new List<string>();
            try
            {
                if (!app.SetUserPreferenceIntegerValue(unitsPreference, (int)swLengthUnit_e.swMM))
                    throw new InvalidOperationException("SOLIDWORKS could not set STL units to millimeters.");
                for (int i = 0; i < toggles.Length; i++)
                    if (!app.SetUserPreferenceToggle(toggles[i], requested[i]))
                        throw new InvalidOperationException("SOLIDWORKS rejected STL preference " + toggles[i]);
                model.ClearSelection2(true);
                int errors = 0, warnings = 0;
                bool ok = model.Extension.SaveAs3(path,
                    (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                    (int)swSaveAsOptions_e.swSaveAsOptions_Silent,
                    null, null, ref errors, ref warnings);
                if (!ok || errors != 0 || !File.Exists(path))
                    throw new InvalidOperationException("STL export failed. Error=" + errors + "; Warning=" + warnings);
                ValidateBinaryStl(path);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                try
                {
                    if (!app.SetUserPreferenceIntegerValue(unitsPreference, oldUnits)) restoreFailures.Add("units");
                }
                catch { restoreFailures.Add("units"); }
                for (int i = 0; i < toggles.Length; i++)
                {
                    try
                    {
                        if (!app.SetUserPreferenceToggle(toggles[i], saved[i])) restoreFailures.Add(toggles[i].ToString());
                    }
                    catch { restoreFailures.Add(toggles[i].ToString()); }
                }
            }
            if (failure != null)
                throw new InvalidOperationException("Не удалось подготовить STL для добавления: " + failure.Message +
                    (restoreFailures.Count == 0 ? String.Empty : "\r\nНе восстановлены настройки STL: " + String.Join(", ", restoreFailures)), failure);
            if (restoreFailures.Count != 0)
                throw new InvalidOperationException("Модель экспортирована, но не отправлена: не восстановлены настройки STL: " + String.Join(", ", restoreFailures));
        }

        internal static void ValidateBinaryStl(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if (stream.Length < 84) throw new InvalidDataException("STL export is empty or incomplete.");
                stream.Position = 80;
                uint triangles = reader.ReadUInt32();
                if (triangles == 0 || stream.Length != 84L + 50L * triangles)
                    throw new InvalidDataException("SOLIDWORKS did not produce a complete binary STL.");
            }
        }
    }
}
