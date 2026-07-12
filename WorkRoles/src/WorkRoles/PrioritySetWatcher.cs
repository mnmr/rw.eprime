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

        internal static void OnBlockedSetPriority(Pawn pawn, WorkTypeDef workType, int priority)
        {
            var settings = WorkRolesMod.Settings;
            if (settings == null) return;
            // A write roles already satisfy is a non-issue: the mod wanted the
            // work on (or off) and it already is — e.g. AllowTool re-enabling
            // Finish Off when Odd Jobs carries it. Warn only when the mod's
            // intent actually differs from what roles produce.
            if (workType != null
                && (priority > 0) == (CompiledJobOrders.PriorityFor(pawn, workType) > 0))
                return;
            var mod = CallingMod();
            if (mod == null) return;
            string key = WorldKey() + "|" + mod.PackageId;
            if (settings.warnedPriorityMods.Contains(key)) return;

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
            Find.WindowStack.Add(new Dialog_MessageBox(
                "WR_SetPriorityBlockedBody".Translate(warning.modName,
                    warning.workTypes.ToCommaList(), warning.pawns.Count),
                title: "WR_SetPriorityBlockedTitle".Translate()));
        }

        /// The first stack frame owned by a mod assembly (ours, vanilla,
        /// Harmony, Multiplayer and system frames are skipped; unknown
        /// assemblies stay silent).
        private static ModContentPack CallingMod()
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

        /// Stable per-world key so the once-per-mod warning is per savegame.
        private static string WorldKey() =>
            Find.World?.info?.persistentRandomValue.ToString() ?? "none";
    }
}
