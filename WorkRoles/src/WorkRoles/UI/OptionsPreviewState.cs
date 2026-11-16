using System.Collections.Generic;
using RimShared.UiLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Signals;

namespace WorkRoles.UI
{
    internal readonly struct OptionsPreviewChip
    {
        internal OptionsPreviewChip(RoleChipRenderData chip, Rect rect,
            ChipStyle style, bool showRemove, bool pinned, bool forcedOn,
            int strikes, RoleChipVerdict verdict, bool rulesDot)
        {
            Chip = chip;
            Rect = rect;
            Style = style;
            ShowRemove = showRemove;
            Pinned = pinned;
            ForcedOn = forcedOn;
            Strikes = strikes;
            Verdict = verdict;
            RulesDot = rulesDot;
        }

        internal RoleChipRenderData Chip { get; }
        internal Rect Rect { get; }
        internal ChipStyle Style { get; }
        internal bool ShowRemove { get; }
        internal bool Pinned { get; }
        internal bool ForcedOn { get; }
        internal int Strikes { get; }
        internal RoleChipVerdict Verdict { get; }
        internal bool RulesDot { get; }
    }

    internal readonly struct OptionsPreviewLabel
    {
        internal OptionsPreviewLabel(Rect rect, string text, Color color,
            bool tiny, TextAnchor anchor)
        {
            Rect = rect;
            Text = text;
            Color = color;
            Tiny = tiny;
            Anchor = anchor;
        }

        internal Rect Rect { get; }
        internal string Text { get; }
        internal Color Color { get; }
        internal bool Tiny { get; }
        internal TextAnchor Anchor { get; }
    }

    /// One preview strip: positioned chips, labels, star pairs, hairlines,
    /// an optional skill caption and an optional button face, all in
    /// strip-local coordinates.
    internal sealed class OptionsPreviewStrip
    {
        internal float Height;
        /// Skill caption entries (the colonist row's own segments), their
        /// origin and line box height.
        internal readonly List<ColonistsTabView.RowCaptionSegment> Caption =
            new List<ColonistsTabView.RowCaptionSegment>();
        internal Vector2 CaptionOrigin;
        internal float CaptionHeight;
        internal readonly List<OptionsPreviewChip> Chips = new List<OptionsPreviewChip>();
        internal readonly List<OptionsPreviewLabel> Labels = new List<OptionsPreviewLabel>();
        /// Horizontal star pairs (skill cells): x, y of the first star.
        internal readonly List<Vector2> StarOrigins = new List<Vector2>();
        internal readonly List<RoleChipVerdict> Stars = new List<RoleChipVerdict>();
        /// Hairlines: width 0 is vertical (height = length), height 0 is
        /// horizontal (width = length).
        internal readonly List<Rect> Lines = new List<Rect>();
        internal Rect Button;
        internal string? ButtonLabel;
    }

    internal sealed class OptionsPreviewSnapshot
    {
        internal readonly OptionsPreviewStrip Row = new OptionsPreviewStrip();
        internal readonly OptionsPreviewStrip Palette = new OptionsPreviewStrip();
        internal readonly OptionsPreviewStrip Panel = new OptionsPreviewStrip();
    }

    /// Live previews under the Display options: a colonist row, a palette
    /// snippet and a selected-colonist panel snippet, composed from the
    /// player's own roles and drawn by the real chip renderer.
    internal sealed class OptionsPreviewState
    {
        private const float Pad = 8f;
        private const float ChipGap = 4f;
        private const float StarSize = ColonistStatsState.VerdictStarSize;

        // Owner: Options tab (window). Key: the Colonists tab's catalog
        // snapshot identity. Value: one immutable preview snapshot (strip
        // geometry, resolved chip data, translated strings); nothing in it
        // is mutated after publication. Dependencies: catalog identity
        // (roles, labels, colours, rules, activation, palette clusters),
        // RoleChipUI.MetricRevision (UI metrics, marker display, remove
        // icon, pin gate), language and definition revisions (skill
        // labels), the strip width, and the exact display values the
        // strips show: colonist/palette verdicts, recommendations, skill
        // captions and palette grouping (palette strip only). Refresh:
        // immediate on the first read after a change. Equality: an
        // unchanged key reuses the snapshot. Teardown: Reset on window
        // release drops the catalog and snapshot references.
        private ColonistsRosterCatalogSnapshot? builtCatalog;
        private int builtMetric = -1;
        private int builtLanguage = -1;
        private int builtDefinitions = -1;
        private float builtWidth = -1f;
        private int builtFlags = -1;
        private OptionsPreviewSnapshot? snapshot;

