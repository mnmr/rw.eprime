using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ReadoutsBooksProbe;

/// Reports, per def, what the colony holds inside container storage
/// (bookcases, outfit stands: haul sources whose contents are unspawned)
/// against what Readouts publishes. Then drops one held book on the floor
/// to show the count depends on where the book sits.
[StaticConstructorOnStartup]
public static class Probe
{
    private static int frames;
    private static bool done;
    private static string Root => GenFilePaths.SaveDataFolderPath;
    static Probe() => new Harmony("eprime.readouts.booksprobe").Patch(
        AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    private static RenderCountSnapshot Snapshot(Map map)
    {
        map.resourceCounter.UpdateResourceCounts();
        return (RenderCountSnapshot)AccessTools.Method(typeof(GameCounts), "BuildSnapshot").Invoke(null, new object[]
            { map, new CountSnapshotOptions(false, true, new PlannedWorkOptions(false, false, false)) });
    }

    private static void Check(List<string> lines, bool condition, string message) =>
        lines.Add((condition ? "PASS " : "FAIL ") + message);

    private static void Update()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
            || LongEventHandler.AnyEventNowOrWaiting || ++frames < 30) return;
        done = true;
        var lines = new List<string>();
        try
        {
            Map map = Find.CurrentMap;
            var held = new Dictionary<ThingDef, int>();
            Building_Bookcase firstCase = null;
            foreach (IHaulSource source in map.haulDestinationManager.AllHaulSourcesListForReading)
            {
                if (source is ISlotGroupParent || source is not IThingHolder holder) continue;
                var owner = holder.GetDirectlyHeldThings();
                lines.Add($"container {source.GetType().Name} holds {owner.Count}");
                foreach (Thing thing in owner)
                {
                    held.TryGetValue(thing.def, out int n); held[thing.def] = n + thing.stackCount;
                    if (firstCase == null && source is Building_Bookcase bookcase) firstCase = bookcase;
                }
            }
            var snapshot = Snapshot(map);
            foreach (var pair in held)
            {
                int spawned = map.listerThings.ThingsOfDef(pair.Key).Sum(t => t.stackCount);
                var search = snapshot.SearchCountOf(pair.Key.defName);
                snapshot.Counts.TryGetValue(pair.Key.defName, out int count);
                Check(lines, search.Total == pair.Value + spawned && search.Stored >= pair.Value && count >= pair.Value,
                    $"{pair.Key.defName}: {pair.Value} in containers + {spawned} on the map; Readouts total={search.Total} stored={search.Stored} group basis={count}");
            }
            Check(lines, firstCase != null, "save has a bookcase holding a book");
            if (firstCase != null)
            {
                Thing book = firstCase.GetDirectlyHeldThings()[0];
                string name = book.def.defName;
                var before = snapshot.SearchCountOf(name);
                book.SetForbidden(true, false);
                var forbidden = Snapshot(map).SearchCountOf(name);
                Check(lines, forbidden.Total == before.Total && forbidden.Unforbidden == before.Unforbidden - 1,
                    $"forbidding one shelved {name}: total {before.Total} -> {forbidden.Total}, unforbidden {before.Unforbidden} -> {forbidden.Unforbidden}");
                book.SetForbidden(false, false);
                firstCase.GetDirectlyHeldThings().TryDrop(book, firstCase.InteractionCell, map, ThingPlaceMode.Near, out _);
                int after = Snapshot(map).SearchCountOf(name).Total;
                Check(lines, after == before.Total, $"moving one {name} from bookcase to floor keeps total {before.Total} -> {after}");
            }
        }
        catch (Exception error) { lines.Add("ERROR " + error); }
        File.WriteAllLines(Path.Combine(Root, "books-results.txt"), lines);
        File.WriteAllText(Path.Combine(Root, "books-done.txt"), "done");
    }
}
