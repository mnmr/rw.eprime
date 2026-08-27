using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs
{
    /// Captured from a bill at pause/pooling time (spec §4): style identity for
    /// share matching and one-shot bill construction, plus store mode and an
    /// ingredient-filter copy. Survives source-bill deletion.
    public class StyleSnapshot : IExposable
    {
        public Precept_ThingStyle? precept;
        public ThingStyleDef? style;
        public bool globalStyle = true;
        public int? graphicIndexOverride;
        public BillStoreModeDef? storeMode;
        public ISlotGroup? storeGroup;
        public ThingFilter? ingredientFilter;
        public bool known;

        public static StyleSnapshot From(Bill_Production bill)
        {
            var s = new StyleSnapshot
            {
                precept = bill.precept,
                style = bill.style,
                globalStyle = bill.globalStyle,
                graphicIndexOverride = bill.graphicIndexOverride,
                storeMode = bill.GetStoreMode(),
                storeGroup = bill.GetSlotGroup(),
                known = true,
            };
            s.ingredientFilter = new ThingFilter();
            s.ingredientFilter.CopyAllowancesFrom(bill.ingredientFilter);
            return s;
        }

        public StyleKey ToStyleKey()
            => known
                ? new StyleKey(precept != null ? PreceptIds.IdOf(precept) : null,
                    style?.defName, globalStyle, graphicIndexOverride)
                : StyleKey.Unknown;

        public static StyleKey KeyOf(Bill_Production bill)
            => new StyleKey(bill.precept != null ? PreceptIds.IdOf(bill.precept) : null,
                bill.style?.defName, bill.globalStyle, bill.graphicIndexOverride);

        public void ExposeData()
        {
            Scribe_References.Look(ref precept, "precept");
            Scribe_Defs.Look(ref style, "style");
            Scribe_Values.Look(ref globalStyle, "globalStyle", true);
            Scribe_Values.Look(ref graphicIndexOverride, "graphicIndexOverride");
            Scribe_Defs.Look(ref storeMode, "storeMode");
            if (Scribe.mode == LoadSaveMode.Saving)
                SaveSlotReferencable(storeGroup, "storeGroup");
            else if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
                LoadSlotReferencable(ref storeGroup, "storeGroup");
            Scribe_Deep.Look(ref ingredientFilter, "ingredientFilter");
            Scribe_Values.Look(ref known, "known", false);
        }

        // Mirrors vanilla Bill_Production.SaveSlotReferencable / LoadSlotReferencable.
        private static void SaveSlotReferencable(ISlotGroup? slot, string key)
        {
            ILoadReferenceable? refee = null;
            if (slot is ILoadReferenceable lr)
                refee = lr;
            else if (slot is SlotGroup sg && sg.parent is ILoadReferenceable sgParent)
                refee = sgParent;
            Scribe_References.Look(ref refee, key);
        }

        private static void LoadSlotReferencable(ref ISlotGroup? slot, string key)
        {
            ILoadReferenceable? refee = null;
            Scribe_References.Look(ref refee, key);
            if (refee is ISlotGroup slotGroup)
                slot = slotGroup;
            else if (refee is ISlotGroupParent slotGroupParent)
                slot = slotGroupParent.GetSlotGroup();
        }
    }
}
