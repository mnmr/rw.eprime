using System;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    public enum ChipClick { None, Remove, Context }

    public enum ChipStyle
    {
        Normal,     // standard chip
        Disabled,   // role off globally or per-pawn: strong dim + red strike-through
        Subtle,     // already-assigned recommendation: bg at ~75% brightness, NO strike, full-brightness label
        AutoOff     // rule-suppressed: dim like Disabled but NO strike (rules, not the player, turned it off)
    }

    public static class RoleChipUI
    {
        public const float Height = 24f;
        private const float PadX = 8f;
        private const float RemoveSize = 16f;
        public static readonly Color DefaultChipColor = new Color(0.25f, 0.35f, 0.45f);
        private static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color LabelColor = new Color(0.95f, 0.95f, 0.95f);
        // #e8e6e0 at 0.85 alpha — tint for the auto-role marker icon.
        public static readonly Color RuleMarkerColor = new Color(232f / 255f, 230f / 255f, 224f / 255f, 0.85f);

        public static float WidthFor(Role role, bool showRemove)
        {
            Text.Font = GameFont.Small;
            return Text.CalcSize(role.label).x + PadX * 2f
                + (showRemove ? RemoveSize + 2f : 0f)
                + (role.HasRules ? RemoveSize + 2f : 0f);
        }

        /// Draws a chip. Clicks are resolved centrally via RoleDrag.ResolveMouseUp.
        /// Only ChipClick.Remove is returned directly (immediate on MouseDown).
        /// interactive: false renders a display-only chip (no clicks, no drag).
        public static ChipClick Draw(Rect rect, Role role, ChipStyle style, bool showRemove, Pawn dragSource, Action onClick,
            bool interactive = true)
        {
            Color bg = role.hasCustomColor ? role.color : DefaultChipColor;

            switch (style)
            {
                case ChipStyle.Disabled:
                case ChipStyle.AutoOff:
                    bg = new Color(bg.r * 0.4f, bg.g * 0.4f, bg.b * 0.4f);
                    break;
                case ChipStyle.Subtle:
                    bg = new Color(bg.r * 0.6f, bg.g * 0.6f, bg.b * 0.6f, 0.4f);
                    break;
                // Normal: bg unchanged
            }

            Widgets.DrawBoxSolidWithOutline(rect, bg, OutlineColor);

            // Auto-role marker: mirrors the remove icon's slot, on the left of the label.
            float labelX = rect.x + PadX;
            if (role.HasRules)
            {
                var markerRect = new Rect(rect.x + 3f, rect.y + (rect.height - RemoveSize) / 2f, RemoveSize, RemoveSize);
                GUI.color = RuleMarkerColor;
                GUI.DrawTexture(markerRect, TexButton.AutoRebuild);
                GUI.color = Color.white;
                labelX += RemoveSize + 2f;
            }

            Rect removeRect = new Rect(rect.xMax - RemoveSize - 3f, rect.y + (rect.height - RemoveSize) / 2f, RemoveSize, RemoveSize);
            Rect labelRect = new Rect(labelX, rect.y, rect.xMax - labelX - PadX - (showRemove ? RemoveSize + 2f : 0f), rect.height);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;

            if (style == ChipStyle.Disabled || style == ChipStyle.AutoOff)
                GUI.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.55f);
            else if (style == ChipStyle.Subtle)
                GUI.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.65f);
            else
                GUI.color = LabelColor;

            Widgets.Label(labelRect, role.label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            if (style == ChipStyle.Disabled)
                Widgets.DrawLineHorizontal(labelRect.x, rect.y + rect.height / 2f, labelRect.width, new Color(1f, 0.3f, 0.3f, 0.75f));

            if (showRemove)
                GUI.DrawTexture(removeRect, TexButton.Delete);

            if (!interactive) return ChipClick.None;

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return ChipClick.Context;
            }
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                if (showRemove && removeRect.Contains(e.mousePosition))
                {
                    e.Use();
                    return ChipClick.Remove;
                }
                // Register press; click fires in ResolveMouseUp if no drag threshold reached.
                RoleDrag.OnPress(role.id, dragSource, onClick);
                e.Use();
            }
            return ChipClick.None;
        }
    }
}
