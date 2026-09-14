using EPrimeReadouts.Core;
using UnityEngine;

namespace EPrimeReadouts.UI
{
    /// One strongest-coverage pixel selected during the existing publication
    /// conversion. A deliberately empty surface contributes a clear pixel.
    internal readonly struct PanelTextureSample
    {
        internal PanelTextureSample(int index, int width, int height, PixelRgba pixel)
        {
            Uv = new Rect((index % width) / (float)width,
                (index / width) / (float)height, 1f / width, 1f / height);
            Expected = new Color32(
                (byte)((pixel.R * pixel.A + 127) / 255),
                (byte)((pixel.G * pixel.A + 127) / 255),
                (byte)((pixel.B * pixel.A + 127) / 255), pixel.A);
        }

        internal Rect Uv { get; }
        // Sprites/Default presents straight-alpha input as premultiplied
        // source-over. The check draws onto clear, so this is its output.
        internal Color32 Expected { get; }
    }
}
