using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WorkRoles.UI
{
    /// Resolved "doing right now" for one pawn: the active role claiming the
    /// running job (RoleId >= 0), or a translated label for non-role activity.
    internal readonly struct ActivitySnapshot
    {
        internal readonly int RoleId;
        internal readonly string Label;

        internal ActivitySnapshot(int roleId, string label)
        {
            RoleId = roleId;
            Label = label;
        }
    }

    /// Per-pawn activity cache re-resolved only when ActivityTracker's
    /// job-transition revision moves — independent of the UiVersion snapshot
    /// generations, which never see activity changes.
    internal sealed class ActivityState
    {
        private readonly Dictionary<Pawn, (int revision, ActivitySnapshot value)> cache =
            new Dictionary<Pawn, (int, ActivitySnapshot)>();

        internal ActivitySnapshot For(Pawn pawn)
        {
            if (pawn == null) return new ActivitySnapshot(-1, "");
            int revision = ActivityTracker.RevisionOf(pawn);
            if (cache.TryGetValue(pawn, out var entry) && entry.revision == revision)
                return entry.value;
            ActivitySnapshot value = Resolve(pawn);
            cache[pawn] = (revision, value);
            return value;
        }

        internal void Release() => cache.Clear();

        private static ActivitySnapshot Other(string key) =>
            new ActivitySnapshot(-1, key.Translate());

        private static ActivitySnapshot Resolve(Pawn pawn)
        {
            if (pawn.Drafted) return Other("WR_NowDrafted");
            if (pawn.InMentalState) return Other("WR_NowMental");
            Job job = pawn.jobs?.curJob;
            if (job == null) return Other("WR_NowIdle");
            if (job.playerForced) return Other("WR_NowForced");
            string giver = job.workGiverDef?.defName;
            if (giver != null)
                return CompiledJobOrders.TryGetClaimingRole(pawn, giver, out int roleId)
                    ? new ActivitySnapshot(roleId, null)
                    : Other("WR_NowBusy");
            string workType = NonScanWorkTypeFor(pawn, job);
            if (workType != null)
                return CompiledJobOrders.TryGetClaimingRoleForWorkType(pawn, workType, out int roleId)
                    ? new ActivitySnapshot(roleId, null)
                    : Other("WR_NowBusy");
            if (pawn.jobs.curDriver?.asleep == true || job.def == JobDefOf.LayDown)
                return Other("WR_NowSleeping");
            if (job.def?.joyKind != null) return Other("WR_NowRecreation");
            if (pawn.mindState?.lastJobTag == JobTag.SatisfyingNeeds) return Other("WR_NowNeeds");
            return Other("WR_NowBusy");
        }

        /// Vanilla's non-scan work givers issue jobs without workGiverDef
        /// (JobGiver_Work's NonScanJob path); job def plus tag pin down the
        /// work type. DLC-gated JobDefOf fields are null when inactive.
        private static string NonScanWorkTypeFor(Pawn pawn, Job job)
        {
            JobDef def = job.def;
            if (def == null) return null;
            JobTag? tag = pawn.mindState?.lastJobTag;
            if (def == JobDefOf.LayDown)
                return tag == JobTag.RestingForMedicalReasons ? "Patient" : null;
            if (def == JobDefOf.TendPatient)
                return tag == JobTag.MiscWork && job.targetA.Thing == pawn ? "Doctor" : null;
            if (def == JobDefOf.PrepareCaravan_GatherItems)
                return tag == JobTag.MiscWork ? "Hauling" : null;
            if (def == JobDefOf.BringBabyToSafetyUnforced) return "Childcare";
            if (ModsConfig.OdysseyActive && def == JobDefOf.Fish) return "Fishing";
            return null;
        }

        /// Tooltip body, composed live on hover so mid-job report drift (e.g.
        /// resting becoming sleeping) never shows stale text.
        internal static string LiveTip(Pawn pawn, RoleStore store)
        {
            if (pawn == null) return "";
            ActivitySnapshot value = Resolve(pawn);
            string head = value.RoleId >= 0
                ? store?.RoleById(value.RoleId)?.label ?? value.RoleId.ToString()
                : value.Label;
            string report = pawn.jobs?.curDriver?.GetReport();
            if (report.NullOrEmpty()) return head;
            string reportCap = report.CapitalizeFirst();
            // "Sleeping: Sleeping." reads broken; a head the report already
            // states collapses to the report alone.
            if (string.Equals(head, reportCap.TrimEnd('.'),
                    System.StringComparison.OrdinalIgnoreCase))
                return reportCap;
            return "WR_NowTip".Translate(head, reportCap);
        }

        /// Bare activity phrase for "<name> is <activity>" compositions: the
        /// live report when one exists, else the resolved label uncapitalized.
        internal static string ActivityPhrase(Pawn pawn, RoleStore store)
        {
            if (pawn == null) return "";
            string report = pawn.jobs?.curDriver?.GetReport();
            if (!report.NullOrEmpty()) return report;
            ActivitySnapshot value = Resolve(pawn);
            string label = value.RoleId >= 0
                ? store?.RoleById(value.RoleId)?.label ?? "" : value.Label;
            return label.UncapitalizeFirst();
        }
    }
}
