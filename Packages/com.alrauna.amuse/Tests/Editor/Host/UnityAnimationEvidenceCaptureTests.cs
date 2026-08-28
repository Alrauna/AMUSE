using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityAnimationEvidenceCaptureTests
    {
        private static readonly EditorCurveBinding FloatBinding =
            EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "material._Cutoff");

        private const string UnattestedShaderFolder =
            "Assets/AmuseTests_UnattestedShaders";

        /// <summary>
        /// The renderer path every fixture observation in this file is authored
        /// at. Capture is renderer-scoped, so each test must say which renderer
        /// it analyzes; passing this keeps the existing same-path closure cases
        /// non-vacuous.
        /// </summary>
        private const string AnalyzedRendererPath = "Body";

        /// <summary>
        /// A different renderer's path. Material-slot bindings here belong to
        /// that renderer and must never enter an <see cref="AnalyzedRendererPath"/>
        /// capture.
        /// </summary>
        private const string ForeignRendererPath = "OtherBody";

        private readonly List<UnityEngine.Object> _owned =
            new List<UnityEngine.Object>();
        private readonly Dictionary<Material, CapturedAlphaMaterialFamily>
            _fixtureFamilies =
                new Dictionary<Material, CapturedAlphaMaterialFamily>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _owned)
            {
                if (value != null) Object.DestroyImmediate(value);
            }

            _owned.Clear();
            _fixtureFamilies.Clear();
            if (AssetDatabase.IsValidFolder(UnattestedShaderFolder))
            {
                AssetDatabase.DeleteAsset(UnattestedShaderFolder);
            }
        }

        /// <summary>
        /// Imports a stand-in shader asset carrying a real supported shader
        /// name. Its source is not the pinned vendor source, so it can pass
        /// family selection and never source attestation.
        /// </summary>
        private static Shader UnattestedShader(string fileName, string shaderName)
        {
            if (!AssetDatabase.IsValidFolder(UnattestedShaderFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_UnattestedShaders");
            }

            var path = UnattestedShaderFolder + "/" + fileName;
            File.WriteAllText(
                path,
                "Shader \"" + shaderName + "\"\n" +
                "{\n    SubShader { Pass {} }\n}\n");
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            return shader;
        }

        [Test]
        public void FloatObservationCopiesValuesAndCollectionStructure()
        {
            var clip = new AnimationClip { name = "observed" };
            try
            {
                AnimationUtility.SetEditorCurve(
                    clip, FloatBinding, AnimationCurve.Constant(0f, 1f, 0.25f));

                var observed = LiveAnimationObservation.ObserveClip(clip, false);

                AnimationUtility.SetEditorCurve(
                    clip,
                    FloatBinding,
                    new AnimationCurve(
                        new Keyframe(0f, 0.75f),
                        new Keyframe(1f, 1f)));

                Assert.That(observed.Floats, Has.Count.EqualTo(1));
                CollectionAssert.AreEqual(
                    new[] { 0.25f, 0.25f }, observed.Floats.Single().Values);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void InterpolatingCurveIsNotFiniteExact()
        {
            Assert.That(
                ObserveFiniteExact(AnimationCurve.Linear(0f, 0f, 1f, 1f)),
                Is.False);
        }

        [Test]
        public void EqualEndpointsWithNonZeroTangentsAreNotFiniteExact()
        {
            var overshooting = new AnimationCurve(
                new Keyframe(0f, 1f) { outTangent = 5f },
                new Keyframe(1f, 1f) { inTangent = -5f });

            Assert.That(
                overshooting.Evaluate(0.5f),
                Is.Not.EqualTo(1f),
                "fixture precondition: the segment must leave its endpoint value");
            Assert.That(ObserveFiniteExact(overshooting), Is.False);
        }

        [Test]
        public void EqualEndpointsWithZeroTangentsAreFiniteExact()
        {
            Assert.That(
                ObserveFiniteExact(new AnimationCurve(
                    new Keyframe(0f, 1f) { outTangent = 0f },
                    new Keyframe(1f, 1f) { inTangent = 0f })),
                Is.True);
        }

        [Test]
        public void SteppedSegmentIsFiniteExact()
        {
            Assert.That(
                ObserveFiniteExact(new AnimationCurve(
                    new Keyframe(0f, 0f)
                    {
                        outTangent = float.PositiveInfinity,
                    },
                    new Keyframe(1f, 1f)
                    {
                        inTangent = float.PositiveInfinity,
                    })),
                Is.True);
        }

        [Test]
        public void SingleKeyCurveIsFiniteExact()
        {
            Assert.That(
                ObserveFiniteExact(new AnimationCurve(new Keyframe(0f, 1f))),
                Is.True);
        }

        [Test]
        public void WeightedKeysAreNeverFiniteExact()
        {
            var weighted = new AnimationCurve(
                new Keyframe(0f, 1f)
                {
                    outTangent = 0f,
                    weightedMode = WeightedMode.Both,
                },
                new Keyframe(1f, 1f) { inTangent = 0f });

            Assert.That(ObserveFiniteExact(weighted), Is.False);
        }

        [Test]
        public void ObjectObservationCapturesIdentityAndNullValues()
        {
            AnimationClip clip = null;
            Material material = null;
            try
            {
                clip = new AnimationClip { name = "objects" };
                var shader = Shader.Find("Unlit/Color");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                var binding = EditorCurveBinding.PPtrCurve(
                    "Body",
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]");
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    binding,
                    new[]
                    {
                        new ObjectReferenceKeyframe { time = 0f, value = material },
                        new ObjectReferenceKeyframe { time = 1f, value = null },
                    });

                var observed = LiveAnimationObservation.ObserveClip(clip, false);
                var values = observed.Objects.Single().Values;
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);

                Assert.That(values, Has.Count.EqualTo(2));
                Assert.That(values[0], Is.SameAs(material));
                Assert.That(values[1], Is.Null);
            }
            finally
            {
                if (material != null) Object.DestroyImmediate(material);
                if (clip != null) Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void SpecialMotionIsDiagnosticOnlyAndDoesNotAlterBindings()
        {
            var clip = new AnimationClip { name = "special" };
            try
            {
                AnimationUtility.SetEditorCurve(
                    clip, FloatBinding, AnimationCurve.Constant(0f, 1f, 0.25f));

                var ordinary = LiveAnimationObservation.ObserveClip(clip, false);
                var special = LiveAnimationObservation.ObserveClip(clip, true);

                Assert.That(ordinary.IsSpecialMotion, Is.False);
                Assert.That(special.IsSpecialMotion, Is.True);
                Assert.That(special.Name, Is.EqualTo(ordinary.Name));
                Assert.That(special.Floats.Single().Path,
                    Is.EqualTo(ordinary.Floats.Single().Path));
                Assert.That(special.Floats.Single().TypeName,
                    Is.EqualTo(ordinary.Floats.Single().TypeName));
                Assert.That(special.Floats.Single().PropertyName,
                    Is.EqualTo(ordinary.Floats.Single().PropertyName));
                CollectionAssert.AreEqual(
                    ordinary.Floats.Single().Values,
                    special.Floats.Single().Values);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [TestCase("m_Materials.Array.data[0]", 0)]
        [TestCase("m_Materials.Array.data[3]", 3)]
        public void MaterialSlotBindingsAreParsed(string property, int expected)
        {
            Assert.That(
                LiveAnimationObservation.TryParseMaterialSlotBinding(
                    property, out var slot),
                Is.True);
            Assert.That(slot, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("m_Materials.Array.size")]
        [TestCase("m_Mesh")]
        [TestCase("material._Cutoff")]
        [TestCase("m_Materials.Array.data[]")]
        [TestCase("m_Materials.Array.data[-1]")]
        [TestCase("m_Materials.Array.data[1]trailing")]
        [TestCase("prefixm_Materials.Array.data[1]")]
        [TestCase("m_Materials.Array.data[2147483648]")]
        public void NonSlotBindingsAreNotParsedAsSlots(string property)
        {
            Assert.That(
                LiveAnimationObservation.TryParseMaterialSlotBinding(
                    property, out _),
                Is.False);
        }

        [Test]
        public void AlphaMaskScalarRequestsAreProofRelevant(
            [Values(
                "_MainAlphaMaskMode",
                "_AlphaMaskBlendStrength",
                "_AlphaMaskValue",
                "_AlphaMaskInvert",
                "_PoiParallax")] string property)
        {
            // The mask interpretation reads these, so animating any of them must
            // reach the existing admitted-state machinery rather than being
            // classified Irrelevant. Relevance comes from the request alone; no
            // new animation code is involved.
            var relevance = PoiyomiMaterialSemantics.AlphaEvidenceRequest;

            Assert.That(
                relevance.ScalarProperties, Contains.Item(property));
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound("material." + property),
                    "Body",
                    relevance,
                    out var reference),
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide));
            Assert.That(reference.PropertyName, Is.EqualTo(property));
        }

        [Test]
        public void ScaleOffsetRequestMakesTheDerivedStPropertyRelevant()
        {
            var relevance = PoiyomiMaterialSemantics.AlphaEvidenceRequest;

            Assert.That(
                relevance.VectorProperties,
                Does.Not.Contain("_MainTex_ST"),
                "fixture must prove texture-evidence derivation, not vector relevance");
            Assert.That(
                relevance.TextureProperties.Any(texture =>
                    texture.PropertyName == "_MainTex" &&
                    (texture.Evidence & TextureEvidenceKinds.ScaleOffset) != 0),
                Is.True,
                "the real frontend must request _MainTex ScaleOffset evidence");
            Assert.That(
                UnityAnimationEvidenceCapture.DeriveTextureScaleOffsetProperties(
                    relevance),
                Contains.Item("_MainTex_ST"));

            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound("material._MainTex_ST.x"),
                    "Body",
                    relevance,
                    out var reference),
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide));
            Assert.That(
                reference.Kind,
                Is.EqualTo(AnimatedPropertyKind.TextureScaleOffsetComponent));
            Assert.That(reference.PropertyName, Is.EqualTo("_MainTex_ST"));
            Assert.That(reference.ComponentIndex, Is.Zero);
        }

        [Test]
        public void ScaleOffsetIsNotDerivedWhenTheEvidenceKindIsNotRequested()
        {
            var relevance = new MaterialEvidenceRequest(
                false,
                false,
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex", TextureEvidenceKinds.SourceIdentity),
                });

            Assert.That(
                UnityAnimationEvidenceCapture.DeriveTextureScaleOffsetProperties(
                    relevance),
                Is.Empty);
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound("material._MainTex_ST.x"),
                    "Body",
                    relevance,
                    out _),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
        }

        [Test]
        public void ScalarBindingResolvesRendererWideWithoutASlot()
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound("material._Cutoff"),
                    "Body",
                    Relevance(),
                    out var reference),
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide));
            Assert.That(reference.Kind, Is.EqualTo(AnimatedPropertyKind.Scalar));
            Assert.That(reference.PropertyName, Is.EqualTo("_Cutoff"));
            Assert.That(reference.ComponentIndex, Is.EqualTo(-1));
        }

        [TestCase("material._Color.r", 0)]
        [TestCase("material._Color.a", 3)]
        public void ColorComponentResolvesToItsParent(
            string property,
            int component)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound(property), "Body", Relevance(), out var reference),
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide));
            Assert.That(
                reference.Kind,
                Is.EqualTo(AnimatedPropertyKind.ColorComponent));
            Assert.That(reference.PropertyName, Is.EqualTo("_Color"));
            Assert.That(reference.ComponentIndex, Is.EqualTo(component));
        }

        [TestCase("material._MainTexPan.x", 0)]
        [TestCase("material._MainTexPan.w", 3)]
        public void VectorComponentResolvesToItsParent(
            string property,
            int component)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound(property), "Body", Relevance(), out var reference),
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide));
            Assert.That(
                reference.Kind,
                Is.EqualTo(AnimatedPropertyKind.VectorComponent));
            Assert.That(reference.PropertyName, Is.EqualTo("_MainTexPan"));
            Assert.That(reference.ComponentIndex, Is.EqualTo(component));
        }

        [TestCase("material[2]._Cutoff")]
        [TestCase("material[-1]._Cutoff")]
        [TestCase("material[slot]._Color.a")]
        [TestCase("material[1]._Color.a")]
        [TestCase("material[0]._MainTexPan.w")]
        public void UnexpectedPotentiallyRelevantMaterialSyntaxFailsClosed(
            string property)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound(property), "Body", Relevance(), out _),
                Is.EqualTo(
                    ProofRelevantBindingResolution.UnrecognizedMaterialBinding));
        }

        [TestCase("material._Unrelated")]
        [TestCase("material._Unrelated.a")]
        [TestCase("material[2]._Unrelated")]
        [TestCase("material[slot]._Unrelated")]
        [TestCase("notmaterial[2]._Unrelated.a")]
        public void UnrequestedPropertiesRemainIrrelevant(string property)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound(property), "Body", Relevance(), out _),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
        }

        [Test]
        public void PresencePropertiesAreNotAnimatedScalarInputs()
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound("material._PresenceOnly"),
                    "Body",
                    Relevance(),
                    out _),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
        }

        [TestCase("body", "material._Cutoff")]
        [TestCase("Body", "material._cutoff")]
        public void RendererPathAndPropertyComparisonsAreOrdinal(
            string rendererPath,
            string property)
        {
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound(property), rendererPath, Relevance(), out _),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
        }

        [Test]
        public void ClosureUnionsEveryAdmittedFamilyNotOnlyTheInitialOne()
        {
            var initial = NewPoiyomiMaterial();
            var swapped = NewLilToonMaterial();

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(swapped) },
                new[] { initial },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(2));

            var onlyInSwapped = RequestedNames(
                    LilToonMaterialSemantics.AlphaEvidenceRequest)
                .Except(
                    RequestedNames(PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                    StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                onlyInSwapped,
                Is.Not.Empty,
                "fixture precondition: the frontend requests must differ");
            foreach (var property in onlyInSwapped)
            {
                Assert.That(
                    RequestedNames(evidence.AlphaRelevanceRequest),
                    Contains.Item(property),
                    "missed a dependency contributed only by the swapped family: " +
                    property);
            }
        }

        [Test]
        public void FailedClosureExposesNoPartialEvidence()
        {
            var initial = NewPoiyomiMaterial();
            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithSlotValue(null) },
                new[] { initial },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.InvalidSwapValue));
            Assert.That(RequestedNames(evidence.AlphaRelevanceRequest), Is.Empty);
            Assert.That(evidence.Clips, Is.Empty);
            Assert.That(evidence.AdmittedMaterials, Is.Empty);
            Assert.That(evidence.CurrentMaterialIndices, Is.Empty);
        }

        [Test]
        public void LiveMaterialReferencesBecomeStableDeterministicIndices()
        {
            var initial = NewPoiyomiMaterial();
            var swapped = NewLilToonMaterial();
            var observation = ObservationWithMaterialSwap(
                swapped, swapped, initial);

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { observation }, new[] { initial }, EmptyGraph());
            var indices = evidence.Clips.Single()
                .ObjectBindings.Single().AdmittedMaterialIndices;

            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(new[] { 0 }, evidence.CurrentMaterialIndices);
            CollectionAssert.AreEqual(new[] { 1, 1, 0 }, indices);
            Assert.That(
                indices,
                Is.All.InRange(0, evidence.AdmittedMaterials.Count - 1));

            Object.DestroyImmediate(swapped);
            Assert.That(evidence.AdmittedMaterials[1], Is.Not.Null);
            CollectionAssert.AreEqual(new[] { 1, 1, 0 }, indices);
        }

        [Test]
        public void EveryCurrentAndSwapMaterialParticipatesInAdmission()
        {
            var initial = NewPoiyomiMaterial();
            var firstSwap = NewLilToonMaterial();
            var secondSwap = NewPoiyomiMaterial();

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(firstSwap, secondSwap) },
                new[] { initial },
                EmptyGraph());

            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(3));
            CollectionAssert.AreEqual(new[] { 0 }, evidence.CurrentMaterialIndices);
            CollectionAssert.AreEqual(
                new[] { 1, 2 },
                evidence.Clips.Single().ObjectBindings.Single()
                    .AdmittedMaterialIndices);
        }

        [Test]
        public void NullOrNonMaterialSlotAssignmentsFailClosure()
        {
            var initial = NewPoiyomiMaterial();
            var texture = Own(new Texture2D(1, 1));

            foreach (var value in new UnityEngine.Object[] { null, texture })
            {
                var evidence = CaptureVerified(
                    AnalyzedRendererPath,
                    new[] { ObservationWithSlotValue(value) },
                    new[] { initial },
                    EmptyGraph());

                Assert.That(evidence.IsClosed, Is.False);
                Assert.That(evidence.ClosureFailure,
                    Is.EqualTo(
                        MaterialDependencyClosureFailure.InvalidSwapValue));
                Assert.That(evidence.Clips, Is.Empty);
                Assert.That(evidence.AdmittedMaterials, Is.Empty);
            }
        }

        [Test]
        public void OutOfRangeMaterialSlotFailsClosure()
        {
            var initial = NewPoiyomiMaterial();
            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[]
                {
                    ObservationWithObjectBinding(
                        "m_Materials.Array.data[1]", initial),
                },
                new[] { initial },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.SlotOutOfRange));
            Assert.That(evidence.Clips, Is.Empty);
        }

        [Test]
        public void NonSlotObjectBindingSurvivesWithoutLiveValues()
        {
            var initial = NewPoiyomiMaterial();
            var mesh = Own(new Mesh { name = "replacement" });
            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithObjectBinding("m_Mesh", mesh) },
                new[] { initial },
                EmptyGraph());

            var binding = evidence.Clips.Single().ObjectBindings.Single();
            Assert.That(binding.Path, Is.EqualTo("Body"));
            Assert.That(binding.TypeName,
                Is.EqualTo(typeof(SkinnedMeshRenderer).FullName));
            Assert.That(binding.PropertyName, Is.EqualTo("m_Mesh"));
            Assert.That(binding.AdmittedMaterialIndices, Is.Empty);

            Object.DestroyImmediate(mesh);
            Assert.That(binding.PropertyName, Is.EqualTo("m_Mesh"));
        }

        [Test]
        public void GraphBlendFactsSurviveIntoImmutableEvidence()
        {
            var material = NewPoiyomiMaterial();
            var graph = new CommittedControllerGraphResult(
                AvatarAnimationRefusal.None,
                new[]
                {
                    Layer(AnimatorLayerBlendingMode.Additive, false),
                    Layer(AnimatorLayerBlendingMode.Override, true),
                });

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                Array.Empty<LiveClipObservation>(),
                new[] { material },
                graph);

            Assert.That(evidence.HasAdditiveLayer, Is.True);
            Assert.That(evidence.HasUnnormalizedDirectBlendTree, Is.True);
        }

        [Test]
        public void ProductionGraphRouteObservesEveryClipAndHostSpecialFact()
        {
            var initial = NewPoiyomiMaterial();
            var swapped = NewLilToonMaterial();
            var specialClip = Own(new AnimationClip { name = "graph swap" });
            AnimationUtility.SetObjectReferenceCurve(
                specialClip,
                EditorCurveBinding.PPtrCurve(
                    "Body",
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]"),
                new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = swapped },
                });
            var ordinaryClip = Own(new AnimationClip { name = "graph float" });
            AnimationUtility.SetEditorCurve(
                ordinaryClip,
                FloatBinding,
                AnimationCurve.Constant(0f, 1f, 0.25f));
            var graph = new CommittedControllerGraphResult(
                AvatarAnimationRefusal.None,
                new[]
                {
                    new CommittedLayer(
                        "controller",
                        0,
                        AnimatorLayerBlendingMode.Override,
                        new[] { specialClip },
                        Array.Empty<StateMachineBehaviour>(),
                        false),
                    new CommittedLayer(
                        "controller",
                        1,
                        AnimatorLayerBlendingMode.Override,
                        new[] { ordinaryClip },
                        Array.Empty<StateMachineBehaviour>(),
                        false),
                });

            var evidence = UnityAnimationEvidenceCapture.CaptureGraphForTests(
                AnalyzedRendererPath,
                new[] { initial },
                graph,
                new StubBindings(specialClip),
                SelectFixtureRequest,
                CaptureFixtureMaterials);

            Assert.That(evidence.Clips, Has.Count.EqualTo(2));
            Assert.That(evidence.Clips[0].Name, Is.EqualTo("graph swap"));
            Assert.That(evidence.Clips[0].IsSpecialMotion, Is.True);
            CollectionAssert.AreEqual(
                new[] { 1 },
                evidence.Clips[0].ObjectBindings.Single()
                    .AdmittedMaterialIndices);
            Assert.That(evidence.Clips[1].Name, Is.EqualTo("graph float"));
            Assert.That(evidence.Clips[1].IsSpecialMotion, Is.False);
            Assert.That(evidence.Clips[1].FloatBindings, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// Selection is a whole-batch pass, and the capture that follows it is
        /// the only one: every admitted material is selected before capture
        /// begins, and the capturer is invoked exactly once with the complete
        /// admitted batch rather than once per material.
        /// </summary>
        [Test]
        public void EverySelectionCompletesBeforeTheSingleClosedCapture()
        {
            var first = NewPoiyomiMaterial();
            var second = NewLilToonMaterial();
            var selected = 0;
            var captureCalls = 0;
            var capturedBatch = Array.Empty<Material>();

            bool Select(
                Material material,
                out CapturedAlphaMaterialFamily family,
                out MaterialEvidenceRequest alphaRelevance,
                out MaterialEvidenceRequest captureSchema)
            {
                Assert.That(
                    captureCalls,
                    Is.Zero,
                    "no evidence may be captured while selection is still running");
                selected++;
                return SelectFixtureRequest(
                    material, out family, out alphaRelevance, out captureSchema);
            }

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                Assert.That(selected, Is.EqualTo(2));
                captureCalls++;
                capturedBatch = materials.ToArray();
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(second) },
                new[] { first },
                EmptyGraph(),
                Select,
                Capture);

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(
                captureCalls,
                Is.EqualTo(1),
                "the admitted batch must be captured exactly once");
            CollectionAssert.AreEqual(new[] { first, second }, capturedBatch);
        }

        [Test]
        public void ConversionEvidenceIsCapturedWithoutWideningAlphaRelevance()
        {
            var poiyomi = NewPoiyomiMaterial();

            // Off the shader's default of 1, so proving the captured value
            // equals the source material's is not 1 == 1. This is a transient
            // test-owned material, never a source asset.
            poiyomi.SetFloat("_ZWrite", 0f);
            MaterialEvidenceRequest capturerSaw = null;

            bool Select(
                Material material,
                out CapturedAlphaMaterialFamily family,
                out MaterialEvidenceRequest alphaRelevance,
                out MaterialEvidenceRequest captureSchema)
            {
                family = CapturedAlphaMaterialFamily.Poiyomi;
                alphaRelevance = PoiyomiMaterialSemantics.AlphaEvidenceRequest;
                captureSchema = MaterialEvidenceRequest.Combine(
                    alphaRelevance,
                    PoiyomiOpaqueConversion.ConversionEvidenceRequest);
                return true;
            }

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                capturerSaw = request;
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                Array.Empty<LiveClipObservation>(),
                new[] { poiyomi },
                EmptyGraph(),
                Select,
                Capture);

            Assert.That(evidence.IsClosed, Is.True);
            CollectionAssert.Contains(
                RequestedNames(capturerSaw),
                "_ZWrite",
                "fixture precondition: the capture schema must carry " +
                "conversion-only render state");
            CollectionAssert.DoesNotContain(
                RequestedNames(evidence.AlphaRelevanceRequest),
                "_ZWrite",
                "conversion-only render state became ordinary alpha relevance");

            // The request reaching the capturer proves only what was asked for.
            // This is the evidence the real capture actually returned, so it
            // proves conversion state survives the one capture rather than
            // being requested and dropped.
            var captured = evidence.AdmittedMaterials.Single().Evidence;
            Assert.That(
                captured.TryGetScalar("_ZWrite", out var capturedZWrite),
                Is.True,
                "conversion-only render state was requested but not captured");
            Assert.That(
                capturedZWrite,
                Is.EqualTo(poiyomi.GetFloat("_ZWrite")),
                "captured conversion state disagrees with the source material");

            // The same evidence, read through ordinary alpha proof, still does
            // not see it.
            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    Bound("material._ZWrite"),
                    AnalyzedRendererPath,
                    evidence.AlphaRelevanceRequest,
                    out _),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant),
                "captured conversion state became an alpha proof input");
        }

        /// <summary>
        /// Conversion evidence widens what the batch capture gathers, not what
        /// ordinary alpha proof considers. lilToon contributes no conversion
        /// request, so a mixed batch must still store exactly the union of the
        /// two families' alpha requests.
        /// </summary>
        [Test]
        public void MixedFamilyClosureKeepsCaptureSchemaApartFromAlphaRelevance()
        {
            var poiyomi = NewPoiyomiMaterial();
            var lilToon = NewLilToonMaterial();
            MaterialEvidenceRequest capturerSaw = null;
            var captureCalls = 0;

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                captureCalls++;
                capturerSaw = request;
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(lilToon) },
                new[] { poiyomi },
                EmptyGraph(),
                SelectFixtureRequest,
                Capture);

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(
                captureCalls,
                Is.EqualTo(1),
                "the admitted batch must still be captured exactly once");

            var expectedAlpha = RequestedNames(
                MaterialEvidenceRequest.Combine(
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                    LilToonMaterialSemantics.AlphaEvidenceRequest));
            CollectionAssert.AreEqual(
                expectedAlpha,
                RequestedNames(evidence.AlphaRelevanceRequest),
                "stored alpha relevance must be exactly the union of the " +
                "admitted families' alpha requests");

            CollectionAssert.Contains(
                RequestedNames(capturerSaw),
                "_EnableOutlines",
                "the capture schema lost Poiyomi's conversion evidence");
            foreach (var conversionOnly in ConversionOnlyProperties())
            {
                CollectionAssert.DoesNotContain(
                    RequestedNames(evidence.AlphaRelevanceRequest),
                    conversionOnly,
                    "conversion-only render state widened alpha relevance, " +
                    "including lilToon's: " + conversionOnly);
            }
        }

        /// <summary>
        /// A conversion-only property is still observed and captured like any
        /// other animated binding; it is the stored alpha request that decides
        /// it is not an ordinary alpha proof input. Storing the broader capture
        /// schema instead would resolve this binding as renderer-wide.
        /// </summary>
        [Test]
        public void ConversionOnlyAnimatedBindingIsCapturedYetIrrelevantToAlphaProof()
        {
            var poiyomi = NewPoiyomiMaterial();

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithFloatBinding("material._ZWrite") },
                new[] { poiyomi },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.True);
            var binding = evidence.Clips.Single().FloatBindings.Single();
            Assert.That(
                binding.PropertyName,
                Is.EqualTo("material._ZWrite"),
                "the conversion-only binding was not captured at all");

            Assert.That(
                UnityAnimationEvidenceCapture.ResolveProofRelevant(
                    binding,
                    AnalyzedRendererPath,
                    evidence.AlphaRelevanceRequest,
                    out _),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant),
                "a conversion-only animated property became an ordinary alpha " +
                "proof input");
        }

        [Test]
        public void ClosureFailureRetainsGraphFactsButNoPartialEvidence()
        {
            var graph = new CommittedControllerGraphResult(
                AvatarAnimationRefusal.None,
                new[]
                {
                    Layer(AnimatorLayerBlendingMode.Additive, true),
                });

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                Array.Empty<LiveClipObservation>(),
                new Material[] { null },
                graph);

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(evidence.ClosureFailure,
                Is.EqualTo(
                    MaterialDependencyClosureFailure.MissingCurrentMaterial));
            Assert.That(evidence.HasAdditiveLayer, Is.True);
            Assert.That(evidence.HasUnnormalizedDirectBlendTree, Is.True);
            Assert.That(evidence.Clips, Is.Empty);
            Assert.That(evidence.AdmittedMaterials, Is.Empty);
        }

        [Test]
        public void MultiSlotDuplicateCurrentMaterialMapsDeterministically()
        {
            var current = NewPoiyomiMaterial();
            var swapped = NewLilToonMaterial();
            var observation = ObservationWithObjectBinding(
                "m_Materials.Array.data[1]", swapped);

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[] { observation },
                new[] { current, current },
                EmptyGraph());

            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(2));
            CollectionAssert.AreEqual(
                new[] { 0, 0 }, evidence.CurrentMaterialIndices);
            CollectionAssert.AreEqual(
                new[] { 1 },
                evidence.Clips.Single().ObjectBindings.Single()
                    .AdmittedMaterialIndices);
        }

        [Test]
        public void SpecialMotionDoesNotAlterClosureOrAdmission()
        {
            var initial = NewPoiyomiMaterial();
            var swapped = NewLilToonMaterial();
            var ordinary = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(swapped, false) },
                new[] { initial },
                EmptyGraph());
            var special = CaptureVerified(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(swapped, true) },
                new[] { initial },
                EmptyGraph());

            Assert.That(ordinary.Clips.Single().IsSpecialMotion, Is.False);
            Assert.That(special.Clips.Single().IsSpecialMotion, Is.True);
            CollectionAssert.AreEqual(
                RequestedNames(ordinary.AlphaRelevanceRequest),
                RequestedNames(special.AlphaRelevanceRequest));
            CollectionAssert.AreEqual(
                ordinary.CurrentMaterialIndices,
                special.CurrentMaterialIndices);
            CollectionAssert.AreEqual(
                ordinary.Clips.Single().ObjectBindings.Single()
                    .AdmittedMaterialIndices,
                special.Clips.Single().ObjectBindings.Single()
                    .AdmittedMaterialIndices);
        }

        [Test]
        public void UnattestedMaterialFailsTheRealCapturePath()
        {
            var material = Own(new Material(Shader.Find("Unlit/Color")));

            var evidence = UnityAnimationEvidenceCapture.Capture(
                AnalyzedRendererPath,
                new[] { material },
                EmptyGraph(),
                new StubBindings());

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.UnattestedMaterial));
            Assert.That(evidence.Clips, Is.Empty);
            Assert.That(evidence.AdmittedMaterials, Is.Empty);
        }

        /// <summary>
        /// The closed batch capture is the sole source-attestation decision, so
        /// its refusal is the same conservative outcome as a material no family
        /// selects: unattested, with nothing partial escaping.
        /// </summary>
        [Test]
        public void RefusedClosedCaptureIsUnattestedWithNoPartialEvidence()
        {
            var first = NewPoiyomiMaterial();
            var second = NewLilToonMaterial();

            bool RefuseCapture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                captured = null;
                return false;
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(second) },
                new[] { first },
                EmptyGraph(),
                SelectFixtureRequest,
                RefuseCapture);

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(
                evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.UnattestedMaterial));
            Assert.That(RequestedNames(evidence.AlphaRelevanceRequest), Is.Empty);
            Assert.That(evidence.Clips, Is.Empty);
            Assert.That(evidence.AdmittedMaterials, Is.Empty);
            Assert.That(evidence.CurrentMaterialIndices, Is.Empty);
        }

        /// <summary>
        /// A family no frontend supports is refused during selection, before the
        /// batch capture is reached, so no evidence is gathered for a batch that
        /// can never close.
        /// </summary>
        [Test]
        public void UnselectableFamilyFailsBeforeTheClosedCaptureIsInvoked()
        {
            var supported = NewPoiyomiMaterial();
            var unselectable = Own(new Material(Shader.Find("Unlit/Color")));
            var captureCalls = 0;

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                captureCalls++;
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[] { ObservationWithMaterialSwap(unselectable) },
                new[] { supported },
                EmptyGraph(),
                SelectFixtureRequest,
                Capture);

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(
                evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.UnattestedMaterial));
            Assert.That(
                captureCalls,
                Is.Zero,
                "an unselectable family must not reach the batch capture");
            Assert.That(evidence.Clips, Is.Empty);
            Assert.That(evidence.AdmittedMaterials, Is.Empty);
        }

        /// <summary>
        /// The real closure path refuses conservatively from both directions: a
        /// shader no family claims, and a shader carrying a supported name over
        /// a source no attestation can verify. Only the closed batch capture
        /// decides the second case, and it still refuses.
        /// </summary>
        [Test]
        public void SupportedShaderNameWithUnattestedSourceFailsTheRealPath()
        {
            var material = Own(new Material(
                UnattestedShader(
                    "poiyomi-named.shader",
                    PoiyomiMaterialSemantics.PoiyomiToonShaderName)));

            var evidence = UnityAnimationEvidenceCapture.Capture(
                AnalyzedRendererPath,
                new[] { material },
                EmptyGraph(),
                new StubBindings());

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.UnattestedMaterial));
            Assert.That(evidence.Clips, Is.Empty);
            Assert.That(evidence.AdmittedMaterials, Is.Empty);
        }

        [Test]
        public void AvatarGraphRefusalIsCallerMisuseNotClosureFailure()
        {
            var refused = new CommittedControllerGraphResult(
                AvatarAnimationRefusal.UnsupportedAnimatorControllerForm,
                Array.Empty<CommittedLayer>());

            Assert.Throws<InvalidOperationException>(() =>
                UnityAnimationEvidenceCapture.Capture(
                    AnalyzedRendererPath,
                    Array.Empty<Material>(),
                    refused,
                    new StubBindings()));
        }

        // --- Renderer-scoped material-swap closure ------------------------
        //
        // A material-slot binding addresses exactly one renderer path. Capture
        // analyzes ONE renderer, so a binding on any other path describes a
        // different renderer's slots and is not this renderer's evidence.

        [Test]
        public void ForeignRendererMaterialSlotBindingIsNotRangeCheckedAgainstThisRenderer()
        {
            var current = NewPoiyomiMaterial();
            var foreignSwap = NewPoiyomiMaterial();

            // One slot here; the OTHER renderer animates its slot 1.
            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[]
                {
                    ObservationWithMaterialSwapAt(
                        ForeignRendererPath,
                        "m_Materials.Array.data[1]",
                        foreignSwap),
                },
                new[] { current },
                EmptyGraph());

            Assert.That(
                evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.None),
                "another renderer's slot index was range-checked against this " +
                "renderer's slot count");
            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(
                evidence.AdmittedMaterials,
                Has.Count.EqualTo(1),
                "only this renderer's current material may be admitted");
            CollectionAssert.AreEqual(new[] { 0 }, evidence.CurrentMaterialIndices);
            Assert.That(
                evidence.Clips.SelectMany(clip => clip.ObjectBindings),
                Is.Empty,
                "a foreign material-slot binding must be omitted entirely, not " +
                "retained with misleading empty material indices");
        }

        [Test]
        public void ForeignRendererSwapMaterialIsNeverSelectedCapturedOrRequestRelevant()
        {
            var current = NewPoiyomiMaterial();
            var foreignSwap = NewLilToonMaterial();

            var selected = new List<Material>();
            var capturedBatch = Array.Empty<Material>();
            var captureCalls = 0;

            bool Select(
                Material material,
                out CapturedAlphaMaterialFamily family,
                out MaterialEvidenceRequest alphaRelevance,
                out MaterialEvidenceRequest captureSchema)
            {
                selected.Add(material);
                return SelectFixtureRequest(
                    material, out family, out alphaRelevance, out captureSchema);
            }

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                captureCalls++;
                capturedBatch = materials.ToArray();
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[]
                {
                    ObservationWithMaterialSwapAt(
                        ForeignRendererPath,
                        "m_Materials.Array.data[0]",
                        foreignSwap),
                },
                new[] { current },
                EmptyGraph(),
                Select,
                Capture);

            Assert.That(evidence.IsClosed, Is.True);
            CollectionAssert.AreEqual(
                new[] { current },
                selected,
                "another renderer's swap material reached request selection");
            Assert.That(captureCalls, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { current },
                capturedBatch,
                "another renderer's swap material reached material capture");

            // The lilToon family contributes request properties Poiyomi does not,
            // so a widened request is directly observable.
            CollectionAssert.AreEqual(
                RequestedNames(PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                RequestedNames(evidence.AlphaRelevanceRequest),
                "another renderer's material widened this renderer's evidence " +
                "request, which also widens what counts as proof-relevant here");
        }

        [Test]
        public void ForeignRendererUnattestedMaterialCannotRefuseThisRenderer()
        {
            var current = NewPoiyomiMaterial();
            // Never registered in _fixtureFamilies, so selection cannot attest it.
            var foreignUnattested = Own(new Material(Shader.Find("Unlit/Color")));

            var capturedBatch = Array.Empty<Material>();
            var captureCalls = 0;

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                captureCalls++;
                capturedBatch = materials.ToArray();
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                AnalyzedRendererPath,
                new[]
                {
                    ObservationWithMaterialSwapAt(
                        ForeignRendererPath,
                        "m_Materials.Array.data[0]",
                        foreignUnattested),
                },
                new[] { current },
                EmptyGraph(),
                SelectFixtureRequest,
                Capture);

            Assert.That(
                evidence.ClosureFailure,
                Is.EqualTo(MaterialDependencyClosureFailure.None),
                "an unattested material on ANOTHER renderer refused this one");
            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(captureCalls, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { current },
                capturedBatch,
                "the closed capturer must see only this renderer's batch");
        }

        [Test]
        public void OwningRendererStillCapturesItsOwnCurrentAndSwappedMaterials()
        {
            var owningCurrent = NewPoiyomiMaterial();
            var owningSwap = NewPoiyomiMaterial();

            // The SAME observation as the foreign cases, analyzed from the
            // renderer that actually owns it.
            var evidence = CaptureVerified(
                ForeignRendererPath,
                new[]
                {
                    ObservationWithMaterialSwapAt(
                        ForeignRendererPath,
                        "m_Materials.Array.data[0]",
                        owningSwap),
                },
                new[] { owningCurrent },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(
                evidence.AdmittedMaterials,
                Has.Count.EqualTo(2),
                "the owning renderer must still close over its own swap");
            CollectionAssert.AreEqual(new[] { 0 }, evidence.CurrentMaterialIndices);

            var objectBindings = evidence.Clips
                .SelectMany(clip => clip.ObjectBindings)
                .ToArray();
            Assert.That(objectBindings, Has.Length.EqualTo(1));
            Assert.That(objectBindings[0].Path, Is.EqualTo(ForeignRendererPath));
            CollectionAssert.AreEqual(
                new[] { 1 },
                objectBindings[0].AdmittedMaterialIndices,
                "the owning renderer's swap must keep stable admitted indices");
        }

        [Test]
        public void SameSlotPropertyOnAnotherPathDoesNotDisplaceThisRenderersOwnSwap()
        {
            var current = NewPoiyomiMaterial();
            var ownSwap = NewPoiyomiMaterial();
            var foreignSwap = NewPoiyomiMaterial();

            var evidence = CaptureVerified(
                AnalyzedRendererPath,
                new[]
                {
                    ObservationWithMaterialSwapAt(
                        ForeignRendererPath,
                        "m_Materials.Array.data[0]",
                        foreignSwap),
                    ObservationWithMaterialSwapAt(
                        AnalyzedRendererPath,
                        "m_Materials.Array.data[0]",
                        ownSwap),
                },
                new[] { current },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(2));

            var objectBindings = evidence.Clips
                .SelectMany(clip => clip.ObjectBindings)
                .ToArray();
            Assert.That(
                objectBindings, Has.Length.EqualTo(1),
                "only the analyzed renderer's own slot binding survives");
            Assert.That(objectBindings[0].Path, Is.EqualTo(AnalyzedRendererPath));
            CollectionAssert.AreEqual(
                new[] { 1 }, objectBindings[0].AdmittedMaterialIndices);
        }

        [Test]
        public void RendererOnTheAvatarRootUsesTheEmptyPath()
        {
            var current = NewPoiyomiMaterial();
            var rootSwap = NewPoiyomiMaterial();

            // A renderer on the avatar root has an empty Unity animation path,
            // so empty must be a valid analyzed path rather than a defect.
            var evidence = CaptureVerified(
                string.Empty,
                new[]
                {
                    ObservationWithMaterialSwapAt(
                        string.Empty, "m_Materials.Array.data[0]", rootSwap),
                },
                new[] { current },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.True);
            Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(2));
            Assert.That(
                evidence.Clips.SelectMany(clip => clip.ObjectBindings).ToArray(),
                Has.Length.EqualTo(1));
        }

        [Test]
        public void NullRendererPathIsACallerDefect()
        {
            var current = NewPoiyomiMaterial();

            Assert.Throws<ArgumentNullException>(() =>
                UnityAnimationEvidenceCapture.Capture(
                    null,
                    new[] { current },
                    EmptyGraph(),
                    new StubBindings()));
        }

        private static bool ObserveFiniteExact(AnimationCurve curve)
        {
            var clip = new AnimationClip { name = "finite exact probe" };
            try
            {
                AnimationUtility.SetEditorCurve(clip, FloatBinding, curve);
                return LiveAnimationObservation.ObserveClip(clip, false)
                    .Floats.Single().IsFiniteExact;
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        private static MaterialEvidenceRequest Relevance()
        {
            return new MaterialEvidenceRequest(
                false,
                false,
                new[] { "_PresenceOnly" },
                new[] { "_Cutoff" },
                new[] { "_Color" },
                new[] { "_MainTexPan" },
                Array.Empty<TexturePropertyEvidenceRequest>());
        }

        private static CapturedFloatBinding Bound(string property)
        {
            return new CapturedFloatBinding(
                "Body",
                typeof(SkinnedMeshRenderer).FullName,
                property,
                true,
                new[] { 1f });
        }

        private Material NewPoiyomiMaterial()
        {
            var material = Own(PoiyomiFixtureTestBase.CreateVerifiedMaterial());
            _fixtureFamilies.Add(material, CapturedAlphaMaterialFamily.Poiyomi);
            return material;
        }

        private Material NewLilToonMaterial()
        {
            var material = Own(LilToonFixtureTestBase.CreateVerifiedMaterial());
            _fixtureFamilies.Add(material, CapturedAlphaMaterialFamily.LilToon);
            return material;
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            _owned.Add(value);
            return value;
        }

        private CapturedAnimationEvidence CaptureVerified(
            string rendererPath,
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph)
        {
            return UnityAnimationEvidenceCapture.CaptureObservedForTests(
                rendererPath,
                observations,
                currentSlots,
                graph,
                SelectFixtureRequest,
                CaptureFixtureMaterials);
        }

        /// <summary>
        /// Mirrors production's family mapping over the public stand-in
        /// fixtures: each family's existing alpha request is what ordinary
        /// alpha proof may consider, and Poiyomi's capture schema additionally
        /// carries conversion evidence.
        /// </summary>
        private bool SelectFixtureRequest(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest alphaRelevance,
            out MaterialEvidenceRequest captureSchema)
        {
            if (_fixtureFamilies.TryGetValue(material, out family))
            {
                if (family == CapturedAlphaMaterialFamily.Poiyomi)
                {
                    alphaRelevance =
                        PoiyomiMaterialSemantics.AlphaEvidenceRequest;
                    captureSchema = MaterialEvidenceRequest.Combine(
                        alphaRelevance,
                        PoiyomiOpaqueConversion.ConversionEvidenceRequest);
                }
                else
                {
                    alphaRelevance =
                        LilToonMaterialSemantics.AlphaEvidenceRequest;
                    captureSchema = alphaRelevance;
                }

                return true;
            }

            alphaRelevance = null;
            captureSchema = null;
            return false;
        }

        private static bool CaptureFixtureMaterials(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            var inputs = materials
                .Select(material =>
                    new MaterialEvidenceCaptureInput(material, request))
                .ToArray();
            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            captured = evidence.Select((value, index) =>
                    new CapturedAlphaMaterial(
                        families[index],
                        value,
                        default(PoiyomiSourceEvidence),
                        null))
                .ToArray();
            return true;
        }

        private static LiveClipObservation ObservationWithMaterialSwap(
            params Material[] materials)
        {
            return ObservationWithMaterialSwap(materials, false);
        }

        private static LiveClipObservation ObservationWithMaterialSwap(
            Material material,
            bool isSpecialMotion)
        {
            return ObservationWithMaterialSwap(
                new[] { material }, isSpecialMotion);
        }

        private static LiveClipObservation ObservationWithMaterialSwap(
            Material[] materials,
            bool isSpecialMotion)
        {
            return ObservationWithMaterialSwapAt(
                AnalyzedRendererPath,
                "m_Materials.Array.data[0]",
                isSpecialMotion,
                materials);
        }

        /// <summary>
        /// A material-slot swap authored at an arbitrary renderer path, so a
        /// test can express another renderer's animation without pretending it
        /// belongs to the renderer under analysis.
        /// </summary>
        private static LiveClipObservation ObservationWithMaterialSwapAt(
            string path,
            string propertyName,
            params Material[] materials)
        {
            return ObservationWithMaterialSwapAt(
                path, propertyName, false, materials);
        }

        private static LiveClipObservation ObservationWithMaterialSwapAt(
            string path,
            string propertyName,
            bool isSpecialMotion,
            Material[] materials)
        {
            return new LiveClipObservation(
                "swap",
                isSpecialMotion,
                Array.Empty<LiveFloatObservation>(),
                new[]
                {
                    new LiveObjectObservation(
                        path,
                        typeof(SkinnedMeshRenderer).FullName,
                        propertyName,
                        materials.Cast<UnityEngine.Object>().ToArray()),
                });
        }

        /// <summary>
        /// Requested names carried by Poiyomi's conversion evidence but by
        /// neither family's alpha request, derived rather than retyped so the
        /// assertions cannot drift from the vendor recipe.
        /// </summary>
        private static string[] ConversionOnlyProperties()
        {
            var alpha = RequestedNames(
                MaterialEvidenceRequest.Combine(
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                    LilToonMaterialSemantics.AlphaEvidenceRequest));
            var conversionOnly = RequestedNames(
                    PoiyomiOpaqueConversion.ConversionEvidenceRequest)
                .Except(alpha, StringComparer.Ordinal)
                .ToArray();
            Assert.That(
                conversionOnly,
                Contains.Item("_ZWrite"),
                "fixture precondition: conversion evidence must carry render " +
                "state no alpha request asks for");
            return conversionOnly;
        }

        private static LiveClipObservation ObservationWithFloatBinding(
            string propertyName)
        {
            return new LiveClipObservation(
                "floats",
                false,
                new[]
                {
                    new LiveFloatObservation(
                        AnalyzedRendererPath,
                        typeof(SkinnedMeshRenderer).FullName,
                        propertyName,
                        true,
                        new[] { 1f }),
                },
                Array.Empty<LiveObjectObservation>());
        }

        private static LiveClipObservation ObservationWithSlotValue(
            UnityEngine.Object value)
        {
            return ObservationWithObjectBinding(
                "m_Materials.Array.data[0]", value);
        }

        private static LiveClipObservation ObservationWithObjectBinding(
            string propertyName,
            UnityEngine.Object value)
        {
            return new LiveClipObservation(
                "objects",
                false,
                Array.Empty<LiveFloatObservation>(),
                new[]
                {
                    new LiveObjectObservation(
                        "Body",
                        typeof(SkinnedMeshRenderer).FullName,
                        propertyName,
                        new[] { value }),
                });
        }

        private static CommittedControllerGraphResult EmptyGraph()
        {
            return new CommittedControllerGraphResult(
                AvatarAnimationRefusal.None,
                Array.Empty<CommittedLayer>());
        }

        private static CommittedLayer Layer(
            AnimatorLayerBlendingMode blendingMode,
            bool hasUnnormalizedDirectBlendTree)
        {
            return new CommittedLayer(
                "controller",
                0,
                blendingMode,
                Array.Empty<AnimationClip>(),
                Array.Empty<StateMachineBehaviour>(),
                hasUnnormalizedDirectBlendTree);
        }

        private static string[] RequestedNames(MaterialEvidenceRequest request)
        {
            return request.PresenceProperties
                .Concat(request.ScalarProperties)
                .Concat(request.ColorProperties)
                .Concat(request.VectorProperties)
                .Concat(request.TextureProperties.Select(value =>
                    value.PropertyName))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private sealed class StubBindings : IPlatformAnimatorBindings
        {
            private readonly Motion _special;

            internal StubBindings(Motion special = null)
            {
                _special = special;
            }

            public bool IsSpecialMotion(Motion motion)
            {
                return ReferenceEquals(motion, _special);
            }
        }
    }
}
