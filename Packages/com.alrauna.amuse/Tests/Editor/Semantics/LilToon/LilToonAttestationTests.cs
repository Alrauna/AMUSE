using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;

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
            IReadOnlyCollection<string> features = null)
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
                features ?? new string[0]);
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
    }
}
