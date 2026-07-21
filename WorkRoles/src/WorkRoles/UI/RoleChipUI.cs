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
        private const float RemoveSize = ChipUI.RemoveSize;
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
        /// (RecsOrderingTests.ProtectedAssignmentsReenterAtTheirOriginalIndexWithToggles).
        private static int MarkerCount(Role role, bool pinned) =>
            (role.blocker ? 1 : 0)
            + (role.activeHours != Role.AllHours ? 1 : 0)
            + (role.locationTokens.Count > 0 ? 1 : 0)
            + (PinShown(role, pinned) ? 1 : 0);

        private static bool PinShown(Role role, bool pinned) =>
            pinned && !role.blocker && !role.HasRules;

        /// One role drag ghost for every tab. It uses the normal chip renderer
        /// and adds a red veil and outline only when the current target rejects
        /// the drop. Group drags remain a Roles-tab-specific label ghost.
        public static void DrawDragGhost(RoleStore store)
        {
            if (!RoleDrag.Active || RoleDrag.GroupId >= 0 || store == null) return;
            Role role = store.RoleById(RoleDrag.RoleId);
            if (role == null) return;
            Vector2 mouse = Event.current.mousePosition;
            float width = WidthFor(role, showRemove: false);
            var rect = new Rect(mouse.x + 10f, mouse.y + 6f, width, Height);
            Draw(rect, role, ChipStyle.Normal, showRemove: false,
                dragSource: null, onClick: null, interactive: false);
            if (!RoleDrag.HoverBlocked) return;
            Widgets.DrawBoxSolid(rect, new Color(0.65f, 0.05f, 0.05f, 0.3f));
            GUI.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;
        }

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

        /// Band chip: role colors/label like a normal chip, plus inner grip
        /// bars at both ends and the X inset past the right grip. Display-only.
        public static void DrawBandChip(Rect rect, Role role)
        {
            var spec = new ChipSpec
            {
                Bg = role.hasCustomColor ? role.color : DefaultChipColor,
                Outline = OutlineColor,
                LabelColor = LabelColor,
                Label = role.label,
                ShowRemove = true,
                Grips = true,
                LabelInsetLeft = ChipUI.BandOuterPad + ChipUI.BandHandleW + 2f,
                // Right grip zone + the X slot (2px to the grip, 4px to the label).
                LabelInsetRight = ChipUI.BandOuterPad + ChipUI.BandHandleW + 2f + RemoveSize + 4f,
            };
            ChipUI.Draw(rect, in spec);
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

            Color labelColor =
                style == ChipStyle.Disabled || style == ChipStyle.AutoOff
                    ? new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.55f)
                : style == ChipStyle.Subtle
                    ? new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.65f)
                : LabelColor;

            // Compact initials get a uniform 4px inset (the exact-measure slack
            // covers it) so text stays left-aligned across rows.
            var spec = new ChipSpec
            {
                Bg = bg,
                Outline = OutlineColor,
                LabelColor = labelColor,
                Label = display == ChipDisplay.Minimal ? null
                    : display == ChipDisplay.Compact && abbrev != null ? abbrev : role.label,
                ShowRemove = showRemove,
                LabelInsetLeft = (display == ChipDisplay.Compact ? 4f : PadFor(display))
                    + MarkerCount(role, pinned) * (RemoveSize + 2f),
                LabelInsetRight = PadFor(display) + (showRemove ? RemoveSize + 2f : 0f),
                StrikeThrough = style == ChipStyle.Disabled,
            };
            ChipUI.Draw(rect, in spec);

            // Prefix markers mirror the remove icon's slot, left of the label:
            // blocker veto, time rule, location rule, pin (plain roles only).
            // Drawn on top of the box; the label inset already reserves the slots.
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
                }
                if (role.blocker) Marker(WorkRolesTex.BlockerMarker, tinted: false); // full-color red X
                if (role.activeHours != Role.AllHours) Marker(WorkRolesTex.TimeMarker, tinted: true);
                if (role.locationTokens.Count > 0) Marker(WorkRolesTex.LocationMarker, tinted: true);
                // The pin texture has less padding than the others; drawn
                // smaller so it doesn't dominate the strip.
                if (PinShown(role, pinned)) Marker(WorkRolesTex.PinMarker, tinted: true, size: 13f);
            }

            // Abbreviated chips: the tooltip carries the identity.
            if (display != ChipDisplay.Normal && Mouse.IsOver(rect))
                TooltipHandler.TipRegion(rect, role.label);

            if (!interactive) return ChipClick.None;

            Rect removeRect = ChipUI.RemoveRect(rect);

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
                RoleDrag.OnPress(rect, role.id, dragSource, onClick);
                e.Use();
            }
            return ChipClick.None;
        }
    }
}
