using System;

namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// The two argument checks every census record repeats. Extracted only
    /// because they are repeated, not to establish a validation layer.
    /// </summary>
    internal static class CensusGuard
    {
        internal static void Defined<TEnum>(TEnum value, string parameterName)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        internal static void NotNegative(int value, string parameterName)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
