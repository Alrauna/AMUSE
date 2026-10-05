using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Canonicalizer shape agreement and container verify falsifiers for the
    /// lilToonMulti containers. The fixtures are schema-only source strings
    /// shaped like the vendor base container: no LIL_RENDER define, a
    /// SubShader-scope HLSLINCLUDE, and the valueless settings block the
    /// generator rewrites per project. Every case is deterministic and needs
    /// no installed lilToon package.
    /// <para>
    /// The verify falsifiers drive the Multi verify conjunction through an
    /// internal profile-injection seam. The production Multi profile table
    /// stays empty until the Task 2 digest measurement lands, so the
    /// production entry point refuses every Multi container and the
    /// fixtures inject one test-only profile row instead.
    /// </para>
    /// </summary>
    public sealed class LilToonSourceAttestationTests
    {

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
        /// around it is fixed container text. By default the block sits
        /// below the container's fixed pragma lines, inside the SubShader
        /// HLSLINCLUDE. With <paramref name="adjacentToHlslInclude"/> the
        /// block sits directly after the HLSLINCLUDE line, which is the
        /// vendor template layout at the pin.
        /// </summary>
        private static string MultiContainerSource(string settingsBlock)
        {
            return MultiContainerSource(
                settingsBlock, adjacentToHlslInclude: false);
        }

        private static string MultiContainerSource(
            string settingsBlock, bool adjacentToHlslInclude)
        {
            var head =
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
                "        HLSLINCLUDE\n";
            var pragmas =
                "            #pragma target 3.5\n" +
                "            #pragma fragmentoption" +
                " ARB_precision_hint_fastest\n";
            var tail =
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

            return head +
                (adjacentToHlslInclude
                    ? settingsBlock + pragmas
                    : pragmas + settingsBlock) +
                tail;
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
                    AttestationEnvironment.Canon(
                        MultiContainerSource(DefaultSettingsBlock))),
                Is.EqualTo(
                    LilToonSourceAttestation.ComputeNormalizedSourceHash(
                        AttestationEnvironment.Canon(
                            MultiContainerSource(ReducedSettingsBlock)))),
                "the Multi settings block is the only difference between " +
                "the two sources, so the canonical digest must not move");
        }

        /// <summary>
        /// Characterization at 728079c, not a falsifier. The installed
        /// vendor containers carry the settings run directly after the
        /// SubShader HLSLINCLUDE line, and the canonicalizer already
        /// stripped that adjacent run at the pin. This fixture records that
        /// pre-existing behavior so the Task 2 digest measurement rests on
        /// an observed canonicalization. It passed on the pre-change code
        /// and must keep passing after the region extension.
        /// </summary>
        [Test]
        public void AdjacentMultiSettingsBlockChangeKeepsCanonicalDigestStable()
        {
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(
                    AttestationEnvironment.Canon(
                        MultiContainerSource(
                            DefaultSettingsBlock, adjacentToHlslInclude: true))),
                Is.EqualTo(
                    LilToonSourceAttestation.ComputeNormalizedSourceHash(
                        AttestationEnvironment.Canon(
                            MultiContainerSource(
                                ReducedSettingsBlock,
                                adjacentToHlslInclude: true)))),
                "the adjacent Multi settings block is the only difference " +
                "between the two sources, so the canonical digest must not " +
                "move");
        }

        // --- Multi container attestation verify (Task 6) ---

        // Test-only profile row for the base container. The production Multi
        // profile table stays empty until Task 2 pins the container digests
        // from installed shapes. The GUID and digest below are synthetic test
        // vocabulary, never measured pins. Task 2 fills the production table,
        // and no fixture value moves there.
        private const string BaseContainerName = "_lil/lilToonMulti";
        private const string BaseContainerGuid =
            "0f19e2a4b7c8d6051324a5b6c7d8e9f0";
        private const string BaseContainerCanonicalDigest =
            "1029384756afbccddeeff0123456789a" +
            "fedcba98765432100123456789abcdef";

        // Sentinel meaning "use the pin". A plain null default would make it
        // impossible to test genuinely missing digests, because null would be
        // coalesced back to the pin. Same pattern as the regular attestation
        // tests.
        private const string UsePin = "\0use-injected-pin";

        // The include-tree digest the 2.3.1 to 2.3.3 rows share. The
        // AdmittedPackageVersions rows are private, so the fixture repeats the
        // value. The prior-version fixture passes it with the 2.3.3 package
        // version, so the version row is the only mismatching conjunction
        // term.
        private const string PriorVersionIncludeDigest =
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416";

        private static LilToonCanonicalizationAnalysis AnalyzeMulti(
            string source)
        {
            return LilToonSourceAttestation.AnalyzeCanonicalization(
                source,
                AttestationEnvironment.ShaderDir,
                AttestationEnvironment.ProjectRoot,
                AttestationEnvironment.Tree());
        }

        /// <summary>
        /// Evidence shaped like one generated base container. A Multi
        /// container declares no LIL_RENDER and carries no pass asset, so the
        /// evidence has no render mode and no pass half. A wrong
        /// implementation that reuses the regular profile path refuses this
        /// shape for the missing render mode and the missing pass asset.
        /// </summary>
        private static LilToonSourceEvidence MultiEvidence(
            string shaderName = BaseContainerName,
            string assetGuid = BaseContainerGuid,
            bool hasVersion = true,
            float version = 45f,
            bool hasPackage = true,
            string packageName = "jp.lilxyzw.liltoon",
            string packageVersion = "2.3.4",
            string shaderDigest = UsePin,
            string includeDigest = UsePin,
            bool hasShaderCanonicalization = true,
            LilToonCanonicalizationAnalysis shaderCanonicalization = null)
        {
            return new LilToonSourceEvidence(
                shaderName,
                assetGuid,
                hasVersion,
                version,
                hasPackage,
                packageName,
                packageVersion,
                null,
                ReferenceEquals(shaderDigest, UsePin)
                    ? BaseContainerCanonicalDigest
                    : shaderDigest,
                null,
                ReferenceEquals(includeDigest, UsePin)
                    ? LilToonSourceAttestation.IncludeTreeDigest
                    : includeDigest,
                false,
                0,
                new string[0],
                hasShaderCanonicalization
                    ? shaderCanonicalization ??
                      AnalyzeMulti(MultiContainerSource(DefaultSettingsBlock))
                    : null,
                null);
        }

        /// <summary>
        /// Runs the Multi verify conjunction once against one injected
        /// profile row carrying the same synthetic identity values the
        /// other fixtures use.
        /// </summary>
        private static bool VerifyAgainstBaseContainerProfile(
            LilToonSourceEvidence evidence,
            out LilToonMultiResolutionRefusal refusal)
        {
            var profile = new LilToonMultiContainerProfile(
                BaseContainerName,
                BaseContainerGuid,
                BaseContainerCanonicalDigest);
            return LilToonSourceAttestation.TryVerifyMultiContainer(
                evidence, profile, out refusal);
        }

        private static List<LilToonActivatorOccurrence> ActivatorOccurrences(
            LilToonCanonicalizationAnalysis analysis,
            string identifier)
        {
            var found = new List<LilToonActivatorOccurrence>();
            foreach (var occurrence in analysis.Activators)
            {
                if (string.Equals(
                        occurrence.Identifier, identifier,
                        StringComparison.Ordinal))
                {
                    found.Add(occurrence);
                }
            }

            return found;
        }

        private static bool HasTrimmedLine(string text, string expected)
        {
            foreach (var line in text.Split('\n'))
            {
                if (string.Equals(
                        line.Trim(), expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTrimmedLineStartingWith(
            string text, string prefix)
        {
            foreach (var line in text.Split('\n'))
            {
                if (line.TrimStart().StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The control for the Multi verify falsifiers. Evidence that matches
        /// the injected profile row on every conjunction term admits, with no
        /// LIL_RENDER scan and no pass asset. The plausible wrong
        /// implementation reuses the regular profile path. That path demands
        /// a render mode and a pass asset, so it refuses this control.
        /// </summary>
        [Test]
        public void MultiContainerVerifyWithMatchingProfileIsAccepted()
        {
            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(), out var refusal),
                Is.True);
        }

        // --- Falsifier: a container verify that skips the asset-GUID term admits a stand-in asset. ---
        [Test]
        public void MultiContainerVerifyRefusesWrongAssetGuid()
        {
            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(assetGuid: new string('0', 32)),
                    out var refusal),
                Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));
        }

        // --- Falsifier 10 (spec): an attestation that admits a container by the regular include tree alone fails the wrong-version fixture. ---
        [Test]
        public void MultiContainerVerifyRefusesPriorVersionStamp()
        {
            // The 2.3.3 install presents the include tree that 2.3.3 ships,
            // so the version row is the only mismatching term. 2.3.4 is the
            // only admitted Multi version row.
            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(
                        packageVersion: "2.3.3",
                        includeDigest: PriorVersionIncludeDigest),
                    out var versionRefusal),
                Is.False,
                "2.3.4 is the only admitted Multi version row");
            Assert.That(
                versionRefusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));

            // The _lilToonVersion stamp moves with the package version, and
            // 2.3.3 stamps 44. The stamp check is exact.
            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(version: 44f), out var stampRefusal),
                Is.False,
                "the pinned _lilToonVersion stamp is exact");
            Assert.That(
                stampRefusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));
        }

        // --- Falsifier: a container verify that trusts the container name and skips the canonical digest admits edited source. ---
        [Test]
        public void MultiContainerVerifyRefusesMutatedCanonicalDigest()
        {
            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(shaderDigest: new string('0', 64)),
                    out var refusal),
                Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));
        }

        /// <summary>
        /// Profile-leakage guards in both directions. Falsifies a verify that
        /// widened a shared conjunction instead of adding an exact identity.
        /// The regular opaque verify must not accept a Multi container
        /// stand-in, and the Multi verify must not accept the regular opaque
        /// identity.
        /// </summary>
        // --- Falsifier: a verify that reuses one conjunction across profiles admits the other profile's container. ---
        [Test]
        public void MultiAndRegularVerifiersRejectEachOthersContainers()
        {
            Assert.That(
                LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    MultiEvidence(), out var regularDiagnostic),
                Is.False);
            Assert.That(
                regularDiagnostic.Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));

            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(
                        shaderName:
                            LilToonSourceAttestation.SupportedShaderName,
                        assetGuid:
                            LilToonSourceAttestation.SupportedShaderGuid,
                        shaderDigest:
                            LilToonSourceAttestation.ShaderCanonicalDigest),
                    out var multiRefusal),
                Is.False);
            Assert.That(
                multiRefusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));
        }

        /// <summary>
        /// Controller ruling, admitted slot: inside the LIL_MULTI-gated
        /// SubShader block a valueless LIL_FEATURE_* define is generator
        /// vocabulary. The canonicalizer removes it, its activator occurrence
        /// carries slot provenance, and the verify admits the source. An
        /// implementation that keeps the pre-F9 maximal-run discipline inside
        /// the gated block retains the define and moves the digest.
        /// Characterization on the pre-change code: the F9 region walk
        /// already admits this slot.
        /// </summary>
        // --- Ruling pin A: a canonicalizer that refuses or retains the valueless define inside the gated block fails this fixture. ---
        [Test]
        public void GatedBlockValuelessFeatureDefineIsRemovedAndProven()
        {
            var source = MultiContainerSource(
                DefaultSettingsBlock +
                "            #define LIL_FEATURE_VRCLIGHTVOLUMES\n");
            var analysis = AnalyzeMulti(source);

            Assert.That(
                analysis.CanonicalSource,
                Does.Not.Contain("#define LIL_FEATURE_VRCLIGHTVOLUMES"),
                "the gated block is generator vocabulary, so the define " +
                "leaves the canonical text");

            var occurrences =
                ActivatorOccurrences(analysis, "LIL_FEATURE_VRCLIGHTVOLUMES");
            Assert.That(
                occurrences.Count, Is.GreaterThan(0),
                "the activator scan still records the occurrence");
            foreach (var occurrence in occurrences)
            {
                Assert.That(
                    occurrence.ProvenSlot, Is.True,
                    "an in-block occurrence sits in the verified setting " +
                    "region, so no activator refusal fires for it");
            }

            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(shaderCanonicalization: analysis),
                    out var refusal),
                Is.True,
                "the verified slot must not refuse through the verify " +
                "conjunction");
        }

        /// <summary>
        /// Controller ruling, pre-F9 behavior kept: the same valueless define
        /// outside any LIL_MULTI-gated block stays in the canonical text, so
        /// it keeps moving the digest, and its activator occurrence carries
        /// no slot provenance, so the existing activator rules refuse it.
        /// </summary>
        // --- Ruling pin A: a canonicalizer that removes or forgives the valueless define outside the gated block fails this fixture. ---
        [Test]
        public void UngatedValuelessFeatureDefineStaysHashedAndUnproven()
        {
            var plain =
                "HLSLINCLUDE\n" +
                "    #define LIL_RENDER 0\n" +
                "ENDHLSL\n" +
                "HLSLINCLUDE\n" +
                "    #pragma target 3.5\n" +
                "ENDHLSL\n";
            var tampered = plain.Replace(
                "    #pragma target 3.5\n",
                "    #pragma target 3.5\n" +
                "#define LIL_FEATURE_VRCLIGHTVOLUMES\n");

            var analysis = AnalyzeMulti(tampered);

            Assert.That(
                analysis.CanonicalSource,
                Does.Contain("#define LIL_FEATURE_VRCLIGHTVOLUMES"),
                "outside the gated block the define stays hashed");
            Assert.That(
                LilToonSourceAttestation.ComputeNormalizedSourceHash(
                    analysis.CanonicalSource),
                Is.Not.EqualTo(
                    LilToonSourceAttestation.ComputeNormalizedSourceHash(
                        AnalyzeMulti(plain).CanonicalSource)),
                "the retained define must move the canonical digest");

            var occurrences =
                ActivatorOccurrences(analysis, "LIL_FEATURE_VRCLIGHTVOLUMES");
            Assert.That(
                occurrences.Count, Is.EqualTo(1),
                "the activator scan records exactly one occurrence");
            Assert.That(
                occurrences[0].ProvenSlot, Is.False,
                "an occurrence outside the verified setting region is a " +
                "tamper signal, and the existing rules refuse it");
        }

        /// <summary>
        /// Controller ruling, retention: canonicalization of a generated Multi
        /// container keeps the container defines in the canonical text. An
        /// implementation that strips LIL_MULTI or the LIL_MULTI_INPUTS_*
        /// declarations from the canonical text fails.
        /// </summary>
        // --- Ruling pin B: a canonicalizer that strips LIL_MULTI or LIL_MULTI_INPUTS_* fails this fixture. ---
        [Test]
        public void MultiContainerCanonicalizationRetainsMultiDefines()
        {
            var canonical =
                AttestationEnvironment.Canon(
                    MultiContainerSource(DefaultSettingsBlock));

            Assert.That(
                HasTrimmedLine(canonical, "#define LIL_MULTI"),
                Is.True,
                "the canonical text must retain the container define");
            Assert.That(
                HasTrimmedLineStartingWith(
                    canonical, "#define LIL_MULTI_INPUTS_"),
                Is.True,
                "the canonical text must retain at least one input set " +
                "declaration");
        }
    }
}
