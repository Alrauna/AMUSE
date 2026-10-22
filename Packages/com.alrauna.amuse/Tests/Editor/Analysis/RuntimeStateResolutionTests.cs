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
    /// resolves them. Later tests characterize slot attribution and the
    /// per-slot outcomes the resolver returns beside them.
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

        [Test]
        public void TypedStructuralBindingRefusesOnlyItsOwnRendererType()
        {
            var typed = new CapturedAnimationEvidence(
                EmptyRequest(),
                new[]
                {
                    new CapturedClipEvidence(
                        "SlotCount", false, false,
                        Array.Empty<CapturedFloatBinding>(),
                        new[]
                        {
                            new CapturedObjectBinding(
                                "Body",
                                typeof(MeshRenderer).FullName,
                                "m_Materials.Array.size",
                                new[] { 1 }),
                        }),
                },
                new[] { ScalarMaterial() },
                new[] { 0 },
                false);

            var refused = Resolve(typed, typeof(MeshRenderer).FullName);

            Assert.That(refused.Refusal, Is.EqualTo(
                RendererAnalysisRefusal.AnimatedMaterialSlotCount),
                "a slot-count curve on the analyzed renderer's own type " +
                "keeps the structural refusal");
            Assert.That(refused.SlotResults, Is.Empty,
                "a renderer-wide refusal happens before any slot resolves");
            Assert.That(refused.Slots, Is.Empty,
                "a renderer-wide refusal resolves no slot evidence");
            Assert.That(refused.CurrentMaterials, Is.Empty,
                "a renderer-wide refusal keeps no current material");

            var foreign = new CapturedAnimationEvidence(
                EmptyRequest(),
                new[]
                {
                    new CapturedClipEvidence(
                        "SlotCount", false, false,
                        Array.Empty<CapturedFloatBinding>(),
                        new[]
                        {
                            new CapturedObjectBinding(
                                "Body",
                                typeof(SkinnedMeshRenderer).FullName,
                                "m_Materials.Array.size",
                                new[] { 1 }),
                        }),
                },
                new[] { NamedMaterial("Current") },
                new[] { 0 },
                false);

            var resolved = Resolve(
                foreign, typeof(MeshRenderer).FullName, OpaqueSemantics);

            Assert.That(resolved.Refusal, Is.EqualTo(
                RendererAnalysisRefusal.None),
                "the same structural curve typed to another renderer " +
                "binds nothing here");
            Assert.That(resolved.SlotResults, Has.Length.EqualTo(1));
            Assert.That(resolved.SlotResults[0].IsResolved, Is.True);
        }

        [Test]
        public void TypedMaterialSwapAdmitsBesideTheCurrentMaterialAndBothResolve()
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
                new[] { NamedMaterial("Current"), NamedMaterial("Swap") },
                new[] { 0 },
                false);

            var resolved = Resolve(
                evidence, typeof(MeshRenderer).FullName, OpaqueSemantics);

            Assert.That(resolved.Refusal, Is.EqualTo(
                RendererAnalysisRefusal.None));

            Assert.That(resolved.CurrentMaterials.Count, Is.EqualTo(1));
            Assert.That(resolved.CurrentMaterials[0].MaterialName,
                Is.EqualTo("Current"),
                "the result's current material holds the slot's current " +
                "assignment, not the swap");

            var slots = resolved.Slots;
            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].SlotIndex, Is.EqualTo(0));
            Assert.That(slots[0].AdmittedMaterialIndices,
                Is.EqualTo(new[] { 0, 1 }),
                "the slot holds its current material first and then the " +
                "swap alternative");

            var slot = resolved.SlotResults[0];
            Assert.That(slot.IsResolved, Is.True);
            Assert.That(slot.MaterialResolutions.Count, Is.EqualTo(2),
                "the raw list keeps one resolution per admitted material " +
                "in admitted order");
            Assert.That(slot.Resolutions.Count, Is.EqualTo(1),
                "two agreeing constant-one resolutions collapse to one " +
                "classified resolution");
            Assert.That(
                slot.Resolutions[0].TryGetUniformOutcome(out var outcome),
                Is.True);
            Assert.That(outcome, Is.EqualTo(
                TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void ClosureFailedSlotKeepsItsNullCurrentEntryWhileTheSiblingResolves()
        {
            var evidence = new CapturedAnimationEvidence(
                ScalarAlphaRequest(),
                Array.Empty<CapturedClipEvidence>(),
                new[] { NamedMaterial("Sibling") },
                new[] { -1, 0 },
                false,
                slotClosureFailures: new[]
                {
                    new SlotClosureFailure(
                        0,
                        MaterialDependencyClosureFailure
                            .MissingCurrentMaterial),
                });

            var resolved = Resolve(
                evidence, typeof(MeshRenderer).FullName, OpaqueSemantics);

            Assert.That(resolved.Refusal, Is.EqualTo(
                RendererAnalysisRefusal.None),
                "one slot's closure failure stays scoped to that slot and " +
                "keeps the sibling convertible");

            Assert.That(resolved.CurrentMaterials.Count, Is.EqualTo(2));
            Assert.That(resolved.CurrentMaterials[0], Is.Null,
                "the closure-failed position keeps its inert null entry");
            Assert.That(resolved.CurrentMaterials[1].MaterialName,
                Is.EqualTo("Sibling"),
                "the sibling keeps its own current material");

            var slots = resolved.Slots;
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].AdmittedMaterialIndices, Is.Empty,
                "a closure-failed slot keeps no admitted material");
            Assert.That(slots[1].AdmittedMaterialIndices,
                Is.EqualTo(new[] { 0 }));

            Assert.That(resolved.SlotResults, Has.Length.EqualTo(2));
            Assert.That(resolved.SlotResults[0].IsResolved, Is.False);
            Assert.That(resolved.SlotResults[0].Refusal, Is.EqualTo(
                RendererAnalysisRefusal.MaterialDependencyClosureFailed));
            Assert.That(resolved.SlotResults[0].ClosureWay, Is.EqualTo(
                MaterialDependencyClosureFailure.MissingCurrentMaterial));
            Assert.That(resolved.SlotResults[0].MaterialResolutions,
                Is.Empty,
                "a refused slot exposes no partial resolutions");

            Assert.That(resolved.SlotResults[1].IsResolved, Is.True);
            Assert.That(
                resolved.SlotResults[1].MaterialResolutions.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void AllSlotsClosureFailedKeepsEverySlotOwnRefusalAndWay()
        {
            var evidence = new CapturedAnimationEvidence(
                ScalarAlphaRequest(),
                Array.Empty<CapturedClipEvidence>(),
                Array.Empty<CapturedAlphaMaterial>(),
                new[] { -1, -1 },
                false,
                slotClosureFailures: new[]
                {
                    new SlotClosureFailure(
                        0,
                        MaterialDependencyClosureFailure
                            .MissingCurrentMaterial),
                    new SlotClosureFailure(
                        1,
                        MaterialDependencyClosureFailure.InvalidSwapValue),
                });

            var resolved = Resolve(evidence, typeof(MeshRenderer).FullName);

            Assert.That(resolved.Refusal, Is.EqualTo(
                RendererAnalysisRefusal.MaterialDependencyClosureFailed),
                "the first slot's refusal is the renderer's reported " +
                "refusal");
            Assert.That(resolved.CurrentMaterials, Is.Empty,
                "an all-slots-refused renderer keeps no current material");

            Assert.That(resolved.SlotResults, Has.Length.EqualTo(2));
            Assert.That(resolved.SlotResults[0].IsResolved, Is.False);
            Assert.That(resolved.SlotResults[0].ClosureWay, Is.EqualTo(
                MaterialDependencyClosureFailure.MissingCurrentMaterial));
            Assert.That(resolved.SlotResults[1].IsResolved, Is.False);
            Assert.That(resolved.SlotResults[1].ClosureWay, Is.EqualTo(
                MaterialDependencyClosureFailure.InvalidSwapValue));

            var slots = resolved.Slots;
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].AdmittedMaterialIndices, Is.Empty);
            Assert.That(slots[1].AdmittedMaterialIndices, Is.Empty);
        }

        private static RuntimeStateResolution.ResolvedRuntimeStates Resolve(
            CapturedAnimationEvidence evidence,
            string rendererTypeName,
            CapturedAlphaMaterialSemanticsResolver resolveSemantics = null)
        {
            return RuntimeStateResolution.ResolveRuntimeStates(
                "Body",
                evidence,
                AlphaSamplingPolicy.ConservativeDesktop,
                EmptyAlphaFields(),
                UnityAnimationEvidenceCapture.TextureScaleOffsetSuffix,
                UnityRendererAlphaAnalysis.StructuralRefusal,
                UnityAnimationEvidenceCapture.ProofRelevantResolver,
                UnityAnimationEvidenceCapture.UnrecognizedObjectBinding,
                UnityAnimationEvidenceCapture.MaterialSlotBindingParser,
                resolveSemantics,
                rendererTypeName: rendererTypeName);
        }

        private static CapturedAlphaSemantics OpaqueSemantics(
            CapturedAlphaMaterial material)
        {
            return new CapturedAlphaSemantics(
                new MaterialSemantics(
                    SemanticOutput<ScalarSemanticValue>.Complete(
                        ScalarSemanticValue.Constant(1f))),
                null);
        }

        private static CapturedAlphaMaterial NamedMaterial(string name)
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
                null,
                materialName: name);
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
