using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

// Disposable main-thread observer. All clicks come through Shared automation;
// setup uses production commands, and assertions inspect authoritative results.
namespace WorkRolesExclusionVerification
{
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static readonly Harmony Patches = new Harmony("eprime.workroles.exclusionprobe");
        private static MainTabWindow_WorkRoles window;
        private static Role role;
        private static RoleStore store;
        private static List<Pawn> pawns;
        private static Pawn pinnedHolder, unpinnedHolder;
        private static int stage, settle;
        private static string requested = "";
        private static readonly Dictionary<string, string> Points = new Dictionary<string, string>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        private static readonly BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static Probe()
        {
            Patches.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            Patches.Patch(AccessTools.Method(typeof(RecommendationsTabView), "DrawExcludedRoles"), prefix: new HarmonyMethod(typeof(Probe), nameof(Panel)));
            Patches.Patch(AccessTools.Method(typeof(FloatMenuOption), "DoGUI"), prefix: new HarmonyMethod(typeof(Probe), nameof(Menu)));
            Patches.Patch(AccessTools.Method(typeof(Dialog_PreviewBase), "DrawPreviewFooter"), prefix: new HarmonyMethod(typeof(Probe), nameof(Footer)));
        }

        private static object Read(object value, string name)
        {
            Type type = value as Type ?? value.GetType();
            object instance = value is Type ? null : value;
            FieldInfo field = AccessTools.Field(type, name);
            return field != null ? field.GetValue(instance) : AccessTools.Property(type, name).GetValue(instance, null);
        }
        private static object Call(object value, string name, params object[] args)
        {
            Type type = value as Type ?? value.GetType();
            return type.GetMethods(All).Single(m => m.Name == name && m.GetParameters().Length == args.Length)
                .Invoke(value is Type ? null : value, args);
        }
        private static void Assert(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Log.Message("[ExclusionProbe] PASS " + message);
        }
        private static void Mark(string value) => File.WriteAllText(Path.Combine(Root, "exclusion-stage.txt"), value);
        private static void Point(string name, Vector2 local)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 point = GUIUtility.GUIToScreenPoint(local);
            // Both runtimes scale the local coordinates but report the current
            // GUI group's origin in logical units. Include every nested group.
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            string value = Math.Round(point.x).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(point.y).ToString(CultureInfo.InvariantCulture);
            if (Points.TryGetValue(name, out string previous) && previous == value) return;
            Points[name] = value;
            File.WriteAllText(Path.Combine(Root, "exclusion-" + name + ".txt"), value);
        }
        private static void Panel(Rect panel, object exclusions)
        {
            Vector2 origin = panel.position + new Vector2(8f, 8f);
            Point("add", origin + ((Rect)Read(exclusions, "AddRect")).center);
            for (int i = 0; i < (int)Read(exclusions, "Count"); i++)
            {
                object chip = Call(exclusions, "ChipAt", i);
                if ((int)Read(Read(chip, "Chip"), "RoleId") != role?.id) continue;
                Rect rect = (Rect)Read(chip, "Rect");
                Point("remove", origin + new Vector2(rect.xMax - 8f, rect.center.y));
                Point("hover", origin + rect.center);
            }
        }
        private static void Menu(FloatMenuOption __instance, Rect rect, FloatMenu floatMenu)
        {
            if (role != null && __instance.Label == role.label)
            {
                Point("menu", rect.center);
            }
        }
        private static void Footer(Dialog_PreviewBase __instance, Rect inRect)
            => Point("apply", new Vector2(inRect.xMax - 60f, inRect.yMax - 16f));
        private static void SelectTab(string name)
        {
            FieldInfo field = AccessTools.Field(window.GetType(), "curTab");
            field.SetValue(window, Enum.Parse(field.FieldType, name));
        }
        private static RecommendationPlan Plan() => RecommendationPlan.Build(RecsAdapter.BuildColonyView(store, pawns), store.recommendationTuning);
        private static bool Has(RecommendationPlan plan, int roleId)
        {
            for (int p = 0; p < plan.PawnCount; p++)
                for (int r = 0; r < plan.RoleCountAt(p); r++)
                    if (plan.RoleAt(p, r) == roleId) return true;
            return false;
        }
        private static void Update()
        {
            if (stage == 99 || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            try
            {
                if (settle++ < 6) return;
                string control = Path.Combine(Root, "exclusion-control.txt");
                if (File.Exists(control)) requested = File.ReadAllText(control).Trim();
                if (stage == 0)
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                    foreach (Window popup in Find.WindowStack.Windows.ToArray()) if (!(popup is MainTabWindow)) popup.Close(false);
                    store = RoleStore.Current;
                    Assert(store != null, "real save loaded");
                    pawns = Find.CurrentMap.mapPawns.FreeColonistsSpawned.ToList();
                    VerifySeededDefaults();
                    role = store.roles.Single(r => r.templateDefName == "WS_Hunter");
                    RoleCommands.SetRoleRecommendationExcluded(role.id, false);
                    var ordering = (IReadOnlyList<int>)Call(typeof(RecsAdapter), "ResolvedRecommendationOrder", store);
                    string automatic = Signature(Plan());
                    RoleCommands.SetRecommendationOrder(new[] { role.id }.Concat(ordering.Reverse()).ToList());
                    Assert(!((IReadOnlyList<int>)Call(typeof(RecsAdapter), "ResolvedRecommendationOrder", store)).Contains(role.id),
                        "legacy Hunter ordering override is ignored");
                    Assert(Signature(Plan()) == automatic, "legacy reversed order does not change any pawn's automatic recommendations");
                    Assert(Has(Plan(), role.id), "Hunter is recommended with automatic shooting-skill placement before exclusion");
                    pinnedHolder = pawns.First(p => p.equipment?.Primary != null && p.equipment.Primary.def.IsRangedWeapon);
                    unpinnedHolder = pawns.First(p => p != pinnedHolder && !p.Downed);
                    RoleCommands.AssignRole(pinnedHolder, role.id);
                    if (!Assignment(pinnedHolder).pinned) RoleCommands.ToggleAssignmentPin(pinnedHolder, role.id);
                    RoleCommands.SetAssignmentState(pinnedHolder, role.id, (int)AssignmentState.Disabled);
                    PrepareUnpinnedHolder();
                    var button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
                    window = (MainTabWindow_WorkRoles)button.TabWindow;
                    Find.MainTabsRoot.SetCurrentTab(button);
                    SelectTab("Recommendations");
                    stage = 10; settle = 0;
                }
                else if (stage == 10)
                {
                    AssertRoleOptions(false);
                    stage = 1; Mark("ready");
                }
                else if (stage == 1 && role.excludedFromRecommendations)
                {
                    Assert(!Has(Plan(), role.id), "UI exclusion removes Hunter from every pawn's recommendations while paused");
                    Assert(store.pawnSets.Values.Any(s => s.assignments.Any(a => a.roleId == role.id)), "exclusion edit leaves existing assignments untouched");
                    int revision = UiVersion.Current;
                    RoleCommands.SetRoleRecommendationExcluded(role.id, true);
                    Assert(UiVersion.Current == revision, "repeated exclusion command preserves revisions");
                    RoundTripRole();
                    // The command runs in the click's GUI pass. Inspect the
                    // next published view after that pass has finished.
                    stage = 11; settle = 0;
                }
                else if (stage == 11)
                {
                    AssertRoleOptions(true);
                    stage = 2; Mark("excluded");
                }
                else if (stage == 2 && requested == "preview")
                {
                    SelectTab("Colonists");
                    stage = 3; settle = 0;
                }
                else if (stage == 3)
                {
                    Call(Read(window, "colonistsTab"), "ShowFixPreview");
                    stage = 4; Mark("preview");
                }
                else if (stage == 4 && Find.WindowStack.WindowOfType<Dialog_ChangesPreview>() == null)
                {
                    AssertPinsAndExclusions("Fix My Colony Apply");
                    SelectTab("Recommendations");
                    stage = 5; Mark("applied");
                }
                else if (stage == 5 && !role.excludedFromRecommendations)
                {
                    Assert(Has(Plan(), role.id), "removing exclusion chip restores Hunter recommendations while paused");
                    stage = 12; settle = 0;
                }
                else if (stage == 12)
                {
                    AssertRoleOptions(false);
                    stage = 6; Mark("restored");
                }
                else if (stage == 6 && requested == "finish")
                {
                    PrepareUnpinnedHolder();
                    Assert(Assignment(unpinnedHolder) != null, "unpinned Hunter assignment exists before auto-optimize");
                    RoleCommands.SetRoleRecommendationExcluded(role.id, true);
                    RoleCommands.SetAutoOptimize(true);
                    Call(typeof(RoleStore).Assembly.GetType("WorkRoles.AutoOptimizer"), "RunForMap", Find.CurrentMap);
                    AssertPinsAndExclusions("Auto-optimize");
                    RoleCommands.SetAutoOptimize(false);
                    stage = 99; Mark("SUCCESS");
                    Log.Message("[ExclusionProbe] SUCCESS");
                    Patches.UnpatchAll(Patches.Id);
                }
            }
            catch (Exception error)
            {
                stage = 99;
                Log.Error("[ExclusionProbe] FAILED " + error);
                Mark("FAILED " + error);
            }
        }
        private static void VerifySeededDefaults()
        {
            string[] defaults = { "WS_Rescuer", "WS_Firefighter", "WS_Patient", "WS_Bedrest", "WS_Laborer" };
            var existing = defaults.Select(name => store.roles.Single(r => r.templateDefName == name)).ToArray();
            Assert(store.defaultRecommendationExclusionsSeeded && existing.All(r => r.excludedFromRecommendations),
                "existing save adopts all five default exclusions on its first upgraded load");
            // A saved opt-in must survive the normal load-time seeding guard.
            foreach (Role saved in existing) RoleCommands.SetRoleRecommendationExcluded(saved.id, false);
            Call(typeof(RoleStore).Assembly.GetType("WorkRoles.Seeding"), "SeedIfNeeded");
            Assert(existing.All(r => !r.excludedFromRecommendations), "existing exclusion choices survive default seeding");
            RoundTripDefaultExclusions(existing);

            // Restoring missing templates exercises the same production creation
            // path as a newly seeded save, using the game's loaded XML definitions.
            foreach (Role saved in existing) RoleCommands.DeleteRole(saved.id);
            RoleCommands.RestoreSelected(new RestoreSelection { templateDefs = defaults.ToList() });
            foreach (string name in defaults)
            {
                Role restored = store.roles.Single(r => r.templateDefName == name);
                Assert(restored.excludedFromRecommendations, name + " is excluded when seeded from its shipped definition");
                Assert(!Has(Plan(), restored.id), name + " is absent from recommendations");
            }
            RecommendationPlan plan = Plan();
            Assert(Has(plan, store.roles.Single(r => r.templateDefName == "WS_Core").id)
                && Has(plan, store.roles.Single(r => r.templateDefName == "WS_Basics").id),
                "Core and Basics remain recommended with all five defaults excluded");
        }

        private static void RoundTripDefaultExclusions(Role[] existing)
        {
            // A detached store uses real RoleStore serialization and PostLoadInit
            // without saving unresolved pawn references from the running world.
            // The source roles already contain the player's removed exclusions.
            RoleStore saved = new RoleStore(null)
            {
                seeded = true,
                defaultRecommendationExclusionsSeeded = store.defaultRecommendationExclusionsSeeded,
                roles = new List<Role>(existing),
            };
            string path = Path.Combine(Root, "default-exclusions-save.xml");
            try
            {
                Scribe.saver.InitSaving(path, "DefaultExclusionRoundTrip");
                Scribe_Deep.Look(ref saved, "store", new object[] { null });
                Scribe.saver.FinalizeSaving();
                RoleStore loaded = null;
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref loaded, "store", new object[] { null });
                Scribe.loader.FinalizeLoading();
                Assert(loaded != null && loaded.defaultRecommendationExclusionsSeeded
                    && loaded.roles.Count == existing.Length
                    && loaded.roles.All(r => !r.excludedFromRecommendations),
                    "save/load preserves the one-time marker and all five player-removed exclusions");
            }
            finally
            {
                // RoleStore's constructor caches its instance; restore the live
                // owner through the normal world-identity guard after this fixture.
                Assert(ReferenceEquals(RoleStore.Current, store), "detached save fixture releases the live store cache");
            }
        }

        private static RoleAssignment Assignment(Pawn pawn)
            => store.pawnSets.TryGetValue(pawn, out PawnRoleSet set)
                ? set.assignments.FirstOrDefault(a => a.roleId == role.id) : null;
        private static string Signature(RecommendationPlan plan)
            => string.Join(";", Enumerable.Range(0, plan.PawnCount).Select(p =>
                string.Join(",", Enumerable.Range(0, plan.RoleCountAt(p)).Select(r => plan.RoleAt(p, r)))));
        private static void AssertRoleOptions(bool excluded)
        {
            object state = Read(Read(window, "recommendationsTab"), "state");
            object panels = Read(state, "Panels");
            var actual = Enumerable.Range(0, (int)Read(panels, "Count"))
                .Select(i => (int)Read(Read(Call(panels, "PanelAt", i), "Chip"), "RoleId")).ToArray();
            Assert(actual.OrderBy(i => i).SequenceEqual(store.roles.Where(r => !r.excludedFromRecommendations)
                .Select(r => r.id).OrderBy(i => i)), "Role Options lists every non-excluded role while paused");
            Assert(actual.Contains(role.id) != excluded, "Hunter options follow exclusion without requiring an ordering chip");
            float width = (float)Read(state, "panelsWidth");
            Call(state, "EnsurePanels", width);
            Assert(ReferenceEquals(panels, Read(state, "Panels")), "unchanged Role Options reuse the published snapshot");
        }
        private static void PrepareUnpinnedHolder()
        {
            RoleCommands.AssignRole(unpinnedHolder, role.id);
            if (Assignment(unpinnedHolder).pinned) RoleCommands.ToggleAssignmentPin(unpinnedHolder, role.id);
        }
        private static void AssertPinsAndExclusions(string operation)
        {
            RoleAssignment kept = Assignment(pinnedHolder);
            Assert(kept != null && kept.pinned && kept.state == AssignmentState.Disabled,
                operation + " preserves the actual pinned Hunter assignment and its disabled state");
            Assert(Assignment(unpinnedHolder) == null,
                operation + " removes the actual unpinned excluded Hunter assignment");
            Assert(!Has(Plan(), role.id), operation + " does not recommend the excluded role despite its retained pin");
        }
        private static void RoundTripRole()
        {
            string path = Path.Combine(Root, "excluded-role-save.xml");
            Role saved = role;
            Scribe.saver.InitSaving(path, "ExclusionRoundTrip");
            Scribe_Deep.Look(ref saved, "role");
            Scribe.saver.FinalizeSaving();
            Role loaded = null;
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "role");
            Scribe.loader.FinalizeLoading();
            Assert(loaded != null && loaded.id == role.id && loaded.excludedFromRecommendations, "RimWorld save/load preserves exclusion flag");
        }
    }
}
