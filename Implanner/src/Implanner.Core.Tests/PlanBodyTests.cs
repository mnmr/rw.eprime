using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Body-typed plans: a plan pinned to one body validates its picks on that
/// body, may only extend a plan of the same body or an unpinned one, and
/// travels through export/import with the rule intact. Unpinned plans keep
/// today's behaviour exactly.
public class PlanBodyTests
{
    int nextPlan = 1;

    int TakePlanId() => nextPlan++;

    static PartAddress Part(int n) => new PartAddress("Part", n);

    /// A resolver that knows one odd race: the kidney (Part#0) and the
    /// liver (Part#1) exclude each other only on the "Odd" body, as the
    /// per-body facts would answer. The reference rule (body "") says no.
    static bool OddRaceResolver(string body, ImplantGoal a, PartAddress slotA,
        ImplantGoal b, PartAddress slotB) =>
        body == "Odd"
        && ((a.ImplantDefName == "ArchotechKidney" && b.ImplantDefName == "ArchotechLiver")
            || (a.ImplantDefName == "ArchotechLiver" && b.ImplantDefName == "ArchotechKidney"));

    [Test]
    public async Task APinnedPlanValidatesPicksOnItsOwnBody()
    {
        var model = new PlannerModel();
        model.SetSlotConflictResolver(OddRaceResolver);

        // Unpinned: the pair stays, as today (humans carry both).
        var any = model.CreatePlan("Any", TakePlanId)!;
        model.SetImplantSlot(any.Id, "ArchotechKidney", Part(0), true);
        model.SetImplantSlot(any.Id, "ArchotechLiver", Part(1), true);
        await Assert.That(any.IsPinned).IsFalse();
        await Assert.That(any.Implants.Count).IsEqualTo(2);

        // Pinned to the odd race: the click IS the choice on that body.
        var odd = model.CreatePlan("Odd folk", TakePlanId, 0, "Odd")!;
        model.SetImplantSlot(odd.Id, "ArchotechKidney", Part(0), true);
        model.SetImplantSlot(odd.Id, "ArchotechLiver", Part(1), true);
        await Assert.That(odd.Body).IsEqualTo("Odd");
        await Assert.That(odd.Implants.Count).IsEqualTo(1);
        await Assert.That(odd.Implants[0].ImplantDefName).IsEqualTo("ArchotechLiver");

        // A pinned child of the unpinned plan inherits on its own body: the
        // base's liver is suppressed behind the base's kidney for the odd
        // race, while a human child inherits both.
        var oddChild = model.CreatePlan("Odd child", TakePlanId, any.Id, "Odd")!;
        IReadOnlyList<ImplantGoal> oddEffective = model.EffectiveImplants(oddChild);
        await Assert.That(oddEffective.Count).IsEqualTo(1);
        await Assert.That(oddEffective[0].ImplantDefName).IsEqualTo("ArchotechKidney");
        var humanChild = model.CreatePlan("Human child", TakePlanId, any.Id, "Human")!;
        await Assert.That(model.EffectiveImplants(humanChild).Count).IsEqualTo(2);
    }

    [Test]
    public async Task OnlyCompatibleBasesMayBeExtended()
    {
        var model = new PlannerModel();
        var any = model.CreatePlan("Any", TakePlanId)!;
        var human = model.CreatePlan("Human", TakePlanId, 0, "Human")!;

        await Assert.That(model.CreatePlan("Any of any", TakePlanId, any.Id)).IsNotNull();
        await Assert.That(model.CreatePlan("Human of any", TakePlanId, any.Id, "Human")).IsNotNull();
        await Assert.That(model.CreatePlan("Human of human", TakePlanId, human.Id, "Human")).IsNotNull();
        await Assert.That(model.CreatePlan("Any of human", TakePlanId, human.Id)).IsNull();
        await Assert.That(model.CreatePlan("Android of human", TakePlanId, human.Id, "Android4Tier")).IsNull();
        await Assert.That(model.Plans.Count).IsEqualTo(5);

        // Hand-edited data: an incompatible chain detaches on cleanup, a
        // compatible one stays.
        var parsed = new List<Plan>
        {
            new Plan(1, "Base human", 0, new List<ImplantGoal>(), 0, "Human"),
            new Plan(2, "Stray any", 1, new List<ImplantGoal>()),
            new Plan(3, "Proper human", 1, new List<ImplantGoal>(), 0, "Human"),
        };
        var loaded = new PlannerModel();
        int next = 10;
        loaded.ImportPlans(parsed, () => next++);
        // Import already refuses the incompatible link; force it to prove
        // the load-time normalizer independently.
        Plan stray = loaded.Plans[1];
        await Assert.That(stray.BasePlanId).IsEqualTo(0);
        await Assert.That(loaded.Plans[2].BasePlanId).IsEqualTo(loaded.Plans[0].Id);
        typeof(Plan).GetProperty("BasePlanId")!.GetSetMethod(true)!
            .Invoke(stray, new object[] { loaded.Plans[0].Id });
        await Assert.That(loaded.CleanupMissing(_ => true)).IsEqualTo(PlannerChange.Plans);
        await Assert.That(stray.BasePlanId).IsEqualTo(0);
        await Assert.That(loaded.Plans[2].BasePlanId).IsEqualTo(loaded.Plans[0].Id);
    }

    [Test]
    public async Task BodyTravelsThroughExportAndImport()
    {
        var model = new PlannerModel();
        var any = model.CreatePlan("Essentials", TakePlanId)!;
        model.SetImplantSlot(any.Id, "BionicEye", new PartAddress("Eye", 0), true);
        var human = model.CreatePlan("Human extras", TakePlanId, any.Id, "Human")!;
        model.SetImplantSlot(human.Id, "ElbowBlade", new PartAddress("Arm", 0), true);

        string xml = PlansXml.Export(model.Plans);
        await Assert.That(xml).Contains("Body=\"Human\"");
        await Assert.That(xml).DoesNotContain("Body=\"\"");

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out string? error)).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(parsed[0].Body).IsEqualTo("");
        await Assert.That(parsed[1].Body).IsEqualTo("Human");
        await Assert.That(parsed[1].BasePlanId).IsEqualTo(1);

        // A payload whose Extends crosses bodies the wrong way loses the
        // link but keeps both plans, like an Extends to a mod-skipped plan.
        const string crossed = "<ImplannerPlans>"
            + "<Plan Name=\"Androids\" Body=\"Android4Tier\"/>"
            + "<Plan Name=\"Everyone\" Extends=\"Androids\"/>"
            + "</ImplannerPlans>";
        await Assert.That(PlansXml.TryImport(crossed, out var degraded, out error)).IsTrue();
        await Assert.That(degraded.Count).IsEqualTo(2);
        await Assert.That(degraded[1].BasePlanId).IsEqualTo(0);
    }
}
