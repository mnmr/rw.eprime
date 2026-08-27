using System;

namespace EPrimeReadouts.Core
{
    public enum PanelHitKind
    {
        None,
        Slot,
        Marker,
    }

    /// Stable identity for the single panel hit beneath the pointer. The UI
    /// stores this across IMGUI events so a click activates only when its
    /// press and release belong to the same cached draw model and hit.
    public readonly struct PanelHitTarget : IEquatable<PanelHitTarget>
    {
        private PanelHitTarget(PanelHitKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public readonly PanelHitKind Kind;
        public readonly int Index;

        public static PanelHitTarget None =>
            new PanelHitTarget(PanelHitKind.None, -1);

        public static PanelHitTarget Slot(int index) =>
            new PanelHitTarget(PanelHitKind.Slot, index);

        public static PanelHitTarget Marker(int index) =>
            new PanelHitTarget(PanelHitKind.Marker, index);

        public bool Equals(PanelHitTarget other)
            => Kind == other.Kind && Index == other.Index;

        public override bool Equals(object obj)
            => obj is PanelHitTarget other && Equals(other);

        public override int GetHashCode()
            => ((int)Kind * 397) ^ Index;
    }

    /// Owns one left-button gesture without registering an IMGUI control for
    /// every slot and marker. Drag/release events remain owned after a press;
    /// activation happens only when the release is over the original target.
    public sealed class PanelClickTracker
    {
        private PanelHitTarget pressed = PanelHitTarget.None;

        public bool OwnsPointer { get; private set; }

        public void Press(PanelHitTarget target)
        {
            pressed = target;
            OwnsPointer = target.Kind != PanelHitKind.None;
        }

        public PanelHitTarget Release(PanelHitTarget target)
        {
            PanelHitTarget clicked = OwnsPointer && pressed.Equals(target)
                ? pressed
                : PanelHitTarget.None;
            Cancel();
            return clicked;
        }

        public void Cancel()
        {
            pressed = PanelHitTarget.None;
            OwnsPointer = false;
        }
    }

    /// Event ownership for a gesture that may cross underneath another
    /// window. Blocking prevents hit resolution and new interaction, but an
    /// already-owned drag/release must still be consumed by the panel.
    public readonly struct PanelPointerPolicy
    {
        private PanelPointerPolicy(
            bool consumeEvent, bool resolveReleaseTarget)
        {
            ConsumeEvent = consumeEvent;
            ResolveReleaseTarget = resolveReleaseTarget;
        }

        public readonly bool ConsumeEvent;
        public readonly bool ResolveReleaseTarget;

        public static PanelPointerPolicy For(
            bool ownsPointer,
            bool inputBlocked,
            bool isDrag,
            bool isRelease)
            => new PanelPointerPolicy(
                consumeEvent: ownsPointer && (isDrag || isRelease),
                resolveReleaseTarget:
                    ownsPointer && isRelease && !inputBlocked);
    }

    /// Pure event/window decision shared by the render path and its tests.
    public readonly struct PanelRenderPolicy
    {
        private PanelRenderPolicy(bool drawCells, bool allowTooltips)
        {
            DrawCells = drawCells;
            AllowTooltips = allowTooltips;
        }

        public readonly bool DrawCells;
        public readonly bool AllowTooltips;

        public static PanelRenderPolicy For(bool repaint, bool inputBlocked)
            => new PanelRenderPolicy(
                drawCells: repaint,
                allowTooltips: repaint && !inputBlocked);
    }
}
