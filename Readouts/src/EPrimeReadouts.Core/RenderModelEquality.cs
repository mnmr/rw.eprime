using System;
using System.Collections.Generic;
using RimShared.Common;

namespace EPrimeReadouts.Core
{
    /// Content comparison between rebuilt render models. The publisher uses it
    /// to preserve the existing model identity when a rebuild produced equal
    /// content, so identity-keyed pixel caches (the base surface) and hit
    /// geometry survive no-op view-stamp bumps.
    public static class RenderModelEquality
    {
        public static bool ContentEquals(RenderModel? a, RenderModel? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return false;
            if (a.TotalWidth != b.TotalWidth
                || a.TotalHeight != b.TotalHeight
                || a.Cells.Count != b.Cells.Count
                || a.Bands.Count != b.Bands.Count
                || a.SlotHits.Count != b.SlotHits.Count
                || a.MarkerHits.Count != b.MarkerHits.Count)
                return false;

            List<RenderCell> aCells = a.Cells;
            List<RenderCell> bCells = b.Cells;
            for (int i = 0; i < aCells.Count; i++)
                if (!CellEquals(aCells[i], bCells[i])) return false;

            List<RenderBand> aBands = a.Bands;
            List<RenderBand> bBands = b.Bands;
            for (int i = 0; i < aBands.Count; i++)
            {
                RenderBand left = aBands[i];
                RenderBand right = bBands[i];
                if (left.GroupId != right.GroupId
                    || !RectEquals(left.Rect, right.Rect)
                    || left.CellStart != right.CellStart
                    || left.CellCount != right.CellCount
                    || left.SlotStart != right.SlotStart
                    || left.SlotCount != right.SlotCount
                    || left.MarkerStart != right.MarkerStart
                    || left.MarkerCount != right.MarkerCount)
                    return false;
            }

            List<SlotHit> aSlots = a.SlotHits;
            List<SlotHit> bSlots = b.SlotHits;
            for (int i = 0; i < aSlots.Count; i++)
            {
                SlotHit left = aSlots[i];
                SlotHit right = bSlots[i];
                if (!string.Equals(left.Token, right.Token, StringComparison.Ordinal)
                    || !RectEquals(left.Rect, right.Rect)
                    || left.CellIndex != right.CellIndex
                    || !MembersEqual(left.Members, right.Members))
                    return false;
            }

            List<MarkerHit> aMarkers = a.MarkerHits;
            List<MarkerHit> bMarkers = b.MarkerHits;
            for (int i = 0; i < aMarkers.Count; i++)
                if (aMarkers[i].GroupId != bMarkers[i].GroupId
                    || !RectEquals(aMarkers[i].Rect, bMarkers[i].Rect))
                    return false;

            return true;
        }

        private static bool CellEquals(in RenderCell left, in RenderCell right) =>
            left.Kind == right.Kind
            && RectEquals(left.Rect, right.Rect)
            && string.Equals(left.DefName, right.DefName, StringComparison.Ordinal)
            && string.Equals(left.Text, right.Text, StringComparison.Ordinal)
            && left.Band == right.Band
            && left.Triangle == right.Triangle
            && string.Equals(left.Token, right.Token, StringComparison.Ordinal)
            && left.GroupIndex == right.GroupIndex
            && left.GroupId == right.GroupId
            && left.Tier == right.Tier
            && left.Slot == right.Slot
            && left.Count == right.Count;

        private static bool RectEquals(in RectF left, in RectF right) =>
            left.X == right.X && left.Y == right.Y
            && left.W == right.W && left.H == right.H;

        private static bool MembersEqual(
            IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;
            for (int i = 0; i < left.Count; i++)
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
