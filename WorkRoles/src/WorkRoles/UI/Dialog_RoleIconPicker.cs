using System;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Selects one of the packaged role icons, or the leading unassigned choice.
    public sealed class Dialog_RoleIconPicker : Window
    {
        private const float CellGap = 8f;
        private const float GridPadding = 6f;
        private const int Columns = 10;
        private const int Rows = 10;
        private const float GridSize = GridPadding * 2f
            + Columns * RoleIconStyle.FrameSize
            + (Columns - 1) * CellGap;

        private static readonly Color SelectedFrameColor =
            new Color(0.88f, 0.77f, 0.38f);

        private readonly int roleId;
        private readonly string selectedPath;

        // Owner: this dialog instance. Key: shared catalog snapshot identity and
        // definition revision. Value: the shared immutable icon choices.
        // Dependencies: RoleIconCatalog. Refresh: immediately on a dependency
        // change. Equality: unchanged dependencies preserve the value. Teardown:
        // PostClose releases the snapshot reference.
        private RoleIconCatalogSnapshot catalog = null!;

        public override Vector2 InitialSize =>
            new Vector2(GridSize + Margin * 2f, GridSize + Margin * 2f);

        public Dialog_RoleIconPicker(int roleId, string selectedPath)
        {
            this.roleId = roleId;
            this.selectedPath = selectedPath ?? "";
            absorbInputAroundWindow = false;
            closeOnClickedOutside = true;
            closeOnAccept = false;
            closeOnCancel = true;
            doCloseX = false;
            draggable = false;
            EnsureSnapshot();
        }

        private void EnsureSnapshot()
        {
            RoleIconCatalogSnapshot next = RoleIconCatalog.Snapshot;
            if (!ReferenceEquals(catalog, next))
                catalog = next;
        }

        public override void DoWindowContents(Rect inRect)
        {
            using var guiState = new GuiStateScope(capture: true);
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
                Close();
            }
        }

        public override void PostClose()
        {
            catalog = null!;
            base.PostClose();
        }
    }
}
