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
    /// writes at <c>Editor/lilMaterialUtils.cs:397-468</c>. The transcription
    /// covers every keyword the writer produces on the supported containers:
    /// the two mode keywords, the dither keyword, the alpha mask keyword,
    /// the layer dissolve keyword, the distance fade keyword, the outline
    /// tone keyword, and the full feature-keyword set (shadow, rim shade,
    /// emission and its blend masks, normal maps, anisotropy, matcaps, rim,
    /// glitter, audio link, backlight, parallax, reflection, main second
    /// and third with their dissolve and decal-animation states). A real
    /// avatar material carries that feature set on every vendor inspector
    /// save, so a table limited to the mode rows would refuse materials the
    /// vendor itself considers consistent. The two animation-derived color
    /// keywords stay tolerated in both directions: the vendor's animation
    /// pass can enable them beyond the static writer state
    /// (<c>SetupMultiMaterial(Material[], AnimationClip[])</c> at the pin),
    /// so their presence never refuses and their absence never refuses.
    /// Every other keyword a material carries is outside the table, so its
    /// captured presence is a state the derivation cannot produce and
    /// refuses as a mismatch.
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
        /// <summary>
        /// The mode the conversion recipe writes keyword state for. The
        /// derivation exposure below exists so the recipe writes the pinned
        /// table's own output, never a second hand-list.
        /// </summary>
        internal const int OpaqueMode = 0;
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
        private const string UseShadowProperty = "_UseShadow";
        private const string UseRimShadeProperty = "_UseRimShade";
        private const string UseEmissionProperty = "_UseEmission";
        private const string UseEmission2ndProperty = "_UseEmission2nd";
        private const string EmissionBlendMaskProperty = "_EmissionBlendMask";
        private const string Emission2ndBlendMaskProperty =
            "_Emission2ndBlendMask";
        private const string UseBumpMapProperty = "_UseBumpMap";
        private const string UseBump2ndMapProperty = "_UseBump2ndMap";
        private const string UseAnisotropyProperty = "_UseAnisotropy";
        private const string UseMatCapProperty = "_UseMatCap";
        private const string UseMatCap2ndProperty = "_UseMatCap2nd";
        private const string MatCapCustomNormalProperty = "_MatCapCustomNormal";
        private const string MatCap2ndCustomNormalProperty =
            "_MatCap2ndCustomNormal";
        private const string UseRimProperty = "_UseRim";
        private const string RimDirStrengthProperty = "_RimDirStrength";
        private const string UseGlitterProperty = "_UseGlitter";
        private const string UseAudioLinkProperty = "_UseAudioLink";
        private const string AudioLinkAsLocalProperty = "_AudioLinkAsLocal";
        private const string UseBacklightProperty = "_UseBacklight";
        private const string UseParallaxProperty = "_UseParallax";
        private const string UsePomProperty = "_UsePOM";
        private const string UseReflectionProperty = "_UseReflection";
        private const string MainGradationStrengthProperty =
            "_MainGradationStrength";
        private const string MainTexHsvgProperty = "_MainTexHSVG";
        private const string UseMain2ndTexProperty = "_UseMain2ndTex";
        private const string UseMain3rdTexProperty = "_UseMain3rdTex";
        private const string Main2ndTexDecalAnimationProperty =
            "_Main2ndTexDecalAnimation";
        private const string Main3rdTexDecalAnimationProperty =
            "_Main3rdTexDecalAnimation";
        private const string Main2ndDissolveParamsProperty =
            "_Main2ndDissolveParams";
        private const string Main3rdDissolveParamsProperty =
            "_Main3rdDissolveParams";

        /// <summary>
        /// The vendor written-off outline tone color
        /// (<c>lilConstants.defaultHSVG</c> at the pin). A material whose
        /// outline tone or main texture HSVG vector holds this exact value
        /// derives no keyword.
        /// </summary>
        private static readonly Vector4 DefaultHsvgColor =
            new Vector4(0f, 1f, 1f, 1f);

        /// <summary>
        /// The vendor default decal animation vector
        /// (<c>lilConstants.defaultDecalAnim</c> at the pin). A material
        /// whose decal animation vector holds this exact value derives no
        /// keyword.
        /// </summary>
        private static readonly Vector4 DefaultDecalAnim =
            new Vector4(1f, 1f, 1f, 30f);

        /// <summary>
        /// Every property name the derivation table's producing conditions
        /// can consult, split by evidence kind. The capture request that
        /// feeds this gate must carry every name: an unrequested name reads
        /// as feature-off, so a missing request entry silently turns a row
        /// off for every real material. The request agreement fixture pins
        /// the union against the production Multi request.
        /// </summary>
        internal static readonly string[] ConsultedScalarProperties =
        {
            ClippingCancellerProperty,
            OverlayProperty,
            DitherProperty,
            AlphaMaskModeProperty,
            UseShadowProperty,
            UseRimShadeProperty,
            UseEmissionProperty,
            UseEmission2ndProperty,
            UseBumpMapProperty,
            UseBump2ndMapProperty,
            UseAnisotropyProperty,
            UseMatCapProperty,
            UseMatCap2ndProperty,
            MatCapCustomNormalProperty,
            MatCap2ndCustomNormalProperty,
            UseRimProperty,
            RimDirStrengthProperty,
            UseGlitterProperty,
            UseAudioLinkProperty,
            AudioLinkAsLocalProperty,
            UseBacklightProperty,
            UseParallaxProperty,
            UsePomProperty,
            UseReflectionProperty,
            MainGradationStrengthProperty,
            UseMain2ndTexProperty,
            UseMain3rdTexProperty,
        };

        internal static readonly string[] ConsultedVectorProperties =
        {
            DissolveParamsProperty,
            DistanceFadeProperty,
            OutlineToneProperty,
            MainTexHsvgProperty,
            Main2ndTexDecalAnimationProperty,
            Main3rdTexDecalAnimationProperty,
            Main2ndDissolveParamsProperty,
            Main3rdDissolveParamsProperty,
        };

        internal static readonly string[] ConsultedTextureProperties =
        {
            EmissionBlendMaskProperty,
            Emission2ndBlendMaskProperty,
        };

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
                Keyword = "_REQUIRE_UV2",
                Produces = ProducesShadow,
            },
            new KeywordDerivationRow
            {
                Keyword = "AUTO_KEY_VALUE",
                Produces = ProducesRimShade,
            },
            new KeywordDerivationRow
            {
                Keyword = "_EMISSION",
                Produces = ProducesEmission,
            },
            new KeywordDerivationRow
            {
                Keyword = "GEOM_TYPE_BRANCH",
                Produces = ProducesEmission2nd,
            },
            new KeywordDerivationRow
            {
                Keyword = "_SUNDISK_SIMPLE",
                Produces = ProducesEmissionBlendMask,
            },
            new KeywordDerivationRow
            {
                Keyword = "_NORMALMAP",
                Produces = ProducesBumpMap,
            },
            new KeywordDerivationRow
            {
                Keyword = "EFFECT_BUMP",
                Produces = ProducesBump2ndMap,
            },
            new KeywordDerivationRow
            {
                Keyword = "SOURCE_GBUFFER",
                Produces = ProducesAnisotropy,
            },
            new KeywordDerivationRow
            {
                Keyword = "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                Produces = ProducesMatCap,
            },
            new KeywordDerivationRow
            {
                Keyword = "_SPECULARHIGHLIGHTS_OFF",
                Produces = ProducesMatCap2nd,
            },
            new KeywordDerivationRow
            {
                Keyword = "GEOM_TYPE_MESH",
                Produces = ProducesMatCapCustomNormal,
            },
            new KeywordDerivationRow
            {
                Keyword = "_METALLICGLOSSMAP",
                Produces = ProducesRim,
            },
            new KeywordDerivationRow
            {
                Keyword = "_SPECGLOSSMAP",
                Produces = ProducesGlitter,
            },
            new KeywordDerivationRow
            {
                Keyword = "_MAPPING_6_FRAMES_LAYOUT",
                Produces = ProducesAudioLink,
            },
            new KeywordDerivationRow
            {
                Keyword = "_SUNDISK_HIGH_QUALITY",
                Produces = ProducesAudioLinkAsLocal,
            },
            new KeywordDerivationRow
            {
                Keyword = "_COLORADDSUBDIFF_ON",
                Produces = ProducesMain2ndTex,
            },
            new KeywordDerivationRow
            {
                Keyword = "_COLORCOLOR_ON",
                Produces = ProducesMain3rdTex,
            },
            new KeywordDerivationRow
            {
                Keyword = "_SUNDISK_NONE",
                Produces = ProducesDecalAnimation,
            },
            new KeywordDerivationRow
            {
                Keyword = "GEOM_TYPE_FROND",
                Produces = ProducesMainDissolve,
            },
            new KeywordDerivationRow
            {
                Keyword = "ANTI_FLICKER",
                Produces = ProducesBacklight,
            },
            new KeywordDerivationRow
            {
                Keyword = "_PARALLAXMAP",
                Produces = ProducesParallax,
            },
            new KeywordDerivationRow
            {
                Keyword = "PIXELSNAP_ON",
                Produces = ProducesParallaxOcclusion,
            },
            new KeywordDerivationRow
            {
                Keyword = "_GLOSSYREFLECTIONS_OFF",
                Produces = ProducesReflection,
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
        /// The keyword set the pinned derivation produces for the captured
        /// facts and the mode: exactly the non-tolerated rows whose
        /// producing condition holds, which is the same set
        /// <see cref="Evaluate"/>'s second loop demands present. Tolerated
        /// rows never enter the set: their presence or absence never
        /// refuses, and on the NDMF play-mode path the vendor preprocess
        /// never writes them.
        /// <para>
        /// The conversion recipe writes this set verbatim through the
        /// keyword API, so the pinned table stays the only derivation in
        /// the codebase: a state this method produces is, by construction,
        /// a state <see cref="Evaluate"/> admits when the same request
        /// captures the material again.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// The mode is outside the admitted set. The derivation has no
        /// meaning for a mode the gate refuses; callers derive only
        /// admitted modes.
        /// </exception>
        internal static string[] DeriveKeywordSet(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (mode < OpaqueMode || mode > MaxAdmittedMode)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            var count = 0;
            for (var index = 0; index < DerivationTable.Length; index++)
            {
                var row = DerivationTable[index];
                if (!row.Tolerated && row.Produces(evidence, mode))
                {
                    count++;
                }
            }

            var derived = new string[count];
            var filled = 0;
            for (var index = 0; index < DerivationTable.Length; index++)
            {
                var row = DerivationTable[index];
                if (!row.Tolerated && row.Produces(evidence, mode))
                {
                    derived[filled++] = row.Keyword;
                }
            }

            return derived;
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
                && tone != DefaultHsvgColor;
        }

        // Editor/lilMaterialUtils.cs:414 at the pin, with the feature read
        // at :364. The gem branch forces the keyword off at :405 for a name
        // family no admitted container belongs to.
        private static bool ProducesShadow(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseShadowProperty);
        }

        // Editor/lilMaterialUtils.cs:415 at the pin, with the feature read
        // at :365.
        private static bool ProducesRimShade(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseRimShadeProperty);
        }

        // Editor/lilMaterialUtils.cs:418 at the pin, with the feature read
        // at :368.
        private static bool ProducesEmission(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseEmissionProperty);
        }

        // Editor/lilMaterialUtils.cs:419 at the pin, with the feature read
        // at :369.
        private static bool ProducesEmission2nd(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseEmission2ndProperty);
        }

        // Editor/lilMaterialUtils.cs:420 at the pin. The blend masks are
        // texture-presence features read at :387-388 through
        // IsFeatureOnTexture.
        private static bool ProducesEmissionBlendMask(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return (FeatureIsOn(evidence, UseEmissionProperty)
                    && TextureIsAssigned(evidence, EmissionBlendMaskProperty))
                || (FeatureIsOn(evidence, UseEmission2ndProperty)
                    && TextureIsAssigned(
                        evidence, Emission2ndBlendMaskProperty));
        }

        // Editor/lilMaterialUtils.cs:422 at the pin, with the feature read
        // at :370.
        private static bool ProducesBumpMap(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseBumpMapProperty);
        }

        // Editor/lilMaterialUtils.cs:423 at the pin, with the feature read
        // at :371.
        private static bool ProducesBump2ndMap(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseBump2ndMapProperty);
        }

        // Editor/lilMaterialUtils.cs:424 at the pin, with the feature read
        // at :372.
        private static bool ProducesAnisotropy(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseAnisotropyProperty);
        }

        // Editor/lilMaterialUtils.cs:425 at the pin, with the feature read
        // at :373.
        private static bool ProducesMatCap(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseMatCapProperty);
        }

        // Editor/lilMaterialUtils.cs:426 at the pin, with the feature read
        // at :374.
        private static bool ProducesMatCap2nd(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseMatCap2ndProperty);
        }

        // Editor/lilMaterialUtils.cs:427 at the pin, with the custom normal
        // features read at :375-376.
        private static bool ProducesMatCapCustomNormal(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return (FeatureIsOn(evidence, UseMatCapProperty)
                    && FeatureIsOn(evidence, MatCapCustomNormalProperty))
                || (FeatureIsOn(evidence, UseMatCap2ndProperty)
                    && FeatureIsOn(evidence, MatCap2ndCustomNormalProperty));
        }

        // Editor/lilMaterialUtils.cs:428 at the pin, with the feature read
        // at :377.
        private static bool ProducesRim(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseRimProperty);
        }

        // Editor/lilMaterialUtils.cs:430 at the pin, with the feature read
        // at :379.
        private static bool ProducesGlitter(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseGlitterProperty);
        }

        // Editor/lilMaterialUtils.cs:431 at the pin, with the feature read
        // at :380.
        private static bool ProducesAudioLink(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseAudioLinkProperty);
        }

        // Editor/lilMaterialUtils.cs:432 at the pin, with the local audio
        // link feature read at :381.
        private static bool ProducesAudioLinkAsLocal(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseAudioLinkProperty)
                && FeatureIsOn(evidence, AudioLinkAsLocalProperty);
        }

        // Editor/lilMaterialUtils.cs:448 at the pin, with the main second
        // feature read at :381 and the decal animation vectors read at
        // :389-390 through IsFeatureOnDecalAnimation.
        private static bool ProducesMain2ndTex(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseMain2ndTexProperty);
        }

        // Editor/lilMaterialUtils.cs:449 at the pin, with the main third
        // feature read at :382.
        private static bool ProducesMain3rdTex(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseMain3rdTexProperty);
        }

        // Editor/lilMaterialUtils.cs:450 at the pin, with the main second
        // feature read at :381 and the decal animation vectors read at
        // :389-390 through IsFeatureOnDecalAnimation.
        private static bool ProducesDecalAnimation(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return (FeatureIsOn(evidence, UseMain2ndTexProperty)
                    && VectorDiffers(
                        evidence,
                        Main2ndTexDecalAnimationProperty,
                        DefaultDecalAnim))
                || (FeatureIsOn(evidence, UseMain3rdTexProperty)
                    && VectorDiffers(
                        evidence,
                        Main3rdTexDecalAnimationProperty,
                        DefaultDecalAnim));
        }

        // Editor/lilMaterialUtils.cs:451 at the pin, with the dissolve
        // features read at :382-383 through IsFeatureOnVectorX.
        private static bool ProducesMainDissolve(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return (FeatureIsOn(evidence, UseMain2ndTexProperty)
                    && VectorComponentIsNonZero(
                        evidence, Main2ndDissolveParamsProperty))
                || (FeatureIsOn(evidence, UseMain3rdTexProperty)
                    && VectorComponentIsNonZero(
                        evidence, Main3rdDissolveParamsProperty));
        }

        // Editor/lilMaterialUtils.cs:453 at the pin, with the feature read
        // at :384.
        private static bool ProducesBacklight(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseBacklightProperty);
        }

        // Editor/lilMaterialUtils.cs:454 at the pin, with the feature read
        // at :385.
        private static bool ProducesParallax(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseParallaxProperty);
        }

        // Editor/lilMaterialUtils.cs:455 at the pin, with the POM feature
        // read at :386.
        private static bool ProducesParallaxOcclusion(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseParallaxProperty)
                && FeatureIsOn(evidence, UsePomProperty);
        }

        // Editor/lilMaterialUtils.cs:456 at the pin, with the feature read
        // at :387.
        private static bool ProducesReflection(
            CapturedMaterialEvidence evidence,
            int mode)
        {
            return FeatureIsOn(evidence, UseReflectionProperty);
        }

        /// <summary>
        /// True when the captured evidence shows the named texture slot
        /// assigned. A slot the request did not carry reads as unassigned,
        /// mirroring the vendor fallback that treats a missing property as
        /// feature-off (<c>Editor/lilMaterialUtils.cs:530-534</c> at the
        /// pin, through IsFeatureOnTexture).
        /// </summary>
        private static bool TextureIsAssigned(
            CapturedMaterialEvidence evidence,
            string property)
        {
            try
            {
                return evidence.TryGetTexture(property, out _);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// True when the named captured vector exists and differs from the
        /// given vendor default. The vendor's IsFeatureOnDecalAnimation
        /// (<c>Editor/lilMaterialUtils.cs:524-528</c> at the pin) compares
        /// the whole vector against the constant.
        /// </summary>
        private static bool VectorDiffers(
            CapturedMaterialEvidence evidence,
            string property,
            Vector4 defaultVector)
        {
            return TryReadVector(evidence, property, out var value)
                && value != defaultVector;
        }

        /// <summary>
        /// True when the named captured vector exists and its x component
        /// is nonzero. The vendor's IsFeatureOnVectorX
        /// (<c>Editor/lilMaterialUtils.cs:512-517</c> at the pin).
        /// </summary>
        private static bool VectorComponentIsNonZero(
            CapturedMaterialEvidence evidence,
            string property)
        {
            return TryReadVector(evidence, property, out var value)
                && value.x != 0f;
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
