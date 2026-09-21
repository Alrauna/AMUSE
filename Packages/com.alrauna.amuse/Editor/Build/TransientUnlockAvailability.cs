using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Alrauna.Amuse.Editor.Host;
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
    /// Locks a batch of unlocked clones again through the vendor seam. One
    /// call covers every open pair of a build. Implementations convert
    /// vendor exceptions to a Failed outcome; per-pair truth after the call
    /// comes from verification, never from this outcome.
    /// </summary>
    internal delegate TransientUnlockVendorOutcome TransientRelockDelegate(
        IReadOnlyList<Material> unlockedClones);

    /// <summary>
    /// The transient unlock window's machine-side availability: the Thry
    /// source-digest attestation, the reflection seam resolution, and the
    /// two production delegates built on that seam. Everything is cached
    /// per domain in keyed statics and never grows into an evidence cache.
    /// <para>
    /// All vendor contact sits behind <see cref="TransientUnlockDelegate"/>
    /// and <see cref="TransientRelockDelegate"/>, so tests script restore
    /// and re-lock outcomes and never install the vendor.
    /// </para>
    /// </summary>
    internal static class TransientUnlockAvailability
    {
        /// <summary>The MonoScript asset name the Thry lock tool ships as.</summary>
        internal const string OptimizerScriptName = "ShaderOptimizer";

        /// <summary>The full name of the vendor type the seam resolves.</summary>
        internal const string OptimizerTypeFullName =
            "Thry.ThryEditor.ShaderOptimizer";

        /// <summary>The vendor restore entry point.</summary>
        internal const string UnlockMethodName = "UnlockMaterials";

        /// <summary>The vendor re-lock entry point.</summary>
        internal const string LockMethodName = "LockMaterials";

        /// <summary>
        /// The pinned <c>ShaderOptimizer</c> source digests, lowercase hex
        /// SHA-256, one per attested tooling pin. The set is deliberately
        /// empty today: the two pin values, one for the 9.3.64 embedded
        /// tool and one for the 2.74.2 standalone tool, await recording
        /// against the verified vendor archives in the lab session. The
        /// archive digests themselves are already pinned in the recorded
        /// characterization, so the lab recording only hashes the named
        /// source file inside each verified archive.
        /// </summary>
        /// <remarks>
        /// Fail closed. With no digest pinned, no machine ever attests, so
        /// every recognized locked material refuses with
        /// <see cref="RendererAnalysisRefusal.LockedPoiyomiThryUnattested"/>
        /// before any clone exists, and the shipped behavior matches the
        /// 2026-09-19 sentinel path. Recording a digest is a reviewed
        /// change to this one table and nothing else.
        /// </remarks>
        internal static readonly string[] PinnedSourceDigests =
            new string[] { };

        /// <summary>
        /// The per-build consent subject for the unlock window, in neutral
        /// language. Short sentences. It names the window, the active
        /// re-lock, the render-equivalence basis, the fallback behavior,
        /// the fact that the upload-time lock is not load-bearing for
        /// swapped materials, the post-build re-processing disclosure,
        /// and the two order-100 residues of spec section 9.
        /// </summary>
        internal const string WindowConsentSubject =
            "Transient unlock window: AMUSE unlocks the locked materials " +
            "of this avatar for the duration of the build and locks the " +
            "swapped copies again before the build ships. The recorded " +
            "round trip characterization is the render-equivalence basis. " +
            "When the second lock fails, AMUSE puts your original locked " +
            "materials back in their slots. An animation may still apply " +
            "an AMUSE-generated material to a slot. A mesh split AMUSE " +
            "generated may also remain. The upload-time lock is not " +
            "load-bearing for materials AMUSE swapped. Materials that " +
            "AMUSE did not swap still depend on the upload-time lock " +
            "callback exactly as they did without AMUSE. The VRChat SDK " +
            "can abort an upload for a malformed descriptor. That abort " +
            "message is not about AMUSE. Tools that run after the NDMF " +
            "build may re-process the re-locked output.";

        // Per-domain caches. The three states are 0 unknown, 1 negative,
        // 2 positive. Unity never reloads the assembly inside one domain,
        // so a keyed static is the whole cache contract.
        private static int sourceAttestedCache;
        private static int seamResolvedCache;
        private static ResolvedSeam resolvedSeamCache;

        /// <summary>
        /// Clears both per-domain caches. Tests call this; production never
        /// needs to, because the inputs it caches cannot change inside one
        /// domain.
        /// </summary>
        internal static void ClearCache()
        {
            sourceAttestedCache = 0;
            seamResolvedCache = 0;
            resolvedSeamCache = null;
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
        /// Whether the reflection seam resolves with the expected method
        /// shapes: static, boolean return, first parameter assignable from
        /// <c>IEnumerable&lt;Material&gt;</c>, every later parameter
        /// optional. The answer is cached per domain.
        /// </summary>
        internal static bool SeamResolved()
        {
            if (seamResolvedCache == 0)
            {
                seamResolvedCache = TryResolveSeam(out _) ? 2 : 1;
            }

            return seamResolvedCache == 2;
        }

        /// <summary>
        /// Whether the window's vendor side is usable on this machine:
        /// the source attests and the seam resolves. This is the combined
        /// section 5 precondition, and its negative is exactly the named
        /// <see cref="RendererAnalysisRefusal.LockedPoiyomiThryUnattested"/>
        /// refusal.
        /// </summary>
        internal static bool WindowVendorReady()
        {
            return ThrySourceAttested() && SeamResolved();
        }

        /// <summary>
        /// The production restore delegate, or null when the seam does not
        /// resolve. The delegate invokes the vendor restore on a list
        /// holding only the given clone, converts the vendor's boolean and
        /// every vendor exception to the named outcome, and lets every
        /// non-vendor exception propagate as the AMUSE defect it is.
        /// </summary>
        internal static TransientUnlockDelegate CreateProductionRestore()
        {
            if (!TryResolveSeam(out var seam))
            {
                return null;
            }

            return clone => InvokeVendor(seam.Unlock, new Material[] { clone })
                ? TransientUnlockVendorOutcome.Succeeded
                : TransientUnlockVendorOutcome.Failed;
        }

        /// <summary>
        /// The production re-lock delegate, or null when the seam does not
        /// resolve. One call covers the whole batch, exactly as the window
        /// close requires.
        /// </summary>
        internal static TransientRelockDelegate CreateProductionRelock()
        {
            if (!TryResolveSeam(out var seam))
            {
                return null;
            }

            return clones => InvokeVendor(seam.Relock, clones.ToArray())
                ? TransientUnlockVendorOutcome.Succeeded
                : TransientUnlockVendorOutcome.Failed;
        }

        /// <summary>
        /// The renderer-scope pre-check for the Thry precondition. It
        /// answers <see cref="RendererAnalysisRefusal.LockedPoiyomiThryUnattested"/>
        /// when the renderer holds at least one recognized locked material
        /// whose recorded original shader attests while the vendor side is
        /// not ready. Every other case answers None, so a locked material
        /// whose original does not attest keeps the earlier named refusal
        /// the selection pre-check already produces. Reads live state,
        /// mutates nothing, and clones nothing: the check runs before any
        /// clone step by contract. The optional seam substitutes only the
        /// machine-readiness answer for tests; null keeps the production
        /// <see cref="WindowVendorReady"/>.
        /// </summary>
        internal static RendererAnalysisRefusal RendererPreCheckRefusal(
            Renderer renderer,
            Func<Material, bool> originalShaderAttested,
            Func<bool> vendorReady = null)
        {
            if (renderer == null)
            {
                return RendererAnalysisRefusal.None;
            }

            var attests = originalShaderAttested ??
                LockedMaterialIdentity.OriginalShaderAttested;
            var ready = vendorReady ?? WindowVendorReady;

            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null ||
                    !LockedMaterialIdentity.RecognizedLockedIdentity(
                        material) ||
                    !attests(material))
                {
                    continue;
                }

                // One eligible locked material is enough to consult the
                // vendor-side precondition, and its negative refuses the
                // renderer before any clone exists.
                if (!ready())
                {
                    return RendererAnalysisRefusal
                        .LockedPoiyomiThryUnattested;
                }

                return RendererAnalysisRefusal.None;
            }

            return RendererAnalysisRefusal.None;
        }

        /// <summary>
        /// Whether any assigned material makes the unlock window eligible
        /// on this build: a recognized locked identity whose original
        /// shader attests, on a machine where the vendor side is ready.
        /// Only an eligible build asks for the window's per-build consent.
        /// The optional seam substitutes only the machine-readiness answer
        /// for tests; null keeps the production <see cref="WindowVendorReady"/>.
        /// </summary>
        internal static bool WindowEligibleForConsent(
            IEnumerable<Material> assignedMaterials,
            Func<Material, bool> originalShaderAttested,
            Func<bool> vendorReady = null)
        {
            var attests = originalShaderAttested ??
                LockedMaterialIdentity.OriginalShaderAttested;
            var ready = vendorReady ?? WindowVendorReady;

            foreach (var material in assignedMaterials)
            {
                if (material == null ||
                    !LockedMaterialIdentity.RecognizedLockedIdentity(
                        material) ||
                    !attests(material))
                {
                    continue;
                }

                return ready();
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
                // Fail closed while the pin table awaits the lab
                // recording. Nothing else runs, so no machine attests.
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

        private static bool TryResolveSeam(out ResolvedSeam seam)
        {
            if (seamResolvedCache == 2 && resolvedSeamCache != null)
            {
                seam = resolvedSeamCache;
                return true;
            }

            seam = ResolveSeam();
            if (seam == null)
            {
                return false;
            }

            resolvedSeamCache = seam;
            return true;
        }

        private static ResolvedSeam ResolveSeam()
        {
            Type optimizerType = null;
            foreach (var assembly in AppDomain.CurrentDomain
                         .GetAssemblies())
            {
                try
                {
                    var candidates = assembly.GetTypes();
                    foreach (var candidate in candidates)
                    {
                        if (string.Equals(
                                candidate.FullName,
                                OptimizerTypeFullName,
                                StringComparison.Ordinal))
                        {
                            optimizerType = candidate;
                            break;
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // An assembly with unloadable types cannot be the
                    // vendor home this domain can invoke into. Skip it.
                    continue;
                }

                if (optimizerType != null)
                {
                    break;
                }
            }

            if (optimizerType == null)
            {
                return null;
            }

            var unlock = ResolveVendorMethod(optimizerType, UnlockMethodName);
            var relock = ResolveVendorMethod(optimizerType, LockMethodName);
            if (unlock == null || relock == null)
            {
                return null;
            }

            return new ResolvedSeam(unlock, relock);
        }

        /// <summary>
        /// The expected method shape, from the live-verified vendor
        /// signatures: public static, boolean return, first parameter
        /// assignable from <c>IEnumerable&lt;Material&gt;</c>, and every
        /// later parameter optional, because the verified signature takes
        /// an optional progress argument. The first name match with this
        /// shape wins; name matches without the shape never do.
        /// </summary>
        private static MethodInfo ResolveVendorMethod(
            Type optimizerType, string methodName)
        {
            foreach (var method in optimizerType.GetMethods(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (!string.Equals(
                        method.Name,
                        methodName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (method.ReturnType != typeof(bool) ||
                    parameters.Length < 1 ||
                    !typeof(IEnumerable<Material>).IsAssignableFrom(
                        parameters[0].ParameterType) ||
                    parameters.Skip(1).Any(p => !p.IsOptional))
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static bool InvokeVendor(
            MethodInfo method, Material[] materials)
        {
            var parameters = method.GetParameters();
            var arguments = new object[parameters.Length];
            arguments[0] = materials;
            for (var index = 1; index < arguments.Length; index++)
            {
                arguments[index] = Type.Missing;
            }

            try
            {
                return (bool)method.Invoke(null, arguments);
            }
            catch (TargetInvocationException)
            {
                // A vendor exception is a named vendor failure, never a
                // reason to widen the refusal or abort the window. The
                // caller records the named refusal and takes the fallback.
                return false;
            }
        }

        private sealed class ResolvedSeam
        {
            internal MethodInfo Unlock { get; }
            internal MethodInfo Relock { get; }

            internal ResolvedSeam(MethodInfo unlock, MethodInfo relock)
            {
                Unlock = unlock;
                Relock = relock;
            }
        }
    }
}