        internal void Reset()
        {
            builtCatalog = null;
            snapshot = null;
            builtMetric = -1;
        }

        internal OptionsPreviewSnapshot Snapshot(
            ColonistsRosterCatalogSnapshot catalog, float width,
            OptionsRenderSnapshot options)
        {
            int flags = (options.ColonistVerdicts ? 1 : 0)
                | (options.PaletteVerdicts ? 2 : 0)
                | (options.Recommendations ? 8 : 0)
                | (options.SkillCaptions ? 16 : 0)
                | (options.PaletteGroupingIndex == 1 ? 32 : 0);
            if (snapshot != null && ReferenceEquals(builtCatalog, catalog)
                && builtMetric == RoleChipUI.MetricRevision
                && builtLanguage == LanguageChangeCoordinator.Revision
                && builtDefinitions == DefinitionReloadCoordinator.Revision
                && builtWidth == width && builtFlags == flags)
                return snapshot;
            snapshot = Build(catalog, width, options);
            builtCatalog = catalog;
            builtMetric = RoleChipUI.MetricRevision;
            builtLanguage = LanguageChangeCoordinator.Revision;
            builtDefinitions = DefinitionReloadCoordinator.Revision;
            builtWidth = width;
            builtFlags = flags;
            return snapshot;
        }

        private static OptionsPreviewSnapshot Build(
            ColonistsRosterCatalogSnapshot catalog, float width,
            OptionsRenderSnapshot options)
        {
            var result = new OptionsPreviewSnapshot();
            PaletteMode mode = options.PaletteGroupingIndex == 1
                ? PaletteMode.Groups : PaletteMode.Skills;
            List<ColonistPaletteRole> roles = SampleRoles(catalog);
            List<SkillDef> skills = DefDatabase<SkillDef>.AllDefsListForReading;

            BuildRow(result.Row, roles, width, options);
            BuildPalette(result.Palette, catalog, mode, roles, width, options);
            BuildPanel(result.Panel, roles, skills, width, options);
            return result;
        }

        // ponytail: the sample is picked by the default role names; a
        // colony without them shows its first palette roles instead.
        private static readonly string[] SampleLabels =
            { "Miner", "Miner Away", "Farmer", "Cook" };

        /// The same four roles for every strip, whatever the palette
        /// grouping: only the palette strip arranges them by grouping.
        private static List<ColonistPaletteRole> SampleRoles(
            ColonistsRosterCatalogSnapshot catalog)
        {
            var roles = new List<ColonistPaletteRole>(SampleLabels.Length);
            int clusterCount = catalog.PaletteClusterCount(PaletteMode.Skills);
            for (int pass = 0; pass <= SampleLabels.Length; pass++)
                for (int c = 0; c < clusterCount; c++)
                {
                    ColonistPaletteClusterSnapshot cluster =
                        catalog.PaletteClusterAt(PaletteMode.Skills, c);
                    for (int r = 0; r < cluster.Count; r++)
                    {
                        ColonistPaletteRole role = cluster.RoleAt(r);
                        // The last pass fills the sample from display order.
                        bool wanted = pass == SampleLabels.Length
                            ? roles.Count < SampleLabels.Length
                            : string.Equals(role.Chip.Label, SampleLabels[pass],
                                System.StringComparison.OrdinalIgnoreCase);
                        if (wanted && !Sampled(roles, role.Chip.RoleId))
                            roles.Add(role);
                    }
                }
            return roles;
        }

        private static bool Sampled(List<ColonistPaletteRole> roles, int roleId)
        {
            for (int i = 0; i < roles.Count; i++)
                if (roles[i].Chip.RoleId == roleId) return true;
            return false;
        }

