using System;
using EPrimeReadouts.Core;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    /// <summary>
    /// Shared resource panel chrome with mode-specific cached tree bodies.
    /// </summary>
    internal sealed class ResourcePanelView
    {
        private const string FilterControl = "EPR.ResourcePickerFilter";

        private readonly ItemPickerState filters = new ItemPickerState();
        private readonly GroupResourceTreeBody groupBody = new GroupResourceTreeBody();
        private readonly PoolResourceTreeBody poolBody = new PoolResourceTreeBody();
        private readonly Action filterChanged;
        private int filterRevision;

        internal ResourcePanelView()
        {
            filterChanged = OnFilterChanged;
        }

        internal void Draw(Rect rect, Dialog_ReadoutConfig owner,
            ReadoutConfigMode mode)
        {
            UiVersion.ObserveCurrentMetrics();
            var settings = EPrimeReadoutsMod.Settings;

            float used = EprStyle.SectionHeader(
                rect.x, rect.y, rect.width, UiText.Get("EPR.Resources"));

            bool folded = settings.helpResourcesFolded;
            string helpKey = mode == ReadoutConfigMode.ResourcePools
                ? "EPR.HelpPoolEditor"
                : "EPR.HelpResources";
            used += EprStyle.HelpGroup(
                rect.x,
                rect.y + used,
                rect.width,
                UiText.Get("EPR.Help"),
                UiText.Get(helpKey),
                ref folded);
            if (folded != settings.helpResourcesFolded)
                EPrimeReadoutsMod.Persist(s => s.helpResourcesFolded = folded);

            ItemPickerFilterBar.Draw(
                new Rect(rect.x, rect.y + used, rect.width,
                    ItemPickerFilterBar.Height),
                filters, FilterControl, filterChanged);

            float bodyHeight = rect.height - used - ItemPickerFilterBar.Height;
            if (bodyHeight <= 0f) return;
            var bodyRect = new Rect(
                rect.x,
                rect.y + used + ItemPickerFilterBar.Height,
                rect.width,
                bodyHeight);

            if (mode == ReadoutConfigMode.ResourcePools
                && owner.selectedPoolId < 0)
            {
                DrawEmpty(bodyRect, UiText.Get("EPR.SelectOrCreatePool"));
                return;
            }

            bool hasRows = mode == ReadoutConfigMode.ResourcePools
                ? poolBody.Draw(bodyRect, owner, filters, filterRevision)
                : groupBody.Draw(bodyRect, owner, filters, filterRevision);
            if (!hasRows)
                DrawEmpty(bodyRect, UiText.Get("EPR.NoMatchingItems"));
        }

        internal bool HandleEscape()
        {
            return DialogInputFocus.TryHandleEscape(
                FilterControl, filters.Query, () =>
                {
                    filters.Query = string.Empty;
                    OnFilterChanged();
                });
        }

        internal void Unfocus()
        {
            DialogInputFocus.Unfocus(FilterControl);
        }

        internal void Reset()
        {
            groupBody.Reset();
            poolBody.Reset();
            filters.Query = string.Empty;
            filters.Type = ItemPickerType.Resources;
            filters.SourceId = ItemSourceIds.All;
            filterRevision = 0;
            Unfocus();
        }

        private void OnFilterChanged()
        {
            filterRevision++;
            groupBody.OnFilterChanged();
            poolBody.OnFilterChanged();
        }

        private static void DrawEmpty(Rect rect, string label)
        {
            using (new GuiStateScope())
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = EprStyle.CaptionText;
                Widgets.Label(rect, label);
            }
        }
    }
}
