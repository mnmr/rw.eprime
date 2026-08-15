using System;
using UnityEngine;
using Verse;

namespace WorkRoles.UI
{
    /// Allocation-free exception boundary for the global IMGUI state owned by
    /// one WorkRoles window content pass. Scroll/group scopes remain paired at
    /// their individual Begin sites so clip-stack ownership stays explicit.
    internal readonly struct GuiStateScope : IDisposable
    {
        private readonly GameFont font;
        private readonly TextAnchor anchor;
        private readonly bool wordWrap;
        private readonly FontStyle fontStyle;
        private readonly Color color;
        private readonly Matrix4x4 matrix;
        private readonly bool enabled;

        internal GuiStateScope(bool capture)
        {
            font = Text.Font;
            anchor = Text.Anchor;
            wordWrap = Text.WordWrap;
            fontStyle = Text.CurFontStyle.fontStyle;
            color = GUI.color;
            matrix = GUI.matrix;
            enabled = GUI.enabled;
        }

        public void Dispose()
        {
            GUI.matrix = matrix;
            GUI.color = color;
            GUI.enabled = enabled;
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
            Text.CurFontStyle.fontStyle = fontStyle;
        }
    }
}