        /// Colonist row: name, optional skill caption, assigned chips in
        /// the states the chip options affect.
        private static void BuildRow(OptionsPreviewStrip strip,
            List<ColonistPaletteRole> roles, float width,
            OptionsRenderSnapshot options)
        {
            const float nameW = 96f;
            bool caption = options.SkillCaptions;
            strip.Height = Pad * 2f + (caption ? 36f : RoleChipUI.Height);
            string name = "WR_PreviewColonist".Translate();
            if (caption)
            {
                strip.Labels.Add(new OptionsPreviewLabel(
                    new Rect(Pad, Pad, nameW, 22f), name, Color.white,
                    false, TextAnchor.MiddleLeft));
                // The colonist rows' own caption entries: abbreviation in
                // the verdict colour, level in the skill-value colour.
                strip.CaptionOrigin = new Vector2(Pad, Pad + 20f);
                strip.CaptionHeight = TinyText.LineHeight;
                strip.Caption.Add(ColonistsTabView.CaptionSegment(
                    SkillDefOf.Mining.label, 15,
                    SkillSignalPresentation.VerdictColor(SignalBucket.Great),
                    WrStyle.MinorAccent));
                strip.Caption.Add(ColonistsTabView.CaptionSegment(
                    SkillDefOf.Plants.label, 12,
                    SkillSignalPresentation.VerdictColor(SignalBucket.Strong),
                    Color.white));
            }
            else
                strip.Labels.Add(new OptionsPreviewLabel(
                    new Rect(Pad, Pad, nameW, RoleChipUI.Height), name,
                    Color.white, false, TextAnchor.MiddleLeft));

            float x = Pad + nameW + 8f;
            float y = (strip.Height - RoleChipUI.Height) / 2f;
            bool remove = RoleChipUI.RemoveIcon;
            for (int i = 0; i < roles.Count && i < 4; i++)
            {
                RoleChipRenderData chip = roles[i].Chip;
                // One chip per marker: off, always on, pinned, has rules.
                bool off = i == 0, forced = i == 1, pinned = i == 2;
                if (i == 3)
                    chip = new RoleChipRenderData(chip.RoleId, chip.Label,
                        chip.BaseColor, false, true, false, true);
                else
                    chip = new RoleChipRenderData(chip.RoleId, chip.Label,
                        chip.BaseColor, false, false, false, false);
                RoleChipVerdict verdict = options.ColonistVerdicts
                    ? SkillSignalPresentation.VerdictBadge(SampleBucket(i))
                    : default;
                float chipW = RoleChipUI.WidthFor(chip, remove, pinned: pinned,
                    forcedOn: forced, verdictSlot: verdict.Shown);
                if (x + chipW > width - Pad) break;
                strip.Chips.Add(new OptionsPreviewChip(chip,
                    new Rect(x, y, chipW, RoleChipUI.Height),
                    off ? ChipStyle.Disabled : ChipStyle.Normal, remove,
                    pinned, forced, RoleChipStrikes.PawnOff, verdict, true));
                x += chipW + ChipGap;
            }
        }

        /// Palette: the sample roles under the clusters the current
        /// grouping puts them in.
        private static void BuildPalette(OptionsPreviewStrip strip,
            ColonistsRosterCatalogSnapshot catalog, PaletteMode mode,
            List<ColonistPaletteRole> roles, float width,
            OptionsRenderSnapshot options)
        {
            const float captionH = 14f;
            strip.Height = Pad * 2f + captionH + 2f + RoleChipUI.Height;
            float x = Pad;
            int sample = 0;
            int clusterCount = catalog.PaletteClusterCount(mode);
            for (int c = 0; c < clusterCount; c++)
            {
                ColonistPaletteClusterSnapshot cluster =
                    catalog.PaletteClusterAt(mode, c);
                float captionW = WrText.FitTinyWidth(cluster.Label);
                float chipX = x;
                int placed = 0;
                for (int r = 0; r < cluster.Count; r++)
                {
                    ColonistPaletteRole role = cluster.RoleAt(r);
                    if (!Sampled(roles, role.Chip.RoleId)) continue;
                    RoleChipVerdict verdict = options.PaletteVerdicts
                            && !role.Chip.Blocker
                        ? SkillSignalPresentation.VerdictBadge(
                            SampleBucket(sample++))
                        : default;
                    bool rulesDot = mode != PaletteMode.Groups;
                    float chipW = RoleChipUI.WidthFor(role.Chip, false,
                        verdictSlot: verdict.Shown, rulesDot: rulesDot);
                    if (chipX + chipW > width - Pad) break;
                    strip.Chips.Add(new OptionsPreviewChip(role.Chip,
                        new Rect(chipX, Pad + captionH + 2f, chipW,
                            RoleChipUI.Height),
                        role.Enabled ? ChipStyle.Normal : ChipStyle.Disabled,
                        false, false, false, RoleChipStrikes.GlobalOff,
                        verdict, rulesDot));
                    chipX += chipW + ChipGap;
                    placed++;
                }
                if (placed == 0) continue;
                strip.Labels.Add(new OptionsPreviewLabel(
                    new Rect(x, Pad, Mathf.Max(captionW, chipX - x), captionH),
                    cluster.Label, WrStyle.CaptionText, true,
                    TextAnchor.UpperLeft));
                x = Mathf.Max(chipX - ChipGap, x + captionW) + 16f;
            }
        }

