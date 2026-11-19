using System;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The three budget numbers of one build: the rational factor of
    /// the group check, the avatar retained byte limit, and the peak
    /// temporary byte limit. All comparisons use these numbers as
    /// integers. Milestone 5 sets the measured values. Until then the
    /// production provider returns <see cref="Unlimited"/>, which
    /// skips every comparison, so no product can overflow.
    /// </summary>
    internal readonly struct AtlasBudgetLimits
    {
        /// <summary>
        /// The limits before Milestone 5. Every comparison is skipped,
        /// so every group passes the budget gates.
        /// </summary>
        internal static AtlasBudgetLimits Unlimited { get; } =
            new AtlasBudgetLimits(
                true,
                1,
                1,
                long.MaxValue,
                long.MaxValue);

        internal bool IsUnlimited { get; }

        /// <summary>
        /// The numerator of the group factor. The group check holds
        /// when the retained bytes times the denominator stay at or
        /// below the source bytes times this numerator.
        /// </summary>
        internal long FactorNumerator { get; }

        /// <summary>The denominator of the group factor.</summary>
        internal long FactorDenominator { get; }

        /// <summary>The avatar retained byte limit.</summary>
        internal long AvatarRetainedBytes { get; }

        /// <summary>The peak temporary byte limit of one group.</summary>
        internal long PeakTemporaryBytes { get; }

        internal AtlasBudgetLimits(
            long factorNumerator,
            long factorDenominator,
            long avatarRetainedBytes,
            long peakTemporaryBytes)
            : this(
                false,
                factorNumerator,
                factorDenominator,
                avatarRetainedBytes,
                peakTemporaryBytes)
        {
        }

        private AtlasBudgetLimits(
            bool isUnlimited,
            long factorNumerator,
            long factorDenominator,
            long avatarRetainedBytes,
            long peakTemporaryBytes)
        {
            if (factorDenominator <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(factorDenominator),
                    "The group factor denominator must be positive.");
            }

            if (factorNumerator < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(factorNumerator),
                    "Every budget number must be zero or positive.");
            }

            if (avatarRetainedBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(avatarRetainedBytes),
                    "Every budget number must be zero or positive.");
            }

            if (peakTemporaryBytes < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(peakTemporaryBytes),
                    "Every budget number must be zero or positive.");
            }

            IsUnlimited = isUnlimited;
            FactorNumerator = factorNumerator;
            FactorDenominator = factorDenominator;
            AvatarRetainedBytes = avatarRetainedBytes;
            PeakTemporaryBytes = peakTemporaryBytes;
        }

        /// <summary>
        /// The production limits delegate. The release gate replaces it
        /// with the measured values of Milestone 5. Tests that replace
        /// it restore it in their tear down.
        /// </summary>
        internal static Func<AtlasBudgetLimits> Provider = () => Unlimited;
    }
}
