using System;
using System.Collections.Generic;
using Implanner.Core;
using Verse;

namespace Implanner.UI
{
    /// One resource whose keep-in-stock reserve the player can configure:
    /// a baseline-reserve resource or a fixed ingredient of any craftable
    /// implant item's recipe.
    internal sealed class ReserveRow
    {
        internal string DefName = "";
        internal string Label = "";
        internal ThingDef? IconDef;
        internal int Amount;

        /// Edit-buffer key ("h|" prefix keeps implant reserves distinct from
        /// resource reserves) and GUI control name, precomputed here so the
        /// steady render pass never concatenates strings.
        internal string BufferKey = "";
        internal string FieldName = "";
    }

    internal sealed class AutomationSnapshot
    {
        internal bool AutomationPaused;
        /// Display index of the iteration segment: 0 = tier iteration (the
        /// default, listed first), 1 = colonist.
        internal int IterationDisplayIndex;
        internal bool AutoDoctorFloor;
        internal bool CountHospitalized;
        internal bool AutoProduction;
        internal bool OnlyIdleBenches;
        internal bool AllowIntermediaries;
        internal string ManualFloorText = "";
        internal string SurgeryConcurrencyText = "";
        internal string ConcurrencyText = "";
        internal string ProductionSkillText = "";
        internal List<ReserveRow> Reserves = new List<ReserveRow>();

        /// Implants held back from surgery automation for manual use;
        /// DefName is the implant HediffDef name.
        internal List<ReserveRow> ImplantReserves = new List<ReserveRow>();
    }

    /// Automation tab presentation state. Owned by the dialog; dies with it.
    internal sealed class AutomationState
    {
        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: UiVersion.Current, store identity, and the Options, Surgery
        //   and Production store revisions.
        // Value: an immutable snapshot (the master switch, iteration,
        //   doctor-floor and hospitalized flags, manual-floor text,
        //   production flags, concurrency and skill text, resource-reserve
        //   rows derived from the baseline table and craftable implant
        //   recipes, and implant reservation rows).
        // Dependencies: the master switch, iteration, doctor floor and
        //   hospitalized counting (Options), implant reservations
        //   (Surgery), production options and reserves (Production), and
        //   the implant catalog + language for the row sets (via UiVersion).
        // Refresh policy: immediate on the next Current read (from the
        //   dialog's WindowUpdate) after any key component moves.
        // Equality policy: rebuilds replace the snapshot.
        // Teardown: Release() drops the snapshot and the edit buffers.
        private AutomationSnapshot? snapshot;
        private int uiStamp = -1;
        private ImplannerStore? owner;
        private int optionsStamp = -1;
        private int surgeryStamp = -1;
        private int productionStamp = -1;

        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: the automation snapshot identity.
        // Value: the reserve fields' edit buffers, parallel to the
        //   snapshot's Reserves and ImplantReserves rows (mutable session
        //   state: the draw pass writes one slot when the player types).
        // Dependencies: the snapshot rows (identity) and the durable
        //   per-key buffer dictionary they are seeded from.
        // Refresh policy: rebuilt with the snapshot; a buffer is carried
        //   over while the model's amount is the one it was seeded against
        //   (rebuilds for unrelated domains, partial typing, and a synced
        //   edit still in flight keep the text) and resets to the amount
        //   once the model's amount actually moved.
        // Equality policy: none (the arrays follow the snapshot).
        // Teardown: Release() clears both arrays and the dictionaries.
        internal string[] ResourceBuffers = Array.Empty<string>();
        internal string[] ImplantBuffers = Array.Empty<string>();

        /// Durable session edit buffers, keyed by ReserveRow.BufferKey, so
        /// in-progress typing survives a snapshot rebuild; written only when
        /// a field's text changes.
        internal readonly Dictionary<string, string> ReserveBuffers =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// The model amount each buffer was last seeded against, so a
        /// rebuild can tell "the model moved" from "the text disagrees".
        private readonly Dictionary<string, int> seededAmounts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// GUI control names of the reserve fields in draw order, rebuilt
        /// each pass; the dialog's Tab handler cycles focus through them.
        internal readonly List<string> ReserveFieldNames = new List<string>();

