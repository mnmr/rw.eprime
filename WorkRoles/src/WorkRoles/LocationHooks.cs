using Verse;
using WorkRoles;

namespace RimShared.GameLib
{
    // WorkRoles' side of the shared location core (Shared/GameLib/Locations).

    internal static partial class MapClassifications
    {
        private static partial void InvalidateLocationSnapshots() =>
            ColonyScope.InvalidateLocationSnapshots();
    }

    internal static partial class LocationTransitions
    {
        // Location rules are snapshot inputs: compiled job orders on the
        // changed map recompile with its new classification.
        static partial void OnClassificationInvalidated(Map map) =>
            CompiledJobOrders.InvalidateLocationRules(map);

        // A map entering or leaving changes the scope filter's settlement
        // membership; window scope stamps key on UiVersion.Current.
        static partial void OnMapSetChanged() => UiVersion.Bump();
    }
}
