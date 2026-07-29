using System;
using System.Text;

namespace WorkRoles.Core
{
    /// One compact row encoding shared by save scribing and export XML:
    /// "1,1,2,..." per series. Uncapped max bands encode as -1 (the constant's
    /// own value) so a flat uncapped row reads naturally.
    public static class HolderScaleCodec
    {
        public static string EncodeRow(int[] values)
        {
            var text = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) text.Append(',');
                text.Append(values[i]);
            }
            return text.ToString();
        }

        /// Lenient decode: missing tokens repeat the last value (a short row
        /// extends flat), garbage tokens fall back to fallback. Always returns
        /// a full band row.
        public static int[] DecodeRow(string encoded, int fallback)
        {
            var row = new int[HolderScale.Bands];
            string[] tokens = string.IsNullOrEmpty(encoded)
                ? Array.Empty<string>()
                : encoded.Split(',');
            int last = fallback;
            for (int i = 0; i < HolderScale.Bands; i++)
            {
                if (i < tokens.Length
                    && int.TryParse(tokens[i].Trim(), out int value))
                    last = value;
                row[i] = last;
            }
            return row;
        }
    }
}
