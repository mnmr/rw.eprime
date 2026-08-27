using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class ColonistSearchQueryTests
{
    [Test]
    public async Task UnprefixedTextSearchesOnlyTheColonistName()
    {
        var target = new SearchTarget(
            name: "Briana",
            roles: ["Doctor"],
            jobs: ["Tend patients"]);

        await Assert.That(ColonistSearchQuery.Parse("doc").Matches(target))
            .IsFalse();
        await Assert.That(ColonistSearchQuery.Parse("bri").Matches(target))
            .IsTrue();
    }

    [Test]
    public async Task MixedScopedTermsMustAllMatch()
    {
        var target = new SearchTarget(
            name: "Briana",
            roles: ["Field Doctor", "Night Owl"],
            jobs: ["Tend patients", "Rescue downed pawns"]);

        await Assert.That(ColonistSearchQuery.Parse(
                "bri r:doc role:night j:tend job:rescue").Matches(target))
            .IsTrue();
        await Assert.That(ColonistSearchQuery.Parse(
                "bri r:doc j:cook").Matches(target))
            .IsFalse();
    }

    [Test]
    public async Task PrefixesAreCaseInsensitiveAndIncompletePrefixesAreIgnored()
    {
        var target = new SearchTarget(
            name: "Briana",
            roles: ["Doctor"],
            jobs: ["Tend patients"]);

        await Assert.That(ColonistSearchQuery.Parse(
                "R:DOC ROLE:do J:TEND JOB:patients r: job:").Matches(target))
            .IsTrue();
    }

    private readonly struct SearchTarget : IColonistSearchTarget
    {
        private readonly string name;
        private readonly string[] roles;
        private readonly string[] jobs;

        internal SearchTarget(string name, string[] roles, string[] jobs)
        {
            this.name = name;
            this.roles = roles;
            this.jobs = jobs;
        }

        public bool NameContains(string term) => Contains(name, term);

        public bool HasRoleContaining(string term) => Contains(roles, term);

        public bool HasJobContaining(string term) => Contains(jobs, term);

        private static bool Contains(string value, string term) =>
            value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool Contains(string[] values, string term)
        {
            for (int i = 0; i < values.Length; i++)
                if (Contains(values[i], term)) return true;
            return false;
        }
    }
}
