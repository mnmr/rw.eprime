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
        internal string ManualFloorText = "";
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
        // Value: an immutable snapshot (manual-floor text, production
        //   concurrency and skill text, resource-reserve rows derived from
        //   the baseline table and craftable implant recipes, and implant
        //   reservation rows).
        // Dependencies: the manual floor and iteration (Options), implant
        //   reservations (Surgery), production options and reserves
        //   (Production), and the implant catalog + language for the row
        //   sets (via UiVersion).
        // Refresh policy: immediate on the next Repaint read after any key
        //   component moves.
        // Equality policy: rebuilds replace the snapshot.
        // Teardown: Release() drops the snapshot and the edit buffers.
        private AutomationSnapshot? snapshot;
        private int uiStamp = -1;
        private ImplannerStore? owner;
        private int optionsStamp = -1;
        private int surgeryStamp = -1;
        private int productionStamp = -1;

        /// Session edit buffers for the reserve numeric fields, keyed by
        /// resource def name (Widgets.TextFieldNumeric contract).
        internal readonly Dictionary<string, string> ReserveBuffers =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// GUI control names of the reserve fields in draw order, rebuilt
        /// each frame; the dialog's Tab handler cycles focus through them.
        internal readonly List<string> ReserveFieldNames = new List<string>();

        internal void Release()
        {
            snapshot = null;
            owner = null;
            uiStamp = -1;
            optionsStamp = -1;
            surgeryStamp = -1;
            productionStamp = -1;
            ReserveBuffers.Clear();
            ReserveFieldNames.Clear();
        }

        /// Called on the Repaint pass only.
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
                uiStamp = UiVersion.Current;
                owner = store;
                optionsStamp = store.OptionsVersion;
                surgeryStamp = store.SurgeryVersion;
                productionStamp = store.ProductionVersion;
            }
            return snapshot;
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
                ReserveBuffers.Remove(dead[i]);
        }

        private static AutomationSnapshot Build(ImplannerStore store)
        {
            var result = new AutomationSnapshot();
            PlannerModel model = store.Model;
            result.ManualFloorText = model.ManualDoctorFloor.ToStringCached();
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
