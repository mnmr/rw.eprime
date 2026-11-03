using UnityEngine;
using Verse;

namespace RimShared.UiLib
{
    /// <summary>
    /// The horizontal-resize cursor shown over a panel splitter. The arrow is
    /// drawn into a small texture at startup (no asset to ship per mod) and
    /// swapped in with Unity's cursor API; Clear restores the game's own
    /// cursor preference.
    /// </summary>
    // Owner: process (per assembly). Key: none. Value: one 24x24 readable
    // texture built in the static constructor, immutable afterwards.
    // Dependencies: none. Refresh: never. Teardown: lives for the process
    // like the game's own cursor texture; Clear only resets the active cursor.
    [StaticConstructorOnStartup]
    public static class SplitterCursor
    {
        private const int Size = 24;
        private static readonly Texture2D Arrow = Build();
        private static readonly Vector2 Hotspot = new Vector2(Size / 2f, Size / 2f);
        private static bool active;

        /// <summary>True while this cursor replaces the game's.</summary>
        public static bool Active => active;

        public static void Set(bool wanted)
        {
            if (wanted == active) return;
            active = wanted;
            if (wanted)
                Cursor.SetCursor(Arrow, Hotspot, CursorMode.Auto);
            else if (Prefs.CustomCursorEnabled)
                CustomCursor.Activate();
            else
                CustomCursor.Deactivate();
        }

        /// <summary>A white double-headed horizontal arrow with a black
        /// outline: a bar across the middle and a triangular head at each end.</summary>
        private static Texture2D Build()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: false)
            {
                name = "RimShared_SplitterCursor",
                filterMode = FilterMode.Point,
            };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    bool inside = Shape(x, y);
                    bool outline = !inside && (Shape(x - 1, y) || Shape(x + 1, y) || Shape(x, y - 1) || Shape(x, y + 1)
                        || Shape(x - 1, y - 1) || Shape(x + 1, y - 1) || Shape(x - 1, y + 1) || Shape(x + 1, y + 1));
                    // Texture rows run bottom-up; the arrow is symmetric, so no flip.
                    pixels[y * Size + x] = inside ? new Color32(255, 255, 255, 255)
                        : outline ? new Color32(0, 0, 0, 255) : new Color32(0, 0, 0, 0);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static bool Shape(int x, int y)
        {
            const int Mid = Size / 2;
            int dy = y - Mid + (y >= Mid ? 0 : 1);   // 0 on the two middle rows
            int ady = dy < 0 ? -dy : dy;
            if (x < 2 || x >= Size - 2) return false;
            if (x < 8) return ady <= 7 - x + 1;        // left head: widens toward the tip
            if (x >= Size - 8) return ady <= x - (Size - 8) + 1;   // right head
            return ady <= 1;                            // bar
        }
    }
}
