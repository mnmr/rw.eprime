using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public enum SignalPassionTier { None, Minor, Major }

    public sealed class SkillSignalView : IEquatable<SkillSignalView>
    {
        public IReadOnlyList<Signal> SkillSignals { get; }
        public IReadOnlyList<Signal> GlobalSignals { get; }
        public IReadOnlyList<Signal> IconCandidates { get; }
        public IReadOnlyList<Signal> OfficialMissingIcons { get; }
        public SignalPassionTier PassionTier { get; }
        public IReadOnlyList<Signal> ActiveSignals { get; }
        public IReadOnlyList<Signal> PassiveSignals { get; }
        public bool HasGlobalSignals => GlobalSignals.Count > 0;
        public bool HasTooltip => true;

        // These two ordered inputs determine every derived list and passion
        // tier. Compare before rebuilding, so an unrelated skill's recapture
        // does not recreate this skill's projection or resolved icon list.
        public bool Matches(SignalSnapshot snapshot, string skillDefName) =>
            SameSignals(SkillSignals, snapshot.ForSkill(skillDefName))
            && SameSignals(GlobalSignals, snapshot.Global);

        public bool Equals(SkillSignalView? other) => other != null
            && SameSignals(SkillSignals, other.SkillSignals)
            && SameSignals(GlobalSignals, other.GlobalSignals);

        public override bool Equals(object? obj) => Equals(obj as SkillSignalView);

        public override int GetHashCode() => SignalHash.WithSequence(
            SignalHash.WithSequence(0, SkillSignals), GlobalSignals);

        private static bool SameSignals(IReadOnlyList<Signal> left,
            IReadOnlyList<Signal> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++)
                if (!left[i].Equals(right[i])) return false;
            return true;
        }

        internal SkillSignalView(
            IReadOnlyList<Signal> skillSignals,
            IReadOnlyList<Signal> globalSignals,
            IReadOnlyList<Signal> iconCandidates,
            IReadOnlyList<Signal> officialMissingIcons,
            SignalPassionTier passionTier,
            IReadOnlyList<Signal> activeSignals,
            IReadOnlyList<Signal> passiveSignals)
        {
            SkillSignals = skillSignals;
            GlobalSignals = globalSignals;
            IconCandidates = iconCandidates;
            OfficialMissingIcons = officialMissingIcons;
            PassionTier = passionTier;
            ActiveSignals = activeSignals;
            PassiveSignals = passiveSignals;
        }
    }

    public static class SignalPresentationPolicy
    {
        public static SkillSignalView ForSkill(SignalSnapshot snapshot, string skillDefName)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            IReadOnlyList<Signal> skillSignals = snapshot.ForSkill(skillDefName);
            var iconCandidates = new List<Signal>();
            var officialMissingIcons = new List<Signal>();
            var activeSignals = new List<Signal>();
            var passiveSignals = new List<Signal>();
            SignalPassionTier passionTier = SignalPassionTier.None;

            foreach (Signal signal in skillSignals)
            {
                if (signal.Type == SignalType.Active)
                    activeSignals.Add(signal);
                else if (signal.Type == SignalType.Passive)
                    passiveSignals.Add(signal);

                if (signal.Relation == SignalRelation.Spillover) continue;

                if (string.IsNullOrWhiteSpace(signal.Ui.IconKey))
                {
                    if (IsOfficialPackage(signal.Source.PackageId))
                        officialMissingIcons.Add(signal);
                }
                else
                {
                    iconCandidates.Add(signal);
                }

                if (signal.Source.Kind != SignalSourceKind.Passion) continue;
                if (StringComparer.OrdinalIgnoreCase.Equals(signal.Ui.AuthorTier, "Major"))
                    passionTier = SignalPassionTier.Major;
                else if (passionTier == SignalPassionTier.None
                    && StringComparer.OrdinalIgnoreCase.Equals(signal.Ui.AuthorTier, "Minor"))
                    passionTier = SignalPassionTier.Minor;
            }

            foreach (Signal signal in snapshot.Global)
            {
                if (signal.Type == SignalType.Active)
                    activeSignals.Add(signal);
                else if (signal.Type == SignalType.Passive)
                    passiveSignals.Add(signal);
            }

            return new SkillSignalView(
                skillSignals,
                snapshot.Global,
                ReadOnly(iconCandidates),
                ReadOnly(officialMissingIcons),
                passionTier,
                ReadOnly(activeSignals),
                ReadOnly(passiveSignals));
        }

        public static bool IsOfficialPackage(string packageId) =>
            StringComparer.OrdinalIgnoreCase.Equals(packageId, "Ludeon.RimWorld")
            || (packageId != null
                && packageId.StartsWith("Ludeon.RimWorld.", StringComparison.OrdinalIgnoreCase));

        private static IReadOnlyList<Signal> ReadOnly(List<Signal> values) =>
            values.Count == 0
                ? Array.Empty<Signal>()
                : new ReadOnlyCollection<Signal>(values);
    }
}
