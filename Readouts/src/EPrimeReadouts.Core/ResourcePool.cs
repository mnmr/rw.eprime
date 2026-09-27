using System;
using System.Collections.Generic;
using System.Text;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Shared normalization and comparison policy for user-visible pool names.
    /// Pool names are unique only within the pool namespace; resource def names
    /// and labels deliberately do not participate in this policy.
    /// </summary>
    public static class PoolNameRules
    {
        public const string LegacyFallbackName = "Pool";
        public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

        public static string Normalize(string? name) => (name ?? "").Trim();
    }

    /// A named, user-defined collection of resources. Members are defNames or
    /// "@CategoryDefName" refs (expanded at snapshot time so newly-added modded
    /// resources join automatically).
    public sealed class ResourcePool
    {
        public int Id;
        public string Name = "";
        /// Members are defNames or "@CategoryDefName" refs (expanded at snapshot).
        public List<string> Members = new List<string>();
        /// Explicit icon choice; null/empty or unresolvable falls back to the
        /// first resolved member.
        public string? IconDefName;
    }

    /// Serializes a pool's member list as a comma-joined blob, escaping ','
    /// '|' and '\' inside member text exactly like TierBlobCodec, so any text
    /// round-trips. Blobs written before escaping contain no backslash and
    /// decode unchanged.
    public static class PoolMembersCodec
    {
        public static string Encode(IReadOnlyList<string>? members)
        {
            if (members == null || members.Count == 0) return "";
            var sb = new StringBuilder();
            bool first = true;
            for (int i = 0; i < members.Count; i++)
            {
                string m = members[i];
                if (string.IsNullOrEmpty(m)) continue;
                if (!first) sb.Append(',');
                BlobText.AppendEscaped(sb, m);
                first = false;
            }
            return sb.ToString();
        }

        public static List<string> Decode(string? blob)
        {
            var list = new List<string>();
            if (blob == null || blob.Length == 0) return list;
            var token = new StringBuilder();
            for (int i = 0; i < blob.Length; i++)
            {
                char c = blob[i];
                if (c == '\\')
                {
                    if (++i < blob.Length) token.Append(blob[i]);
                }
                else if (c == ',') BlobText.Flush(token, list);
                else token.Append(c);
            }
            BlobText.Flush(token, list);
            return list;
        }
    }
}
