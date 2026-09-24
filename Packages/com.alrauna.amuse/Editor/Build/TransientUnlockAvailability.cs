using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The outcome one vendor call reports. The vendor's own boolean return
    /// and its exceptions both fold into this named outcome, so no caller
    /// ever sees a vendor exception directly. A Failed outcome is an
    /// expected, named vendor failure, never an AMUSE defect.
    /// </summary>
    internal enum TransientUnlockVendorOutcome
    {
        Succeeded,
        Failed,
    }

    /// <summary>
    /// Restores one locked clone to its unlocked form through the vendor
    /// seam. Implementations convert vendor exceptions to a Failed outcome
    /// and must never mutate the locked original.
    /// </summary>
    internal delegate TransientUnlockVendorOutcome TransientUnlockDelegate(
        Material lockedClone);

    /// <summary>
    /// The transient unlock window's machine-side availability is the
    /// in-memory production restore. The class also retains the Thry
    /// source-digest attestation block as recorded evidence; it is no
    /// live gate input (see the remark on
    /// <see cref="PinnedSourceDigests"/>). The attestation answer is
    /// cached per domain in a keyed static and never grows into an
    /// evidence cache.
    /// <para>
    /// No build path consults a vendor answer. The restore runs the
    /// in-memory reconstruction through <see cref="TransientUnlockDelegate"/>,
    /// and the window opens on the original-shader attestation gates
    /// alone.
    /// </para>
    /// </summary>
    internal static class TransientUnlockAvailability
    {
        /// <summary>The MonoScript asset name the Thry lock tool ships as.</summary>
        internal const string OptimizerScriptName = "ShaderOptimizer";

        /// <summary>
        /// The pinned <c>ShaderOptimizer</c> source digests, lowercase hex
        /// SHA-256, one per attested tooling pin. Recorded on 2026-09-21
        /// from freshly fetched vendor archives whose archive SHA-256
        /// matched the pins in the 2026-09-19 characterization: the first
        /// entry is the <c>Editor/ShaderOptimizer.cs</c> embedded in
        /// <c>com.poiyomi.toon</c> 9.3.64, the second is the one shipped
        /// in <c>com.poiyomi.thryeditor</c> 2.74.2.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Retained as recorded attestation evidence with no production
        /// consumer since the 2026-09-24 vendor lock handover. The pinned
        /// digests cannot be faithfully reconstructed after deletion, so
        /// the record stays.
        /// </para>
        /// <para>
        /// Fail closed. An empty table, zero locator hits, several hits,
        /// and an unpinned digest all attest nothing. Recording a digest
        /// is a reviewed change to this one table and nothing else.
        /// </para>
        /// </remarks>
        internal static readonly string[] PinnedSourceDigests =
            new string[]
            {
                // 9.3.64 embedded tooling, era 1.
                "9000377ab486863e20d25b8bd025863c7590354ea3cdb1a5ca493d8e5045f3e5",
                // 2.74.2 standalone tooling, era 2.
                "7c1ffe78c872288ec605b5bd742467401c952562701aa171fead0df4ae1883ee",
            };

        // Per-domain cache. The three states are 0 unknown, 1 negative,
        // 2 positive. Unity never reloads the assembly inside one domain,
        // so a keyed static is the whole cache contract.
        private static int sourceAttestedCache;

        /// <summary>
        /// Clears the per-domain cache. Tests call this; production never
        /// needs to, because the input it caches cannot change inside one
        /// domain.
        /// </summary>
        internal static void ClearCache()
        {
            sourceAttestedCache = 0;
        }

        /// <summary>
        /// Whether the installed <c>ShaderOptimizer</c> source attests.
        /// The rule: locate the MonoScript assets named
        /// <see cref="OptimizerScriptName"/>, require exactly one hit, hash
        /// the file, and compare against <see cref="PinnedSourceDigests"/>.
        /// Zero hits, several hits, an unreadable file, and an unpinned
        /// digest all answer false. The answer is cached per domain.
        /// </summary>
        internal static bool ThrySourceAttested()
        {
            if (sourceAttestedCache == 0)
            {
                sourceAttestedCache = ComputeSourceAttested() ? 2 : 1;
            }

            return sourceAttestedCache == 2;
        }

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
        /// <see cref="TransientUnlockVendorOutcome.Succeeded"/>, every
        /// named refusal is <see cref="TransientUnlockVendorOutcome.Failed"/>.
        /// </summary>
        internal static TransientUnlockDelegate CreateProductionRestore()
        {
            return clone => LockedMaterialReconstruction.TryApply(
                clone,
                _ => true,
                out _)
                ? TransientUnlockVendorOutcome.Succeeded
                : TransientUnlockVendorOutcome.Failed;
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

        /// <summary>
        /// The lowercase hex SHA-256 digest of one file's bytes. Internal
        /// so the digest rule stays testable without a vendor install.
        /// </summary>
        internal static string SourceDigestOfFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static bool ComputeSourceAttested()
        {
            if (PinnedSourceDigests.Length == 0)
            {
                // Fail closed when nothing is pinned. Nothing else runs,
                // so no machine attests.
                return false;
            }

            var hits = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(
                         OptimizerScriptName + " t:MonoScript"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = string.IsNullOrEmpty(path)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null &&
                    string.Equals(
                        script.name,
                        OptimizerScriptName,
                        StringComparison.Ordinal))
                {
                    hits.Add(path);
                }
            }

            if (hits.Count != 1)
            {
                return false;
            }

            try
            {
                var digest = SourceDigestOfFile(hits[0]);
                return PinnedSourceDigests.Any(pinned =>
                    string.Equals(
                        pinned,
                        digest,
                        StringComparison.Ordinal));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
