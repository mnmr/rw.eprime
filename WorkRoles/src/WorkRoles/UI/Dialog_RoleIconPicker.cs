using System;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Selects one of the packaged role icons, or the leading unassigned choice.
    public sealed class Dialog_RoleIconPicker : Window
    {
        private const float CellGap = 8f;
        private const float GridPadding = 6f;
        private const float AnchorGap = 6f;
        private const int Columns = 10;
        private const int Rows = 10;
        private const float GridSize = GridPadding * 2f
            + Columns * RoleIconStyle.FrameSize
            + (Columns - 1) * CellGap;

        private static readonly Color SelectedFrameColor =
            new Color(0.88f, 0.77f, 0.38f);

        private readonly int roleId;
        private readonly string selectedPath;
        private readonly Rect anchorRect;

        // WindowStack closes closeOnClickedOutside dialogs before the parent
        // window processes the same MouseDown. Remember that one close so the
        // role-icon opener can treat it as a toggle instead of reopening.
        private static int outsideCloseRoleId = -1;
        private static int outsideCloseFrame = -1;

        // Owner: this dialog instance. Key: shared catalog snapshot identity and
        // definition revision. Value: the shared immutable icon choices.
        // Dependencies: RoleIconCatalog. Refresh: immediately on a dependency
        // change. Equality: unchanged dependencies preserve the value. Teardown:
        // PostClose releases the snapshot reference.
        private RoleIconCatalogSnapshot catalog = null!;

        public override Vector2 InitialSize =>
            new Vector2(GridSize + Margin * 2f, GridSize + Margin * 2f);

        public Dialog_RoleIconPicker(int roleId, string selectedPath,
            Rect anchorRect)
        {
            this.roleId = roleId;
            this.selectedPath = selectedPath ?? "";
            this.anchorRect = anchorRect;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            closeOnAccept = false;
            closeOnCancel = true;
            doCloseX = false;
            draggable = false;
            EnsureSnapshot();
        }

        protected override void SetInitialSizeAndPosition()
        {
            Vector2 size = InitialSize;
            float maxX = Mathf.Max(0f, Verse.UI.screenWidth - size.x);
            float maxY = Mathf.Max(0f, Verse.UI.screenHeight - size.y);
            float x;
            float y;

            if (anchorRect.xMax + AnchorGap + size.x
                    <= Verse.UI.screenWidth)
            {
                x = anchorRect.xMax + AnchorGap;
                y = Mathf.Clamp(anchorRect.y, 0f, maxY);
            }
            else if (anchorRect.x - AnchorGap - size.x >= 0f)
            {
                x = anchorRect.x - AnchorGap - size.x;
                y = Mathf.Clamp(anchorRect.y, 0f, maxY);
            }
            else if (anchorRect.yMax + AnchorGap + size.y
                    <= Verse.UI.screenHeight)
            {
                x = Mathf.Clamp(anchorRect.x, 0f, maxX);
                y = anchorRect.yMax + AnchorGap;
            }
            else if (anchorRect.y - AnchorGap - size.y >= 0f)
            {
                x = Mathf.Clamp(anchorRect.x, 0f, maxX);
                y = anchorRect.y - AnchorGap - size.y;
            }
            else
            {
                // Extremely small displays may have no side large enough for
                // the picker. Keep it on-screen and favor the roomier side.
                float rightRoom = Verse.UI.screenWidth - anchorRect.xMax;
                x = rightRoom >= anchorRect.x
                    ? anchorRect.xMax + AnchorGap
                    : anchorRect.x - AnchorGap - size.x;
                x = Mathf.Clamp(x, 0f, maxX);
                y = Mathf.Clamp(anchorRect.y, 0f, maxY);
            }

            windowRect = new Rect(x, y, size.x, size.y).Rounded();
        }

        internal static void Toggle(int roleId, string selectedPath,
            Rect anchorRect)
        {
            if (outsideCloseRoleId == roleId
                && outsideCloseFrame == Time.frameCount)
            {
                outsideCloseRoleId = -1;
                outsideCloseFrame = -1;
                return;
            }

            if (Find.WindowStack.TryGetWindow<Dialog_RoleIconPicker>(
                    out Dialog_RoleIconPicker open))
            {
                open.Close();
                return;
            }

            Find.WindowStack.Add(new Dialog_RoleIconPicker(
                roleId, selectedPath, anchorRect));
        }

        private void EnsureSnapshot()
        {
            RoleIconCatalogSnapshot next = RoleIconCatalog.Snapshot;
            if (!ReferenceEquals(catalog, next))
                catalog = next;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = GuiStateScope.Capture();
            if (WrEvent.SkipContentPass()) return;
            EnsureSnapshot();

            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            try
            {
                GUI.color = Color.white;
                Widgets.DrawMenuSection(inRect);
                DrawGrid(inRect);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                GUI.color = oldColor;
            }
        }

        private void DrawGrid(Rect rect)
        {
            float stride = RoleIconStyle.FrameSize + CellGap;
            int count = Mathf.Min(catalog.Count, Columns * Rows);
            for (int i = 0; i < count; i++)
            {
                RoleIconChoiceSnapshot choice = catalog.At(i);
                int row = i / Columns;
                int column = i - row * Columns;
                var frameRect = new Rect(
                    rect.x + GridPadding + column * stride,
                    rect.y + GridPadding + row * stride,
                    RoleIconStyle.FrameSize, RoleIconStyle.FrameSize);

                bool selected = string.Equals(
                    selectedPath, choice.Path, StringComparison.Ordinal);
                if (selected) Widgets.DrawHighlightSelected(frameRect);
                else Widgets.DrawHighlightIfMouseover(frameRect);

                GUI.color = selected
                    ? SelectedFrameColor
                    : RoleIconStyle.FrameColor;
                Widgets.DrawBox(frameRect);
                GUI.color = choice.Unassigned
                    ? RoleIconStyle.PlaceholderTint
                    : RoleIconStyle.IconTint;
                GUI.DrawTexture(new Rect(
                    frameRect.x + RoleIconStyle.IconInset,
                    frameRect.y + RoleIconStyle.IconInset,
                    RoleIconStyle.IconSize, RoleIconStyle.IconSize),
                    choice.Texture);
                GUI.color = Color.white;

                if (!Widgets.ButtonInvisible(frameRect)) continue;
                if (!selected)
                    RoleCommands.SetRoleIcon(roleId, choice.Path);
                // Close() runs PostClose immediately, which nulls the
                // catalog reference; iterating further would dereference
                // it. Stop after the pick.
                Close();
                return;
            }
        }

        public override void PostClose()
        {
            Event? current = Event.current;
            if (current != null && current.type == EventType.MouseDown
                && current.button == 0)
            {
                outsideCloseRoleId = roleId;
                outsideCloseFrame = Time.frameCount;
            }
            catalog = null!;
            base.PostClose();
        }
    }
}
