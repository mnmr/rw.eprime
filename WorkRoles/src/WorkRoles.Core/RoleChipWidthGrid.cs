using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// Collects natural assigned-chip widths for one roster generation and
    /// resolves either natural flow widths or one roster-wide grid width.
    /// </summary>
    public struct RoleChipWidthGrid
    {
        private float widest;

        public void Include(float width)
        {
            ValidateWidth(width, nameof(width));
            if (width > widest) widest = width;
        }

        public float WidthFor(float naturalWidth, bool grid)
        {
            ValidateWidth(naturalWidth, nameof(naturalWidth));
            return grid ? widest : naturalWidth;
        }

        /// <summary>
        /// Returns a row's width including its existing trailing gap contract.
        /// </summary>
        public float UnwrappedWidth(int chipCount, float naturalWidth,
            float gap, bool grid)
        {
            if (chipCount < 0)
                throw new ArgumentOutOfRangeException(nameof(chipCount));
            ValidateWidth(naturalWidth, nameof(naturalWidth));
            ValidateWidth(gap, nameof(gap));
            return grid ? chipCount * (widest + gap) : naturalWidth;
        }

        private static void ValidateWidth(float value, string parameter)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
