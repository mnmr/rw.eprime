using Implanner.Core;

namespace Implanner.Core.Tests;

/// Race bodies against the human body (owner, 2026-11-01): a race whose
/// implant slots are exactly the human ones is folded under the human body,
/// a race carrying every human slot and more (Milira's wings) joins human
/// plans and keeps its own entry, and a race missing human slots (an
/// android) is a body of its own.
public class BodyKinshipTests
{
    static readonly string[] HumanSlots =
        { "Torso#0", "Brain#0", "Eye#0", "Eye#1", "Kidney#0", "Kidney#1", "Liver#0", "Shoulder#0", "Shoulder#1" };

    [Test]
    public async Task RaceBodiesAreClassifiedBySlotsAgainstTheHumanBody()
    {
        string[] copy = { "Shoulder#1", "Liver#0", "Kidney#1", "Kidney#0", "Eye#1", "Eye#0", "Brain#0", "Torso#0", "Shoulder#0" };
        var milira = new List<string>(HumanSlots) { "Milira_Wing#0", "Milira_Wing#1" };
        string[] android = { "Torso#0", "ArtificialBrain#0", "LeftSightSensor#0", "RightSightSensor#0" };
        string[] oneSlot = { "Torso#0" };

        await Assert.That(BodyKinships.Classify(HumanSlots, copy)).IsEqualTo(BodyKinship.HumanEquivalent);
        await Assert.That(BodyKinships.Classify(HumanSlots, milira)).IsEqualTo(BodyKinship.HumanSuperset);
        await Assert.That(BodyKinships.Classify(HumanSlots, android)).IsEqualTo(BodyKinship.Distinct);
        await Assert.That(BodyKinships.Classify(HumanSlots, oneSlot)).IsEqualTo(BodyKinship.Distinct);
    }

    /// Who a pinned plan takes: a plan for the human body (or for a body
    /// folded under it) takes humans, copies and supersets but no android;
    /// a Milira plan takes Milira only; a plan for any body takes everyone.
    /// Copies disappear from the body lists, supersets stay.
    [Test]
    public async Task HumanPlansAcceptTheHumanFamilyAndSupersetPlansOnlyTheirOwnBody()
    {
        const string human = "Human", copy = "RaceCopy", milira = "Milira_Body", android = "Android4Tier";

        await Assert.That(BodyKinships.Accepts(human, BodyKinship.Human, human, BodyKinship.Human)).IsTrue();
        await Assert.That(BodyKinships.Accepts(human, BodyKinship.Human, copy, BodyKinship.HumanEquivalent)).IsTrue();
        await Assert.That(BodyKinships.Accepts(human, BodyKinship.Human, milira, BodyKinship.HumanSuperset)).IsTrue();
        await Assert.That(BodyKinships.Accepts(human, BodyKinship.Human, android, BodyKinship.Distinct)).IsFalse();

        await Assert.That(BodyKinships.Accepts(copy, BodyKinship.HumanEquivalent, human, BodyKinship.Human)).IsTrue();
        await Assert.That(BodyKinships.Accepts(copy, BodyKinship.HumanEquivalent, milira, BodyKinship.HumanSuperset)).IsTrue();

        await Assert.That(BodyKinships.Accepts(milira, BodyKinship.HumanSuperset, milira, BodyKinship.HumanSuperset)).IsTrue();
        await Assert.That(BodyKinships.Accepts(milira, BodyKinship.HumanSuperset, human, BodyKinship.Human)).IsFalse();
        await Assert.That(BodyKinships.Accepts(milira, BodyKinship.HumanSuperset, copy, BodyKinship.HumanEquivalent)).IsFalse();

        await Assert.That(BodyKinships.Accepts(android, BodyKinship.Distinct, android, BodyKinship.Distinct)).IsTrue();
        await Assert.That(BodyKinships.Accepts(android, BodyKinship.Distinct, human, BodyKinship.Human)).IsFalse();
        await Assert.That(BodyKinships.Accepts("", BodyKinship.Distinct, android, BodyKinship.Distinct)).IsTrue();

        await Assert.That(BodyKinships.Offered(BodyKinship.Human)).IsTrue();
        await Assert.That(BodyKinships.Offered(BodyKinship.HumanEquivalent)).IsFalse();
        await Assert.That(BodyKinships.Offered(BodyKinship.HumanSuperset)).IsTrue();
        await Assert.That(BodyKinships.Offered(BodyKinship.Distinct)).IsTrue();
    }
}
