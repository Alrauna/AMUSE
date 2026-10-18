using System;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Builds the two duplicated twin alpha evidence requests — cutout and
    /// transparent — from one shared schema plus the per-family delta table
    /// the record measured. The table has exactly five entries: the shared
    /// 46-entry scalar base, the transparent additions
    /// (<c>_AlphaBoostFA</c>, <c>_SubpassCutoff</c>), the dither removal,
    /// the mask-pair reorder, and the per-family cutoff declaration on the
    /// <c>_MainTex</c> texture request. The cutout and transparent requests
    /// stay distinct values. The main lilToon request in
    /// <see cref="LilToonMaterialSemantics"/> is a different schema and
    /// keeps its own construction; it is not part of the duplicated twin.
    /// </summary>
    internal static class LilToonAlphaEvidenceRequests
    {
        // --- The per-family delta table ---------------------------------
        //
        // The shared scalar base of both families is the cutout list, split
        // at the mask scale/value pair the transparent order moves. The
        // transparent family applies exactly three deltas to the base: the
        // removal of _UseDither (LIL_RENDER 2 compiles the runtime dither
        // path out entirely, T1 §6 row 16), the additions behind _Cutoff,
        // and the reorder that moves the mask pair behind the additions.

        /// <summary>
        /// The shared scalar head both families keep, through
        /// <c>_AlphaMaskMode</c>.
        /// </summary>
        private static readonly string[] ScalarsThroughAlphaMaskMode =
        {
            LilToonSourceAttestation.ShaderFormatVersionProperty,
            LilToonProperties.Invisible,
            LilToonProperties.UdimDiscardCompile,
            LilToonProperties.UdimDiscardMode,
            LilToonProperties.ShiftBackfaceUV,
            LilToonProperties.UseParallax,
            LilToonProperties.UseMain2ndTex,
            LilToonProperties.UseMain3rdTex,
            LilToonProperties.AlphaMaskMode,
        };

        /// <summary>
        /// The mask scale/value pair the transparent reorder moves behind
        /// the additions.
        /// </summary>
        private static readonly string[] AlphaMaskPairScalars =
        {
            LilToonProperties.AlphaMaskScale,
            LilToonProperties.AlphaMaskValue,
        };

        /// <summary>
        /// The cutout scalar middle: the dither gate, the IDMask block, and
        /// the cutoff.
        /// </summary>
        private static readonly string[] CutoutScalarsAfterMaskPair =
        {
            LilToonProperties.UseDither,
            LilToonProperties.IdMask1,
            LilToonProperties.IdMask2,
            LilToonProperties.IdMask3,
            LilToonProperties.IdMask4,
            LilToonProperties.IdMask5,
            LilToonProperties.IdMask6,
            LilToonProperties.IdMask7,
            LilToonProperties.IdMask8,
            LilToonProperties.IdMaskControlsDissolve,
            LilToonProperties.Cutoff,
        };

        /// <summary>
        /// The transparent scalar middle: the cutout middle minus the dither
        /// removal.
        /// </summary>
        private static readonly string[] TransparentScalarsAfterMaskPair =
        {
            LilToonProperties.IdMask1,
            LilToonProperties.IdMask2,
            LilToonProperties.IdMask3,
            LilToonProperties.IdMask4,
            LilToonProperties.IdMask5,
            LilToonProperties.IdMask6,
            LilToonProperties.IdMask7,
            LilToonProperties.IdMask8,
            LilToonProperties.IdMaskControlsDissolve,
            LilToonProperties.Cutoff,
        };

        /// <summary>
        /// The transparent additions: the two transparent-only proof facts.
        /// </summary>
        private static readonly string[] TransparentAddedScalars =
        {
            LilToonProperties.AlphaBoostFA,
            LilToonProperties.SubpassCutoff,
        };

        /// <summary>
        /// The shared scalar tail both families keep: the main-layer and
        /// audio-link proof facts, in pinned order.
        /// </summary>
        private static readonly string[] SharedTailScalars =
        {
            "_Main2ndTex_UVMode",
            "_Main3rdTex_UVMode",
            "_Main2ndTexAngle",
            "_Main3rdTexAngle",
            "_Main2ndTex_Cull",
            "_Main3rdTex_Cull",
            "_Main2ndTexAlphaMode",
            "_Main3rdTexAlphaMode",
            "_Main2ndTexIsDecal",
            "_Main3rdTexIsDecal",
            "_Main2ndTexIsLeftOnly",
            "_Main3rdTexIsLeftOnly",
            "_Main2ndTexIsRightOnly",
            "_Main3rdTexIsRightOnly",
            "_Main2ndTexShouldCopy",
            "_Main3rdTexShouldCopy",
            "_Main2ndTexShouldFlipMirror",
            "_Main3rdTexShouldFlipMirror",
            "_Main2ndTexShouldFlipCopy",
            "_Main3rdTexShouldFlipCopy",
            "_Main2ndTexIsMSDF",
            "_Main3rdTexIsMSDF",
            "_AudioLink2Main2nd",
            "_AudioLink2Main3rd",
        };

        /// <summary>
        /// The cutout alpha evidence request (spec §8.2): the shared base in
        /// the cutout order, with the cutoff declared on the main texture
        /// request. Exact by contract: no fewer and no more.
        /// <c>_IDMaskPrior8</c> is deliberately absent (it is a fixture-only
        /// vendor prior byte, not an AMUSE-proof fact), and
        /// <c>_MainTex_ST</c> is deliberately not a vector request — it
        /// rides the texture request's ScaleOffset kind, which also derives
        /// the animatable binding name. <c>_Cutoff</c> rides as a captured
        /// theorem scalar even though conversion also reads it.
        /// </summary>
        internal static MaterialEvidenceRequest CutoutRequest()
        {
            return Build(declaresMainTexCutoff: true);
        }

        /// <summary>
        /// The transparent alpha evidence request (design §8): the shared
        /// base under the three transparent deltas, with no cutoff
        /// declaration on the main texture request. Exact by contract: no
        /// fewer and no more.
        /// <c>_IDMaskPrior8</c> is deliberately absent (it is a fixture-only
        /// vendor prior byte, not an AMUSE-proof fact), and
        /// <c>_MainTex_ST</c> is deliberately not a vector request — it
        /// rides the texture request's ScaleOffset kind, which also derives
        /// the animatable binding name. <c>_Cutoff</c> rides as a captured
        /// theorem scalar even though conversion also reads it.
        /// </summary>
        internal static MaterialEvidenceRequest TransparentRequest()
        {
            return Build(declaresMainTexCutoff: false);
        }

        private static MaterialEvidenceRequest Build(
            bool declaresMainTexCutoff)
        {
            var scalarProperties = declaresMainTexCutoff
                ? Join(
                    ScalarsThroughAlphaMaskMode,
                    AlphaMaskPairScalars,
                    CutoutScalarsAfterMaskPair,
                    SharedTailScalars)
                : Join(
                    ScalarsThroughAlphaMaskMode,
                    TransparentScalarsAfterMaskPair,
                    TransparentAddedScalars,
                    AlphaMaskPairScalars,
                    SharedTailScalars);

            return new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: scalarProperties,
                colorProperties: new[]
                {
                    LilToonProperties.Color, "_Color2nd", "_Color3rd",
                },
                vectorProperties: new[]
                {
                    LilToonProperties.DissolveParams,
                    LilToonProperties.MainTexScrollRotate,
                    LilToonDistanceFadeSemantics.DistanceFadeProperty,
                    "_Main2ndTex_ScrollRotate",
                    "_Main3rdTex_ScrollRotate",
                    "_Main2ndDistanceFade",
                    "_Main3rdDistanceFade",
                    "_Main2ndDissolveParams",
                    "_Main3rdDissolveParams",
                },
                textureProperties: TextureRequests(declaresMainTexCutoff));
        }

        private static TexturePropertyEvidenceRequest[] TextureRequests(
            bool declaresMainTexCutoff)
        {
            // The per-family cutoff declaration. Cutout: the cutout runtime
            // clips by the shader cutoff, so the capture declares it — every
            // route binarizes by the cutoff and keeps the alpha policy inert
            // for this source (spec section 3). Transparent: no cutoff
            // declaration — the transparent alpha is the plain unclipped
            // _MainTex sample (T1 §9.1 clause 5), so the exact-255 arm is
            // this family's capture arm and the alpha policy applies to it.
            var mainTexCutoffProperty =
                declaresMainTexCutoff ? LilToonProperties.Cutoff : null;

            return new[]
            {
                new TexturePropertyEvidenceRequest(
                    "_Main2ndTex",
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.AlphaChannel |
                    TextureEvidenceKinds.SampledAlphaIsOne),
                new TexturePropertyEvidenceRequest(
                    "_Main3rdTex",
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.AlphaChannel |
                    TextureEvidenceKinds.SampledAlphaIsOne),
                new TexturePropertyEvidenceRequest(
                    "_Main2ndBlendMask",
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.RedChannel),
                new TexturePropertyEvidenceRequest(
                    "_Main3rdBlendMask",
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.RedChannel),
                new TexturePropertyEvidenceRequest(
                    LilToonProperties.MainTex,
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling |
                    TextureEvidenceKinds.AlphaChannel |
                    TextureEvidenceKinds.SampledAlphaIsOne,
                    mainTexCutoffProperty),

                // The alpha mask rides _MainTex's sampler, so this
                // request deliberately asks for no sampling facts of
                // its own: wrap, filter, and anisotropy evidence comes
                // from the _MainTex assignment. The red field, not
                // alpha, is the mask channel.
                new TexturePropertyEvidenceRequest(
                    "_AlphaMask",
                    TextureEvidenceKinds.ScaleOffset |
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.RedChannel),
            };
        }

        private static string[] Join(params string[][] segments)
        {
            var total = 0;
            foreach (var segment in segments)
            {
                total += segment.Length;
            }

            var joined = new string[total];
            var offset = 0;
            foreach (var segment in segments)
            {
                Array.Copy(segment, 0, joined, offset, segment.Length);
                offset += segment.Length;
            }

            return joined;
        }
    }
}
