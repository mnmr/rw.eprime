using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace WorkRolesMechanicalVerification
{
    public static partial class Probe
    {
        private static object presentationView;
        private static Pawn builder;
        private static Role presentationFarm, presentationMixed, presentationClean;
        private static int presentationScene, presentationFrames;
        private static object omniStats;
        private static readonly HashSet<string> observedNames = new HashSet<string>();
        private static readonly HashSet<string> overflowingNames = new HashSet<string>();
        private static readonly string[] sceneNames = { "cleaner-native-none", "cleaner-native-race", "omni-long-name", "omni-paused-rename", "constructoid-skills" };
        private static object InvokePresentation(object target, string method, params object[] args) =>
            AccessTools.GetDeclaredMethods(target.GetType()).Single(m => m.Name == method && m.GetParameters().Length == args.Length).Invoke(target, args);
        private static object Field(object target, string name) => AccessTools.Field(target.GetType(), name).GetValue(target);
        private static object Property(object target, string name) => AccessTools.Property(target.GetType(), name).GetValue(target);
        private static Role PresentationRole(string name, params string[] givers)
        {
            RoleCommands.CreateRole(name);
            Role role = RoleStore.Current.roles.Single(r => r.label == name);
            foreach (string giver in givers) RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giver));
            return role;
        }
        private static void PrepareRobotsWithoutBiotech()
        {
            var factory = AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotCreator"), "CreateRobot", new[] { typeof(string), typeof(IntVec3), typeof(Map) });
            IntVec3 cell = map.AllCells.First(c => c.x > 8 && c.z > 8 && CellRect.CenteredOn(c, 5, 5).Cells.All(x => x.Standable(map) && x.GetEdifice(map) == null));
            workers = new[] {
                (Pawn)factory.Invoke(null, new object[] { "AIRobot_Cleaner", cell, map }),
                (Pawn)factory.Invoke(null, new object[] { "RPP_Bot_Omni_V", cell + IntVec3.East * 3, map }) };
        }
        private static void PreparePresentation()
        {
            AccessTools.Field(AccessTools.TypeByName("LudeonTK.EditWindow_Log"), "canAutoOpen").SetValue(null, false);
            AccessTools.Field(AccessTools.TypeByName("LudeonTK.EditWindow_Log"), "wantsToOpen").SetValue(null, false);
            presentationFarm = PresentationRole("UI farming", "GrowerSow");
            presentationClean = PresentationRole("UI cleaning", "CleanFilth");
            presentationMixed = PresentationRole("UI clean and farm", "CleanFilth", "GrowerSow");
            if (ModsConfig.BiotechActive)
            {
                builder = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Mech_Constructoid"), Faction.OfPlayer);
                GenSpawn.Spawn(builder, workers[2].Position + IntVec3.North * 3, map);
            }
            workers[1].Name = new NameSingle("WR Mehrzweckroboter mit aussergewoehnlich langem Namen 12345");
            foreach (Pawn worker in workers)
                RoleCommands.PasteRoleSet(worker, new List<RoleAssignment> {
                    new RoleAssignment { roleId = presentationClean.id },
                    new RoleAssignment { roleId = presentationFarm.id },
                    new RoleAssignment { roleId = presentationMixed.id } });
            foreach (Window popup in Find.WindowStack.Windows.ToArray()) if (!(popup is MainTabWindow)) popup.Close(false);
            Find.Selector.ClearSelection(); Find.Selector.Select(workers[0]);
            WorkRolesMod.Settings.groupBy = "none";
            MainButtonDef button = DefDatabase<MainButtonDef>.AllDefsListForReading.First(d => d.TabWindow is MainTabWindow_WorkRoles);
            presentationView = Field(button.TabWindow, "colonistsTab");
            // Restrict only the test view's cohort, keeping native names intact.
            var profile = (ColonistsViewProfile)Field(presentationView, "profile");
            profile.PawnsIn = _ => builder == null ? workers.ToList() : workers.Concat(new[] { builder }).ToList();
            // Set the test cohort before PreOpen captures it. Removing Biotech
            // from the source save can leave unrelated colonists with missing
            // gene defs; they are outside this native robot UI scenario.
            Find.MainTabsRoot.SetCurrentTab(button);
            InvokePresentation(presentationView, "InvalidatePawnSnapshot");
            new Harmony("eprime.workroles.mechanicalprobe.names").Patch(
                AccessTools.Method(typeof(Widgets), "Label", new[] { typeof(Rect), typeof(string) }),
                prefix: new HarmonyMethod(typeof(Probe), nameof(ObservePresentationLabel)));
            phase = 100;
            SelectPresentationPawn(workers[0]);
            Mark("RUNNING presentation");
        }
        private static void SelectPresentationPawn(Pawn pawn)
        {
            object selection = Field(presentationView, "selection");
            var click = AccessTools.Method(selection.GetType(), "Click");
            click.Invoke(selection, click.GetParameters().Select((p, i) => i == 0 ? (object)pawn : p.DefaultValue).ToArray());
        }
        private static void ObservePresentationLabel(Rect __0, string __1)
        {
            if (done || phase != 100 || string.IsNullOrEmpty(__1) || __0.width > 150f) return;
            bool name = __1.StartsWith("WR ") || workers.Any(p => p.LabelShortCap.ToString().StartsWith(__1.TrimEnd('.', '…')))
                || builder != null && builder.LabelShortCap.ToString().StartsWith(__1.TrimEnd('.', '…'));
            if (!name) return;
            string key = presentationScene + ":" + __0.width + ":" + __1;
            if (!observedNames.Add(key)) return;
            bool fits = Text.CalcSize(__1).x <= __0.width + 0.01f && __0.height >= Mathf.Ceil(Text.LineHeight) && !Text.WordWrap;
            ReviewCheck(fits, "name fits one measured line: " + key + " height=" + __0.height + " line=" + Text.LineHeight);
            if (!fits) overflowingNames.Add(key);
        }
        private static void VerifyPresentationFacts()
        {
            object stats = Field(presentationView, "statsState");
            omniStats = InvokePresentation(stats, "Snapshot", workers[1]);
            foreach (Pawn pawn in builder == null ? new[] { workers[0] } : new[] { workers[0], workers[2] })
            {
                object sequence = InvokePresentation(presentationView, "ChipSequenceFor", pawn, RoleStore.Current);
                foreach (var expected in new[] { (presentationFarm, "Critical"), (presentationMixed, "Caution"), (presentationClean, "None") })
                {
                    object chip = Enumerable.Range(0, (int)Property(sequence, "Count"))
                        .Select(i => InvokePresentation(sequence, "ChipAt", i))
                        .Single(c => (int)Property(Property(c, "RenderData"), "RoleId") == expected.Item1.id);
                    object result = Property(chip, "Capability");
                    string severity = Property(result, "WarningSeverity").ToString();
                    ReviewCheck(severity == expected.Item2, pawn.def.defName + " " + expected.Item1.label + " warning=" + severity + " expected=" + expected.Item2);
                    if (expected.Item2 != "None") ReviewCheck(!string.IsNullOrEmpty((string)Property(result, "Tooltip")), "unavailable work warning explains why");
                }
                IDictionary verdicts = (IDictionary)InvokePresentation(presentationView, "VerdictsFor", pawn);
                object verdict = verdicts[presentationFarm.id];
                ReviewCheck(Field(verdict, "Item2").ToString() == "Awful", pawn.def.defName + " impossible farming has two red stars");
                ReviewCheck(CompiledJobOrders.NormalFor(pawn).All(g => g.def.workType == WorkTypeDefOf.Cleaning), "manual impossible assignments never grant unsupported work");
                List<SkillLine> skills = PublishedSkills(stats, pawn);
                ReviewCheck(skills.Count == DefDatabase<SkillDef>.DefCount && skills.All(s => s.Disabled), pawn.def.defName + " has no usable skilled work in stats");
            }
            SkillLine omniConstruction = PublishedSkills(stats, workers[1]).Single(s => s.Def == SkillDefOf.Construction);
            ReviewCheck(!omniConstruction.Disabled && omniConstruction.Level == workers[1].skills.GetSkill(SkillDefOf.Construction).Level,
                "omni robot preserves its real usable construction skill");
            ReviewCheck(omniConstruction.ValueText == omniConstruction.Level.ToString() && omniConstruction.Passion == Passion.None,
                "fixed robot skills have no fractional learning progress or human passion");
            if (builder != null)
            {
                var builderSkills = PublishedSkills(stats, builder);
                SkillLine construction = builderSkills.FirstOrDefault(s => s.Def == SkillDefOf.Construction);
                ReviewCheck(construction.Def != null && !construction.Disabled && construction.Level == builder.RaceProps.mechFixedSkillLevel,
                    "constructoid stats show its native fixed construction skill");
            }
            VerifyMechanicalRecommendationFeedback(presentationView);
        }
        private static List<SkillLine> PublishedSkills(object stats, Pawn pawn) =>
            ((IEnumerable)Property(InvokePresentation(stats, "Snapshot", pawn), "Skills"))
                .Cast<object>().Select(s => (SkillLine)Property(s, "Line")).ToList();
        private static void ContinuePresentation()
        {
            if (++presentationFrames == 3)
            {
                if (presentationScene == 0) VerifyPresentationFacts();
                if (presentationScene == 3)
                {
                    object chrome = Field(Field(presentationView, "selectedPanelState"), "chrome");
                    ReviewCheck((string)Property(chrome, "Label") == "WR Short", "paused rename immediately refreshes selected name");
                    object stats = Field(presentationView, "statsState");
                    ReviewCheck(ReferenceEquals(omniStats, InvokePresentation(stats, "Snapshot", workers[1])), "rename preserves the existing robot stats snapshot");
                    object row = InvokePresentation(presentationView, "RowSnapshotFor", workers[1], RoleStore.Current, Field(presentationView, "chipLayoutWidth"));
                    ReviewCheck((string)Property(row, "Label") == "WR Short" && ReferenceEquals(row,
                        InvokePresentation(presentationView, "RowSnapshotFor", workers[1], RoleStore.Current, Field(presentationView, "chipLayoutWidth"))),
                        "paused rename refreshes the roster and unchanged reads reuse its snapshot");
                }
                Mark("CAPTURE " + sceneNames[presentationScene]);
            }
            // A separate existence-only acknowledgement per capture avoids
            // reading a file while the Windows controller writes it over WSL.
            string acknowledgement = Path.Combine(Root, "presentation-captured-" + sceneNames[presentationScene] + ".txt");
            if (presentationFrames < 3 || !File.Exists(acknowledgement)) return;
            if (presentationScene == 2)
            {
                Type presenter = typeof(RecsAdapter).Assembly.GetType("RimShared.UiLib.StructuredTipPresenter");
                object frozen = AccessTools.Field(presenter, "frozen").GetValue(null);
                ReviewCheck(frozen != null && ((string)Property(frozen, "PlainText")).Contains(workers[1].LabelShort),
                    "hovering the fitted selected name shows the complete robot name");
            }
            if (++presentationScene == (builder == null ? 4 : sceneNames.Length))
            {
                ReviewCheck(observedNames.Count > 0 && overflowingNames.Count == 0, "all observed roster and selected-card names stay inside measured bounds");
                done = true; Mark(reviewFailures == 0 ? "SUCCESS" : "FAILED " + reviewFailures + " presentation regressions");
                return;
            }
            WorkRolesMod.Settings.groupBy = presentationScene == 1 ? "race" : "none";
            if (presentationScene == 3) workers[1].Name = new NameSingle("WR Short");
            SelectPresentationPawn(presentationScene < 2 ? workers[0] : presentationScene < 4 ? workers[1] : builder);
            presentationFrames = 0;
        }
    }
}
