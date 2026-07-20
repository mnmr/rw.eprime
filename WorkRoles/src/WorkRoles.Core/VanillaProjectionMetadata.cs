using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core
{
    /// Pure, definition-derived work-type facts. Names are invariant defNames;
    /// game Def instances and translated labels never cross into Core.
    public readonly struct VanillaProjectionWorkTypeSource
    {
        public VanillaProjectionWorkTypeSource(
            string workTypeName, bool skilled, bool research)
        {
            WorkTypeName = workTypeName;
            Skilled = skilled;
            Research = research;
        }

        public string WorkTypeName { get; }
        public bool Skilled { get; }
        public bool Research { get; }
    }

    /// Copy-owned snapshot of the definition facts that do not depend on the
    /// current save's Basics role.
    public sealed class VanillaProjectionDefinitionMetadata
    {
        private readonly List<string> workTypes;
        private readonly Dictionary<string, int> columns;
        private readonly HashSet<string> skilled;
        private readonly HashSet<string> research;

        public VanillaProjectionDefinitionMetadata(
            IEnumerable<VanillaProjectionWorkTypeSource> sources) : this(sources, null)
        {
        }

        public VanillaProjectionDefinitionMetadata(
            IEnumerable<VanillaProjectionWorkTypeSource> sources,
            IEnumerable<string> priorityOrderedWorkTypes)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));

            workTypes = new List<string>();
            columns = new Dictionary<string, int>(StringComparer.Ordinal);
            var seenWorkTypes = new HashSet<string>(StringComparer.Ordinal);
            skilled = new HashSet<string>(StringComparer.Ordinal);
            research = new HashSet<string>(StringComparer.Ordinal);
            foreach (VanillaProjectionWorkTypeSource source in sources)
            {
                string name = source.WorkTypeName;
                if (string.IsNullOrEmpty(name) || !seenWorkTypes.Add(name)) continue;
                if (priorityOrderedWorkTypes == null)
                    columns.Add(name, columns.Count);
                workTypes.Add(name);
                if (source.Skilled)
                {
                    skilled.Add(name);
                    if (source.Research) research.Add(name);
                }
            }

            if (priorityOrderedWorkTypes != null)
                foreach (string name in priorityOrderedWorkTypes)
                    if (!string.IsNullOrEmpty(name) && !columns.ContainsKey(name))
                        columns.Add(name, columns.Count);
        }

        public VanillaProjectionMetadata WithBasics(IEnumerable<string> basics) =>
            new VanillaProjectionMetadata(workTypes, columns, skilled, research, basics);
    }

    /// Immutable projection policy for one definition snapshot and one Basics
    /// revision. Public views are read-only and all caller-owned inputs are copied.
    public sealed class VanillaProjectionMetadata
    {
        private readonly Dictionary<string, int> columns;
        private readonly HashSet<string> basicsSet;
        private readonly HashSet<string> skilledSet;
        private readonly HashSet<string> gruntSet;
        private readonly HashSet<string> researchSet;

        internal VanillaProjectionMetadata(
            IEnumerable<string> workTypes,
            IDictionary<string, int> columns,
            IEnumerable<string> skilled,
            IEnumerable<string> research,
            IEnumerable<string> basics)
        {
            if (basics == null) throw new ArgumentNullException(nameof(basics));

            var ownedWorkTypes = new List<string>(workTypes);
            this.columns = new Dictionary<string, int>(columns, StringComparer.Ordinal);
            skilledSet = new HashSet<string>(skilled, StringComparer.Ordinal);
            researchSet = new HashSet<string>(research, StringComparer.Ordinal);

            var ownedBasics = new List<string>();
            basicsSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in basics)
                if (!string.IsNullOrEmpty(name) && basicsSet.Add(name))
                    ownedBasics.Add(name);

            var ownedSkilled = new List<string>();
            var ownedGrunt = new List<string>();
            var ownedResearch = new List<string>();
            gruntSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in ownedWorkTypes)
            {
                if (skilledSet.Contains(name)) ownedSkilled.Add(name);
                else if (!basicsSet.Contains(name))
                {
                    gruntSet.Add(name);
                    ownedGrunt.Add(name);
                }
                if (researchSet.Contains(name)) ownedResearch.Add(name);
            }

            WorkTypes = new ReadOnlyCollection<string>(ownedWorkTypes);
            Columns = new ReadOnlyDictionary<string, int>(this.columns);
            Basics = new ReadOnlyCollection<string>(ownedBasics);
            Skilled = new ReadOnlyCollection<string>(ownedSkilled);
            Grunt = new ReadOnlyCollection<string>(ownedGrunt);
            Research = new ReadOnlyCollection<string>(ownedResearch);
            ColumnResolver = ColumnOf;
            ProjectionCategories = new VanillaProjectionCategories
            {
                Basics = basicsSet,
                Skilled = skilledSet,
                Grunt = gruntSet,
                Research = researchSet,
            };
        }

        public IReadOnlyList<string> WorkTypes { get; }
        public IReadOnlyDictionary<string, int> Columns { get; }
        public IReadOnlyList<string> Basics { get; }
        public IReadOnlyList<string> Skilled { get; }
        public IReadOnlyList<string> Grunt { get; }
        public IReadOnlyList<string> Research { get; }

        internal Func<string, int> ColumnResolver { get; }
        internal VanillaProjectionCategories ProjectionCategories { get; }

        public int ColumnOf(string workTypeName)
        {
            return workTypeName != null
                && columns.TryGetValue(workTypeName, out int column)
                    ? column : int.MaxValue;
        }

        public bool IsBasics(string workTypeName) =>
            workTypeName != null && basicsSet.Contains(workTypeName);

        public bool IsSkilled(string workTypeName) =>
            workTypeName != null && skilledSet.Contains(workTypeName);

        public bool IsGrunt(string workTypeName) =>
            workTypeName != null && gruntSet.Contains(workTypeName);

        public bool IsResearch(string workTypeName) =>
            workTypeName != null && researchSet.Contains(workTypeName);
    }
}
