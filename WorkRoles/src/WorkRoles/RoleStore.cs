using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WorkRoles
{
    public class RoleStore : WorldComponent
    {
        /// Mutate only via RoleCommands — direct writes bypass cache invalidation.
        public List<Role> roles = new List<Role>();
        /// Mutate only via RoleCommands — direct writes bypass cache invalidation.
        public Dictionary<Pawn, PawnRoleSet> pawnSets = new Dictionary<Pawn, PawnRoleSet>();
        public bool seeded;
        /// Engine-internal role carrying invisible modded work types; lives outside
        /// the catalog (never in roles, never shown in UI, never assigned to pawns).
        public Role allRole;
        public List<string> knownWorkTypes = new List<string>();
        /// Player-defined swatch slots for the role editor (alpha 0 = empty slot).
        public List<UnityEngine.Color> customSwatches = new List<UnityEngine.Color>();
        /// Per-bill role restrictions (see BillRoles). Mutate via RoleCommands.
        public Dictionary<Bill, int> billRoles = new Dictionary<Bill, int>();
        private int nextRoleId = 1;

        private List<Pawn> pawnKeysWorkingList;
        private List<PawnRoleSet> setValuesWorkingList;
        private List<Bill> billKeysWorkingList;
        private List<int> billValuesWorkingList;

        private static RoleStore cached;

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

        public int NextId() => nextRoleId++;

        public Role RoleById(int id) => roles.FirstOrDefault(r => r.id == id);

        /// Lazily creates the engine-internal All role (label is internal-only).
        public Role EnsureAllRole()
        {
            allRole ??= new Role { id = NextId(), label = "All" };
            return allRole;
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

        public IEnumerable<Pawn> PawnsWithRole(int roleId) =>
            pawnSets.Where(kv => kv.Value.assignments.Any(a => a.roleId == roleId)).Select(kv => kv.Key);

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // The saved vanilla priority maps double as the mod-removal fallback —
                // make sure every managed pawn's projection is current before writing.
                foreach (var pawn in pawnSets.Keys)
                    if (pawn != null && !pawn.Destroyed)
                        CompiledJobOrders.EnsureFresh(pawn);
            }
            Scribe_Values.Look(ref seeded, "seeded");
            Scribe_Deep.Look(ref allRole, "allRole");
            Scribe_Values.Look(ref nextRoleId, "nextRoleId", 1);
            Scribe_Collections.Look(ref roles, "roles", LookMode.Deep);
            Scribe_Collections.Look(ref knownWorkTypes, "knownWorkTypes", LookMode.Value);
            Scribe_Collections.Look(ref customSwatches, "customSwatches", LookMode.Value);
            Scribe_Collections.Look(ref pawnSets, "pawnSets", LookMode.Reference, LookMode.Deep,
                ref pawnKeysWorkingList, ref setValuesWorkingList);
            Scribe_Collections.Look(ref billRoles, "billRoles", LookMode.Reference, LookMode.Value,
                ref billKeysWorkingList, ref billValuesWorkingList);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                roles ??= new List<Role>();
                knownWorkTypes ??= new List<string>();
                customSwatches ??= new List<UnityEngine.Color>();
                pawnSets ??= new Dictionary<Pawn, PawnRoleSet>();
                pawnSets.RemoveAll(kv => kv.Key == null || kv.Value == null);
                billRoles ??= new Dictionary<Bill, int>();
                billRoles.RemoveAll(kv => kv.Key == null || kv.Key.DeletedOrDereferenced);
                CompiledJobOrders.InvalidateAll();
            }
        }
    }
}
