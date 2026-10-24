using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>Folder candidates for RimWorld's selected language.</summary>
    public static class LanguageFolders
    {
        public static string[] Candidates(string? selected, string? legacy = null)
        {
            var result = new List<string>(3);
            Add(result, string.IsNullOrWhiteSpace(selected) ? "English" : selected!);
            Add(result, legacy);
            // RimWorld's legacy alias is the part preceding the native name
            // in parentheses. Preserve regional names such as SpanishLatin
            // and PortugueseBrazilian: they are independent translations.
            string folder = result[0];
            int nativeName = folder.IndexOf('(');
            if (nativeName > 0) Add(result, folder.Substring(0, nativeName).Trim());
            return result.ToArray();
        }

        private static void Add(List<string> result, string? folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return;
            for (int i = 0; i < result.Count; i++)
                if (string.Equals(result[i], folder, StringComparison.OrdinalIgnoreCase)) return;
            result.Add(folder!);
        }
    }
}
