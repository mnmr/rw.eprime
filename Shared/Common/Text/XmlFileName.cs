using System;

namespace RimShared.Common
{
    /// Export file naming shared by the mods' file pickers: exported files
    /// always end in ".xml" so pickers that list "*.xml" can find them.
    public static class XmlFileName
    {
        public const string Extension = ".xml";

        public static string WithExtension(string name) =>
            name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
                ? name
                : name + Extension;
    }
}
