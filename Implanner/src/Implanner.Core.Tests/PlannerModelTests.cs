using Implanner.Core;

namespace Implanner.Core.Tests;

/// Behavioral coverage for the authoritative model: exact change reporting,
/// no-op preservation (revisions must not advance), deterministic ids, and
/// assignment lifecycle.
public class PlannerModelTests
{
    // Tests run in parallel: the id source must be per-test, not static.
    int nextId = 1;

    PlannerModel NewModel() => new PlannerModel();

    int TakeId() => nextId++;

    [Test]
    public async Task CreatePlanAllocatesSequentialIdsAndUniqueNames()
    {
        var model = NewModel();

        var first = model.CreatePlan("Marines", TakeId);
        var second = model.CreatePlan("Marines", TakeId);

        await Assert.That(first!.Id).IsEqualTo(1);
        await Assert.That(second!.Id).IsEqualTo(2);
        await Assert.That(second.Name).IsEqualTo("Marines (2)");
    }

    [Test]
    public async Task RenameToSameNameIsNoOp()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;

        await Assert.That(model.RenamePlan(plan.Id, "Marines")).IsEqualTo(PlannerChange.None);
        await Assert.That(model.RenamePlan(plan.Id, "  Marines  ")).IsEqualTo(PlannerChange.None);
        await Assert.That(model.RenamePlan(plan.Id, "Snipers")).IsEqualTo(PlannerChange.Plans);
    }

    [Test]
    public async Task DeletePlanClearsItsAssignmentsAndReportsBothDomains()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;
        model.AssignPlan(pawnId: 42, plan.Id);

        var change = model.DeletePlan(plan.Id);

        await Assert.That(change).IsEqualTo(PlannerChange.Plans | PlannerChange.Assignments);
        await Assert.That(model.AssignedPlan(42)).IsNull();
    }

    [Test]
    public async Task DeleteUnassignedPlanReportsPlansOnly()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;

        await Assert.That(model.DeletePlan(plan.Id)).IsEqualTo(PlannerChange.Plans);
    }

    /// A goal's identity is natural — the owning plan plus the implant
    /// kind — so removing and re-adding a pick reproduces the exact same
    /// goal keys with no allocation involved.
    [Test]
    public async Task RemovingAndReAddingAPickReproducesItsIdentity()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;
        model.SetImplantSlot(plan.Id, "BionicLeg", 0, true);
        string before = GoalKeys.ImplantSlot(plan.Implants[0], 0);
        model.SetImplantSlot(plan.Id, "BionicLeg", 0, false);
        model.SetImplantSlot(plan.Id, "BionicLeg", 0, true);

        await Assert.That(GoalKeys.ImplantSlot(plan.Implants[0], 0))
            .IsEqualTo(before);
        await Assert.That(plan.Implants[0].PlanId).IsEqualTo(plan.Id);
    }

    [Test]
    public async Task ImplantSlotToggleUpsertRemoveAndNoOp()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;

        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 1, true)).IsEqualTo(PlannerChange.Plans);
        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 1, true)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 0, true)).IsEqualTo(PlannerChange.Plans);

        // Ordinals stay sorted regardless of toggle order.
        await Assert.That(plan.Implants[0].SlotOrdinals).IsEquivalentTo(new[] { 0, 1 });

        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 1, false)).IsEqualTo(PlannerChange.Plans);
        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 1, false)).IsEqualTo(PlannerChange.None);

        // Removing the last selected slot removes the goal entirely.
        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 0, false)).IsEqualTo(PlannerChange.Plans);
        await Assert.That(plan.Implants.Count).IsEqualTo(0);
        await Assert.That(model.SetImplantSlot(plan.Id, "BionicArm", 0, false)).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task RemoveImplantDropsTheWholeGoal()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;
        model.SetImplantSlot(plan.Id, "BionicLeg", 0, true);
        model.SetImplantSlot(plan.Id, "BionicLeg", 1, true);

        await Assert.That(model.RemoveImplant(plan.Id, "BionicLeg")).IsEqualTo(PlannerChange.Plans);
        await Assert.That(plan.Implants.Count).IsEqualTo(0);
        await Assert.That(model.RemoveImplant(plan.Id, "BionicLeg")).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task SurgeryConcurrencyClampsAndPreservesNoOps()
    {
        var model = NewModel();

        await Assert.That(model.SurgeryConcurrency).IsEqualTo(1);
        await Assert.That(model.SetSurgeryConcurrency(3))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.SetSurgeryConcurrency(3))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetSurgeryConcurrency(99))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.SurgeryConcurrency)
            .IsEqualTo(PlannerModel.SurgeryConcurrencyMax);
        await Assert.That(model.SetSurgeryConcurrency(0))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.SurgeryConcurrency)
            .IsEqualTo(PlannerModel.SurgeryConcurrencyMin);
    }

    [Test]
    public async Task CountHospitalizedTogglesAndPreservesNoOps()
    {
        var model = NewModel();

        await Assert.That(model.CountHospitalized).IsTrue();
        await Assert.That(model.SetCountHospitalized(true))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetCountHospitalized(false))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.CountHospitalized).IsFalse();
        await Assert.That(model.SetCountHospitalized(false))
            .IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task AssignPlanIsExplicitAndNoOpSafe()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;

        await Assert.That(model.AssignPlan(7, plan.Id)).IsEqualTo(PlannerChange.Assignments);
        await Assert.That(model.AssignPlan(7, plan.Id)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.AssignPlan(7, 0)).IsEqualTo(PlannerChange.Assignments);
        await Assert.That(model.AssignPlan(7, 0)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.AssignPlan(7, 999)).IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task PawnPriorityDefaultsToNormalAndNeverStoresDefaults()
    {
        var model = NewModel();

        await Assert.That(model.PriorityOf(7)).IsEqualTo(PlannerModel.PriorityNormal);
        await Assert.That(model.SetPawnPriority(7, PlannerModel.PriorityNormal))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetPawnPriority(7, 0)).IsEqualTo(PlannerChange.Priorities);
        await Assert.That(model.SetPawnPriority(7, 0)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetPawnPriority(7, PlannerModel.PriorityNormal))
            .IsEqualTo(PlannerChange.Priorities);
        await Assert.That(model.Priorities.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CleanupMissingDropsDeadPawnPriorities()
    {
        var model = NewModel();
        model.SetPawnPriority(1, 0);
        model.SetPawnPriority(2, 4);

        var change = model.CleanupMissing(pawnExists: id => id != 2);

        await Assert.That(change).IsEqualTo(PlannerChange.Priorities);
        await Assert.That(model.PriorityOf(1)).IsEqualTo(0);
        await Assert.That(model.PriorityOf(2)).IsEqualTo(PlannerModel.PriorityNormal);
    }

    [Test]
    public async Task CleanupMissingDropsDeadPawnsAndPlans()
    {
        var model = NewModel();
        var plan = model.CreatePlan("Marines", TakeId)!;
        model.AssignPlan(1, plan.Id);
        model.AssignPlan(2, plan.Id);
        model.AddLoadedAssignment(3, 999); // plan gone from the save

        var change = model.CleanupMissing(pawnExists: id => id != 2);

        await Assert.That(change).IsEqualTo(PlannerChange.Assignments);
        await Assert.That(model.AssignedPlan(1)).IsEqualTo(plan);
        await Assert.That(model.AssignedPlan(2)).IsNull();
        await Assert.That(model.AssignedPlan(3)).IsNull();
    }

    [Test]
    public async Task OptionsToggleWithNoOpPreservation()
    {
        var model = NewModel();

        await Assert.That(model.SetAutomationPaused(false)).IsEqualTo(PlannerChange.None);
        await Assert.That(model.SetAutomationPaused(true)).IsEqualTo(PlannerChange.Options);
    }

    /// Persisted option values outside their ranges (hand-edited saves,
    /// values from a build with other bounds) load clamped exactly like
    /// the setters clamp them; an unknown iteration strategy falls back to
    /// the default. Loaded doctor floors normalize the same way: zero
    /// stores nothing, above-max clamps.
    [Test]
    public async Task LoadedOptionsAndFloorsClampLikeTheSetters()
    {
        var model = NewModel();

        model.LoadOptions(automationPaused: false,
            iteration: (IterationStrategy)7, manualDoctorFloor: 99,
            autoDoctorFloor: true, surgeryConcurrency: 0,
            countHospitalized: true, autoProduction: true,
            productionConcurrency: 99, onlyIdleBenches: true,
            productionSkill: -3, allowIntermediaries: true);
        model.AddLoadedDoctorFloor("home", 0);
        model.AddLoadedDoctorFloor("ship", 25);

        await Assert.That(model.Iteration).IsEqualTo(IterationStrategy.ImplantTier);
        await Assert.That(model.ManualDoctorFloor).IsEqualTo(PlannerModel.DoctorFloorMax);
        await Assert.That(model.SurgeryConcurrency).IsEqualTo(PlannerModel.SurgeryConcurrencyMin);
        await Assert.That(model.ProductionConcurrency).IsEqualTo(PlannerModel.ConcurrencyMax);
        await Assert.That(model.ProductionSkill).IsEqualTo(PlannerModel.DoctorFloorMin);
        await Assert.That(model.DoctorFloors.ContainsKey("home")).IsFalse();
        await Assert.That(model.DoctorFloorOf("ship")).IsEqualTo(PlannerModel.DoctorFloorMax);
    }

    /// The synced iteration command carries the strategy as a plain int:
    /// a value outside the enum (a client on a build with other
    /// strategies) must normalize to the default exactly like the load
    /// path, never be stored raw, and never report a change when the
    /// model already holds the default.
    [Test]
    public async Task SetIterationNormalizesUnknownValuesToTheDefault()
    {
        var model = NewModel();

        await Assert.That(model.SetIteration((IterationStrategy)7))
            .IsEqualTo(PlannerChange.None);
        await Assert.That(model.Iteration).IsEqualTo(IterationStrategy.ImplantTier);

        model.SetIteration(IterationStrategy.Colonist);
        await Assert.That(model.SetIteration((IterationStrategy)(-1)))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.Iteration).IsEqualTo(IterationStrategy.ImplantTier);

        await Assert.That(model.SetIteration(IterationStrategy.Asap))
            .IsEqualTo(PlannerChange.Options);
        await Assert.That(model.Iteration).IsEqualTo(IterationStrategy.Asap);
        await Assert.That(model.SetIteration(IterationStrategy.Asap))
            .IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task RevisionsBumpOnlyReportedDomains()
    {
        var revisions = new PlannerRevisions();

        revisions.Bump(PlannerChange.Plans);

        await Assert.That(revisions.Plans).IsEqualTo(1);
        await Assert.That(revisions.Assignments).IsEqualTo(0);
        await Assert.That(revisions.Version).IsEqualTo(1);

        revisions.Bump(PlannerChange.None);

        await Assert.That(revisions.Version).IsEqualTo(1);
    }
}
