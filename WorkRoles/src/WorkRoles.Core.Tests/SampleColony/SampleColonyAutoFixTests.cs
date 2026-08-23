using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

/// Auto-optimize planning pinned to the sample colony: which pawns an hourly
/// automatic Fix My Colony run touches, the exact assignment order it writes,
/// and the guarantee that one application is a fixpoint — the next run must
/// flag nobody, or an hourly auto-optimize would oscillate assignments.
public class SampleColonyAutoFixTests
{
    /// Noin's stored assignments differ from the published recommendation, so
    /// the fix plan flags him and carries exactly the published target order.
    /// A second build over an identical colony reproduces every target and
    /// flag (the multiplayer determinism contract for tick-driven applies).
    [Test]
    public async Task ChangedPawnCarriesThePublishedTargetOrder()
    {
        IReadOnlyList<PawnFixTarget> targets =
            ColonyFixPlanner.Build(SampleColony.BuildColonyView());

        PawnFixTarget noin = targets[PawnIndexOf("Noin")];
        await Assert.That(noin.Changed).IsTrue()
            .Because("Noin's stored assignments differ from the plan");
        await Assert.That(Labels(noin)).IsEqualTo(
            "Core, Basics, Farmer Away, Miner, Artist, Butcher, Brewer, Hunter, Grunt");

        IReadOnlyList<PawnFixTarget> again =
            ColonyFixPlanner.Build(SampleColony.BuildColonyView());
        await Assert.That(again.Count).IsEqualTo(targets.Count);
        for (int pawnIndex = 0; pawnIndex < targets.Count; pawnIndex++)
        {
            await Assert.That(Labels(again[pawnIndex])).IsEqualTo(Labels(targets[pawnIndex]));
            await Assert.That(again[pawnIndex].Changed).IsEqualTo(targets[pawnIndex].Changed);
        }
    }

    /// Applying the colony fix once and re-planning flags nobody: pawns whose
    /// assignments already equal their recommendation are left untouched, so
    /// repeated hourly runs write nothing after the first.
    [Test]
    public async Task AppliedFixPlanIsAFixpointOnTheNextRun()
    {
        IReadOnlyList<PawnFixTarget> first =
            ColonyFixPlanner.Build(SampleColony.BuildColonyView());

        ColonyView applied = SampleColony.BuildColonyView();
        for (int pawnIndex = 0; pawnIndex < first.Count; pawnIndex++)
            ApplyTarget(applied.Pawns[pawnIndex], first[pawnIndex]);

        IReadOnlyList<PawnFixTarget> second = ColonyFixPlanner.Build(applied);
        string stillChanged = string.Join(", ", second
            .Where(target => target.Changed)
            .Select(target => SampleColony.CurrentMapPawns[target.PawnIndex].Name));
        await Assert.That(stillChanged).IsEqualTo("")
            .Because("an applied fix plan must be stable on the next hourly run");
    }

    /// Applying a target mirrors the game-side apply: recommended order wins,
    /// kept assignments preserve their enabled state and pin, added roles
    /// arrive enabled and unpinned.
    private static void ApplyTarget(PawnView pawn, PawnFixTarget target)
    {
        var next = new List<AssignmentView>(target.RoleIds.Count);
        foreach (int roleId in target.RoleIds)
        {
            AssignmentView applied = new AssignmentView { RoleId = roleId, Enabled = true };
            foreach (AssignmentView held in pawn.Existing)
                if (held.RoleId == roleId)
                {
                    applied.Enabled = held.Enabled;
                    applied.Pinned = held.Pinned;
                    break;
                }
            next.Add(applied);
        }
        pawn.Existing = next;
    }

    private static int PawnIndexOf(string name)
    {
        SamplePawn pawn = SampleColony.Pawn(name);
        return Enumerable.Range(0, SampleColony.CurrentMapPawns.Count)
            .Single(index => SampleColony.CurrentMapPawns[index] == pawn);
    }

    private static string Labels(PawnFixTarget target) =>
        string.Join(", ", target.RoleIds.Select(SampleColony.RoleLabel));
}
