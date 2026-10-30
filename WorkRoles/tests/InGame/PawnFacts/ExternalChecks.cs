using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles;
using WorkRoles.Core.Recs;

namespace WorkRolesPawnFacts;

public sealed partial class Probe
{
    private sealed class FactsSession : IDisposable
    {
        internal readonly List<Pawn> Pawns;
        internal readonly object Stats, Producer, Getter;
        internal FactsSession()
        {
            Pawns = (List<Pawn>)Call(ModType("AutoOptimizer"), "SimulationColonists", Find.CurrentMap);
            Stats = Activator.CreateInstance(ModType("UI.ColonistStatsState"), true);
            Call(Stats, "Reset", Pawns);
            Getter = Delegate.CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(Pawn), ModType("PawnExternalSnapshot")),
                Stats, Stats.GetType().GetMethod("ExternalSnapshot", All));
            Producer = Activator.CreateInstance(ModType("UI.ColonistRecommendationProducer"), true);
        }
        internal RecommendationSnapshot Plan() => (RecommendationSnapshot)Call(Producer, "For", RoleStore.Current, Pawns, Getter, null);
        internal void Refresh()
        {
            object[] args = { Pawns, false };
            if ((bool)Call(Stats, "RefreshExternalSnapshot", args)) Call(Producer, "InvalidateExternalFacts");
        }
        public void Dispose() => Call(Stats, "ReleaseSnapshots");
    }
    private void ExternalChecks()
    {
        Require(ModsConfig.AnomalyActive && ModsConfig.BiotechActive, "WR16 native Anomaly and Biotech active");
        SkillHediff(); Mechlink(); Equipment();
        Require(Find.TickManager.Paused, "WR16 WR17 native changes and refreshed recommendations verified while paused");
    }
    private void Warm(FactsSession session, Pawn pawn, string label)
    {
        object snapshot = Snapshot(session.Stats, pawn), skills = SkillIdentity(session.Stats, pawn);
        RecommendationSnapshot plan = session.Plan();
        int factual = Revision("FactRevisions", pawn), context = Revision("ContextRevisions", pawn), work = Revision("WorkEvidenceRevisions", pawn);
        for (int i = 0; i < 8; i++)
        {
            session.Refresh();
            Require(ReferenceEquals(snapshot, Snapshot(session.Stats, pawn)) && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn))
                && ReferenceEquals(plan, session.Plan()), label + " unchanged read " + i + " reuses factual, skill and recommendation snapshots");
        }
        Require(factual == Revision("FactRevisions", pawn) && context == Revision("ContextRevisions", pawn) && work == Revision("WorkEvidenceRevisions", pawn),
            label + " warm reads preserve narrow revisions");
    }
    private void SkillHediff()
    {
        using var session = new FactsSession();
        Pawn pawn = session.Pawns.Single(p => p.LabelShort == "Hysott");
        Require(!pawn.health.hediffSet.HasHediff(HediffDefOf.Inhumanized) && pawn.skills.GetSkill(SkillDefOf.Animals).Level == 16,
            "WR16 native Hysott starts Animals 16 without Inhumanized");
        int index = session.Pawns.IndexOf(pawn), handler = store.RoleByTemplate("WS_Handler").id, herder = store.RoleByTemplate("WS_Herder").id;
        Require(Has(session.Plan().Plan, index, handler) && !Has(session.Plan().Plan, index, herder), "WR16 initial training recommendation chooses Handler over Herder");
        Warm(session, pawn, "WR16 baseline");
        object before = Snapshot(session.Stats, pawn), beforeSkills = SkillIdentity(session.Stats, pawn);
        int beforeRevision = Revision("FactRevisions", pawn);
        Hediff added = null;
        try
        {
            // Same native construction as a humanity break, including the
            // aptitude supplied by the real DLC definition.
            added = pawn.health.AddHediff(HediffDefOf.Inhumanized);
            session.Refresh();
            object after = Snapshot(session.Stats, pawn);
            Require(pawn.skills.GetSkill(SkillDefOf.Animals).Level == 4 && Facts(after).SkillLevels["Animals"] == 4,
                "WR16 native Inhumanized Animals 16-to-4 immediately reaches actual cached facts");
            Require(!ReferenceEquals(before, after) && !ReferenceEquals(beforeSkills, SkillIdentity(session.Stats, pawn))
                && Revision("FactRevisions", pawn) != beforeRevision, "WR16 aptitude membership replaces skill capture and factual revision");
            Require(!Has(session.Plan().Plan, index, handler) && Has(session.Plan().Plan, index, herder),
                "WR16 actual cached recommendation replaces Handler with lower-band Herder");
            object mergedSnapshot = Snapshot(session.Stats, pawn), mergedSkills = SkillIdentity(session.Stats, pawn);
            int mergedRevision = Revision("FactRevisions", pawn);
            pawn.health.AddHediff(HediffDefOf.Inhumanized);
            session.Refresh();
            Require(pawn.health.hediffSet.hediffs.Count(h => h.def == HediffDefOf.Inhumanized) == 1
                && ReferenceEquals(mergedSnapshot, Snapshot(session.Stats, pawn)) && ReferenceEquals(mergedSkills, SkillIdentity(session.Stats, pawn))
                && mergedRevision == Revision("FactRevisions", pawn), "WR16 native duplicate hediff merge preserves membership, snapshots and factual revision");
        }
        finally { if (added != null) pawn.health.RemoveHediff(added); }
        session.Refresh();
        Require(Facts(Snapshot(session.Stats, pawn)).SkillLevels["Animals"] == 16
            && Has(session.Plan().Plan, index, handler) && !Has(session.Plan().Plan, index, herder),
            "WR16 native removal restores Animals 16 and Handler recommendation");
        object stable = Snapshot(session.Stats, pawn), skills = SkillIdentity(session.Stats, pawn);
        int fact = Revision("FactRevisions", pawn), context = Revision("ContextRevisions", pawn), evidence = Revision("WorkEvidenceRevisions", pawn);
        // Use the same torso attachment as the native implant recipe. It has
        // no skill aptitude, capacity or work restriction.
        Hediff harmless = pawn.health.AddHediff(HediffDefOf.LoveEnhancer, pawn.RaceProps.body.corePart);
        session.Refresh();
        Require(ReferenceEquals(stable, Snapshot(session.Stats, pawn)) && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn))
            && fact == Revision("FactRevisions", pawn) && context == Revision("ContextRevisions", pawn) && evidence == Revision("WorkEvidenceRevisions", pawn),
            "WR16 unrelated native health addition preserves factual, skill and evidence caches");
        pawn.health.RemoveHediff(harmless); session.Refresh();
        Require(ReferenceEquals(stable, Snapshot(session.Stats, pawn)) && fact == Revision("FactRevisions", pawn)
            && context == Revision("ContextRevisions", pawn), "WR16 unrelated native health removal preserves cached facts and revisions");
    }
    private void Mechlink()
    {
        Role role = store.RoleByTemplate("WS_Mechanitor");
        if (role == null)
        {
            RoleCommands.RestoreSelected(new RestoreSelection { templateDefs = new List<string> { "WS_Mechanitor" } });
            role = store.RoleByTemplate("WS_Mechanitor");
        }
        using var session = new FactsSession();
        Pawn pawn = session.Pawns.Single(p => p.LabelShort == "Hysott");
        int index = session.Pawns.IndexOf(pawn);
        Require(!MechanitorUtility.IsMechanitor(pawn) && !Facts(Snapshot(session.Stats, pawn)).IsMechanitor
            && !Has(session.Plan().Plan, index, role.id), "WR16 Hysott initially lacks native and cached mechanitor qualification");
        object before = Snapshot(session.Stats, pawn), skills = SkillIdentity(session.Stats, pawn);
        int fact = Revision("FactRevisions", pawn), context = Revision("ContextRevisions", pawn);
        Hediff added = null;
        try
        {
            added = pawn.health.AddHediff(HediffDefOf.MechlinkImplant, pawn.health.hediffSet.GetBrain());
            session.Refresh();
            Require(MechanitorUtility.IsMechanitor(pawn) && Facts(Snapshot(session.Stats, pawn)).IsMechanitor
                && Has(session.Plan().Plan, index, role.id), "WR16 native mechlink immediately publishes mechanitor facts and newly qualified recommendation");
            Require(!ReferenceEquals(before, Snapshot(session.Stats, pawn)) && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn))
                && fact == Revision("FactRevisions", pawn) && context != Revision("ContextRevisions", pawn),
                "WR16 mechlink changes only non-skill context and retains shared skill capture");
        }
        finally { if (added != null) pawn.health.RemoveHediff(added); }
        session.Refresh();
        Require(!MechanitorUtility.IsMechanitor(pawn) && !Facts(Snapshot(session.Stats, pawn)).IsMechanitor
            && !Has(session.Plan().Plan, index, role.id) && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn))
            && fact == Revision("FactRevisions", pawn), "WR16 native mechlink removal restores recommendation without recapturing skills");
    }
    private void Equipment()
    {
        using var session = new FactsSession();
        Pawn pawn = session.Pawns.Single(p => p.LabelShort == "Flow");
        int index = session.Pawns.IndexOf(pawn), hunter = store.RoleByTemplate("WS_Hunter").id;
        ThingWithComps weapon = pawn.equipment.Primary;
        Require(weapon != null && weapon.def.IsRangedWeapon && Has(session.Plan().Plan, index, hunter),
            "WR17 native Flow ranged primary starts with Hunter recommendation");
        Warm(session, pawn, "WR17 baseline");
        object before = Snapshot(session.Stats, pawn), skills = SkillIdentity(session.Stats, pawn);
        int factual = Revision("FactRevisions", pawn), context = Revision("ContextRevisions", pawn);
        try
        {
            Record("EXPECTED native rejected-existing-primary AddEquipment logs one error; primary and ownership must stay unchanged");
            pawn.equipment.AddEquipment(weapon); session.Refresh();
            Require(ReferenceEquals(pawn.equipment.Primary, weapon) && ReferenceEquals(before, Snapshot(session.Stats, pawn))
                && context == Revision("ContextRevisions", pawn), "WR17 rejected native equipment no-op preserves primary, snapshot and context revision");
            Require(pawn.equipment.TryDropEquipment(weapon, out ThingWithComps dropped, pawn.Position, false)
                && ReferenceEquals(dropped, weapon) && pawn.equipment.Primary == null, "WR17 native TryDropEquipment actually leaves Flow unarmed");
            session.Refresh();
            Require(!Facts(Snapshot(session.Stats, pawn)).HasRangedWeapon && !Has(session.Plan().Plan, index, hunter),
                "WR17 native ranged drop immediately removes cached weapon qualification and Hunter recommendation");
            Require(!ReferenceEquals(before, Snapshot(session.Stats, pawn)) && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn))
                && factual == Revision("FactRevisions", pawn) && context != Revision("ContextRevisions", pawn),
                "WR17 ranged drop changes non-skill context while retaining skill capture");
            object unarmed = Snapshot(session.Stats, pawn); int unarmedContext = Revision("ContextRevisions", pawn);
            var knife = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("MeleeWeapon_Knife"), ThingDefOf.Steel);
            try
            {
                pawn.equipment.AddEquipment(knife); session.Refresh();
                Require(ReferenceEquals(pawn.equipment.Primary, knife) && ReferenceEquals(unarmed, Snapshot(session.Stats, pawn))
                    && unarmedContext == Revision("ContextRevisions", pawn) && !Has(session.Plan().Plan, index, hunter),
                    "WR17 actual melee equip preserves unarmed ranged-fact snapshot and does not recommend Hunter");
                Require(pawn.equipment.TryDropEquipment(knife, out _, pawn.Position, false), "WR17 native melee drop succeeds");
                session.Refresh();
                Require(ReferenceEquals(unarmed, Snapshot(session.Stats, pawn)) && unarmedContext == Revision("ContextRevisions", pawn),
                    "WR17 melee removal preserves external identity and context revision");
            }
            finally { if (pawn.equipment.Contains(knife)) pawn.equipment.Remove(knife); knife.Destroy(); }
        }
        finally
        {
            if (!pawn.equipment.Contains(weapon)) { if (weapon.Spawned) weapon.DeSpawn(); pawn.equipment.AddEquipment(weapon); }
        }
        session.Refresh();
        Require(Facts(Snapshot(session.Stats, pawn)).HasRangedWeapon && Has(session.Plan().Plan, index, hunter)
            && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn)) && factual == Revision("FactRevisions", pawn),
            "WR17 adding native ranged primary back restores Hunter and retains skill capture");
        Pawn other = session.Pawns.First(p => p != pawn && p.equipment?.Primary?.def.IsRangedWeapon == true);
        ThingWithComps otherWeapon = other.equipment.Primary;
        object stable = Snapshot(session.Stats, pawn); context = Revision("ContextRevisions", pawn);
        try
        {
            Require(other.equipment.TryDropEquipment(otherWeapon, out _, other.Position, false), "WR17 native other-pawn equipment drop succeeds");
            session.Refresh();
            Require(ReferenceEquals(stable, Snapshot(session.Stats, pawn)) && context == Revision("ContextRevisions", pawn)
                && ReferenceEquals(skills, SkillIdentity(session.Stats, pawn)), "WR17 other pawn's ranged notification preserves Flow snapshot and skill identity");
        }
        finally { if (!other.equipment.Contains(otherWeapon)) { if (otherWeapon.Spawned) otherWeapon.DeSpawn(); other.equipment.AddEquipment(otherWeapon); } }
    }
}
