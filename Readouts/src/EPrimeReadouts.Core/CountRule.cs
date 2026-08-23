using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// One per-option override state: follow the player's global count option,
    /// or force the option on/off for every slot showing the token.
    public enum BasisOverride : byte
    {
        Inherit = 0,
        ForceOn = 1,
        ForceOff = 2,
    }

    /// Shared per-token override of the storage-only / hide-forbidden count
    /// basis. Keyed by canonical slot token (defName or "#poolId") so every
    /// slot showing the token displays the same number. Stored in the
    /// authoritative model beside thresholds; a fully-inherit rule is the
    /// absent state and is never stored.
    public readonly struct CountRule : IEquatable<CountRule>
    {
        public readonly BasisOverride StorageOnly;
        public readonly BasisOverride HideForbidden;

        public CountRule(BasisOverride storageOnly, BasisOverride hideForbidden)
        {
            StorageOnly = storageOnly;
            HideForbidden = hideForbidden;
        }

        public bool IsInherit =>
            StorageOnly == BasisOverride.Inherit
            && HideForbidden == BasisOverride.Inherit;

        public bool ResolveStorageOnly(bool globalStorageOnly)
            => StorageOnly == BasisOverride.Inherit
                ? globalStorageOnly
                : StorageOnly == BasisOverride.ForceOn;

        public bool ResolveHideForbidden(bool globalHideForbidden)
            => HideForbidden == BasisOverride.Inherit
                ? globalHideForbidden
                : HideForbidden == BasisOverride.ForceOn;

        public bool Equals(CountRule other)
            => StorageOnly == other.StorageOnly
               && HideForbidden == other.HideForbidden;

        public override bool Equals(object obj)
            => obj is CountRule other && Equals(other);

        public override int GetHashCode()
            => (int)StorageOnly | ((int)HideForbidden << 2);
    }

    /// Compact persistence codec for CountRule values ("0".."8"). Malformed
    /// input is rejected so a bad save entry is skipped instead of applied.
    public static class CountRuleCodec
    {
        public static string Encode(CountRule rule)
            => ((int)rule.StorageOnly * 3 + (int)rule.HideForbidden)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool TryDecode(string? text, out CountRule rule)
        {
            rule = default;
            if (string.IsNullOrEmpty(text)) return false;
            if (!int.TryParse(text,
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out int packed))
                return false;
            if (packed < 0 || packed > 8) return false;
            rule = new CountRule(
                (BasisOverride)(packed / 3), (BasisOverride)(packed % 3));
            return true;
        }
    }

    /// Union-of-needs over every stored rule: what the count snapshot pass
    /// must collect beyond what the global options alone would require.
    public static class CountRuleUnion
    {
        /// True when any rule forces map-wide counting (storage-only off) for
        /// its token, so the scattered pass must run even when the global
        /// option is storage-only.
        public static bool AnyForcesScattered(Dictionary<string, CountRule> rules)
        {
            foreach (var pair in rules)
                if (pair.Value.StorageOnly == BasisOverride.ForceOff)
                    return true;
            return false;
        }

        /// True when any rule forces hide-forbidden on for its token, so
        /// forbidden flags must be inspected even when the global option
        /// shows forbidden items.
        public static bool AnyForcesForbidden(Dictionary<string, CountRule> rules)
        {
            foreach (var pair in rules)
                if (pair.Value.HideForbidden == BasisOverride.ForceOn)
                    return true;
            return false;
        }
    }
}
