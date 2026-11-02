using System.Collections.Generic;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using PartAddress = Implanner.Core.PartAddress;
using Plan = Implanner.Core.Plan;

namespace Implanner.UI
{
    /// The new-plan prompt: the shared name dialog (Enter confirms, ESC
    /// cancels) plus an "Extends" selector so a plan can inherit an existing
    /// plan's selections and, with more than one plannable body, a "Body"
    /// selector pinning the plan to one body. The two constrain each other
    /// (Plan.IsCompatibleBase): a pinned base fixes the body, a chosen body
    /// offers only unpinned bases and bases of that body. Labels resolve at
    /// construction and on choice; the draw pass renders stored text.
    public class Dialog_NewPlan : NameDialog
    {
        private int basePlanId;
        private string basePlanLabel;
        private string body = "";
        private string bodyLabel;
        private readonly bool offersBody;
        private readonly string bodyTip;

        public Dialog_NewPlan()
            : base(PlannerLabels.PlanNameTitle, "")
        {
            basePlanLabel = PlannerLabels.ExtendsNothing;
            bodyLabel = "IMP_PlanBodyAny".Translate();
            bodyTip = "IMP_PlanBodyTip".Translate();
            offersBody = Catalogs.OfferedBodies().Count > 1;
        }

        protected override float ExtraHeight => offersBody ? 60f : 30f;

        protected override void DrawExtra(Rect rect)
        {
            DrawRow(new Rect(rect.x, rect.y, rect.width, 30f),
                PlannerLabels.ExtendsPlan, basePlanLabel, OpenBaseMenu, null);
            if (offersBody)
                DrawRow(new Rect(rect.x, rect.y + 30f, rect.width, 30f),
                    "IMP_PlanBody".Translate(), bodyLabel, OpenBodyMenu, bodyTip);
        }

        private static void DrawRow(Rect rect, string label, string value,
            System.Action open, string? tip)
        {
            TextAnchor oldAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleLeft;
            float labelWidth = WrText.FitWidth(PlannerLabels.ExtendsPlan) + 8f;
            labelWidth = Mathf.Max(labelWidth, WrText.FitWidth(label) + 8f);
            Widgets.Label(new Rect(rect.x, rect.y, labelWidth, rect.height - 4f), label);
            Text.Anchor = oldAnchor;
            var buttonRect = new Rect(rect.x + labelWidth, rect.y,
                rect.width - labelWidth, rect.height - 4f);
            if (Widgets.ButtonText(buttonRect, value)) open();
            if (tip != null) TooltipHandler.TipRegion(buttonRect, tip);
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
                // Only a base this plan's body may extend; a pinned base
                // pins the new plan to its body.
                if (!Plan.IsCompatibleBase(plan.Body, body)) continue;
                options.Add(new FloatMenuOption(plan.Name, () =>
                {
                    basePlanId = plan.Id;
                    basePlanLabel = plan.Name;
                    if (plan.IsPinned) SetBody(plan.Body);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void OpenBodyMenu()
        {
            var options = new List<FloatMenuOption>
            {
                new FloatMenuOption("IMP_PlanBodyAny".Translate(), () => SetBody("")),
            };
            // The offered bodies only: a copy of the human anatomy is the
            // human body for planning purposes.
            List<BodyDef> bodies = Catalogs.OfferedBodies();
            for (int i = 0; i < bodies.Count; i++)
            {
                BodyDef candidate = bodies[i];
                options.Add(new FloatMenuOption(candidate.LabelCap,
                    () => SetBody(candidate.defName)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// Applies a body choice; a base the new body may no longer extend
        /// is dropped so the pair stays compatible.
        private void SetBody(string defName)
        {
            body = defName;
            bodyLabel = defName.Length == 0
                ? "IMP_PlanBodyAny".Translate().ToString()
                : PlanBodies.Label(defName);
            ImplannerStore? store = ImplannerStore.Current;
            Plan? basePlan = store != null && basePlanId != 0
                ? store.Model.PlanById(basePlanId) : null;
            if (basePlan != null && !Plan.IsCompatibleBase(basePlan.Body, body))
            {
                basePlanId = 0;
                basePlanLabel = PlannerLabels.ExtendsNothing;
            }
        }

        protected override void Confirm(string trimmedName) =>
            PlannerCommands.CreatePlan(trimmedName, basePlanId, body);
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
        private readonly PartAddress slot;
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
            slot = row.Slot;
            this.candidates = candidates;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;

            title = "IMP_ReqTitle".Translate().ToString();
            body = "IMP_ReqBody".Translate(row.Label, row.RequirementSlot).ToString();
            okLabel = "IMP_ReqConfirm".Translate().ToString();
            cancelLabel = "CancelButton".Translate().ToString();
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
            PlannerCommands.SetImplantSlot(planId, host.DefName, host.Slot.Key, true);
            PlannerCommands.SetImplantSlot(planId, defName, slot.Key, true);
            Close();
        }
    }
}
