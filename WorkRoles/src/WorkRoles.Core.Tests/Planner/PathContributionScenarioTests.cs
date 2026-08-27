using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Training-path contribution rules: a path covers the skills its non-target
/// roles actually train (needed intersect trained), is valid for that
/// subset, and the target entry bands on path-covered skills only.
public class PathContributionScenarioTests
{
    [Test]
    public async Task PathTrainingASubsetOfNeededSkillsStillSubstitutes()
    {
        // Drug Maker needs Intellectual (primary), Crafting (content gate),
        // and Cooking through the dedicated Cook trainer's primary. The path
        // trains Intellectual and Cooking only; it remains a valid path for
        // that subset instead of being rejected for the untrained Crafting
        // gate.
        RoleView drugMaker = RecsTestBed.Unskilled(1, "Crafting", "MakeFlake", "MakeGoJuice", "CookDrug");
        RecsTestBed.SetSpec(drugMaker, RecsTestBed.Capability("Crafting", 0,
            RecsTestBed.Giver("MakeFlake", used: ["Intellectual"], trained: ["Intellectual"], gates: ("Crafting", 4)),
            RecsTestBed.Giver("MakeGoJuice", used: ["Intellectual"], trained: ["Intellectual"]),
            RecsTestBed.Giver("CookDrug", used: ["Cooking"], trained: ["Cooking"])));
        RoleView researcher = RecsTestBed.Unskilled(2, "Research", "ResearchWork");
        RecsTestBed.SetSpec(researcher, RecsTestBed.Capability("Research", 0,
            RecsTestBed.Giver("ResearchWork", used: ["Intellectual"], trained: ["Intellectual"])));
        RoleView cook = RecsTestBed.Unskilled(3, "Cooking", "CookMeals");
        RecsTestBed.SetSpec(cook, RecsTestBed.Capability("Cooking", 0,
            RecsTestBed.Giver("CookMeals", used: ["Cooking"], trained: ["Cooking"])));
        PathView path = RecsTestBed.Path(10, (researcher.Id, 0, 15), (cook.Id, 0, 15), (drugMaker.Id, 15, 21));

        PawnView pawn = RecsTestBed.Pawn();
        pawn.CapableWorkTypes.Add("Research");
        // Neutral keeps the trainers below their own surplus signal floors:
        // only the path's optional-target aptitude flow can hand them out.
        // Levels sit at the promotion threshold so the two covered skills
        // score the default aptitude points while staying below the target
        // band.
        pawn.SkillLevels["Intellectual"] = 10;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        pawn.SkillLevels["Cooking"] = 11;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Neutral;
        pawn.SkillLevels["Crafting"] = 2;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([drugMaker, researcher, cook], pawn);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.Contains(researcher.Id)).IsTrue();
        await Assert.That(assignments.Contains(cook.Id)).IsTrue();
        await Assert.That(assignments.Contains(drugMaker.Id)).IsFalse();
    }

    [Test]
    public async Task RoleUsingASkillWithoutXpContributesNothingToAPath()
    {
        // The target trains Cooking and Intellectual. The cook genuinely
        // trains Cooking; the phony trainer merely uses Intellectual without
        // granting XP, so it contributes nothing and is not handed out as a
        // training substitute while the real trainer is.
        RoleView target = RecsTestBed.Unskilled(1, "Cooking", "CookDrug", "ResearchDrug");
        RecsTestBed.SetSpec(target, RecsTestBed.Capability("Cooking", 0,
            RecsTestBed.Giver("CookDrug", used: ["Cooking"], trained: ["Cooking"]),
            RecsTestBed.Giver("ResearchDrug", used: ["Intellectual"], trained: ["Intellectual"])));
        RoleView cookTrainer = RecsTestBed.Unskilled(2, "Cooking", "CookMeals");
        RecsTestBed.SetSpec(cookTrainer, RecsTestBed.Capability("Cooking", 0,
            RecsTestBed.Giver("CookMeals", used: ["Cooking"], trained: ["Cooking"])));
        RoleView phonyTrainer = RecsTestBed.Unskilled(3, "Crafting", "UseOnlyWork");
        RecsTestBed.SetSpec(phonyTrainer, RecsTestBed.Capability("Crafting", 0,
            RecsTestBed.Giver("UseOnlyWork", used: ["Intellectual"])));
        PathView path = RecsTestBed.Path(10, (phonyTrainer.Id, 0, 15), (cookTrainer.Id, 0, 15), (target.Id, 15, 21));

        PawnView pawn = RecsTestBed.Pawn();
        // Strong Cooking drives the target's surplus pick; Neutral keeps the
        // phony trainer below its own surplus floor, so only the path flow
        // could hand it out, and it must not.
        pawn.SkillLevels["Cooking"] = 6;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Strong;
        pawn.SkillLevels["Intellectual"] = 5;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([target, cookTrainer, phonyTrainer], pawn);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.Contains(cookTrainer.Id)).IsTrue();
        await Assert.That(assignments.Contains(phonyTrainer.Id)).IsFalse();
    }

    [Test]
    public async Task UntrainedGateStillGatesTheTargetBand()
    {
        // The target's Intellectual content gate belongs to its qualifying
        // set whether or not the path trains it: a pawn above the Cooking
        // band but at Intellectual 0 is not target-ready, matching the
        // pre-contribution band behavior.
        RoleView target = RecsTestBed.Unskilled(1, "Cooking", "CookDrug");
        RecsTestBed.SetSpec(target, RecsTestBed.Capability("Cooking", 0,
            RecsTestBed.Giver("CookDrug", used: ["Cooking"], trained: ["Cooking"], gates: ("Intellectual", 4))));
        RoleView cookTrainer = RecsTestBed.Unskilled(2, "Cooking", "CookMeals");
        RecsTestBed.SetSpec(cookTrainer, RecsTestBed.Capability("Cooking", 0,
            RecsTestBed.Giver("CookMeals", used: ["Cooking"], trained: ["Cooking"])));
        PathView path = RecsTestBed.Path(10, (cookTrainer.Id, 0, 15), (target.Id, 15, 21));

        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = 16;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Strong;
        pawn.SkillLevels["Intellectual"] = 0;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([target, cookTrainer], pawn);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.Contains(target.Id)).IsFalse();
    }
}
