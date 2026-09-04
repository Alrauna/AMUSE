using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Canonicalization, digesting, define and render-mode scanning, exact
    /// version comparison, and the verification conjunction. Every case is
    /// deterministic and needs no installed lilToon package.
    /// </summary>
    public sealed class LilToonAttestationTests
    {
        // Path arithmetic only; nothing here touches the filesystem.
        private static readonly string ProjectRoot =
            Path.Combine(Path.GetTempPath(), "AmuseTests", "Project");

        private static readonly string ShaderDir = Path.Combine(
            ProjectRoot, "Packages", "jp.lilxyzw.liltoon", "Shader");

        /// <summary>
        /// Sentinel meaning "use the valid pin". A plain null default would make
        /// it impossible to test genuinely missing evidence, because null would
        /// be coalesced back to the pin.
        /// </summary>
        private const string UsePin = "\0use-pinned-digest";

        private static LilToonIncludeTree Tree()
        {
            return LilToonIncludeTree.ForTests(
                Path.Combine(ShaderDir, "Includes"),
                new[]
                {
                    ("lil_common.hlsl", "11"),
                    ("VRC Light Volumes/LightVolumes.cginc", "22"),
                });
        }

        private static string Canon(string source)
        {
            // The project root is an explicit argument; canonicalization must
            // never consult the process working directory.
            return LilToonSourceAttestation.Canonicalize(
                source, ShaderDir, ProjectRoot, Tree());
        }

        private static LilToonCanonicalizationAnalysis Analyze(string source)
        {
            return LilToonSourceAttestation.AnalyzeCanonicalization(
                source, ShaderDir, ProjectRoot, Tree());
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
            string packageVersion = "2.3.4",
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

        /// <summary>Adds <paramref name="steps"/> ULPs to a float.</summary>
        private static float Ulp(float value, int steps)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits + steps), 0);
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
                    "#define LIL_FEATURE_LTCGI"),
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
        public void Canonicalize_ConstantSkipVariantsAfterSettingRegion_IsRetained()
        {
            // GetSkipVariants{Decals,AddLightShadows,ProbeVolumes,AO} return
            // fixed literals, so their lines are stable across settings and must
            // be hashed rather than dropped.
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

            Assert.That(Canon(withTail), Is.Not.EqualTo(Canon(withoutTail)));
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
        public void Canonicalize_MultiKeywordPragmaAtSlot_IsRetained()
        {
            // The dedup pass reduces a surviving line to one keyword, so a
            // multi-keyword line at the slot is not generator output either.
            var multi = ForwardPrologue(
                "            #pragma skip_variants SHADOW_HIGH SHADOW_VERY_HIGH\n");
            var absent = ForwardPrologue(string.Empty);

            Assert.That(Canon(multi), Is.Not.EqualTo(Canon(absent)));
        }

        [Test]
        public void Canonicalize_ShadowPragmaAwayFromSlot_IsRetained()
        {
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

            Assert.That(Canon(offSlot), Is.Not.EqualTo(Canon(without)));
            Assert.That(Canon(offSlot), Does.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_ShadowPragmaAfterDifferentDefine_IsRetained()
        {
            var afterOtherDefine =
                "            #define LIL_PASS_FORWARDADD\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n";

            Assert.That(Canon(afterOtherDefine), Does.Contain("SHADOW_VERY_HIGH"));
        }

        [Test]
        public void Canonicalize_ShadowPragmaInPassBody_IsRetained()
        {
            var injected =
                "    HLSLPROGRAM\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "            #include \"Includes/lil_common.hlsl\"\n" +
                "            #pragma skip_variants SHADOW_VERY_HIGH\n" +
                "    ENDHLSL\n";

            Assert.That(Canon(injected), Does.Contain("SHADOW_VERY_HIGH"));
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
        public void Verify_HiddenExternalActivatorWithOldCanonicalOutput_IsRefused(
            string identifier)
        {
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
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
            Assert.That(diagnostic.Detail, Does.Contain(identifier));
        }

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
        public void Verify_BothLightVolumesForms_AreRefused()
        {
            var records = DefaultStandaloneRecords();
            InsertAfter(
                records,
                "#define LIL_OPTIMIZE_USE_VERTEXLIGHT",
                "#define LIL_FEATURE_VRCLIGHTVOLUMES");
            InsertAfter(
                records,
                "#define LIL_FEATURE_VRCLIGHTVOLUMES",
                "#define LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE");

            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(passCanonicalization: PassAnalysis(records)), out _),
                Is.False);
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
            var nextAbove = Ulp(45f, 1);
            var nextBelow = Ulp(45f, -1);

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

        [Test]
        public void Verify_WrongPackageVersion_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    Evidence(packageVersion: "2.3.3"), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
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

        // --- pins are the Task 0 measurements ---

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
                    "6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14"));
            Assert.That(
                LilToonSourceAttestation.IncludeTreeDigest,
                Is.EqualTo(
                    "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46"));
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
            string packageVersion = "2.3.4",
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
            string packageVersion = "2.3.4",
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
                    "700a607661f2cc43550452795d8eae0634509dbd07b4e8c381d94" +
                    "12fcc52517f"));
        }

        [TestCase("Hidden/lilToonOnePassTransparent")]
        [TestCase("Hidden/lilToonTwoPassTransparent")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        [TestCase("Hidden/lilToonTransparen")]
        [TestCase("hidden/liltoontransparent")]
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

        // --- rows 12-13: exact-name selection ------------------------------

        [TestCase("Hidden/lilToonOnePassTransparent")]
        [TestCase("Hidden/lilToonTwoPassTransparent")]
        [TestCase("Hidden/lilToonTransparentOutline")]
        public void NearMissTransparentName_IsNeverSelectedOrAdmitted(
            string shaderName)
        {
            // Both near misses declare the SAME pass asset
            // (Hidden/ltspass_transparent) and the same LIL_RENDER 2, queue
            // 2460 and RenderType as the supported family. Falsifies:
            // prefix/substring matching, Contains("Transparent"), grouping by
            // LIL_RENDER, by queue, or by pass-asset identity alone.
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

        [Test]
        public void VerifyCutout_WrongPackageVersion_IsRefused()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                    CutoutEvidence(packageVersion: "2.3.3"), out var diagnostic),
                Is.False);
            Assert.That(
                diagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedVersion));
        }

        [Test]
        public void VerifyCutout_ExternalActivatorProvenance_IsRefused()
        {
            // One representative mutation from the existing provenance-refusal
            // family: an external activator define keeps the old canonical
            // output but refuses as an unsupported variant.
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
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant));
            Assert.That(diagnostic.Detail, Does.Contain("LIL_FEATURE_LTCGI"));
        }

        [Test]
        public void GatherCutout_MissingPassAsset_FailsClosedWithoutNameOnlyFallback()
        {
            const string folder = "Assets/AmuseTests_LilToonAttestation";
            const string shaderPath =
                folder + "/LilToonCutoutAttestationProbe.shader";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_LilToonAttestation");
            }

            try
            {
                // A stand-in carrying the pinned cutout identity: the exact
                // shader name, the exact pinned asset GUID (hand-written
                // .meta), and the format stamp. The pass asset
                // Hidden/ltspass_cutout is deliberately never created — this
                // project has no resolvable cutout pass, which is the
                // fail-closed premise under test.
                File.WriteAllText(
                    shaderPath,
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
                    shaderPath + ".meta",
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
                    shaderPath, ImportAssetOptions.ForceSynchronousImport);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                Assert.That(shader, Is.Not.Null, shaderPath);

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

                    // No resolvable Hidden/ltspass_cutout asset: the gather
                    // must leave the pass unidentified rather than fall back
                    // to the material shader's name.
                    Assert.That(evidence.PassShaderGuid, Is.Null);

                    Assert.That(
                        LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                            evidence, out var diagnostic),
                        Is.False);
                    Assert.That(
                        diagnostic.Code,
                        Is.EqualTo(
                            LilToonSemanticDiagnosticCode.MissingSourceEvidence));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.DeleteAsset(folder);
                }
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
                    "ecd1caedc99c4569fb17898de16ce2025c21e2d191e06532098370a1291bfe92"));
        }
    }
}
