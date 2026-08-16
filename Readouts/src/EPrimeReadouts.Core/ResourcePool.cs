using System.Collections.Generic;
using System.Text;

namespace EPrimeReadouts.Core
{
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

    /// Serializes a pool's member list as a comma-joined blob.
    /// '@' is safe (never appears in defNames); ',' never in defNames.
    /// Mirrors TierBlobCodec's single-level join.
    public static class PoolMembersCodec
    {
        public static string Encode(List<string>? members)
        {
            if (members == null || members.Count == 0) return "";
            var sb = new StringBuilder();
            bool first = true;
            foreach (var m in members)
            {
                if (string.IsNullOrEmpty(m)) continue;
                if (!first) sb.Append(',');
                sb.Append(m);
                first = false;
            }
            return sb.ToString();
        }

        public static List<string> Decode(string? blob)
        {
            var list = new List<string>();
            if (blob == null || blob.Length == 0) return list;
            foreach (var part in blob.Split(','))
                if (part.Length > 0) list.Add(part);
            return list;
        }
    }
}
