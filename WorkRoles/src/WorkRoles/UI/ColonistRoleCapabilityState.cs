using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Signals;

namespace WorkRoles.UI
{
    /// <summary>
    /// Immutable, render-ready warning data for one assigned role. Pawn and
    /// definition queries are confined to this snapshot owner; layout and draw
    /// passes consume only the resulting severity and tooltip.
    /// </summary>
    internal sealed class RoleCapabilityPresentation
    {
        internal static readonly RoleCapabilityPresentation Available =
            new RoleCapabilityPresentation(
                RoleAssignmentWarningSeverity.None,
                null);

        internal RoleCapabilityPresentation(
            RoleAssignmentWarningSeverity warningSeverity,
            string tooltip)
        {
            WarningSeverity = warningSeverity;
            Tooltip = tooltip;
        }

        internal RoleAssignmentWarningSeverity WarningSeverity { get; }
        internal string Tooltip { get; }
    }

    /// <summary>
    /// View-owned capability snapshot cache. Scope changes invalidate by stamp;
    /// mutable pawn signals and language changes explicitly invalidate the owner.
    /// </summary>
    internal sealed class ColonistRoleCapabilityState
    {
        private const int ExplicitJobCap = 3;
        private readonly Dictionary<(Pawn pawn, int roleId), RoleCapabilityPresentation> presentations
            = new Dictionary<(Pawn pawn, int roleId), RoleCapabilityPresentation>();
        private ScopeCacheStamp stamp = ScopeCacheStamp.Invalid;

        internal RoleCapabilityPresentation PresentationFor(
            Pawn pawn,
            Role role,
            ScopeCacheStamp currentStamp,
            PawnSignalSnapshot signalSnapshot)
        {
            if (stamp != currentStamp)
            {
                presentations.Clear();
                stamp = currentStamp;
            }

            if (pawn == null || role == null || role.blocker)
                return RoleCapabilityPresentation.Available;

            var key = (pawn, role.id);
            if (!presentations.TryGetValue(key, out RoleCapabilityPresentation presentation))
                presentations[key] = presentation = Build(pawn, role, signalSnapshot);
            return presentation;
        }

        internal void Invalidate()
        {
            presentations.Clear();
            stamp = ScopeCacheStamp.Invalid;
        }

