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
    ///   settings (QualityBionicsManager.CalculateEfficiency). Those
    ///   settings are per player, so the simulation uses the store's synced
    ///   table (PlannerModel.QualityMultipliers) and this client follows
    ///   its own settings into it through a synced command.
    /// - Vanilla Genetics Expanded's hybrid implants carry CompQuality in
    ///   their defs; the installed quality selects a hediff stage (severity
    ///   0 Awful … 0.6 Legendary) whose partEfficiencyOffset applies.
    /// Elite Bionics Framework's body part HP bonus is read here too, for
    /// the implant tooltips. Called from reconcile passes and snapshot
    /// builders only, never while drawing.
    internal static class ImplantQualities
    {
        // Cache contract:
        // Owner: process/loaded assembly set (handles); the store (table).
        // Key: none (one set of handles, one published table).
        // Value: immutable reflection handles into Quality Bionics
        //   Remastered, Vanilla Genetics Expanded and Elite Bionics
        //   Framework, each null while its mod is absent; never a result.
        //   Plus the store's synced multiplier table as published
        //   (PublishTable: the model's own immutable array, never copied).
        // Dependencies: the loaded assemblies (fixed for the session).
        //   Quality Bionics Remastered's multipliers are PER-PLAYER settings:
        //   the simulation never reads them. It reads the store's synced copy
        //   (PlannerModel.QualityMultipliers, Options domain), which this
        //   client follows: the local table is compared with the last one
        //   seen (qbrSeen) on the approved 1020-tick boundary
        //   (CheckSettings) and a change, or a load/new game (FollowPending),
        //   issues the synced SetQualityMultipliers command from the frame
        //   path (Follow). A published table that differs from the previous
        //   advances SettingsRevision and ExternalPawnFacts.Revision.
        // Refresh policy: handles resolved once on first use; the table on
        //   every Options publication of the store.
        // Equality policy: the same handles for the session; equal table
        //   contents keep SettingsRevision.
        // Teardown: Reset on world teardown drops the published table.
        private static bool resolved;
        private static Func<HediffDef, bool>? qbrIsQualityBionic;
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
        private static IReadOnlyList<float>? synced;

        private const int Qualities = ImplantQuality.Highest + 1;

        /// Advances when the multiplier table the simulation uses changes;
        /// caches of efficiency text key on it.
        internal static int SettingsRevision { get; private set; }

        /// Set when a game loads or starts, and when CheckSettings sees the
        /// local table change; the next frame update while playing follows
        /// the local settings (Follow). Cleared on world teardown.
        internal static bool FollowPending;

        /// The store publishes its synced table here on construction, load
        /// and every Options change; the simulation reads only this.
        internal static void PublishTable(IReadOnlyList<float> table)
        {
            bool changed = synced != null && !QualityMultipliers.Same(synced, table);
            synced = table;
            if (!changed) return;
            SettingsRevision = unchecked(SettingsRevision + 1);
            ExternalPawnFacts.Bump();
        }

        internal static void Reset()
        {
            synced = null;
            FollowPending = false;
        }

        /// Compares Quality Bionics Remastered's local efficiency multiplier
        /// table with the last one seen. Its settings have no change
        /// notification, so this runs on the approved 1020-tick boundary; a
        /// no-op without the mod. A change only flags Follow for the frame
        /// path: Multiplayer syncs commands issued from the interface, never
        /// from a tick.
        internal static void CheckSettings()
        {
            Resolve();
            if (!ReadLocalTable(qbrSeen, out bool changed)) return;
            if (changed && qbrSeenValid) FollowPending = true;
            qbrSeenValid = true;
        }

        /// Implanner follows this client's Quality Bionics Remastered
        /// settings: when the local table differs from the synced one, the
        /// synced option is set through the ordinary synced command, so
        /// automation only ever reads synced state (mirrors
        /// AndroidTiers.FollowSetting). In multiplayer, players whose
        /// settings differ leave the last write in effect for everyone. A
        /// no-op without the mod or a game.
        internal static void Follow()
        {
            if (Current.ProgramState != ProgramState.Playing) return;
            Resolve();
            ImplannerStore? store = ImplannerStore.Current;
            if (store == null || !ReadLocalTable(qbrSeen, out _)) return;
            qbrSeenValid = true;
            if (QualityMultipliers.Same(store.Model.QualityMultipliers, qbrSeen)) return;
            PlannerCommands.SetQualityMultipliers(QualityMultipliers.Encode(qbrSeen));
        }

        /// Reads the mod's live table into the buffer; false without the mod.
        private static bool ReadLocalTable(float[] into, out bool changed)
        {
            changed = false;
            if (qbrMultipliers == null
                || !(qbrMultipliers.GetValue(null) is Dictionary<QualityCategory, float> table))
                return false;
            for (int q = 0; q < Qualities; q++)
            {
                table.TryGetValue((QualityCategory)q, out float value);
                if (into[q] != value) changed = true;
                into[q] = value;
            }
            return true;
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
            qbrComp = GenTypes.GetTypeInAnyAssembly(
                "QualityBionicsRemastered.Comps.HediffCompQualityBionics");
            qbrCompQuality = qbrComp?.GetField("quality");
            // Re-read on every check: its Reset button replaces the table.
            qbrMultipliers = GenTypes.GetTypeInAnyAssembly(
                    "QualityBionicsRemastered.Settings")
                ?.GetField("qualityMultipliers",
                    BindingFlags.Public | BindingFlags.Static);
            if (isQuality != null && qbrCompQuality != null && qbrMultipliers != null)
                qbrIsQualityBionic = (Func<HediffDef, bool>)Delegate.CreateDelegate(
                    typeof(Func<HediffDef, bool>), isQuality);
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
                if (HasQuality(catalog[i].Item))
                    return true;
            return false;
        }

        /// What the implant does to its part's efficiency at each quality:
        /// a replacement's own efficiency (times the synced Quality Bionics
        /// Remastered multiplier for a quality bionic, as that mod's
        /// CalculateEfficiency does with its local one), and for an implant
        /// that leaves the part natural, 100% plus any quality stage offset.
        /// Equal at every quality for items without one. Without a store
        /// (no game) the local table serves presentation.
        internal static float[] AfterInstall(ImplantCatalogEntry entry)
            => AfterInstall(entry.Def, entry.IsReplacement);

        internal static float[] AfterInstall(HediffDef def, bool replacement)
        {
            Resolve();
            float base_ = replacement ? def.addedPartProps?.partEfficiency ?? 1f : 1f;
            float[] result;
            if (qbrIsQualityBionic != null && qbrIsQualityBionic(def))
            {
                IReadOnlyList<float>? table = synced;
                if (table == null && ReadLocalTable(qbrSeen, out _)) table = qbrSeen;
                result = ImplantQuality.Scaled(
                    def.addedPartProps?.partEfficiency ?? 1f, table ?? QualityMultipliers.Default);
            }
            else
            {
                result = new float[Qualities];
                for (int q = 0; q < Qualities; q++) result[q] = base_;
            }
            if (HasComp(def, vgeCompProps))
                for (int q = 0; q < Qualities; q++)
                    result[q] += StageOffset(def, q * 0.1f);
            return result;
        }

        /// A recipe which creates a hediff without an implant ingredient
        /// does not inherit the old hediff's quality. QBR constructs Normal
        /// and only transfers quality from a matching ingredient. For other
        /// quality mechanisms require the worst possible result to be safe.
        internal static float AfterInstallWithoutItem(HediffDef def, bool replacement)
        {
            float[] values = AfterInstall(def, replacement);
            if (qbrIsQualityBionic != null && qbrIsQualityBionic(def) && !HasComp(def, vgeCompProps))
                return values[(int)QualityCategory.Normal];
            float worst = values[0];
            for (int i = 1; i < values.Length; i++) worst = Math.Min(worst, values[i]);
            return worst;
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
            else if (qbrIsQualityBionic != null && qbrIsQualityBionic(def)
                && TryCompQuality(hediff, qbrComp, qbrCompQuality, out int quality))
                value = (def.addedPartProps?.partEfficiency ?? 1f)
                    * (synced ?? QualityMultipliers.Default)[ImplantQuality.Clamp(quality)];
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
                linear = (int)ebfLinear.GetValue(props)!;
                float adjustment = (float)ebfScale.GetValue(props)!;
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
                    quality = (int)(QualityCategory)field.GetValue(comps[i])!;
                    return true;
                }
            return false;
        }
    }
}
