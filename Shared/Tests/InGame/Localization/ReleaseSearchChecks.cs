using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Verse;

namespace LocalizationProbe;

// Opt-in integration assertions. Uses real consumers with detached fixtures;
// nothing is added to the loaded world's authoritative stores.
internal static class ReleaseSearchChecks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.Static | BindingFlags.Instance;

    internal static object Check()
    {
        var checks = new List<Dictionary<string, object>>();
        foreach (string assembly in new[] { "WorkRoles", "EPrimeReadouts", "QualityJobs", "Implanner" })
        {
            string mod = assembly;
            Run(checks, mod + "-selected-turkish-search", () =>
            {
                CultureInfo culture = Culture(mod);
                Require(culture.Name == "tr-TR", "Selected display culture was " + culture.Name);
                Type matcher = TypeOf(mod + ".Core", "RimShared.Common.SearchMatcher");
                Require(Match(matcher, "İnşaatçı", "inşaatçı", culture), "Dotted-I construction search failed");
                Require(Match(matcher, "İtfaiyeci", "itfaiye", culture), "Dotted-I firefighter search failed");
                Require(Match(matcher, "Işık", "ışık", culture), "Dotless-I light search failed");
                Require(!Match(matcher, "Işık", "işik", culture), "Distinct Turkish letters incorrectly matched");
                Require(Match(matcher, "IRON", "iron", null), "Default identifier comparison changed");
                return "tr-TR; dotted/dotless positive cases and distinct-letter negative passed";
            });
        }
        Run(checks, "workroles-actual-multi-term-role-memo", CheckRoleMemo);
        Run(checks, "workroles-actual-multi-term-job-memo", CheckJobMemo);
        Run(checks, "workroles-turkish-and-grapheme-abbreviations", () =>
        {
            Type type = TypeOf("WorkRoles.Core", "RimShared.Common.RoleAbbreviations");
            var labels = new List<(int, string)> { (0, "inşaat işçisi"), (1, "ışık ustası"),
                (2, "e\u0301béniste"), (3, "\ud840\udc00工") };
            var result = (IDictionary)type.GetMethod("Build", Flags)!.Invoke(null,
                new object[] { labels, Culture("WorkRoles") })!;
            Require((string)result[0]! == "İİ" && (string)result[1]! == "IU",
                "Turkish uppercase initials failed");
            Require((string)result[2]! == "E\u0301b" && (string)result[3]! == "\ud840\udc00工",
                "Abbreviations split complete letters");
            return "İİ / IU / Éb / 𠀀工";
        });
        Run(checks, "workroles-turkish-abbreviation-uniqueness", () =>
        {
            Type type = TypeOf("WorkRoles.Core", "RimShared.Common.RoleAbbreviations");
            var labels = new List<(int, string)> { (0, "Ib"), (1, "ıb"), (2, "ib"), (3, "İb") };
            var result = (IDictionary)type.GetMethod("Build", Flags)!.Invoke(null,
                new object[] { labels, Culture("WorkRoles") })!;
            string actual = string.Join(" / ", Enumerable.Range(0, 4).Select(i => (string)result[i]!));
            Require(actual == "Ib / I / İb / İ", "Turkish collision policy produced " + actual);
            return actual;
        });
        Run(checks, "implanner-native-implant-picker", CheckImplannerPicker);
        Run(checks, "readouts-native-resource-layout", CheckReadoutLayout);
        return new Dictionary<string, object>
        {
            ["language"] = LanguageDatabase.activeLanguage.LegacyFolderName,
            ["runtime"] = Environment.Version.ToString(),
            ["mono"] = Type.GetType("Mono.Runtime") != null,
            ["passed"] = checks.All(c => (bool)c["passed"]),
            ["checks"] = checks
        };
    }

    private static string CheckRoleMemo() => CheckMemo(false);
    private static string CheckJobMemo() => CheckMemo(true);

    private static string CheckMemo(bool jobs)
    {
        Type catalogType = TypeOf("WorkRoles", "WorkRoles.UI.ColonistsRosterCatalogSnapshot");
        Type optionType = TypeOf("WorkRoles", jobs
            ? "WorkRoles.UI.ColonistJobFilterOption" : "WorkRoles.UI.ColonistRoleFilterOption");
        IList options = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(optionType))!;
        options.Add(Create(optionType, jobs ? new object[] { "Light", "Işık", "" } : new object[] { 101, "Işık" }));
        options.Add(Create(optionType, jobs ? new object[] { "Builder", "İşçi", "" } : new object[] { 202, "İşçi" }));
