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

    /// The requirement prompt for a slot the game only offers on an
    /// artificial part (a Bionic modularity limb module): lists the
    /// replacements able to host it as a pick-one choice. Confirm adds the
    /// chosen host and the clicked slot to the plan; Cancel or ESC adds
    /// nothing, and the row keeps its published unchecked state. Labels
    /// and the body measurement resolve once at construction (language
    /// cannot change while a dialog is open); the draw pass renders
    /// prebuilt rows only.
    internal sealed class Dialog_ImplantRequirements : Window
    {
        private const float RowH = 26f;
        private const float TitleH = 34f;
        private const float BtnW = 120f;
        private const float BtnH = 32f;
        private const float Gap = 10f;
        private const float DialogWidth = 460f;

        private readonly int planId;
        private readonly string defName;
        private readonly int ordinal;
        private readonly List<RequirementCandidate> candidates;
        private readonly string title;
        private readonly string body;
        private readonly string okLabel;
        private readonly string cancelLabel;
        private readonly float bodyHeight;
        private int picked;

        public override Vector2 InitialSize => new Vector2(DialogWidth,
            TitleH + bodyHeight + Gap + candidates.Count * RowH
            + Gap + BtnH + Margin * 2f);

        internal Dialog_ImplantRequirements(int planId, PickerRow row,
            List<RequirementCandidate> candidates)
        {
            this.planId = planId;
            defName = row.DefName;
            ordinal = row.Ordinal;
            this.candidates = candidates;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            title = "IMP_ReqTitle".Translate().ToString();
            body = "IMP_ReqBody".Translate(row.Label, row.RequirementSlot).ToString();
            okLabel = "IMP_ReqConfirm".Translate().ToString();
            cancelLabel = "IMP_Cancel".Translate().ToString();
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                bodyHeight = Mathf.Ceil(
                    Text.CalcHeight(body, DialogWidth - Margin * 2f));
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), title);
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                Widgets.Label(new Rect(inRect.x, inRect.y + TitleH,
                    inRect.width, bodyHeight), body);

                Text.WordWrap = false;
                float y = inRect.y + TitleH + bodyHeight + Gap;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var rowRect = new Rect(inRect.x + 8f, y, inRect.width - 8f, RowH);
                    if (Widgets.RadioButtonLabeled(rowRect, candidates[i].Label,
                            picked == i))
                        picked = i;
                    y += RowH;
                }

                var cancelRect = new Rect(inRect.x, inRect.yMax - BtnH, BtnW, BtnH);
                var okRect = new Rect(inRect.xMax - BtnW, inRect.yMax - BtnH, BtnW, BtnH);
                if (Widgets.ButtonText(cancelRect, cancelLabel)) Close();
                if (Widgets.ButtonText(okRect, okLabel)) Apply();
            }
        }

        /// Host first, then the clicked slot: two synced commands, each
        /// resolving its own conflicts exactly as a direct click would.
        private void Apply()
        {
            RequirementCandidate host = candidates[picked];
            PlannerCommands.SetImplantSlot(planId, host.DefName, host.Ordinal, true);
            PlannerCommands.SetImplantSlot(planId, defName, ordinal, true);
            Close();
        }
    }
}
