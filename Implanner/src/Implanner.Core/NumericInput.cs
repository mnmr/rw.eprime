using System;
using System.Globalization;

namespace Implanner.Core
{
    /// The typing rules of an integer text field, mirroring the game's own
    /// numeric field: text is either refused (the buffer and value stay
    /// put), kept as a partially typed buffer (empty, or a lone minus sign
    /// on a negative field), or committed as the clamped value with the
    /// buffer normalized to that value's digits. Digits that overflow an
    /// int stay visible as typed without committing, like the game's field.
    public static class NumericInput
    {
        /// The game's field refuses text longer than this.
        public const int MaxLength = 12;

        /// Applies one edit of the field text. Returns true when the buffer
        /// (and possibly the value) changed; unchanged or refused text
        /// returns false and leaves both untouched.
        public static bool Apply(string? typed, int min, int max,
            ref int value, ref string buffer)
        {
            typed ??= "";
            if (string.Equals(typed, buffer, StringComparison.Ordinal)) return false;
            if (!IsPartiallyTyped(typed, min)) return false;
            buffer = typed;
            if (IsFullyTyped(typed)
                && int.TryParse(typed, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out int parsed))
            {
                value = parsed < min ? min : parsed > max ? max : parsed;
                buffer = value.ToString(CultureInfo.InvariantCulture);
            }
            return true;
        }

        private static bool IsPartiallyTyped(string text, int min)
        {
            if (text.Length == 0) return true;
            if (text[0] == '-' && min >= 0) return false;
            if (text.Length > 1 && text[text.Length - 1] == '-') return false;
            if (text == "00") return false;
            if (text.Length > MaxLength) return false;
            // A lone minus sign on a negative field passes here and stays a
            // buffer-only edit below (it never parses).
            return IsFullyTyped(text);
        }

        private static bool IsFullyTyped(string text)
        {
            if (text.Length == 0) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '-' && (c < '0' || c > '9')) return false;
            }
            return true;
        }
    }
}
