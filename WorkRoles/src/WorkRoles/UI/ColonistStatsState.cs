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
    /// Owns explicit external pawn snapshots and all skill/stats presentation
    /// derived from them. Role/UI revisions never recapture live pawn data;
    /// external invalidation rebuilds only dirty owners unless the complete
    /// generation changed.
    internal sealed class ColonistStatsState
    {
        private const float MinimumSkillColumnWidth = 200f;
        private const float DecoratorSize = 16f;
        private const float DecoratorGap = 2f;
        private const float LabelDecoratorGap = 4f;
        private const float SkillValueGap = 8f;
        private const float SkillValueWidth = 48f;
        // Horizontal verdict star pair after the (left-shifted) skill value.
        internal const float VerdictStarSize = 10f;
        internal const float VerdictStarGap = 2f;
        internal const float VerdictStarsReserve =
            4f + 2f * VerdictStarSize + VerdictStarGap;
        // Roster cell layout (DrawSkillCell): value slot, then 16px icons at
        // an 18px stride from x+48. Floor keeps room for two decorators.
        private const float RosterIconStartX = 48f;
        private const float RosterCellMinWidth = 82f;

        private static readonly Color Low = new Color(0.65f, 0.65f, 0.65f);
        private static readonly Color Major = new Color(1f, 0.65f, 0.2f);

        private sealed class ExternalCapture
        {
            internal PawnExternalSnapshot Snapshot;
            internal List<SkillLine> SkillLines;
            internal Dictionary<SkillDef, SkillLine> SkillLinesByDef;
        }

        // Owner: Colonists window. Key: Pawn reference identity. Value: immutable
        // game-derived pawn/signal snapshots plus producer-owned skill indexes;
        // only stable external Def references are retained. Dependencies:
        // ExternalPawnFacts current/full/per-pawn revisions; live skill XP is
        // deliberately excluded. Refresh: explicit at the window's Repaint
        // boundary, targeted where possible. Equality: unchanged revision gates
        // preserve capture identity; an invalidated external capture is replaced.
        // Teardown: Reset/ReleaseSnapshots
        // clears entries and the shared pawn-signal generation.
        private readonly SelectiveSnapshotCache<Pawn, ExternalCapture> externalSnapshots =
            new SelectiveSnapshotCache<Pawn, ExternalCapture>(
                CaptureExternal, ReferenceIdentityComparer<Pawn>.Instance);

        // Owner: Colonists window external-snapshot generation. Key: SkillDef
        // reference identity. Value: measured maximum roster-cell width.
        // Dependencies: the published external captures and signal decorators.
        // Refresh: lazy per skill after external generation refresh. Equality:
        // repeated reads reuse the stored float. Teardown: external refresh or
        // ReleaseSnapshots clears every measured width.
        private readonly Dictionary<SkillDef, float> rosterCellWidths =
            new Dictionary<SkillDef, float>();

        // Owner: Colonists window. Key: (Pawn, SkillDef). Value: immutable skill
        // render presentation with stable external texture references.
        // Dependencies: UiVersion presentation stamp, language, current RoleStore
        // identity and RecommendationTuningRevision, and the current external
        // pawn snapshot. Refresh: lazy on first presentation read after
        // invalidation. Equality: exact key hits preserve presentation identity.
        // Teardown: InvalidatePresentations/ReleaseSnapshots clears the table.
        private readonly Dictionary<(Pawn pawn, SkillDef skill), ColonistSkillPresentation>
            presentations =
                new Dictionary<(Pawn, SkillDef), ColonistSkillPresentation>();
        private int presentationStamp = -1;
        private RoleStore presentationStore;
        private int presentationTuningRevision = -1;

        // Owner: Colonists window. Key: (UiVersion, selected Pawn identity).
        // Value: immutable selected-pawn stats render projection with a
        // producer-owned presentation buffer. Dependencies: presentation cache,
        // language, RoleStore identity and RecommendationTuningRevision, and the
        // published external pawn snapshot. Refresh: immediate on the next
        // Snapshot read after key change. Equality: exact key hits preserve
        // snapshot identity. Teardown: InvalidatePresentations clears it.
        private int statsStamp = -1;
        private Pawn statsPawn;
        private ColonistStatsSnapshot stats;

        internal bool NeedsExternalSnapshotRefresh =>
            externalSnapshots.NeedsRefresh(ExternalPawnFacts.Revisions);

        internal void Reset(IEnumerable<Pawn> pawns)
        {
            PawnSignalSnapshotCache.Clear();
            externalSnapshots.Clear();
            RefreshExternalSnapshot(pawns);
        }

        internal void InvalidateLanguageCaches()
        {
            InvalidatePresentations();
        }

        internal void ReleaseSnapshots()
        {
            PawnSignalSnapshotCache.Clear();
            externalSnapshots.Clear();
            rosterCellWidths.Clear();
            InvalidatePresentations();
        }

        /// Reconciles the external cohort at the window's Repaint boundary.
        /// Unaffected owner snapshots survive targeted invalidations.
        internal bool RefreshExternalSnapshot(IEnumerable<Pawn> pawns)
        {
            if (!externalSnapshots.Refresh(pawns, ExternalPawnFacts.Revisions))
                return false;

            rosterCellWidths.Clear();
            InvalidatePresentations();
            return true;
        }

        private static ExternalCapture CaptureExternal(Pawn pawn)
        {
            PawnSignalSnapshot signals = PawnSignalSnapshotCache.Get(pawn);
            List<SkillLine> lines = SkillsTip.Lines(pawn);
            var byDef = new Dictionary<SkillDef, SkillLine>();
            for (int i = 0; i < lines.Count; i++)
                byDef[lines[i].Def] = lines[i];
            return new ExternalCapture
            {
                Snapshot = RecsAdapter.CapturePawnSnapshot(pawn, signals),
                SkillLines = lines,
                SkillLinesByDef = byDef,
            };
        }

        internal PawnExternalSnapshot ExternalSnapshot(Pawn pawn) =>
            externalSnapshots.TryGet(pawn, out ExternalCapture capture)
                ? capture.Snapshot
                : PawnExternalSnapshot.Empty;

        internal PawnSignalSnapshot SignalSnapshot(Pawn pawn) =>
            ExternalSnapshot(pawn).Signals;

        internal SkillLine SkillLineSnapshot(Pawn pawn, SkillDef skill)
        {
            if (pawn != null && skill != null
                && externalSnapshots.TryGet(pawn, out ExternalCapture capture)
                && capture.SkillLinesByDef.TryGetValue(skill, out SkillLine line))
                return line;
            return new SkillLine(skill,
                skill?.skillLabel.CapitalizeFirst() ?? "",
                "-", Passion.None, 0, 0, -1f, disabled: true);
        }

        internal float SkillSortValue(Pawn pawn, SkillDef skill) =>
            SkillLineSnapshot(pawn, skill).SortValue;

        /// Widest roster cell for this skill across the whole generation, so a
        /// third-plus decorator widens the column instead of clipping. Whole
        /// pixels by construction (48 + 16/2 multiples). Generation-scoped like
        /// the lines it measures; lazy per skill, so column toggles stay free.
        internal float RosterCellWidth(SkillDef skill)
        {
            if (skill == null) return RosterCellMinWidth;
            if (rosterCellWidths.TryGetValue(skill, out float width)) return width;
            width = RosterCellMinWidth;
            foreach (Pawn pawn in externalSnapshots.Owners)
            {
                if (!externalSnapshots.TryGet(pawn, out ExternalCapture capture)
                    || !capture.SkillLinesByDef.TryGetValue(skill, out SkillLine line)
                    || line.Disabled) continue;
                SkillSignalView view = SignalPresentationPolicy.ForSkill(
                    SignalSnapshot(pawn).Signals, skill.defName);
                int icons = SkillSignalPresentation.ResolveIcons(view).Count;
                if (icons == 0) continue;
                width = Mathf.Max(width, RosterIconStartX
                    + icons * DecoratorSize + (icons - 1) * DecoratorGap);
            }
            rosterCellWidths[skill] = width;
            return width;
        }

        private void InvalidatePresentations()
        {
            statsStamp = -1;
            statsPawn = null;
            stats = null;
            presentations.Clear();
            presentationStamp = -1;
            presentationStore = null;
            presentationTuningRevision = -1;
        }

        private void EnsurePresentationGeneration()
        {
            RoleStore store = RoleStore.Current;
            int tuningRevision = store?.RecommendationTuningRevision ?? -1;
            if (presentationStamp == UiVersion.Current
                && ReferenceEquals(presentationStore, store)
                && presentationTuningRevision == tuningRevision)
                return;
            presentations.Clear();
            presentationStamp = UiVersion.Current;
            presentationStore = store;
            presentationTuningRevision = tuningRevision;
            statsStamp = -1;
            statsPawn = null;
            stats = null;
        }

        internal ColonistSkillPresentation PresentationFor(Pawn pawn, SkillLine line)
        {
            PawnSignalSnapshot pawnSnapshot = SignalSnapshot(pawn);
            EnsurePresentationGeneration();

            var key = (pawn, line.Def);
            if (presentations.TryGetValue(key, out ColonistSkillPresentation cached))
                return cached;

            SkillSignalView signalView = SignalPresentationPolicy.ForSkill(
                pawnSnapshot.Signals, line.Def?.defName);
            List<Texture2D> icons = SkillSignalPresentation.ResolveIcons(signalView);
            float labelWidth;
            using (new TextBlock(GameFont.Small))
                labelWidth = Text.CalcSize(line.Label).x;
            SignalBucket? baseBucket = pawnSnapshot.SkillBuckets
                .ForSkill(line.Def?.defName)?.Bucket;
            SignalBucket? bucket = baseBucket;
            int promotionThreshold = -1;
            if (bucket.HasValue)
            {
                RecommendationsTuningOptions tuning =
                    presentationStore?.recommendationTuning
                    ?? RecommendationsTuningOptions.Default;
                bucket = tuning.PromoteSkillSignal(line.Level, bucket.Value);
                if (bucket.Value > baseBucket.Value)
                    promotionThreshold = line.Level >= tuning.Get(
                            RecommendationTuningOption.OptionalTargetGreatLevel)
                        ? tuning.Get(
                            RecommendationTuningOption.OptionalTargetGreatLevel)
                        : tuning.Get(
                            RecommendationTuningOption.OptionalTargetStrongLevel);
            }

            var result = new ColonistSkillPresentation(
                line,
                labelWidth,
                signalView,
                icons,
                line.Disabled
                    ? default(RoleChipVerdict)
                    : SkillSignalPresentation.VerdictBadgePanel(
                        bucket ?? SignalBucket.Neutral),
                SkillSignalPresentation.CreateTooltip(
                    pawn,
                    line.Def?.defName,
                    line.Label,
                    line.ValueText,
                    SkillTextColor(line, signalView.PassionTier),
                    signalView,
                    bucket,
                    baseBucket,
                    line.Level,
                    promotionThreshold));
            presentations.Add(key, result);
            return result;
        }

        internal ColonistStatsSnapshot Snapshot(Pawn pawn)
        {
            SignalSnapshot(pawn);
            EnsurePresentationGeneration();
            if (statsStamp == UiVersion.Current && statsPawn == pawn) return stats;
            statsStamp = UiVersion.Current;
            statsPawn = pawn;

            List<SkillLine> lines = externalSnapshots.TryGet(
                pawn, out ExternalCapture capture)
                ? capture.SkillLines
                : new List<SkillLine>();
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
                        + SkillValueGap + SkillValueWidth + VerdictStarsReserve;
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
            RoleChipVerdict verdictStars, StructuredTip tooltip)
        {
            Line = line;
            LabelWidth = labelWidth;
            SignalView = signalView;
            SignalIcons = signalIcons;
            VerdictStars = verdictStars;
            Tooltip = tooltip;
        }

        internal SkillLine Line { get; }
        internal float LabelWidth { get; }
        internal SkillSignalView SignalView { get; }
        internal IReadOnlyList<Texture2D> SignalIcons { get; }
        /// The tooltip's (promoted) skill verdict as a star pair; default for
        /// disabled skills.
        internal RoleChipVerdict VerdictStars { get; }
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
