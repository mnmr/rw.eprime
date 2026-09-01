using System;
using System.Collections.Generic;
using Implanner.Core;

namespace Implanner.Core.Tests;

/// Behavioral coverage for the plans XML codec: full round-trips through
/// Export → TryImport → ImportPlans, atomic import failure, MayRequire
/// filtering, name uniquification against an existing model, and slot
/// normalization. Save-local ids never travel; base links travel by name
/// and come back as temp ids that ImportPlans remaps.
public class PlansXmlTests
{
    // Tests run in parallel: id sources must be per-test, not static.
    int nextPlanId = 1;

    int TakePlanId() => nextPlanId++;

    /// Builds the canonical two-plan fixture: "Essentials" (BionicEye slot 0)
    /// and "Full bionics" extending it (BionicArm slots 0+1, BionicLeg slot 1).
    PlannerModel SourceModel()
    {
        var model = new PlannerModel();
        var essentials = model.CreatePlan("Essentials", TakePlanId)!;
        model.SetImplantSlot(essentials.Id, "BionicEye", 0, true);
        var full = model.CreatePlan("Full bionics", TakePlanId, essentials.Id)!;
        model.SetImplantSlot(full.Id, "BionicArm", 0, true);
        model.SetImplantSlot(full.Id, "BionicArm", 1, true);
        model.SetImplantSlot(full.Id, "BionicLeg", 1, true);
        return model;
    }

