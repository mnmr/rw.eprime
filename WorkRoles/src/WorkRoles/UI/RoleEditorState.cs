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
        // Owner: Roles window. Key: UiVersion/language generation. Value:
        // immutable blocker and holder StructuredTip models. Dependencies: role
        // terminology and language. Refresh: lazy on first tip read after a
        // revision change. Equality: a matching stamp preserves tip identity.
        // Teardown: Reset/language invalidation releases both tip references.
        private int tipsStamp = -1;
        private StructuredTip blockerTip;
        private StructuredTip holdersTip;

        // Owner: Roles window. Key: language generation for label/button widths,
        // then (editor width, row height, holder mode) for the layout. Value:
        // measured scalars and an immutable RoleTuningLayout. Dependencies:
        // translated tuning text, font, available width, row height, and mode.
        // Refresh: lazy after language or geometry key change. Equality: exact
        // keys reuse the measured values/layout identity. Teardown: Reset/language
        // invalidation clears every metric and the layout reference.
        private float tuningLabelWidth = -1f;
        private float tuningButtonWidth = -1f;
        private float tuningLayoutWidth = -1f;
        private float tuningLayoutRowHeight = -1f;
        private RoleHolderMode tuningLayoutMode;
        private RoleTuningLayout tuningLayout;

        // Owner: Roles window. Key: (UiVersion, selected role id). Value:
        // producer-owned immutable skill presentations. Dependencies: role job
        // coverage, definition labels, role revision, and language. Refresh: lazy
        // on first read after key change. Equality: exact keys reuse list/item
        // identity. Teardown: Reset/language invalidation releases the list.
        private List<RoleSkillPresentation> skillsUsed;
        private int skillsStamp = -1;
        private int skillsRoleId = -1;

        // Owner: Roles window. Key: selected role id plus ScopeCacheStamp.
        // Value: producer-owned holder name/position projections. Dependencies:
        // role assignments, listed-pawn cohort, pawn-scope revision, and language.
        // Refresh: immediate on the next Holders read after key change. Equality:
        // exact keys reuse list identity. Teardown: Reset releases holder rows,
        // owner references, and the remembered scope stamp.
        private List<RoleHolderPresentation> holders;
        private ScopeCacheStamp holdersStamp = ScopeCacheStamp.Invalid;
        private int holdersRoleId = -1;

        // Owner: Roles window. Key: (UiVersion, selected role id). Value: private
        // set of selected-entry indexes that compile to no jobs. Dependencies:
        // role entries and the game job catalog represented by UiVersion.
        // Refresh: lazy on first read after key change. Equality: exact keys reuse
        // set identity. Teardown: Reset releases the set and stamps.
        private HashSet<int> deadEntries;
        private int deadEntriesStamp = -1;
        private int deadEntriesRoleId = -1;

        // Owner: Roles window. Key: (entry kind, defName), with separate available
        // widths for truncation tables. Value: immutable resolved/full/truncated
        // label strings and a missing-def flag. Dependencies: definition catalog,
        // language, font, and type/job column widths. Refresh: lazy on a label miss;
        // width changes immediately clear only the affected truncations. Equality:
        // exact key/width hits preserve strings. Teardown: ClearEntryLabels on
        // Reset/language invalidation clears labels, truncations, and width keys.
        private readonly Dictionary<(JobEntryKind kind, string defName),
            (string type, string job, bool missing)> entryLabels =
                new Dictionary<(JobEntryKind, string), (string, string, bool)>();
        private readonly Dictionary<string, string> typeTruncations =
            new Dictionary<string, string>();
        private readonly Dictionary<string, string> jobTruncations =
            new Dictionary<string, string>();
        private float typeTruncationWidth = -1f;
        private float jobTruncationWidth = -1f;

        // Owner: Roles window. Key: UiVersion. Value: private producer-owned sets
        // and immutable RoleCoveragePresentation/warning string. Dependencies:
        // all non-blocker role coverage, job definitions, and language. Refresh:
        // lazy on first Coverage read after revision change. Equality: matching
        // revision reuses sets and presentation identity. Teardown: Reset/language
        // invalidation releases the complete coverage projection.
        private HashSet<string> uncoveredGivers;
        private HashSet<string> uncoveredTypes;
        private string uncoveredWarning;
        private int uncoveredStamp = -1;
        private RoleCoveragePresentation coverage;
        // Owner: view. Key: (warning string identity, available width).
        // Value: wrapped Small-font warning height. Dependencies: coverage
        // generation, language, font, and width. Refresh: immediately on key
        // change. Equality: identical keys reuse the measured float. Teardown:
        // Reset/language invalidation clears the single slot.
        private string measuredCoverageWarning;
        private float measuredCoverageWarningWidth = -1f;
        private float measuredCoverageWarningHeight;

        // Owner: Roles window. Key: UiVersion, selected role id, local expansion
        // revision, and search filter. Value: producer-owned immutable job-tree
        // row projections. Dependencies: role entries, definition/job catalogs,
        // coverage, language, filter, and expansion state. Refresh: immediate on
        // the next TreeNodes read after key change. Equality: exact keys preserve
        // list/row identity. Teardown: Reset/language invalidation releases rows.
        private List<RoleJobTreeNode> treeNodes;
        private int treeNodesStamp = -1;
        private int treeNodesRoleId = -1;
        private int treeNodesRevision = -1;
        private string treeNodesFilter;
        private int treeRevision;
        private readonly HashSet<string> expandedWorkTypes = new HashSet<string>();

        // Owner: Roles window builder. Key: (UiVersion, selected role id). Value:
        // private work-type/work-giver membership indexes. Dependencies: selected
        // role entries only. Refresh: lazy before state derivation after key change.
        // Equality: exact keys reuse both set instances and contents. Teardown:
        // Reset clears both sets and invalidates their keys.
        private int entrySetsStamp = -1;
        private int entrySetsRoleId = -1;
        private readonly HashSet<string> entryTypes = new HashSet<string>();
        private readonly HashSet<string> entryGivers = new HashSet<string>();

        // Owner: window. Key: selected role identity. Value: immutable editor
        // render projection with producer-owned buffers hidden behind indexed
        // accessors. Dependencies: UiVersion, location and pawn-scope revisions,
        // editor width, language, filter/expansion state, and local rules/tuning
        // disclosure. Refresh: immediate when any dependency changes. Equality:
        // unchanged dependencies preserve the snapshot identity. Teardown:
        // Reset/language invalidation releases the complete projection.
        private RoleEditorSnapshot editorSnapshot;
        private int editorSnapshotStamp = -1;
        private int editorSnapshotRoleId = -1;
        private int editorSnapshotLocationRevision = -1;
        private int editorSnapshotPawnRevision = -1;
        private int editorSnapshotTreeRevision = -1;
        private float editorSnapshotWidth = -1f;
        private bool editorSnapshotRulesRevealed;
        private bool editorSnapshotTuningExpanded;
        private string editorSnapshotFilter;

        internal string Filter { get; set; } = "";

        internal RoleEditorSnapshot Snapshot(RoleStore store, int roleId,
            Func<IReadOnlyList<Pawn>> listedPawns, int pawnRevision,
            float width, bool rulesRevealed, bool tuningExpanded,
            bool revealTreeSelection)
        {
            if (!revealTreeSelection && editorSnapshotStamp == UiVersion.Current
                && editorSnapshotRoleId == roleId
                && editorSnapshotLocationRevision == ColonyScope.LocationRevision
                && editorSnapshotPawnRevision == pawnRevision
                && editorSnapshotTreeRevision == treeRevision
                && editorSnapshotWidth == width
                && editorSnapshotRulesRevealed == rulesRevealed
                && editorSnapshotTuningExpanded == tuningExpanded
                && editorSnapshotFilter == Filter)
                return editorSnapshot;

            editorSnapshotStamp = UiVersion.Current;
            editorSnapshotRoleId = roleId;
            editorSnapshotLocationRevision = ColonyScope.LocationRevision;
            editorSnapshotPawnRevision = pawnRevision;
            editorSnapshotWidth = width;
            editorSnapshotRulesRevealed = rulesRevealed;
            editorSnapshotTuningExpanded = tuningExpanded;
            editorSnapshotFilter = Filter;

            Role role = store?.RoleById(roleId);
            if (role == null)
            {
                editorSnapshotTreeRevision = treeRevision;
                return editorSnapshot = null;
            }

            const float TopBoxPadding = 8f;
            const float PencilSize = 26f;
            const int SwatchCols = 19;
            string autoAssignLabel = "WR_AutoAssign".Translate().ToString();
            string blockerLabel = "WR_BlockerRole".Translate().ToString();
            string autoRoleLabel = "WR_AutoRole".Translate().ToString();
            Text.Font = GameFont.Small;
            float checksWidth = Mathf.Max(
                Mathf.Max(WrText.FitWidth(autoAssignLabel),
                    WrText.FitWidth(blockerLabel)),
                WrText.FitWidth(autoRoleLabel)) + 30f;
            float checksX = width / 2f - checksWidth;
            float titleMaxWidth = checksX - 8f - TopBoxPadding
                - PencilSize - 6f;
            Text.Font = GameFont.Medium;
            float roleLabelWidth = Mathf.Min(
                WrText.FitWidth(role.label), titleMaxWidth);
            string shownRoleLabel = role.label.Truncate(roleLabelWidth);
            Text.Font = GameFont.Small;

            var customSwatches = new List<Color>(store.customSwatches.Count);
            for (int i = 0; i < store.customSwatches.Count; i++)
                customSwatches.Add(store.customSwatches[i]);
            bool firstCustomRowFull = customSwatches.Count >= SwatchCols;
            if (firstCustomRowFull)
                for (int i = 0; i < SwatchCols; i++)
                    if (customSwatches[i].a < 0.5f)
                    {
                        firstCustomRowFull = false;
                        break;
                    }
            bool secondCustomRowUsed = false;
            for (int i = SwatchCols; i < customSwatches.Count; i++)
                if (customSwatches[i].a >= 0.5f)
                {
                    secondCustomRowUsed = true;
                    break;
                }
            int customRows = firstCustomRowFull || secondCustomRowUsed ? 2 : 1;

            string assignedLabel = "WR_AssignedTo".Translate().ToString();
            float assignedLabelWidth = WrText.FitWidth(assignedLabel);
            IReadOnlyList<RoleHolderPresentation> holders = Holders(role, store,
                listedPawns?.Invoke() ?? Array.Empty<Pawn>(), pawnRevision);
            var holderOverflowLabels = new List<string>(holders.Count + 1)
            {
                ""
            };
            for (int i = 1; i <= holders.Count; i++)
                holderOverflowLabels.Add(
                    "WR_PlusOthers".Translate(i).ToString());

            bool overlay = role.HasRules;
            string defaultGroupLabel = "WR_GroupDefault".Translate().ToString();
            string currentGroup = overlay
                ? "WR_GroupAutoRules".Translate().ToString()
                : role.groupId == RoleGroup.DefaultId
                    ? defaultGroupLabel
                    : store.GroupById(role.groupId)?.label ?? defaultGroupLabel;
            string groupButtonFull = "WR_GroupButton".Translate(currentGroup);
            float groupButtonWidth = Mathf.Min(
                checksX - 8f - TopBoxPadding, 180f);
            string groupButtonShown = groupButtonFull.Truncate(
                groupButtonWidth - 16f);
            var groups = new List<RoleGroupOptionSnapshot>();
            for (int i = 0; i < store.groups.Count; i++)
            {
                RoleGroup group = store.groups[i];
                if (group.id != RoleGroup.DefaultId)
                    groups.Add(new RoleGroupOptionSnapshot(
                        group.label, group.label));
            }

            IReadOnlyList<RoleSkillPresentation> skills = SkillsUsed(role);
            bool tuningShown = TuningShown(role);
            float tuningWidth = width / 2f - TopBoxPadding;
            ScaleEditorSnapshot scale = tuningShown
                ? ScaleEditorUI.BuildSnapshot(store, role, tuningWidth)
                : null;
            float tuningHeight = !tuningShown ? 0f : 4f + 24f
                + (tuningExpanded ? 4f + (scale?.Height ?? 0f) : 0f);

            var header = new RoleEditorHeaderSnapshot(role.id, role.label,
                shownRoleLabel, roleLabelWidth, role.hasCustomColor, role.color,
                role.autoAssign, role.blocker, role.HasRules,
                role.HasRules || rulesRevealed, customSwatches, customRows,
                checksWidth, autoAssignLabel, blockerLabel, autoRoleLabel,
                BlockerTip.Activate(),
                "WR_ClearRulesConfirm".Translate(role.label).ToString(),
                assignedLabel, assignedLabelWidth,
                holders, holderOverflowLabels,
                "WR_Nobody".Translate().ToString(),
                groupButtonFull, groupButtonShown, groups, defaultGroupLabel,
                "WR_GroupNewOption".Translate().ToString(),
                "WR_NewGroupTitle".Translate().ToString(),
                skills, "WR_SkillsUsedLabel".Translate().ToString(),
                tuningShown, tuningExpanded, tuningHeight,
                "WR_TuningHeader".Translate().ToString(),
                tuningShown ? HolderSummary(store, role) : null, scale);

            var locations = BuildLocationOptions(role);
            var rules = new RoleRulesSnapshot(role.id, role.activeHours,
                "WR_HoursActive".Translate().ToString(),
                "WR_HoursInactive".Translate().ToString(),
                LocationSummary(role), locations);

            float halfWidth = (width - 6f) / 2f;
            float removeWidth = (24f + 4f) * 3f;
            float typeWidth = (halfWidth - 8f - removeWidth - 8f) * 0.45f;
            float jobWidth = (halfWidth - 8f - removeWidth - 8f) * 0.55f;
            IReadOnlyCollection<int> deadEntries = DeadEntryIndexes(role);
            var entryRows = new List<RoleEntryRowSnapshot>(role.entries.Count);
            for (int i = 0; i < role.entries.Count; i++)
            {
                JobEntry entry = role.entries[i];
                RoleEntryPresentation presentation = EntryPresentation(
                    entry, typeWidth - 4f, jobWidth - 4f);
                string skillTip = presentation.Missing ? null
                    : entry.Kind == JobEntryKind.WorkType
                        ? JobSkillProfiles.WorkTypeTip(entry.DefName)
                        : JobSkillProfiles.GiverTip(entry.DefName);
                entryRows.Add(new RoleEntryRowSnapshot(entry, presentation,
                    !presentation.Missing && deadEntries.Contains(i), skillTip));
            }
            var entries = new RoleEntriesSnapshot(role.id,
                "WR_SelectedJobs".Translate().ToString(),
                "WR_TypeColumn".Translate().ToString(),
                "WR_JobColumn".Translate().ToString(), entryRows);

            RoleCoveragePresentation coverage = Coverage(store);
            (string typeDefName, string giverDefName)? target = null;
            if (revealTreeSelection)
            {
                target = FirstEntryTreeTarget(role);
                if (target?.giverDefName != null)
                    EnsureWorkTypeExpanded(target.Value.typeDefName);
            }
            bool filtering = !Filter.NullOrEmpty();
            IReadOnlyList<RoleJobTreeNode> nodes = TreeNodes(
                filtering, role, coverage);
            int targetIndex = -1;
            if (target != null)
                for (int i = 0; i < nodes.Count; i++)
                    if (nodes[i].TypeDefName == target.Value.typeDefName
                        && nodes[i].GiverDefName == target.Value.giverDefName)
                    {
                        targetIndex = i;
                        break;
                    }
            const float WarningMargin = 8f;
            const float WarningPadding = 8f;
            float warningHeight = coverage.Warning == null ? 0f
                : CoverageWarningHeight(coverage,
                    halfWidth - WarningMargin - WarningPadding * 2f)
                    + WarningPadding * 2f;
            var tree = new RoleJobTreeSnapshot(
                "WR_AvailableJobs".Translate().ToString(),
                "WR_Search".Translate().ToString(),
                "WR_AddAllJobs".Translate().ToString(), coverage.Warning,
                warningHeight, nodes, targetIndex);

            editorSnapshotTreeRevision = treeRevision;
            return editorSnapshot = new RoleEditorSnapshot(
                header, rules, entries, tree);
        }

        private static bool TuningShown(Role role) =>
            !role.autoAssign && !role.blocker && !role.HasRules
                && !RecsAdapter.ProvidesHunting(role);

        private static string HolderSummary(RoleStore store, Role role)
        {
            HolderScale scale = store?.ScaleFor(role) ?? store?.ScaleByName("Never");
            if (scale == null) return "";
            int lo = int.MaxValue;
            int hi = int.MinValue;
            for (int i = 0; i < scale.Min.Length; i++)
            {
                lo = Mathf.Min(lo, scale.Min[i]);
                hi = Mathf.Max(hi, scale.Min[i]);
            }
            string range = " (" + lo + "-" + hi + ")";
            Role target = ScaleEditorUI.ControllingTarget(store, role.id);
            if (target != null)
            {
                string label = "WR_ScaleControlledBy".Translate(
                    target.label).ToString();
                return scale.IsNever ? label : label + range;
            }
            Role autoParent = store.roles.FirstOrDefault(candidate =>
                candidate.autoAssign && candidate.enabled
                && candidate.CoversOrMatches(role));
            if (autoParent != null)
            {
                string label = "WR_ScaleInAutoRole".Translate(
                    autoParent.label).ToString();
                return scale.IsNever ? label : label + range;
            }
            return scale.Name + range;
        }

        private static List<RoleLocationOptionSnapshot> BuildLocationOptions(
            Role role)
        {
            string Check(bool on, string label) => (on ? "✓ " : "") + label;
            var result = new List<RoleLocationOptionSnapshot>
            {
                new RoleLocationOptionSnapshot(
                    Check(role.locationTokens.Count == 0,
                        "WR_LocationAny".Translate()), null, null),
                new RoleLocationOptionSnapshot(
                    Check(role.locationTokens.Contains(LocationRules.Settlements),
                        "WR_LocationSettlements".Translate()),
                    LocationRules.Settlements, null),
            };
            foreach (WorkRoles.Core.LocationInfo location in ColonyScope.Locations()
                .OrderBy(item => item.IsShip)
                .ThenBy(item => item.Label,
                    StringComparer.OrdinalIgnoreCase))
            {
                string token = (location.IsShip
                    ? LocationRules.ShipPrefix
                    : LocationRules.SettlementPrefix) + location.Id;
                result.Add(new RoleLocationOptionSnapshot(
                    Check(role.locationTokens.Contains(token),
                        LocationItemLabel(location)), token,
                    location.IsShip
                        ? "WR_ShipTip".Translate().ToString() : null));
            }
            result.Add(new RoleLocationOptionSnapshot(
                Check(role.locationTokens.Contains(LocationRules.Caravans),
                    "WR_LocationCaravans".Translate()),
                LocationRules.Caravans, null));
            return result;
        }

        private static string LocationSummary(Role role)
        {
            if (role.locationTokens.Count == 0)
                return "WR_LocationAny".Translate();
            if (role.locationTokens.Count > 1)
                return "WR_LocationCount".Translate(role.locationTokens.Count);
            string token = role.locationTokens[0];
            if (token == LocationRules.Settlements)
                return "WR_LocationSettlements".Translate();
            if (token == LocationRules.Caravans)
                return "WR_LocationCaravans".Translate();
            string id = token.Substring(token.IndexOf(':') + 1);
            WorkRoles.Core.LocationInfo location = ColonyScope.Locations()
                .FirstOrDefault(item => item.Id == id);
            return location != null ? LocationItemLabel(location)
                : "WR_LocationGone".Translate().ToString();
        }

        private static string LocationItemLabel(
            WorkRoles.Core.LocationInfo location) =>
            (location.IsShip ? "WR_LocationShipItem" : "WR_LocationSettlementItem")
                .Translate(location.Label).ToString();

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
            treeNodesFilter = null;
            treeNodesRevision = -1;
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
            editorSnapshot = null;
            editorSnapshotStamp = -1;
            editorSnapshotRoleId = -1;
            editorSnapshotLocationRevision = -1;
            editorSnapshotPawnRevision = -1;
            editorSnapshotTreeRevision = -1;
            editorSnapshotWidth = -1f;
            editorSnapshotFilter = null;
            tipsStamp = -1;
            blockerTip = null;
            holdersTip = null;
            tuningLabelWidth = tuningButtonWidth = -1f;
            tuningLayoutWidth = tuningLayoutRowHeight = -1f;
            tuningLayout = null;
            skillsUsed = null;
            skillsStamp = -1;
            skillsRoleId = -1;
            ClearEntryLabels();
            uncoveredGivers = null;
            uncoveredTypes = null;
            uncoveredWarning = null;
            coverage = null;
            uncoveredStamp = -1;
            measuredCoverageWarning = null;
            measuredCoverageWarningWidth = -1f;
            measuredCoverageWarningHeight = 0f;
            treeNodes = null;
            treeNodesStamp = -1;
            treeNodesRoleId = -1;
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

        internal RoleTuningLayout TuningLayout(
            float width,
            RoleHolderMode mode,
            float rowHeight)
        {
            EnsureTuningMetrics();
            if (tuningLayout != null
                && tuningLayoutWidth == width
                && tuningLayoutRowHeight == rowHeight
                && tuningLayoutMode == mode)
                return tuningLayout;

            tuningLayoutWidth = width;
            tuningLayoutRowHeight = rowHeight;
            tuningLayoutMode = mode;

            Text.Font = GameFont.Small;
            float descriptionWidth = width
                - (tuningLabelWidth + tuningButtonWidth + 8f);
            string intro = "WR_TuningHelp".Translate();
            string modeHelp = TuningModeHelpKey(mode).Translate();
            float introHeight = Text.CalcHeight(intro, width);
            float modeHeight = Mathf.Max(rowHeight,
                Text.CalcHeight(modeHelp, descriptionWidth));

            string minHelp = null;
            string maxHelp = null;
            string waiversHelp = null;
            float minHeight = 0f;
            float maxHeight = 0f;
            float waiversHeight = 0f;
            if (mode == RoleHolderMode.Custom)
            {
                minHelp = "WR_TuningMinHelp".Translate();
                maxHelp = "WR_TuningMaxHelp".Translate();
                waiversHelp = "WR_TuningWaiversHelp".Translate();
                minHeight = Mathf.Max(rowHeight,
                    Text.CalcHeight(minHelp, descriptionWidth));
                maxHeight = Mathf.Max(rowHeight,
                    Text.CalcHeight(maxHelp, descriptionWidth));
                waiversHeight = Mathf.Max(rowHeight,
                    Text.CalcHeight(waiversHelp, descriptionWidth));
            }

            tuningLayout = new RoleTuningLayout(
                intro, introHeight,
                modeHelp, modeHeight,
                minHelp, minHeight,
                maxHelp, maxHeight,
                waiversHelp, waiversHeight,
                mode == RoleHolderMode.Custom);
            return tuningLayout;
        }

        private static string TuningModeHelpKey(RoleHolderMode mode)
            => mode == RoleHolderMode.Auto ? "WR_TuningAutoHelp"
                : mode == RoleHolderMode.Never ? "WR_TuningNeverHelp"
                : "WR_TuningCustomHelp";

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
                        holders.Add(new RoleHolderPresentation(
                            pawn.LabelShortCap, position + 1));
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

        internal float CoverageWarningHeight(
            RoleCoveragePresentation presentation, float width)
        {
            string warning = presentation?.Warning;
            if (ReferenceEquals(measuredCoverageWarning, warning)
                && measuredCoverageWarningWidth == width)
                return measuredCoverageWarningHeight;

            measuredCoverageWarning = warning;
            measuredCoverageWarningWidth = width;
            if (warning == null)
                return measuredCoverageWarningHeight = 0f;

            GameFont previousFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                measuredCoverageWarningHeight = Text.CalcHeight(warning, width);
            }
            finally
            {
                Text.Font = previousFont;
            }
            return measuredCoverageWarningHeight;
        }

        internal IReadOnlyList<RoleJobTreeNode> TreeNodes(bool filtering,
            Role role, RoleCoveragePresentation coverage)
        {
            if (treeNodes != null && treeNodesStamp == UiVersion.Current
                && treeNodesRoleId == role.id
                && treeNodesRevision == treeRevision && treeNodesFilter == Filter)
                return treeNodes;

            treeNodesStamp = UiVersion.Current;
            treeNodesRoleId = role.id;
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

                int typeEntryIndex = role.entries.FindIndex(entry =>
                    entry.Kind == JobEntryKind.WorkType
                    && entry.DefName == type.defName);
                var missingGivers = new List<string>();
                for (int giverIndex = 0; giverIndex < givers.Count; giverIndex++)
                    if (!role.entries.Any(entry =>
                        entry.Kind == JobEntryKind.WorkGiver
                        && entry.DefName == givers[giverIndex].defName))
                        missingGivers.Add(givers[giverIndex].defName);
                bool expanded = filtering || expandedWorkTypes.Contains(type.defName);
                treeNodes.Add(new RoleJobTreeNode(type.defName, null,
                    typeName + " (" + givers.Count + ")",
                    WorkTypeState(role, type), expanded,
                    coverage.WorkTypes.Contains(type.defName),
                    JobSkillProfiles.WorkTypeTip(type.defName),
                    typeEntryIndex, typeEntryIndex, role.entries.Count,
                    missingGivers));
                if (!expanded) continue;

                IReadOnlyList<WorkGiverDef> visible = matching ?? givers;
                for (int i = 0; i < visible.Count; i++)
                {
                    WorkGiverDef giver = visible[i];
                    int giverEntryIndex = role.entries.FindIndex(entry =>
                        entry.Kind == JobEntryKind.WorkGiver
                        && entry.DefName == giver.defName);
                    treeNodes.Add(new RoleJobTreeNode(
                        type.defName, giver.defName,
                        WorkJobLabels.GiverDisplayName(giver),
                        GiverState(role, type, giver), expanded: true,
                        coverage.Givers.Contains(giver.defName),
                        JobSkillProfiles.GiverTip(giver.defName),
                        giverEntryIndex, typeEntryIndex, role.entries.Count,
                        missingGivers: null));
                }
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

        internal static (string typeDefName, string giverDefName)?
            FirstEntryTreeTarget(Role role)
        {
            foreach (JobEntry entry in role.entries)
            {
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    WorkTypeDef type = DefDatabase<WorkTypeDef>
                        .GetNamedSilentFail(entry.DefName);
                    if (type != null) return (type.defName, null);
                }
                else
                {
                    WorkGiverDef giver = DefDatabase<WorkGiverDef>
                        .GetNamedSilentFail(entry.DefName);
                    if (giver?.workType != null)
                        return (giver.workType.defName, giver.defName);
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

    internal sealed class RoleEditorSnapshot
    {
        internal RoleEditorSnapshot(RoleEditorHeaderSnapshot header,
            RoleRulesSnapshot rules, RoleEntriesSnapshot entries,
            RoleJobTreeSnapshot jobTree)
        {
            Header = header;
            Rules = rules;
            Entries = entries;
            JobTree = jobTree;
        }

        internal RoleEditorHeaderSnapshot Header { get; }
        internal RoleRulesSnapshot Rules { get; }
        internal RoleEntriesSnapshot Entries { get; }
        internal RoleJobTreeSnapshot JobTree { get; }
        internal int RoleId => Header.RoleId;
        internal string RoleLabel => Header.RoleLabel;
    }

    internal sealed class RoleEditorHeaderSnapshot
    {
        private readonly List<Color> customSwatches;
        private readonly IReadOnlyList<RoleHolderPresentation> holders;
        private readonly List<string> holderOverflowLabels;
        private readonly IReadOnlyList<RoleSkillPresentation> skills;
        private readonly List<RoleGroupOptionSnapshot> groups;

        internal RoleEditorHeaderSnapshot(int roleId, string roleLabel,
            string shownRoleLabel, float roleLabelWidth,
            bool hasCustomColor, Color roleColor, bool autoAssign,
            bool blocker, bool hasRules, bool rulesShown,
            List<Color> customSwatches, int customRows, float checksWidth,
            string autoAssignLabel, string blockerLabel, string autoRoleLabel,
            string blockerTip, string clearRulesConfirmation,
            string assignedLabel, float assignedLabelWidth,
            IReadOnlyList<RoleHolderPresentation> holders,
            List<string> holderOverflowLabels, string nobodyLabel,
            string groupButtonFull, string groupButtonShown,
            List<RoleGroupOptionSnapshot> groups,
            string defaultGroupLabel, string newGroupLabel,
            string newGroupTitle, IReadOnlyList<RoleSkillPresentation> skills,
            string skillsCaption, bool tuningShown, bool tuningExpanded,
            float tuningHeight, string tuningHeader, string holderSummary,
            ScaleEditorSnapshot scale)
        {
            RoleId = roleId;
            RoleLabel = roleLabel;
            ShownRoleLabel = shownRoleLabel;
            RoleLabelWidth = roleLabelWidth;
            HasCustomColor = hasCustomColor;
            RoleColor = roleColor;
            AutoAssign = autoAssign;
            Blocker = blocker;
            HasRules = hasRules;
            RulesShown = rulesShown;
            this.customSwatches = customSwatches;
            CustomRows = customRows;
            ChecksWidth = checksWidth;
            AutoAssignLabel = autoAssignLabel;
            BlockerLabel = blockerLabel;
            AutoRoleLabel = autoRoleLabel;
            BlockerTip = blockerTip;
            ClearRulesConfirmation = clearRulesConfirmation;
            AssignedLabel = assignedLabel;
            AssignedLabelWidth = assignedLabelWidth;
            this.holders = holders;
            this.holderOverflowLabels = holderOverflowLabels;
            NobodyLabel = nobodyLabel;
            GroupButtonFull = groupButtonFull;
            GroupButtonShown = groupButtonShown;
            this.groups = groups;
            DefaultGroupLabel = defaultGroupLabel;
            NewGroupLabel = newGroupLabel;
            NewGroupTitle = newGroupTitle;
            this.skills = skills;
            SkillsCaption = skillsCaption;
            TuningShown = tuningShown;
            TuningExpanded = tuningExpanded;
            TuningHeight = tuningHeight;
            TuningHeader = tuningHeader;
            HolderSummary = holderSummary;
            Scale = scale;
        }

        internal int RoleId { get; }
        internal string RoleLabel { get; }
        internal string ShownRoleLabel { get; }
        internal float RoleLabelWidth { get; }
        internal bool HasCustomColor { get; }
        internal Color RoleColor { get; }
        internal bool AutoAssign { get; }
        internal bool Blocker { get; }
        internal bool HasRules { get; }
        internal bool RulesShown { get; }
        internal int CustomSwatchCount => customSwatches.Count;
        internal Color CustomSwatchAt(int index) => customSwatches[index];
        internal int CustomRows { get; }
        internal float ChecksWidth { get; }
        internal string AutoAssignLabel { get; }
        internal string BlockerLabel { get; }
        internal string AutoRoleLabel { get; }
        internal string BlockerTip { get; }
        internal string ClearRulesConfirmation { get; }
        internal string AssignedLabel { get; }
        internal float AssignedLabelWidth { get; }
        internal int HolderCount => holders.Count;
        internal RoleHolderPresentation HolderAt(int index) => holders[index];
        internal string HolderOverflowLabel(int remaining) =>
            holderOverflowLabels[remaining];
        internal string NobodyLabel { get; }
        internal string GroupButtonFull { get; }
        internal string GroupButtonShown { get; }
        internal int GroupCount => groups.Count;
        internal RoleGroupOptionSnapshot GroupAt(int index) => groups[index];
        internal string DefaultGroupLabel { get; }
        internal string NewGroupLabel { get; }
        internal string NewGroupTitle { get; }
        internal int SkillCount => skills.Count;
        internal RoleSkillPresentation SkillAt(int index) => skills[index];
        internal string SkillsCaption { get; }
        internal bool TuningShown { get; }
        internal bool TuningExpanded { get; }
        internal float TuningHeight { get; }
        internal string TuningHeader { get; }
        internal string HolderSummary { get; }
        internal ScaleEditorSnapshot Scale { get; }
    }

    internal readonly struct RoleGroupOptionSnapshot
    {
        internal RoleGroupOptionSnapshot(string label, string commandName)
        {
            Label = label;
            CommandName = commandName;
        }

        internal string Label { get; }
        internal string CommandName { get; }
    }

    internal sealed class RoleRulesSnapshot
    {
        private readonly List<RoleLocationOptionSnapshot> locations;

        internal RoleRulesSnapshot(int roleId, int activeHours,
            string activeLabel, string inactiveLabel, string locationSummary,
            List<RoleLocationOptionSnapshot> locations)
        {
            RoleId = roleId;
            ActiveHours = activeHours;
            ActiveLabel = activeLabel;
            InactiveLabel = inactiveLabel;
            LocationSummary = locationSummary;
            this.locations = locations;
        }

        internal int RoleId { get; }
        internal int ActiveHours { get; }
        internal string ActiveLabel { get; }
        internal string InactiveLabel { get; }
        internal string LocationSummary { get; }
        internal int LocationCount => locations.Count;
        internal RoleLocationOptionSnapshot LocationAt(int index) =>
            locations[index];
    }

    internal readonly struct RoleLocationOptionSnapshot
    {
        internal RoleLocationOptionSnapshot(string label, string token,
            string tooltip)
        {
            Label = label;
            Token = token;
            Tooltip = tooltip;
        }

        internal string Label { get; }
        internal string Token { get; }
        internal string Tooltip { get; }
    }

    internal sealed class RoleEntriesSnapshot
    {
        private readonly List<RoleEntryRowSnapshot> rows;

        internal RoleEntriesSnapshot(int roleId, string title,
            string typeColumn, string jobColumn,
            List<RoleEntryRowSnapshot> rows)
        {
            RoleId = roleId;
            Title = title;
            TypeColumn = typeColumn;
            JobColumn = jobColumn;
            this.rows = rows;
        }

        internal int RoleId { get; }
        internal string Title { get; }
        internal string TypeColumn { get; }
        internal string JobColumn { get; }
        internal int Count => rows.Count;
        internal RoleEntryRowSnapshot RowAt(int index) => rows[index];
    }

    internal readonly struct RoleEntryRowSnapshot
    {
        internal RoleEntryRowSnapshot(JobEntry entry,
            RoleEntryPresentation presentation, bool dead, string skillTip)
        {
            Entry = entry;
            Presentation = presentation;
            Dead = dead;
            SkillTip = skillTip;
        }

        internal JobEntry Entry { get; }
        internal RoleEntryPresentation Presentation { get; }
        internal bool Dead { get; }
        internal string SkillTip { get; }
    }

    internal sealed class RoleJobTreeSnapshot
    {
        private readonly IReadOnlyList<RoleJobTreeNode> nodes;

        internal RoleJobTreeSnapshot(string title, string searchLabel,
            string addAllJobsLabel, string warning, float warningHeight,
            IReadOnlyList<RoleJobTreeNode> nodes, int targetIndex)
        {
            Title = title;
            SearchLabel = searchLabel;
            AddAllJobsLabel = addAllJobsLabel;
            Warning = warning;
            WarningHeight = warningHeight;
            this.nodes = nodes;
            TargetIndex = targetIndex;
        }

        internal string Title { get; }
        internal string SearchLabel { get; }
        internal string AddAllJobsLabel { get; }
        internal string Warning { get; }
        internal float WarningHeight { get; }
        internal int Count => nodes.Count;
        internal RoleJobTreeNode NodeAt(int index) => nodes[index];
        internal int TargetIndex { get; }
    }

    internal sealed class RoleTuningLayout
    {
        internal RoleTuningLayout(
            string intro,
            float introHeight,
            string modeHelp,
            float modeHeight,
            string minHelp,
            float minHeight,
            string maxHelp,
            float maxHeight,
            string waiversHelp,
            float waiversHeight,
            bool custom)
        {
            Intro = intro;
            IntroHeight = introHeight;
            ModeHelp = modeHelp;
            ModeHeight = modeHeight;
            MinHelp = minHelp;
            MinHeight = minHeight;
            MaxHelp = maxHelp;
            MaxHeight = maxHeight;
            WaiversHelp = waiversHelp;
            WaiversHeight = waiversHeight;
            ExpandedHeight = 4f + introHeight + 2f + modeHeight
                + (custom ? 4f + minHeight + maxHeight + waiversHeight : 0f);
        }

        internal string Intro { get; }
        internal float IntroHeight { get; }
        internal string ModeHelp { get; }
        internal float ModeHeight { get; }
        internal string MinHelp { get; }
        internal float MinHeight { get; }
        internal string MaxHelp { get; }
        internal float MaxHeight { get; }
        internal string WaiversHelp { get; }
        internal float WaiversHeight { get; }
        internal float ExpandedHeight { get; }
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
        internal RoleHolderPresentation(string label, int position)
        {
            Label = label;
            Position = position;
        }

        internal string Label { get; }
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
        private readonly List<string> missingGivers;

        internal RoleJobTreeNode(string typeDefName, string giverDefName,
            string label, MultiCheckboxState state, bool expanded,
            bool warning, string skillTip, int ownEntryIndex,
            int typeEntryIndex, int entryCount, List<string> missingGivers)
        {
            TypeDefName = typeDefName;
            GiverDefName = giverDefName;
            Label = label;
            State = state;
            Expanded = expanded;
            Warning = warning;
            SkillTip = skillTip;
            OwnEntryIndex = ownEntryIndex;
            TypeEntryIndex = typeEntryIndex;
            EntryCount = entryCount;
            this.missingGivers = missingGivers;
        }

        internal string TypeDefName { get; }
        internal string GiverDefName { get; }
        internal string Label { get; }
        internal MultiCheckboxState State { get; }
        internal bool Expanded { get; }
        internal bool Warning { get; }
        internal string SkillTip { get; }
        internal int OwnEntryIndex { get; }
        internal int TypeEntryIndex { get; }
        internal int EntryCount { get; }
        internal int MissingGiverCount => missingGivers?.Count ?? 0;
        internal string MissingGiverAt(int index) => missingGivers[index];
    }
}
