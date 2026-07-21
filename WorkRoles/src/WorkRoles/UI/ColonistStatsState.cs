using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;
using WorkRoles.Signals;

namespace WorkRoles.UI
{
    /// Owns the explicit external pawn snapshot generation and all skill/stats
    /// presentation derived from it. Live game state is read only when a UI
    /// revision requests a complete generation refresh.
    internal sealed class ColonistStatsState
    {
        private const float MinimumSkillColumnWidth = 200f;
        private const float DecoratorSize = 16f;
        private const float DecoratorGap = 2f;
        private const float LabelDecoratorGap = 4f;
        private const float SkillValueGap = 8f;
        private const float SkillValueWidth = 48f;

        private static readonly Color Low = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color Major = new Color(1f, 0.65f, 0.2f);

        private int externalSnapshotStamp = -1;
        private readonly Dictionary<Pawn, PawnExternalSnapshot> externalSnapshots =
            new Dictionary<Pawn, PawnExternalSnapshot>();
        private readonly Dictionary<Pawn, List<SkillLine>> pawnSkillLines =
            new Dictionary<Pawn, List<SkillLine>>();
        private readonly Dictionary<(Pawn pawn, SkillDef skill), SkillLine> skillLines =
            new Dictionary<(Pawn, SkillDef), SkillLine>();

        private readonly Dictionary<(Pawn pawn, SkillDef skill), ColonistSkillPresentation>
            presentations =
                new Dictionary<(Pawn, SkillDef), ColonistSkillPresentation>();
        private int presentationStamp = -1;

        private int statsStamp = -1;
        private Pawn statsPawn;
        private ColonistStatsSnapshot stats;

        internal bool NeedsExternalSnapshotRefresh =>
            externalSnapshotStamp != UiVersion.Current;

        internal void Reset(IEnumerable<Pawn> pawns)
        {
            externalSnapshotStamp = -1;
            RefreshExternalSnapshot(pawns);
        }

        internal void InvalidateLanguageCaches()
        {
            InvalidatePresentations();
        }

        internal void ReleaseSnapshots()
        {
            externalSnapshotStamp = -1;
            PawnSignalSnapshotCache.Clear();
            externalSnapshots.Clear();
            pawnSkillLines.Clear();
            skillLines.Clear();
            InvalidatePresentations();
        }

        /// Rebuilds one complete generation only after an explicit UiVersion
        /// event. The caller invokes this at the window's Layout boundary.
        internal bool RefreshExternalSnapshot(IEnumerable<Pawn> pawns)
        {
            int current = UiVersion.Current;
            if (!NeedsExternalSnapshotRefresh) return false;

            PawnSignalSnapshotCache.Clear();
            externalSnapshots.Clear();
            pawnSkillLines.Clear();
            skillLines.Clear();
            if (pawns != null)
                foreach (Pawn pawn in pawns)
                {
                    if (pawn == null || externalSnapshots.ContainsKey(pawn)) continue;
                    PawnSignalSnapshot signals = PawnSignalSnapshotCache.Get(pawn);
                    List<SkillLine> lines = SkillsTip.Lines(pawn);
                    pawnSkillLines.Add(pawn, lines);
                    for (int i = 0; i < lines.Count; i++)
                        skillLines[(pawn, lines[i].Def)] = lines[i];
                    externalSnapshots.Add(pawn,
                        RecsAdapter.CapturePawnSnapshot(pawn, signals));
                }
            externalSnapshotStamp = current;
            InvalidatePresentations();
            return true;
        }

        internal PawnExternalSnapshot ExternalSnapshot(Pawn pawn) =>
            pawn != null && externalSnapshots.TryGetValue(
                pawn, out PawnExternalSnapshot snapshot)
                ? snapshot
                : PawnExternalSnapshot.Empty;

        internal PawnSignalSnapshot SignalSnapshot(Pawn pawn) =>
            ExternalSnapshot(pawn).Signals;

        internal SkillLine SkillLineSnapshot(Pawn pawn, SkillDef skill)
        {
            if (pawn != null && skill != null
                && skillLines.TryGetValue((pawn, skill), out SkillLine line))
                return line;
            return new SkillLine(skill,
                skill?.skillLabel.CapitalizeFirst() ?? "",
                "-", Passion.None, 0, 0, -1f, disabled: true);
        }

