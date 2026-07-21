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
    /// Owns mutable signal observation and all skill/stats presentation derived
    /// from those snapshots. A revision callback invalidates the few external
    /// projections that also embed signal classifications.
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

        private readonly Action invalidateExternal;
        private readonly ObservationEpochGate<ScopeCacheStamp> listedObservations =
            new ObservationEpochGate<ScopeCacheStamp>();
        private readonly ObservationEpochGate<int> mapObservations =
            new ObservationEpochGate<int>();
        private long signalRevision = -1;

        private readonly Dictionary<(Pawn pawn, SkillDef skill), ColonistSkillPresentation>
            presentations =
                new Dictionary<(Pawn, SkillDef), ColonistSkillPresentation>();
        private int presentationStamp = -1;

        private int statsStamp = -1;
        private Pawn statsPawn;
        private ColonistStatsSnapshot stats;

        internal ColonistStatsState(Action invalidateExternal)
            => this.invalidateExternal = invalidateExternal;

        internal void Reset()
        {
            PawnSignalSnapshotCache.Clear();
            signalRevision = PawnSignalSnapshotCache.Revision;
            listedObservations.Clear();
            mapObservations.Clear();
            InvalidatePresentations();
        }

        internal void InvalidateLanguageCaches()
        {
            InvalidatePresentations();
        }

        internal void ReleaseSnapshots() => Reset();

        internal PawnSignalSnapshot Observe(Pawn pawn)
        {
            PawnSignalSnapshot snapshot = PawnSignalSnapshotCache.Get(pawn);
            ObserveRevision();
            return snapshot;
        }

        internal void Observe(IEnumerable<Pawn> pawns, ScopeCacheStamp cohort)
        {
            long epoch = PawnSignalSnapshotCache.ObservationEpoch;
            if (listedObservations.Enter(epoch, cohort) && pawns != null)
                foreach (Pawn pawn in pawns)
                    PawnSignalSnapshotCache.Get(pawn);
            ObserveRevision();
        }

        internal void Observe(Map map)
        {
            int mapId = map?.uniqueID ?? -1;
            long epoch = PawnSignalSnapshotCache.ObservationEpoch;
            if (mapObservations.Enter(epoch, mapId) && map != null)
            {
                foreach (Pawn pawn in map.mapPawns.FreeColonistsSpawned)
                    if (!pawn.DevelopmentalStage.Baby())
                        PawnSignalSnapshotCache.Get(pawn);
                foreach (Pawn pawn in map.mapPawns.SlavesOfColonySpawned)
                    if (!pawn.DevelopmentalStage.Baby())
                        PawnSignalSnapshotCache.Get(pawn);
            }
            ObserveRevision();
        }

        private void ObserveRevision()
        {
            long current = PawnSignalSnapshotCache.Revision;
            if (signalRevision == current) return;
            signalRevision = current;
            InvalidatePresentations();
            invalidateExternal();
        }

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
            PawnSignalSnapshot pawnSnapshot = Observe(pawn);
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
            Observe(pawn);
            if (statsStamp == UiVersion.Current && statsPawn == pawn) return stats;
            statsStamp = UiVersion.Current;
            statsPawn = pawn;

            List<SkillLine> lines = SkillsTip.Lines(pawn);
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
