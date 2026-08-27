using RimShared.Common;
using UnityEngine;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Window-content pass gate: first statement of a DoWindowContents.
    /// ContentPassPolicy documents why skipped passes are safe to drop.
    public static class WrEvent
    {
        /// True when this pass carries nothing the mod's rect-based content
        /// consumes. rawType, not type: a MouseUp consumed elsewhere must
        /// still reach RoleDrag.ResolveMouseUp.
        public static bool SkipContentPass()
        {
            return !ContentPassPolicy.DrawsContent(
                Kind(Event.current.rawType), GUIUtility.hotControl != 0);
        }

        private static ContentPassKind Kind(EventType rawType)
        {
            switch (rawType)
            {
                case EventType.Repaint: return ContentPassKind.Repaint;
                case EventType.Layout: return ContentPassKind.Layout;
                case EventType.MouseDown: return ContentPassKind.MouseDown;
                case EventType.MouseUp: return ContentPassKind.MouseUp;
                case EventType.MouseDrag: return ContentPassKind.MouseDrag;
                case EventType.KeyDown: return ContentPassKind.KeyDown;
                case EventType.ScrollWheel: return ContentPassKind.ScrollWheel;
                case EventType.ExecuteCommand:
                case EventType.ValidateCommand: return ContentPassKind.Command;
                case EventType.ContextClick: return ContentPassKind.ContextClick;
                default: return ContentPassKind.Other;
            }
        }
    }
}
