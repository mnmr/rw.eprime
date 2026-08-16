using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Window-scoped drag state for palette buttons, role chips, and role-tree rows.
    /// Press → move beyond threshold = drag; release over the source without
    /// crossing the threshold = click (completed centrally in ResolveMouseUp).
    public static class RoleDrag
    {
        private const float StartDistanceSq = 36f; // 6px

        public static bool Active { get; private set; }
        public static int RoleId { get; private set; }
        /// Group-header drag (role-list group reorder); -1 when dragging a role.
        public static int GroupId { get; private set; } = -1;
        /// Pawn the chip was dragged from; null when dragging from the palette.
        public static Pawn? SourcePawn { get; private set; }

        private static bool pending;
        private static Vector2 pressPos;
        private static int pendingControlId;
        private static int pendingRoleId = -1;
        private static int pendingGroupId = -1;
        private static Pawn? pendingSource;
        private static Action? pendingClickAction;
        private static bool pendingReleaseOverSource;

        // Owner: the open Work Roles window's single pending/active drag.
        // Key: one press-to-release drag session.
        // Value: immutable detached role-chip presentation, or detached group
        // label plus its premeasured Small-font width.
        // Dependencies: the source producer snapshot at mouse-down and the
        // source group's language/Small-font measurement revision.
        // Refresh: event-driven when a new press starts; an active drag is
        // intentionally frozen until release/cancel.
        // Equality: a session never republishes, so every pass reuses the same
        // value fields and string identity.
        // Teardown: Cancel clears pending and active presentation, and is called
        // on mouse-up, window open/close, and relevant game lifecycle release.
        private static RoleChipRenderData pendingRoleGhost;
        private static RoleChipRenderData roleGhost;
        private static string? pendingGroupGhostLabel;
        private static string? groupGhostLabel;
        private static float pendingGroupGhostWidth;
        private static float groupGhostWidth;

        internal static RoleChipRenderData RoleGhost => roleGhost;
        internal static string? GroupGhostLabel => groupGhostLabel;
        internal static float GroupGhostWidth => groupGhostWidth;

        // Drop target registered by whichever row the mouse is over this frame.
        public static Pawn? HoverPawn;
        public static int HoverInsertIndex = -1;

        /// Generic drop registered by the hovered target (Roles tab role tree):
        /// invoked on mouse-up, taking precedence over the pawn drop context.
        public static Action? HoverDropAction;

        // Visual feedback: true when dragging over a pawn that already has the role.
        public static bool HoverBlocked;

        /// Register a press. controlId is the source control's IMGUI identity;
        /// release containment is confirmed by that same control in its own clip.
        internal static void OnPress(int controlId, RoleChipRenderData role,
            Pawn? source, Action? clickAction)
        {
            pending = true;
            pressPos = (Vector2)UnityEngine.Input.mousePosition;   // raw screen pixels, GUI-independent
            pendingControlId = controlId;
            pendingRoleId = role.RoleId;
            pendingGroupId = -1;
            pendingSource = source;
            pendingClickAction = clickAction;
            pendingRoleGhost = role;
            pendingGroupGhostLabel = null;
            pendingGroupGhostWidth = 0f;
        }

        /// Press on a role-list group header: short release = clickAction
        /// (collapse toggle), threshold crossed = group reorder drag.
        internal static void OnPressGroup(int controlId, int groupId,
            string ghostLabel, float ghostWidth, Action clickAction)
        {
            pending = true;
            pressPos = (Vector2)UnityEngine.Input.mousePosition;
            pendingControlId = controlId;
            pendingRoleId = -1;
            pendingGroupId = groupId;
            pendingSource = null;
            pendingClickAction = clickAction;
            pendingRoleGhost = default(RoleChipRenderData);
            pendingGroupGhostLabel = ghostLabel;
            pendingGroupGhostWidth = ghostWidth;
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

        /// Call once per OnGUI pass BEFORE drawing tab content.
        public static void Update()
        {
            pendingReleaseOverSource = false;
            if (pending && !Active
                && ((Vector2)UnityEngine.Input.mousePosition - pressPos).sqrMagnitude > StartDistanceSq)
            {
                Active = true;
                RoleId = pendingRoleId;
                GroupId = pendingGroupId;
                SourcePawn = pendingSource;
                roleGhost = pendingRoleGhost;
                groupGhostLabel = pendingGroupGhostLabel;
                groupGhostWidth = pendingGroupGhostWidth;
            }
            HoverPawn = null;
            HoverInsertIndex = -1;
            HoverBlocked = false;
            HoverDropAction = null;
        }

        /// Call once per OnGUI pass AFTER drawing tab content: resolves drops and clears
        /// presses on mouse-up. Uses rawType so it fires even if the event was consumed.
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

                if (Active && HoverDropAction != null)
                {
                    HoverDropAction();
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
                                var sourceState = AssignmentState.Enabled;
                                if (SourcePawn != null && store.pawnSets.TryGetValue(SourcePawn, out var sourceSet))
                                    sourceState = sourceSet.assignments.FirstOrDefault(a => a.roleId == RoleId)?.state
                                        ?? AssignmentState.Enabled;
                                RoleCommands.AssignRole(HoverPawn, RoleId, HoverInsertIndex);
                                if (SourcePawn != null && SourcePawn != HoverPawn)
                                    RoleCommands.RemoveRoleFromPawn(SourcePawn, RoleId);
                                if (sourceState != AssignmentState.Enabled)
                                    RoleCommands.SetAssignmentState(HoverPawn, RoleId, (int)sourceState);
                            }
                        }
                    }
                }
            }
            finally
            {
                // Never retain a pawn or callback after mouse-up, even if a
                // click/drop action throws.
                Cancel();
            }
        }

        /// Insert slot within a wrapped chip flow (mouse in the same coordinate
        /// space as the rects): fully below a line lands after its last chip;
        /// within a line, a chip's right half advances the slot. Shared by every
        /// chip-strip drop target.
        public static int ChipInsertIndex<T>(Vector2 mouse, IReadOnlyList<T> chips,
            Func<T, Rect> rectOf)
        {
            int insertIndex = 0;
            for (int i = 0; i < chips.Count; i++)
            {
                var r = rectOf(chips[i]);
                if (mouse.y > r.yMax)
                {
                    insertIndex = i + 1;
                    continue;
                }
                if (mouse.y >= r.y && mouse.x > r.x + r.width / 2f)
                    insertIndex = i + 1;
            }
            return insertIndex;
        }

        public static void Cancel()
        {
            pending = false;
            Active = false;
            pressPos = default(Vector2);
            pendingControlId = 0;
            pendingRoleId = -1;
            GroupId = -1;
            pendingGroupId = -1;
            pendingSource = null;
            pendingRoleGhost = default(RoleChipRenderData);
            pendingGroupGhostLabel = null;
            pendingGroupGhostWidth = 0f;
            pendingReleaseOverSource = false;
            RoleId = -1;
            SourcePawn = null;
            roleGhost = default(RoleChipRenderData);
            groupGhostLabel = null;
            groupGhostWidth = 0f;
            HoverPawn = null;
            HoverInsertIndex = -1;
            HoverBlocked = false;
            HoverDropAction = null;
            pendingClickAction = null;
        }
    }
}
