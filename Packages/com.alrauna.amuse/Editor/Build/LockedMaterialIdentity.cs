using System;
using System.IO;
using System.Text;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The recognized Thry locked identity of one material's serialization.
    /// The classifier is pure: it reads the serialization facts it is given
    /// and touches no live Unity state, so tests drive it on literal inputs.
    /// <para>
    /// Locked identity demands two signals, and both must hold: the shader
    /// name starts with <see cref="LockedShaderNamePrefix"/>, and the
    /// <see cref="OptimizerEnabledPropertyName"/> float equals exactly one.
    /// Stale identity tags alone are never lock evidence, and the name prefix
    /// alone is never lock evidence. The classification also records the
    /// <c>OriginalShader</c>, <c>OriginalShaderGUID</c>, and
    /// <c>AllLockedGUIDS</c> tags and whether the generated shader asset is
    /// present, because later increments restore against those facts.
    /// </para>
    /// </summary>
    internal sealed class LockedMaterialIdentity
    {
        /// <summary>The locked generated shader name prefix Thry writes.</summary>
        internal const string LockedShaderNamePrefix = "Hidden/Locked/";

        /// <summary>The lock flag a locked material carries as a float.</summary>
        internal const string OptimizerEnabledPropertyName =
            "_ShaderOptimizerEnabled";

        /// <summary>The tag naming the original locked shader.</summary>
        internal const string OriginalShaderTagName = "OriginalShader";

        /// <summary>The tag carrying the original shader's asset GUID.</summary>
        internal const string OriginalShaderGuidTagName = "OriginalShaderGUID";

        /// <summary>
        /// The tag listing the generated GUIDs one lock cycle keeps alive.
        /// </summary>
        internal const string AllLockedGuidsTagName = "AllLockedGUIDS";

        internal bool IsLocked { get; }
        internal string OriginalShader { get; }
        internal string OriginalShaderGuid { get; }
        internal string AllLockedGuids { get; }
        internal bool HasGeneratedShaderAsset { get; }

        private LockedMaterialIdentity(
            bool isLocked,
            string originalShader,
            string originalShaderGuid,
            string allLockedGuids,
            bool hasGeneratedShaderAsset)
        {
            IsLocked = isLocked;
            OriginalShader = originalShader;
            OriginalShaderGuid = originalShaderGuid;
            AllLockedGuids = allLockedGuids;
            HasGeneratedShaderAsset = hasGeneratedShaderAsset;
        }

        /// <summary>
        /// The material serialization facts the classifier reads. Absent tags
        /// are null, exactly as the extraction produces them.
        /// </summary>
        internal sealed class Serialization
        {
            internal string ShaderName { get; }
            internal bool HasOptimizerEnabled { get; }
            internal float OptimizerEnabled { get; }
            internal string OriginalShaderTag { get; }
            internal string OriginalShaderGuidTag { get; }
            internal string AllLockedGuidsTag { get; }
            internal bool GeneratedShaderAssetPresent { get; }

            internal Serialization(
                string shaderName,
                bool hasOptimizerEnabled,
                float optimizerEnabled,
                string originalShaderTag,
                string originalShaderGuidTag,
                string allLockedGuidsTag,
                bool generatedShaderAssetPresent)
            {
                ShaderName = shaderName;
                HasOptimizerEnabled = hasOptimizerEnabled;
                OptimizerEnabled = optimizerEnabled;
                OriginalShaderTag = originalShaderTag;
                OriginalShaderGuidTag = originalShaderGuidTag;
                AllLockedGuidsTag = allLockedGuidsTag;
                GeneratedShaderAssetPresent = generatedShaderAssetPresent;
            }
        }

        /// <summary>
        /// Classifies one material serialization. The two required signals
        /// are the locked name prefix and the optimizer flag equal to one.
        /// The tags never decide the answer, so removed tags leave a locked
        /// material locked and stale tags leave an unlocked material
        /// unlocked.
        /// </summary>
        internal static LockedMaterialIdentity Classify(
            Serialization serialization)
        {
            if (serialization == null)
            {
                throw new ArgumentNullException(nameof(serialization));
            }

            var nameIsLocked =
                !string.IsNullOrEmpty(serialization.ShaderName) &&
                serialization.ShaderName.StartsWith(
                    LockedShaderNamePrefix, StringComparison.Ordinal);
            var flagIsLocked = serialization.HasOptimizerEnabled &&
                serialization.OptimizerEnabled == 1f;

            // Both required signals hold, or the material is not locked.
            // The tags never decide the answer: stale tags do not lock an
            // unlocked material, and removed tags do not unlock a locked
            // one.
            var isLocked = nameIsLocked && flagIsLocked;

            return new LockedMaterialIdentity(
                isLocked,
                serialization.OriginalShaderTag,
                serialization.OriginalShaderGuidTag,
                serialization.AllLockedGuidsTag,
                serialization.GeneratedShaderAssetPresent);
        }

        /// <summary>
        /// The selection pre-check for one live material that selection
        /// could not attest. It answers the named renderer refusal the
        /// recognized locked identity demands, or
        /// <see cref="RendererAnalysisRefusal.None"/> when the material
        /// stays on the generic path: a material that is not locked, a
        /// locked serialization that names no original shader, or a locked
        /// serialization whose original shader resolves and passes the
        /// pinned identity. It reads serialization state only. It never
        /// mutates the material, the shader, or any asset.
        /// </summary>
        internal static RendererAnalysisRefusal PreCheckRefusal(
            Material material)
        {
            if (material == null || material.shader == null)
            {
                return RendererAnalysisRefusal.None;
            }

            var identity = Classify(SerializationOf(material));
            if (!identity.IsLocked)
            {
                return RendererAnalysisRefusal.None;
            }

            if (identity.OriginalShaderGuid == null &&
                identity.OriginalShader == null)
            {
                // The orphan case: the serialization names no original
                // shader, so the 2026-09-19 destination stays.
                return RendererAnalysisRefusal.None;
            }

            return ResolvesToAttestedPoiyomi(identity.OriginalShaderGuid)
                ? RendererAnalysisRefusal.None
                : RendererAnalysisRefusal
                    .LockedPoiyomiOriginalShaderUnattested;
        }

        /// <summary>
        /// Whether one live material carries the recognized locked
        /// identity: both required signals hold, the locked name prefix
        /// and the optimizer flag equal to one. The transient unlock
        /// window's pre-check and swap-in read this. It reads live
        /// serialization state only and mutates nothing.
        /// </summary>
        internal static bool RecognizedLockedIdentity(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            return Classify(SerializationOf(material)).IsLocked;
        }

        /// <summary>
        /// Whether the live material's recorded original shader resolves
        /// through the AssetDatabase and passes the pinned Poiyomi
        /// identity. The transient unlock window's precondition calls
        /// this; it is the same conjunction <see
        /// cref="PreCheckRefusal"/> applies, so the two paths cannot pin
        /// differently. Reads live state only and mutates nothing.
        /// </summary>
        internal static bool OriginalShaderAttested(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            var identity = Classify(SerializationOf(material));
            if (identity.OriginalShaderGuid == null &&
                identity.OriginalShader == null)
            {
                return false;
            }

            return ResolvesToAttestedPoiyomi(identity.OriginalShaderGuid);
        }

        /// <summary>
        /// Reads the serialization facts of one live material: the shader
        /// name, the lock flag float, the three identity tags, and the
        /// generated shader asset presence. The tag read follows the
        /// material first and the shader tags second, because the locked
        /// generated shader carries the identity tags in its own tag block.
        /// An absent tag reads as null.
        /// </summary>
        private static Serialization SerializationOf(Material material)
        {
            var shader = material.shader;
            var hasOptimizerEnabled =
                material.HasProperty(OptimizerEnabledPropertyName);
            return new Serialization(
                shader.name,
                hasOptimizerEnabled,
                hasOptimizerEnabled
                    ? material.GetFloat(OptimizerEnabledPropertyName)
                    : 0f,
                TagOrNull(material, OriginalShaderTagName),
                TagOrNull(material, OriginalShaderGuidTagName),
                TagOrNull(material, AllLockedGuidsTagName),
                !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(shader)));
        }

        private static string TagOrNull(Material material, string tagName)
        {
            var value = material.GetTag(tagName, true, null);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Decides the original-shader precondition: the recorded GUID must
        /// resolve through the AssetDatabase to a shader asset that passes
        /// the pinned Poiyomi identity. An unresolvable GUID and a resolved
        /// but unattested shader both answer false, and both carry the same
        /// named refusal by design. Both mean the original shader this lock
        /// names is not usable.
        /// </summary>
        private static bool ResolvesToAttestedPoiyomi(string originalGuid)
        {
            Shader resolved = null;
            if (!string.IsNullOrEmpty(originalGuid))
            {
                var assetPath =
                    AssetDatabase.GUIDToAssetPath(originalGuid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    resolved =
                        AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                }
            }

            return resolved != null &&
                PassesPinnedPoiyomiIdentity(resolved);
        }

        /// <summary>
        /// The existing pinned Poiyomi identity, applied to the resolved
        /// original shader asset: exact pinned name, canonical GUID,
        /// package identity, and normalized source digest. The check
        /// reuses the production identity conjunction, so these pins cannot
        /// drift from the pins every other path applies. The schema check
        /// is outside the precondition's pin list, so the evidence carries
        /// the recorded schema state.
        /// </summary>
        private static bool PassesPinnedPoiyomiIdentity(Shader resolved)
        {
            var assetPath = AssetDatabase.GetAssetPath(resolved);
            var hasReadableSource = false;
            string normalizedHash = null;
            if (!string.IsNullOrEmpty(assetPath) && File.Exists(assetPath))
            {
                try
                {
                    normalizedHash = PoiyomiMaterialSemantics
                        .ComputeNormalizedSourceHash(
                            File.ReadAllText(assetPath, Encoding.UTF8));
                    hasReadableSource = true;
                }
                catch (IOException)
                {
                    hasReadableSource = false;
                }
                catch (UnauthorizedAccessException)
                {
                    hasReadableSource = false;
                }
            }

            var hasGuid = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                resolved, out var guid, out long _);
            var package = UnityEditor.PackageManager.PackageInfo
                .FindForAssetPath(assetPath);

            var evidence = new PoiyomiSourceEvidence(
                resolved.name,
                isLocked: false,
                hasReadableSource,
                hasGuid ? guid?.ToLowerInvariant() : null,
                normalizedHash,
                package != null,
                package?.name,
                package?.version,
                hasRequiredSchema: true);

            return PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                evidence, out _);
        }
    }
}
