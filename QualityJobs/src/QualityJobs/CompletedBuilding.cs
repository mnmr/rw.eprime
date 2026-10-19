using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Immutable, compact presentation facts. Deconstruction/research/order
    /// eligibility is revalidated by the command, never polled by the renderer.
    internal sealed class CompletedBuildingSnapshot
    {
        internal readonly Map Map;
        internal readonly QualityCategory Quality;
        internal CompletedBuildingSnapshot(Map map, QualityCategory quality)
        { Map = map; Quality = quality; }
    }

    internal static class CompletedBuilding
    {
        internal static bool IsCandidate(Thing thing) => thing is Building
            && !(thing is Frame) && thing.Spawned && !thing.Destroyed
            && thing.Faction == Faction.OfPlayer
            && thing.def.blueprintDef != null && thing.def.frameDef != null
            && thing.TryGetComp<CompQuality>() != null;

        internal static bool CanStart(Building building) => IsCandidate(building)
            && building.def.BuildableByPlayer && building.def.IsResearchFinished
            && building.DeconstructibleBy(Faction.OfPlayer).Accepted
            && building.Map.designationManager.DesignationOn(building, DesignationDefOf.Deconstruct) == null
            && building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) == null
            && InstallBlueprintUtility.ExistingBlueprintFor(building) == null
            && GenConstruct.CanPlaceBlueprintAt(building.def, building.Position,
                building.Rotation, building.Map, thingToIgnore: building,
                stuffDef: building.Stuff).Accepted;
    }
}
