using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    public readonly struct WorkCapabilityFact
    {
        public WorkCapabilityFact(string giverDefName, string? recipeDefName, string sourcePackageId, string reasonKey)
        { GiverDefName = giverDefName; RecipeDefName = recipeDefName; SourcePackageId = sourcePackageId; ReasonKey = reasonKey; }
        public string GiverDefName { get; }
        public string? RecipeDefName { get; }
        public string SourcePackageId { get; }
        public string ReasonKey { get; }
    }

    public readonly struct WorkAversionFact
    {
        public WorkAversionFact(string workTypeDefName, string preceptDefName, string sourcePackageId)
        { WorkTypeDefName = workTypeDefName; PreceptDefName = preceptDefName; SourcePackageId = sourcePackageId; }
        public string WorkTypeDefName { get; }
        public string PreceptDefName { get; }
        public string SourcePackageId { get; }
    }

    /// Detached, normalized evidence owned by the pawn snapshot. A capability
    /// fact is a verified block; absence never certifies that arbitrary jobs work.
    public sealed class PawnWorkEvidence
    {
        public static readonly PawnWorkEvidence Empty = new PawnWorkEvidence(Array.Empty<WorkCapabilityFact>(), Array.Empty<WorkAversionFact>());
        private readonly WorkCapabilityFact[] capabilities;
        private readonly WorkAversionFact[] aversions;
        private PawnWorkEvidence(WorkCapabilityFact[] capabilities, WorkAversionFact[] aversions)
        { this.capabilities = capabilities; this.aversions = aversions; }
        public int CapabilityCount => capabilities.Length;
        public WorkCapabilityFact CapabilityAt(int index) => capabilities[index];
        public int AversionCount => aversions.Length;
        public WorkAversionFact AversionAt(int index) => aversions[index];

        public static PawnWorkEvidence Create(IReadOnlyList<WorkCapabilityFact> capabilities, IReadOnlyList<WorkAversionFact> aversions)
        {
            if (capabilities.Count == 0 && aversions.Count == 0) return Empty;
            return new PawnWorkEvidence(Normalize(capabilities, CompareCapability), Normalize(aversions, CompareAversion));
        }

        private static T[] Normalize<T>(IReadOnlyList<T> source, Comparison<T> compare)
        {
            var values = new List<T>(source.Count);
            for (int i = 0; i < source.Count; i++) values.Add(source[i]);
            values.Sort(compare);
            for (int i = values.Count - 1; i > 0; i--)
                if (compare(values[i - 1], values[i]) == 0) values.RemoveAt(i);
            return values.ToArray();
        }

        private static int CompareCapability(WorkCapabilityFact a, WorkCapabilityFact b)
        {
            int c = string.CompareOrdinal(a.GiverDefName, b.GiverDefName);
            if (c == 0) c = string.CompareOrdinal(a.RecipeDefName, b.RecipeDefName);
            if (c == 0) c = string.CompareOrdinal(a.SourcePackageId, b.SourcePackageId);
            return c != 0 ? c : string.CompareOrdinal(a.ReasonKey, b.ReasonKey);
        }
        private static int CompareAversion(WorkAversionFact a, WorkAversionFact b)
        {
            int c = string.CompareOrdinal(a.WorkTypeDefName, b.WorkTypeDefName);
            if (c == 0) c = string.CompareOrdinal(a.PreceptDefName, b.PreceptDefName);
            return c != 0 ? c : string.CompareOrdinal(a.SourcePackageId, b.SourcePackageId);
        }
        public bool ContentEquals(PawnWorkEvidence other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (!CapabilitiesEqual(other) || AversionCount != other.AversionCount) return false;
            for (int i = 0; i < aversions.Length; i++)
                if (CompareAversion(aversions[i], other.aversions[i]) != 0) return false;
            return true;
        }
        public bool CapabilitiesEqual(PawnWorkEvidence other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (CapabilityCount != other.CapabilityCount) return false;
            for (int i = 0; i < capabilities.Length; i++)
                if (CompareCapability(capabilities[i], other.capabilities[i]) != 0) return false;
            return true;
        }
        public bool Blocks(string giver, string? recipe = null)
        {
            for (int i = 0; i < capabilities.Length; i++)
                if (capabilities[i].GiverDefName == giver
                    && (capabilities[i].RecipeDefName == null || capabilities[i].RecipeDefName == recipe)) return true;
            return false;
        }
        public bool Opposes(string workType)
        {
            for (int i = 0; i < aversions.Length; i++)
                if (aversions[i].WorkTypeDefName == workType) return true;
            return false;
        }
    }
}
