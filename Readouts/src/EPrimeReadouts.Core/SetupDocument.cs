using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace EPrimeReadouts.Core
{
    [Flags]
    public enum SetupSections { None = 0, Groups = 1, Pools = 2, Thresholds = 4, CountRules = 8, PlayerSettings = 16, Model = 15, All = 31 }

    /// Detached portable configuration; constructed only at explicit setup boundaries.
    public sealed class PlayerSetup
    {
        public string ProfileId = "";
        public Dictionary<string, string> Options = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, bool> EnabledGroups = new Dictionary<string, bool>(ReadoutGroupNames.Comparer);

        public static readonly string[] BooleanOptions = {
            "useVanillaReadout", "showSearchFilter", "showModNameWhenNoSearch", "searchHideZero",
            "searchStorageOnly", "searchHideForbidden", "selectJumpCamera", "expandOnHover",
            "collapseWhenIdle", "keepReadoutToggle", "verticalTiers", "renderCountersWithTinyFont",
            "reserveForBills", "reserveForBuildables", "showNegativeCounts", "qualityJobsRework" };
        public static readonly string[] NumericOptions = { "offsetX", "offsetY", "panelWidth", "bottomMargin" };

        internal static bool NormalizeOption(string name, string value, out string? normalized)
        {
            normalized = null;
            if (Array.IndexOf(BooleanOptions, name) >= 0)
            {
                if (!bool.TryParse(value, out bool flag)) return false;
                normalized = flag ? "True" : "False";
            }
            else if (Array.IndexOf(NumericOptions, name) >= 0)
            {
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number)
                    || float.IsNaN(number) || float.IsInfinity(number)) return false;
                float min = name == "panelWidth" ? 80f : 0f;
                float max = name == "panelWidth" ? 400f : name == "bottomMargin" ? 500f : 200f;
                normalized = Math.Max(min, Math.Min(max, number)).ToString("R", CultureInfo.InvariantCulture);
            }
            return true; // unknown future options are ignored
        }
    }

    public sealed class SetupDocument
    {
        public List<ResourcePool> Pools = new List<ResourcePool>();
        public List<ReadoutGroup> Groups = new List<ReadoutGroup>();
        public Dictionary<string, ThresholdSpec> Thresholds = new Dictionary<string, ThresholdSpec>(StringComparer.Ordinal);
        public Dictionary<string, CountRule> CountRules = new Dictionary<string, CountRule>(StringComparer.Ordinal);
        public PlayerSetup? Player;
        public SetupSections Present = SetupSections.All;
        private readonly Dictionary<string, string> requiredPackages = new Dictionary<string, string>(StringComparer.Ordinal);

        public void RetainUnavailableFrom(SetupDocument original, SetupDocument availableBeforeEdits)
        {
            foreach (var pair in original.requiredPackages) requiredPackages[pair.Key] = pair.Value;
            var poolNames = new Dictionary<string, string?>(PoolNameRules.Comparer);
            foreach (var beforePool in availableBeforeEdits.Pools)
            {
                ResourcePool? currentPool = FindPoolAfterEdits(Pools, beforePool);
                poolNames[beforePool.Name] = currentPool?.Name;
            }
            foreach (var source in original.Pools)
            {
                ResourcePool? before = FindPool(availableBeforeEdits.Pools, source.Name);
                ResourcePool? current = before == null ? FindPool(Pools, source.Name) : FindPoolAfterEdits(Pools, before);
                if (before == null)
                {
                    if (current == null) Pools.Add(CopyPool(source, 0));
                    continue;
                }
                if (current == null) continue; // explicitly deleted
                foreach (string member in source.Members)
                    if (!before.Members.Contains(member) && !current.Members.Contains(member)) current.Members.Add(member);
                if (before.IconDefName == null && current.IconDefName == null) current.IconDefName = source.IconDefName;
            }
            foreach (var source in original.Groups)
            {
                var sourceTiers = RemapTiers(source.Tiers, poolNames);
                ReadoutGroup? before = FindGroup(availableBeforeEdits.Groups, source.Name);
                ReadoutGroup? current = before == null ? FindGroup(Groups, source.Name) : FindGroupAfterEdits(Groups, before);
                if (before == null)
                {
                    if (current == null) Groups.Insert(Math.Min(original.Groups.IndexOf(source), Groups.Count), new ReadoutGroup { Name = source.Name, SeedName = source.SeedName,
                        DefaultEnabled = source.DefaultEnabled, Tiers = sourceTiers });
                    if (Player != null && original.Player != null && original.Player.EnabledGroups.TryGetValue(source.Name, out bool enabled))
                        Player.EnabledGroups[source.Name] = enabled;
                    continue;
                }
                if (current == null) continue;
                var beforeTiers = RemapTiers(before.Tiers, poolNames);
                if (SameTiers(beforeTiers, current.Tiers))
                {
                    current.Tiers = sourceTiers;
                    continue;
                }
                for (int t = 0; t < sourceTiers.Count; t++)
                    foreach (string token in sourceTiers[t])
                    {
                        if (TierOps.Contains(beforeTiers, token) || TierOps.Contains(current.Tiers, token)) continue;
                        if (ReadoutsXml.IsPortablePoolRef(token) && FindPool(Pools, ReadoutsXml.PortablePoolName(token)) == null) continue;
                        while (current.Tiers.Count <= t) current.Tiers.Add(new List<string>());
                        current.Tiers[t].Add(token);
                    }
            }
            foreach (var pair in original.Thresholds)
            {
                string? key = RemapToken(pair.Key, poolNames);
                if (key != null && !availableBeforeEdits.Thresholds.ContainsKey(pair.Key) && !Thresholds.ContainsKey(key))
                    Thresholds.Add(key, pair.Value);
            }
            foreach (var pair in original.CountRules)
            {
                string? key = RemapToken(pair.Key, poolNames);
                if (key != null && !availableBeforeEdits.CountRules.ContainsKey(pair.Key) && !CountRules.ContainsKey(key))
                    CountRules.Add(key, pair.Value);
            }
        }

        private static string? RemapToken(string token, Dictionary<string, string?> names)
        {
            if (!ReadoutsXml.IsPortablePoolRef(token) || !names.TryGetValue(ReadoutsXml.PortablePoolName(token), out string? name)) return token;
            return name == null ? null : (SlotToken.ShowWhenZero(token) ? "" : "~") + "pool:" + name;
        }

        private static List<List<string>> RemapTiers(List<List<string>> source, Dictionary<string, string?> names)
        {
            var result = new List<List<string>>();
            foreach (var tier in source)
            {
                var tokens = new List<string>();
                foreach (string token in tier) { string? mapped = RemapToken(token, names); if (mapped != null) tokens.Add(mapped); }
                if (tokens.Count != 0) result.Add(tokens);
            }
            return result;
        }

        private static ResourcePool? FindPool(List<ResourcePool> pools, string name)
        { foreach (var pool in pools) if (PoolNameRules.Comparer.Equals(pool.Name, name)) return pool; return null; }
        private static ReadoutGroup? FindGroup(List<ReadoutGroup> groups, string name)
        { foreach (var group in groups) if (ReadoutGroupNames.Comparer.Equals(group.Name, name)) return group; return null; }

        private static ResourcePool? FindPoolAfterEdits(List<ResourcePool> pools, ResourcePool before)
        {
            if (before.Id == 0) return FindPool(pools, before.Name);
            foreach (var pool in pools) if (pool.Id == before.Id) return pool;
            return null;
        }

        private static ReadoutGroup? FindGroupAfterEdits(List<ReadoutGroup> groups, ReadoutGroup before)
        {
            if (before.Id == 0) return FindGroup(groups, before.Name);
            foreach (var group in groups) if (group.Id == before.Id) return group;
            return null;
        }

        private static bool SameTiers(List<List<string>> first, List<List<string>> second)
        {
            if (first.Count != second.Count) return false;
            for (int t = 0; t < first.Count; t++)
            {
                if (first[t].Count != second[t].Count) return false;
                for (int s = 0; s < first[t].Count; s++) if (first[t][s] != second[t][s]) return false;
            }
            return true;
        }

        public static SetupDocument Capture(ReadoutModel model, PlayerSetup? player = null)
        {
            TryRead(ReadoutsXml.Export(model.Pools, model.InDisplayOrder()), out var result, out _);
            result!.Player = player;
            result.Present = SetupSections.Model | (player != null ? SetupSections.PlayerSettings : SetupSections.None);
            var names = new Dictionary<int, string>();
            foreach (var pool in model.Pools) names.Add(pool.Id, pool.Name);
            foreach (var pool in result.Pools) pool.Id = FindPool(model.Pools, pool.Name)!.Id;
            foreach (var group in result.Groups) group.Id = FindGroup(model.Groups, group.Name)!.Id;
            foreach (var pair in model.Thresholds) result.Thresholds.Add(Portable(pair.Key, names), pair.Value);
            foreach (var pair in model.CountRules) result.CountRules.Add(Portable(pair.Key, names), pair.Value);
            return result;
        }

        private static string Portable(string token, Dictionary<int, string> names)
        {
            if (!SlotToken.IsPoolRef(token)) return token;
            if (!names.TryGetValue(SlotToken.PoolId(token), out string name))
                throw new InvalidOperationException("Unknown resource pool: " + token);
            return (SlotToken.ShowWhenZero(token) ? "" : "~") + "pool:" + name;
        }

        private static string Resolve(string token, Dictionary<string, int> ids)
        {
            if (!ReadoutsXml.IsPortablePoolRef(token)) return token;
            return (SlotToken.ShowWhenZero(token) ? "" : "~") + "#" + ids[ReadoutsXml.PortablePoolName(token)].ToString(CultureInfo.InvariantCulture);
        }

        public string ToXml(Func<string, string?>? packageIdOf = null)
        {
            Func<string, string?> resolvePackage = token => packageIdOf?.Invoke(token)
                ?? (requiredPackages.TryGetValue(token, out string package) ? package : null);
            var ids = new Dictionary<string, int>(PoolNameRules.Comparer);
            var pools = new List<ResourcePool>();
            foreach (var source in Pools)
            {
                int id = pools.Count + 1; ids.Add(source.Name, id);
                pools.Add(CopyPool(source, id));
            }
            var groups = new List<ReadoutGroup>();
            foreach (var source in Groups) groups.Add(CopyGroup(source, groups.Count + 1, groups.Count, ids));
            var root = XElement.Parse(ReadoutsXml.Export(pools, groups, resolvePackage));
            root.SetAttributeValue("Version", 2);
            if ((Present & SetupSections.Pools) != 0 && root.Element("Pools") == null) root.Add(new XElement("Pools"));
            if ((Present & SetupSections.Groups) != 0 && root.Element("Groups") == null) root.Add(new XElement("Groups"));
            if ((Present & SetupSections.Thresholds) != 0)
            {
                var section = new XElement("Thresholds");
                foreach (string key in SortedKeys(Thresholds))
                {
                    ThresholdSpec value = Thresholds[key];
                    var item = new XElement("Threshold", new XAttribute("Token", key),
                        new XAttribute("Low", value.Low), new XAttribute("Critical", value.Critical));
                    Require(item, key, resolvePackage); section.Add(item);
                }
                root.Add(section);
            }
            if ((Present & SetupSections.CountRules) != 0)
            {
                var section = new XElement("CountRules");
                foreach (string key in SortedKeys(CountRules))
                {
                    CountRule value = CountRules[key];
                    var item = new XElement("Rule", new XAttribute("Token", key),
                        new XAttribute("StorageOnly", (int)value.StorageOnly), new XAttribute("HideForbidden", (int)value.HideForbidden));
                    Require(item, key, resolvePackage); section.Add(item);
                }
                root.Add(section);
            }
            if (Player != null)
            {
                var section = new XElement("PlayerSettings", new XAttribute("ProfileId", Player.ProfileId));
                foreach (string key in SortedKeys(Player.Options))
                    section.Add(new XElement("Option", new XAttribute("Name", key), new XAttribute("Value", Player.Options[key])));
                foreach (string key in SortedKeys(Player.EnabledGroups))
                    section.Add(new XElement("Group", new XAttribute("Name", key), new XAttribute("Enabled", Player.EnabledGroups[key])));
                root.Add(section);
            }
            return root.ToString();
        }

        private static List<string> SortedKeys<T>(Dictionary<string, T> source)
        { var keys = new List<string>(source.Keys); keys.Sort(StringComparer.Ordinal); return keys; }

        private static void Require(XElement node, string token, Func<string, string?>? packageIdOf)
        {
            if (packageIdOf == null || ReadoutsXml.IsPortablePoolRef(token)) return;
            string? package = packageIdOf(token);
            if (!string.IsNullOrEmpty(package)) node.SetAttributeValue("MayRequire", package);
        }

        public static bool TryRead(string xml, out SetupDocument? result, out string? error,
            Func<string, bool>? isModActive = null, Func<string, bool>? memberExists = null)
        {
            result = null;
            if (!ReadoutsXml.TryImport(xml, out var pools, out var groups, out error, isModActive)) return false;
            ReadoutModel.NormalizeImportedGroupNames(groups);
            pools.Sort((a, b) => PoolNameRules.Comparer.Compare(a.Name, b.Name));
            foreach (var group in groups)
                foreach (var tier in group.Tiers)
                    for (int i = 0; i < tier.Count; i++)
                        if (ReadoutsXml.IsPortablePoolRef(tier[i]))
                            tier[i] = (SlotToken.ShowWhenZero(tier[i]) ? "" : "~") + "pool:" + FindPool(pools, ReadoutsXml.PortablePoolName(tier[i]))!.Name;
            var root = XElement.Parse(xml);
            var document = new SetupDocument { Pools = pools, Groups = groups, Present = SetupSections.Groups | SetupSections.Pools };
            foreach (var item in root.Descendants())
            {
                string? requirement = (string?)item.Attribute("MayRequire");
                if (requirement == null) continue;
                string kind = item.Name.LocalName;
                string? token = kind == "Member" || kind == "Slot" ? item.Value.Trim()
                    : kind == "Threshold" || kind == "Rule" ? (string?)item.Attribute("Token") : null;
                if (!string.IsNullOrEmpty(token)) document.requiredPackages[SlotToken.Canonical(token!)] = requirement;
            }
            // Resolve availability once at this boundary, before any live mutation.
            if (memberExists != null)
            {
                foreach (var pool in pools)
                {
                    pool.Members.RemoveAll(member => !memberExists(member));
                    if (pool.IconDefName != null && !memberExists(pool.IconDefName)) pool.IconDefName = null;
                }
                foreach (var group in groups)
                    TierOps.Cleanup(group.Tiers, token => ReadoutsXml.IsPortablePoolRef(token) || memberExists(SlotToken.Canonical(token)));
            }
            var poolNames = new HashSet<string>(PoolNameRules.Comparer);
            foreach (var pool in pools) poolNames.Add(pool.Name);
            foreach (string sectionName in new[] { "Thresholds", "CountRules" })
            {
                XElement? section = root.Element(sectionName);
                if (section == null) continue;
                bool thresholds = sectionName == "Thresholds";
                document.Present |= thresholds ? SetupSections.Thresholds : SetupSections.CountRules;
                foreach (XElement item in section.Elements(thresholds ? "Threshold" : "Rule"))
                {
                    string key = SlotToken.Canonical(((string?)item.Attribute("Token") ?? "").Trim());
                    if (key.Length == 0 || SlotToken.IsPoolRef(key)) { error = "Invalid setup token."; return false; }
                    string? require = (string?)item.Attribute("MayRequire");
                    if (isModActive != null && require != null && !isModActive(require)) continue;
                    if (ReadoutsXml.IsPortablePoolRef(key))
                    {
                        if (!poolNames.Contains(ReadoutsXml.PortablePoolName(key)))
                        {
                            // Only a genuinely mod-gated source pool may be omitted.
                            bool excluded = false;
                            foreach (var pool in root.Element("Pools")?.Elements("Pool") ?? new XElement[0])
                                if (PoolNameRules.Comparer.Equals(PoolNameRules.Normalize((string?)pool.Attribute("Name")), ReadoutsXml.PortablePoolName(key))) excluded = true;
                            if (isModActive != null && excluded) continue;
                            error = "Unknown resource pool: " + key; return false;
                        }
                        key = "pool:" + FindPool(pools, ReadoutsXml.PortablePoolName(key))!.Name;
                    }
                    else if (memberExists != null && !memberExists(key)) continue;
                    if (thresholds)
                    {
                        if (!Integer(item, "Low", out int low) || !Integer(item, "Critical", out int critical)
                            || document.Thresholds.ContainsKey(key)) { error = "Invalid or duplicate threshold: " + key; return false; }
                        document.Thresholds.Add(key, new ThresholdSpec(low, critical));
                    }
                    else
                    {
                        if (!Integer(item, "StorageOnly", out int storage) || !Integer(item, "HideForbidden", out int forbidden)
                            || storage < 0 || storage > 2 || forbidden < 0 || forbidden > 2 || document.CountRules.ContainsKey(key))
                        { error = "Invalid or duplicate count rule: " + key; return false; }
                        var rule = new CountRule((BasisOverride)storage, (BasisOverride)forbidden);
                        if (!rule.IsInherit) document.CountRules.Add(key, rule);
                    }
                }
            }
            XElement? playerNode = root.Element("PlayerSettings");
            if (playerNode != null)
            {
                document.Present |= SetupSections.PlayerSettings;
                var player = new PlayerSetup { ProfileId = (string?)playerNode.Attribute("ProfileId") ?? "" };
                foreach (var item in playerNode.Elements("Option"))
                {
                    string name = (string?)item.Attribute("Name") ?? "";
                    if (!PlayerSetup.NormalizeOption(name, (string?)item.Attribute("Value") ?? "", out string? value)
                        || player.Options.ContainsKey(name)) { error = "Invalid player option: " + name; return false; }
                    if (value != null) player.Options.Add(name, value);
                }
                foreach (var item in playerNode.Elements("Group"))
                {
                    string name = ReadoutGroupNames.Normalize((string?)item.Attribute("Name"));
                    if (name.Length == 0 || !bool.TryParse((string?)item.Attribute("Enabled"), out bool enabled)
                        || player.EnabledGroups.ContainsKey(name)) { error = "Invalid group visibility: " + name; return false; }
                    // Mod-gated groups have no local presentation entry.
                    bool available = false;
                    foreach (var group in groups) if (ReadoutGroupNames.Comparer.Equals(group.Name, name)) available = true;
                    if (available || isModActive == null) player.EnabledGroups.Add(name, enabled);
                }
                document.Player = player;
            }
            result = document; return true;
        }

        private static bool Integer(XElement item, string name, out int value) => int.TryParse(
            (string?)item.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        private SetupDocument Combine(ReadoutModel model, SetupSections selected)
        {
            selected &= Present;
            var result = Capture(model);
            if ((selected & SetupSections.Groups) != 0) result.Groups = Groups;
            if ((selected & SetupSections.Pools) != 0) result.Pools = Pools;
            if ((selected & SetupSections.Thresholds) != 0) result.Thresholds = Thresholds;
            if ((selected & SetupSections.CountRules) != 0) result.CountRules = CountRules;
            if ((selected & SetupSections.PlayerSettings) != 0) result.Player = Player;
            return result;
        }

        public bool CanApply(ReadoutModel model, SetupSections selected, out string? error) => Combine(model, selected).Validate(out error);

        private bool Validate(out string? error)
        {
            if (!ReadoutsXml.TryValidatePortableModel(Pools, Groups, out error)) return false;
            var poolNames = new HashSet<string>(PoolNameRules.Comparer);
            foreach (var pool in Pools) poolNames.Add(pool.Name);
            foreach (string key in Thresholds.Keys)
                if (!ValidPool(key, poolNames, out error)) return false;
            foreach (string key in CountRules.Keys)
                if (!ValidPool(key, poolNames, out error)) return false;
            if (Player != null)
                foreach (string name in Player.EnabledGroups.Keys)
                {
                    bool found = false;
                    foreach (var group in Groups) if (ReadoutGroupNames.Comparer.Equals(name, group.Name)) found = true;
                    if (!found) { error = "Player settings require group: " + name; return false; }
                }
            return true;
        }

        private static bool ValidPool(string token, HashSet<string> pools, out string? error)
        {
            error = null;
            if (!ReadoutsXml.IsPortablePoolRef(token) || pools.Contains(ReadoutsXml.PortablePoolName(token))) return true;
            error = "Retained or imported settings require pool: " + ReadoutsXml.PortablePoolName(token); return false;
        }

        public bool TryApply(ReadoutModel model, SetupSections selected, Func<int> takePoolId,
            Func<int> takeGroupId, out ReadoutChange change, out string? error)
        {
            change = ReadoutChange.None;
            var before = Capture(model);
            var combined = Combine(model, selected);
            if (!combined.Validate(out error)) return false;
            // Complete validation precedes both mutation and ID allocation.
            var oldPools = new Dictionary<string, ResourcePool>(PoolNameRules.Comparer);
            foreach (var pool in model.Pools) oldPools.Add(pool.Name, pool);
            var ids = new Dictionary<string, int>(PoolNameRules.Comparer);
            var newPools = new List<ResourcePool>();
            foreach (var pool in combined.Pools)
            {
                int id = oldPools.TryGetValue(pool.Name, out var old) ? old.Id : takePoolId();
                ids.Add(pool.Name, id); newPools.Add(CopyPool(pool, id));
            }
            var oldGroups = new Dictionary<string, ReadoutGroup>(ReadoutGroupNames.Comparer);
            foreach (var group in model.Groups) oldGroups.Add(group.Name, group);
            var newGroups = new List<ReadoutGroup>();
            foreach (var group in combined.Groups)
            {
                int id = oldGroups.TryGetValue(group.Name, out var old) ? old.Id : takeGroupId();
                newGroups.Add(CopyGroup(group, id, newGroups.Count, ids));
            }
            var thresholds = new Dictionary<string, ThresholdSpec>();
            foreach (var pair in combined.Thresholds) thresholds.Add(Resolve(pair.Key, ids), pair.Value);
            var rules = new Dictionary<string, CountRule>();
            foreach (var pair in combined.CountRules) rules.Add(Resolve(pair.Key, ids), pair.Value);
            // Canonical, domain-specific comparisons preserve unchanged live objects and revisions.
            XElement beforeXml = XElement.Parse(before.ToXml()), afterXml = XElement.Parse(combined.ToXml());
            if (!XNode.DeepEquals(beforeXml.Element("Pools"), afterXml.Element("Pools"))) { model.Pools = newPools; change |= ReadoutChange.Pools; }
            if (!XNode.DeepEquals(beforeXml.Element("Groups"), afterXml.Element("Groups"))) { model.Groups = newGroups; change |= ReadoutChange.Groups; }
            if (!XNode.DeepEquals(beforeXml.Element("Thresholds"), afterXml.Element("Thresholds"))) { model.Thresholds = thresholds; change |= ReadoutChange.Thresholds; }
            if (!XNode.DeepEquals(beforeXml.Element("CountRules"), afterXml.Element("CountRules"))) { model.CountRules = rules; change |= ReadoutChange.CountRules; }
            return true;
        }

        private static ResourcePool CopyPool(ResourcePool source, int id) => new ResourcePool {
            Id = id, Name = source.Name, SeedName = source.SeedName, IconDefName = source.IconDefName,
            Members = new List<string>(source.Members) };

        private static ReadoutGroup CopyGroup(ReadoutGroup source, int id, int order, Dictionary<string, int> ids)
        {
            var result = new ReadoutGroup { Id = id, Name = source.Name, SeedName = source.SeedName,
                OrderIndex = order, DefaultEnabled = source.DefaultEnabled };
            foreach (var tier in source.Tiers)
            {
                var tokens = new List<string>();
                foreach (string token in tier) tokens.Add(Resolve(token, ids));
                result.Tiers.Add(tokens);
            }
            return result;
        }
    }
}
