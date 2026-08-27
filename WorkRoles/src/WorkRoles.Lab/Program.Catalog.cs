using System.Xml.Linq;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Lab.Data;

namespace WorkRoles.Lab;

internal static partial class Program
{
    internal sealed class Catalog
    {
        public List<RoleView> Roles = new();
        public List<PathView> Paths = new();
        public Dictionary<int, string> DefNames = new();
        public Dictionary<int, string> Labels = new();
        public Dictionary<int, string> PathNames = new();
        public List<int> Template = new();
        public RecommendationCatalogProjection Projection = null!;

        public string LabelOf(int roleId)
            => Labels.TryGetValue(roleId, out string? label) ? label
                : DefNames.TryGetValue(roleId, out string? defName)
                    ? defName.Substring(3) : $"?{roleId}";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "mod", "1.6", "Defs")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static readonly LabJobCatalog JobCatalog = new();

    // The CLI owns only offline input translation. All recommendation-derived
    // catalog facts are built by the same Core projection used by RecsAdapter.
    internal static Catalog Shipped()
    {
        var defsDir = Path.Combine(RepoRoot(), "mod", "1.6", "Defs");
        var catalog = new Catalog();
        var idByDef = new Dictionary<string, int>();
        var sources = new List<RecommendationRoleSource>();
        int id = 1;
        var roleDefs = XElement.Load(Path.Combine(defsDir, "Roles.xml"))
            .Elements("WorkRoles.RoleDef").ToList();
        foreach (var def in roleDefs)
        {
            if (!AvailableInAllDlc(def)) continue;
            string defName = RequiredText(def, "defName");
            List<JobEntry> entries = LoadEntries(def, defName);
            XElement? tuning = def.Element("tuning");
            int colonyMin = OptionalInt(
                tuning?.Element("colonyMin")?.Value, 0, defName, "colonyMin");
            int demandCoverage = OptionalInt(
                tuning?.Element("coverage")?.Value, 0, defName, "coverage");
            RecommendationSpecialRoleKind specialRole =
                OptionalEnum(
                    def.Element("recommendationSpecialRole")?.Value,
                    RecommendationSpecialRoleKind.None,
                    defName,
                    "recommendationSpecialRole");
            bool championPenalty = tuning == null
                ? !OptionalBool(def, "usesOccasionalRepeatChampionPenalty")
                : tuning.Element("championPenalty")?.Value.Trim() != "false";
            var source = new RecommendationRoleSource
            {
                Id = id,
                TemplateDefName = defName,
                Entries = entries,
                AutoAssign = OptionalBool(def, "autoAssign"),
                HasRules = !string.IsNullOrEmpty(
                        def.Element("activeHours")?.Value.Trim())
                    || def.Element("locations")?.Elements("li").Any() == true,
                Blocker = def.Element("blocker")?.Value.Trim() == "true",
                PreserveRecommendationOrder = OptionalBool(
                    def, "preserveRecommendationOrder"),
                ChampionPenalty = championPenalty,
                Category = OptionalEnum(
                    tuning?.Element("category")?.Value,
                    RoleCategory.None, defName, "category"),
                Time = OptionalEnum(
                    tuning?.Element("time")?.Value,
                    RoleTime.None, defName, "time"),
                DeclaredRequiredSkills = SkillList(
                    tuning?.Element("skills")?.Element("required"))!,
                ColonyMin = colonyMin,
                Coverage = demandCoverage,
                Available = true,
                Enabled = true,
                SpecialRole = specialRole,
            };
            sources.Add(source);
            catalog.DefNames[id] = defName;
            catalog.Labels[id] = def.Element("label")?.Value.Trim() ?? defName.Substring(3);
            idByDef[defName] = id;
            id++;
        }
        // Second pass: role-owned training paths resolve after every role id
        // is known (entries may reference roles declared later in the file).
        foreach (var def in roleDefs)
        {
            if (!AvailableInAllDlc(def)) continue;
            string defName = RequiredText(def, "defName");
            XElement? training = def.Element("tuning")?.Element("training");
            if (training == null || !idByDef.TryGetValue(defName, out int ownerId))
                continue;
            var path = new PathView { Id = ownerId };
            foreach (var li in training.Elements("li"))
            {
                string? roleDef = li.Element("role")?.Value.Trim();
                if (roleDef == null || !idByDef.TryGetValue(roleDef, out int roleId))
                    throw new InvalidDataException(
                        $"RoleDef {defName}: unknown training role '{roleDef}'.");
                path.RoleIds.Add(roleId);
                path.BandMins.Add(OptionalInt(
                    li.Element("min")?.Value,
                    0,
                    defName,
                    $"{roleDef} min"));
                path.BandMaxes.Add(OptionalInt(
                    li.Element("max")?.Value,
                    SkillProgressionMath.MaxLevel,
                    defName,
                    $"{roleDef} max"));
            }
            if (path.RoleIds.Count < 2
                || !path.RoleIds.Contains(ownerId)
                || !SkillProgressionMath.Validate(
                    path.RoleIds.Count, path.BandMins, path.BandMaxes))
                throw new InvalidDataException(
                    $"RoleDef {defName}: invalid training geometry.");
            catalog.Paths.Add(path);
            catalog.PathNames[path.Id] = catalog.Labels[ownerId];
        }

        catalog.Projection = RecommendationCatalogBuilder.Build(
            sources,
            catalog.Paths,
            JobCatalog,
            VanillaWorkOrder.NaturalPriority,
            VanillaJobSkillBaseline.Index);
        catalog.Roles = catalog.Projection.Roles.ToList();
        catalog.Template = OrderTemplate.DeriveTemplate(catalog.Roles);
        ApplyNewTemplateDefault(catalog);
        return catalog;
    }

