using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;

namespace WorkRolesPawnFacts;

public sealed partial class Probe
{
    private Pawn birthdayPawn;
    private Role birthdayRole;
    private object capabilityState, birthdayExternal, birthdaySkills, birthdayPresentation;
    private List<RoleAssignment> birthdayAssignments;
    private int birthdayStart, birthdayTick = -1, birthdayCalls, birthdayFactRevision, birthdayContextRevision;
    private static void Birthday(Pawn ___pawn, int birthdayAge)
    {
        if (instance?.birthdayPawn != ___pawn || instance.phase != 4) return;
        instance.birthdayCalls++;
        instance.birthdayTick = Find.TickManager.TicksGame;
        Record("OBSERVED normal native BirthdayBiological age=" + birthdayAge);
    }
    private object BirthdayPresentation(object snapshot) => Call(capabilityState, "PresentationFor", birthdayPawn, birthdayRole, Get(view, "PawnListStamp"), snapshot);
    private void BirthdayUpdate()
    {
        if (phase == 0)
        {
            birthdayPawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.First(p => !p.Downed && !p.Dead && !p.InMentalState
                && p.RaceProps.lifeStageWorkSettings.Count > 0 && !p.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning)
                && p.ageTracker.BiologicalTicksPerTick > 0f);
            Require(!birthdayPawn.RaceProps.lifeStageWorkSettings.Any(s => s.minAge == 18),
                "WR18 native race has no disabled-work unlock at eighteen to mask birthday invalidation");
            // Fixture preparation only. The crossing itself must occur through
            // the ordinary game tick; DebugForceBirthday is never called.
            birthdayPawn.ageTracker.AgeBiologicalTicks = 18L * BiologicalAge.TicksPerYear - 180;
            birthdayPawn.Notify_DisabledWorkTypesChanged();
            RoleCommands.CreateRole("Adult Cleaning regression");
            birthdayRole = store.roles.Single(r => r.label == "Adult Cleaning regression");
            RoleCommands.AddEntry(birthdayRole.id, new JobEntry(JobEntryKind.WorkType, "Cleaning"));
            RoleCommands.SetRoleAgeRange(birthdayRole.id, 18, 0);
            SetOnly(birthdayPawn, birthdayRole);
            birthdayAssignments = store.pawnSets[birthdayPawn].assignments;
            Require(!birthdayRole.autoAssign, "WR18 legitimate standing adult-only assignment is manually held");
            Find.Selector.ClearSelection(); Find.Selector.Select(birthdayPawn);
            Next(1); return;
        }
        if (phase == 1)
        {
            if (!OpenColonists()) return;
            Set(roster, "Search", birthdayPawn.LabelShortCap.ToString());
            ((ListSelection<Pawn>)Get(view, "selection")).Click(birthdayPawn);
            Next(2); return;
        }
        if (phase == 2)
        {
            Call(view, "RefreshExternalSnapshotIfNeeded");
            birthdayExternal = Call(view, "ExternalSnapshotFor", birthdayPawn);
            birthdaySkills = SkillIdentity(Get(view, "statsState"), birthdayPawn);
            capabilityState = Get(view, "roleCapabilityState");
            birthdayPresentation = BirthdayPresentation(birthdayExternal);
            birthdayFactRevision = Revision("FactRevisions", birthdayPawn);
            birthdayContextRevision = Revision("ContextRevisions", birthdayPawn);
            Require(Facts(birthdayExternal).AgeLimitsApply && Facts(birthdayExternal).BiologicalAgeTicks < 18L * BiologicalAge.TicksPerYear,
                "WR18 actual window captures the seventeen-year-old before native birthday");
            Require(Get(birthdayPresentation, "WarningSeverity").ToString() != "None" && Get(birthdayPresentation, "Tooltip") != null,
                "WR18 actual held-role chip warns too young before birthday");
            Record("WR18 BEFORE pawn=" + birthdayPawn.LabelShort + " ageTicks=" + Facts(birthdayExternal).BiologicalAgeTicks
                + " tooltip=" + Get(birthdayPresentation, "Tooltip"));
            Capture("wr18-before"); Next(3); return;
        }
        if (phase == 3 && Captured())
        {
            birthdayStart = Find.TickManager.TicksGame;
            phase = 4;
            Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
        }
        else if (phase == 5 && Captured()) Finish("SUCCESS birthday WR18");
    }
    public override void GameComponentTick()
    {
        if (mode != "birthday" || phase != 4) return;
        try
        {
            birthdayPawn.needs.food.CurLevelPercentage = 1f;
            birthdayPawn.needs.rest.CurLevelPercentage = 1f;
            if (birthdayTick >= 0 && Find.TickManager.TicksGame >= birthdayTick + 5)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Call(view, "RefreshExternalSnapshotIfNeeded");
                object cached = Call(view, "ExternalSnapshotFor", birthdayPawn);
                object presentation = BirthdayPresentation(cached);
                object fresh = Call(typeof(RecsAdapter), "CapturePawnSnapshot", birthdayPawn, PawnSignalSnapshot.Empty);
                Require(birthdayCalls == 1 && birthdayPawn.ageTracker.AgeBiologicalYears == 18,
                    "WR18 ordinary native AgeTick crosses eighteenth birthday exactly once");
                Require(Facts(cached).BiologicalAgeTicks >= 18L * BiologicalAge.TicksPerYear
                    && !ReferenceEquals(cached, birthdayExternal), "WR18 actual window external cache immediately observes normal birthday");
                Require(Get(presentation, "WarningSeverity").ToString() == "None" && Get(presentation, "Tooltip") == null
                    && Equals(Get(presentation, "WarningSeverity"), Get(BirthdayPresentation(fresh), "WarningSeverity")),
                    "WR18 actual cached assigned-role warning clears too-young state and agrees with current native facts");
                Require(ReferenceEquals(birthdayAssignments, store.pawnSets[birthdayPawn].assignments)
                    && birthdayAssignments.Count == 1 && birthdayAssignments[0].roleId == birthdayRole.id,
                    "WR18 birthday preserves the legitimate standing assignment");
                // This independently meaningful cache contract is not visible
                // in pixels: age facts must not recapture unchanged skills.
                Require(ReferenceEquals(birthdaySkills, SkillIdentity(Get(view, "statsState"), birthdayPawn))
                    && birthdayFactRevision == Revision("FactRevisions", birthdayPawn)
                    && birthdayContextRevision != Revision("ContextRevisions", birthdayPawn),
                    "WR18 age context changes preserve factual skill revision and shared skill capture");
                BirthdayPresentation(cached);
                Record("WR18 AFTER pawn=" + birthdayPawn.LabelShort + " ageTicks=" + Facts(cached).BiologicalAgeTicks
                    + " warning=" + Get(presentation, "WarningSeverity") + " elapsedTicks=" + (Find.TickManager.TicksGame - birthdayStart));
                Capture("wr18-after"); Next(5);
            }
            else if (Find.TickManager.TicksGame - birthdayStart > 2000) Finish("FAILED birthday normal native aging timeout");
        }
        catch (Exception ex) { Finish("FAILED birthday tick " + ex); }
    }
}
