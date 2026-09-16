using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The outcome of interpreting one lilToon layer (second or third main
    /// texture) against one material's captured evidence.
    /// </summary>
    internal enum LilToonLayerAlphaTermKind
    {
        /// <summary>The toggle is off, or the alpha mode writes no alpha.</summary>
        Inert,

        /// <summary>The whole layer alpha is a proven constant.</summary>
        Constant,

        /// <summary>
        /// The layer alpha is the represented sampled chain times the
        /// constant.
        /// </summary>
        Field,

        /// <summary>A gate refused; the diagnostic is already recorded.</summary>
        Refused,
    }

    /// <summary>
    /// One layer's alpha contribution, pinned to lilToon 2.3.4
    /// (<c>lil_common_frag.hlsl:724-811</c> second layer, <c>:820-907</c>
    /// third; the stage 0 record of 2026-09-15 carries every line
    /// reference). Inside the toggle gate the chain builds in this order:
    /// <code>
    /// layerAlpha = _Color{N}.a
    ///            * _Main{N}Tex alpha sample at the layer UV mode
    ///            * _Main{N}BlendMask red sample at uvMain (unassigned: 1)
    /// layerAlpha *= dissolve mask value   (rounded mode nonzero)
    /// layerAlpha *= audio link value      (_AudioLink2Main{N})
    /// layerAlpha  = distance fade lerp    (_Main{N}DistanceFade.z)
    /// layerAlpha  = 0 on one facing       (_Main{N}Tex_Cull)
    /// mode 1: fd.col.a = layerAlpha       // replace
    /// mode 2: fd.col.a = fd.col.a * layerAlpha
    /// mode 3: fd.col.a = saturate(fd.col.a + layerAlpha)
    /// mode 4: fd.col.a = saturate(fd.col.a - layerAlpha)
    /// </code>
    /// Stage A admits the layer sampled at UV0 with exact identity scroll,
    /// rotate, and angle, identity scale and offset, every decal flag and the
    /// MSDF flag exactly off, the audio link toggle exactly off, the distance
    /// fade strength exactly zero, the per-layer cull exactly zero, and the
    /// layer dissolve's rounded mode exactly zero. The blending after the
    /// writers is RGB only and never reaches the proof.
    /// </summary>
    internal readonly struct LilToonLayerAlphaTerm
    {
        internal LilToonLayerAlphaTermKind Kind { get; }
        internal float Constant { get; }
        internal ScalarSemanticValue Value { get; }

        /// <summary>The layer's alpha writer mode: 1 replace, 2 multiply,
        /// 3 add, 4 subtract. Inert terms carry the captured mode anyway so
        /// the caller sees the writer is silent.</summary>
        internal float AlphaMode { get; }

        private LilToonLayerAlphaTerm(
            LilToonLayerAlphaTermKind kind,
            float constant,
            ScalarSemanticValue value,
            float alphaMode)
        {
            Kind = kind;
            Constant = constant;
            Value = value;
            AlphaMode = alphaMode;
        }

        private static LilToonLayerAlphaTerm Inert(float alphaMode)
        {
            return new LilToonLayerAlphaTerm(
                LilToonLayerAlphaTermKind.Inert, default, null, alphaMode);
        }

        private static LilToonLayerAlphaTerm Refuse(
            List<LilToonSemanticDiagnostic> diagnostics,
            string propertyName)
        {
            diagnostics.Add(
                new LilToonSemanticDiagnostic(
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    propertyName));
            return new LilToonLayerAlphaTerm(
                LilToonLayerAlphaTermKind.Refused,
                default, null, default);
        }

        /// <summary>
        /// Interprets one layer. The property names follow the second layer's
        /// spelling; the third layer swaps the "2nd" token for "3rd".
        /// </summary>
        internal static LilToonLayerAlphaTerm Interpret(
            CapturedMaterialEvidence evidence,
            bool third,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }
            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            var token = third ? "3rd" : "2nd";
            var toggleProperty = "_UseMain" + token + "Tex";
            var colorProperty = "_Color" + token;
            var uvModeProperty = "_Main" + token + "Tex_UVMode";
            var angleProperty = "_Main" + token + "TexAngle";
            var scrollRotateProperty = "_Main" + token + "Tex_ScrollRotate";
            var cullProperty = "_Main" + token + "Tex_Cull";
            var alphaModeProperty = "_Main" + token + "TexAlphaMode";
            var isDecalProperty = "_Main" + token + "TexIsDecal";
            var isLeftOnlyProperty = "_Main" + token + "TexIsLeftOnly";
            var isRightOnlyProperty = "_Main" + token + "TexIsRightOnly";
            var shouldCopyProperty = "_Main" + token + "TexShouldCopy";
            var shouldFlipMirrorProperty =
                "_Main" + token + "TexShouldFlipMirror";
            var shouldFlipCopyProperty =
                "_Main" + token + "TexShouldFlipCopy";
            var isMsdfProperty = "_Main" + token + "TexIsMSDF";
            var audioLinkProperty = "_AudioLink2Main" + token;
            var distanceFadeProperty = "_Main" + token + "DistanceFade";
            var dissolveParamsProperty = "_Main" + token + "DissolveParams";
            var textureProperty = "_Main" + token + "Tex";
            var blendMaskProperty = "_Main" + token + "BlendMask";

            // Toggle: off is the pinned off-state reduction (the alpha
            // writers sit strictly inside this gate).
            if (!evidence.TryGetScalar(toggleProperty, out var toggle) ||
                !IsFinite(toggle))
            {
                return Refuse(diagnostics, toggleProperty);
            }

            // Alpha mode: a finite non-negative integer. Modes one to four
            // write alpha; zero and any larger value write nothing, which is
            // alpha neutral.
            if (!evidence.TryGetScalar(alphaModeProperty, out var alphaMode) ||
                !IsFinite(alphaMode) ||
                alphaMode < 0f ||
                alphaMode != Mathf.Floor(alphaMode))
            {
                return Refuse(diagnostics, alphaModeProperty);
            }

            if (toggle == 0f || alphaMode == 0f || alphaMode > 4f)
            {
                return Inert(alphaMode);
            }

            // Coordinate boundary: modes zero to three are mesh channels
            // the proof selects exactly. Mode four is the view dependent
            // matcap coordinate and refuses. Stage B keeps exact identity
            // scale and offset. A time varying scroll or rotation can prove
            // only through the whole texture domain rule below.
            if (!evidence.TryGetScalar(uvModeProperty, out var uvMode) ||
                !IsFinite(uvMode) ||
                uvMode < 0f ||
                uvMode > 3f ||
                uvMode != Mathf.Floor(uvMode))
            {
                return Refuse(diagnostics, uvModeProperty);
            }

            if (!evidence.TryGetScalar(angleProperty, out var angle) ||
                !IsFinite(angle))
            {
                return Refuse(diagnostics, angleProperty);
            }

            if (!evidence.TryGetVector(
                    scrollRotateProperty, out var scrollRotate) ||
                !IsFinite(scrollRotate) ||
                scrollRotate.z != 0f)
            {
                return Refuse(diagnostics, scrollRotateProperty);
            }

            var hasTimeVaryingCoordinates =
                scrollRotate.x != 0f ||
                scrollRotate.y != 0f ||
                scrollRotate.w != 0f;
            if (!hasTimeVaryingCoordinates && angle != 0f)
            {
                return Refuse(diagnostics, angleProperty);
            }

            if (!evidence.TryGetVector(
                    dissolveParamsProperty, out var dissolveParams) ||
                !IsFinite(dissolveParams) ||
                // The shader rounds the mode with HLSL round before it
                // branches, so the proof rounds the same way: half to even.
                // Anything the shader leaves off is alpha neutral; anything
                // it can turn on refuses, because the masked threshold is
                // texture backed.
                Mathf.RoundToInt(dissolveParams.x) != 0)
            {
                return Refuse(diagnostics, dissolveParamsProperty);
            }

            if (!evidence.TryGetScalar(cullProperty, out var cull) ||
                !IsFinite(cull) ||
                cull != 0f)
            {
                return Refuse(diagnostics, cullProperty);
            }

            // The decal flags and the MSDF flag change the sampled coordinate
            // or replace the sampled alpha outright. The shipped shader
            // compiles the decal path, so the plain rounding argument that
            // makes identity scale exact holds with or without the flags.
            var exactOffFlags = new[]
            {
                isDecalProperty,
                isLeftOnlyProperty,
                isRightOnlyProperty,
                shouldCopyProperty,
                shouldFlipMirrorProperty,
                shouldFlipCopyProperty,
                isMsdfProperty,
            };
            foreach (var propertyName in exactOffFlags)
            {
                if (!evidence.TryGetScalar(
                        propertyName, out var flag) ||
                    !IsFinite(flag) ||
                    flag != 0f)
                {
                    return Refuse(diagnostics, propertyName);
                }
            }

            if (!evidence.TryGetScalar(
                    audioLinkProperty, out var audioLink) ||
                !IsFinite(audioLink) ||
                audioLink != 0f)
            {
                return Refuse(diagnostics, audioLinkProperty);
            }

            if (!evidence.TryGetVector(
                    distanceFadeProperty, out var distanceFade) ||
                !IsFinite(distanceFade) ||
                distanceFade.z != 0f)
            {
                return Refuse(diagnostics, distanceFadeProperty);
            }

            if (!evidence.TryGetColor(colorProperty, out var color) ||
                !IsFinite(color.a))
            {
                return Refuse(diagnostics, colorProperty);
            }

            var colorAlpha = color.a;

            if (!evidence.TryGetTexture(
                    textureProperty, out var assignment) ||
                !assignment.IsAssigned)
            {
                return Refuse(diagnostics, textureProperty);
            }

            if (!assignment.Texture.HasSampling)
            {
                return Refuse(diagnostics, textureProperty);
            }

            if (!assignment.HasScaleOffset)
            {
                return Refuse(diagnostics, textureProperty);
            }

            if (assignment.Scale.x != 1f ||
                assignment.Scale.y != 1f ||
                assignment.Offset.x != 0f ||
                assignment.Offset.y != 0f)
            {
                return Refuse(diagnostics, textureProperty);
            }

            var layerTextureIsProvenOne =
                assignment.Texture.SampledAlphaIsProvenOne;
            if (hasTimeVaryingCoordinates)
            {
                // Motion removes the fixed triangle domain. A complete chain
                // of exact one texels makes every coordinate equivalent.
                // Any other chain keeps the named layer scope unproven.
                if (!layerTextureIsProvenOne &&
                    !assignment.Texture.AlphaChannelIsProvenFullyOpaque)
                {
                    return Refuse(diagnostics, scrollRotateProperty);
                }

                layerTextureIsProvenOne = true;
            }

            // The layer samples through its own sampler, so its sampling
            // facts ride its own assignment. The blend mask rides uvMain and
            // the main sampler, so it needs no facts of its own beyond its
            // red field and identity.
            var layerMapping = new UvMapping(
                (int)uvMode, assignment.Scale, assignment.Offset);
            var layerSampling = assignment.Texture.Sampling;

            // Alpha factor assembly. The importer theorem or the moving
            // whole domain proof can establish that the layer texture
            // samples exactly one. Such a sample contributes no factor.
            // An unassigned blend mask also contributes no factor.
            var factors = new List<(TextureSample Sample, TextureChannel Channel)>();
            if (layerTextureIsProvenOne)
            {
                // No field is needed.
            }
            else
            {
                if (!assignment.Texture.HasSourceIdentity)
                {
                    return Refuse(diagnostics, textureProperty);
                }

                if (!assignment.Texture.HasAlphaChannel)
                {
                    return Refuse(diagnostics, textureProperty);
                }

                factors.Add((
                    new TextureSample(
                        assignment.Texture.SourceIdentity,
                        layerMapping,
                        layerSampling),
                    TextureChannel.Alpha));
            }

            if (evidence.TryGetTexture(
                    blendMaskProperty, out var blendMask) &&
                blendMask.IsAssigned)
            {
                if (!blendMask.Texture.HasSourceIdentity)
                {
                    return Refuse(diagnostics, blendMaskProperty);
                }

                if (!blendMask.Texture.HasRedChannel)
                {
                    return Refuse(diagnostics, blendMaskProperty);
                }

                // uvMain is UV0 under the families' identity gates. The mask
                // borrows _MainTex's sampler, which is the main assignment's
                // sampling evidence.
                factors.Add((
                    new TextureSample(
                        blendMask.Texture.SourceIdentity,
                        new UvMapping(0, Vector2.one, Vector2.zero),
                        layerSampling),
                    TextureChannel.Red));
            }

            if (factors.Count == 0)
            {
                return new LilToonLayerAlphaTerm(
                    LilToonLayerAlphaTermKind.Constant,
                    colorAlpha, null, alphaMode);
            }

            if (factors.Count == 1)
            {
                var value = colorAlpha == 1f
                    ? ScalarSemanticValue.Texture(
                        factors[0].Sample, factors[0].Channel)
                    : ScalarSemanticValue.TextureTimesConstant(
                        factors[0].Sample, factors[0].Channel, colorAlpha);
                return new LilToonLayerAlphaTerm(
                    LilToonLayerAlphaTermKind.Field,
                    default, value, alphaMode);
            }

            var chainSamples = new List<TextureSample>();
            var chainChannels = new List<TextureChannel>();
            foreach (var (sample, channel) in factors)
            {
                chainSamples.Add(sample);
                chainChannels.Add(channel);
            }

            return new LilToonLayerAlphaTerm(
                LilToonLayerAlphaTermKind.Field,
                default,
                ScalarSemanticValue.ProductChain(
                    chainSamples, chainChannels, colorAlpha),
                alphaMode);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w);
        }
    }
}
