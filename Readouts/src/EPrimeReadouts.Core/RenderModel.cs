using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    public enum CellKind { GroupBack, Triangle, Highlight, Icon, Counter, Label, EmptySlot }

    public struct RenderCell
    {
        public CellKind Kind;
        public RectF Rect;
        public string DefName;        // Icon, Counter, Highlight
        public string Text;           // Counter (formatted count) or Label (translation key)
        public Band Band;             // Counter tint
        public TriangleState Triangle;
        public string Token;          // Icon and Counter: the raw slot token (null elsewhere)
        public int GroupIndex;        // GroupBack: display position (0-based); -1 for Results
        public int Tier;              // Editor mode: tier index for Icon, Counter, EmptySlot
        public int Slot;              // Editor mode: slot index for Icon, Counter, EmptySlot
        public int Count;             // Icon and Counter: raw (unformatted) count/sum
    }

    public struct MarkerHit
    {
        public int GroupId;
        public RectF Rect;
    }

    /// Complete draw plan for one panel width + state. The game assembly
    /// resolves DefNames to ThingDefs once and then only blits.
    public sealed class RenderModel
    {
        public List<RenderCell> Cells = new List<RenderCell>();
        public List<MarkerHit> MarkerHits = new List<MarkerHit>();
        public float TotalHeight;
        public float TotalWidth;
    }
}
