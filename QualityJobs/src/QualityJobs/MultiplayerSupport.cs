using Multiplayer.API;
using Verse;

namespace QualityJobs
{
    /// Registers [SyncMethod]s with RimWorld Multiplayer when present. The API
    /// dll ships with the mod; without the MP mod, MP.enabled is false and
    /// this is a no-op.
    [StaticConstructorOnStartup]
    public static class MultiplayerSupport
    {
        static MultiplayerSupport()
        {
            if (MP.enabled) MP.RegisterAll();
        }
    }
}
