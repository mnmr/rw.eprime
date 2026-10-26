using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RimShared.Common
{
    /// Short UNIQUE (case-insensitive) abbreviations for role labels (compact
    /// chip mode). Multi-word names OWN their initials: a pre-pass reserves
    /// them, so a single-word ladder can never squat on "Hu" and push "Haul
    /// urgently" off "HU" — the single-word role falls through instead (down
    /// to a bare letter before numbering). Ladder: first+second letter,
    /// first+first-vowel, first+last, the first letter alone, then numbered
    /// (C1, C2, ...). First letters always capitalize; other picked characters
    /// keep the label's case. Input order decides remaining contests, so pass
    /// roles in catalog order for stability.
    public static class RoleAbbreviations
    {
        public static Dictionary<int, string> Build(IReadOnlyList<(int id, string label)> roles,
            CultureInfo? culture = null)
        {
            culture = culture ?? CultureInfo.InvariantCulture;
            // Explicit casing also works on Unity Mono, whose Turkish
            // case-insensitive collation can disagree with its case conversion.
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (_, rawLabel) in roles)
            {
                string? initials = InitialsOf(CleanWords(rawLabel), culture);
                if (initials != null) reserved.Add(ComparisonKey(initials, culture));
            }

            var result = new Dictionary<int, string>();
            foreach (var (id, rawLabel) in roles)
            {
                var words = CleanWords(rawLabel);
                string? picked = null;

                // A multi-word name may claim its own reservation.
                string? own = InitialsOf(words, culture);
                if (own != null && used.Add(ComparisonKey(own, culture)))
                    picked = own;

                if (picked == null)
                    foreach (var candidate in LadderCandidates(words, culture))
                        if (!reserved.Contains(ComparisonKey(candidate, culture))
                            && used.Add(ComparisonKey(candidate, culture)))
                        {
                            picked = candidate;
                            break;
                        }

                if (picked == null)
                {
                    // Numbered fallback seeds from the first usable character
                    // ('R' when the label has none).
                    string first = words.Count > 0 ? TextElements.Prefix(words[0], 1).ToUpper(culture) : "R";
                    for (int i = 1; picked == null; i++)
                    {
                        string candidate = first + i.ToString(CultureInfo.InvariantCulture);
                        if (!reserved.Contains(ComparisonKey(candidate, culture))
                            && used.Add(ComparisonKey(candidate, culture)))
                            picked = candidate;
                    }
                }
                result[id] = picked;
            }
            return result;
        }

        /// Only letters and digits ever appear in an abbreviation:
        /// "Cook (Michelin)" yields "CM", never "C(".
        private static List<string> CleanWords(string rawLabel)
        {
            string label = string.IsNullOrWhiteSpace(rawLabel) ? "Role" : rawLabel.Trim();
            var words = new List<string>();
            var word = new StringBuilder();
            for (int i = 0; i < label.Length;)
            {
                int end = TextElements.NextBoundary(label, i);
                if (char.IsWhiteSpace(label, i))
                {
                    if (word.Length > 0) { words.Add(word.ToString()); word.Clear(); }
                }
                else if (char.IsLetterOrDigit(label, i))
                    word.Append(label, i, end - i);
                i = end;
            }
            if (word.Length > 0) words.Add(word.ToString());
            return words;
        }

        private static string? InitialsOf(List<string> words, CultureInfo culture) =>
            words.Count >= 2
                ? string.Concat(words.Select(w => TextElements.Prefix(w, 1).ToUpper(culture)))
                : null;

        private static IEnumerable<string> LadderCandidates(List<string> words, CultureInfo culture)
        {
            if (words.Count == 0) yield break; // nothing usable: numbered fallback
            string word = words[0];
            int[] starts = TextElements.GetStarts(word);
            string first = TextElements.Prefix(word, 1).ToUpper(culture);
            if (starts.Length >= 2)
            {
                yield return first + ElementAt(word, starts, 1);
                const string Vowels = "aeiouyAEIOUYıİöÖüÜ";
                for (int i = 1; i < starts.Length; i++)
                    if (Vowels.IndexOf(word[starts[i]]) >= 0)
                    {
                        yield return first + ElementAt(word, starts, i);
                        break;
                    }
                yield return first + ElementAt(word, starts, starts.Length - 1);
            }
            // A bare single letter beats a numbered fallback.
            yield return first;
        }

        private static string ElementAt(string text, int[] starts, int index) =>
            text.Substring(starts[index],
                (index + 1 == starts.Length ? text.Length : starts[index + 1]) - starts[index]);

        private static string ComparisonKey(string value, CultureInfo culture) =>
            culture.Equals(CultureInfo.InvariantCulture) ? value : value.ToUpper(culture);
    }
}
