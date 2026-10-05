using System;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Float steps of exactly one unit in the last place, taken from the
    /// bit pattern directly. The profile has no
    /// <c>Math.BitIncrement</c>, so the bit trick is written once here.
    /// Boundary rows that falsify epsilon-based bounds depend on the step
    /// being exactly one ULP: a larger step would stop falsifying the
    /// bound.
    /// </summary>
    internal static class FloatUlp
    {
        /// <summary>Returns the next representable float above.</summary>
        internal static float NextFloatAbove(float value)
        {
            return Ulp(value, 1);
        }

        /// <summary>Adds <paramref name="steps"/> ULPs to a float.</summary>
        internal static float Ulp(float value, int steps)
        {
            return BitConverter.Int32BitsToSingle(
                BitConverter.SingleToInt32Bits(value) + steps);
        }
    }
}
