using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Tests.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Canonicalization, digesting, define and render-mode scanning, exact
    /// version comparison, and the verification conjunction. Every case is
    /// deterministic and needs no installed lilToon package.
    /// </summary>
    public sealed class LilToonAttestationTests
    {

        /// <summary>
        /// Sentinel meaning "use the valid pin". A plain null default would make
        /// it impossible to test genuinely missing evidence, because null would
        /// be coalesced back to the pin.
        /// </summary>
        private const string UsePin = "\0use-pinned-digest";
        private static readonly (string RelativePath, string Stamp)[]
            AdditionalIncludes =
            {
                ("VRC Light Volumes/LightVolumes.cginc", "22"),
            };

        private static LilToonIncludeTree Tree()
        {
            return AttestationEnvironment.Tree(AdditionalIncludes);
        }

        private static string Canon(string source)
        {
            return AttestationEnvironment.Canon(
                source, AdditionalIncludes);
        }

        private static LilToonCanonicalizationAnalysis Analyze(string source)
        {
            return LilToonSourceAttestation.AnalyzeCanonicalization(
                source,
                AttestationEnvironment.ShaderDir,
                AttestationEnvironment.ProjectRoot,
                Tree());
        }

        private static LilToonCanonicalizationAnalysis EmptyShaderAnalysis()
        {
            return Analyze("Shader \"lilToon\"\n{\n}\n");
        }

        private static LilToonCanonicalizationAnalysis PassAnalysis(
            IEnumerable<string> settingRecords,
            string beforeSecondEnd = null)
        {
            return Analyze(
                "HLSLINCLUDE\n" +
                "    #define LIL_RENDER 0\n" +
                "ENDHLSL\n" +
                "HLSLINCLUDE\n" +
                string.Join("\n", settingRecords.Select(line => "    " + line)) +
                "\n    #pragma target 3.5\n" +
                (beforeSecondEnd ?? string.Empty) +
                "ENDHLSL\n");
        }

        private static List<string> DefaultStandaloneRecords()
        {
            var identifiers = new[]
            {
                "LIL_FEATURE_ANIMATE_MAIN_UV",
                "LIL_FEATURE_MAIN_TONE_CORRECTION",
                "LIL_FEATURE_MAIN_GRADATION_MAP",
                "LIL_FEATURE_MAIN2ND",
                "LIL_FEATURE_MAIN3RD",
                "LIL_FEATURE_DECAL",
                "LIL_FEATURE_ANIMATE_DECAL",
                "LIL_FEATURE_LAYER_DISSOLVE",
                "LIL_FEATURE_ALPHAMASK",
                "LIL_FEATURE_SHADOW",
                "LIL_FEATURE_RECEIVE_SHADOW",
                "LIL_FEATURE_SHADOW_3RD",
                "LIL_FEATURE_SHADOW_LUT",
                "LIL_FEATURE_RIMSHADE",
                "LIL_FEATURE_EMISSION_1ST",
                "LIL_FEATURE_EMISSION_2ND",
                "LIL_FEATURE_ANIMATE_EMISSION_UV",
                "LIL_FEATURE_ANIMATE_EMISSION_MASK_UV",
                "LIL_FEATURE_EMISSION_GRADATION",
                "LIL_FEATURE_NORMAL_1ST",
                "LIL_FEATURE_NORMAL_2ND",
                "LIL_FEATURE_ANISOTROPY",
                "LIL_FEATURE_REFLECTION",
                "LIL_FEATURE_MATCAP",
                "LIL_FEATURE_MATCAP_2ND",
                "LIL_FEATURE_RIMLIGHT",
                "LIL_FEATURE_RIMLIGHT_DIRECTION",
                "LIL_FEATURE_GLITTER",
                "LIL_FEATURE_BACKLIGHT",
                "LIL_FEATURE_PARALLAX",
                "LIL_FEATURE_POM",
                "LIL_FEATURE_DISTANCE_FADE",
                "LIL_FEATURE_AUDIOLINK",
                "LIL_FEATURE_AUDIOLINK_VERTEX",
                "LIL_FEATURE_AUDIOLINK_LOCAL",
                "LIL_FEATURE_DISSOLVE",
                "LIL_FEATURE_DITHER",
                "LIL_FEATURE_IDMASK",
                "LIL_FEATURE_UDIMDISCARD",
                "LIL_FEATURE_OUTLINE_TONE_CORRECTION",
                "LIL_FEATURE_OUTLINE_RECEIVE_SHADOW",
                "LIL_FEATURE_ANIMATE_OUTLINE_UV",
                "LIL_FEATURE_FUR_COLLISION",
                "LIL_FEATURE_MainGradationTex",
                "LIL_FEATURE_MainColorAdjustMask",
                "LIL_FEATURE_Main2ndTex",
                "LIL_FEATURE_Main2ndBlendMask",
                "LIL_FEATURE_Main2ndDissolveMask",
                "LIL_FEATURE_Main2ndDissolveNoiseMask",
                "LIL_FEATURE_Main3rdTex",
                "LIL_FEATURE_Main3rdBlendMask",
                "LIL_FEATURE_Main3rdDissolveMask",
                "LIL_FEATURE_Main3rdDissolveNoiseMask",
                "LIL_FEATURE_AlphaMask",
                "LIL_FEATURE_BumpMap",
                "LIL_FEATURE_Bump2ndMap",
                "LIL_FEATURE_Bump2ndScaleMask",
                "LIL_FEATURE_AnisotropyTangentMap",
                "LIL_FEATURE_AnisotropyScaleMask",
                "LIL_FEATURE_AnisotropyShiftNoiseMask",
                "LIL_FEATURE_ShadowBorderMask",
                "LIL_FEATURE_ShadowBlurMask",
                "LIL_FEATURE_ShadowStrengthMask",
                "LIL_FEATURE_ShadowColorTex",
                "LIL_FEATURE_Shadow2ndColorTex",
                "LIL_FEATURE_Shadow3rdColorTex",
                "LIL_FEATURE_RimShadeMask",
                "LIL_FEATURE_BacklightColorTex",
                "LIL_FEATURE_SmoothnessTex",
                "LIL_FEATURE_MetallicGlossMap",
                "LIL_FEATURE_ReflectionColorTex",
                "LIL_FEATURE_ReflectionCubeTex",
                "LIL_FEATURE_MatCapTex",
                "LIL_FEATURE_MatCapBlendMask",
                "LIL_FEATURE_MatCapBumpMap",
                "LIL_FEATURE_MatCap2ndTex",
                "LIL_FEATURE_MatCap2ndBlendMask",
                "LIL_FEATURE_MatCap2ndBumpMap",
                "LIL_FEATURE_RimColorTex",
                "LIL_FEATURE_GlitterColorTex",
                "LIL_FEATURE_GlitterShapeTex",
                "LIL_FEATURE_EmissionMap",
                "LIL_FEATURE_EmissionBlendMask",
                "LIL_FEATURE_EmissionGradTex",
                "LIL_FEATURE_Emission2ndMap",
                "LIL_FEATURE_Emission2ndBlendMask",
                "LIL_FEATURE_Emission2ndGradTex",
                "LIL_FEATURE_ParallaxMap",
                "LIL_FEATURE_AudioLinkMask",
                "LIL_FEATURE_AudioLinkLocalMap",
                "LIL_FEATURE_DissolveMask",
                "LIL_FEATURE_DissolveNoiseMask",
                "LIL_FEATURE_OutlineTex",
                "LIL_FEATURE_OutlineWidthMask",
                "LIL_FEATURE_OutlineVectorTex",
                "LIL_FEATURE_FurNoiseMask",
                "LIL_FEATURE_FurMask",
                "LIL_FEATURE_FurLengthMask",
                "LIL_FEATURE_FurVectorTex",
                "LIL_OPTIMIZE_APPLY_SHADOW_FA",
                "LIL_OPTIMIZE_USE_FORWARDADD",
                "LIL_OPTIMIZE_USE_VERTEXLIGHT",
            };
            var records = identifiers
                .Select(identifier => "#define " + identifier)
                .ToList();
            records.Add("#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE");
            Assert.That(records, Has.Count.EqualTo(103));
            return records;
        }

        private static List<string> StrippedStandaloneRecords()
        {
            var removed = new HashSet<string>(new[]
            {
                "#define LIL_FEATURE_RECEIVE_SHADOW",
                "#define LIL_FEATURE_EMISSION_1ST",
                "#define LIL_FEATURE_EMISSION_2ND",
                "#define LIL_FEATURE_ANIMATE_EMISSION_UV",
                "#define LIL_FEATURE_ANIMATE_EMISSION_MASK_UV",
                "#define LIL_FEATURE_EMISSION_GRADATION",
                "#define LIL_FEATURE_NORMAL_1ST",
                "#define LIL_FEATURE_NORMAL_2ND",
                "#define LIL_FEATURE_BACKLIGHT",
                "#define LIL_FEATURE_OUTLINE_RECEIVE_SHADOW",
                "#define LIL_FEATURE_BumpMap",
                "#define LIL_FEATURE_EmissionMap",
            }, StringComparer.Ordinal);
            var records = DefaultStandaloneRecords()
                .Where(record => !removed.Contains(record))
                .ToList();
            Assert.That(records, Has.Count.EqualTo(91));
            return records;
        }

        private static void InsertAfter(
            List<string> records,
            string predecessor,
            string record)
        {
            records.Insert(records.IndexOf(predecessor) + 1, record);
        }

        private static readonly object[] GeneratorDependencies =
        {
            new object[] { "LIL_FEATURE_DECAL", new[] { "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD" } },
            new object[] { "LIL_FEATURE_ANIMATE_DECAL", new[] { "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD" } },
            new object[] { "LIL_FEATURE_LAYER_DISSOLVE", new[] { "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD" } },
            new object[] { "LIL_FEATURE_RECEIVE_SHADOW", new[] { "LIL_FEATURE_SHADOW" } },
            new object[] { "LIL_FEATURE_SHADOW_3RD", new[] { "LIL_FEATURE_SHADOW" } },
            new object[] { "LIL_FEATURE_SHADOW_LUT", new[] { "LIL_FEATURE_SHADOW" } },
            new object[] { "LIL_FEATURE_ANIMATE_EMISSION_UV", new[] { "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND" } },
            new object[] { "LIL_FEATURE_ANIMATE_EMISSION_MASK_UV", new[] { "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND" } },
            new object[] { "LIL_FEATURE_EMISSION_GRADATION", new[] { "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND" } },
            new object[] { "LIL_FEATURE_RIMLIGHT_DIRECTION", new[] { "LIL_FEATURE_RIMLIGHT" } },
            new object[] { "LIL_FEATURE_POM", new[] { "LIL_FEATURE_PARALLAX" } },
            new object[] { "LIL_FEATURE_AUDIOLINK_VERTEX", new[] { "LIL_FEATURE_AUDIOLINK" } },
            new object[] { "LIL_FEATURE_AUDIOLINK_LOCAL", new[] { "LIL_FEATURE_AUDIOLINK" } },
        };

        private static LilToonSourceEvidence Evidence(
            string shaderName = "lilToon",
            string assetGuid = "df12117ecd77c31469c224178886498e",
            bool hasVersion = true,
            float version = 45f,
            bool hasPackage = true,
            string packageName = "jp.lilxyzw.liltoon",
            string packageVersion = LilToonSourceAttestation.PackageVersion,
            string passGuid = "61b4f98a5d78b4a4a9d89180fac793fc",
            string shaderDigest = UsePin,
            string passDigest = UsePin,
            string includeDigest = UsePin,
            bool hasRenderMode = true,
            int renderMode = 0,
            IReadOnlyCollection<string> features = null,
            bool hasShaderCanonicalization = true,
            LilToonCanonicalizationAnalysis shaderCanonicalization = null,
            bool hasPassCanonicalization = true,
            LilToonCanonicalizationAnalysis passCanonicalization = null)
        {
            return new LilToonSourceEvidence(
                shaderName,
                assetGuid,
                hasVersion,
                version,
                hasPackage,
                packageName,
                packageVersion,
                passGuid,
                ReferenceEquals(shaderDigest, UsePin)
                    ? LilToonSourceAttestation.ShaderCanonicalDigest
                    : shaderDigest,
                ReferenceEquals(passDigest, UsePin)
                    ? LilToonSourceAttestation.PassCanonicalDigest
                    : passDigest,
                ReferenceEquals(includeDigest, UsePin)
                    ? LilToonSourceAttestation.IncludeTreeDigest
                    : includeDigest,
                hasRenderMode,
                renderMode,
                features ?? new string[0],
                hasShaderCanonicalization
                    ? shaderCanonicalization ?? EmptyShaderAnalysis()
                    : null,
                hasPassCanonicalization
                    ? passCanonicalization ?? PassAnalysis(DefaultStandaloneRecords())
                    : null);
        }

        // --- normalized hashing ---

        [Test]
        public void ComputeNormalizedSourceHash_IgnoresBomAndLineEndings()
        {
            const string lf = "float4 c;\nreturn c;\n";
            var crlf = "﻿" + lf.Replace("\n", "\r\n");
            var cr = lf.Replace("\n", "\r");
            var expected = LilToonSourceAttestation.ComputeNormalizedSourceHash(lf);

            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(crlf),
                Is.EqualTo(expected));
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(cr),
                Is.EqualTo(expected));
        }

        [Test]
        public void ComputeNormalizedSourceHash_DetectsContentEdit()
        {
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash("a\n"),
                Is.Not.EqualTo(
                    LilToonSourceAttestation.ComputeNormalizedSourceHash("b\n")));
        }

        // --- R1: the setting region ---

        [Test]
        public void AnalyzeCanonicalization_RecordsEveryHlslIncludeRegionInOrder()
        {
            const string source =
                "HLSLINCLUDE\n" +
                "    #define LIL_RENDER 0\n" +
                "ENDHLSL\n" +
                "HLSLINCLUDE\n" +
                "    #define LIL_FEATURE_MAIN2ND\n" +
                "    #pragma skip_variants LIGHTPROBE_SH\n" +
                "    #pragma target 3.5\n" +
                "ENDHLSL\n";

            var analysis = Analyze(source);

            Assert.That(analysis.RemovedRegions, Has.Count.EqualTo(2));
            Assert.That(analysis.RemovedRegions[0].HlslIncludeOrdinal, Is.Zero);
            Assert.That(analysis.RemovedRegions[0].HlslIncludeLineIndex, Is.Zero);
            Assert.That(analysis.RemovedRegions[0].Records, Is.Empty);
            Assert.That(analysis.RemovedRegions[1].HlslIncludeOrdinal, Is.EqualTo(1));
            Assert.That(analysis.RemovedRegions[1].HlslIncludeLineIndex, Is.EqualTo(3));
            Assert.That(analysis.RemovedRegions[1].Records, Has.Count.EqualTo(2));
            Assert.That(analysis.RemovedRegions[1].Records[0].LineIndex, Is.EqualTo(4));
            Assert.That(analysis.RemovedRegions[1].Records[0].OffsetInRegion, Is.Zero);
            Assert.That(
                analysis.RemovedRegions[1].Records[0].Kind,
                Is.EqualTo(LilToonRemovedRecordKind.Define));
            Assert.That(
                analysis.RemovedRegions[1].Records[0].Text,
                Is.EqualTo("#define LIL_FEATURE_MAIN2ND"));
            Assert.That(
                analysis.RemovedRegions[1].Records[1].Kind,
                Is.EqualTo(LilToonRemovedRecordKind.SkipVariants));
        }

        [Test]
        public void AnalyzeCanonicalization_RecordsKnownActivatorsAnywhere()
        {
            const string source =
                "#define LIL_FEATURE_LTCGI\n" +
                "HLSLINCLUDE\n" +
                "  #define  LIL_FEATURE_AUDIOLINK_PACKAGE\n" +
                "  #pragma target 3.5\n" +
                "#define LIL_FEATURE_VRCLIGHTVOLUMES 1\n";

            var analysis = Analyze(source);

            Assert.That(
                analysis.Activators.Select(value => value.Identifier),
                Is.EqualTo(new[]
                {
                    "LIL_FEATURE_LTCGI",
                    "LIL_FEATURE_AUDIOLINK_PACKAGE",
                    "LIL_FEATURE_VRCLIGHTVOLUMES",
                }));
            Assert.That(
                analysis.Activators.Select(value => value.LineIndex),
                Is.EqualTo(new[] { 0, 2, 4 }));
            Assert.That(
                analysis.Activators.Select(value => value.ProvenSlot),
                Is.EqualTo(new[] { false, true, false }));
        }

        [Test]
        public void AnalyzeCanonicalization_IgnoresNonDefineActivatorMentions()
        {
            const string source =
                "// #define LIL_FEATURE_LTCGI\n" +
                "const char* name = \"LIL_FEATURE_LTCGI\";\n" +
                "#undef LIL_FEATURE_LTCGI\n";

            Assert.That(Analyze(source).Activators, Is.Empty);
        }

        [Test]
        public void AnalyzeCanonicalization_HiddenUnknownRecordKeepsOldCanonicalOutput()
        {
            const string clean =
                "HLSLINCLUDE\n" +
                "    #define LIL_FEATURE_MAIN2ND\n" +
                "    #pragma target 3.5\n";
            const string mutated =
                "HLSLINCLUDE\n" +
                "    #define LIL_FEATURE_MAIN2ND\n" +
                "    #define LIL_FEATURE_AMUSE_UNKNOWN\n" +
                "    #pragma target 3.5\n";

            Assert.That(
                Analyze(mutated).CanonicalSource,
                Is.EqualTo(Analyze(clean).CanonicalSource));
            Assert.That(
                Analyze(mutated).RemovedRegions[0].Records
                    .Select(value => value.Text),
                Does.Contain("#define LIL_FEATURE_AMUSE_UNKNOWN"));
        }

        [Test]
        public void ProvenanceCollections_DefensivelyCopyAndExposeReadOnlyViews()
        {
            var recordInput = new List<LilToonRemovedRecord>
            {
                new LilToonRemovedRecord(
                    1, 0, LilToonRemovedRecordKind.Define,
                    "#define LIL_FEATURE_MAIN2ND"),
            };
            var region = new LilToonRemovedRegion(0, 0, recordInput);
            var regionInput = new List<LilToonRemovedRegion> { region };
            var activatorInput = new List<LilToonActivatorOccurrence>
            {
                new LilToonActivatorOccurrence(
                    2,
                    "LIL_FEATURE_LTCGI",
                    false),
            };
            var analysis = new LilToonCanonicalizationAnalysis(
                string.Empty, regionInput, activatorInput);

            recordInput.Clear();
            regionInput.Clear();
            activatorInput.Clear();

            Assert.That(region.Records, Has.Count.EqualTo(1));
            Assert.That(region.Records, Is.Not.InstanceOf<LilToonRemovedRecord[]>());
            Assert.That(analysis.RemovedRegions, Has.Count.EqualTo(1));
            Assert.That(analysis.Activators, Has.Count.EqualTo(1));
            Assert.That(
                analysis.RemovedRegions,
                Is.Not.InstanceOf<LilToonRemovedRegion[]>());
            Assert.That(
                analysis.Activators,
                Is.Not.InstanceOf<LilToonActivatorOccurrence[]>());

            Assert.Throws<NotSupportedException>(() =>
                ((IList<LilToonRemovedRecord>)region.Records).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LilToonRemovedRegion>)analysis.RemovedRegions).Clear());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<LilToonActivatorOccurrence>)analysis.Activators).Clear());
        }

        [Test]
        public void Canonicalize_DropsSettingRegionInsideHlslInclude()
        {
            const string withFeatures =
                "SubShader\n{\n    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #define LIL_OPTIMIZE_USE_FORWARDADD\n" +
                "        #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n}\n";
            const string withoutFeatures =
                "SubShader\n{\n    HLSLINCLUDE\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(withFeatures), Is.EqualTo(Canon(withoutFeatures)));
        }

        [Test]
        public void Canonicalize_InjectedFeatureDefineAfterSettingRegion_IsRetained()
        {
            const string clean =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n";
            const string injected =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "        #define LIL_FEATURE_BumpMap\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
            Assert.That(Canon(injected), Does.Contain("LIL_FEATURE_BumpMap"));
        }

        [Test]
        public void Canonicalize_InjectedFeatureDefineInPassBody_IsRetained()
        {
            const string clean =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n}\n";
            const string injected =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    #define LIL_FEATURE_EmissionMap\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
            Assert.That(Canon(injected), Does.Contain("LIL_FEATURE_EmissionMap"));
        }

        [Test]
        public void Canonicalize_InjectedSkipVariantsOutsideSettingRegion_IsRetained()
        {
            const string clean =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n}\n";
            const string injected =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    #pragma skip_variants EVIL\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
        }

        [Test]
        public void Canonicalize_ConstantSkipVariantsAfterSettingRegion_IsRemoved()
        {
            // The Get* literals are fixed, but the unpacker's dedup pass
            // redistributes their tokens across slots, so a fixed-literal line
            // or any leftover fragment of one varies with settings. The
            // vocabulary decides removal; injected unknown tokens stay hashed.
            const string withTail =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "        #pragma skip_variants _DBUFFER_MRT3\n" +
                "    ENDHLSL\n";
            const string withoutTail =
                "    HLSLINCLUDE\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "        #pragma target 3.5\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(withTail), Is.EqualTo(Canon(withoutTail)));
        }

        [Test]
        public void Canonicalize_VocabularySkipVariantsLine_IsRemovedAnywhere()
        {
            // The unpacker's dedup pass redistributes skip tokens across the
            // slots, so a vocabulary line can survive at any position with any
            // token subset. Content, not position, decides removal. A
            // skip_variants line only prunes compile variants and cannot
            // change an AMUSE decision.
            var withLine =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n}\n";
            var withoutLine =
                "Pass\n{\n    HLSLPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n}\n";

            Assert.That(Canon(withLine), Is.EqualTo(Canon(withoutLine)));
        }

        [Test]
        public void Canonicalize_LtcgiDefineBeforePassForwardAnchor_IsRemoved()
        {
            // The container generator inserts this define directly before the
            // LIL_PASS_FORWARD terminal line of the built-in-RP forward block
            // when LTCGI is installed.
            var withDefine =
                "            #pragma multi_compile_fwdbase\n" +
                "            #define LIL_FEATURE_LTCGI\n" +
                "            #define LIL_PASS_FORWARD\n";
            var withoutDefine =
                "            #pragma multi_compile_fwdbase\n" +
                "            #define LIL_PASS_FORWARD\n";

            Assert.That(Canon(withDefine), Is.EqualTo(Canon(withoutDefine)));
        }

        [Test]
        public void Canonicalize_LtcgiDefineAwayFromForwardAnchor_IsRetained()
        {
            // Falsifier F4: without the exact next-line anchor the define is
            // not proven generator output and stays hashed.
            var away =
                "            #define LIL_FEATURE_LTCGI\n" +
                "            #pragma multi_compile_instancing\n" +
                "            #define LIL_PASS_FORWARD\n";

            Assert.That(Canon(away), Does.Contain("LIL_FEATURE_LTCGI"));
        }

        [Test]
        public void Canonicalize_ValuedLtcgiDefine_IsRetained()
        {
            // Falsifier F3: only the exact valueless generator form is proven.
            Assert.That(
                Canon("#define LIL_FEATURE_LTCGI 1\n"),
                Does.Contain("LIL_FEATURE_LTCGI"));
        }

        [Test]
        public void Canonicalize_AppendedLtcgiTagTokenInsideSubShader_IsRemoved()
        {
            const string clean =
                "Shader \"s\" {\n" +
                "    SubShader {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\"}\n" +
                "    }\n" +
                "}\n";
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Is.EqualTo(Canon(clean)));
        }

        [Test]
        public void Canonicalize_AppendedLtcgiTagTokenInsideAllmanSubShader_IsRemoved()
        {
            // The shipped 2.3.4 containers place the SubShader brace on its
            // own line. The R6 proof covers the token on that Tags line too.
            const string clean =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\"}\n" +
                "    }\n" +
                "}\n";
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Is.EqualTo(Canon(clean)));
        }

        // --- Falsifier F7: an Allman Pass block never grants SubShader
        // scope. Kills a repair that pushes SubShader for any keyword or
        // brace line. ---
        [Test]
        public void Canonicalize_AllmanPassScopeLtcgiToken_IsRetained()
        {
            const string clean =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Pass\n" +
                "        {\n" +
                "            Tags {\"LightMode\" = \"ForwardBase\"}\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Pass\n" +
                "        {\n" +
                "            Tags {\"LightMode\" = \"ForwardBase\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Is.Not.EqualTo(Canon(clean)));
        }

        // --- Falsifier F8: an anonymous brace block grants no scope. Kills a
        // repair that treats any bare brace line as SubShader. ---
        [Test]
        public void Canonicalize_AnonymousBraceBlockTagsLineLtcgiToken_IsRetained()
        {
            const string tagged =
                "Shader \"s\" {\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
        }

        // --- Falsifier F9: quoted SubShader text grants no scope. Kills a
        // repair that matches the keyword by containment. ---
        [Test]
        public void Canonicalize_QuotedSubShaderTextDoesNotOpenSubShaderScope()
        {
            const string tagged =
                "Shader \"s\" {\n" +
                "    // the text SubShader appears here\n" +
                "    \"SubShader\"\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
        }

        // --- Falsifier F10: a content line between keyword and brace breaks
        // the Allman pair. Kills a repair whose pending kind survives any
        // intervening line. ---
        [Test]
        public void Canonicalize_ContentBetweenSubShaderKeywordAndBrace_BreaksEligibility()
        {
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    LOD 200\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
        }

        // --- Falsifier F11: blank lines and comments between keyword and
        // brace keep the pair. Kills a repair that clears the pending kind on
        // blank or comment lines. ---
        [Test]
        public void Canonicalize_AllmanSubShaderBlankAndCommentLinesBeforeBrace_StillStrips()
        {
            const string clean =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "\n" +
                "    // a comment between keyword and brace\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\"}\n" +
                "    }\n" +
                "}\n";
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "\n" +
                "    // a comment between keyword and brace\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Is.EqualTo(Canon(clean)));
        }

        // --- Falsifier F12: a different value or key stays hashed in Allman
        // form. Kills a repair that strips any trailing quoted token. ---
        [Test]
        public void Canonicalize_AllmanSubShaderScopeTokenWithOtherLineEdits_IsRetained()
        {
            const string clean =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\"}\n" +
                "    }\n" +
                "}\n";
            const string otherValue =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"OFF\"}\n" +
                "    }\n" +
                "}\n";
            const string otherKey =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGIX\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(otherValue), Is.Not.EqualTo(Canon(clean)));
            Assert.That(Canon(otherKey), Is.Not.EqualTo(Canon(clean)));
        }

        // --- Falsifier F13: a SubShader keyword line followed by another
        // block's inline brace line grants no SubShader scope to that block.
        // Kills a repair whose pending kind attaches to any brace-carrying
        // line. ---
        [Test]
        public void Canonicalize_SubShaderKeywordBeforeInlinePassBlock_DoesNotGrantScope()
        {
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader\n" +
                "    Pass {\n" +
                "        Tags {\"LightMode\" = \"ForwardBase\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
        }

        [Test]
        public void Canonicalize_AppendedLtcgiTagTokenOutsideSubShaderScope_IsRetained()
        {
            const string tagged =
                "        Tags {\"RenderType\" = \"Opaque\" \"LTCGI\"=\"ALWAYS\"}\n";

            Assert.That(Canon(tagged), Does.Contain("\"LTCGI\"=\"ALWAYS\""));
        }

        [Test]
        public void Canonicalize_PassTagsLineWithLtcgiToken_IsRetained()
        {
            const string clean =
                "Shader \"s\" {\n" +
                "    SubShader {\n" +
                "        Pass {\n" +
                "            Tags {\"LightMode\" = \"ForwardBase\"}\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
            const string tagged =
                "Shader \"s\" {\n" +
                "    SubShader {\n" +
                "        Pass {\n" +
                "            Tags {\"LightMode\" = \"ForwardBase\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(tagged), Is.Not.EqualTo(Canon(clean)));
        }

        [Test]
        public void Canonicalize_OtherTagTokenEdit_IsRetained()
        {
            // Falsifier F1: only the exact LTCGI token in the final position
            // is proven generator output.
            const string clean =
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\"}\n";
            const string otherValue =
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGI\"=\"OFF\"}\n";
            const string otherKey =
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGIX\"=\"ALWAYS\"}\n";

            Assert.That(Canon(otherValue), Is.Not.EqualTo(Canon(clean)));
            Assert.That(Canon(otherKey), Is.Not.EqualTo(Canon(clean)));
        }

        [Test]
        public void Canonicalize_GeneratorShapes_AgreeOnOneForm()
        {
            // Falsifier F5 as a property: the shipped 2.3.4 bytes and a LTCGI
            // plus AudioLink regeneration differ only in generator-owned text,
            // so canonicalization must map both shapes to one form.
            var shipped =
                "Shader \"lilToon\"\n" +
                "{\n" +
                "    SubShader {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\"}\n" +
                "        HLSLINCLUDE\n" +
                "            #define LIL_FEATURE_MAIN2ND\n" +
                "            #pragma skip_variants LIGHTMAP_ON DYNAMICLIGHTMAP_ON LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED _MIXED_LIGHTING_SUBTRACTIVE\n" +
                "            #pragma target 3.5\n" +
                "            #pragma skip_variants DECALS_OFF DECALS_3RT DECALS_4RT DECAL_SURFACE_GRADIENT _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3\n" +
                "        ENDHLSL\n" +
                "        Pass\n" +
                "        {\n" +
                "            #pragma multi_compile_fwdbase\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
            var regenerated =
                "Shader \"lilToon\"\n" +
                "{\n" +
                "    SubShader {\n" +
                "        Tags {\"RenderType\" = \"Opaque\" \"Queue\" = \"Geometry\" \"LTCGI\"=\"ALWAYS\"}\n" +
                "        HLSLINCLUDE\n" +
                "            #define LIL_FEATURE_MAIN2ND\n" +
                "            #pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE\n" +
                "            #pragma target 3.5\n" +
                "            #pragma skip_variants _DBUFFER_MRT3\n" +
                "        ENDHLSL\n" +
                "        Pass\n" +
                "        {\n" +
                "            #pragma multi_compile_fwdbase\n" +
                "            #define LIL_FEATURE_LTCGI\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            Assert.That(Canon(regenerated), Is.EqualTo(Canon(shipped)));
        }


        [Test]
        public void OfficialSkipVariantVocabulary_IsClosedAndUnique()
        {
            var field = typeof(LilToonSourceAttestation).GetField(
                "OfficialSkipVariantVocabulary",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            var vocabulary = (string[])field.GetValue(null);

            Assert.That(vocabulary, Has.Length.EqualTo(33));
            Assert.That(
                vocabulary.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(33));
        }

        [Test]
        public void Canonicalize_RetainsValuedDefines()
        {
            Assert.That(
                Canon("    HLSLINCLUDE\n        #define LIL_RENDER 0\n    ENDHLSL\n"),
                Is.Not.EqualTo(
                    Canon("    HLSLINCLUDE\n        #define LIL_RENDER 2\n    ENDHLSL\n")));
        }

        [Test]
        public void Canonicalize_ShaderScopeRenderDefine_IsNeverASettingCandidate()
        {
            // A valued define is not a region-A line, so the run is empty and
            // LIL_RENDER survives even as the first line of an HLSLINCLUDE —
            // exactly its position in ltspass_opaque.lilinternal.
            var canon = Canon(
                "    HLSLINCLUDE\n" +
                "        #define LIL_RENDER 0\n" +
                "        #define LIL_FEATURE_MAIN2ND\n" +
                "    ENDHLSL\n");

            Assert.That(canon, Does.Contain("#define LIL_RENDER 0"));
            Assert.That(canon, Does.Contain("LIL_FEATURE_MAIN2ND"));
        }

        // --- R2: the shadow substitution slot ---

        private static string ForwardPrologue(string shadowSlotLine)
        {
            return
                "    HLSLPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #pragma multi_compile_fwdbase\n" +
                "            #pragma multi_compile_vertex _ FOG_LINEAR FOG_EXP FOG_EXP2\n" +
                "            #pragma multi_compile_instancing\n" +
                "            #define LIL_PASS_FORWARD\n" +
                shadowSlotLine +
                "\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n";
        }

        [Test]
        public void Canonicalize_ShadowSlotPragma_IsDropped()
        {
            var filled = ForwardPrologue(
                "            #pragma skip_variants SHADOW_VERY_HIGH\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(filled), Is.EqualTo(Canon(absent)));
            Assert.That(Canon(filled), Does.Not.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_ShadowSlotAbsentForm_NeedsNoBlankLineRule()
        {
            // Task 0 showed the empty expansion leaves no indentation-only
            // residue, so the absent form is already canonical: canonicalization
            // is the identity on it, trailing newline included.
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(absent), Is.EqualTo(absent));
        }

        [Test]
        public void Canonicalize_UnrelatedKeywordAtSlot_IsRetained()
        {
            // Correct anchor, wrong keyword. SHADOW_VERY_HIGH is the entire
            // generator-produced domain here, so anything else is not generator
            // output and must stay hashed.
            var unrelated = ForwardPrologue(
                "            #pragma skip_variants AMUSE_UNRELATED_KEYWORD\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(unrelated), Is.Not.EqualTo(Canon(absent)));
            Assert.That(Canon(unrelated), Does.Contain("AMUSE_UNRELATED_KEYWORD"));
        }

        [Test]
        public void Canonicalize_MultiKeywordPragmaAtSlot_IsRemoved()
        {
            // Dedup can leave any subset of a literal's tokens on a slot
            // line, so a multi-keyword vocabulary line is still generator
            // output. The unknown-keyword falsifier above keeps the hostile
            // case hashed.
            var multi = ForwardPrologue(
                "            #pragma skip_variants SHADOW_HIGH SHADOW_VERY_HIGH\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(multi), Is.EqualTo(Canon(absent)));
        }

        [Test]
        public void Canonicalize_ShadowPragmaAwayFromSlot_IsRemoved()
        {
            // G1: a vocabulary line is decided by content wherever it sits.
            var offSlot =
                "    HLSLPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n";
            var without =
                "    HLSLPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(offSlot), Is.EqualTo(Canon(without)));
        }

        [Test]
        public void Canonicalize_ShadowPragmaAfterDifferentDefine_IsRemoved()
        {
            var afterOtherDefine =
                "            #define LIL_PASS_FORWARDADD\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n";

            Assert.That(Canon(afterOtherDefine), Does.Not.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_ShadowPragmaInPassBody_IsRemoved()
        {
            var injected =
                "    HLSLPROGRAM\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(injected), Does.Not.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_RetainsInjectedPassBody()
        {
            const string clean = "Pass\n{\n    HLSLPROGRAM\n    ENDHLSL\n}\n";
            const string injected =
                "Pass\n{\n    HLSLPROGRAM\n    fd.col.rgb = 0;\n    ENDHLSL\n}\n";

            Assert.That(Canon(injected), Is.Not.EqualTo(Canon(clean)));
        }

        // --- R3: include path identity ---

        [Test]
        public void Canonicalize_NormalizesAttestedIncludeRegardlessOfPrefix()
        {
            const string relative = "#include \"Includes/lil_common.hlsl\"\n";
            const string packaged =
                "#include \"Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common.hlsl\"\n";

            Assert.That(Canon(packaged), Is.EqualTo(Canon(relative)));
            Assert.That(Canon(packaged), Does.Contain("Includes/lil_common.hlsl"));
        }

        [Test]
        public void Canonicalize_PreservesSubdirectoryWithinAttestedTree()
        {
            var canon = Canon(
                "#include \"Includes/VRC Light Volumes/LightVolumes.cginc\"\n");

            Assert.That(
                canon, Does.Contain("Includes/VRC Light Volumes/LightVolumes.cginc"));
        }

        [Test]
        public void Canonicalize_RedirectedIncludeWithSameBasename_IsNotNormalized()
        {
            const string trusted = "#include \"Includes/lil_common.hlsl\"\n";
            const string redirected = "#include \"Evil/lil_common.hlsl\"\n";

            Assert.That(Canon(redirected), Is.Not.EqualTo(Canon(trusted)));
            Assert.That(Canon(redirected), Does.Contain("Evil/lil_common.hlsl"));
        }

        [Test]
        public void Canonicalize_IncludeEscapingTreeByTraversal_IsNotNormalized()
        {
            const string trusted = "#include \"Includes/lil_common.hlsl\"\n";
            const string escaped =
                "#include \"Includes/../../Evil/Includes/lil_common.hlsl\"\n";

            Assert.That(Canon(escaped), Is.Not.EqualTo(Canon(trusted)));
        }

        [Test]
        public void Canonicalize_DifferentlyCasedIncludePath_IsNotNormalized()
        {
            // Exact ordinal identity: a casing difference cannot silently assume
            // the identity of an attested path, even where the filesystem would
            // resolve both to one file.
            const string trusted = "#include \"Includes/lil_common.hlsl\"\n";
            const string cased = "#include \"Includes/LIL_COMMON.HLSL\"\n";

            Assert.That(Canon(cased), Is.Not.EqualTo(Canon(trusted)));
            Assert.That(Canon(cased), Does.Contain("Includes/LIL_COMMON.HLSL"));
        }

        [Test]
        public void Canonicalize_CommentedIncludeIsNormalizedToo()
        {
            const string relative = "//#include \"Includes/lil_common.hlsl\"\n";
            const string packaged =
                "//#include \"Packages/jp.lilxyzw.liltoon/Shader/Includes/lil_common.hlsl\"\n";

            Assert.That(Canon(packaged), Is.EqualTo(Canon(relative)));
        }

        [Test]
        public void Canonicalize_NonIncludeQuotedStrings_AreUntouched()
        {
            const string source =
                "Fallback \"Includes/lil_common.hlsl\"\n" +
                "CustomEditor \"lilToon.lilToonInspector\"\n";

            Assert.That(
                Canon(source), Does.Contain("Fallback \"Includes/lil_common.hlsl\""));
        }

        // --- include tree digest ---

        [Test]
        public void ComputeIncludeTreeDigest_IsOrderIndependent()
        {
            var a = new List<(string, string)> { ("b.hlsl", "22"), ("a.hlsl", "11") };
            var b = new List<(string, string)> { ("a.hlsl", "11"), ("b.hlsl", "22") };

            Assert.That(
                LilToonSourceAttestation.ComputeIncludeTreeDigest(a),
                Is.EqualTo(LilToonSourceAttestation.ComputeIncludeTreeDigest(b)));
        }

        [Test]
        public void ComputeIncludeTreeDigest_DetectsAddedFile()
        {
            var baseline = new List<(string, string)> { ("a.hlsl", "11") };
            var extra = new List<(string, string)>
            {
                ("a.hlsl", "11"), ("z.hlsl", "99"),
            };

            Assert.That(
                LilToonSourceAttestation.ComputeIncludeTreeDigest(extra),
                Is.Not.EqualTo(
                    LilToonSourceAttestation.ComputeIncludeTreeDigest(baseline)));
        }

        // --- define and render-mode scans ---

        [Test]
        public void ScanCompiledFeatures_ReadsValuelessFeatureSymbolsOnly()
        {
            const string source =
                "        #define LIL_RENDER 0\n" +
                "        #define LIL_FEATURE_NORMAL_1ST\n" +
                "        #define LIL_FEATURE_BumpMap\n" +
                "        //#define LIL_FEATURE_EMISSION_1ST\n" +
                "        #define LIL_PASS_FORWARD\n";

            var features = LilToonSourceAttestation.ScanCompiledFeatures(source);

            Assert.That(features, Contains.Item("LIL_FEATURE_NORMAL_1ST"));
            Assert.That(features, Contains.Item("LIL_FEATURE_BumpMap"));
            Assert.That(features, Does.Not.Contain("LIL_FEATURE_EMISSION_1ST"));
            Assert.That(features, Does.Not.Contain("LIL_RENDER"));
            Assert.That(features, Does.Not.Contain("LIL_PASS_FORWARD"));
        }

        [Test]
        public void ScanCompiledFeatures_NullSource_IsEmpty()
        {
            Assert.That(
                LilToonSourceAttestation.ScanCompiledFeatures(null), Is.Empty);
        }

        [Test]
        public void TryScanRenderMode_SingleDefine_ReadsValue()
        {
            Assert.That(
                LilToonSourceAttestation.TryScanRenderMode(
                    "        #define LIL_RENDER 0\n", out var mode),
                Is.True);
            Assert.That(mode, Is.EqualTo(0));
        }

        [Test]
        public void TryScanRenderMode_TransparentPass_ReadsTwo()
        {
            Assert.That(
                LilToonSourceAttestation.TryScanRenderMode(
                    "#define LIL_RENDER 2\n", out var mode),
                Is.True);
            Assert.That(mode, Is.EqualTo(2));
        }

        [TestCase("")]
        [TestCase("#define LIL_RENDER\n")]
        [TestCase("#define LIL_RENDER x\n")]
        [TestCase("#define LIL_RENDER 0\n#define LIL_RENDER 1\n")]
        public void TryScanRenderMode_AmbiguousOrMissing_IsRefused(string source)
        {
            Assert.That(
                LilToonSourceAttestation.TryScanRenderMode(source, out _), Is.False);
        }

        // --- verification conjunction ---

        [Test]
        public void OfficialSettingIdentifierDomain_Has109UniqueEntries()
        {
            var field = typeof(LilToonSourceAttestation).GetField(
                "OfficialSettingIdentifiers",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null);
            var identifiers = (string[])field.GetValue(null);

            Assert.That(identifiers, Has.Length.EqualTo(109));
            Assert.That(
                identifiers.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(109));
        }

        [Test]
        public void Verify_DefaultStandaloneGeneratorRecord_Succeeds()
        {
            var analysis = PassAnalysis(DefaultStandaloneRecords());
            Assert.That(analysis.RemovedRegions[1].Records, Has.Count.EqualTo(103));

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: analysis), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Verify_StrippedStandaloneGeneratorRecord_Succeeds()
        {
            var analysis = PassAnalysis(StrippedStandaloneRecords());
            Assert.That(analysis.RemovedRegions[1].Records, Has.Count.EqualTo(91));

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: analysis), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [TestCase("pom")]
        [TestCase("clipping")]
        [TestCase("forwardadd-shadow")]
        [TestCase("bundled-light-volumes")]
        [TestCase("input-optimized")]
        public void Verify_StandaloneGrammarWitness_Succeeds(string witness)
        {
            var records = DefaultStandaloneRecords();
            switch (witness)
            {
                case "pom":
                    records.Remove("#define LIL_FEATURE_POM");
                    InsertAfter(
                        records,
                        "#define LIL_FEATURE_PARALLAX",
                        "#define LIL_FEATURE_POM");
                    break;
                case "clipping":
                    InsertAfter(
                        records,
                        "#define LIL_FEATURE_POM",
                        "#define LIL_FEATURE_CLIPPING_CANCELLER");
                    break;
                case "forwardadd-shadow":
                    InsertAfter(
                        records,
                        "#define LIL_OPTIMIZE_USE_FORWARDADD",
                        "#define LIL_OPTIMIZE_USE_FORWARDADD_SHADOW");
                    break;
                case "bundled-light-volumes":
                    InsertAfter(
                        records,
                        "#define LIL_OPTIMIZE_USE_VERTEXLIGHT",
                        "#define LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE");
                    break;
                case "input-optimized":
                    records.Add("#define LIL_INPUT_OPTIMIZED");
                    break;
                default:
                    Assert.Fail("unknown witness " + witness);
                    break;
            }

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.True);
        }

        [Test]
        public void Verify_LightmapOptimizationAndInverseSkipStates_Succeed()
        {
            var lightmap = DefaultStandaloneRecords();
            lightmap.Remove("#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE");
            InsertAfter(
                lightmap,
                "#define LIL_OPTIMIZE_USE_VERTEXLIGHT",
                "#define LIL_OPTIMIZE_USE_LIGHTMAP");

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(lightmap)), out _),
                Is.True);
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(
                        DefaultStandaloneRecords())), out _),
                Is.True,
                "without LIL_OPTIMIZE_USE_LIGHTMAP the inverse skip is required");
        }

        [TestCase("LIL_FEATURE_VRCLIGHTVOLUMES")]
        [TestCase("LIL_FEATURE_AUDIOLINK_PACKAGE")]
        [TestCase("LIL_FEATURE_LTCGI")]
        public void Verify_MisplacedExternalActivator_IsRefusedAsModifiedSource(
            string identifier)
        {
            // The generator emits these defines only at proven slots. A
            // define injected at the head of the setting run breaks the
            // generator ordering of the official record, so the record
            // check refuses it as modified source. LTCGI is not a setting
            // identifier at all and refuses for that reason.
            var clean = PassAnalysis(DefaultStandaloneRecords());
            var records = DefaultStandaloneRecords();
            records.Insert(1, "#define " + identifier);
            var mutated = PassAnalysis(records);

            Assert.That(mutated.CanonicalSource, Is.EqualTo(clean.CanonicalSource));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: mutated), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void Verify_GeneratorSlotActivator_Attests()
        {
            // The Census Lab evidence: the generator appends the AudioLink
            // define at the tail of the setting run, in generator order,
            // when AudioLink is installed. The occurrence is proven and
            // attests.
            var records = DefaultStandaloneRecords();
            records.Insert(
                records.Count - 1, "#define LIL_FEATURE_AUDIOLINK_PACKAGE");
            var mutated = PassAnalysis(records);

            Assert.That(mutated.Activators, Has.Count.EqualTo(1));
            Assert.That(mutated.Activators[0].ProvenSlot, Is.True);
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: mutated), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Verify_ActivatorOutsideGeneratorSlots_IsRefused()
        {
            // Falsifier: the same define in a pass body, away from every
            // proven slot, stays a tamper signal.
            var records = DefaultStandaloneRecords();
            var mutated = PassAnalysis(
                records,
                "    #define LIL_FEATURE_AUDIOLINK_PACKAGE\n");

            Assert.That(mutated.Activators, Has.Count.EqualTo(1));
            Assert.That(mutated.Activators[0].ProvenSlot, Is.False);
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: mutated), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
            Assert.That(
                diagnostic.Detail, Does.Contain("LIL_FEATURE_AUDIOLINK_PACKAGE"));
        }

        [Test]
        [TestCase("#define LIL_FEATURE_AMUSE_UNKNOWN")]
        [TestCase("#define LIL_OPTIMIZE_AMUSE_UNKNOWN")]
        [TestCase("#pragma skip_variants AMUSE_UNKNOWN")]
        [TestCase("#pragma skip_variants LIGHTPROBE_SH AMUSE_UNKNOWN")]
        public void Verify_HiddenUnknownRecordWithOldCanonicalOutput_IsRefused(
            string record)
        {
            var clean = PassAnalysis(DefaultStandaloneRecords());
            var records = DefaultStandaloneRecords();
            records.Insert(1, record);
            var mutated = PassAnalysis(records);

            Assert.That(mutated.CanonicalSource, Is.EqualTo(clean.CanonicalSource));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: mutated), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void Verify_DuplicateOrReorderedKnownRecord_IsRefused()
        {
            var duplicate = DefaultStandaloneRecords();
            duplicate.Insert(4, "#define LIL_FEATURE_MAIN2ND");
            var reordered = DefaultStandaloneRecords();
            var swap = reordered[4];
            reordered[4] = reordered[5];
            reordered[5] = swap;

            foreach (var records in new[] { duplicate, reordered })
            {
                Assert.That(
                    LilToonSourceAttestation.TryVerifyLilToonIdentity(
                        Evidence(passCanonicalization: PassAnalysis(records)), out _),
                    Is.False);
            }
        }

        [TestCase("LIL_FEATURE_Main2ndDissolveNoiseMask")]
        [TestCase("LIL_FEATURE_Main3rdDissolveNoiseMask")]
        [TestCase("LIL_FEATURE_DissolveNoiseMask")]
        public void Verify_MissingMandatoryNoiseRecord_IsRefused(string identifier)
        {
            var records = DefaultStandaloneRecords();
            records.Remove("#define " + identifier);

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.False);
        }

        [TestCaseSource(nameof(GeneratorDependencies))]
        public void Verify_DependencyWithoutParent_IsRefused(
            string child,
            string[] parents)
        {
            var records = DefaultStandaloneRecords();
            if (child == "LIL_FEATURE_POM")
            {
                InsertAfter(
                    records,
                    "#define LIL_FEATURE_PARALLAX",
                    "#define LIL_FEATURE_POM");
            }
            foreach (var parent in parents)
            {
                records.Remove("#define " + parent);
            }

            Assert.That(records, Does.Contain("#define " + child));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.False);
        }

        [TestCase("reflection")]
        [TestCase("vertex-light")]
        [TestCase("lightmap")]
        public void Verify_InverseSkipMismatch_IsRefused(string relationship)
        {
            var records = DefaultStandaloneRecords();
            switch (relationship)
            {
                case "reflection":
                    records.Insert(
                        records.Count - 1,
                        "#pragma skip_variants _REFLECTION_PROBE_BOX_PROJECTION");
                    break;
                case "vertex-light":
                    records.Insert(
                        records.Count - 1,
                        "#pragma skip_variants LIGHTPROBE_SH");
                    break;
                case "lightmap":
                    records.Remove(
                        "#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE");
                    break;
            }

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.False);
        }

        [Test]
        public void Verify_InputOptimizedBeforePragma_IsRefused()
        {
            var records = DefaultStandaloneRecords();
            records.Insert(records.Count - 1, "#define LIL_INPUT_OPTIMIZED");

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.False);
        }

        [Test]
        public void Verify_BothLightVolumesForms_Attest()
        {
            // The activator gate no longer refuses generator-slot defines.
            // Both LightVolumes forms in generator order sit inside the
            // record grammar's official domain: the record check admits
            // them, and the defines gate lighting variants, not alpha
            // semantics. The vendor emits one form per SDK state; a hand
            // edit flipping the form is alpha-irrelevant and attests like
            // any official-identifier state.
            var records = DefaultStandaloneRecords();
            InsertAfter(
                records,
                "#define LIL_OPTIMIZE_USE_VERTEXLIGHT",
                "#define LIL_FEATURE_VRCLIGHTVOLUMES");
            InsertAfter(
                records,
                "#define LIL_FEATURE_VRCLIGHTVOLUMES",
                "#define LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE");
            var mutated = PassAnalysis(records);

            Assert.That(
                mutated.Activators.Select(value => value.ProvenSlot),
                Has.All.True);
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: mutated), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Verify_RecordInShaderScopeRegion_IsRefused()
        {
            var pass = Analyze(
                "HLSLINCLUDE\n" +
                "    #define LIL_FEATURE_MAIN2ND\n" +
                "    #define LIL_RENDER 0\n" +
                "ENDHLSL\n" +
                "HLSLINCLUDE\n" +
                string.Join("\n", DefaultStandaloneRecords()) +
                "\n#pragma target 3.5\nENDHLSL\n");

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: pass), out _),
                Is.False);
        }

        [Test]
        public void Verify_NonGeneratorWhitespace_IsRefused()
        {
            var records = DefaultStandaloneRecords();
            records[3] = "#define  LIL_FEATURE_MAIN2ND";

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Verify_MissingCanonicalizationAnalysis_IsRefused(bool shader)
        {
            var evidence = shader
                ? Evidence(hasShaderCanonicalization: false)
                : Evidence(hasPassCanonicalization: false);

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    evidence, out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        [TestCase("LIL_FEATURE_VRCLIGHTVOLUMES", true)]
        [TestCase("LIL_FEATURE_VRCLIGHTVOLUMES", false)]
        [TestCase("LIL_FEATURE_AUDIOLINK_PACKAGE", true)]
        [TestCase("LIL_FEATURE_AUDIOLINK_PACKAGE", false)]
        [TestCase("LIL_FEATURE_LTCGI", true)]
        [TestCase("LIL_FEATURE_LTCGI", false)]
        public void Verify_ExternalActivatorOutsideR1_IsRefused(
            string identifier,
            bool shader)
        {
            var evidence = shader
                ? Evidence(shaderCanonicalization: Analyze(
                    "#define " + identifier + " 1\n"))
                : Evidence(passCanonicalization: PassAnalysis(
                    DefaultStandaloneRecords(),
                    "#define " + identifier + " 1\n"));

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    evidence, out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
        }

        [Test]
        public void Verify_DuplicatedRelocatedActivatorKeepsOldCanonicalOutputAndRefuses()
        {
            var clean = PassAnalysis(DefaultStandaloneRecords());
            var records = DefaultStandaloneRecords();
            records.Insert(1, "#define LIL_FEATURE_AUDIOLINK_PACKAGE");
            records.Insert(20, "#define LIL_FEATURE_AUDIOLINK_PACKAGE");
            var mutated = PassAnalysis(records);

            Assert.That(mutated.CanonicalSource, Is.EqualTo(clean.CanonicalSource));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: mutated), out _),
                Is.False);
        }

        [Test]
        public void Verify_CanonicalEvidence_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [TestCase("Standard")]
        [TestCase("Hidden/lilToonCutout")]
        [TestCase("Hidden/lilToonTransparent")]
        [TestCase("_lil/lilToonMulti")]
        [TestCase("lilToon ")]
        public void Verify_UnsupportedShaderName_IsRefused(string name)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(shaderName: name), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(
                diagnostic.Output, Is.EqualTo(LilToonSemanticOutput.Material));
        }

        // --- _lilToonVersion exactness ---

        [Test]
        public void Verify_ExactVersion_IsAccepted()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(version: 45f), out _),
                Is.True);
        }

        [Test]
        public void Verify_NearbyVersionValues_AreRefused()
        {
            var nextAbove = FloatUlp.Ulp(45f, 1);
            var nextBelow = FloatUlp.Ulp(45f, -1);

            Assert.That(nextAbove, Is.Not.EqualTo(45f), "ULP step must differ");
            Assert.That(nextBelow, Is.Not.EqualTo(45f), "ULP step must differ");

            foreach (var value in new[]
                     {
                         44f, 46f, 44.999f, 45.001f, nextAbove, nextBelow,
                         float.NaN, float.PositiveInfinity, float.NegativeInfinity,
                     })
            {
                Assert.That(
                    LilToonSourceAttestation.TryVerifyLilToonIdentity(
                        Evidence(version: value), out var diagnostic),
                    Is.False,
                    "version " + value.ToString("R"));
                Assert.That(
                    diagnostic.Code,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
            }
        }

        [Test]
        public void Verify_MissingVersionProperty_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(hasVersion: false, version: 45f), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        // --- remaining conjuncts ---

        [Test]
        public void Verify_GuidMismatch_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(assetGuid: new string('0', 32)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        // --- admitted set (T1) ---

        [Test]
        public void AdmittedVersionRows_AreTheMeasuredSet()
        {
            var rows = LilToonSourceAttestation.AdmittedRows();

            Assert.That(rows.Select(row => row.Version).ToList(), Is.Unique);
            Assert.That(rows.Select(row => row.Version).ToList(), Is.EqualTo(
                new[] { "2.3.0", "2.3.1", "2.3.2", "2.3.3", "2.3.4", "2.4.0",
                    "2.4.1" }));
            Assert.That(rows[1].IncludeDigest, Is.EqualTo(rows[2].IncludeDigest),
                "2.3.1 through 2.3.3 ship one shared tree");
            Assert.That(rows[3].IncludeDigest, Is.EqualTo(rows[2].IncludeDigest),
                "2.3.1 through 2.3.3 ship one shared tree");
            Assert.That(rows[4].IncludeDigest,
                Is.Not.EqualTo(rows[3].IncludeDigest),
                "2.3.4 anchors its own tree");
            Assert.That(
                rows.Take(5).Select(row => row.IncludeDigest),
                Has.None.EqualTo(rows[5].IncludeDigest),
                "the 2.4.0 tree matches no earlier row");
            Assert.That(
                rows.Take(6).Select(row => row.IncludeDigest),
                Has.None.EqualTo(rows[6].IncludeDigest),
                "the 2.4.1 tree matches no earlier row");

            var multiRows = LilToonSourceAttestation.AdmittedMultiRows();
            Assert.That(
                multiRows.Select(row => row.Version).ToList(), Is.Unique);
            Assert.That(
                multiRows.ToArray(),
                Is.EqualTo(new[]
                {
                    ("2.3.4",
                        "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                        "225af2c6eba7a017593ca8a0f38114a98580ed67844a4d8465fed2c96f7cb75f"),
                    ("2.4.0",
                        "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                        "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85"),
                    ("2.4.1",
                        "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                        "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85"),
                }),
                "Multi admission is exactly the three version rows with " +
                "their per-version digests");
        }

        /// <summary>
        /// The wrong-package-version guard must name a version genuinely
        /// outside the approved range: below the floor and at the bound.
        /// The approved range is [2.3.0, 2.5.0), so 2.2.0 and 2.5.0 stay
        /// unsupported versions even after the range goes live.
        /// </summary>
        [TestCase("2.2.0")]
        [TestCase("2.5.0")]
        public void Verify_WrongPackageVersion_IsRefused(string version)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: version), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        [TestCase("2.5.0")]
        [TestCase("2.4")]
        [TestCase("02.4.0")]
        public void Verify_NonAdmittedVersionForms_Refuse(string version)
        {
            // Evidence exactly as Verify_WrongPackageVersion_IsRefused
            // builds it, with only the case version substituted.
            // Non-canonical and out-of-range forms refuse as an
            // unsupported version and never normalize into an admitted
            // row or into the approved range.
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: version), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        [Test]
        public void ApprovedRangePatchWithTheNearestRowTreeVerifies()
        {
            // One patch and many patches past the newest admitted row both
            // pair that row, so the evidence presents that row's tree and
            // the whole conjunction verifies honestly. The witness derives
            // from the row list, so a new admitted row never edits this
            // test.
            foreach (var patchesPast in new[] { 1, 9 })
            {
                var version = VerifiedLilToonTestSeams.ApprovedWitnessVersion(
                    patchesPast);
                Assert.That(
                    LilToonSourceAttestation.TryVerifyLilToonIdentity(
                        Evidence(packageVersion: version), out var diagnostic),
                    Is.True,
                    version);
                Assert.That(diagnostic, Is.Null, version);
            }
        }

        [Test]
        public void ApprovedRangeVersionWithAForeignTreeReportsModifiedSource()
        {
            // The pairing still binds version to tree. The witness pairs
            // the newest admitted row, so the oldest row's tree is a
            // foreign tree, and the answer is modified source, never
            // UnsupportedVersion.
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        packageVersion:
                            VerifiedLilToonTestSeams.ApprovedWitnessVersion(),
                        includeDigest:
                            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46"),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic?.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void AdmittedPatchVersionWithThePriorTreeReportsModifiedSource()
        {
            // The 2.4.1 row binds version to its own tree. A 2.4.1 label
            // over the 2.4.0 tree is a foreign tree, and the answer is
            // modified source, never UnsupportedVersion (spec B5).
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        packageVersion: "2.4.1",
                        includeDigest:
                            "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670"),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void TwoPointThreeTenPairsTheTwoPointThreeFourRow()
        {
            // The numeric-comparison pin. 2.3.10 pairs the 2.3.4 row,
            // because the scan compares numeric triples. An ordinal scan
            // would reach the 2.3.1 shared tree instead. With that
            // shared tree the version refuses as modified source.
            const string tree234 =
                "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46";
            const string sharedTree231 =
                "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416";

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: "2.3.10", includeDigest: tree234),
                    out _),
                Is.True,
                "2.3.10 pairs the 2.3.4 row's tree");

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        packageVersion: "2.3.10",
                        includeDigest: sharedTree231),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic?.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource),
                "the 2.3.1 shared tree is a foreign tree for 2.3.10");
        }

        [TestCase("2.10.0")]
        [TestCase("2.3.100")]
        [TestCase("2.4.4294967300")]
        public void VersionsTheRangeCannotContainStayUnapproved(string version)
        {
            // 2.10.0 is numerically at or past the bound. 2.3.100 carries
            // a component no real release line of this vendor could
            // ship, so it stays outside the range the measured pins
            // could cover. 2.4.4294967300 carries a run too long for the
            // strict parse: a naive int parse would overflow the patch
            // back into the range. All three stay unapproved and refuse
            // as unsupported versions.
            Assert.That(
                LilToonSourceAttestation.IsApprovedPackageVersion(version),
                Is.False,
                version);
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: version), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion),
                version);
        }

        [TestCase("2.4.0.1")]
        [TestCase("02.4.0")]
        [TestCase(" 2.4.0 ")]
        [TestCase("2.4.0-preview")]
        public void NonCanonicalFormsStayUnapproved(string version)
        {
            // Four components, a leading zero, whitespace, and a
            // prerelease suffix are all non-canonical. The predicate
            // answers false and the identity refuses. Nothing
            // normalizes.
            Assert.That(
                LilToonSourceAttestation.IsApprovedPackageVersion(version),
                Is.False,
                version);
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: version), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion),
                version);
        }

        [Test]
        public void ApprovalGrammarMatchesTheHostGrammarOnSharedCanonicalForms()
        {
            // Both parsers are private (the host one to the Build layer),
            // so the drive runs by reflection, the house pattern for
            // private members. Parse success must agree on every shared
            // canonical form, so the range edges cannot drift from the
            // host grammar. The attestation parser is stricter by design
            // on non-canonical forms. The range tests above pin those.
            var host = typeof(HostLifecycleCapability).GetMethod(
                "TryParsePackageVersion",
                BindingFlags.NonPublic | BindingFlags.Static);
            var attestation = typeof(LilToonSourceAttestation).GetMethod(
                "TryParseCanonicalVersion",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(host, Is.Not.Null, "the host parser exists");
            Assert.That(
                attestation, Is.Not.Null, "the attestation parser exists");

            foreach (var form in new[]
            {
                "1.2.3", "2.3.0", "2.3.4", "2.3.10", "2.4.0", "2.4.1",
                "2.4.9", "2.4.99", "2.5.0", "2.10.0", "2.3.100",
                "10.20.30",
            })
            {
                var hostArgs = new object[] { form, null };
                var attestationArgs = new object[] { form, null };
                Assert.That(
                    (bool)host.Invoke(null, hostArgs),
                    Is.EqualTo((bool)attestation.Invoke(null, attestationArgs)),
                    form);
            }
        }

        [Test]
        public void Verify_LegacyAssetsInstall_SkipsPackageCheck()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        hasPackage: false, packageName: null, packageVersion: null),
                    out _),
                Is.True);
        }

        [Test]
        public void Verify_EditedIncludeTree_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(includeDigest: new string('0', 64)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        // --- real gathers over a stand-in tree on disk (G2). The mismatch
        // tests above feed synthetic evidence; these drive the production
        // gather path and pin the diagnostic code it reports. ---

        /// <summary>
        /// Builds a stand-in include tree on disk and runs the production
        /// gather over it: <see cref="LilToonIncludeTree.Enumerate"/> with the
        /// production hash, then
        /// <see cref="LilToonSourceAttestation.ComputeIncludeTreeDigest"/> —
        /// exactly the two steps the gather front half runs over a live
        /// project's include folder. The admitted row digests are SHA-256
        /// over the official tag artifacts, whose bytes no test can
        /// reconstruct, so the stand-in tree stands in for any tree that is
        /// not the claimed version's — the drift case itself. The scratch
        /// folder is the test's own and goes away on return.
        /// </summary>
        private static string GatheredStandInIncludeDigest()
        {
            var includeFolder = Path.Combine(
                Path.GetTempPath(), "AmuseTests_StandInIncludes");
            if (Directory.Exists(includeFolder))
            {
                Directory.Delete(includeFolder, recursive: true);
            }

            Directory.CreateDirectory(Path.Combine(includeFolder, "Sub"));
            File.WriteAllText(
                Path.Combine(includeFolder, "lil_common.hlsl"),
                "// stand-in\nfloat lil StandInCommon();\n");
            File.WriteAllText(
                Path.Combine(includeFolder, "Sub", "lil_extra.hlsl"),
                "// stand-in extra\nvoid lil StandInExtra();\n");

            var tree = LilToonIncludeTree.Enumerate(
                includeFolder,
                fullPath => File.Exists(fullPath)
                    ? File.ReadAllText(fullPath)
                    : null,
                LilToonSourceAttestation.ComputeNormalizedSourceHash);
            var digest =
                LilToonSourceAttestation.ComputeIncludeTreeDigest(tree.Files);

            Directory.Delete(includeFolder, recursive: true);
            return digest;
        }

        [Test]
        public void GatheredTreeMatchingAnOlderRowWhileVersionIsNewer_IsModifiedSource()
        {
            var gathered = GatheredStandInIncludeDigest();

            // The stand-in tree reproduces no admitted row's bytes, so it
            // plays an older row's tree under an admitted version's label.
            Assert.That(
                LilToonSourceAttestation.AdmittedRows()
                    .Any(row => row.IncludeDigest == gathered),
                Is.False,
                "the stand-in gather must not reproduce an admitted tree");

            // The claimed version is admitted on purpose: the version gate
            // must pass and the include gate must be the one that fires.
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: "2.3.4", includeDigest: gathered),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource),
                "an admitted version over a foreign tree is modified " +
                "source, never UnsupportedVersion");
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        [Test]
        public void MutatedProfileDigestInput_OpaqueFamily_ReportsModifiedSource()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(includeDigest: GatheredStandInIncludeDigest()),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        [Test]
        public void MutatedProfileDigestInput_CutoutFamily_ReportsModifiedSource()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(includeDigest: GatheredStandInIncludeDigest()),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        [Test]
        public void MutatedProfileDigestInput_TransparentFamily_ReportsModifiedSource()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonTransparentIdentity(
                    TransparentEvidence(
                        includeDigest: GatheredStandInIncludeDigest()),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        [Test]
        public void Verify_EditedPassAsset_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passDigest: new string('0', 64)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void Verify_EditedMaterialShaderAsset_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(shaderDigest: new string('0', 64)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void Verify_MissingDigestEvidence_IsRefused()
        {
            // The sentinel default means this genuinely passes null, rather than
            // being coalesced back to the pin.
            var evidence = Evidence(includeDigest: null);
            Assert.That(evidence.IncludeTreeDigest, Is.Null);

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    evidence, out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        [Test]
        public void Verify_MissingPassDigestEvidence_IsRefused()
        {
            var evidence = Evidence(passDigest: null);
            Assert.That(evidence.PassCanonicalDigest, Is.Null);

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    evidence, out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        [Test]
        public void Verify_WrongPassShaderGuid_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passGuid: new string('0', 32)), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        // --- live LIL_RENDER ---

        [TestCase(1)]
        [TestCase(2)]
        public void Verify_NonOpaqueRenderMode_IsRefused(int mode)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(renderMode: mode), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
            Assert.That(diagnostic.Detail, Does.Contain("LIL_RENDER"));
        }

        [Test]
        public void Verify_UnreadableRenderMode_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(hasRenderMode: false, renderMode: 0),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
        }

        // --- pins are the Task 0 measurements; the pass digests were
        // --- re-measured on 2026-09-07 under the R4/R5/R6 canonicalization

        [Test]
        public void PinnedDigests_AreTheTask0Measurements()
        {
            Assert.That(
                LilToonSourceAttestation.ShaderCanonicalDigest,
                Is.EqualTo(
                    "5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704"));
            Assert.That(
                LilToonSourceAttestation.PassCanonicalDigest,
                Is.EqualTo(
                    "aee1ea0c1fd0ae26f561fbade4c23309bb62ed8aff0c9221d1cba7c74a31d9d1"));
            // The flip moved the include-tree pin to the newest admitted
            // tree (2.4.1). The digest was computed from the official 2.4.1
            // tag tree on 2026-10-20 by the production recipe, licensed by
            // reproducing the 2.4.0 pin from the 2.4.0 tag tree (spec B1).
            // The 2.4.0 anchor lives in the AdmittedPackageVersions row
            // literal and in the S11 tests. The installed 2.4.1 shape
            // reproduced the same digest by the same recipe on 2026-10-20.
            Assert.That(
                LilToonSourceAttestation.IncludeTreeDigest,
                Is.EqualTo(
                    "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625"));

            // All three admitted Multi version rows, verbatim: a
            // transcription error in any Multi row fails this audit
            // independently of the rows themselves. The 2.4.1 row is the
            // 2026-10-20 installed-shape measurement.
            var multiRows = LilToonSourceAttestation.AdmittedMultiRows();
            Assert.That(multiRows.Count, Is.EqualTo(3));
            Assert.That(multiRows[0], Is.EqualTo(
                ("2.3.4",
                    "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                    "225af2c6eba7a017593ca8a0f38114a98580ed67844a4d8465fed2c96f7cb75f")));
            Assert.That(multiRows[1], Is.EqualTo(
                ("2.4.0",
                    "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                    "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85")));
            Assert.That(multiRows[2], Is.EqualTo(
                ("2.4.1",
                    "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                    "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85")));
        }

        // --- cutout profile: Task 1 Step 2 RED tests ---

        /// <summary>
        /// Mirrors <see cref="Evidence"/> for the pinned cutout identity: the
        /// same <see cref="UsePin"/> sentinel mechanics, the same shared
        /// package/format/include-tree pins, and the cutout name, GUIDs,
        /// render mode, and canonical digests by default.
        /// </summary>
        private static LilToonSourceEvidence CutoutEvidence(
            string shaderName = "Hidden/lilToonCutout",
            string assetGuid = "85d6126cae43b6847aff4b13f4adb8ec",
            bool hasVersion = true,
            float version = 45f,
            bool hasPackage = true,
            string packageName = "jp.lilxyzw.liltoon",
            string packageVersion = LilToonSourceAttestation.PackageVersion,
            string passGuid = "ad219df2a46e841488aee6a013e84e36",
            string shaderDigest = UsePin,
            string passDigest = UsePin,
            string includeDigest = UsePin,
            bool hasRenderMode = true,
            int renderMode = 1,
            IReadOnlyCollection<string> features = null,
            bool hasShaderCanonicalization = true,
            LilToonCanonicalizationAnalysis shaderCanonicalization = null,
            bool hasPassCanonicalization = true,
            LilToonCanonicalizationAnalysis passCanonicalization = null)
        {
            return new LilToonSourceEvidence(
                shaderName,
                assetGuid,
                hasVersion,
                version,
                hasPackage,
                packageName,
                packageVersion,
                passGuid,
                ReferenceEquals(shaderDigest, UsePin)
                    ? LilToonSourceAttestation.CutoutShaderCanonicalDigest
                    : shaderDigest,
                ReferenceEquals(passDigest, UsePin)
                    ? LilToonSourceAttestation.CutoutPassCanonicalDigest
                    : passDigest,
                ReferenceEquals(includeDigest, UsePin)
                    ? LilToonSourceAttestation.IncludeTreeDigest
                    : includeDigest,
                hasRenderMode,
                renderMode,
                features ?? new string[0],
                hasShaderCanonicalization
                    ? shaderCanonicalization ?? EmptyShaderAnalysis()
                    : null,
                hasPassCanonicalization
                    ? passCanonicalization ?? PassAnalysis(DefaultStandaloneRecords())
                    : null);
        }

        [Test]
        public void VerifyCutout_CanonicalEvidence_Succeeds()
        {
            // The true case also pins the cutout provenance premise: the
            // unchanged canonicalization conjunction (two removed regions,
            // official setting record) accepts the cutout pass, exactly as
            // B2 §5 clause 1 attests.
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Verify_OpaqueVerifierStillRejectsCutoutIdentity()
        {
            // Profile-leakage guard: the opaque verifier must keep refusing
            // the cutout identity even after the cutout profile goes live.
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    CutoutEvidence(), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        // --- transparent profile ---

        /// <summary>
        /// Mirrors <see cref="CutoutEvidence"/> for the pinned transparent
        /// identity: the same <see cref="UsePin"/> sentinel mechanics, the
        /// same shared package/format/include-tree pins, and the transparent
        /// name, GUIDs, render mode, and canonical digests by default.
        /// </summary>
        private static LilToonSourceEvidence TransparentEvidence(
            string shaderName = "Hidden/lilToonTransparent",
            string assetGuid = "165365ab7100a044ca85fc8c33548a62",
            bool hasVersion = true,
            float version = 45f,
            bool hasPackage = true,
            string packageName = "jp.lilxyzw.liltoon",
            string packageVersion = LilToonSourceAttestation.PackageVersion,
            string passGuid = "2683fad669f20ec49b8e9656954a33a8",
            string shaderDigest = UsePin,
            string passDigest = UsePin,
            string includeDigest = UsePin,
            bool hasRenderMode = true,
            int renderMode = 2,
            IReadOnlyCollection<string> features = null,
            bool hasShaderCanonicalization = true,
            LilToonCanonicalizationAnalysis shaderCanonicalization = null,
            bool hasPassCanonicalization = true,
            LilToonCanonicalizationAnalysis passCanonicalization = null)
        {
            return new LilToonSourceEvidence(
                shaderName,
                assetGuid,
                hasVersion,
                version,
                hasPackage,
                packageName,
                packageVersion,
                passGuid,
                ReferenceEquals(shaderDigest, UsePin)
                    ? LilToonSourceAttestation
                        .TransparentShaderCanonicalDigest
                    : shaderDigest,
                ReferenceEquals(passDigest, UsePin)
                    ? LilToonSourceAttestation.TransparentPassCanonicalDigest
                    : passDigest,
                ReferenceEquals(includeDigest, UsePin)
                    ? LilToonSourceAttestation.IncludeTreeDigest
                    : includeDigest,
                hasRenderMode,
                renderMode,
                features ?? new string[0],
                hasShaderCanonicalization
                    ? shaderCanonicalization ?? EmptyShaderAnalysis()
                    : null,
                hasPassCanonicalization
                    ? passCanonicalization ?? PassAnalysis(DefaultStandaloneRecords())
                    : null);
        }

        [Test]
        public void VerifyTransparent_CanonicalEvidence_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void TransparentConstants_AreTheMeasuredPins()
        {
            Assert.That(
                LilToonSourceAttestation.TransparentShaderName,
                Is.EqualTo("Hidden/lilToonTransparent"));
            Assert.That(
                LilToonSourceAttestation.TransparentShaderGuid,
                Is.EqualTo("165365ab7100a044ca85fc8c33548a62"));
            Assert.That(
                LilToonSourceAttestation.TransparentPassShaderName,
                Is.EqualTo("Hidden/ltspass_transparent"));
            Assert.That(
                LilToonSourceAttestation.TransparentPassShaderGuid,
                Is.EqualTo("2683fad669f20ec49b8e9656954a33a8"));
            Assert.That(
                LilToonSourceAttestation.TransparentRenderMode, Is.EqualTo(2));
            Assert.That(
                LilToonSourceAttestation.TransparentShaderCanonicalDigest,
                Is.EqualTo(
                    "ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b4576240" +
                    "97f2372ba13"));
            Assert.That(
                LilToonSourceAttestation.TransparentPassCanonicalDigest,
                Is.EqualTo(
                    "b60c492d4fa407b3ae4158d22891159be5f4a73a1f7b6925b6ab335" +
                    "9cdac7762"));
        }

        [TestCase("Hidden/lilToonTransparen")]
        [TestCase("hidden/liltoontransparent")]
        [TestCase("Hidden/lilToonOnePassTransparentX")]
        [TestCase("Hidden/lilToonTwoPassTransparentX")]
        [TestCase("Hidden/lilToonOnePassTransparentOutlineX")]
        [TestCase("Hidden/lilToonTwoPassTransparentOutlineX")]
        public void VerifyTransparent_NearMissShaderName_Refuses(string name)
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(shaderName: name),
                        out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        // --- exactness falsifier -------------------------------------------

        [TestCase("Hidden/lilToonOnePassTransparentX")]
        [TestCase("Hidden/lilToonTwoPassTransparentX")]
        [TestCase("Hidden/lilToonOnePassTransparentOutlineX")]
        [TestCase("Hidden/lilToonTwoPassTransparentOutlineX")]
        [TestCase("Hidden/lilToonTransparentOutlineX")]
        public void NearMissTransparentName_IsNeverSelectedOrAdmitted(
            string shaderName)
        {
            // The near misses are one character off an admitted transparent
            // identity and declare the SAME pass asset
            // (Hidden/ltspass_transparent) and the same LIL_RENDER 2, queue
            // 2460 and RenderType as the supported family. Falsifies:
            // prefix/substring matching, Contains("Transparent"), grouping
            // by LIL_RENDER, by queue, or by pass-asset identity alone.
            Assert.That(
                shaderName,
                Is.Not.EqualTo(LilToonSourceAttestation.TransparentShaderName));
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(shaderName: shaderName), out _),
                Is.False);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(3)]
        public void VerifyTransparent_WrongRenderMode_Refuses(int mode)
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(renderMode: mode), out _),
                Is.False);
        }

        [Test]
        public void VerifyTransparent_WrongShaderDigest_Refuses()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(
                            shaderDigest: new string('0', 64)),
                        out _),
                Is.False);
        }

        [Test]
        public void VerifyTransparent_WrongPassDigest_Refuses()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(passDigest: new string('0', 64)),
                        out _),
                Is.False);
        }

        [Test]
        public void VerifyTransparent_WrongPassGuid_Refuses()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        TransparentEvidence(passGuid: new string('0', 32)),
                        out _),
                Is.False);
        }

        /// <summary>
        /// Profile-leakage guards in both directions. Falsifies a third
        /// profile that widened a shared conjunction instead of adding an
        /// exact identity.
        /// </summary>
        [Test]
        public void ExistingVerifiers_StillRejectTransparentIdentity()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    TransparentEvidence(), out var opaqueDiagnostic),
                Is.False);
            Assert.That(
                opaqueDiagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    TransparentEvidence(), out var cutoutDiagnostic),
                Is.False);
            Assert.That(
                cutoutDiagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        [Test]
        public void TransparentVerifier_RejectsCutoutAndOpaqueIdentities()
        {
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        CutoutEvidence(), out _),
                Is.False);
            Assert.That(
                LilToonSourceAttestation
                    .TryVerifyLilToonTransparentIdentity(
                        Evidence(), out _),
                Is.False);
        }

        [TestCase("Standard")]
        [TestCase("Hidden/lilToonTransparent")]
        [TestCase("lilToonCutout")]
        public void VerifyCutout_UnsupportedShaderName_IsRefused(string name)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(shaderName: name), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        [Test]
        public void VerifyCutout_GuidMismatch_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(assetGuid: new string('0', 32)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
        }

        [Test]
        public void VerifyCutout_WrongPassShaderGuid_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(passGuid: new string('0', 32)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        [TestCase(0)]
        [TestCase(2)]
        public void VerifyCutout_NonCutoutRenderMode_IsRefused(int mode)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(renderMode: mode), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
            Assert.That(diagnostic.Detail, Does.Contain("LIL_RENDER"));
        }

        [Test]
        public void VerifyCutout_UnreadableRenderMode_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(hasRenderMode: false, renderMode: 0),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
        }

        [Test]
        public void VerifyCutout_EditedMaterialShaderAsset_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(shaderDigest: new string('0', 64)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void VerifyCutout_EditedPassAsset_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(passDigest: new string('0', 64)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void VerifyCutout_EditedIncludeTree_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(includeDigest: new string('0', 64)),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
            Assert.That(diagnostic.Detail, Does.Contain("Includes"));
        }

        [Test]
        public void VerifyCutout_MissingDigestEvidence_IsRefused()
        {
            // The sentinel default means this genuinely passes null, rather
            // than being coalesced back to the pin.
            var evidence = CutoutEvidence(includeDigest: null);
            Assert.That(evidence.IncludeTreeDigest, Is.Null);

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    evidence, out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.MissingSourceEvidence));
        }

        [Test]
        public void VerifyCutout_FormatVersion44_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(version: 44f), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        /// <summary>
        /// The cutout identity shares the version gate, so the approved
        /// edges refuse here too: below the floor and at the bound stay
        /// unsupported versions.
        /// </summary>
        [TestCase("2.2.0")]
        [TestCase("2.5.0")]
        public void VerifyCutout_WrongPackageVersion_IsRefused(string version)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(packageVersion: version), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        // --- S11: admitted versions 2.3.0 through 2.4.1 --------------------

        /// <summary>
        /// Shader and pass bytes are byte-identical across 2.3.0-2.4.1 (the
        /// 2.4.0 digests reproduce the pinned constants on two installed
        /// 2.4.0 shapes, 2026-10-19), so each admitted version verifies
        /// against the shared digests with its own include-tree digest.
        /// Digests were computed by the production ComputeIncludeTreeDigest
        /// over each official tag's Shader/Includes tree (37 files each),
        /// anchored by reproducing the pinned 2.3.4 digest exactly; the
        /// 2.4.0 digest was measured over the installed tree (38 files) and
        /// reproduced on a second settings shape, and the 2.4.1 digest was
        /// computed from the official tag tree (38 files) under the anchor
        /// license on 2026-10-20.
        /// </summary>
        [TestCase("2.3.0",
            "bba3205a08b2bd56b4d2c69b8de3144377ec0b4ca4c44cc9d71b1341e62c94a0")]
        [TestCase("2.3.1",
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416")]
        [TestCase("2.3.2",
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416")]
        [TestCase("2.3.3",
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416")]
        [TestCase("2.3.4",
            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46")]
        [TestCase("2.4.0",
            "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670")]
        [TestCase("2.4.1",
            "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625")]
        public void Verify_AcceptsAdmittedPatchVersionWithItsOwnTree(
            string version,
            string includeDigest)
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: version, includeDigest: includeDigest),
                    out var diagnostic),
                Is.True,
                version);
            Assert.That(diagnostic, Is.Null, version);
        }

        /// <summary>
        /// The in-range non-row twin of the admitted-patch pin: 2.4.3 is
        /// no admitted row, so the pairing fallback binds it to the
        /// greatest row at or below it (2.4.1), and the evidence presents
        /// exactly that row's tree.
        /// </summary>
        [Test]
        public void Verify_AcceptsApprovedNonRowVersionWithTheNearestRowTree()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: "2.4.3"), out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        /// <summary>
        /// The in-range non-row twin of the cross-version pin: an
        /// approved version still pairs its own row, so a foreign row's
        /// tree fails closed as modified source.
        /// </summary>
        [Test]
        public void Verify_ApprovedNonRowVersionWithAnotherVersionsTree_IsModifiedSource()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        packageVersion: "2.4.3",
                        includeDigest:
                            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic?.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        /// <summary>
        /// The version and the include tree must pair: a 2.3.3 install
        /// carrying the 2.3.4 include tree fails closed as modified source,
        /// because the tree is the only per-version bytes the identity
        /// conjunction can check.
        /// </summary>
        [Test]
        public void Verify_VersionWithAnotherVersionsTree_IsModifiedSource()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        packageVersion: "2.3.3",
                        includeDigest:
                            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46"),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic?.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        /// <summary>
        /// A loose (non-package) install carries no package version, so its
        /// include tree alone identifies the shipped version: an admitted
        /// tree admits, an unknown tree fails closed.
        /// </summary>
        [Test]
        public void Verify_NoPackageInstallIsAdmittedByItsTree()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        hasPackage: false,
                        includeDigest:
                            "bba3205a08b2bd56b4d2c69b8de3144377ec0b4ca4c44cc9d71b1341e62c94a0"),
                    out _),
                Is.True);
        }

        [Test]
        public void Verify_NoPackageUnknownTree_IsModifiedSource()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        hasPackage: false,
                        includeDigest:
                            "0000000000000000000000000000000000000000000000000000000000000000"),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic?.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [Test]
        public void VerifyCutout_ExternalActivatorProvenance_IsRefused()
        {
            // A representative mutation from the provenance-refusal family:
            // an activator define injected at the head of the setting run
            // breaks the generator ordering of the official record, so the
            // record check refuses it as modified source.
            var clean = PassAnalysis(DefaultStandaloneRecords());
            var records = DefaultStandaloneRecords();
            records.Insert(1, "#define LIL_FEATURE_LTCGI");
            var mutated = PassAnalysis(records);

            Assert.That(
                mutated.CanonicalSource, Is.EqualTo(clean.CanonicalSource));
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(passCanonicalization: mutated),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        /// <summary>
        /// The stand-in asset folder the gather tests share.
        /// </summary>
        private const string ProbeFolder = "Assets/AmuseTests_LilToonAttestation";

        private const string ProbeShaderPath =
            ProbeFolder + "/LilToonCutoutAttestationProbe.shader";

        /// <summary>
        /// Writes the stand-in shader the gather tests share: a stand-in
        /// carrying the pinned cutout identity, the exact shader name, the
        /// exact pinned asset GUID (hand-written .meta), and the format
        /// stamp. The pass asset Hidden/ltspass_cutout is deliberately never
        /// created — this project has no resolvable cutout pass of its own,
        /// which is the fail-closed premise the gather tests share.
        /// </summary>
        private static Shader WriteSyntheticCutoutProbeShader()
        {
            if (!AssetDatabase.IsValidFolder(ProbeFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_LilToonAttestation");
            }

            File.WriteAllText(
                ProbeShaderPath,
                "Shader \"Hidden/lilToonCutout\"\n" +
                "{\n" +
                "    Properties\n" +
                "    {\n" +
                "        [HideInInspector] _lilToonVersion" +
                " (\"Version\", Int) = 45\n" +
                "        _Invisible (\"Invisible\", Int) = 0\n" +
                "        _UDIMDiscardCompile (\"UDIM\", Int) = 0\n" +
                "    }\n" +
                "    SubShader { Pass {} }\n" +
                "}\n");
            File.WriteAllText(
                ProbeShaderPath + ".meta",
                "fileFormatVersion: 2\n" +
                "guid: 85d6126cae43b6847aff4b13f4adb8ec\n" +
                "ShaderImporter:\n" +
                "  externalObjects: {}\n" +
                "  defaultTextures: []\n" +
                "  nonModifiableTextures: []\n" +
                "  userData: \n" +
                "  assetBundleName: \n" +
                "  assetBundleVariant: \n");
            AssetDatabase.ImportAsset(
                ProbeShaderPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Shader>(ProbeShaderPath);
        }

        private static void DeleteSyntheticProbeFolder()
        {
            if (AssetDatabase.IsValidFolder(ProbeFolder))
            {
                AssetDatabase.DeleteAsset(ProbeFolder);
            }
        }

        /// <summary>
        /// The suite's synthetic-shader gather path: one fixture material,
        /// captured through the production request and gathered through the
        /// production entry point the suite drives.
        /// </summary>
        private static LilToonSourceEvidence GatherSourceEvidence(
            Material fixtureMaterial)
        {
            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    fixtureMaterial,
                    LilToonMaterialSemantics.AlphaEvidenceRequest),
            })[0];

            return LilToonSourceAttestation.GatherCutoutSourceEvidence(
                fixtureMaterial.shader, captured);
        }

        private static LilToonSourceEvidence GatherSourceEvidence(
            Shader fixtureShader)
        {
            var material = new Material(fixtureShader);
            try
            {
                return GatherSourceEvidence(material);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        /// <summary>
        /// One gather serves repeated materials on one shader until the
        /// build boundary: the second gather answers with the first gather's
        /// own evidence instance. The per-build clear sites drop the memo,
        /// so the next build re-gathers.
        /// </summary>
        [Test]
        public void AGatheredShaderSourceIsMemoizedUntilTheBuildBoundary()
        {
            try
            {
                var fixtureShader = WriteSyntheticCutoutProbeShader();
                Assert.That(fixtureShader, Is.Not.Null, ProbeShaderPath);

                var first = GatherSourceEvidence(fixtureShader);
                var second = GatherSourceEvidence(fixtureShader);
                Assert.That(
                    second,
                    Is.SameAs(first),
                    "one gather serves repeated materials on one shader");
            }
            finally
            {
                DeleteSyntheticProbeFolder();
            }
        }

        /// <summary>
        /// Two materials on one shader with different serialized
        /// _lilToonVersion stamps must each gather their own stamp. The
        /// gather embeds the stamp it reads from the captured material, so
        /// the memo key carries it: a key without the stamp lets the first
        /// material's stamp serve every later material on the shader.
        /// </summary>
        [Test]
        public void TwoMaterialsOnOneShaderWithDivergentStampsGatherTheirOwnEvidence()
        {
            try
            {
                var fixtureShader = WriteSyntheticCutoutProbeShader();
                Assert.That(fixtureShader, Is.Not.Null, ProbeShaderPath);

                var restamped = new Material(fixtureShader);
                var untouched = new Material(fixtureShader);
                try
                {
                    restamped.SetFloat("_lilToonVersion", 46f);

                    // The restamped material gathers first. The untouched
                    // material's serialized stamp is the shader default 45,
                    // so first-gather-wins is observable: its 46 would
                    // serve the untouched material too.
                    var restampedEvidence = GatherSourceEvidence(restamped);
                    var untouchedEvidence = GatherSourceEvidence(untouched);

                    Assert.That(
                        restampedEvidence.HasShaderFormatVersion,
                        Is.True);
                    Assert.That(
                        restampedEvidence.ShaderFormatVersion,
                        Is.EqualTo(46f));
                    Assert.That(
                        untouchedEvidence.HasShaderFormatVersion,
                        Is.True);
                    Assert.That(
                        untouchedEvidence.ShaderFormatVersion,
                        Is.EqualTo(45f),
                        "the first gather's stamp was served to the " +
                        "second material");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(restamped);
                    UnityEngine.Object.DestroyImmediate(untouched);
                }
            }
            finally
            {
                DeleteSyntheticProbeFolder();
            }
        }

        [Test]
        public void GatherCutout_MissingPassAsset_FailsClosedWithoutNameOnlyFallback()
        {
            try
            {
                var shader = WriteSyntheticCutoutProbeShader();
                Assert.That(shader, Is.Not.Null, ProbeShaderPath);

                var material = new Material(shader);
                try
                {
                    var captured = UnityMaterialEvidenceCapture.Capture(new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            material,
                            LilToonMaterialSemantics.AlphaEvidenceRequest),
                    })[0];

                    var evidence =
                        LilToonSourceAttestation.GatherCutoutSourceEvidence(
                            shader, captured);

                    // The gather must identify the pass only by the pinned
                    // pass name, never by the material shader's own name or
                    // GUID. Whether Hidden/ltspass_cutout resolves depends
                    // on the environment: with jp.lilxyzw.liltoon installed
                    // the vendor pass asset resolves; without it nothing
                    // resolves. Both environments prove the same rule.
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        shader, out var materialGuid, out long _);
                    if (Shader.Find(
                            LilToonSourceAttestation.CutoutPassShaderName) !=
                        null)
                    {
                        // Vendor present: the pass resolves by name to the
                        // pinned pass asset. A name-only fallback would
                        // have recorded the material shader's own GUID.
                        Assert.That(
                            evidence.PassShaderGuid,
                            Is.EqualTo(
                                LilToonSourceAttestation
                                    .CutoutPassShaderGuid));
                        Assert.That(
                            evidence.PassShaderGuid,
                            Is.Not.EqualTo(
                                materialGuid.ToLowerInvariant()));
                    }
                    else
                    {
                        // No resolvable pass: the gather leaves the pass
                        // unidentified rather than fall back to the
                        // material shader's name.
                        Assert.That(evidence.PassShaderGuid, Is.Null);
                    }

                    // The stand-in's own bytes are not the pinned shader
                    // bytes, so the identity conjunction refuses in both
                    // environments.
                    Assert.That(
                        LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                            evidence, out var diagnostic),
                        Is.False);
                    Assert.That(diagnostic, Is.Not.Null);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
            finally
            {
                DeleteSyntheticProbeFolder();
            }
        }

        // --- cutout pins are the B1 measurements ---

        [Test]
        public void PinnedCutoutConstants_AreTheB1Measurements()
        {
            Assert.That(
                LilToonSourceAttestation.CutoutShaderName,
                Is.EqualTo("Hidden/lilToonCutout"));
            Assert.That(
                LilToonSourceAttestation.CutoutShaderGuid,
                Is.EqualTo("85d6126cae43b6847aff4b13f4adb8ec"));
            Assert.That(
                LilToonSourceAttestation.CutoutPassShaderName,
                Is.EqualTo("Hidden/ltspass_cutout"));
            Assert.That(
                LilToonSourceAttestation.CutoutPassShaderGuid,
                Is.EqualTo("ad219df2a46e841488aee6a013e84e36"));
            Assert.That(
                LilToonSourceAttestation.CutoutRenderMode,
                Is.EqualTo(1));
            Assert.That(
                LilToonSourceAttestation.CutoutShaderCanonicalDigest,
                Is.EqualTo(
                    "c83d73a26ab86e933f8cacb8c71307d8715fcc1693cdc08d209011bb0f836178"));
            Assert.That(
                LilToonSourceAttestation.CutoutPassCanonicalDigest,
                Is.EqualTo(
                    "91563265289452c61e50203235792fadba17c828dbbf59c9f8ce6013538b15fc"));
        }

        // --- S8: outline wrapper identities --------------------------------

        /// <summary>
        /// The three pinned outline wrapper identities are the measured tag
        /// 2.3.4 values. Pins are data: this audit fails when a digest or
        /// GUID is retyped, so a silent value change cannot pass as a
        /// refactor.
        /// </summary>
        [Test]
        public void OutlineWrapperIdentityPins_AreTheMeasuredValues()
        {
            Assert.That(
                LilToonSourceAttestation.OutlineShaderName,
                Is.EqualTo("Hidden/lilToonOutline"));
            Assert.That(
                LilToonSourceAttestation.OutlineShaderGuid,
                Is.EqualTo("efa77a80ca0344749b4f19fdd5891cbe"));
            Assert.That(
                LilToonSourceAttestation.OutlineShaderCanonicalDigest,
                Is.EqualTo(
                    "bbd886afd367d73ba3e2208aa42086e9149ccf826564ce4bb4f571d16861aa36"));
            Assert.That(
                LilToonSourceAttestation.OutlineCutoutShaderName,
                Is.EqualTo("Hidden/lilToonCutoutOutline"));
            Assert.That(
                LilToonSourceAttestation.OutlineCutoutShaderGuid,
                Is.EqualTo("3b4aa19949601f046a20ca8bdaee929f"));
            Assert.That(
                LilToonSourceAttestation.OutlineCutoutShaderCanonicalDigest,
                Is.EqualTo(
                    "9fa9e7e7be55d29851fe4dd5cf2078e259a0b439dd1b61075a7c0448c176a9ec"));
            Assert.That(
                LilToonSourceAttestation.OutlineTransparentShaderName,
                Is.EqualTo("Hidden/lilToonTransparentOutline"));
            Assert.That(
                LilToonSourceAttestation.OutlineTransparentShaderGuid,
                Is.EqualTo("3c79b10c7e0b2784aaa4c2f8dd17d55e"));
            Assert.That(
                LilToonSourceAttestation.OutlineTransparentShaderCanonicalDigest,
                Is.EqualTo(
                    "d105a112d4b8c984baf00ccbffd56213d1e399ef7d4de122b2f39442d2ac198f"));
        }

        [Test]
        public void Verify_OutlineOpaqueWrapperIdentity_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        shaderName:
                            LilToonSourceAttestation.OutlineShaderName,
                        assetGuid:
                            LilToonSourceAttestation.OutlineShaderGuid,
                        shaderDigest:
                            LilToonSourceAttestation
                                .OutlineShaderCanonicalDigest),
                    out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Verify_OutlineCutoutWrapperIdentity_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    Evidence(
                        shaderName:
                            LilToonSourceAttestation
                                .OutlineCutoutShaderName,
                        assetGuid:
                            LilToonSourceAttestation
                                .OutlineCutoutShaderGuid,
                        passGuid:
                            LilToonSourceAttestation.CutoutPassShaderGuid,
                        shaderDigest:
                            LilToonSourceAttestation
                                .OutlineCutoutShaderCanonicalDigest,
                        passDigest:
                            LilToonSourceAttestation
                                .CutoutPassCanonicalDigest,
                        renderMode: 1),
                    out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Verify_OutlineTransparentWrapperIdentity_Succeeds()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonTransparentIdentity(
                    Evidence(
                        shaderName:
                            LilToonSourceAttestation
                                .OutlineTransparentShaderName,
                        assetGuid:
                            LilToonSourceAttestation
                                .OutlineTransparentShaderGuid,
                        passGuid:
                            LilToonSourceAttestation
                                .TransparentPassShaderGuid,
                        shaderDigest:
                            LilToonSourceAttestation
                                .OutlineTransparentShaderCanonicalDigest,
                        passDigest:
                            LilToonSourceAttestation
                                .TransparentPassCanonicalDigest,
                        renderMode: 2),
                    out var diagnostic),
                Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        /// <summary>
        /// The S9 transparent-variant identities are pinned data: the names,
        /// GUIDs, and measured digests must equal the official tag 2.3.4
        /// values, so a silent repin is impossible.
        /// </summary>
        [Test]
        public void TransparentVariantIdentityPins_AreTheMeasuredValues()
        {
            Assert.That(
                LilToonSourceAttestation.OnePassTransparentShaderName,
                Is.EqualTo("Hidden/lilToonOnePassTransparent"));
            Assert.That(
                LilToonSourceAttestation.OnePassTransparentShaderGuid,
                Is.EqualTo("b269573b9937b8340b3e9e191a3ba5a8"));
            Assert.That(
                LilToonSourceAttestation
                    .OnePassTransparentShaderCanonicalDigest,
                Is.EqualTo(
                    "4a5bbd07997e3150bf904aae89223b207c2628e2f1e2d7fe690bcdb76c0d7143"));
            Assert.That(
                LilToonSourceAttestation.TwoPassTransparentShaderName,
                Is.EqualTo("Hidden/lilToonTwoPassTransparent"));
            Assert.That(
                LilToonSourceAttestation.TwoPassTransparentShaderGuid,
                Is.EqualTo("6a77405f7dfdc1447af58854c7f43f39"));
            Assert.That(
                LilToonSourceAttestation
                    .TwoPassTransparentShaderCanonicalDigest,
                Is.EqualTo(
                    "c06143d3d345efc1c1dcc8128193a3bb9b1535e4c640b024562923b7b7008074"));
            Assert.That(
                LilToonSourceAttestation
                    .OnePassTransparentOutlineShaderName,
                Is.EqualTo("Hidden/lilToonOnePassTransparentOutline"));
            Assert.That(
                LilToonSourceAttestation
                    .OnePassTransparentOutlineShaderGuid,
                Is.EqualTo("7171688840c632447b22ec14e2bdef7e"));
            Assert.That(
                LilToonSourceAttestation
                    .OnePassTransparentOutlineShaderCanonicalDigest,
                Is.EqualTo(
                    "7d87faf6ad3f8217f86b91330d6b1767b75fd91d689ae6f7243c6808b3009f80"));
            Assert.That(
                LilToonSourceAttestation
                    .TwoPassTransparentOutlineShaderName,
                Is.EqualTo("Hidden/lilToonTwoPassTransparentOutline"));
            Assert.That(
                LilToonSourceAttestation
                    .TwoPassTransparentOutlineShaderGuid,
                Is.EqualTo("9cf054060007d784394b8b0bb703e441"));
            Assert.That(
                LilToonSourceAttestation
                    .TwoPassTransparentOutlineShaderCanonicalDigest,
                Is.EqualTo(
                    "af28ad17be74f0077a5a4b154a81a70d3dd98bc0e387b93f859b97dd4c505974"));
        }

        /// <summary>
        /// Each S9 variant identity verifies against the transparent family
        /// with its own pinned name, GUID, and digest over the shared pinned
        /// transparent pass asset and LIL_RENDER 2 - the same conjunction
        /// the S8 outline wrappers already satisfy.
        /// </summary>
        [Test]
        public void Verify_TransparentVariantIdentities_Succeed()
        {
            foreach (var (name, guid, digest) in new[]
                     {
                         (LilToonSourceAttestation.OnePassTransparentShaderName,
                             LilToonSourceAttestation.OnePassTransparentShaderGuid,
                             LilToonSourceAttestation
                                 .OnePassTransparentShaderCanonicalDigest),
                         (LilToonSourceAttestation.TwoPassTransparentShaderName,
                             LilToonSourceAttestation.TwoPassTransparentShaderGuid,
                             LilToonSourceAttestation
                                 .TwoPassTransparentShaderCanonicalDigest),
                         (LilToonSourceAttestation
                             .OnePassTransparentOutlineShaderName,
                             LilToonSourceAttestation
                                 .OnePassTransparentOutlineShaderGuid,
                             LilToonSourceAttestation
                                 .OnePassTransparentOutlineShaderCanonicalDigest),
                         (LilToonSourceAttestation
                             .TwoPassTransparentOutlineShaderName,
                             LilToonSourceAttestation
                                 .TwoPassTransparentOutlineShaderGuid,
                             LilToonSourceAttestation
                                 .TwoPassTransparentOutlineShaderCanonicalDigest),
                     })
            {
                Assert.That(
                    LilToonSourceAttestation
                        .TryVerifyLilToonTransparentIdentity(
                            Evidence(
                                shaderName: name,
                                assetGuid: guid,
                                passGuid:
                                    LilToonSourceAttestation
                                        .TransparentPassShaderGuid,
                                shaderDigest: digest,
                                passDigest:
                                    LilToonSourceAttestation
                                        .TransparentPassCanonicalDigest,
                                renderMode: 2),
                            out var diagnostic),
                    Is.True, name);
                Assert.That(diagnostic, Is.Null, name);
            }
        }

        /// <summary>
        /// A variant name selects its variant profile, so evidence carrying
        /// the variant name over the plain transparent digest must fail
        /// closed on the digest, not fall through to the plain profile.
        /// </summary>
        [Test]
        public void Verify_TransparentVariantNameWithPlainDigest_FailsClosed()
        {
            foreach (var (name, guid) in new[]
                     {
                         (LilToonSourceAttestation.OnePassTransparentShaderName,
                             LilToonSourceAttestation
                                 .OnePassTransparentShaderGuid),
                         (LilToonSourceAttestation.TwoPassTransparentShaderName,
                             LilToonSourceAttestation
                                 .TwoPassTransparentShaderGuid),
                         (LilToonSourceAttestation
                             .OnePassTransparentOutlineShaderName,
                             LilToonSourceAttestation
                                 .OnePassTransparentOutlineShaderGuid),
                         (LilToonSourceAttestation
                             .TwoPassTransparentOutlineShaderName,
                             LilToonSourceAttestation
                                 .TwoPassTransparentOutlineShaderGuid),
                     })
            {
                Assert.That(
                    LilToonSourceAttestation
                        .TryVerifyLilToonTransparentIdentity(
                            Evidence(
                                shaderName: name,
                                assetGuid: guid,
                                passGuid:
                                    LilToonSourceAttestation
                                        .TransparentPassShaderGuid,
                                shaderDigest:
                                    LilToonSourceAttestation
                                        .TransparentShaderCanonicalDigest,
                                passDigest:
                                    LilToonSourceAttestation
                                        .TransparentPassCanonicalDigest,
                                renderMode: 2),
                            out var diagnostic),
                    Is.False, name);
                Assert.That(
                    diagnostic?.Code,
                    Is.EqualTo(
                        LilToonSemanticDiagnosticCode.ModifiedShaderSource),
                    name);
            }
        }

        /// <summary>
        /// A wrapper name selects the wrapper profile, so evidence carrying
        /// the wrapper name over the plain shader's digest must fail closed
        /// on the digest, not fall through to the plain profile.
        /// </summary>
        [Test]
        public void Verify_OutlineWrapperNameWithPlainDigest_FailsClosed()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(
                        shaderName:
                            LilToonSourceAttestation.OutlineShaderName,
                        assetGuid:
                            LilToonSourceAttestation.OutlineShaderGuid,
                        shaderDigest:
                            LilToonSourceAttestation.ShaderCanonicalDigest),
                    out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic?.Code,
                Is.EqualTo(
                    LilToonSemanticDiagnosticCode.ModifiedShaderSource));
        }

        [TestCase("Hidden/lilToonOutline", "Hidden/lilToonOutline")]
        [TestCase(
            "Hidden/lilToonCutoutOutline", "Hidden/lilToonOutline")]
        [TestCase(
            "Hidden/lilToonTransparentOutline",
            "Hidden/lilToonOutline")]
        [TestCase("lilToon", "lilToon")]
        [TestCase("Hidden/lilToonCutout", "lilToon")]
        [TestCase("Hidden/lilToonOnePassTransparent", "lilToon")]
        [TestCase("Hidden/lilToonTwoPassTransparent", "lilToon")]
        [TestCase(
            "Hidden/lilToonOnePassTransparentOutline",
            "Hidden/lilToonOutline")]
        [TestCase(
            "Hidden/lilToonTwoPassTransparentOutline",
            "Hidden/lilToonOutline")]
        [TestCase("Some/Unknown", "lilToon")]
        public void ResolveCanonicalTargetShaderName_MapsOutlineWrappersToOutlineOpaqueAndPlainToPlain(
            string sourceShaderName,
            string expectedTargetName)
        {
            Assert.That(
                LilToonSourceAttestation.ResolveCanonicalTargetShaderName(
                    sourceShaderName),
                Is.EqualTo(expectedTargetName));
        }

        [Test]
        public void ResolveCanonicalTargetShaderName_OutlineWrappers_ReturnsOutlineOpaqueShader()
        {
            Assert.That(
                LilToonSourceAttestation.ResolveCanonicalTargetShaderName(LilToonSourceAttestation.OutlineCutoutShaderName),
                Is.EqualTo(LilToonSourceAttestation.OutlineShaderName));
            Assert.That(
                LilToonSourceAttestation.ResolveCanonicalTargetShaderName(LilToonSourceAttestation.OutlineTransparentShaderName),
                Is.EqualTo(LilToonSourceAttestation.OutlineShaderName));
        }
    }
}
