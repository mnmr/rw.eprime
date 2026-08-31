using UnityEngine;
using Verse;

namespace Implanner.UI
{
    /// Window-scoped drag state for the plan editor's tier rows (the EprDrag
    /// pattern from EPrimeReadouts, trimmed to one payload kind): press →
    /// move beyond a threshold = drag; the hovered insertion point registers
    /// as the drop target each pass; release resolves into a synced move
    /// command placing the implant at an exact position inside a tier.
    internal static class PlannerDrag
    {
        private const float StartDistanceSq = 36f; // 6px

        internal static bool Active { get; private set; }
        internal static string? Payload { get; private set; }
        internal static string PayloadLabel { get; private set; } = "";

        private static bool pending;
        private static Vector2 pressPos;
        private static string? pendingPayload;
        private static string pendingLabel = "";

        private static int dropStars = -1;
        private static string dropBefore = "";

        /// Register a press on a tier row. The drag begins once the mouse
        /// moves past the threshold; a short release is a no-op.
        internal static void OnPress(string defName, string label)
        {
            pending = true;
            pressPos = (Vector2)Input.mousePosition; // raw screen pixels
            pendingPayload = defName;
            pendingLabel = label;
        }

        /// The hovered tier registers this pass's insertion point: the row
        /// the payload would land before, or empty for the tier's end.
        internal static void SetDrop(int stars, string beforeDefName)
        {
            dropStars = stars;
            dropBefore = beforeDefName ?? "";
        }

        /// Call once per OnGUI pass BEFORE drawing dialog content.
        internal static void Update()
        {
            if (pending && !Active
                && ((Vector2)Input.mousePosition - pressPos).sqrMagnitude > StartDistanceSq)
            {
                Active = true;
                Payload = pendingPayload;
                PayloadLabel = pendingLabel;
            }
            dropStars = -1;
            dropBefore = "";
        }

        /// Call once per OnGUI pass AFTER drawing dialog content: resolves
        /// the drop and clears press state on mouse-up. Uses rawType so it
        /// fires even when the event was consumed.
        internal static void ResolveMouseUp()
        {
            if (Event.current.rawType != EventType.MouseUp) return;
            try
            {
                if (Active && dropStars >= 0 && Payload != null)
                    PlannerCommands.MoveImplantRank(Payload, dropStars, dropBefore);
                // Released without a target (or without crossing the
                // threshold): cancelled silently.
            }
            finally
            {
                Cancel();
            }
        }

        internal static void Cancel()
        {
            pending = false;
            Active = false;
            pressPos = default(Vector2);
            pendingPayload = null;
            pendingLabel = "";
            Payload = null;
            PayloadLabel = "";
            dropStars = -1;
            dropBefore = "";
        }
    }
}
