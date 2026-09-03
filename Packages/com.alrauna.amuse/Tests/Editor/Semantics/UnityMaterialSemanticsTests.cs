using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Frontend selection, and an explicit record of its public-project limit.
    /// <para>
    /// The public development project installs neither Poiyomi nor lilToon, so
    /// no material here can pass either frontend's source attestation. These
    /// tests therefore exercise the real refusal path on real Unity objects and
    /// make no claim about vendor dispatch, which remains a production
    /// capability the public suite cannot observe.
    /// </para>
    /// </summary>
    public sealed class UnityMaterialSemanticsTests
    {
        private const string TempFolder = "Assets/AmuseTests_MaterialDispatch";

        private Material _material;
        private readonly List<Material> _batchMaterials = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_MaterialDispatch");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
            {
                UnityEngine.Object.DestroyImmediate(_material);
            }

            _material = null;

            foreach (var material in _batchMaterials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            _batchMaterials.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        private static void AssertAllUnknown(MaterialSemantics semantics)
        {
            Assert.That(semantics, Is.Not.Null);
            Assert.That(semantics.BaseColor.IsComplete, Is.False);
            Assert.That(semantics.Alpha.IsComplete, Is.False);
            Assert.That(semantics.Emission.IsComplete, Is.False);
            Assert.That(semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void NullMaterialIsAllUnknownRatherThanAThrow()
        {
            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void DestroyedMaterialIsAllUnknown()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            UnityEngine.Object.DestroyImmediate(material);

            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void MaterialNeitherFrontendAttestsIsAllUnknown()
        {
            _material = new Material(Shader.Find("Unlit/Color"));

            AssertAllUnknown(
                UnityMaterialSemantics.AnalyzeBaseMaterial(_material));
        }

        [Test]
        public void AllUnknownIsUnknownInEveryOutput()
        {
            AssertAllUnknown(UnityMaterialSemantics.AllUnknown());
        }

        [Test]
        public void CaptureAlphaMaterialsKeepsFamilyRequestsIsolated()
        {
            var poiyomi = NewMaterial(
                "poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());
            var lilToon = NewMaterial(
                "liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(
                new[] { poiyomi, lilToon });

            Assert.That(captured.Count, Is.EqualTo(2));
            Assert.That(
                captured[0].Family, Is.EqualTo(CapturedAlphaMaterialFamily.Poiyomi));
            Assert.That(
                captured[1].Family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.Throws<ArgumentException>(
                () => captured[0].Evidence.TryGetScalar("_Invisible", out _));
            Assert.Throws<ArgumentException>(
                () => captured[1].Evidence.TryGetScalar(
                    "_AlphaForceOpaque", out _));

            var poiyomiAlpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                captured[0].Evidence);
            var lilToonAlpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                captured[1].Evidence);
            Assert.That(poiyomiAlpha.IsComplete, Is.True);
            Assert.That(
                poiyomiAlpha.GetCompleteValue().GetConstantValue(), Is.EqualTo(1f));
            Assert.That(lilToonAlpha.IsComplete, Is.True);
            Assert.That(
                lilToonAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(1f));
        }

        /// <summary>
        /// Request selection identifies the family and hands back that family's
        /// existing alpha request. It deliberately does not attest the source:
        /// this material carries the supported shader name over a stand-in
        /// source no attestation can verify, and selection still succeeds.
        /// <see cref="ClosedCaptureRevalidatesSourceAttestation"/> is the pass
        /// that refuses it.
        /// </summary>
        [Test]
        public void SelectionIdentifiesPoiyomiWithoutAttestingSource()
        {
            var material = NewMaterial(
                "selected-poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material, out var family, out var request, out _);

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.Poiyomi));
            Assert.That(
                request,
                Is.SameAs(PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                "selection must hand back the family's existing request");
        }

        [Test]
        public void SelectionIdentifiesLilToonWithoutAttestingSource()
        {
            var material = NewMaterial(
                "selected-liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material, out var family, out var request, out _);

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.That(
                request,
                Is.SameAs(LilToonMaterialSemantics.AlphaEvidenceRequest),
                "selection must hand back the family's existing request");
        }

        /// <summary>
        /// Selection answers two separate questions for one material: what
        /// ordinary alpha proof may consider, and what the single closed
        /// capture must gather. Poiyomi is the family where they differ,
        /// because conversion reads render state alpha does not depend on.
        /// </summary>
        [Test]
        public void PoiyomiCaptureSchemaCarriesConversionEvidenceAlphaRelevanceDoesNot()
        {
            var material = NewMaterial(
                "schema-poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material,
                out _,
                out var alphaRelevance,
                out var captureSchema);

            Assert.That(selected, Is.True);
            Assert.That(
                alphaRelevance,
                Is.SameAs(PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                "alpha relevance must remain the family's existing request");

            // Representative conversion-only render state. Neither name appears
            // in the alpha request, so each one separates the two questions.
            foreach (var conversionOnly in new[] { "_ZWrite", "_EnableOutlines" })
            {
                CollectionAssert.Contains(
                    captureSchema.ScalarProperties,
                    conversionOnly,
                    "the capture schema must gather conversion evidence: " +
                    conversionOnly);
                CollectionAssert.Contains(
                    captureSchema.PresenceProperties,
                    conversionOnly,
                    "the capture schema must gather conversion presence: " +
                    conversionOnly);
                CollectionAssert.DoesNotContain(
                    alphaRelevance.ScalarProperties,
                    conversionOnly,
                    "conversion-only render state widened alpha relevance: " +
                    conversionOnly);
                CollectionAssert.DoesNotContain(
                    alphaRelevance.PresenceProperties,
                    conversionOnly,
                    "conversion-only render state widened alpha relevance: " +
                    conversionOnly);
            }

            CollectionAssert.IsSubsetOf(
                alphaRelevance.ScalarProperties,
                captureSchema.ScalarProperties,
                "the capture schema must still gather everything alpha needs");
            Assert.That(
                PoiyomiOpaqueConversion.ConversionEvidenceRequest
                    .TextureProperties,
                Is.Empty,
                "conversion evidence must acquire no texture");
            CollectionAssert.AreEqual(
                alphaRelevance.TextureProperties
                    .Select(value => value.PropertyName).ToArray(),
                captureSchema.TextureProperties
                    .Select(value => value.PropertyName).ToArray(),
                "conversion capture introduced a new texture acquisition");
        }

        /// <summary>
        /// lilToon has no opaque-conversion request, so its two questions have
        /// the same answer and neither may acquire conversion render state.
        /// </summary>
        [Test]
        public void LilToonCaptureSchemaIsExactlyItsAlphaRelevance()
        {
            var material = NewMaterial(
                "schema-liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material,
                out _,
                out var alphaRelevance,
                out var captureSchema);

            Assert.That(selected, Is.True);
            Assert.That(
                alphaRelevance,
                Is.SameAs(LilToonMaterialSemantics.AlphaEvidenceRequest));
            Assert.That(
                captureSchema,
                Is.SameAs(LilToonMaterialSemantics.AlphaEvidenceRequest),
                "lilToon has no conversion request, so its capture schema is " +
                "its alpha request");

            foreach (var conversionOnly in new[] { "_ZWrite", "_EnableOutlines" })
            {
                CollectionAssert.DoesNotContain(
                    captureSchema.ScalarProperties,
                    conversionOnly,
                    "conversion render state entered lilToon's schema: " +
                    conversionOnly);
                CollectionAssert.DoesNotContain(
                    captureSchema.PresenceProperties,
                    conversionOnly,
                    "conversion render state entered lilToon's schema: " +
                    conversionOnly);
            }
        }

        /// <summary>
        /// Selection identifies the cutout family from the exact shader name
        /// and hands back the cutout alpha request itself. As for every
        /// family, selection does not attest the source: this material
        /// carries the cutout shader name over a stand-in source no
        /// attestation can verify, and selection still succeeds.
        /// </summary>
        [Test]
        public void SelectionIdentifiesLilToonCutoutWithoutAttestingSource()
        {
            var material = NewMaterial(
                "selected-cutout.shader",
                LilToonSourceAttestation.CutoutShaderName,
                CutoutProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material, out var family, out var request, out _);

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout));
            Assert.That(
                request,
                Is.SameAs(LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                "selection must hand back the cutout family's existing " +
                "request");
        }

        /// <summary>
        /// The cutout frontend answers selection's two questions the way
        /// Poiyomi does: its capture schema is its alpha request combined
        /// with the lilToon conversion request, so one capture serves both
        /// the cutout alpha proof and the conversion. The combination must
        /// widen in exactly one direction: conversion render state enters
        /// the schema, and neither Poiyomi's conversion-only state nor any
        /// new texture acquisition does.
        /// </summary>
        [Test]
        public void CutoutCaptureSchemaCarriesConversionEvidenceAlphaRelevanceDoesNot()
        {
            var material = NewMaterial(
                "schema-cutout.shader",
                LilToonSourceAttestation.CutoutShaderName,
                CutoutProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material,
                out _,
                out var alphaRelevance,
                out var captureSchema);

            Assert.That(selected, Is.True);
            Assert.That(
                alphaRelevance,
                Is.SameAs(LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                "alpha relevance must remain the cutout request itself");

            // Representative conversion render state. _ZWrite is
            // conversion-only outright; _Cutoff is already a cutout theorem
            // scalar, so only its presence dimension shows the widening.
            foreach (var conversionOnly in new[] { "_ZWrite", "_Cutoff" })
            {
                CollectionAssert.Contains(
                    captureSchema.ScalarProperties,
                    conversionOnly,
                    "the capture schema must gather conversion evidence: " +
                    conversionOnly);
                CollectionAssert.Contains(
                    captureSchema.PresenceProperties,
                    conversionOnly,
                    "the capture schema must gather conversion presence: " +
                    conversionOnly);
            }

            CollectionAssert.DoesNotContain(
                alphaRelevance.ScalarProperties,
                "_ZWrite",
                "conversion-only render state widened alpha relevance: " +
                "_ZWrite");
            CollectionAssert.DoesNotContain(
                alphaRelevance.PresenceProperties,
                "_ZWrite",
                "conversion-only render state widened alpha relevance: " +
                "_ZWrite");

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite",
                    "_ZTest", "_OffsetFactor", "_OffsetUnits", "_ColorMask",
                    "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp",
                    "_BlendOpAlpha", "_SrcBlendFA", "_DstBlendFA",
                    "_SrcBlendAlphaFA", "_DstBlendAlphaFA", "_BlendOpFA",
                    "_BlendOpAlphaFA", "_Cutoff",
                },
                captureSchema.PresenceProperties,
                "the cutout capture schema's presence dimension must stay " +
                "exactly the recipe plus the cutout source's own _Cutoff");
            CollectionAssert.IsSubsetOf(
                alphaRelevance.ScalarProperties,
                captureSchema.ScalarProperties,
                "the capture schema must still gather everything alpha " +
                "needs");

            foreach (var poiyomiOnly in new[] { "_EnableOutlines" })
            {
                CollectionAssert.DoesNotContain(
                    captureSchema.ScalarProperties,
                    poiyomiOnly,
                    "Poiyomi's conversion state entered the cutout schema: " +
                    poiyomiOnly);
                CollectionAssert.DoesNotContain(
                    captureSchema.PresenceProperties,
                    poiyomiOnly,
                    "Poiyomi's conversion state entered the cutout schema: " +
                    poiyomiOnly);
            }

            CollectionAssert.AreEqual(
                alphaRelevance.TextureProperties
                    .Select(value => value.PropertyName).ToArray(),
                captureSchema.TextureProperties
                    .Select(value => value.PropertyName).ToArray(),
                "conversion capture introduced a new texture acquisition");
        }

        /// <summary>
        /// The transparent frontend answers selection's two questions the way
        /// the cutout frontend does: its capture schema is its alpha request
        /// combined with the lilToon conversion request, so one capture
        /// serves both the transparent alpha proof and the conversion — and
        /// the combination widens in exactly one direction, without the
        /// compiled-out dither toggle.
        /// </summary>
        [Test]
        public void TransparentCaptureSchemaCarriesConversionEvidence()
        {
            var material = NewMaterial(
                "schema-transparent.shader",
                LilToonSourceAttestation.TransparentShaderName,
                TransparentProperties());

            var selected =
                UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                    material,
                    out var family,
                    out var alphaRelevance,
                    out var captureSchema);

            Assert.That(selected, Is.True);
            Assert.That(
                family,
                Is.EqualTo(
                    CapturedAlphaMaterialFamily.LilToonTransparent));
            Assert.That(
                alphaRelevance,
                Is.SameAs(
                    LilToonTransparentMaterialSemantics
                        .AlphaEvidenceRequest),
                "alpha relevance must remain the transparent request itself");

            foreach (var conversionOnly in
                     new[] { "_ZWrite", "_Cutoff", "_SubpassCutoff" })
            {
                CollectionAssert.Contains(
                    captureSchema.ScalarProperties, conversionOnly);
            }

            // The transparent capture must not widen the cutout or opaque
            // requests, and must not gather the compiled-out dither toggle.
            CollectionAssert.DoesNotContain(
                captureSchema.ScalarProperties, "_UseDither");
            CollectionAssert.DoesNotContain(
                captureSchema.ScalarProperties, "_EnableOutlines");
        }

        /// <summary>
        /// The anti-mutation guard for the fourth family: adding the
        /// transparent requests must leave every existing request object
        /// exactly as it was.
        /// </summary>
        [Test]
        public void ExistingRequests_AreNotMutatedByTheTransparentFamily()
        {
            // Falsifies: a shared or widened request object.
            CollectionAssert.DoesNotContain(
                LilToonCutoutMaterialSemantics.AlphaEvidenceRequest
                    .ScalarProperties,
                "_SubpassCutoff");
            CollectionAssert.Contains(
                LilToonCutoutMaterialSemantics.AlphaEvidenceRequest
                    .ScalarProperties,
                "_UseDither");
            CollectionAssert.DoesNotContain(
                LilToonCutoutSourceEligibility.SourceEvidenceRequest
                    .ScalarProperties,
                "_AlphaBoostFA");
        }

        /// <summary>
        /// Only the exact cutout name is the cutout frontend. Near-miss
        /// vendor shader names stay unsupported and yield no request and no
        /// capture schema. The transparent normal name is no longer in this
        /// list: it is its own supported family, selected by
        /// TransparentCaptureSchemaCarriesConversionEvidence, and its own
        /// near misses are covered by
        /// NearMissTransparentName_IsNeverSelectedOrAdmitted.
        /// </summary>
        [Test]
        public void SelectionRefusesNearCutoutLilToonShaderNames()
        {
            foreach (var shaderName in new[]
                     {
                         "Hidden/lilToonCutoutOutline",
                         "Hidden/lilToonOnePassTransparent",
                         "Hidden/lilToonTwoPassTransparent",
                         "Hidden/lilToonTransparentOutline",
                         "Hidden/lilToonOnePassTransparentOutline",
                         "Hidden/lilToonTwoPassTransparentOutline",
                         "_lil/[Optional] lilToonOutlineOnly",
                         "_lil/[Optional] lilToonOutlineOnlyCutout",
                         "_lil/[Optional] lilToonOutlineOnlyTransparent",
                         "Hidden/lilToonLite",
                         "Hidden/lilToonLiteCutout",
                         "_lil/[Optional] lilToonLiteOverlay",
                         "_lil/[Optional] lilToonLiteOverlayOnePass",
                         "Hidden/lilToonTessellation",
                         "Hidden/lilToonTessellationCutout",
                         "Hidden/lilToonRefraction",
                         "Hidden/lilToonRefractionBlur",
                         "Hidden/lilToonFur",
                         "Hidden/lilToonFurCutout",
                         "Hidden/lilToonFurTwoPass",
                         "_lil/[Optional] lilToonFurOnlyTransparent",
                         "_lil/[Optional] lilToonFurOnlyCutout",
                         "_lil/[Optional] lilToonFurOnlyTwoPass",
                         "Hidden/lilToonGem",
                         "_lil/[Optional] lilToonFakeShadow",
                         "_lil/[Optional] lilToonOverlay",
                         "_lil/[Optional] lilToonOverlayOnePass",
                         "_lil/lilToonMulti",
                         "Hidden/lilToonMultiOutline",
                         "Hidden/lilToonMultiRefraction",
                         "Hidden/lilToonMultiFur",
                         "Hidden/lilToonMultiGem",
                         "Custom/GeneratedLilToonContainer",
                     })
            {
                var material = NewMaterial(
                    "refused-" + shaderName.Replace('/', '-') + ".shader",
                    shaderName,
                    CutoutProperties());

                var selected =
                    UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                        material,
                        out var family,
                        out var request,
                        out var captureSchema);

                Assert.That(selected, Is.False, shaderName);
                Assert.That(
                    family,
                    Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported),
                    shaderName);
                Assert.That(request, Is.Null, shaderName);
                Assert.That(captureSchema, Is.Null, shaderName);
            }
        }

        /// <summary>
        /// Fresh invariance row beside
        /// <see cref="LilToonCaptureSchemaIsExactlyItsAlphaRelevance"/>:
        /// adding the cutout family to the selection map must not disturb
        /// the opaque lilToon answers — both questions still return the
        /// existing reference-equal request objects.
        /// </summary>
        [Test]
        public void OpaqueLilToonSelectionStaysReferenceStableBesideCutout()
        {
            var material = NewMaterial(
                "stable-opaque-liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material,
                out var family,
                out var alphaRelevance,
                out var captureSchema);

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.That(
                alphaRelevance,
                Is.SameAs(LilToonMaterialSemantics.AlphaEvidenceRequest));
            Assert.That(
                captureSchema,
                Is.SameAs(LilToonMaterialSemantics.AlphaEvidenceRequest));
        }

        /// <summary>
        /// Routing (R4): once captured as cutout-family evidence, the alpha
        /// verdict is the cutout interpreter's verdict on the same evidence —
        /// never the opaque lilToon or Poiyomi verdict, which would collapse
        /// gate-off cutout coverage to a complete constant 1. Both rows
        /// discriminate: a gate-active material and a gate-off one whose
        /// main texture carries no resolvable source identity on this
        /// scaffold. AnalyzeAlphaMaterial re-verifies source attestation,
        /// which no stand-in passes, so the observable surface is
        /// all-Unknown both before and after the cutout arm exists; the
        /// equality assertion is the shape discriminator that keeps holding
        /// when the arm completes.
        /// </summary>
        [Test]
        public void RoutedCutoutFamilyAlphaNeverCollapsesToConstantOne()
        {
            var gateActive = NewMaterial(
                "routed-cutout-gate-active.shader",
                LilToonSourceAttestation.CutoutShaderName,
                CutoutProperties());
            gateActive.SetFloat("_UseDither", 1f);
            var gateOff = NewMaterial(
                "routed-cutout-gate-off.shader",
                LilToonSourceAttestation.CutoutShaderName,
                CutoutProperties());

            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(
                new[] { gateActive, gateOff });
            Assert.That(
                captured[0].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout));
            Assert.That(
                captured[1].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout));

            foreach (var material in captured)
            {
                var routed = UnityMaterialSemantics.AnalyzeAlphaMaterial(
                    material);
                var cutout = LilToonCutoutMaterialSemantics
                    .InterpretVerifiedCutoutAlpha(material.Evidence);

                Assert.That(
                    routed.Alpha.IsComplete &&
                        routed.Alpha.GetCompleteValue().Kind ==
                        ScalarSemanticValueKind.Constant,
                    Is.False,
                    "routed cutout alpha must never complete as a constant");
                Assert.That(
                    routed.Alpha,
                    Is.EqualTo(cutout),
                    "the routed alpha must be the cutout interpreter's " +
                    "verdict on the same evidence");
            }
        }

        /// <summary>
        /// Batch capture classifies the cutout name identically to
        /// selection — same family, and a combined capture schema whose
        /// every scalar the selection handback also names. The last
        /// assertion fails while the name map is duplicated between the two
        /// consumers, which is why the map is one function.
        /// </summary>
        [Test]
        public void CaptureAlphaMaterialsClassifiesCutoutLikeSelection()
        {
            var material = NewMaterial(
                "captured-cutout.shader",
                LilToonSourceAttestation.CutoutShaderName,
                CutoutProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                material, out var family, out var alphaRelevance, out _);
            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(
                new[] { material });

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToonCutout));
            Assert.That(
                captured[0].Family,
                Is.EqualTo(family),
                "batch capture must classify the cutout name identically " +
                "to selection");
            foreach (var name in alphaRelevance.ScalarProperties)
            {
                Assert.DoesNotThrow(
                    () => captured[0].Evidence.TryGetScalar(name, out _),
                    name + " must be captured under the alpha relevance");
            }

            Assert.Throws<ArgumentException>(
                () => captured[0].Evidence.TryGetScalar(
                    "_EnableOutlines", out _),
                "batch capture must not widen toward Poiyomi's request");
        }

        [Test]
        public void SelectionRejectsAnUnsupportedShaderFamily()
        {
            _material = new Material(Shader.Find("Unlit/Color"));

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                _material,
                out var family,
                out var request,
                out var captureSchema);

            Assert.That(selected, Is.False);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported));
            Assert.That(request, Is.Null);
            Assert.That(
                captureSchema,
                Is.Null,
                "an unsupported family must yield no capture schema either");
        }

        [Test]
        public void SelectionRejectsANullMaterial()
        {
            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequests(
                null,
                out var family,
                out var request,
                out var captureSchema);

            Assert.That(selected, Is.False);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported));
            Assert.That(request, Is.Null);
            Assert.That(
                captureSchema,
                Is.Null,
                "an unsupported family must yield no capture schema either");
        }

        [Test]
        public void ClosedCaptureRevalidatesLilToonSourceAttestation()
        {
            var material = NewMaterial(
                "unattested-liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var success = UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                new[] { material },
                new[] { CapturedAlphaMaterialFamily.LilToon },
                LilToonMaterialSemantics.AlphaEvidenceRequest,
                out var captured);

            Assert.That(success, Is.False);
            Assert.That(captured, Is.Null);
        }

        [Test]
        public void ClosedCaptureRevalidatesSourceAttestation()
        {
            var material = NewMaterial(
                "unattested-poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());

            var success = UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                new[] { material },
                new[] { CapturedAlphaMaterialFamily.Poiyomi },
                PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                out var captured);

            Assert.That(success, Is.False);
            Assert.That(captured, Is.Null);
        }

        [Test]
        public void AnalyzeAlphaMaterialUnsupportedFamilyIsAllUnknown()
        {
            _material = new Material(Shader.Find("Unlit/Color"));
            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(
                new[] { _material });

            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(
                captured[0].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported));
            AssertAllUnknown(
                UnityMaterialSemantics.AnalyzeAlphaMaterial(captured[0]));
        }

        private Material NewMaterial(
            string fileName,
            string shaderName,
            string properties)
        {
            var path = TempFolder + "/" + fileName;
            File.WriteAllText(
                path,
                "Shader \"" + shaderName + "\"\n" +
                "{\n    Properties\n    {" + properties +
                "\n    }\n    SubShader { Pass {} }\n}\n");
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            var material = new Material(shader);
            _batchMaterials.Add(material);
            return material;
        }

        private static string LilToonProperties()
        {
            return @"
        [HideInInspector] _lilToonVersion (""Version"", Int) = 45
        _Invisible (""Invisible"", Int) = 0
        _UDIMDiscardCompile (""UDIM"", Int) = 0";
        }

        /// <summary>
        /// The cutout stand-in property block: every property the cutout
        /// alpha request and the lilToon conversion request name, at
        /// gate-off defaults.
        /// </summary>
        private static string CutoutProperties()
        {
            return @"
        [HideInInspector] _lilToonVersion (""Version"", Int) = 45
        _Invisible (""Invisible"", Int) = 0
        _UDIMDiscardCompile (""UDIMDiscardCompile"", Int) = 0
        _UDIMDiscardMode (""UDIMDiscardMode"", Int) = 0
        _ShiftBackfaceUV (""ShiftBackfaceUV"", Int) = 0
        _UseParallax (""UseParallax"", Int) = 0
        _UseMain2ndTex (""UseMain2ndTex"", Int) = 0
        _UseMain3rdTex (""UseMain3rdTex"", Int) = 0
        _AlphaMaskMode (""AlphaMaskMode"", Int) = 0
        _UseDither (""UseDither"", Int) = 0
        _IDMask1 (""IDMask1"", Int) = 0
        _IDMask2 (""IDMask2"", Int) = 0
        _IDMask3 (""IDMask3"", Int) = 0
        _IDMask4 (""IDMask4"", Int) = 0
        _IDMask5 (""IDMask5"", Int) = 0
        _IDMask6 (""IDMask6"", Int) = 0
        _IDMask7 (""IDMask7"", Int) = 0
        _IDMask8 (""IDMask8"", Int) = 0
        _IDMaskControlsDissolve (""IDMaskControlsDissolve"", Int) = 0
        _Cutoff (""Cutoff"", Range(0,1)) = 0.5
        _Color (""Color"", Color) = (1,1,1,1)
        _MainTex (""Texture"", 2D) = ""white"" {}
        _DissolveParams (""DissolveParams"", Vector) = (0,0,0.5,0.1)
        _MainTex_ScrollRotate (""ScrollRotate"", Vector) = (0,0,0,0)
        _SrcBlend (""SrcBlend"", Float) = 1
        _DstBlend (""DstBlend"", Float) = 0
        _AlphaToMask (""AlphaToMask"", Float) = 0
        _ZWrite (""ZWrite"", Float) = 1
        _ZTest (""ZTest"", Float) = 4
        _OffsetFactor (""OffsetFactor"", Float) = 0
        _OffsetUnits (""OffsetUnits"", Float) = 0
        _ColorMask (""ColorMask"", Float) = 15
        _SrcBlendAlpha (""SrcBlendAlpha"", Float) = 1
        _DstBlendAlpha (""DstBlendAlpha"", Float) = 10
        _BlendOp (""BlendOp"", Float) = 0
        _BlendOpAlpha (""BlendOpAlpha"", Float) = 0
        _SrcBlendFA (""SrcBlendFA"", Float) = 1
        _DstBlendFA (""DstBlendFA"", Float) = 1
        _SrcBlendAlphaFA (""SrcBlendAlphaFA"", Float) = 0
        _DstBlendAlphaFA (""DstBlendAlphaFA"", Float) = 1
        _BlendOpFA (""BlendOpFA"", Float) = 4
        _BlendOpAlphaFA (""BlendOpAlphaFA"", Float) = 4";
        }
        /// <summary>
        /// The transparent stand-in property block: every property the
        /// transparent alpha request and the lilToon conversion request
        /// name, at the vendor defaults the fixture shader declares —
        /// including the compiled-out dither toggle the request
        /// deliberately does not gather.
        /// </summary>
        private static string TransparentProperties()
        {
            return @"
        [HideInInspector] _lilToonVersion (""Version"", Int) = 45
        _Invisible (""Invisible"", Int) = 0
        _UDIMDiscardCompile (""UDIMDiscardCompile"", Int) = 0
        _UDIMDiscardMode (""UDIMDiscardMode"", Int) = 0
        _ShiftBackfaceUV (""ShiftBackfaceUV"", Int) = 0
        _UseParallax (""UseParallax"", Int) = 0
        _UseMain2ndTex (""UseMain2ndTex"", Int) = 0
        _UseMain3rdTex (""UseMain3rdTex"", Int) = 0
        _AlphaMaskMode (""AlphaMaskMode"", Int) = 0
        _UseDither (""UseDither"", Int) = 0
        _IDMask1 (""IDMask1"", Int) = 0
        _IDMask2 (""IDMask2"", Int) = 0
        _IDMask3 (""IDMask3"", Int) = 0
        _IDMask4 (""IDMask4"", Int) = 0
        _IDMask5 (""IDMask5"", Int) = 0
        _IDMask6 (""IDMask6"", Int) = 0
        _IDMask7 (""IDMask7"", Int) = 0
        _IDMask8 (""IDMask8"", Int) = 0
        _IDMaskControlsDissolve (""IDMaskControlsDissolve"", Int) = 0
        _Cutoff (""Cutoff"", Range(0,1)) = 0.5
        _Color (""Color"", Color) = (1,1,1,1)
        _MainTex (""Texture"", 2D) = ""white"" {}
        _DissolveParams (""DissolveParams"", Vector) = (0,0,0.5,0.1)
        _MainTex_ScrollRotate (""ScrollRotate"", Vector) = (0,0,0,0)
        _AlphaBoostFA (""AlphaBoostFA"", Float) = 10
        _SubpassCutoff (""SubpassCutoff"", Range(0,1)) = 0.5
        _DistanceFade (""DistanceFade"", Vector) = (0.1,0.01,0,0)
        _SrcBlend (""SrcBlend"", Float) = 1
        _DstBlend (""DstBlend"", Float) = 10
        _AlphaToMask (""AlphaToMask"", Float) = 0
        _ZWrite (""ZWrite"", Float) = 1
        _ZTest (""ZTest"", Float) = 4
        _OffsetFactor (""OffsetFactor"", Float) = 0
        _OffsetUnits (""OffsetUnits"", Float) = 0
        _ColorMask (""ColorMask"", Float) = 15
        _SrcBlendAlpha (""SrcBlendAlpha"", Float) = 1
        _DstBlendAlpha (""DstBlendAlpha"", Float) = 10
        _BlendOp (""BlendOp"", Float) = 0
        _BlendOpAlpha (""BlendOpAlpha"", Float) = 0
        _SrcBlendFA (""SrcBlendFA"", Float) = 1
        _DstBlendFA (""DstBlendFA"", Float) = 1
        _SrcBlendAlphaFA (""SrcBlendAlphaFA"", Float) = 0
        _DstBlendAlphaFA (""DstBlendAlphaFA"", Float) = 1
        _BlendOpFA (""BlendOpFA"", Float) = 4
        _BlendOpAlphaFA (""BlendOpAlphaFA"", Float) = 4";
        }

        private static string PoiyomiProperties()
        {
            return @"
        shader_master_label (""Master"", Float) = 0
        _ShaderOptimizerEnabled (""Locked"", Float) = 0
        _MainTex (""Main"", 2D) = ""white"" {}
        _Color (""Color"", Color) = (1,1,1,1)
        _BumpMap (""Bump"", 2D) = ""bump"" {}
        _EmissionMap (""Emission"", 2D) = ""white"" {}
        _EnableEmission (""Emission 0"", Float) = 0
        _EnableEmission1 (""Emission 1"", Float) = 0
        _EnableEmission2 (""Emission 2"", Float) = 0
        _EnableEmission3 (""Emission 3"", Float) = 0
        _AlphaForceOpaque (""Force Opaque"", Float) = 1
        _MainIgnoreTexAlpha (""Ignore Alpha"", Float) = 0
        _AlphaToCoverage (""Coverage"", Float) = 0
        _AlphaSharpenedA2C (""Sharpened"", Float) = 0
        _AlphaDithering (""Dither"", Float) = 0
        _EnableDissolve (""Dissolve"", Float) = 0
        _EnableUDIMDiscardOptions (""UDIM"", Float) = 0
        _AlphaMod (""Alpha Mod"", Float) = 0
        _MainAlphaMaskMode (""Mask Mode"", Float) = 0
        _AlphaDistanceFade (""Distance"", Float) = 0
        _AlphaFresnel (""Fresnel"", Float) = 0
        _AlphaAngular (""Angular"", Float) = 0
        _AlphaAudioLinkEnabled (""Audio Alpha"", Float) = 0
        _EnableAudioLink (""Audio"", Float) = 0
        _AlphaGlobalMask (""Global Mask"", Float) = 0
        _AlphaPremultiply (""Premultiply"", Float) = 0
        _BackFaceEnabled (""Backface"", Float) = 0
        _RGBMaskEnabled (""RGB Mask"", Float) = 0
        _DecalEnabled (""Decal 0"", Float) = 0
        _DecalEnabled1 (""Decal 1"", Float) = 0
        _DecalEnabled2 (""Decal 2"", Float) = 0
        _DecalEnabled3 (""Decal 3"", Float) = 0
        _EnableFlipbook (""Flipbook"", Float) = 0
        _EnableRimLighting (""Rim"", Float) = 0
        _EnableRim2Lighting (""Rim 2"", Float) = 0
        _EnableDepthRimLighting (""Depth Rim"", Float) = 0
        _EnableEnvironmentalRim (""Env Rim"", Float) = 0
        _VideoEffectsEnable (""Video"", Float) = 0
        _EnableTouchGlow (""Touch"", Float) = 0
        _MainVertexColoringEnabled (""Vertex"", Float) = 0
        _MainTexUV (""UV"", Float) = 0
        _MainTexPan (""Pan"", Vector) = (0,0,0,0)
        _MainPixelMode (""Pixel"", Float) = 0
        _MainTexStochastic (""Stochastic"", Float) = 0";
        }
    }
}