        internal void Release()
        {
            snapshot = null;
            owner = null;
            uiStamp = -1;
            optionsStamp = -1;
            surgeryStamp = -1;
            productionStamp = -1;
            ResourceBuffers = Array.Empty<string>();
            ImplantBuffers = Array.Empty<string>();
            ReserveBuffers.Clear();
            seededAmounts.Clear();
            ReserveFieldNames.Clear();
        }

        /// Called from the dialog's WindowUpdate (never inside a render
        /// pass) so every pass of a frame draws one snapshot.
        internal AutomationSnapshot Current(ImplannerStore store)
        {
            if (snapshot == null
                || uiStamp != UiVersion.Current
                || !ReferenceEquals(owner, store)
                || optionsStamp != store.OptionsVersion
                || surgeryStamp != store.SurgeryVersion
                || productionStamp != store.ProductionVersion)
            {
                snapshot = Build(store);
                PruneBuffers(snapshot);
                ResourceBuffers = SeedBuffers(snapshot.Reserves);
                ImplantBuffers = SeedBuffers(snapshot.ImplantReserves);
                uiStamp = UiVersion.Current;
                owner = store;
                optionsStamp = store.OptionsVersion;
                surgeryStamp = store.SurgeryVersion;
                productionStamp = store.ProductionVersion;
            }
            return snapshot;
        }

        /// Records a field edit: the parallel slot the draw pass reads and
        /// the durable dictionary a rebuild reseeds from.
        internal void StoreBuffer(string[] buffers, int index,
            ReserveRow row, string buffer)
        {
            buffers[index] = buffer;
            ReserveBuffers[row.BufferKey] = buffer;
        }

        /// One buffer per row: the retained typing while the model's amount
        /// is still the one it was seeded against (the rebuild came from
        /// elsewhere, or the player's own edit has not landed yet),
        /// otherwise the amount's digits, re-seeded against the new amount.
        private string[] SeedBuffers(List<ReserveRow> rows)
        {
            var buffers = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                ReserveRow row = rows[i];
                if (ReserveBuffers.TryGetValue(row.BufferKey, out string retained)
                    && seededAmounts.TryGetValue(row.BufferKey, out int seeded)
                    && seeded == row.Amount)
                {
                    buffers[i] = retained;
                    continue;
                }
                buffers[i] = row.Amount.ToStringCached();
                seededAmounts[row.BufferKey] = row.Amount;
                ReserveBuffers.Remove(row.BufferKey);
            }
            return buffers;
        }

