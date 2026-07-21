using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Third-party SetPriority calls on managed pawns are no-ops (WorkRoles
    /// owns their priorities; our own mirror writes the DefMap directly and
    /// never lands here). Caller attribution is sampled sparsely: once on the
    /// first conflicting write, then at most once per 500 active game ticks.
    /// The shown-state lives in per-player ModSettings keyed by world — NOT
    /// world state, since calls can be client-local and a scribed write would
    /// desync multiplayer.
    internal static class PrioritySetWatcher
    {
        private sealed class PendingWarning
        {
            public string key;
            public string modName;
            public string sampledWorkType;
        }

        // Assembly ownership is process-stable and safe to retain across saves.
        private static readonly Dictionary<Assembly, ModContentPack> modByAssembly =
            new Dictionary<Assembly, ModContentPack>();
        private static readonly HashSet<Assembly> ignoredAssemblies = new HashSet<Assembly>();

        private static readonly PriorityWriterProbe probe = new PriorityWriterProbe();
        private static World sessionWorld;
        private static PendingWarning pending;

        internal static bool HasPendingWarning => pending != null;

        internal static void OnBlockedSetPriority(Pawn pawn, WorkTypeDef workType, int priority)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            // No world, no per-savegame dedup key — a pre-world write would file
            // under "none" and collide with (or suppress) a real world's warning.
            var world = Find.World;
            if (world == null) return;
            EnsureSession(world);
            // The Harmony prefix still blocks the call; only diagnostics stop.
            if (probe.Stopped) return;

            // A write roles already satisfy is a non-issue: the mod wanted the
            // work on (or off) and it already is — e.g. AllowTool re-enabling
            // Finish Off when Odd Jobs carries it. Count only conflicting intent.
            if (workType != null
                && (priority > 0) == (CompiledJobOrders.PriorityFor(pawn, workType) > 0))
                return;

            int tick = Find.TickManager.TicksGame;
            if (!probe.ObserveBlockedWrite(tick)) return;

            var mod = ResolveCallingMod();
            if (mod == null)
            {
                probe.RecordInspection(tick, PriorityWriterSampleKind.Unknown);
                return;
            }

            string key = world.info.persistentRandomValue + "|" + mod.PackageId;
            if (settings.warnedPriorityMods.Contains(key))
            {
                probe.RecordInspection(tick, PriorityWriterSampleKind.KnownSource);
                return;
            }

            probe.RecordInspection(tick, PriorityWriterSampleKind.NewSource);
            pending = new PendingWarning
            {
                key = key,
                modName = mod.Name,
                sampledWorkType = workType?.labelShort ?? workType?.defName,
            };
        }

        /// Runs from the existing game-component tick only while a new-source
        /// report is pending. The probe itself enforces the following-tick delay.
        internal static void ShowPendingWarning(int tick)
        {
            var warning = pending;
            if (warning == null) return;
            var world = Find.World;
            if (world == null || !ReferenceEquals(world, sessionWorld))
            {
                ResetSession(world);
                return;
            }

            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            // Persisted keys are authoritative. A known mod can never show,
            // refresh or reschedule a dialog, even if state changed after sampling.
            if (settings.warnedPriorityMods.Contains(warning.key))
            {
                pending = null;
                probe.CancelPendingReport();
                return;
            }
            if (!probe.TryConsumeReport(tick, out long blockedWrites)) return;

            pending = null;
            settings.warnedPriorityMods.Add(warning.key);
            settings.Write();
            string sampledWorkType = warning.sampledWorkType
                ?? "WR_AllWork".Translate().ToString();
            Find.WindowStack.Add(new Dialog_MessageBox(
                "WR_SetPriorityBlockedBody".Translate(warning.modName,
                    sampledWorkType, blockedWrites),
                title: "WR_SetPriorityBlockedTitle".Translate()));
        }

        private static void EnsureSession(World world)
        {
            if (ReferenceEquals(world, sessionWorld)) return;
            ResetSession(world);
        }

        private static void ResetSession(World world)
        {
            sessionWorld = world;
            pending = null;
            probe.Reset();
        }

        /// The first stack frame owned by a mod assembly (ours, vanilla,
        /// Harmony, Multiplayer and system frames are skipped; unknown
        /// assemblies stay silent).
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
