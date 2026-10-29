using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.UI;

namespace WorkRolesMechanicalVerification
{
    public static partial class Probe
    {
        private static int reviewFailures;
        private static string applyLabel;
        private static bool observedRobotApply;
        private static int MechanicalRevision => (int)AccessTools.Field(typeof(RoleStore), "MechanicalFactsRevision").GetValue(RoleStore.Current);
        private static void ReviewCheck(bool value, string message)
        {
            Log.Message("[MechanicalProbe] " + (value ? "PASS " : "REVIEW FAILURE ") + message);
            if (!value) reviewFailures++;
        }
        private static string AssignmentIds(Pawn pawn) => string.Join(",", RoleStore.Current.pawnSets[pawn].assignments.Select(a => a.roleId));
        private static void VerifyReviewBoundaries()
        {
            VerifyWorkerLocations();
            Pawn mech = workers[2];
            // The captured save already reaches the native allowed-area cap.
            Area_Allowed restriction = map.areaManager.AllAreas.OfType<Area_Allowed>().Last();
            restriction[mech.Position] = true;
            mech.playerSettings.AreaRestrictionInPawnCurrentMap = restriction;
            int before = MechanicalRevision;
            restriction.Delete();
            ReviewCheck(mech.playerSettings.AreaRestrictionInPawnCurrentMap == null && MechanicalRevision != before,
                "deleting the active mechanical area invalidates coverage while paused");
            map.areaManager.TryMakeNewAllowed(out Area_Allowed unrelated);
            before = MechanicalRevision; unrelated.Delete();
            ReviewCheck(MechanicalRevision == before, "deleting an unrelated area preserves mechanical facts");
            map.areaManager.TryMakeNewAllowed(out Area_Allowed dockedArea);
            var dockedAreas = (Dictionary<Map, Area>)AccessTools.Field(typeof(Pawn_PlayerSettings), "allowedAreas").GetValue(workers[1].playerSettings);
            dockedAreas[map] = dockedArea;
            before = MechanicalRevision; dockedArea.Delete();
            ReviewCheck(!dockedAreas.ContainsKey(map) && MechanicalRevision != before,
                "deleting a docked robot's area clears native containment restrictions and coverage");
            RoleCommands.SetMechanicalWorkersEnabled(false);
            before = MechanicalRevision;
            mech.GetMechControlGroup().SetWorkMode(MechWorkModeDefOf.Escort);
            mech.GetMechControlGroup().SetWorkMode(MechWorkModeDefOf.Work);
            ReviewCheck(MechanicalRevision == before, "native mode changes with support off do not invalidate human consumers");
            RoleCommands.SetMechanicalWorkersEnabled(true);

            var window = (MainTabWindow_WorkRoles)Find.MainTabsRoot.OpenTab.TabWindow;
            var view = (ColonistsTabView)AccessTools.Field(typeof(MainTabWindow_WorkRoles), "colonistsTab").GetValue(window);
            VerifyRecommendationBoundaries(view);
            string[] assigned = workers.Select(AssignmentIds).ToArray();
            view.ShowAutoOptimizeEnablePreview();
            var dialog = Find.WindowStack.WindowOfType<Dialog_ChangesPreview>();
            var entries = (List<Dialog_ChangesPreview.PawnPreview>)AccessTools.Field(typeof(Dialog_ChangesPreview), "entries").GetValue(dialog);
            ReviewCheck(entries.All(e => !workers.Contains(e.pawn)), "human auto-optimize enable preview excludes mechanical assignments");
            ((Action<HashSet<Pawn>>)AccessTools.Field(typeof(Dialog_ChangesPreview), "onApply").GetValue(dialog))(new HashSet<Pawn>());
            ReviewCheck(workers.Select(AssignmentIds).SequenceEqual(assigned), "enabling human auto-optimize leaves all robot assignments unchanged");
            dialog.Close(false);

            Role role = RoleStore.Current.RoleByTemplate("WS_Builder");
            Type presentation = AccessTools.TypeByName("WorkRoles.UI.RecommendationPresentation");
            object tip = AccessTools.Method(presentation, "CreateTooltip").Invoke(null,
                new object[] { RoleStore.Current, workers[1], role, Dialog_ChangesPreview.ChipState.Added, null, null });
            string already = "WR_AlreadyAssigned".Translate();
            string tipText = (string)AccessTools.Property(tip.GetType(), "PlainText").GetValue(tip);
            ReviewCheck(!tipText.Contains(already),
                "a new robot recommendation never claims it is already assigned");

            // Observe the actual button submitted by the next UI frame while
            // the human optimizer is on and a multi-purpose robot is selected.
            object selection = AccessTools.Field(typeof(ColonistsTabView), "selection").GetValue(view);
            var click = AccessTools.Method(selection.GetType(), "Click");
            click.Invoke(selection, click.GetParameters().Select((p, i) => i == 0 ? (object)workers[1] : p.DefaultValue).ToArray());
            applyLabel = "WR_MakeItSo".Translate();
            var observer = new Harmony("eprime.workroles.mechanicalprobe.ui");
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(Widgets)).Where(m => m.Name == "ButtonText"
                && m.GetParameters().Length > 1 && m.GetParameters()[0].ParameterType == typeof(Rect)
                && m.GetParameters()[1].ParameterType == typeof(string)))
                observer.Patch(method, prefix: new HarmonyMethod(typeof(Probe), nameof(ObserveButton)));
            phase = 6;
        }

        private static void VerifyWorkerLocations()
        {
            Pawn robot = workers[0];
            Type stationType = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            Thing station = map.listerThings.AllThings.First(t => stationType.IsInstanceOfType(t)
                && ((System.Collections.IList)AccessTools.Field(stationType, "container").GetValue(t)).Contains(robot));
            ReviewCheck(RoleRules.LocalHour(robot) == GenLocalDate.HourInteger(map),
                "docked robot time rules use its actual container map");
            AccessTools.Method(stationType, "Button_SpawnBot").Invoke(station, null);
            Check(robot.Spawned, "native station release creates the location-transition fixture");
            IntVec3 position = robot.Position;
            robot.DeSpawn();
            ReviewCheck(RoleRules.LocalHour(robot) == -1,
                "an undocked off-map robot does not inherit its former station's clock");
            var away = new Role { locationTokens = new List<string> { WorkRoles.Core.LocationRules.Caravans } };
            ReviewCheck(RoleRules.FailReason(away, robot) == RuleFailReason.None,
                "an undocked off-map robot is not classified at its former station");
            GenSpawn.Spawn(robot, position, map);
            AccessTools.Method(stationType, "AddRobotToContainer").Invoke(station, new object[] { robot });
            ReviewCheck(RoleRules.LocalHour(robot) == GenLocalDate.HourInteger(map),
                "native redocking restores the robot's map-based time rules");
        }
        private static void ObserveButton(string __1)
        {
            if (__1 == applyLabel) observedRobotApply = true;
        }
        private static void FinishReviewBoundaries()
        {
            ReviewCheck(observedRobotApply, "manual robot Apply remains available while human auto-optimize is enabled");
            RoleCommands.SetAutoOptimize(false);
            if (reviewFailures != 0) throw new InvalidOperationException(reviewFailures + " final review regressions failed");
            done = true; Mark("SUCCESS");
        }
    }
}
