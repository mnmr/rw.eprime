using System;
using System.Collections.Generic;
using System.Globalization;

namespace Implanner.Core
{
    /// Quality Bionics Remastered's per-quality part efficiency multipliers
    /// are per-player mod settings. The simulation reads the model's synced
    /// copy (PlannerModel.QualityMultipliers), which travels as one plain
    /// string: seven invariant-culture floats, Awful to Legendary, joined by
    /// ';' (round-trip "R" format, so equal tables encode equally).
    public static class QualityMultipliers
    {
        public const int Count = ImplantQuality.Highest + 1;

        /// That mod's shipped defaults, the table a save starts with.
        public static readonly float[] Default = { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.7f, 2f };

        public static string Encode(IReadOnlyList<float> table)
        {
            var parts = new string[Count];
            for (int q = 0; q < Count; q++)
                parts[q] = table[q].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(";", parts);
        }

        /// False for anything but exactly seven finite floats.
        public static bool TryParse(string? text, out float[] table)
        {
            table = new float[Count];
            if (text == null) return false;
            string[] parts = text.Split(';');
            if (parts.Length != Count) return false;
            for (int q = 0; q < Count; q++)
            {
                if (!float.TryParse(parts[q], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float value) || float.IsNaN(value) || float.IsInfinity(value))
                    return false;
                table[q] = value;
            }
            return true;
        }

        public static bool Same(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            if (a.Count != b.Count) return false;
            for (int q = 0; q < a.Count; q++)
                if (a[q] != b[q]) return false;
            return true;
        }
    }
}
