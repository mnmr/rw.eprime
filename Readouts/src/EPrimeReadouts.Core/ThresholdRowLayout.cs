namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Column layout for the threshold editor row: low label, low field,
    /// critical label, critical field, set button, clear button. Label and
    /// button widths are measured by the game layer (they vary with language
    /// and with the Small-font substitution when tiny text is unavailable);
    /// each column starts where the previous one ends, so wider text shifts
    /// the row instead of clipping. Measured widths clamp up to the classic
    /// baseline so the English tiny-font layout is reproduced exactly.
    /// </summary>
    public readonly struct ThresholdRowLayout
    {
        /// Gap between a label (or button) and the control that follows it.
        public const float LabelGap = 4f;
        /// Gap separating the low/critical/button column groups.
        public const float GroupGap = 8f;
        public const float FieldW = 60f;
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

        public float Width => ClearX + ClearW;

        private ThresholdRowLayout(float lowLabelW, float criticalLabelW,
            float setW, float clearW)
        {
            LowLabelW = lowLabelW;
            LowFieldX = lowLabelW + LabelGap;
            CriticalLabelX = LowFieldX + FieldW + GroupGap;
            CriticalLabelW = criticalLabelW;
            CriticalFieldX = CriticalLabelX + criticalLabelW + LabelGap;
            SetX = CriticalFieldX + FieldW + GroupGap;
            SetW = setW;
            ClearX = SetX + setW + LabelGap;
            ClearW = clearW;
        }

        public static ThresholdRowLayout Compute(float lowLabelW,
            float criticalLabelW, float setW, float clearW) =>
            new ThresholdRowLayout(
                lowLabelW > MinLowLabelW ? lowLabelW : MinLowLabelW,
                criticalLabelW > MinCriticalLabelW ? criticalLabelW : MinCriticalLabelW,
                setW > MinSetW ? setW : MinSetW,
                clearW > MinClearW ? clearW : MinClearW);
    }
}
