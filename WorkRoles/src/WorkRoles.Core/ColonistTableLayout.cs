using System;

namespace WorkRoles.Core
{
    public readonly struct ColonistTableHeaderLayout
    {
        private const float HeaderHeight = 30f;
        private const float GridWidth = 39f;
        private const float GridHeight = 20f;
        private const float EdgeInset = 5f;
        private const float ScrollBarReservation = 16f;

        private ColonistTableHeaderLayout(float headerWidth,
            float scrollContentWidth, float priorityGridLeft,
            float priorityGridTop)
        {
            HeaderWidth = headerWidth;
            ScrollContentWidth = scrollContentWidth;
            PriorityGridLeft = priorityGridLeft;
            PriorityGridTop = priorityGridTop;
        }

        public float HeaderWidth { get; }
        public float ScrollContentWidth { get; }
        public float PriorityGridLeft { get; }
        public float PriorityGridTop { get; }
        public float PriorityGridWidth => GridWidth;
        public float PriorityGridHeight => GridHeight;

        public static ColonistTableHeaderLayout Calculate(
            float tableLeft, float tableTop, float tableWidth)
        {
            float right = tableLeft + tableWidth;
            return new ColonistTableHeaderLayout(
                tableWidth,
                Math.Max(0f, tableWidth - ScrollBarReservation),
                right - EdgeInset - GridWidth,
                tableTop + (HeaderHeight - GridHeight) / 2f);
        }
    }
}
