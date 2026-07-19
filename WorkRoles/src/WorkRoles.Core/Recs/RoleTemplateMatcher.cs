using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    public sealed class RoleTemplateCandidate
    {
        public RoleTemplateCandidate(string key, IEnumerable<string> coverage,
            string primarySkill)
        {
            Key = key;
            Coverage = new HashSet<string>(coverage);
            PrimarySkill = primarySkill;
        }

        public string Key { get; }
        public HashSet<string> Coverage { get; }
        public string PrimarySkill { get; }
    }

    public static class RoleTemplateMatcher
    {
        public static RoleTemplateCandidate Closest(
            IReadOnlyCollection<string> requestedCoverage,
            string requestedPrimarySkill,
            IEnumerable<RoleTemplateCandidate> candidates)
        {
            if (requestedCoverage == null || requestedCoverage.Count == 0) return null;
            return candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Overlap = requestedCoverage.Count(candidate.Coverage.Contains),
                })
                .Where(item => item.Overlap > 0)
                .OrderByDescending(item => item.Overlap * 10000
                    / requestedCoverage.Count)
                .ThenByDescending(item => item.Overlap * 10000
                    / System.Math.Max(1, item.Candidate.Coverage.Count))
                .ThenByDescending(item => requestedPrimarySkill != null
                    && requestedPrimarySkill == item.Candidate.PrimarySkill)
                .ThenBy(item => item.Candidate.Key, System.StringComparer.Ordinal)
                .Select(item => item.Candidate)
                .FirstOrDefault();
        }
    }
}
