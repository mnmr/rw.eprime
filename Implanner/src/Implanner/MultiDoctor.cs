using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Implanner.Core;
using Multiplayer.API;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Implanner
{
    /// Multi-Doctor Surgery bridge (package masterpnj.multidoctor), resolved
    /// by name (no assembly reference; verified against the mod's decompiled
    /// 1.6 and 1.7 assemblies, which are identical). The mod turns an
    /// operation into a team surgery only when its bill is the mod's own
    /// BillMedicalEx; it makes those in its health-tab hook through the
    /// factory Patch_HealthCardUtility_CreateSurgeryBill.CreateBill, which
    /// keeps a plain Bill_Medical for the operations its settings exclude.
    /// Implanner calls that factory and then assigns the team with the mod's
    /// own SurgeryTeamUtility.AssignTeam (lead plus assistants, capped at the
    /// mod's doctor limit), which also restricts the bill to the lead: the
    /// game then skips the bill's skill range (Bill.PawnAllowedToStartAnew),
    /// so the lead is exempt from Implanner's doctor floor. The mod drops a
    /// lead who can no longer operate, and the floor applies again.
    /// The mod reads its exclusions and doctor limit from per-player
    /// settings and syncs nothing, so in multiplayer every mode is Solo.
    // Cache contract:
    // Owner: process.
    // Key: none.
    // Value: the mod's bill type, factory, team methods and fields, resolved
    //   once by name; immutable facts, never results.
    // Dependencies: the loaded assembly set (static per session).
    // Refresh policy: resolved once on first query.
    // Equality policy: never changes within a session.
    // Teardown: none needed; process-lifetime facts that retain no world,
    //   pawn or Unity object.
    internal static class MultiDoctor
    {
        private static bool resolved;
        private static Type? billType;
        private static MethodInfo? createBill;
        private static MethodInfo? assignTeam;
        private static MethodInfo? availableDoctors;
        private static MethodInfo? canOperate;
        private static MethodInfo? estimateSuccess;
        private static AccessTools.FieldRef<object, Pawn?>? surgeon;
        private static Type? defaultTeam;
        private static FieldInfo? defaultLead;
        private static FieldInfo? defaultAssistants;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            Type? bill = GenTypes.GetTypeInAnyAssembly("MultiDoctorSurgery.BillMedicalEx");
            if (bill == null) return;
            Type? factory = GenTypes.GetTypeInAnyAssembly(
                "MultiDoctorSurgery.Patches.Patch_HealthCardUtility_CreateSurgeryBill");
            Type? utility = GenTypes.GetTypeInAnyAssembly("MultiDoctorSurgery.SurgeryTeamUtility");
            Type? team = GenTypes.GetTypeInAnyAssembly("MultiDoctorSurgery.DefaultSurgeryTeamComponent");
            MethodInfo? create = factory == null ? null : AccessTools.Method(factory, "CreateBill",
                new[] { typeof(RecipeDef), typeof(List<Thing>), typeof(Pawn) });
            MethodInfo? assign = utility == null ? null : AccessTools.Method(utility, "AssignTeam",
                new[] { bill, typeof(Pawn), typeof(IEnumerable<Pawn>) });
            MethodInfo? available = utility == null ? null : AccessTools.Method(utility,
                "AvailableDoctors", new[] { typeof(Pawn) });
            MethodInfo? operate = utility == null ? null : AccessTools.Method(utility, "CanOperate",
                new[] { typeof(Pawn), typeof(Pawn), typeof(RecipeDef) });
            MethodInfo? success = utility == null ? null : AccessTools.Method(utility,
                "EstimateSuccess", new[] { typeof(IEnumerable<Pawn>), typeof(RecipeDef) });
            FieldInfo? lead = AccessTools.Field(bill, "surgeon");
            FieldInfo? teamLead = team == null ? null : AccessTools.Field(team, "defaultLeadSurgeon");
            FieldInfo? teamAssistants = team == null ? null : AccessTools.Field(team, "defaultAssistants");
            if (create == null || create.ReturnType != typeof(Bill_Medical)
                || assign == null || available == null || operate == null || success == null
                || lead == null || lead.FieldType != typeof(Pawn)
                || teamLead == null || teamAssistants == null)
            {
                Log.Warning("[Implanner] Multi-Doctor Surgery was found but its surgery team "
                    + "methods were not; Implanner operations stay solo.");
                return;
            }
            billType = bill;
            createBill = create;
            assignTeam = assign;
            availableDoctors = available;
            canOperate = operate;
            estimateSuccess = success;
            surgeon = AccessTools.FieldRefAccess<Pawn?>(bill, "surgeon");
            defaultTeam = team;
            defaultLead = teamLead;
            defaultAssistants = teamAssistants;
        }

        /// Whether Multi-Doctor Surgery is loaded with every member the
        /// bridge needs.
        internal static bool Loaded
        {
            get
            {
                Resolve();
                return billType != null;
            }
        }

        /// The surgery team mode automation applies: Solo without the mod,
        /// in multiplayer, or when the player chose it.
        internal static SurgeryTeamMode ModeFor(PlannerModel model) =>
            model.SurgeryTeams == SurgeryTeamMode.Solo || !Loaded || MP.IsInMultiplayer
                ? SurgeryTeamMode.Solo
                : model.SurgeryTeams;

        /// A new operation bill: the mod's own bill from its factory (a
        /// plain one for the operations its settings exclude) in a team
        /// mode, else the plain bill the health tab makes.
        internal static Bill_Medical NewBill(SurgeryTeamMode mode, Pawn pawn, RecipeDef recipe)
        {
            if (mode != SurgeryTeamMode.Solo && Loaded
                && createBill!.Invoke(null, new object?[] { recipe, null, pawn }) is Bill_Medical bill)
                return bill;
            return new Bill_Medical(recipe, null);
        }

        /// Assigns the team for a bill already on the patient's stack. Does
        /// nothing for a plain bill (Solo, or an operation the mod's
        /// settings exclude). When no valid lead exists the bill stays
        /// without a team: the first doctor to take it operates alone.
        internal static void AssignTeam(SurgeryTeamMode mode, Bill_Medical bill, Pawn patient)
        {
            if (mode == SurgeryTeamMode.Solo || !Loaded || !billType!.IsInstanceOfType(bill)) return;
            Pawn? lead;
            IEnumerable<Pawn>? assistants;
            if (mode == SurgeryTeamMode.DefaultTeam)
            {
                WorldComponent? team = Find.World?.GetComponent(defaultTeam!);
                lead = team == null ? null : defaultLead!.GetValue(team) as Pawn;
                assistants = team == null ? null : defaultAssistants!.GetValue(team) as List<Pawn>;
            }
            else
                lead = BestTeam(bill.recipe, patient, out assistants);
            if (lead != null)
                assignTeam!.Invoke(null, new object?[] { bill, lead, assistants });
        }

        /// The best team on the patient's map, from the doctors the mod
        /// itself offers: the lead is the most skilled doctor able to
        /// operate (the game's skill level, a mech's fixed level), the
        /// assistants follow by the success bonus the mod gives each, ties
        /// by pawn id. AssignTeam trims the assistants to the mod's limit.
        private static Pawn? BestTeam(RecipeDef recipe, Pawn patient, out IEnumerable<Pawn>? assistants)
        {
            assistants = null;
            if (!(availableDoctors!.Invoke(null, new object[] { patient }) is List<Pawn> doctors)
                || doctors.Count == 0)
                return null;
            SkillDef skill = recipe.workSkill ?? SkillDefOf.Medicine;
            Pawn? lead = null;
            int leadLevel = -1;
            for (int i = 0; i < doctors.Count; i++)
            {
                Pawn doctor = doctors[i];
                int level = doctor.skills != null ? doctor.skills.GetSkill(skill).Level
                    : doctor.RaceProps.mechFixedSkillLevel;
                if ((level > leadLevel || (level == leadLevel
                        && doctor.thingIDNumber < lead!.thingIDNumber))
                    && canOperate!.Invoke(null, new object[] { doctor, patient, recipe }) is true)
                {
                    lead = doctor;
                    leadLevel = level;
                }
            }
            if (lead == null) return null;
            var ranked = new List<(Pawn Pawn, float Bonus)>(doctors.Count);
            var one = new Pawn[1];
            for (int i = 0; i < doctors.Count; i++)
            {
                if (doctors[i] == lead) continue;
                one[0] = doctors[i];
                ranked.Add((doctors[i],
                    estimateSuccess!.Invoke(null, new object[] { one, recipe }) is float bonus
                        ? bonus : 0f));
            }
            ranked.Sort(static (a, b) => a.Bonus != b.Bonus
                ? b.Bonus.CompareTo(a.Bonus)
                : a.Pawn.thingIDNumber.CompareTo(b.Pawn.thingIDNumber));
            var team = new List<Pawn>(ranked.Count);
            for (int i = 0; i < ranked.Count; i++) team.Add(ranked[i].Pawn);
            assistants = team;
            return lead;
        }

        /// Whether the bill is a team surgery with a lead surgeon, who
        /// alone may start it whatever the bill's skill range says.
        /// Allocation-free; false without the mod.
        internal static bool HasLead(Bill bill)
        {
            if (!Loaded || !billType!.IsInstanceOfType(bill)) return false;
            return surgeon!(bill) != null;
        }
    }
}
