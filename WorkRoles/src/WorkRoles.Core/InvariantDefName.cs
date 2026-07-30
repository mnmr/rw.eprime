using System.Text;

namespace WorkRoles.Core
{
    /// Humanizes invariant defNames for display fallbacks: an optional
    /// prefix is stripped, underscores and dashes become spaces, and
    /// CamelCase words split while acronym runs (VSE) stay together.
    public static class InvariantDefName
    {
        public static string Humanize(string defName, string prefix = null)
        {
            if (string.IsNullOrWhiteSpace(defName)) return "?";
            string text = defName;
            if (!string.IsNullOrEmpty(prefix)
                && text.StartsWith(prefix, System.StringComparison.Ordinal))
                text = text.Substring(prefix.Length);
            var result = new StringBuilder(text.Length + 8);
            char previous = '\0';
            foreach (char current in text)
            {
                if (current == '_' || current == '-')
                {
                    if (result.Length > 0 && result[result.Length - 1] != ' ')
                        result.Append(' ');
                }
                else
                {
                    if (result.Length > 0 && char.IsUpper(current)
                        && (char.IsLower(previous) || char.IsDigit(previous)))
                        result.Append(' ');
                    result.Append(current);
                }
                previous = current;
            }
            return result.ToString();
        }
    }
}
