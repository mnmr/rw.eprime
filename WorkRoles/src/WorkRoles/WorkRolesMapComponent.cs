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

        public WorkRolesMapComponent(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            CompiledJobOrders.DrainPendingReconciles(map);
            if (hourBoundary.Observe(GenTicks.TicksAbs))
                CompiledJobOrders.InvalidateTimeRuledForMap(map);
        }
    }
}
