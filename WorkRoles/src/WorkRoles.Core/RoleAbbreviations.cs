using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// Short UNIQUE abbreviations for role labels (compact chip mode). Candidate
    /// ladder: word initials (multi-word), first+second letter, first+first-vowel,
    /// first+last letter, then numbered (C1, C2, ...). Input order decides who wins
    /// a contested abbreviation, so pass roles in catalog order for stability.
    public static class RoleAbbreviations
    {
        public static Dictionary<int, string> Build(IReadOnlyList<(int id, string label)> roles)
        {
            var used = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var result = new Dictionary<int, string>();
            foreach (var (id, rawLabel) in roles)
            {
                string label = string.IsNullOrWhiteSpace(rawLabel) ? "Role" : rawLabel.Trim();
                string picked = null;
                foreach (var candidate in Candidates(label))
                    if (used.Add(candidate)) { picked = candidate; break; }
                if (picked == null)
                {
                    char first = char.ToUpperInvariant(label[0]);
                    for (int i = 1; picked == null; i++)
                        if (used.Add($"{first}{i}"))
                            picked = $"{first}{i}";
                }
                result[id] = picked;
            }
            return result;
        }

        private static IEnumerable<string> Candidates(string label)
        {
            var words = label.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                yield return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
            string word = words.Length > 0 ? words[0] : label;
            char first = char.ToUpperInvariant(word[0]);
            if (word.Length < 2) yield break;
            yield return $"{first}{char.ToLowerInvariant(word[1])}";
            const string Vowels = "aeiouyAEIOUY";
            for (int i = 1; i < word.Length; i++)
                if (Vowels.IndexOf(word[i]) >= 0)
                {
                    yield return $"{first}{char.ToLowerInvariant(word[i])}";
                    break;
                }
            yield return $"{first}{char.ToLowerInvariant(word[word.Length - 1])}";
        }
    }
}
