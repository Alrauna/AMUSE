using System;

namespace Alrauna.Amuse.Editor.Build
{
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
}
