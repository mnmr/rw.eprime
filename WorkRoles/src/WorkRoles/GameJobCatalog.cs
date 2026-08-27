using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public sealed class GameJobCatalog : IJobCatalog
    {
        public static readonly GameJobCatalog Instance = new GameJobCatalog();

        private static readonly List<string> NoGivers = new List<string>();

        private Dictionary<string, WorkGiverDef>? givers;
        private Dictionary<string, List<string>>? giversByType;

        internal void InvalidateSessionCache()
        {
            givers = null;
            giversByType = null;
        }

        private void EnsureBuilt()
        {
            if (givers != null) return;
            givers = DefDatabase<WorkGiverDef>.AllDefsListForReading.ToDictionary(d => d.defName);
            giversByType = DefDatabase<WorkTypeDef>.AllDefsListForReading.ToDictionary(
                t => t.defName,
                t => t.workGiversByPriority.Select(g => g.defName).ToList());
        }

        public IReadOnlyList<string> WorkGiversOf(string workTypeDefName)
        {
            EnsureBuilt();
            return giversByType!.TryGetValue(workTypeDefName, out var list) ? list : (IReadOnlyList<string>)NoGivers;
        }

        public string WorkTypeOf(string workGiverDefName)
        {
            EnsureBuilt();
            // Returns null for unknown givers; IJobCatalog.WorkTypeOf is not
            // nullable-annotated, so the contract is suppressed here.
            return (givers!.TryGetValue(workGiverDefName, out var def) ? def.workType?.defName : null)!;
        }

        public bool IsEmergency(string workGiverDefName)
        {
            EnsureBuilt();
            return givers!.TryGetValue(workGiverDefName, out var def) && def.emergency;
        }

        public WorkGiverDef GiverDef(string workGiverDefName)
        {
            EnsureBuilt();
            // Returns null for unknown givers; callers null-check or own the
            // invariant that the giver exists.
            return (givers!.TryGetValue(workGiverDefName, out var def) ? def : null)!;
        }
    }
}
