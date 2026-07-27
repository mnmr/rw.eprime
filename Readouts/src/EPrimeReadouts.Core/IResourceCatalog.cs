using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Core's window onto def data. The game assembly wraps DefDatabase;
    /// tests use FakeResourceCatalog.
    public interface IResourceCatalog
    {
        bool Exists(string defName);
        string LabelOf(string defName);
        System.Collections.Generic.IReadOnlyList<string> CountedDefsIn(string categoryDefName);
        string CategoryLabelOf(string categoryDefName);
    }
}