        /// Deleting a reserve row must not leave its edit buffer behind: a
        /// later re-add would resurrect the stale text over the model's
        /// authoritative amount. Buffers of rows still present are kept —
        /// they may hold in-progress typing.
        private void PruneBuffers(AutomationSnapshot current)
        {
            if (ReserveBuffers.Count == 0) return;
            var live = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < current.Reserves.Count; i++)
                live.Add(current.Reserves[i].BufferKey);
            for (int i = 0; i < current.ImplantReserves.Count; i++)
                live.Add(current.ImplantReserves[i].BufferKey);
            List<string>? dead = null;
            foreach (string key in ReserveBuffers.Keys)
                if (!live.Contains(key))
                    (dead ??= new List<string>()).Add(key);
            if (dead == null) return;
            for (int i = 0; i < dead.Count; i++)
            {
                ReserveBuffers.Remove(dead[i]);
                seededAmounts.Remove(dead[i]);
            }
        }

        private static AutomationSnapshot Build(ImplannerStore store)
        {
            var result = new AutomationSnapshot();
            PlannerModel model = store.Model;
            result.AutomationPaused = model.AutomationPaused;
            result.IterationDisplayIndex =
                model.Iteration == IterationStrategy.ImplantTier ? 0 : 1;
            result.AutoDoctorFloor = model.AutoDoctorFloor;
            result.CountHospitalized = model.CountHospitalized;
            result.AutoProduction = model.AutoProduction;
            result.OnlyIdleBenches = model.OnlyIdleBenches;
            result.AllowIntermediaries = model.AllowIntermediaries;
            result.ManualFloorText = model.ManualDoctorFloor.ToStringCached();
            result.SurgeryConcurrencyText = model.SurgeryConcurrency.ToStringCached();
            result.ConcurrencyText = model.ProductionConcurrency.ToStringCached();
            result.ProductionSkillText = model.ProductionSkill.ToStringCached();
            BuildReserves(model, result.Reserves);
            BuildImplantReserves(model, result.ImplantReserves);
            return result;
        }

        /// The configured implant reservations, resolved for drawing and
        /// sorted by label.
        private static void BuildImplantReserves(
            PlannerModel model, List<ReserveRow> rows)
        {
            foreach (KeyValuePair<string, int> pair in model.ImplantReserves)
            {
                ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(pair.Key);
                rows.Add(new ReserveRow
                {
                    DefName = pair.Key,
                    Label = entry?.Label ?? pair.Key,
                    IconDef = entry?.Def.spawnThingOnRemoved,
                    Amount = pair.Value,
                    BufferKey = "h|" + pair.Key,
                    FieldName = "IMP_ImplantReserve_" + pair.Key,
                });
            }
            rows.Sort(ByLabelThenDef);
        }

        /// Shared row order for both reserve lists.
        private static readonly Comparison<ReserveRow> ByLabelThenDef =
            static (a, b) =>
            {
                int label = string.Compare(a.Label, b.Label,
                    StringComparison.OrdinalIgnoreCase);
                return label != 0
                    ? label
                    : string.CompareOrdinal(a.DefName, b.DefName);
            };

        /// The reserve rows: the baseline-reserve resources (so gold and kin
        /// are always configurable) plus every fixed ingredient of a
        /// craftable implant item's production recipe, deduplicated and
        /// sorted by label. Missing defs (DLC or mod content that is not
        /// loaded) are skipped, so the list adapts to the active game.
        private static void BuildReserves(PlannerModel model, List<ReserveRow> rows)
        {
            var seen = new HashSet<ThingDef>();
            foreach (KeyValuePair<string, int> pair in
                PlannerModel.DefaultResourceReserves)
            {
                ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(pair.Key);
                if (def != null && seen.Add(def))
                    AddReserveRow(model, rows, def);
            }
            IReadOnlyList<ImplantCatalogEntry> implants = Catalogs.Implants();
            for (int i = 0; i < implants.Count; i++)
            {
                ThingDef? item = implants[i].Def.spawnThingOnRemoved;
                if (item == null) continue;
                RecipeDef? recipe = PlannerProduction.ProductionRecipeFor(item);
                List<IngredientCount>? ingredients = recipe?.ingredients;
                if (ingredients == null) continue;
                for (int g = 0; g < ingredients.Count; g++)
                {
                    if (!ingredients[g].IsFixedIngredient) continue;
                    ThingDef def = ingredients[g].FixedIngredient;
                    if (def == null || !seen.Add(def)) continue;
                    AddReserveRow(model, rows, def);
                }
            }
            rows.Sort(ByLabelThenDef);
        }

        private static void AddReserveRow(
            PlannerModel model, List<ReserveRow> rows, ThingDef def)
        {
            rows.Add(new ReserveRow
            {
                DefName = def.defName,
                Label = def.LabelCap.ToString(),
                IconDef = def,
                Amount = model.ResourceReserveOf(def.defName),
                BufferKey = def.defName,
                FieldName = "IMP_Reserve_" + def.defName,
            });
        }
    }
}
