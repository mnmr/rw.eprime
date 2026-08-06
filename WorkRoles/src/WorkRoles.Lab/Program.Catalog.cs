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
        public RecommendationCatalogProjection Projection;

        public string LabelOf(int roleId)
            => Labels.TryGetValue(roleId, out string label) ? label
                : DefNames.TryGetValue(roleId, out string defName)
                    ? defName.Substring(3) : $"?{roleId}";
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static readonly LabJobCatalog JobCatalog = new();

    // The CLI owns only offline input translation. All recommendation-derived
    // catalog facts are built by the same Core projection used by RecsAdapter.
    internal static Catalog Shipped()
    {
        var defsDir = Path.Combine(RepoRoot(), "mod", "1.6", "Defs");
        var scales = LoadScales(defsDir);
        var catalog = new Catalog();
        var idByDef = new Dictionary<string, int>();
        var sources = new List<RecommendationRoleSource>();
        int id = 1;
        foreach (var def in XElement.Load(Path.Combine(defsDir, "Roles.xml"))
                     .Elements("WorkRoles.RoleDef"))
        {
            if (!AvailableInAllDlc(def)) continue;
            string defName = RequiredText(def, "defName");
            List<JobEntry> entries = LoadEntries(def, defName);
            XElement minHoldersElement = def.Element("minHolders");
            int requiredTotal = OptionalInt(
                minHoldersElement?.Value, fallback: 0, defName, "minHolders");
            int trainingWaivers = OptionalInt(
                minHoldersElement?.Attribute("waivers")?.Value,
                fallback: 0,
                defName,
                "minHolders waivers");
            string scaleName = def.Element("holderScale")?.Value.Trim();
            HolderScale scale = null;
            if (!string.IsNullOrEmpty(scaleName)
                && !scales.TryGetValue(scaleName, out scale))
                throw new InvalidDataException(
                    $"RoleDef {defName}: unknown holderScale '{scaleName}'.");
            RecommendationSpecialRoleKind specialRole =
                OptionalEnum(
                    def.Element("recommendationSpecialRole")?.Value,
                    RecommendationSpecialRoleKind.None,
                    defName,
                    "recommendationSpecialRole");
            var source = new RecommendationRoleSource
            {
                Id = id,
                Entries = entries,
                AutoAssign = OptionalBool(def, "autoAssign"),
                HasRules = !string.IsNullOrEmpty(
                        def.Element("activeHours")?.Value.Trim())
                    || def.Element("locations")?.Elements("li").Any() == true,
                Blocker = def.Element("blocker")?.Value.Trim() == "true",
                PreserveRecommendationOrder = OptionalBool(
                    def, "preserveRecommendationOrder"),
                UsesOccasionalRepeatChampionPenalty = OptionalBool(
                    def, "usesOccasionalRepeatChampionPenalty"),
                HolderMode = RoleHolderMode.Auto,
                Scale = scale,
                RequiredTotal = requiredTotal,
                MaxHolders = OptionalInt(
                    def.Element("maxHolders")?.Value,
                    RoleHolderRange.Uncapped,
                    defName,
                    "maxHolders"),
                TrainingWaivers = RoleHolderPolicy.WithTrainingWaivers(
                    requiredTotal, trainingWaivers),
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
        int pathId = 1;
        foreach (var def in XElement.Load(Path.Combine(defsDir, "TrainingPaths.xml"))
                     .Elements("WorkRoles.TrainingPathDef"))
        {
            if (!AvailableInAllDlc(def)) continue;
            string pathDefName = RequiredText(def, "defName");
            var path = new PathView { Id = pathId++ };
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string roleDef = li.Element("role")?.Value.Trim();
                if (roleDef == null || !idByDef.TryGetValue(roleDef, out int roleId))
                    throw new InvalidDataException(
                        $"TrainingPathDef {pathDefName}: unknown role '{roleDef}'.");
                path.RoleIds.Add(roleId);
                path.BandMins.Add(OptionalInt(
                    li.Element("min")?.Value,
                    0,
                    pathDefName,
                    $"{roleDef} min"));
                path.BandMaxes.Add(OptionalInt(
                    li.Element("max")?.Value,
                    SkillProgressionMath.MaxLevel,
                    pathDefName,
                    $"{roleDef} max"));
            }
            string anchor = def.Element("anchorRole")?.Value.Trim();
            if (anchor != null)
            {
                if (!idByDef.TryGetValue(anchor, out int anchorId))
                    throw new InvalidDataException(
                        $"TrainingPathDef {pathDefName}: unknown anchorRole '{anchor}'.");
                path.AnchorRoleId = anchorId;
            }
            path.AnchorBefore = def.Element("anchorBefore")?.Value.Trim() != "false";
            if (path.RoleIds.Count < 2
                || !SkillProgressionMath.Validate(
                    path.RoleIds.Count, path.BandMins, path.BandMaxes))
                throw new InvalidDataException(
                    $"TrainingPathDef {pathDefName}: invalid path geometry.");
            catalog.Paths.Add(path);
            catalog.PathNames[path.Id] =
                def.Element("label")?.Value.Trim() ?? $"Path{path.Id}";
        }

        ApplyNewPathDefaults(catalog);
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

    private static bool AvailableInAllDlc(XElement element)
    {
        string packageId = element.Attribute("MayRequire")?.Value.Trim();
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
        string value = parent.Element(elementName)?.Value.Trim();
        if (!string.IsNullOrEmpty(value)) return value;
        throw new InvalidDataException(
            $"{parent.Name}: missing {elementName}.");
    }

    private static bool OptionalBool(XElement parent, string elementName)
    {
        string value = parent.Element(elementName)?.Value.Trim();
        if (string.IsNullOrEmpty(value)) return false;
        if (bool.TryParse(value, out bool parsed)) return parsed;
        throw new InvalidDataException(
            $"{RequiredText(parent, "defName")}: invalid {elementName} '{value}'.");
    }

    private static int OptionalInt(
        string value, int fallback, string owner, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (int.TryParse(value.Trim(), out int parsed)) return parsed;
        throw new InvalidDataException(
            $"{owner}: invalid {field} '{value}'.");
    }

    private static T OptionalEnum<T>(
        string value, T fallback, string owner, string field)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        if (Enum.TryParse(value.Trim(), ignoreCase: true, out T parsed)
            && Enum.IsDefined(parsed))
            return parsed;
        throw new InvalidDataException(
            $"{owner}: invalid {field} '{value}'.");
    }

    private static Dictionary<string, HolderScale> LoadScales(string defsDir)
    {
        var result = new Dictionary<string, HolderScale>(
            StringComparer.Ordinal);
        foreach (var def in XElement.Load(Path.Combine(defsDir, "Scales.xml"))
                     .Elements("WorkRoles.ScaleDef"))
        {
            if (!AvailableInAllDlc(def)) continue;
            string label = def.Element("label")?.Value.Trim();
            if (string.IsNullOrEmpty(label))
                throw new InvalidDataException(
                    $"ScaleDef {RequiredText(def, "defName")}: missing label.");
            var scale = new HolderScale
            {
                Name = label,
                Preset = true,
                RequiredTotals = HolderScaleCodec.DecodeRow(
                    def.Element("min")?.Value, fallback: 0),
                TrainingWaivers = HolderScaleCodec.DecodeRow(
                    def.Element("train")?.Value, fallback: 0),
                Max = HolderScaleCodec.DecodeRow(
                    def.Element("max")?.Value,
                    fallback: RoleHolderRange.Uncapped),
            };
            scale.Normalize();
            result[label] = scale;
        }
        return result;
    }

    /// New defaults (owner, 2026-08-01), not yet productized: re-anchored paths.
    /// These are inputs to the shared catalog builder, which owns its snapshot.
    private static void ApplyNewPathDefaults(Catalog catalog)
    {
        int IdOfLabel(string label) => catalog.Labels.First(kv => kv.Value == label).Key;

        void Anchor(string pathName, string roleLabel, bool before)
        {
            var path = catalog.Paths.First(p => catalog.PathNames[p.Id] == pathName);
            path.AnchorRoleId = IdOfLabel(roleLabel);
            path.AnchorBefore = before;
        }
        Anchor("Drug Maker", "Warden", false);
        Anchor("Fabricator", "Warden", false);
        Anchor("Builder", "Warden", false);
        Anchor("Cook", "Handler", false);
        Anchor("Socialist", "Basics", false);
        Anchor("Handler", "Warden", false);
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
