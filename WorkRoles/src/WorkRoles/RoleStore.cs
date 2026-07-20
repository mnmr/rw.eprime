using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public class RoleStore : WorldComponent
    {
        /// Mutate only via RoleCommands — direct writes bypass cache invalidation.
        public List<Role> roles = new List<Role>();
        /// Mutate only via RoleCommands — direct writes bypass cache invalidation.
        public Dictionary<Pawn, PawnRoleSet> pawnSets = new Dictionary<Pawn, PawnRoleSet>();
        public bool seeded;
        /// Default training paths seeded (only ever set alongside role seeding;
        /// pre-existing saves adopt them via Restore Defaults instead).
        public bool pathsSeeded;
        /// GetPriority reports the vanilla 0-4 projection instead of raw ranks.
        /// World state, not a mod setting: other mods consume the values in
        /// sim-relevant code, so MP clients must agree.
        public bool reportVanillaPriorities = true;
        /// The user's recommendation order template (role ids); empty = the
        /// vanilla-grid-derived default. A pure override: unlisted roles are
        /// not merged in — they place dynamically (RecommendationOrder).
        public List<int> recommendationOrder = new List<int>();
        /// Legacy scribe slot: very old saves carry the hidden All role here;
        /// PostLoadInit migrates it into the catalog as an ordinary role.
        public Role allRole;
        public List<string> knownWorkTypes = new List<string>();
        /// Custom swatch slot count: the editor's two rows of 19.
        public const int MaxCustomSwatches = 38;
        /// Player-defined swatch slots for the role editor (alpha 0 = empty slot).
        public List<UnityEngine.Color> customSwatches = new List<UnityEngine.Color>();
        /// Slot names (index-aligned with customSwatches): auto-named "custom-N",
        /// renamed only by editing an export file — imported names stick. Used by
        /// export/import to merge palettes by name.
        public List<string> customSwatchNames = new List<string>();
        /// Per-bill role restrictions (see BillRoles). Mutate via RoleCommands.
        public Dictionary<Bill, int> billRoles = NewBillRoleDictionary();
        /// Role-list groups in display order. Mutate via RoleCommands.
        public List<RoleGroup> groups = new List<RoleGroup>();
        /// Named training paths (Options tab). Mutate via RoleCommands.
        public List<TrainingPath> trainingPaths = new List<TrainingPath>();
        private int nextRoleId = 1;
        private int nextGroupId = 1; // 0 reserved for the Default group
        private int nextPathId = 1;

        private List<Pawn> pawnKeysWorkingList;
        private List<PawnRoleSet> setValuesWorkingList;
        private List<Bill> billKeysWorkingList;
        private List<int> billValuesWorkingList;

        private static RoleStore cached;

        private static Dictionary<Bill, int> NewBillRoleDictionary() =>
            new Dictionary<Bill, int>(ReferenceIdentityComparer<Bill>.Instance);

        private void EnsureBillRoleIdentityComparer()
        {
            if (billRoles == null)
            {
                billRoles = NewBillRoleDictionary();
                return;
            }
            if (ReferenceEquals(billRoles.Comparer,
                    ReferenceIdentityComparer<Bill>.Instance)) return;

            var loaded = billRoles;
            billRoles = NewBillRoleDictionary();
            foreach (var mapping in loaded)
                billRoles[mapping.Key] = mapping.Value;
        }

        public RoleStore(World world) : base(world)
        {
            cached = this;
        }

        public static RoleStore Current
        {
            get
            {
                var world = Find.World;
                if (world == null) return null;
                if (cached == null || cached.world != world)
                    cached = world.GetComponent<RoleStore>();
                return cached;
            }
        }

        /// World teardown: drop the reference so the old world graph (pawn and
        /// bill keyed maps) is collectable while the player sits in the menu.
        internal static void ClearCached() => cached = null;

        public int NextId() => nextRoleId++;

        public int NextGroupId() => nextGroupId++;

        public int NextPathId() => nextPathId++;

        public RoleGroup GroupById(int id) => groups.FirstOrDefault(g => g.id == id);

        public RoleGroup GroupByName(string name) => groups.FirstOrDefault(g =>
            string.Equals(g.label, name?.Trim(), System.StringComparison.OrdinalIgnoreCase));

        public TrainingPath PathById(int id) =>
            trainingPaths.FirstOrDefault(p => p.id == id);

        /// The Default group (id 0), materialized on demand: pinned first,
        /// swept like any user group when it empties. The stored label is
        /// INVARIANT (never the translated name — this is scribed, synced
        /// state); the UI renders id 0 from the keyed string.
        public RoleGroup EnsureDefaultGroup()
        {
            var group = GroupById(RoleGroup.DefaultId);
            if (group == null)
            {
                group = new RoleGroup { id = RoleGroup.DefaultId, label = GroupNameRules.DefaultName };
                groups.Insert(0, group);
            }
            return group;
        }

        /// Keeps slot names index-aligned with the swatch list (auto-name gaps).
        public void SyncSwatchNames()
        {
            while (customSwatchNames.Count < customSwatches.Count)
                customSwatchNames.Add($"custom-{customSwatchNames.Count + 1}");
            foreach (var i in Enumerable.Range(0, customSwatchNames.Count))
                if (customSwatchNames[i].NullOrEmpty())
                    customSwatchNames[i] = $"custom-{i + 1}";
        }

        // Hot path: chips resolve roles per visible row per GUI pass. The index
        // rebuilds lazily; every roles-list mutation calls InvalidateRoleIndex.
        private Dictionary<int, Role> roleIndex;

        internal void InvalidateRoleIndex() => roleIndex = null;

        public Role RoleById(int id)
        {
            if (roleIndex == null)
            {
                roleIndex = new Dictionary<int, Role>(roles.Count);
                foreach (var role in roles) roleIndex[role.id] = role;
            }
            return roleIndex.TryGetValue(id, out var found) ? found : null;
        }

        public Role RoleByTemplate(string templateDefName) =>
            roles.FirstOrDefault(r => r.templateDefName == templateDefName);

        public bool IsManaged(Pawn pawn) =>
            pawn != null && pawnSets.TryGetValue(pawn, out var set) && set.assignments.Count > 0;

        public PawnRoleSet SetFor(Pawn pawn)
        {
            if (!pawnSets.TryGetValue(pawn, out var set))
            {
                set = new PawnRoleSet();
                pawnSets[pawn] = set;
            }
            return set;
        }

        /// Returns vanilla authority to a managed pawn in one ordered transition:
        /// preserve the current projection, remove mod state, dirty vanilla's
        /// cached giver lists, then request UI invalidation. Callers performing a
        /// bulk command may supply a coalescing request action.
        internal bool UnmanagePawn(Pawn pawn, Action invalidateUi = null)
        {
            if (pawn == null || !pawnSets.TryGetValue(pawn, out var set)) return false;
            var requestUiInvalidation = invalidateUi ?? UiVersion.Bump;
            if (set.assignments.Count == 0)
            {
                pawnSets.Remove(pawn);
                CompiledJobOrders.RemoveCached(pawn);
                requestUiInvalidation();
                return true;
            }

            // A missing work-settings component means there is no vanilla priority
            // map or work-giver cache to restore. Removal remains necessary to avoid
            // stale managed state. When it exists, capture the authority up front so
            // both fallback mirroring and notification are always attempted.
            var workSettings = pawn.workSettings;
            PawnManagementLifecycle.Unmanage(
                hasVanillaWorkSettings: workSettings != null,
                mirrorFallback: () => CompiledJobOrders.MirrorFreshVanillaFallback(pawn),
                removeManagedState: () =>
                {
                    pawnSets.Remove(pawn);
                    CompiledJobOrders.RemoveCached(pawn);
                },
                notifyVanilla: () => workSettings.Notify_UseWorkPrioritiesChanged(),
                invalidateUi: requestUiInvalidation);
            return true;
        }

        public IEnumerable<Pawn> PawnsWithRole(int roleId) =>
            pawnSets.Where(kv => kv.Value.assignments.Any(a => a.roleId == roleId)).Select(kv => kv.Key);

        internal bool RemoveBillRole(Bill bill)
        {
            return bill != null && billRoles != null && billRoles.Remove(bill);
        }

        internal bool SetBillRole(Bill bill, int roleId)
        {
            if (bill == null) return false;
            if (roleId < 0 || RoleById(roleId) == null)
                return RemoveBillRole(bill);
            EnsureBillRoleIdentityComparer();
            billRoles[bill] = roleId;
            return true;
        }

        internal int RemoveBillRolesForRole(int roleId)
        {
            if (billRoles == null || billRoles.Count == 0) return 0;
            List<Bill> candidates = null;
            foreach (var mapping in billRoles)
                if (mapping.Value == roleId)
                {
                    candidates ??= new List<Bill>();
                    candidates.Add(mapping.Key);
                }
            if (candidates == null) return 0;

            int removed = 0;
            foreach (Bill bill in candidates)
                if (RemoveBillRole(bill)) removed++;
            return removed;
        }

        /// Captures only mapped bills belonging to a stack. The list remains null
        /// when no cleanup can occur, so the common RemoveIncompletableBills path
        /// adds no allocation for unrestricted or still-completable bills.
        internal List<Bill> CaptureBillRolesForStack(BillStack stack,
            bool onlyIncompletable = false)
        {
            if (stack == null || billRoles == null || billRoles.Count == 0) return null;
            List<Bill> candidates = null;
            foreach (Bill bill in billRoles.Keys)
            {
                if (bill == null || !ReferenceEquals(bill.billStack, stack)) continue;
                if (onlyIncompletable && !bill.deleted && bill.CompletableEver) continue;
                candidates ??= new List<Bill>();
                candidates.Add(bill);
            }
            return candidates;
        }

        internal int RemoveCapturedBillRolesMissingFromStack(BillStack stack,
            List<Bill> candidates)
        {
            if (candidates == null || candidates.Count == 0) return 0;
            int removed = 0;
            foreach (Bill bill in candidates)
                if (!BillStackContainsReference(stack, bill) && RemoveBillRole(bill))
                    removed++;
            return removed;
        }

        internal int RemoveBillRolesForStack(BillStack stack)
        {
            List<Bill> candidates = CaptureBillRolesForStack(stack);
            if (candidates == null) return 0;
            int removed = 0;
            foreach (Bill bill in candidates)
                if (RemoveBillRole(bill)) removed++;
            return removed;
        }

        internal int SweepBillRoles(IEnumerable<Bill> liveBills)
        {
            if (billRoles == null || billRoles.Count == 0) return 0;
            IReadOnlyList<Bill> stale = IdentityKeySweepPlanner.StaleKeys(
                billRoles.Keys, liveBills ?? Array.Empty<Bill>());
            int removed = 0;
            foreach (Bill bill in stale)
                if (RemoveBillRole(bill)) removed++;
            return removed;
        }

        internal static bool BillStackContainsReference(BillStack stack, Bill bill)
        {
            List<Bill> bills = stack?.Bills;
            if (bills == null || bill == null) return false;
            for (int i = 0; i < bills.Count; i++)
                if (ReferenceEquals(bills[i], bill)) return true;
            return false;
        }

        private void SweepBillRolesBeforeSave()
        {
            if (billRoles == null || billRoles.Count == 0) return;
            var live = new HashSet<Bill>(ReferenceIdentityComparer<Bill>.Instance);

            // Shipped 1.6 IBillGiver implementations are Pawn, Corpse, and
            // Building_WorkTable. Map inventory covers all spawned owners.
            List<Map> maps = Find.Maps;
            if (maps != null)
                for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
                {
                    Map map = maps[mapIndex];
                    if (map?.listerThings == null) continue;
                    List<Thing> things = map.listerThings.AllThings;
                    if (things == null) continue;
                    for (int thingIndex = 0; thingIndex < things.Count; thingIndex++)
                        if (things[thingIndex] is IBillGiver giver)
                            AddLiveBills(giver, live);
                }

            // Pawns can own surgery bills while in world storage, caravans,
            // travelling transporters, temporary holders, or the current gravship.
            List<Pawn> pawns = Find.World == null ? null : PawnsFinder.All_AliveOrDead;
            if (pawns != null)
                for (int i = 0; i < pawns.Count; i++)
                    AddLiveBills(pawns[i], live);

            // A carried corpse or mod-defined non-map owner may not appear in the
            // inventories above. Preserve a mapped bill only when its own live,
            // non-destroyed owner stack still contains that exact reference.
            foreach (Bill bill in billRoles.Keys)
                if (IsAttachedToLiveOwner(bill)) live.Add(bill);

            SweepBillRoles(live);
        }

        private static void AddLiveBills(IBillGiver giver, HashSet<Bill> live)
        {
            if (giver == null || live == null) return;
            if (giver is Thing owner && owner.Destroyed) return;
            List<Bill> bills = giver.BillStack?.Bills;
            if (bills == null) return;
            for (int i = 0; i < bills.Count; i++)
            {
                Bill bill = bills[i];
                if (bill != null && !bill.deleted) live.Add(bill);
            }
        }

        private static bool IsAttachedToLiveOwner(Bill bill)
        {
            if (bill == null || bill.deleted) return false;
            BillStack stack = bill.billStack;
            IBillGiver giver = stack?.billGiver;
            if (giver == null || giver is Thing owner && owner.Destroyed) return false;
            return BillStackContainsReference(stack, bill);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // The saved vanilla priority maps double as the mod-removal fallback —
                // make sure every managed pawn's projection is current before writing.
                // Empty sets are skipped: an unmanaged pawn's projection is empty and
                // syncing it would zero their real vanilla priorities.
                foreach (var kv in pawnSets)
                    if (kv.Key != null && !kv.Key.Destroyed && kv.Value.assignments.Count > 0)
                        CompiledJobOrders.EnsureFresh(kv.Key);
                SweepBillRolesBeforeSave();
            }
            Scribe_Values.Look(ref seeded, "seeded");
            Scribe_Values.Look(ref pathsSeeded, "pathsSeeded");
            Scribe_Values.Look(ref reportVanillaPriorities, "reportVanillaPriorities", true);
            Scribe_Collections.Look(ref recommendationOrder, "recommendationOrder", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars && recommendationOrder == null)
                recommendationOrder = new List<int>();
            Scribe_Deep.Look(ref allRole, "allRole");
            Scribe_Values.Look(ref nextRoleId, "nextRoleId", 1);
            Scribe_Collections.Look(ref roles, "roles", LookMode.Deep);
            Scribe_Values.Look(ref nextGroupId, "nextGroupId", 1);
            Scribe_Collections.Look(ref groups, "groups", LookMode.Deep);
            Scribe_Collections.Look(ref knownWorkTypes, "knownWorkTypes", LookMode.Value);
            Scribe_Collections.Look(ref customSwatches, "customSwatches", LookMode.Value);
            Scribe_Collections.Look(ref customSwatchNames, "customSwatchNames", LookMode.Value);
            Scribe_Collections.Look(ref pawnSets, "pawnSets", LookMode.Reference, LookMode.Deep,
                ref pawnKeysWorkingList, ref setValuesWorkingList);
            Scribe_Collections.Look(ref billRoles, "billRoles", LookMode.Reference, LookMode.Value,
                ref billKeysWorkingList, ref billValuesWorkingList);
            // Scribe replaces dictionaries with its default comparer in LoadingVars
            // and fills reference-keyed maps only in ResolvingCrossRefs. Replace the
            // still-empty shell now; the working lists remain owned by Scribe.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                billRoles = NewBillRoleDictionary();
            Scribe_Values.Look(ref nextPathId, "nextPathId", 1);
            Scribe_Collections.Look(ref trainingPaths, "trainingPaths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                roles ??= new List<Role>();
                groups ??= new List<RoleGroup>();
                knownWorkTypes ??= new List<string>();
                customSwatches ??= new List<UnityEngine.Color>();
                customSwatchNames ??= new List<string>();
                SyncSwatchNames();
                pawnSets ??= new Dictionary<Pawn, PawnRoleSet>();
                pawnSets.RemoveAll(kv => kv.Key == null || kv.Value == null);
                EnsureBillRoleIdentityComparer();
                // Bill.DeletedOrDereferenced dereferences billStack without a null
                // guard in 1.6. Remove only definitely dead bill references here;
                // role-id sanitation waits until legacy allRole has migrated.
                billRoles.RemoveAll(kv => kv.Key == null || kv.Key.deleted);
                trainingPaths ??= new List<TrainingPath>();
                // Empty paths survive (named containers); only non-empty corrupt geometry drops.
                trainingPaths.RemoveAll(p =>
                    p == null
                    || (p.roleIds.Count > 0 && !SkillProgressionMath.Validate(p.roleIds.Count, p.bandMins, p.bandMaxes)));
                foreach (var path in trainingPaths)
                    if (path.roleIds.Count == 0 && (path.bandMins.Count > 0 || path.bandMaxes.Count > 0))
                    {
                        path.bandMins.Clear();
                        path.bandMaxes.Clear();
                    }
                // Migration: the once-hidden All role becomes an ordinary catalog
                // role, assigned to every managed pawn at the last position (its
                // old implicit spot).
                if (allRole != null)
                {
                    allRole.autoAssign = true;
                    allRole.label = "WR_OddJobsRole".Translate();
                    roles.Add(allRole);
                    InvalidateRoleIndex();
                    foreach (var set in pawnSets.Values)
                        if (set.assignments.Count > 0 && set.assignments.All(a => a.roleId != allRole.id))
                            set.assignments.Add(new RoleAssignment { roleId = allRole.id });
                    allRole = null;
                }
                // A bill could legitimately reference the hidden legacy All role.
                // Only ids still unresolved after migration are corrupt.
                billRoles.RemoveAll(kv => RoleById(kv.Value) == null);
                // Corrupt-save hygiene (after the allRole migration, so its id
                // resolves): assignments referencing deleted roles are inert but
                // count as managed; drop them and any set they empty.
                foreach (var set in pawnSets.Values)
                    set.assignments?.RemoveAll(a => RoleById(a.roleId) == null);
                pawnSets.RemoveAll(kv => kv.Value.assignments == null || kv.Value.assignments.Count == 0);
                CompiledJobOrders.InvalidateAll();
            }
        }
    }
}
