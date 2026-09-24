using System;
using System.Collections.Generic;
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
    /// Restores one locked clone to its unlocked form in memory through
    /// <see cref="LockedMaterialReconstruction"/>. Implementations must
    /// never mutate the locked original.
    /// </summary>
    internal delegate TransientUnlockRestoreOutcome
        TransientUnlockRestoreDelegate(Material lockedClone);

    /// <summary>
    /// The transient unlock window's machine-side availability has two
    /// members. <see cref="CreateProductionRestore"/> returns the
    /// in-memory production restore. The restore reconstructs the
    /// unlocked form through <see cref="LockedMaterialReconstruction"/>.
    /// <see cref="WindowEligibleForConsent"/> is the window admission
    /// gate on original-shader attestation.
    /// <para>
    /// No build path consults a vendor answer. The restore runs the
    /// in-memory reconstruction through <see
    /// cref="TransientUnlockRestoreDelegate"/>, and the window opens on
    /// the original-shader attestation gates alone.
    /// </para>
    /// </summary>
    internal static class TransientUnlockAvailability
    {
        /// <summary>
        /// The production restore delegate. The delegate reconstructs
        /// the unlocked form in memory through <see
        /// cref="LockedMaterialReconstruction.TryApply"/> and never
        /// resolves or invokes the vendor unlock seam, so an admitted
        /// build runs no vendor call and writes nothing for the
        /// restore. The window's own admission gates apply the
        /// original-shader attestation before any clone exists, so the
        /// reconstruction starts from an attested original. The
        /// conversion is unchanged: a completed reconstruction is
        /// <see cref="TransientUnlockRestoreOutcome.Succeeded"/>, every
        /// named refusal is <see cref="TransientUnlockRestoreOutcome.Failed"/>.
        /// </summary>
        internal static TransientUnlockRestoreDelegate CreateProductionRestore()
        {
            return clone => LockedMaterialReconstruction.TryApply(
                clone,
                _ => true,
                out _)
                ? TransientUnlockRestoreOutcome.Succeeded
                : TransientUnlockRestoreOutcome.Failed;
        }

        /// <summary>
        /// Whether any assigned material makes the unlock window eligible
        /// on this build: a recognized locked identity whose original
        /// shader attests. V2 and V4 passed live observation on
        /// 2026-09-21, so an eligible build grants the window without
        /// per-build consent per spec section 12. No vendor answer
        /// narrows the answer: the restore reconstructs in memory.
        /// </summary>
        internal static bool WindowEligibleForConsent(
            IEnumerable<Material> assignedMaterials,
            Func<Material, bool> originalShaderAttested)
        {
            var attests = originalShaderAttested ??
                LockedMaterialIdentity.OriginalShaderAttested;

            foreach (var material in assignedMaterials)
            {
                if (material == null ||
                    !LockedMaterialIdentity.RecognizedLockedIdentity(
                        material) ||
                    !attests(material))
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
