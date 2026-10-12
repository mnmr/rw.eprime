using System;
using System.Collections.Generic;
using RimShared.Common;
using RimWorld;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
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
            string? tooltip)
        {
            WarningSeverity = warningSeverity;
            Tooltip = tooltip;
        }

        internal RoleAssignmentWarningSeverity WarningSeverity { get; }
        internal string? Tooltip { get; }
    }

    /// <summary>
    /// View-owned capability snapshots, independent of assignment presentation
    /// and role activation. External refresh evicts only changed pawns.
    /// </summary>
    internal sealed class ColonistRoleCapabilityState
    {
        private const int ExplicitJobCap = 3;
        private sealed class Entry
        {
            internal Role Role = null!;
            internal PawnExternalSnapshot External = null!;
            internal int CapabilityRevision;
            internal int LanguageRevision;
            internal int DefinitionRevision;
            internal RoleCapabilityPresentation Presentation = null!;
        }

        // Owner: Colonists window within RoleStore identity. Key: pawn identity
        // and role id. Value: immutable severity/tooltip projection. Dependencies:
        // pawn external snapshot identity, role identity and CapabilityForRole,
        // language and definitions. Assignment, activation, scope and unrelated
        // role revisions are excluded. Refresh: first read after a key change;
        // catalog changes only prune removed roles. Equality: equal severity and
        // text retain identity within the same store. Teardown: Invalidate on
        // window release/full external refresh; InvalidatePawn on pawn removal.
        private readonly Dictionary<(Pawn pawn, int roleId), Entry> presentations
            = new Dictionary<(Pawn pawn, int roleId), Entry>();
        private RoleStore? owner;
        private int catalogRevision = -1;

        internal RoleCapabilityPresentation PresentationFor(
            Pawn pawn,
            Role role,
            ScopeCacheStamp currentStamp,
            PawnExternalSnapshot externalSnapshot)
        {
            EnsureOwner();

            if (pawn == null || role == null || role.blocker)
                return RoleCapabilityPresentation.Available;

            var key = (pawn, role.id);
            int capability = UiVersion.CapabilityForRole(role.id);
            int language = LanguageChangeCoordinator.Revision;
            int definition = DefinitionReloadCoordinator.Revision;
            bool found = presentations.TryGetValue(key, out Entry entry);
            if (found && ReferenceEquals(entry.Role, role)
                && ReferenceEquals(entry.External, externalSnapshot)
                && entry.CapabilityRevision == capability
                && entry.LanguageRevision == language
                && entry.DefinitionRevision == definition)
                return entry.Presentation;
            RoleCapabilityPresentation rebuilt = Build(pawn, role, externalSnapshot);
            if (found && entry.Presentation.WarningSeverity == rebuilt.WarningSeverity
                && string.Equals(entry.Presentation.Tooltip, rebuilt.Tooltip,
                    StringComparison.Ordinal))
                rebuilt = entry.Presentation;
            presentations[key] = new Entry
            {
                Role = role, External = externalSnapshot,
                CapabilityRevision = capability, LanguageRevision = language,
                DefinitionRevision = definition, Presentation = rebuilt,
            };
            return rebuilt;
        }

        private void EnsureOwner()
        {
            RoleStore? store = RoleStore.Current;
            if (!ReferenceEquals(owner, store))
            {
                presentations.Clear();
                owner = store;
                catalogRevision = -1;
            }
            if (catalogRevision == UiVersion.Catalog) return;
            keyBuffer.Clear();
            foreach (KeyValuePair<(Pawn pawn, int roleId), Entry> pair in presentations)
                if (store?.RoleById(pair.Key.roleId) == null)
                    keyBuffer.Add(pair.Key);
            for (int i = 0; i < keyBuffer.Count; i++)
                presentations.Remove(keyBuffer[i]);
            keyBuffer.Clear();
            catalogRevision = UiVersion.Catalog;
        }

        internal void Invalidate()
        {
            presentations.Clear();
            owner = null;
            catalogRevision = -1;
            keyBuffer.Clear();
        }

        private readonly List<(Pawn pawn, int roleId)> keyBuffer =
            new List<(Pawn, int)>();

        /// Drops one pawn's entries after a targeted external refresh; a
        /// bounded key scan at that refresh boundary, never on a draw path.
        internal void InvalidatePawn(Pawn pawn)
        {
            keyBuffer.Clear();
            foreach (KeyValuePair<(Pawn pawn, int roleId), Entry> pair
                in presentations)
                if (ReferenceEquals(pair.Key.pawn, pawn))
                    keyBuffer.Add(pair.Key);
            for (int i = 0; i < keyBuffer.Count; i++)
                presentations.Remove(keyBuffer[i]);
            keyBuffer.Clear();
        }

        private static RoleCapabilityPresentation Build(
            Pawn pawn,
            Role role,
            PawnExternalSnapshot externalSnapshot)
        {
            externalSnapshot = externalSnapshot ?? PawnExternalSnapshot.Empty;
            PawnSignalSnapshot signalSnapshot = externalSnapshot.Signals;
            bool hasRangedWeapon = externalSnapshot.HasRangedWeapon;
            int totalJobs = 0;
            var blocked = new List<(string label, string reason)>();
            SortedSet<string>? awfulWorkTypes = null;
            SortedSet<string>? awfulSkills = null;
            SortedSet<string>? awfulDescriptions = null;
            SortedSet<string>? missingRequiredSkills = null;
            string? incapableReason = null;
            string? noRangedWeaponReason = null;
            SortedDictionary<int, string>? tooYoungReasons = null;
            bool ageLimitsApply =
                externalSnapshot.RecommendationFacts.AgeLimitsApply;
            bool tooYoungForRole = ageLimitsApply && role.minAge > 0
                && externalSnapshot.RecommendationFacts.BiologicalAgeTicks
                    < role.minAge * BiologicalAge.TicksPerYear;
            // The cap is inclusive: over-age starts one year past it.
            bool tooOldForRole = ageLimitsApply && role.maxAge > 0
                && externalSnapshot.RecommendationFacts.BiologicalAgeTicks
                    >= (role.maxAge + 1L) * BiologicalAge.TicksPerYear;
            bool hasWorkTypeSignals = signalSnapshot.WorkTypeBuckets.All.Count > 0;
            string? primarySkill = RecsAdapter.PrimarySkillOf(role);
            bool awfulPrimarySkill = false;
            // Only participating skills matter for warnings — a sliver like
            // Finish Off's melee never marks.
            WorkRoles.Core.Recs.RoleWorkSpec workSpec = RoleWorkSpecs.For(role);
            var participatingSkills = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (WorkRoles.Core.Recs.RoleSkillFact fact in workSpec.Skills)
                if (fact.Participates)
                    participatingSkills.Add(fact.SkillDefName);
            IReadOnlyList<string> requiredGates = workSpec.AssignmentSkillGates;
            for (int index = 0; index < requiredGates.Count; index++)
            {
                string defName = requiredGates[index];
                if (externalSnapshot.RecommendationFacts.SkillLevels
                    .ContainsKey(defName)) continue;
                SkillDef skill = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
                (missingRequiredSkills ??= new SortedSet<string>(
                    System.StringComparer.Ordinal)).Add(
                    skill?.LabelCap ?? defName);
            }

            foreach (string giverName in role.Coverage())
            {
                WorkGiverDef def = GameJobCatalog.Instance.GiverDef(giverName);
                if (def == null) continue;
                totalJobs++;

                WorkTypeBucketSignal? workTypeSignal = !hasWorkTypeSignals
                    || def.workType == null
                    ? null
                    : signalSnapshot.WorkTypeBuckets.ForWorkType(def.workType.defName);
                if (workTypeSignal?.Bucket == SignalBucket.Awful)
                {
                    (awfulWorkTypes ??= new SortedSet<string>(
                        System.StringComparer.Ordinal))
                        .Add(def.workType!.labelShort.CapitalizeFirst()); // signal exists only for a non-null workType
                    foreach (SignalContribution contribution in workTypeSignal.Contributions)
                        if (contribution.IsClassified
                            && contribution.Signal.Ui.Description is { Length: > 0 } description)
                            (awfulDescriptions ??= new SortedSet<string>(
                                System.StringComparer.Ordinal))
                                .Add(description);
                }

                if (def.workType?.relevantSkills != null)
                    foreach (SkillDef skill in def.workType.relevantSkills)
                        if (participatingSkills.Contains(skill.defName)
                            && signalSnapshot.SkillBuckets.ForSkill(skill.defName)?.Bucket
                            == SignalBucket.Awful)
                        {
                            (awfulSkills ??= new SortedSet<string>(
                                System.StringComparer.Ordinal)).Add(skill.LabelCap);
                            if (skill.defName == primarySkill)
                                awfulPrimarySkill = true;
                        }

                bool incapable = !externalSnapshot.CanDo(def);
                bool lacksHuntingWeapon = def.workType == WorkTypeDefOf.Hunting
                    && !hasRangedWeapon;
                if (!incapable && !lacksHuntingWeapon) continue;

                string reason;
                if (incapable && def.workType != null
                    && externalSnapshot.AgeBlockedWorkTypes.TryGetValue(
                        def.workType.defName, out int unlockAge))
                {
                    tooYoungReasons ??= new SortedDictionary<int, string>();
                    if (!tooYoungReasons.TryGetValue(unlockAge, out reason))
                        tooYoungReasons[unlockAge] = reason =
                            "WR_RoleJobTooYoung".Translate(unlockAge).ToString();
                }
                else
                    reason = incapable
                        ? incapableReason ?? (incapableReason =
                            "WR_RoleJobIncapable".Translate().ToString())
                        : noRangedWeaponReason ?? (noRangedWeaponReason =
                            "WR_RoleJobNoRangedWeapon".Translate().ToString());
                blocked.Add((WorkJobLabels.GiverDisplayName(def), reason));
            }

            RoleJobAvailability availability = RoleJobAvailabilitySummary.FromCounts(
                totalJobs, blocked.Count);

            bool hasAwfulSignal = awfulWorkTypes?.Count > 0 || awfulSkills?.Count > 0;
            if (!tooYoungForRole && !tooOldForRole
                && availability == RoleJobAvailability.Available
                && !hasAwfulSignal
                && missingRequiredSkills == null)
                return RoleCapabilityPresentation.Available;

            var warnings = new List<string>(3);
            var reasons = new List<string>(2);
            if (incapableReason != null) reasons.Add(incapableReason);
            if (noRangedWeaponReason != null) reasons.Add(noRangedWeaponReason);
            if (tooYoungReasons != null) reasons.AddRange(tooYoungReasons.Values);

            if (tooYoungForRole)
                warnings.Add("WR_RoleTooYoung".Translate(
                    pawn.LabelShortCap, role.minAge).ToString());
            if (tooOldForRole)
                warnings.Add("WR_RoleTooOld".Translate(
                    pawn.LabelShortCap, role.maxAge).ToString());
            if (missingRequiredSkills != null)
                warnings.Add("WR_RoleMissingRequiredSkills".Translate(
                    pawn.LabelShortCap,
                    new List<string>(missingRequiredSkills)
                        .ToCommaList(useAnd: true)).ToString());

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
                RoleAssignmentWarningSummary.From(availability,
                    hasVetoSignal: awfulWorkTypes?.Count > 0 || awfulPrimarySkill,
                    hasDampenedSignal: awfulSkills?.Count > 0,
                    hasMissingRequiredSkill: missingRequiredSkills != null);
            // Outside the role's age gates the assignment makes no sense at
            // all, whatever the per-job availability worked out to.
            if (tooYoungForRole || tooOldForRole)
                severity = RoleAssignmentWarningSeverity.Critical;
            return new RoleCapabilityPresentation(
                severity, string.Join("\n", warnings));
        }
    }
}
