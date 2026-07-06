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

        public override Vector2 RequestedTabSize
        {
            get
            {
                float w = Mathf.Min(ColonistsTabView.DesiredWidth() + 200f, Verse.UI.screenWidth);
                float h = Mathf.Min(ColonistsTabView.DesiredHeight(), Verse.UI.screenHeight - 35f);
                return new Vector2(w, h);
            }
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
            float targetW = Mathf.Min(ColonistsTabView.DesiredWidth() + 200f, Verse.UI.screenWidth);
            float targetH = Mathf.Min(ColonistsTabView.DesiredHeight(), Verse.UI.screenHeight - 35f);

            bool grew = false;
            if (targetW > windowRect.width + 1f)
            {
                windowRect.width = targetW;
                grew = true;
            }
            if (targetH > windowRect.height + 1f)
            {
                windowRect.height = targetH;
                grew = true;
            }
            if (grew)
            {
                windowRect.x = 0f;
                windowRect.y = Verse.UI.screenHeight - 35f - windowRect.height;
            }

            var tabs = new List<TabRecord>
            {
                new TabRecord("WR_ColonistsTab".Translate(), () => curTab = Tab.Colonists, curTab == Tab.Colonists),
                new TabRecord("WR_RolesTab".Translate(), () => curTab = Tab.Roles, curTab == Tab.Roles),
            };
            Rect content = new Rect(inRect.x, inRect.y + TabHeight, inRect.width, inRect.height - TabHeight);
            Widgets.DrawMenuSection(content);
            TabDrawer.DrawTabs(content, tabs);
            content = content.ContractedBy(8f);
            if (curTab == Tab.Colonists) colonistsTab.Draw(content);
            else rolesTab.Draw(content);
        }
    }
}
