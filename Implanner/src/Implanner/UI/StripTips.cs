using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace Implanner.UI
{
    internal enum StripTipKind
    {
        Production = 0,
        Surgery = 1,
    }

    /// The colony strip's column tooltips: a per-implant breakdown table
    /// (dispatch order) under the column's title and percent, plus the
    /// facts the strip has no room for. Content is assembled only when a
    /// hover session opens, from the overview snapshot the strip is
    /// drawing; the steady offer path stores one reference.
    // Cache contract:
    // Owner: the Implanner dialog window (one source per column).
    // Key: overview DATA identity (OverviewData, not the ordered snapshot:
    //   a sort or group-by click changes nothing the tooltip shows).
    // Value: one immutable StructuredTip (its TipModel keeps its own
    //   geometry cache behind UiVersion).
    // Dependencies: the overview data, which already folds
    //   UiVersion.Current (language and metrics), store identity and
    //   Version, pawn facts, locations, and the grouping selection.
    // Refresh policy: rebuilt on the next display session after the
    //   data reference changes; an open session stays frozen by the
    //   presenter.
    // Equality policy: the same data returns the same tip identity.
    // Teardown: Release() on window close drops the data and tip.
    internal sealed class StripTipSource : IStructuredTipSource
    {
        private readonly StripTipKind kind;
        private readonly string stableKey;
        private OverviewData? offered;
        private OverviewData? builtFor;
        private StructuredTip? tip;

        internal StripTipSource(StripTipKind kind)
        {
            this.kind = kind;
            stableKey = kind == StripTipKind.Production
                ? "IMP_StripProductionTip"
                : "IMP_StripSurgeryTip";
        }

        /// Called while drawing the strip; a reference store, no work.
        internal void Offer(OverviewData data) => offered = data;

        internal void Release()
        {
            offered = null;
            builtFor = null;
            tip = null;
        }

        string IStructuredTipSource.StableKey => stableKey;

        StructuredTip? IStructuredTipSource.Resolve()
        {
            OverviewData? snapshot = offered;
            if (snapshot == null) return null;
            if (!ReferenceEquals(builtFor, snapshot))
            {
                TipModel? model = kind == StripTipKind.Production
                    ? BuildProduction(snapshot)
                    : BuildSurgery(snapshot);
                tip = model == null ? null : new StructuredTip(stableKey, model);
                builtFor = snapshot;
            }
            return tip;
        }

        private static readonly Color HeaderColor = TipText.DimColor;

        private static TipModel? BuildProduction(OverviewData snapshot)
        {
            IReadOnlyList<ProductionTipRow> rows = snapshot.ProductionRows;
            if (rows.Count == 0) return null;
            bool heldBack = snapshot.HeldBackShown;
            bool status = snapshot.ProductionStatusShown;

            var headers = new List<string>(9)
            {
                "IMP_TipColTier".Translate(),
                "IMP_TipColItem".Translate(),
                "IMP_TipColNeeded".Translate(),
                "IMP_TipColFree".Translate(),
                "IMP_TipColReserved".Translate(),
            };
            var alignments = new List<TipColumnAlignment>(9)
            {
                TipColumnAlignment.Left,
                TipColumnAlignment.Left,
                TipColumnAlignment.Right,
                TipColumnAlignment.Right,
                TipColumnAlignment.Right,
            };
            if (heldBack)
            {
                headers.Add("IMP_TipColHeldBack".Translate());
                alignments.Add(TipColumnAlignment.Right);
            }
            headers.Add("IMP_TipColQueued".Translate());
            alignments.Add(TipColumnAlignment.Right);
            headers.Add("IMP_TipColCrafting".Translate());
            alignments.Add(TipColumnAlignment.Right);
            if (status)
            {
                headers.Add("IMP_TipColStatus".Translate());
                alignments.Add(TipColumnAlignment.Left);
            }

            var model = new TipModel
            {
                Title = "IMP_TipProduction".Translate(),
                Badge = snapshot.ProductionPercent.ToStringCached() + "%",
            };
            TipSection table = model.AddSection();
            table.Columns(headers, HeaderColor, alignments: alignments);
            table.Rule();
            for (int i = 0; i < rows.Count; i++)
            {
                ProductionTipRow row = rows[i];
                var cells = new List<string>(headers.Count)
                {
                    TierStars(row.Tier),
                    row.Label,
                    row.Needed.ToStringCached(),
                    row.Free.ToStringCached(),
                    row.Reserved.ToStringCached(),
                };
                if (heldBack) cells.Add(row.HeldBack.ToStringCached());
                cells.Add(row.Queued.ToStringCached());
                cells.Add(row.Crafting.ToStringCached());
                if (status) cells.Add(StatusText(row));
                Color? color = row.Queued <= 0
                    ? TipText.DimColor
                    : row.Crafting > 0
                        ? PlannerStyle.ActiveText
                        : (Color?)null;
                table.Columns(cells, color, alignments: alignments);
            }

            if (status)
                model.AddSection().Text("IMP_TipBenchesInUse".Translate(
                    snapshot.BenchesInUse), dim: true);
            return model;
        }

        private static string StatusText(ProductionTipRow row)
        {
            switch (row.Status)
            {
                case ProductionRowStatus.Covered:
                    return "IMP_TipCovered".Translate();
                case ProductionRowStatus.Making:
                    return "IMP_TipMaking".Translate();
                case ProductionRowStatus.MakingMaterials:
                    return "IMP_TipMakingMaterials".Translate();
                case ProductionRowStatus.NoRecipe:
                    return "IMP_TipNoRecipe".Translate();
                case ProductionRowStatus.WaitingFor:
                    return "IMP_StripWaiting".Translate(row.StatusArg)
                        .CapitalizeFirst();
                case ProductionRowStatus.WaitingBench:
                    return "IMP_StripWaitingBench".Translate();
                default:
                    return "";
            }
        }

        private static readonly TipColumnAlignment[] SurgeryAlignments =
        {
            TipColumnAlignment.Left,
            TipColumnAlignment.Left,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
            TipColumnAlignment.Right,
        };

        private static TipModel? BuildSurgery(OverviewData snapshot)
        {
            IReadOnlyList<SurgeryKindTotals> rows = snapshot.SurgeryRows;
            if (rows.Count == 0) return null;

            var model = new TipModel
            {
                Title = "IMP_TipSurgery".Translate(),
                Badge = snapshot.SurgeryPercent.ToStringCached() + "%",
            };
            TipSection table = model.AddSection();
            table.Columns(new[]
            {
                "IMP_TipColTier".Translate().ToString(),
                "IMP_TipColImplant".Translate().ToString(),
                "IMP_TipColPlanned".Translate().ToString(),
                "IMP_TipColInstalled".Translate().ToString(),
                "IMP_TipColMissing".Translate().ToString(),
                "IMP_TipColReserved".Translate().ToString(),
                "IMP_TipColScheduled".Translate().ToString(),
            }, HeaderColor, alignments: SurgeryAlignments);
            table.Rule();
            for (int i = 0; i < rows.Count; i++)
            {
                SurgeryKindTotals row = rows[i];
                ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(row.Kind);
                string label = (entry?.Label ?? row.Kind).CapitalizeFirst();
                Color? color = row.Installed >= row.Planned
                    ? TipText.DimColor
                    : row.Scheduled > 0
                        ? PlannerStyle.ActiveText
                        : (Color?)null;
                table.Columns(new[]
                {
                    TierStars(row.Tier),
                    label,
                    row.Planned.ToStringCached(),
                    row.Installed.ToStringCached(),
                    row.Waiting.ToStringCached(),
                    row.Reserved.ToStringCached(),
                    row.Scheduled.ToStringCached(),
                }, color, alignments: SurgeryAlignments);
            }

            int[] states = snapshot.StateCounts;
            TipSection facts = model.AddSection();
            facts.Text("IMP_TipColonistStates".Translate(
                states[(int)ColonistStatus.Waiting],
                states[(int)ColonistStatus.Preparing],
                states[(int)ColonistStatus.Operating],
                states[(int)ColonistStatus.Done],
                states[(int)ColonistStatus.Away]), dim: true);
            return model;
        }

        /// The tier's star run; unranked kinds (never the case for catalog
        /// implants, which default to three stars) show no stars.
        private static string TierStars(int tier) =>
            tier >= 0 && tier < PlannerStyle.TierStars.Length
                ? PlannerStyle.TierStars[tier]
                : "";
    }
}
