using EPrimeReadouts.Core;
using EPrimeReadouts.Patches;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// The configuration window: inset top panel, then Groups | Resources |
    /// (Editor over Preview). Every completed action fires a sync command
    /// immediately — no Apply/Cancel. Resizable; size persists per-player.
    public class Dialog_ReadoutConfig : Window
    {
        private const float PanelH = 56f;
        private const float Gap = 10f;
        private const float LeftW = 220f;
        private const float CenterW = 280f;

        public int selectedGroupId = -1;

        /// Canonical token of the currently selected slot (e.g. "Steel" or
        /// "@MeatRaw"). Set by the editor task; may be null. ResourceTreeView
        /// reads this to tint matching rows.
        public string selectedCanonical;

        private readonly object structuredTipOwner = new object();
        private readonly GroupListView groups = new GroupListView();
        private readonly ResourceTreeView tree = new ResourceTreeView();
        private readonly EditorView editor = new EditorView();

        public Dialog_ReadoutConfig()
        {
            doCloseX = true;
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
            absorbInputAroundWindow = false;
            forcePause = false;
            closeOnClickedOutside = false;
        }

        public override Vector2 InitialSize =>
            EPrimeReadoutsMod.Settings.dialogW > 0f
                ? new Vector2(EPrimeReadoutsMod.Settings.dialogW, EPrimeReadoutsMod.Settings.dialogH)
                : new Vector2(920f, 640f);

        public ReadoutGroup SelectedGroup =>
            ReadoutStore.Current?.Model.GroupById(selectedGroupId);

        public override void PreClose()
        {
            base.PreClose();
            EPrimeReadoutsMod.Persist(s =>
            {
                s.dialogW = windowRect.width;
                s.dialogH = windowRect.height;
            });
            Patch_ActiveTip_TipRect.ReleaseOwner(structuredTipOwner);
        }

        public override void DoWindowContents(Rect inRect)
        {
            var store = ReadoutStore.Current;
            if (store == null) return;

            bool repaint = Event.current.type == EventType.Repaint;
            if (repaint) Patch_ActiveTip_TipRect.BeginGeneration(structuredTipOwner);
            try
            {
                EprDrag.Update();

                // --- Top panel ---
                var panelRect = new Rect(inRect.x, inRect.y, inRect.width, PanelH);
                Widgets.DrawBoxSolidWithOutline(panelRect, EprStyle.PanelBackground, EprStyle.PanelOutline);

                // Mod icon (40x40, 8px left padding, vertically centred)
                var iconRect = new Rect(panelRect.x + 8f, panelRect.y + 8f, 40f, 40f);
                GUI.DrawTexture(iconRect, ReadoutTextures.ModIcon);

                // Title "EPrime's Readouts"
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = EprStyle.HeaderText;
                Widgets.Label(new Rect(iconRect.xMax + 8f, panelRect.y, panelRect.width - iconRect.xMax - 8f - 150f, PanelH),
                    "EPR.Title".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                // Restore-defaults button vertically centred in panel, 8px from right edge
                var restoreRect = new Rect(panelRect.xMax - 138f, panelRect.y + (PanelH - 28f) / 2f, 130f, 28f);
                if (Widgets.ButtonText(restoreRect, "EPR.RestoreDefaults".Translate()))
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "EPR.RestoreConfirm".Translate(),
                        ReadoutCommands.RestoreDefaults, destructive: true));

                // --- Content area ---
                var content = new Rect(inRect.x, inRect.y + PanelH + Gap,
                    inRect.width, inRect.height - PanelH - Gap);
                var leftRect = new Rect(content.x, content.y, LeftW, content.height);
                var centerRect = new Rect(leftRect.xMax + Gap, content.y, CenterW, content.height);
                var rightRect = new Rect(centerRect.xMax + Gap, content.y,
                    content.xMax - centerRect.xMax - Gap, content.height);

                groups.Draw(leftRect, this);
                tree.Draw(centerRect, this);
                editor.Draw(rightRect, this);

                DrawDragGhost();
                EprDrag.ResolveMouseUp();
            }
            finally
            {
                if (repaint) Patch_ActiveTip_TipRect.EndGeneration(structuredTipOwner);
            }
        }

        private static void DrawDragGhost()
        {
            if (!EprDrag.Active || EprDrag.Payload == null) return;
            // Resolve the icon def: pools → first CountedDefsIn member; defs → direct lookup.
            ThingDef def;
            if (SlotToken.IsPool(EprDrag.Payload))
            {
                var members = GameResourceCatalog.Instance.CountedDefsIn(
                    SlotToken.MemberName(EprDrag.Payload));
                def = members.Count > 0
                    ? DefDatabase<ThingDef>.GetNamedSilentFail(members[0])
                    : null;
            }
            else
            {
                def = DefDatabase<ThingDef>.GetNamedSilentFail(SlotToken.MemberName(EprDrag.Payload));
            }
            if (def == null) return;
            var mouse = Event.current.mousePosition;
            Widgets.ThingIcon(new Rect(mouse.x - 16f, mouse.y - 16f, 32f, 32f), def);
        }
    }
}
