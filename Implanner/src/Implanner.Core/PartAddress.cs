using System;
using System.Globalization;

namespace Implanner.Core
{
    /// One anatomy instance on any body: a body part kind (BodyPartDef
    /// defName) and its occurrence among that kind's records in the body's
    /// own record order. This is exactly how the game itself tells twin
    /// parts apart — RimWorld has no "side" field; vanilla's two eyes are
    /// Eye#0 and Eye#1, while a race whose sides are separate part kinds
    /// (Android Tiers' LeftSightSensor/RightSightSensor) has
    /// LeftSightSensor#0 and RightSightSensor#0. An address means the same
    /// part on every body that has it and nothing on a body that does not,
    /// so goals carry addresses rather than positions in a per-body list.
    ///
    /// The persisted and wire form is Key, "Part#n". An address with an
    /// empty part is an unresolved legacy slot ordinal ("#n"): a goal saved
    /// before addresses existed whose implant content was not loaded when
    /// it was migrated; it targets nothing until a later load resolves it.
    public readonly struct PartAddress : IEquatable<PartAddress>, IComparable<PartAddress>
    {
        public const char Separator = '#';

        public PartAddress(string part, int occurrence)
        {
            Part = part ?? "";
            Occurrence = occurrence;
            Key = Part + Separator + occurrence.ToString(CultureInfo.InvariantCulture);
        }

        /// The body part kind's defName; empty for a legacy ordinal.
        public string Part { get; }

        /// Zero-based index among the body's records of this part kind.
        public int Occurrence { get; }

        /// "Part#n" (legacy: "#n").
        public string Key { get; }

        public bool IsLegacy => string.IsNullOrEmpty(Part);

        /// A goal slot persisted as a bare ordinal that could not be mapped
        /// onto an address yet (its implant content is missing).
        public static PartAddress Legacy(int ordinal) => new PartAddress("", ordinal);

        public static bool TryParse(string? text, out PartAddress address)
        {
            address = default;
            return text != null && TryParse(text, 0, text.Length, out address);
        }

        /// Parses text[start, end) as "Part#n": the part may be empty
        /// (legacy) but must not contain ':' or '#'; n is unsigned decimal.
        public static bool TryParse(string text, int start, int end, out PartAddress address)
        {
            address = default;
            if (text == null || start < 0 || end > text.Length || start >= end) return false;
            int separator = text.LastIndexOf(Separator, end - 1, end - start);
            if (separator < 0 || separator == end - 1) return false;
            for (int i = start; i < separator; i++)
                if (text[i] == ':' || text[i] == Separator) return false;
            int occurrence = 0;
            for (int i = separator + 1; i < end; i++)
            {
                int digit = text[i] - '0';
                if (digit < 0 || digit > 9) return false;
                if (occurrence > (int.MaxValue - digit) / 10) return false;
                occurrence = occurrence * 10 + digit;
            }
            address = new PartAddress(text.Substring(start, separator - start), occurrence);
            return true;
        }

        /// Whether text[start, start + length) is this address's Key.
        /// Allocation-free (goal-key matching on the tick path).
        public bool KeyEquals(string text, int start, int length) =>
            Key != null && Key.Length == length
            && string.CompareOrdinal(text, start, Key, 0, length) == 0;

        public bool Equals(PartAddress other) =>
            Occurrence == other.Occurrence
            && string.Equals(Part ?? "", other.Part ?? "", StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is PartAddress other && Equals(other);

        public override int GetHashCode() =>
            unchecked(StringComparer.Ordinal.GetHashCode(Part ?? "") * 31 + Occurrence);

        /// Part kind (ordinal), then occurrence: deterministic everywhere.
        public int CompareTo(PartAddress other)
        {
            int part = string.CompareOrdinal(Part ?? "", other.Part ?? "");
            return part != 0 ? part : Occurrence.CompareTo(other.Occurrence);
        }

        public static bool operator ==(PartAddress a, PartAddress b) => a.Equals(b);
        public static bool operator !=(PartAddress a, PartAddress b) => !a.Equals(b);

        public override string ToString() => Key ?? "";
    }
}
