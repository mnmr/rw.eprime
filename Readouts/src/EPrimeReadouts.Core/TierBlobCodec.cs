using System.Collections.Generic;
using System.Text;

namespace EPrimeReadouts.Core
{
    /// Serializes a tier layout for the save file: slots joined by ',', tiers
    /// by '|'. Slot text is not limited to
    /// defName characters (imported files can carry anything), so ',', '|' and
    /// '\' inside a slot are written as "\," "\|" "\\". Blobs written before
    /// escaping contain no backslash and decode unchanged.
    public static class TierBlobCodec
    {
        public static string Encode(List<List<string>>? tiers)
        {
            if (tiers == null) return "";
            var sb = new StringBuilder();
            for (int t = 0; t < tiers.Count; t++)
            {
                if (t > 0) sb.Append('|');
                List<string> tier = tiers[t];
                for (int i = 0; i < tier.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    BlobText.AppendEscaped(sb, tier[i]);
                }
            }
            return sb.ToString();
        }

        public static List<List<string>> Decode(string? blob)
        {
            var tiers = new List<List<string>>();
            if (blob == null || blob.Length == 0) return tiers;
            var tier = new List<string>();
            var token = new StringBuilder();
            for (int i = 0; i < blob.Length; i++)
            {
                char c = blob[i];
                if (c == '\\')
                {
                    if (++i < blob.Length) token.Append(blob[i]);
                }
                else if (c == ',')
                {
                    BlobText.Flush(token, tier);
                }
                else if (c == '|')
                {
                    BlobText.Flush(token, tier);
                    if (tier.Count > 0)
                    {
                        tiers.Add(tier);
                        tier = new List<string>();
                    }
                }
                else
                {
                    token.Append(c);
                }
            }
            BlobText.Flush(token, tier);
            if (tier.Count > 0) tiers.Add(tier);
            return tiers;
        }
    }

    /// Escaping shared by the tier and pool-member blobs.
    internal static class BlobText
    {
        internal static void AppendEscaped(StringBuilder sb, string? text)
        {
            if (text == null) return;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' || c == ',' || c == '|') sb.Append('\\');
                sb.Append(c);
            }
        }

        /// Ends the current token; empty tokens are dropped.
        internal static void Flush(StringBuilder token, List<string> into)
        {
            if (token.Length > 0) into.Add(token.ToString());
            token.Length = 0;
        }
    }
}
