using System;
using System.Collections.Generic;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// Shared defensive-copy helper for the census record constructors. Every
    /// census record stores a private copy of the list it was given, and every
    /// null element must fail with the same exception and message.
    /// </summary>
    internal static class CensusCopy
    {
        /// <summary>
        /// Copies <paramref name="source"/> into a fresh array, throwing
        /// <see cref="ArgumentNullException"/> named after the parameter on a
        /// null element, exactly as the constructors did inline.
        /// </summary>
        internal static T[] CheckedCopy<T>(
            IReadOnlyList<T> source, string parameterName)
        {
            var copied = new T[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                copied[index] = source[index]
                    ?? throw new ArgumentNullException(parameterName);
            }

            return copied;
        }
    }
}
