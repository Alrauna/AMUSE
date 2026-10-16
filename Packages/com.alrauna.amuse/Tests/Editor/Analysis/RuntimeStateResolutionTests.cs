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
                false,
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
                false);

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

        [Test]
        public void AdditiveSourceFloatBindingRefusesAndOverrideSourceDoesNot()
        {
            var additiveSource = new CapturedAnimationEvidence(
                ScalarAlphaRequest(),
                new[]
                {
                    new CapturedClipEvidence(
                        "AdditiveClip", false, true,
                        new[] { RendererWideFloatBinding() },
                        Array.Empty<CapturedObjectBinding>()),
                },
                new[] { ScalarMaterial() },
                new[] { 0 },
                false);
            var overrideSource = new CapturedAnimationEvidence(
                ScalarAlphaRequest(),
                new[]
                {
                    new CapturedClipEvidence(
                        "OverrideClip", false, false,
                        new[] { RendererWideFloatBinding() },
                        Array.Empty<CapturedObjectBinding>()),
                },
                new[] { ScalarMaterial() },
                new[] { 0 },
                false);

            var additiveResolved = RuntimeStateResolution.ResolveRuntimeStates(
                "Body",
                additiveSource,
                AlphaSamplingPolicy.ConservativeDesktop,
                EmptyAlphaFields(),
                UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                UnityRendererAlphaAnalysis.StructuralRefusal,
                UnityAnimationEvidenceCapture.ProofRelevantResolver,
                UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser);
            var overrideResolved = RuntimeStateResolution.ResolveRuntimeStates(
                "Body",
                overrideSource,
                AlphaSamplingPolicy.ConservativeDesktop,
                EmptyAlphaFields(),
                UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                UnityRendererAlphaAnalysis.StructuralRefusal,
                UnityAnimationEvidenceCapture.ProofRelevantResolver,
                UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser);

            Assert.That(additiveResolved.Refusal, Is.EqualTo(
                RendererAnalysisRefusal
                    .AdditiveLayerWithProofRelevantMaterialProperty),
                "a float binding from an additive-source clip kept the " +
                "renderer's singleton property unprovable");
            Assert.That(overrideResolved.Refusal, Is.Not.EqualTo(
                RendererAnalysisRefusal
                    .AdditiveLayerWithProofRelevantMaterialProperty),
                "the identical binding from an override-source clip refused " +
                "like an additive one");
            Assert.That(overrideResolved.Refusal,
                Is.EqualTo(overrideResolved.SlotResults[0].Refusal),
                "the override-source resolution kept only its slot's own " +
                "refusal");
        }

        [Test]
        public void AdditiveSourceObjectBindingRefuses()
        {
            // --- Falsifier 2: an implementation that narrows the renderer predicate but forgets the object-binding pass leaves the additive material swap admitted. This test must fail it. ---
            var evidence = new CapturedAnimationEvidence(
                ScalarAlphaRequest(),
                new[]
                {
                    new CapturedClipEvidence(
                        "AdditiveSwap", false, true,
                        Array.Empty<CapturedFloatBinding>(),
                        new[]
                        {
                            new CapturedObjectBinding(
                                "Body",
                                typeof(MeshRenderer).FullName,
                                "m_Materials.Array.data[0]",
                                new[] { 1 }),
                        }),
                },
                new[] { UnsupportedMaterial(), UnsupportedMaterial() },
                new[] { 0 },
                false);

            Assert.That(
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser(
                    "Body",
                    typeof(MeshRenderer).FullName,
                    "m_Materials.Array.data[0]",
                    "Body",
                    null,
                    out var parsedSlot),
                Is.True,
                "fixture precondition: the object binding did not parse as " +
                "a material-slot binding");
            Assert.That(parsedSlot, Is.EqualTo(0),
                "fixture precondition: the object binding parsed against " +
                "another slot");

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
                RendererAnalysisRefusal
                    .AdditiveLayerWithProofRelevantMaterialProperty),
                "an additive-source material swap was admitted instead of " +
                "refusing the renderer");
        }

        [Test]
        public void OverrideSourceObjectBindingStillAdmitsSwap()
        {
            var evidence = new CapturedAnimationEvidence(
                ScalarAlphaRequest(),
                new[]
                {
                    new CapturedClipEvidence(
                        "OverrideSwap", false, false,
                        Array.Empty<CapturedFloatBinding>(),
                        new[]
                        {
                            new CapturedObjectBinding(
                                "Body",
                                typeof(MeshRenderer).FullName,
                                "m_Materials.Array.data[0]",
                                new[] { 1 }),
                        }),
                },
                new[] { UnsupportedMaterial(), UnsupportedMaterial() },
                new[] { 0 },
                false);

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

            Assert.That(resolved.Refusal, Is.Not.EqualTo(
                RendererAnalysisRefusal
                    .AdditiveLayerWithProofRelevantMaterialProperty),
                "an override-source material swap refused like an additive " +
                "one");

            var slots = RuntimeStateResolution.MaterialSlotsFor(
                evidence,
                "Body",
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser);
            Assert.That(slots[0].AdmittedMaterialIndices,
                Is.EqualTo(new[] { 0, 1 }),
                "the override-source swap belongs to the slot admission " +
                "beside the current assignment");
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

        private static MaterialEvidenceRequest ScalarAlphaRequest()
        {
            return new MaterialEvidenceRequest(
                false, false,
                Array.Empty<string>(),
                new[] { "_AlphaForceOpaque" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<TexturePropertyEvidenceRequest>());
        }

        private static CapturedFloatBinding RendererWideFloatBinding()
        {
            return new CapturedFloatBinding(
                "Body",
                typeof(MeshRenderer).FullName,
                "material._AlphaForceOpaque",
                true,
                new[] { 1f });
        }

        private static CapturedAlphaMaterial ScalarMaterial()
        {
            return new CapturedAlphaMaterial(
                CapturedAlphaMaterialFamily.Unsupported,
                new CapturedMaterialEvidence(
                    false, null, false, default(ColorSpace),
                    Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                    new[]
                    {
                        new CapturedMaterialEvidence.ScalarEntry(
                            "_AlphaForceOpaque", true, 1f),
                    },
                    Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                    Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                    Array.Empty<CapturedMaterialEvidence.TextureEntry>(),
                    Array.Empty<CapturedTextureEvidence>()),
                default(PoiyomiSourceEvidence),
                null);
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
