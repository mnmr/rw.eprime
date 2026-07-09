using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public class MainTabWindow_WorkRoles : MainTabWindow
    {
        private enum Tab { Colonists, Roles }

        private Tab curTab = Tab.Colonists;
        private readonly ColonistsTabView colonistsTab = new ColonistsTabView();
        private readonly RolesTabView rolesTab = new RolesTabView();

        private const float TabHeight = 32f;

        public MainTabWindow_WorkRoles()
        {
            resizeable = false;
            draggable = false;   // main tab windows are not draggable
        }

        public override Vector2 RequestedTabSize => TargetSize();

        /// Width floors at the design width (fixed chrome overlaps below it) and
        /// grows with the widest chip strip; whichever tab wants more height wins
        /// (small colonies would otherwise cramp the Roles tab). Both capped at
        /// the screen.
        private static Vector2 TargetSize()
        {
            float w = Mathf.Clamp(ColonistsTabView.DesiredWidth() + 200f,
                ColonistsTabView.DefaultWidth, Verse.UI.screenWidth);
            float h = Mathf.Min(
                Mathf.Max(ColonistsTabView.DesiredHeight(), RolesTabView.DesiredHeight()),
                Verse.UI.screenHeight - 35f);
            return new Vector2(w, h);
        }

        public override void PreOpen()
        {
            base.PreOpen();
            RoleDrag.Cancel();
            colonistsTab.Reset();
            rolesTab.Reset();
        }

        public override void DoWindowContents(Rect inRect)
        {
            // Grow-only mid-session resize: if content grew, expand windowRect; never shrink.
            var target = TargetSize();

            bool grew = false;
            if (target.x > windowRect.width + 1f)
            {
                windowRect.width = target.x;
                grew = true;
            }
            if (target.y > windowRect.height + 1f)
            {
                windowRect.height = target.y;
                grew = true;
            }
            if (grew)
            {
                windowRect.x = 0f;
                windowRect.y = Verse.UI.screenHeight - 35f - windowRect.height;
            }

            var tabs = new List<TabRecord>
            {
                new TabRecord("WR_ColonistsTab".Translate(), () =>
                {
                    // Role edits on the Roles tab aren't tracked; recompute the
                    // suggestion plan whenever the user comes back.
                    if (curTab != Tab.Colonists) colonistsTab.InvalidateRecommendationCache();
                    curTab = Tab.Colonists;
                }, curTab == Tab.Colonists),
                new TabRecord("WR_RolesTab".Translate(), () => curTab = Tab.Roles, curTab == Tab.Roles),
            };
            Rect content = new Rect(inRect.x, inRect.y + TabHeight, inRect.width, inRect.height - TabHeight);
            Widgets.DrawMenuSection(content);
            TabDrawer.DrawTabs(content, tabs);

            // Per-tab action button in the window's top-right corner, beside the tab
            // strip: Fix My Colony on Colonists, Restore Roles on Roles.
            const float ActionBtnW = 130f;
            const float ActionBtnH = 28f;
            float btnY = inRect.y + (TabHeight - ActionBtnH) / 2f;
            var actionRect = new Rect(inRect.xMax - ActionBtnW, btnY, ActionBtnW, ActionBtnH);
            if (curTab == Tab.Colonists)
            {
                TooltipHandler.TipRegion(actionRect, "WR_FixMyColonyTip".Translate());
                if (Widgets.ButtonText(actionRect, "WR_FixMyColony".Translate()))
                    colonistsTab.ShowFixPreview();
            }
            else
            {
                TooltipHandler.TipRegion(actionRect, "WR_RestoreRolesTip".Translate());
                if (Widgets.ButtonText(actionRect, "WR_RestoreRoles".Translate()))
                {
                    var missing = Seeding.MissingSeededRoles();
                    if (missing.Count == 0)
                        Messages.Message("WR_NothingToRestore".Translate(),
                            MessageTypeDefOf.RejectInput, historical: false);
                    else
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            "WR_RestoreRolesConfirm".Translate(missing.ToCommaList()),
                            RoleCommands.RestoreMissingRoles));
                }
            }

            content = content.ContractedBy(8f);
            if (curTab == Tab.Colonists) colonistsTab.Draw(content);
            else rolesTab.Draw(content);
        }
    }
}
