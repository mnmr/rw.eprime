using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core;

namespace ImportRestore;

public sealed class NativeChecks : GameComponent
{
    private int frames;
    private bool done;
    private RoleStore store;
    public NativeChecks(Game game) { }
    private static void Record(string line)
    {
        File.AppendAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"persistence-audit-results.txt"),line+Environment.NewLine);
        Log.Message("[ImportRestore] "+line);
    }
    private static void Require(bool value,string line)
    {
        if(!value) throw new Exception(line);
        Record("PASS "+line);
    }
    public override void GameComponentUpdate()
    {
        if(done || Current.ProgramState!=ProgramState.Playing || LongEventHandler.AnyEventNowOrWaiting || Find.CurrentMap==null || ++frames<30) return;
        done=true;
        try
        {
            store=RoleStore.Current;
            Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
            RoleCommands.SetAutoOptimize(false);
            string mode=File.ReadAllText(Path.Combine(GenFilePaths.SaveDataFolderPath,"runtime-authority-mode.txt")).Trim();
            if(mode=="restore-history") { HistoryRecovery(); Recovery(); Record("SUCCESS WR21 history"); }
            else { Identity(); Recovery(); Record("SUCCESS WR19 WR21"); }
        }
        catch(Exception e) { Record("FAILED "+e); }
    }
    private void Apply(string xml,bool overwrite,List<int> rows) => RoleCommands.ApplyImport(new ImportSelection
        {xml=xml,roles=true,rolesOverwrite=overwrite,roleRows=rows});
    private void Identity()
    {
        string baseline=RoleIO.BuildXml(store);
        Role doctor=store.RoleByTemplate("WS_Doctor");
        Require(doctor!=null,"native colony has seeded Doctor");
        string originalLabel=doctor.label;
        int originalId=doctor.id;
        var originalEntries=doctor.entries.ToArray();
        var holders=store.pawnSets.Where(p=>p.Value.assignments.Any(a=>a.roleId==doctor.id)).Select(p=>p.Key).ToArray();
        Require(holders.Length>0,"WR19 native Doctor has actual assigned holders");
        Record("WR19 original Doctor holders="+string.Join(",",holders.Select(p=>p.LabelShort)));
        foreach(int mode in new[]{0,1,2})
        {
            bool overwrite=mode==2;
            RoleCommands.RenameRole(doctor.id,"Audit Medic");
            RoleCommands.CreateRole(originalLabel);
            Role custom=store.roles.Single(r=>r.label==originalLabel);
            RoleCommands.AddEntry(custom.id,new JobEntry(JobEntryKind.WorkType,"Hauling"));
            RoleCommands.MoveRoleInCatalog(store.roles.IndexOf(custom),0);
            string source=RoleIO.BuildXml(store);
            var doc=RoleIO.Parse(source);
            int customRow=doc.roles.FindIndex(r=>r.label==originalLabel),medicRow=doc.roles.FindIndex(r=>r.templateDef=="WS_Doctor");
            Require(customRow<medicRow,"WR19 normal commands put custom Doctor before renamed template");
            RoleCommands.DeleteRole(custom.id); RoleCommands.RenameRole(doctor.id,originalLabel);
            Apply(source,overwrite,mode==0?new List<int>{customRow}:new List<int>{customRow,medicRow});
            var actual=store.RoleById(originalId);
            Require(actual.label==(mode==0?originalLabel:"Audit Medic") && actual.templateDefName=="WS_Doctor"
                && actual.entries.SequenceEqual(originalEntries),"WR19 partial merge/full merge/overwrite preserves the assigned Doctor and medical work: mode="+mode);
            Require(holders.All(p=>store.pawnSets[p].assignments.Any(a=>a.roleId==originalId)),"WR19 original Doctor holders retain their identity");
            Require(store.roles.Any(r=>r.id!=originalId && r.templateDefName==null && r.entries.Any(e=>e.DefName=="Hauling")
                && r.label.StartsWith(originalLabel,StringComparison.Ordinal)),"WR19 imported custom Doctor is a separate hauling role");
            Apply(baseline,true,new List<int>()); doctor=store.RoleByTemplate("WS_Doctor");
        }
    }
    private static bool HasJob(Pawn pawn,string giver) => CompiledJobOrders.NormalFor(pawn).Any(g=>g.def.defName==giver)
        || CompiledJobOrders.EmergencyFor(pawn).Any(g=>g.def.defName==giver);
    private void HistoryRecovery()
    {
        const string job="DoBillsButcherFlesh";
        Role doctor=store.RoleByTemplate("WS_Doctor"), cook=store.RoleByTemplate("WS_Cook"), butcher=store.RoleByTemplate("WS_Butcher");
        var giver=DefDatabase<WorkGiverDef>.GetNamed(job);
        Require(giver.workType.defName=="FSFButcher","RV1 real Complex Jobs moves native butchering into FSFButcher");
        Require(cook.workTypeSnapshots["Cooking"].Contains(job) && !cook.Coverage().Contains(job),
            "RV1 unmodified saved Cook keeps butchering only through its historical snapshot");
        Pawn pawn=store.PawnsWithRole(cook.id).First(p=>p.Spawned && !p.Dead && !p.Downed && HasJob(p,job));
        RoleCommands.CreateRole("RV1 downstream butcher veto");
        Role veto=store.roles.Single(r=>r.label=="RV1 downstream butcher veto");
        RoleCommands.AddEntry(veto.id,new JobEntry(JobEntryKind.WorkGiver,job));
        RoleCommands.SetRoleBlocker(veto.id,true);
        foreach(bool downstreamVeto in new[]{false,true})
        {
            RoleCommands.AddEntry(doctor.id,new JobEntry(JobEntryKind.WorkGiver,job));
            var original=new List<RoleAssignment>{new RoleAssignment{roleId=doctor.id,pinned=true}};
            if(downstreamVeto) original.Add(new RoleAssignment{roleId=veto.id,pinned=true});
            original.Add(new RoleAssignment{roleId=butcher.id,state=AssignmentState.Disabled,pinned=true});
            RoleCommands.PasteRoleSet(pawn,original);
            Require(HasJob(pawn,job),"RV1 edited Doctor supplies actual butcher work before restore, downstream veto="+downstreamVeto);
            RoleCommands.RestoreSelected(new RestoreSelection{entriesRoleIds=new List<int>{doctor.id}});
            var restored=store.pawnSets[pawn].assignments;
            bool recovered=HasJob(pawn,job);
            bool positioned=restored.FindIndex(a=>a.roleId==cook.id)>=0
                && restored.FindIndex(a=>a.roleId==cook.id)<restored.FindIndex(a=>a.roleId==doctor.id);
            bool intent=restored.Single(a=>a.roleId==doctor.id).pinned
                && restored.Single(a=>a.roleId==butcher.id).state==AssignmentState.Disabled
                && restored.Single(a=>a.roleId==butcher.id).pinned;
            Record("RV1 restore recovered="+recovered+" correctPosition="+positioned+" intentPreserved="+intent
                +" pawn="+pawn.LabelShort+" downstreamVeto="+downstreamVeto+" assignments="
                +string.Join(",",restored.Select(a=>store.RoleById(a.roleId).label+":"+a.state)));
            // Independent normal-command control proves the old save's Cook
            // really can execute the lost job despite its changed work type.
            var control=new List<RoleAssignment>(original);
            control.Insert(0,new RoleAssignment{roleId=cook.id});
            RoleCommands.PasteRoleSet(pawn,control);
            Require(HasJob(pawn,job),"RV1 normally assigning Cook recovers native butcher work without enabling Butcher");
            Require(recovered && positioned && intent,"RV1 Restore Jobs recovers moved historical work at the old carrier position and preserves pins/disabled intent");
        }
    }
    private void Recovery()
    {
        string baseline=RoleIO.BuildXml(store);
        Role doctor=store.RoleByTemplate("WS_Doctor");
        const string job="HaulGeneral";
        var giver=DefDatabase<WorkGiverDef>.GetNamed(job);
        Pawn pawn=Find.Maps.SelectMany(m=>m.mapPawns.FreeColonistsSpawned).First(p=>!p.Dead && !p.Downed && !p.InMentalState
            && !p.WorkTypeIsDisabled(giver.workType) && !p.WorkTagIsDisabled(giver.workTags));
        var original=store.pawnSets.TryGetValue(pawn,out var set)?set.assignments.Select(a=>new RoleAssignment
            {roleId=a.roleId,state=a.state,pinned=a.pinned}).ToList():new List<RoleAssignment>();
        Role disabled=store.RoleByTemplate("WS_Grunt"), alternate=store.RoleByTemplate("WS_Hauler");
        Require(disabled!=null && alternate!=null,"WR21 native Grunt and Hauler templates exist");
        foreach(Role candidate in new[]{disabled,alternate})
        {
            RoleCommands.PasteRoleSet(pawn,new List<RoleAssignment>{new RoleAssignment{roleId=candidate.id}});
            Require(HasJob(pawn,job),"WR21 normally assigning "+candidate.label+" supplies actual HaulGeneral work");
        }
        foreach(bool inactiveCandidate in new[]{false,true})
        {
            RoleCommands.AddEntry(doctor.id,new JobEntry(JobEntryKind.WorkGiver,job));
            RoleCommands.SetRoleActiveHours(doctor.id,Role.AllHours); RoleCommands.ClearRoleLocations(doctor.id);
            RoleCommands.SetRoleActiveHours(disabled.id,inactiveCandidate?0:Role.AllHours);
            var assignments=new List<RoleAssignment>{new RoleAssignment{roleId=doctor.id,pinned=true}};
            if(!inactiveCandidate) assignments.Add(new RoleAssignment{roleId=disabled.id,state=AssignmentState.Disabled,pinned=true});
            RoleCommands.PasteRoleSet(pawn,assignments);
            Require(HasJob(pawn,job),"WR21 native pawn has executable hauling through edited Doctor before restore");
            RoleCommands.RestoreSelected(new RestoreSelection{entriesRoleIds=new List<int>{doctor.id}});
            var restored=store.pawnSets[pawn].assignments;
            Require(HasJob(pawn,job) && restored.Any(a=>a.roleId==alternate.id),"WR21 viable alternate recovers hauling when first candidate is "+(inactiveCandidate?"outside its hours":"already disabled"));
            Require(restored.FindIndex(a=>a.roleId==alternate.id)<restored.FindIndex(a=>a.roleId==doctor.id)
                && restored.Single(a=>a.roleId==doctor.id).pinned,"WR21 recovery uses old work position and retains Doctor pin");
            if(!inactiveCandidate) Require(restored.Single(a=>a.roleId==disabled.id).state==AssignmentState.Disabled
                && restored.Single(a=>a.roleId==disabled.id).pinned,"WR21 disabled Grunt and its pin remain unchanged");
            else Require(!restored.Any(a=>a.roleId==disabled.id),"WR21 ineffective hour-restricted candidate is not assigned");
            int revision=UiVersion.Current; var orders=CompiledJobOrders.NormalFor(pawn);
            RoleCommands.RestoreSelected(new RestoreSelection{entriesRoleIds=new List<int>{doctor.id}});
            Require(UiVersion.Current==revision && ReferenceEquals(orders,CompiledJobOrders.NormalFor(pawn)),"WR21 repeated restore preserves revisions and compiled identity");
            RoleCommands.SetRoleActiveHours(disabled.id,Role.AllHours);
        }
        Apply(baseline,true,new List<int>()); RoleCommands.PasteRoleSet(pawn,original);
    }
}
