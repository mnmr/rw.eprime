using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Resolves portable file labels once, then replays only invariant tokens.
    public static class ImportLocationResolver
    {
        public static string Resolve(string fileToken,
            IReadOnlyList<LocationInfo> locations)
        {
            if (fileToken == LocationRules.Settlements
                || fileToken == LocationRules.Caravans
                || fileToken == LocationRules.Nowhere)
                return fileToken;
            if (string.IsNullOrEmpty(fileToken) || locations == null) return null;

            bool ship;
            int prefixLength;
            if (fileToken.StartsWith(LocationRules.ShipPrefix,
                    StringComparison.Ordinal))
            {
                ship = true;
                prefixLength = LocationRules.ShipPrefix.Length;
            }
            else if (fileToken.StartsWith(LocationRules.SettlementPrefix,
                         StringComparison.Ordinal))
            {
                ship = false;
                prefixLength = LocationRules.SettlementPrefix.Length;
            }
            else return null;

            string name = fileToken.Substring(prefixLength);
            for (int i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location != null && location.IsShip == ship
                    && string.Equals(location.Label, name,
                        StringComparison.OrdinalIgnoreCase))
                    return (ship ? LocationRules.ShipPrefix
                        : LocationRules.SettlementPrefix) + location.Id;
            }
            return null;
        }

        public static Dictionary<string, string> BuildMap(
            IReadOnlyList<string> fileTokens,
            IReadOnlyList<string> runtimeTokens)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (fileTokens == null || runtimeTokens == null) return result;
            int count = Math.Min(fileTokens.Count, runtimeTokens.Count);
            for (int i = 0; i < count; i++)
                if (!string.IsNullOrEmpty(fileTokens[i]))
                    result[fileTokens[i]] = runtimeTokens[i];
            return result;
        }

        public static string FromMap(string fileToken,
            IReadOnlyDictionary<string, string> resolved)
        {
            if (fileToken == LocationRules.Settlements
                || fileToken == LocationRules.Caravans
                || fileToken == LocationRules.Nowhere)
                return fileToken;
            return resolved != null
                && resolved.TryGetValue(fileToken, out string runtimeToken)
                && !string.IsNullOrEmpty(runtimeToken)
                ? runtimeToken : null;
        }
    }
}
