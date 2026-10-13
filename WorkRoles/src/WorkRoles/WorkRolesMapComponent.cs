using RimShared.Common;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// <summary>
    /// Map-local runtime work. Multiplayer Async Time invokes this while the
    /// map's tick and faction contexts are active.
    /// </summary>
    public sealed class WorkRolesMapComponent : MapComponent
    {
        private readonly FixedTickBoundaryGate hourBoundary =
            new FixedTickBoundaryGate(2500);

        private readonly System.Action hourUpdates;
        private readonly System.Action reconcile;

        public WorkRolesMapComponent(Map map) : base(map)
        {
            hourUpdates = UpdateHour;
            reconcile = ReconcilePending;
        }

        public override void MapComponentTick()
        {
            RuntimeWorkBoundary.Process(hourBoundary.Observe(GenTicks.TicksAbs), hourUpdates, reconcile);
        }

        private void UpdateHour()
        {
            CompiledJobOrders.InvalidateTimeRuledForMap(map);
            AutoOptimizer.RunForMap(map);
        }

        private void ReconcilePending() => CompiledJobOrders.DrainPendingReconciles(map);
    }
}
