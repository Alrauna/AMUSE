using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Unit tests for finiteness validation in lilToon alpha mask interpretation.
    /// </summary>
    public sealed class LilToonAlphaMaskSemanticsTests
    {
        [Test]
        public void AlphaMask_WithNaNScale_RefusesWithoutException()
        {
            var nonFiniteScales = new[]
            {
                new Vector2(float.NaN, 1f),
                new Vector2(1f, float.NaN),
                new Vector2(float.NaN, float.NaN),
            };

            foreach (var scale in nonFiniteScales)
            {
                var evidence = CreateMaskEvidence(scale, Vector2.zero);
                var diagnostics = new List<LilToonSemanticDiagnostic>();

                LilToonAlphaMaskTerm term = default;
                Assert.DoesNotThrow(() =>
                {
                    term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);
                });

                Assert.That(term.Kind, Is.EqualTo(LilToonAlphaMaskTermKind.Refused));
                Assert.That(
                    term.RefusalCode,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
                Assert.That(term.RefusalDetail, Is.EqualTo("_AlphaMask"));
                Assert.That(diagnostics.Count, Is.EqualTo(1));
                Assert.That(
                    diagnostics[0].Code,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
                Assert.That(diagnostics[0].Detail, Is.EqualTo("_AlphaMask"));
            }
        }

        [Test]
        public void AlphaMask_WithInfiniteOffset_RefusesWithoutException()
        {
            var infiniteOffsets = new[]
            {
                new Vector2(float.PositiveInfinity, 0f),
                new Vector2(0f, float.PositiveInfinity),
                new Vector2(float.NegativeInfinity, 0f),
                new Vector2(0f, float.NegativeInfinity),
                new Vector2(float.PositiveInfinity, float.NegativeInfinity),
            };

            foreach (var offset in infiniteOffsets)
            {
                var evidence = CreateMaskEvidence(Vector2.one, offset);
                var diagnostics = new List<LilToonSemanticDiagnostic>();

                LilToonAlphaMaskTerm term = default;
                Assert.DoesNotThrow(() =>
                {
                    term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);
                });

                Assert.That(term.Kind, Is.EqualTo(LilToonAlphaMaskTermKind.Refused));
                Assert.That(
                    term.RefusalCode,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
                Assert.That(term.RefusalDetail, Is.EqualTo("_AlphaMask"));
                Assert.That(diagnostics.Count, Is.EqualTo(1));
                Assert.That(
                    diagnostics[0].Code,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
                Assert.That(diagnostics[0].Detail, Is.EqualTo("_AlphaMask"));
            }
        }

        [Test]
        public void AlphaMaskWithNonFiniteScaleOrOffsetRefusesWithoutException()
        {
            var nonFinitePairs = new (Vector2 scale, Vector2 offset)[]
            {
                (new Vector2(float.NaN, 1f), Vector2.zero),
                (new Vector2(1f, float.NaN), Vector2.zero),
                (new Vector2(float.PositiveInfinity, 1f), Vector2.zero),
                (new Vector2(1f, float.NegativeInfinity), Vector2.zero),
                (Vector2.one, new Vector2(float.NaN, 0f)),
                (Vector2.one, new Vector2(0f, float.NaN)),
                (Vector2.one, new Vector2(float.PositiveInfinity, 0f)),
                (Vector2.one, new Vector2(0f, float.NegativeInfinity)),
            };

            foreach (var pair in nonFinitePairs)
            {
                var evidence = CreateMaskEvidence(pair.scale, pair.offset);
                var diagnostics = new List<LilToonSemanticDiagnostic>();

                LilToonAlphaMaskTerm term = default;
                Assert.DoesNotThrow(() =>
                {
                    term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);
                });

                Assert.That(term.Kind, Is.EqualTo(LilToonAlphaMaskTermKind.Refused));
                Assert.That(
                    term.RefusalCode,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
                Assert.That(term.RefusalDetail, Is.EqualTo("_AlphaMask"));
                Assert.That(diagnostics.Count, Is.EqualTo(1));
                Assert.That(
                    diagnostics[0].Code,
                    Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedFeature));
                Assert.That(diagnostics[0].Detail, Is.EqualTo("_AlphaMask"));
            }
        }

        [Test]
        public void AlphaMask_UnassignedWithSubOneMultiplyConstant_YieldsConstantMultiplier()
        {
            var evidence = CreateMaskEvidence(
                Vector2.one, Vector2.zero,
                mode: 2f, maskScale: 0.5f, maskValue: 0f,
                isAssigned: false);
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);

            Assert.That(
                term.Kind,
                Is.EqualTo(LilToonAlphaMaskTermKind.ConstantMultiplier));
            Assert.That(term.Constant, Is.EqualTo(0.5f));
            Assert.That(term.ReplacesMainAlpha, Is.False);
            Assert.That(diagnostics, Is.Empty);
        }

        [Test]
        public void AlphaMask_AssignedZeroScaleWithoutIdentity_YieldsConstantMultiplierWithoutIdentityDemand()
        {
            var evidence = CreateMaskEvidenceWithoutSourceIdentity(
                mode: 2f, maskScale: 0f, maskValue: 0.4f);
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var term = LilToonAlphaMaskTerm.Interpret(evidence, diagnostics);

            Assert.That(
                term.Kind,
                Is.EqualTo(LilToonAlphaMaskTermKind.ConstantMultiplier));
            Assert.That(term.Constant, Is.EqualTo(0.4f));
            Assert.That(diagnostics, Is.Empty);
        }

        private static CapturedMaterialEvidence CreateMaskEvidence(
            Vector2 scale,
            Vector2 offset,
            float mode = 1f,
            float maskScale = 1f,
            float maskValue = 0f,
            bool isAssigned = true,
            bool hasSourceIdentity = true)
        {
            var textureEvidence = new CapturedTextureEvidence(
                hasSourceIdentity: hasSourceIdentity,
                sourceIdentity: new TextureSourceId("test:alpha_mask"),
                captureThreshold: 1f,
                captureBounds: AlphaPolicyBounds.Inert,
                hasSampling: false,
                sampling: default,
                hasColorInterpretation: false,
                colorInterpretation: default,
                sampledAlphaIsProvenOne: false,
                isCanonicalNormalMap: false,
                colorValuesProvenInUnitRange: false,
                hasAlphaChannel: false,
                alphaChannel: null,
                alphaCaptureRefusal: TextureCaptureRefusalReason.None,
                hasRedChannel: false,
                redChannel: null,
                redCaptureRefusal: TextureCaptureRefusalReason.None);

            var maskAssignment = new CapturedTextureAssignment(
                isAssigned: isAssigned,
                requestedEvidence: TextureEvidenceKinds.SourceIdentity,
                hasScaleOffset: true,
                scale: scale,
                offset: offset,
                texture: textureEvidence,
                captureRefusals: Array.Empty<TextureCaptureRefusal>());

            return new CapturedMaterialEvidence(
                hasShaderName: false,
                shaderName: null,
                hasActiveColorSpace: false,
                activeColorSpace: default,
                presence: Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                scalars: new[]
                {
                    new CapturedMaterialEvidence.ScalarEntry("_AlphaMaskMode", true, mode),
                    new CapturedMaterialEvidence.ScalarEntry("_AlphaMaskScale", true, maskScale),
                    new CapturedMaterialEvidence.ScalarEntry("_AlphaMaskValue", true, maskValue),
                },
                colors: Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                vectors: Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                textureAssignments: new[]
                {
                    new CapturedMaterialEvidence.TextureEntry("_AlphaMask", true, maskAssignment),
                },
                textures: new[] { textureEvidence });
        }

        private static CapturedMaterialEvidence CreateMaskEvidenceWithoutSourceIdentity(
            float mode,
            float maskScale,
            float maskValue)
        {
            return CreateMaskEvidence(
                Vector2.one, Vector2.zero,
                mode: mode,
                maskScale: maskScale,
                maskValue: maskValue,
                hasSourceIdentity: false);
        }
    }
}
