using System.Collections.Generic;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    /// The new-plan prompt: the shared name dialog (Enter confirms, ESC
    /// cancels) plus an "Extends" selector so a plan can inherit an existing
    /// plan's selections.
    public class Dialog_NewPlan : NameDialog
    {
        private int basePlanId;
        private string basePlanLabel;

        public Dialog_NewPlan()
            : base(PlannerLabels.PlanNameTitle, "")
        {
            basePlanLabel = PlannerLabels.ExtendsNothing;
        }

        protected override float ExtraHeight => 30f;

        protected override void DrawExtra(Rect rect)
        {
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            float labelWidth = WrText.FitWidth(PlannerLabels.ExtendsPlan) + 8f;
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height - 4f),
                PlannerLabels.ExtendsPlan);
            Text.Anchor = oldAnchor;
            var buttonRect = new Rect(rect.x + labelWidth, rect.y,
                rect.width - labelWidth, rect.height - 4f);
            if (Widgets.ButtonText(buttonRect, basePlanLabel))
                OpenBaseMenu();
        }

        private void OpenBaseMenu()
        {
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null) return;
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption(PlannerLabels.ExtendsNothing, () =>
                {
                    basePlanId = 0;
                    basePlanLabel = PlannerLabels.ExtendsNothing;
                }),
            };
            IReadOnlyList<Plan> plans = store.Model.Plans;
            for (int i = 0; i < plans.Count; i++)
            {
                Plan plan = plans[i];
                options.Add(new FloatMenuOption(plan.Name, () =>
                {
                    basePlanId = plan.Id;
                    basePlanLabel = plan.Name;
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        protected override void Confirm(string trimmedName) =>
            PlannerCommands.CreatePlan(trimmedName, basePlanId);
    }
}
