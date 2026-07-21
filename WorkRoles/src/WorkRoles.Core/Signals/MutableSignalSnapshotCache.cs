using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Signals
{
    /// <summary>
    /// Compact deterministic identity for every mutable input used to build a
    /// pawn signal snapshot. Two independent 64-bit lanes make an accidental
    /// collision unsuitable as a practical cache identity without retaining
    /// per-pawn input collections.
    /// </summary>
    public readonly struct MutableSignalSignature : IEquatable<MutableSignalSignature>
    {
        internal MutableSignalSignature(ulong first, ulong second, int fields)
        {
            First = first;
            Second = second;
            Fields = fields;
        }

        public ulong First { get; }
        public ulong Second { get; }
        public int Fields { get; }

        public bool Equals(MutableSignalSignature other) =>
            First == other.First && Second == other.Second && Fields == other.Fields;

        public override bool Equals(object obj) =>
            obj is MutableSignalSignature other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)First * 397) ^ (int)(First >> 32)
                    ^ (int)Second ^ (int)(Second >> 32) ^ Fields;
            }
        }

        public static bool operator ==(MutableSignalSignature left,
            MutableSignalSignature right) => left.Equals(right);

        public static bool operator !=(MutableSignalSignature left,
            MutableSignalSignature right) => !left.Equals(right);
    }

    /// <summary>
    /// Allocation-free writer for <see cref="MutableSignalSignature"/>. Category
    /// markers and string lengths make differently-shaped input streams distinct.
    /// </summary>
    public struct MutableSignalSignatureBuilder
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const ulong SecondOffset = 7809847782465536322UL;
        private const ulong SecondPrime = 14029467366897019727UL;

        private ulong first;
        private ulong second;
        private int fields;

        private MutableSignalSignatureBuilder(ulong first, ulong second)
        {
            this.first = first;
            this.second = second;
            fields = 0;
        }

        public static MutableSignalSignatureBuilder Start() =>
            new MutableSignalSignatureBuilder(FnvOffset, SecondOffset);

        public void AddSkill(string defName, bool enabled, int passion)
        {
            AddToken(1);
            AddString(defName);
            AddBoolean(enabled);
            AddInteger(passion);
        }

        public void AddModdedPassion(string packageId, string defName, bool isBad,
            float learnRate, float forgetRate, float otherLearnRate,
            string iconPath, string authorTier)
        {
            AddToken(2);
            AddString(packageId);
            AddString(defName);
            AddBoolean(isBad);
            AddFloat(learnRate);
            AddFloat(forgetRate);
            AddFloat(otherLearnRate);
            AddString(iconPath);
            AddString(authorTier);
        }

        public void AddExpertise(string packageId, string defName,
            string skillDefName, int level, float xpSinceLastLevel,
            float xpRequiredForLevelUp, float statMultiplier)
        {
            AddToken(3);
            AddString(packageId);
            AddString(defName);
            AddString(skillDefName);
            AddInteger(level);
            AddInteger(DisplayedWholeXp(xpSinceLastLevel));
            AddInteger(DisplayedWholeXp(xpRequiredForLevelUp));
            AddFloat(statMultiplier);
        }

        // VSE FullDescription renders earned progress as {F0}, and its level
        // requirement is an integral XP value. Matching their whole-XP display
        // avoids rebuilding every dependent cache for invisible fractions.
        private static int DisplayedWholeXp(float value)
        {
            if (float.IsNaN(value)) return int.MinValue;
            if (value >= int.MaxValue) return int.MaxValue;
            if (value <= int.MinValue) return int.MinValue + 1;
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        public void AddTrait(string packageId, string defName, int degree,
            bool suppressed)
        {
            AddToken(4);
            AddString(packageId);
            AddString(defName);
            AddInteger(degree);
            AddBoolean(suppressed);
        }

        public void AddGene(string packageId, string defName, bool active)
        {
            AddToken(5);
            AddString(packageId);
            AddString(defName);
            AddBoolean(active);
        }

        public void AddHediff(string packageId, string defName, float severity,
            int stage, string partDefName)
        {
            AddToken(6);
            AddString(packageId);
            AddString(defName);
            AddFloat(severity);
            AddInteger(stage);
            AddString(partDefName);
        }

        public void AddProviderCondition(string name, bool value)
        {
            AddToken(7);
            AddString(name);
            AddBoolean(value);
        }

        public void AddWorkAversionState(int disabledWorkTags)
        {
            AddToken(8);
            AddInteger(disabledWorkTags);
        }

        public MutableSignalSignature Build() =>
            new MutableSignalSignature(first, second, fields);

        private void AddString(string value)
        {
            if (value == null)
            {
                AddToken(ulong.MaxValue);
                return;
            }
            AddToken((ulong)value.Length);
            for (int i = 0; i < value.Length; i++) AddToken(value[i]);
        }

        private void AddBoolean(bool value) => AddToken(value ? 1UL : 0UL);

        private void AddInteger(int value) => AddToken(unchecked((uint)value));

        private void AddFloat(float value) => AddInteger(value.GetHashCode());

        private void AddToken(ulong value)
        {
            unchecked
            {
                first = (first ^ value) * FnvPrime;
                second = (second ^ (value + 0x9e3779b97f4a7c15UL)) * SecondPrime;
                second = (second << 31) | (second >> 33);
                fields++;
            }
        }
    }

    /// <summary>
    /// Snapshot cache keyed by an owner and its mutable-input signature. The
    /// observation epoch bounds signature traversal to once per owner per
    /// polling interval while a later epoch discovers unhooked mutations.
    /// </summary>
    public sealed class MutableSignalSnapshotCache<TKey, TSnapshot>
        where TKey : class
    {
        private sealed class Entry
        {
            internal MutableSignalSignature Signature;
            internal TSnapshot Snapshot;
            internal long ObservationEpoch;
        }

        private readonly Dictionary<TKey, Entry> entries =
            new Dictionary<TKey, Entry>();
        private readonly Func<TKey, MutableSignalSignature> signatureOf;
        private readonly Func<TKey, TSnapshot> build;

        public MutableSignalSnapshotCache(
            Func<TKey, MutableSignalSignature> signatureOf,
            Func<TKey, TSnapshot> build)
        {
            this.signatureOf = signatureOf
                ?? throw new ArgumentNullException(nameof(signatureOf));
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public long Revision { get; private set; }

        public TSnapshot Get(TKey key, long observationEpoch)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (entries.TryGetValue(key, out Entry entry)
                && entry.ObservationEpoch == observationEpoch)
                return entry.Snapshot;

            MutableSignalSignature signature = signatureOf(key);
            if (entry != null && entry.Signature == signature)
            {
                entry.ObservationEpoch = observationEpoch;
                return entry.Snapshot;
            }

            TSnapshot snapshot = build(key);
            if (entry == null)
            {
                entry = new Entry();
                entries.Add(key, entry);
            }
            entry.Signature = signature;
            entry.Snapshot = snapshot;
            entry.ObservationEpoch = observationEpoch;
            AdvanceRevision();
            return snapshot;
        }

        public void Invalidate(TKey key)
        {
            if (key != null && entries.Remove(key)) AdvanceRevision();
        }

        public void Clear()
        {
            if (entries.Count == 0) return;
            entries.Clear();
            AdvanceRevision();
        }

        private void AdvanceRevision()
        {
            checked { Revision++; }
        }
    }

    /// <summary>
    /// Coalesces a whole-cohort traversal by an external epoch and cohort key.
    /// It deliberately has no single-item API, so observing one pawn cannot mark
    /// a map or scoped pawn list as already traversed.
    /// </summary>
    public sealed class ObservationEpochGate<TKey>
    {
        private readonly EqualityComparer<TKey> comparer =
            EqualityComparer<TKey>.Default;
        private bool observed;
        private long epoch;
        private TKey key;

        public bool Enter(long epoch, TKey key)
        {
            if (observed && this.epoch == epoch && comparer.Equals(this.key, key))
                return false;
            observed = true;
            this.epoch = epoch;
            this.key = key;
            return true;
        }

        public void Clear()
        {
            observed = false;
            key = default(TKey);
        }
    }
}
