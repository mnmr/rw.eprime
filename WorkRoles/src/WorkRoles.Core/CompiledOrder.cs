using System.Collections.Generic;

namespace WorkRoles.Core
{
    public sealed class CompiledOrder
    {
        public List<string> AllInOrder = new List<string>();
        public List<string> Normal = new List<string>();
        public List<string> Emergency = new List<string>();
        /// <summary>workType defName -> unique rank (1..N, by first appearance in the compiled
        /// order); a work type absent from this map is priority 0 (never).</summary>
        public Dictionary<string, int> WorkTypePriorities = new Dictionary<string, int>();
    }
}
