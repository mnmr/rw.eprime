using System;
using System.Globalization;
using EPrimeReadouts.Core;
using EPrimeReadouts.UI;

namespace EPrimeReadouts
{
    internal static class PlayerSetupBridge
    {
        // Owner: process/local preferences. Key: explicit durable preference edits.
        // Value: scalar revision. Dependencies: supported options and enabled groups only.
        // Refresh: event-driven. Equality: no-op setters do not notify. Teardown: Reset.
        internal static int Revision { get; private set; }
        internal static void Changed() { Revision++; SetupDefaults.Changed(); }
        internal static void Reset() { Revision = 0; }

        internal static PlayerSetup Capture(ReadoutStore? store)
        {
            var s = EPrimeReadoutsMod.Settings;
            var result = new PlayerSetup { ProfileId = s.setupProfileId };
            result.Options["useVanillaReadout"] = s.useVanillaReadout ? "True" : "False";
            result.Options["showSearchFilter"] = s.showSearchFilter ? "True" : "False";
            result.Options["showModNameWhenNoSearch"] = s.showModNameWhenNoSearch ? "True" : "False";
            result.Options["searchHideZero"] = s.searchHideZero ? "True" : "False";
            result.Options["searchStorageOnly"] = s.searchStorageOnly ? "True" : "False";
            result.Options["searchHideForbidden"] = s.searchHideForbidden ? "True" : "False";
            result.Options["selectJumpCamera"] = s.selectJumpCamera ? "True" : "False";
            result.Options["expandOnHover"] = s.expandOnHover ? "True" : "False";
            result.Options["collapseWhenIdle"] = s.collapseWhenIdle ? "True" : "False";
            result.Options["keepReadoutToggle"] = s.keepReadoutToggle ? "True" : "False";
            result.Options["verticalTiers"] = s.verticalTiers ? "True" : "False";
            result.Options["renderCountersWithTinyFont"] = s.renderCountersWithTinyFont ? "True" : "False";
            result.Options["reserveForBills"] = s.reserveForBills ? "True" : "False";
            result.Options["reserveForBuildables"] = s.reserveForBuildables ? "True" : "False";
            result.Options["showNegativeCounts"] = s.showNegativeCounts ? "True" : "False";
            result.Options["qualityJobsRework"] = s.qualityJobsRework ? "True" : "False";
            result.Options["offsetX"] = s.offsetX.ToString("R", CultureInfo.InvariantCulture);
            result.Options["offsetY"] = s.offsetY.ToString("R", CultureInfo.InvariantCulture);
            result.Options["panelWidth"] = s.panelWidth.ToString("R", CultureInfo.InvariantCulture);
            result.Options["bottomMargin"] = s.bottomMargin.ToString("R", CultureInfo.InvariantCulture);
            if (store != null)
                foreach (var group in store.Model.Groups)
                    result.EnabledGroups[group.Name] = s.enabledGroups.TryGetValue(store.DepthKey(group.Id), out bool enabled)
                        ? enabled : group.DefaultEnabled;
            return result;
        }

        internal static void Apply(PlayerSetup? player, ReadoutStore store)
        {
            if (player == null) return;
            var s = EPrimeReadoutsMod.Settings;
            var before = Capture(store);
            bool optionsChanged = false, enabledChanged = false;
            foreach (var pair in player.Options)
            {
                if (!before.Options.TryGetValue(pair.Key, out string? oldValue) || oldValue != pair.Value) optionsChanged = true;
                switch (pair.Key)
                {
                    case "useVanillaReadout": s.useVanillaReadout = bool.Parse(pair.Value); break;
                    case "showSearchFilter": s.showSearchFilter = bool.Parse(pair.Value); break;
                    case "showModNameWhenNoSearch": s.showModNameWhenNoSearch = bool.Parse(pair.Value); break;
                    case "searchHideZero": s.searchHideZero = bool.Parse(pair.Value); break;
                    case "searchStorageOnly": s.searchStorageOnly = bool.Parse(pair.Value); break;
                    case "searchHideForbidden": s.searchHideForbidden = bool.Parse(pair.Value); break;
                    case "selectJumpCamera": s.selectJumpCamera = bool.Parse(pair.Value); break;
                    case "expandOnHover": s.expandOnHover = bool.Parse(pair.Value); break;
                    case "collapseWhenIdle": s.collapseWhenIdle = bool.Parse(pair.Value); break;
                    case "keepReadoutToggle": s.keepReadoutToggle = bool.Parse(pair.Value); break;
                    case "verticalTiers": s.verticalTiers = bool.Parse(pair.Value); break;
                    case "renderCountersWithTinyFont": s.renderCountersWithTinyFont = bool.Parse(pair.Value); break;
                    case "reserveForBills": s.reserveForBills = bool.Parse(pair.Value); break;
                    case "reserveForBuildables": s.reserveForBuildables = bool.Parse(pair.Value); break;
                    case "showNegativeCounts": s.showNegativeCounts = bool.Parse(pair.Value); break;
                    case "qualityJobsRework": s.qualityJobsRework = bool.Parse(pair.Value); break;
                    case "offsetX": s.offsetX = float.Parse(pair.Value, CultureInfo.InvariantCulture); break;
                    case "offsetY": s.offsetY = float.Parse(pair.Value, CultureInfo.InvariantCulture); break;
                    case "panelWidth": s.panelWidth = float.Parse(pair.Value, CultureInfo.InvariantCulture); break;
                    case "bottomMargin": s.bottomMargin = float.Parse(pair.Value, CultureInfo.InvariantCulture); break;
                }
            }
            foreach (var group in store.Model.Groups)
                if (player.EnabledGroups.TryGetValue(group.Name, out bool enabled)
                    && before.EnabledGroups[group.Name] != enabled)
                {
                    s.enabledGroups[store.DepthKey(group.Id)] = enabled;
                    enabledChanged = true;
                }
            if (!optionsChanged && !enabledChanged) return;
            if (enabledChanged) GroupListView.InvalidateEnabledGroups();
            if (!s.showSearchFilter) ReadoutPanel.SearchText = "";
            Changed();
            ReadoutPanel.BumpView();
            SetupDefaults.QueuePreferencesWrite();
        }
    }
}

