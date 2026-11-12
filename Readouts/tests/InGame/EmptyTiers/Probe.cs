using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EPrimeReadouts;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using Verse;

namespace ReadoutsEmptyTiersProbe;

[StaticConstructorOnStartup]
public static class Probe
{
    private static string Root => GenFilePaths.SaveDataFolderPath;
    private static string request = "";
    private static int frames, groupId;
    private static bool prepared;
    static Probe() => new Harmony("eprime.readouts.emptytiersprobe").Patch(
        AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));

    private static void Update()
    {
        if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        try
        {
            string path = Path.Combine(Root, "tiers-request.txt");
            if (!File.Exists(path)) return;
            string next;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream)) next = reader.ReadToEnd();
            if (!next.EndsWith("\n")) return;
            var parts = next.Trim().Split(';');
            if (request != next)
            {
                request = next; frames = 0;
                if (!prepared) Prepare();
                if (parts[1] == "setup")
                {
                    var s = EPrimeReadoutsMod.Settings;
                    s.expandOnHover = bool.Parse(parts[2]); s.collapseWhenIdle = bool.Parse(parts[3]);
                    s.verticalTiers = bool.Parse(parts[4]);
                    s.tierDepths[ReadoutStore.Current.DepthKey(groupId)] = 1;
                    ReadoutPanel.BumpView();
                }
            }
            if (++frames != 15) return;
            object draw = AccessTools.Field(typeof(ReadoutPanel), "draw").GetValue(null);
            var model = (RenderModel)AccessTools.Field(draw.GetType(), "Model").GetValue(draw);
            var settings = EPrimeReadoutsMod.Settings;
            var band = model.Bands.Single(b => b.GroupId == groupId);
            var state = new XElement("State",
                new XAttribute("depth", settings.tierDepths[ReadoutStore.Current.DepthKey(groupId)]),
                new XAttribute("markers", band.MarkerCount),
                new XAttribute("x", (int)((settings.offsetX + 9) * Prefs.UIScale)),
                new XAttribute("y", (int)((settings.offsetY + 26 + band.Rect.Y + band.Rect.H / 2) * Prefs.UIScale)),
                new XAttribute("width", band.Rect.W), new XAttribute("height", band.Rect.H),
                model.SlotHits.Select(h => new XElement("Slot", new XAttribute("token", h.Token),
                    new XAttribute("count", model.Cells[h.CellIndex].Count))),
                model.Cells.Where(c => c.Kind == CellKind.Triangle).Select(c => new XElement("Arrow", c.Triangle)));
            string result = Path.Combine(Root, "tiers-" + parts[0] + ".xml");
            state.Save(result + ".tmp");
            File.Move(result + ".tmp", result, true);
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(Root, "tiers-error.txt"), e.ToString()); }
    }

    private static void Prepare()
    {
        prepared = true;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
        EditWindow_Log.wantsToOpen = false;
        for (int i = Find.WindowStack.Count - 1; i >= 0; i--) Find.WindowStack[i].Close(false);
        var map = Find.CurrentMap;
        foreach (string def in new[] { "Gold", "Silver" })
            foreach (Thing thing in map.listerThings.ThingsOfDef(ThingDef.Named(def)).ToArray()) thing.Destroy(DestroyMode.Vanish);
        var steel = ThingMaker.MakeThing(ThingDef.Named("Steel")); steel.stackCount = 25;
        GenPlace.TryPlaceThing(steel, map.Center, map, ThingPlaceMode.Near);
        map.resourceCounter.UpdateResourceCounts();
        ReadoutCommands.ImportAll("<Readouts><Pools/><Groups><Group Name='Empty leading tiers'><Tier><Slot>~Gold</Slot></Tier><Tier><Slot>~Silver</Slot></Tier><Tier><Slot>~Steel</Slot></Tier></Group></Groups><Thresholds/></Readouts>");
        groupId = ReadoutStore.Current.Model.Groups.Single().Id;
        var s = EPrimeReadoutsMod.Settings;
        s.useVanillaReadout = false; s.keepReadoutToggle = false; s.showSearchFilter = false;
        s.searchStorageOnly = false; s.searchHideForbidden = false;
        s.reserveForBills = s.reserveForBuildables = false;
        s.offsetX = 7; s.offsetY = 7;
        s.enabledGroups.Clear(); s.enabledGroups[ReadoutStore.Current.DepthKey(groupId)] = true;
        ReadoutPanel.SearchText = ""; ReadoutPanel.BumpView();
    }
}
