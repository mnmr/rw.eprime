using System;

namespace EPrimeReadouts.Core
{
    public readonly struct PixelRgba : IEquatable<PixelRgba>
    {
        public PixelRgba(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public bool Equals(PixelRgba other) =>
            R == other.R && G == other.G && B == other.B && A == other.A;

        public override bool Equals(object obj) =>
            obj is PixelRgba other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return R | (G << 8) | (B << 16) | (A << 24);
            }
        }
    }

    public static class Rgba32Math
    {
        public static PixelRgba Premultiply(byte r, byte g, byte b, byte a) =>
            new PixelRgba(
                MultiplyByAlpha(r, a),
                MultiplyByAlpha(g, a),
                MultiplyByAlpha(b, a),
                a);

        public static PixelRgba Unpremultiply(byte r, byte g, byte b, byte a)
        {
            if (a == 0) return new PixelRgba(0, 0, 0, 0);
            return new PixelRgba(
                DivideByAlpha(r, a),
                DivideByAlpha(g, a),
                DivideByAlpha(b, a),
                a);
        }

        /// Returns a straight-alpha pixel for straight-alpha inputs.
        public static PixelRgba SourceOver(
            PixelRgba source, PixelRgba destination)
        {
            PixelRgba sourcePremultiplied = Premultiply(
                source.R, source.G, source.B, source.A);
            PixelRgba destinationPremultiplied = Premultiply(
                destination.R, destination.G,
                destination.B, destination.A);
            int inverseSourceAlpha = 255 - source.A;
            byte r = AddClamped(sourcePremultiplied.R,
                MultiplyByAlpha(destinationPremultiplied.R,
                    inverseSourceAlpha));
            byte g = AddClamped(sourcePremultiplied.G,
                MultiplyByAlpha(destinationPremultiplied.G,
                    inverseSourceAlpha));
            byte b = AddClamped(sourcePremultiplied.B,
                MultiplyByAlpha(destinationPremultiplied.B,
                    inverseSourceAlpha));
            byte a = AddClamped(source.A,
                MultiplyByAlpha(destination.A, inverseSourceAlpha));
            return Unpremultiply(r, g, b, a);
        }

        private static byte MultiplyByAlpha(int channel, int alpha) =>
            (byte)((channel * alpha + 127) / 255);

        private static byte DivideByAlpha(int channel, int alpha)
        {
            int value = (channel * 255 + alpha / 2) / alpha;
            return (byte)(value > 255 ? 255 : value);
        }

        private static byte AddClamped(byte left, byte right)
        {
            int value = left + right;
            return (byte)(value > 255 ? 255 : value);
        }
    }
}
