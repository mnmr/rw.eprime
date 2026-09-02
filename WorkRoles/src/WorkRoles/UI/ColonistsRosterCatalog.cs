using System;
using System.Collections.Generic;
using RimShared.Common;
using RimWorld;
using UnityEngine;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.UI
{
    internal sealed class ColonistChipCatalogSnapshot
    {
        private readonly Dictionary<int, RoleChipRenderData> chips;

        internal ColonistChipCatalogSnapshot(
            Dictionary<int, RoleChipRenderData> chips)
        {
            this.chips = chips;
        }

        internal bool TryGet(int roleId, out RoleChipRenderData chip) =>
            chips.TryGetValue(roleId, out chip);

        internal bool ContentEquals(ColonistChipCatalogSnapshot other)
        {
            if (other == null || chips.Count != other.chips.Count) return false;
            foreach (KeyValuePair<int, RoleChipRenderData> pair in chips)
                if (!other.chips.TryGetValue(pair.Key,
                        out RoleChipRenderData value)
                    || !pair.Value.ContentEquals(value)) return false;
            return true;
        }
    }

    internal readonly struct ColonistPaletteRole
    {
        internal ColonistPaletteRole(RoleChipRenderData chip, bool enabled)
        {
            Chip = chip;
            Enabled = enabled;
        }

        internal RoleChipRenderData Chip { get; }
        internal bool Enabled { get; }

        internal bool ContentEquals(ColonistPaletteRole other) =>
            Enabled == other.Enabled && Chip.ContentEquals(other.Chip);
    }

    internal sealed class ColonistPaletteClusterSnapshot
    {
        private readonly List<ColonistPaletteRole> roles;

        internal ColonistPaletteClusterSnapshot(string label,
            List<ColonistPaletteRole> roles)
        {
            Label = label;
            this.roles = roles;
        }

        internal string Label { get; }
        internal int Count => roles.Count;
        internal ColonistPaletteRole RoleAt(int index) => roles[index];

        internal bool ContentEquals(ColonistPaletteClusterSnapshot other)
        {
            if (other == null || !string.Equals(Label, other.Label,
                    StringComparison.Ordinal) || roles.Count != other.roles.Count)
                return false;
            for (int i = 0; i < roles.Count; i++)
                if (!roles[i].ContentEquals(other.roles[i])) return false;
            return true;
        }
    }

    internal readonly struct ColonistRoleFilterOption
    {
        internal ColonistRoleFilterOption(int roleId, string label)
        {
            RoleId = roleId;
            Label = label;
        }

        internal int RoleId { get; }
        internal string Label { get; }

        internal bool ContentEquals(ColonistRoleFilterOption other) =>
            RoleId == other.RoleId
            && string.Equals(Label, other.Label, StringComparison.Ordinal);
    }

    internal readonly struct ColonistJobFilterOption
    {
        internal ColonistJobFilterOption(string defName, string label,
            string gerundLabel)
        {
            DefName = defName;
            Label = label;
            GerundLabel = gerundLabel;
        }

        internal string DefName { get; }
        internal string Label { get; }
        internal string GerundLabel { get; }

        internal bool ContentEquals(ColonistJobFilterOption other) =>
            string.Equals(DefName, other.DefName, StringComparison.Ordinal)
            && string.Equals(Label, other.Label, StringComparison.Ordinal)
            && string.Equals(GerundLabel, other.GerundLabel,
                StringComparison.Ordinal);
    }

    internal readonly struct ColonistSkillFilterOption
    {
        internal ColonistSkillFilterOption(SkillDef skill, string label)
        {
            Skill = skill;
            Label = label;
        }

        internal SkillDef Skill { get; }
        internal string Label { get; }

        internal bool ContentEquals(ColonistSkillFilterOption other) =>
            ReferenceEquals(Skill, other.Skill)
            && string.Equals(Label, other.Label, StringComparison.Ordinal);
    }

    internal readonly struct ColonistGroupOption
    {
        internal ColonistGroupOption(string key, string label)
        {
            Key = key;
            Label = label;
        }

        internal string Key { get; }
        internal string Label { get; }

        internal bool ContentEquals(ColonistGroupOption other) =>
            string.Equals(Key, other.Key, StringComparison.Ordinal)
            && string.Equals(Label, other.Label, StringComparison.Ordinal);
    }

    internal sealed class ColonistsRosterCatalogSnapshot
    {
        private sealed class RoleFacts
        {
            internal RoleFacts(RoleChipRenderData chip, bool enabled,
                string abbreviation, int gridPrefixRequirement,
                Texture2D? icon, HashSet<string> coverage)
            {
                Chip = chip;
                Enabled = enabled;
                Abbreviation = abbreviation;
                GridPrefixRequirement = gridPrefixRequirement;
                Icon = icon;
                Coverage = coverage;
            }

            internal RoleChipRenderData Chip { get; }
            internal bool Enabled { get; }
            internal string Abbreviation { get; }
            /// Fewest leading characters keeping this label distinct from
            /// every other role label (depends on the other labels, hence
            /// compared explicitly).
            internal int GridPrefixRequirement { get; }
            internal Texture2D? Icon { get; }
            internal HashSet<string> Coverage { get; }

            internal bool ContentEquals(RoleFacts other) =>
                other != null && Enabled == other.Enabled
                && Chip.ContentEquals(other.Chip)
                && string.Equals(Abbreviation, other.Abbreviation,
                    StringComparison.Ordinal)
                && GridPrefixRequirement == other.GridPrefixRequirement
                && ReferenceEquals(Icon, other.Icon)
                && Coverage.SetEquals(other.Coverage);
        }

        private readonly Dictionary<int, RoleFacts> roles;
        private readonly ColonistChipCatalogSnapshot chipCatalog;
        private readonly List<ColonistPaletteClusterSnapshot> skillClusters;
        private readonly List<ColonistPaletteClusterSnapshot> groupClusters;
        private readonly List<ColonistRoleFilterOption> roleOptions;
        private readonly List<ColonistJobFilterOption> jobOptions;
        private readonly List<ColonistSkillFilterOption> skillOptions;
        private readonly List<ColonistGroupOption> groupOptions;
        private readonly Dictionary<string, int> jobIndexes;
        private readonly Dictionary<string, SkillDef> skillsByName;
        private readonly Dictionary<string, string> groupLabels;

        private ColonistsRosterCatalogSnapshot(
            Dictionary<int, RoleFacts> roles,
            ColonistChipCatalogSnapshot chipCatalog,
            List<ColonistPaletteClusterSnapshot> skillClusters,
            List<ColonistPaletteClusterSnapshot> groupClusters,
            List<ColonistRoleFilterOption> roleOptions,
            List<ColonistJobFilterOption> jobOptions,
            List<ColonistSkillFilterOption> skillOptions,
            List<ColonistGroupOption> groupOptions,
            ColonistsRosterCatalogSnapshot? previous)
        {
            this.roles = roles;
            this.chipCatalog = chipCatalog;
            this.skillClusters = skillClusters;
            this.groupClusters = groupClusters;
            this.roleOptions = roleOptions;
            this.jobOptions = jobOptions;
            this.skillOptions = skillOptions;
            this.groupOptions = groupOptions;
            if (previous != null
                && ReferenceEquals(jobOptions, previous.jobOptions))
                jobIndexes = previous.jobIndexes;
            else
            {
                jobIndexes = new Dictionary<string, int>(jobOptions.Count,
                    StringComparer.Ordinal);
                for (int i = 0; i < jobOptions.Count; i++)
                    jobIndexes[jobOptions[i].DefName] = i;
            }
            if (previous != null
                && ReferenceEquals(skillOptions, previous.skillOptions))
                skillsByName = previous.skillsByName;
            else
            {
                skillsByName = new Dictionary<string, SkillDef>(
                    skillOptions.Count, StringComparer.Ordinal);
                for (int i = 0; i < skillOptions.Count; i++)
                    skillsByName[skillOptions[i].Skill.defName] =
                        skillOptions[i].Skill;
            }
            if (previous != null
                && ReferenceEquals(groupOptions, previous.groupOptions))
                groupLabels = previous.groupLabels;
            else
            {
                groupLabels = new Dictionary<string, string>(
                    groupOptions.Count, StringComparer.Ordinal);
                for (int i = 0; i < groupOptions.Count; i++)
                    groupLabels[groupOptions[i].Key] = groupOptions[i].Label;
            }
        }

        internal static ColonistsRosterCatalogSnapshot Build(RoleStore store,
            ColonistsRosterCatalogSnapshot? previous,
            bool rebuildDefinitions, bool rebuildGroups,
            bool forceNewOwnedSnapshots)
        {
            var labels = new List<(int id, string label)>(store.roles.Count);
            var plainLabels = new List<string>(store.roles.Count);
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                labels.Add((role.id, role.label));
                plainLabels.Add(role.label);
            }
            Dictionary<int, string> abbreviations =
                RoleAbbreviations.Build(labels);
            int[] gridPrefixRequirements =
                GridLabelTruncation.RequiredPrefixLengths(plainLabels);

            var roles = new Dictionary<int, RoleFacts>(store.roles.Count);
            var detachedChips = new Dictionary<int, RoleChipRenderData>(
                store.roles.Count);
            var roleOptions = new List<ColonistRoleFilterOption>(
                store.roles.Count);
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                string abbreviation = abbreviations.TryGetValue(role.id,
                    out string value) ? value : role.label;
                RoleChipRenderData chip = RoleChipRenderData.From(role);
                RoleIconPresentation icon =
                    RoleIconPresentationCatalog.For(role.id);
                detachedChips[role.id] = chip;
                roles[role.id] = new RoleFacts(chip,
                    role.enabled, abbreviation, gridPrefixRequirements[i],
                    icon.Texture,
                    new HashSet<string>(role.Coverage(),
                        StringComparer.Ordinal));
                roleOptions.Add(new ColonistRoleFilterOption(role.id,
                    role.label));
            }
            roleOptions.Sort((left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Label,
                    right.Label));
            var chipCatalog = new ColonistChipCatalogSnapshot(detachedChips);
            if (!forceNewOwnedSnapshots && previous != null
                && previous.chipCatalog.ContentEquals(chipCatalog))
                chipCatalog = previous.chipCatalog;

            List<ColonistJobFilterOption> jobOptions;
            List<ColonistSkillFilterOption> skillOptions;
            if (!rebuildDefinitions && previous != null)
            {
                jobOptions = previous.jobOptions;
                skillOptions = previous.skillOptions;
            }
            else
            {
                jobOptions = new List<ColonistJobFilterOption>();
                List<WorkGiverDef> workGivers =
                    DefDatabase<WorkGiverDef>.AllDefsListForReading;
                for (int i = 0; i < workGivers.Count; i++)
                {
                    WorkGiverDef def = workGivers[i];
                    if (def.workType == null) continue;
                    jobOptions.Add(new ColonistJobFilterOption(def.defName,
                        WorkJobLabels.GiverDisplayName(def),
                        def.workType.gerundLabel));
                }
                jobOptions.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(left.Label,
                        right.Label));

                skillOptions = new List<ColonistSkillFilterOption>();
                List<SkillDef> skills =
                    DefDatabase<SkillDef>.AllDefsListForReading;
                for (int i = 0; i < skills.Count; i++)
                    skillOptions.Add(new ColonistSkillFilterOption(skills[i],
                        skills[i].skillLabel.CapitalizeFirst()));
            }

            List<ColonistGroupOption> groupOptions;
            if (!rebuildGroups && previous != null)
                groupOptions = previous.groupOptions;
            else
            {
                List<GroupSourceDef> sources = GroupSources.All();
                groupOptions = new List<ColonistGroupOption>(sources.Count);
                for (int i = 0; i < sources.Count; i++)
                    groupOptions.Add(new ColonistGroupOption(sources[i].Key,
                        sources[i].Label));
            }

            return new ColonistsRosterCatalogSnapshot(roles, chipCatalog,
                BuildSkillClusters(store, roles, skillOptions),
                BuildGroupClusters(store, roles), roleOptions, jobOptions,
                skillOptions, groupOptions, previous);
        }

        private static List<ColonistPaletteClusterSnapshot> BuildSkillClusters(
            RoleStore store, Dictionary<int, RoleFacts> roles,
            List<ColonistSkillFilterOption> skillOptions)
        {
            var skillLabels = new List<string>();
            var skillRoles = new List<List<ColonistPaletteRole>>();
            List<ColonistPaletteRole>? everyone = null;
            List<ColonistPaletteRole>? unskilled = null;
            string? everyoneLabel = null;
            string? unskilledLabel = null;

            int ClusterIndex(string label)
            {
                for (int i = 0; i < skillLabels.Count; i++)
                    if (string.Equals(skillLabels[i], label,
                            StringComparison.Ordinal)) return i;
                skillLabels.Add(label);
                skillRoles.Add(new List<ColonistPaletteRole>());
                return skillLabels.Count - 1;
            }

            List<ColonistPaletteRole> RolesFor(Role root)
            {
                if (root.autoAssign)
                {
                    everyoneLabel = everyoneLabel
                        ?? "WR_ClusterEveryone".Translate();
                    return everyone ?? (everyone =
                        new List<ColonistPaletteRole>());
                }
                string primaryName = RecsAdapter.PrimarySkillOf(root) ?? "";
                SkillDef? primary = null;
                for (int i = 0; i < skillOptions.Count; i++)
                    if (string.Equals(skillOptions[i].Skill.defName,
                            primaryName, StringComparison.Ordinal))
                    {
                        primary = skillOptions[i].Skill;
                        break;
                    }
                if (primary == null)
                {
                    unskilledLabel = unskilledLabel
                        ?? "WR_ClusterUnskilled".Translate();
                    return unskilled ?? (unskilled =
                        new List<ColonistPaletteRole>());
                }
                return skillRoles[ClusterIndex(primary.LabelCap)];
            }

            var seen = new HashSet<int>();
            Role? root = null;
            var tree = RolesListState.BuildRoleTree(store).rows;
            for (int i = 0; i < tree.Count; i++)
            {
                (Role role, _, int depth, bool virtualRow) = tree[i];
                if (depth == 0) root = role;
                if (virtualRow || !seen.Add(role.id) || root == null
                    || !roles.TryGetValue(role.id, out RoleFacts facts))
                    continue;
                RolesFor(depth == 0 ? role : root).Add(
                    new ColonistPaletteRole(facts.Chip, facts.Enabled));
            }

            var result = new List<ColonistPaletteClusterSnapshot>();
            if (everyone != null)
                result.Add(new ColonistPaletteClusterSnapshot(everyoneLabel!, // set together with the list
                    everyone));
            for (int i = 0; i < skillLabels.Count; i++)
                result.Add(new ColonistPaletteClusterSnapshot(skillLabels[i],
                    skillRoles[i]));
            if (unskilled != null)
                result.Add(new ColonistPaletteClusterSnapshot(unskilledLabel!, // set together with the list
                    unskilled));
            return result;
        }

        private static List<ColonistPaletteClusterSnapshot> BuildGroupClusters(
            RoleStore store, Dictionary<int, RoleFacts> roles)
        {
            IReadOnlyList<RoleSection> sections =
                RolesListState.BuildSections(store, nested: true);
            var result = new List<ColonistPaletteClusterSnapshot>(
                sections.Count);
            for (int sectionIndex = 0; sectionIndex < sections.Count;
                    sectionIndex++)
            {
                RoleSection section = sections[sectionIndex];
                var clusterRoles = new List<ColonistPaletteRole>();
                var seen = new HashSet<int>();
                for (int rowIndex = 0; rowIndex < section.rows.Count;
                        rowIndex++)
                {
                    var row = section.rows[rowIndex];
                    if (row.virtualRow || !seen.Add(row.role.id)
                        || !roles.TryGetValue(row.role.id,
                            out RoleFacts facts))
                        continue;
                    clusterRoles.Add(new ColonistPaletteRole(facts.Chip,
                        facts.Enabled));
                }
                if (clusterRoles.Count > 0)
                    result.Add(new ColonistPaletteClusterSnapshot(
                        section.title, clusterRoles));
            }
            return result;
        }

        internal int PaletteClusterCount(PaletteMode mode) =>
            (mode == PaletteMode.Groups ? groupClusters : skillClusters).Count;

        internal ColonistPaletteClusterSnapshot PaletteClusterAt(
            PaletteMode mode, int index) =>
            (mode == PaletteMode.Groups ? groupClusters : skillClusters)[index];

        internal bool TryGetChip(int roleId, out RoleChipRenderData chip)
            => chipCatalog.TryGet(roleId, out chip);

        internal ColonistChipCatalogSnapshot ChipCatalog => chipCatalog;

        internal bool IsRoleEnabled(int roleId) =>
            roles.TryGetValue(roleId, out RoleFacts facts) && facts.Enabled;

        internal string? AbbreviationFor(int roleId) =>
            roles.TryGetValue(roleId, out RoleFacts facts)
                ? facts.Abbreviation : null;

        /// Fewest leading characters keeping the role's label distinct from
        /// the other roles (before the player's floor); full-name grid chips
        /// truncate to it. One for unknown roles: nothing to collide with.
        internal int GridPrefixRequirementFor(int roleId) =>
            roles.TryGetValue(roleId, out RoleFacts facts)
                ? facts.GridPrefixRequirement : 1;

        internal Texture2D? IconFor(int roleId) =>
            roles.TryGetValue(roleId, out RoleFacts facts)
                ? facts.Icon : null;

        internal bool ContainsRole(int roleId) => roles.ContainsKey(roleId);

        internal bool RoleCoversJob(int roleId, string giverDefName) =>
            roles.TryGetValue(roleId, out RoleFacts facts)
            && !facts.Chip.Blocker && facts.Coverage.Contains(giverDefName);

        internal void AddRolesCovering(int selectedRoleId,
            HashSet<int> result)
        {
            if (!roles.TryGetValue(selectedRoleId,
                    out RoleFacts selected)) return;
            foreach (KeyValuePair<int, RoleFacts> pair in roles)
                if (pair.Key != selectedRoleId && !pair.Value.Chip.Blocker
                    && selected.Coverage.Count > 0
                    && selected.Coverage.IsSubsetOf(pair.Value.Coverage))
                    result.Add(pair.Key);
        }

        internal int RoleOptionCount => roleOptions.Count;
        internal ColonistRoleFilterOption RoleOptionAt(int index) =>
            roleOptions[index];

        internal string? RoleLabelOrNull(int roleId) =>
            roles.TryGetValue(roleId, out RoleFacts facts)
                ? facts.Chip.Label : null;

        internal HashSet<int>? SearchMatchingRoles(string term)
        {
            HashSet<int>? result = null;
            for (int i = 0; i < roleOptions.Count; i++)
            {
                ColonistRoleFilterOption option = roleOptions[i];
                if (option.Label.IndexOf(term,
                        StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                (result ?? (result = new HashSet<int>())).Add(option.RoleId);
            }
            return result;
        }

        internal int JobOptionCount => jobOptions.Count;
        internal ColonistJobFilterOption JobOptionAt(int index) =>
            jobOptions[index];

        internal bool ContainsJob(string? defName)
            => defName != null && jobIndexes.ContainsKey(defName);

        internal string? JobLabelOrNull(string? defName)
        {
            return defName != null && jobIndexes.TryGetValue(defName,
                out int index) ? jobOptions[index].Label : null;
        }

        internal HashSet<string>? SearchMatchingGivers(string? term)
        {
            if (term.NullOrEmpty()) return null;
            HashSet<string>? result = null;
            for (int i = 0; i < jobOptions.Count; i++)
            {
                ColonistJobFilterOption option = jobOptions[i];
                if (option.Label.IndexOf(term,
                        StringComparison.OrdinalIgnoreCase) < 0
                    && (option.GerundLabel == null
                        || option.GerundLabel.IndexOf(term,
                            StringComparison.OrdinalIgnoreCase) < 0))
                    continue;
                (result ?? (result = new HashSet<string>(
                    StringComparer.Ordinal))).Add(option.DefName);
            }
            return result;
        }

        internal int SkillOptionCount => skillOptions.Count;
        internal ColonistSkillFilterOption SkillOptionAt(int index) =>
            skillOptions[index];

        internal SkillDef? SkillOrNull(string? defName)
            => !defName.NullOrEmpty() && skillsByName.TryGetValue(defName!,
                out SkillDef skill) ? skill : null;

        internal int GroupOptionCount => groupOptions.Count;
        internal ColonistGroupOption GroupOptionAt(int index) =>
            groupOptions[index];

        internal string? GroupLabelOrNull(string? key)
            => key != null && groupLabels.TryGetValue(key, out string label)
                ? label : null;

        internal bool ContentEquals(ColonistsRosterCatalogSnapshot other)
        {
            if (other == null || roles.Count != other.roles.Count
                || skillClusters.Count != other.skillClusters.Count
                || groupClusters.Count != other.groupClusters.Count
                || roleOptions.Count != other.roleOptions.Count
                || jobOptions.Count != other.jobOptions.Count
                || skillOptions.Count != other.skillOptions.Count
                || groupOptions.Count != other.groupOptions.Count)
                return false;
            foreach (KeyValuePair<int, RoleFacts> pair in roles)
                if (!other.roles.TryGetValue(pair.Key, out RoleFacts value)
                    || !pair.Value.ContentEquals(value)) return false;
            for (int i = 0; i < skillClusters.Count; i++)
                if (!skillClusters[i].ContentEquals(other.skillClusters[i]))
                    return false;
            for (int i = 0; i < groupClusters.Count; i++)
                if (!groupClusters[i].ContentEquals(other.groupClusters[i]))
                    return false;
            for (int i = 0; i < roleOptions.Count; i++)
                if (!roleOptions[i].ContentEquals(other.roleOptions[i]))
                    return false;
            if (!ReferenceEquals(jobOptions, other.jobOptions))
                for (int i = 0; i < jobOptions.Count; i++)
                    if (!jobOptions[i].ContentEquals(other.jobOptions[i]))
                        return false;
            if (!ReferenceEquals(skillOptions, other.skillOptions))
                for (int i = 0; i < skillOptions.Count; i++)
                    if (!skillOptions[i].ContentEquals(other.skillOptions[i]))
                        return false;
            if (!ReferenceEquals(groupOptions, other.groupOptions))
                for (int i = 0; i < groupOptions.Count; i++)
                    if (!groupOptions[i].ContentEquals(other.groupOptions[i]))
                        return false;
            return true;
        }
    }
}
