using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimShared.UiLib;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    internal static class ItemPickerFilterBar
    {
        internal static float Height =>
            EprStyle.TinyTextMetrics.LineHeight + ControlH + BottomGap;
        private const float Gap = 6f;
        private const float ClearW = 20f;
        private const float ControlH = 24f;
        private const float BottomGap = 8f;
        /// Horizontal room a dropdown button keeps around its label so the
        /// text never touches the border (matches the tooltip threshold).
        private const float ButtonPad = 16f;
        /// The search field may shrink to this share of the bar when a
        /// button label needs the room.
        private const float SearchMinShare = 0.15f;

        internal static void Draw(Rect rect, ItemPickerState state,
            string controlName, Action changed)
        {
            ResolvedTinyTextMetrics metrics = EprStyle.TinyTextMetrics;
            float captionH = metrics.LineHeight;

            string typeLabel = state.Type == ItemPickerType.Resources
                ? UiText.Get("EPR.Resources")
                : UiText.Get("EPR.AllStorableItems");
            IReadOnlyList<ItemSourceOption> sources = GameResourceCatalog.Instance.SourceChoices();
            string sourceLabel = SourceLabel(sources, state.SourceId);
            ItemPickerFilterBarWidths widths = ItemPickerFilterBarLayout.Solve(
                rect.width,
                Gap,
                Mathf.Floor(rect.width * SearchMinShare),
                WrText.FitWidth(typeLabel) + ButtonPad,
                WrText.FitWidth(sourceLabel) + ButtonPad);

            float searchAreaW = widths.Search;
            float pickerAreaX = rect.x + searchAreaW + Gap;
            float typeW = Mathf.Max(1f, widths.Type);
            float controlY = rect.y + captionH;
            var typeRect = new Rect(pickerAreaX, controlY, typeW, ControlH);
            var sourceRect = new Rect(typeRect.xMax + Gap, controlY,
                Mathf.Max(1f, widths.Source), ControlH);

            using (GuiStateScope.Capture())
            {
                GUI.color = EprStyle.CaptionText;
                DrawFilterCaption(new Rect(
                    rect.x,
                    rect.y,
                    searchAreaW,
                    captionH),
                    UiText.Get("EPR.SearchFilter"));
                DrawFilterCaption(new Rect(
                    pickerAreaX,
                    rect.y,
                    typeW,
                    captionH),
                    UiText.Get("EPR.ItemFilter"));
                DrawFilterCaption(new Rect(
                    sourceRect.x,
                    rect.y,
                    sourceRect.width,
                    captionH),
                    UiText.Get("EPR.SourceFilter"));
            }

            float searchFieldW = state.Query.NullOrEmpty()
                ? searchAreaW
                : Mathf.Max(1f, searchAreaW - ClearW - 2f);
            var searchRect = new Rect(
                rect.x, controlY, searchFieldW, ControlH);
            GUI.SetNextControlName(controlName);
            string query = Widgets.TextField(searchRect, state.Query);
            if (!string.Equals(query, state.Query, StringComparison.Ordinal))
            {
                state.Query = query;
                changed();
            }
            if (!state.Query.NullOrEmpty())
            {
                var clearRect = new Rect(
                    rect.x + searchAreaW - ClearW,
                    controlY + 2f,
                    ClearW,
                    ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    state.Query = "";
                    changed();
                }
            }

            if (Widgets.ButtonText(typeRect, typeLabel))
            {
                var options = new List<FloatMenuOption>(2)
                {
                    TypeOption(UiText.Get("EPR.Resources"), ItemPickerType.Resources,
                        state, changed),
                    TypeOption(UiText.Get("EPR.AllStorableItems"),
                        ItemPickerType.AllStorableItems, state, changed),
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (WrText.FitWidth(typeLabel) > typeRect.width - ButtonPad)
                TooltipHandler.TipRegion(typeRect, typeLabel);

            if (Widgets.ButtonText(sourceRect, sourceLabel))
            {
                var options = new List<FloatMenuOption>(sources.Count);
                for (int i = 0; i < sources.Count; i++)
                {
                    ItemSourceOption captured = sources[i];
                    options.Add(new FloatMenuOption(captured.Label, () =>
                    {
                        if (string.Equals(state.SourceId, captured.Id,
                            StringComparison.OrdinalIgnoreCase))
                            return;
                        state.SourceId = captured.Id;
                        changed();
                    }));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            if (WrText.FitWidth(sourceLabel) > sourceRect.width - ButtonPad)
                TooltipHandler.TipRegion(sourceRect, sourceLabel);
        }

        private static void DrawFilterCaption(Rect rect, string text)
        {
            rect.y -= 2f;
            rect.height += 2f;
            TinyText.Label(rect, text);
        }

        private static FloatMenuOption TypeOption(string label, ItemPickerType type,
            ItemPickerState state, Action changed)
        {
            return new FloatMenuOption(label, () =>
            {
                if (state.Type == type) return;
                state.Type = type;
                changed();
            });
        }

        private static string SourceLabel(IReadOnlyList<ItemSourceOption> sources, string sourceId)
        {
            for (int i = 0; i < sources.Count; i++)
                if (string.Equals(sources[i].Id, sourceId, StringComparison.OrdinalIgnoreCase))
                    return sources[i].Label;
            return sourceId;
        }
    }
}
