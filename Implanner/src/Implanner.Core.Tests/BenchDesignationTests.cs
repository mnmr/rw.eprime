using Implanner.Core;

namespace Implanner.Core.Tests;

/// Benches the player designates for Implanner: the bookkeeping that lets a
/// release restore the bench exactly, and the rule production uses to pick
/// a bench for a new bill.
public class BenchDesignationTests
{
    /// Designating remembers which bills were ALREADY suspended, so a
    /// release leaves exactly those suspended and resumes the rest. The
    /// remembered set is normalized (blank ids dropped, duplicates merged,
    /// ordinal order) so every client and every save holds the same data.
    [Test]
    public async Task DesignationRemembersPriorSuspensionsUntilRelease()
    {
        var model = new PlannerModel();

        await Assert.That(model.DesignateBench(7, new[] { "Bill_3", "Bill_1", "Bill_3", "" }))
            .IsEqualTo(PlannerChange.Benches);
        // A second designation must not overwrite the original record:
        // by now Implanner suspended everything, so it would remember all.
        await Assert.That(model.DesignateBench(7, new[] { "Bill_1", "Bill_2", "Bill_3" }))
            .IsEqualTo(PlannerChange.None);

        await Assert.That(model.IsBenchDesignated(7)).IsTrue();
        await Assert.That(string.Join(",", model.DesignatedBenches[7]))
            .IsEqualTo("Bill_1,Bill_3");
        await Assert.That(model.WasSuspendedAtDesignation(7, "Bill_1")).IsTrue();
        await Assert.That(model.WasSuspendedAtDesignation(7, "Bill_2")).IsFalse();

        await Assert.That(model.ReleaseBench(7)).IsEqualTo(PlannerChange.Benches);
        await Assert.That(model.ReleaseBench(7)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.IsBenchDesignated(7)).IsFalse();
        await Assert.That(model.WasSuspendedAtDesignation(7, "Bill_1")).IsFalse();
    }

    /// A bench that no longer exists (deconstructed, destroyed) loses its
    /// designation; live benches keep theirs. Nothing to drop is a no-op.
    [Test]
    public async Task PruneDropsOnlyBenchesThatAreGone()
    {
        var model = new PlannerModel();
        model.DesignateBench(7, new string[0]);
        model.DesignateBench(9, new[] { "Bill_4" });

        await Assert.That(model.PruneDesignatedBenches(new HashSet<int> { 9 }))
            .IsEqualTo(PlannerChange.Benches);
        await Assert.That(model.IsBenchDesignated(7)).IsFalse();
        await Assert.That(model.WasSuspendedAtDesignation(9, "Bill_4")).IsTrue();
        await Assert.That(model.PruneDesignatedBenches(new HashSet<int> { 9 }))
            .IsEqualTo(PlannerChange.None);
    }

    /// The bench a new one-craft production bill goes to. Candidates are
    /// the benches that can work the recipe, in thing-id order. A bench
    /// holds at most two Implanner bills, so the next craft is already
    /// queued when the first completes. Designated benches come first and
    /// ignore the idle rule (their own bills are suspended, and a bill
    /// slipped in since the last pass does not disqualify them); within a
    /// group an empty bench beats a second bill on a busy one, so benches
    /// work in parallel, but a bench without Implanner bills may only
    /// start one while the colony is under its bench limit. The rest follow
    /// unless only designated benches may be used.
    [Test]
    public async Task ProductionPrefersDesignatedBenchesAndStacksTwoBills()
    {
        var model = new PlannerModel();
        var idle10 = new BenchCandidate(10, implannerBills: 0, hasOtherWork: false);
        var busy20 = new BenchCandidate(20, implannerBills: 0, hasOtherWork: true);
        var busy30 = new BenchCandidate(30, implannerBills: 0, hasOtherWork: true);
        var one40 = new BenchCandidate(40, implannerBills: 1, hasOtherWork: false);
        var full50 = new BenchCandidate(50, implannerBills: 2, hasOtherWork: false);
        var benches = new[] { idle10, busy20, busy30, one40, full50 };

        // No designations: the lowest-id idle bench.
        await Assert.That(model.ChooseProductionBench(benches, mayStartBench: true))
            .IsEqualTo(10);
        // At the bench limit only a bench already working for Implanner
        // takes a bill: its second one.
        await Assert.That(model.ChooseProductionBench(benches, mayStartBench: false))
            .IsEqualTo(40);

        // A designated bench wins over a lower-id idle bench, even with an
        // unsuspended bill of its own.
        model.DesignateBench(30, new string[0]);
        await Assert.That(model.ChooseProductionBench(benches, mayStartBench: true))
            .IsEqualTo(30);

        // A designated bench with one Implanner bill takes the second
        // before any ordinary bench is used; with two it is full.
        model.ReleaseBench(30);
        model.DesignateBench(40, new string[0]);
        model.DesignateBench(50, new string[0]);
        await Assert.That(model.ChooseProductionBench(benches, mayStartBench: true))
            .IsEqualTo(40);
        await Assert.That(model.ChooseProductionBench(
            new[] { idle10, full50 }, mayStartBench: true)).IsEqualTo(10);

        // Only designated benches: the idle bench is off limits.
        await Assert.That(model.SetOnlyDesignatedBenches(true))
            .IsEqualTo(PlannerChange.Production);
        await Assert.That(model.SetOnlyDesignatedBenches(true))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.ChooseProductionBench(
            new[] { idle10, full50 }, mayStartBench: true)).IsEqualTo(-1);

        // Without the idle rule, a busy ordinary bench qualifies.
        model.SetOnlyDesignatedBenches(false);
        model.SetOnlyIdleBenches(false);
        await Assert.That(model.ChooseProductionBench(
            new[] { busy20, idle10 }, mayStartBench: true)).IsEqualTo(20);
    }

    /// Saves carry designations and the option through the load path,
    /// normalized like the live mutators.
    [Test]
    public async Task LoadedDesignationsNormalizeLikeTheMutators()
    {
        var model = new PlannerModel();

        model.AddLoadedDesignatedBench(7, new[] { "Bill_9", "", "Bill_2", "Bill_9" });
        model.LoadOptions(automationPaused: false,
            iteration: IterationStrategy.ImplantTier, manualDoctorFloor: 0,
            autoDoctorFloor: true, surgeryConcurrency: 1,
            countHospitalized: true, autoProduction: true,
            productionConcurrency: 3, onlyIdleBenches: true,
            productionSkill: 8, allowIntermediaries: true,
            allowMultipleBladders: true, allowMultipleHygieneEnhancers: true,
            showPurchaseOnly: false, onlyDesignatedBenches: true);

        await Assert.That(string.Join(",", model.DesignatedBenches[7]))
            .IsEqualTo("Bill_2,Bill_9");
        await Assert.That(model.OnlyDesignatedBenches).IsTrue();
    }
}
