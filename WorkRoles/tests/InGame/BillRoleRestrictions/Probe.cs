using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles;
using WorkRoles.Core;

namespace BillRoleReview
{
    // Regression boundary is the real bill menu and its selected restriction.
    // The copied save supplies historical jobs; Complex Jobs supplies real moves.
    // Extra composite/blocker roles are created only through player commands.
    [StaticConstructorOnStartup]
    public static class Probe
    {
        private static int frames;
        private static bool done;
        private static Dialog_BillConfig dialog;
        private static Bill_Production selectedBill;
        private static Role selectedRole;
        private static Pawn selectedWorker;
        private static bool verifiedSelection;
        private static string Root => GenFilePaths.SaveDataFolderPath;
        static Probe()
        {
            var h = new Harmony("eprime.workroles.billreviewprobe");
            h.Patch(AccessTools.Method(typeof(Root_Play), "Update"), postfix: new HarmonyMethod(typeof(Probe), nameof(Update)));
            h.Patch(typeof(Widgets).GetMethods().Single(m => m.Name == "ButtonTextDraggable" && m.GetParameters().Length == 6), prefix: new HarmonyMethod(typeof(Probe), nameof(Button)));
            h.Patch(AccessTools.Method(typeof(FloatMenuOption), nameof(FloatMenuOption.DoGUI)), prefix: new HarmonyMethod(typeof(Probe), nameof(MenuOption)));
        }
        private static void Record(string text)
        {
            File.AppendAllText(Path.Combine(Root, "bill-review-results.txt"), text + Environment.NewLine);
            Log.Message("[BillReview] " + text);
        }
        private static void Require(bool condition, string text)
        {
            if (!condition) throw new InvalidOperationException(text);
            Record("PASS " + text);
        }
        private static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.AnyEventNowOrWaiting) return;
            if (done)
            {
                if (!verifiedSelection && selectedBill != null && BillRoles.RestrictionFor(selectedBill) == selectedRole)
                {
                    verifiedSelection = true;
                    try
                    {
                        Require(selectedBill.PawnRestriction == null, "selecting Cook clears the individual-worker restriction");
                        Require(selectedBill.PawnAllowedToStartAnew(selectedWorker), "selected bill admits its active Cook holder");
                        Pawn other = PawnsFinder.AllMaps_FreeColonists.First(p => !RoleStore.Current.PawnsWithRole(selectedRole.id).Contains(p));
                        Require(!selectedBill.PawnAllowedToStartAnew(other), "selected bill rejects a colonist without Cook");
                        RoleCommands.SetAssignmentState(selectedWorker, selectedRole.id, (int)AssignmentState.Disabled);
                        Require(!selectedBill.PawnAllowedToStartAnew(selectedWorker), "disabling Cook rejects its holder immediately while paused");
                        RoleCommands.SetAssignmentState(selectedWorker, selectedRole.id, (int)AssignmentState.Enabled);
                        Require(selectedBill.PawnAllowedToStartAnew(selectedWorker), "reenabling Cook admits its holder again");
                        File.WriteAllText(Path.Combine(Root, "bill-review-stage.txt"), "SUCCESS");
                    }
                    catch (Exception e) { Record(e.ToString()); File.WriteAllText(Path.Combine(Root, "bill-review-stage.txt"), "FAILED"); }
                }
                return;
            }
            if (++frames < 45) return;
            done = true;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                AccessTools.Field(typeof(LudeonTK.EditWindow_Log), "canAutoOpen").SetValue(null, false);
                var store = RoleStore.Current;
                var cook = store.roles.Single(r => r.templateDefName == "WS_Cook");
                var giverDef = DefDatabase<WorkGiverDef>.GetNamed("DoBillsButcherFlesh");
                Require(giverDef.workType.defName == "FSFButcher", "actual Complex Jobs moved DoBillsButcherFlesh to FSFButcher");
                Require(cook.workTypeSnapshots["Cooking"].Contains(giverDef.defName), "unmodified saved Cook role remembers butchering");
                var giver = (WorkGiver_DoBill)giverDef.Worker;
                var bench = Find.Maps.SelectMany(m => m.listerThings.AllThings).OfType<Building_WorkTable>()
                    .First(b => giver.ThingIsUsableBillGiver(b) && b.BillStack.Bills.OfType<Bill_Production>().Any());
                var bill = bench.BillStack.Bills.OfType<Bill_Production>().First();
                Record("Saved bench=" + bench.Label + "; bill=" + bill.Label + "; recipe=" + bill.recipe.defName);
                var claimMethod = AccessTools.Method(typeof(CompiledJobOrders), "TryGetClaimingRole");
                Pawn worker = null;
                foreach (var pawn in store.PawnsWithRole(cook.id))
                {
                    object[] args = { pawn, giverDef.defName, -1 };
                    if ((bool)claimMethod.Invoke(null, args) && (int)args[2] == cook.id)
                    { worker = pawn; break; }
                }
                Require(worker != null, "saved Cook holder's compiled work still assigns butchering to Cook");
                Record("Worker=" + worker.LabelShort + "; role=" + cook.label + "; roleId=" + cook.id);
                Require(CompiledJobOrders.NormalFor(worker).Any(g => g.def == giverDef), "game-consumed normal work list contains actual butcher workgiver");
                var eligible = BillRoles.EligibleRoles(bill);
                Record("Eligible role labels=" + string.Join(", ", eligible.Select(r => r.label)));
                Require(eligible.Contains(cook), "saved Cook role remains selectable for its preserved butcher job");
                Require(eligible.Any(r => r.templateDefName == "WS_Butcher"), "explicit Butcher role remains selectable");
                RoleCommands.CreateRole("WR1 kitchen team");
                Role combo = store.roles.Single(r => r.label == "WR1 kitchen team");
                RoleCommands.SetRoleComposite(combo.id, true);
                RoleCommands.AddCompositeMember(combo.id, cook.id);
                Require(BillRoles.EligibleRoles(bill).Contains(combo), "composite containing Cook retains the same bill choice");
                RoleCommands.CreateRole("WR1 unrelated role");
                Role unrelated = store.roles.Single(r => r.label == "WR1 unrelated role");
                RoleCommands.AddEntry(unrelated.id, new JobEntry(JobEntryKind.WorkType, "Construction"));
                Require(!BillRoles.EligibleRoles(bill).Contains(unrelated), "unrelated work is absent from the bill menu");
                var cookingGiver = (WorkGiver_DoBill)DefDatabase<WorkGiverDef>.GetNamed("DoBillsCook").Worker;
                var cookingBench = Find.Maps.SelectMany(m => m.listerThings.AllThings).OfType<Building_WorkTable>()
                    .First(b => cookingGiver.ThingIsUsableBillGiver(b) && b.BillStack.Bills.OfType<Bill_Production>().Any());
                Require(BillRoles.EligibleRoles(cookingBench.BillStack.Bills.First()).Contains(cook), "unchanged Cooking job remains selectable");
                // Exercise mutual exclusion through the real menu click below.
                bill.SetPawnRestriction(worker);
                RoleCommands.SetBillRole(bill, -1);
                foreach (var w in Find.WindowStack.Windows.ToList()) w.Close(false);
                dialog = new Dialog_BillConfig(bill, bench.Position);
                Find.WindowStack.Add(dialog);
                var options = (IEnumerable<Widgets.DropdownMenuElement<Pawn>>)AccessTools.Method(typeof(Dialog_BillConfig), "GeneratePawnRestrictionOptions").Invoke(dialog, null);
                var labels = options.Select(o => o.option.Label).ToList();
                Record("Actual patched dropdown=" + string.Join(" | ", labels));
                string cookOption = (string)AccessTools.Property(typeof(Role), "BillOptionLabel").GetValue(cook);
                Require(labels.Contains(cookOption), "actual patched worker dropdown contains Role: Cook");
                selectedBill = bill; selectedRole = cook; selectedWorker = worker;
                File.WriteAllText(Path.Combine(Root, "bill-review-stage.txt"), "READY");
            }
            catch (Exception e)
            {
                Record(e.ToString());
                File.WriteAllText(Path.Combine(Root, "bill-review-stage.txt"), "FAILED");
            }
        }
        private static void Button(Rect rect, string label)
        {
            if (dialog == null || selectedWorker == null || Event.current.type != EventType.Repaint || label != selectedWorker.LabelShortCap.ToString()) return;
            Point("worker", rect);
        }
        private static void MenuOption(FloatMenuOption __instance, Rect rect)
        {
            if (selectedRole == null || Event.current.type != EventType.Repaint) return;
            string label = (string)AccessTools.Property(typeof(Role), "BillOptionLabel").GetValue(selectedRole);
            if (__instance.Label == label) Point("cook", rect);
        }
        private static void Point(string name, Rect rect)
        {
            Vector2 point = GUIUtility.GUIToScreenPoint(rect.center);
            point += GUIUtility.GUIToScreenPoint(Vector2.zero) * (Prefs.UIScale - 1f);
            File.WriteAllText(Path.Combine(Root, "bill-review-" + name + "-point.txt"),
                Math.Round(point.x).ToString(CultureInfo.InvariantCulture) + "," + Math.Round(point.y).ToString(CultureInfo.InvariantCulture));
        }
    }
}
