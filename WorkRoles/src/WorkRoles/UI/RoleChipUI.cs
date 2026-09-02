using System;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    public enum ChipClick { None, Remove, Context }

    public enum ChipStyle
    {
        Normal,     // standard chip
        Disabled,   // role off: strong dim + strikes (RoleChipStrikes: 1 pawn, 2 global, 3 both)
        Subtle,     // already-assigned recommendation: bg at ~75% brightness, NO strike, full-brightness label
        ConditionalOff // rule-suppressed: dim like Disabled but NO strike (rules, not the player, turned it off)
    }

    /// Suitability badge shown as a chip's first marker: a vertical pair of
    /// 5-pointed stars, colored bottom-up by verdict strength (red for bad
    /// verdicts). Colors are resolved in cache builders; default (not shown)
    /// draws nothing.
    public readonly struct RoleChipVerdict
    {
        public RoleChipVerdict(Color bottom, Color top)
        {
            Bottom = bottom;
            Top = top;
            Shown = true;
        }

        public Color Bottom { get; }
        public Color Top { get; }
        public bool Shown { get; }
    }

    internal readonly struct RoleChipRenderData
    {
        internal RoleChipRenderData(int roleId, string label, Color baseColor,
            bool blocker, bool hasTimeRule, bool hasLocationRule, bool hasRules)
        {
            RoleId = roleId;
            Label = label;
            BaseColor = baseColor;
            Blocker = blocker;
            HasTimeRule = hasTimeRule;
            HasLocationRule = hasLocationRule;
            HasRules = hasRules;
        }

        internal static RoleChipRenderData From(Role role) =>
            new RoleChipRenderData(role.id, role.label,
                role.hasCustomColor ? role.color : RoleChipUI.DefaultChipColor,
                role.blocker, role.activeHours != Role.AllHours,
                role.locationTokens.Count > 0, role.HasRules);

        internal bool ContentEquals(RoleChipRenderData other) =>
            RoleId == other.RoleId
            && string.Equals(Label, other.Label,
                StringComparison.Ordinal)
            && BaseColor.r == other.BaseColor.r
            && BaseColor.g == other.BaseColor.g
            && BaseColor.b == other.BaseColor.b
            && BaseColor.a == other.BaseColor.a
            && Blocker == other.Blocker
            && HasTimeRule == other.HasTimeRule
            && HasLocationRule == other.HasLocationRule
            && HasRules == other.HasRules;

        internal int RoleId { get; }
        internal string Label { get; }
        internal Color BaseColor { get; }
        internal bool Blocker { get; }
        internal bool HasTimeRule { get; }
        internal bool HasLocationRule { get; }
        internal bool HasRules { get; }
    }

    public static class RoleChipUI
    {
        public const float Height = 24f;
        private const float PadX = 8f;
        private const float RemoveSize = ChipUI.RemoveSize;
        public static readonly Color DefaultChipColor = new Color(0.25f, 0.35f, 0.45f);
        private static readonly Color OutlineColor = new Color(1f, 1f, 1f, 0.25f);
        // Outline recolor (never a second ring) marking the role whose job the
        // pawn is doing right now; matches the path-chip selection idiom.
        private static readonly Color ActiveOutlineColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color LabelColor = new Color(0.95f, 0.95f, 0.95f);
        // #e8e6e0 at 0.85 alpha — tint for the conditional-role marker icon.
        public static readonly Color RuleMarkerColor = new Color(232f / 255f, 230f / 255f, 224f / 255f, 0.85f);
        private static readonly Color RemovedColor = new Color(1f, 0f, 0f, 1f); // #ff0000

        private const float VerdictStarSize = 10f;
        private const float RoleIconSize = 20f;
        /// Vertical two-star verdict stack: equal gaps above, between, and
        /// below the stars across the chip height, then each star nudged 1px
        /// toward the middle so the pair reads as one marker.
        internal static void DrawVerdictBadge(Rect rect, RoleChipVerdict verdict)
        {
            Color oldColor = GUI.color;
            try
            {
                float x = rect.x + (rect.width - VerdictStarSize) / 2f;
                float gap = (rect.height - 2f * VerdictStarSize) / 3f;
                GUI.color = verdict.Top;
                GUI.DrawTexture(new Rect(x, rect.y + gap + 1f, VerdictStarSize,
                    VerdictStarSize), WorkRolesTex.Star);
                GUI.color = verdict.Bottom;
                GUI.DrawTexture(new Rect(x, rect.y + gap * 2f + VerdictStarSize - 1f,
                    VerdictStarSize, VerdictStarSize), WorkRolesTex.Star);
            }
            finally
            {
                GUI.color = oldColor;
            }
        }

        /// Red border marking a chip whose role is about to be removed.
        /// Snapped like the chip box so it replaces the outline pixels exactly.
        public static void DrawRemovedOutline(Rect rect)
            => RimShared.UiLib.PixelBox.Outline(rect, RemovedColor);

        // Shared chip metrics: markers sit MarkerEdgePad from the left border
        // with MarkerGap between slots; the label follows the band (or the
        // border) by LabelGap, and ends LabelGap before the remove slot. The
        // verdict star pair gets a narrow slot so the stars hug the border
        // like icon markers do.
        private const float MarkerEdgePad = 3f;
        private const float MarkerGap = 2f;
        private const float VerdictSlotW = 12f;

        private static bool Condensed(ChipDisplay display) =>
            display != ChipDisplay.Normal;

        internal static bool UsesIcons(ChipDisplay display) =>
            display == ChipDisplay.Icons;

        /// Initials replace the label in the condensed displays (icons fall
        /// back to them when a role has no icon).
        internal static bool NeedsAbbreviation(ChipDisplay display) =>
            display == ChipDisplay.Compact || UsesIcons(display);

        private static float LabelGap(ChipDisplay display) =>
            Condensed(display) ? 2f : 4f;

        /// Prefix markers: suitability verdict, cached pawn capability, blocker,
        /// time rule, location rule, the forced-on flag, and — on plain manual
        /// roles only — the assignment pin. Blockers and rule-carrying roles are
        /// already plan-protected, so a pin there would be redundant. Returns
        /// the band's pixel width (slots plus inner gaps, no edge or label
        /// gaps); this math and the Draw sequence must agree.
        private static float MarkerBand(RoleChipRenderData role, bool pinned,
            RoleAssignmentWarningSeverity warningSeverity, bool forcedOn,
            bool verdictSlot)
        {
            int icons =
                (warningSeverity != RoleAssignmentWarningSeverity.None ? 1 : 0)
                + (role.Blocker ? 1 : 0)
                + (role.HasTimeRule ? 1 : 0)
                + (role.HasLocationRule ? 1 : 0)
                + (forcedOn ? 1 : 0)
                + (PinShown(role, pinned) ? 1 : 0);
            float band = verdictSlot ? VerdictSlotW : 0f;
            if (icons > 0)
                band += (band > 0f ? MarkerGap : 0f)
                    + icons * RemoveSize + (icons - 1) * MarkerGap;
            return band;
        }

        private static float LeftInset(float markerBand, ChipDisplay display) =>
            markerBand > 0f ? MarkerEdgePad + markerBand + LabelGap(display)
            : Condensed(display) ? 4f : PadFor(display);

        private static float RightInset(bool showRemove, ChipDisplay display) =>
            showRemove ? MarkerEdgePad + RemoveSize + LabelGap(display)
                : PadFor(display);

        private static bool PinShown(Role role, bool pinned) =>
            pinned && !role.blocker && !role.HasRules;

        private static bool PinShown(RoleChipRenderData role, bool pinned) =>
            pinned && !role.Blocker && !role.HasRules;

        /// One role drag ghost for every tab. It uses the normal chip renderer
        /// and adds a red veil and outline only when the current target rejects
        /// the drop. Group drags remain a Roles-tab-specific label ghost.
        internal static void DrawDragGhost()
        {
            RoleChipRenderData role = RoleDrag.RoleGhost;
            if (!RoleDrag.Active || RoleDrag.GroupId >= 0
                || role.RoleId != RoleDrag.RoleId) return;
            Vector2 mouse = Event.current.mousePosition;
            float width = WidthFor(role, showRemove: false);
            var rect = new Rect(mouse.x + 10f, mouse.y + 6f, width, Height);
            Draw(rect, role, ChipStyle.Normal, showRemove: false,
                dragSource: null, onClick: null, interactive: false);
            if (!RoleDrag.HoverBlocked) return;
            Widgets.DrawBoxSolid(rect, new Color(0.65f, 0.05f, 0.05f, 0.3f));
            Color oldColor = GUI.color;
            try
            {
                GUI.color = new Color(1f, 0.3f, 0.3f, 0.9f);
                Widgets.DrawBox(rect, 2);
            }
            finally
            {
                GUI.color = oldColor;
            }
        }

        /// Compact chips run tight: exact-measured initials need no breathing room.
        private static float PadFor(ChipDisplay display) =>
            Condensed(display) ? 2f : PadX;

        /// shortLabel: the text the width is measured from instead of the
        /// role label when given: initials for the condensed displays (kept
        /// at least two letters wide), or the sizing prefix of a cut
        /// full-name grid chip, whose fadeAllowance reserves the fade region
        /// after it.
        public static float WidthFor(Role role, bool showRemove,
            ChipDisplay display = ChipDisplay.Normal, string? shortLabel = null, bool pinned = false,
            RoleAssignmentWarningSeverity warningSeverity = RoleAssignmentWarningSeverity.None,
            bool forcedOn = false, bool verdictSlot = false,
            Texture2D? icon = null, bool fadeAllowance = false)
            => WidthFor(RoleChipRenderData.From(role), showRemove, display,
                shortLabel, pinned, warningSeverity, forcedOn, verdictSlot, icon,
                fadeAllowance);

        internal static float WidthFor(RoleChipRenderData role, bool showRemove,
            ChipDisplay display = ChipDisplay.Normal, string? shortLabel = null,
            bool pinned = false,
            RoleAssignmentWarningSeverity warningSeverity = RoleAssignmentWarningSeverity.None,
            bool forcedOn = false, bool verdictSlot = false,
            Texture2D? icon = null, bool fadeAllowance = false)
        {
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                float labelW = UsesIcons(display) && icon != null
                    ? RoleIconSize
                    : shortLabel == null
                        ? WrText.FitWidth(role.Label)
                    : Condensed(display)
                        ? System.Math.Max(WrText.FitWidth(shortLabel), WrText.FitWidth("MM"))
                        : WrText.FitWidth(shortLabel)
                            + (fadeAllowance ? ChipUI.LabelFadeWidth : 0f);
                return labelW + LabelInsets(role, showRemove, display, pinned,
                    warningSeverity, forcedOn, verdictSlot);
            }
            finally
            {
                Text.Font = oldFont;
            }
        }

        /// Small-font fit width of a label, for a chip snapshot that must
        /// know whether its name overruns its room at draw time.
        /// Builder-only.
        internal static float SmallFitWidth(string text)
        {
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                return WrText.FitWidth(text);
            }
            finally
            {
                Text.Font = oldFont;
            }
        }

        /// Horizontal space a chip spends outside its label: the marker band
        /// with its edge and label gaps, and the remove slot. Subtracting it
        /// from a grid column width gives the label room of that chip.
        internal static float LabelInsets(RoleChipRenderData role,
            bool showRemove, ChipDisplay display, bool pinned,
            RoleAssignmentWarningSeverity warningSeverity, bool forcedOn,
            bool verdictSlot)
        {
            float band = MarkerBand(role, pinned, warningSeverity, forcedOn,
                verdictSlot);
            return LeftInset(band, display) + RightInset(showRemove, display);
        }


        /// Band chip: role colors/label like a normal chip, plus inner grip
        /// bars at both ends and the X inset past the right grip. Display-only.
        /// showRemove: false hides the X (a path's owner role cannot leave it).
        public static void DrawBandChip(Rect rect, Role role, bool showRemove = true)
            => DrawBandChip(rect, RoleChipRenderData.From(role), showRemove);

        internal static void DrawBandChip(Rect rect, RoleChipRenderData role,
            bool showRemove = true)
        {
            var spec = new ChipSpec
            {
                Bg = role.BaseColor,
                Outline = OutlineColor,
                LabelColor = LabelColor,
                Label = role.Label,
                ShowRemove = showRemove,
                Grips = true,
                LabelInsetLeft = ChipUI.BandOuterPad + ChipUI.BandHandleW + 2f,
                // Right grip zone + the X slot (2px to the grip, 4px to the label).
                LabelInsetRight = ChipUI.BandOuterPad + ChipUI.BandHandleW + 2f
                    + (showRemove ? RemoveSize + 4f : 2f),
            };
            ChipUI.Draw(rect, in spec);
        }

        /// Draws a chip. Clicks are resolved centrally via RoleDrag.ResolveMouseUp.
        /// Only ChipClick.Remove is returned directly (immediate on MouseDown).
        /// interactive: false renders a display-only chip (no clicks, no drag).
        /// paint: false retains control registration/interaction but skips visuals.
        /// strikes: RoleChipStrikes pattern, consumed only by ChipStyle.Disabled.
        /// forcedOn: draws the Claim marker; the assignment overrides the
        /// role's global off.
        public static ChipClick Draw(Rect rect, Role role, ChipStyle style, bool showRemove, Pawn? dragSource, Action? onClick,
            bool interactive = true, ChipDisplay display = ChipDisplay.Normal, string? shortLabel = null, bool pinned = false,
            RoleAssignmentWarningSeverity warningSeverity = RoleAssignmentWarningSeverity.None,
            bool paint = true, bool activeOutline = false,
            int strikes = RoleChipStrikes.PawnOff, bool forcedOn = false,
            RoleChipVerdict verdict = default, Texture2D? icon = null,
            float fadeLabelWidth = 0f)
            => Draw(rect, RoleChipRenderData.From(role), style, showRemove,
                dragSource, onClick, interactive, display, shortLabel, pinned,
                warningSeverity, paint, activeOutline, strikes, forcedOn,
                verdict, icon, fadeLabelWidth);

        /// fadeLabelWidth: the role label's own fit width (SmallFitWidth)
        /// for a full-name grid chip. The whole label is drawn, clipped to
        /// its room with its tail faded when that width overruns it;
        /// shortLabel is then only the sizing text and is not drawn.
        internal static ChipClick Draw(Rect rect, RoleChipRenderData role,
            ChipStyle style, bool showRemove, Pawn? dragSource, Action? onClick,
            bool interactive = true, ChipDisplay display = ChipDisplay.Normal,
            string? shortLabel = null, bool pinned = false,
            RoleAssignmentWarningSeverity warningSeverity = RoleAssignmentWarningSeverity.None,
            bool paint = true, bool activeOutline = false,
            int strikes = RoleChipStrikes.PawnOff, bool forcedOn = false,
            RoleChipVerdict verdict = default, Texture2D? icon = null,
            float fadeLabelWidth = 0f)
        {
            if (paint)
            {
                Color bg = role.BaseColor;

                switch (style)
                {
                    case ChipStyle.Disabled:
                    case ChipStyle.ConditionalOff:
                        bg = new Color(bg.r * 0.4f, bg.g * 0.4f, bg.b * 0.4f);
                        break;
                    case ChipStyle.Subtle:
                        bg = new Color(bg.r * 0.6f, bg.g * 0.6f, bg.b * 0.6f, 0.4f);
                        break;
                    // Normal: bg unchanged
                }

                Color labelColor =
                    style == ChipStyle.Disabled || style == ChipStyle.ConditionalOff
                        ? new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.55f)
                    : style == ChipStyle.Subtle
                        ? new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.65f)
                    : LabelColor;

                float markerBand = MarkerBand(role, pinned, warningSeverity,
                    forcedOn, verdict.Shown);
                float leftInset = LeftInset(markerBand, display);
                float rightInset = RightInset(showRemove, display);
                string? label = UsesIcons(display) && icon != null
                    ? null
                    : fadeLabelWidth > 0f ? role.Label
                    : shortLabel ?? role.Label;
                var spec = new ChipSpec
                {
                    Bg = bg,
                    Outline = activeOutline ? ActiveOutlineColor : OutlineColor,
                    LabelColor = labelColor,
                    Label = label!,
                    Icon = UsesIcons(display) ? icon : null,
                    ShowRemove = showRemove,
                    LabelInsetLeft = leftInset,
                    LabelInsetRight = rightInset,
                    StrikeCount = style == ChipStyle.Disabled
                        ? strikes : RoleChipStrikes.None,
                    FadeLabelWidth = label != null ? fadeLabelWidth : 0f,
                };
                ChipUI.Draw(rect, in spec);

                // Prefix markers mirror the remove icon's slot, left of the label:
                // capability, blocker veto, time rule, location rule, pin.
                // Drawn on top of the box; the label inset already reserves the slots.
                // No tips here: a chip has exactly one tooltip, owned by the caller.
                Color oldMarkerColor = GUI.color;
                try
                {
                    float markerX = rect.x + MarkerEdgePad;
                    void Marker(Texture2D tex, bool tinted, float size = RemoveSize)
                    {
                        var markerRect = new Rect(markerX + (RemoveSize - size) / 2f,
                            rect.y + (rect.height - size) / 2f, size, size);
                        GUI.color = tinted ? RuleMarkerColor : Color.white;
                        GUI.DrawTexture(markerRect, tex);
                        GUI.color = Color.white;
                        markerX += RemoveSize + MarkerGap;
                    }
                    if (verdict.Shown)
                    {
                        // Full chip height (the star pair needs the vertical
                        // room) in the narrow star slot.
                        DrawVerdictBadge(new Rect(markerX, rect.y,
                            VerdictSlotW, rect.height), verdict);
                        markerX += VerdictSlotW + MarkerGap;
                    }
                    if (warningSeverity != RoleAssignmentWarningSeverity.None)
                        Marker(warningSeverity == RoleAssignmentWarningSeverity.Caution
                                ? WorkRolesTex.RoleCapabilityPartial
                                : WorkRolesTex.RoleCapabilityAll,
                            tinted: false);
                    if (role.Blocker) Marker(WorkRolesTex.BlockerMarker, tinted: false); // full-color red X
                    if (role.HasTimeRule) Marker(WorkRolesTex.TimeMarker, tinted: true);
                    if (role.HasLocationRule) Marker(WorkRolesTex.LocationMarker, tinted: true);
                    // Claim keeps its own colors so the override reads at a glance.
                    if (forcedOn) Marker(WorkRolesTex.ForceOnMarker, tinted: false);
                    // The pin texture has less padding than the others; drawn
                    // smaller so it doesn't dominate the strip.
                    if (PinShown(role, pinned)) Marker(WorkRolesTex.PinMarker,
                        tinted: true, size: 13f);
                }
                finally
                {
                    GUI.color = oldMarkerColor;
                }
            }

            if (!interactive) return ChipClick.None;

            // GetControlID is RimWorld's own identity mechanism for custom
            // draggable buttons. ObserveSource runs here, inside this chip's
            // current scroll/group clip, so Mouse.IsOver compares like units.
            int dragControlId = GUIUtility.GetControlID(FocusType.Passive, rect);
            RoleDrag.ObserveSource(dragControlId, rect);

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
                RoleDrag.OnPress(dragControlId, role, dragSource, onClick);
                e.Use();
            }
            return ChipClick.None;
        }
    }
}
