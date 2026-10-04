using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace RimShared.Common
{
    public sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public static readonly ReferenceIdentityComparer<T> Instance =
            new ReferenceIdentityComparer<T>();

        private ReferenceIdentityComparer() { }

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) =>
            obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
    }
}
