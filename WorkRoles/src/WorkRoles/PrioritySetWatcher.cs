using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using RimWorld;
using Verse;

namespace WorkRoles
{
    /// Third-party SetPriority calls on managed pawns are no-ops (WorkRoles
    /// owns their priorities; our own mirror writes the DefMap directly and
    /// never lands here). This watcher identifies the calling mod from the
    /// stack, aggregates the burst (mods typically sweep many pawns at once)
    /// and shows one explanatory dialog per mod per savegame. The shown-state
    /// lives in per-player ModSettings keyed by world — NOT world state, since
    /// these calls can be client-local and a scribed write would desync MP.
    internal static class PrioritySetWatcher
    {
        private sealed class PendingWarning
        {
            public string modName;
            public HashSet<Pawn> pawns = new HashSet<Pawn>();
            public HashSet<string> workTypes = new HashSet<string>();
        }

        private static readonly Dictionary<Assembly, ModContentPack> modByAssembly =
            new Dictionary<Assembly, ModContentPack>();
        private static readonly HashSet<Assembly> ignoredAssemblies = new HashSet<Assembly>();
        private static readonly Dictionary<string, PendingWarning> pending =
            new Dictionary<string, PendingWarning>();

        /// Tick whose stack walk resolved to an already-warned (or unknown)
        /// caller. Identifying the caller costs a stack capture; a mod sweeping
        /// every pawn each tick would otherwise pay it per pawn forever after
        /// its one-time dialog. Settling the tick bounds that to one walk/tick;
        /// a different, unwarned mod writing in the same tick is caught on the
        /// next one.
        private static int settledTick = -1;
        private static int settledWorldKey = int.MinValue;

        // Priority writers normally sweep many pawns in one tick. Resolve the
        // caller once for that tick, then keep aggregating the individual pawns
        // and work types without another stack capture.
        private static int callerTick = -1;
        private static int callerWorldKey = int.MinValue;
        private static ModContentPack callerMod;

        internal static void OnBlockedSetPriority(Pawn pawn, WorkTypeDef workType, int priority)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            // No world, no per-savegame dedup key — a pre-world write would file
            // under "none" and collide with (or suppress) a real world's warning.
            var world = Find.World;
            if (world == null) return;
            // A write roles already satisfy is a non-issue: the mod wanted the
            // work on (or off) and it already is — e.g. AllowTool re-enabling
            // Finish Off when Odd Jobs carries it. Warn only when the mod's
            // intent actually differs from what roles produce.
            if (workType != null
                && (priority > 0) == (CompiledJobOrders.PriorityFor(pawn, workType) > 0))
                return;
            int tick = Find.TickManager.TicksGame;
            int worldKey = world.info.persistentRandomValue;
            if (worldKey != settledWorldKey)
            {
                settledWorldKey = worldKey;
                settledTick = -1;
            }
            if (tick == settledTick) return;
            var mod = CallingMod(worldKey, tick);
            if (mod == null) { settledTick = tick; return; }
            string key = worldKey + "|" + mod.PackageId;
            if (settings.warnedPriorityMods.Contains(key)) { settledTick = tick; return; }

            if (!pending.TryGetValue(key, out var warning))
            {
                pending[key] = warning = new PendingWarning { modName = mod.Name };
                string capturedKey = key;
                // Deferred so a burst (one call per pawn) aggregates into one
                // dialog, and so load-time calls surface after loading ends.
                LongEventHandler.ExecuteWhenFinished(() => Show(capturedKey));
            }
            warning.pawns.Add(pawn);
            if (workType != null)
                warning.workTypes.Add(workType.labelShort ?? workType.defName);
        }

        private static void Show(string key)
        {
            if (!pending.TryGetValue(key, out var warning)) return;
            pending.Remove(key);
            var settings = WorkRolesMod.Settings;
            if (settings == null || settings.warnedPriorityMods.Contains(key)) return;
            settings.warnedPriorityMods.Add(key);
            settings.Write();
            // No work types = a blocked DisableAll sweep.
            string types = warning.workTypes.Count > 0
                ? warning.workTypes.ToCommaList()
                : "WR_AllWork".Translate().ToString();
            Find.WindowStack.Add(new Dialog_MessageBox(
                "WR_SetPriorityBlockedBody".Translate(warning.modName,
                    types, warning.pawns.Count),
                title: "WR_SetPriorityBlockedTitle".Translate()));
        }

        /// The first stack frame owned by a mod assembly (ours, vanilla,
        /// Harmony, Multiplayer and system frames are skipped; unknown
        /// assemblies stay silent).
        private static ModContentPack CallingMod(int worldKey, int tick)
        {
            if (worldKey == callerWorldKey && tick == callerTick)
                return callerMod;
            callerWorldKey = worldKey;
            callerTick = tick;
            callerMod = ResolveCallingMod();
            return callerMod;
        }

        private static ModContentPack ResolveCallingMod()
        {
            var trace = new StackTrace(2, false);
            for (int i = 0; i < trace.FrameCount; i++)
            {
                var assembly = trace.GetFrame(i)?.GetMethod()?.DeclaringType?.Assembly;
                if (assembly == null || ignoredAssemblies.Contains(assembly)) continue;
                if (modByAssembly.TryGetValue(assembly, out var known)) return known;

                string name = assembly.FullName;
                if (assembly == typeof(PrioritySetWatcher).Assembly
                    || assembly == typeof(Pawn).Assembly
                    || assembly == typeof(HarmonyLib.Harmony).Assembly
                    || name.StartsWith("mscorlib") || name.StartsWith("System")
                    || name.StartsWith("UnityEngine") || name.StartsWith("netstandard")
                    || name.StartsWith("Multiplayer"))
                {
                    ignoredAssemblies.Add(assembly);
                    continue;
                }

                foreach (var mod in LoadedModManager.RunningMods)
                    if (mod.assemblies.loadedAssemblies.Contains(assembly))
                    {
                        modByAssembly[assembly] = mod;
                        return mod;
                    }
                ignoredAssemblies.Add(assembly);
            }
            return null;
        }
    }
}
