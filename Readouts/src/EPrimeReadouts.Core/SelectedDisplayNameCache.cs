using System;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Caches the display name for the current selection. Revision-sensitive
    /// selections are resolved again only after their backing data changes.
    /// </summary>
    public sealed class SelectedDisplayNameCache
    {
        private string cachedCanonical;
        private int cachedRevision;
        private string cachedValue;
        private bool hasValue;

        public string Get(
            string canonical,
            int revision,
            bool refreshOnRevisionChange,
            Func<string, string> resolve)
        {
            if (hasValue
                && string.Equals(canonical, cachedCanonical, StringComparison.Ordinal)
                && (!refreshOnRevisionChange || revision == cachedRevision))
                return cachedValue;

            cachedValue = resolve(canonical);
            cachedCanonical = canonical;
            cachedRevision = revision;
            hasValue = true;
            return cachedValue;
        }
    }
}
