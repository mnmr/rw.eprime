using WorkRoles.Core.Recs;
using WorkRoles.Core.Tests.Planner;

namespace WorkRoles.Core.Tests.SampleColony;

// Full native captures retain the real colony's demand, coverage, automatic
// roles, training paths and aptitude. Assertions concern the final assignments
// and the ordered jobs they compile, rather than the planner's internal stages.
public class RecommendationProtectionRegressionTests
{
    [Test]
    public async Task UnpinnedCompositeOverrideKeepsNormalEnginePlacementWhenNoProtectedBarrierExists()
    {
        var capture = new CapturedColony("1.7");
        var existing = capture.Colony.Pawns[0].Existing;
        for (int index = 0; index < existing.Count; index++)
        { AssignmentView assignment = existing[index]; assignment.Pinned = false; existing[index] = assignment; }
        RoleView owner = Bundle(capture.Colony, 43, 1);
        existing.RemoveAll(a => a.RoleId == 43 || a.RoleId == 1);
        existing.Add(new AssignmentView { RoleId = owner.Id, Enabled = true, ForceOn = true });
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        await Assert.That(capture.Colony.Pawns[0].Existing[0].RoleId).IsEqualTo(owner.Id);
        await Assert.That(capture.Colony.Pawns[0].Existing[0].Pinned).IsFalse();
    }

    [Test]
    [Arguments(false, "ordinary-blocker")]
    [Arguments(true, "ordinary-blocker")]
    [Arguments(false, "excluded-pinned-blocker")]
    [Arguments(true, "excluded-pinned-blocker")]
    [Arguments(false, "excluded-pinned-rule")]
    [Arguments(true, "excluded-pinned-rule")]
    public async Task RetainedCompositeWorkKeepsItsAuthoredSideOfASurvivingBlocker(bool blockerFirst, string protection)
    {
        var capture = PartialOwnerCapture(true, false, false);
        var existing = capture.Colony.Pawns[0].Existing;
        existing.RemoveAll(a => a.RoleId == 998 || a.RoleId == 999);
        bool rule = protection == "excluded-pinned-rule";
        bool pinned = protection != "ordinary-blocker";
        RoleView barrier = NativeGiverRole(capture.Colony, 995, 43, "FightFires", blocker: !rule);
        barrier.HasRules = rule;
        barrier.ExcludedFromRecommendations = pinned;
        AssignmentView owner = new() { RoleId = 999, Enabled = true, ForceOn = true };
        AssignmentView blocker = new() { RoleId = 995, Enabled = true, Pinned = pinned };
        existing.InsertRange(0, blockerFirst ? [blocker, owner] : [owner, blocker]);
        await Assert.That(Jobs(capture.Colony, 0).Contains("FightFires")).IsEqualTo(rule || !blockerFirst);
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        await Assert.That(Jobs(capture.Colony, 0).Contains("FightFires")).IsEqualTo(rule || !blockerFirst);
        await Assert.That(capture.Colony.Pawns[0].Existing.FindIndex(a => a.RoleId == 995)
            < capture.Colony.Pawns[0].Existing.FindIndex(a => a.RoleId == 999)).IsEqualTo(blockerFirst);
        await Assert.That(ColonyFixPlanner.Build(capture.Colony, capture.Tuning)[0].Changed).IsFalse();
    }

