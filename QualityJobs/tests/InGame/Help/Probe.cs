using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using QualityJobs;
using QualityJobs.Patches;
using QualityJobs.UI;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace HelpVerification
{
    // Test-only observer: input always comes through the scoped automation pipe.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static int frames;
        private static bool initialized;
        private static bool failed;
        private static bool metricsSet;
        private static object settingsSnapshot;
        private static Window lastDialog;
        private static bool inHelp, sampling;
        private static int sampleFrames, measurements;
        private static readonly System.Collections.Generic.Dictionary<string,string> points = new System.Collections.Generic.Dictionary<string,string>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        static Probe()
        {
            var harmony = new Harmony("eprime.qualityjobs.helpprobe");
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            harmony.Patch(AccessTools.Method(typeof(Patch_PlaySettings), "Postfix"), postfix: new HarmonyMethod(typeof(Probe), nameof(Toolbar)));
            harmony.Patch(AccessTools.Method(typeof(Command_QualityJob), "GizmoOnGUI"), postfix: new HarmonyMethod(typeof(Probe), nameof(Gizmo)));
            harmony.Patch(AccessTools.Method(typeof(HelpTabView), "Draw"), prefix: new HarmonyMethod(typeof(Probe), nameof(BeforeHelp)), postfix: new HarmonyMethod(typeof(Probe), nameof(AfterHelp)));
            harmony.Patch(AccessTools.Method(typeof(Text), "CalcSize", new[]{typeof(string)}), prefix: new HarmonyMethod(typeof(Probe), nameof(Measure)));
            harmony.Patch(AccessTools.Method(typeof(Text), "CalcHeight", new[]{typeof(string),typeof(float)}), prefix: new HarmonyMethod(typeof(Probe), nameof(Measure)));
            Mark("initializing");
        }
        private static void BeforeHelp() { inHelp=true; }
        private static void Measure() { if(inHelp && sampling && sampleFrames>=8) measurements++; }
        private static void AfterHelp()
        {
            inHelp=false;
            if(!sampling || ++sampleFrames<28) return;
            sampling=false;
            try { Require(measurements==0,"steady Help rendering performs no text measurement"); Mark("steady"); }
            catch(Exception e) { failed=true; Mark("FAILED " + e.Message); }
        }
        private static object Field(object value, string name) => AccessTools.Field(value.GetType(), name).GetValue(value);
        private static object Snapshot => AccessTools.Property(typeof(QualityJobsStore), "SettingsPresentation").GetValue(QualityJobsStore.Active);
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Log.Message("[HelpProbe] PASS " + message);
        }
        private static void Point(string name, Vector2 point)
        {
            string value = Math.Round(point.x) + "," + Math.Round(point.y);
            if (points.TryGetValue(name, out string old) && old == value) return;
            points[name] = value;
            File.WriteAllText(Path.Combine(Root, "help-" + name + ".txt"), value);
        }
        private static void Gizmo(Vector2 topLeft)
        {
            if (Event.current.type == EventType.Repaint) Point("gizmo", GUIUtility.GUIToScreenPoint(topLeft + new Vector2(35,35)));
        }
        private static void Toolbar()
        {
            if (Event.current.type != EventType.Repaint || !initialized) return;
            Rect rect = (Rect)AccessTools.Field(typeof(Patch_PlaySettings), "lastIconRect").GetValue(null);
            var point = GUIUtility.GUIToScreenPoint(rect.center);
            Point("toolbar", point);
        }
        private static void Mark(string value) => File.WriteAllText(Path.Combine(Root, "help-stage.txt"), value);
        private static void Update()
        {
            if (failed || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                || LongEventHandler.AnyEventNowOrWaiting || ++frames < 4) return;
            try
            {
                if (!initialized)
                {
                    // Waylith may quantize the preference on startup (observed
                    // 0.9375 despite a saved 1.0). Set the requested capture
                    // metric once after loading, then allow the UI to settle.
                    if (!metricsSet)
                    {
                        Prefs.UIScale = 1f;
                        metricsSet = true;
                        frames = 0;
                        Mark("settling");
                        return;
                    }
                    foreach (var window in Find.WindowStack.Windows.ToArray())
                        if (window.GetType().Name.Contains("Welcome") || window.GetType().Name.Contains("Migration")
                            || window.GetType().Name.Contains("MessageBox")) window.Close(false);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    Prefs.DevMode = false;
                    AccessTools.Field(typeof(EditWindow_Log), "canAutoOpen").SetValue(null, false);
                    EditWindow_Log.wantsToOpen = false;
                    Find.WindowStack.TryRemove(typeof(EditWindow_Log), false);
                    settingsSnapshot = Snapshot;
                    Require(LanguageDatabase.activeLanguage.LegacyFolderName==File.ReadAllText(Path.Combine(Root,"help-locale.txt")),
                        "requested native language is active: " + LanguageDatabase.activeLanguage.LegacyFolderName);
                    Require(Screen.width == 1920 && Screen.height == 1080 && Prefs.UIScale == 1f,
                        "screenshots use 1920x1080 at UI scale 1.0; actual " + Screen.width + "x" + Screen.height + " scale=" + Prefs.UIScale);
                    initialized = true; Mark("ready");
                }
                var request = Path.Combine(Root, "help-request.txt");
                if (!File.Exists(request)) return;
                string action = File.ReadAllText(request).Trim();
                File.Delete(request);
                if (action == "check-window")
                {
                    var dialog = Find.WindowStack.Windows.FirstOrDefault(w => w.GetType().Name == "Dialog_QualityJobs");
                    if (dialog == null) throw new Exception("Toolbar must open Options and Help tabs; it opened the legacy settings window.");
                    lastDialog = dialog;
                    var origin = dialog.windowRect.position + new Vector2(18f,18f);
                    Point("help-tab", origin + new Vector2(285f,16f));
                    Point("options-tab", origin + new Vector2(95f,16f));
                    Point("sharing", origin + new Vector2(300f,398f));
                    File.WriteAllText(Path.Combine(Root,"help-window-rect.txt"), dialog.windowRect.ToString());
                    Require(ReferenceEquals(Snapshot, settingsSnapshot), "opening Options preserves colony settings snapshot");
                    Mark("window");
                }
                else if (action == "check-help")
                {
                    Require((bool)Field(lastDialog,"showingHelp"), "real tab click opens Help");
                    Require(ReferenceEquals(Snapshot,settingsSnapshot), "Help navigation does not change colony settings");
                    var help = (HelpTabView)Field(lastDialog,"help");
                    var content = (HelpContentState)Field(help,"content");
                    int count = 0;
                    for(int c=0;c<4;c++)
                    {
                        var topics=content.ChapterTopics(c); count+=topics.Length;
                        foreach(var item in topics)
                        {
                            var built=content.Model(c,item,700f);
                            var kinds=(byte[])Field(built,"Kinds");
                            var images=(Texture2D[])Field(built,"Images");
                            var texts=(string[])Field(built,"Texts");
                            var targets=(string[])Field(built,"Targets");
                            for(int i=0;i<kinds.Length;i++)
                            {
                                if((texts[i]??"").Contains("l10n:")) throw new Exception("localization metadata rendered as prose");
                                if((texts[i]??"").Contains("{k:")) throw new Exception("unresolved UI label rendered as prose");
                                if(kinds[i]==3) Require(images[i]!=null,"help screenshot loaded");
                                if(kinds[i]==1) Require(content.TryFindTopic(targets[i],out _,out _),"help link resolves: " + targets[i]);
                            }
                        }
                    }
                    Require(count == 12, "all twelve help topics load");
                    var topic = content.ChapterTopics(0)[0];
                    var model = content.Model(0,topic,700f);
                    Require(ReferenceEquals(model,content.Model(0,topic,700f)), "steady help reads reuse draw model identity");
                    Commands.SetShareUnfinishedWork(QualityJobsStore.Active.shareUnfinishedWork);
                    Require(ReferenceEquals(Snapshot,settingsSnapshot), "no-op option command preserves snapshot");
                    Require(ReferenceEquals(model,content.Model(0,topic,700f)), "colony command does not invalidate help geometry");
                    Require(!ReferenceEquals(model,content.Model(0,topic,500f)), "changed wrapping width rebuilds help geometry");
                    Mark("help");
                }
                else if(action == "sample-start") { sampling=true;sampleFrames=measurements=0; }
                else if(action == "check-toggle")
                {
                    Require(!ReferenceEquals(Snapshot,settingsSnapshot),"Options click publishes changed colony settings while paused");
                    Mark("toggled");
                }
                else if(action == "check-restored")
                {
                    Require(Field(Snapshot,"ShareUnfinishedWork").Equals(Field(settingsSnapshot,"ShareUnfinishedWork")),"second Options click restores original sharing choice");
                    settingsSnapshot=Snapshot; Mark("restored");
                }
                else if (action.StartsWith("topic:"))
                {
                    var parts = action.Split(':'); int chapter=int.Parse(parts[1]); int index=int.Parse(parts[2]);
                    var origin = lastDialog.windowRect.position + new Vector2(28f,60f);
                    float width=lastDialog.windowRect.width-56f;
                    Point("chapter", origin + new Vector2(width*(chapter+0.5f)/4f,14f));
                    Point("topic", origin + new Vector2(100f,36f+28f*(index+0.5f)));
                    Mark("topic-points");
                }
                else if (action.StartsWith("topic-row:"))
                {
                    int index=int.Parse(action.Split(':')[1]);
                    var help=(HelpTabView)Field(lastDialog,"help");
                    Rect row=((Rect[])Field(help,"topicRowRects"))[index];
                    Point("topic",lastDialog.windowRect.position+new Vector2(30f,98f)+row.center);
                    Mark("topic-row");
                }
                else if(action.StartsWith("check-topic:"))
                {
                    var parts=action.Split(':'); int chapter=int.Parse(parts[1]);int index=int.Parse(parts[2]);
                    var help=(HelpTabView)Field(lastDialog,"help");
                    var content=(HelpContentState)Field(help,"content");
                    Require((int)Field(help,"activeChapter")==chapter,"chapter click selects requested chapter");
                    string selected=((string[])Field(help,"selectedSlugs"))[chapter];
                    Require(selected==content.ChapterTopics(chapter)[index].Entry.Slug,"topic click selects requested translated topic");
                    Mark("topic-checked");
                }
                else if (action == "check-options")
                {
                    Require(!(bool)Field(lastDialog,"showingHelp"), "returning to Options works");
                    Require(ReferenceEquals(Snapshot,settingsSnapshot), "topic changes and scrolling leave settings untouched");
                    Mark("options");
                }
                else if (action == "check-closed")
                {
                    Require(!Find.WindowStack.Windows.Contains(lastDialog), "window closes via actual input");
                    var help=(HelpTabView)Field(lastDialog,"help");
                    var content=Field(help,"content");
                    Require(((System.Collections.IDictionary)Field(content,"textures")).Count==0, "closing releases owned help textures");
                    Require(Field(lastDialog,"tabs")==null, "closing releases tab delegates");
                    Require(QualityJobsMod.Settings.helpTopicsRead.Count>0, "reading history persists outside render passes");
                    Mark("closed");
                }
                else if (action == "crafting")
                {
                    CloseDialogs();
                    var bench = Find.CurrentMap.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                        .First(b=>b.def.AllRecipes.Any(r=>ManagedRecipes.IsManagedRecipe(r)));
                    var recipe=bench.def.AllRecipes.First(r=>ManagedRecipes.IsManagedRecipe(r));
                    var bill=(Bill_Production)BillUtility.MakeNewBill(recipe);
                    bench.BillStack.AddBill(bill);
                    Commands.SetBillManaged(bill.GetUniqueLoadID(),true);
                    Commands.SetBillAutoBest(bill.GetUniqueLoadID(),true);
                    Commands.SetBillTargetQuality(bill.GetUniqueLoadID(),3);
                    CameraJumper.TryJump(bench);
                    Find.Selector.ClearSelection(); Find.Selector.Select(bench);
                    Find.WindowStack.Add((Window)AccessTools.Method(typeof(Bill_Production),"GetBillDialog").Invoke(bill,null));
                    Mark("crafting");
                }
                else if(action == "construction" || action == "rebuild")
                {
                    CloseDialogs();
                    Thing target;
                    var bed=Find.CurrentMap.listerBuildings.allBuildingsColonist.OfType<Building_Bed>().First(b=>b.TryGetComp<CompQuality>()!=null);
                    if(action == "rebuild") target=bed;
                    else
                    {
                        var position=CellFinderLoose.RandomCellWith(c=>c.Standable(Find.CurrentMap) && c.GetEdifice(Find.CurrentMap)==null,
                            Find.CurrentMap);
                        target=GenConstruct.PlaceBlueprintForBuild(DefDatabase<ThingDef>.GetNamed("Armchair"),position,Find.CurrentMap,Rot4.North,Faction.OfPlayer,ThingDefOf.Cloth);
                        Commands.SetPlanMinSkill(target.thingIDNumber,15);
                        Commands.SetPlanMinQuality(target.thingIDNumber,4);
                    }
                    Find.Selector.ClearSelection(); Find.Selector.Select(target); CameraJumper.TryJump(target);
                    Mark(action);
                }
                else if(action == "check-construction")
                {
                    Require(Find.WindowStack.WindowOfType<Dialog_ConstructionPlanConfig>()!=null,"real gizmo click opens construction controls");
                    Mark("construction-open");
                }
            }
            catch (Exception error) { failed=true; Mark("FAILED " + error.Message); Log.Error("[HelpProbe] " + error); }
        }
        private static void CloseDialogs()
        {
            foreach(var w in Find.WindowStack.Windows.ToArray())
                if(w is Dialog_BillConfig || w is Dialog_ConstructionPlanConfig || w.GetType().Name=="Dialog_QualityJobs") w.Close(false);
        }
    }
}
