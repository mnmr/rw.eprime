using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public sealed class FakeResourceCatalog : IResourceCatalog
{
    private readonly Dictionary<string, string> labels = new();
    private readonly Dictionary<string, (string label, List<string> members)> categories = new();

    public FakeResourceCatalog With(string defName, string label)
    {
        labels[defName] = label;
        return this;
    }

    public FakeResourceCatalog WithCategory(string catDefName, string label, params string[] memberDefNames)
    {
        categories[catDefName] = (label, new List<string>(memberDefNames));
        return this;
    }

    public bool Exists(string defName) => labels.ContainsKey(defName);

    public string LabelOf(string defName) =>
        labels.TryGetValue(defName, out var label) ? label : "";

    public IReadOnlyList<string> CountedDefsIn(string categoryDefName) =>
        categories.TryGetValue(categoryDefName, out var entry)
            ? entry.members
            : (IReadOnlyList<string>)Array.Empty<string>();

    public string CategoryLabelOf(string categoryDefName) =>
        categories.TryGetValue(categoryDefName, out var entry) ? entry.label : "";
}
