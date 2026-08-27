using System;
using System.Runtime.CompilerServices;

namespace EPrimeReadouts.Core
{
    /// Complete dependency key for count-independent panel pixels.
    public readonly struct PanelBaseRevision : IEquatable<PanelBaseRevision>
    {
        public PanelBaseRevision(
            RenderModel model,
            int width,
            int height,
            int uiRevision,
            int iconScaleRevision,
            int iconDataRevision,
            PanelVisualOptions visualOptions)
        {
            Model = model;
            Width = width;
            Height = height;
            UiRevision = uiRevision;
            IconScaleRevision = iconScaleRevision;
            IconDataRevision = iconDataRevision;
            VisualOptions = visualOptions;
        }

        public RenderModel Model { get; }
        public int Width { get; }
        public int Height { get; }
        public int UiRevision { get; }
        public int IconScaleRevision { get; }
        public int IconDataRevision { get; }
        public PanelVisualOptions VisualOptions { get; }

        public bool Equals(PanelBaseRevision other) =>
            ReferenceEquals(Model, other.Model)
            && Width == other.Width
            && Height == other.Height
            && UiRevision == other.UiRevision
            && IconScaleRevision == other.IconScaleRevision
            && IconDataRevision == other.IconDataRevision
            && VisualOptions.Equals(other.VisualOptions);

        public override bool Equals(object obj) =>
            obj is PanelBaseRevision other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = RuntimeHelpers.GetHashCode(Model);
                hash = hash * 397 ^ Width;
                hash = hash * 397 ^ Height;
                hash = hash * 397 ^ UiRevision;
                hash = hash * 397 ^ IconScaleRevision;
                hash = hash * 397 ^ IconDataRevision;
                return hash * 397 ^ VisualOptions.GetHashCode();
            }
        }
    }
}
