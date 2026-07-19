using System.Xml.Linq;

namespace WorkRoles.Core.Tests;

public class RoleDefHolderDefaultsTests
{
    [Test]
    public async Task ShippedRolesHaveTheConfiguredMinimumsAndWaivers()
    {
        var expected = new Dictionary<string, (int minimum, int waivers)>
        {
            ["WS_Doctor"] = (2, 1),
            ["WS_Farmer"] = (2, 1),
            ["WS_Warden"] = (2, 1),
            ["WS_Childminder"] = (1, 0),
            ["WS_Builder"] = (1, 0),
            ["WS_Tailor"] = (1, 0),
            ["WS_Smith"] = (1, 0),
            ["WS_Crafter"] = (1, 0),
            ["WS_Miner"] = (1, 0),
            ["WS_Cook"] = (1, 0),
            ["WS_Researcher"] = (3, 0),
            ["WS_DarkStudier"] = (1, 0),
        };
        string path = Path.Combine(RepoRoot(), "mod", "1.6", "Defs", "Roles.xml");

        var seen = new HashSet<string>();
        foreach (var def in XElement.Load(path).Elements("WorkRoles.RoleDef"))
        {
            string defName = def.Element("defName")!.Value.Trim();
            seen.Add(defName);
            var configured = expected.TryGetValue(defName, out var value)
                ? value : (minimum: 0, waivers: 0);
            var minimumNode = def.Element("minHolders");
            int minimum = minimumNode == null ? 0 : int.Parse(minimumNode.Value);
            int waivers = minimumNode?.Attribute("waivers") == null
                ? 0 : int.Parse(minimumNode.Attribute("waivers")!.Value);

            await Assert.That((minimum, waivers)).IsEqualTo(configured)
                .Because($"{defName} has the wrong shipped holder defaults");
        }

        // A renamed or deleted def must not leave a stale expectation behind.
        foreach (string defName in expected.Keys)
            await Assert.That(seen.Contains(defName)).IsTrue()
                .Because($"{defName} is expected but not shipped");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
    }
}
