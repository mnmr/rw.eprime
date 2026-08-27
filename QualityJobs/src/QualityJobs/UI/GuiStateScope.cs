using System;
using UnityEngine;
using Verse;

namespace QualityJobs.UI
{
    /// <summary>Restores the global IMGUI/Text state changed by a draw routine.
    /// Use as: using (GuiStateScope.Capture()) { ... }
    /// (Ported from EPrimeReadouts; factory instead of a parameterless struct
    /// ctor so the type compiles at any C# LangVersion.)</summary>
    internal readonly struct GuiStateScope : IDisposable
    {
        private readonly GameFont font;
        private readonly TextAnchor anchor;
        private readonly bool wordWrap;
        private readonly Color color;

        private GuiStateScope(GameFont font, TextAnchor anchor, bool wordWrap, Color color)
        {
            this.font = font;
            this.anchor = anchor;
            this.wordWrap = wordWrap;
            this.color = color;
        }

        public static GuiStateScope Capture()
            => new GuiStateScope(Text.Font, Text.Anchor, Text.WordWrap, GUI.color);

        public void Dispose()
        {
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
            GUI.color = color;
        }
    }
}
