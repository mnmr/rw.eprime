using System.Xml.Linq;

namespace WorkRoles.Core.Tests.UI;

/// Pins the shipped Palette.xml to the canonical Tailwind v3 values: exactly
/// the editor's swatch vocabulary (19 families, shades 600-900), each hex
/// matching. The editor's C# table (RolesTabView) cannot be referenced from
/// this project; keeping it in sync with these canonical values is manual.
public class PaletteDefTests
{
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

    private static readonly int[] Shades = [900, 800, 700, 600];

    private static Dictionary<string, string> ShippedPalette()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "mod", "1.6", "Defs")))
            dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "mod", "1.6", "Defs", "Palette.xml");
        return XElement
            .Load(path)
            .Elements("WorkRoles.PaletteDef")
            .ToDictionary(def => def.Element("defName")!.Value.Trim(), def => def.Element("hex")!.Value.Trim().TrimStart('#').ToLowerInvariant());
    }

    [Test]
    public async Task PaletteIsExactlyTheEditorVocabulary()
    {
        var shipped = ShippedPalette();
        HashSet<string> expectedNames =
        [
            "slate-900",
            "slate-800",
            "slate-700",
            "slate-600",
            "stone-900",
            "stone-800",
            "stone-700",
            "stone-600",
            "red-900",
            "red-800",
            "red-700",
            "red-600",
            "orange-900",
            "orange-800",
            "orange-700",
            "orange-600",
            "amber-900",
            "amber-800",
            "amber-700",
            "amber-600",
            "yellow-900",
            "yellow-800",
            "yellow-700",
            "yellow-600",
            "lime-900",
            "lime-800",
            "lime-700",
            "lime-600",
            "green-900",
            "green-800",
            "green-700",
            "green-600",
            "emerald-900",
            "emerald-800",
            "emerald-700",
            "emerald-600",
            "teal-900",
            "teal-800",
            "teal-700",
            "teal-600",
            "cyan-900",
            "cyan-800",
            "cyan-700",
            "cyan-600",
            "sky-900",
            "sky-800",
            "sky-700",
            "sky-600",
            "blue-900",
            "blue-800",
            "blue-700",
            "blue-600",
            "indigo-900",
            "indigo-800",
            "indigo-700",
            "indigo-600",
            "violet-900",
            "violet-800",
            "violet-700",
            "violet-600",
            "purple-900",
            "purple-800",
            "purple-700",
            "purple-600",
            "fuchsia-900",
            "fuchsia-800",
            "fuchsia-700",
            "fuchsia-600",
            "pink-900",
            "pink-800",
            "pink-700",
            "pink-600",
            "rose-900",
            "rose-800",
            "rose-700",
            "rose-600",
        ];
        await Assert
            .That(shipped.Keys.ToHashSet().SetEquals(expectedNames))
            .IsTrue()
            .Because(
                "Palette.xml must carry exactly the editor's swatch names, "
                    + $"missing: [{string.Join(", ", expectedNames.Except(shipped.Keys))}], "
                    + $"extra: [{string.Join(", ", shipped.Keys.Except(expectedNames))}]"
            );
    }

    [Test]
    public async Task PaletteHexesMatchTheCanonicalValues()
    {
        Dictionary<string, string> shipped = ShippedPalette();
        foreach ((string family, string[] expectedHexes) in Hexes)
        {
            foreach ((int shade, string expectedHex) in Shades.Zip(expectedHexes))
            {
                string name = $"{family}-{shade}";
                await Assert.That(shipped.TryGetValue(name, out string? actualHex)).IsTrue();
                await Assert.That(actualHex).IsEqualTo(expectedHex).Because($"{name} drifted from the canonical Tailwind value");
            }
        }
    }
}
