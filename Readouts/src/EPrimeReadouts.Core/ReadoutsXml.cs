using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Import/Export in a human-readable/editable XML format using System.Xml.Linq.
    /// <para>
    /// Format:
    /// <code>
    /// &lt;Readouts&gt;
    ///   &lt;Pools&gt;
    ///     &lt;Pool Name="Meats" Icon="Meat_Cow"&gt;
    ///       &lt;Member&gt;@MeatRaw&lt;/Member&gt;
    ///       &lt;Member&gt;SomeDefName&lt;/Member&gt;
    ///     &lt;/Pool&gt;
    ///   &lt;/Pools&gt;
    ///   &lt;Groups&gt;
    ///     &lt;Group Name="Raw" DefaultEnabled="True"&gt;
    ///       &lt;Tier&gt;
    ///         &lt;Slot&gt;pool:Meats&lt;/Slot&gt;
    ///         &lt;Slot&gt;~Milk&lt;/Slot&gt;
    ///       &lt;/Tier&gt;
    ///     &lt;/Group&gt;
    ///   &lt;/Groups&gt;
    /// &lt;/Readouts&gt;
    /// </code>
    /// </para>
    /// <para>
    /// Pool references in slots use the portable form "pool:Name" (or "~pool:Name" with
    /// the hide-when-zero flag). Save-local "#id" tokens are rewritten on export and
    /// resolved back to "#id" on ApplyImport.
    /// </para>
    /// </summary>
    public static class ReadoutsXml
    {
        private const string PoolRefPrefix = "pool:";

        // ── Export ────────────────────────────────────────────────────────────

        /// <summary>
        /// Pretty-printed export of everything (pools + groups in display order).
        /// "#id" pool-ref tokens are rewritten to "pool:Name" (flag preserved);
        /// tokens whose pool id is unknown are dropped.
        /// </summary>
        public static string Export(
            IReadOnlyList<ResourcePool> pools,
            IReadOnlyList<ReadoutGroup> groupsInDisplayOrder)
        {
            // Build id→name lookup
            var idToName = new Dictionary<int, string>();
            if (pools != null)
                foreach (var p in pools)
                    idToName[p.Id] = p.Name;

            var root = new XElement("Readouts");

            // ── Pools section ─────────────────────────────────────────────
            if (pools != null && pools.Count > 0)
            {
                var poolsEl = new XElement("Pools");
                foreach (var pool in pools)
                {
                    var poolEl = new XElement("Pool");
                    poolEl.SetAttributeValue("Name", pool.Name ?? "");
                    if (!string.IsNullOrEmpty(pool.IconDefName))
                        poolEl.SetAttributeValue("Icon", pool.IconDefName);
                    if (pool.Members != null)
                    {
                        foreach (var member in pool.Members)
                        {
                            if (!string.IsNullOrEmpty(member))
                                poolEl.Add(new XElement("Member", member));
                        }
                    }
                    poolsEl.Add(poolEl);
                }
                root.Add(poolsEl);
            }

            // ── Groups section ────────────────────────────────────────────
            if (groupsInDisplayOrder != null && groupsInDisplayOrder.Count > 0)
            {
                var groupsEl = new XElement("Groups");
                foreach (var group in groupsInDisplayOrder)
                {
                    var groupEl = new XElement("Group");
                    groupEl.SetAttributeValue("Name", group.Name ?? "");
                    if (!group.DefaultEnabled)
                        groupEl.SetAttributeValue("DefaultEnabled", "False");

                    if (group.Tiers != null)
                    {
                        foreach (var tier in group.Tiers)
                        {
                            var tierEl = new XElement("Tier");
                            foreach (var token in tier)
                            {
                                if (string.IsNullOrEmpty(token)) continue;
                                string portable = TokenToPortable(token, idToName);
                                if (portable != null)
                                    tierEl.Add(new XElement("Slot", portable));
                            }
                            // Only add tier element if it has slots after rewriting
                            if (tierEl.HasElements)
                                groupEl.Add(tierEl);
                        }
                    }
                    groupsEl.Add(groupEl);
                }
                root.Add(groupsEl);
            }

            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = true,
            };

            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, settings))
                root.WriteTo(writer);
            return sb.ToString();
        }

        /// Converts a save-local token to its portable XML representation.
        /// Returns null when the token references an unknown pool (drop it).
        private static string TokenToPortable(string token, Dictionary<int, string> idToName)
        {
            if (!SlotToken.IsPoolRef(token)) return token;

            bool flag = !SlotToken.ShowWhenZero(token);
            int id = SlotToken.PoolId(token);
            if (!idToName.TryGetValue(id, out string name)) return null; // unknown pool → drop

            string portable = PoolRefPrefix + name;
            return flag ? "~" + portable : portable;
        }

        // ── Import ────────────────────────────────────────────────────────────

        /// <summary>
        /// Parses the XML format. Returns false with a short error on malformed XML
        /// or a missing &lt;Readouts&gt; root. Unknown elements/attributes are ignored;
        /// empty slots/members are dropped; imported group tiers keep "pool:Name"
        /// tokens verbatim (resolution to "#id" happens in ApplyImport).
        /// Imported ResourcePool/ReadoutGroup instances carry Id = 0 (assigned on apply);
        /// ReadoutGroup.OrderIndex = position in file.
        /// Excess tiers beyond TierOps.MaxTiers are dropped; tiers are compacted.
        /// </summary>
        public static bool TryImport(
            string xml,
            out List<ResourcePool> pools,
            out List<ReadoutGroup> groups,
            out string error)
        {
            pools = new List<ResourcePool>();
            groups = new List<ReadoutGroup>();
            error = null;

            if (string.IsNullOrEmpty(xml))
            {
                error = "XML string is null or empty.";
                return false;
            }

            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (Exception ex)
            {
                error = "Malformed XML: " + ex.Message;
                return false;
            }

            var root = doc.Root;
            if (root == null || root.Name.LocalName != "Readouts")
            {
                error = "Missing <Readouts> root element.";
                return false;
            }

            // ── Pools ─────────────────────────────────────────────────────
            var poolsEl = root.Element("Pools");
            if (poolsEl != null)
            {
                foreach (var poolEl in poolsEl.Elements("Pool"))
                {
                    string name = (string)poolEl.Attribute("Name") ?? "";
                    string icon = (string)poolEl.Attribute("Icon");

                    var pool = new ResourcePool
                    {
                        Id = 0,
                        Name = name,
                        IconDefName = string.IsNullOrEmpty(icon) ? null : icon,
                    };

                    foreach (var memberEl in poolEl.Elements("Member"))
                    {
                        string member = memberEl.Value;
                        if (!string.IsNullOrEmpty(member))
                            pool.Members.Add(member);
                    }

                    pools.Add(pool);
                }
            }

            // ── Groups ────────────────────────────────────────────────────
            var groupsEl = root.Element("Groups");
            if (groupsEl != null)
            {
                int orderIndex = 0;
                foreach (var groupEl in groupsEl.Elements("Group"))
                {
                    string name = (string)groupEl.Attribute("Name") ?? "";

                    bool defaultEnabled = true;
                    var deAttr = groupEl.Attribute("DefaultEnabled");
                    if (deAttr != null)
                    {
                        // Accept "False"/"false"/"0" as false; anything else as true
                        string val = deAttr.Value.Trim();
                        if (val.Equals("False", StringComparison.OrdinalIgnoreCase) ||
                            val == "0")
                            defaultEnabled = false;
                    }

                    var group = new ReadoutGroup
                    {
                        Id = 0,
                        Name = name,
                        OrderIndex = orderIndex++,
                        DefaultEnabled = defaultEnabled,
                    };

                    int tierCount = 0;
                    foreach (var tierEl in groupEl.Elements("Tier"))
                    {
                        if (tierCount >= TierOps.MaxTiers) break; // drop excess tiers

                        var tierTokens = new List<string>();
                        foreach (var slotEl in tierEl.Elements("Slot"))
                        {
                            string slot = slotEl.Value;
                            if (!string.IsNullOrEmpty(slot))
                                tierTokens.Add(slot);
                        }

                        if (tierTokens.Count > 0)
                        {
                            group.Tiers.Add(tierTokens);
                            tierCount++;
                        }
                        // empty tier → skip (compaction)
                    }

                    groups.Add(group);
                }
            }

            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// True when the canonical form of a token is a portable pool reference
        /// ("pool:…"). The ':' character is not legal in RimWorld defNames, so
        /// there is no collision risk.
        /// </summary>
        internal static bool IsPortablePoolRef(string token) =>
            SlotToken.Canonical(token).StartsWith(PoolRefPrefix);

        /// <summary>
        /// Extracts the pool name from a portable token (strips flag and "pool:" prefix).
        /// </summary>
        internal static string PortablePoolName(string token) =>
            SlotToken.Canonical(token).Substring(PoolRefPrefix.Length);
    }
}
