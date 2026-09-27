using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    public sealed class ReadoutGroup
    {
        public int Id;
        public string Name = "";
        public int OrderIndex;
        public List<List<string>> Tiers = new List<List<string>>();
        public int TierCount => Tiers.Count;
        public bool DefaultEnabled = true;
    }

    /// Group names are unique under the same normalization and comparison as
    /// pool names, in their own namespace.
    public static class ReadoutGroupNames
    {
        public const string FallbackName = "Group";
        public static StringComparer Comparer => PoolNameRules.Comparer;
        public static string Normalize(string? name) => PoolNameRules.Normalize(name);
    }

    public readonly struct ThresholdSpec
    {
        public readonly int Low;
        public readonly int Critical;
        public ThresholdSpec(int low, int critical) { Low = low; Critical = critical; }
    }

    /// <summary>
    /// Shared readout definition: groups, pools, and global thresholds. Pure
    /// state and operations; persistence and MP sync live in the game assembly,
    /// which routes every mutation through these methods.
    /// </summary>
    public sealed class ReadoutModel
    {
        public List<ReadoutGroup> Groups = new List<ReadoutGroup>();
        public List<ResourcePool> Pools = new List<ResourcePool>();
        public Dictionary<string, ThresholdSpec> Thresholds = new Dictionary<string, ThresholdSpec>();
        /// Per-token count-basis overrides, keyed like Thresholds by canonical
        /// token. Absent means fully inherit the player's global options.
        public Dictionary<string, CountRule> CountRules = new Dictionary<string, CountRule>();

        public ReadoutGroup? GroupById(int id)
        {
            foreach (var group in Groups)
                if (group.Id == id) return group;
            return null;
        }

        public List<ReadoutGroup> InDisplayOrder()
        {
            var sorted = new List<ReadoutGroup>(Groups);
            sorted.Sort((a, b) => a.OrderIndex.CompareTo(b.OrderIndex));
            return sorted;
        }

        public ReadoutGroup? GroupByName(string? name)
        {
            string normalized = ReadoutGroupNames.Normalize(name);
            if (normalized.Length == 0) return null;
            foreach (var group in Groups)
                if (ReadoutGroupNames.Comparer.Equals(group.Name, normalized)) return group;
            return null;
        }

        /// True when a normalized, non-empty name is unused by every group
        /// other than <paramref name="exceptGroupId"/>. Unique names let the
        /// UI find a group it just created by name, even when the synced
        /// create lands frames later in multiplayer.
        public bool CanUseGroupName(string? name, int exceptGroupId = -1)
        {
            string normalized = ReadoutGroupNames.Normalize(name);
            if (normalized.Length == 0) return false;
            foreach (var group in Groups)
                if (group.Id != exceptGroupId
                    && ReadoutGroupNames.Comparer.Equals(group.Name, normalized))
                    return false;
            return true;
        }

        public ReadoutGroup CreateGroup(int id, string name)
        {
            string normalized = ReadoutGroupNames.Normalize(name);
            if (!CanUseGroupName(normalized))
                throw new InvalidOperationException(
                    "Group name \"" + normalized + "\" is empty or already in use.");
            int maxOrder = -1;
            foreach (var group in Groups)
                if (group.OrderIndex > maxOrder) maxOrder = group.OrderIndex;
            var created = new ReadoutGroup { Id = id, Name = normalized, OrderIndex = maxOrder + 1 };
            Groups.Add(created);
            return created;
        }

        public bool RenameGroup(int id, string name)
        {
            var group = GroupById(id);
            if (group == null) return false;
            string nextName = ReadoutGroupNames.Normalize(name);
            if (!CanUseGroupName(nextName, id)) return false;
            if (group.Name == nextName) return false;
            group.Name = nextName;
            return true;
        }

        /// Renames later duplicates (display order) and blank names with a
        /// " (n)" suffix no other group uses. Deterministic for the same
        /// groups, so load and synced import converge on every client.
        private void MakeGroupNamesUnique(List<ReadoutGroup> ordered)
        {
            var reserved = new HashSet<string>(ReadoutGroupNames.Comparer);
            foreach (var group in ordered)
            {
                string normalized = ReadoutGroupNames.Normalize(group.Name);
                if (normalized.Length != 0) reserved.Add(normalized);
            }

            var claimed = new HashSet<string>(ReadoutGroupNames.Comparer);
            foreach (var group in ordered)
            {
                string baseName = ReadoutGroupNames.Normalize(group.Name);
                string unique = baseName;
                if (baseName.Length == 0 || !claimed.Add(baseName))
                {
                    if (baseName.Length == 0) baseName = ReadoutGroupNames.FallbackName;
                    unique = baseName;
                    int suffix = 2;
                    while (!reserved.Add(unique))
                        unique = baseName + " (" + suffix++ + ")";
                    claimed.Add(unique);
                }
                group.Name = unique;
            }
        }

        public bool DeleteGroup(int id)
        {
            var group = GroupById(id);
            if (group == null) return false;
            Groups.Remove(group);
            return true;
        }

        /// <summary>
        /// Swaps display position with the neighbor delta steps away
        /// (-1 = toward the top of the readout, +1 = toward the bottom).
        /// </summary>
        public bool ReorderGroup(int id, int delta)
        {
            var order = InDisplayOrder();
            int index = order.FindIndex(g => g.Id == id);
            int target = index + delta;
            if (index < 0 || target < 0 || target >= order.Count || target == index) return false;
            (order[index].OrderIndex, order[target].OrderIndex) =
                (order[target].OrderIndex, order[index].OrderIndex);
            return true;
        }

        /// <summary>
        /// Repositions the group identified by <paramref name="id"/> so that
        /// it appears at <paramref name="targetDisplayIndex"/> in display order
        /// (clamped to 0..count-1). Afterwards all groups' OrderIndex values
        /// are normalized to 0..n-1 matching the new display order.
        /// Returns false when the id is unknown.
        /// </summary>
        public bool MoveGroupTo(int id, int targetDisplayIndex)
        {
            var group = GroupById(id);
            if (group == null) return false;
            var order = InDisplayOrder();
            int count = order.Count;
            if (count <= 1) return false;
            int clampedTarget = Math.Max(0, Math.Min(count - 1, targetDisplayIndex));
            int currentIndex = order.IndexOf(group);
            if (currentIndex == clampedTarget) return false;
            // Remove from current position, insert at target
            order.Remove(group);
            order.Insert(clampedTarget, group);
            // Normalize all OrderIndexes to 0..n-1
            for (int i = 0; i < order.Count; i++)
                order[i].OrderIndex = i;
            return true;
        }

        public bool SetTiers(int id, List<List<string>> tiers)
        {
            var group = GroupById(id);
            if (group == null || tiers == null || tiers.Count > TierOps.MaxTiers) return false;
            var nextTiers = TierOps.Clone(tiers);
            TierOps.Compact(nextTiers);
            if (TiersEqual(group.Tiers, nextTiers)) return false;
            group.Tiers = nextTiers;
            return true;
        }

        public bool SetThreshold(string defName, int low, int critical)
        {
            if (Thresholds.TryGetValue(defName, out var current)
                && current.Low == low && current.Critical == critical)
                return false;
            Thresholds[defName] = new ThresholdSpec(low, critical);
            return true;
        }

        public bool ClearThreshold(string defName) => Thresholds.Remove(defName);

        /// Sets or clears the per-token count-basis override. A fully-inherit
        /// rule removes the entry (absent == inherit). Returns false when
        /// nothing changed.
        public bool SetCountRule(string token, CountRule rule)
        {
            if (rule.IsInherit) return CountRules.Remove(token);
            if (CountRules.TryGetValue(token, out var current)
                && current.Equals(rule))
                return false;
            CountRules[token] = rule;
            return true;
        }

        // ── Intent edits ──────────────────────────────────────────────────
        // Synced edits land frames after the click in multiplayer, so a second
        // click is built from a model the first has not changed yet. These
        // carry the intent and apply it to the model as it is when they run,
        // so quick successive edits all survive and a repeated intent is a
        // no-op rather than a toggle back.

        /// Sets one count-rule option, keeping the other as it currently is.
        public bool SetCountRuleStorageOnly(string token, BasisOverride state)
        {
            CountRules.TryGetValue(token, out CountRule current);
            return SetCountRule(token, new CountRule(state, current.HideForbidden));
        }

        public bool SetCountRuleHideForbidden(string token, BasisOverride state)
        {
            CountRules.TryGetValue(token, out CountRule current);
            return SetCountRule(token, new CountRule(current.StorageOnly, state));
        }

        /// Adds a token at (tier, slot); tier -1 appends to the last tier, or
        /// the next one when it is full. Refused when the token is present.
        public bool AddGroupSlot(int groupId, string token, int tier, int slot)
        {
            var group = GroupById(groupId);
            if (group == null) return false;
            var tiers = TierOps.Clone(group.Tiers);
            if (tier < 0)
            {
                tier = tiers.Count == 0 ? 0 : tiers.Count - 1;
                if (tier < tiers.Count && tiers[tier].Count >= TierOps.MaxSlotsPerTier)
                    tier++;
                slot = -1;
            }
            return TierOps.Add(tiers, token, tier, slot) && SetTiers(groupId, tiers);
        }

        public bool RemoveGroupSlot(int groupId, string token)
        {
            var group = GroupById(groupId);
            if (group == null) return false;
            var tiers = TierOps.Clone(group.Tiers);
            return TierOps.Remove(tiers, token) && SetTiers(groupId, tiers);
        }

        /// Moves the token (found by canonical form, wherever it now is) to
        /// (toTier, toSlot) as positioned in the layout the drop was aimed at.
        public bool MoveGroupSlot(int groupId, string token, int toTier, int toSlot)
        {
            var group = GroupById(groupId);
            if (group == null) return false;
            if (!FindSlot(group.Tiers, token, out int fromTier, out int fromSlot))
                return false;
            var tiers = TierOps.Clone(group.Tiers);
            return TierOps.Move(tiers, fromTier, fromSlot, toTier, toSlot)
                && SetTiers(groupId, tiers);
        }

        public bool SetGroupSlotShowWhenZero(int groupId, string token, bool show)
        {
            var group = GroupById(groupId);
            if (group == null) return false;
            if (!FindSlot(group.Tiers, token, out int tier, out int slot)) return false;
            var tiers = TierOps.Clone(group.Tiers);
            tiers[tier][slot] = SlotToken.WithShowWhenZero(tiers[tier][slot], show);
            return SetTiers(groupId, tiers);
        }

        private static bool FindSlot(List<List<string>> tiers, string token,
            out int tier, out int slot)
        {
            string canonical = SlotToken.Canonical(token);
            for (tier = 0; tier < tiers.Count; tier++)
                for (slot = 0; slot < tiers[tier].Count; slot++)
                    if (SlotToken.Canonical(tiers[tier][slot]) == canonical) return true;
            tier = slot = -1;
            return false;
        }

        public bool SetPoolMemberSelected(int poolId, string defName, bool selected,
            IResourceCatalog catalog)
        {
            var pool = PoolById(poolId);
            if (pool == null) return false;
            return SetPoolMembers(poolId,
                PoolTriState.SetDef(pool.Members, defName, selected, catalog));
        }

        public bool SetPoolCategoryScopeSelected(int poolId, string categoryDefName,
            IReadOnlyList<string> scopedDefs, bool selected, IResourceCatalog catalog)
        {
            var pool = PoolById(poolId);
            if (pool == null) return false;
            return SetPoolMembers(poolId, PoolTriState.SetCategoryScope(
                pool.Members, categoryDefName, scopedDefs, selected, catalog));
        }

        // ── Pool operations ───────────────────────────────────────────────

        public ResourcePool? PoolById(int id)
        {
            foreach (var pool in Pools)
                if (pool.Id == id) return pool;
            return null;
        }

        public ResourcePool? PoolByName(string name)
        {
            string normalized = PoolNameRules.Normalize(name);
            if (normalized.Length == 0) return null;
            foreach (var pool in Pools)
                if (PoolNameRules.Comparer.Equals(pool.Name, normalized)) return pool;
            return null;
        }

        /// <summary>
        /// True when a normalized, non-empty name is unused by every pool other
        /// than <paramref name="exceptPoolId"/>. Resource and category names are
        /// intentionally outside this namespace.
        /// </summary>
        public bool CanUsePoolName(string? name, int exceptPoolId = -1)
        {
            string normalized = PoolNameRules.Normalize(name);
            if (normalized.Length == 0) return false;
            foreach (var pool in Pools)
                if (pool.Id != exceptPoolId
                    && PoolNameRules.Comparer.Equals(pool.Name, normalized))
                    return false;
            return true;
        }

        /// Adds a new pool with the given id and name, returns it. The pools
        /// list stays name-sorted.
        public ResourcePool CreatePool(int id, string name)
        {
            string normalized = PoolNameRules.Normalize(name);
            if (normalized.Length == 0)
                throw new ArgumentException("Pool name must not be empty.", nameof(name));
            if (!CanUsePoolName(normalized))
                throw new InvalidOperationException(
                    "A resource pool named \"" + normalized + "\" already exists.");
            var pool = new ResourcePool { Id = id, Name = normalized };
            Pools.Add(pool);
            SortPools();
            return pool;
        }

        public bool RenamePool(int id, string name)
        {
            var pool = PoolById(id);
            if (pool == null) return false;
            string nextName = PoolNameRules.Normalize(name);
            if (nextName.Length == 0 || !CanUsePoolName(nextName, id)) return false;
            if (pool.Name == nextName) return false;
            pool.Name = nextName;
            SortPools();
            return true;
        }

        private string MakeUniquePoolName(string? requestedName)
        {
            string baseName = PoolNameRules.Normalize(requestedName);
            if (baseName.Length == 0) baseName = PoolNameRules.LegacyFallbackName;
            if (CanUsePoolName(baseName)) return baseName;

            int suffix = 2;
            while (true)
            {
                string candidate = baseName + " (" + suffix + ")";
                if (CanUsePoolName(candidate)) return candidate;
                suffix++;
            }
        }

        private void NormalizeLegacyPoolNames()
        {
            var normalizedNames = new string[Pools.Count];
            var reservedNames = new HashSet<string>(PoolNameRules.Comparer);
            for (int i = 0; i < Pools.Count; i++)
            {
                string normalized = PoolNameRules.Normalize(Pools[i].Name);
                normalizedNames[i] = normalized;
                if (normalized.Length != 0) reservedNames.Add(normalized);
            }

            var claimedNames = new HashSet<string>(PoolNameRules.Comparer);
            bool changed = false;
            for (int i = 0; i < Pools.Count; i++)
            {
                ResourcePool pool = Pools[i];
                string baseName = normalizedNames[i];
                string uniqueName;
                if (baseName.Length != 0 && claimedNames.Add(baseName))
                {
                    uniqueName = baseName;
                }
                else
                {
                    if (baseName.Length == 0) baseName = PoolNameRules.LegacyFallbackName;
                    uniqueName = baseName;
                    int suffix = 2;
                    while (!reservedNames.Add(uniqueName))
                        uniqueName = baseName + " (" + suffix++ + ")";
                }

                if (pool.Name == uniqueName) continue;
                pool.Name = uniqueName;
                changed = true;
            }
            if (changed) SortPools();
        }

        /// Pools are kept name-sorted (case-insensitive, id tie-break) as a
        /// list invariant — deterministic, so safe inside sync commands.
        private void SortPools() =>
            Pools.Sort((a, b) =>
            {
                int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : a.Id.CompareTo(b.Id);
            });

        /// Deletes the pool and purges all #id tokens (including ~#id) from
        /// every group's tiers, then removes threshold entries keyed "#id".
        public bool DeletePool(int id) => DeletePool(id, out _);

        public bool DeletePool(int id, out ReadoutChange change)
        {
            change = ReadoutChange.None;
            var pool = PoolById(id);
            if (pool == null) return false;
            Pools.Remove(pool);
            change = ReadoutChange.Pools;

            // Build the canonical token string to match against
            string canonicalToken = SlotToken.PoolToken(id); // "#id"

            foreach (var group in Groups)
            {
                bool groupChanged = false;
                foreach (var tier in group.Tiers)
                    groupChanged |= tier.RemoveAll(
                        t => SlotToken.Canonical(t) == canonicalToken) > 0;
                if (groupChanged)
                {
                    TierOps.Compact(group.Tiers);
                    change |= ReadoutChange.Groups;
                }
            }

            // Remove threshold and count rule keyed "#id"
            if (Thresholds.Remove(canonicalToken))
                change |= ReadoutChange.Thresholds;
            if (CountRules.Remove(canonicalToken))
                change |= ReadoutChange.CountRules;
            return true;
        }

        /// Replaces the pool's member list with a clone of the supplied list.
        public bool SetPoolMembers(int id, List<string> members)
        {
            var pool = PoolById(id);
            if (pool == null) return false;
            if (ListEqual(pool.Members, members)) return false;
            pool.Members = members != null ? new List<string>(members) : new List<string>();
            return true;
        }

        /// Sets the explicit icon def name for the pool.
        public bool SetPoolIcon(int id, string? defName)
        {
            var pool = PoolById(id);
            if (pool == null) return false;
            string? currentDefName = string.IsNullOrEmpty(pool.IconDefName) ? null : pool.IconDefName;
            string? nextDefName = string.IsNullOrEmpty(defName) ? null : defName;
            if (currentDefName == nextDefName) return false;
            pool.IconDefName = nextDefName;
            return true;
        }

        private static bool TiersEqual(List<List<string>> left, List<List<string>> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int tier = 0; tier < left.Count; tier++)
                if (!ListEqual(left[tier], right[tier])) return false;
            return true;
        }

        private static bool ListEqual(List<string>? left, List<string>? right)
        {
            int leftCount = left != null ? left.Count : 0;
            int rightCount = right != null ? right.Count : 0;
            if (leftCount != rightCount) return false;
            for (int i = 0; i < leftCount; i++)
                if (left![i] != right![i]) return false; // count > 0 => lists exist.
            return true;
        }

        // ── Cleanup ───────────────────────────────────────────────────────

        /// <summary>
        /// Load-time cleanup: purge tokens that no longer resolve (per
        /// <paramref name="tokenValid"/>), compact tiers, drop stale
        /// thresholds. Also purges pool members that fail
        /// <paramref name="memberValid"/> (pools themselves are kept even when
        /// empty — they are user-owned). Deterministic for a given save plus
        /// def set, so MP clients converge without any syncing.
        /// <para>
        /// The game passes "#id" tokens through <paramref name="tokenValid"/>
        /// as raw tokens — the predicate resolves pool existence. For members,
        /// plain defNames are checked via def existence; "@Category" refs via
        /// category existence.
        /// </para>
        /// </summary>
        public void CleanupMissing(Func<string, bool> tokenValid, Func<string, bool> memberValid)
        {
            NormalizeLegacyPoolNames();
            MakeGroupNamesUnique(InDisplayOrder());
            foreach (var group in Groups) TierOps.Cleanup(group.Tiers, tokenValid);

            var stale = new List<string>();
            foreach (var key in Thresholds.Keys)
                if (!tokenValid(key)) stale.Add(key);
            foreach (var key in stale) Thresholds.Remove(key);

            stale.Clear();
            foreach (var key in CountRules.Keys)
                if (!tokenValid(key)) stale.Add(key);
            foreach (var key in stale) CountRules.Remove(key);

            foreach (var pool in Pools)
                pool.Members.RemoveAll(m => !memberValid(m));
        }

        // ── Import ───────────────────────────────────────────────────────────

        /// <summary>
        /// Overwrite-import: clears Pools, Groups and Thresholds, then recreates
        /// pools (new ids) and groups (new ids, file order = display order),
        /// resolving "pool:Name" slot tokens to "#id" (flag preserved; refs to
        /// unknown or duplicate normalized pool names are rejected before any
        /// mutation). Deterministic given the same xml + id allocators, so it is
        /// MP-sync safe.
        /// <para>
        /// Pool-name lookup trims names and uses ordinal, case-insensitive
        /// comparison, matching create, rename, migration, and export.
        /// </para>
        /// </summary>
        public ReadoutChange ApplyImport(
            List<ResourcePool> pools,
            List<ReadoutGroup> groups,
            Func<int> takePoolId,
            Func<int> takeGroupId)
        {
            if (!ReadoutsXml.TryValidatePortableModel(pools, groups, out _))
                return ReadoutChange.None;

            ReadoutChange change = ReadoutChange.None;
            if (Pools.Count > 0 || (pools != null && pools.Count > 0))
                change |= ReadoutChange.Pools;
            if (Groups.Count > 0 || (groups != null && groups.Count > 0))
                change |= ReadoutChange.Groups;
            if (Thresholds.Count > 0)
                change |= ReadoutChange.Thresholds;
            if (CountRules.Count > 0)
                change |= ReadoutChange.CountRules;

            Pools.Clear();
            Groups.Clear();
            Thresholds.Clear();
            CountRules.Clear();

            // Create pools with real ids; names were validated before mutation.
            var nameToId = new Dictionary<string, int>(PoolNameRules.Comparer);
            if (pools != null)
            {
                foreach (var imported in pools)
                {
                    int newId = takePoolId();
                    var pool = new ResourcePool
                    {
                        Id = newId,
                        Name = PoolNameRules.Normalize(imported.Name),
                        IconDefName = imported.IconDefName,
                        Members = imported.Members != null
                            ? new List<string>(imported.Members)
                            : new List<string>(),
                    };
                    Pools.Add(pool);
                    nameToId.Add(pool.Name, newId);
                }
                SortPools();
            }

            // Create groups, resolving "pool:Name" tokens and compacting
            if (groups != null)
            {
                int orderIndex = 0;
                foreach (var imported in groups)
                {
                    int newId = takeGroupId();
                    var group = new ReadoutGroup
                    {
                        Id = newId,
                        Name = imported.Name ?? "",
                        OrderIndex = orderIndex++,
                        DefaultEnabled = imported.DefaultEnabled,
                    };

                    if (imported.Tiers != null)
                    {
                        foreach (var importedTier in imported.Tiers)
                        {
                            var resolvedTier = new List<string>();
                            foreach (var token in importedTier)
                            {
                                if (string.IsNullOrEmpty(token)) continue;

                                if (ReadoutsXml.IsPortablePoolRef(token))
                                {
                                    // Resolve "pool:Name" → "#id" (flag preserved)
                                    string poolName = ReadoutsXml.PortablePoolName(token);
                                    int resolvedId = nameToId[poolName];
                                    bool flag = !SlotToken.ShowWhenZero(token);
                                    resolvedTier.Add(SlotToken.WithShowWhenZero(
                                        SlotToken.PoolToken(resolvedId), !flag));
                                }
                                else
                                {
                                    resolvedTier.Add(token);
                                }
                            }
                            if (resolvedTier.Count > 0)
                                group.Tiers.Add(resolvedTier);
                        }
                        TierOps.Compact(group.Tiers);
                    }

                    Groups.Add(group);
                }
                // Files exported before names were unique may repeat them.
                MakeGroupNamesUnique(Groups);
            }
            return change;
        }

        // ── Migration ─────────────────────────────────────────────────────

        /// <summary>
        /// Deterministic save migration: scans all groups in display order,
        /// replacing each "@Category" slot token with a "#poolId" token.
        /// Find-or-create: if a pool already exists whose Members == exactly
        /// [that @ref], reuse it; otherwise create a new one via
        /// <paramref name="takeId"/> and name it via
        /// <paramref name="nameForCategory"/>. Preserves the '~' flag.
        /// Returns true when at least one token was changed.
        /// </summary>
        public bool MigrateCategoryTokens(Func<int> takeId, Func<string, string> nameForCategory)
        {
            bool changed = false;
            foreach (var group in InDisplayOrder())
            {
                foreach (var tier in group.Tiers)
                {
                    for (int i = 0; i < tier.Count; i++)
                    {
                        string token = tier[i];
                        string canonical = SlotToken.Canonical(token);
                        if (!canonical.StartsWith("@")) continue;

                        // Find existing pool whose members == exactly [canonical]
                        ResourcePool? match = null;
                        foreach (var pool in Pools)
                        {
                            if (pool.Members.Count == 1 && pool.Members[0] == canonical)
                            {
                                match = pool;
                                break;
                            }
                        }

                        if (match == null)
                        {
                            string catName = canonical.Substring(1); // strip '@'
                            int newId = takeId();
                            string requestedName = nameForCategory != null
                                ? nameForCategory(catName)
                                : catName;
                            string poolName = MakeUniquePoolName(requestedName);
                            match = CreatePool(newId, poolName);
                            match.Members.Add(canonical);
                        }

                        bool showWhenZero = SlotToken.ShowWhenZero(token);
                        tier[i] = SlotToken.WithShowWhenZero(
                            SlotToken.PoolToken(match.Id), showWhenZero);
                        changed = true;
                    }
                }
            }
            return changed;
        }
    }
}