        /// Selected colonist panel: two skill cells, then the
        /// recommendations line when recommendations are on.
        private static void BuildPanel(OptionsPreviewStrip strip,
            List<ColonistPaletteRole> roles, List<SkillDef> skills,
            float width, OptionsRenderSnapshot options)
        {
            const float cellH = ColonistSkillGridLayout.CellHeight;
            const float cellW = 200f;
            float reserve = options.PaletteVerdicts
                ? ColonistStatsState.VerdictStarsReserve : 0f;
            float y = Pad;
            for (int i = 0; i < 2 && i < skills.Count; i++)
            {
                float x = Pad + i * (cellW + 25f);
                if (x + cellW > width - Pad) break;
                if (i > 0)
                    strip.Lines.Add(new Rect(x - 13f, y, 0f, cellH));
                strip.Labels.Add(new OptionsPreviewLabel(
                    new Rect(x, y, cellW - 60f - reserve, cellH),
                    skills[i].LabelCap.ToString(), Color.white, false,
                    TextAnchor.MiddleLeft));
                strip.Labels.Add(new OptionsPreviewLabel(
                    new Rect(x + cellW - 56f - reserve, y, 56f, cellH),
                    i == 0 ? "12.40" : "6.10", Color.white, false,
                    TextAnchor.MiddleRight));
                if (reserve > 0f)
                {
                    strip.StarOrigins.Add(new Vector2(
                        x + cellW - 2f * StarSize, y + (cellH - StarSize) / 2f));
                    strip.Stars.Add(SkillSignalPresentation.VerdictBadgePanel(
                        i == 0 ? SignalBucket.Strong : SignalBucket.Neutral));
                }
            }
            y += cellH;
            if (options.Recommendations)
            {
                y += 6f;
                strip.Lines.Add(new Rect(Pad, y, width - 2f * Pad, 0f));
                y += 6f;
                string prefix = "WR_RecommendedRoles".Translate();
                float prefixW = RoleChipUI.SmallFitWidth(prefix);
                strip.Labels.Add(new OptionsPreviewLabel(
                    new Rect(Pad, y, prefixW, RoleChipUI.Height), prefix,
                    WrStyle.CaptionText, false, TextAnchor.MiddleLeft));
                const float buttonW = 106f;
                strip.Button = new Rect(width - Pad - buttonW, y, buttonW,
                    RoleChipUI.Height);
                strip.ButtonLabel = "WR_MakeItSo".Translate();
                float x = Pad + prefixW + 8f;
                bool remove = RoleChipUI.RemoveIcon;
                for (int i = 0; i < roles.Count && i < 3; i++)
                {
                    RoleChipRenderData chip = roles[i].Chip;
                    // Two kept roles, then one the plan would add.
                    bool assigned = i < 2;
                    RoleChipVerdict verdict = !chip.Blocker
                        ? SkillSignalPresentation.VerdictBadge(SampleBucket(i + 1))
                        : default;
                    float chipW = RoleChipUI.WidthFor(chip,
                        assigned && remove, verdictSlot: verdict.Shown);
                    if (x + chipW > strip.Button.x - 8f) break;
                    strip.Chips.Add(new OptionsPreviewChip(chip,
                        new Rect(x, y, chipW, RoleChipUI.Height),
                        assigned ? ChipStyle.Subtle : ChipStyle.Normal,
                        assigned && remove, false, false,
                        RoleChipStrikes.None, verdict, true));
                    x += chipW + ChipGap;
                }
                y += RoleChipUI.Height;
            }
            strip.Height = y + Pad;
        }

