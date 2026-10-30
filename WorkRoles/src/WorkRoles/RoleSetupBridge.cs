using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    /// Explicit capture/import boundaries only. Player settings never enter the
    /// shared store; the issuing multiplayer client applies them locally.
    internal static class RoleSetupBridge
    {
        internal static SetupOptions CaptureGame(RoleStore store)
        {
            var result = new SetupOptions();
            Add(result, "useWorkPriorities", Current.Game?.playSettings?.useWorkPriorities ?? true);
            Add(result, "reportVanillaPriorities", store.reportVanillaPriorities);
            Add(result, "rolePriorityEmergencyRule", store.rolePriorityEmergencyRule);
            Add(result, "autoOptimize", store.autoOptimize);
            Add(result, "mechanicalWorkersEnabled", store.mechanicalWorkersEnabled);
            Add(result, "mechanicalRecommendationsEnabled", store.mechanicalRecommendationsEnabled);
            foreach (var descriptor in RecommendationsTuningOptions.Descriptors)
                result.values.Add("recommendation." + descriptor.StableKey,
                    store.recommendationTuning.Get(descriptor.Option).ToString(CultureInfo.InvariantCulture));
            return result;
        }

        internal static SetupOptions CapturePlayer() => CapturePlayer(WorkRolesMod.Settings ?? new WorkRolesSettings());

        private static SetupOptions CapturePlayer(WorkRolesSettings s)
        {
            var result = new SetupOptions { skillColumns = new List<string>(s.skillColumns) };
            Add(result, "chipGrid", s.chipGrid);
            Add(result, "nestedRoleTree", s.nestedRoleTree);
            Add(result, "verdictsOnColonistChips", s.verdictsOnColonistChips);
            Add(result, "verdictsInPalette", s.verdictsInPalette);
            Add(result, "verdictsOnRecommendationChips", s.verdictsOnRecommendationChips);
            Add(result, "colonistSkillCaptions", s.colonistSkillCaptions);
            result.values["chipDisplay"] = ((int)s.chipDisplay).ToString(CultureInfo.InvariantCulture);
            result.values["chipGridNames"] = ((int)s.chipGridNames).ToString(CultureInfo.InvariantCulture);
            result.values["colonistOrder"] = ((int)s.colonistOrder).ToString(CultureInfo.InvariantCulture);
            result.values["paletteMode"] = ((int)s.paletteMode).ToString(CultureInfo.InvariantCulture);
            result.values["groupBy"] = s.groupBy;
            result.values["sortColumn"] = s.sortColumn;
            result.values["windowWidth"] = s.windowWidth.ToString("R", CultureInfo.InvariantCulture);
            result.values["windowHeight"] = s.windowHeight.ToString("R", CultureInfo.InvariantCulture);
            return result;
        }

        private static void Add(SetupOptions options, string key, bool value) => options.values[key] = value ? "true" : "false";
        internal static bool Boolean(SetupOptions options, string key, bool fallback) =>
            options.values.TryGetValue(key, out string? value) && bool.TryParse(value, out bool parsed) ? parsed : fallback;

        internal static void ApplyPlayer(SetupOptions? imported)
        {
            WorkRolesSettings s = WorkRolesMod.Settings;
            if (imported == null || s == null) return;
            var defaults = CapturePlayer(new WorkRolesSettings());
            foreach (var pair in imported.values) defaults.values[pair.Key] = pair.Value;
            defaults.skillColumns = imported.skillColumns ?? new List<string>();
            if (CapturePlayer(s).ToXml("PlayerSettings").ToString() == defaults.ToXml("PlayerSettings").ToString()) return;
            s.chipGrid = Boolean(defaults, "chipGrid", true);
            s.nestedRoleTree = Boolean(defaults, "nestedRoleTree", true);
            s.verdictsOnColonistChips = Boolean(defaults, "verdictsOnColonistChips", true);
            s.verdictsInPalette = Boolean(defaults, "verdictsInPalette", true);
            s.verdictsOnRecommendationChips = Boolean(defaults, "verdictsOnRecommendationChips", true);
            s.colonistSkillCaptions = Boolean(defaults, "colonistSkillCaptions", true);
            s.chipDisplay = (ChipDisplay)int.Parse(defaults.values["chipDisplay"], CultureInfo.InvariantCulture);
            s.chipGridNames = (GridNamePreference)int.Parse(defaults.values["chipGridNames"], CultureInfo.InvariantCulture);
            s.colonistOrder = (ColonistOrder)int.Parse(defaults.values["colonistOrder"], CultureInfo.InvariantCulture);
            s.paletteMode = (PaletteMode)int.Parse(defaults.values["paletteMode"], CultureInfo.InvariantCulture);
            s.groupBy = defaults.values["groupBy"];
            s.sortColumn = defaults.values["sortColumn"];
            s.windowWidth = float.Parse(defaults.values["windowWidth"], CultureInfo.InvariantCulture);
            s.windowHeight = float.Parse(defaults.values["windowHeight"], CultureInfo.InvariantCulture);
            s.skillColumns = new List<string>(defaults.skillColumns);
            WorkRolesGameComponent.RequestSettingsWrite();
        }
    }

    public static partial class RoleCommands
    {
        internal static bool ApplyNewGameSetup(string xml)
        {
            if (Store == null) return false;
            var document = RoleFile.Parse(xml);
            var selection = new ImportSelection { xml = xml, roles = true, rolesOverwrite = true };
            if (!document.hasRoles || RoleIO.ValidateSelection(Store, document, selection) != null) return false;
            RoleIO.ResolveImportLocations(selection, document, ColonyScope.Locations());
            using var batch = Store.Revisions.Batch();
            ApplyImportToStore(Store, document,
                document.hasPalette, true, selection.paletteRows,
                true, true, selection.roleRows,
                document.trainingPaths.Count > 0, true, selection.pathRows, false,
                ImportLocationResolver.BuildMap(selection.locationFileTokens, selection.locationRuntimeTokens));
            ApplyGameSetup(document.gameSettings);
            return true;
        }

        // Called only inside the synced import or deterministic initial setup.
        private static bool ApplyGameSetup(SetupOptions? options)
        {
            if (options == null || Store == null) return false;
            string before = RoleSetupBridge.CaptureGame(Store).ToXml("GameSettings").ToString();
            SetUseWorkPriorities(RoleSetupBridge.Boolean(options, "useWorkPriorities", true));
            SetReportVanillaPriorities(RoleSetupBridge.Boolean(options, "reportVanillaPriorities", true));
            SetRolePriorityEmergencyRule(RoleSetupBridge.Boolean(options, "rolePriorityEmergencyRule", true));
            SetMechanicalWorkersEnabled(RoleSetupBridge.Boolean(options, "mechanicalWorkersEnabled", false));
            SetMechanicalRecommendationsEnabled(RoleSetupBridge.Boolean(options, "mechanicalRecommendationsEnabled", false));
            SetAutoOptimize(RoleSetupBridge.Boolean(options, "autoOptimize", false));
            var values = new Dictionary<RecommendationTuningOption, int>();
            foreach (var descriptor in RecommendationsTuningOptions.Descriptors)
                if (options.values.TryGetValue("recommendation." + descriptor.StableKey, out string? value))
                    values.Add(descriptor.Option, int.Parse(value, CultureInfo.InvariantCulture));
            var tuning = RecommendationsTuningOptions.FromValues(values);
            bool tuningChanged = RecommendationsTuningOptions.Descriptors.Any(d =>
                Store.recommendationTuning.Get(d.Option) != tuning.Get(d.Option));
            if (tuningChanged) { Store.recommendationTuning = tuning; Store.RecommendationTuningRevision++; }
            return before != RoleSetupBridge.CaptureGame(Store).ToXml("GameSettings").ToString();
        }
    }
}
