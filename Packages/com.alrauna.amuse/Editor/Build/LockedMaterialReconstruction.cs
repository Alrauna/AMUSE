using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The AMUSE-owned in-memory reconstruction of one locked material
    /// onto its recorded original shader. It replaces the vendor unlock
    /// for the swapped source pair, so a build that admits locked
    /// materials runs no vendor call, writes no file, refreshes nothing,
    /// and leaves the on-disk lock state untouched.
    /// <para>
    /// The order follows A5.2 and A8.1: the recorded identity resolves
    /// the original shader, the generated shader must compile, both texts
    /// are read through the one shared reader, the aligner runs through
    /// the session memo, the texture identity of the clone and the
    /// locked original must agree through the rename mapping, every
    /// value of the locked clone is read through
    /// the generated-shader view while the clone still has the generated
    /// shader, the reproduction map builds the plan, the shader swaps,
    /// the plan is written, the lock flag clears, and the consistency
    /// checks run on the finished clone. Every refusal returns its fixed
    /// sentence in the result detail. A sentence never names a path, a
    /// GUID, or a property value.
    /// </para>
    /// </summary>
    internal static class LockedMaterialReconstruction
    {
        private const string NameTagMissingDetail =
            "the recorded original shader name tag is missing";

        private const string GuidTagMissingDetail =
            "the recorded original shader GUID tag is missing";

        private const string GuidUnresolvableDetail =
            "the recorded original shader GUID does not resolve to a " +
            "shader asset";

        private const string AttestationFailedDetail =
            "the resolved original shader did not pass attestation";

        private const string FlagPropertyMissingDetail =
            "the resolved original shader does not declare the " +
            "optimizer flag property";

        private const string CompileErrorDetail =
            "The generated shader of the locked material does not " +
            "compile.";

        private const string OriginalTextUnreadableDetail =
            "The recorded original shader file is not readable.";

        private const string GeneratedTextUnreadableDetail =
            "The generated locked shader file is not readable.";

        /// <summary>
        /// The fixed refusal sentence of the texture identity fact
        /// (TX3). The sentence names no property, no texture, and no
        /// path. The tests read this constant, so the pinned text lives
        /// in one place.
        /// </summary>
        internal const string TextureIdentityDetail =
            "A texture of the rebuilt material is not the texture of " +
            "the locked material.";

        /// <summary>
        /// Reconstructs the unlocked material on <paramref name="clone"/>
        /// in memory. <paramref name="lockedOriginal"/> is the locked
        /// material the clone was instantiated from. The method resolves
        /// and loads the recorded original shader asset and reads the two
        /// shader files and nothing else external: it writes no file,
        /// mutates no asset, and triggers no refresh, so the next build
        /// of the same input starts from the same on-disk state. The
        /// locked original is read only.
        /// </summary>
        internal static TransientUnlockRestoreResult TryApply(
            Material clone,
            Material lockedOriginal,
            Func<Shader, bool> originalShaderAttested)
        {
            // The recorded identity is the only link back to the original
            // shader; the locked name prefix and the reduced declared
            // table make every other route ambiguous.
            var recordedName = clone.GetTag(
                LockedMaterialIdentity.OriginalShaderTagName, true, null);
            if (string.IsNullOrEmpty(recordedName))
            {
                return TransientUnlockRestoreResult.Refused(
                    NameTagMissingDetail);
            }

            var recordedGuid = clone.GetTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                true,
                null);
            if (string.IsNullOrEmpty(recordedGuid))
            {
                return TransientUnlockRestoreResult.Refused(
                    GuidTagMissingDetail);
            }

            // Read-only resolution through the AssetDatabase: the GUID is
            // the pinned binding the lock recorded, and the name alone is
            // not trusted for the asset lookup.
            var resolvedPath =
                AssetDatabase.GUIDToAssetPath(recordedGuid);
            var resolved = string.IsNullOrEmpty(resolvedPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Shader>(resolvedPath);
            if (resolved == null)
            {
                return TransientUnlockRestoreResult.Refused(
                    GuidUnresolvableDetail);
            }

            if (originalShaderAttested == null ||
                !originalShaderAttested(resolved))
            {
                return TransientUnlockRestoreResult.Refused(
                    AttestationFailedDetail);
            }

            // A generated shader that does not compile bakes nothing the
            // aligner can trust, so the pair refuses before any read.
            if (ShaderUtil.ShaderHasError(clone.shader))
            {
                return TransientUnlockRestoreResult.Refused(
                    CompileErrorDetail);
            }

            var generatedText = LockedMaterialIdentity.ReadOriginalText(
                clone.shader);
            if (generatedText == null)
            {
                return TransientUnlockRestoreResult.Refused(
                    GeneratedTextUnreadableDetail);
            }

            // The one shared reader serves attestation and alignment, so
            // both see the same bytes (Constraints).
            var originalText = LockedMaterialIdentity.ReadOriginalText(
                resolved);
            if (originalText == null)
            {
                return TransientUnlockRestoreResult.Refused(
                    OriginalTextUnreadableDetail);
            }

            var properties = LockedSourceAlignmentMemo.PropertiesOf(
                resolved, originalText);
            var aligned = LockedSourceAlignmentMemo.GetOrAlign(
                clone.shader, resolved, properties);
            if (!aligned.Succeeded)
            {
                return TransientUnlockRestoreResult.Refused(
                    aligned.RefusalDetail);
            }

            // The locked values are read while the clone still has the
            // generated shader, so the renamed saved names stay
            // reachable (A5.2).
            var lockedView = new UnityLockedMaterialValues(clone);

            // The locked original snapshot rides beside the clone
            // snapshot (TX3). Both snapshots are taken before the plan
            // builds, so the identity fact reads one lock state.
            var originalView =
                new UnityLockedMaterialValues(lockedOriginal);
            var identityRefusal = TextureIdentityRefusal(
                aligned.Source, properties, lockedView, originalView);
            if (identityRefusal != null)
            {
                return TransientUnlockRestoreResult.Refused(
                    identityRefusal);
            }

            var reproduction = LockedSourceReproduction.Build(
                aligned.Source, properties, lockedView);
            if (!reproduction.Succeeded)
            {
                return TransientUnlockRestoreResult.Refused(
                    reproduction.RefusalDetail);
            }

            clone.shader = resolved;

            ApplyPlan(clone, reproduction.Plan, properties);

            // The cleared flag is part of the verification contract, and
            // a shader without the property cannot carry it, so a missing
            // declaration refuses instead of writing an orphan value.
            if (!clone.HasProperty(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName))
            {
                return TransientUnlockRestoreResult.Refused(
                    FlagPropertyMissingDetail);
            }

            clone.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 0f);

            var consistency = LockedSourceReproduction.CheckConsistency(
                aligned.Source,
                properties,
                new UnityLockedMaterialValues(clone));
            if (consistency != null)
            {
                return TransientUnlockRestoreResult.Refused(consistency);
            }

            return TransientUnlockRestoreResult.Succeeded();
        }

        /// <summary>
        /// Compares the texture identity of the clone and the locked
        /// original (TX3). The walk uses the typed property table. A
        /// renamed property is read under its renamed name on both
        /// sides. The first mismatch refuses with the fixed identity
        /// sentence. The method returns null on success.
        /// </summary>
        private static string TextureIdentityRefusal(
            AlignedLockedSource source,
            IReadOnlyList<LockedSourceProperty> properties,
            ILockedMaterialValues cloneView,
            ILockedMaterialValues originalView)
        {
            foreach (var property in properties)
            {
                if (property.Type != ShaderPropertyType.Texture)
                {
                    continue;
                }

                var readName = property.Name;
                if (source.States.TryGetValue(property.Name, out var state)
                    && state == LockedPropertyState.Renamed
                    && source.RenamedNames.TryGetValue(
                        property.Name, out var renamedName))
                {
                    readName = renamedName;
                }

                var cloneToken =
                    cloneView.TryGetTexture(readName, out var cloneTexture)
                        ? cloneTexture.IdentityToken
                        : null;
                var originalToken = originalView.TryGetTexture(
                        readName, out var originalTexture)
                    ? originalTexture.IdentityToken
                    : null;

                // An absent token on both sides is agreement. Most
                // slots of the table carry no texture on either side,
                // and the build treats that as equal on purpose.
                if (cloneToken == null && originalToken == null)
                {
                    continue;
                }

                if (!string.Equals(
                        cloneToken,
                        originalToken,
                        StringComparison.Ordinal))
                {
                    return TextureIdentityDetail;
                }
            }

            return null;
        }

        /// <summary>
        /// Writes the reproduction plan on the swapped clone (A5). Every
        /// write is by original property name. The keyword list is
        /// assigned whole, because it replaces the whole set (A5.9).
        /// </summary>
        private static void ApplyPlan(
            Material clone,
            LockedReproductionPlan plan,
            IReadOnlyList<LockedSourceProperty> properties)
        {
            foreach (var entry in plan.FloatWrites)
            {
                if (IsIntegerProperty(properties, entry.Key))
                {
                    clone.SetInteger(
                        entry.Key,
                        Mathf.RoundToInt(entry.Value));
                }
                else
                {
                    clone.SetFloat(entry.Key, entry.Value);
                }
            }

            foreach (var entry in plan.VectorWrites)
            {
                clone.SetVector(entry.Key, entry.Value);
            }

            foreach (var entry in plan.ColorWrites)
            {
                clone.SetColor(entry.Key, entry.Value);
            }

            foreach (var entry in plan.TextureWrites)
            {
                clone.SetTexture(
                    entry.Key,
                    UnityLockedMaterialValues.TextureFromToken(
                        entry.Value.IdentityToken));
            }

            foreach (var entry in plan.TextureScaleOffsetWrites)
            {
                clone.SetTextureScale(
                    entry.Key,
                    new Vector2(entry.Value.x, entry.Value.y));
                clone.SetTextureOffset(
                    entry.Key,
                    new Vector2(entry.Value.z, entry.Value.w));
            }

            clone.shaderKeywords = plan.Keywords.ToArray();
            clone.renderQueue = plan.RenderQueue;
            if (!string.IsNullOrEmpty(plan.RenderTypeTag))
            {
                clone.SetOverrideTag("RenderType", plan.RenderTypeTag);
            }

            if (!string.IsNullOrEmpty(plan.VrcFallbackTag))
            {
                clone.SetOverrideTag("VRCFallback", plan.VrcFallbackTag);
            }
        }

        /// <summary>
        /// Whether one original property is declared as an integer, so
        /// the write goes through the integer setter and reads back as an
        /// integer.
        /// </summary>
        private static bool IsIntegerProperty(
            IReadOnlyList<LockedSourceProperty> properties, string name)
        {
            foreach (var property in properties)
            {
                if (property.Name == name)
                {
                    return property.Type == ShaderPropertyType.Int;
                }
            }

            return false;
        }
    }
}
