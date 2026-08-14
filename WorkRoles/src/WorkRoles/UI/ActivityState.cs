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

    /// Per-pawn activity cache re-resolved when ActivityTracker's live-activity
    /// revision moves, UiVersion changes the compiled role claiming the same
    /// running job, or the owning RoleStore changes.
    internal sealed class ActivityState
    {
        // Owner: Colonists window. Key: RoleStore and Pawn reference identity,
        // activity revision, and UiVersion. Value: detached ActivitySnapshot.
        // Dependencies: job/draft/mental transitions plus compiled claiming-role
        // changes.
        // Refresh: immediate on the next read after a dependency changes.
        // Equality: exact key hits reuse the cached value. Teardown: Release on
        // window reset/close/language change; an owner change clears all entries.
        private readonly Dictionary<Pawn,
            (int activityRevision, int uiRevision, ActivitySnapshot value)> cache =
            new Dictionary<Pawn, (int, int, ActivitySnapshot)>();
        private RoleStore observedOwner;

        internal ActivitySnapshot For(Pawn pawn)
        {
            if (pawn == null) return new ActivitySnapshot(-1, "");
            return For(pawn, ActivityTracker.RevisionOf(pawn),
                UiVersion.Current, RoleStore.Current);
        }

        internal ActivitySnapshot For(Pawn pawn, int activityRevision,
            int uiRevision, RoleStore owner)
        {
            if (pawn == null) return new ActivitySnapshot(-1, "");
            if (!ReferenceEquals(observedOwner, owner))
            {
                cache.Clear();
                observedOwner = owner;
            }
            if (cache.TryGetValue(pawn, out var entry)
                && entry.activityRevision == activityRevision
                && entry.uiRevision == uiRevision)
                return entry.value;
            ActivitySnapshot value = Resolve(pawn);
            cache[pawn] = (activityRevision, uiRevision, value);
            return value;
        }

        internal void Release()
        {
            cache.Clear();
            observedOwner = null;
        }

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

        /// Jobs carrying no workGiverDef: vanilla's non-scan givers, plus
        /// opportunistic and bill-product hauls started outside JobGiver_Work.
        /// Job def plus tag pin down the work type; DLC-gated JobDefOf fields
        /// are null when inactive.
        private static string NonScanWorkTypeFor(Pawn pawn, Job job)
        {
            JobDef def = job.def;
            if (def == null) return null;
            if (def == JobDefOf.HaulToCell || def == JobDefOf.HaulToContainer)
                return "Hauling";
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