    private static List<string>? SkillList(XElement? listElement) =>
        listElement?.Elements("li").Select(li => li.Value.Trim()).ToList();

    private static bool AvailableInAllDlc(XElement element)
    {
        string? packageId = element.Attribute("MayRequire")?.Value.Trim();
        return string.IsNullOrEmpty(packageId)
            || packageId.StartsWith("Ludeon.", StringComparison.OrdinalIgnoreCase);
    }

    private static List<JobEntry> LoadEntries(
        XElement definition, string defName)
    {
        var result = new List<JobEntry>();
        foreach (XElement element in definition.Element("entries")?.Elements("li")
                 ?? Enumerable.Empty<XElement>())
        {
            if (!AvailableInAllDlc(element)) continue;
            string raw = element.Value.Trim();
            if (!JobEntry.TryDecode(raw, out JobEntry entry))
                throw new InvalidDataException(
                    $"RoleDef {defName}: unparseable entry '{raw}'.");
            bool known = entry.Kind == JobEntryKind.WorkType
                ? VanillaWorkOrder.NaturalPriority.ContainsKey(entry.DefName)
                : VanillaGiverBaseline.GiverWorkType.ContainsKey(entry.DefName);
            if (!known)
                throw new InvalidDataException(
                    $"RoleDef {defName}: all-DLC baseline does not contain "
                    + $"{entry.Kind} '{entry.DefName}'.");
            result.Add(entry);
        }
        return result;
    }

    private static string RequiredText(XElement parent, string elementName)
    {
        string? value = parent.Element(elementName)?.Value.Trim();
        if (!string.IsNullOrEmpty(value)) return value;
        throw new InvalidDataException(
            $"{parent.Name}: missing {elementName}.");
    }

    private static bool OptionalBool(XElement parent, string elementName)
    {
        string? value = parent.Element(elementName)?.Value.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        if (bool.TryParse(value, out bool parsed)) return parsed;
        throw new InvalidDataException(
            $"{RequiredText(parent, "defName")}: invalid {elementName} '{value}'.");
    }

    private static int OptionalInt(
        string? value, int fallback, string owner, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (int.TryParse(value.Trim(), out int parsed)) return parsed;
        throw new InvalidDataException(
            $"{owner}: invalid {field} '{value}'.");
    }

    private static T OptionalEnum<T>(
        string? value, T fallback, string owner, string field)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (Enum.TryParse(value.Trim(), ignoreCase: true, out T parsed)
            && Enum.IsDefined(parsed))
            return parsed;
        throw new InvalidDataException(
            $"{owner}: invalid {field} '{value}'.");
    }

    /// New defaults (owner, 2026-08-01), not yet productized: reordered
    /// recommendation template.
    private static void ApplyNewTemplateDefault(Catalog catalog)
    {
        int IdOfLabel(string label) => catalog.Labels.First(kv => kv.Value == label).Key;
        catalog.Template = new[]
        {
            "Core", "Doctor", "Basics", "Caretaker", "Warden", "Handler", "Builder",
            "Cook", "Farmer", "Miner", "Tailor", "Smith", "Crafter", "Artist",
            "Fisher", "Grunt", "Researcher", "Anomalist",
        }.Select(IdOfLabel).ToList();
    }
}
