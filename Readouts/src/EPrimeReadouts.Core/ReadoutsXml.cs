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
        /// <para>
        /// When <paramref name="packageIdOf"/> is provided (defName or
        /// "@CategoryDefName" → owning packageId, null for base-game content),
        /// mod requirements are derived and emitted: members and plain slots
        /// from a non-base package get <c>MayRequire</c>; a pool whose members
        /// are ALL restricted gets <c>MayRequireAnyOf</c> with their distinct
        /// packageIds (it exists if any member can); a group whose slots are
        /// ALL restricted (pool-ref slots inherit their pool's derived set)
        /// likewise gets <c>MayRequireAnyOf</c>. Pool-ref slots themselves are
        /// never annotated — gating cascades through the pool.
        /// </para>
        /// </summary>
        public static string Export(
            IReadOnlyList<ResourcePool> pools,
            IReadOnlyList<ReadoutGroup> groupsInDisplayOrder,
            Func<string, string?>? packageIdOf = null)
        {
            // Build id→name lookup
            var idToName = new Dictionary<int, string>();
            if (pools != null)
                foreach (var p in pools)
                    idToName[p.Id] = p.Name;

            var root = new XElement("Readouts");

            // Per-pool derived requirement set: distinct member packageIds when
            // ALL members are restricted, null when the pool is unrestricted.
            // Group derivation below reads it for pool-ref slots.
            Dictionary<int, List<string>?>? poolRequirements = packageIdOf != null
                ? new Dictionary<int, List<string>?>()
                : null;

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

                    List<string>? memberIds = null;
                    bool allRestricted = packageIdOf != null;
                    int memberCount = 0;
                    if (pool.Members != null)
                    {
                        foreach (var member in pool.Members)
                        {
                            if (string.IsNullOrEmpty(member)) continue;
                            memberCount++;
                            var memberEl = new XElement("Member", member);
                            string? packageId = packageIdOf?.Invoke(member);
                            if (packageId != null)
                            {
                                memberEl.SetAttributeValue("MayRequire", packageId);
                                AddDistinct(ref memberIds, packageId);
                            }
                            else
                            {
                                allRestricted = false;
                            }
                            poolEl.Add(memberEl);
                        }
                    }

                    bool restricted = allRestricted && memberCount > 0;
                    if (restricted)
                        poolEl.SetAttributeValue("MayRequireAnyOf", string.Join(",", memberIds));
                    if (poolRequirements != null)
                        poolRequirements[pool.Id] = restricted ? memberIds : null;

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

                    List<string>? slotIds = null;
                    bool allSlotsRestricted = packageIdOf != null;
                    int slotCount = 0;
                    if (group.Tiers != null)
                    {
                        foreach (var tier in group.Tiers)
                        {
                            var tierEl = new XElement("Tier");
                            foreach (var token in tier)
                            {
                                if (string.IsNullOrEmpty(token)) continue;
                                string? portable = TokenToPortable(token, idToName);
                                if (portable == null) continue;
                                var slotEl = new XElement("Slot", portable);
                                slotCount++;

                                if (packageIdOf != null)
                                {
                                    if (SlotToken.IsPoolRef(token))
                                    {
                                        // Never annotated directly: gating cascades
                                        // through the pool's own requirement set.
                                        poolRequirements!.TryGetValue( // Non-null when packageIdOf is provided.
                                            SlotToken.PoolId(token), out var poolSet);
                                        if (poolSet != null)
                                            foreach (var id in poolSet)
                                                AddDistinct(ref slotIds, id);
                                        else
                                            allSlotsRestricted = false;
                                    }
                                    else
                                    {
                                        string? packageId = packageIdOf(SlotToken.Canonical(token));
                                        if (packageId != null)
                                        {
                                            slotEl.SetAttributeValue("MayRequire", packageId);
                                            AddDistinct(ref slotIds, packageId);
                                        }
                                        else
                                        {
                                            allSlotsRestricted = false;
                                        }
                                    }
                                }

                                tierEl.Add(slotEl);
                            }
                            // Only add tier element if it has slots after rewriting
                            if (tierEl.HasElements)
                                groupEl.Add(tierEl);
                        }
                    }

                    if (allSlotsRestricted && slotCount > 0)
                        groupEl.SetAttributeValue("MayRequireAnyOf", string.Join(",", slotIds));

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

        /// Appends a packageId to a lazily created list, preserving first-encounter
        /// order (deterministic attribute output) and skipping duplicates.
        private static void AddDistinct(ref List<string>? ids, string packageId)
        {
            if (ids == null) ids = new List<string>();
            if (!ids.Contains(packageId)) ids.Add(packageId);
        }

        /// Converts a save-local token to its portable XML representation.
        /// Returns null when the token references an unknown pool (drop it).
        private static string? TokenToPortable(string token, Dictionary<int, string> idToName)
        {
            if (!SlotToken.IsPoolRef(token)) return token;

            bool flag = !SlotToken.ShowWhenZero(token);
            int id = SlotToken.PoolId(token);
            if (!idToName.TryGetValue(id, out string name)) return null; // unknown pool → drop

            string portable = PoolRefPrefix + name;
            return flag ? "~" + portable : portable;
        }

        /// <summary>
        /// Evaluates the vanilla-style mod-requirement attributes on an element:
        /// <c>MayRequire</c> (comma-separated packageIds, ALL must be active) and
        /// <c>MayRequireAnyOf</c> (comma-separated packageIds, ANY must be active).
        /// A null predicate keeps everything.
        /// </summary>
        private static bool ModsPresent(XElement el, Func<string, bool>? isModActive)
        {
            if (isModActive == null) return true;

            string all = (string)el.Attribute("MayRequire");
            if (!string.IsNullOrEmpty(all))
            {
                foreach (var id in all.Split(','))
                    if (!isModActive(id.Trim()))
                        return false;
            }

            string any = (string)el.Attribute("MayRequireAnyOf");
            if (!string.IsNullOrEmpty(any))
            {
                bool anyActive = false;
                foreach (var id in any.Split(','))
                {
                    if (isModActive(id.Trim()))
                    {
                        anyActive = true;
                        break;
                    }
                }
                if (!anyActive) return false;
            }

            return true;
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
        /// <para>
        /// When <paramref name="isModActive"/> is provided, Pool, Member, Group,
        /// Tier, and Slot elements honor <c>MayRequire</c>/<c>MayRequireAnyOf</c>
        /// attributes (see <see cref="ModsPresent"/>); elements whose requirements
        /// are not met are skipped along with their children.
        /// </para>
        /// </summary>
        public static bool TryImport(
            string? xml,
            out List<ResourcePool> pools,
            out List<ReadoutGroup> groups,
            out string? error,
            Func<string, bool>? isModActive = null)
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
                    if (!ModsPresent(poolEl, isModActive)) continue;
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
                        if (!ModsPresent(memberEl, isModActive)) continue;
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
                    if (!ModsPresent(groupEl, isModActive)) continue;
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
                        if (!ModsPresent(tierEl, isModActive)) continue;

                        var tierTokens = new List<string>();
                        foreach (var slotEl in tierEl.Elements("Slot"))
                        {
                            if (!ModsPresent(slotEl, isModActive)) continue;
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
