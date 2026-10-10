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
            bool blendMaskEntryHasValue = true)
        {
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
                    "_Main2ndTex",
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
                        "_Main2ndBlendMask",
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
                new CapturedMaterialEvidence.ScalarEntry("_UseMain2ndTex", true, 1f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexAlphaMode", true, 1f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTex_UVMode", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexAngle", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTex_Cull", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexIsDecal", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexIsLeftOnly", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexIsRightOnly", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexShouldCopy", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexShouldFlipMirror", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexShouldFlipCopy", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_Main2ndTexIsMSDF", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry("_AudioLink2Main2nd", true, 0f),
            };

            var vectors = new[]
            {
                new CapturedMaterialEvidence.VectorEntry("_Main2ndTex_ScrollRotate", true, Vector4.zero),
                new CapturedMaterialEvidence.VectorEntry("_Main2ndDistanceFade", true, Vector4.zero),
                new CapturedMaterialEvidence.VectorEntry("_Main2ndDissolveParams", true, Vector4.zero),
            };

            var colors = new[]
            {
                new CapturedMaterialEvidence.ColorEntry("_Color2nd", true, Color.white),
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
