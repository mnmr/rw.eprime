using System;

namespace RimShared.Common
{
    /// Vanilla-style mod requirements carried by exported XML elements, shared
    /// by the mods' import formats. The codecs read the attribute values and
    /// pass them in, so this stays free of XML types.
    internal static class MayRequireRules
    {
        /// <summary>
        /// Evaluates <c>MayRequire</c> (comma-separated packageIds, ALL must be
        /// active) and <c>MayRequireAnyOf</c> (comma-separated packageIds, ANY
        /// must be active). Ids are trimmed; a missing or empty attribute adds
        /// no requirement. A null predicate keeps everything.
        /// </summary>
        public static bool Satisfied(string? mayRequire, string? mayRequireAnyOf,
            Func<string, bool>? isModActive)
        {
            if (isModActive == null) return true;

            if (!string.IsNullOrEmpty(mayRequire))
            {
                foreach (var id in mayRequire!.Split(','))
                    if (!isModActive(id.Trim()))
                        return false;
            }

            if (!string.IsNullOrEmpty(mayRequireAnyOf))
            {
                bool anyActive = false;
                foreach (var id in mayRequireAnyOf!.Split(','))
                {
                    if (isModActive(id.Trim()))
                    {
                        anyActive = true;
                        break;
                    }
                }
                if (!anyActive) return false;
            }

            return true;
        }
    }
}
