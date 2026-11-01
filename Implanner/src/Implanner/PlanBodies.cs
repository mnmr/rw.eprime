using RimWorld;
using Verse;

namespace Implanner
{
    /// Game-side reading of Plan.Body (a BodyDef name, "" = any body): who
    /// a pinned plan accepts, the body's label, and its position among the
    /// plannable bodies. A pinned plan whose body is not loaded (its race
    /// mod removed) accepts nobody and reads as not loaded; it pins again
    /// when the mod returns. Allocation-free except for labels; the
    /// acceptance test runs on the reconcile tick path per pawn.
    internal static class PlanBodies
    {
        /// Whether a colonist with this body may be enlisted in (and is
        /// evaluated against) a plan of the given body.
        internal static bool Accepts(string planBody, Pawn pawn) =>
            planBody.Length == 0
            || string.Equals(pawn.RaceProps.body.defName, planBody, System.StringComparison.Ordinal);

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
