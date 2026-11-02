using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace Implanner
{
    /// Hediffs a pawn's genes grant. Vanilla genes grant none; Vanilla
    /// Expanded Framework's GeneExtension adds hediffs to body parts
    /// (Vanilla Races Expanded - Saurid's claws on both hands) or to the
    /// whole body when the gene is added. Such a hediff is not something
    /// a surgery destroys for good: the gene stays with the pawn, and the
    /// hediff goes wherever the natural part goes (owner, 2026-11-02;
    /// player report: planned bionic arms were never installed on saurids).
    /// Reconcile pass and details builder only.
    internal static class GeneHediffs
    {
        // Cache contract:
        // Owner: process.
        // Key: GeneDef.
        // Value: immutable HediffDef[] the gene grants (empty for most).
        // Dependencies: the loaded definition and assembly set (static per
        //   session); the extension type and its fields are resolved once.
        // Refresh policy: built on the first query per gene.
        // Equality policy: never changes within a session.
        // Teardown: Release (Catalogs.Release on world teardown) drops the
        //   table; defs are never mutated or owned.
        private static readonly HediffDef[] None = new HediffDef[0];
        private static Dictionary<GeneDef, HediffDef[]>? granted;
        private static bool resolved;
        private static Type? extension;
        private static FieldInfo? partsField, wholeField, hediffField;

        internal static void Release() => granted = null;

        /// Whether one of the pawn's active genes grants the hediff.
        internal static bool IsGranted(Pawn pawn, HediffDef def)
        {
            List<Gene>? genes = pawn.genes?.GenesListForReading;
            if (genes == null) return false;
            for (int i = 0; i < genes.Count; i++)
            {
                if (!genes[i].Active) continue;
                HediffDef[] hediffs = GrantedBy(genes[i].def);
                for (int k = 0; k < hediffs.Length; k++)
                    if (hediffs[k] == def) return true;
            }
            return false;
        }

        private static HediffDef[] GrantedBy(GeneDef gene)
        {
            granted ??= new Dictionary<GeneDef, HediffDef[]>();
            if (granted.TryGetValue(gene, out HediffDef[]? cached)) return cached;
            HediffDef[] result = Build(gene);
            granted[gene] = result;
            return result;
        }

        private static HediffDef[] Build(GeneDef gene)
        {
            Resolve();
            if (extension == null || gene.modExtensions == null) return None;
            List<HediffDef>? found = null;
            for (int i = 0; i < gene.modExtensions.Count; i++)
            {
                DefModExtension ext = gene.modExtensions[i];
                if (ext == null || ext.GetType() != extension) continue;
                if (wholeField?.GetValue(ext) is HediffDef whole)
                    (found ??= new List<HediffDef>()).Add(whole);
                if (partsField?.GetValue(ext) is IEnumerable parts)
                    foreach (object part in parts)
                        if (part != null && hediffField?.GetValue(part) is HediffDef def)
                            (found ??= new List<HediffDef>()).Add(def);
            }
            return found == null ? None : found.ToArray();
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            extension = GenTypes.GetTypeInAnyAssembly("VEF.Genes.GeneExtension")
                ?? GenTypes.GetTypeInAnyAssembly("VanillaGenesExpanded.GeneExtension");
            if (extension == null) return;
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            partsField = extension.GetField("hediffsToBodyParts", Any);
            wholeField = extension.GetField("hediffToWholeBody", Any);
            Type? partType = partsField?.FieldType.IsGenericType == true
                ? partsField.FieldType.GetGenericArguments()[0] : null;
            hediffField = partType?.GetField("hediff", Any);
            if (wholeField != null && wholeField.FieldType != typeof(HediffDef)) wholeField = null;
        }
    }
}
