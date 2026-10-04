using System;
using System.IO;
using System.Linq;
using System.Text;

namespace RimShared.Common
{
    /// Why a picker's typed name and directory don't compose into a usable
    /// path. Each mod translates it with its own keys.
    public enum ExportPathProblem { None, BadFileName, BadDirectory }

    /// Path rules shared by the mods' export/import file pickers: what may be
    /// typed into the file name and directory fields, and how the two compose
    /// into one destination.
    public static class ExportPathRules
    {
        // Characters the file system rejects can't be typed at all. A file name
        // additionally never holds separators or a drive colon: Windows' invalid
        // set includes them but Unix's doesn't, so they're explicit.
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '\\', '/', ':' }).Distinct().ToArray();
        private static readonly char[] InvalidDirChars = Path.GetInvalidFileNameChars()
            .Where(c => c != '\\' && c != '/' && c != ':').ToArray();

        /// Typed file-name text without the characters a name can't hold.
        /// Clean text is returned as is (no allocation).
        public static string StripFileName(string text) => Strip(text, InvalidNameChars);

        /// Typed directory text without the characters a path can't hold;
        /// separators and drive colons stay.
        public static string StripDirectory(string text) => Strip(text, InvalidDirChars);

        private static string Strip(string text, char[] invalid)
        {
            if (text.IndexOfAny(invalid) < 0) return text;
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
                if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>
        /// Full destination for <paramref name="fileName"/> (trimmed) in
        /// <paramref name="directory"/>, or null with the reason. The name
        /// gains ".xml" unless it already ends with it or
        /// <paramref name="addExtension"/> is false, so export and import
        /// resolve the same typed name to the same file. The result uses the
        /// platform's directory separator throughout (game paths arrive with
        /// '/', Path.Combine joins with the native one: never mix them).
        /// </summary>
        public static string? Compose(string directory, string fileName, bool addExtension,
            out ExportPathProblem problem)
        {
            problem = ExportPathProblem.None;
            string name = fileName.Trim();
            if (name.Length == 0 || name.IndexOfAny(InvalidNameChars) >= 0)
            {
                problem = ExportPathProblem.BadFileName;
                return null;
            }
            if (directory.Length == 0 || directory.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                problem = ExportPathProblem.BadDirectory;
                return null;
            }
            try
            {
                return Path.Combine(directory, addExtension ? XmlFileName.WithExtension(name) : name)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            }
            catch (Exception)
            {
                problem = ExportPathProblem.BadDirectory;
                return null;
            }
        }
    }
}
