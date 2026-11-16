namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Column layout for the threshold editor row: low label, low field,
    /// critical label, critical field, set button, clear button. Label and
    /// button widths are measured by the game layer (they vary with language
    /// and with the Small-font substitution when tiny text is unavailable);
    /// each column starts where the previous one ends, so wider text shifts
    /// the row instead of clipping. Measured widths clamp up to the classic
    /// baseline so the English tiny-font layout is reproduced exactly. A
    /// column narrower than the row takes the difference out of the two
    /// number fields, down to MinFieldW, so the buttons stay inside it.
    /// </summary>
    public readonly struct ThresholdRowLayout
    {
        /// Gap between a label (or button) and the control that follows it.
        public const float LabelGap = 4f;
        /// Gap separating the low/critical/button column groups.
        public const float GroupGap = 8f;
        public const float FieldW = 60f;
        public const float MinFieldW = 40f;
        public const float MinLowLabelW = 34f;
        public const float MinCriticalLabelW = 56f;
        public const float MinSetW = 50f;
        public const float MinClearW = 56f;

        public readonly float LowLabelW;
        public readonly float LowFieldX;
        public readonly float CriticalLabelX;
        public readonly float CriticalLabelW;
        public readonly float CriticalFieldX;
        public readonly float SetX;
        public readonly float SetW;
        public readonly float ClearX;
        public readonly float ClearW;
        /// Width of each number field: FieldW unless the column is narrower.
        public readonly float FieldWidth;

        public float Width => ClearX + ClearW;

        private ThresholdRowLayout(float lowLabelW, float criticalLabelW,
            float setW, float clearW, float fieldW)
        {
            FieldWidth = fieldW;
            LowLabelW = lowLabelW;
            LowFieldX = lowLabelW + LabelGap;
            CriticalLabelX = LowFieldX + fieldW + GroupGap;
            CriticalLabelW = criticalLabelW;
            CriticalFieldX = CriticalLabelX + criticalLabelW + LabelGap;
            SetX = CriticalFieldX + fieldW + GroupGap;
            SetW = setW;
            ClearX = SetX + setW + LabelGap;
            ClearW = clearW;
        }

        public static ThresholdRowLayout Compute(float lowLabelW,
            float criticalLabelW, float setW, float clearW,
            float availableWidth = float.MaxValue)
        {
            lowLabelW = lowLabelW > MinLowLabelW ? lowLabelW : MinLowLabelW;
            criticalLabelW = criticalLabelW > MinCriticalLabelW ? criticalLabelW : MinCriticalLabelW;
            setW = setW > MinSetW ? setW : MinSetW;
            clearW = clearW > MinClearW ? clearW : MinClearW;
            // Everything but the two fields; they share what is left.
            float rest = lowLabelW + criticalLabelW + setW + clearW
                + 3f * LabelGap + 2f * GroupGap;
            float fieldW = (availableWidth - rest) / 2f;
            fieldW = fieldW >= FieldW ? FieldW
                : fieldW <= MinFieldW ? MinFieldW
                : (float)System.Math.Floor(fieldW);
            return new ThresholdRowLayout(lowLabelW, criticalLabelW, setW, clearW, fieldW);
        }
    }
}
