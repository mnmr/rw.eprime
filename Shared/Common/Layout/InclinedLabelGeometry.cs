using System;

namespace RimShared.Common
{
    /// <summary>
    /// Axis-aligned space occupied above and to the right of a label whose
    /// lower-left corner is anchored at the bottom-right of a grid column.
    /// </summary>
    public readonly struct InclinedLabelGeometry
    {
        private InclinedLabelGeometry(
            float verticalExtent,
            float rightRunOut,
            float anchorToCenterX,
            float anchorToCenterY)
        {
            VerticalExtent = verticalExtent;
            RightRunOut = rightRunOut;
            AnchorToCenterX = anchorToCenterX;
            AnchorToCenterY = anchorToCenterY;
        }

        public float VerticalExtent { get; }
        public float RightRunOut { get; }
        public float AnchorToCenterX { get; }
        public float AnchorToCenterY { get; }

        public static InclinedLabelGeometry Calculate(
            float labelWidth,
            float labelHeight,
            float angleDegrees)
        {
            RequireNonnegativeFinite(labelWidth, nameof(labelWidth));
            RequireNonnegativeFinite(labelHeight, nameof(labelHeight));
            if (angleDegrees <= 0f || angleDegrees >= 90f
                || float.IsNaN(angleDegrees) || float.IsInfinity(angleDegrees))
                throw new ArgumentOutOfRangeException(nameof(angleDegrees));

            double radians = Math.PI / 180d * angleDegrees;
            float sine = (float)Math.Sin(radians);
            float cosine = (float)Math.Cos(radians);
            float verticalExtent = labelWidth * sine + labelHeight * cosine;
            return new InclinedLabelGeometry(
                verticalExtent,
                labelWidth * cosine,
                (labelWidth * cosine - labelHeight * sine) / 2f,
                -verticalExtent / 2f);
        }

        private static void RequireNonnegativeFinite(float value, string parameterName)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
