using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// One-time normalization for location tokens written before ship identity
    /// was independent of the landing map.
    /// </summary>
    public static class LocationTokenMigration
    {
        public static List<string> Normalize(
            IReadOnlyList<string> source,
            string stableShipToken,
            ISet<string> liveSettlementTokens)
        {
            var result = new List<string>();
            if (source == null || source.Count == 0) return result;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool canMapShip = !string.IsNullOrEmpty(stableShipToken)
                && stableShipToken.StartsWith(
                    LocationRules.ShipPrefix, StringComparison.Ordinal)
                && stableShipToken.Length > LocationRules.ShipPrefix.Length;

            for (int i = 0; i < source.Count; i++)
            {
                string token = source[i];
                string keep = null;
                if (token == LocationRules.Settlements
                    || token == LocationRules.Caravans)
                    keep = token;
                else if (!string.IsNullOrEmpty(token)
                    && token.StartsWith(
                        LocationRules.ShipPrefix, StringComparison.Ordinal)
                    && token.Length > LocationRules.ShipPrefix.Length
                    && canMapShip)
                    keep = stableShipToken;
                else if (!string.IsNullOrEmpty(token)
                    && token.StartsWith(
                        LocationRules.SettlementPrefix, StringComparison.Ordinal)
                    && token.Length > LocationRules.SettlementPrefix.Length
                    && liveSettlementTokens?.Contains(token) == true)
                    keep = token;

                if (keep != null && seen.Add(keep)) result.Add(keep);
            }

            if (result.Count == 0) result.Add(LocationRules.Nowhere);
            return result;
        }
    }

    /// <summary>
    /// Deterministic token-list edits shared by synced commands and tests.
    /// </summary>
    public static class LocationTokenSelection
    {
        public static bool Toggle(List<string> tokens, string token)
        {
            if (tokens == null || string.IsNullOrEmpty(token)) return false;

            bool removed = false;
            for (int i = tokens.Count - 1; i >= 0; i--)
                if (tokens[i] == token)
                {
                    tokens.RemoveAt(i);
                    removed = true;
                }
            if (removed) return true;

            if (token != LocationRules.Nowhere)
                for (int i = tokens.Count - 1; i >= 0; i--)
                    if (tokens[i] == LocationRules.Nowhere)
                        tokens.RemoveAt(i);
            tokens.Add(token);
            return true;
        }
    }
}
