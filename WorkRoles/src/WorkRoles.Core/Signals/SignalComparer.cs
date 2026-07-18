using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Signals
{
    public sealed class SignalComparer : IComparer<Signal>
    {
        public static readonly SignalComparer Instance = new SignalComparer();

        private SignalComparer() { }

        public int Compare(Signal x, Signal y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int compare = x.Type.CompareTo(y.Type);
            if (compare != 0) return compare;
            compare = x.Source.Kind.CompareTo(y.Source.Kind);
            if (compare != 0) return compare;
            compare = StringComparer.Ordinal.Compare(x.Source.PackageId, y.Source.PackageId);
            if (compare != 0) return compare;
            compare = StringComparer.Ordinal.Compare(x.Source.DefName, y.Source.DefName);
            if (compare != 0) return compare;
            compare = StringComparer.Ordinal.Compare(x.Source.EffectDiscriminator, y.Source.EffectDiscriminator);
            if (compare != 0) return compare;
            return StringComparer.Ordinal.Compare(x.SkillDefName, y.SkillDefName);
        }
    }
}
