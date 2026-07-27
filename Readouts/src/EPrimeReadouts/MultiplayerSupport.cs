using Multiplayer.API;
using Verse;

namespace EPrimeReadouts
{
    /// Registers the [SyncMethod]s on ReadoutCommands with RimWorld
    /// Multiplayer when it is present. The API dll ships with the mod;
    /// without the Multiplayer mod, MP.enabled is false and this is a no-op.
    [StaticConstructorOnStartup]
    public static class MultiplayerSupport
    {
        static MultiplayerSupport()
        {
            if (MP.enabled) MP.RegisterAll();
        }
    }
}
