using Implanner.Core;

namespace Implanner.UI
{
    internal sealed class OptionsSnapshot
    {
        /// Mod compatibility: modded bladder implants planned and
        /// installed side by side.
        internal bool AllowMultipleBladders;

        /// Mod compatibility: the same for hygiene enhancers.
        internal bool AllowMultipleHygieneEnhancers;

        /// Catalog: purchase-only implants listed in the picker.
        internal bool ShowPurchaseOnly;
    }

    /// Options tab presentation state. Owned by the dialog; dies with it.
    internal sealed class OptionsState
    {
        // Cache contract:
        // Owner: the Implanner dialog window.
        // Key: UiVersion.Current, store identity, and the Options store
        //   revision.
        // Value: an immutable snapshot of the mod compatibility and catalog
        //   flags so the tab never reads the live model.
        // Dependencies: the mod compatibility and catalog options (Options
        //   domain).
        // Refresh policy: immediate on the next Current read (from the
        //   dialog's WindowUpdate) after any key component moves.
        // Equality policy: rebuilds replace the snapshot.
        // Teardown: Release() drops the snapshot.
        private OptionsSnapshot? snapshot;
        private int uiStamp = -1;
        private ImplannerStore? owner;
        private int optionsStamp = -1;

        internal void Release()
        {
            snapshot = null;
            owner = null;
            uiStamp = -1;
            optionsStamp = -1;
        }

        /// Called from the dialog's WindowUpdate (never inside a render
        /// pass) so every pass of a frame draws one snapshot.
        internal OptionsSnapshot Current(ImplannerStore store)
        {
            if (snapshot == null
                || uiStamp != UiVersion.Current
                || !ReferenceEquals(owner, store)
                || optionsStamp != store.OptionsVersion)
            {
                PlannerModel model = store.Model;
                snapshot = new OptionsSnapshot
                {
                    AllowMultipleBladders = model.AllowMultipleBladders,
                    AllowMultipleHygieneEnhancers = model.AllowMultipleHygieneEnhancers,
                    ShowPurchaseOnly = model.ShowPurchaseOnly,
                };
                uiStamp = UiVersion.Current;
                owner = store;
                optionsStamp = store.OptionsVersion;
            }
            return snapshot;
        }
    }

    /// The Options tab's tooltip sources, resolved from the shared WrTips
    /// registry once per UI revision. Each carries the loaded implants it
    /// affects with their mods as the tip argument (definition set and
    /// language: both static within one UI revision).
    // Cache contract:
    // Owner: the Implanner dialog window.
    // Key: UiVersion.Current.
    // Value: WrTip references (the registry's own immutable-per-revision
    //   entries; their text gathers lazily on hover).
    // Dependencies: UiVersion.Current only — the WrTips registry clears
    //   its entries on that revision, so the holder must re-resolve then.
    // Refresh policy: immediate on the first Ensure after the stamp moves.
    // Equality policy: an unchanged stamp reuses every reference.
    // Teardown: Release() drops the references; the registry keeps its
    //   own lifecycle (WrTips.Reset on world teardown).
    internal sealed class OptionsTips
    {
        private int stamp = -1;

        internal WrTip AllowMultipleBladders = null!;
        internal WrTip AllowMultipleHygieneEnhancers = null!;
        internal WrTip ShowPurchaseOnly = null!;

        /// Called after the window observed the current UI metrics.
        internal void Ensure()
        {
            int current = UiVersion.Current;
            if (stamp == current) return;
            stamp = current;
            AllowMultipleBladders = WrTips.Key("IMP_OptAllowMultipleBladdersTip",
                ModCompatibility.TipLines(ImplantCompatibility.BladderImplants));
            AllowMultipleHygieneEnhancers = WrTips.Key(
                "IMP_OptAllowMultipleHygieneEnhancersTip",
                ModCompatibility.TipLines(ImplantCompatibility.HygieneEnhancerImplants));
            ShowPurchaseOnly = WrTips.Key("IMP_OptShowPurchaseOnlyTip",
                ModCompatibility.PurchaseOnlyTipLines());
        }

        internal void Release()
        {
            stamp = -1;
            AllowMultipleBladders = AllowMultipleHygieneEnhancers = null!;
            ShowPurchaseOnly = null!;
        }
    }
}
