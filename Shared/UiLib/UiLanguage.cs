using System;
using System.Globalization;
using Verse;

namespace RimShared.UiLib
{
    /// Explicit presentation-only casing. Turkish distinguishes I/ı and İ/i;
    /// other supported languages retain the existing invariant behavior.
    /// Read this only in language-gated builders or while handling an edit.
    internal static class UiLanguage
    {
        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        internal static CultureInfo CaseCulture
        {
            get
            {
                LoadedLanguage? language = LanguageDatabase.activeLanguage;
                return string.Equals(language?.folderName, "Turkish", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(language?.LegacyFolderName, "Turkish", StringComparison.OrdinalIgnoreCase)
                    ? Turkish : CultureInfo.InvariantCulture;
            }
        }
    }
}
