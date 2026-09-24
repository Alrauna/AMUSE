using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Thry.ThryEditor
{
    /// <summary>
    /// Stand-in for the vendor lock tool type. The declaring full name
    /// matches the verified vendor type on purpose, so a reintroduced
    /// reflection seam would resolve this type. No production path
    /// resolves or calls this type since the 2026-09-24 vendor lock
    /// handover. The suite asserts <c>LockCallCount</c> and
    /// <c>UnlockCallCount</c> stay zero, and those counters are the
    /// tripwire against any reintroduced vendor call. The method
    /// signatures mirror the live-verified vendor signatures: public
    /// static boolean, an IEnumerable of Material first. No vendor
    /// source exists here; every behavior is a scripted stand-in.
    /// </summary>
    internal static class ShaderOptimizer
    {
        /// <summary>
        /// The scripted vendor behaviors the falsifiers need. Real mirrors
        /// the recorded mechanism on the stand-in shaders; every other mode
        /// is one named wrong or degenerate vendor outcome.
        /// </summary>
        internal enum Mode
        {
            /// <summary>Restore rebinds the recorded locked shader's
            /// original by tag, and the lock rebinds the recorded locked
            /// shader for a restored clone or the locked stand-in shader
            /// for a generated output the restore never saw. Flags
            /// follow.</summary>
            Real,

            /// <summary>The restore reports success and changes nothing:
            /// the no-op restore of falsifier F1.</summary>
            NoOpRestore,

            /// <summary>The restore rebinds the shader but leaves the lock
            /// flag at one: the partial restore of F2.</summary>
            PartialRestore,

            /// <summary>The restore skips every material without an asset
            /// path, exactly as the vendor filter does. An unpersisted
            /// clone restores as a no-op.</summary>
            SkipUnpersisted,

            /// <summary>The re-lock throws, like the reproduced throwing
            /// vendor callback class. The production delegate converts the
            /// exception to a Failed outcome.</summary>
            ThrowOnRelock,
        }

        internal static Mode CurrentMode = Mode.Real;

        internal static int LockCallCount;

        internal static int UnlockCallCount;

        internal static void Reset()
        {
            CurrentMode = Mode.Real;
            LockCallCount = 0;
            UnlockCallCount = 0;
            lockedShaderByMaterial.Clear();
        }

        // The locked shader each restored material wore at unlock time, so
        // the scripted lock can rebind the same generated shader, exactly
        // as the vendor's own lock rebinds the generated shader by tag.
        private static readonly Dictionary<Material, Shader>
            lockedShaderByMaterial =
                new Dictionary<Material, Shader>();

        /// <summary>
        /// A wrong-shape decoy sharing the lock method name. The seam must
        /// pick by verified shape, never by name order alone.
        /// </summary>
        public static void LockMaterials(string decoy)
        {
        }

        public static bool LockMaterials(IEnumerable<Material> materials)
        {
            LockCallCount++;
            if (CurrentMode == Mode.ThrowOnRelock)
            {
                throw new System.InvalidOperationException(
                    "The stand-in vendor lock threw, as scripted.");
            }

            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (lockedShaderByMaterial.TryGetValue(
                        material, out var lockedShader) &&
                    lockedShader != null)
                {
                    // A restored clone rebinds the recorded locked
                    // shader, exactly as the vendor's own lock rebinds
                    // the generated shader by tag.
                    material.shader = lockedShader;
                }
                else
                {
                    // A material the restore never recorded is a
                    // generated output the transformation created after
                    // the unlock; the vendor lock rebinds its locked
                    // form the same way. A source clone the restore
                    // skipped never reaches this call: its swap-in
                    // verification removes the pair before the window
                    // close, so an unrecorded batch member is never a
                    // skipped source clone.
                    var lockedForm = LockedFormShader();
                    if (lockedForm != null)
                    {
                        material.shader = lockedForm;
                    }
                }

                SetFlag(material, 1f);
            }

            return true;
        }

        public static bool UnlockMaterials(IEnumerable<Material> materials)
        {
            UnlockCallCount++;
            if (CurrentMode == Mode.NoOpRestore)
            {
                return true;
            }

            foreach (var material in materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (CurrentMode == Mode.SkipUnpersisted &&
                    string.IsNullOrEmpty(
                        AssetDatabase.GetAssetPath(material)))
                {
                    // The vendor lock filter skips materials without an
                    // asset path, so the restore runs without effect for
                    // exactly those materials.
                    continue;
                }

                lockedShaderByMaterial[material] = material.shader;

                if (CurrentMode == Mode.PartialRestore)
                {
                    var original = ShaderForRecordedOriginal(material);
                    if (original != null)
                    {
                        material.shader = original;
                    }

                    // The partial restore leaves the lock flag at one.
                    continue;
                }

                var restored = ShaderForRecordedOriginal(material);
                if (restored != null)
                {
                    material.shader = restored;
                }

                SetFlag(material, 0f);
            }

            return true;
        }

        private static Shader ShaderForRecordedOriginal(Material material)
        {
            var recordedName = material.GetTag("OriginalShader", true, null);
            return string.IsNullOrEmpty(recordedName)
                ? null
                : Shader.Find(recordedName);
        }

        // The locked stand-in shader the fixtures recognize as the
        // locked form, named by the one constant the locked-material
        // fixtures also read.
        private static Shader LockedFormShader()
        {
            return Shader.Find(
                Alrauna.Amuse.Tests.Editor.Build
                    .TransientUnlockTestLifecycle.LockedStandInShaderName);
        }

        private static void SetFlag(Material material, float value)
        {
            const string flagName = "_ShaderOptimizerEnabled";
            if (material.HasProperty(flagName))
            {
                material.SetFloat(flagName, value);
            }
        }
    }
}
