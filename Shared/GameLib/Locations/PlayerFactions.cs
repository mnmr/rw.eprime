using Multiplayer.API;
using RimWorld;

namespace RimShared.GameLib
{
    /// The two player factions location code classifies against: the local
    /// client's (presentation) and the synchronized one (simulation).
    internal static class PlayerFactions
    {
        // MP.RealPlayerFaction exists only in Multiplayer API 0.5+, but the
        // first Multiplayer.API assembly in mod-list order wins resolution, so
        // an older stub shipped by another mod would make a direct call throw
        // MissingMethodException at JIT time. Bind via reflection once instead.
        private static readonly System.Func<Faction>? realPlayerFactionGetter =
            ResolveRealPlayerFactionGetter();

        private static System.Func<Faction>? ResolveRealPlayerFactionGetter()
        {
            var getter = typeof(MP).GetProperty(
                "RealPlayerFaction",
                System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static)?.GetGetMethod();
            if (getter == null || getter.ReturnType != typeof(Faction))
                return null;
            return (System.Func<Faction>)System.Delegate.CreateDelegate(
                typeof(System.Func<Faction>), getter);
        }

        /// The faction presentation code shows: the LOCAL client's player
        /// faction. Per-client by design — it must never feed synchronized
        /// mutations.
        internal static Faction ViewFaction
        {
            get
            {
                if (!MP.enabled) return Faction.OfPlayer;
                var faction = realPlayerFactionGetter?.Invoke();
                return faction ?? Faction.OfPlayer;
            }
        }

        /// The faction the deterministic tick path and synced commands use.
        /// Faction.OfPlayer is identical on every multiplayer client during
        /// synchronized execution (Multiplayer swaps it under the synced
        /// faction context), unlike MP.RealPlayerFaction, which is the local
        /// client's faction and diverges in multifaction sessions.
        internal static Faction AuthoritativeFaction => Faction.OfPlayer;
    }
}
