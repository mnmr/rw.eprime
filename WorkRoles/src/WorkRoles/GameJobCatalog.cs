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

        private Dictionary<string, WorkGiverDef> givers;
        private Dictionary<string, List<string>> giversByType;

        private void EnsureBuilt()
        {
            if (givers != null) return;
            givers = DefDatabase<WorkGiverDef>.AllDefsListForReading.ToDictionary(d => d.defName);
            giversByType = DefDatabase<WorkTypeDef>.AllDefsListForReading.ToDictionary(
                t => t.defName,
                t => t.workGiversByPriority.Select(g => g.defName).ToList());
        }

        public bool WorkGiverExists(string workGiverDefName)
        {
            EnsureBuilt();
            return givers.ContainsKey(workGiverDefName);
        }

        public bool WorkTypeExists(string workTypeDefName)
        {
            EnsureBuilt();
            return giversByType.ContainsKey(workTypeDefName);
        }

        public IReadOnlyList<string> WorkGiversOf(string workTypeDefName)
        {
            EnsureBuilt();
            return giversByType.TryGetValue(workTypeDefName, out var list) ? list : (IReadOnlyList<string>)NoGivers;
        }

        public string WorkTypeOf(string workGiverDefName)
        {
            EnsureBuilt();
            return givers.TryGetValue(workGiverDefName, out var def) ? def.workType?.defName : null;
        }

        public bool IsEmergency(string workGiverDefName)
        {
            EnsureBuilt();
            return givers.TryGetValue(workGiverDefName, out var def) && def.emergency;
        }

        public WorkGiverDef GiverDef(string workGiverDefName)
        {
            EnsureBuilt();
            return givers.TryGetValue(workGiverDefName, out var def) ? def : null;
        }
    }
}
