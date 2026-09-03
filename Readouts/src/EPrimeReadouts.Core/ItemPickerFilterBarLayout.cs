using System;

namespace EPrimeReadouts.Core
{
    public readonly struct ItemPickerFilterBarWidths
    {
        public readonly float Search;
        public readonly float Type;
        public readonly float Source;

        public ItemPickerFilterBarWidths(float search, float type, float source)
        {
            Search = search;
            Type = type;
            Source = source;
        }
    }

    /// <summary>
    /// Splits the picker filter bar between the search field and the two
    /// dropdown buttons. The default proportions (30% search, then 45/55 for
    /// the buttons) hold while every label fits. A button whose label needs
    /// more room takes the shortfall from its neighbours in proportion to the
    /// room each one can spare. When the labels cannot fit at all, the search
    /// field drops to its minimum and the buttons share the rest in proportion
    /// to what they asked for.
    /// </summary>
    public static class ItemPickerFilterBarLayout
    {
        private const float SearchShare = 0.30f;
        private const float TypeShare = 0.45f;

        public static ItemPickerFilterBarWidths Solve(float width, float gap,
            float searchMin, float typeRequired, float sourceRequired)
        {
            float search = (float)Math.Floor(width * SearchShare);
            float picker = Math.Max(1f, width - search - 2f * gap);
            float type = (float)Math.Floor(picker * TypeShare);
            float source = picker - type;
            float content = search + picker;

            searchMin = Math.Min(searchMin, search);
            typeRequired = Math.Max(0f, typeRequired);
            sourceRequired = Math.Max(0f, sourceRequired);

            if (typeRequired <= type && sourceRequired <= source)
                return new ItemPickerFilterBarWidths(search, type, source);

            if (searchMin + typeRequired + sourceRequired > content)
            {
                float rest = Math.Max(0f, content - searchMin);
                float demand = typeRequired + sourceRequired;
                float typeShare = demand > 0f ? typeRequired / demand : TypeShare;
                float typeW = (float)Math.Floor(rest * typeShare);
                return new ItemPickerFilterBarWidths(searchMin, typeW, rest - typeW);
            }

            if (typeRequired > type)
                Borrow(typeRequired - type, ref type, ref search, searchMin, ref source, sourceRequired);
            if (sourceRequired > source)
                Borrow(sourceRequired - source, ref source, ref search, searchMin, ref type, typeRequired);

            return new ItemPickerFilterBarWidths(search, type, source);
        }

        private static void Borrow(float deficit, ref float target,
            ref float first, float firstMin, ref float second, float secondMin)
        {
            float firstSlack = Math.Max(0f, first - firstMin);
            float secondSlack = Math.Max(0f, second - secondMin);
            float slack = firstSlack + secondSlack;
            if (slack <= 0f) return;
            float take = Math.Min(deficit, slack);
            float fromFirst = (float)Math.Floor(take * (firstSlack / slack));
            float fromSecond = take - fromFirst;
            first -= fromFirst;
            second -= fromSecond;
            target += take;
        }
    }
}
