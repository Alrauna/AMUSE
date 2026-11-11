using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Real-source ground checks over the installed vendor packages.
    /// Every shader loads by its pinned asset GUID, because a name lookup
    /// can answer with a stand-in or a foreign shader that shares the
    /// name. Each family also records what the name lookup answers on
    /// this install, so a change in the name map surfaces here. A
    /// first-run pass is characterization of the pinned environment. A
    /// failure is a dated drift finding. It never justifies weakening a
    /// test.
    /// </summary>
    public sealed class RealSourceGroundCheckTests
    {
        // The installed Multi containers' pinned asset GUIDs. They repeat
        // the sibling fixture pins in LilToonSourceAttestationTests, both
        // measured from the installed .meta files. The GUID load is the
        // only reliable resolve, because Shader.Find matches any shader
        // that carries a container name, including the test stand-ins
        // under Tests.
        private const string InstalledBaseContainerGuid =
            "9294844b15dca184d914a632279b24e1";
        private const string InstalledOutlineContainerGuid =
            "51b2dee0ab07bd84d8147601ff89e511";

        // The installed outline opaque target's pinned asset GUID,
        // measured from the installed lts_o.shader .meta file. It follows
        // the installed container constant precedent. Production carries
        // the same value in LilToonSourceAttestation.OutlineShaderGuid,
        // and the verify conjunction below compares against that pin.
        private const string InstalledOpaqueTargetGuid =
            "efa77a80ca0344749b4f19fdd5891cbe";

        [Test]
        public void RealLilToonMainAndPassShadersAttestThroughProduction()
        {
            var shader = LoadByPinnedGuid(
                LilToonSourceAttestation.SupportedShaderGuid,
                LilToonSourceAttestation.SupportedShaderName);
            var passShader = LoadByPinnedGuid(
                LilToonSourceAttestation.PassShaderGuid,
                LilToonSourceAttestation.PassShaderName);
            AssertNameLookupResolvesTheSameObject(
                shader, LilToonSourceAttestation.SupportedShaderName);
            AssertNameLookupResolvesTheSameObject(
                passShader, LilToonSourceAttestation.PassShaderName);

            var material = new Material(shader);
            try
            {
                var captured = CaptureOne(
                    material, LilToonMaterialSemantics.AlphaEvidenceRequest);
                var evidence = LilToonSourceAttestation.GatherSourceEvidence(
                    shader, captured);
                Assert.That(
                    evidence.AssetGuid,
                    Is.EqualTo(LilToonSourceAttestation.SupportedShaderGuid),
                    "the verified evidence must carry the shader the " +
                    "pinned GUID loaded");
                Assert.That(
                    evidence.PassShaderGuid,
                    Is.EqualTo(LilToonSourceAttestation.PassShaderGuid),
                    "the verified pass half must resolve the pinned pass " +
                    "asset the GUID load returned");
                Assert.That(
                    LilToonSourceAttestation.TryVerifyLilToonIdentity(
                        evidence, out var diagnostic),
                    Is.True,
                    "the installed lilToon main and pass shaders attest " +
                    "through production: " + diagnostic?.Detail);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RealLilToonCutoutShadersAttestThroughProduction()
        {
            var shader = LoadByPinnedGuid(
                LilToonSourceAttestation.CutoutShaderGuid,
                LilToonSourceAttestation.CutoutShaderName);
            var passShader = LoadByPinnedGuid(
                LilToonSourceAttestation.CutoutPassShaderGuid,
                LilToonSourceAttestation.CutoutPassShaderName);
            AssertNameLookupResolvesTheSameObject(
                shader, LilToonSourceAttestation.CutoutShaderName);
            AssertNameLookupResolvesTheSameObject(
                passShader, LilToonSourceAttestation.CutoutPassShaderName);

            var material = new Material(shader);
            try
            {
                var captured = CaptureOne(
                    material,
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest);
                var evidence = LilToonSourceAttestation
                    .GatherCutoutSourceEvidence(shader, captured);
                Assert.That(
                    evidence.AssetGuid,
                    Is.EqualTo(LilToonSourceAttestation.CutoutShaderGuid),
                    "the verified evidence must carry the shader the " +
                    "pinned GUID loaded");
                Assert.That(
                    evidence.PassShaderGuid,
                    Is.EqualTo(LilToonSourceAttestation.CutoutPassShaderGuid),
                    "the verified pass half must resolve the pinned pass " +
                    "asset the GUID load returned");
                Assert.That(
                    LilToonSourceAttestation.TryVerifyLilToonCutoutIdentity(
                        evidence, out var diagnostic),
                    Is.True,
                    "the installed lilToon cutout shaders attest through " +
                    "production: " + diagnostic?.Detail);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RealLilToonTransparentShadersAttestThroughProduction()
        {
            var shader = LoadByPinnedGuid(
                LilToonSourceAttestation.TransparentShaderGuid,
                LilToonSourceAttestation.TransparentShaderName);
            var passShader = LoadByPinnedGuid(
                LilToonSourceAttestation.TransparentPassShaderGuid,
                LilToonSourceAttestation.TransparentPassShaderName);
            AssertNameLookupResolvesTheSameObject(
                shader, LilToonSourceAttestation.TransparentShaderName);
            AssertNameLookupResolvesTheSameObject(
                passShader,
                LilToonSourceAttestation.TransparentPassShaderName);

            var material = new Material(shader);
            try
            {
                var captured = CaptureOne(
                    material,
                    LilToonTransparentMaterialSemantics.AlphaEvidenceRequest);
                var evidence = LilToonSourceAttestation
                    .GatherTransparentSourceEvidence(shader, captured);
                Assert.That(
                    evidence.AssetGuid,
                    Is.EqualTo(
                        LilToonSourceAttestation.TransparentShaderGuid),
                    "the verified evidence must carry the shader the " +
                    "pinned GUID loaded");
                Assert.That(
                    evidence.PassShaderGuid,
                    Is.EqualTo(
                        LilToonSourceAttestation.TransparentPassShaderGuid),
                    "the verified pass half must resolve the pinned pass " +
                    "asset the GUID load returned");
                Assert.That(
                    LilToonSourceAttestation
                        .TryVerifyLilToonTransparentIdentity(
                            evidence, out var diagnostic),
                    Is.True,
                    "the installed lilToon transparent shaders attest " +
                    "through production: " + diagnostic?.Detail);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RealLilToonOpaqueTargetAttestsThroughProduction()
        {
            // Route an outline wrapper source the way the production
            // conversion wrapper routes it. The routing tripwire pins
            // the target before any attestation runs.
            var targetName = LilToonSourceAttestation
                .ResolveCanonicalTargetShaderName(
                    LilToonSourceAttestation.OutlineCutoutShaderName);
            Assert.That(
                targetName,
                Is.EqualTo(LilToonSourceAttestation.OutlineShaderName),
                "the canonical opaque routing moved off the outline " +
                "opaque target. Re-derive this ground check against the " +
                "new target before trusting a green result");

            var shader = LoadByPinnedGuid(
                InstalledOpaqueTargetGuid,
                LilToonSourceAttestation.OutlineShaderName);
            AssertNameLookupResolvesTheSameObject(shader, targetName);

            var material = new Material(shader);
            try
            {
                var captured = CaptureOne(
                    material, LilToonMaterialSemantics.AlphaEvidenceRequest);
                Assert.That(
                    LilToonSourceAttestation
                        .TryGatherSourceEvidenceForShaderName(
                            shader, captured, out var evidence,
                            out var refusal),
                    Is.True,
                    "the outline opaque target must gather: " +
                    refusal?.Detail);
                Assert.That(
                    evidence.AssetGuid,
                    Is.EqualTo(LilToonSourceAttestation.OutlineShaderGuid),
                    "the verified evidence must carry the production pin " +
                    "of the outline opaque target");
                Assert.That(
                    LilToonSourceAttestation.TryVerifyIdentityForShaderName(
                        evidence, out var diagnostic),
                    Is.True,
                    "the installed outline opaque target attests through " +
                    "production: " + diagnostic?.Detail);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void RealLilToonMultiContainersAttestThroughProduction()
        {
            var baseShader = LoadByPinnedGuid(
                InstalledBaseContainerGuid, "lilToonMulti base container");
            var outlineShader = LoadByPinnedGuid(
                InstalledOutlineContainerGuid,
                "lilToonMulti outline container");
            AssertNameLookupResolvesTheSameObject(
                baseShader, baseShader.name);
            AssertNameLookupResolvesTheSameObject(
                outlineShader, outlineShader.name);

            var baseMaterial = new Material(baseShader);
            var outlineMaterial = new Material(outlineShader);
            try
            {
                AssertContainerResolvesAndVerifies(
                    baseShader, baseMaterial, InstalledBaseContainerGuid);
                AssertContainerResolvesAndVerifies(
                    outlineShader, outlineMaterial,
                    InstalledOutlineContainerGuid);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseMaterial);
                UnityEngine.Object.DestroyImmediate(outlineMaterial);
            }
        }

        // --- Falsifier 10: a ground check that resolves the shader by release name loads the wrong bytes on a 10.x install. ---
        [Test]
        public void NameLookupCanDisagreeWithThePinnedGuidLoad()
        {
            // No-op guard: both loads must resolve before the
            // disagreement claim means anything.
            var guidShader = LoadByPinnedGuid(
                PoiyomiSourceAttestation.CanonicalShaderGuid,
                "bundled Poiyomi Toon shader");
            var nameShader = Shader.Find(
                PoiyomiSourceAttestation.PoiyomiToonShaderName);
            Assert.That(
                nameShader, Is.Not.Null,
                "fixture precondition: the release name must resolve a " +
                "shader on this install");

            Assert.That(
                nameShader, Is.Not.SameAs(guidShader),
                "the release name must resolve the Poiyomi 10.x root " +
                "shader, while the pinned GUID loads the bundled 9.3 " +
                "asset. The two loads disagree on this install, so a " +
                "ground check that resolves by name attests the wrong " +
                "bytes");
        }

        [Test]
        public void RealPoiyomiBundledShadersAttestThroughProduction()
        {
            var mainShader = LoadByPinnedGuid(
                PoiyomiSourceAttestation.CanonicalShaderGuid,
                "bundled Poiyomi Toon shader");
            var twoPassShader = LoadByPinnedGuid(
                PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                "bundled Poiyomi Toon Two Pass shader");
            Assert.That(
                PoiyomiSourceAttestation.TryClassifyShaderName(
                    mainShader.name, out _),
                Is.True,
                "the installed bundled main shader declares an admitted " +
                "name");
            Assert.That(
                PoiyomiSourceAttestation.TryClassifyShaderName(
                    twoPassShader.name, out _),
                Is.True,
                "the installed bundled two-pass shader declares an " +
                "admitted name");

            var mainMaterial = new Material(mainShader);
            var twoPassMaterial = new Material(twoPassShader);
            try
            {
                var mainEvidence = PoiyomiSourceAttestation
                    .GatherAlphaSourceEvidence(
                        mainShader,
                        CaptureOne(
                            mainMaterial,
                            PoiyomiMaterialSemantics.AlphaEvidenceRequest));
                Assert.That(
                    PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                        mainEvidence, out var mainDiagnostic),
                    Is.True,
                    "the installed bundled main shader attests through " +
                    "production: " + mainDiagnostic?.Detail);
                Assert.That(
                    mainEvidence.AssetGuid,
                    Is.EqualTo(PoiyomiSourceAttestation.CanonicalShaderGuid),
                    "the verified evidence must carry the shader the " +
                    "pinned GUID loaded");
                Assert.That(
                    PoiyomiSourceAttestation.IsLockedOriginalAdmittedName(
                        mainShader.name),
                    Is.True,
                    "the admitted row of the installed bundled main " +
                    "shader must admit locked originals");

                var twoPassEvidence = PoiyomiSourceAttestation
                    .GatherAlphaSourceEvidence(
                        twoPassShader,
                        CaptureOne(
                            twoPassMaterial,
                            PoiyomiMaterialSemantics
                                .TwoPassAlphaEvidenceRequest));
                Assert.That(
                    PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                        twoPassEvidence, out var twoPassDiagnostic),
                    Is.True,
                    "the installed bundled two-pass shader attests " +
                    "through production: " + twoPassDiagnostic?.Detail);
                Assert.That(
                    twoPassEvidence.AssetGuid,
                    Is.EqualTo(
                        PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid),
                    "the verified evidence must carry the shader the " +
                    "pinned GUID loaded");
                Assert.That(
                    PoiyomiSourceAttestation.IsLockedOriginalAdmittedName(
                        twoPassShader.name),
                    Is.True,
                    "the admitted row of the installed bundled two-pass " +
                    "shader must admit locked originals");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mainMaterial);
                UnityEngine.Object.DestroyImmediate(twoPassMaterial);
            }
        }

        [Test]
        public void RealPoiyomiTenMaterialAnswersUnattested()
        {
            var tenXShader = Shader.Find(
                PoiyomiSourceAttestation.PoiyomiToonShaderName);
            Assert.That(
                tenXShader, Is.Not.Null,
                "fixture precondition: the release name must resolve a " +
                "shader on this install");
            var bundledShader = LoadByPinnedGuid(
                PoiyomiSourceAttestation.CanonicalShaderGuid,
                "bundled Poiyomi Toon shader");
            Assert.That(
                tenXShader, Is.Not.SameAs(bundledShader),
                "fixture precondition: the release name must resolve the " +
                "10.x root shader, not the bundled 9.3 asset");

            var material = new Material(tenXShader);
            try
            {
                // Production selection admits the release name, so the
                // material joins the Poiyomi family. The refusal must
                // come from the source attestation, never from the name.
                Assert.That(
                    PoiyomiSourceAttestation.TryClassifyShaderName(
                        tenXShader.name, out _),
                    Is.True,
                    "the release name is an admitted Poiyomi name");

                var evidence = PoiyomiSourceAttestation
                    .GatherAlphaSourceEvidence(
                        tenXShader,
                        CaptureOne(
                            material,
                            PoiyomiMaterialSemantics.AlphaEvidenceRequest));
                Assert.That(
                    PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                        evidence, out var diagnostic),
                    Is.False,
                    "a real Poiyomi 10.x material must answer unattested");
                Assert.That(
                    diagnostic?.Code,
                    Is.EqualTo(
                        PoiyomiSemanticDiagnosticCode.MissingSourceEvidence),
                    "the 10.x shader fails the GUID gate of the admitted " +
                    "rows");
                Assert.That(
                    diagnostic?.Detail,
                    Does.Contain("GUID"),
                    "the refusal must name the GUID gate");

                var success = UnityMaterialSemantics
                    .TryCaptureClosedAlphaMaterials(
                        new[] { material },
                        new[] { CapturedAlphaMaterialFamily.Poiyomi },
                        PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                        AlphaPolicyBounds.Inert,
                        out var outcome,
                        RegisteredSourceIdentity.Resolve);
                Assert.That(success, Is.True);
                Assert.That(
                    outcome.Captured, Is.Empty,
                    "the production capture must not admit the 10.x " +
                    "material");
                Assert.That(
                    outcome.UnattestedOrdinals,
                    Is.EqualTo(new[] { 0 }),
                    "the production capture must name the 10.x material " +
                    "unattested");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void AssertContainerResolvesAndVerifies(
            Shader shader,
            Material material,
            string pinnedGuid)
        {
            var captured = CaptureOne(
                material, LilToonMultiResolution.MultiEvidenceRequest);
            Assert.That(
                LilToonSourceAttestation.TryResolveMultiContainer(
                    shader, captured, out var evidence,
                    out var profile, out var resolveRefusal),
                Is.True,
                "the installed container must resolve through the " +
                "production resolver: " + resolveRefusal);
            Assert.That(
                evidence.AssetGuid,
                Is.EqualTo(pinnedGuid),
                "the resolved evidence must carry the shader the pinned " +
                "GUID loaded");
            Assert.That(
                LilToonSourceAttestation.TryVerifyMultiContainer(
                    evidence, profile, out var verifyRefusal),
                Is.True,
                "the installed container attests as the version it " +
                "identifies: " + verifyRefusal);
        }

        private static Shader LoadByPinnedGuid(
            string assetGuid,
            string shaderLabel)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGuid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed " + shaderLabel +
                " asset must resolve by its pinned GUID");
            var shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(
                path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed " + shaderLabel +
                " asset must load");
            return shader;
        }

        private static void AssertNameLookupResolvesTheSameObject(
            Shader shader,
            string shaderName)
        {
            Assert.That(
                Shader.Find(shaderName),
                Is.SameAs(shader),
                "Shader.Find must resolve the same object as the pinned " +
                "GUID load for '" + shaderName + "'. A stand-in or a " +
                "foreign shader that shadows the name answers here. " +
                "Record the disagreement as a dated drift finding");
        }

        private static CapturedMaterialEvidence CaptureOne(
            Material material,
            MaterialEvidenceRequest request)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, request),
            })[0];
        }

    }
}
