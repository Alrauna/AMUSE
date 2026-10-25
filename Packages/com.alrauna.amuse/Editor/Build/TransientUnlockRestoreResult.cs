using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The outcome of one restore call. A completed reconstruction is
    /// Succeeded. Every named refusal is Failed. A Failed outcome is an
    /// expected named failure, never a defect.
    /// </summary>
    internal enum TransientUnlockRestoreOutcome
    {
        Succeeded,
        Failed,
    }

    /// <summary>
    /// The outcome and the detail of one restore call (A8.2). A completed
    /// reconstruction is <see cref="TransientUnlockRestoreOutcome.Succeeded"/>
    /// with a null detail. Every named refusal is
    /// <see cref="TransientUnlockRestoreOutcome.Failed"/> and carries one
    /// fixed detail sentence. A Failed outcome is an expected named
    /// failure, never a defect. A detail sentence never names a property
    /// value or a path.
    /// </summary>
    internal readonly struct TransientUnlockRestoreResult
    {
        internal TransientUnlockRestoreResult(
            TransientUnlockRestoreOutcome outcome,
            string detail)
        {
            Outcome = outcome;
            Detail = detail;
        }

        internal TransientUnlockRestoreOutcome Outcome { get; }

        /// <summary>
        /// The fixed sentence of the refusal. Null on success.
        /// </summary>
        internal string Detail { get; }

        internal static TransientUnlockRestoreResult Succeeded()
        {
            return new TransientUnlockRestoreResult(
                TransientUnlockRestoreOutcome.Succeeded, null);
        }

        internal static TransientUnlockRestoreResult Refused(string detail)
        {
            return new TransientUnlockRestoreResult(
                TransientUnlockRestoreOutcome.Failed, detail);
        }
    }

    /// <summary>
    /// Restores one locked clone to its unlocked form in memory through
    /// <see cref="LockedMaterialReconstruction"/>. Implementations must
    /// never mutate the locked original. A Failed result carries the
    /// refusal sentence of the broken step (A8.2).
    /// </summary>
    internal delegate TransientUnlockRestoreResult
        TransientUnlockRestoreDelegate(Material lockedClone);
}
