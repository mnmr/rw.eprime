using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class CompositeRolesTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);

    private static Func<int, CompositeMemberFacts> Facts(
        params (int id, bool composite, bool hasRules)[] roles) =>
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
        var facts = Facts((1, true, false), (2, false, false), (3, false, false),
            (4, true, false), (5, false, true));
        // 99 = deleted, 1 = the composite itself, 4 = another composite,
        // 5 = rule-carrying, 2 repeats. Order of the survivors is preserved.
        var members = new List<int> { 3, 99, 1, 2, 4, 5, 2 };
        bool changed = CompositeRoles.SanitizeMembers(members, 1, facts);
        await Assert.That(changed).IsTrue();
        await Assert.That(string.Join(",", members)).IsEqualTo("3,2");
    }

    [Test]
    public async Task SanitizeLeavesValidListUntouched()
    {
        var facts = Facts((1, true, false), (2, false, false), (3, false, false));
        var members = new List<int> { 2, 3 };
        bool changed = CompositeRoles.SanitizeMembers(members, 1, facts);
        await Assert.That(changed).IsFalse();
        await Assert.That(string.Join(",", members)).IsEqualTo("2,3");
    }

    /// The headline behavioral contract: compiling a pawn holding a composite
    /// yields exactly the order compiled for the member roles assigned
    /// independently, including a blocker member's vetoes.
    [Test]
    public async Task CompositeExpansionEqualsIndependentAssignment()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Firefighter", "FightFires")
            .WithWorkType("Hauling", "HaulGeneral", "HaulCorpses")
            .WithWorkType("Cleaning", "CleanFilth");
        // Members of "Core+": Pyrophobe (blocker on Firefighter) then Core.
        var pyrophobe = (entries: (IReadOnlyList<JobEntry>)new List<JobEntry> { WT("Firefighter") },
            enabled: true, blocker: true);
        var core = (entries: (IReadOnlyList<JobEntry>)new List<JobEntry>
            { WT("Firefighter"), WT("Hauling"), WT("Cleaning") }, enabled: true, blocker: false);

        var expanded = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
        foreach (var member in new[] { pyrophobe, core })
            if (CompositeRoles.TryGetMemberSlice(compositeBlocker: false,
                    member.enabled, member.blocker, out bool sliceBlocker))
                expanded.Add((member.entries, sliceBlocker));

        var independent = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>
        {
            (pyrophobe.entries, true),
            (core.entries, false),
        };

        var compiledComposite = JobOrderCompiler.Compile(expanded, catalog, _ => true);
        var compiledIndependent = JobOrderCompiler.Compile(independent, catalog, _ => true);
        string flatComposite = string.Join(",", compiledComposite.AllInOrder);
        await Assert.That(flatComposite)
            .IsEqualTo(string.Join(",", compiledIndependent.AllInOrder));
        // Firefighting is vetoed by the blocker member; the rest rank in order.
        await Assert.That(flatComposite).IsEqualTo("HaulGeneral,HaulCorpses,CleanFilth");
        await Assert.That(compiledComposite.WorkTypePriorities.ContainsKey("Firefighter")).IsFalse();
    }

    [Test]
    public async Task BlockerCompositeVetoesEveryMemberJob()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral");
        var slices = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
        if (CompositeRoles.TryGetMemberSlice(compositeBlocker: true,
                memberEnabled: true, memberBlocker: false, out bool sliceBlocker))
            slices.Add((new List<JobEntry> { WT("Hauling") }, sliceBlocker));
        var compiled = JobOrderCompiler.Compile(slices, catalog, _ => true);
        await Assert.That(compiled.AllInOrder.Count).IsEqualTo(0);
        // The veto still claims the job against later roles.
        var withLater = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>(slices)
        {
            (new List<JobEntry> { WT("Hauling") }, false),
        };
        var compiledWithLater = JobOrderCompiler.Compile(withLater, catalog, _ => true);
        await Assert.That(compiledWithLater.AllInOrder.Count).IsEqualTo(0);
    }

    [Test]
    public async Task DisabledMemberContributesNoSlice()
    {
        bool contributes = CompositeRoles.TryGetMemberSlice(compositeBlocker: false,
            memberEnabled: false, memberBlocker: false, out _);
        await Assert.That(contributes).IsFalse();
    }
}
