using System;
using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// Allocation-free restore scope for the global IMGUI/Text state a draw
    /// routine may change: font, anchor, word wrap, font style, color,
    /// matrix, and enabled. Use as: using (GuiStateScope.Capture()) { ... }.
    /// Scroll/group scopes stay paired at their own Begin sites so
    /// clip-stack ownership remains explicit. (Canonical shared version of
    /// the per-mod copies; new code uses this one.)
    public readonly struct GuiStateScope : IDisposable
    {
        private readonly GameFont font;
        private readonly TextAnchor anchor;
        private readonly bool wordWrap;
        private readonly FontStyle fontStyle;
        private readonly Color color;
        private readonly Matrix4x4 matrix;
        private readonly bool enabled;

        private GuiStateScope(bool capture)
        {
            font = Text.Font;
            anchor = Text.Anchor;
            wordWrap = Text.WordWrap;
            fontStyle = Text.CurFontStyle.fontStyle;
            color = GUI.color;
            matrix = GUI.matrix;
            enabled = GUI.enabled;
        }

        public static GuiStateScope Capture() => new GuiStateScope(true);

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
