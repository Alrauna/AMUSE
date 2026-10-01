using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The Multi mode-consistency gate. The gate admits one material when
    /// the captured keyword set is exactly a state the pinned vendor
    /// derivation produces for the captured scalars and the mode, when the
    /// captured <c>_UseClippingCanceller</c> is zero, and when the captured
    /// <c>_AsOverlay</c> is zero.
    /// <para>
    /// The derivation table is a static pinned array, one row per keyword
    /// with its producing condition. It is transcribed from lilToon 2.3.4,
    /// upstream commit 252fd8cfc46106d4967e95b3f2c788418502f227, keyword
    /// writes at <c>Editor/lilMaterialUtils.cs:397-468</c>. The table pins
    /// the rows the design's gate section names: the two mode keywords, the
    /// dither keyword, the alpha mask keyword, the layer dissolve keyword,
    /// the distance fade keyword, the outline tone keyword, and the two
    /// tolerated animation-derived color keywords. Every other keyword a
    /// material carries is outside the table, so its captured presence is a
    /// state the derivation cannot produce and refuses as a mismatch.
    /// </para>
    /// <para>
    /// Missing-fact policy: the derivation consults captured facts only. A
    /// scalar or vector the request did not carry is not shown to be on. A
    /// requested property the material does not have is not shown to be on
    /// either. This mirrors the vendor fallback that reads a missing
    /// property as feature-off (<c>Editor/lilMaterialUtils.cs:500-503</c>
    /// at the pin). The outline tone row also needs a captured shader name,
    /// so a request without the name cannot derive that keyword.
    /// </para>
    /// <para>
    /// <see cref="GEOM_TYPE_LEAF"/> and
    /// <see cref="EFFECT_HUE_VARIATION"/> are animation-derived color
    /// keywords. They map to rim-light direction and tone correction, which
    /// are not alpha facts. The exact-set comparison tolerates them present
    /// or absent and never requires them. On the NDMF play-mode path the
    /// vendor preprocess returns early and they never appear.
    /// </para>
    /// <para>
    /// Evaluation order: mode range, then the two gate scalars, then the
    /// keyword derivation. Evaluation reads only evidence arrays and the
    /// static table, so it allocates nothing and is deterministic.
    /// </para>
    /// </summary>
    internal static class LilToonMultiModeGate
    {
        private const int OpaqueMode = 0;
        private const int CutoutMode = 1;
        private const int TransparentMode = 2;
        private const int MaxAdmittedMode = TransparentMode;

        private const string ClippingCancellerProperty = "_UseClippingCanceller";
        private const string OverlayProperty = "_AsOverlay";
        private const string DitherProperty = "_UseDither";
        private const string AlphaMaskModeProperty = "_AlphaMaskMode";
        private const string DissolveParamsProperty = "_DissolveParams";
        private const string DistanceFadeProperty = "_DistanceFade";
        private const string OutlineToneProperty = "_OutlineTexHSVG";

        /// <summary>
        /// The vendor written-off outline tone color
        /// (<c>lilConstants.defaultHSVG</c> at the pin). A material whose
        /// outline tone vector holds this exact value derives no keyword.
        /// </summary>
        private static readonly Vector4 OutlineToneOffColor =
            new Vector4(0f, 1f, 1f, 1f);

        private delegate bool ProducingCondition(
            CapturedMaterialEvidence evidence,
            int mode);

        /// <summary>
        /// One row of the pinned derivation table. A tolerated row carries
        /// no condition, because the comparison skips tolerated rows in both
        /// directions: their presence never refuses and their absence never
        /// refuses.
        /// </summary>
        private sealed class KeywordDerivationRow
        {
            internal string Keyword;
            internal bool Tolerated;
            internal ProducingCondition Produces;
        }

        /// <summary>
        /// The pinned vendor derivation table. Rows sit in the order the
        /// design's gate section names them. Each condition comment cites
        /// the vendor row it transcribes at the pin.
        /// </summary>
        private static readonly KeywordDerivationRow[] DerivationTable =
        {
            new KeywordDerivationRow
            {
                Keyword = "UNITY_UI_ALPHACLIP",
                Produces = ProducesAlphaClip,
            },
            new KeywordDerivationRow
            {
                Keyword = "UNITY_UI_CLIP_RECT",
                Produces = ProducesClipRect,
            },
            new KeywordDerivationRow
            {
                Keyword = "ETC1_EXTERNAL_ALPHA",
                Produces = ProducesDither,
            },
            new KeywordDerivationRow
            {
                Keyword = "_COLOROVERLAY_ON",
                Produces = ProducesAlphaMask,
            },
            new KeywordDerivationRow
            {
                Keyword = "GEOM_TYPE_BRANCH_DETAIL",
                Produces = ProducesLayerDissolve,
            },
            new KeywordDerivationRow
            {
                Keyword = "_FADING_ON",
                Produces = ProducesDistanceFade,
            },
            new KeywordDerivationRow
            {
                Keyword = "_DETAIL_MULX2",
                Produces = ProducesOutlineTone,
            },
            new KeywordDerivationRow
            {
                Keyword = "GEOM_TYPE_LEAF",
                Tolerated = true,
            },
            new KeywordDerivationRow
            {
                Keyword = "EFFECT_HUE_VARIATION",
                Tolerated = true,
            },
        };

        /// <summary>
        /// Runs the gate. Returns true when the material holds a consistent
        /// Multi state. Returns false and names the refusal otherwise. The
        /// out value carries no meaning when the result is true.
        /// </summary>
        internal static bool Evaluate(
            CapturedMaterialEvidence evidence,
            int mode,
            out LilToonMultiResolutionRefusal refusal)
        {
            if (mode < OpaqueMode || mode > MaxAdmittedMode)
            {
                refusal = LilToonMultiResolutionRefusal.ModeOutsideAdmittedSet;
                return false;
            }

            if (FeatureIsOn(evidence, ClippingCancellerProperty))
            {
                refusal = LilToonMultiResolutionRefusal.ClippingCancellerEnabled;
                return false;
            }

            if (FeatureIsOn(evidence, OverlayProperty))
            {
                refusal = LilToonMultiResolutionRefusal.OverlayPassEnableUnsupported;
                return false;
            }

            var keywords = evidence.Keywords;
            for (var index = 0; index < keywords.Count; index++)
            {
                if (!CapturedKeywordIsDerivable(keywords[index], evidence, mode))
                {
                    refusal = LilToonMultiResolutionRefusal.KeywordModeMismatch;
                    return false;
                }
            }

            for (var index = 0; index < DerivationTable.Length; index++)
            {
                var row = DerivationTable[index];
                if (row.Tolerated || !row.Produces(evidence, mode))
                {
                    continue;
                }

                if (!KeywordIsPresent(keywords, row.Keyword))
                {
                    refusal = LilToonMultiResolutionRefusal.KeywordModeMismatch;
                    return false;
                }
            }

            refusal = default;
            return true;
        }

        /// <summary>
        /// True when the table derives the captured keyword from the
        /// captured facts and the mode, or when the keyword is tolerated. A
        /// keyword outside the table derives nowhere, so it refuses.
        /// </summary>
        private static bool CapturedKeywordIsDerivable(
            string keyword,
            CapturedMaterialEvidence evidence,
            int mode)
        {
            for (var index = 0; index < DerivationTable.Length; index++)
            {
                var row = DerivationTable[index];
                if (!string.Equals(row.Keyword, keyword, StringComparison.Ordinal))
                {
                    continue;
                }

                return row.Tolerated || row.Produces(evidence, mode);
            }

            return false;
        }

        private static bool KeywordIsPresent(
            IReadOnlyList<string> keywords,
            string keyword)
        {
            for (var index = 0; index < keywords.Count; index++)
            {
                if (string.Equals(keywords[index], keyword, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // Editor/lilMaterialUtils.cs:397 at the pin.
        private static bool ProducesAlphaClip(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return mode == CutoutMode;
        }

        // Editor/lilMaterialUtils.cs:398 at the pin. The vendor condition
        // also admits a second mode value outside the admitted set, so that
        // arm stays inert behind the mode range check.
        private static bool ProducesClipRect(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return mode == TransparentMode || mode == 4;
        }

        // Editor/lilMaterialUtils.cs:399 at the pin. The vendor compares the
        // dither float against exactly one, not against zero.
        private static bool ProducesDither(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return mode == CutoutMode
                && TryReadScalar(evidence, DitherProperty, out var dither)
                && dither == 1f;
        }

        // Editor/lilMaterialUtils.cs:455 at the pin, with the alpha mask
        // feature read at :385. The gem branch forces the keyword off at
        // :442, and the gem name family carries no Multi container this
        // gate admits, so the transcription keeps the regular branch only.
        private static bool ProducesAlphaMask(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return mode != OpaqueMode
                && FeatureIsOn(evidence, AlphaMaskModeProperty);
        }

        // Editor/lilMaterialUtils.cs:433 at the pin, with the dissolve
        // feature read at :374: the x component of the params vector.
        private static bool ProducesLayerDissolve(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return TryReadVector(evidence, DissolveParamsProperty, out var dissolve)
                && dissolve.x != 0f;
        }

        // Editor/lilMaterialUtils.cs:416 at the pin, with the distance fade
        // feature read at :359: the z component of the fade vector. The gem
        // branch forces the keyword off at :410 for the same name-family
        // reason as the alpha mask row.
        private static bool ProducesDistanceFade(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return TryReadVector(evidence, DistanceFadeProperty, out var fade)
                && fade.z != 0f;
        }

        // Editor/lilMaterialUtils.cs:462-468 at the pin. The vendor branch
        // forces the keyword off on the refraction, fur, and gem name
        // families, so the keyword is refused everywhere except outline
        // shaders. The name classifiers are transcribed from
        // Editor/lilShaderUtils.cs:54-77 and :192-202 at the pin. The row
        // needs the captured shader name and the captured outline tone
        // vector, so a request without them derives nothing here.
        private static bool ProducesOutlineTone(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            if (!evidence.HasShaderName)
            {
                return false;
            }

            var shaderName = evidence.ShaderName;
            if (NameContainsAfterLastSeparator(shaderName, "Refraction")
                || NameContainsAfterLastSeparator(shaderName, "Fur")
                || NameContainsAfterLastSeparator(shaderName, "Gem"))
            {
                return false;
            }

            if (!IsOutlineShaderName(shaderName))
            {
                return false;
            }

            return TryReadVector(evidence, OutlineToneProperty, out var tone)
                && tone != OutlineToneOffColor;
        }

        /// <summary>
        /// True when the captured evidence shows a float feature on. A
        /// scalar the request did not carry is not shown to be on. A
        /// requested scalar the material does not have is not shown to be
        /// on either. Any captured value that is not the written zero
        /// counts as on, so a non-finite gate scalar refuses.
        /// </summary>
        private static bool FeatureIsOn(
            CapturedMaterialEvidence evidence,
            string property)
        {
            return TryReadScalar(evidence, property, out var value)
                && value != 0f;
        }

        /// <summary>
        /// Reads a captured scalar. A name outside the request reads as
        /// absent: the evidence API refuses unrequested names as a
        /// programming defect, and this gate turns that outcome into the
        /// missing-fact policy above.
        /// </summary>
        private static bool TryReadScalar(
            CapturedMaterialEvidence evidence,
            string property,
            out float value)
        {
            try
            {
                return evidence.TryGetScalar(property, out value);
            }
            catch (ArgumentException)
            {
                value = 0f;
                return false;
            }
        }

        /// <summary>
        /// The vector counterpart of <see cref="TryReadScalar"/>. The vendor
        /// reads the dissolve and distance fade features from vector
        /// components (<c>Editor/lilMaterialUtils.cs:506-515</c> at the
        /// pin).
        /// </summary>
        private static bool TryReadVector(
            CapturedMaterialEvidence evidence,
            string property,
            out Vector4 value)
        {
            try
            {
                return evidence.TryGetVector(property, out value);
            }
            catch (ArgumentException)
            {
                value = default;
                return false;
            }
        }

        // Transcribed from lilShaderUtils.ContainsAfterLastSeparator
        // (Editor/lilShaderUtils.cs:192-202 at the pin), with an ordinal
        // search for a culture-independent result.
        private static bool NameContainsAfterLastSeparator(
            string shaderName,
            string token)
        {
            var separatorIndex = shaderName.LastIndexOf('/');
            if (separatorIndex == -1 || separatorIndex + 1 == shaderName.Length)
            {
                return false;
            }

            return shaderName.IndexOf(
                token,
                separatorIndex + 1,
                StringComparison.Ordinal) != -1;
        }

        // Transcribed from lilShaderUtils.IsOutlineShaderName
        // (Editor/lilShaderUtils.cs:54-77 at the pin), including the custom
        // name form *LIL_SHADER_NAME*/[Optional] OutlineOnly/….
        private static bool IsOutlineShaderName(string shaderName)
        {
            var separatorIndex = shaderName.LastIndexOf('/');
            if (separatorIndex == -1 || separatorIndex + 1 == shaderName.Length)
            {
                return false;
            }

            if (shaderName.IndexOf(
                "Outline",
                separatorIndex + 1,
                StringComparison.Ordinal) != -1)
            {
                return true;
            }

            var partIndex = shaderName.LastIndexOf(
                "/[Optional] OutlineOnly/",
                StringComparison.Ordinal);
            return partIndex != -1 && partIndex + 23 == separatorIndex;
        }
    }
}
