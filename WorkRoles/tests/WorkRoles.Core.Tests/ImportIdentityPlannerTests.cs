using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ImportIdentityPlannerTests
{
    [Test]
    public async Task DuplicateLabelsIntoEmptyCatalogReceiveDistinctDeterministicNames()
    {
        var imports = new[]
        {
            new ImportIdentitySource("Worker", null),
            new ImportIdentitySource("worker", null),
            new ImportIdentitySource("Worker", null),
        };

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(
            imports, Array.Empty<ImportIdentityExisting>());

        await Assert.That(plan.Select(item => item.ExistingIndex))
            .IsEquivalentTo(new[] { -1, -1, -1 });
        await Assert.That(plan.Select(item => item.DisplayLabel))
            .IsEquivalentTo(new[] { "Worker", "worker (2)", "Worker (3)" });
    }

    [Test]
    public async Task DuplicateExistingObjectsMatchOccurrenceWiseAndRemainKeptByOverwrite()
    {
        var imports = new[]
        {
            new ImportIdentitySource("Worker", null),
            new ImportIdentitySource("Worker", null),
        };
        var existing = new[]
        {
            new ImportIdentityExisting("Worker", null),
            new ImportIdentityExisting("worker", null),
            new ImportIdentityExisting("Unlisted", null),
        };

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, existing);
        int[] kept = plan.Where(item => item.ExistingIndex >= 0)
            .Select(item => item.ExistingIndex).ToArray();

        await Assert.That(plan.Select(item => item.ExistingIndex))
            .IsEquivalentTo(new[] { 0, 1 });
        await Assert.That(kept).IsEquivalentTo(new[] { 0, 1 });
        await Assert.That(plan.Select(item => item.DisplayLabel))
            .IsEquivalentTo(new[] { "Worker", "Worker" });
    }

    [Test]
    public async Task PreferredIdentityMatchesFirstThenLabelFallbackUsesNextUnclaimedObject()
    {
        var imports = new[]
        {
            new ImportIdentitySource("Renamed", "template-b"),
            new ImportIdentitySource("Worker", "missing-template"),
        };
        var existing = new[]
        {
            new ImportIdentityExisting("Worker", "template-a"),
            new ImportIdentityExisting("Worker", "template-b"),
        };

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, existing);

        await Assert.That(plan[0].ExistingIndex).IsEqualTo(1);
        await Assert.That(plan[1].ExistingIndex).IsEqualTo(0);
    }

    [Test]
    public async Task MatchedRenameReservesItsPlannedLabelBeforeNewRowsAreNamed()
    {
        var imports = new[]
        {
            new ImportIdentitySource("Worker", "template"),
            new ImportIdentitySource("Worker", null),
        };
        var existing = new[]
        {
            new ImportIdentityExisting("Old", "template"),
        };

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, existing);

        await Assert.That(plan[0].ExistingIndex).IsEqualTo(0);
        await Assert.That(plan[0].DisplayLabel).IsEqualTo("Worker");
        await Assert.That(plan[1].ExistingIndex).IsEqualTo(-1);
        await Assert.That(plan[1].DisplayLabel).IsEqualTo("Worker (2)");
    }

    [Test]
    public async Task OverwriteDoesNotReserveTheLabelOfAnUnmatchedDoomedObject()
    {
        var imports = new[]
        {
            new ImportIdentitySource("Worker", "template-a"),
        };
        var existing = new[]
        {
            new ImportIdentityExisting("Old", "template-a"),
            new ImportIdentityExisting("Worker", "template-doomed"),
        };

        IReadOnlyList<ImportIdentityDecision> merge = ImportIdentityPlanner.Plan(
            imports, existing);
        IReadOnlyList<ImportIdentityDecision> overwrite = ImportIdentityPlanner.Plan(
            imports, existing, discardUnmatchedExistingLabels: true);

        await Assert.That(merge[0].DisplayLabel).IsEqualTo("Worker (2)");
        await Assert.That(overwrite[0].DisplayLabel).IsEqualTo("Worker");
    }

    [Test]
    public async Task OverwritePlansMatchedNameSwapsWithoutTransientSuffixes()
    {
        var imports = new[]
        {
            new ImportIdentitySource("Beta", "template-a"),
            new ImportIdentitySource("Alpha", "template-b"),
        };
        var existing = new[]
        {
            new ImportIdentityExisting("Alpha", "template-a"),
            new ImportIdentityExisting("Beta", "template-b"),
        };

        IReadOnlyList<ImportIdentityDecision> overwrite = ImportIdentityPlanner.Plan(
            imports, existing, discardUnmatchedExistingLabels: true);

        await Assert.That(string.Join("|", overwrite.Select(row => row.DisplayLabel)))
            .IsEqualTo("Beta|Alpha");
    }

    [Test]
    public async Task DuplicateGroupLabelsUseTheSameDeterministicIdentityPlan()
    {
        var groups = new[]
        {
            new ImportIdentitySource("Team", null),
            new ImportIdentitySource("Team", null),
        };

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(
            groups, Array.Empty<ImportIdentityExisting>());

        await Assert.That(string.Join("|", plan.Select(item => item.DisplayLabel)))
            .IsEqualTo("Team|Team (2)");
    }

    [Test]
    public async Task StableReferencesSelectTheCorrectRuntimeObjectAfterDuplicateLabelsArePlanned()
    {
        RoleFileDocument document = RoleFile.Parse(
            "<WorkRoles version=\"7\"><Roles>" +
            "<Role fileId=\"role-a\" name=\"Worker\"><Jobs/></Role>" +
            "<Role fileId=\"role-b\" name=\"Worker\"><Jobs/></Role>" +
            "</Roles><RecommendationOrder>" +
            "<Role roleId=\"role-b\">Worker</Role>" +
            "<Role roleId=\"role-a\">Worker</Role>" +
            "</RecommendationOrder></WorkRoles>");
        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(
            document.roles.Select(role => new ImportIdentitySource(
                role.label, role.templateDef)).ToArray(),
            Array.Empty<ImportIdentityExisting>());
        var runtimeByFileRole = document.roles
            .Select((role, index) => (role, runtimeId: 100 + index))
            .ToDictionary(item => item.role, item => item.runtimeId);

        int[] resolvedOrder = document.recommendationOrderWithIds
            .Select(reference => RoleFile.ResolveRole(
                document, reference.fileId, reference.label))
            .Select(role => runtimeByFileRole[role])
            .ToArray();

        await Assert.That(string.Join("|", plan.Select(item => item.DisplayLabel)))
            .IsEqualTo("Worker|Worker (2)");
        await Assert.That(string.Join(",", resolvedOrder)).IsEqualTo("101,100");
    }
}
