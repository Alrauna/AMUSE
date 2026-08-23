using System;
using nadena.dev.ndmf;

namespace Alrauna.Amuse.Editor.Build
{
    internal enum AmuseBuildOperationOutcome
    {
        LifecycleRefused,
        PreparationRefused,
        NoMutationRequired,
        Mutated,
    }

    /// <summary>
    /// The result of preparing an AMUSE mutation. Only an explicit refusal is an
    /// ordinary conservative outcome; an unexpected defect during preparation is
    /// an exception, not a decision.
    /// </summary>
    internal readonly struct AmusePreparationDecision
    {
        private AmusePreparationDecision(
            bool isPrepared,
            bool hasMutation,
            string refusalReason)
        {
            IsPrepared = isPrepared;
            HasMutation = hasMutation;
            RefusalReason = refusalReason;
        }

        internal bool IsPrepared { get; }
        internal bool HasMutation { get; }
        internal string RefusalReason { get; }

        internal static AmusePreparationDecision Refused(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException(
                    "A preparation refusal must explain why AMUSE preserved the input.",
                    nameof(reason));
            }

            return new AmusePreparationDecision(false, false, reason);
        }

        internal static AmusePreparationDecision NoMutation()
        {
            return new AmusePreparationDecision(true, false, null);
        }

        internal static AmusePreparationDecision Ready()
        {
            return new AmusePreparationDecision(true, true, null);
        }
    }

    internal sealed class AmuseBuildOperationResult
    {
        internal AmuseBuildOperationResult(
            AmuseBuildOperationOutcome outcome,
            HostLifecycleCapability lifecycle,
            string refusalReason)
        {
            Outcome = outcome;
            Lifecycle = lifecycle;
            RefusalReason = refusalReason;
        }

        internal AmuseBuildOperationOutcome Outcome { get; }
        internal HostLifecycleCapability Lifecycle { get; }
        internal string RefusalReason { get; }
    }

    /// <summary>
    /// Prepares an AMUSE mutation. Preparation receives only the asset saver that
    /// currently owns generated output; prepared results are closed over locally
    /// and applied by the matching apply delegate.
    /// </summary>
    internal delegate AmusePreparationDecision PrepareAmuseMutation(
        IAssetSaver assetSaver);

    /// <summary>
    /// Applies a prepared AMUSE mutation. Invoking this is the first live-avatar
    /// mutation; no rollback is promised once it starts.
    /// </summary>
    internal delegate void ApplyAmuseMutation();

    internal static class AmuseBuildOperation
    {
        internal static AmuseBuildOperationResult Execute(
            HostLifecycleCapability lifecycle,
            IAssetSaver assetSaver,
            PrepareAmuseMutation prepare,
            ApplyAmuseMutation apply)
        {
            if (lifecycle == null)
            {
                throw new ArgumentNullException(nameof(lifecycle));
            }

            if (assetSaver == null)
            {
                throw new ArgumentNullException(nameof(assetSaver));
            }

            if (prepare == null)
            {
                throw new ArgumentNullException(nameof(prepare));
            }

            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }

            if (!lifecycle.MayUsePositiveMutation)
            {
                return new AmuseBuildOperationResult(
                    AmuseBuildOperationOutcome.LifecycleRefused, lifecycle, null);
            }

            // An unexpected preparation defect is not caught: it propagates so
            // NDMF records a build-blocking InternalError before anything is
            // mutated.
            var decision = prepare(assetSaver);

            if (!decision.IsPrepared)
            {
                return new AmuseBuildOperationResult(
                    AmuseBuildOperationOutcome.PreparationRefused,
                    lifecycle,
                    decision.RefusalReason);
            }

            if (!decision.HasMutation)
            {
                return new AmuseBuildOperationResult(
                    AmuseBuildOperationOutcome.NoMutationRequired, lifecycle, null);
            }

            // First mutation boundary. An apply defect is not caught either, and
            // nothing is rolled back.
            apply();

            return new AmuseBuildOperationResult(
                AmuseBuildOperationOutcome.Mutated, lifecycle, null);
        }
    }
}
