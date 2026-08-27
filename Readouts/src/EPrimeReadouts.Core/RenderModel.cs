using System.Collections.Generic;
using RimShared.Common;

namespace EPrimeReadouts.Core
{
    public enum CellKind { GroupBack, Triangle, Highlight, Icon, Counter, Label, EmptySlot }

    public enum CellRenderPass { Visual, Text }

    public static class RenderCellPass
    {
        public static CellRenderPass PassOf(CellKind kind)
            => kind == CellKind.Counter || kind == CellKind.Label
                ? CellRenderPass.Text
                : CellRenderPass.Visual;
    }

    public struct RenderCell
    {
        public CellKind Kind;
        public RectF Rect;
        public string? DefName;       // Icon, Counter, Highlight
        public string? Text;          // Counter (formatted count) or Label (translation key)
        public Band Band;             // Counter tint
        public TriangleState Triangle;
        public string? Token;         // Icon and Counter: the raw slot token (null elsewhere)
        public int GroupIndex;        // GroupBack: display position (0-based); -1 for Results
        public int GroupId;           // GroupBack: owning group id; -1 for Results
        public int Tier;              // Editor mode: tier index for Icon, Counter, EmptySlot
        public int Slot;              // Editor mode: slot index for Icon, Counter, EmptySlot
        public int Count;             // Icon and Counter: raw (unformatted) count/sum
    }

    public struct MarkerHit
    {
        public int GroupId;
        public RectF Rect;
    }

    /// Clickable slot region in the main readout: the icon+counter cell
    /// column plus the member defNames that map selection operates on. The
    /// list is built at layout time and owned by the render model; consumers
    /// must not mutate it.
    public struct SlotHit
    {
        public string Token;
        public IReadOnlyList<string> Members;
        public RectF Rect;
        public int CellIndex;
    }

    /// One vertically ordered, non-overlapping panel section. Its ranges point
    /// into the render model's flat buffers so viewport consumers can skip
    /// complete offscreen groups without walking their cells or hit regions.
    public struct RenderBand
    {
        public int GroupId;
        public RectF Rect;
        public int CellStart;
        public int CellCount;
        public int SlotStart;
        public int SlotCount;
        public int MarkerStart;
        public int MarkerCount;
    }

    /// Complete draw plan for one panel width + state. The game assembly
    /// resolves DefNames to ThingDefs once and then only blits.
    ///
    /// Immutability is by specification, not type: after publication the ONLY
    /// permitted mutation is ReadoutLayoutEngine.TryRefreshCounts updating
    /// count-derived cell payloads (Count, Text, Band) on the main thread —
    /// geometry, structure, hit lists, and band records never change on a
    /// published model. Consumers keying caches on model identity treat that
    /// count refresh as a separate published domain (the text content
    /// revision), never as a base-geometry change.
    public sealed class RenderModel
    {
        public List<RenderCell> Cells = new List<RenderCell>();
        public List<MarkerHit> MarkerHits = new List<MarkerHit>();
        public List<SlotHit> SlotHits = new List<SlotHit>();
        public List<RenderBand> Bands = new List<RenderBand>();
        public float TotalHeight;
        public float TotalWidth;
    }
}
