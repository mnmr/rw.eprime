using System;

namespace EPrimeReadouts.Core
{
    /// Value key for the exact content glyph pixels used by the buffered
    /// panel. Header text lives on its own surface with its own revision, so
    /// typing in the search field never invalidates the content glyphs.
    public readonly struct PanelTextRevision : IEquatable<PanelTextRevision>
    {
        private PanelTextRevision(
            int contentHash,
            int textCellCount,
            int uiRevision,
            int width,
            int height)
        {
            ContentHash = contentHash;
            TextCellCount = textCellCount;
            UiRevision = uiRevision;
            Width = width;
            Height = height;
        }

        public int ContentHash { get; }
        public int TextCellCount { get; }
        public int UiRevision { get; }
        public int Width { get; }
        public int Height { get; }

        public static PanelTextRevision Create(
            RenderModel model,
            int uiRevision,
            int width,
            int height)
        {
            int hash = 17;
            int count = 0;
            for (int i = 0; i < model.Cells.Count; i++)
            {
                RenderCell cell = model.Cells[i];
                if (cell.Kind != CellKind.Counter
                    && cell.Kind != CellKind.Label) continue;
                count++;
                hash = Mix(hash, (int)cell.Kind);
                hash = Mix(hash, StableStringHash(cell.Text));
                hash = Mix(hash, cell.Count);
                hash = Mix(hash, (int)cell.Band);
                hash = Mix(hash, cell.Rect.X.GetHashCode());
                hash = Mix(hash, cell.Rect.Y.GetHashCode());
                hash = Mix(hash, cell.Rect.W.GetHashCode());
                hash = Mix(hash, cell.Rect.H.GetHashCode());
            }
            return new PanelTextRevision(
                hash, count, uiRevision, width, height);
        }

        public bool Equals(PanelTextRevision other) =>
            ContentHash == other.ContentHash
            && TextCellCount == other.TextCellCount
            && UiRevision == other.UiRevision
            && Width == other.Width
            && Height == other.Height;

        public override bool Equals(object obj) =>
            obj is PanelTextRevision other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Mix(ContentHash, TextCellCount);
                hash = Mix(hash, UiRevision);
                hash = Mix(hash, Width);
                return Mix(hash, Height);
            }
        }

        private static int StableStringHash(string? value)
        {
            if (value == null) return 0;
            unchecked
            {
                int hash = 23;
                for (int i = 0; i < value.Length; i++)
                    hash = Mix(hash, value[i]);
                return hash;
            }
        }

        private static int Mix(int hash, int value)
        {
            unchecked
            {
                return hash * 397 ^ value;
            }
        }
    }
}
