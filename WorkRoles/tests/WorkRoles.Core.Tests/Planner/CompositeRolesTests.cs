namespace WorkRoles.Core.Tests.Planner;

public class CompositeRolesTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);

    private static Func<int, CompositeMemberFacts> Facts(params (int id, bool composite, bool hasRules)[] roles) =>
        id =>
        {
            foreach (var role in roles)
                if (role.id == id)
                    return new CompositeMemberFacts(true, role.composite, role.hasRules);
            return default;
        };

    [Test]
    public async Task SanitizeDropsDeadSelfDuplicateCompositeAndRuleCarryingMembers()
    {
        var facts = Facts((1, true, false), (2, false, false), (3, false, false), (4, true, false), (5, false, true));
        // 99 = deleted, 1 = the composite itself, 4 = another composite,
        // 5 = rule-carrying, 2 repeats. Order of the survivors is preserved.
        List<int> members = [3, 99, 1, 2, 4, 5, 2];
        bool changed = CompositeRoles.SanitizeMembers(members, 1, facts);
        await Assert.That(changed).IsTrue();
        await Assert.That(string.Join(",", members)).IsEqualTo("3,2");
    }

    [Test]
    public async Task SanitizeLeavesValidListUntouched()
    {
        var facts = Facts((1, true, false), (2, false, false), (3, false, false));
        List<int> members = [2, 3];
        bool changed = CompositeRoles.SanitizeMembers(members, 1, facts);
        await Assert.That(changed).IsFalse();
        await Assert.That(string.Join(",", members)).IsEqualTo("2,3");
    }

    /// The headline behavioral contract: compiling a pawn holding a composite
    /// yields exactly the order compiled for the member roles assigned
    /// independently, including a blocker member's vetoes.
    [Test]
    public async Task CompositeExpansionAppliesMemberOrderAndBlockers()
    {
        var catalog = new FakeCatalog().WithWorkType("Firefighter", "FightFires").WithWorkType("Hauling", "HaulGeneral", "HaulCorpses").WithWorkType("Cleaning", "CleanFilth");
        // Members of "Core+": Pyrophobe (blocker on Firefighter) then Core.
        IReadOnlyList<JobEntry> pyrophobeEntries = [WT("Firefighter")];
        IReadOnlyList<JobEntry> coreEntries = [WT("Firefighter"), WT("Hauling"), WT("Cleaning")];
        var pyrophobe = (entries: pyrophobeEntries, enabled: true, blocker: true);
        var core = (entries: coreEntries, enabled: true, blocker: false);

        List<(IReadOnlyList<JobEntry> entries, bool blocker)> expanded = [];
        if (CompositeRoles.TryGetMemberSlice(compositeBlocker: false, pyrophobe.enabled, pyrophobe.blocker, out bool pyrophobeBlocker))
            expanded.Add((pyrophobe.entries, pyrophobeBlocker));
        if (CompositeRoles.TryGetMemberSlice(compositeBlocker: false, core.enabled, core.blocker, out bool coreBlocker))
            expanded.Add((core.entries, coreBlocker));

        var compiledComposite = JobOrderCompiler.Compile(expanded, catalog, _ => true);
        string flatComposite = string.Join(",", compiledComposite.AllInOrder);

        await Assert.That(flatComposite).IsEqualTo("HaulGeneral,HaulCorpses,CleanFilth");
        await Assert.That(compiledComposite.WorkTypePriorities.ContainsKey("Firefighter")).IsFalse();
    }

    [Test]
    public async Task BlockerCompositeVetoesEveryMemberJob()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral");
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> slices = [];
        if (CompositeRoles.TryGetMemberSlice(compositeBlocker: true, memberEnabled: true, memberBlocker: false, out bool sliceBlocker))
            slices.Add(([WT("Hauling")], sliceBlocker));
        var compiled = JobOrderCompiler.Compile(slices, catalog, _ => true);
        await Assert.That(compiled.AllInOrder.Count).IsEqualTo(0);
        // The veto still claims the job against later roles.
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> withLater = [.. slices, ([WT("Hauling")], false)];
        var compiledWithLater = JobOrderCompiler.Compile(withLater, catalog, _ => true);
        await Assert.That(compiledWithLater.AllInOrder.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DisabledMemberContributesNoSlice()
    {
        bool contributes = CompositeRoles.TryGetMemberSlice(compositeBlocker: false, memberEnabled: false, memberBlocker: false, out _);
        await Assert.That(contributes).IsFalse();
    }
}
