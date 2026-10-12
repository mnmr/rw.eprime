using System;

namespace WorkRoles.Core
{
    /// Immutable ordered column widths shared by headers and rows. The caller's
    /// revision-gated builder transfers ownership of the supplied array and must
    /// never modify it afterward; mutable access is not published.
    public sealed class ColumnWidthSnapshot
    {
        private readonly float[] widths;

        private ColumnWidthSnapshot(float[] widths)
        {
            this.widths = widths ?? throw new ArgumentNullException(nameof(widths));
            for (int i = 0; i < widths.Length; i++) TotalWidth += widths[i];
        }

        public int Count => widths.Length;
        public float TotalWidth { get; }
        public float At(int index) => widths[index];

        public static ColumnWidthSnapshot Publish(ColumnWidthSnapshot? previous,
            float[] ownedWidths)
        {
            if (ownedWidths == null) throw new ArgumentNullException(nameof(ownedWidths));
            if (previous != null && previous.Count == ownedWidths.Length)
            {
                bool equal = true;
                for (int i = 0; i < ownedWidths.Length; i++)
                    if (previous.widths[i] != ownedWidths[i])
                    {
                        equal = false;
                        break;
                    }
                if (equal) return previous;
            }
            return new ColumnWidthSnapshot(ownedWidths);
        }
    }
}
