using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.UI;

namespace WorkRolesFixPerformance;

// Test-only instrumentation: one actual native window repaint per sample.
// There is no synthetic draw loop, production profiler or injected draw call.
public sealed class Probe : GameComponent
{
    private const int WarmupRepaints = 60, SampleRepaints = 180;
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static Probe instance;
    private static readonly Func<long> Allocated = CreateAllocationReader();
    private readonly long[] bytes = new long[SampleRepaints], timestamps = new long[SampleRepaints];
    private int phase, settle, warm, count, captures, capturesBefore, geometryChanges;
    private readonly int[] gcBefore = new int[3];
    private MainTabWindow_WorkRoles window;
    private object view, roster, stats, externalBefore, skillsBefore, sectionsBefore;
    private Pawn pawn;
    private Rect measuredRect, sampleRect;
    private int initialTick;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static string Number(double number) => number.ToString("R", CultureInfo.InvariantCulture);
    private static object Get(object owner, string name) => AccessTools.Property(owner.GetType(), name)?.GetValue(owner) ?? AccessTools.Field(owner.GetType(), name)?.GetValue(owner);
    private static object Call(object owner, string name, params object[] args)
    {
        Type type = owner as Type ?? owner.GetType();
        return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(owner is Type ? null : owner, args);
    }
    private static Func<long> CreateAllocationReader()
    {
        MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes);
        return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
    }
    static Probe()
    {
        var harmony = new Harmony("tests.workroles.fixperformance");
        harmony.Patch(AccessTools.Method(typeof(MainTabWindow_WorkRoles), nameof(MainTabWindow_WorkRoles.DoWindowContents)),
            prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeRepaint)), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterRepaint)));
        harmony.Patch(AccessTools.Method(typeof(RecsAdapter), "CapturePawnSnapshot"), postfix: new HarmonyMethod(typeof(Probe), nameof(CapturedPawn)));
    }
    public Probe(Game game) { instance = this; }
    private struct Measurement
    {
        internal bool Active;
        internal long Bytes, Timestamp;
    }
    private static void CapturedPawn() { if (instance != null) instance.captures++; }
    private static void BeforeRepaint(MainTabWindow_WorkRoles __instance, Rect inRect, out Measurement __state)
    {
        __state = default;
        Probe probe = instance;
        if (probe == null || probe.window != __instance || (probe.phase != 1 && probe.phase != 2)
            || Event.current.type != EventType.Repaint || Allocated == null) return;
        probe.measuredRect = inRect;
        if (probe.phase == 2)
        {
            if (probe.count == 0) probe.sampleRect = inRect;
            else if (probe.sampleRect != inRect) probe.geometryChanges++;
        }
        __state.Active = true;
        __state.Bytes = Allocated();
        __state.Timestamp = Stopwatch.GetTimestamp();
    }
    private static void AfterRepaint(Measurement __state)
    {
        if (!__state.Active) return;
        // End the native-window bracket before counters, arrays, reflection,
        // reports, captures or any other test bookkeeping.
        long elapsed = Stopwatch.GetTimestamp() - __state.Timestamp;
        long allocated = Allocated() - __state.Bytes;
        Probe probe = instance;
        if (probe.phase == 1) { probe.warm++; return; }
        int index = probe.count;
        probe.timestamps[index] = elapsed;
        probe.bytes[index] = allocated;
        probe.count = index + 1;
        if (probe.count == SampleRepaints) probe.phase = 3;
    }
    private void Record(string text)
    {
        File.AppendAllText(Path.Combine(Root, "fix-performance-results.txt"), text + Environment.NewLine);
        Log.Message("[FixPerformanceProbe] " + text);
    }
    private void Stage(string stage) => File.WriteAllText(Path.Combine(Root, "fix-performance-stage.txt"), stage);
    public override void GameComponentUpdate()
    {
        if (phase == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting || ++settle < 30) return;
        try
        {
            if (phase == 0)
            {
                if (Allocated == null) throw new NotSupportedException("Native runtime has no GC.GetAllocatedBytesForCurrentThread; allocation comparison is unavailable");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                foreach (Window popup in Find.WindowStack.Windows.ToArray()) popup.Close(false);
                pawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.Single(p => p.LabelShort == "Takeo");
                Find.Selector.ClearSelection(); Find.Selector.Select(pawn);
                // Fixed saved-colony workload, same in original and fixed
                // assemblies. Keep every roster row; select the same pawn.
                WorkRolesMod.Settings.groupBy = "ideo";
                WorkRolesGameComponent.RequestSettingsWrite();
                var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                window = (MainTabWindow_WorkRoles)button.TabWindow;
                var curTab = AccessTools.Field(window.GetType(), "curTab");
                curTab.SetValue(window, Enum.Parse(curTab.FieldType, "Colonists"));
                Find.MainTabsRoot.SetCurrentTab(button);
                if (!Find.WindowStack.Windows.Contains(window))
                {
                    Type defaults = typeof(RoleIO).Assembly.GetType("WorkRoles.SetupDefaults");
                    Call(defaults, "Decide", false, false);
                    return;
                }
                view = Get(window, "colonistsTab"); roster = Get(view, "rosterState"); stats = Get(view, "statsState");
                AccessTools.Property(roster.GetType(), "Search").SetValue(roster, "");
                AccessTools.Property(roster.GetType(), "RoleFilterId").SetValue(roster, -1);
                AccessTools.Property(roster.GetType(), "JobFilterDefName").SetValue(roster, null);
                ((ListSelection<Pawn>)Get(view, "selection")).Click(pawn);
                initialTick = Find.TickManager.TicksGame;
                phase = 1;
                Stage("WARMING");
            }
            else if (phase == 1 && warm >= WarmupRepaints)
            {
                externalBefore = Call(stats, "ExternalSnapshot", pawn);
                skillsBefore = Get(Call(stats, "Snapshot", pawn), "Facts");
                sectionsBefore = Call(roster, "Sections", RoleStore.Current);
                capturesBefore = captures;
                for (int i = 0; i < gcBefore.Length; i++) gcBefore[i] = GC.CollectionCount(i);
                phase = 2;
                Stage("SAMPLING");
            }
            else if (phase == 3) Report();
            else if (phase == 4 && File.Exists(Path.Combine(Root, "fix-performance-captured.txt")))
            {
                Stage("SUCCESS"); phase = 99;
            }
        }
        catch (Exception ex) { phase = 99; Record("FAILED " + ex); Stage("FAILED " + ex.Message); }
    }
    private static string Distribution(long[] input, double scale)
    {
        long[] sorted = (long[])input.Clone();
        Array.Sort(sorted);
        double sum = 0;
        for (int i = 0; i < sorted.Length; i++) sum += sorted[i];
        double median = (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;
        int p95 = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        return "{\"min\":" + Number(sorted[0] * scale) + ",\"median\":" + Number(median * scale)
            + ",\"p95\":" + Number(sorted[p95] * scale) + ",\"max\":" + Number(sorted[sorted.Length - 1] * scale)
            + ",\"mean\":" + Number(sum / sorted.Length * scale) + "}";
    }
    private void Report()
    {
        bool externalReuse = ReferenceEquals(externalBefore, Call(stats, "ExternalSnapshot", pawn));
        bool skillReuse = ReferenceEquals(skillsBefore, Get(Call(stats, "Snapshot", pawn), "Facts"));
        bool sectionReuse = ReferenceEquals(sectionsBefore, Call(roster, "Sections", RoleStore.Current));
        bool paused = Find.TickManager.Paused && Find.TickManager.TicksGame == initialTick;
        bool sameSelection = ReferenceEquals(((ListSelection<Pawn>)Get(view, "selection")).Single, pawn);
        string allocations = Distribution(bytes, 1), times = Distribution(timestamps, 1000000.0 / Stopwatch.Frequency);
        string gc = string.Join(",", Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - gcBefore[i]));
        string identity = "{\"external\":" + externalReuse.ToString().ToLowerInvariant() + ",\"skills\":" + skillReuse.ToString().ToLowerInvariant()
            + ",\"sections\":" + sectionReuse.ToString().ToLowerInvariant() + "}";
        string json = "{\"sampleCount\":" + count + ",\"warmupRepaints\":" + warm + ",\"allocatedBytes\":" + allocations
            + ",\"elapsedMicroseconds\":" + times + ",\"gcCollections\":[" + gc + "],\"snapshotReuse\":" + identity
            + ",\"capturesDuringSamples\":" + (captures - capturesBefore) + ",\"pausedSameTick\":" + paused.ToString().ToLowerInvariant()
            + ",\"gameTick\":" + initialTick + ",\"sameSelection\":" + sameSelection.ToString().ToLowerInvariant() + ",\"geometryChanges\":" + geometryChanges
            + ",\"physicalWidth\":" + Screen.width + ",\"physicalHeight\":" + Screen.height + ",\"uiScale\":" + Number(Prefs.UIScale)
            + ",\"contentRect\":[" + Number(measuredRect.x) + "," + Number(measuredRect.y) + "," + Number(measuredRect.width) + "," + Number(measuredRect.height)
            + "],\"mapId\":" + Find.CurrentMap.uniqueID + ",\"pawnId\":" + pawn.thingIDNumber + ",\"pawn\":\"Takeo\",\"tab\":\"Colonists\",\"groupBy\":\"ideo\"}";
        File.WriteAllText(Path.Combine(Root, "fix-performance-summary.json"), json);
        var raw = new StringBuilder("[\n");
        for (int i = 0; i < count; i++)
        {
            if (i != 0) raw.Append(",\n");
            raw.Append("{\"repaint\":").Append(i).Append(",\"allocatedBytes\":").Append(bytes[i])
                .Append(",\"stopwatchTicks\":").Append(timestamps[i]).Append('}');
        }
        raw.Append("\n]");
        File.WriteAllText(Path.Combine(Root, "fix-performance-samples.json"), raw.ToString());
        Record("NATIVE REPAINT count=" + count + " warmup=" + warm + " allocatedBytes=" + allocations + " microseconds=" + times);
        Record("CACHE snapshots=" + identity + " capturesDuringSamples=" + (captures - capturesBefore) + " pausedSameTick=" + paused);
        if (!paused || !sameSelection || geometryChanges != 0 || !externalReuse || !skillReuse || !sectionReuse || captures != capturesBefore)
            throw new InvalidOperationException("Steady-state benchmark changed tick or rebuilt pawn/roster snapshots; inspect raw evidence");
        phase = 4;
        Stage("CAPTURE");
    }
}
