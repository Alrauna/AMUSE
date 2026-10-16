using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    /// <summary>
    /// Direct behavior coverage for the moved runtime-state pipeline over
    /// hand-built Host evidence records: one closure-refused slot and one
    /// unproven slot, each resolved and classified exactly as the barrier
    /// resolves them.
    /// </summary>
    public sealed class RuntimeStateResolutionTests
    {
        [Test]
        public void ClosureRefusedSlotResolvesToClosureFailureResult()
        {
            var evidence = new CapturedAnimationEvidence(
                EmptyRequest(),
                Array.Empty<CapturedClipEvidence>(),
                Array.Empty<CapturedAlphaMaterial>(),
                new[] { -1 },
                false, false,
                slotClosureFailures: new[]
                {
                    new SlotClosureFailure(
                        0,
                        MaterialDependencyClosureFailure
                            .MissingCurrentMaterial),
                });

            var resolved = RuntimeStateResolution.ResolveRuntimeStates(
                "Body",
                evidence,
                AlphaSamplingPolicy.ConservativeDesktop,
                EmptyAlphaFields(),
                UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                UnityRendererAlphaAnalysis.StructuralRefusal,
                UnityAnimationEvidenceCapture.ProofRelevantResolver,
                UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser);

            Assert.That(resolved.Refusal, Is.EqualTo(
                RendererAnalysisRefusal.MaterialDependencyClosureFailed));
            Assert.That(resolved.SlotResults, Has.Length.EqualTo(1));
            Assert.That(resolved.SlotResults[0].IsResolved, Is.False);
            Assert.That(resolved.SlotResults[0].Refusal, Is.EqualTo(
                RendererAnalysisRefusal.MaterialDependencyClosureFailed));
            Assert.That(resolved.SlotResults[0].ClosureWay, Is.EqualTo(
                MaterialDependencyClosureFailure.MissingCurrentMaterial));
        }

        [Test]
        public void UnprovenSlotResolvesToUnprovenOutcomes()
        {
            var evidence = new CapturedAnimationEvidence(
                EmptyRequest(),
                Array.Empty<CapturedClipEvidence>(),
                new[] { UnsupportedMaterial() },
                new[] { 0 },
                false, false);

            var resolved = RuntimeStateResolution.ResolveRuntimeStates(
                "Body",
                evidence,
                AlphaSamplingPolicy.ConservativeDesktop,
                EmptyAlphaFields(),
                UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                UnityRendererAlphaAnalysis.StructuralRefusal,
                UnityAnimationEvidenceCapture.ProofRelevantResolver,
                UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser);
            Assert.That(resolved.SlotResults[0].IsResolved, Is.False);

            var snapshot = new UnityRendererAlphaSnapshot(
                3,
                new[] { Vector3.zero, Vector3.right, Vector3.up },
                Array.Empty<Vector2>(),
                false,
                new[] { new UnitySubmeshAlphaSnapshot(
                    0, 0, new[] { 0, 1, 2 }) },
                Array.Empty<CapturedAlphaMaterial>());
            var plan = RuntimeStateResolution.ClassifyRuntimeStates(
                snapshot,
                resolved.SlotResults,
                UnityRendererAlphaAnalysis.TriangleClassifier,
                UnityRendererAlphaAnalysis.OutcomeIntersector);

            Assert.That(plan.OpaqueTriangleCount, Is.EqualTo(0));
            Assert.That(plan.Submeshes[0].Disposition, Is.EqualTo(
                SubmeshSeparationDisposition.Unchanged));
            Assert.That(plan.Source.Submeshes[0].Outcomes, Has.All.EqualTo(
                TriangleAlphaOutcome.Unknown));
        }

        private static AlphaFieldSet EmptyAlphaFields()
        {
            return new AlphaFieldSet(
                new Dictionary<AlphaFieldKey, AlphaMipChain>());
        }

        private static MaterialEvidenceRequest EmptyRequest()
        {
            return new MaterialEvidenceRequest(
                false, false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());
        }

        private static CapturedAlphaMaterial UnsupportedMaterial()
        {
            return new CapturedAlphaMaterial(
                CapturedAlphaMaterialFamily.Unsupported,
                new CapturedMaterialEvidence(
                    false, null, false, default(ColorSpace),
                    Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                    Array.Empty<CapturedMaterialEvidence.ScalarEntry>(),
                    Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                    Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                    Array.Empty<CapturedMaterialEvidence.TextureEntry>(),
                    Array.Empty<CapturedTextureEvidence>()),
                default(PoiyomiSourceEvidence),
                null);
        }
    }
}
