using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace WorkRolesMechanicalVerification
{
    public static partial class Probe
    {
        private static void VerifyMechanicalRecommendationFeedback(object view)
        {
            RoleStore store = RoleStore.Current;
            Pawn omni = workers[1];
            Role build = store.RoleByTemplate("WS_Builder");
            Role core = store.RoleByTemplate("WS_Core");
            var original = store.pawnSets[omni].assignments.Select(a => new RoleAssignment
                { roleId = a.roleId, state = a.state, pinned = a.pinned }).ToList();
            bool showVerdicts = WorkRolesMod.Settings.verdictsOnRecommendationChips;
            Type stateType = AccessTools.TypeByName("WorkRoles.UI.ColonistRecommendationState");
            object state = Activator.CreateInstance(stateType, true);
            var plansMethod = AccessTools.GetDeclaredMethods(stateType).Single(m => m.Name == "Plans" && m.GetParameters().Length == 3);
            object stamp = Activator.CreateInstance(plansMethod.GetParameters()[1].ParameterType);
            object external = Field(view, "externalSnapshotProvider");
            Func<IList> read = () => (IList)plansMethod.Invoke(state, new[] { omni, stamp, external });
            Func<IList, object> robotPlan = plans => plans.Cast<object>().Single(p => (Pawn)Property(p, "Pawn") == omni);
            Func<object> render = () => InvokePresentation(state, "RenderSnapshot", store, omni,
                InvokePresentation(Field(view, "rosterState"), "Catalog", store), stamp, 320f, 210f, external);
            Func<bool> firefighting = () => CompiledJobOrders.NormalFor(omni).Concat(CompiledJobOrders.EmergencyFor(omni))
                .Any(g => g.def.workType == WorkTypeDefOf.Firefighter);
            try
            {
                WorkRolesMod.Settings.verdictsOnRecommendationChips = true;
                RoleCommands.PasteRoleSet(omni, new List<RoleAssignment> {
                    new RoleAssignment { roleId = build.id }, new RoleAssignment { roleId = core.id } });
                ReviewCheck(firefighting(), "native Omni Core supplies firefighting before Fix");
                IList first = read();
                object plan = robotPlan(first);
                var target = (List<RoleAssignment>)Property(plan, "Target");
                var reasons = (Dictionary<int, RoleRecommendationExplanation>)Property(plan, "Explanations");
                ReviewCheck(target.Any(a => a.roleId == core.id), "Fix keeps an Omni's automatic emergency duty after Builder");
                ReviewCheck(reasons.ContainsKey(build.id) && reasons.ContainsKey(core.id)
                    && reasons.Values.All(r => !r.DemandApplies && r.TrainingSkills.Count == 0),
                    "robot preview explains each retained duty without human demand or learning claims");
                object snapshot = render();
                object builderChip = Enumerable.Range(0, (int)Property(snapshot, "ChipCount"))
                    .Select(i => InvokePresentation(snapshot, "ChipAt", i))
                    .Single(c => (int)Property(Property(c, "Chip"), "RoleId") == build.id);
                ReviewCheck(((RoleChipVerdict)Property(builderChip, "Verdict")).Shown,
                    "actual mechanical recommendation chip shows its capability verdict");
                object tooltip = Property(builderChip, "Tooltip");
                ReviewCheck(tooltip != null && ((string)Property(tooltip, "PlainText"))
                    .Contains("WR_RecMechanicalRetained".Translate().ToString()),
                    "actual robot recommendation tooltip explains why its main role is kept");
                ReviewCheck(ReferenceEquals(snapshot, render()) && ReferenceEquals(first, read()),
                    "unchanged mechanical feedback reuses both plan and render snapshots");
                InvokePresentation(state, "InvalidateExternalFacts");
                ReviewCheck(ReferenceEquals(plan, robotPlan(read())) && ReferenceEquals(snapshot, render()),
                    "equal external refresh preserves mechanical reasons, verdicts and preview identity");

                RoleCommands.ToggleAssignmentPin(omni, build.id);
                object pinned = robotPlan(read());
                ReviewCheck(!ReferenceEquals(plan, pinned)
                    && ((Dictionary<int, RoleRecommendationExplanation>)Property(pinned, "Explanations"))[build.id]
                        .SpecialPickReason == SpecialPickReason.Protected,
                    "paused pin edit refreshes the mechanical target and reason");
                RoleCommands.ToggleAssignmentPin(omni, build.id);
                InvokePresentation(render(), "Apply");
                ReviewCheck(firefighting(), "applying the visible robot recommendation preserves native firefighting");

                RoleCommands.PasteRoleSet(omni, new List<RoleAssignment> { new RoleAssignment { roleId = build.id } });
                InvokePresentation(render(), "Apply");
                ReviewCheck(firefighting(), "explicit Fix adds supported automatic firefighting when Core was not assigned");
                ReviewCheck(!CompiledJobOrders.NormalFor(omni).Concat(CompiledJobOrders.EmergencyFor(omni))
                    .Any(g => g.def.workType?.defName == "Patient" || g.def.workType?.defName == "BedRest"),
                    "adding automatic Core never grants unsupported patient or bed-rest jobs");
            }
            finally
            {
                RoleCommands.PasteRoleSet(omni, original);
                WorkRolesMod.Settings.verdictsOnRecommendationChips = showVerdicts;
                InvokePresentation(state, "Reset");
            }
        }
    }
}