    [Test]
    public async Task FullRoundTripPreservesPlansGoalsAndBaseLink()
    {
        var source = SourceModel();

        string xml = PlansXml.Export(source.Plans);

        // Save-local ids never travel; the base link travels by name.
        await Assert.That(xml).DoesNotContain("Id=");
        await Assert.That(xml).Contains("Extends=\"Essentials\"");

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out string? error)).IsTrue();
        await Assert.That(error).IsNull();

        // Temp-id contract: plan i carries Id = i + 1; Extends resolves to
        // the base's temp id.
        await Assert.That(parsed.Count).IsEqualTo(2);
        await Assert.That(parsed[0].Id).IsEqualTo(1);
        await Assert.That(parsed[1].Id).IsEqualTo(2);
        await Assert.That(parsed[0].BasePlanId).IsEqualTo(0);
        await Assert.That(parsed[1].BasePlanId).IsEqualTo(1);

        // Apply into a fresh model whose allocator starts elsewhere, so the
        // temp ids can never accidentally line up with the real ids.
        var target = new PlannerModel();
        int planId = 100;
        var change = target.ImportPlans(parsed, () => planId++);

        await Assert.That(change).IsEqualTo(PlannerChange.Plans);
        await Assert.That(target.Plans.Count).IsEqualTo(2);
        var essentials = target.Plans[0];
        var full = target.Plans[1];
        await Assert.That(essentials.Name).IsEqualTo("Essentials");
        await Assert.That(full.Name).IsEqualTo("Full bionics");
        await Assert.That(essentials.Id).IsEqualTo(100);
        await Assert.That(full.BasePlanId).IsEqualTo(essentials.Id);

        await Assert.That(essentials.Implants.Count).IsEqualTo(1);
        await Assert.That(essentials.Implants[0].ImplantDefName).IsEqualTo("BionicEye");
        await Assert.That(essentials.Implants[0].SlotOrdinals).IsEquivalentTo(new[] { 0 });
        await Assert.That(full.Implants.Count).IsEqualTo(2);
        await Assert.That(full.Implants[0].ImplantDefName).IsEqualTo("BionicArm");
        await Assert.That(full.Implants[0].SlotOrdinals).IsEquivalentTo(new[] { 0, 1 });
        await Assert.That(full.Implants[1].ImplantDefName).IsEqualTo("BionicLeg");
        await Assert.That(full.Implants[1].SlotOrdinals).IsEquivalentTo(new[] { 1 });

        // The base link and effective goals actually work in the new model.
        await Assert.That(target.EffectiveImplants(full).Count).IsEqualTo(3);
    }

    [Test]
    public async Task SpecialXmlCharactersInNamesRoundTrip()
    {
        var model = new PlannerModel();
        model.CreatePlan("Spike's <Best> \"Plan\" & Co", TakePlanId);

        string xml = PlansXml.Export(model.Plans);

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out _)).IsTrue();
        await Assert.That(parsed[0].Name).IsEqualTo("Spike's <Best> \"Plan\" & Co");
    }

    [Test]
    public async Task ExportOmitsBaseLinksOutsideThePayloadAndThrowsOnBadNames()
    {
        var model = SourceModel();
        var derived = model.PlanById(2)!; // "Full bionics", extends plan 1

        // Export only the derived plan: its base is not in the payload, so
        // the link is silently omitted and the import carries no base.
        string xml = PlansXml.Export(new List<Plan> { derived });
        await Assert.That(xml).DoesNotContain("Extends");
        await Assert.That(PlansXml.TryImport(xml, out var parsed, out _)).IsTrue();
        await Assert.That(parsed[0].BasePlanId).IsEqualTo(0);

        // The model enforces name uniqueness, so only hand-built lists can
        // violate it — and Export refuses them.
        var duplicate = new List<Plan> { new Plan(1, "Same"), new Plan(2, "same ") };
        await Assert.That(() => PlansXml.Export(duplicate))
            .Throws<InvalidOperationException>();
        var blank = new List<Plan> { new Plan(1, "  ") };
        await Assert.That(() => PlansXml.Export(blank))
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task InvalidPayloadsFailAtomically()
    {
        var cases = new[]
        {
            "not xml at all <",
            "<WrongRoot><Plan Name=\"A\"/></WrongRoot>",
            "<ImplannerPlans><Plan Name=\"  \"/></ImplannerPlans>",
            "<ImplannerPlans><Plan Name=\"A\"/><Plan Name=\"a \"/></ImplannerPlans>",
            "<ImplannerPlans><Plan Name=\"A\" Extends=\"Missing\"/></ImplannerPlans>",
            "<ImplannerPlans><Plan Name=\"A\"><Implant Def=\"BionicArm\">"
                + "<Slot>-1</Slot></Implant></Plan></ImplannerPlans>",
            "<ImplannerPlans><Plan Name=\"A\"><Implant Def=\"BionicArm\">"
                + "<Slot>zero</Slot></Implant></Plan></ImplannerPlans>",
            null,
            "",
        };

        foreach (string? xml in cases)
        {
            bool ok = PlansXml.TryImport(xml, out var plans, out string? error);
            await Assert.That(ok).IsFalse();
            await Assert.That(plans.Count).IsEqualTo(0);
            await Assert.That(error).IsNotNull();
        }
    }

    [Test]
    public async Task ImportPlansRejectsInvalidPayloadWithoutTouchingTheModel()
    {
        var model = new PlannerModel();
        model.CreatePlan("Existing", TakePlanId);
        int planCalls = 0;

        // A blank-named plan can only come from a hand-built payload;
        // ImportPlans re-checks defensively and never half-applies.
        var invalid = new List<Plan>
        {
            new Plan(1, "Fine"),
            new Plan(2, "   "),
        };
        var change = model.ImportPlans(invalid,
            () => { planCalls++; return TakePlanId(); });

        await Assert.That(change).IsEqualTo(PlannerChange.None);
        await Assert.That(model.Plans.Count).IsEqualTo(1);
        await Assert.That(planCalls).IsEqualTo(0);

        await Assert.That(model.ImportPlans(new List<Plan>(), TakePlanId))
            .IsEqualTo(PlannerChange.None);
    }

    [Test]
    public async Task MayRequireFiltersImplantsPlansAndDegradesExtends()
    {
        // "Royalty stuff" needs an inactive mod → the whole plan disappears;
        // "Mixed" keeps its vanilla arm but loses the modded eye; its
        // Extends onto the skipped plan degrades to no base (not an error).
        string xml =
            "<ImplannerPlans>"
            + "<Plan Name=\"Royalty stuff\" MayRequire=\"ludeon.rimworld.royalty\">"
            + "<Implant Def=\"Coagulator\"><Slot>0</Slot></Implant></Plan>"
            + "<Plan Name=\"Mixed\" Extends=\"Royalty stuff\">"
            + "<Implant Def=\"BionicArm\"><Slot>0</Slot></Implant>"
            + "<Implant Def=\"ArchoEye\" MayRequire=\"some.mod\"><Slot>0</Slot></Implant>"
            + "<Implant Def=\"AnyOfEye\" MayRequireAnyOf=\"some.mod, other.mod\">"
            + "<Slot>1</Slot></Implant>"
            + "</Plan></ImplannerPlans>";
        Func<string, bool> isModActive = id => id == "other.mod";

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out string? error,
            isModActive)).IsTrue();
        await Assert.That(error).IsNull();
        await Assert.That(parsed.Count).IsEqualTo(1);
        await Assert.That(parsed[0].Name).IsEqualTo("Mixed");
        await Assert.That(parsed[0].BasePlanId).IsEqualTo(0);
        await Assert.That(parsed[0].Implants.Count).IsEqualTo(2);
        await Assert.That(parsed[0].Implants[0].ImplantDefName).IsEqualTo("BionicArm");
        // MayRequireAnyOf passes when any listed mod is active.
        await Assert.That(parsed[0].Implants[1].ImplantDefName).IsEqualTo("AnyOfEye");

        // A null predicate keeps everything.
        await Assert.That(PlansXml.TryImport(xml, out var all, out _)).IsTrue();
        await Assert.That(all.Count).IsEqualTo(2);
        await Assert.That(all[1].BasePlanId).IsEqualTo(1);
    }

    [Test]
    public async Task ExportDerivesMayRequireFromPackageIdOf()
    {
        var model = new PlannerModel();
        var plan = model.CreatePlan("Mixed", TakePlanId)!;
        model.SetImplantSlot(plan.Id, "BionicArm", 0, true);
        model.SetImplantSlot(plan.Id, "ArchoEye", 0, true);

        string xml = PlansXml.Export(model.Plans,
            def => def == "ArchoEye" ? "some.mod" : null);

        await Assert.That(xml).Contains("Def=\"ArchoEye\" MayRequire=\"some.mod\"");
        await Assert.That(xml).DoesNotContain("Def=\"BionicArm\" MayRequire");

        // Round-trip: without the mod, only the vanilla arm survives.
        await Assert.That(PlansXml.TryImport(xml, out var parsed, out _,
            _ => false)).IsTrue();
        await Assert.That(parsed[0].Implants.Count).IsEqualTo(1);
        await Assert.That(parsed[0].Implants[0].ImplantDefName).IsEqualTo("BionicArm");
    }

    [Test]
    public async Task ImportIntoExistingModelUniquifiesNamesAndKeepsExistingPlansIntact()
    {
        var model = new PlannerModel();
        var existing = model.CreatePlan("Essentials", TakePlanId)!;
        model.SetImplantSlot(existing.Id, "PowerClaw", 0, true);

        string xml = PlansXml.Export(SourceModel().Plans);
        await Assert.That(PlansXml.TryImport(xml, out var parsed, out _)).IsTrue();
        var change = model.ImportPlans(parsed, TakePlanId);

        await Assert.That(change).IsEqualTo(PlannerChange.Plans);
        await Assert.That(model.Plans.Count).IsEqualTo(3);

        // The clashing import got a suffixed name; the existing plan is untouched.
        await Assert.That(model.Plans[0].Name).IsEqualTo("Essentials");
        await Assert.That(model.Plans[0].Implants[0].ImplantDefName).IsEqualTo("PowerClaw");
        await Assert.That(model.Plans[1].Name).IsEqualTo("Essentials (2)");
        await Assert.That(model.Plans[2].Name).IsEqualTo("Full bionics");

        // The imported base link binds to the imported "Essentials (2)",
        // never to the pre-existing plan of the same original name.
        await Assert.That(model.Plans[2].BasePlanId).IsEqualTo(model.Plans[1].Id);
    }

    [Test]
    public async Task SlotOrdinalsAreDeduplicatedSortedAndEmptyImplantsDropped()
    {
        string xml =
            "<ImplannerPlans><Plan Name=\"A\">"
            + "<Implant Def=\"BionicArm\">"
            + "<Slot>2</Slot><Slot>0</Slot><Slot>2</Slot><Slot> 1 </Slot></Implant>"
            + "<Implant Def=\"BionicLeg\"/>"
            + "<Unknown/>"
            + "</Plan></ImplannerPlans>";

        await Assert.That(PlansXml.TryImport(xml, out var parsed, out _)).IsTrue();
        await Assert.That(parsed[0].Implants.Count).IsEqualTo(1);
        await Assert.That(parsed[0].Implants[0].SlotOrdinals)
            .IsEquivalentTo(new[] { 0, 1, 2 });
    }
}
