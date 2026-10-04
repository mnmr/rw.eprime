using System.Collections.Generic;
using RimShared.UiLib;
using Verse;

namespace EPrimeReadouts.UI
{
    internal static class UiText
    {
        // Cache contract:
        // Owner: process/current language.
        // Key: translation key string.
        // Value: immutable translated string.
        // Dependencies: UiRevision.LanguageCurrent.
        // Refresh policy: immediate clear on observed language revision change.
        // Equality policy: cache hits preserve the string reference.
        // Teardown: Reset clears every translated string.
        private static readonly Dictionary<string, string> text =
            new Dictionary<string, string>();
        private static int languageVersion = -1;

        internal static string Get(string key)
        {
            UiRevision.ObserveCurrentMetrics();
            if (languageVersion != UiRevision.LanguageCurrent)
            {
                text.Clear();
                languageVersion = UiRevision.LanguageCurrent;
            }
            if (!text.TryGetValue(key, out string value))
            {
                value = key.Translate().ToString();
                text.Add(key, value);
            }
            return value;
        }

        internal static void Reset()
        {
            text.Clear();
            languageVersion = -1;
        }
    }
}
