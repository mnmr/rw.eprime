using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    /// Owns derived presentation state for the Roles editor. The view keeps
    /// interaction, commands, scrolling, and rendering; this object snapshots
    /// the translated labels and domain projections consumed by those passes.
    internal sealed class RoleEditorState
    {
        private int tipsStamp = -1;
        private StructuredTip blockerTip;
        private StructuredTip holdersTip;
        private float tuningLabelWidth = -1f;
        private float tuningButtonWidth = -1f;

        private List<RoleSkillPresentation> skillsUsed;
        private int skillsStamp = -1;
        private int skillsRoleId = -1;

        private List<RoleHolderPresentation> holders;
        private ScopeCacheStamp holdersStamp = ScopeCacheStamp.Invalid;
        private int holdersRoleId = -1;

        private HashSet<int> deadEntries;
        private int deadEntriesStamp = -1;
        private int deadEntriesRoleId = -1;

        private readonly Dictionary<(JobEntryKind kind, string defName),
            (string type, string job, bool missing)> entryLabels =
                new Dictionary<(JobEntryKind, string), (string, string, bool)>();
        private readonly Dictionary<string, string> typeTruncations =
            new Dictionary<string, string>();
        private readonly Dictionary<string, string> jobTruncations =
            new Dictionary<string, string>();
        private float typeTruncationWidth = -1f;
        private float jobTruncationWidth = -1f;

        private HashSet<string> uncoveredGivers;
        private HashSet<string> uncoveredTypes;
        private string uncoveredWarning;
        private int uncoveredStamp = -1;
        private RoleCoveragePresentation coverage;

        private List<RoleJobTreeNode> treeNodes;
        private int treeNodesStamp = -1;
        private int treeNodesRevision = -1;
        private string treeNodesFilter;
        private int treeRevision;
        private readonly HashSet<string> expandedWorkTypes = new HashSet<string>();

        private int entrySetsStamp = -1;
        private int entrySetsRoleId = -1;
        private readonly HashSet<string> entryTypes = new HashSet<string>();
        private readonly HashSet<string> entryGivers = new HashSet<string>();

        internal string Filter { get; set; } = "";

        internal StructuredTip BlockerTip
        {
            get { EnsureTips(); return blockerTip; }
        }

        internal StructuredTip HoldersTip
        {
            get { EnsureTips(); return holdersTip; }
        }

        internal float TuningLabelWidth
        {
            get { EnsureTuningMetrics(); return tuningLabelWidth; }
        }

        internal float TuningButtonWidth
        {
            get { EnsureTuningMetrics(); return tuningButtonWidth; }
        }

        internal void Reset()
        {
            Filter = "";
            expandedWorkTypes.Clear();
            treeRevision++;
            holders = null;
            holdersStamp = ScopeCacheStamp.Invalid;
            holdersRoleId = -1;
            deadEntries = null;
            deadEntriesStamp = -1;
            deadEntriesRoleId = -1;
            entrySetsStamp = -1;
            entrySetsRoleId = -1;
            entryTypes.Clear();
            entryGivers.Clear();
            InvalidateLanguageCaches();
        }

        internal void InvalidateLanguageCaches()
        {
            tipsStamp = -1;
            blockerTip = null;
            holdersTip = null;
            tuningLabelWidth = tuningButtonWidth = -1f;
            skillsUsed = null;
            skillsStamp = -1;
            skillsRoleId = -1;
            ClearEntryLabels();
            uncoveredGivers = null;
            uncoveredTypes = null;
            uncoveredWarning = null;
            coverage = null;
            uncoveredStamp = -1;
            treeNodes = null;
            treeNodesStamp = -1;
        }

        private void EnsureTips()
        {
            if (tipsStamp == UiVersion.Current) return;
            tipsStamp = UiVersion.Current;
            tuningLabelWidth = tuningButtonWidth = -1f;

            var blocker = new TipModel { Title = "WR_BlockerRole".Translate() };
            blocker.AddSection().Text("WR_BlockerRoleTipWhat".Translate());
            blocker.AddSection().Text("WR_BlockerRoleTipWhy".Translate(), dim: true);
            blockerTip = new StructuredTip("roles:blocker", blocker);

            var holderModel = new TipModel();
            holderModel.AddSection().Text("WR_HoldersTipWhat".Translate());
            holderModel.AddSection()
                .Fact("WR_HoldersAuto".Translate(), "WR_HoldersTipAuto".Translate())
                .Fact("WR_HoldersCustom".Translate(), "WR_HoldersTipCustom".Translate())
                .Fact("WR_HoldersWaivers".Translate(), "WR_HoldersTipWaivers".Translate())
                .Fact("WR_HoldersNever".Translate(), "WR_HoldersTipNever".Translate());
            holdersTip = new StructuredTip("roles:holders", holderModel);
        }

        private void EnsureTuningMetrics()
        {
            EnsureTips();
            if (tuningLabelWidth >= 0f) return;
            Text.Font = GameFont.Small;
            tuningLabelWidth = Mathf.Max(
                Mathf.Max(WrText.FitWidth("WR_HoldersAuto".Translate()),
                    Mathf.Max(WrText.FitWidth("WR_HoldersNever".Translate()),
                        WrText.FitWidth("WR_HoldersCustom".Translate()))),
                Mathf.Max(WrText.FitWidth("WR_HoldersMin".Translate()),
                    Mathf.Max(WrText.FitWidth("WR_HoldersMax".Translate()),
                        WrText.FitWidth("WR_HoldersWaivers".Translate())))) + 10f;
            tuningButtonWidth = WrText.FitWidth("WR_HoldersUncapped".Translate()) + 16f;
        }

        internal IReadOnlyList<RoleSkillPresentation> SkillsUsed(Role role)
        {
            if (skillsUsed == null || skillsStamp != UiVersion.Current
                || skillsRoleId != role.id)
            {
                skillsStamp = UiVersion.Current;
                skillsRoleId = role.id;
                skillsUsed = RoleSkillProfiles.ForRole(role)
                    .Select(skill => new RoleSkillPresentation(
                        SkillLabel(skill.SkillDefName), skill.Primary))
                    .ToList();
            }
            return skillsUsed;
        }

        private static string SkillLabel(string defName)
        {
            SkillDef skill = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return skill == null ? defName
                : (skill.skillLabel ?? skill.label ?? skill.defName).CapitalizeFirst();
        }

        internal IReadOnlyList<RoleHolderPresentation> Holders(Role role, RoleStore store,
            IReadOnlyList<Pawn> pawns, int pawnRevision)
        {
            var stamp = new ScopeCacheStamp(UiVersion.Current, pawnRevision);
            if (holders == null || holdersStamp != stamp || holdersRoleId != role.id)
            {
                holdersStamp = stamp;
                holdersRoleId = role.id;
                holders = new List<RoleHolderPresentation>();
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (!store.pawnSets.TryGetValue(pawn, out PawnRoleSet set)) continue;
                    int position = set.assignments.FindIndex(a => a.roleId == role.id);
                    if (position >= 0)
                        holders.Add(new RoleHolderPresentation(pawn, position + 1));
                }
                holders.Sort((a, b) => a.Position.CompareTo(b.Position));
            }
            return holders;
        }

        internal IReadOnlyCollection<int> DeadEntryIndexes(Role role)
        {
            if (deadEntries == null || deadEntriesStamp != UiVersion.Current
                || deadEntriesRoleId != role.id)
            {
                deadEntriesStamp = UiVersion.Current;
                deadEntriesRoleId = role.id;
                deadEntries = JobOrderCompiler.DeadEntryIndexes(
                    role.entries, GameJobCatalog.Instance);
            }
            return deadEntries;
        }

        internal RoleEntryPresentation EntryPresentation(JobEntry entry,
            float typeWidth, float jobWidth)
        {
            var key = (entry.Kind, entry.DefName);
            if (!entryLabels.TryGetValue(key, out var labels))
            {
                labels = ResolveEntryLabels(entry);
                entryLabels[key] = labels;
            }
            string shownType = Truncate(labels.type, typeWidth,
                typeTruncations, ref typeTruncationWidth);
            string shownJob = Truncate(labels.job, jobWidth,
                jobTruncations, ref jobTruncationWidth);
            return new RoleEntryPresentation(labels.type, labels.job,
                shownType, shownJob, labels.missing);
        }

        private static (string type, string job, bool missing) ResolveEntryLabels(JobEntry entry)
        {
            if (entry.Kind == JobEntryKind.WorkType)
            {
                WorkTypeDef def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                    return ((def.gerundLabel ?? def.labelShort ?? def.defName).CapitalizeFirst(),
                        "WR_AllJobs".Translate(), false);
            }
            else
            {
                WorkGiverDef def = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                if (def != null)
                    return (def.workType != null
                            ? (def.workType.gerundLabel ?? def.workType.labelShort
                                ?? def.workType.defName).CapitalizeFirst()
                            : "?",
                        WorkJobLabels.GiverDisplayName(def), false);
            }
            return (entry.DefName, "", true);
        }

        private static string Truncate(string value, float width,
            Dictionary<string, string> cache, ref float cachedWidth)
        {
            if (!Mathf.Approximately(width, cachedWidth))
            {
                cache.Clear();
                cachedWidth = width;
            }
            return value.Truncate(width, cache);
        }

        private void ClearEntryLabels()
        {
            entryLabels.Clear();
            typeTruncations.Clear();
            jobTruncations.Clear();
            typeTruncationWidth = jobTruncationWidth = -1f;
        }

        internal RoleCoveragePresentation Coverage(RoleStore store)
        {
            if (uncoveredGivers != null && uncoveredStamp == UiVersion.Current)
                return coverage;

            uncoveredStamp = UiVersion.Current;
            var covered = new HashSet<string>();
            foreach (Role role in store.roles)
                if (!role.blocker)
                    covered.UnionWith(role.Coverage());

            uncoveredGivers = new HashSet<string>();
            uncoveredTypes = new HashSet<string>();
            foreach (WorkGiverDef giver in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (giver.workType == null || covered.Contains(giver.defName)) continue;
                uncoveredGivers.Add(giver.defName);
                uncoveredTypes.Add(giver.workType.defName);
            }
            uncoveredWarning = uncoveredGivers.Count == 0 ? null
                : "WR_WarningPrefix".Translate() + " " + "WR_UnusedJobsWarning".Translate();
            coverage = new RoleCoveragePresentation(
                uncoveredGivers, uncoveredTypes, uncoveredWarning);
            return coverage;
        }

        internal IReadOnlyList<RoleJobTreeNode> TreeNodes(bool filtering)
        {
            if (treeNodes != null && treeNodesStamp == UiVersion.Current
                && treeNodesRevision == treeRevision && treeNodesFilter == Filter)
                return treeNodes;

            treeNodesStamp = UiVersion.Current;
            treeNodesRevision = treeRevision;
            treeNodesFilter = Filter;
            treeNodes = new List<RoleJobTreeNode>();
            foreach (WorkTypeDef type in DefDatabase<WorkTypeDef>.AllDefsListForReading
                .OrderByDescending(t => t.naturalPriority))
            {
                List<WorkGiverDef> givers = type.workGiversByPriority;
                string typeName = (type.gerundLabel ?? type.labelShort ?? type.defName)
                    .CapitalizeFirst();
                bool typeMatches = !filtering || Matches(typeName);
                List<WorkGiverDef> matching = null;
                if (filtering && !typeMatches)
                {
                    matching = new List<WorkGiverDef>();
                    for (int i = 0; i < givers.Count; i++)
                        if (Matches(WorkJobLabels.GiverDisplayName(givers[i])))
                            matching.Add(givers[i]);
                    if (matching.Count == 0) continue;
                }

                treeNodes.Add(new RoleJobTreeNode(type, null,
                    typeName + " (" + givers.Count + ")"));
                if (!filtering && !expandedWorkTypes.Contains(type.defName)) continue;

                IReadOnlyList<WorkGiverDef> visible = matching ?? givers;
                for (int i = 0; i < visible.Count; i++)
                    treeNodes.Add(new RoleJobTreeNode(
                        type, visible[i], WorkJobLabels.GiverDisplayName(visible[i])));
            }
            return treeNodes;
        }

        private bool Matches(string value) => value != null
            && value.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) >= 0;

        internal bool IsWorkTypeExpanded(string defName)
            => expandedWorkTypes.Contains(defName);

        internal void EnsureWorkTypeExpanded(string defName)
        {
            if (expandedWorkTypes.Add(defName)) treeRevision++;
        }

        internal void ToggleWorkTypeExpanded(string defName)
        {
            if (!expandedWorkTypes.Add(defName)) expandedWorkTypes.Remove(defName);
            treeRevision++;
        }

        internal static (WorkTypeDef type, WorkGiverDef giver)? FirstEntryTreeTarget(Role role)
        {
            foreach (JobEntry entry in role.entries)
            {
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    WorkTypeDef type = DefDatabase<WorkTypeDef>
                        .GetNamedSilentFail(entry.DefName);
                    if (type != null) return (type, null);
                }
                else
                {
                    WorkGiverDef giver = DefDatabase<WorkGiverDef>
                        .GetNamedSilentFail(entry.DefName);
                    if (giver?.workType != null) return (giver.workType, giver);
                }
            }
            return null;
        }

        internal MultiCheckboxState WorkTypeState(Role role, WorkTypeDef type)
        {
            EnsureEntrySets(role);
            if (entryTypes.Contains(type.defName)) return MultiCheckboxState.On;
            List<WorkGiverDef> givers = type.workGiversByPriority;
            for (int i = 0; i < givers.Count; i++)
                if (entryGivers.Contains(givers[i].defName))
                    return MultiCheckboxState.Partial;
            return MultiCheckboxState.Off;
        }

        internal MultiCheckboxState GiverState(Role role, WorkTypeDef type,
            WorkGiverDef giver)
        {
            EnsureEntrySets(role);
            if (entryGivers.Contains(giver.defName)) return MultiCheckboxState.On;
            return entryTypes.Contains(type.defName)
                ? MultiCheckboxState.Partial
                : MultiCheckboxState.Off;
        }

        private void EnsureEntrySets(Role role)
        {
            if (entrySetsStamp == UiVersion.Current && entrySetsRoleId == role.id) return;
            entrySetsStamp = UiVersion.Current;
            entrySetsRoleId = role.id;
            entryTypes.Clear();
            entryGivers.Clear();
            foreach (JobEntry entry in role.entries)
                (entry.Kind == JobEntryKind.WorkType ? entryTypes : entryGivers)
                    .Add(entry.DefName);
        }

    }

    internal readonly struct RoleSkillPresentation
    {
        internal RoleSkillPresentation(string label, bool primary)
        {
            Label = label;
            Primary = primary;
        }

        internal string Label { get; }
        internal bool Primary { get; }
    }

    internal readonly struct RoleHolderPresentation
    {
        internal RoleHolderPresentation(Pawn pawn, int position)
        {
            Pawn = pawn;
            Position = position;
        }

        internal Pawn Pawn { get; }
        internal int Position { get; }
    }

    internal readonly struct RoleEntryPresentation
    {
        internal RoleEntryPresentation(string typeLabel, string jobLabel,
            string typeShown, string jobShown, bool missing)
        {
            TypeLabel = typeLabel;
            JobLabel = jobLabel;
            TypeShown = typeShown;
            JobShown = jobShown;
            Missing = missing;
        }

        internal string TypeLabel { get; }
        internal string JobLabel { get; }
        internal string TypeShown { get; }
        internal string JobShown { get; }
        internal bool Missing { get; }
    }

    internal sealed class RoleCoveragePresentation
    {
        internal RoleCoveragePresentation(IReadOnlyCollection<string> givers,
            IReadOnlyCollection<string> workTypes, string warning)
        {
            Givers = givers;
            WorkTypes = workTypes;
            Warning = warning;
        }

        internal IReadOnlyCollection<string> Givers { get; }
        internal IReadOnlyCollection<string> WorkTypes { get; }
        internal string Warning { get; }
    }

    internal readonly struct RoleJobTreeNode
    {
        internal RoleJobTreeNode(WorkTypeDef type, WorkGiverDef giver, string label)
        {
            Type = type;
            Giver = giver;
            Label = label;
        }

        internal WorkTypeDef Type { get; }
        internal WorkGiverDef Giver { get; }
        internal string Label { get; }
    }
}
