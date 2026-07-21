using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Signals
{
    public sealed class SignalSnapshot
    {
        private static readonly IReadOnlyList<Signal> NoSignals =
            new ReadOnlyCollection<Signal>(new List<Signal>());

        private readonly Dictionary<string, IReadOnlyList<Signal>> bySkill;
        private Dictionary<string, IReadOnlyList<Signal>> byWorkType;

        public static readonly SignalSnapshot Empty =
            new SignalSnapshot(Array.Empty<Signal>());

        public IReadOnlyList<Signal> All { get; }
        public IReadOnlyList<Signal> Global { get; }
        internal bool HasWorkTypeSignals => byWorkType != null;

        public SignalSnapshot(IEnumerable<Signal> signals)
        {
            if (signals == null) throw new ArgumentNullException(nameof(signals));

            var all = new List<Signal>();
            foreach (Signal signal in signals)
            {
                if (signal == null)
                    throw new ArgumentException("Signals cannot contain null.", nameof(signals));
                all.Add(signal);
            }

            bySkill = new Dictionary<string, IReadOnlyList<Signal>>(StringComparer.Ordinal);
            if (all.Count == 0)
            {
                All = NoSignals;
                Global = NoSignals;
                return;
            }

            all.Sort(SignalComparer.Instance);
            var global = new List<Signal>();
            var grouped = new Dictionary<string, List<Signal>>(StringComparer.Ordinal);
            Dictionary<string, List<Signal>> groupedByWorkType = null;
            foreach (Signal signal in all)
            {
                if (signal.WorkTypeDefName != null)
                {
                    if (groupedByWorkType == null)
                        groupedByWorkType = new Dictionary<string, List<Signal>>(
                            StringComparer.Ordinal);
                    if (!groupedByWorkType.TryGetValue(signal.WorkTypeDefName, out var workTypes))
                        groupedByWorkType[signal.WorkTypeDefName] = workTypes = new List<Signal>();
                    workTypes.Add(signal);
                    continue;
                }
                if (signal.SkillDefName == null)
                {
                    global.Add(signal);
                    continue;
                }

                if (!grouped.TryGetValue(signal.SkillDefName, out var values))
                    grouped[signal.SkillDefName] = values = new List<Signal>();
                values.Add(signal);
            }

            All = new ReadOnlyCollection<Signal>(all);
            Global = global.Count == 0
                ? NoSignals
                : new ReadOnlyCollection<Signal>(global);
            foreach (var pair in grouped)
                bySkill[pair.Key] = new ReadOnlyCollection<Signal>(pair.Value);
            if (groupedByWorkType != null)
            {
                byWorkType = new Dictionary<string, IReadOnlyList<Signal>>(
                    groupedByWorkType.Count, StringComparer.Ordinal);
                foreach (var pair in groupedByWorkType)
                    byWorkType[pair.Key] = new ReadOnlyCollection<Signal>(pair.Value);
            }
        }

        public IReadOnlyList<Signal> ForSkill(string skillDefName)
        {
            if (string.IsNullOrWhiteSpace(skillDefName)) return NoSignals;
            return bySkill.TryGetValue(skillDefName, out var values)
                ? values
                : NoSignals;
        }

        public IReadOnlyList<Signal> ForWorkType(string workTypeDefName)
        {
            if (string.IsNullOrWhiteSpace(workTypeDefName)) return NoSignals;
            return byWorkType != null
                && byWorkType.TryGetValue(workTypeDefName, out var values)
                ? values
                : NoSignals;
        }
    }
}
