using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RimShared.Common;
using Verse;

namespace RimShared.UiLib
{
    /// File I/O for a mod's Export/Import dialogs. Each mod keeps one instance
    /// for its folder under RimWorld's save data directory (beside Saves and
    /// Config), so exports survive game reinstalls and are easy to find.
    internal sealed class ExportFolder
    {
        private readonly string folderName;

        // Cache contract:
        // Owner: process (one instance per mod).
        // Key: the folder name given at construction.
        // Value: immutable absolute path string.
        // Dependencies: GenFilePaths.SaveDataFolderPath, fixed for the session.
        // Refresh policy: none; resolved on first read.
        // Equality policy: every read returns the same string.
        // Teardown: none needed (one process-lifetime string).
        private string? folder;

        internal ExportFolder(string folderName)
        {
            this.folderName = folderName;
        }

        /// Absolute path to the mod's export folder. Pure: never touches the
        /// filesystem (tooltips read it while drawing); TryWrite creates the
        /// folder on the first save.
        public string Folder =>
            folder ??= Path.Combine(GenFilePaths.SaveDataFolderPath, folderName);

        /// One listed export file with its preformatted modified time.
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

        /// Lists all .xml files in <paramref name="directory"/>, newest-modified
        /// first. Missing, bad or inaccessible paths (the picker allows custom
        /// input) yield an empty list rather than throwing.
        public static List<Entry> ListFiles(string directory)
        {
            var result = new List<Entry>();
            try
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    return result;

                var files = Directory.GetFiles(directory, "*" + XmlFileName.Extension,
                    SearchOption.TopDirectoryOnly);
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
                xml = File.ReadAllText(fullPath, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// Writes <paramref name="xml"/> to <paramref name="fullPath"/> as UTF-8
        /// without a BOM, creating its directory first. Returns true on success.
        public static bool TryWrite(string fullPath, string xml, out string? error)
        {
            error = null;
            try
            {
                string? dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(fullPath, xml, new UTF8Encoding(false));
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
