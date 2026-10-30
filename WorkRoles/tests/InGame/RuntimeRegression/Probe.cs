using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles;
using WorkRoles.Core;

namespace RuntimeAudit;

// Non-shipped native regressions. Setup touches only this run's copy.
public sealed class Probe : GameComponent
{
    private int frames;
    private bool done;
    private bool awaitGhoulCapture;
    private object ghoulView;
    private object ghoulRoster;
    private Pawn ghoulPawn;
    private Role ghoulRole;
    private int rosterRevision;
    public Probe(Game game) { }
    private static void Record(string value)
    {
        File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "runtime-audit-results.txt"), value + Environment.NewLine);
        Log.Message("[RuntimeAudit] " + value);
    }
    private static void Require(bool value, string text)
    {
        if (!value) throw new InvalidOperationException(text);
        Record("PASS " + text);
    }
    private static Role Create(string label)
    {
        RoleCommands.CreateRole(label);
        return RoleStore.Current.roles.Single(r => r.label == label);
    }
    public override void GameComponentUpdate()
    {
        if (done || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
        if (awaitGhoulCapture)
        {
            if (!File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "runtime-ghoul-captured.txt"))) return;
            awaitGhoulCapture = false;
        }
        frames++;
        if (frames != 45 && frames != 60 && frames != 75 && frames != 90 && frames != 105) return;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        try
        {
            if (frames == 45)
            {
                CompositeBill();
                FloorClassification();
            }
            GhoulRosterStage();
        }
        catch (Exception error) { Record("FAILED " + error); done = true; Record("COMPLETE"); }
    }
    private List<Pawn> DisplayedPawns()
    {
        object sections = AccessTools.Method(ghoulRoster.GetType(), "Sections").Invoke(ghoulRoster, new object[] { RoleStore.Current });
        return (List<Pawn>)AccessTools.Method(sections.GetType(), "CopyPawns").Invoke(sections, null);
    }
    private IReadOnlyList<Pawn> FreshPawns() => (IReadOnlyList<Pawn>)AccessTools.Method(ghoulRoster.GetType(), "SnapshotPawns").Invoke(ghoulRoster, null);
    private void OpenWorkTab()
    {
        Find.MainTabsRoot.SetCurrentTab(DefDatabase<MainButtonDef>.GetNamed("Work"));
        var window = (WorkRoles.UI.MainTabWindow_WorkRoles)DefDatabase<MainButtonDef>.GetNamed("Work").TabWindow;
        ghoulView = AccessTools.Field(window.GetType(), "colonistsTab").GetValue(window);
        ghoulRoster = AccessTools.Field(ghoulView.GetType(), "rosterState").GetValue(ghoulView);
    }
    private void GhoulRosterStage()
    {
        if (frames == 45)
        {
            AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
            RoleCommands.SetAutoOptimize(false);
            foreach (var window in Find.WindowStack.Windows.ToList()) window.Close(false);
            foreach (var role in RoleStore.Current.roles.ToArray()) RoleCommands.SetRoleAutoAssign(role.id, false);
            ghoulPawn = Find.CurrentMap.mapPawns.FreeColonistsSpawned.First(p => !p.Dead && !p.Downed && !p.IsMutant);
            ghoulRole = Create("AR4 Native lifecycle test");
            RoleCommands.AddEntry(ghoulRole.id, new JobEntry(JobEntryKind.WorkGiver, "CleanFilth"));
            RoleCommands.PasteRoleSet(ghoulPawn, new List<RoleAssignment> { new RoleAssignment { roleId = ghoulRole.id } });
            ghoulPawn.jobs.StopAll();
            OpenWorkTab();
            Require(DisplayedPawns().Contains(ghoulPawn) && FreshPawns().Contains(ghoulPawn), "AR4 actual work window and fresh cohort initially include colonist");
            rosterRevision = UiVersion.Roster;
        }
        else if (frames == 60)
        {
            MutantUtility.SetPawnAsMutantInstantly(ghoulPawn, MutantDefOf.Ghoul);
            Require(ghoulPawn.IsMutant && !ghoulPawn.IsColonist && ghoulPawn.Faction == Faction.OfPlayer && ghoulPawn.Spawned,
                "AR4 native ghoul conversion changes membership without faction or spawn transition");
            Require(!RoleStore.Current.IsManaged(ghoulPawn), "AR4 native conversion removes managed work roles");
        }
        else if (frames == 75)
        {
            Require(!FreshPawns().Contains(ghoulPawn), "AR4 fresh supported cohort excludes ghoul");
            Require(!DisplayedPawns().Contains(ghoulPawn), "WR29 warm rendered work sections remove converted ghoul");
            Require(UiVersion.Roster != rosterRevision, "WR29 conversion publishes roster membership");
            rosterRevision = UiVersion.Roster;
            File.WriteAllText(Path.Combine(GenFilePaths.SaveDataFolderPath, "runtime-regression-stage.txt"), "ghoul");
            awaitGhoulCapture = true;
        }
        else if (frames == 90)
        {
            ghoulPawn.mutant.Revert();
            Require(ghoulPawn.IsColonist && !ghoulPawn.IsMutant && ghoulPawn.Faction == Faction.OfPlayer && ghoulPawn.Spawned,
                "AR4 native revert restores colonist membership without faction or spawn transition");
            Require(!RoleStore.Current.IsManaged(ghoulPawn), "AR4 auto-assign disabled leaves reverted colonist roleless");
        }
        else
        {
            Require(FreshPawns().Contains(ghoulPawn), "AR4 fresh supported cohort includes reverted colonist");
            Require(DisplayedPawns().Contains(ghoulPawn), "WR29 warm rendered work sections restore reverted colonist with autoassign off");
            Require(UiVersion.Roster != rosterRevision, "WR29 roleless revert publishes roster membership");
            rosterRevision = UiVersion.Roster;
            var stableSections = DisplayedPawns();
            Require(DisplayedPawns().SequenceEqual(stableSections) && UiVersion.Roster == rosterRevision,
                "WR29 repeated reads without transitions preserve roster revision");
            // The same native transition must notify even when no assignment
            // mutation can happen: the reverted pawn is deliberately roleless.
            MutantUtility.SetPawnAsMutantInstantly(ghoulPawn, MutantDefOf.Ghoul);
            Require(!DisplayedPawns().Contains(ghoulPawn) && UiVersion.Roster != rosterRevision,
                "WR29 roleless conversion removes warm row independently of assignments");
            ghoulPawn.mutant.Revert();
            Require(DisplayedPawns().Contains(ghoulPawn) && !RoleStore.Current.IsManaged(ghoulPawn),
                "WR29 second roleless revert restores row without assigning work");
            Record("SUCCESS WR23 WR24 WR29 native runtime regressions");
            done = true;
            Record("COMPLETE");
        }
    }
    private static void CompositeBill()
    {
        var giver = DefDatabase<WorkGiverDef>.GetNamed("DoBillsCook");
        var worker = (WorkGiver_DoBill)giver.Worker;
        var bench = Find.Maps.SelectMany(m => m.listerThings.AllThings).OfType<Building_WorkTable>()
            .First(b => worker.ThingIsUsableBillGiver(b) && b.BillStack.Bills.OfType<Bill_Production>().Any());
        var bill = bench.BillStack.Bills.OfType<Bill_Production>().First();
        bill.SetAnyPawnRestriction();
        RoleCommands.SetBillRole(bill, -1);
        Pawn pawn = bench.Map.mapPawns.FreeColonistsSpawned.First(p => !p.Dead && !p.Downed
            && !p.WorkTypeIsDisabled(giver.workType) && !p.WorkTagIsDisabled(giver.workTags)
            && bill.PawnAllowedToStartAnew(p));
        var member = Create("AR2 Cook");
        RoleCommands.AddEntry(member.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));
        var composite = Create("AR2 Kitchen team");
        RoleCommands.SetRoleComposite(composite.id, true);
        RoleCommands.AddCompositeMember(composite.id, member.id);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = member.id } });
        RoleCommands.SetBillRole(bill, member.id);
        Require(BillRoles.EligibleRoles(bill).Contains(member) && BillRoles.EligibleRoles(bill).Contains(composite), "AR2 real bill offers both member and composite roles");
        Require(bill.PawnAllowedToStartAnew(pawn), "AR2 native bill gate admits active direct member holder");
        Require(CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver), "AR2 direct member provides native cooking work");
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = composite.id } });
        Require(CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver), "AR2 bundle preserves exact native cooking giver");
        Require(bill.PawnAllowedToStartAnew(pawn), "WR23 same native bill admits effective member held through bundle");
        object[] claimArguments = { pawn, giver.defName, -1 };
        bool claimed = (bool)AccessTools.Method(typeof(CompiledJobOrders), "TryGetClaimingRole").Invoke(null, claimArguments);
        Require(claimed && (int)claimArguments[2] == composite.id,
            "WR23 work priority remains attributed to the outer combined assignment");
        var warm = CompiledJobOrders.NormalFor(pawn);
        Require(bill.PawnAllowedToStartAnew(pawn) && ReferenceEquals(warm, CompiledJobOrders.NormalFor(pawn)),
            "WR23 repeated native eligibility reads reuse compiled work");
        RoleCommands.ToggleRoleGlobal(member.id);
        Require(!bill.PawnAllowedToStartAnew(pawn) && !CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver),
            "WR23 disabled member grants neither bill membership nor cooking");
        RoleCommands.ToggleRoleGlobal(member.id);
        SetMemberBlocker(composite, member, true);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 blocker member cannot satisfy bill restriction");
        SetMemberBlocker(composite, member, false);
        RoleCommands.SetRoleBlocker(composite.id, true);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 blocker bundle cannot grant effective member eligibility");
        RoleCommands.SetRoleBlocker(composite.id, false);
        RoleCommands.SetAssignmentState(pawn, composite.id, (int)AssignmentState.Disabled);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 disabled outer assignment suppresses member eligibility");
        RoleCommands.SetAssignmentState(pawn, composite.id, (int)AssignmentState.Enabled);
        RoleCommands.ToggleRoleGlobal(composite.id);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 globally disabled outer suppresses member eligibility");
        RoleCommands.SetAssignmentState(pawn, composite.id, (int)AssignmentState.ForceOn);
        Require(bill.PawnAllowedToStartAnew(pawn), "WR23 force-on outer preserves normal activation semantics");
        RoleCommands.ToggleRoleGlobal(member.id);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 force-on outer does not override disabled member");
        RoleCommands.ToggleRoleGlobal(member.id);
        RoleCommands.ToggleRoleGlobal(composite.id);
        RoleCommands.SetRoleActiveHours(composite.id, 0);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 outer hours suppress effective member despite force-on");
        RoleCommands.ClearRoleRules(composite.id);
        RoleCommands.ToggleRoleLocation(composite.id, LocationRules.Caravans);
        Require(!bill.PawnAllowedToStartAnew(pawn), "WR23 outer location rules suppress effective member");
        RoleCommands.ClearRoleRules(composite.id);
        Require(bill.PawnAllowedToStartAnew(pawn), "WR23 clearing outer rules restores native member eligibility");
        // Membership does not promise unvetoed work: preserve the established
        // direct-assignment semantics for empty roles and earlier vetoes.
        RoleCommands.RemoveEntry(member.id, 0);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = member.id } });
        bool emptyDirect = bill.PawnAllowedToStartAnew(pawn);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = composite.id } });
        Require(emptyDirect && bill.PawnAllowedToStartAnew(pawn)
            && !CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver),
            "WR23 empty member preserves direct active-membership semantics without granting work");
        RoleCommands.AddEntry(member.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));
        var veto = Create("WR23 Earlier cooking veto");
        RoleCommands.AddEntry(veto.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));
        RoleCommands.SetRoleBlocker(veto.id, true);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment>
        {
            new RoleAssignment { roleId = veto.id }, new RoleAssignment { roleId = member.id }
        });
        bool vetoedDirect = bill.PawnAllowedToStartAnew(pawn);
        Require(!CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver), "WR23 direct-role control respects earlier veto");
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment>
        {
            new RoleAssignment { roleId = veto.id }, new RoleAssignment { roleId = composite.id }
        });
        Require(vetoedDirect && bill.PawnAllowedToStartAnew(pawn)
            && !CompiledJobOrders.NormalFor(pawn).Any(g => g.def == giver),
            "WR23 earlier veto preserves direct bill-membership semantics while withholding work");
        SetMemberBlocker(composite, member, true);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = member.id } });
        Require(bill.PawnAllowedToStartAnew(pawn), "WR23 existing direct blocker restriction semantics remain unchanged");
        SetMemberBlocker(composite, member, false);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = composite.id } });
        RoleCommands.SetBillRole(bill, composite.id);
        Require(bill.PawnAllowedToStartAnew(pawn), "AR2 changing only bill restriction to bundle restores eligibility");
        Record("VERIFIED WR23 worker=" + pawn.LabelShort + " recipe=" + bill.recipe.defName + " member=" + member.id + " composite=" + composite.id);
    }
    private static void SetMemberBlocker(Role composite, Role member, bool value)
    {
        // Normal editing forbids changing an included member's blocker policy.
        // Detach, edit, and reinsert exactly as the supported UI requires.
        int index = composite.memberRoleIds.IndexOf(member.id);
        RoleCommands.RemoveCompositeMember(composite.id, index);
        RoleCommands.SetRoleBlocker(member.id, value);
        RoleCommands.AddCompositeMember(composite.id, member.id, index);
        Require(member.blocker == value && composite.memberRoleIds.Contains(member.id),
            "WR23 blocker control uses a legal authored composite member");
    }
    private static void FloorClassification()
    {
        var original = Find.CurrentMap;
        var giver = DefDatabase<WorkGiverDef>.GetNamed("CleanFilth");
        var pawn = original.mapPawns.FreeColonistsSpawned.First(p => !p.Dead && !p.Downed && !p.WorkTypeIsDisabled(giver.workType));
        var surfacePawn = original.mapPawns.FreeColonistsSpawned.First(p => p != pawn && !p.Dead && !p.Downed);
        var unrelatedPawn = original.mapPawns.FreeColonistsSpawned.First(p => p != pawn && p != surfacePawn
            && !p.Dead && !p.Downed && !p.WorkTypeIsDisabled(giver.workType));
        PlanetTile tile = Enumerable.Range(0, Find.WorldGrid.TilesCount).Select(i => new PlanetTile(i, Find.WorldGrid.Surface))
            .First(t => t.Tile.PrimaryBiome.defName == "TemperateForest" && !Find.WorldObjects.AnyMapParentAt(t));
        var parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Site);
        parent.Tile = tile;
        Find.WorldObjects.Add(parent);
        Map source = MapGenerator.GenerateMap(new IntVec3(75, 1, 75), parent, MapGeneratorDefOf.Encounter);
        surfacePawn.jobs.StopAll();
        surfacePawn.DeSpawn();
        GenSpawn.Spawn(surfacePawn, CellFinder.RandomClosewalkCellNear(source.Center, source, 30), source);
        Record("AR1 creating native undercave attached to new encounter parentType=" + source.Parent.GetType().Name);
        Map pocket = PocketMapUtility.GeneratePocketMap(new IntVec3(50, 1, 50), MapGeneratorDefOf.Undercave, null, source);
        pawn.jobs.StopAll();
        pawn.DeSpawn();
        IntVec3 cell = CellFinder.RandomClosewalkCellNear(pocket.Center, pocket, 20);
        GenSpawn.Spawn(pawn, cell, pocket);
        var role = Create("AR1 Settlement cleaning");
        RoleCommands.AddEntry(role.id, new JobEntry(JobEntryKind.WorkGiver, giver.defName));
        RoleCommands.ToggleRoleLocation(role.id, LocationRules.Settlements);
        RoleCommands.PasteRoleSet(pawn, new List<RoleAssignment> { new RoleAssignment { roleId = role.id } });
        RoleCommands.PasteRoleSet(surfacePawn, new List<RoleAssignment> { new RoleAssignment { roleId = role.id } });
        RoleCommands.PasteRoleSet(unrelatedPawn, new List<RoleAssignment> { new RoleAssignment { roleId = role.id } });
        Require(!RoleRules.Pass(role, pawn), "AR1 encounter is not settlement before native settle");
        var old = CompiledJobOrders.NormalFor(pawn);
        Require(!old.Any(g => g.def == giver), "AR1 location-gated native work initially absent");
        var oldSurface = CompiledJobOrders.NormalFor(surfacePawn);
        var unrelated = CompiledJobOrders.NormalFor(unrelatedPawn);
        SettleInExistingMapUtility.Settle(source);
        Require(RoleRules.Pass(role, pawn), "AR1 native Settle makes undercave's canonical source a settlement");
        var observed = CompiledJobOrders.NormalFor(pawn);
        Require(!ReferenceEquals(old, observed) && observed.Any(g => g.def == giver),
            "WR24 native Settle refreshes attached cave work immediately while paused");
        Require(!ReferenceEquals(oldSurface, CompiledJobOrders.NormalFor(surfacePawn))
            && CompiledJobOrders.NormalFor(surfacePawn).Any(g => g.def == giver), "WR24 surface holder also observes native Settle");
        Require(ReferenceEquals(unrelated, CompiledJobOrders.NormalFor(unrelatedPawn)),
            "WR24 unrelated canonical map retains its compiled work snapshot");
        Require(ReferenceEquals(observed, CompiledJobOrders.NormalFor(pawn)), "WR24 unchanged cave reads reuse refreshed work snapshot");
        Current.Game.CurrentMap = original;
        Record("VERIFIED WR24 pawn=" + pawn.LabelShort + " source=" + source.uniqueID + " pocket=" + pocket.uniqueID);
    }
}
