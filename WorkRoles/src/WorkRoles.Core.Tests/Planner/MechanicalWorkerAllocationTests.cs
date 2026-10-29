using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class MechanicalWorkerAllocationTests
{
    [Test]
    public async Task MechanicalPlanningDoesNotMultiplyRoleOverlapAllocationsPerWorker()
    {
        const int workerCount = 30;
        ColonyView oneWorker = ColonyWithWorkers(1);
        ColonyView manyWorkers = ColonyWithWorkers(workerCount);
        // Warm both paths before measuring; the budget covers the real human
        // plan and all mechanical fix targets, excluding fixture/assertion work.
        MeasurePlanning(oneWorker);
        MeasurePlanning(manyWorkers);
        var one = MeasurePlanning(oneWorker);
        var many = MeasurePlanning(manyWorkers);

        foreach (var measured in new[] { one, many })
        {
            await Assert.That(measured.Plan.PawnCount).IsEqualTo(1);
            await Assert.That(measured.Plan.RoleCountAt(0)).IsEqualTo(1);
            await Assert.That(measured.Plan.RoleAt(0, 0)).IsEqualTo(151);
            await Assert.That(measured.Plan.PathCountAt(0)).IsEqualTo(0);
            foreach (PawnFixTarget target in measured.RobotTargets)
            {
                await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("1,151");
                await Assert.That(target.Changed).IsTrue();
            }
        }
        Console.WriteLine($"Mechanical planning allocations: one worker={one.Bytes}, {workerCount} workers={many.Bytes}");
        // Each additional worker needs linear role dictionaries and targets.
        // Rebuilding the 150 mutually overlapping role pairs for each worker
        // costs over 500 KB per context, even before its second UI context.
        await Assert.That(many.Bytes - one.Bytes).IsLessThan((workerCount - 1) * 100_000L);
    }

    private static ColonyView ColonyWithWorkers(int count)
    {
        var recs = new RecsProjection()
            .WorkType("Crafting", "Crafting", 400, "Craft")
            .WorkType("Cleaning", null, 100, "Clean");
        for (int id = 1; id <= 150; id++) recs.RoleByWorkType(id, 1, 0, "Crafting");
        recs.RoleByWorkType(151, 1, 0, "Cleaning");
        ColonyView colony = recs.Colony(new PawnView
        {
            SkillLevels = { ["Crafting"] = 12 },
            CapableWorkTypes = { "Crafting", "Cleaning" },
        });
        colony.IntegrateMechanicalWorkers = true;
        for (int index = 0; index < count; index++)
            colony.MechanicalWorkers.Add(new MechanicalWorkerView
            {
                Id = index + 1,
                ProvidesService = true,
                ExecutableGivers = { "Craft", "Clean" },
                CommittedGivers = { "Craft" },
                ActiveRoleIds = { 1 },
                Facts = new PawnView
                {
                    SkillLevels = { ["Crafting"] = 20 },
                    CapableWorkTypes = { "Crafting", "Cleaning" },
                    Existing = { new AssignmentView { RoleId = 1 } },
                },
            });
        return colony;
    }

    private static (long Bytes, RecommendationPlan Plan, PawnFixTarget[] RobotTargets) MeasurePlanning(ColonyView colony)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        RecommendationPlan plan = RecommendationPlan.Build(colony);
        var targets = new PawnFixTarget[colony.MechanicalWorkers.Count];
        for (int index = 0; index < targets.Length; index++)
            targets[index] = MechanicalDutyPlan.BuildFixTarget(colony, colony.MechanicalWorkers[index]);
        return (GC.GetAllocatedBytesForCurrentThread() - before, plan, targets);
    }
}
