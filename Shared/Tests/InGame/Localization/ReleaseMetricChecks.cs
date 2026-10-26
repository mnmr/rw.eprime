using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace LocalizationProbe;

// Targeted cache-invalidation checks. The measurement hook counts real builder
// calls, because a metric change can rebuild equal geometry and retain identity.
// The label hook models a translated caption change with identical core cells.
internal static class ReleaseMetricChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Static | BindingFlags.Instance;
    private static int fitted;
    private static bool replacement;
    internal static object Check()
    {
        var results = new List<object>();
        Run(results, "workroles-toolbar-metric-invalidation", CheckToolbar);
        Run(results, "readouts-equal-layout-label-invalidation", CheckReadouts);
        return results;
    }

    private static object CheckToolbar()
    {
        Type profileType = TypeOf("WorkRoles", "WorkRoles.UI.ColonistsViewProfile");
        object profile = profileType.GetMethod("Colonists", Flags)!.Invoke(null, null)!;
        Type viewType = TypeOf("WorkRoles", "WorkRoles.UI.ColonistsTabView");
        object view = Activator.CreateInstance(viewType, profile)!;
        object store = TypeOf("WorkRoles", "WorkRoles.RoleStore").GetProperty("Current", Flags)!.GetValue(null)!;
        MethodInfo chrome = viewType.GetMethod("ChromeSnapshot", Flags)!;
        MethodInfo truncate = TypeOf("WorkRoles", "RimShared.UiLib.WrText").GetMethod("Truncate", Flags)!;
        var harmony = new Harmony("release.metric.toolbar");
        try
        {
            harmony.Patch(truncate, prefix: new HarmonyMethod(typeof(ReleaseMetricChecks), nameof(CountFit)));
            object first = chrome.Invoke(view, new[] { store })!;
            int firstCount = fitted;
            object same = chrome.Invoke(view, new[] { store })!;
            bool stable = ReferenceEquals(first, same) && fitted == firstCount;
            AdvanceMetrics("WorkRoles");
            chrome.Invoke(view, new[] { store });
            bool refreshed = fitted > firstCount;
            int after = fitted;
            chrome.Invoke(view, new[] { store });
            return new { passed = stable && refreshed && after == fitted, stable, refreshed, repeatedStable = after == fitted };
        }
        finally
        {
            harmony.Unpatch(truncate, HarmonyPatchType.All, harmony.Id);
            Observe("WorkRoles");
            viewType.GetMethod("ReleaseSnapshots", Flags)!.Invoke(view, null);
        }
    }

    private static object CheckReadouts()
    {
        const string assembly = "EPrimeReadouts";
        Type panel = TypeOf(assembly, "EPrimeReadouts.UI.ReadoutPanel");
        object store = TypeOf(assembly, "EPrimeReadouts.ReadoutStore").GetProperty("Current", Flags)!.GetValue(null)!;
        object data = TypeOf(assembly, "EPrimeReadouts.GameRenderData").GetMethod("Get", Flags)!.Invoke(null, new object[] { Find.CurrentMap, store })!;
        MethodInfo rebuild = panel.GetMethod("Rebuild", Flags)!;
        FieldInfo search = panel.GetField("SearchText", Flags)!;
        FieldInfo drawField = panel.GetField("draw", Flags)!;
        object? previousSearch = search.GetValue(null);
        var harmony = new Harmony("release.metric.readouts");
        MethodInfo getText = TypeOf(assembly, "EPrimeReadouts.UI.UiText").GetMethod("Get", Flags)!;
        try
        {
            search.SetValue(null, "___release_no_match___");
            rebuild.Invoke(null, new[] { store, Find.CurrentMap, (object)140f, data });
            object before = drawField.GetValue(null)!;
            object model = before.GetType().GetField("Model", Flags)!.GetValue(before)!;
            harmony.Patch(getText, postfix: new HarmonyMethod(typeof(ReleaseMetricChecks), nameof(ReplaceLabel)));
            replacement = true;
            AdvanceMetrics(assembly);
            rebuild.Invoke(null, new[] { store, Find.CurrentMap, (object)140f, data });
            object after = drawField.GetValue(null)!;
            object afterModel = after.GetType().GetField("Model", Flags)!.GetValue(after)!;
            string?[] labels = (string?[])after.GetType().GetField("Labels", Flags)!.GetValue(after)!;
            bool refreshed = labels.Contains("Metric caption");
            bool sameModel = ReferenceEquals(model, afterModel);
            rebuild.Invoke(null, new[] { store, Find.CurrentMap, (object)140f, data });
            bool stable = ReferenceEquals(after, drawField.GetValue(null));
            return new { passed = refreshed && sameModel && stable, refreshed, sameModel, stable };
        }
        finally
        {
            replacement = false;
            harmony.Unpatch(getText, HarmonyPatchType.All, harmony.Id);
            search.SetValue(null, previousSearch);
            Observe(assembly);
            rebuild.Invoke(null, new[] { store, Find.CurrentMap, (object)140f, data });
        }
    }

    private static void CountFit() => fitted++;
    private static void ReplaceLabel(string __0, ref string __result)
    { if (replacement && __0 == "EPR.NoMatches") __result = "Metric caption"; }

    private static void AdvanceMetrics(string assembly)
    {
        Type type = TypeOf(assembly, "RimShared.UiLib.UiRevision");
        object revision = type.GetField("revision", Flags)!.GetValue(null)!;
        revision.GetType().GetMethod("Bump", Flags)!.Invoke(revision, null);
    }
    private static void Observe(string assembly) =>
        TypeOf(assembly, "RimShared.UiLib.UiRevision").GetMethod("ObserveCurrentMetrics", Flags)!.Invoke(null, null);
    private static Type TypeOf(string assembly, string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == assembly).GetType(name, true)!;
    private static void Run(List<object> results, string name, Func<object> check)
    {
        try { results.Add(new { check = name, detail = check() }); }
        catch (Exception ex) { results.Add(new { check = name, detail = new { passed = false, error = ex.ToString() } }); }
    }
}
