using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using Alrauna.Amuse.Tests.Editor.Shared;
using UnityEngine;

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
    /// internal profile-injection seam. The production rows resolve by
    /// container name and asset GUID and take their per-version digests from
    /// the admitted Multi table; the fixtures inject one test-only profile
    /// row instead. The installed-container ground check at the bottom runs
    /// the production resolve and gather against the real package.
    /// </para>
    /// </summary>
    public sealed class LilToonSourceAttestationTests
    {

        // --- Characterization: the shared normalize rule on the public seam. ---

        /// <summary>
        /// Characterization, not a behavior change. The assertions pass
        /// before and after the Task 10 extraction. They pin the BOM drop
        /// and the newline fold on the public seam, so no later edit of the
        /// private rule can silently move the digests.
        /// </summary>
        [Test]
        public void NormalizeTextMatchesTheAttestationRule()
        {
            Assert.That(
                NormalizedSourceHash.NormalizeText("﻿a\r\nb\rc"),
                Is.EqualTo("a\nb\nc"));
            Assert.That(
                NormalizedSourceHash.Compute("a\r\nb"),
                Is.EqualTo(
                    NormalizedSourceHash.Compute("a\nb")));
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

        // Test-only profile row for the base container. The production
        // Multi rows resolve by container name and asset GUID and take
        // their per-version digests from AdmittedMultiVersions. The GUID
        // and digest below are synthetic test vocabulary, never measured
        // pins; no fixture value moves there.
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
        // term. The newest admitted pairing lives in the attestation's
        // IncludeTreeDigest const, which the fixtures reach through the
        // UsePin sentinel.
        private const string PriorVersionIncludeDigest =
            "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416";

        // The 2.3.4 Multi row literals (measured 2026-10-01 from installed
        // 2.3.4 shapes) and the 2.4.0 outline digest (measured 2026-10-19
        // per the two-shape rule). The 2.4.0 base digest equals the 2.3.4
        // base pin. The audit test pins both rows independently of these
        // fixtures, so a transcription error here fails there too.
        private const string Multi234IncludeTree =
            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46";
        private const string Multi234BaseDigest =
            "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f";
        private const string Multi234OutlineDigest =
            "225af2c6eba7a017593ca8a0f38114a98580ed67844a4d8465fed2c96f7cb75f";
        private const string Multi240OutlineDigest =
            "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85";

        // The 2.4.0 include-tree pin, computed from the official 2.4.0 tag
        // tree and reproduced on installed 2.4.0 shapes on 2026-10-19. It
        // anchors the 2.4.1 row's tag-tree license (spec B1).
        private const string Multi240IncludeTree =
            "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670";

        // The installed containers' pinned asset GUIDs, the identity half of
        // the production rows. The ground check resolves the live assets by
        // GUID, because Shader.Find matches any shader carrying the
        // container name, including test stand-ins.
        private const string InstalledBaseContainerGuid =
            "9294844b15dca184d914a632279b24e1";
        private const string InstalledOutlineContainerGuid =
            "51b2dee0ab07bd84d8147601ff89e511";

        // Test-only profile row for the outline wrapper, shaped exactly like
        // the base container's: the real container name with synthetic GUID
        // and digest vocabulary. The production per-version digest rows in
        // AdmittedMultiVersions own the real outline digests, and no fixture
        // value moves there.
        private const string OutlineContainerName = "Hidden/lilToonMultiOutline";
        private const string OutlineContainerGuid =
            "0f19e2a4b7c8d6051324a5b6c7d8e9f1";
        private const string OutlineContainerCanonicalDigest =
            "1029384756afbccddeeff0123456789a" +
            "fedcba98765432100123456789abcdee";

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
            string packageVersion = LilToonSourceAttestation.PackageVersion,
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
            // The 2.3.3 install presents the include tree that 2.3.3 ships.
            // 2.3.3 names no admitted Multi row, so no conjunction term of
            // an admitted row matches: 2.3.4 and 2.4.0 are the admitted
            // Multi version rows.
            Assert.That(
                VerifyAgainstBaseContainerProfile(
                    MultiEvidence(
                        packageVersion: "2.3.3",
                        includeDigest: PriorVersionIncludeDigest),
                    out var versionRefusal),
                Is.False,
                "2.3.4 and 2.4.0 are the admitted Multi version rows");
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
        /// Runs the Multi verify conjunction once against the injected
        /// outline wrapper row, the way
        /// <see cref="VerifyAgainstBaseContainerProfile"/> runs it against
        /// the base container's row.
        /// </summary>
        private static bool VerifyAgainstOutlineContainerProfile(
            LilToonSourceEvidence evidence,
            out LilToonMultiResolutionRefusal refusal)
        {
            var profile = new LilToonMultiContainerProfile(
                OutlineContainerName,
                OutlineContainerGuid,
                OutlineContainerCanonicalDigest);
            return LilToonSourceAttestation.TryVerifyMultiContainer(
                evidence, profile, out refusal);
        }

        // --- Falsifier: a container verify that pairs one version's label with another version's digest admits a cross-combination. ---
        [Test]
        public void MultiContainer_VersionAndOutlineDigestFromDifferentVersions_Refuse()
        {
            // Evidence A: the pre-flip newest admitted version presents the
            // outline wrapper, but the outline digest slot carries an
            // impossible value. The digest term refuses.
            Assert.That(
                VerifyAgainstOutlineContainerProfile(
                    MultiEvidence(
                        shaderName: OutlineContainerName,
                        assetGuid: OutlineContainerGuid,
                        shaderDigest: new string('0', 64)),
                    out var digestRefusal),
                Is.False,
                "the outline digest slot does not accept a foreign value");
            Assert.That(
                digestRefusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));

            // Evidence B: the fully correct outline digest under an
            // unadmitted version label. The version term refuses before any
            // digest work, and the digest slot carries the matching row
            // value so the version term is the only mismatching one.
            Assert.That(
                VerifyAgainstOutlineContainerProfile(
                    MultiEvidence(
                        shaderName: OutlineContainerName,
                        assetGuid: OutlineContainerGuid,
                        packageVersion: "2.3.5",
                        shaderDigest: OutlineContainerCanonicalDigest),
                    out var versionRefusal),
                Is.False,
                "an unadmitted version label refuses outright");
            Assert.That(
                versionRefusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.AttestationFailed));
        }

        // --- (A2) sharp form: the correct per-version pairing verifies, and every swapped pairing refuses. The rows below are the pinned
        // --- literals; the audit test pins both rows independently of these fixtures. ---

        /// <summary>
        /// Runs the Multi verify conjunction against one row built from the
        /// caller's pairing, the way the production resolver hands the
        /// identified version's row digest to the verify. The row digest and
        /// the evidence digest are separate on purpose: a swapped pairing
        /// presents one version's label and tree with the other version's
        /// container digest.
        /// </summary>
        private static bool VerifyPairing(
            string shaderName,
            string assetGuid,
            string packageVersion,
            string includeDigest,
            string rowDigest,
            string evidenceDigest)
        {
            var profile = new LilToonMultiContainerProfile(
                shaderName, assetGuid, rowDigest);
            var evidence = MultiEvidence(
                shaderName: shaderName,
                assetGuid: assetGuid,
                packageVersion: packageVersion,
                shaderDigest: evidenceDigest,
                includeDigest: includeDigest);
            return LilToonSourceAttestation.TryVerifyMultiContainer(
                evidence, profile, out _);
        }

        [TestCase(
            "2.3.4", Multi234IncludeTree, Multi234OutlineDigest,
            OutlineContainerName, OutlineContainerGuid)]
        [TestCase(
            LilToonSourceAttestation.PackageVersion,
            LilToonSourceAttestation.IncludeTreeDigest, Multi240OutlineDigest,
            OutlineContainerName, OutlineContainerGuid)]
        [TestCase(
            "2.3.4", Multi234IncludeTree, Multi234BaseDigest,
            BaseContainerName, BaseContainerGuid)]
        [TestCase(
            LilToonSourceAttestation.PackageVersion,
            LilToonSourceAttestation.IncludeTreeDigest, Multi234BaseDigest,
            BaseContainerName, BaseContainerGuid)]
        [TestCase(
            "2.3.10", Multi234IncludeTree, Multi234OutlineDigest,
            OutlineContainerName, OutlineContainerGuid)]
        [TestCase(
            "2.3.10", Multi234IncludeTree, Multi234BaseDigest,
            BaseContainerName, BaseContainerGuid)]
        public void MultiContainer_CorrectPerVersionPairing_Verifies(
            string packageVersion,
            string includeDigest,
            string digest,
            string shaderName,
            string assetGuid)
        {
            Assert.That(
                VerifyPairing(
                    shaderName, assetGuid, packageVersion,
                    includeDigest, digest, digest),
                Is.True,
                "the version's own tree and container digest attest");
        }

        [TestCase(
            LilToonSourceAttestation.PackageVersion,
            LilToonSourceAttestation.IncludeTreeDigest,
            Multi234OutlineDigest, Multi240OutlineDigest)]
        [TestCase(
            "2.3.4", Multi234IncludeTree,
            Multi240OutlineDigest, Multi234OutlineDigest)]
        [TestCase(
            "2.3.10", Multi234IncludeTree,
            Multi240OutlineDigest, Multi234OutlineDigest)]
        public void MultiContainer_SwappedVersionPairing_Refuses(
            string packageVersion,
            string includeDigest,
            string foreignDigest,
            string rowDigest)
        {
            Assert.That(
                foreignDigest, Is.Not.EqualTo(rowDigest),
                "fixture precondition: the swapped digest differs from " +
                "the row's own digest");

            // The outline wrapper presents the other version's outline
            // digest under this version's label and tree. The profile row
            // carries the presented version's own row digest, so only the
            // digest term mismatches.
            Assert.That(
                VerifyPairing(
                    OutlineContainerName, OutlineContainerGuid,
                    packageVersion, includeDigest, rowDigest, foreignDigest),
                Is.False,
                "a version label paired with the other version's outline " +
                "digest is a cross combination");
        }

        [Test]
        public void MultiContainer_PackagedPairing_FlowsThroughThePairedRow()
        {
            Assert.That(
                VerifyPairing(
                    BaseContainerName, BaseContainerGuid,
                    "2.4.1",
                    "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625",
                    Multi234BaseDigest, Multi234BaseDigest),
                Is.True, "the packaged 2.4.1 Multi install pairs the 2.4.0 row");
            Assert.That(
                VerifyPairing(
                    BaseContainerName, BaseContainerGuid,
                    "2.4.1", Multi240IncludeTree,
                    Multi234BaseDigest, Multi234BaseDigest),
                Is.False, "a 2.4.1 label over a foreign tree refuses");
        }

        // --- (A2) loose installs: no version label, so the tree identifies the shipped version and the digest compares against that row's column. ---

        [TestCase(Multi234IncludeTree, Multi234BaseDigest, ExpectedResult = true)]
        [TestCase(Multi240IncludeTree, Multi234BaseDigest, ExpectedResult = true)]
        [TestCase(Multi234IncludeTree, Multi240OutlineDigest, ExpectedResult = false)]
        [TestCase(Multi240IncludeTree, Multi234OutlineDigest, ExpectedResult = false)]
        [TestCase(
            LilToonSourceAttestation.IncludeTreeDigest, Multi234BaseDigest,
            ExpectedResult = false)]
        [TestCase(
            LilToonSourceAttestation.IncludeTreeDigest, Multi234OutlineDigest,
            ExpectedResult = false)]
        public bool MultiContainer_LooseInstall_TreeIdentifiesTheRow(
            string includeDigest, string shaderDigest)
        {
            // The injected profile row carries synthetic vocabulary; the
            // loose branch reads the row the tree identifies, not the
            // injected digest, so a synthetic profile digest proves the
            // column lookup is the binding term.
            var profile = new LilToonMultiContainerProfile(
                BaseContainerName, BaseContainerGuid,
                BaseContainerCanonicalDigest);
            var evidence = MultiEvidence(
                hasPackage: false,
                packageName: null,
                packageVersion: null,
                shaderDigest: shaderDigest,
                includeDigest: includeDigest);
            return LilToonSourceAttestation.TryVerifyMultiContainer(
                evidence, profile, out _);
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

        // --- (A2) installed-bytes ground: the production resolve, gather,
        // --- and verify run against the real installed containers. Every
        // --- other Multi vehicle is synthetic, so the rows would be
        // --- self-consistent even if wrong without this check. ---

        /// <summary>
        /// The one real-gather Multi check. The production
        /// <see cref="LilToonSourceAttestation.TryResolveMultiContainer"/>
        /// resolves each installed container through the production gather,
        /// the row the identified version selects verifies the gathered
        /// bytes, and the other column's digest refuses the same evidence.
        /// </summary>
        [Test]
        public void InstalledMultiContainers_ResolveVerifyAndRefuseASwappedDigest()
        {
            // By pinned GUID, not by name: Shader.Find matches any shader
            // carrying the container name, including test stand-ins.
            var baseShader = LoadInstalledMultiContainer(
                InstalledBaseContainerGuid, BaseContainerName);
            var outlineShader = LoadInstalledMultiContainer(
                InstalledOutlineContainerGuid, OutlineContainerName);

            var multiRows = LilToonSourceAttestation.AdmittedMultiRows();

            var baseMaterial = new Material(baseShader);
            var outlineMaterial = new Material(outlineShader);
            try
            {
                var baseCaptured = UnityMaterialEvidenceCapture.Capture(
                    new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            baseMaterial,
                            LilToonMultiResolution.MultiEvidenceRequest),
                    })[0];
                var outlineCaptured = UnityMaterialEvidenceCapture.Capture(
                    new[]
                    {
                        new MaterialEvidenceCaptureInput(
                            outlineMaterial,
                            LilToonMultiResolution.MultiEvidenceRequest),
                    })[0];

                Assert.That(
                    LilToonSourceAttestation.TryResolveMultiContainer(
                        baseShader, baseCaptured,
                        out var baseEvidence, out var baseProfile,
                        out var baseResolveRefusal),
                    Is.True);
                Assert.That(
                    LilToonSourceAttestation.TryVerifyMultiContainer(
                        baseEvidence, baseProfile, out _),
                    Is.True,
                    "the installed base container attests as the version " +
                    "it identifies");

                Assert.That(
                    LilToonSourceAttestation.TryResolveMultiContainer(
                        outlineShader, outlineCaptured,
                        out var outlineEvidence, out var outlineProfile,
                        out var outlineResolveRefusal),
                    Is.True);
                Assert.That(
                    LilToonSourceAttestation.TryVerifyMultiContainer(
                        outlineEvidence, outlineProfile, out _),
                    Is.True,
                    "the installed outline container attests as the " +
                    "version it identifies");

                // Swapped digest, same container: the other column's row
                // digest must not attest. The version and tree terms stay
                // correct, so the digest term is the one that fires.
                var baseRow = FindMultiRow(multiRows, baseEvidence);
                var swappedBase = SwapMultiDigest(
                    baseEvidence, baseRow.MultiOutlineDigest);
                Assert.That(
                    baseRow.MultiOutlineDigest,
                    Is.Not.EqualTo(baseEvidence.ShaderCanonicalDigest),
                    "fixture precondition: the outline column digest " +
                    "differs from the gathered base digest");
                Assert.That(
                    LilToonSourceAttestation.TryVerifyMultiContainer(
                        swappedBase, baseProfile, out _),
                    Is.False,
                    "the base container refuses the outline column's digest");

                var otherRow = FindOtherMultiRow(multiRows, outlineEvidence);
                var swappedOutline = SwapMultiDigest(
                    outlineEvidence, otherRow.MultiOutlineDigest);
                Assert.That(
                    otherRow.MultiOutlineDigest,
                    Is.Not.EqualTo(outlineEvidence.ShaderCanonicalDigest),
                    "fixture precondition: the other version's outline " +
                    "digest differs from the gathered outline digest");
                Assert.That(
                    LilToonSourceAttestation.TryVerifyMultiContainer(
                        swappedOutline, outlineProfile, out _),
                    Is.False,
                    "the outline container refuses the other version's " +
                    "outline digest");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseMaterial);
                UnityEngine.Object.DestroyImmediate(outlineMaterial);
            }
        }

        private static Shader LoadInstalledMultiContainer(
            string assetGuid, string containerName)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid);
            Assert.That(path, Is.Not.Empty,
                "fixture precondition: the installed " + containerName +
                " asset must resolve by its pinned GUID");
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                path);
            Assert.That(shader, Is.Not.Null,
                "fixture precondition: the installed " + containerName +
                " asset must load");
            return shader;
        }

        private static (string Version, string MultiDigest, string MultiOutlineDigest)
            FindMultiRow(
                IReadOnlyList<(string Version, string MultiDigest, string MultiOutlineDigest)> rows,
                LilToonSourceEvidence evidence)
        {
            foreach (var row in rows)
            {
                if (string.Equals(
                        row.Version, evidence.PackageVersion,
                        StringComparison.Ordinal))
                {
                    return row;
                }
            }

            Assert.Fail(
                "fixture precondition: the gathered evidence must carry " +
                "an admitted Multi version");
            return default;
        }

        private static (string Version, string MultiDigest, string MultiOutlineDigest)
            FindOtherMultiRow(
                IReadOnlyList<(string Version, string MultiDigest, string MultiOutlineDigest)> rows,
                LilToonSourceEvidence evidence)
        {
            foreach (var row in rows)
            {
                if (!string.Equals(
                        row.Version, evidence.PackageVersion,
                        StringComparison.Ordinal))
                {
                    return row;
                }
            }

            Assert.Fail(
                "fixture precondition: a second Multi version row must " +
                "exist");
            return default;
        }

        private static LilToonSourceEvidence SwapMultiDigest(
            LilToonSourceEvidence evidence, string swappedDigest)
        {
            return new LilToonSourceEvidence(
                evidence.ShaderName,
                evidence.AssetGuid,
                evidence.HasShaderFormatVersion,
                evidence.ShaderFormatVersion,
                evidence.HasPackage,
                evidence.PackageName,
                evidence.PackageVersion,
                evidence.PassShaderGuid,
                swappedDigest,
                evidence.PassCanonicalDigest,
                evidence.IncludeTreeDigest,
                evidence.HasRenderMode,
                evidence.RenderMode,
                evidence.CompiledFeatures,
                evidence.ShaderCanonicalization,
                evidence.PassCanonicalization);
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

        /// <summary>
        /// A shader name that matches no pinned profile refuses before any
        /// gather. The refusal carries the unsupported-shader code. The old
        /// lookup defaulted an unmatched name to the opaque profile and
        /// gathered anyway.
        /// </summary>
        [Test]
        public void TryGatherSourceEvidenceForShaderName_UnknownShaderName_ReturnsFalseWithNamedRefusal()
        {
            // The built-in shader's name matches no pinned profile. The
            // capture follows the LilToonOpaqueTargetTests shape under the
            // standard cutout request.
            var shader = Shader.Find("Unlit/Color");
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: Unlit/Color must exist");
            var source = new Material(shader);
            try
            {
                var captured = UnityMaterialEvidenceCapture.Capture(new[]
                {
                    new MaterialEvidenceCaptureInput(
                        source,
                        LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                })[0];

                var matched = LilToonSourceAttestation
                    .TryGatherSourceEvidenceForShaderName(
                        source.shader, captured,
                        out var evidence, out var refusal);

                Assert.That(matched, Is.False);
                Assert.That(evidence, Is.Null);
                Assert.That(refusal, Is.Not.Null);
                Assert.That(
                    refusal.Code,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
    }
}
