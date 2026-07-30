using System.Collections.Generic;
using Verse;

namespace EPrimeReadouts.UI
{
    internal static class UiText
    {
        // Cache contract:
        // Owner: process/current language.
        // Key: translation key string.
        // Value: immutable translated string.
        // Dependencies: UiVersion.Current, including active language.
        // Refresh policy: immediate clear on observed UI revision change.
        // Equality policy: cache hits preserve the string reference.
        // Teardown: Reset clears every translated string.
        private static readonly Dictionary<string, string> text =
            new Dictionary<string, string>();
        private static int uiVersion = -1;

        internal static string Get(string key)
        {
            UiVersion.ObserveCurrentMetrics();
            if (uiVersion != UiVersion.Current)
            {
                text.Clear();
                uiVersion = UiVersion.Current;
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
            uiVersion = -1;
        }
    }
}
