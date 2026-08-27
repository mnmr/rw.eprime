using System.Collections.Generic;
using RimShared.Common;

namespace EPrimeReadouts.Core
{
    public readonly struct PanelBandRange
    {
        public PanelBandRange(int start, int count)
        {
            Start = start;
            Count = count;
        }

        public readonly int Start;
        public readonly int Count;
        public int End => Start + Count;
    }

    /// Pure viewport and pointer queries shared by the main-panel renderer and
    /// its input path. Bounds use the scroll view's content coordinates.
    public static class PanelViewport
    {
        public static bool IntersectsVertically(
            in RectF rect, float viewportTop, float viewportBottom)
            => rect.Y < viewportBottom && rect.Y + rect.H > viewportTop;

        public static PanelBandRange VisibleBands(
            List<RenderBand> bands, float viewportTop, float viewportBottom)
        {
            int low = 0;
            int high = bands.Count;
            while (low < high)
            {
                int middle = low + ((high - low) >> 1);
                RectF rect = bands[middle].Rect;
                if (rect.Y + rect.H <= viewportTop) low = middle + 1;
                else high = middle;
            }

            int start = low;
            while (low < bands.Count && bands[low].Rect.Y < viewportBottom)
                low++;
            return new PanelBandRange(start, low - start);
        }

        public static int BandAt(
            List<RenderBand> bands,
            float x,
            float y,
            float viewportTop,
            float viewportBottom)
        {
            PanelBandRange visible = VisibleBands(
                bands, viewportTop, viewportBottom);
            for (int i = visible.Start; i < visible.End; i++)
            {
                RectF rect = bands[i].Rect;
                if (x >= rect.X && x < rect.X + rect.W
                    && y >= rect.Y && y < rect.Y + rect.H)
                    return i;
            }
            return -1;
        }

        public static int SlotAt(
            List<SlotHit> hits,
            float x,
            float y,
            float viewportTop,
            float viewportBottom)
            => SlotAt(hits, 0, hits.Count, x, y,
                viewportTop, viewportBottom);

        public static int SlotAt(
            List<SlotHit> hits,
            int start,
            int count,
            float x,
            float y,
            float viewportTop,
            float viewportBottom)
        {
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                RectF rect = hits[i].Rect;
                if (ContainsVisible(rect, x, y, viewportTop, viewportBottom))
                    return i;
            }
            return -1;
        }

        public static int MarkerAt(
            List<MarkerHit> hits,
            float x,
            float y,
            float viewportTop,
            float viewportBottom)
            => MarkerAt(hits, 0, hits.Count, x, y,
                viewportTop, viewportBottom);

        public static int MarkerAt(
            List<MarkerHit> hits,
            int start,
            int count,
            float x,
            float y,
            float viewportTop,
            float viewportBottom)
        {
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                RectF rect = hits[i].Rect;
                if (ContainsVisible(rect, x, y, viewportTop, viewportBottom))
                    return i;
            }
            return -1;
        }

        private static bool ContainsVisible(
            in RectF rect,
            float x,
            float y,
            float viewportTop,
            float viewportBottom)
            => y >= viewportTop
               && y < viewportBottom
               && IntersectsVertically(rect, viewportTop, viewportBottom)
               && x >= rect.X
               && x < rect.X + rect.W
               && y >= rect.Y
               && y < rect.Y + rect.H;
    }
}
