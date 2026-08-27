using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public sealed class FakeResourceCatalog : IItemPickerCatalog
{
    private readonly Dictionary<string, string> labels = new();
    private readonly Dictionary<string, string> aliases = new();
    private readonly Dictionary<string, (string label, List<string> members)> categories = new();
    private readonly Dictionary<string, (bool resource, bool storable, string sourceId)> itemMetadata = new();

    public FakeResourceCatalog With(string defName, string label)
    {
        labels[defName] = label;
        aliases.Remove(defName);
        itemMetadata[defName] = (true, true, ItemSourceIds.Vanilla);
        return this;
    }

    public FakeResourceCatalog WithAlias(string aliasDefName, string canonicalDefName)
    {
        aliases[aliasDefName] = canonicalDefName;
        return this;
    }

    public FakeResourceCatalog WithItem(string defName, string label,
        bool isResource, bool isStorable, string sourceId)
    {
        labels[defName] = label;
        itemMetadata[defName] = (isResource, isStorable, sourceId);
        return this;
    }

    public FakeResourceCatalog WithCategory(string catDefName, string label, params string[] memberDefNames)
    {
        categories[catDefName] = (label, new List<string>(memberDefNames));
        return this;
    }

    public bool Exists(string defName) =>
        labels.ContainsKey(defName)
        || aliases.TryGetValue(defName, out string? canonical)
            && labels.ContainsKey(canonical);

    public string CanonicalDefNameOf(string defName) =>
        labels.ContainsKey(defName)
            ? defName
            : aliases.TryGetValue(defName, out string? canonical)
                && labels.ContainsKey(canonical)
                ? canonical
                : "";

    public string LabelOf(string defName) =>
        labels.TryGetValue(CanonicalDefNameOf(defName), out var label) ? label : "";

    public string LabelCapOf(string defName)
    {
        string label = LabelOf(defName);
        return label.Length == 0
            ? label
            : char.ToUpperInvariant(label[0]) + label.Substring(1);
    }

    public IReadOnlyList<string> CountedDefsIn(string categoryDefName) =>
        categories.TryGetValue(categoryDefName, out var entry)
            ? entry.members
            : (IReadOnlyList<string>)Array.Empty<string>();

    public string CategoryLabelOf(string categoryDefName) =>
        categories.TryGetValue(categoryDefName, out var entry) ? entry.label : "";

    public bool IsResource(string defName) =>
        itemMetadata.TryGetValue(CanonicalDefNameOf(defName), out var metadata)
        && metadata.resource;

    public bool IsStorable(string defName) =>
        itemMetadata.TryGetValue(CanonicalDefNameOf(defName), out var metadata)
        && metadata.storable;

    public string SourceIdOf(string defName) =>
        itemMetadata.TryGetValue(CanonicalDefNameOf(defName), out var metadata)
            ? metadata.sourceId
            : ItemSourceIds.Vanilla;
}
