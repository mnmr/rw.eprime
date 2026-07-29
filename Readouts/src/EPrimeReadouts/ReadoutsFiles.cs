using System;
using System.Collections.Generic;
using System.IO;
using Verse;

namespace EPrimeReadouts
{
    /// File I/O helpers for Export/Import. Folder lives under RimWorld's save
    /// data directory so it survives game reinstalls and is easy to find.
    public static class ReadoutsFiles
    {
        private const string FolderName = "EPrimeReadouts";
        private const string Extension = ".xml";

        /// Absolute path to the export folder; created on first access.
        public static string Folder
        {
            get
            {
                string path = Path.Combine(GenFilePaths.SaveDataFolderPath, FolderName);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        /// Sanitizes <paramref name="name"/> (strips invalid filename chars) and
        /// returns the full path, appending ".xml" if the name lacks it.
        public static string PathFor(string name)
        {
            if (string.IsNullOrEmpty(name)) name = "readouts";

            // Strip invalid filename characters
            char[] invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
                if (Array.IndexOf(invalid, c) < 0)
                    sb.Append(c);
            name = sb.Length > 0 ? sb.ToString() : "readouts";

            if (!name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                name += Extension;

            return Path.Combine(Folder, name);
        }

        /// Lists all .xml files in the export folder, newest-modified first.
        public static List<(string name, string fullPath, DateTime modified)> ListFiles()
        {
            var result = new List<(string name, string fullPath, DateTime modified)>();
            string folder = Folder;
            if (!Directory.Exists(folder)) return result;

            var files = Directory.GetFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly);
            foreach (var fullPath in files)
            {
                string name = Path.GetFileNameWithoutExtension(fullPath);
                DateTime modified = File.GetLastWriteTime(fullPath);
                result.Add((name, fullPath, modified));
            }

            // Newest first
            result.Sort((a, b) => b.modified.CompareTo(a.modified));
            return result;
        }

        /// Reads the file at <paramref name="fullPath"/>. Returns true on success.
        public static bool TryRead(string fullPath, out string xml, out string error)
        {
            xml = null;
            error = null;
            try
            {
                xml = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// Writes <paramref name="xml"/> to <paramref name="fullPath"/>. Returns true on success.
        public static bool TryWrite(string fullPath, string xml, out string error)
        {
            error = null;
            try
            {
                File.WriteAllText(fullPath, xml, new System.Text.UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
