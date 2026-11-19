using System;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The sixth AMUSE pass. It reads the shared gate verdict the
    /// barrier recorded and applies every avatar-scope atlas gate,
    /// naming each refusal. The pass never reads
    /// <see cref="AmusePlatformFinishState.Separation"/>, because an
    /// atlas page reads materials, not the alpha proof plan.
    /// Milestone 1 stops at the analysis point. Milestone 2 adds the
    /// first atlas path there.
    /// <para>
    /// The check order below is part of the contract. The settings
    /// gates run before the target gate, so a wrong setting names
    /// itself on every machine, and the once-only guard runs first so
    /// a double registration can never report twice.
    /// </para>
    /// </summary>
    internal static class AtlasPass
    {
        internal const string PassName = "AMUSE atlas";

        internal static void Execute(BuildContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var state = context.GetState<AmusePlatformFinishState>();
            if (state.HasAtlasExecuted)
            {
                throw new InvalidOperationException(
                    "AMUSE atlas pass executed more than once.");
            }

            state.HasAtlasExecuted = true;

            // The barrier owns every shared report. This pass runs
            // only behind it, so a missing lifecycle verdict is a
            // caller defect, not a refusal.
            if (state.Lifecycle == null)
            {
                throw new InvalidOperationException(
                    "AMUSE atlas pass ran before the barrier pass.");
            }

            var optimizer = AmusePlatformFinishPass.ActivatedOptimizer(
                context);
            if (optimizer == null)
            {
                return;
            }

            if (!optimizer.AtlasingEnabled)
            {
                return;
            }

            if (!state.SharedGatesPassed)
            {
                // The barrier already reported the cause.
                return;
            }

            if (optimizer.MaximumAtlasPageSize < 1 ||
                optimizer.MaximumAtlasPageSize >
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
                        .MaximumAtlasPageSizeLimit)
            {
                Refuse(
                    context, state,
                    AtlasUseRefusal.MaximumPageSizeOutOfRange);
                return;
            }

            if (optimizer.ProtectedMipLevelCount < 1 ||
                optimizer.ProtectedMipLevelCount >
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
                        .MaximumProtectedMipLevelCount)
            {
                Refuse(
                    context, state,
                    AtlasUseRefusal.ProtectedMipLevelCountOutOfRange);
                return;
            }

            if (AmusePlatformFinishPlugin.ActiveBuildTarget() !=
                PlatformAlphaSamplingPolicy.DesktopTarget)
            {
                Refuse(
                    context, state,
                    AtlasUseRefusal.UnsupportedBuildTarget);
                return;
            }

            state.ReachedAtlasAnalysis = true;
        }

        /// <summary>
        /// Records one avatar-scope refusal and reports it once. The
        /// avatar keeps every original texture after a refusal.
        /// </summary>
        private static void Refuse(
            BuildContext context,
            AmusePlatformFinishState state,
            AtlasUseRefusal cause)
        {
            state.AtlasRefusal = cause;
            AmuseReports.AtlasRefusal(context.AvatarRootObject, cause);
        }
    }
}
