using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Core's window onto def data. The game assembly wraps DefDatabase;
    /// tests use FakeResourceCatalog.
    public interface IResourceCatalog
    {
        bool Exists(string defName);
        /// <summary>
        /// Resolves a requested name, including compatibility aliases, to the
        /// live definition's own name; returns an empty string when missing.
        /// </summary>
        string CanonicalDefNameOf(string defName);
        string LabelOf(string defName);
        /// <summary>Returns the label formatted for standalone UI display.</summary>
        string LabelCapOf(string defName);
        System.Collections.Generic.IReadOnlyList<string> CountedDefsIn(string categoryDefName);
        string CategoryLabelOf(string categoryDefName);
    }
}