#pragma warning disable SYSLIB0050 // Test fixture, never a production construction path.
        object catalog = FormatterServices.GetUninitializedObject(catalogType);
#pragma warning restore SYSLIB0050
        catalogType.GetField(jobs ? "jobOptions" : "roleOptions", Flags)!.SetValue(catalog, options);
        Type memoType = TypeOf("WorkRoles", "WorkRoles.UI.ColonistsRosterState+ColonistSearchMatches");
        object memo = Create(memoType, catalog);
        Type queryType = TypeOf("WorkRoles.Core", "WorkRoles.Core.ColonistSearchQuery");
        string query = jobs ? "j:I j:i" : "r:I r:i";
        object parsed = queryType.GetMethod("Parse", Flags)!.Invoke(null, new object[] { query })!;
        Array terms = (Array)queryType.GetField("terms", Flags)!.GetValue(parsed)!;
        Require(terms.Length == 2, "Query did not retain two terms");
        var outputs = new List<string>();
        foreach (object term in terms)
        {
            string value = (string)term.GetType().GetProperty("Value", Flags)!.GetValue(term)!;
            object? matched = memoType.GetMethod(jobs ? "Jobs" : "Roles", Flags)!.Invoke(memo, new object[] { value });
            outputs.Add(matched == null ? "<none>" : string.Join(",",
                ((IEnumerable)matched).Cast<object>().Select(x => x.ToString()).OrderBy(x => x)));
        }
        Require(outputs[0] == (jobs ? "Light" : "101"), "Dotless term got " + outputs[0]);
        Require(outputs[1] == (jobs ? "Builder" : "202"), "Dotted term reused wrong memo: " + outputs[1]);
        return query + " => " + string.Join(" / ", outputs);
    }

    private static string CheckImplannerPicker()
    {
        Type catalogs = TypeOf("Implanner", "Implanner.Catalogs");
        IEnumerable entries = (IEnumerable)catalogs.GetMethod("Implants", Flags)!.Invoke(null, null)!;
        object candidate = entries.Cast<object>().First(e => DistinguishesDottedI(
            (string)Read(e, "Label") + " " + (string)Read(e, "GroupLabel"))
            && ((IList)Read(e, "SlotLabels")).Count > 0);
        string defName = ((Def)Read(candidate, "Def")).defName;
        string label = (string)Read(candidate, "Label");
        object model = Create(TypeOf("Implanner.Core", "Implanner.Core.PlannerModel"));
        model.GetType().GetMethod("SetShowPurchaseOnly", Flags)!.Invoke(model, new object[] { true });
        object plan = Create(TypeOf("Implanner.Core", "Implanner.Core.Plan"), 1, "Detached search fixture");
        model.GetType().GetMethod("AddLoadedPlan", Flags)!.Invoke(model, new[] { plan });
        Type stateType = TypeOf("Implanner", "Implanner.UI.PlansState");
        object state = Create(stateType);
        Type snapshotType = TypeOf("Implanner", "Implanner.UI.PlansSnapshot");
        bool Has(string query)
        {
            object snapshot = Create(snapshotType);
            snapshotType.GetField("Query", Flags)!.SetValue(snapshot, query);
            stateType.GetMethod("BuildTree", Flags)!.Invoke(state, new[] { model, plan, snapshot });
            return ((IEnumerable)Read(snapshot, "Tree")).Cast<object>()
                .Any(r => (string)Read(r, "DefName") == defName);
        }
        Require(Has("İ"), "Native implant absent from dotted-I query: " + label);
        Require(!Has("I"), "Native implant incorrectly matched dotless-I query: " + label);
        return defName + " / " + label + ": İ includes; I excludes";
    }

    private static string CheckReadoutLayout()
    {
        Type catalogType = TypeOf("EPrimeReadouts", "EPrimeReadouts.GameResourceCatalog");
        object catalog = catalogType.GetField("Instance", Flags)!.GetValue(null)!;
        IEnumerable candidates = (IEnumerable)catalogType.GetProperty("SearchableDefNames", Flags)!.GetValue(null)!;
        MethodInfo labelOf = catalogType.GetMethod("LabelOf", Flags)!;
        string defName = candidates.Cast<string>().First(d => DistinguishesDottedI(
            (string)labelOf.Invoke(catalog, new object[] { d })!));
        string label = (string)labelOf.Invoke(catalog, new object[] { defName })!;
        Type inputType = TypeOf("EPrimeReadouts.Core", "EPrimeReadouts.Core.LayoutInput");
        Type engineType = TypeOf("EPrimeReadouts.Core", "EPrimeReadouts.Core.ReadoutLayoutEngine");
        object input = Create(inputType);
        inputType.GetField("Catalog", Flags)!.SetValue(input, catalog);
        inputType.GetField("Counts", Flags)!.SetValue(input, new Dictionary<string, int> { [defName] = 1 });
        FieldInfo thresholds = inputType.GetField("Thresholds", Flags)!;
        thresholds.SetValue(input, Activator.CreateInstance(thresholds.FieldType));
        inputType.GetField("SearchableDefNames", Flags)!.SetValue(input, new[] { defName });
        inputType.GetField("SearchCulture", Flags)!.SetValue(input, Culture("EPrimeReadouts"));
        bool Has(string query)
        {
            inputType.GetField("SearchText", Flags)!.SetValue(input, query);
            object output = engineType.GetMethod("Build", Flags)!.Invoke(null, new[] { input })!;
            return ((IEnumerable)Read(output, "Cells")).Cast<object>()
                .Any(c => (string?)Read(c, "DefName") == defName && Read(c, "Kind").ToString() == "Icon");
        }
        Require(Has("İ"), "Native resource absent from dotted-I query: " + label);
        Require(!Has("I"), "Native resource incorrectly matched dotless-I query: " + label);
        return defName + " / " + label + ": İ includes; I excludes";
    }

    private static bool DistinguishesDottedI(string text) => text.IndexOf('i') >= 0
        && text.IndexOf('İ') < 0 && text.IndexOf('ı') < 0 && text.IndexOf('I') < 0;

    private static CultureInfo Culture(string assembly) => (CultureInfo)TypeOf(assembly, "RimShared.UiLib.UiLanguage")
        .GetProperty("CaseCulture", Flags)!.GetValue(null)!;

    private static bool Match(Type matcher, string text, string query, CultureInfo? culture) =>
        (bool)matcher.GetMethod("Matches", Flags)!.Invoke(null, new object?[] { text, query, culture })!;

    private static object Read(object target, string member)
    {
        FieldInfo? field = target.GetType().GetField(member, Flags);
        return field != null ? field.GetValue(target)!
            : target.GetType().GetProperty(member, Flags)!.GetValue(target)!;
    }

    private static Type TypeOf(string assembly, string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == assembly).GetType(name, true)!;

    private static object Create(Type type, params object[] args) =>
        Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, args, CultureInfo.InvariantCulture)!;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Run(List<Dictionary<string, object>> checks, string name, Func<string> action)
    {
        bool passed = true;
        string detail;
        try { detail = action(); }
        catch (Exception e) { passed = false; detail = e.ToString(); }
        checks.Add(new Dictionary<string, object> { ["name"] = name, ["passed"] = passed, ["detail"] = detail });
    }
}
