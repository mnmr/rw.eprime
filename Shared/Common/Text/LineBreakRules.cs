namespace RimShared.Common
{
    /// <summary>
    /// Script-aware line-break opportunities for custom flow layouts. Latin
    /// text breaks only at spaces; Han, kana, Hangul, and their punctuation
    /// may break between any two characters, subject to the kinsoku rules
    /// that keep closing punctuation off a line start and opening brackets
    /// off a line end. Scripts that need dictionary segmentation (Thai, Lao,
    /// Khmer) are not handled and wrap at spaces only.
    /// </summary>
    public static class LineBreakRules
    {
        /// <summary>ASCII space or the ideographic space U+3000.</summary>
        public static bool IsSpace(char c) => c == ' ' || c == (char)0x3000;

        /// <summary>
        /// True for characters that permit a break on either side without a
        /// space: CJK ideographs, kana, Hangul syllables, CJK punctuation,
        /// and the halfwidth/fullwidth forms. Surrogate halves are excluded
        /// so supplementary-plane pairs are never split.
        /// </summary>
        public static bool IsCharacterBreakable(char c)
        {
            int u = c;
            if (u < 0x2E80) return false;
            return u <= 0x2FDF                        // CJK and Kangxi radicals
                || (u >= 0x3000 && u <= 0x30FF)       // CJK punctuation, Hiragana, Katakana
                || (u >= 0x3100 && u <= 0x31FF)       // Bopomofo, Hangul compatibility Jamo, Katakana extensions
                || (u >= 0x3200 && u <= 0x4DBF)       // enclosed and compatibility CJK, Extension A
                || (u >= 0x4E00 && u <= 0x9FFF)       // unified ideographs
                || (u >= 0xAC00 && u <= 0xD7AF)       // Hangul syllables
                || (u >= 0xF900 && u <= 0xFAFF)       // compatibility ideographs
                || (u >= 0xFE30 && u <= 0xFE4F)       // CJK compatibility forms
                || (u >= 0xFF00 && u <= 0xFFEF);      // halfwidth and fullwidth forms
        }

        // Kinsoku shori: closing punctuation, small kana, iteration marks,
        // and the prolonged sound mark attach to the preceding character;
        // opening brackets attach to the following one. Every entry lies
        // inside the character-breakable ranges above, so the tables are
        // only consulted for glyphs that would otherwise break freely.
        private const string LineStartForbidden =
            "、。，．・：；？！゛゜ヽヾゝゞ々ー）］｝〕〉》」』】〙〗〟｠〜゠"
            + "ぁぃぅぇぉっゃゅょゎゕゖァィゥェォッャュョヮヵヶ";

        private const string LineEndForbidden = "（［｛〔〈《「『【〘〖〝｟";

        /// <summary>The character may not begin a line; it glues to the
        /// character before it.</summary>
        public static bool ForbidsLineStart(char c) =>
            LineStartForbidden.IndexOf(c) >= 0;

        /// <summary>The character may not end a line; it glues to the
        /// character after it.</summary>
        public static bool ForbidsLineEnd(char c) =>
            LineEndForbidden.IndexOf(c) >= 0;
    }
}
