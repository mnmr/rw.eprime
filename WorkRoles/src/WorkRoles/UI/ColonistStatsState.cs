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
            internal SkillCapture Skills = null!;
            internal int FactRevision;
            internal int FactGeneration;
            internal int WorkRevision;
            internal int WorkGeneration;
        }

        private sealed class SkillInput
        {
            internal SkillLine Line;
            internal SkillSignalView View = null!;
            internal IReadOnlyList<Texture2D> Icons = null!;
            internal SignalBucket? Bucket;
            internal float RosterWidth => Line.Disabled || Icons.Count == 0
                ? RosterCellMinWidth : Mathf.Max(RosterCellMinWidth,
                    RosterIconStartX + Icons.Count * DecoratorSize + (Icons.Count - 1) * DecoratorGap);
        }

        private sealed class SkillCapture
        {
            internal SkillInput[] Inputs = null!;
            internal Dictionary<SkillDef, SkillInput> ByDef = null!;
            internal int DefinitionRevision;
            internal int LanguageRevision;
        }

        private sealed class SkillCaptureEntry
        {
            internal SkillCapture Capture = null!;
            internal int Revision;
            internal int Generation;
        }

        // Owner: Colonists window. Key: Pawn reference identity. Value: immutable
        // game-derived pawn/signal snapshots plus producer-owned skill indexes;
        // only stable external Def references are retained. Dependencies:
        // ExternalPawnFacts broad revisions publish display/grammar notifications;
        // narrower factual and work-evidence revisions gate actual game reads;
        // work-only changes preserve the skill capture. Live skill
        // XP is deliberately excluded. Refresh: explicit at Repaint. Equality:
        // name-only notifications reuse the exact factual and skill snapshots;
        // equal skill inputs preserve both per-skill and complete identities.
        // Teardown: Reset/ReleaseSnapshots
        // clears entries and the shared pawn-signal generation.
        private readonly SelectiveSnapshotCache<Pawn, ExternalCapture> externalSnapshots;

        // Owner: this window/cohort. Key: pawn, then SkillDef identity. Value:
        // immutable owned skill inputs (lines, signal views, icon references and
        // raw buckets). Dependencies: FactRevisions and language/definitions.
        // Refresh: explicit factual capture; equality preserves each unchanged
        // input and the whole capture. Teardown: cohort departures, reset/release.
        private readonly Dictionary<Pawn, SkillCaptureEntry> skillCaptures =
            new Dictionary<Pawn, SkillCaptureEntry>(ReferenceIdentityComparer<Pawn>.Instance);
        private readonly List<Pawn> departedSkills = new List<Pawn>();

        // Owner: Colonists window external-snapshot generation. Key: SkillDef
        // reference identity. Value: measured maximum roster-cell width.
        // Dependencies: actual per-pawn disabled/icon-width contributions only.
        // Refresh: lazy per affected skill after changed contributions; names,
        // downed state, levels, tuning and metrics preserve these logical widths
        // whenever the recaptured disabled/decorator contribution is equal.
        // Equality: equal contributions retain maxima. Teardown: reset/release.
        private readonly Dictionary<SkillDef, float> rosterCellWidths =
            new Dictionary<SkillDef, float>();
        internal int RosterWidthsRevision { get; private set; }

        // Owner: Colonists window. Key: Pawn reference identity within RoleStore.
        // Value: immutable per-pawn stats, skill presentations and grid geometry
        // (privately owned cell/separator offsets), shared by the
        // roster, selected panel, and role tips. Dependencies: external capture
        // identity, store identity, the five skill-promotion tuning inputs, language,
        // definitions, and UiRevision metrics. Role/assignment UiVersion is not
        // consumed. Refresh: lazy on changed dependencies; external refresh
        // removes departed pawns only. Equality: unchanged presentation facts
        // preserve individual and complete snapshot identities; grid geometry
        // is reused when skill count and measured column width are equal. Teardown:
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
            externalSnapshots = new SelectiveSnapshotCache<Pawn, ExternalCapture>(
                CaptureExternal, ReferenceIdentityComparer<Pawn>.Instance);
            stats = new PawnPresentationCache<Pawn, ColonistStatsSnapshot>(
                BuildSnapshot, StatsContentEquals, ReferenceIdentityComparer<Pawn>.Instance);
        }

        internal bool NeedsExternalSnapshotRefresh =>
            externalSnapshots.NeedsRefresh(ExternalPawnFacts.Revisions);

        internal void Reset(IEnumerable<Pawn> pawns)
        {
            PawnSignalSnapshotCache.Clear();
            externalSnapshots.Clear();
            skillCaptures.Clear();
            rosterCellWidths.Clear();
            RosterWidthsRevision++;
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
            skillCaptures.Clear();
            departedSkills.Clear();
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

            // Keep previous stats available to compare each refreshed skill.
            // Only departures release ownership; changed owners retain their
            // snapshots until the next dependency-gated read.
            departedSkills.Clear();
            foreach (var pair in skillCaptures)
                if (!externalSnapshots.Contains(pair.Key)) departedSkills.Add(pair.Key);
            for (int i = 0; i < departedSkills.Count; i++)
            {
                Pawn pawn = departedSkills[i];
                InvalidateRosterWidths(skillCaptures[pawn].Capture, null);
                skillCaptures.Remove(pawn);
                stats.Remove(pawn);
            }
            departedSkills.Clear();
            return true;
        }

        internal bool Contains(Pawn pawn) => externalSnapshots.Contains(pawn);

        private ExternalCapture CaptureExternal(Pawn pawn)
        {
            externalSnapshots.TryGet(pawn, out ExternalCapture? previous);
            SkillCapture skills = CaptureSkills(pawn);
            int factRevision = ExternalPawnFacts.FactRevisions.RevisionOf(pawn);
            int factGeneration = ExternalPawnFacts.FactRevisions.FullGeneration;
            int workRevision = ExternalPawnFacts.WorkEvidenceRevisions.RevisionOf(pawn);
            int workGeneration = ExternalPawnFacts.WorkEvidenceRevisions.FullGeneration;
            PawnExternalSnapshot snapshot = previous != null
                && previous.FactRevision == factRevision
                && previous.FactGeneration == factGeneration
                && previous.WorkRevision == workRevision
                && previous.WorkGeneration == workGeneration
                ? previous.Snapshot
                : RecsAdapter.CapturePawnSnapshot(pawn, PawnSignalSnapshotCache.Get(pawn));
            return new ExternalCapture {
                Snapshot = snapshot, Skills = skills,
                FactRevision = factRevision, FactGeneration = factGeneration,
                WorkRevision = workRevision, WorkGeneration = workGeneration };
        }

        private SkillCapture CaptureSkills(Pawn pawn)
        {
            int revision = ExternalPawnFacts.FactRevisions.RevisionOf(pawn);
            int generation = ExternalPawnFacts.FactRevisions.FullGeneration;
            skillCaptures.TryGetValue(pawn, out SkillCaptureEntry? entry);
            if (entry != null && entry.Revision == revision && entry.Generation == generation)
                return entry.Capture;
            SkillCapture? previous = entry?.Capture;
            PawnSignalSnapshot signals = PawnSignalSnapshotCache.Get(pawn);
            List<SkillLine> lines = SkillsTip.Lines(pawn);
            var inputs = new SkillInput[lines.Count];
            var byDef = new Dictionary<SkillDef, SkillInput>();
            bool equal = previous != null && previous.Inputs.Length == lines.Count;
            bool sameDefinitions = previous != null
                && previous.DefinitionRevision == DefinitionReloadCoordinator.Revision
                && previous.LanguageRevision == LanguageChangeCoordinator.Revision;
            for (int i = 0; i < lines.Count; i++)
            {
                SkillLine line = lines[i];
                SkillInput? old = null;
                previous?.ByDef.TryGetValue(line.Def, out old);
                bool sameView = sameDefinitions && old != null
                    && old.View.Matches(signals.Signals, line.Def.defName);
                SkillSignalView view = sameView ? old!.View
                    : SignalPresentationPolicy.ForSkill(signals.Signals, line.Def.defName);
                SignalBucket? bucket = signals.SkillBuckets.ForSkill(line.Def.defName)?.Bucket;
                SkillInput input = sameView && old!.Bucket == bucket && SameLine(old.Line, line)
                    ? old : new SkillInput { Line = line, View = view, Bucket = bucket,
                        Icons = sameView ? old!.Icons : SkillSignalPresentation.ResolveIcons(view) };
                inputs[i] = input;
                byDef.Add(line.Def, input);
                equal &= previous != null && i < previous.Inputs.Length
                    && ReferenceEquals(previous.Inputs[i], input);
            }
            SkillCapture next = equal ? previous! : new SkillCapture { Inputs = inputs,
                ByDef = byDef, DefinitionRevision = DefinitionReloadCoordinator.Revision,
                LanguageRevision = LanguageChangeCoordinator.Revision };
            if (!ReferenceEquals(previous, next)) InvalidateRosterWidths(previous, next);
            skillCaptures[pawn] = new SkillCaptureEntry {
                Capture = next, Revision = revision, Generation = generation };
            return next;
        }

        private static bool SameLine(SkillLine left, SkillLine right) =>
            ReferenceEquals(left.Def, right.Def) && left.Label == right.Label
            && left.ValueText == right.ValueText && left.Passion == right.Passion
            && left.Aptitude == right.Aptitude && left.Level == right.Level
            && left.SortValue == right.SortValue && left.Disabled == right.Disabled;

        private void InvalidateRosterWidths(SkillCapture? previous, SkillCapture? next)
        {
            bool changed = false;
            if (previous != null)
                for (int i = 0; i < previous.Inputs.Length; i++)
                {
                    SkillInput old = previous.Inputs[i];
                    if (next != null && next.ByDef.TryGetValue(old.Line.Def, out SkillInput current)
                        && old.RosterWidth == current.RosterWidth) continue;
                    rosterCellWidths.Remove(old.Line.Def);
                    changed = true;
                }
            if (next != null)
                for (int i = 0; i < next.Inputs.Length; i++)
                    if (previous == null || !previous.ByDef.ContainsKey(next.Inputs[i].Line.Def))
                    {
                        rosterCellWidths.Remove(next.Inputs[i].Line.Def);
                        changed = true;
                    }
            if (changed) RosterWidthsRevision++;
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
                && capture!.Skills.ByDef.TryGetValue(skill, out SkillInput input))
                return input.Line;
            return new SkillLine(skill!, // existing behavior: placeholder lines may carry a null def
                skill?.skillLabel.CapitalizeFirst() ?? "",
                "-", Passion.None, 0, 0, -1f, disabled: true);
        }

        internal float SkillSortValue(Pawn pawn, SkillDef skill) =>
            SkillLineSnapshot(pawn, skill).SortValue;

        /// Widest roster cell for this skill, so a third-plus decorator widens
        /// the column instead of clipping. Whole logical pixels by construction.
        /// Only changed width contributions invalidate this lazy per-skill max.
        internal float RosterCellWidth(SkillDef skill)
        {
            if (skill == null) return RosterCellMinWidth;
            if (rosterCellWidths.TryGetValue(skill, out float width)) return width;
            width = RosterCellMinWidth;
            foreach (SkillCaptureEntry entry in skillCaptures.Values)
            {
                if (entry.Capture.ByDef.TryGetValue(skill, out SkillInput input))
                    width = Mathf.Max(width, input.RosterWidth);
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
            PawnSignalSnapshot signals = SignalSnapshot(pawn);
            SkillSignalView view = SignalPresentationPolicy.ForSkill(signals.Signals, line.Def?.defName!);
            return BuildPresentation(pawn, new SkillInput { Line = line, View = view,
                Icons = SkillSignalPresentation.ResolveIcons(view),
                Bucket = signals.SkillBuckets.ForSkill(line.Def?.defName!)?.Bucket }, null);
        }

        private ColonistSkillPresentation BuildPresentation(Pawn pawn, SkillInput input,
            ColonistSkillPresentation? previous)
        {
            SkillLine line = input.Line;
            bool sameSource = previous != null && previous.SourceMatches(
                input, LanguageChangeCoordinator.Revision,
                DefinitionReloadCoordinator.Revision);
            SkillSignalView signalView = input.View;
            IReadOnlyList<Texture2D> icons = input.Icons;
            float labelWidth = LabelWidth(line.Label);
            SignalBucket? baseBucket = input.Bucket;
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
                pawn, input, LanguageChangeCoordinator.Revision,
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
                store, capture!.Skills, (store?.recommendationTuning
                    ?? RecommendationsTuningOptions.Default).SkillPromotion,
                LanguageChangeCoordinator.Revision, DefinitionReloadCoordinator.Revision,
                UiRevision.Current));
        }

        private ColonistStatsSnapshot BuildSnapshot(Pawn pawn,
            ColonistStatsSnapshot? previous)
        {
            ExternalCapture capture = externalSnapshots.Get(pawn);
            SkillInput[] inputs = capture.Skills.Inputs;
            var items = new ColonistSkillPresentation[inputs.Length];
            float columnWidth = MinimumSkillColumnWidth;
            using (new TextBlock(GameFont.Small))
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    ColonistSkillPresentation item = BuildPresentation(pawn, inputs[i],
                        previous?.ForSkill(inputs[i].Line.Def));
                    items[i] = item;
                    float iconWidth = item.SignalIcons.Count == 0 ? 0f
                        : LabelDecoratorGap + item.SignalIcons.Count * DecoratorSize
                            + (item.SignalIcons.Count - 1) * DecoratorGap;
                    float required = item.LabelWidth + iconWidth
                        + SkillValueGap + SkillValueWidth + VerdictStarsReserve;
                    columnWidth = Mathf.Max(columnWidth, Mathf.Ceil(required));
                }
            }
            ColonistSkillGridLayout? grid = previous != null
                && previous.Skills.Count == items.Length
                && previous.SkillColumnWidth == columnWidth
                    ? previous.SkillGrid : null;
            return new ColonistStatsSnapshot(items, columnWidth, capture.Skills, grid);
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
        // skill input, language, definitions and consumed tuning result. Pawn
        // grammar follows its explicit broad external revision on Resolve only;
        // unchanged skills can therefore retain their presentation across names.
        // UI geometry
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
            private int grammarRevision;
            private int grammarGeneration;

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
                int revision = ExternalPawnFacts.Revisions.RevisionOf(pawn);
                int generation = ExternalPawnFacts.Revisions.FullGeneration;
                if (!built || grammarRevision != revision || grammarGeneration != generation)
                {
                    StructuredTip? rebuilt = SkillSignalPresentation.CreateTooltip(
                        pawn, line.Def?.defName!, line.Label, line.ValueText,
                        ColonistStatsState.SkillTextColor(line, view.PassionTier),
                        view, bucket, baseBucket, line.Level, promotionThreshold);
                    if (value == null || rebuilt == null || !value.ContentEquals(rebuilt))
                        value = rebuilt;
                    grammarRevision = revision;
                    grammarGeneration = generation;
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
            IReadOnlyList<ColonistSkillPresentation> skills, float skillColumnWidth,
            object? facts = null, ColonistSkillGridLayout? skillGrid = null)
        {
            Skills = skills;
            SkillColumnWidth = skillColumnWidth;
            SkillGrid = skillGrid ?? ColonistSkillGridLayout.Calculate(
                skills.Count, skillColumnWidth);
            Facts = facts ?? this;
        }

        internal object Facts { get; }
        internal IReadOnlyList<ColonistSkillPresentation> Skills { get; }
        internal float SkillColumnWidth { get; }
        internal ColonistSkillGridLayout SkillGrid { get; }

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