    [Test]
    [Arguments(false, "ordinary-blocker")]
    [Arguments(true, "ordinary-blocker")]
    [Arguments(false, "excluded-pinned-blocker")]
    [Arguments(true, "excluded-pinned-blocker")]
    [Arguments(false, "excluded-pinned-rule")]
    [Arguments(true, "excluded-pinned-rule")]
    public async Task ProjectedMemberWorkInheritsItsRemovedSourcesSideOfASurvivingBlocker(bool blockerFirst, string protection)
    {
        var capture = PartialOwnerCapture(true, true, false);
        var existing = capture.Colony.Pawns[0].Existing;
        existing.RemoveAll(a => a.RoleId == 998 || a.RoleId == 999);
        bool rule = protection == "excluded-pinned-rule";
        bool pinned = protection != "ordinary-blocker";
        RoleView barrier = NativeGiverRole(capture.Colony, 995, 1, "Flick", blocker: !rule);
        barrier.HasRules = rule;
        barrier.ExcludedFromRecommendations = pinned;
        AssignmentView source = new() { RoleId = 998, Enabled = true };
        AssignmentView blocker = new() { RoleId = 995, Enabled = true, Pinned = pinned };
        existing.InsertRange(0, blockerFirst ? [blocker, source] : [source, blocker]);
        existing.Insert(2, new AssignmentView { RoleId = 999, Enabled = false });
        await Assert.That(Jobs(capture.Colony, 0).Contains("Flick")).IsEqualTo(rule || !blockerFirst);
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 1 && a.Enabled)).IsTrue();
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 998 || a.RoleId == 997)).IsFalse();
        await Assert.That(Jobs(capture.Colony, 0).Contains("Flick")).IsEqualTo(rule || !blockerFirst);
        await Assert.That(capture.Colony.Pawns[0].Existing.FindIndex(a => a.RoleId == 995)
            < capture.Colony.Pawns[0].Existing.FindIndex(a => a.RoleId == 1)).IsEqualTo(blockerFirst);
        await Assert.That(ColonyFixPlanner.Build(capture.Colony, capture.Tuning)[0].Changed).IsFalse();
    }

    [Test]
    public async Task PartialOwnerCacheTracksTransitiveHeldCompositeOverlapThroughFinalTargets()
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = Enumerable.Range(0, 27).Select(_ => new object()).ToArray();
        ColonyView Inputs(bool active)
        {
            var capture = PartialOwnerCapture(true, true, false);
            RoleView bridge = capture.Colony.Roles.Single(r => r.Id == 998);
            bridge.MemberRoleIds = [1, 21];
            bridge.WorkSpec = RoleWorkSpecBuilder.Merge(998,
                bridge.MemberRoleIds.Select(id => capture.Colony.Roles.Single(r => r.Id == id).WorkSpec).ToArray(), []);
            RoleView source = Bundle(capture.Colony, 21, 997);
            source.Id = 996;
            source.ExcludedFromRecommendations = true;
            capture.Colony.Pawns[0].Existing.RemoveAll(a => a.RoleId == 21);
            capture.Colony.Pawns[0].Existing.Insert(2, new AssignmentView { RoleId = source.Id, Enabled = active });
            return capture.Colony;
        }
        foreach (bool active in new[] { false, true })
            await Assert.That(ColonyFixPlanner.Build(Inputs(active))[0].RoleIds.Contains(21)).IsEqualTo(active);
        RecommendationSnapshot Read(int activation, bool active) => cache.For(owner, pawns, catalog,
            0, 0, 0, RecommendationsTuningOptions.Default, (_, _) => Inputs(active), assignmentActivation: activation);
        var inactive = Read(0, false);
        var badges = inactive.Suitability;
        await Assert.That(inactive.FixTargets[0].RoleIds.Contains(21)).IsFalse();
        var activeOwner = Read(1, true);
        await Assert.That(activeOwner.FixTargets[0].RoleIds.Contains(21)).IsTrue();
        await Assert.That(ReferenceEquals(badges, activeOwner.Suitability)).IsTrue();
        await Assert.That(ReferenceEquals(activeOwner, Read(2, true))).IsTrue();
        await Assert.That(Read(3, false).FixTargets[0].RoleIds.Contains(21)).IsFalse();
    }

    private static RoleView NativeGiverRole(ColonyView colony, int id, int sourceId, string giverName, bool blocker)
    {
        RoleWorkCapabilitySpec source = colony.Roles.Single(r => r.Id == sourceId).WorkSpec.Capabilities
            .Single(c => c.Givers.Any(g => g.WorkGiverDefName == giverName));
        RoleWorkGiverSpec giver = source.Givers.Single(g => g.WorkGiverDefName == giverName);
        var role = new RoleView { Id = id, Blocker = blocker, Coverage = [giverName], OrderedCoverage = [giverName],
            WorkSpec = RoleWorkSpecBuilder.Build(id,
                [new RoleWorkCapabilitySpec(source.WorkTypeDefName, source.NaturalPriority, false, [giver])], []) };
        colony.Roles.Add(role);
        return role;
    }

    [Test]
    [Arguments(true, true, false)]
    [Arguments(true, false, false)]
    [Arguments(true, true, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    public async Task RemovedPartialCompositeOwnerPreservesOnlyItsStillRecommendedActiveMember(bool globalEnabled, bool assignmentEnabled, bool forceOn)
    {
        var capture = PartialOwnerCapture(globalEnabled, assignmentEnabled, forceOn);
        string[] memberGivers = capture.Colony.Roles.Single(r => r.Id == 1).WorkSpec.Capabilities
            .SelectMany(c => c.Givers).Select(g => g.WorkGiverDefName).ToArray();
        string before = string.Join(",", memberGivers.Where(Jobs(capture.Colony, 0).Contains));
        var plan = RecommendationPlan.Build(capture.Colony, capture.Tuning);
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, plan));
        var final = capture.Colony.Pawns[0].Existing;
        bool activeOwner = assignmentEnabled && (globalEnabled || forceOn);
        await Assert.That(final.Any(a => a.RoleId == 999 && !a.Enabled)).IsTrue();
        await Assert.That(final.Any(a => a.RoleId == 998 || a.RoleId == 997)).IsFalse();
        await Assert.That(final.Any(a => a.RoleId == 1 && a.Enabled)).IsEqualTo(activeOwner);
        await Assert.That(string.Join(",", memberGivers.Where(Jobs(capture.Colony, 0).Contains))).IsEqualTo(before);
        await Assert.That(Jobs(capture.Colony, 0)).DoesNotContain("FightFires");
        await Assert.That(ColonyFixPlanner.Build(capture.Colony, capture.Tuning)[0].Changed).IsFalse();
    }

    [Test]
    [Arguments("global-off")]
    [Arguments("excluded-owner")]
    [Arguments("excluded-unwanted-member")]
    public async Task ActivePartialOwnerStateRefreshesFinalTargetsWithoutInvalidatingSuitability(string unavailable)
    {
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = Enumerable.Range(0, 27).Select(_ => new object()).ToArray();
        RecommendationSnapshot Read(int activation, bool active) => cache.For(owner, pawns, catalog,
            0, 0, 0, RecommendationsTuningOptions.Default, (_, _) =>
            {
                var capture = PartialOwnerCapture(unavailable != "global-off",
                    unavailable == "global-off" || active, unavailable == "global-off" && active);
                if (unavailable == "excluded-owner") capture.Colony.Roles.Single(r => r.Id == 998).ExcludedFromRecommendations = true;
                if (unavailable == "excluded-unwanted-member") capture.Colony.Roles.Single(r => r.Id == 997).ExcludedFromRecommendations = true;
                return capture.Colony;
            }, assignmentActivation: activation);
        var inactive = Read(0, false);
        var badges = inactive.Suitability;
        await Assert.That(ColonyFixPlanner.Build(inactive.Colony, inactive.Plan)[0].RoleIds.Contains(1)).IsFalse();
        var active = Read(1, true);
        await Assert.That(ColonyFixPlanner.Build(active.Colony, active.Plan)[0].RoleIds.Contains(1)).IsTrue();
        await Assert.That(ColonyFixPlanner.Build(active.Colony, active.Plan)[0].RoleIds.Contains(998)).IsFalse();
        await Assert.That(ReferenceEquals(badges, active.Suitability)).IsTrue();
        await Assert.That(ReferenceEquals(active, Read(2, true))).IsTrue();
        var restored = Read(3, false);
        await Assert.That(ColonyFixPlanner.Build(restored.Colony, restored.Plan)[0].RoleIds.Contains(1)).IsFalse();
        await Assert.That(ReferenceEquals(badges, restored.Suitability)).IsTrue();
    }

    private static CapturedColony PartialOwnerCapture(bool globalEnabled, bool assignmentEnabled, bool forceOn)
    {
        var capture = new CapturedColony("1.7");
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        RoleView core = capture.Colony.Roles.Single(r => r.Id == 43);
        // A legal player-created, demand-less firefighting role, using the
        // exact native work facts. Its unskilled work earns no recommendation.
        RoleWorkCapabilitySpec fire = core.WorkSpec.Capabilities.Single(c => c.WorkTypeDefName == "Firefighter");
        var unwanted = new RoleView { Id = 997, Coverage = ["FightFires"], OrderedCoverage = ["FightFires"],
            WorkSpec = RoleWorkSpecBuilder.Build(997, [fire], []) };
        capture.Colony.Roles.Add(unwanted);
        RoleView partial = Bundle(capture.Colony, 1, unwanted.Id);
        partial.Id = 998;
        partial.Enabled = globalEnabled;
        RoleView disabled = Bundle(capture.Colony, 43, 1);
        var existing = capture.Colony.Pawns[0].Existing;
        existing.RemoveAll(a => a.RoleId == 43 || a.RoleId == 1);
        existing.InsertRange(0, [new AssignmentView { RoleId = disabled.Id, Enabled = false },
            new AssignmentView { RoleId = partial.Id, Enabled = assignmentEnabled, ForceOn = forceOn }]);
        return capture;
    }

    [Test]
    public async Task OverlappingHeldCompositeOverridesRetainBothStatesAndExecutableWork()
    {
        foreach (bool secondForceOn in new[] { false, true })
        {
        var capture = new CapturedColony("1.7");
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        RoleView first = Bundle(capture.Colony, 21, 20);
        first.Id = 998;
        RoleView second = Bundle(capture.Colony, 20, 22);
        var existing = capture.Colony.Pawns[0].Existing;
        existing.RemoveAll(a => a.RoleId == 21 || a.RoleId == 20 || a.RoleId == 22);
        existing.InsertRange(0, [new AssignmentView { RoleId = first.Id, Enabled = false },
            new AssignmentView { RoleId = second.Id, Enabled = true, ForceOn = secondForceOn }]);
        string[] ownedGivers = new[] { first, second }.SelectMany(r => r.WorkSpec.Capabilities)
            .SelectMany(c => c.Givers).Select(g => g.WorkGiverDefName).Distinct().ToArray();
        string before = string.Join(",", ownedGivers.Where(Jobs(capture.Colony, 0).Contains));
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == first.Id && !a.Enabled)).IsTrue();
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == second.Id && a.Enabled && a.ForceOn == secondForceOn)).IsTrue();
        await Assert.That(string.Join(",", ownedGivers.Where(Jobs(capture.Colony, 0).Contains))).IsEqualTo(before);
        await Assert.That(ColonyFixPlanner.Build(capture.Colony, capture.Tuning)[0].Changed).IsFalse();
        }
    }

    [Test]
    public async Task CompositeOverrideKeepsPartialExplicitMemberWorkWithoutEnablingUnassignedMembers()
    {
        var capture = new CapturedColony("1.7");
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        RoleView held = Bundle(capture.Colony, 43, 1);
        var existing = capture.Colony.Pawns[0].Existing;
        existing.RemoveAll(a => a.RoleId == 1);
        existing.Insert(0, new AssignmentView { RoleId = held.Id, Enabled = false });
        string[] ownedGivers = held.WorkSpec.Capabilities.SelectMany(c => c.Givers).Select(g => g.WorkGiverDefName).Distinct().ToArray();
        string before = string.Join(",", ownedGivers.Where(Jobs(capture.Colony, 0).Contains));
        Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == held.Id && !a.Enabled)).IsTrue()
            .Because(string.Join(",", capture.Colony.Pawns[0].Existing.Select(a => $"{a.RoleId}/{a.Enabled}/{a.ForceOn}")));
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 43 && a.Enabled)).IsTrue();
        await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 1)).IsFalse();
        await Assert.That(string.Join(",", ownedGivers.Where(Jobs(capture.Colony, 0).Contains))).IsEqualTo(before);
        await Assert.That(ColonyFixPlanner.Build(capture.Colony, capture.Tuning)[0].Changed).IsFalse();
    }

    [Test]
    [Arguments("equivalent")]
    [Arguments("superset")]
    [Arguments("left-overlap")]
    [Arguments("right-overlap")]
    public async Task CompetingCompositeWindowsCannotReplaceAnExistingOverrideOrChangeExplicitMemberWork(string conflict)
    {
        foreach (bool forceOn in new[] { false, true })
        foreach (bool directMembers in new[] { false, true })
        {
            var capture = new CapturedColony("1.7");
            Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
            int[] competitorMembers = conflict switch
            {
                "equivalent" => [21, 20],
                "superset" => [21, 20, 22],
                "left-overlap" => [14, 21],
                "right-overlap" => [20, 22],
                _ => throw new ArgumentOutOfRangeException(nameof(conflict)),
            };
            RoleView competitor = Bundle(capture.Colony, competitorMembers);
            competitor.Id = 998;
            RoleView held = Bundle(capture.Colony, 21, 20); // Fabricator, Smith
            var existing = capture.Colony.Pawns[0].Existing;
            if (!directMembers) existing.RemoveAll(a => a.RoleId == 21 || a.RoleId == 20);
            existing.Insert(0, new AssignmentView { RoleId = held.Id, Enabled = forceOn, ForceOn = forceOn });
            string[] ownedGivers = held.WorkSpec.Capabilities.SelectMany(c => c.Givers).Select(g => g.WorkGiverDefName).Distinct().ToArray();
            string before = string.Join(",", ownedGivers.Where(Jobs(capture.Colony, 0).Contains));
            Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
            string context = $"{conflict}; force={forceOn}; explicit members={directMembers}";
            if (!directMembers)
            {
                await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == held.Id
                    && a.Enabled == forceOn && a.ForceOn == forceOn)).IsTrue().Because(context);
                await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == competitor.Id)).IsFalse().Because(context);
            }
            await Assert.That(string.Join(",", ownedGivers.Where(Jobs(capture.Colony, 0).Contains)))
                .IsEqualTo(before).Because(context);
            await Assert.That(ColonyFixPlanner.Build(capture.Colony, capture.Tuning)[0].Changed).IsFalse().Because(context);
        }
    }

    [Test]
    [Arguments("1.6", false)]
    [Arguments("1.7", false)]
    [Arguments("1.6", true)]
    [Arguments("1.7", true)]
    public async Task ExistingCompositeOverrideKeepsItsIdentityWithoutAbsorbingEnabledMemberAssignments(string version, bool forceOn)
    {
        foreach (bool directMembers in new[] { false, true })
        {
            var capture = new CapturedColony(version);
            RoleView bundle = Bundle(capture.Colony, 43, 1);
            var existing = capture.Colony.Pawns[0].Existing;
            if (!directMembers) existing.RemoveAll(a => a.RoleId == 43 || a.RoleId == 1);
            existing.Insert(0, new AssignmentView { RoleId = bundle.Id, Enabled = forceOn, ForceOn = forceOn });
            bool firefightingBefore = Jobs(capture.Colony, 0).Contains("FightFires");
            Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
            if (!directMembers)
            {
                await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == bundle.Id
                    && a.Enabled == forceOn && a.ForceOn == forceOn && !a.Pinned)).IsTrue();
                await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 43 || a.RoleId == 1)).IsFalse();
            }
            else
            {
                await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 43 && a.Enabled)).IsTrue();
                await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 1 && a.Enabled)).IsTrue();
            }
            await Assert.That(Jobs(capture.Colony, 0).Contains("FightFires")).IsEqualTo(firefightingBefore)
                .Because($"target override={forceOn}; directly-held members={directMembers}");
        }
    }

    [Test]
    public async Task OrdinaryCompositePinRemainsAnApplyProtectionWithoutChangingColonyStaffing()
    {
        var capture = new CapturedColony("1.7");
        var before = RecommendationPlan.Build(capture.Colony, capture.Tuning);
        RoleView bundle = Bundle(capture.Colony, 43, 16);
        capture.Colony.Pawns[0].Existing.Insert(0, new AssignmentView { RoleId = bundle.Id, Enabled = true, Pinned = true });
        var after = RecommendationPlan.Build(capture.Colony, capture.Tuning);
        for (int pawn = 0; pawn < before.PawnCount; pawn++)
            await Assert.That(string.Join(",", Enumerable.Range(0, after.RoleCountAt(pawn)).Select(i => after.RoleAt(pawn, i))))
                .IsEqualTo(string.Join(",", Enumerable.Range(0, before.RoleCountAt(pawn)).Select(i => before.RoleAt(pawn, i))));
        await Assert.That(ColonyFixPlanner.Build(capture.Colony, after)[0].RoleIds.Contains(bundle.Id)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompositeDoesNotDiscardDisabledForcedOrPinnedSourceAssignments(bool forceOn)
    {
        foreach (bool pinned in new[] { false, true })
        {
            var capture = new CapturedColony("1.7");
            Bundle(capture.Colony, 43, 1);
            var existing = capture.Colony.Pawns[0].Existing;
            int sourceAt = existing.FindIndex(a => a.RoleId == 43);
            AssignmentView source = existing[sourceAt];
            source.Enabled = forceOn;
            source.ForceOn = forceOn;
            source.Pinned = pinned;
            existing[sourceAt] = source;
            Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
            await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 43
                && a.Enabled == forceOn && a.ForceOn == forceOn && a.Pinned == pinned)).IsTrue();
            await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == 999)).IsFalse();
        }
    }

    [Test]
    [Arguments("1.6")]
    [Arguments("1.7")]
    public async Task CompositeSubstitutionPreservesNativeJobsAndExistingAssignmentState(string version)
    {
        var baseline = new CapturedColony(version);
        Apply(baseline.Colony, ColonyFixPlanner.Build(baseline.Colony, baseline.Tuning));
        foreach (string mode in new[] { "ordinary", "disabled", "blocker", "conditional", "excluded" })
        {
            var capture = new CapturedColony(version);
            RoleView bundle = Bundle(capture.Colony, 43, 1); // Core, Basics
            bundle.Enabled = mode != "disabled";
            bundle.Blocker = mode == "blocker";
            bundle.HasRules = mode == "conditional";
            bundle.ExcludedFromRecommendations = mode == "excluded";
            Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
            await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == bundle.Id))
                .IsEqualTo(mode == "ordinary").Because(mode);
            for (int pawn = 0; pawn < capture.Colony.Pawns.Count; pawn++)
                await Assert.That(string.Join(",", Jobs(capture.Colony, pawn)))
                    .IsEqualTo(string.Join(",", Jobs(baseline.Colony, pawn))).Because($"{mode}, pawn {pawn}");
        }

        var disabledCook = new CapturedColony(version);
        Bundle(disabledCook.Colony, 9, 21); // Cook, Fabricator
        await Assert.That(Jobs(disabledCook.Colony, 8)).DoesNotContain("DoBillsCook"); // Native Niklas
        Apply(disabledCook.Colony, ColonyFixPlanner.Build(disabledCook.Colony, disabledCook.Tuning));
        await Assert.That(disabledCook.Colony.Pawns[8].Existing.Any(a => a.RoleId == 9 && !a.Enabled)).IsTrue();
        await Assert.That(Jobs(disabledCook.Colony, 8)).DoesNotContain("DoBillsCook");
    }

    [Test]
    [Arguments("1.6")]
    [Arguments("1.7")]
    public async Task ExistingConditionalAndBlockerCompositesSurviveFixUnlessExplicitlyExcluded(string version)
    {
        foreach (string mode in new[] { "conditional", "blocker", "pinned", "ordinary" })
        {
            var capture = new CapturedColony(version);
            RoleView bundle = Bundle(capture.Colony, 43, 16); // Core, Farmer
            bundle.HasRules = mode == "conditional";
            bundle.Blocker = mode == "blocker";
            capture.Colony.Pawns[0].Existing.Insert(0,
                new AssignmentView { RoleId = bundle.Id, Enabled = true, Pinned = mode == "pinned" });
            bool fireBefore = Jobs(capture.Colony, 0).Contains("FightFires");
            var fix = ColonyFixPlanner.Build(capture.Colony, capture.Tuning);
            await Assert.That(fix[0].RoleIds.Contains(bundle.Id)).IsEqualTo(mode != "ordinary").Because(mode);
            Apply(capture.Colony, fix);
            if (mode == "blocker")
            {
                await Assert.That(fireBefore).IsFalse();
                await Assert.That(Jobs(capture.Colony, 0)).DoesNotContain("FightFires");
            }
            bundle.ExcludedFromRecommendations = true;
            Apply(capture.Colony, ColonyFixPlanner.Build(capture.Colony, capture.Tuning));
            await Assert.That(capture.Colony.Pawns[0].Existing.Any(a => a.RoleId == bundle.Id))
                .IsEqualTo(mode == "pinned").Because($"explicit exclusion, {mode}");
        }
    }

    [Test]
    [Arguments("1.6")]
    [Arguments("1.7")]
    public async Task HunterPlacementRespectsPinnedAndConditionalAssignmentOrder(string version)
    {
        foreach (bool pinned in new[] { false, true })
        {
            var capture = new CapturedColony(version);
            var existing = capture.Colony.Pawns[2].Existing;
            AssignmentView hunter = existing.Single(a => a.RoleId == 12);
            hunter.Pinned = pinned;
            existing.RemoveAll(a => a.RoleId == 12);
            existing.Insert(existing.FindIndex(a => a.RoleId == 39), hunter);
            var fix = ColonyFixPlanner.Build(capture.Colony, capture.Tuning);
            int hunterAt = fix[2].RoleIds.ToList().IndexOf(12);
            int conditionalAt = fix[2].RoleIds.ToList().IndexOf(39);
            await Assert.That(hunterAt < conditionalAt).IsTrue().Because($"pinned={pinned}; conditional protects relative order");
        }
    }

    [Test]
    [Arguments("1.6")]
    [Arguments("1.7")]
    public async Task OneHourCookShiftDoesNotReplaceOrdinaryMealStaffing(string version)
    {
        foreach (bool excludedShift in new[] { false, true })
        {
            var capture = new CapturedColony(version);
            ColonyView colony = capture.Colony;
            RoleView shift = colony.Roles.Single(r => r.Id == 9);
            RoleView ordinaryCook = RecommendationRolePolicy.From(shift).Apply(shift);
            ordinaryCook.Id = 999;
            colony.Roles.Add(ordinaryCook);
            shift.HasRules = true;
            shift.ExcludedFromRecommendations = excludedShift;
            foreach (PawnView pawn in colony.Pawns)
                if (!pawn.Existing.Any(a => a.RoleId == shift.Id))
                    pawn.Existing.Add(new AssignmentView { RoleId = shift.Id, Enabled = true });
            Apply(colony, ColonyFixPlanner.Build(colony, capture.Tuning));
            // An actual nonzero breakfast-only mask; noon must retain cooks.
            const int breakfast = 1 << 8;
            await Assert.That(TimedRoleInvalidationPlanner.HoursPass(breakfast, 8)).IsTrue();
            await Assert.That(TimedRoleInvalidationPlanner.HoursPass(breakfast, 12)).IsFalse();
            await Assert.That(Enumerable.Range(0, colony.Pawns.Count)
                .Count(p => Jobs(colony, p).Contains("DoBillsCook"))).IsGreaterThan(0);
            await Assert.That(colony.Pawns.Count(p => p.Existing.Any(a => a.RoleId == ordinaryCook.Id)))
                .IsGreaterThan(0).Because($"excluded shift={excludedShift}");
        }
    }

    internal static RoleView Bundle(ColonyView colony, params int[] members)
    {
        var bundle = new RoleView
        {
            Id = 999, MemberRoleIds = [.. members],
            WorkSpec = RoleWorkSpecBuilder.Merge(999,
                members.Select(id => colony.Roles.Single(r => r.Id == id).WorkSpec).ToArray(), []),
        };
        colony.Roles.Add(bundle);
        return bundle;
    }

    internal static void Apply(ColonyView colony, IReadOnlyList<PawnFixTarget> targets)
    {
        foreach (PawnFixTarget target in targets)
        {
            List<AssignmentView> prior = colony.Pawns[target.PawnIndex].Existing;
            colony.Pawns[target.PawnIndex].Existing = target.RoleIds.Select(id => prior.Any(a => a.RoleId == id)
                ? prior.First(a => a.RoleId == id) : new AssignmentView { RoleId = id, Enabled = true }).ToList();
        }
    }

    internal static string[] Jobs(ColonyView colony, int pawnIndex)
    {
        var catalog = new FakeCatalog();
        foreach (var group in colony.Roles.SelectMany(r => r.WorkSpec.Capabilities).GroupBy(c => c.WorkTypeDefName))
            catalog.WithWorkType(group.Key, group.SelectMany(c => c.Givers).Select(g => g.WorkGiverDefName).Distinct().ToArray());
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> slices = [];
        foreach (AssignmentView assignment in colony.Pawns[pawnIndex].Existing)
        {
            RoleView role = colony.Roles.Single(r => r.Id == assignment.RoleId);
            // Authored conditions are inactive for this detached noon scenario.
            if ((!role.Enabled && !assignment.ForceOn) || !assignment.Enabled || role.HasRules) continue;
            foreach (int id in role.MemberRoleIds ?? [role.Id])
            {
                RoleView member = colony.Roles.Single(r => r.Id == id);
                bool blocker = role.Blocker;
                if (role.MemberRoleIds != null
                    && !CompositeRoles.TryGetMemberSlice(role.Blocker, member.Enabled, member.Blocker, out blocker)) continue;
                slices.Add((member.WorkSpec.Capabilities.SelectMany(c => c.Givers)
                    .Select(g => new JobEntry(JobEntryKind.WorkGiver, g.WorkGiverDefName)).ToArray(), blocker));
            }
        }
        return JobOrderCompiler.Compile(slices, catalog, _ => true).AllInOrder.ToArray();
    }
}
