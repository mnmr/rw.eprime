using System;
using System.Collections.Generic;
using System.Reflection;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Game-side quality facts for implants: which items carry a quality,
    /// what an implant does to its part at each quality, and the quality of
    /// an installed implant. Two mods define quality for implants, each read
    /// by type name without an assembly reference:
    /// - Quality Bionics Remastered adds CompQuality to replacement items
    ///   and a hediff comp holding the installed quality; the part's
    ///   efficiency is the base times a per-quality multiplier from its
    ///   settings (QualityBionicsManager.CalculateEfficiency).
    /// - Vanilla Genetics Expanded's hybrid implants carry CompQuality in
    ///   their defs; the installed quality selects a hediff stage (severity
    ///   0 Awful … 0.6 Legendary) whose partEfficiencyOffset applies.
    /// Elite Bionics Framework's body part HP bonus is read here too, for
    /// the implant tooltips. Called from reconcile passes and snapshot
    /// builders only, never while drawing.
    internal static class ImplantQualities
    {
        // Cache contract:
        // Owner: process/loaded assembly set.
        // Key: none (one set of handles).
        // Value: immutable reflection handles into Quality Bionics
        //   Remastered, Vanilla Genetics Expanded and Elite Bionics
        //   Framework, each null while its mod is absent; never a result.
        // Dependencies: the loaded assemblies (fixed for the session).
        //   Quality Bionics Remastered's multipliers are player settings, so
        //   they are read through its API on every call, never cached; only
        //   the last observed table is kept (qbrSeen) to detect a change.
        // Refresh policy: resolved once on first use. The multiplier table
        //   is compared on the approved 1020-tick boundary (CheckSettings,
        //   only while the mod is loaded); a change advances
        //   SettingsRevision and ExternalPawnFacts.Revision.
        // Equality policy: the same handles for the session.
        // Teardown: none needed; process lifetime, no world or game object
        //   retained.
        private static bool resolved;
        private static Func<HediffDef, bool>? qbrIsQualityBionic;
        private static Func<HediffDef, QualityCategory, float>? qbrEfficiency;
        private static Type? qbrComp;
        private static FieldInfo? qbrCompQuality;
        private static Type? vgeCompProps;
        private static Type? vgeComp;
        private static FieldInfo? vgeCompQuality;
        private static Type? ebfProps;
        private static FieldInfo? ebfLinear;
        private static FieldInfo? ebfScale;
        private static FieldInfo? qbrMultipliers;
        private static readonly float[] qbrSeen = new float[Qualities];
        private static bool qbrSeenValid;

        private const int Qualities = ImplantQuality.Highest + 1;

        /// Advances when Quality Bionics Remastered's efficiency multipliers
        /// change; caches of efficiency text key on it.
        internal static int SettingsRevision { get; private set; }

        /// Compares Quality Bionics Remastered's efficiency multiplier table
        /// with the last one seen. Its settings have no change notification,
        /// so this runs on the approved 1020-tick boundary; a no-op without
        /// the mod. Local presentation only: every evaluation already reads
        /// the multipliers live.
        internal static void CheckSettings()
        {
            Resolve();
            if (qbrMultipliers == null
                || !(qbrMultipliers.GetValue(null) is Dictionary<QualityCategory, float> table))
                return;
            bool changed = false;
            for (int q = 0; q < Qualities; q++)
            {
                table.TryGetValue((QualityCategory)q, out float value);
                if (qbrSeen[q] != value) changed = true;
                qbrSeen[q] = value;
            }
            if (changed && qbrSeenValid)
            {
                SettingsRevision = unchecked(SettingsRevision + 1);
                ExternalPawnFacts.Bump();
            }
            qbrSeenValid = true;
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            Type? manager = GenTypes.GetTypeInAnyAssembly(
                "QualityBionicsRemastered.Core.QualityBionicsManager");
            MethodInfo? isQuality = manager?.GetMethod("IsQualityBionic",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(HediffDef) }, null);
            MethodInfo? efficiency = manager?.GetMethod("CalculateEfficiency",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(HediffDef), typeof(QualityCategory) }, null);
            qbrComp = GenTypes.GetTypeInAnyAssembly(
                "QualityBionicsRemastered.Comps.HediffCompQualityBionics");
            qbrCompQuality = qbrComp?.GetField("quality");
            if (isQuality != null && efficiency != null && qbrCompQuality != null)
            {
                qbrIsQualityBionic = (Func<HediffDef, bool>)Delegate.CreateDelegate(
                    typeof(Func<HediffDef, bool>), isQuality);
                qbrEfficiency = (Func<HediffDef, QualityCategory, float>)
                    Delegate.CreateDelegate(
                        typeof(Func<HediffDef, QualityCategory, float>), efficiency);
                // Re-read on every check: its Reset button replaces the table.
                qbrMultipliers = GenTypes.GetTypeInAnyAssembly(
                        "QualityBionicsRemastered.Settings")
                    ?.GetField("qualityMultipliers",
                        BindingFlags.Public | BindingFlags.Static);
            }
            vgeCompProps = GenTypes.GetTypeInAnyAssembly(
                "GeneticRim.HediffCompProperties_ImplantQuality");
            vgeComp = GenTypes.GetTypeInAnyAssembly("GeneticRim.HediffCompImplantQuality");
            vgeCompQuality = vgeComp?.GetField("quality");
            ebfProps = GenTypes.GetTypeInAnyAssembly(
                "EBF.Hediffs.HediffCompProperties_MaxHPAdjust");
            ebfLinear = ebfProps?.GetField("linearAdjustment");
            ebfScale = ebfProps?.GetField("scaleAdjustment");
            // Seed the table the first readers see.
            CheckSettings();
        }

        /// Whether items of this kind carry a quality.
        internal static bool HasQuality(ThingDef? item) =>
            item != null && item.HasComp(typeof(CompQuality));

        /// The item's quality as 0..6, or 0 for an item without one.
        internal static int QualityOf(Thing thing) =>
            thing.TryGetQuality(out QualityCategory quality) ? (int)quality : 0;

        /// Whether any catalog implant's item carries a quality: the plans
        /// offer a minimum quality only then.
        internal static bool AnyInCatalog()
        {
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
                if (HasQuality(catalog[i].Def.spawnThingOnRemoved))
                    return true;
            return false;
        }

        /// What the implant does to its part's efficiency at each quality:
        /// a replacement's own efficiency, and for an implant that leaves
        /// the part natural, 100% plus any quality stage offset. Equal at
        /// every quality for items without one.
        internal static float[] AfterInstall(ImplantCatalogEntry entry)
        {
            Resolve();
            HediffDef def = entry.Def;
            var result = new float[Qualities];
            bool qbr = qbrIsQualityBionic != null && qbrIsQualityBionic(def);
            float base_ = entry.IsReplacement ? def.addedPartProps?.partEfficiency ?? 1f : 1f;
            bool staged = HasComp(def, vgeCompProps);
            for (int q = 0; q < Qualities; q++)
            {
                float value = qbr ? qbrEfficiency!(def, (QualityCategory)q) : base_;
                if (staged) value += StageOffset(def, q * 0.1f);
                result[q] = value;
            }
            return result;
        }

        /// The installed implant's efficiency as the game computes it,
        /// without damage: Quality Bionics Remastered's multiplier on the
        /// base, or the base, plus the current stage's offset (Vanilla
        /// Genetics Expanded's quality stages). An implant that is not an
        /// added part leaves the part at 100% plus its offset.
        internal static float InstalledEfficiency(Hediff hediff)
        {
            Resolve();
            HediffDef def = hediff.def;
            float value;
            if (!(hediff is Hediff_AddedPart))
                value = 1f;
            else if (qbrEfficiency != null && TryCompQuality(hediff, qbrComp,
                    qbrCompQuality, out int quality))
                value = qbrEfficiency(def, (QualityCategory)quality);
            else
                value = def.addedPartProps?.partEfficiency ?? 1f;
            return value + (hediff.CurStage?.partEfficiencyOffset ?? 0f);
        }

        /// The installed implant's item quality, or -1 without one.
        internal static int InstalledQuality(Hediff hediff)
        {
            Resolve();
            if (TryCompQuality(hediff, qbrComp, qbrCompQuality, out int quality))
                return quality;
            if (TryCompQuality(hediff, vgeComp, vgeCompQuality, out quality))
                return quality;
            return ImplantQuality.None;
        }

        /// Elite Bionics Framework's body part max HP adjustment the implant
        /// declares: a flat bonus and a scale (1.25 = 125%). False when the
        /// implant declares none or the framework is absent.
        internal static bool TryMaxHpAdjustment(HediffDef def, out int linear,
            out float scale)
        {
            Resolve();
            linear = 0;
            scale = 1f;
            if (ebfProps == null || ebfLinear == null || ebfScale == null
                || def.comps == null)
                return false;
            for (int i = 0; i < def.comps.Count; i++)
            {
                HediffCompProperties props = def.comps[i];
                if (props == null || !ebfProps.IsInstanceOfType(props)) continue;
                linear = (int)ebfLinear.GetValue(props);
                float adjustment = (float)ebfScale.GetValue(props);
                // The framework ignores factors at or below zero.
                scale = adjustment + 1f > 0f ? adjustment + 1f : 1f;
                return linear != 0 || scale != 1f;
            }
            return false;
        }

        private static bool HasComp(HediffDef def, Type? propsType)
        {
            if (propsType == null || def.comps == null) return false;
            for (int i = 0; i < def.comps.Count; i++)
                if (def.comps[i] != null && propsType.IsInstanceOfType(def.comps[i]))
                    return true;
            return false;
        }

        private static float StageOffset(HediffDef def, float severity) =>
            def.stages.NullOrEmpty()
                ? 0f
                : def.stages[def.StageAtSeverity(severity)].partEfficiencyOffset;

        private static bool TryCompQuality(Hediff hediff, Type? compType,
            FieldInfo? field, out int quality)
        {
            quality = ImplantQuality.None;
            if (compType == null || field == null
                || !(hediff is HediffWithComps withComps))
                return false;
            List<HediffComp> comps = withComps.comps;
            for (int i = 0; i < comps.Count; i++)
                if (compType.IsInstanceOfType(comps[i]))
                {
                    quality = (int)(QualityCategory)field.GetValue(comps[i]);
                    return true;
                }
            return false;
        }
    }
}
