using System.Text;
using System.Xml.Linq;

namespace WorkRoles.Core.Tests;

/// Snapshots the shipped Roles.xml into StaticRoles.DefaultSet source text:
/// run, then paste bin/.../static-roles.g.cs over the table. Third-party
/// MayRequire entries are skipped, DLC (Ludeon.*) kept — the same runtime the
/// def tests emulate.
public class StaticRolesGenerator
{
    [Test]
    public async Task Generate()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        var xml = XElement.Load(Path.Combine(dir!.FullName, "mod", "1.6", "Defs", "Roles.xml"));

        var sb = new StringBuilder();
        sb.AppendLine("    private static readonly RoleSpec[] DefaultSet =");
        sb.AppendLine("    {");
        foreach (var def in xml.Elements("WorkRoles.RoleDef"))
        {
            string defName = def.Element("defName")!.Value;
            string label = def.Element("label")!.Value;
            bool auto = def.Element("autoAssign")?.Value == "true";
            bool blocker = def.Element("blocker")?.Value == "true";
            int minHolders = int.TryParse(def.Element("minHolders")?.Value, out int mh) ? mh : -1;
            var entries = new List<string>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                string mayRequire = li.Attribute("MayRequire")?.Value;
                if (mayRequire != null && !mayRequire.StartsWith("Ludeon.")) continue;
                entries.Add(li.Value.Trim());
            }

            string Quote(string s) => s == null ? "null" : $"\"{s}\"";
            string Array(IEnumerable<string> items)
            {
                var list = items.ToList();
                return list.Count == 0 ? "new string[0]"
                    : "new[] { " + string.Join(", ", list.Select(x => $"\"{x}\"")) + " }";
            }

            sb.AppendLine($"        new RoleSpec({Quote(label)}, {Quote(defName)},");
            sb.AppendLine($"            AutoAssign: {(auto ? "true" : "false")}, Blocker: {(blocker ? "true" : "false")},");
            sb.AppendLine($"            MinHolders: {minHolders},");
            sb.AppendLine($"            Entries: {Array(entries)}),");
        }
        sb.AppendLine("    };");

        string path = Path.Combine(AppContext.BaseDirectory, "static-roles.g.cs");
        File.WriteAllText(path, sb.ToString());
        await Assert.That(File.Exists(path)).IsTrue();
    }
}
