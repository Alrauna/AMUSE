using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The pinned lilToon opaque target: the canonical Opaque counterpart,
    /// the eighteen-property recipe that writes it, and the transient
    /// validated clone that normalizes one attested source onto it.
    /// <para>
    /// Semantics describe output facts; conversion decides mutation. This
    /// class therefore models render state that the cutout alpha semantics
    /// deliberately do not, and
    /// <see cref="LilToonCutoutMaterialSemantics"/> is unchanged.
    /// </para>
    /// <para>
    /// The recipe below is a transcription of B1 §9: the eighteen scalar
    /// writes were measured 2026-08-30 from the installed jp.lilxyzw.liltoon
    /// 2.3.4 package with exact read-back (spec §9.1). lilToon regenerates its
    /// shader assets from per-project settings, so the measurements came from
    /// the installed package, not from an upstream checkout.
    /// <strong>Never re-derive this tuple from the upstream git tag.</strong>
    /// Changing the attestation pins (<see cref="LilToonSourceAttestation"/>,
    /// whose identity the recipe's values are measured against) requires
    /// re-measuring this tuple from the newly attested install before the
    /// pins are updated.
    /// </para>
    /// <para>
    /// This is one shader, one direction, one version. It is deliberately not
    /// a render-state framework, a pass model, a conversion interface, or a
    /// provider registry.
    /// </para>
    /// </summary>
    internal static class LilToonOpaqueTarget
    {
        // --- Canonical Opaque recipe (pinned lilToon 2.3.4, B1 §9) ----------

        /// <summary>
        /// The complete canonical Opaque recipe's eighteen scalar writes, in
        /// the order B1 §9 measured them. The recipe's other two actions are
        /// the render queue and the <c>RenderType</c> tag, which are not
        /// material properties and are pinned by the constants below.
        /// <para>
        /// Deliberately absent from the recipe: the cutout clip threshold,
        /// <c>_Color</c>, and every alpha-mask/dither/dissolve property. The
        /// attested opaque target compiles <c>LIL_RENDER 0</c>, which excludes
        /// the alpha path at compile time. The clip threshold still enters
        /// the source family's own evidence; the other properties constrain
        /// the alpha proof and are never written.
        /// </para>
        /// </summary>
        private static readonly (string Property, float Value)[] CanonicalOpaqueTuple =
        {
            ("_SrcBlend", 1f),
            ("_DstBlend", 0f),
            ("_AlphaToMask", 0f),
            ("_ZWrite", 1f),
            ("_ZTest", 4f),
            ("_OffsetFactor", 0f),
            ("_OffsetUnits", 0f),
            ("_ColorMask", 15f),
            ("_SrcBlendAlpha", 1f),
            ("_DstBlendAlpha", 10f),
            ("_BlendOp", 0f),
            ("_BlendOpAlpha", 0f),
            ("_SrcBlendFA", 1f),
            ("_DstBlendFA", 1f),
            ("_SrcBlendAlphaFA", 0f),
            ("_DstBlendAlphaFA", 1f),
            ("_BlendOpFA", 4f),
            ("_BlendOpAlphaFA", 4f),
        };

        internal static IReadOnlyList<(string Property, float Value)>
            CanonicalOpaqueProperties { get; } =
                new ReadOnlyCollection<(string, float)>(CanonicalOpaqueTuple);

        internal const int CanonicalOpaqueRenderQueue = 2000;
        internal const string RenderTypeTagName = "RenderType";
        internal const string CanonicalOpaqueRenderType = "Opaque";

        /// <summary>
        /// The recipe's own property names, projected from the tuple rather
        /// than retyped so the two cannot drift. Before the ownership split
        /// this was the tuple plus a nineteenth source-eligibility property;
        /// the schema is now an identity on the tuple, not a concatenation.
        /// </summary>
        private static readonly string[] RecipeSchema = BuildRecipeSchema();

        internal static IReadOnlyCollection<string>
            RecipeSchemaProperties { get; } =
                new ReadOnlyCollection<string>(RecipeSchema);

        /// <summary>
        /// The target's own request: the recipe, its presence, and nothing
        /// else. The recipe writes no colors, vectors, or textures, and no
        /// source-eligibility property belongs here.
        /// </summary>
        internal static MaterialEvidenceRequest RecipeEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: RecipeSchema,
                scalarProperties: RecipeSchema,
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>());

        private static string[] BuildRecipeSchema()
        {
            var schema = new string[CanonicalOpaqueTuple.Length];
            for (var index = 0; index < CanonicalOpaqueTuple.Length; index++)
            {
                schema[index] = CanonicalOpaqueTuple[index].Property;
            }

            return schema;
        }

        // --- Effective render state and canonical-fact comparison -----------

        /// <summary>
        /// The two canonical facts that are not shader properties. Neither is
        /// animation-reachable - Unity's material binding syntax is
        /// <c>material.&lt;PropertyName&gt;</c>, and no binding form addresses a
        /// material's render queue or an override tag - so neither belongs in
        /// the evidence request, whose job is to close the animation-relevant
        /// set. <c>renderQueue</c> already resolves an absent override to the
        /// shader's declared queue, so "an override exists" is an
        /// implementation detail this design does not model.
        /// </summary>
        internal static void ReadEffectiveRenderState(
            Material material,
            out int renderQueue,
            out string renderType)
        {
            if (material == null) throw new ArgumentNullException(nameof(material));

            renderQueue = material.renderQueue;
            renderType = material.GetTag(RenderTypeTagName, false);
        }

        /// <summary>
        /// Reports the first of the 20 canonical facts the candidate disagrees
        /// with, in a deterministic order: recipe order, then the render
        /// queue, then the <c>RenderType</c> tag. A property the material does
        /// not declare is reported by name; the caller decides whether that
        /// is a refusal or a defect.
        /// </summary>
        internal static bool TryFindNonCanonicalFact(
            Material candidate,
            out string factName)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            foreach (var (property, value) in CanonicalOpaqueTuple)
            {
                if (!candidate.HasProperty(property) ||
                    candidate.GetFloat(property) != value)
                {
                    factName = property;
                    return true;
                }
            }

            ReadEffectiveRenderState(candidate, out var queue, out var renderType);
            if (queue != CanonicalOpaqueRenderQueue)
            {
                factName = nameof(Material.renderQueue);
                return true;
            }

            if (!string.Equals(
                    renderType, CanonicalOpaqueRenderType, StringComparison.Ordinal))
            {
                factName = RenderTypeTagName;
                return true;
            }

            factName = null;
            return false;
        }

        // --- Preparation ------------------------------------------------------

        /// <summary>
        /// Attests the target's capability, clones the source, swaps the
        /// clone to the caller-supplied attested opaque target shader,
        /// applies the complete canonical Opaque tuple, then re-reads and
        /// validates every canonical fact and the target identity.
        /// <para>
        /// The source material is never written: <c>new Material(source)</c>
        /// is the only relationship between them. Source avatar assets are
        /// evidence, not mutation targets.
        /// </para>
        /// <para>
        /// Nothing is saved. Persistence belongs to assignment, which is the
        /// consumer's job, so this method takes no asset saver and cannot
        /// persist anything by accident. The clone is also left unnamed:
        /// naming generated assets is a consumer obligation, because container
        /// sub-asset names come from the object's own name and NDMF guarantees
        /// no determinism.
        /// </para>
        /// <para>
        /// Its precondition is an attested and eligible source plus the
        /// attested opaque target resolved by the caller (the production
        /// wrapper below resolves it from the pinned environment; tests and
        /// seams supply a stand-in reference).
        /// </para>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The attested target does not declare a recipe property, a written
        /// canonical fact did not read back, or the clone did not take the
        /// attested target shader. The property check runs before
        /// <c>new Material(source)</c>, so that throw leaves no clone to
        /// destroy; the other two throw after
        /// <see cref="UnityEngine.Object.DestroyImmediate"/> has destroyed
        /// the clone, so no material leaks. The source gates have already
        /// proven every property present on the SOURCE and every input
        /// finite, and
        /// this method writes exact canonical constants, so a read-back
        /// disagreement falsifies the assumption that AMUSE can write this
        /// material's render state; a target that cannot declare the recipe,
        /// or a target mismatch, would silently convert the material onto a
        /// wrong or partially-capable shader. All three are compatibility or
        /// programming failures, not unsupported materials, and converting
        /// either into a conservative refusal would hide a broken write path
        /// behind a plausible-looking "preserved the input" outcome.
        /// </exception>
        /// <remarks>
        /// The target-identity check is the family-specific replacement for
        /// the Poiyomi module's shader-preservation check (spec R5): the
        /// Poiyomi conversion clones in place on the same shader, while this
        /// conversion's whole point is moving a cutout material onto the
        /// attested opaque shader, so the validated fact is that the clone
        /// <em>carries the target</em>, not that it preserved the source.
        /// </remarks>
        internal static Material PrepareCanonicalOpaqueClone(
            Material source,
            Shader attestedTarget)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (attestedTarget == null)
            {
                throw new ArgumentNullException(nameof(attestedTarget));
            }

            // Attest the target's capability at the SHADER level, before any
            // clone exists. Unity's observed behavior: SetFloat on a property
            // the shader does not declare still stores the value in the
            // material's saved properties, and HasProperty/GetFloat then read
            // it back - so a material-level read-back after the writes cannot
            // detect a target shader missing a recipe property; it would
            // faithfully echo whatever the recipe wrote. The read-back below
            // therefore validates the writes, and only this shader-level
            // check validates the writer.
            foreach (var (property, _) in CanonicalOpaqueTuple)
            {
                if (attestedTarget.FindPropertyIndex(property) < 0)
                {
                    throw new InvalidOperationException(
                        "The attested opaque target shader does not declare '" +
                        property + "'.");
                }
            }

            var clone = new Material(source);
            clone.name = string.Empty;
            clone.shader = attestedTarget;
            foreach (var (property, value) in CanonicalOpaqueTuple)
            {
                clone.SetFloat(property, value);
            }

            clone.renderQueue = CanonicalOpaqueRenderQueue;
            clone.SetOverrideTag(RenderTypeTagName, CanonicalOpaqueRenderType);

            if (TryFindNonCanonicalFact(clone, out var fact))
            {
                UnityEngine.Object.DestroyImmediate(clone);
                throw new InvalidOperationException(
                    "Generated opaque material did not read back canonical '" +
                    fact + "'.");
            }

            if (clone.shader != attestedTarget)
            {
                UnityEngine.Object.DestroyImmediate(clone);
                throw new InvalidOperationException(
                    "Generated opaque material did not take the attested " +
                    "opaque target shader.");
            }

            return clone;
        }

        /// <summary>
        /// Resolves and fully attests the opaque lilToon target from the live
        /// environment before delegating to the explicit-target writer.
        /// Name and GUID alone are insufficient: the recipe is measured
        /// against the pinned shader, pass, include tree, package version,
        /// and <c>LIL_RENDER 0</c> source profile.
        /// <para>
        /// An attestation failure is an environment invariant failure, not an
        /// unsupported-material refusal, and occurs before any clone exists.
        /// </para>
        /// </summary>
        internal static Material PrepareCanonicalOpaqueClone(
            Material source,
            CapturedMaterialEvidence evidence)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (evidence == null) throw new ArgumentNullException(nameof(evidence));

            var target = Shader.Find(LilToonSourceAttestation.SupportedShaderName);
            if (target == null)
            {
                throw new InvalidOperationException(
                    "The attested lilToon environment regressed: opaque target '" +
                    LilToonSourceAttestation.SupportedShaderName +
                    "' did not resolve.");
            }

            var targetEvidence =
                LilToonSourceAttestation.GatherOpaqueTargetSourceEvidence(
                    target, evidence);
            if (!LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    targetEvidence, out var diagnostic))
            {
                throw new InvalidOperationException(
                    "The attested lilToon opaque target failed source " +
                    "attestation: " + diagnostic?.Detail);
            }

            return PrepareCanonicalOpaqueClone(source, target);
        }
    }
}
