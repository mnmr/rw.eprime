using System;
using System.Linq.Expressions;
using System.Reflection;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// EPrime's Quality Jobs integration: how many times a bill or buildable is
    /// expected to run before it hits its quality target. Bound against that
    /// mod's declared public read-only surface (QualityJobs.QualityJobsApi), so
    /// a version mismatch disables the feature instead of misreporting it.
    internal static class QualityJobsBridge
    {
        /// The neutral answer everywhere the integration is unavailable or the
        /// work carries no quality target: one run, no rework.
        internal const float NoRework = 1f;

        /// The mod's own packageId, matched the way vanilla matches MayRequire
        /// so a Workshop install's "_steam" suffix still resolves.
        private const string PackageId = "EPrime.QualityJobs";

        // One-time binding: the loaded mod set is fixed for the process, so a
        // failed or absent probe never retries. On any API mismatch or runtime
        // failure the integration disables itself with a single warning and
        // reservations fall back to one run per item.
        private static bool resolved;
        private static bool installed;

        // Compiled once at bind time into cached static delegates so the
        // throttled snapshot pass makes plain delegate calls with no
        // MethodInfo.Invoke argument-array or boxing allocations.
        private static Func<Bill, float> billAttempts;
        private static Func<Thing, float> constructibleAttempts;

        /// True when Quality Jobs is in the active mod list. Drives the options
        /// dialog's disabled state, and is deliberately independent of whether
        /// the API bound: a player who has the mod should see why the toggle is
        /// misbehaving in the log, not a silently greyed-out row.
        internal static bool Installed
        {
            get { Resolve(); return installed; }
        }

        /// True when the API bound and the expected-attempts queries are live.
        internal static bool Available
        {
            get { Resolve(); return billAttempts != null; }
        }

        /// Expected production runs of this bill per product that meets its
        /// quality target. Returns 1 when the integration is unavailable, the
        /// bill is unmanaged, or it carries no quality target.
        internal static float ExpectedAttemptsForBill(Bill bill)
        {
            Resolve();
            if (billAttempts == null || bill == null) return NoRework;
            try
            {
                return Sane(billAttempts(bill));
            }
            catch (Exception exception)
            {
                Disable("bill quality lookup failed", exception);
                return NoRework;
            }
        }

        /// Expected build attempts for a blueprint or frame, counting the
        /// deconstruct-and-rebuild cycles a below-target roll triggers.
        internal static float ExpectedAttemptsForConstructible(Thing thing)
        {
            Resolve();
            if (constructibleAttempts == null || thing == null) return NoRework;
            try
            {
                return Sane(constructibleAttempts(thing));
            }
            catch (Exception exception)
            {
                Disable("construction quality lookup failed", exception);
                return NoRework;
            }
        }

        internal static void Reset()
        {
            // Binding is process-scoped and def-independent; nothing to release.
            // Present so the teardown path can stay uniform across bridges.
        }

        /// A foreign mod's answer is untrusted input: anything not finite and
        /// at least one run would corrupt the reservation arithmetic.
        private static float Sane(float attempts)
            => attempts > NoRework && !float.IsNaN(attempts)
               && !float.IsInfinity(attempts)
                ? attempts : NoRework;

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            installed = ModLister.GetActiveModWithIdentifier(
                PackageId, ignorePostfix: true) != null;
            if (!installed) return;

            Type api = GenTypes.GetTypeInAnyAssembly("QualityJobs.QualityJobsApi");
            if (api == null)
            {
                Log.Warning("[EPrimeReadouts] Quality Jobs is active but exposes "
                    + "no integration API; quality rework is not reserved for.");
                return;
            }
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            MethodInfo forBill = api.GetMethod("ExpectedAttemptsForBill", flags);
            MethodInfo forConstructible =
                api.GetMethod("ExpectedAttemptsForConstructible", flags);
            if (!ShapeMatches(forBill, typeof(Bill))
                || !ShapeMatches(forConstructible, typeof(Thing)))
            {
                Log.Warning("[EPrimeReadouts] Quality Jobs detected but its "
                    + "integration API changed; quality rework is not reserved for.");
                return;
            }
            try
            {
                billAttempts = Compile<Bill>(forBill);
                constructibleAttempts = Compile<Thing>(forConstructible);
            }
            catch (Exception exception)
            {
                billAttempts = null;
                constructibleAttempts = null;
                Log.Warning("[EPrimeReadouts] Quality Jobs integration API "
                    + "binding failed; quality rework is not reserved for: "
                    + exception.Message);
            }
        }

        private static bool ShapeMatches(MethodInfo method, Type parameterType)
        {
            if (method == null || method.ReturnType != typeof(float)) return false;
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 1
                && parameters[0].ParameterType == parameterType;
        }

        private static Func<T, float> Compile<T>(MethodInfo method)
        {
            ParameterExpression argument = Expression.Parameter(typeof(T), "arg");
            return Expression.Lambda<Func<T, float>>(
                Expression.Call(method, argument), argument).Compile();
        }

        private static void Disable(string what, Exception exception)
        {
            billAttempts = null;
            constructibleAttempts = null;
            Log.Warning("[EPrimeReadouts] Quality Jobs " + what
                + "; quality rework is no longer reserved for: "
                + exception.Message);
        }
    }
}
