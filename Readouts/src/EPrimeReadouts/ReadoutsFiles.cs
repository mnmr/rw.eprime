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

        /// Lists all .xml files in <paramref name="directory"/>, newest-modified
        /// first. Bad or inaccessible paths (the picker allows custom input)
        /// yield an empty list rather than throwing.
        public sealed class Entry
        {
            internal Entry(string name, string fullPath, DateTime modified)
            {
                Name = name;
                FullPath = fullPath;
                Modified = modified;
                ModifiedText = modified.ToString("yyyy-MM-dd HH:mm");
            }

            public string Name { get; }
            public string FullPath { get; }
            public DateTime Modified { get; }
            public string ModifiedText { get; }
        }

        public static List<Entry> ListFiles(string directory)
        {
            var result = new List<Entry>();
            try
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    return result;

                var files = Directory.GetFiles(directory, "*" + Extension, SearchOption.TopDirectoryOnly);
                foreach (var fullPath in files)
                {
                    string name = Path.GetFileNameWithoutExtension(fullPath);
                    DateTime modified = File.GetLastWriteTime(fullPath);
                    result.Add(new Entry(name, fullPath, modified));
                }

                // Newest first
                result.Sort((a, b) => b.Modified.CompareTo(a.Modified));
            }
            catch (Exception)
            {
                result.Clear();
            }
            return result;
        }

        /// Reads the file at <paramref name="fullPath"/>. Returns true on success.
        public static bool TryRead(string fullPath, out string? xml, out string? error)
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
        public static bool TryWrite(string fullPath, string xml, out string? error)
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
