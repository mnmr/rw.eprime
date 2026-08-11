using System.Collections.Generic;
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
            Rect rowRect,
            string controlName,
            IReadOnlyList<string> enumOptions,
            IReadOnlyList<Color> enumColors)
        {
            Descriptor = descriptor;
            Value = value;
            SectionLabel = sectionLabel;
            Label = label;
            Description = description;
            ValueLabel = valueLabel;
            SectionRect = sectionRect;
            RowRect = rowRect;
            ControlName = controlName;
            EnumOptions = enumOptions;
            EnumColors = enumColors;
        }

        internal RecommendationTuningDescriptor Descriptor { get; }
        internal int Value { get; }
        internal string SectionLabel { get; }
        internal string Label { get; }
        internal string Description { get; }
        internal string ValueLabel { get; }
        internal Rect SectionRect { get; }
        internal Rect RowRect { get; }
        /// IMGUI focus identity for the row's editable numeric field.
        internal string ControlName { get; }
        /// Segment labels for enum-valued rows (index 0 = MinimumValue);
        /// null for numeric rows, which render steppers instead.
        internal IReadOnlyList<string> EnumOptions { get; }
        internal IReadOnlyList<Color> EnumColors { get; }
        internal bool StartsSection => SectionLabel != null;
    }
}
