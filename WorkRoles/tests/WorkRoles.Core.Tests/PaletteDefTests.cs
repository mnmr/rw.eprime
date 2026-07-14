using System.Xml.Linq;

namespace WorkRoles.Core.Tests;

/// Pins the shipped Palette.xml to the canonical Tailwind v3 values: exactly
/// the editor's swatch vocabulary (19 families, shades 600-900), each hex
/// matching. The editor's C# table and this data must change together.
public class PaletteDefTests
{
    private static readonly string[] Families =
    {
        "slate", "stone", "red", "orange", "amber", "yellow", "lime", "green", "emerald",
        "teal", "cyan", "sky", "blue", "indigo", "violet", "purple", "fuchsia", "pink", "rose",
    };

    // family -> (900, 800, 700, 600), same source as RolesTabView's swatch table.
    private static readonly Dictionary<string, string[]> Hexes = new()
    {
        ["slate"] = ["0f172a", "1e293b", "334155", "475569"],
        ["stone"] = ["1c1917", "292524", "44403c", "57534e"],
        ["red"] = ["7f1d1d", "991b1b", "b91c1c", "dc2626"],
        ["orange"] = ["7c2d12", "9a3412", "c2410c", "ea580c"],
        ["amber"] = ["78350f", "92400e", "b45309", "d97706"],
        ["yellow"] = ["713f12", "854d0e", "a16207", "ca8a04"],
        ["lime"] = ["365314", "3f6212", "4d7c0f", "65a30d"],
        ["green"] = ["14532d", "166534", "15803d", "16a34a"],
        ["emerald"] = ["064e3b", "065f46", "047857", "059669"],
        ["teal"] = ["134e4a", "115e59", "0f766e", "0d9488"],
        ["cyan"] = ["164e63", "155e75", "0e7490", "0891b2"],
        ["sky"] = ["0c4a6e", "075985", "0369a1", "0284c7"],
        ["blue"] = ["1e3a8a", "1e40af", "1d4ed8", "2563eb"],
        ["indigo"] = ["312e81", "3730a3", "4338ca", "4f46e5"],
        ["violet"] = ["4c1d95", "5b21b6", "6d28d9", "7c3aed"],
        ["purple"] = ["581c87", "6b21a8", "7e22ce", "9333ea"],
        ["fuchsia"] = ["701a75", "86198f", "a21caf", "c026d3"],
        ["pink"] = ["831843", "9d174d", "be185d", "db2777"],
        ["rose"] = ["881337", "9f1239", "be123c", "e11d48"],
    };

    private static readonly int[] Shades = { 900, 800, 700, 600 };

    private static Dictionary<string, string> ShippedPalette()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "mod", "1.6", "Defs", "Palette.xml");
        return XElement.Load(path).Elements("WorkRoles.PaletteDef").ToDictionary(
            def => def.Element("defName")!.Value.Trim(),
            def => def.Element("hex")!.Value.Trim().TrimStart('#').ToLowerInvariant());
    }

    [Test]
    public async Task PaletteIsExactlyTheEditorVocabulary()
    {
        var shipped = ShippedPalette();
        var expectedNames = Families
            .SelectMany(family => Shades.Select(shade => $"{family}-{shade}"))
            .ToHashSet();
        await Assert.That(shipped.Keys.ToHashSet().SetEquals(expectedNames)).IsTrue()
            .Because("Palette.xml must carry exactly the editor's swatch names, "
                + $"missing: [{string.Join(", ", expectedNames.Except(shipped.Keys))}], "
                + $"extra: [{string.Join(", ", shipped.Keys.Except(expectedNames))}]");
    }

    [Test]
    public async Task PaletteHexesMatchTheCanonicalValues()
    {
        var shipped = ShippedPalette();
        foreach (var family in Families)
            for (int i = 0; i < Shades.Length; i++)
            {
                string name = $"{family}-{Shades[i]}";
                await Assert.That(shipped[name]).IsEqualTo(Hexes[family][i])
                    .Because($"{name} drifted from the canonical Tailwind value");
            }
    }
}