        private static RoleCapabilityPresentation Build(
            Pawn pawn,
            Role role,
            PawnSignalSnapshot signalSnapshot)
        {
            signalSnapshot = signalSnapshot ?? PawnSignalSnapshot.Empty;
            bool hasRangedWeapon = pawn.equipment?.Primary?.def?.IsRangedWeapon == true;
            int totalJobs = 0;
            var blocked = new List<(string label, string reason)>();
            SortedSet<string> awfulWorkTypes = null;
            SortedSet<string> awfulSkills = null;
            SortedSet<string> awfulDescriptions = null;
            string incapableReason = null;
            string noRangedWeaponReason = null;
            bool hasWorkTypeSignals = signalSnapshot.WorkTypeBuckets.All.Count > 0;

            foreach (string giverName in role.Coverage())
            {
                WorkGiverDef def = GameJobCatalog.Instance.GiverDef(giverName);
                if (def == null) continue;
                totalJobs++;

                WorkTypeBucketSignal workTypeSignal = !hasWorkTypeSignals
                    || def.workType == null
                    ? null
                    : signalSnapshot.WorkTypeBuckets.ForWorkType(def.workType.defName);
                if (workTypeSignal?.Bucket == SignalBucket.Awful)
                {
                    (awfulWorkTypes ??= new SortedSet<string>(
                        System.StringComparer.Ordinal))
                        .Add(def.workType.labelShort.CapitalizeFirst());
                    foreach (SignalContribution contribution in workTypeSignal.Contributions)
                        if (contribution.IsClassified
                            && !contribution.Signal.Ui.Description.NullOrEmpty())
                            (awfulDescriptions ??= new SortedSet<string>(
                                System.StringComparer.Ordinal))
                                .Add(contribution.Signal.Ui.Description);
                }

                if (def.workType?.relevantSkills != null)
                    foreach (SkillDef skill in def.workType.relevantSkills)
                        if (signalSnapshot.SkillBuckets.ForSkill(skill.defName)?.Bucket
                            == SignalBucket.Awful)
                            (awfulSkills ??= new SortedSet<string>(
                                System.StringComparer.Ordinal)).Add(skill.LabelCap);

                bool incapable = pawn.WorkTypeIsDisabled(def.workType)
                    || pawn.WorkTagIsDisabled(def.workTags);
                bool lacksHuntingWeapon = def.workType == WorkTypeDefOf.Hunting
                    && !hasRangedWeapon;
                if (!incapable && !lacksHuntingWeapon) continue;

                string reason = incapable
                    ? incapableReason ?? (incapableReason =
                        "WR_RoleJobIncapable".Translate().ToString())
                    : noRangedWeaponReason ?? (noRangedWeaponReason =
                        "WR_RoleJobNoRangedWeapon".Translate().ToString());
                blocked.Add((WorkJobLabels.GiverDisplayName(def), reason));
            }

            RoleJobAvailability availability = RoleJobAvailabilitySummary.FromCounts(
                totalJobs, blocked.Count);

            bool hasAwfulSignal = awfulWorkTypes?.Count > 0 || awfulSkills?.Count > 0;
            if (availability == RoleJobAvailability.Available && !hasAwfulSignal)
                return RoleCapabilityPresentation.Available;

            var warnings = new List<string>(2);
            var reasons = new List<string>(2);
            if (incapableReason != null) reasons.Add(incapableReason);
            if (noRangedWeaponReason != null) reasons.Add(noRangedWeaponReason);

            if (availability != RoleJobAvailability.Available)
            {
                // One sentence chained from snippets: mixed reasons ride each
                // listed job; a uniform or counted jobs part gets one reason tail.
                string jobsPart;
                string reasonTail;
                if (blocked.Count <= ExplicitJobCap)
                {
                    var names = new List<string>(blocked.Count);
                    foreach ((string label, string reason) in blocked)
                        names.Add(reasons.Count == 1
                            ? label
                            : label + " (" + reason + ")");
                    jobsPart = names.ToCommaList(useAnd: true);
                    reasonTail = reasons.Count == 1 ? ": " + reasons[0] : "";
                }
                else
                {
                    jobsPart = availability == RoleJobAvailability.AllUnavailable
                        ? "WR_RoleCapAllJobs".Translate().ToString()
                        : "WR_RoleCapSomeJobs".Translate(
                            blocked.Count, totalJobs).ToString();
                    reasonTail = ": " + reasons.ToCommaList();
                }
                warnings.Add("WR_RoleCapNeverDo".Translate(
                    pawn.LabelShortCap, jobsPart).ToString() + reasonTail);
            }

            if (hasAwfulSignal)
            {
                var targets = new List<string>(
                    (awfulWorkTypes?.Count ?? 0) + (awfulSkills?.Count ?? 0));
                if (awfulWorkTypes != null) targets.AddRange(awfulWorkTypes);
                if (awfulSkills != null) targets.AddRange(awfulSkills);
                string awful = "WR_RoleAwfulSignal".Translate(
                    pawn.LabelShortCap, targets.ToCommaList(useAnd: true)).ToString();
                if (awfulDescriptions?.Count > 0)
                    awful += " " + new List<string>(awfulDescriptions).ToCommaList();
                warnings.Add(awful);
            }

            RoleAssignmentWarningSeverity severity =
                RoleAssignmentWarningSummary.From(availability, hasAwfulSignal);
            return new RoleCapabilityPresentation(
                severity, string.Join("\n", warnings));
        }
    }
}
