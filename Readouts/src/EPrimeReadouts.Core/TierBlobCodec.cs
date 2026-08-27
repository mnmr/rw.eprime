using System.Collections.Generic;
using System.Text;

namespace EPrimeReadouts.Core
{
    /// Serializes a tier layout for the SetGroupLayout sync command. RimWorld
    /// defNames never contain ',' or '|' (identifier characters only), so a
    /// two-level join is unambiguous.
    public static class TierBlobCodec
    {
        public static string Encode(List<List<string>>? tiers)
        {
            if (tiers == null) return "";
            var sb = new StringBuilder();
            for (int t = 0; t < tiers.Count; t++)
            {
                if (t > 0) sb.Append('|');
                sb.Append(string.Join(",", tiers[t]));
            }
            return sb.ToString();
        }

        public static List<List<string>> Decode(string? blob)
        {
            var tiers = new List<List<string>>();
            if (blob == null || blob.Length == 0) return tiers;
            foreach (var part in blob.Split('|'))
            {
                var tier = new List<string>();
                foreach (var defName in part.Split(','))
                    if (defName.Length > 0) tier.Add(defName);
                if (tier.Count > 0) tiers.Add(tier);
            }
            return tiers;
        }
    }
}
