using System.Linq;
using HarmonyLib;
using Verse;

namespace WorkRoles
{
    public class WorkRolesMod : Mod
    {
        public const string HarmonyId = "mnmr.workroles";

        public static WorkRolesSettings Settings { get; private set; }

        /// About.xml modVersion of the running mod (stamped on seeded roles).
        public static string Version { get; private set; }

        public WorkRolesMod(ModContentPack content) : base(content)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Version = content.ModMetaData?.ModVersion;
            Settings = GetSettings<WorkRolesSettings>();
            StartupTiming.Record("settings", sw.ElapsedMilliseconds);

            // Resolve the bill-dialog private members before Harmony processes
            // its UI patch classes. A changed RimWorld layout disables only
            // that optional UI integration instead of aborting mod startup.
            Patches.BillDialogCompatibility.Initialize();

            // Patching, unrolled per class and timed: patching a method
            // recompiles every other mod's patches on it too, so one popular
            // target (Pawn.SpawnSetup, Dialog_BillConfig...) can eat seconds on
            // a large modlist — classes over 100ms are named in the timing line
            // instead of leaving load-time stalls attributed to "WorkRoles" as
            // a whole.
            long patchStart = sw.ElapsedMilliseconds;
            var harmony = new Harmony(HarmonyId);
            var slow = new System.Text.StringBuilder();
            foreach (var type in AccessTools.GetTypesFromAssembly(GetType().Assembly))
            {
                long before = sw.ElapsedMilliseconds;
                harmony.CreateClassProcessor(type).Patch();
                long ms = sw.ElapsedMilliseconds - before;
                if (ms >= 100)
                    slow.Append(slow.Length > 0 ? ", " : " [").Append($"{type.Name} {ms}ms");
            }
            StartupTiming.Record(
                "harmony patching" + (slow.Length > 0 ? slow.Append("]").ToString() : ""),
                sw.ElapsedMilliseconds - patchStart);
        }
    }

    /// Collects startup costs across the two load phases — the Mod constructor
    /// (early) and our [StaticConstructorOnStartup] types (much later, in
    /// CallAll) — and emits ONE consolidated log line when the last expected
    /// mark arrives. One line keeps the log readable and still leaves enough to
    /// answer "is WorkRoles slowing my load?" reports without a debug build.
    internal static class StartupTiming
    {
        private const int ExpectedMarks = 4; // settings, patching, multiplayer, textures
        private static readonly System.Collections.Generic.List<(string label, long ms)> marks
            = new System.Collections.Generic.List<(string, long)>();

        public static void Record(string label, long ms)
        {
            marks.Add((label, ms));
            if (marks.Count < ExpectedMarks) return;
            Log.Message($"[WorkRoles] load: {marks.Sum(m => m.ms)}ms ("
                + string.Join(", ", marks.Select(m => $"{m.label}: {m.ms}ms")) + ")");
        }
    }
}
