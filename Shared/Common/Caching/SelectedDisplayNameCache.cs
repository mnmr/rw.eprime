using System;

namespace RimShared.Common
{
    /// <summary>
    /// Caches the display name for the current selection. Revision-sensitive
    /// selections are resolved again only after their backing data changes.
    /// Cache contract: Owner = caller-supplied world/store identity; Key = owner
    /// plus canonical token; Value = immutable display string; Dependencies =
    /// pool revision when requested and presentation revision; Refresh policy =
    /// immediate; Equality policy = exact dependency reuse; Teardown = Reset.
    /// </summary>
    public sealed class SelectedDisplayNameCache
    {
        private object? cachedOwner;
        private string? cachedCanonical;
        private int cachedRevision;
        private int cachedPresentationRevision;
        private string? cachedValue;
        private bool hasValue;

        public string Get(
            object owner,
            string canonical,
            int revision,
            int presentationRevision,
            bool refreshOnRevisionChange,
            Func<string, string> resolve)
        {
            if (hasValue
                && ReferenceEquals(owner, cachedOwner)
                && string.Equals(canonical, cachedCanonical, StringComparison.Ordinal)
                && presentationRevision == cachedPresentationRevision
                && (!refreshOnRevisionChange || revision == cachedRevision))
                return cachedValue!; // hasValue guarantees a cached resolve result.

            cachedValue = resolve(canonical);
            cachedOwner = owner;
            cachedCanonical = canonical;
            cachedRevision = revision;
            cachedPresentationRevision = presentationRevision;
            hasValue = true;
            return cachedValue;
        }

        public void Reset()
        {
            cachedOwner = null;
            cachedCanonical = null;
            cachedValue = null;
            hasValue = false;
        }
    }
}
