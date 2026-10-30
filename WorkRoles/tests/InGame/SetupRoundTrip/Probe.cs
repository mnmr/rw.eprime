using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Profile;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace WorkRolesSetupVerification
{
    // Test-only observer. Production commands construct the fixture; the real
    // preview Apply and defaults choices are clicked through the managed pipe.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static int stage, settle;
        private static RoleStore store;
        private static MainTabWindow_WorkRoles window;
        private static string expected, saved, originalPreferences;
        private static object roster;
        private static DateTime savedTime;
        private static readonly Type Defaults = typeof(RoleIO).Assembly.GetType("WorkRoles.SetupDefaults");
        private static readonly Type Bridge = typeof(RoleIO).Assembly.GetType("WorkRoles.RoleSetupBridge");
        private static readonly Dictionary<string, string> points = new Dictionary<string, string>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static string DefaultPath => Path.Combine(GenFilePaths.ConfigFolderPath, "WorkRoles.Default.xml");
        private static string Mode => File.ReadAllText(Path.Combine(Root, "setup-mode.txt")).Trim();

        static Probe()
        {
            var harmony = new Harmony("eprime.workroles.setupprobe");
            harmony.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            harmony.Patch(AccessTools.Method(typeof(SavedGameLoaderNow), "LoadGameFromSaveFileNow"), prefix: new HarmonyMethod(typeof(Probe), nameof(Load)));
            harmony.Patch(AccessTools.Method(typeof(Dialog_PreviewBase), "DrawPreviewFooter"), prefix: new HarmonyMethod(typeof(Probe), nameof(Footer)));
            harmony.Patch(typeof(Widgets).GetMethods().Single(m => m.Name == "ButtonText" && m.GetParameters().Length == 6), prefix: new HarmonyMethod(typeof(Probe), nameof(Button)));
            harmony.Patch(AccessTools.Method(typeof(Widgets), "CheckboxLabeled"), prefix: new HarmonyMethod(typeof(Probe), nameof(Checkbox)));
        }

        private static object Call(Type type, string name, params object[] args) =>
            type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(null, args);
        private static void Set(object instance, string name, object value) => AccessTools.Field(instance.GetType(), name).SetValue(instance, value);
        private static object Get(object instance, string name) => AccessTools.Field(instance.GetType(), name)?.GetValue(instance)
            ?? AccessTools.Property(instance.GetType(), name).GetValue(instance);
        private static object Invoke(object instance, string name, params object[] args) => AccessTools.Method(instance.GetType(), name).Invoke(instance, args);
        private static string Preferences() => ((SetupOptions)Call(Bridge, "CapturePlayer")).ToXml("PlayerSettings").ToString();
        private static bool Load()
        {
            originalPreferences = Preferences();
            if (!Mode.StartsWith("newgame", StringComparison.Ordinal)) return true;
            Log.Message("[SetupProbe] Generating genuine new game through vanilla quick-test initialization");
            Root_Play.SetupForQuickTestPlay();
            Find.GameInitData.mapSize = 150;
            Find.GameInitData.PrepForMapGen();
            Find.Scenario.PreMapGenerate();
            Current.Game.InitNewGame();
            return false;
        }
        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Log.Message("[SetupProbe] PASS " + message);
            File.AppendAllText(Path.Combine(Root, "setup-results.txt"), "PASS " + message + Environment.NewLine);
        }
        private static void Mark(string message) => File.WriteAllText(Path.Combine(Root, "setup-stage.txt"), message);
        private static void Point(string name, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 point = GUIUtility.GUIToScreenPoint(rect.center);
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            string text = Math.Round(point.x).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(point.y).ToString(CultureInfo.InvariantCulture);
            if (points.TryGetValue(name, out string previous) && previous == text) return;
            points[name] = text;
            File.WriteAllText(Path.Combine(Root, "setup-" + name + ".txt"), text);
        }
        private static void Footer(Dialog_PreviewBase __instance, Rect inRect)
        { if (__instance is Dialog_ImportPreview) Point("apply", new Rect(inRect.xMax - 120, inRect.yMax - 32, 120, 32)); }
        private static void Button(Rect rect, string label)
        {
            if (label == "WR_NewGameDefaults".Translate().ToString()) Point("defaults", rect);
            if (label == "WR_DefaultUseCurrent".Translate().ToString()) Point("use", rect);
            if (label == "WR_DefaultKeep".Translate().ToString()) Point("keep", rect);
        }
        private static void Checkbox(Rect rect, string label)
        {
            if (label == "WR_SetupPlayerSettings".Translate().ToString()) Point("preferences", rect);
            if (label == "WR_DefaultAutoUpdate".Translate().ToString()) Point("auto", rect);
        }
        private static void OpenRoles()
        {
            var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
            window = (MainTabWindow_WorkRoles)button.TabWindow;
            var field = AccessTools.Field(window.GetType(), "curTab");
            field.SetValue(window, Enum.Parse(field.FieldType, "Roles"));
            Find.MainTabsRoot.SetCurrentTab(button);
        }
        private static Role Named(string name) => store.roles.Single(r => r.label == name);
        private static void Import(string xml, bool preferences = true)
        {
            var selection = new ImportSelection { xml = xml, roles = true, rolesOverwrite = true,
                palette = true, paletteOverwrite = true, gameSettings = true, playerSettings = preferences };
            Call(typeof(RoleIO), "ResolveImportLocations", selection, RoleIO.Parse(xml),
                Call(typeof(RoleIO).Assembly.GetType("WorkRoles.ColonyScope"), "Locations"));
            RoleCommands.ApplyImport(selection);
        }
        private static FileRole Role(string id, string label) => new FileRole
        {
            fileId = id, label = label, customName = true, groupId = "setup-group", group = "Setup workshop",
            hasTuning = true, minAge = 0, hasIcon = true,
            entries = new List<JobEntry> { new JobEntry(JobEntryKind.WorkType, "Crafting") }
        };
        private static void Prepare()
        {
            var doc = RoleIO.Parse(RoleIO.BuildXml(store));
            var apprentice = Role("setup-apprentice", "Setup apprentice");
            apprentice.iconPath = "WorkRoles/RoleIcons/sowing-seeds";
            apprentice.colorRef = "setup-used";
            apprentice.requiredSkills.Add("Crafting");
            var maker = Role("setup-maker", "Setup maker");
            maker.training.Add(new FileTrainingPathEntry(apprentice.fileId, apprentice.label, 0, 12));
            maker.training.Add(new FileTrainingPathEntry(maker.fileId, maker.label, 8, 21));
            maker.iconPath = "";
            var team = Role("setup-team", "Setup team");
            team.composite = true; team.entries.Clear();
            team.members.Add(new FileRoleReference(maker.fileId, maker.label));
            team.members.Add(new FileRoleReference(apprentice.fileId, apprentice.label));
            var restricted = Role("setup-location", "Setup location");
            restricted.locations.Add("settlement:Old,caravans"); restricted.activeHours = 0x00FFFF;
            var unavailable = Role("setup-unavailable", "Setup unavailable");
            unavailable.entries.Add(new JobEntry(JobEntryKind.WorkGiver, "OptionalModCraft"));
            unavailable.requiredSkills.Add("OptionalModSkill"); unavailable.iconPath = "OptionalMod/Icons/Maker";
            unavailable.enabled = false; unavailable.excludedFromRecommendations = true;
            doc.groups.Insert(0, "Setup workshop");
            doc.groupsWithIds.Insert(0, new FileGroup { fileId = "setup-group", name = "Setup workshop" });
            doc.roles.InsertRange(0, new[] { maker, apprentice, team, restricted, unavailable });
            doc.palette.Clear();
            doc.palette.Add(("setup-unused", new ColorRgb(0x12 / 255f, 0x34 / 255f, 0x56 / 255f)));
            doc.palette.Add(("setup-used", new ColorRgb(0xAB / 255f, 0xCD / 255f, 0xEF / 255f)));
            doc.gameSettings.values["useWorkPriorities"] = "false";
            doc.gameSettings.values["reportVanillaPriorities"] = "false";
            doc.gameSettings.values["rolePriorityEmergencyRule"] = "false";
            doc.gameSettings.values["recommendation.championSkillDivisor"] = "7";
            doc.playerSettings.values["chipGrid"] = "false";
            doc.playerSettings.skillColumns = new List<string> { "Crafting", "OptionalModSkill" };
            doc.playerSettings.values["sortColumn"] = "OptionalModSkill";
            RoleCommands.CreateRole("Setup deletion trigger");
            Import(RoleFile.Build(doc));
            Assert(!store.roles.Any(r => r.label == "Setup deletion trigger"), "overwrite removes unmatched role");
            Assert(store.roles.Take(5).Select(r => r.label).SequenceEqual(new[] { "Setup maker", "Setup apprentice", "Setup team", "Setup location", "Setup unavailable" }), "overwrite restores authored catalog order");
            VerifySetup();
            RoleCommands.DuplicateRole(Named("Setup unavailable").id, "Setup copied missing content");
            RoleCommands.ScrubDeadEntries(Named("Setup copied missing content").id);
            Assert(Named("Setup copied missing content").entries.Any(e => e.DefName == "OptionalModCraft"), "duplicating imported role retains unavailable jobs");
            RoleCommands.DeleteRole(Named("Setup copied missing content").id);
            expected = RoleIO.BuildXml(store);
            File.WriteAllText(Path.Combine(Root, "setup-export.xml"), expected);
            RoleCommands.SetRoleIcon(Named("Setup apprentice").id, null);
            RoleCommands.MoveRoleInCatalog(0, store.roles.Count - 1);
            RoleCommands.SetReportVanillaPriorities(true);
            WorkRolesMod.Settings.chipGrid = true;
            WorkRolesMod.Settings.skillColumns = new List<string> { "Shooting" };
            OpenRoles(); Call(Defaults, "ProcessPending");
            roster = Get(Get(window, "colonistsTab"), "rosterState");
            Assert(((SkillDef)Invoke(Get(roster, "SkillColumns"), "At", 0)).defName == "Shooting", "open roster starts with previous skill columns");
            var dialog = new Dialog_ImportPreview(expected, RoleIO.Parse(expected));
            Set(dialog, "rolesOverwrite", true); Set(dialog, "paletteOverwrite", true);
            Set(dialog, "rowLayoutDirty", true); Set(dialog, "validationDirty", true);
            Set(dialog, "scroll", new Vector2(0, 100000));
            Find.WindowStack.Add(dialog);
            stage = 1; Mark("import");
        }
        private static void VerifySetup()
        {
            Assert(store.GroupById(Named("Setup maker").groupId)?.label == "Setup workshop"
                && Named("Setup maker").groupId == Named("Setup apprentice").groupId,
                "group membership survives overwrite deletion and new-world import");
            Assert(Named("Setup team").memberRoleIds.Select(id => store.RoleById(id).label).SequenceEqual(new[] { "Setup maker", "Setup apprentice" }), "ordered composite members survive");
            Assert(Named("Setup maker").trainingRoleIds.Select(id => store.RoleById(id).label).SequenceEqual(new[] { "Setup apprentice", "Setup maker" }), "training path resolves to imported roles");
            Assert(Named("Setup apprentice").iconPath == "WorkRoles/RoleIcons/sowing-seeds", "custom icon survives");
            Assert(Named("Setup maker").iconPath == "", "explicitly hidden icon survives");
            Assert(Named("Setup location").locationTokens.SequenceEqual(new[] { "unresolved:settlement:Old,caravans" }), "unmatched named location remains restricted and portable");
            Assert(!LocationRules.Matches(Named("Setup location").locationTokens, default), "unmatched location cannot activate work anywhere");
            Role savedRole = Named("Setup location");
            string path = Path.Combine(Root, "setup-role-save.xml");
            Scribe.saver.InitSaving(path, "SetupLocationRoundTrip");
            Scribe_Deep.Look(ref savedRole, "role"); Scribe.saver.FinalizeSaving();
            Role loadedRole = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loadedRole, "role"); Scribe.loader.FinalizeLoading();
            Assert(loadedRole.locationTokens.SequenceEqual(savedRole.locationTokens)
                && !LocationRules.Matches(loadedRole.locationTokens, default), "comma in unavailable location name survives real Scribe save/reload without broadening rules");
            RoleCommands.ScrubDeadEntries(Named("Setup unavailable").id);
            Assert(Named("Setup unavailable").entries.Any(e => e.DefName == "OptionalModCraft") && Named("Setup unavailable").requiredSkills.Contains("OptionalModSkill"), "unavailable jobs and skills survive runtime cleanup");
            Assert(RoleIO.Parse(RoleIO.BuildXml(store)).palette.Any(p => p.name == "setup-unused"), "unused palette color survives");
            Assert(!Current.Game.playSettings.useWorkPriorities && !store.reportVanillaPriorities && !store.rolePriorityEmergencyRule, "save-wide options survive");
            Assert(store.recommendationTuning.Get(RecommendationTuningOption.ChampionSkillDivisor) == 7, "global recommendation tuning survives");
        }
        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (settle++ < 8) return;
                if (stage == 0)
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                    foreach (Window popup in Find.WindowStack.Windows.ToArray()) if (!(popup is MainTabWindow)) popup.Close(false);
                    store = RoleStore.Current;
                    if (Mode == "roundtrip") { Prepare(); return; }
                    saved = File.ReadAllText(DefaultPath); savedTime = File.GetLastWriteTimeUtc(DefaultPath);
                    if (Mode.StartsWith("newgame", StringComparison.Ordinal))
                    {
                        Assert((bool)AccessTools.Property(typeof(RoleStore), "IsNewWorld").GetValue(store), "real new-world lifecycle reached");
                        VerifySetup();
                        Assert(Preferences() == originalPreferences, "new-game defaults preserve local display preferences");
                        Assert(Find.CurrentMap.mapPawns.FreeColonistsSpawned.Count == 3, "new colony and starting pawns were generated");
                        Assert(Find.CurrentMap.mapPawns.FreeColonistsSpawned.Any(p => p.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)), "starting colonists retain usable work priorities");
                        if (Mode == "newgame-sparse")
                            Assert(store.roles.Count == 5, "sparse personal default is not expanded with generated or carrier roles");
                    }
                    else Assert(!store.roles.Any(r => r.label == "Setup maker"), "loading an existing save does not apply new-game defaults");
                    Assert(File.ReadAllText(DefaultPath) == saved && File.GetLastWriteTimeUtc(DefaultPath) == savedTime, "load and generation do not rewrite personal default");
                    File.WriteAllText(Path.Combine(Root, "setup-reexport.xml"), RoleIO.BuildXml(store));
                    stage = 99; Mark("SUCCESS"); return;
                }
                if (stage == 1 && Find.WindowStack.WindowOfType<Dialog_ImportPreview>() == null)
                {
                    string actual = RoleIO.BuildXml(store);
                    File.WriteAllText(Path.Combine(Root, "setup-reexport.xml"), actual);
                    Assert(actual == expected, "complete UI import/export round trip preserves exact canonical setup");
                    Assert(((SkillDef)Invoke(Get(roster, "SkillColumns"), "At", 0)).defName == "Crafting", "open roster immediately observes imported skill columns");
                    Invoke(roster, "ToggleSkillColumn", DefDatabase<SkillDef>.GetNamed("Shooting"));
                    Assert(WorkRolesMod.Settings.skillColumns.Contains("Crafting") && WorkRolesMod.Settings.skillColumns.Contains("OptionalModSkill"), "editing columns preserves imported and unavailable columns");
                    Import(expected);
                    VerifySetup();
                    int version = UiVersion.Current;
                    Import(expected);
                    Assert(UiVersion.Current == version, "identical complete import preserves revisions");
                    var legacy = XElement.Parse(expected); legacy.SetAttributeValue("version", "13");
                    legacy.Element("GameSettings").Remove(); legacy.Element("PlayerSettings").Remove();
                    foreach (var icon in legacy.Descendants("Icon").ToList()) icon.Remove();
                    RoleCommands.SetRoleIcon(Named("Setup apprentice").id, "legacy-icon");
                    RoleCommands.SetReportVanillaPriorities(true);
                    Import(legacy.ToString());
                    Assert(Named("Setup apprentice").iconPath == "legacy-icon" && store.reportVanillaPriorities, "legacy export preserves absent icons and settings");
                    Import(expected);
                    string before = RoleIO.BuildXml(store);
                    Import("<WorkRoles version=\"14\"><Roles><Role name=\"Broken\" composite=\"true\"><Members><Role roleId=\"missing\">Lost</Role></Members></Role></Roles></WorkRoles>");
                    Assert(RoleIO.BuildXml(store) == before, "invalid dependency is rejected without partial mutation");
                    WorkRolesMod.Settings.autoUpdateDefault = false;
                    stage = 2; settle = 0;
                }
                else if (stage == 2) { stage = 3; Mark("roles"); }
                else if (stage == 3 && File.Exists(DefaultPath))
                {
                    Assert(WorkRolesMod.Settings.autoUpdateDefault, "defaults dialog enables auto-update by explicit choice");
                    Assert(File.ReadAllText(DefaultPath) == RoleIO.BuildXml(store), "defaults dialog saves complete setup");
                    savedTime = File.GetLastWriteTimeUtc(DefaultPath);
                    Call(Defaults, "QueueSave", true); stage = 4; settle = 0;
                }
                else if (stage == 4)
                {
                    Assert(File.GetLastWriteTimeUtc(DefaultPath) == savedTime, "saving unchanged default preserves file timestamp");
                    RoleCommands.SetRoleCoverage(Named("Setup maker").id, 47);
                    window.Close(); stage = 5; settle = 0;
                }
                else if (stage == 5)
                {
                    saved = File.ReadAllText(DefaultPath);
                    Assert(RoleIO.Parse(saved).roles.Single(r => r.label == "Setup maker").coverage == 47, "closing editor flushes authorized authored changes");
                    // Reset only the local decision session, like loading a different
                    // existing save. No world lifecycle flags are fabricated.
                    Call(Defaults, "Dispose");
                    RoleCommands.SetRoleCoverage(Named("Setup maker").id, 48);
                    OpenRoles(); stage = 6; settle = 0;
                }
                else if (stage == 6) { stage = 7; Mark("conflict"); }
                else if (stage == 7 && Find.WindowStack.Windows.All(w => w.GetType().Name != "Dialog_SetupChoice"))
                {
                    RoleCommands.SetRoleCoverage(Named("Setup maker").id, 49);
                    window.Close(); stage = 8; settle = 0;
                }
                else if (stage == 8)
                {
                    Assert(File.ReadAllText(DefaultPath) == saved, "Keep default protects it from edits in a differing save");
                    var sparse = RoleIO.Parse(saved);
                    sparse.roles.RemoveAll(r => !r.label.StartsWith("Setup ", StringComparison.Ordinal));
                    sparse.groups.Clear(); sparse.groupsWithIds.Clear();
                    sparse.groups.Add("Setup workshop");
                    sparse.groupsWithIds.Add(new FileGroup { fileId = sparse.roles[0].groupId, name = "Setup workshop" });
                    File.WriteAllText(Path.Combine(Root, "setup-sparse-default.xml"), RoleFile.Build(sparse));
                    stage = 9; Mark("protected");
                }
                else if (stage == 9 && File.Exists(Path.Combine(Root, "setup-teardown.txt")))
                {
                    Call(Defaults, "QueueSave", true); Call(Defaults, "ProcessPending");
                    RoleCommands.SetRoleCoverage(Named("Setup maker").id, 51);
                    MemoryUtility.ClearAllMapsAndWorld();
                    Assert(RoleIO.Parse(File.ReadAllText(DefaultPath)).roles.Single(r => r.label == "Setup maker").coverage == 51,
                        "real world teardown flushes authorized edits before clearing the store");
                    Assert(AccessTools.Field(Defaults, "owner").GetValue(null) == null, "world teardown releases the default session and store");
                    Call(Defaults, "Dispose");
                    GenScene.GoToMainMenu(); stage = 99; Mark("SUCCESS");
                }
            }
            catch (Exception error)
            {
                stage = 99; Mark("FAILED " + error);
                Log.Error("[SetupProbe] " + error);
            }
        }
    }
}
