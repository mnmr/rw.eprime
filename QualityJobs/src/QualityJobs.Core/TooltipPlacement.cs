using System;

namespace QualityJobs.Core
{
    /// <summary>
    /// Pure bounded placement for a pointer-anchored tooltip. When an exclusion
    /// rectangle is supplied, the tooltip is moved below, above, right, or left
    /// of it in that order, and is omitted if no non-overlapping position fits.
    /// </summary>
    public static class TooltipPlacement
    {
        public static bool TryPlace(
            float mouseX, float mouseY, float width, float height,
            float screenWidth, float screenHeight, bool hasExclusion,
            float exclusionX, float exclusionY,
            float exclusionWidth, float exclusionHeight,
            out float x, out float y)
        {
            y = mouseY + 14f + height < screenHeight
                ? mouseY + 14f
                : mouseY - 5f - height >= 0f
                    ? mouseY - 5f - height
                    : screenHeight - 14f - height;
            x = mouseX + 16f + width < screenWidth
                ? mouseX + 16f
                : mouseX - 4f - width;
            x = Clamp(x, 0f, Math.Max(0f, screenWidth - width));
            y = Clamp(y, 0f, Math.Max(0f, screenHeight - height));

            if (!hasExclusion || !Overlaps(
                    x, y, width, height,
                    exclusionX, exclusionY, exclusionWidth, exclusionHeight))
                return true;

            const float gap = 4f;
            float below = exclusionY + exclusionHeight + gap;
            if (below + height <= screenHeight)
            {
                y = below;
                return true;
            }

            float above = exclusionY - gap - height;
            if (above >= 0f)
            {
                y = above;
                return true;
            }

            float right = exclusionX + exclusionWidth + gap;
            if (right + width <= screenWidth)
            {
                x = right;
                return true;
            }

            float left = exclusionX - gap - width;
            if (left >= 0f)
            {
                x = left;
                return true;
            }

            x = 0f;
            y = 0f;
            return false;
        }

        private static bool Overlaps(
            float x, float y, float width, float height,
            float otherX, float otherY, float otherWidth, float otherHeight) =>
            x < otherX + otherWidth && x + width > otherX
            && y < otherY + otherHeight && y + height > otherY;

        private static float Clamp(float value, float min, float max) =>
            value < min ? min : value > max ? max : value;
    }
}
