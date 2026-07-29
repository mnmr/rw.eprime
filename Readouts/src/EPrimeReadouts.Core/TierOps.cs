using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Pure operations on a group's tier layout: an ordered list of up to
    /// MaxTiers ordered token lists. All mutating operations keep the layout
    /// compacted — no empty tiers, so tiers are always sequential.
    /// </summary>
    public static class TierOps
    {
        public const int MaxTiers = 3;
        public const int MaxSlotsPerTier = 8;

        public static List<List<string>> Clone(List<List<string>> tiers)
        {
            var copy = new List<List<string>>(tiers.Count);
            foreach (var tier in tiers) copy.Add(new List<string>(tier));
            return copy;
        }

        /// Returns true when any stored token's canonical form equals the
        /// canonical form of <paramref name="token"/> (flag-insensitive).
        public static bool Contains(List<List<string>> tiers, string token)
        {
            string canonical = SlotToken.Canonical(token);
            foreach (var tier in tiers)
                foreach (var stored in tier)
                    if (SlotToken.Canonical(stored) == canonical) return true;
            return false;
        }

        /// <summary>
        /// Adds token at (tier, slot). tier == tiers.Count appends a new
        /// tier when below MaxTiers; slot -1 or past-end appends within the
        /// tier. Refused when a token with the same canonical form is already
        /// anywhere in the layout.
        /// </summary>
        public static bool Add(List<List<string>> tiers, string token, int tier, int slot)
        {
            if (string.IsNullOrEmpty(token) || Contains(tiers, token)) return false;
            if (tier < 0 || tier > tiers.Count) return false;
            if (tier == tiers.Count)
            {
                if (tiers.Count >= MaxTiers) return false;
                tiers.Add(new List<string>());
            }
            var target = tiers[tier];
            if (target.Count >= MaxSlotsPerTier) return false;
            if (slot < 0 || slot > target.Count) slot = target.Count;
            target.Insert(slot, token);
            return true;
        }

        /// Removes the first token whose canonical form matches <paramref name="token"/>'s canonical.
        public static bool Remove(List<List<string>> tiers, string token)
        {
            string canonical = SlotToken.Canonical(token);
            foreach (var tier in tiers)
            {
                int idx = -1;
                for (int i = 0; i < tier.Count; i++)
                    if (SlotToken.Canonical(tier[i]) == canonical) { idx = i; break; }
                if (idx >= 0)
                {
                    tier.RemoveAt(idx);
                    Compact(tiers);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Moves the slot at (fromTier, fromSlot) to (toTier, toSlot).
        /// toTier == tiers.Count creates a new tier when below MaxTiers.
        /// </summary>
        public static bool Move(List<List<string>> tiers, int fromTier, int fromSlot, int toTier, int toSlot)
        {
            if (fromTier < 0 || fromTier >= tiers.Count) return false;
            if (fromSlot < 0 || fromSlot >= tiers[fromTier].Count) return false;
            if (toTier < 0 || toTier > tiers.Count) return false;
            if (toTier == tiers.Count && tiers.Count >= MaxTiers) return false;
            // Refuse cross-tier move into a full tier (same-tier reorders always allowed)
            if (toTier != fromTier && toTier < tiers.Count && tiers[toTier].Count >= MaxSlotsPerTier) return false;
            string token = tiers[fromTier][fromSlot];
            tiers[fromTier].RemoveAt(fromSlot);
            if (toTier == tiers.Count) tiers.Add(new List<string>());
            var target = tiers[toTier];
            if (fromTier == toTier && toSlot > fromSlot) toSlot--;
            if (toSlot < 0 || toSlot > target.Count) toSlot = target.Count;
            target.Insert(toSlot, token);
            Compact(tiers);
            return true;
        }

        /// Removes every token for which <paramref name="exists"/> returns
        /// false, then compacts. The predicate receives the raw token
        /// (including any '~' or '@' markers) so the game layer can decide
        /// validity from the full token.
        public static void Cleanup(List<List<string>> tiers, Func<string, bool> exists)
        {
            foreach (var tier in tiers) tier.RemoveAll(d => !exists(d));
            Compact(tiers);
        }

        public static void Compact(List<List<string>> tiers) =>
            tiers.RemoveAll(t => t.Count == 0);
    }
}
