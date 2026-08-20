using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using UnityEngine;
using Verse;

namespace EPrimeReadouts.UI
{
    internal static class ItemPickerFilterBar
    {
        internal const float Height = 32f;
        private const float Gap = 6f;
        private const float ClearW = 20f;

        internal static void Draw(Rect rect, ItemPickerState state,
            string controlName, Action changed)
        {
            float searchAreaW = Mathf.Floor(rect.width * 0.30f);
            float searchFieldW = state.Query.NullOrEmpty()
                ? searchAreaW
                : Mathf.Max(1f, searchAreaW - ClearW - 2f);
            var searchRect = new Rect(rect.x, rect.y, searchFieldW, 24f);
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
                    rect.x + searchAreaW - ClearW, rect.y + 2f, ClearW, ClearW);
                if (Widgets.ButtonImage(clearRect, TexButton.CloseXSmall))
                {
                    state.Query = "";
                    changed();
                }
            }

            float pickerAreaX = rect.x + searchAreaW + Gap;
            float pickerAreaW = Mathf.Max(0f, rect.xMax - pickerAreaX);
            float pickerW = Mathf.Max(1f, pickerAreaW - Gap);
            float typeW = pickerW * 0.45f;
            var typeRect = new Rect(pickerAreaX, rect.y, typeW, 24f);
            var sourceRect = new Rect(typeRect.xMax + Gap, rect.y,
                Mathf.Max(1f, rect.xMax - typeRect.xMax - Gap), 24f);

            string typeLabel = state.Type == ItemPickerType.Resources
                ? UiText.Get("EPR.Resources")
                : UiText.Get("EPR.AllStorableItems");
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
            if (WrText.FitWidth(typeLabel) > typeRect.width - 16f)
                TooltipHandler.TipRegion(typeRect, typeLabel);

            IReadOnlyList<ItemSourceOption> sources = GameResourceCatalog.Instance.SourceChoices();
            string sourceLabel = SourceLabel(sources, state.SourceId);
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
            if (WrText.FitWidth(sourceLabel) > sourceRect.width - 16f)
                TooltipHandler.TipRegion(sourceRect, sourceLabel);
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
