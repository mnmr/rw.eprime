using Implanner;

namespace RimShared.GameLib
{
    // Implanner's side of the shared location core (Shared/GameLib/Locations).
    // The optional LocationTransitions hooks stay unimplemented: location-aware
    // snapshots gate on MapClassifications.LocationRevision, and the UI metric
    // revision is deliberately left alone (it would clear text-width, tooltip,
    // and help caches that do not depend on locations).
    internal static partial class MapClassifications
    {
        private static partial void InvalidateLocationSnapshots() =>
            ColonyScope.InvalidateLocationSnapshots();
    }
}
