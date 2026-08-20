using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    public enum ItemPickerType
    {
        Resources,
        AllStorableItems,
    }

    public static class ItemSourceIds
    {
        public const string All = "";
        public const string Vanilla = "__vanilla__";
    }

    public readonly struct ItemTreeFilter
    {
        public readonly string Query;
        public readonly ItemPickerType Type;
        public readonly string SourceId;

        public ItemTreeFilter(string query, ItemPickerType type, string sourceId)
        {
            Query = query ?? "";
            Type = type;
            SourceId = sourceId ?? ItemSourceIds.All;
        }
    }

    /// Session-local mutable state. Each picker owns its own instance; callers
    /// may share the filtering code without coupling the user's choices.
    public sealed class ItemPickerState
    {
        public string Query = "";
        public ItemPickerType Type = ItemPickerType.Resources;
        public string SourceId = ItemSourceIds.All;
    }

    public interface IItemPickerCatalog : IResourceCatalog
    {
        bool IsResource(string defName);
        bool IsStorable(string defName);
        string SourceIdOf(string defName);
    }

    public readonly struct ItemSourceOption
    {
        public readonly string Id;
        public readonly string Label;

        public ItemSourceOption(string id, string label)
        {
            Id = id ?? "";
            Label = label ?? "";
        }
    }

    public static class ItemSourceChoices
    {
        public static List<ItemSourceOption> Build(IEnumerable<ItemSourceOption> contributing,
            string allLabel, string vanillaLabel)
        {
            var byId = new Dictionary<string, ItemSourceOption>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in contributing)
            {
                if (string.IsNullOrEmpty(option.Id) || option.Id == ItemSourceIds.Vanilla)
                    continue;
                if (!byId.ContainsKey(option.Id))
                    byId.Add(option.Id, option);
            }

            var userSources = new List<ItemSourceOption>(byId.Values);
            userSources.Sort((left, right) =>
            {
                int label = string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase);
                return label != 0
                    ? label
                    : string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase);
            });

            var result = new List<ItemSourceOption>(userSources.Count + 2)
            {
                new ItemSourceOption(ItemSourceIds.All, allLabel),
                new ItemSourceOption(ItemSourceIds.Vanilla, vanillaLabel),
            };
            result.AddRange(userSources);
            return result;
        }
    }
}
