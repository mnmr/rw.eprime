using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    public static class IdentitySelectionPreserver
    {
        public static Dictionary<TIdentity, bool> Capture<TItem, TIdentity>(
            IReadOnlyList<TItem> prior,
            Func<TItem, TIdentity> identityOf,
            Func<TItem, bool> selectedOf,
            IEqualityComparer<TIdentity> comparer = null)
        {
            var selections = new Dictionary<TIdentity, bool>(
                comparer ?? EqualityComparer<TIdentity>.Default);
            if (prior != null)
                for (int i = 0; i < prior.Count; i++)
                {
                    TItem item = prior[i];
                    TIdentity identity = identityOf(item);
                    if (identity is not null)
                        selections[identity] = selectedOf(item);
                }
            return selections;
        }

        public static int Restore<TItem, TIdentity>(
            IReadOnlyDictionary<TIdentity, bool> selections,
            IReadOnlyList<TItem> refreshed,
            Func<TItem, TIdentity> identityOf,
            Func<TItem, bool> selectedOf,
            Action<TItem, bool> setSelected)
        {
            if (selections == null)
                throw new ArgumentNullException(nameof(selections));

            int selected = 0;
            if (refreshed == null) return selected;
            for (int i = 0; i < refreshed.Count; i++)
            {
                TItem item = refreshed[i];
                TIdentity identity = identityOf(item);
                if (identity is not null && selections.TryGetValue(identity, out bool wasSelected))
                    setSelected(item, wasSelected);
                if (selectedOf(item)) selected++;
            }
            return selected;
        }

        public static int Restore<TItem, TIdentity>(
            IReadOnlyList<TItem> prior,
            IReadOnlyList<TItem> refreshed,
            Func<TItem, TIdentity> identityOf,
            Func<TItem, bool> selectedOf,
            Action<TItem, bool> setSelected,
            IEqualityComparer<TIdentity> comparer = null)
        {
            Dictionary<TIdentity, bool> selections = Capture(
                prior, identityOf, selectedOf, comparer);
            return Restore(selections, refreshed, identityOf, selectedOf, setSelected);
        }
    }
}
