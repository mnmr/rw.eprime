using System;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// Window-scoped drag state for resource token slots and group-list rows.
    /// Press → move beyond threshold = drag; release over the source without
    /// crossing the threshold = click (completed centrally in ResolveMouseUp).
    public static class EprDrag
    {
        private const float StartDistanceSq = 36f; // 6px

        public static bool Active { get; private set; }
        /// Raw slot token being dragged; null when dragging a group row.
        public static string Payload { get; private set; }
        /// Tier the token came from; -1 when sourced from the resource tree.
        public static int FromTier { get; private set; } = -1;
        /// Slot index the token came from; -1 when sourced from the resource tree.
        public static int FromSlot { get; private set; } = -1;
        /// Group id being reordered; -1 when dragging a token.
        public static int GroupId { get; private set; } = -1;

        private static bool pending;
        private static Vector2 pressPos;
        private static int pendingControlId;
        private static string pendingPayload;
        private static int pendingFromTier = -1;
        private static int pendingFromSlot = -1;
        private static int pendingGroupId = -1;
        private static Action pendingClickAction;
        private static bool pendingReleaseOverSource;

        private enum DropKind { None, Group, Token }
        private static DropKind dropKind;
        private static int dropGroupId;
        private static int dropTargetIndex;
        private static int dropToTier;
        private static int dropToSlot;
        private static bool dropBandSourced;
        private static string dropToken;
        private static int dropFromTier;
        private static int dropFromSlot;

        internal static void SetGroupDrop(int groupId, int targetIndex)
        {
            dropKind = DropKind.Group;
            dropGroupId = groupId;
            dropTargetIndex = targetIndex;
        }

        internal static void SetTokenDrop(int groupId, int toTier, int toSlot,
            bool bandSourced, string token, int fromTier, int fromSlot)
        {
            dropKind = DropKind.Token;
            dropGroupId = groupId;
            dropToTier = toTier;
            dropToSlot = toSlot;
            dropBandSourced = bandSourced;
            dropToken = token;
            dropFromTier = fromTier;
            dropFromSlot = fromSlot;
        }

        /// Register a press on a resource token slot. controlId is the IMGUI
        /// identity of the source control; release containment is confirmed by
        /// that same control via ObserveSource inside its own clip space.
        public static void OnPressToken(int controlId, string token,
            int fromTier, int fromSlot, Action clickAction)
        {
            pending = true;
            pressPos = (Vector2)UnityEngine.Input.mousePosition; // raw screen pixels
            pendingControlId = controlId;
            pendingPayload = token;
            pendingFromTier = fromTier;
            pendingFromSlot = fromSlot;
            pendingGroupId = -1;
            pendingClickAction = clickAction;
        }

        /// Register a press on a group-list row header. Short release =
        /// clickAction (selection); threshold crossed = group reorder drag.
        public static void OnPressGroup(int controlId, int groupId, Action clickAction)
        {
            pending = true;
            pressPos = (Vector2)UnityEngine.Input.mousePosition;
            pendingControlId = controlId;
            pendingPayload = null;
            pendingFromTier = -1;
            pendingFromSlot = -1;
            pendingGroupId = groupId;
            pendingClickAction = clickAction;
        }

        /// Called while the source control's GUI/scroll clip is active. This
        /// deliberately mirrors Widgets.ButtonInvisibleDraggable: the control
        /// ID identifies the original control, while Mouse.IsOver performs the
        /// release hit-test in the control's own scaled, clip-local GUI space.
        /// No raw-pixel/GUI-coordinate conversion is involved.
        public static void ObserveSource(int controlId, Rect rect)
        {
            if (!pending || Active || pendingControlId != controlId
                || Event.current.rawType != EventType.MouseUp)
                return;
            pendingReleaseOverSource = Mouse.IsOver(rect);
        }

        /// Call once per OnGUI pass BEFORE drawing dialog content.
        public static void Update()
        {
            pendingReleaseOverSource = false;
            if (pending && !Active
                && ((Vector2)UnityEngine.Input.mousePosition - pressPos).sqrMagnitude > StartDistanceSq)
            {
                Active = true;
                Payload = pendingPayload;
                FromTier = pendingFromTier;
                FromSlot = pendingFromSlot;
                GroupId = pendingGroupId;
            }
            dropKind = DropKind.None;
        }

        /// Call once per OnGUI pass AFTER drawing dialog content (including the
        /// drag ghost): resolves drops and clears presses on mouse-up.
        /// Uses rawType so it fires even when the event was consumed.
        public static void ResolveMouseUp()
        {
            if (Event.current.rawType != EventType.MouseUp) return;

            try
            {
                if (pending && !Active)
                {
                    if (pendingReleaseOverSource)
                        pendingClickAction?.Invoke();
                    return;
                }

                if (Active && dropKind != DropKind.None)
                {
                    ExecuteDrop();
                    return;
                }
                // Active but no drop target registered → drag cancelled silently.
            }
            finally
            {
                // Never retain a callback or state after mouse-up, even if an
                // action throws.
                Cancel();
            }
        }

        private static void ExecuteDrop()
        {
            if (dropKind == DropKind.Group)
            {
                ReadoutCommands.MoveGroupTo(dropGroupId, dropTargetIndex);
                return;
            }

            var group = ReadoutStore.Current?.Model.GroupById(dropGroupId);
            if (group == null) return;
            var tiers = Core.TierOps.Clone(group.Tiers);
            bool changed = dropBandSourced
                ? Core.TierOps.Move(tiers, dropFromTier, dropFromSlot, dropToTier, dropToSlot)
                : Core.TierOps.Add(tiers, dropToken, dropToTier, dropToSlot);
            if (changed)
                ReadoutCommands.SetGroupLayout(dropGroupId, Core.TierBlobCodec.Encode(tiers));
        }

        public static void Cancel()
        {
            pending = false;
            Active = false;
            pressPos = default(Vector2);
            pendingControlId = 0;
            pendingPayload = null;
            pendingFromTier = -1;
            pendingFromSlot = -1;
            pendingGroupId = -1;
            pendingClickAction = null;
            pendingReleaseOverSource = false;
            Payload = null;
            FromTier = -1;
            FromSlot = -1;
            GroupId = -1;
            dropKind = DropKind.None;
            dropGroupId = -1;
            dropTargetIndex = -1;
            dropToTier = -1;
            dropToSlot = -1;
            dropBandSourced = false;
            dropToken = null;
            dropFromTier = -1;
            dropFromSlot = -1;
        }
    }
}
