using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Snapshot-time validity for role-list presentation. Named locations are
    /// compared against one precomputed set; renderers consume only the result.
    /// </summary>
    public static class RoleLocationValidity
    {
        public static bool IsInvalid(int entryCount,
            IReadOnlyList<string> locationTokens,
            ISet<string> liveLocationIds)
        {
            if (entryCount == 0) return true;
            if (locationTokens == null || locationTokens.Count == 0) return false;
            for (int i = 0; i < locationTokens.Count; i++)
                if (!IsStale(locationTokens[i], liveLocationIds))
                    return false;
            return true;
        }

        private static bool IsStale(string token, ISet<string> liveLocationIds)
        {
            if (token == LocationRules.Settlements
                || token == LocationRules.Caravans)
                return false;
            int prefixLength;
            if (token != null && token.StartsWith(
                    LocationRules.SettlementPrefix, StringComparison.Ordinal))
                prefixLength = LocationRules.SettlementPrefix.Length;
            else if (token != null && token.StartsWith(
                         LocationRules.ShipPrefix, StringComparison.Ordinal))
                prefixLength = LocationRules.ShipPrefix.Length;
            else
                return true;
            string id = token.Substring(prefixLength);
            return id.Length == 0 || liveLocationIds == null
                || !liveLocationIds.Contains(id);
        }
    }
}
