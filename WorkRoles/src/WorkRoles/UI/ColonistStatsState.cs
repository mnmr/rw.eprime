using System;
using System.Collections.Generic;
using RimShared.Common;
using RimShared.UiLib;
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
        // 0: the star polygon is inset ~1px inside its own rect, so touching
        // rects still show a visible seam.
        internal const float VerdictStarGap = 0f;
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
            // All fields are set by CaptureExternal's object initializer.
            internal PawnExternalSnapshot Snapshot = null!;
            internal List<SkillLine> SkillLines = null!;
            internal Dictionary<SkillDef, SkillLine> SkillLinesByDef = null!;
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

        // Owner: Colonists window. Key: Pawn reference identity within RoleStore.
        // Value: immutable per-pawn stats and skill presentations, shared by the
        // roster, selected panel, and role tips. Dependencies: external capture
        // identity, store identity, RecommendationTuningRevision, language,
        // definitions, and UiRevision metrics. Role/assignment UiVersion is not
        // consumed. Refresh: lazy on changed dependencies; external refresh
        // removes departed/changed pawns. Equality: unchanged presentation facts
        // preserve individual and complete snapshot identities. Teardown:
        // Reset/InvalidatePresentations/ReleaseSnapshots clear all entries.
        private readonly PawnPresentationCache<Pawn, ColonistStatsSnapshot> stats;
        private static readonly Func<ColonistStatsSnapshot, ColonistStatsSnapshot, bool>
            StatsContentEquals = (left, right) => left.ContentEquals(right);

        // Owner: window. Key: exact skill label, fixed Small font. Value: raw
        // measured width, immutable. Dependencies: label, language/definition
        // revisions, UiRevision metrics. Refresh: first builder read after a
        // revision changes; tuning and pawn changes reuse measurements. Equality:
        // repeated keys reuse the float. Teardown: InvalidatePresentations/reset.
        private readonly Dictionary<string, float> labelWidths =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private int labelMetricRevision = -1;
        private int labelLanguageRevision = -1;
        private int labelDefinitionRevision = -1;

        internal ColonistStatsState()
        {
            stats = new PawnPresentationCache<Pawn, ColonistStatsSnapshot>(
                BuildSnapshot, StatsContentEquals, ReferenceIdentityComparer<Pawn>.Instance);
        }

        internal bool NeedsExternalSnapshotRefresh =>
            externalSnapshots.NeedsRefresh(ExternalPawnFacts.Revisions);

        internal void Reset(IEnumerable<Pawn> pawns)
        {
            PawnSignalSnapshotCache.Clear();
            externalSnapshots.Clear();
            RefreshExternalSnapshot(pawns, out _);
            InvalidatePresentations();
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
            changedPawns.Clear();
            InvalidatePresentations();
        }

        /// Pawns whose capture was replaced or removed by the latest partial
        /// refresh; empty after a full generation. Producer-owned buffer.
        private readonly List<Pawn> changedPawns = new List<Pawn>();

        internal IReadOnlyList<Pawn> ChangedPawns => changedPawns;

        /// Reconciles the external cohort at the window's Repaint boundary.
        /// Unaffected owner snapshots and their derived presentations survive
        /// targeted invalidations; <paramref name="fullGeneration"/> reports
        /// a language/definition-style refresh that replaced everything.
        internal bool RefreshExternalSnapshot(IEnumerable<Pawn> pawns,
            out bool fullGeneration)
        {
            if (!externalSnapshots.Refresh(pawns, ExternalPawnFacts.Revisions,
                    changedPawns, out fullGeneration))
                return false;

            // Widest-cell widths aggregate every pawn: always recomputed.
            rosterCellWidths.Clear();
            if (fullGeneration)
            {
                InvalidatePresentations();
                return true;
            }
            for (int i = 0; i < changedPawns.Count; i++)
                InvalidatePresentationsFor(changedPawns[i]);
            return true;
        }

        /// Drops one pawn's owned presentation buffer at an explicit external
        /// refresh boundary, including removed pawns so none stay rooted.
        private void InvalidatePresentationsFor(Pawn pawn)
        {
            stats.Remove(pawn);
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
            externalSnapshots.TryGet(pawn, out ExternalCapture? capture)
                ? capture!.Snapshot
                : PawnExternalSnapshot.Empty;

        internal PawnSignalSnapshot SignalSnapshot(Pawn pawn) =>
            ExternalSnapshot(pawn).Signals;

        internal SkillLine SkillLineSnapshot(Pawn pawn, SkillDef skill)
        {
            if (pawn != null && skill != null
                && externalSnapshots.TryGet(pawn, out ExternalCapture? capture)
                && capture!.SkillLinesByDef.TryGetValue(skill, out SkillLine line))
                return line;
            return new SkillLine(skill!, // existing behavior: placeholder lines may carry a null def
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
                ColonistSkillPresentation? presentation = Snapshot(pawn).ForSkill(skill);
                if (presentation == null || presentation.Line.Disabled) continue;
                int icons = presentation.SignalIcons.Count;
                if (icons == 0) continue;
                width = Mathf.Max(width, RosterIconStartX
                    + icons * DecoratorSize + (icons - 1) * DecoratorGap);
            }
            rosterCellWidths[skill] = width;
            return width;
        }

        private void InvalidatePresentations()
        {
            stats.Clear();
            labelWidths.Clear();
            labelMetricRevision = -1;
            labelLanguageRevision = -1;
            labelDefinitionRevision = -1;
        }

        private float LabelWidth(string label)
        {
            if (labelMetricRevision != UiRevision.Current
                || labelLanguageRevision != LanguageChangeCoordinator.Revision
                || labelDefinitionRevision != DefinitionReloadCoordinator.Revision)
            {
                labelWidths.Clear();
                labelMetricRevision = UiRevision.Current;
                labelLanguageRevision = LanguageChangeCoordinator.Revision;
                labelDefinitionRevision = DefinitionReloadCoordinator.Revision;
            }
            if (!labelWidths.TryGetValue(label, out float width))
            {
                using (new TextBlock(GameFont.Small))
                    width = Text.CalcSize(label).x;
                labelWidths.Add(label, width);
            }
            return width;
        }

        internal ColonistSkillPresentation PresentationFor(Pawn pawn, SkillLine line)
        {
            ColonistStatsSnapshot snapshot = Snapshot(pawn);
            ColonistSkillPresentation? cached = snapshot.ForSkill(line.Def);
            if (cached != null) return cached;
            // A role can mention a skill absent from this pawn's tracker. This
            // placeholder is requested only by the revision-gated role-tip
            // builder; ordinary skill rows use the published snapshot above.
            return BuildPresentation(pawn, line, ExternalSnapshot(pawn), null);
        }

        private ColonistSkillPresentation BuildPresentation(Pawn pawn, SkillLine line,
            object externalIdentity, ColonistSkillPresentation? previous)
        {
            PawnSignalSnapshot pawnSnapshot = SignalSnapshot(pawn);
            bool sameSource = previous != null && previous.SourceMatches(
                externalIdentity, LanguageChangeCoordinator.Revision,
                DefinitionReloadCoordinator.Revision);
            SkillSignalView signalView = sameSource ? previous!.SignalView
                : SignalPresentationPolicy.ForSkill(
                    pawnSnapshot.Signals, line.Def?.defName!);
            IReadOnlyList<Texture2D> icons = sameSource ? previous!.SignalIcons
                : SkillSignalPresentation.ResolveIcons(signalView);
            float labelWidth = LabelWidth(line.Label);
            SignalBucket? baseBucket = pawnSnapshot.SkillBuckets
                .ForSkill(line.Def?.defName!)?.Bucket;
            SignalBucket? bucket = baseBucket;
            int promotionThreshold = -1;
            if (bucket.HasValue)
            {
                RecommendationsTuningOptions tuning =
                    RoleStore.Current?.recommendationTuning
                    ?? RecommendationsTuningOptions.Default;
                bucket = tuning.PromoteSkillSignal(line.Level, bucket.Value);
                if (bucket.Value > baseBucket!.Value) // baseBucket had a value for bucket.HasValue to hold
                    promotionThreshold = line.Level >= tuning.Get(
                            RecommendationTuningOption.OptionalTargetGreatLevel)
                        ? tuning.Get(
                            RecommendationTuningOption.OptionalTargetGreatLevel)
                        : tuning.Get(
                            RecommendationTuningOption.OptionalTargetStrongLevel);
            }

            if (sameSource && previous!.PresentationMatches(
                    labelWidth, bucket, baseBucket, promotionThreshold))
                return previous;
            return new ColonistSkillPresentation(
                pawn, externalIdentity, LanguageChangeCoordinator.Revision,
                DefinitionReloadCoordinator.Revision,
                line,
                labelWidth,
                signalView,
                icons,
                line.Disabled
                    ? default(RoleChipVerdict)
                    : SkillSignalPresentation.VerdictBadgePanel(
                        bucket ?? SignalBucket.Neutral),
                bucket, baseBucket, promotionThreshold,
                sameSource ? previous : null);
        }

        internal ColonistStatsSnapshot Snapshot(Pawn pawn)
        {
            if (!externalSnapshots.TryGet(pawn, out ExternalCapture? capture))
                return ColonistStatsSnapshot.Empty;
            RoleStore? store = RoleStore.Current;
            return stats.Get(pawn, new PawnPresentationStamp(
                store, capture!, store?.RecommendationTuningRevision ?? -1,
                LanguageChangeCoordinator.Revision, DefinitionReloadCoordinator.Revision,
                UiRevision.Current));
        }

        private ColonistStatsSnapshot BuildSnapshot(Pawn pawn,
            ColonistStatsSnapshot? previous)
        {
            ExternalCapture capture = externalSnapshots.Get(pawn);
            List<SkillLine> lines = capture.SkillLines;
            var items = new ColonistSkillPresentation[lines.Count];
            float columnWidth = MinimumSkillColumnWidth;
            using (new TextBlock(GameFont.Small))
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    ColonistSkillPresentation item = BuildPresentation(pawn, lines[i],
                        capture, previous?.ForSkill(lines[i].Def));
                    items[i] = item;
                    float iconWidth = item.SignalIcons.Count == 0 ? 0f
                        : LabelDecoratorGap + item.SignalIcons.Count * DecoratorSize
                            + (item.SignalIcons.Count - 1) * DecoratorGap;
                    float required = item.LabelWidth + iconWidth
                        + SkillValueGap + SkillValueWidth + VerdictStarsReserve;
                    columnWidth = Mathf.Max(columnWidth, Mathf.Ceil(required));
                }
            }
            return new ColonistStatsSnapshot(items, columnWidth);
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
        private readonly object externalIdentity;
        private readonly int languageRevision;
        private readonly int definitionRevision;
        private readonly SignalBucket? bucket;
        private readonly SignalBucket? baseBucket;
        private readonly int promotionThreshold;
        private readonly TooltipContent? tooltip;

        internal ColonistSkillPresentation(Pawn pawn, object externalIdentity,
            int languageRevision, int definitionRevision,
            SkillLine line, float labelWidth,
            SkillSignalView signalView, IReadOnlyList<Texture2D> signalIcons,
            RoleChipVerdict verdictStars, SignalBucket? bucket,
            SignalBucket? baseBucket, int promotionThreshold,
            ColonistSkillPresentation? previous)
        {
            this.externalIdentity = externalIdentity;
            this.languageRevision = languageRevision;
            this.definitionRevision = definitionRevision;
            this.bucket = bucket;
            this.baseBucket = baseBucket;
            this.promotionThreshold = promotionThreshold;
            Line = line;
            LabelWidth = labelWidth;
            SignalView = signalView;
            SignalIcons = signalIcons;
            VerdictStars = verdictStars;
            if (signalView.HasTooltip)
                tooltip = previous != null && previous.TooltipMatches(
                        bucket, baseBucket, promotionThreshold)
                    ? previous.tooltip
                    : new TooltipContent(pawn, line, signalView,
                        bucket, baseBucket, promotionThreshold);
        }

        internal bool SourceMatches(object external, int language, int definition) =>
            ReferenceEquals(externalIdentity, external)
            && languageRevision == language && definitionRevision == definition;

        private bool TooltipMatches(SignalBucket? nextBucket,
            SignalBucket? nextBaseBucket, int nextPromotionThreshold) =>
            bucket == nextBucket && baseBucket == nextBaseBucket
            && promotionThreshold == nextPromotionThreshold;

        internal bool PresentationMatches(float labelWidth,
            SignalBucket? nextBucket, SignalBucket? nextBaseBucket,
            int nextPromotionThreshold) => LabelWidth == labelWidth
            && TooltipMatches(nextBucket, nextBaseBucket, nextPromotionThreshold);

        internal SkillLine Line { get; }
        internal float LabelWidth { get; }
        internal SkillSignalView SignalView { get; }
        internal IReadOnlyList<Texture2D> SignalIcons { get; }
        /// The tooltip's (promoted) skill verdict as a star pair; default for
        /// disabled skills.
        internal RoleChipVerdict VerdictStars { get; }
        internal StructuredTip? Tooltip => tooltip?.Get();
        internal IStructuredTipSource? TooltipSource => tooltip;

        // Owner: one immutable skill presentation, shared across metric-only
        // replacements. Key: captured pawn/skill facts, source/language/definition
        // epoch and promoted verdict. Value: immutable tooltip content (including
        // a memoized null). Dependencies: the enclosing presentation's external
        // capture, language, definitions and consumed tuning result; UI geometry
        // belongs to the shared presenter's frozen session. Refresh: first
        // eligible hover request only. Equality: repeated requests reuse content.
        // Teardown: releasing the per-pawn snapshot releases this memo; there are
        // no subscriptions or owned Unity resources. The caller must check hover
        // eligibility before reading Tooltip.
        private sealed class TooltipContent : IStructuredTipSource
        {
            private readonly Pawn pawn;
            private readonly SkillLine line;
            private readonly SkillSignalView view;
            private readonly SignalBucket? bucket;
            private readonly SignalBucket? baseBucket;
            private readonly int promotionThreshold;
            private bool built;
            private StructuredTip? value;

            internal TooltipContent(Pawn pawn, SkillLine line,
                SkillSignalView view, SignalBucket? bucket,
                SignalBucket? baseBucket, int promotionThreshold)
            {
                this.pawn = pawn;
                this.line = line;
                this.view = view;
                this.bucket = bucket;
                this.baseBucket = baseBucket;
                this.promotionThreshold = promotionThreshold;
                StableKey = $"skill-signal:{pawn.thingIDNumber}:{line.Def?.defName}";
            }

            public string StableKey { get; }
            public StructuredTip? Resolve() => Get();

            internal StructuredTip? Get()
            {
                if (!built)
                {
                    value = SkillSignalPresentation.CreateTooltip(
                        pawn, line.Def?.defName!, line.Label, line.ValueText,
                        ColonistStatsState.SkillTextColor(line, view.PassionTier),
                        view, bucket, baseBucket, line.Level, promotionThreshold);
                    built = true;
                }
                return value;
            }
        }
    }

    internal sealed class ColonistStatsSnapshot
    {
        internal static readonly ColonistStatsSnapshot Empty =
            new ColonistStatsSnapshot(Array.Empty<ColonistSkillPresentation>(), 200f);

        internal ColonistStatsSnapshot(
            IReadOnlyList<ColonistSkillPresentation> skills, float skillColumnWidth)
        {
            Skills = skills;
            SkillColumnWidth = skillColumnWidth;
        }

        internal IReadOnlyList<ColonistSkillPresentation> Skills { get; }
        internal float SkillColumnWidth { get; }

        internal ColonistSkillPresentation? ForSkill(SkillDef skill)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (ReferenceEquals(Skills[i].Line.Def, skill)) return Skills[i];
            return null;
        }

        internal bool ContentEquals(ColonistStatsSnapshot other)
        {
            if (SkillColumnWidth != other.SkillColumnWidth
                || Skills.Count != other.Skills.Count) return false;
            for (int i = 0; i < Skills.Count; i++)
                if (!ReferenceEquals(Skills[i], other.Skills[i])) return false;
            return true;
        }
    }
}
