using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The budget admission of the atlas pass. It walks the group
    /// plans in plan order and names the budget number that refuses
    /// each group.
    /// </summary>
    internal static class AtlasBudgetAdmission
    {
        /// <summary>
        /// Admits each group plan against the three budget numbers and
        /// returns one verdict per group, in plan order. The group
        /// check runs first, then the peak check, then the avatar
        /// check. Only an admitted group adds its retained bytes to
        /// the avatar sum. A refusal carries the value and the limit
        /// of its number, so the report can state them.
        /// </summary>
        internal static IReadOnlyList<AtlasBudgetVerdict> Admit(
            IReadOnlyList<AtlasGroupPlan> plans,
            IReadOnlyList<CapturedAtlasUse> uses,
            int protectedMipLevelCount,
            AtlasBudgetLimits limits)
        {
            checked
            {
                var results = new List<AtlasBudgetVerdict>(plans.Count);
                long avatarRetainedSum = 0;
                foreach (AtlasGroupPlan plan in plans)
                {
                    long retainedBytes = AtlasBudget.GroupRetainedBytes(plan);
                    long sourceBytes = AtlasBudget.SourceBytes(plan, uses);
                    if (retainedBytes * limits.FactorDenominator
                        > sourceBytes * limits.FactorNumerator)
                    {
                        results.Add(new AtlasBudgetVerdict(
                            AtlasUseRefusal.GroupBudget,
                            retainedBytes,
                            sourceBytes * limits.FactorNumerator
                                / limits.FactorDenominator));
                        continue;
                    }

                    long peakBytes = AtlasBudget.PredictPeakBytes(
                        plan,
                        uses,
                        protectedMipLevelCount);
                    if (peakBytes > limits.PeakTemporaryBytes)
                    {
                        results.Add(new AtlasBudgetVerdict(
                            AtlasUseRefusal.PeakBudget,
                            peakBytes,
                            limits.PeakTemporaryBytes));
                        continue;
                    }

                    if (avatarRetainedSum + retainedBytes
                        > limits.AvatarRetainedBytes)
                    {
                        results.Add(new AtlasBudgetVerdict(
                            AtlasUseRefusal.AvatarBudget,
                            avatarRetainedSum + retainedBytes,
                            limits.AvatarRetainedBytes));
                        continue;
                    }

                    avatarRetainedSum += retainedBytes;
                    results.Add(AtlasBudgetVerdict.Admitted);
                }

                return results;
            }
        }
    }
}
