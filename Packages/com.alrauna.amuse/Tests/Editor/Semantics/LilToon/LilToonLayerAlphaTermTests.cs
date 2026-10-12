using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;
using TextureWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Unit tests for sampler assignment and fail-closed checks in layer alpha.
    /// </summary>
    public sealed class LilToonLayerAlphaTermTests
    {
        [Test]
        public void BlendMask_UsesMainTexSamplingSettings()
        {
            var mainSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Clamp);
            var layerSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Repeat);

            var mainAssignment = CreateTextureAssignment(
                "test:main_tex",
                hasSampling: true,
                sampling: mainSampling,
                hasAlpha: false,
                hasRed: false);
            var layerAssignment = CreateTextureAssignment(
                "test:main_2nd_tex",
                hasSampling: true,
                sampling: layerSampling,
                hasAlpha: true,
                hasRed: false);
            var blendMaskAssignment = CreateTextureAssignment(
                "test:main_2nd_blend_mask",
                hasSampling: false,
                sampling: default,
                hasAlpha: false,
                hasRed: true);

            var evidence = CreateLayerEvidence(
                mainAssignment: mainAssignment,
                layerAssignment: layerAssignment,
                blendMaskAssignment: blendMaskAssignment);
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var term = LilToonLayerAlphaTerm.Interpret(
                evidence,
                third: false,
                diagnostics: diagnostics);

            Assert.That(term.Kind, Is.EqualTo(LilToonLayerAlphaTermKind.Field));
            Assert.That(
                term.Value.Kind,
                Is.EqualTo(ScalarSemanticValueKind.ProductChainOfTextureSamples));
            Assert.That(term.Value.GetChainFactorCount(), Is.EqualTo(2));

            var layerSample = term.Value.GetChainSample(0);
            Assert.That(layerSample.Sampling, Is.EqualTo(layerSampling));
            Assert.That(
                layerSample.Sampling.Wrap,
                Is.EqualTo(TextureWrapMode.Repeat));

            var blendMaskSample = term.Value.GetChainSample(1);
            Assert.That(blendMaskSample.Sampling, Is.EqualTo(mainSampling));
            Assert.That(
                blendMaskSample.Sampling.Wrap,
                Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void BlendMask_WhenMainTexLacksSampling_RefusesFailClosed()
        {
            var layerSampling = new TextureSampling(
                TextureFilterMode.Bilinear,
                TextureWrapMode.Repeat);
            var layerAssignment = CreateTextureAssignment(
                "test:main_2nd_tex",
                hasSampling: true,
                sampling: layerSampling,
                hasAlpha: true,
                hasRed: false);
            var blendMaskAssignment = CreateTextureAssignment(
                "test:main_2nd_blend_mask",
                hasSampling: false,
                sampling: default,
                hasAlpha: false,
                hasRed: true);

            var invalidMainAssignments = new[]
            {
                new CapturedTextureAssignment(
                    isAssigned: false,
                    requestedEvidence: TextureEvidenceKinds.SourceIdentity | TextureEvidenceKinds.Sampling,
                    hasScaleOffset: true,
                    scale: Vector2.one,
                    offset: Vector2.zero,
                    texture: null,
                    captureRefusals: Array.Empty<TextureCaptureRefusal>()),

                CreateTextureAssignment(
                    "test:main_tex_no_sampling",
                    hasSampling: false,
                    sampling: default,
                    hasAlpha: false,
                    hasRed: false),
            };

            foreach (var mainAssignment in invalidMainAssignments)
            {
                var evidence = CreateLayerEvidence(
                    mainAssignment: mainAssignment,
                    layerAssignment: layerAssignment,
                    blendMaskAssignment: blendMaskAssignment);
                var diagnostics = new List<LilToonSemanticDiagnostic>();

                var term = LilToonLayerAlphaTerm.Interpret(
                    evidence,
                    third: false,
                    diagnostics: diagnostics);

                Assert.That(
                    term.Kind,
                    Is.EqualTo(LilToonLayerAlphaTermKind.Refused));
                Assert.That(
                    diagnostics.Any(d =>
                        d.Code == LilToonSemanticDiagnosticCode.UnsupportedFeature &&
                        d.Detail == "_MainTex"),
                    Is.True);
            }

            {
                var evidenceWithoutMain = CreateLayerEvidence(
                    mainAssignment: null,
                    layerAssignment: layerAssignment,
                    blendMaskAssignment: blendMaskAssignment);
                var diagnostics = new List<LilToonSemanticDiagnostic>();

                var term = LilToonLayerAlphaTerm.Interpret(
                    evidenceWithoutMain,
                    third: false,
                    diagnostics: diagnostics);

                Assert.That(
                    term.Kind,
                    Is.EqualTo(LilToonLayerAlphaTermKind.Refused));
                Assert.That(
                    diagnostics.Any(d =>
                        d.Code == LilToonSemanticDiagnosticCode.UnsupportedFeature &&
                        d.Detail == "_MainTex"),
                    Is.True);
            }
        }

        [Test]
        public void LayerBlendMask_RequestedButAbsentFromEvidence_RefusesInsteadOfDroppingFactor()
        {
            var evidence = CreateLayerEvidenceWithBlendMaskEntryAbsent();
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var term = LilToonLayerAlphaTerm.Interpret(
                evidence, third: false, diagnostics);

            Assert.That(
                term.Kind,
                Is.EqualTo(LilToonLayerAlphaTermKind.Refused));
            Assert.That(
                diagnostics.Any(d =>
                    d.Code == LilToonSemanticDiagnosticCode.UnsupportedFeature &&
                    d.Detail == "_Main2ndBlendMask"),
                Is.True);
        }

        // --- third layer: the same equation under the 3rd spellings --------

        /// <summary>
        /// The standard third-layer evidence: main, third texture, and
        /// third blend mask assignments on the admitted shape, every gate
        /// at its exact-off value.
        /// </summary>
        private static CapturedMaterialEvidence
            CreateStandardThirdLayerEvidence()
        {
            return CreateLayerEvidence(
                mainAssignment: CreateTextureAssignment(
                    "test:main_tex",
                    hasSampling: true,
                    sampling: new TextureSampling(
                        TextureFilterMode.Bilinear,
                        TextureWrapMode.Clamp),
                    hasAlpha: false,
                    hasRed: false),
                layerAssignment: CreateTextureAssignment(
                    "test:main_3rd_tex",
                    hasSampling: true,
                    sampling: new TextureSampling(
                        TextureFilterMode.Bilinear,
                        TextureWrapMode.Repeat),
                    hasAlpha: true,
                    hasRed: false),
                blendMaskAssignment: CreateTextureAssignment(
                    "test:main_3rd_blend_mask",
                    hasSampling: false,
                    sampling: default,
                    hasAlpha: false,
                    hasRed: true),
                third: true);
        }

        private static void AssertThirdLayerRefusal(
            CapturedMaterialEvidence evidence,
            string expectedDetail)
        {
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var term = LilToonLayerAlphaTerm.Interpret(
                evidence, third: true, diagnostics);

            Assert.That(
                term.Kind,
                Is.EqualTo(LilToonLayerAlphaTermKind.Refused));
            Assert.That(
                diagnostics.Any(d =>
                    d.Code == LilToonSemanticDiagnosticCode.UnsupportedFeature &&
                    d.Detail == expectedDetail),
                Is.True,
                $"expected a refusal naming {expectedDetail}");
        }

        [Test]
        public void ThirdLayer_ComposesTheChainUnderTheThirdSpellings()
        {
            var evidence = CreateStandardThirdLayerEvidence();
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var term = LilToonLayerAlphaTerm.Interpret(
                evidence, third: true, diagnostics);

            Assert.That(term.Kind, Is.EqualTo(LilToonLayerAlphaTermKind.Field));
            Assert.That(
                term.Value.Kind,
                Is.EqualTo(ScalarSemanticValueKind.ProductChainOfTextureSamples));
            Assert.That(term.Value.GetChainFactorCount(), Is.EqualTo(2));

            var layerSample = term.Value.GetChainSample(0);
            Assert.That(
                layerSample.Sampling.Wrap,
                Is.EqualTo(TextureWrapMode.Repeat));

            var blendMaskSample = term.Value.GetChainSample(1);
            Assert.That(
                blendMaskSample.Sampling.Wrap,
                Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void ThirdLayer_DecalFlags_RefuseFailClosed()
        {
            var decalFlags = new[]
            {
                "_Main3rdTexIsDecal",
                "_Main3rdTexIsLeftOnly",
                "_Main3rdTexIsRightOnly",
                "_Main3rdTexShouldCopy",
                "_Main3rdTexShouldFlipMirror",
                "_Main3rdTexShouldFlipCopy",
            };

            foreach (var flag in decalFlags)
            {
                AssertThirdLayerRefusal(
                    CreateStandardThirdLayerEvidence().WithScalar(flag, 1f),
                    flag);
            }
        }

        [Test]
        public void ThirdLayer_MsdfFlag_RefusesFailClosed()
        {
            AssertThirdLayerRefusal(
                CreateStandardThirdLayerEvidence()
                    .WithScalar("_Main3rdTexIsMSDF", 1f),
                "_Main3rdTexIsMSDF");
        }

        [Test]
        public void ThirdLayer_AudioLinkToggle_RefusesFailClosed()
        {
            AssertThirdLayerRefusal(
                CreateStandardThirdLayerEvidence()
                    .WithScalar("_AudioLink2Main3rd", 1f),
                "_AudioLink2Main3rd");
        }

        [Test]
        public void ThirdLayer_Cull_RefusesFailClosed()
        {
            AssertThirdLayerRefusal(
                CreateStandardThirdLayerEvidence()
                    .WithScalar("_Main3rdTex_Cull", 1f),
                "_Main3rdTex_Cull");
        }

        [Test]
        public void ThirdLayer_DissolveModeOn_RefusesFailClosed()
        {
            // A rounded mode of exactly one turns the masked threshold on.
            AssertThirdLayerRefusal(
                CreateStandardThirdLayerEvidence()
                    .WithVector("_Main3rdDissolveParams",
                        new Vector4(1f, 0f, 0.5f, 0.1f)),
                "_Main3rdDissolveParams");

            // The rounding boundary: half rounds to even, which the shader
            // leaves off, so the gate must admit it rather than refuse.
            var admitted = CreateStandardThirdLayerEvidence()
                .WithVector("_Main3rdDissolveParams",
                    new Vector4(0.5f, 0f, 0.5f, 0.1f));
            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var term = LilToonLayerAlphaTerm.Interpret(
                admitted, third: true, diagnostics);

            Assert.That(
                term.Kind,
                Is.EqualTo(LilToonLayerAlphaTermKind.Field),
                "half rounds to even, which stays off");
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void ThirdLayer_DistanceFadeStrength_RefusesFailClosed()
        {
            AssertThirdLayerRefusal(
                CreateStandardThirdLayerEvidence()
                    .WithVector("_Main3rdDistanceFade",
                        new Vector4(0.1f, 0.01f, 0.5f, 0f)),
                "_Main3rdDistanceFade");
        }

        [Test]
        public void ThirdLayer_AlphaMode_OutOfRangeStaysInertAndBrokenValuesRefuse()
        {
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            // Modes above four write nothing, exactly like mode zero.
            foreach (var outOfRange in new[] { 5f, 6f })
            {
                var inert = CreateStandardThirdLayerEvidence()
                    .WithScalar("_Main3rdTexAlphaMode", outOfRange);
                var inertTerm = LilToonLayerAlphaTerm.Interpret(
                    inert, third: true, diagnostics);

                Assert.That(
                    inertTerm.Kind,
                    Is.EqualTo(LilToonLayerAlphaTermKind.Inert),
                    $"alpha mode {outOfRange} writes no alpha");
            }

            Assert.That(diagnostics, Is.Empty);

            // Negative and fractional modes are captures the proof cannot
            // pin to a writer, so they refuse instead of dropping in.
            foreach (var broken in new[] { -1f, 0.5f, float.NaN })
            {
                AssertThirdLayerRefusal(
                    CreateStandardThirdLayerEvidence()
                        .WithScalar("_Main3rdTexAlphaMode", broken),
                    "_Main3rdTexAlphaMode");
            }
        }

        [Test]
        public void ThirdLayer_NonFiniteColorAlpha_RefusesFailClosed()
        {
            foreach (var alpha in new[] { float.NaN, float.PositiveInfinity })
            {
                AssertThirdLayerRefusal(
                    CreateStandardThirdLayerEvidence()
                        .WithColor("_Color3rd", new Color(1f, 1f, 1f, alpha)),
                    "_Color3rd");
            }
        }

        [Test]
        public void ThirdLayer_SampledBlendMask_WithUnassignedMain_RefusesNamingMainTex()
        {
            // The blend mask borrows _MainTex's sampling, so an unassigned
            // main must refuse by name instead of proving through facts
            // nobody captured.
            var layerAssignment = CreateTextureAssignment(
                "test:main_3rd_tex",
                hasSampling: true,
                sampling: new TextureSampling(
                    TextureFilterMode.Bilinear,
                    TextureWrapMode.Repeat),
                hasAlpha: true,
                hasRed: false);
            var blendMaskAssignment = CreateTextureAssignment(
                "test:main_3rd_blend_mask",
                hasSampling: false,
                sampling: default,
                hasAlpha: false,
                hasRed: true);
            var unassignedMain = new CapturedTextureAssignment(
                isAssigned: false,
                requestedEvidence:
                    TextureEvidenceKinds.SourceIdentity |
                    TextureEvidenceKinds.Sampling,
                hasScaleOffset: true,
                scale: Vector2.one,
                offset: Vector2.zero,
                texture: null,
                captureRefusals: Array.Empty<TextureCaptureRefusal>());

            var evidence = CreateLayerEvidence(
                mainAssignment: unassignedMain,
                layerAssignment: layerAssignment,
                blendMaskAssignment: blendMaskAssignment,
                third: true);

            AssertThirdLayerRefusal(evidence, "_MainTex");
        }

        /// <summary>
        /// Builds the standard layer evidence with one change: the
        /// _Main2ndBlendMask entry stays requested but holds no value,
        /// exactly as capture records a requested texture slot absent from
        /// the material.
        /// </summary>
        private static CapturedMaterialEvidence
            CreateLayerEvidenceWithBlendMaskEntryAbsent()
        {
            return CreateLayerEvidence(
                mainAssignment: CreateTextureAssignment(
                    "test:main_tex",
                    hasSampling: true,
                    sampling: new TextureSampling(
                        TextureFilterMode.Bilinear,
                        TextureWrapMode.Clamp),
                    hasAlpha: false,
                    hasRed: false),
                layerAssignment: CreateTextureAssignment(
                    "test:main_2nd_tex",
                    hasSampling: true,
                    sampling: new TextureSampling(
                        TextureFilterMode.Bilinear,
                        TextureWrapMode.Repeat),
                    hasAlpha: true,
                    hasRed: false),
                blendMaskAssignment: CreateTextureAssignment(
                    "test:main_2nd_blend_mask",
                    hasSampling: false,
                    sampling: default,
                    hasAlpha: false,
                    hasRed: true),
                blendMaskEntryHasValue: false);
        }

        private static CapturedTextureAssignment CreateTextureAssignment(
            string sourceIdentity,
            bool hasSampling,
            TextureSampling sampling,
            bool hasAlpha,
            bool hasRed)
        {
            var textureEvidence = new CapturedTextureEvidence(
                hasSourceIdentity: true,
                sourceIdentity: new TextureSourceId(sourceIdentity),
                captureThreshold: 1f,
                captureBounds: AlphaPolicyBounds.Inert,
                hasSampling: hasSampling,
                sampling: sampling,
                hasColorInterpretation: false,
                colorInterpretation: default,
                sampledAlphaIsProvenOne: false,
                isCanonicalNormalMap: false,
                colorValuesProvenInUnitRange: false,
                hasAlphaChannel: hasAlpha,
                alphaChannel: null,
                alphaCaptureRefusal: TextureCaptureRefusalReason.None,
                hasRedChannel: hasRed,
                redChannel: null,
                redCaptureRefusal: TextureCaptureRefusalReason.None);

            return new CapturedTextureAssignment(
                isAssigned: true,
                requestedEvidence: TextureEvidenceKinds.SourceIdentity |
                    (hasSampling ? TextureEvidenceKinds.Sampling : TextureEvidenceKinds.None) |
                    (hasAlpha ? TextureEvidenceKinds.AlphaChannel : TextureEvidenceKinds.None) |
                    (hasRed ? TextureEvidenceKinds.RedChannel : TextureEvidenceKinds.None),
                hasScaleOffset: true,
                scale: Vector2.one,
                offset: Vector2.zero,
                texture: textureEvidence,
                captureRefusals: Array.Empty<TextureCaptureRefusal>());
        }

        private static CapturedMaterialEvidence CreateLayerEvidence(
            CapturedTextureAssignment? mainAssignment,
            CapturedTextureAssignment layerAssignment,
            CapturedTextureAssignment? blendMaskAssignment,
            bool blendMaskEntryHasValue = true,
            bool third = false)
        {
            var token = third ? "3rd" : "2nd";
            var textureEntries = new List<CapturedMaterialEvidence.TextureEntry>();
            var textures = new List<CapturedTextureEvidence>();

            if (mainAssignment.HasValue)
            {
                textureEntries.Add(
                    new CapturedMaterialEvidence.TextureEntry(
                        "_MainTex",
                        true,
                        mainAssignment.Value));
                if (mainAssignment.Value.Texture != null)
                {
                    textures.Add(mainAssignment.Value.Texture);
                }
            }

            textureEntries.Add(
                new CapturedMaterialEvidence.TextureEntry(
                    "_Main" + token + "Tex",
                    true,
                    layerAssignment));
            if (layerAssignment.Texture != null)
            {
                textures.Add(layerAssignment.Texture);
            }

            if (blendMaskAssignment.HasValue)
            {
                textureEntries.Add(
                    new CapturedMaterialEvidence.TextureEntry(
                        "_Main" + token + "BlendMask",
                        blendMaskEntryHasValue,
                        blendMaskEntryHasValue
                            ? blendMaskAssignment.Value
                            : default));
                if (blendMaskEntryHasValue &&
                    blendMaskAssignment.Value.Texture != null)
                {
                    textures.Add(blendMaskAssignment.Value.Texture);
                }
            }

            var scalars = new[]
            {
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UseMain" + token + "Tex", true, 1f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexAlphaMode", true, 1f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "Tex_UVMode", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexAngle", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "Tex_Cull", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexIsDecal", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexIsLeftOnly", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexIsRightOnly", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexShouldCopy", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexShouldFlipMirror", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexShouldFlipCopy", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main" + token + "TexIsMSDF", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_AudioLink2Main" + token, true, 0f),
            };

            var vectors = new[]
            {
                new CapturedMaterialEvidence.VectorEntry(
                    "_Main" + token + "Tex_ScrollRotate",
                    true, Vector4.zero),
                new CapturedMaterialEvidence.VectorEntry(
                    "_Main" + token + "DistanceFade", true, Vector4.zero),
                new CapturedMaterialEvidence.VectorEntry(
                    "_Main" + token + "DissolveParams", true, Vector4.zero),
            };

            var colors = new[]
            {
                new CapturedMaterialEvidence.ColorEntry(
                    "_Color" + token, true, Color.white),
            };

            return new CapturedMaterialEvidence(
                hasShaderName: false,
                shaderName: null,
                hasActiveColorSpace: false,
                activeColorSpace: default,
                presence: Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                scalars: scalars,
                colors: colors,
                vectors: vectors,
                textureAssignments: textureEntries.ToArray(),
                textures: textures.ToArray());
        }
    }
}
