using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Window-scoped drag state for palette buttons and role chips.
    /// Press → move beyond threshold = drag; release without threshold = click
    /// (completed centrally in ResolveMouseUp via rawType, which survives event consumption).
    public static class RoleDrag
    {
        private const float StartDistanceSq = 36f; // 6px

        public static bool Active { get; private set; }
        public static int RoleId { get; private set; }
        /// Pawn the chip was dragged from; null when dragging from the palette.
        public static Pawn SourcePawn { get; private set; }

        private static bool pending;
        private static Vector2 pressPos;
        private static int pendingRoleId;
        private static Pawn pendingSource;
        private static Action pendingClickAction;

        // Drop target registered by whichever row the mouse is over this frame.
        public static Pawn HoverPawn;
        public static int HoverInsertIndex = -1;

        // Visual feedback: true when dragging over a pawn that already has the role.
        public static bool HoverBlocked;

        /// Register a press. If the mouse is released before moving 6px the clickAction fires.
        public static void OnPress(int roleId, Pawn source, Action clickAction)
        {
            pending = true;
            pressPos = (Vector2)UnityEngine.Input.mousePosition;   // raw screen pixels, GUI-independent
            pendingRoleId = roleId;
            pendingSource = source;
            pendingClickAction = clickAction;
        }

        /// Call once per frame BEFORE drawing tab content.
        public static void Update()
        {
            if (pending && !Active
                && ((Vector2)UnityEngine.Input.mousePosition - pressPos).sqrMagnitude > StartDistanceSq)
            {
                Active = true;
                RoleId = pendingRoleId;
                SourcePawn = pendingSource;
            }
            HoverPawn = null;
            HoverInsertIndex = -1;
            HoverBlocked = false;
        }

        /// Call once per frame AFTER drawing tab content: resolves drops and clears
        /// presses on mouse-up. Uses rawType so it fires even if the event was consumed.
        public static void ResolveMouseUp()
        {
            if (Event.current.rawType != EventType.MouseUp) return;

            if (pending && !Active)
            {
                // Short press = click: invoke the registered callback.
                pendingClickAction?.Invoke();
                Cancel();
                return;
            }

            if (Active && HoverPawn != null && HoverInsertIndex >= 0)
            {
                var store = RoleStore.Current;
                if (store != null)
                {
                    if (SourcePawn == HoverPawn && store.pawnSets.TryGetValue(HoverPawn, out var set))
                    {
                        int from = set.assignments.FindIndex(a => a.roleId == RoleId);
                        if (from >= 0)
                        {
                            int to = HoverInsertIndex > from ? HoverInsertIndex - 1 : HoverInsertIndex;
                            RoleCommands.MoveRoleOnPawn(HoverPawn, from, to);
                        }
                    }
                    else
                    {
                        bool targetHasRole = store.pawnSets.TryGetValue(HoverPawn, out var targetSet)
                            && targetSet.assignments.Any(a => a.roleId == RoleId);
                        if (!targetHasRole)
                        {
                            bool wasEnabled = true;
                            if (SourcePawn != null && store.pawnSets.TryGetValue(SourcePawn, out var sourceSet))
                                wasEnabled = sourceSet.assignments.FirstOrDefault(a => a.roleId == RoleId)?.enabled ?? true;
                            RoleCommands.AssignRole(HoverPawn, RoleId, HoverInsertIndex);
                            if (SourcePawn != null && SourcePawn != HoverPawn)
                                RoleCommands.RemoveRoleFromPawn(SourcePawn, RoleId);
                            if (!wasEnabled)
                                RoleCommands.ToggleRoleForPawn(HoverPawn, RoleId);
                        }
                    }
                }
            }
            Cancel();
        }

        public static void Cancel()
        {
            pending = false;
            Active = false;
            SourcePawn = null;
            HoverPawn = null;
            HoverInsertIndex = -1;
            HoverBlocked = false;
            pendingClickAction = null;
        }
    }
}
