using System.Xml.Linq;

namespace WorkRoles.Core.Tests.SampleColony;

/// Shipped role-def tuning parsed from mod/1.6/Defs/Roles.xml at fixture init,
/// so sample-colony planning always uses the defaults the game ships.
public static class RoleDefaults
{
    public sealed class DefTuning
    {
        public RoleCategory Category;
        public RoleTime Time;
        public bool ChampionPenalty = true;
        public int ColonyMin;
        public int Coverage;
        public List<string> RequiredSkills = new();
        public List<(string Role, int Min, int Max)> Training = new();
    }

    public static readonly IReadOnlyDictionary<string, DefTuning> ByDefName = Load();

    private static Dictionary<string, DefTuning> Load()
    {
        var result = new Dictionary<string, DefTuning>(StringComparer.Ordinal);
        XElement root = XDocument.Load(RolesXmlPath()).Root ?? throw new InvalidDataException("Roles.xml has no root element");
        foreach (XElement def in root.Elements("WorkRoles.RoleDef"))
        {
            string? defName = def.Element("defName")?.Value;
            if (defName == null)
                continue;
            var tuning = new DefTuning();
            XElement? values = def.Element("tuning");
            if (values != null)
            {
                tuning.Category = EnumOf(values.Element("category"), tuning.Category);
                tuning.Time = EnumOf(values.Element("time"), tuning.Time);
                tuning.ChampionPenalty = BoolOf(values.Element("championPenalty"), tuning.ChampionPenalty);
                tuning.ColonyMin = IntOf(values.Element("colonyMin"));
                tuning.Coverage = IntOf(values.Element("coverage"));
                XElement? skills = values.Element("skills");
                tuning.RequiredSkills = SkillsOf(skills?.Element("required"));
                tuning.Training = TrainingOf(values.Element("training"));
            }
            result[defName] = tuning;
        }
        return result;
    }

    private static TEnum EnumOf<TEnum>(XElement? element, TEnum fallback)
        where TEnum : struct => element == null ? fallback : Enum.Parse<TEnum>(element.Value, true);

    private static bool BoolOf(XElement? element, bool fallback) => element == null ? fallback : bool.Parse(element.Value);

    private static int IntOf(XElement? element) => element == null ? 0 : int.Parse(element.Value);

    private static List<string> SkillsOf(XElement? list) => list == null ? [] : [.. list.Elements("li").Select(skill => skill.Value.Trim())];

    /// Band entries as authored: min defaults to 0 and a missing max is the
    /// open top, matching the game-side def seeding.
    private static List<(string Role, int Min, int Max)> TrainingOf(XElement? list)
    {
        List<(string Role, int Min, int Max)> result = [];
        if (list == null)
            return result;
        foreach (XElement entry in list.Elements("li"))
        {
            string? role = entry.Element("role")?.Value.Trim();
            if (string.IsNullOrEmpty(role))
                continue;
            result.Add((role, IntOf(entry.Element("min")), entry.Element("max") == null ? SkillProgressionMath.MaxLevel : IntOf(entry.Element("max"))));
        }
        return result;
    }

    private static string RolesXmlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(directory.FullName, "mod", "1.6", "Defs", "Roles.xml");
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("mod/1.6/Defs/Roles.xml not found above " + AppContext.BaseDirectory);
    }
}
