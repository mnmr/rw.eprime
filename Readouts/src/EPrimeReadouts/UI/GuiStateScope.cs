using System;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// <summary>Restores the global IMGUI/Text state changed by a draw routine.</summary>
    internal readonly struct GuiStateScope : IDisposable
    {
        private readonly GameFont font;
        private readonly TextAnchor anchor;
        private readonly bool wordWrap;
        private readonly Color color;

        public GuiStateScope()
        {
            font = Text.Font;
            anchor = Text.Anchor;
            wordWrap = Text.WordWrap;
            color = GUI.color;
        }

        public void Dispose()
        {
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
            GUI.color = color;
        }
    }
}
