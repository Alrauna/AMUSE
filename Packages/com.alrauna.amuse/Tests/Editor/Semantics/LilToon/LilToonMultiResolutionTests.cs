using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Falsifier fixtures for the Multi resolution routing: the follower
    /// that turns captured Multi evidence into one resolved regular family,
    /// or one named refusal from the closed Multi vocabulary.
    /// <para>
    /// The admit-path and mode-gate fixtures drive the resolver through the
    /// profile injection seam the Task 6 verify fixtures established: the
    /// production Multi row table stays empty until the Task 2 digest
    /// measurement, so the production entry point refuses every Multi
    /// container and the fixtures inject one test-only profile row plus the
    /// gathered identity evidence that matches it. The specialized-container
    /// fixture injects a fur-named row, so a specialized container's
    /// identity refusal is pinned against attestation with a row that
    /// would otherwise attest. Every test comment names the path it
    /// exercises and the phase that turns it green. Every input flows
    /// through the production capture path, so no fixture depends on
    /// stand-in shader defaults.
    /// </para>
    /// </summary>
    public sealed class LilToonMultiResolutionTests
    {
        // Synthetic container identity vocabulary, the same test-only values
        // the Task 6 verify fixtures use. They are never measured pins; Task
        // 2 fills the production table and no fixture value moves there.
        internal const string BaseContainerName = "_lil/lilToonMulti";
        private const string OutlineContainerName =
            "Hidden/lilToonMultiOutline";
        private const string SpecializedFurName = "Hidden/lilToonMultiFur";
        internal const string TestContainerGuid =
            "0f19e2a4b7c8d6051324a5b6c7d8e9f0";
        internal const string TestContainerCanonicalDigest =
            "1029384756afbccddeeff0123456789a" +
            "fedcba98765432100123456789abcdef";

        private const string TempFolder = "Assets/AmuseTests_MultiResolution";

        private readonly List<Material> _materials = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_MultiResolution");
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            _materials.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        // Mode 0 on the base container is the observed corpus state: the
        // empty keyword set the derivation produces, both gate scalars off,
        // and the vendor opaque queue form. The resolver must verify the
        // container identity, run the mode-consistency gate, and map the
        // mode to the regular LilToon family - the family every downstream
        // switch already answers for - never to a Multi-specific family.
        // Path: profile injection seam. Green when the resolver lands; no
        // Task 2 digests needed.
        // --- Falsifier: a resolver that invents a Multi-specific downstream family, or that refuses the all-off corpus state, fails this fixture. ---
        [Test]
        public void ModeZeroOnTheBaseContainerResolvesToTheLilToonFamily()
        {
            var resolved = ResolveThroughSeam(
                CapturedMultiEvidence(BaseContainerName),
                BaseContainerName,
                out var family,
                out var mode);

            Assert.That(resolved, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.That(mode, Is.EqualTo(0));
        }

        // Mode 0 on the outline container: the outline copy compiles the
        // same opaque theorem, so parity requires the same resolved family.
        // The outline name is live container identity here, and the outline
        // tone keyword row derives nothing from the empty captured set.
        // Path: profile injection seam. Green when the resolver lands.
        // --- Falsifier: a resolver that admits only the base container leaves half the parity set unsupported. ---
        [Test]
        public void ModeZeroOnTheOutlineContainerResolvesToTheLilToonFamily()
        {
            var resolved = ResolveThroughSeam(
                CapturedMultiEvidence(OutlineContainerName),
                OutlineContainerName,
                out var family,
                out var mode);

            Assert.That(resolved, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.That(mode, Is.EqualTo(0));
        }

        // _TransparentMode 3 is outside the vendor admitted set, which holds
        // the modes zero to two. The facts are otherwise admitting, so the
        // refusal value - not the attestation - is the single fault under
        // test, whatever internal order the resolver verifies in.
        // Path: profile injection seam. Green when the resolver lands.
        // --- Falsifier: a resolver that trusts the shader name alone admits a mode the vendor set refuses. ---
        [Test]
        public void TransparentModeOutsideTheAdmittedSetRefusesAsModeOutsideAdmittedSet()
        {
            var resolved = ResolveThroughSeam(
                CapturedMultiEvidence(BaseContainerName, transparentMode: 3f),
                BaseContainerName,
                out _,
                out _,
                out var refusal);

            Assert.That(resolved, Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal
                    .ModeOutsideAdmittedSet));
        }

        // The specialized containers refuse by container identity before
        // attestation matters - they carry no digest rows at all. The seam
        // runs with a fur-named profile the identity evidence matches by
        // construction, so the identity-versus-attestation order is the
        // single fault under test: a resolver that attests first would
        // admit this state instead of refusing it.
        // Path: profile injection seam, matched fur row. Green when the
        // resolver lands.
        // --- Falsifier: a resolver that attests before classifying container identity admits the matched fur row instead of refusing by identity. ---
        [Test]
        public void SpecializedFurContainerRefusesByIdentityBeforeAttestation()
        {
            var resolved = LilToonMultiResolution.Resolve(
                CapturedMultiEvidence(SpecializedFurName),
                MatchingSourceEvidence(SpecializedFurName),
                MatchingProfile(SpecializedFurName),
                keywordsRequested: true,
                out _,
                out _,
                out var refusal);

            Assert.That(resolved, Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal
                    .SpecializedContainer));
        }

        // An empty captured keyword set at mode one is a state the pinned
        // derivation cannot produce: the derivation writes the alpha clip
        // keyword at mode one, so the gate must demand it. The refusal the
        // resolver answers is the gate's own value, unchanged - the resolver
        // transports the mode-consistency verdict, never reclassifies it.
        // Path: profile injection seam. Green when the resolver lands.
        // --- Falsifier: a resolver that swallows the gate refusal into an attestation or keyword-evidence refusal fails this fixture. ---
        [Test]
        public void EmptyKeywordSetAtCutoutModeRefusesAsKeywordModeMismatch()
        {
            var resolved = ResolveThroughSeam(
                CapturedMultiEvidence(BaseContainerName, transparentMode: 1f),
                BaseContainerName,
                out _,
                out _,
                out var refusal);

            Assert.That(resolved, Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal
                    .KeywordModeMismatch));
        }

        // The routing invariant: the resolver must never read a missing
        // keyword capture as the empty consistent set the derivation
        // produces at mode zero. The evidence record cannot carry the
        // requested and not-requested fact apart - both capture as an empty
        // set - so the routing caller owns the fact, and the resolver's
        // precondition shape takes it as one bool. The same evidence that
        // admits with the fact true refuses with the fact false.
        // Path: profile injection seam; the keyword-request parameter is the
        // resolver precondition shape this fixture pins. Green when the
        // resolver lands.
        // --- Falsifier: a resolver that admits on an empty keyword set without the routing fact fails this fixture. ---
        [Test]
        public void MissingKeywordCaptureRefusesAsMissingKeywordEvidence()
        {
            var resolved = LilToonMultiResolution.Resolve(
                CapturedMultiEvidence(BaseContainerName),
                MatchingSourceEvidence(BaseContainerName),
                MatchingProfile(BaseContainerName),
                keywordsRequested: false,
                out _,
                out _,
                out var refusal);

            Assert.That(resolved, Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal
                    .MissingKeywordEvidence));
        }

        // The parity oracle: a mode-0 Multi material and the regular opaque
        // identity, captured under one shared request so their
        // alpha-relevant evidence is identical, must land on the same family
        // and on the same proven-opaque alpha answer from the regular
        // interpreter. The resolution half runs through the profile
        // injection seam; the interpretation half is the production
        // interpreter on captured evidence, which attests nothing itself.
        // Green when the resolver lands.
        // --- Falsifier: a resolver that produces a classification the regular twin does not share, or an interpretation that diverges on identical evidence, fails this fixture. ---
        [Test]
        public void ModeZeroMultiAndRegularTwinResolveToTheSameProvenOpaqueAnswer()
        {
            var twinMaterial = NewMaterial(
                LilToonSourceAttestation.SupportedShaderName);
            twinMaterial.shaderKeywords = Array.Empty<string>();
            twinMaterial.SetFloat("_TransparentMode", 0f);
            twinMaterial.SetFloat("_UseClippingCanceller", 0f);
            twinMaterial.SetFloat("_AsOverlay", 0f);
            var twinSelected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    twinMaterial,
                    out var twinFamily,
                    out _,
                    out _);
            Assert.That(twinSelected, Is.True);
            Assert.That(
                twinFamily,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            var twin = Capture(twinMaterial, MultiResolutionRequest());
            var multi = CapturedMultiEvidence(BaseContainerName);

            var resolved = ResolveThroughSeam(
                multi,
                BaseContainerName,
                out var family,
                out var mode);

            Assert.That(resolved, Is.True);
            Assert.That(family, Is.EqualTo(twinFamily));
            Assert.That(mode, Is.EqualTo(0));

            var multiAlpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                multi);
            var twinAlpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                twin);
            Assert.That(multiAlpha.IsComplete, Is.True);
            Assert.That(twinAlpha.IsComplete, Is.True);
            Assert.That(
                multiAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(twinAlpha.GetCompleteValue().GetConstantValue()));
            Assert.That(
                multiAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(1f));
        }

        // The mode-1 parity oracle: a keyword-consistent mode-1 Multi
        // material and the regular cutout identity, captured under ONE
        // shared request - the production combined Multi request the closed
        // capture selection hands back - must land on the same resolved
        // family, and the resolved family's own interpreter must answer
        // without throwing and with the same proven-opaque alpha on the
        // identical alpha-relevant facts. The interpreter reads the cutout
        // schema by name; before the request carried the cutout and
        // transparent schemas beside the opaque one, this read threw on the
        // first unrequested cutout fact, which is the defect the widened
        // union fixes.
        // Path: profile injection seam for the resolution half; the
        // production selection and the production interpreter for the rest.
        // Green when the union lands.
        // --- Falsifier: a Multi request that omits the cutout schema, or an interpreter answer that diverges from the regular twin on identical evidence, fails this fixture. ---
        [Test]
        public void CutoutModeMultiAndTheCutoutTwinAnswerTheSameResolvedFamilyAlpha()
        {
            var multiMaterial = NewCutoutSchemaMaterial(
                BaseContainerName, 1f, new[] { "UNITY_UI_ALPHACLIP" });
            var twinMaterial = NewCutoutSchemaMaterial(
                "Hidden/lilToonCutout", 0f, Array.Empty<string>());

            var multiSelected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    multiMaterial,
                    out var multiFamily,
                    out _,
                    out var sharedRequest);
            Assert.That(multiSelected, Is.True);
            Assert.That(
                multiFamily,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonMulti));
            Assert.That(sharedRequest, Is.Not.Null);

            var multi = Capture(multiMaterial, sharedRequest);
            var twin = Capture(twinMaterial, sharedRequest);

            var resolved = ResolveThroughSeam(
                multi,
                BaseContainerName,
                out var family,
                out var mode);
            Assert.That(resolved, Is.True);
            Assert.That(
                family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout));
            Assert.That(mode, Is.EqualTo(1));

            var multiAlpha = LilToonCutoutMaterialSemantics
                .InterpretVerifiedCutoutAlpha(multi);
            var twinAlpha = LilToonCutoutMaterialSemantics
                .InterpretVerifiedCutoutAlpha(twin);
            Assert.That(multiAlpha.IsComplete, Is.True);
            Assert.That(twinAlpha.IsComplete, Is.True);
            Assert.That(
                multiAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(twinAlpha.GetCompleteValue().GetConstantValue()));
            Assert.That(
                multiAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(1f));
        }

        // The mode-2 parity oracle, shaped exactly like the mode-1 one: the
        // resolved transparent interpreter reads the transparent schema by
        // name, so the same union argument applies with the transparent
        // request in place of the cutout one.
        // Path: profile injection seam for the resolution half; the
        // production selection and the production interpreter for the rest.
        // Green when the union lands.
        // --- Falsifier: a Multi request that omits the transparent schema, or an interpreter answer that diverges from the regular twin on identical evidence, fails this fixture. ---
        [Test]
        public void TransparentModeMultiAndTheTransparentTwinAnswerTheSameResolvedFamilyAlpha()
        {
            var multiMaterial = NewCutoutSchemaMaterial(
                BaseContainerName, 2f, new[] { "UNITY_UI_CLIP_RECT" });
            var twinMaterial = NewCutoutSchemaMaterial(
                "Hidden/lilToonTransparent", 0f, Array.Empty<string>());

            var multiSelected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    multiMaterial,
                    out var multiFamily,
                    out _,
                    out var sharedRequest);
            Assert.That(multiSelected, Is.True);
            Assert.That(
                multiFamily,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonMulti));
            Assert.That(sharedRequest, Is.Not.Null);

            var multi = Capture(multiMaterial, sharedRequest);
            var twin = Capture(twinMaterial, sharedRequest);

            var resolved = ResolveThroughSeam(
                multi,
                BaseContainerName,
                out var family,
                out var mode);
            Assert.That(resolved, Is.True);
            Assert.That(
                family,
                Is.EqualTo(CapturedAlphaMaterialFamily
                    .LilToonTransparent));
            Assert.That(mode, Is.EqualTo(2));

            var multiAlpha = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentAlpha(multi);
            var twinAlpha = LilToonTransparentMaterialSemantics
                .InterpretVerifiedTransparentAlpha(twin);
            Assert.That(multiAlpha.IsComplete, Is.True);
            Assert.That(twinAlpha.IsComplete, Is.True);
            Assert.That(
                multiAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(twinAlpha.GetCompleteValue().GetConstantValue()));
            Assert.That(
                multiAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(1f));
        }

        /// <summary>
        /// Runs the resolver once through the profile injection seam: the
        /// gathered identity evidence and the test-only row the Task 6
        /// verify fixtures established, with the keyword-request fact true.
        /// </summary>
        private static bool ResolveThroughSeam(
            CapturedMaterialEvidence evidence,
            string containerName,
            out CapturedAlphaMaterialFamily family,
            out int mode)
        {
            return ResolveThroughSeam(
                evidence,
                containerName,
                out family,
                out mode,
                out _);
        }

        private static bool ResolveThroughSeam(
            CapturedMaterialEvidence evidence,
            string containerName,
            out CapturedAlphaMaterialFamily family,
            out int mode,
            out LilToonMultiResolutionRefusal refusal)
        {
            return LilToonMultiResolution.Resolve(
                evidence,
                MatchingSourceEvidence(containerName),
                MatchingProfile(containerName),
                keywordsRequested: true,
                out family,
                out mode,
                out refusal);
        }

        /// <summary>
        /// The gathered identity evidence of one installed-shape container:
        /// every conjunction term matches the test-only row exactly. The
        /// canonicalization analyses stay unset, because the verify
        /// conjunction reads only the name, GUID, stamp, package, include
        /// tree, and canonical digest terms.
        /// </summary>
        internal static LilToonSourceEvidence MatchingSourceEvidence(
            string containerName)
        {
            return new LilToonSourceEvidence(
                containerName,
                TestContainerGuid,
                true,
                LilToonSourceAttestation.ShaderFormatVersion,
                true,
                LilToonSourceAttestation.PackageName,
                LilToonSourceAttestation.PackageVersion,
                null,
                TestContainerCanonicalDigest,
                null,
                LilToonSourceAttestation.IncludeTreeDigest,
                false,
                0,
                Array.Empty<string>(),
                null,
                null);
        }

        /// <summary>
        /// The test-only profile row: the same synthetic identity values
        /// the Task 6 verify fixtures use, named after the container under
        /// test.
        /// </summary>
        internal static LilToonMultiContainerProfile MatchingProfile(
            string containerName)
        {
            return new LilToonMultiContainerProfile(
                containerName,
                TestContainerGuid,
                TestContainerCanonicalDigest);
        }

        /// <summary>
        /// One captured Multi evidence, built through the production capture
        /// path: a stand-in material named after the container under test,
        /// with the mode and both gate scalars set explicitly.
        /// </summary>
        private CapturedMaterialEvidence CapturedMultiEvidence(
            string shaderAssetName,
            float transparentMode = 0f)
        {
            var material = NewMaterial(shaderAssetName);
            material.shaderKeywords = Array.Empty<string>();
            material.SetFloat("_TransparentMode", transparentMode);
            material.SetFloat("_UseClippingCanceller", 0f);
            material.SetFloat("_AsOverlay", 0f);
            return Capture(material, MultiResolutionRequest());
        }

        private Material NewMaterial(string shaderName)
        {
            var path = TempFolder + "/" +
                shaderName.Replace('/', '-') + ".shader";
            File.WriteAllText(
                path,
                "Shader \"" + shaderName + "\"\n" +
                "{\n    Properties\n    {\n" +
                "        _TransparentMode (\"TransparentMode\", Float) = 0\n" +
                "        _UseClippingCanceller" +
                " (\"ClippingCanceller\", Float) = 0\n" +
                "        _AsOverlay (\"AsOverlay\", Float) = 0\n" +
                "        _Invisible (\"Invisible\", Int) = 0\n" +
                "        _UDIMDiscardCompile" +
                " (\"UDIMDiscardCompile\", Int) = 0\n" +
                "    }\n    SubShader { Pass {} }\n}\n");
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            var material = new Material(shader);
            _materials.Add(material);
            return material;
        }

        /// <summary>
        /// The full cutout-schema stand-in minted under this class's own
        /// temp folder, tracked for teardown. See
        /// <see cref="LilToonFixtureTestBase.CreateCutoutSchemaStandIn"/>
        /// for the minted state.
        /// </summary>
        private Material NewCutoutSchemaMaterial(
            string shaderName,
            float transparentMode,
            string[] keywords)
        {
            var material = LilToonFixtureTestBase.CreateCutoutSchemaStandIn(
                TempFolder, shaderName, transparentMode, keywords);
            _materials.Add(material);
            return material;
        }

        private static CapturedMaterialEvidence Capture(
            Material material,
            MaterialEvidenceRequest request)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, request),
            })[0];
        }

        /// <summary>
        /// The closed request every fixture captures under: the shader name
        /// the routing identity reads, the three Multi scalars the mode and
        /// the gate rules read, the two opaque coverage facts the parity
        /// oracle interprets, and the keyword set. Every fixture sets
        /// exactly these material properties, so no fixture depends on
        /// stand-in shader defaults.
        /// </summary>
        private static MaterialEvidenceRequest MultiResolutionRequest()
        {
            return new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: new[]
                {
                    "_TransparentMode",
                    "_UseClippingCanceller",
                    "_AsOverlay",
                    "_Invisible",
                    "_UDIMDiscardCompile",
                },
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties:
                    Array.Empty<TexturePropertyEvidenceRequest>(),
                captureKeywords: true);
        }
    }
}
