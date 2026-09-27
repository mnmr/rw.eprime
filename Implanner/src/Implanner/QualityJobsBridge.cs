using System;
using System.Reflection;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Soft-bound Quality Jobs integration (API version 2): hands a new
    /// implant production bill to Quality Jobs with a target quality, so the
    /// finishing step goes to a crafter able to reach it and a result below
    /// it is made again. Bound by reflection (no assembly reference), once
    /// per session; a missing or failing API disables the bridge and
    /// production continues without a quality promise.
    internal static class QualityJobsBridge
    {
        private const string PackageId = "EPrime.QualityJobs";

        // Cache contract:
        // Owner: process/loaded assembly set.
        // Key: none.
        // Value: the QualityJobsApi.ManageBill method, or null when Quality
        //   Jobs is absent, older than API version 2, or failed once.
        // Dependencies: the active mod list and loaded assemblies (fixed for
        //   the session).
        // Refresh policy: resolved once on first use.
        // Equality policy: the same handle for the session.
        // Teardown: none needed; process lifetime, no game object retained.
        private static bool resolved;
        private static MethodInfo? manageBill;

        /// Whether Quality Jobs can take Implanner's bills.
        internal static bool Available
        {
            get
            {
                Resolve();
                return manageBill != null;
            }
        }

        /// Places the bill under Quality Jobs management at the target
        /// quality (0 Awful … 6 Legendary). True when Quality Jobs accepted
        /// the command; false when it is absent or does not manage the
        /// recipe. Called from the synchronized reconcile pass: Quality Jobs'
        /// own synced command then runs on every client alike.
        internal static bool ManageBill(Bill_ProductionWithUft bill, int targetQuality)
        {
            Resolve();
            if (manageBill == null) return false;
            try
            {
                object status = manageBill.Invoke(null, new object[] { bill, targetQuality });
                return Convert.ToInt32(status) == 0; // BillManagementStatus.Success
            }
            catch (Exception exception)
            {
                manageBill = null;
                Log.Warning("[Implanner] Quality Jobs API failed; production no longer "
                    + "asks it for a quality: " + exception.GetType().Name + ": "
                    + (exception.InnerException ?? exception).Message);
                return false;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            if (ModLister.GetActiveModWithIdentifier(PackageId, ignorePostfix: true) == null)
                return;
            Type? api = GenTypes.GetTypeInAnyAssembly("QualityJobs.QualityJobsApi");
            FieldInfo? version = api?.GetField("ApiVersion",
                BindingFlags.Public | BindingFlags.Static);
            if (version == null || !version.IsLiteral
                || Convert.ToInt32(version.GetRawConstantValue()) < 2)
                return;
            manageBill = api!.GetMethod("ManageBill",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Bill_ProductionWithUft), typeof(int) }, null);
        }
    }
}
