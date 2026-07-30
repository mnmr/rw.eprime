using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core.Recs;
using WorkRoles.Signals;

namespace WorkRoles
{
    /// One-shot load-end cache warm: exercises the code paths the Work tab's
    /// first open needs (JIT, reflection bindings, def scans, localized
    /// facades) while the loading screen still owns the frame. Snapshot
    /// CONTENTS the window rebuilds at open are irrelevant; the compiled code
    /// and definition caches persist. Read-only over synced state (MP-safe).
    internal static class FirstOpenWarmup
    {
        internal static void Queue() => LongEventHandler.ExecuteWhenFinished(Run);

        private static void Run()
        {
            var store = RoleStore.Current;
            if (store == null || !store.seeded) return;
            try
            {
                JobSkillProfiles.WarmLocalizedFacade();
                var colonists = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive
                    .Where(p => p.IsColonist || p.IsSlaveOfColony).ToList();
                foreach (var pawn in colonists)
                    PawnSignalSnapshotCache.Get(pawn);
                RecsEngine.Run(RecsAdapter.BuildColonyView(store, colonists));
            }
            catch (System.Exception e)
            {
                // A warm-up must never break loading; the window builds
                // everything itself on open regardless.
                Log.Warning($"[WorkRoles] first-open warm-up aborted: {e.Message}");
            }
        }
    }
}
