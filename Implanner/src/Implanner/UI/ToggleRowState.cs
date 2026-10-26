using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    internal enum ToggleRow
    {
        Enable, AutoProduction, IdleBenches, DesignatedBenches, Intermediaries,
        CountHospitalized, UpgradeByPriority, AutoFloor,
        MultipleBladders, MultipleHygiene, PurchaseOnly
    }

    internal sealed class ToggleRowSnapshot
    {
        private readonly float[] heights;
        internal readonly float IterationHeight;
        internal ToggleRowSnapshot(float[] ownedHeights, float iterationHeight)
        { heights = ownedHeights; IterationHeight = iterationHeight; }
        internal float this[ToggleRow row] => heights[(int)row];
        internal bool Matches(float[] values, float iterationHeight)
        {
            if (IterationHeight != iterationHeight) return false;
            for (int i = 0; i < heights.Length; i++)
                if (heights[i] != values[i]) return false;
            return true;
        }
    }

    internal sealed class ToggleRowState
    {
        // Owner: Implanner dialog. Key: narrowest checkbox label width and UI
        // metric revision, including language. Value: immutable per-caption
        // Small-font checkbox and iteration-segment heights; the snapshot
        // exclusively owns its array. Dependencies: those dimensions, the
        // segment count, UI scale/pixel insets and translated PlannerLabels.
        // Refresh: WindowUpdate, with a gated first-tab-render fallback.
        // Equality: equal rebuilt heights preserve snapshot identity.
        // Teardown: Release from window close; no game references/resources.
        private ToggleRowSnapshot? snapshot;
        private int revision = -1;
        private float width = -1;

        internal ToggleRowSnapshot Current(float labelWidth)
        {
            if (snapshot != null && revision == UiRevision.Current && width == labelWidth) return snapshot;
            string[] labels = { PlannerLabels.OptEnable, PlannerLabels.OptAutoProduction,
                PlannerLabels.OptIdleBenches, PlannerLabels.OptDesignatedBenches,
                PlannerLabels.OptIntermediaries, PlannerLabels.OptCountHospitalized,
                PlannerLabels.OptUpgradeByPriority, PlannerLabels.OptAutoFloor,
                PlannerLabels.OptAllowMultipleBladders, PlannerLabels.OptAllowMultipleHygieneEnhancers,
                PlannerLabels.OptShowPurchaseOnly };
            float[] heights = new float[labels.Length];
            float iterationHeight;
            using (GuiStateScope.Capture())
            {
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
                float minimum = Mathf.Max(28f, Mathf.Ceil(Text.LineHeight));
                for (int i = 0; i < labels.Length; i++)
                    heights[i] = Mathf.Max(minimum, Mathf.Ceil(Text.CalcHeight(labels[i], labelWidth)));
                // The conservative checkbox width excludes the 12px nested
                // indent and 24px checkbox. Restore those to obtain the narrowest
                // column width, then use SegmentedControl's physical-pixel frame
                // and divider insets. Flooring guards the one-pixel origin phase.
                float scale = Prefs.UIScale > 0f ? Prefs.UIScale : 1f;
                int count = PlannerLabels.IterationModes.Length;
                float segmentWidth = Mathf.Max(1f, Mathf.Floor(
                    (Mathf.Floor((labelWidth + 36f) * scale) - 4f - (count - 1)) / count) / scale);
                float segmentHeight = Mathf.Ceil(Text.LineHeight);
                for (int i = 0; i < count; i++)
                    segmentHeight = Mathf.Max(segmentHeight,
                        Mathf.Ceil(Text.CalcHeight(PlannerLabels.IterationModes[i], segmentWidth)));
                iterationHeight = Mathf.Max(30f, Mathf.Ceil(segmentHeight + 4f / scale));
            }
            if (snapshot == null || !snapshot.Matches(heights, iterationHeight))
                snapshot = new ToggleRowSnapshot(heights, iterationHeight);
            revision = UiRevision.Current; width = labelWidth;
            return snapshot;
        }

        internal void Release() { snapshot = null; revision = -1; width = -1; }
    }
}
