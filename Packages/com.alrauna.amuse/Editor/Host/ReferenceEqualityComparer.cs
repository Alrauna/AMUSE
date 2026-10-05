using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// An equality comparer that compares by object reference. Unity's own
    /// equality collapses a destroyed object onto null and equates destroyed
    /// objects with each other, so any host-side set or dictionary keyed by a
    /// live Unity object identity must use this comparer instead.
    /// </summary>
    internal sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        internal static readonly ReferenceEqualityComparer<T> Instance =
            new ReferenceEqualityComparer<T>();

        public bool Equals(T x, T y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(T obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
