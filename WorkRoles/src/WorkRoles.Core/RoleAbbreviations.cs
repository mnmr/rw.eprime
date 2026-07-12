using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
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
        public static Dictionary<int, string> Build(IReadOnlyList<(int id, string label)> roles)
        {
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var reserved = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (_, rawLabel) in roles)
            {
                string initials = InitialsOf(CleanWords(rawLabel));
                if (initials != null) reserved.Add(initials);
            }

            var result = new Dictionary<int, string>();
            foreach (var (id, rawLabel) in roles)
            {
                var words = CleanWords(rawLabel);
                string picked = null;

                // A multi-word name may claim its own reservation.
                string own = InitialsOf(words);
                if (own != null && used.Add(own))
                    picked = own;

                if (picked == null)
                    foreach (var candidate in LadderCandidates(words))
                        if (!reserved.Contains(candidate) && used.Add(candidate))
                        {
                            picked = candidate;
                            break;
                        }

                if (picked == null)
                {
                    // Numbered fallback seeds from the first usable character
                    // ('R' when the label has none).
                    char first = words.Count > 0 ? char.ToUpperInvariant(words[0][0]) : 'R';
                    for (int i = 1; picked == null; i++)
                    {
                        string candidate = $"{first}{i}";
                        if (!reserved.Contains(candidate) && used.Add(candidate))
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
            return label.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
                .Where(w => w.Length > 0)
                .ToList();
        }

        private static string InitialsOf(List<string> words) =>
            words.Count >= 2
                ? string.Concat(words.Select(w => char.ToUpperInvariant(w[0])))
                : null;

        private static IEnumerable<string> LadderCandidates(List<string> words)
        {
            if (words.Count == 0) yield break; // nothing usable: numbered fallback
            string word = words[0];
            char first = char.ToUpperInvariant(word[0]);
            if (word.Length >= 2)
            {
                yield return $"{first}{word[1]}";
                const string Vowels = "aeiouyAEIOUY";
                for (int i = 1; i < word.Length; i++)
                    if (Vowels.IndexOf(word[i]) >= 0)
                    {
                        yield return $"{first}{word[i]}";
                        break;
                    }
                yield return $"{first}{word[word.Length - 1]}";
            }
            // A bare single letter beats a numbered fallback.
            yield return first.ToString();
        }
    }
}
