using System;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The three budget numbers of one build: the rational factor of
    /// the group check, the avatar retained byte limit, and the peak
    /// temporary byte limit. All comparisons use these numbers as
    /// integers. Milestone 5 set the production values from
    /// measurements on public fixtures. The values and their reasons
    /// are in
    /// docs/superpowers/plans/2026-11-21-atlasing-milestone-5-release-qualification.md,
    /// decisions B1 to B3.
    /// </summary>
    internal readonly struct AtlasBudgetLimits
    {
        /// <summary>
        /// The production limits.
        /// <list type="bullet">
        /// <item>B1. Group factor 5/4. Borders and packing gaps are the
        /// only cause of page bytes above source bytes. The public
        /// fixtures measure at most 1.061.</item>
        /// <item>B2. Avatar retained bytes 524,288,000 (500 MiB). This
        /// is the VRChat SDK limit for the uncompressed size of a PC
        /// avatar bundle. Pages above it alone can never upload.</item>
        /// <item>B3. Peak temporary bytes 4,294,967,296 (4 GiB). It
        /// admits a full 8192 page with up to five distinct 8192 by
        /// 8192 RGBA32 sources.</item>
        /// </list>
        /// </summary>
        internal static AtlasBudgetLimits Production { get; } =
            new AtlasBudgetLimits(5, 4, 524288000L, 4294967296L);

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

            FactorNumerator = factorNumerator;
            FactorDenominator = factorDenominator;
            AvatarRetainedBytes = avatarRetainedBytes;
            PeakTemporaryBytes = peakTemporaryBytes;
        }

        /// <summary>
        /// The limits delegate of the atlas pass. It returns
        /// <see cref="Production"/>. Tests that replace it restore the
        /// saved delegate in their tear down.
        /// </summary>
        internal static Func<AtlasBudgetLimits> Provider = () => Production;
    }
}
