using System;
using RimWorld;
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
        private static readonly Color RemovedColor = new Color(1f, 0f, 0f, 1f); // #ff0000

        /// Red border marking a chip whose role is about to be removed.
        public static void DrawRemovedOutline(Rect rect)
        {
            GUI.color = RemovedColor;
            Widgets.DrawBox(rect);
            GUI.color = Color.white;
        }

        /// Prefix markers: blocker, time rule, location rule, and — on plain
        /// manual roles only — the assignment pin. Blockers and rule-carrying
        /// roles are already plan-protected, so a pin there would be redundant
        /// (TargetPlannerTests.PinningBlockersOrRuleCarryingRolesChangesNothing).
        private static int MarkerCount(Role role, bool pinned) =>
            (role.blocker ? 1 : 0)
            + (role.activeHours != Role.AllHours ? 1 : 0)
            + (role.locationTokens.Count > 0 ? 1 : 0)
            + (PinShown(role, pinned) ? 1 : 0);

        private static bool PinShown(Role role, bool pinned) =>
            pinned && !role.blocker && !role.HasRules;

        /// Compact chips run tight: exact-measured initials need no breathing room.
        private static float PadFor(ChipDisplay display) =>
            display == ChipDisplay.Compact ? 2f : PadX;

        public static float WidthFor(Role role, bool showRemove,
            ChipDisplay display = ChipDisplay.Normal, string abbrev = null, bool pinned = false)
        {
            Text.Font = GameFont.Small;
            int markers = MarkerCount(role, pinned);
            // Minimal chips with markers carry no blank label square; Compact
            // chips share one width (the widest initials) so columns line up.
            float labelW = display == ChipDisplay.Minimal
                ? (markers > 0 ? 0f : 10f)
                : display == ChipDisplay.Compact && abbrev != null
                    ? System.Math.Max(WrText.FitWidth(abbrev), WrText.FitWidth("MM"))
                    : WrText.FitWidth(role.label);
            return labelW + PadFor(display) * 2f
                + (showRemove ? RemoveSize + 2f : 0f)
                + markers * (RemoveSize + 2f);
        }

        /// Draws a chip. Clicks are resolved centrally via RoleDrag.ResolveMouseUp.
        /// Only ChipClick.Remove is returned directly (immediate on MouseDown).
        /// interactive: false renders a display-only chip (no clicks, no drag).
        public static ChipClick Draw(Rect rect, Role role, ChipStyle style, bool showRemove, Pawn dragSource, Action onClick,
            bool interactive = true, ChipDisplay display = ChipDisplay.Normal, string abbrev = null, bool pinned = false)
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

            // Prefix markers mirror the remove icon's slot, left of the label:
            // blocker veto, time rule, location rule, pin (plain roles only).
            // Compact initials get a uniform 4px inset (the exact-measure slack
            // covers it) so text stays left-aligned across rows.
            float labelX = rect.x
                + (display == ChipDisplay.Compact ? 4f : PadFor(display));
            {
                float markerX = rect.x + 3f;
                void Marker(Texture2D tex, bool tinted, float size = RemoveSize)
                {
                    var markerRect = new Rect(markerX + (RemoveSize - size) / 2f,
                        rect.y + (rect.height - size) / 2f, size, size);
                    GUI.color = tinted ? RuleMarkerColor : Color.white;
                    GUI.DrawTexture(markerRect, tex);
                    GUI.color = Color.white;
                    markerX += RemoveSize + 2f;
                    labelX += RemoveSize + 2f;
                }
                if (role.blocker) Marker(WorkRolesTex.BlockerMarker, tinted: false); // full-color red X
                if (role.activeHours != Role.AllHours) Marker(WorkRolesTex.TimeMarker, tinted: true);
                if (role.locationTokens.Count > 0) Marker(WorkRolesTex.LocationMarker, tinted: true);
                // The pin texture has less padding than the others; drawn
                // smaller so it doesn't dominate the strip.
                if (PinShown(role, pinned)) Marker(WorkRolesTex.PinMarker, tinted: true, size: 13f);
            }

            Rect removeRect = new Rect(rect.xMax - RemoveSize - 3f, rect.y + (rect.height - RemoveSize) / 2f, RemoveSize, RemoveSize);
            Rect labelRect = new Rect(labelX, rect.y, rect.xMax - labelX - PadFor(display) - (showRemove ? RemoveSize + 2f : 0f), rect.height);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            // Never wrap: a rect that comes up a pixel short at fractional UI
            // scales must clip the last glyph, not spill a second line out of
            // the chip.
            bool wrap = Text.WordWrap;
            Text.WordWrap = false;

            if (style == ChipStyle.Disabled || style == ChipStyle.AutoOff)
                GUI.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.55f);
            else if (style == ChipStyle.Subtle)
                GUI.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.65f);
            else
                GUI.color = LabelColor;

            if (display != ChipDisplay.Minimal)
                Widgets.Label(labelRect,
                    display == ChipDisplay.Compact && abbrev != null ? abbrev : role.label);
            GUI.color = Color.white;
            Text.WordWrap = wrap;
            Text.Anchor = TextAnchor.UpperLeft;

            // Abbreviated chips: the tooltip carries the identity.
            if (display != ChipDisplay.Normal && Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, role.label);

            if (style == ChipStyle.Disabled)
            {
                GUI.color = new Color(1f, 0.3f, 0.3f, 0.75f);
                WrText.LineHorizontal(labelRect.x, rect.y + rect.height / 2f, labelRect.width);
                GUI.color = Color.white;
            }

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
