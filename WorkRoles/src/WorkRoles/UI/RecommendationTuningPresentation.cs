using UnityEngine;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    internal sealed class RecommendationTuningRow
    {
        internal RecommendationTuningRow(
            RecommendationTuningDescriptor descriptor,
            int value,
            string sectionLabel,
            string label,
            string description,
            string valueLabel,
            Rect sectionRect,
            Rect rowRect)
        {
            Descriptor = descriptor;
            Value = value;
            SectionLabel = sectionLabel;
            Label = label;
            Description = description;
            ValueLabel = valueLabel;
            SectionRect = sectionRect;
            RowRect = rowRect;
        }

        internal RecommendationTuningDescriptor Descriptor { get; }
        internal int Value { get; }
        internal string SectionLabel { get; }
        internal string Label { get; }
        internal string Description { get; }
        internal string ValueLabel { get; }
        internal Rect SectionRect { get; }
        internal Rect RowRect { get; }
        internal bool StartsSection => SectionLabel != null;
    }
}