        private static SignalBucket SampleBucket(int index)
        {
            switch (index % 4)
            {
                case 0: return SignalBucket.Neutral;
                case 1: return SignalBucket.Strong;
                case 2: return SignalBucket.Great;
                default: return SignalBucket.Poor;
            }
        }

        /// Draws a strip from its published geometry: indexed reads only.
        internal static void Draw(Rect rect, OptionsPreviewStrip strip)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            bool oldWrap = Text.WordWrap;
            Color oldColor = GUI.color;
            try
            {
                PixelBox.SolidWithOutline(rect, WrStyle.PanelBackground,
                    WrStyle.PanelOutline);
                Text.WordWrap = false;
                for (int i = 0; i < strip.Lines.Count; i++)
                {
                    Rect line = strip.Lines[i];
                    Widgets.DrawBoxSolid(line.width == 0f
                        ? PixelBox.HairlineVertical(rect.x + line.x,
                            rect.y + line.y, line.height)
                        : PixelBox.HairlineHorizontal(rect.x + line.x,
                            rect.y + line.y, line.width),
                        WrStyle.PanelOutline);
                }
                for (int i = 0; i < strip.Labels.Count; i++)
                {
                    OptionsPreviewLabel label = strip.Labels[i];
                    var labelRect = new Rect(rect.x + label.Rect.x,
                        rect.y + label.Rect.y, label.Rect.width,
                        label.Rect.height);
                    GUI.color = label.Color;
                    Text.Anchor = label.Anchor;
                    if (label.Tiny) TinyText.Label(labelRect, label.Text);
                    else
                    {
                        Text.Font = GameFont.Small;
                        Widgets.Label(labelRect, label.Text);
                    }
                }
                Text.Anchor = TextAnchor.MiddleLeft;
                float captionX = rect.x + strip.CaptionOrigin.x;
                for (int i = 0; i < strip.Caption.Count; i++)
                    captionX = ColonistsTabView.DrawCaptionSegment(captionX,
                        rect.y + strip.CaptionOrigin.y, strip.CaptionHeight,
                        strip.Caption[i]);
                Text.Anchor = TextAnchor.UpperLeft;
                for (int i = 0; i < strip.Stars.Count; i++)
                {
                    Vector2 origin = strip.StarOrigins[i];
                    RoleChipVerdict stars = strip.Stars[i];
                    GUI.color = stars.Bottom;
                    GUI.DrawTexture(new Rect(rect.x + origin.x,
                        rect.y + origin.y, StarSize, StarSize),
                        WorkRolesTex.Star);
                    GUI.color = stars.Top;
                    GUI.DrawTexture(new Rect(rect.x + origin.x + StarSize,
                        rect.y + origin.y, StarSize, StarSize),
                        WorkRolesTex.Star);
                }
                GUI.color = Color.white;
                for (int i = 0; i < strip.Chips.Count; i++)
                {
                    OptionsPreviewChip chip = strip.Chips[i];
                    RoleChipUI.Draw(new Rect(rect.x + chip.Rect.x,
                            rect.y + chip.Rect.y, chip.Rect.width,
                            chip.Rect.height), chip.Chip, chip.Style,
                        chip.ShowRemove, dragSource: null, onClick: null,
                        interactive: false, pinned: chip.Pinned,
                        strikes: chip.Strikes, forcedOn: chip.ForcedOn,
                        verdict: chip.Verdict, rulesDot: chip.RulesDot);
                }
                if (strip.ButtonLabel != null)
                    Widgets.ButtonText(new Rect(rect.x + strip.Button.x,
                        rect.y + strip.Button.y, strip.Button.width,
                        strip.Button.height), strip.ButtonLabel,
                        active: false);
            }
            finally
            {
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;
                GUI.color = oldColor;
            }
        }
    }
}
