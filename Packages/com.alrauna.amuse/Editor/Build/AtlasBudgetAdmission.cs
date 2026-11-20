using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The budget admission of the atlas pass. It walks the group
    /// plans in plan order and names the budget number that refuses
    /// each group. Unlimited limits skip every comparison.
    /// </summary>
    internal static class AtlasBudgetAdmission
    {
        /// <summary>
        /// Admits each group plan against the three budget numbers and
        /// returns one refusal per group, in plan order. The group
        /// check runs first, then the peak check, then the avatar
        /// check. Only an admitted group adds its retained bytes to
        /// the avatar sum.
        /// </summary>
        internal static IReadOnlyList<AtlasUseRefusal> Admit(
            IReadOnlyList<AtlasGroupPlan> plans,
            IReadOnlyList<CapturedAtlasUse> uses,
            int protectedMipLevelCount,
            AtlasBudgetLimits limits)
        {
            checked
            {
                var results = new List<AtlasUseRefusal>(plans.Count);
                if (limits.IsUnlimited)
                {
                    for (int i = 0; i < plans.Count; i++)
                    {
                        results.Add(AtlasUseRefusal.None);
                    }

                    return results;
                }

                long avatarRetainedSum = 0;
                foreach (AtlasGroupPlan plan in plans)
                {
                    long retainedBytes = AtlasBudget.GroupRetainedBytes(plan);
                    long sourceBytes = AtlasBudget.SourceBytes(plan, uses);
                    if (retainedBytes * limits.FactorDenominator
                        > sourceBytes * limits.FactorNumerator)
                    {
                        results.Add(AtlasUseRefusal.GroupBudget);
                        continue;
                    }

                    long peakBytes = AtlasBudget.PredictPeakBytes(
                        plan,
                        uses,
                        protectedMipLevelCount);
                    if (peakBytes > limits.PeakTemporaryBytes)
                    {
                        results.Add(AtlasUseRefusal.PeakBudget);
                        continue;
                    }

                    if (avatarRetainedSum + retainedBytes
                        > limits.AvatarRetainedBytes)
                    {
                        results.Add(AtlasUseRefusal.AvatarBudget);
                        continue;
                    }

                    avatarRetainedSum += retainedBytes;
                    results.Add(AtlasUseRefusal.None);
                }

                return results;
            }
        }
    }
}
