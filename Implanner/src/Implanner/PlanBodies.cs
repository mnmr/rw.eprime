using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// Game-side reading of Plan.Body (a BodyDef name, "" = any body): who
    /// a pinned plan accepts, the body's label, and its position among the
    /// plannable bodies. A plan for the human body also takes colonists
    /// whose body carries every human implant slot (a race mod's copy of
    /// the human body, or Milira with its extra wings), by the catalog's
    /// body kinship (Core BodyKinships, owner 2026-11-01). A pinned plan
    /// whose body is not loaded (its race mod removed) accepts nobody and
    /// reads as not loaded; it pins again when the mod returns.
    /// Allocation-free except for labels; the acceptance test runs on the
    /// reconcile tick path per pawn.
    internal static class PlanBodies
    {
        /// Whether the pawn may be enlisted in (and is evaluated against)
        /// the plan: the plan's mutant kind must be exactly the pawn's
        /// (an ordinary plan never takes a ghoul, a ghoul plan nobody
        /// else), then the body rule.
        internal static bool Accepts(Plan plan, Pawn pawn) =>
            Accepts(plan.Body, plan.Mutant, pawn);

        internal static bool Accepts(string planBody, string planMutant, Pawn pawn) =>
            string.Equals(planMutant, ColonyScope.MutantOf(pawn), System.StringComparison.Ordinal)
            && Accepts(planBody, pawn);

        /// Who the plan is for, for the greyed plan menu entry: its mutant
        /// kind ("Ghouls"), its body, or ordinary colonists. Builder path.
        internal static string TargetLabel(Plan plan) =>
            plan.IsForMutant ? "IMP_PlanGhouls".Translate().ToString()
            : plan.IsPinned ? Label(plan.Body)
            : "IMP_PlanTargetColonists".Translate().ToString();

        /// Whether a colonist with this body may be enlisted in (and is
        /// evaluated against) a plan of the given body.
        internal static bool Accepts(string planBody, Pawn pawn)
        {
            if (planBody.Length == 0) return true;
            BodyDef pawnBody = pawn.RaceProps.body;
            if (string.Equals(pawnBody.defName, planBody, System.StringComparison.Ordinal))
                return true;
            int plan = IndexOf(planBody);
            if (plan < 0) return false;
            int own = Catalogs.PlannableBodies().IndexOf(pawnBody);
            if (own < 0) return false;
            return BodyKinships.Accepts(planBody, Catalogs.KinshipOf(plan),
                pawnBody.defName, Catalogs.KinshipOf(own));
        }

        /// The plan body's position in Catalogs.PlannableBodies, or -1 for
        /// any body or an unloaded one.
        internal static int IndexOf(string planBody)
        {
            if (planBody.Length == 0) return -1;
            var bodies = Catalogs.PlannableBodies();
            for (int i = 0; i < bodies.Count; i++)
                if (string.Equals(bodies[i].defName, planBody, System.StringComparison.Ordinal))
                    return i;
            return -1;
        }

        /// The body's label ("Android"), or the def name marked as not
        /// loaded. Builder path only (translates and allocates).
        internal static string Label(string planBody)
        {
            BodyDef? body = DefDatabase<BodyDef>.GetNamedSilentFail(planBody);
            return body != null
                ? body.LabelCap.ToString()
                : "IMP_PlanBodyNotLoaded".Translate(planBody).ToString();
        }
    }
}