        internal float SkillSortValue(Pawn pawn, SkillDef skill) =>
            SkillLineSnapshot(pawn, skill).SortValue;

        private void InvalidatePresentations()
        {
            statsStamp = -1;
            statsPawn = null;
            stats = null;
            presentations.Clear();
            presentationStamp = -1;
        }

        internal ColonistSkillPresentation PresentationFor(Pawn pawn, SkillLine line)
        {
            PawnSignalSnapshot pawnSnapshot = SignalSnapshot(pawn);
            if (presentationStamp != UiVersion.Current)
            {
                presentations.Clear();
                presentationStamp = UiVersion.Current;
            }

            var key = (pawn, line.Def);
            if (presentations.TryGetValue(key, out ColonistSkillPresentation cached))
                return cached;

            SkillSignalView signalView = SignalPresentationPolicy.ForSkill(
                pawnSnapshot.Signals, line.Def?.defName);
            List<Texture2D> icons = SkillSignalPresentation.ResolveIcons(signalView);
            float labelWidth;
            using (new TextBlock(GameFont.Small))
                labelWidth = Text.CalcSize(line.Label).x;

            var result = new ColonistSkillPresentation(
                line,
                labelWidth,
                signalView,
                icons,
                SkillSignalPresentation.CreateTooltip(
                    pawn,
                    line.Def?.defName,
                    line.Label,
                    line.ValueText,
                    SkillTextColor(line, signalView.PassionTier),
                    signalView,
                    pawnSnapshot.SkillBuckets.ForSkill(line.Def?.defName)?.Bucket));
            presentations.Add(key, result);
            return result;
        }

        internal ColonistStatsSnapshot Snapshot(Pawn pawn)
        {
            SignalSnapshot(pawn);
            if (statsStamp == UiVersion.Current && statsPawn == pawn) return stats;
            statsStamp = UiVersion.Current;
            statsPawn = pawn;

            if (!pawnSkillLines.TryGetValue(pawn, out List<SkillLine> lines))
                lines = new List<SkillLine>();
            var items = new List<ColonistSkillPresentation>(lines.Count);
            float columnWidth = MinimumSkillColumnWidth;
            using (new TextBlock(GameFont.Small))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    ColonistSkillPresentation item = PresentationFor(pawn, lines[i]);
                    items.Add(item);
                    float iconWidth = item.SignalIcons.Count == 0 ? 0f
                        : LabelDecoratorGap + item.SignalIcons.Count * DecoratorSize
                            + (item.SignalIcons.Count - 1) * DecoratorGap;
                    float required = item.LabelWidth + iconWidth
                        + SkillValueGap + SkillValueWidth;
                    columnWidth = Mathf.Max(columnWidth, Mathf.Ceil(required));
                }
            }
            stats = new ColonistStatsSnapshot(items, columnWidth);
            return stats;
        }

        internal static Color SkillTextColor(SkillLine line, SignalPassionTier tier)
        {
            if (line.Disabled || line.Level <= 1) return WrStyle.DisabledText;
            if (line.Level <= 5) return Low;
            if (tier == SignalPassionTier.Major) return Major;
            if (tier == SignalPassionTier.Minor) return WrStyle.MinorAccent;
            return Color.white;
        }
    }

    internal sealed class ColonistSkillPresentation
    {
        internal ColonistSkillPresentation(SkillLine line, float labelWidth,
            SkillSignalView signalView, IReadOnlyList<Texture2D> signalIcons,
            StructuredTip tooltip)
        {
            Line = line;
            LabelWidth = labelWidth;
            SignalView = signalView;
            SignalIcons = signalIcons;
            Tooltip = tooltip;
        }

        internal SkillLine Line { get; }
        internal float LabelWidth { get; }
        internal SkillSignalView SignalView { get; }
        internal IReadOnlyList<Texture2D> SignalIcons { get; }
        internal StructuredTip Tooltip { get; }
    }

    internal sealed class ColonistStatsSnapshot
    {
        internal ColonistStatsSnapshot(
            IReadOnlyList<ColonistSkillPresentation> skills, float skillColumnWidth)
        {
            Skills = skills;
            SkillColumnWidth = skillColumnWidth;
        }

        internal IReadOnlyList<ColonistSkillPresentation> Skills { get; }
        internal float SkillColumnWidth { get; }
    }
}
