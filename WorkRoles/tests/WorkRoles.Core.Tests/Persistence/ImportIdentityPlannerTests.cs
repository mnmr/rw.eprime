namespace WorkRoles.Core.Tests.Persistence;

public class ImportIdentityPlannerTests
{
    [Test]
    public async Task DuplicateLabelsIntoEmptyCatalogReceiveDistinctDeterministicNames()
    {
        ImportIdentitySource[] imports = [new ImportIdentitySource("Worker", null), new ImportIdentitySource("worker", null), new ImportIdentitySource("Worker", null)];

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, []);

        await Assert.That(string.Join(",", plan.Select(item => item.ExistingIndex))).IsEqualTo("-1,-1,-1");
        await Assert.That(string.Join("|", plan.Select(item => item.DisplayLabel))).IsEqualTo("Worker|worker (2)|Worker (3)");
    }

    [Test]
    public async Task DuplicateExistingObjectsMatchOccurrenceWiseAndRemainKeptByOverwrite()
    {
        ImportIdentitySource[] imports = [new ImportIdentitySource("Worker", null), new ImportIdentitySource("Worker", null)];
        ImportIdentityExisting[] existing = [new ImportIdentityExisting("Worker", null), new ImportIdentityExisting("worker", null), new ImportIdentityExisting("Unlisted", null)];

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, existing);
        int[] kept = plan.Where(item => item.ExistingIndex >= 0).Select(item => item.ExistingIndex).ToArray();

        await Assert.That(string.Join(",", plan.Select(item => item.ExistingIndex))).IsEqualTo("0,1");
        await Assert.That(string.Join(",", kept)).IsEqualTo("0,1");
        await Assert.That(string.Join("|", plan.Select(item => item.DisplayLabel))).IsEqualTo("Worker|Worker");
    }

    [Test]
    public async Task PreferredIdentityMatchesFirstThenLabelFallbackUsesNextUnclaimedObject()
    {
        ImportIdentitySource[] imports = [new ImportIdentitySource("Renamed", "template-b"), new ImportIdentitySource("Worker", "missing-template")];
        ImportIdentityExisting[] existing = [new ImportIdentityExisting("Worker", "template-a"), new ImportIdentityExisting("Worker", "template-b")];

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, existing);

        await Assert.That(plan[0].ExistingIndex).IsEqualTo(1);
        await Assert.That(plan[1].ExistingIndex).IsEqualTo(0);
    }

    [Test]
    public async Task MatchedRenameReservesItsPlannedLabelBeforeNewRowsAreNamed()
    {
        ImportIdentitySource[] imports = [new ImportIdentitySource("Worker", "template"), new ImportIdentitySource("Worker", null)];
        ImportIdentityExisting[] existing = [new ImportIdentityExisting("Old", "template")];

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(imports, existing);

        await Assert.That(plan[0].ExistingIndex).IsEqualTo(0);
        await Assert.That(plan[0].DisplayLabel).IsEqualTo("Worker");
        await Assert.That(plan[1].ExistingIndex).IsEqualTo(-1);
        await Assert.That(plan[1].DisplayLabel).IsEqualTo("Worker (2)");
    }

    [Test]
    public async Task OverwriteDoesNotReserveTheLabelOfAnUnmatchedDoomedObject()
    {
        ImportIdentitySource[] imports = [new ImportIdentitySource("Worker", "template-a")];
        ImportIdentityExisting[] existing = [new ImportIdentityExisting("Old", "template-a"), new ImportIdentityExisting("Worker", "template-doomed")];

        IReadOnlyList<ImportIdentityDecision> merge = ImportIdentityPlanner.Plan(imports, existing);
        IReadOnlyList<ImportIdentityDecision> overwrite = ImportIdentityPlanner.Plan(imports, existing, discardUnmatchedExistingLabels: true);

        await Assert.That(merge[0].DisplayLabel).IsEqualTo("Worker (2)");
        await Assert.That(overwrite[0].DisplayLabel).IsEqualTo("Worker");
    }

    [Test]
    public async Task OverwritePlansMatchedNameSwapsWithoutTransientSuffixes()
    {
        ImportIdentitySource[] imports = [new ImportIdentitySource("Beta", "template-a"), new ImportIdentitySource("Alpha", "template-b")];
        ImportIdentityExisting[] existing = [new ImportIdentityExisting("Alpha", "template-a"), new ImportIdentityExisting("Beta", "template-b")];

        IReadOnlyList<ImportIdentityDecision> overwrite = ImportIdentityPlanner.Plan(imports, existing, discardUnmatchedExistingLabels: true);

        await Assert.That(string.Join("|", overwrite.Select(row => row.DisplayLabel))).IsEqualTo("Beta|Alpha");
    }

    [Test]
    public async Task DuplicateGroupLabelsUseTheSameDeterministicIdentityPlan()
    {
        ImportIdentitySource[] groups = [new ImportIdentitySource("Team", null), new ImportIdentitySource("Team", null)];

        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(groups, []);

        await Assert.That(string.Join("|", plan.Select(item => item.DisplayLabel))).IsEqualTo("Team|Team (2)");
    }

    [Test]
    public async Task StableReferencesSelectTheCorrectRuntimeObjectAfterDuplicateLabelsArePlanned()
    {
        RoleFileDocument document = RoleFile.Parse(
            "<WorkRoles version=\"7\"><Roles>"
                + "<Role fileId=\"role-a\" name=\"Worker\"><Jobs/></Role>"
                + "<Role fileId=\"role-b\" name=\"Worker\"><Jobs/></Role>"
                + "</Roles><RecommendationOrder>"
                + "<Role roleId=\"role-b\">Worker</Role>"
                + "<Role roleId=\"role-a\">Worker</Role>"
                + "</RecommendationOrder></WorkRoles>"
        );
        IReadOnlyList<ImportIdentityDecision> plan = ImportIdentityPlanner.Plan(document.roles.Select(role => new ImportIdentitySource(role.label, role.templateDef)).ToArray(), []);
        var runtimeByFileRole = document.roles.Select((role, index) => (role, runtimeId: 100 + index)).ToDictionary(item => item.role, item => item.runtimeId);

        int[] resolvedOrder = document
            .recommendationOrderWithIds.Select(reference => RoleFile.ResolveRole(document, reference.fileId, reference.label))
            .Select(role => runtimeByFileRole[role])
            .ToArray();

        await Assert.That(string.Join("|", plan.Select(item => item.DisplayLabel))).IsEqualTo("Worker|Worker (2)");
        await Assert.That(string.Join(",", resolvedOrder)).IsEqualTo("101,100");
    }
}
