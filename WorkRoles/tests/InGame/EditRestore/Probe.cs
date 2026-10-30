using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.UI;

namespace PersistenceReview
{
    [StaticConstructorOnStartup]
    public static class RuntimeProbe
    {
        private static int frames;
        private static bool done;
        private static Dialog_RestorePreview preview;
        private static RoleStore store;
        private static readonly List<string> results = new List<string>();
        private static string Root => GenFilePaths.SaveDataFolderPath;
        static RuntimeProbe()
        {
            new Harmony("eprime.workroles.persistencereviewprobe").Patch(AccessTools.Method(typeof(Root_Play), "Update"),
                postfix: new HarmonyMethod(typeof(RuntimeProbe), nameof(Update)));
        }
        private static void Line(string value) { results.Add(value); Log.Message("[PersistenceReview] " + value); File.WriteAllLines(Path.Combine(Root,"persistence-review-results.txt"),results); }
        private static void Check(bool condition,string value) { if(!condition) throw new InvalidOperationException(value); Line("CONTROL PASS " + value); }
        private static string Hex(Color c) => new ColorRgb(c.r,c.g,c.b).Hex();
        private static Role Named(string name) => store.roles.Single(r=>r.label==name);
        private static string Names(IEnumerable<int> ids) => string.Join(" > ",ids.Select(id=>store.RoleById(id)?.label ?? ("missing:"+id)));
        private static PathView CapturePath(Role role) => new PathView{Id=role.id,RoleIds=role.trainingRoleIds.ToList(),BandMins=role.trainingMins.ToList(),BandMaxes=role.trainingMaxes.ToList()};
        private static PathView Rebind(PathView path,int id) => new PathView{Id=id,RoleIds=path.RoleIds.Select(x=>x==path.Id?id:x).ToList(),BandMins=path.BandMins.ToList(),BandMaxes=path.BandMaxes.ToList()};
        private static void Update()
        {
            if(preview!=null)
            {
                if(!File.Exists(Path.Combine(Root,"persistence-review-continue.txt"))) return;
                preview.Close(); preview=null;
                File.WriteAllText(Path.Combine(Root,"persistence-review-stage.txt"),"SUCCESS");
                Line("SUCCESS WR6-WR10: palette import, duplicated training, single-pass restore, native job restoration and effective coverage");
                return;
            }
            if(done || Current.ProgramState!=ProgramState.Playing || Find.CurrentMap==null || LongEventHandler.AnyEventNowOrWaiting || ++frames<20) return;
            done=true;
            try
            {
                Find.TickManager.CurTimeSpeed=TimeSpeed.Paused;
                store=RoleStore.Current;
                File.WriteAllText(Path.Combine(Root,"persistence-review-before.xml"),RoleIO.BuildXml(store));
                Palette(); CopyTraining(); RestoreTraining(); RestoreComposite(); RestoreBlocker();
                Role owner=store.RoleByTemplate("WS_Handler");
                Check(owner!=null && owner.trainingRoleIds.Count>1,"WR8 restored training remains published after unrelated restore commands");
                RoleCommands.DeleteRole(owner.id);
                var rows=Seeding.ComputeRestoreItems().Where(i=>i.templateDef=="WS_Handler" || i.pathDef=="WS_Handler").ToList();
                Check(rows.Count==2 && rows.All(i=>!string.IsNullOrEmpty(i.label)),"WR8 real preview communicates separate missing role and training choices");
                preview=new Dialog_RestorePreview(rows);
                Find.WindowStack.Add(preview);
                File.WriteAllText(Path.Combine(Root,"persistence-review-stage.txt"),"restore-preview");
            }
            catch(Exception e)
            {
                Line("FAILED " + e); File.WriteAllText(Path.Combine(Root,"persistence-review-stage.txt"),"FAILED " + e);
            }
        }
        private static void Palette()
        {
            Color a=new Color(18f/255f,52f/255f,86f/255f),b=new Color(101f/255f,67f/255f,33f/255f);
            RoleCommands.CreateRole("Review palette A"); RoleCommands.CreateRole("Review palette B");
            RoleCommands.CreateRole("Review palette untouched");
            Role ra=Named("Review palette A"),rb=Named("Review palette B"),untouched=Named("Review palette untouched");
            RoleCommands.SetCustomSwatch(0,a); RoleCommands.SetCustomSwatch(1,b);
            Color c=new Color(0x87/255f,0xA9/255f,0xCB/255f);
            RoleCommands.SetCustomSwatch(2,c); RoleCommands.SetRoleColor(untouched.id,c);
            AccessTools.Method(typeof(RoleStore),"SyncSwatchNames").Invoke(store,null);
            Color temporary=new Color(0x31/255f,0x79/255f,0xA5/255f);
            RoleCommands.SetRoleColor(ra.id,a); RoleCommands.SetRoleColor(rb.id,b);
            // Three distinct picks mirror the UI without ever selecting an
            // existing other-slot color (the UI otherwise clears duplicates).
            RoleCommands.SetCustomSwatch(0,temporary); RoleCommands.SetRoleColor(ra.id,temporary);
            RoleCommands.SetCustomSwatch(1,a); RoleCommands.SetRoleColor(rb.id,a);
            RoleCommands.SetCustomSwatch(0,b); RoleCommands.SetRoleColor(ra.id,b);
            string xml=RoleIO.BuildXml(store);
            RoleFileDocument exported=RoleIO.Parse(xml);
            string n0=store.customSwatchNames[0],n1=store.customSwatchNames[1];
            Check(exported.error==null && exported.palette.Count(p=>p.name==n0 && p.color.Hex()=="#654321")==1
                && exported.palette.Count(p=>p.name==n1 && p.color.Hex()=="#123456")==1,"P1 real export retains both distinct swapped named slots");
            Check(exported.roles.Single(r=>r.label==ra.label).colorRef==n0 && exported.roles.Single(r=>r.label==rb.label).colorRef==n1,
                "P1 real source roles bind the expected named custom slots");
            File.WriteAllText(Path.Combine(Root,"persistence-review-swapped-palette.xml"),xml);
            foreach(bool overwrite in new[]{false,true})
            {
                RoleCommands.SetCustomSwatch(0,a); RoleCommands.SetCustomSwatch(1,b);
                RoleCommands.SetRoleColor(ra.id,a); RoleCommands.SetRoleColor(rb.id,b);
                RoleCommands.ApplyImport(new ImportSelection{xml=xml,palette=true,paletteOverwrite=overwrite,paletteRows=new List<int>{0,1}});
                Check(Hex(ra.color)=="#654321" && Hex(rb.color)=="#123456",
                    "WR6 "+(overwrite?"Overwrite":"Merge")+" swaps each named role color exactly once");
                Check(Hex(untouched.color)=="#87a9cb","WR6 unrelated named colors stay unchanged");
            }
            RoleCommands.DeleteRole(ra.id); RoleCommands.DeleteRole(rb.id);
            RoleCommands.DeleteRole(untouched.id);
        }
        private static void CopyTraining()
        {
            Role[] candidates=store.roles.Where(r=>r.trainingRoleIds.Count>1 && !r.composite).ToArray();
            Check(candidates.Length>0,"native save contains trained source roles");
            bool witnessed=false;
            foreach(Role source in candidates)
            {
                PathView expected=CapturePath(source);
                RoleCommands.DuplicateRole(source.id,"Review copied training");
                Role copy=Named("Review copied training");
                Check(copy.entries.SequenceEqual(source.entries),"P2 copied jobs match source");
                PathView rebound=Rebind(expected,copy.id);
                Check(copy.trainingRoleIds.SequenceEqual(rebound.RoleIds) && copy.trainingMins.SequenceEqual(rebound.BandMins)
                    && copy.trainingMaxes.SequenceEqual(rebound.BandMaxes),"WR7 duplicate keeps training bands and rebinds its owner");
                Check(!ReferenceEquals(source.trainingRoleIds,copy.trainingRoleIds)
                    && !ReferenceEquals(source.trainingMins,copy.trainingMins) && !ReferenceEquals(source.trainingMaxes,copy.trainingMaxes),
                    "WR7 duplicate independently owns every training collection");
                witnessed=TrainingWitness(copy,Rebind(expected,copy.id),"P2");
                Check(source.trainingRoleIds.SequenceEqual(expected.RoleIds) && source.trainingMins.SequenceEqual(expected.BandMins)
                    && source.trainingMaxes.SequenceEqual(expected.BandMaxes),"WR7 editing copied training keeps source unchanged");
                RoleCommands.DeleteRole(copy.id);
                if(witnessed) break;
            }
            Check(witnessed,"WR7 copied training chooses a real native trainee instead of the skilled owner");
        }
        // The real adapter captures complete native pawn, automatic-role,
        // training-work, ordering, hunter, blocker and mechanical context.
        // The counterfactual is built through the same adapter after the sole
        // corrective mutation: restoring the missing path via its normal command.
        // A normal demand edit exposes trainees without simplifying pawn facts.
        private static bool TrainingWitness(Role target,PathView expected,string id)
        {
            int priorMin=target.colonyMin; bool excluded=target.excludedFromRecommendations;
            PathView priorPath=CapturePath(target);
            RoleCommands.SetRoleColonyMinimum(target.id,30); RoleCommands.SetRoleRecommendationExcluded(target.id,false);
            try
            {
                var pawns=PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.Where(p=>p.IsColonist && !p.Dead && p.skills!=null).ToList();
                RoleCommands.SetRoleTraining(target.id,new List<int>(),new List<int>(),new List<int>());
                ColonyView colony=RecsAdapter.BuildColonyView(store,pawns);
                RecommendationPlan actual=RecommendationPlan.Build(colony,store.recommendationTuning);
                IReadOnlyList<PawnFixTarget> actualTargets=ColonyFixPlanner.Build(colony,actual);
                RoleCommands.SetRoleTraining(target.id,expected.RoleIds,expected.BandMins,expected.BandMaxes);
                Check(target.trainingRoleIds.SequenceEqual(expected.RoleIds),id+" ordinary training command accepts path-preserving counterfactual");
                ColonyView correctedColony=RecsAdapter.BuildColonyView(store,pawns);
                RecommendationPlan corrected=RecommendationPlan.Build(correctedColony,store.recommendationTuning);
                IReadOnlyList<PawnFixTarget> correctTargets=ColonyFixPlanner.Build(correctedColony,corrected);
                for(int p=0;p<pawns.Count;p++)
                {
                    var actualIds=actualTargets[p].RoleIds;
                    var correctIds=correctTargets[p].RoleIds;
                    bool active=Enumerable.Range(0,corrected.PathCountAt(p)).Any(i=>corrected.PathAt(p,i)==target.id && corrected.PathActivatedAt(p,i));
                    if(!active || actualIds.SequenceEqual(correctIds)) continue;
                    Check(!correctIds.Contains(target.id) && actualIds.Contains(target.id),
                        id+" native trainee "+pawns[p].LabelShort+" avoids the skilled owner when its copied/restored training applies");
                    Line(id+" full native colony final="+Names(correctIds)+"; untrained control="+Names(actualIds));
                    return true;
                }
                return false;
            }
            finally { RoleCommands.SetRoleTraining(target.id,priorPath.RoleIds,priorPath.BandMins,priorPath.BandMaxes); RoleCommands.SetRoleColonyMinimum(target.id,priorMin); RoleCommands.SetRoleRecommendationExcluded(target.id,excluded); }
        }
        private static void RestoreTraining()
        {
            Role owner=store.roles.First(r=>r.trainingRoleIds.Count>1 && r.templateDefName!=null && DefDatabase<RoleDef>.GetNamedSilentFail(r.templateDefName)?.tuning?.training?.Count>1);
            string template=owner.templateDefName,label=owner.label; PathView prior=CapturePath(owner);
            RoleCommands.DeleteRole(owner.id);
            List<Seeding.RestoreItem> items=Seeding.ComputeRestoreItems();
            Check(items.Any(i=>i.templateDef==template),"P3 missing trained template appears in real restore preview");
            Check(items.Any(i=>i.pathDef==template),"WR8 initial preview includes the missing owner's training");
            RoleCommands.RestoreSelected(new RestoreSelection{pathDefs=new List<string>{template}});
            Check(store.RoleByTemplate(template)==null,"WR8 selecting training alone does not recreate an unselected owner");
            RoleCommands.RestoreSelected(new RestoreSelection{templateDefs=new List<string>{template}});
            Check(store.RoleByTemplate(template).trainingRoleIds.Count==0,"WR8 selecting only the owner does not apply an unselected training change");
            RoleCommands.DeleteRole(store.RoleByTemplate(template).id);
            items=Seeding.ComputeRestoreItems();
            RoleCommands.RestoreSelected(Selection(items.Where(i=>i.templateDef==template || i.pathDef==template).ToList()));
            Role restored=store.RoleByTemplate(template);
            Check(restored!=null,"P3 actual restore selection recreated native role");
            PathView expected=Rebind(prior,restored.id);
            Check(restored.trainingRoleIds.SequenceEqual(expected.RoleIds) && restored.trainingMins.SequenceEqual(expected.BandMins)
                && restored.trainingMaxes.SequenceEqual(expected.BandMaxes),"WR8 one selected restore recreates the native owner with its original training");
            Check(!Seeding.ComputeRestoreItems().Any(i=>i.pathDef==template),"WR8 completed training needs no second restore");
            Check(TrainingWitness(restored,expected,"P3"),"WR8 restored training chooses a real native trainee instead of the skilled owner");
        }
        private static RestoreSelection Selection(List<Seeding.RestoreItem> items) => new RestoreSelection
        {
            templateDefs=items.Select(i=>i.templateDef).OfType<string>().ToList(),workTypes=items.Select(i=>i.workType).OfType<string>().ToList(),
            backfillRoleIds=items.Where(i=>i.backfillRoleId>=0).Select(i=>i.backfillRoleId).ToList(),pathDefs=items.Select(i=>i.pathDef).OfType<string>().ToList(),
            groupRoleIds=items.Where(i=>i.groupRoleId>=0).Select(i=>i.groupRoleId).ToList(),colorRoleIds=items.Where(i=>i.colorRoleId>=0).Select(i=>i.colorRoleId).ToList(),
            holderRoleIds=items.Where(i=>i.holderRoleId>=0).Select(i=>i.holderRoleId).ToList(),entriesRoleIds=items.Where(i=>i.entriesRoleId>=0).Select(i=>i.entriesRoleId).ToList(),
            paletteSnapRoleIds=items.Where(i=>i.paletteSnapRoleId>=0).Select(i=>i.paletteSnapRoleId).ToList(),defaultExclusions=items.Any(i=>i.defaultExclusions),recommendationOrder=items.Any(i=>i.recommendationOrder),
        };
        private static void RestoreComposite()
        {
            Role role=store.roles.First(r=>r.templateDefName=="WS_Doctor" && !r.composite);
            Role member=store.roles.First(r=>r.id!=role.id && !r.composite && !r.blocker && r.enabled && r.activeHours==Role.AllHours && r.locationTokens.Count==0 && r.entries.Any(e=>e.Kind==JobEntryKind.WorkType && e.DefName=="Hauling"));
            Pawn pawn=PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.First(p=>p.IsColonist && p.workSettings!=null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Doctor));
            RoleCommands.PasteRoleSet(pawn,new List<RoleAssignment>{new RoleAssignment{roleId=role.id}});
            string[] reference=CompiledJobOrders.CombinedFor(pawn).Select(g=>g.def.defName).ToArray();
            Check(reference.Length>0,"P4 native Doctor gives executable work for "+pawn.LabelShort);
            RoleCommands.SetRoleComposite(role.id,true); RoleCommands.AddCompositeMember(role.id,member.id);
            Check(role.composite && role.memberRoleIds.Contains(member.id),"P4 real commands converted Doctor to hauling composite");
            var items=Seeding.ComputeRestoreItems().Where(i=>i.entriesRoleId==role.id).ToList();
            Check(items.Count==1,"P4 real restore preview offers Doctor default jobs");
            string[] before=CompiledJobOrders.CombinedFor(pawn).Select(g=>g.def.defName).ToArray();
            RoleCommands.RestoreSelected(Selection(items));
            string[] after=CompiledJobOrders.CombinedFor(pawn).Select(g=>g.def.defName).ToArray();
            string[] emergency=CompiledJobOrders.EmergencyFor(pawn).Select(g=>g.def.defName).ToArray();
            Check(!role.composite && role.memberRoleIds.Count==0,"WR9 default jobs restore returns the template to an ordinary role");
            Check(reference.All(g=>after.Contains(g) || emergency.Contains(g)),"WR9 native pawn again receives every original executable Doctor job");
            Check(!Seeding.ComputeRestoreItems().Any(i=>i.entriesRoleId==role.id),"WR9 completed native job restoration disappears from preview");
            int revision=UiVersion.Current;
            RoleCommands.RestoreSelected(Selection(items));
            Check(UiVersion.Current==revision,"WR9 repeated default-job restoration publishes no revisions");
        }
        private static void RestoreBlocker()
        {
            Role role=store.RoleByTemplate("WS_Doctor");
            RoleCommands.SetRoleComposite(role.id,false);
            var jobs=Seeding.ComputeRestoreItems().Where(i=>i.entriesRoleId==role.id).ToList();
            Check(jobs.Count==0,"WR10 Doctor begins with restored ordinary defaults");
            RoleCommands.RestoreSelected(Selection(jobs));
            RoleCommands.SetRoleBlocker(role.id,true);
            RoleCommands.AddEntry(role.id,new JobEntry(JobEntryKind.WorkType,"Construction"));
            Check(role.blocker && role.entries.Any(e=>e.DefName=="Construction"),"P5 normal editor commands create Construction veto on seeded Doctor");
            Role hauler=store.RoleByTemplate("WS_Hauler");
            Pawn pawn=PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive.First(p=>p.IsColonist && p.workSettings!=null && !p.WorkTypeIsDisabled(WorkTypeDefOf.Construction));
            RoleCommands.PasteRoleSet(pawn,new List<RoleAssignment>{new RoleAssignment{roleId=role.id},new RoleAssignment{roleId=hauler.id}});
            int before=CompiledJobOrders.PriorityFor(pawn,WorkTypeDefOf.Construction);
            string prior=Names(store.pawnSets[pawn].assignments.Select(a=>a.roleId));
            var restore=Seeding.ComputeRestoreItems().Where(i=>i.entriesRoleId==role.id).ToList();
            Check(restore.Count==1 && before==0,"P5 no enabled Construction before real offered default-job reset");
            RoleCommands.RestoreSelected(Selection(restore));
            int after=CompiledJobOrders.PriorityFor(pawn,WorkTypeDefOf.Construction);
            Check(after==0 && Names(store.pawnSets[pawn].assignments.Select(a=>a.roleId))==prior,
                "WR10 restoring a veto never assigns Builder or invents executable Construction work");
            RoleCommands.SetRoleBlocker(role.id,false);
            RoleCommands.AddEntry(role.id,new JobEntry(JobEntryKind.WorkType,"Construction"));
            Check(CompiledJobOrders.PriorityFor(pawn,WorkTypeDefOf.Construction)>0,"WR10 ordinary edited Doctor positively supplies Construction");
            RoleCommands.RestoreSelected(Selection(Seeding.ComputeRestoreItems().Where(i=>i.entriesRoleId==role.id).ToList()));
            Check(CompiledJobOrders.PriorityFor(pawn,WorkTypeDefOf.Construction)>0 && store.pawnSets[pawn].assignments.Any(a=>a.roleId!=role.id && a.roleId!=hauler.id),
                "WR10 ordinary executable Construction is still recovered by a template role");
            RoleCommands.AddEntry(role.id,new JobEntry(JobEntryKind.WorkType,"Construction"));
            foreach(int mode in new[]{0,1})
            {
                RoleCommands.PasteRoleSet(pawn,new List<RoleAssignment>{new RoleAssignment{roleId=role.id},new RoleAssignment{roleId=hauler.id}});
                if(mode==0) RoleCommands.SetAssignmentState(pawn,role.id,(int)AssignmentState.Disabled);
                else RoleCommands.SetRoleActiveHours(role.id,0);
                string assignments=Names(store.pawnSets[pawn].assignments.Select(a=>a.roleId));
                Check(CompiledJobOrders.PriorityFor(pawn,WorkTypeDefOf.Construction)==0,"WR10 inactive assignment/rule supplies no Construction");
                RoleCommands.RestoreSelected(Selection(Seeding.ComputeRestoreItems().Where(i=>i.entriesRoleId==role.id).ToList()));
                Check(CompiledJobOrders.PriorityFor(pawn,WorkTypeDefOf.Construction)==0
                    && Names(store.pawnSets[pawn].assignments.Select(a=>a.roleId))==assignments,
                    "WR10 inactive assignment/rule creates no recovery role during restoration");
                RoleCommands.SetRoleActiveHours(role.id,Role.AllHours);
                if(mode==0) RoleCommands.AddEntry(role.id,new JobEntry(JobEntryKind.WorkType,"Construction"));
            }
        }
    }
}
