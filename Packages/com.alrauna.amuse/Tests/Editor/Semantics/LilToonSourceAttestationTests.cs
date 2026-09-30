using System.IO;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Canonicalizer shape agreement for the lilToonMulti containers. The
    /// fixtures are schema-only source strings shaped like the vendor base
    /// container: no LIL_RENDER define, a SubShader-scope HLSLINCLUDE, and
    /// the valueless settings block the generator rewrites per project.
    /// Every case is deterministic and needs no installed lilToon package.
    /// </summary>
    public sealed class LilToonSourceAttestationTests
    {
        // Path arithmetic only. Nothing here touches the file system.
        private static readonly string ProjectRoot =
            Path.Combine(Path.GetTempPath(), "AmuseTests", "Project");

        private static readonly string ShaderDir = Path.Combine(
            ProjectRoot, "Packages", "jp.lilxyzw.liltoon", "Shader");

        private static LilToonIncludeTree Tree()
        {
            return LilToonIncludeTree.ForTests(
                Path.Combine(ShaderDir, "Includes"),
                new[] { ("lil_common.hlsl", "11") });
        }

        private static string Canon(string source)
        {
            // The project root is an explicit argument. Canonicalization
            // must never consult the process working directory.
            return LilToonSourceAttestation.Canonicalize(
                source, ShaderDir, ProjectRoot, Tree());
        }

        private const string DefaultSettingsBlock =
            "            #define LIL_OPTIMIZE_APPLY_SHADOW_FA\n" +
            "            #define LIL_OPTIMIZE_USE_FORWARDADD\n" +
            "            #define LIL_OPTIMIZE_USE_VERTEXLIGHT\n" +
            "            #define LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE\n";

        private const string ReducedSettingsBlock =
            "            #define LIL_OPTIMIZE_APPLY_SHADOW_FA\n";

        /// <summary>
        /// Builds one installed-shape candidate of the base container. The
        /// settings block is the generator-rewritten region. Everything
        /// around it is fixed container text. The block sits below the
        /// container's fixed pragma lines, inside the SubShader HLSLINCLUDE.
        /// </summary>
        private static string MultiContainerSource(string settingsBlock)
        {
            return
                "Shader \"_lil/lilToonMulti\"\n" +
                "{\n" +
                "    Properties\n" +
                "    {\n" +
                "        _Color (\"Color\", Color) = (1, 1, 1, 1)\n" +
                "    }\n" +
                "\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags {\"RenderType\" = \"Opaque\"" +
                " \"Queue\" = \"Geometry\"}\n" +
                "        HLSLINCLUDE\n" +
                "            #pragma target 3.5\n" +
                "            #pragma fragmentoption" +
                " ARB_precision_hint_fastest\n" +
                settingsBlock +
                "            #define LIL_MULTI\n" +
                "            #define LIL_MULTI_INPUTS_SHADOW\n" +
                "            #pragma skip_variants _DBUFFER_MRT3\n" +
                "        ENDHLSL\n" +
                "\n" +
                "        Pass\n" +
                "        {\n" +
                "            Name \"FORWARD\"\n" +
                "            Tags {\"LightMode\" = \"ForwardBase\"}\n" +
                "            #define LIL_PASS_FORWARD\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
        }

        /// <summary>
        /// A settings-only change inside the Multi settings block must not
        /// move the canonical digest. The two sources are byte-identical
        /// except for the valueless LIL_OPTIMIZE_* lines. The plausible
        /// wrong implementation is a file-wide removal of valueless
        /// defines. The existing attestation tests refuse a file-wide
        /// removal. A valueless define away from the setting region must
        /// stay hashed.
        /// </summary>
        // --- Falsifier 9 (spec): a canonicalizer that retains the Multi settings block fails the shape-agreement fixture. ---
        [Test]
        public void MultiSettingsBlockChangeKeepsCanonicalDigestStable()
        {
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(
                    Canon(MultiContainerSource(DefaultSettingsBlock))),
                Is.EqualTo(
                    LilToonSourceAttestation.ComputeNormalizedSourceHash(
                        Canon(MultiContainerSource(ReducedSettingsBlock)))),
                "the Multi settings block is the only difference between " +
                "the two sources, so the canonical digest must not move");
        }
    }
}
