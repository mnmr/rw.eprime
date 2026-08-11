using System.Xml.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles.Lab;

internal static partial class Program
{
    /// Prints a ready-to-paste <tuning> block per shipped RoleDef: skills from
    /// the same derivation the engine uses, category/time from the owner's
    /// defaults with coverage inheritance, demand numbers and championPenalty
    /// carried over from the current defs.
    private static void PrintTuning()
    {
        var catalog = Shipped();
        string[] importants = { "Core", "Basics", "Doctor", "Warden", "Caretaker" };
        string[] optionals = { "Hunter", "Fisher", "Grunt", "Researcher", "Anomalist" };
        string[] partTimes = { "Core", "Basics", "Doctor", "Warden", "Caretaker", "Anomalist" };
        string[] opportunistics = { "Grunt", "Researcher" };

        var demand = new Dictionary<string, (int colonyMin, int coverage)>();
        var legacyOccasional = new HashSet<string>();
        var defsDir = Path.Combine(RepoRoot(), "mod", "1.6", "Defs");
        foreach (var def in XElement.Load(Path.Combine(defsDir, "Roles.xml"))
                     .Elements("WorkRoles.RoleDef"))
        {
            string defName = def.Element("defName")!.Value.Trim();
            XElement tuningEl = def.Element("tuning");
            int colonyMin = OptionalInt(
                tuningEl?.Element("colonyMin")?.Value, 0, defName, "colonyMin");
            int coverage = OptionalInt(
                tuningEl?.Element("coverage")?.Value, 0, defName, "coverage");
            if (colonyMin != 0 || coverage != 0)
                demand[defName] = (colonyMin, coverage);
            bool occasional = tuningEl == null
                ? def.Element("usesOccasionalRepeatChampionPenalty")?.Value.Trim() == "true"
                : tuningEl.Element("championPenalty")?.Value.Trim() == "false";
            if (occasional) legacyOccasional.Add(defName);
        }

        RoleView ViewOfLabel(string label) =>
            catalog.Roles.First(r => catalog.LabelOf(r.Id) == label);
        bool CoveredBy(RoleView role, string label)
        {
            RoleView owner = ViewOfLabel(label);
            return owner.Id != role.Id && owner.Coverage.IsSupersetOf(role.Coverage);
        }
        string Classify(RoleView role, string label, string[] first, string firstValue,
            string[] second, string secondValue, string fallback)
        {
            if (first.Contains(label)) return firstValue;
            if (second.Contains(label)) return secondValue;
            if (first.Any(l => CoveredBy(role, l))) return firstValue;
            if (second.Any(l => CoveredBy(role, l))) return secondValue;
            return fallback;
        }

        foreach (var role in catalog.Roles)
        {
            string defName = catalog.DefNames[role.Id];
            string label = catalog.LabelOf(role.Id);
            string category = Classify(role, label,
                importants, "Important", optionals, "Optional", "Normal");
            string time = Classify(role, label,
                partTimes, "PartTime", opportunistics, "Opportunistic", "FullTime");
            var required = role.Skills.Where(s => s.Required)
                .Select(s => s.SkillDefName).ToList();
            var optional = role.Skills.Where(s => !s.Required)
                .Select(s => s.SkillDefName).ToList();

            Console.WriteLine($"  {defName} ({label})");
            Console.WriteLine("    <tuning>");
            if (required.Count > 0 || optional.Count > 0)
            {
                Console.WriteLine("      <skills>");
                if (required.Count > 0)
                {
                    Console.WriteLine("        <required>");
                    foreach (var skill in required)
                        Console.WriteLine($"          <li>{skill}</li>");
                    Console.WriteLine("        </required>");
                }
                if (optional.Count > 0)
                {
                    Console.WriteLine("        <optional>");
                    foreach (var skill in optional)
                        Console.WriteLine($"          <li>{skill}</li>");
                    Console.WriteLine("        </optional>");
                }
                Console.WriteLine("      </skills>");
            }
            Console.WriteLine($"      <category>{category}</category>");
            Console.WriteLine($"      <time>{time}</time>");
            if (demand.TryGetValue(defName, out var numbers))
            {
                if (numbers.colonyMin != 0)
                    Console.WriteLine($"      <colonyMin>{numbers.colonyMin}</colonyMin>");
                if (numbers.coverage != 0)
                    Console.WriteLine($"      <coverage>{numbers.coverage}</coverage>");
            }
            if (legacyOccasional.Contains(defName))
                Console.WriteLine("      <championPenalty>false</championPenalty>");
            Console.WriteLine("    </tuning>");
        }
    }
}
