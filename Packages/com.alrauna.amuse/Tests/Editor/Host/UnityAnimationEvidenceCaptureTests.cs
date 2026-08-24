using System;
using System.Collections.Generic;
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
        public void ClosureUnionsEveryAdmittedFamilyNotOnlyTheInitialOne()
        {
            var initial = NewPoiyomiMaterial();
            var swapped = NewLilToonMaterial();

            var evidence = CaptureVerified(
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
                    RequestedNames(evidence.RelevanceRequest),
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
                new[] { ObservationWithSlotValue(null) },
                new[] { initial },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(RequestedNames(evidence.RelevanceRequest), Is.Empty);
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
                    new[] { ObservationWithSlotValue(value) },
                    new[] { initial },
                    EmptyGraph());

                Assert.That(evidence.IsClosed, Is.False);
                Assert.That(evidence.Clips, Is.Empty);
                Assert.That(evidence.AdmittedMaterials, Is.Empty);
            }
        }

        [Test]
        public void OutOfRangeMaterialSlotFailsClosure()
        {
            var initial = NewPoiyomiMaterial();
            var evidence = CaptureVerified(
                new[]
                {
                    ObservationWithObjectBinding(
                        "m_Materials.Array.data[1]", initial),
                },
                new[] { initial },
                EmptyGraph());

            Assert.That(evidence.IsClosed, Is.False);
            Assert.That(evidence.Clips, Is.Empty);
        }

        [Test]
        public void NonSlotObjectBindingSurvivesWithoutLiveValues()
        {
            var initial = NewPoiyomiMaterial();
            var mesh = Own(new Mesh { name = "replacement" });
            var evidence = CaptureVerified(
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
                new[] { initial },
                graph,
                new StubBindings(specialClip),
                TryAttestFixture,
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

        [Test]
        public void EveryAttestationCompletesBeforeClosedCaptureStarts()
        {
            var first = NewPoiyomiMaterial();
            var second = NewLilToonMaterial();
            var attested = 0;

            bool Attest(
                Material material,
                out CapturedAlphaMaterialFamily family,
                out MaterialEvidenceRequest request)
            {
                attested++;
                return TryAttestFixture(material, out family, out request);
            }

            bool Capture(
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                out IReadOnlyList<CapturedAlphaMaterial> captured)
            {
                Assert.That(attested, Is.EqualTo(2));
                return CaptureFixtureMaterials(
                    materials, families, request, out captured);
            }

            var evidence = UnityAnimationEvidenceCapture.CaptureObservedForTests(
                new[] { ObservationWithMaterialSwap(second) },
                new[] { first },
                EmptyGraph(),
                Attest,
                Capture);

            Assert.That(evidence.IsClosed, Is.True);
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
                Array.Empty<LiveClipObservation>(),
                new Material[] { null },
                graph);

            Assert.That(evidence.IsClosed, Is.False);
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
                new[] { ObservationWithMaterialSwap(swapped, false) },
                new[] { initial },
                EmptyGraph());
            var special = CaptureVerified(
                new[] { ObservationWithMaterialSwap(swapped, true) },
                new[] { initial },
                EmptyGraph());

            Assert.That(ordinary.Clips.Single().IsSpecialMotion, Is.False);
            Assert.That(special.Clips.Single().IsSpecialMotion, Is.True);
            CollectionAssert.AreEqual(
                RequestedNames(ordinary.RelevanceRequest),
                RequestedNames(special.RelevanceRequest));
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
                new[] { material },
                EmptyGraph(),
                new StubBindings());

            Assert.That(evidence.IsClosed, Is.False);
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
                    Array.Empty<Material>(),
                    refused,
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
            IReadOnlyList<LiveClipObservation> observations,
            IReadOnlyList<Material> currentSlots,
            CommittedControllerGraphResult graph)
        {
            return UnityAnimationEvidenceCapture.CaptureObservedForTests(
                observations,
                currentSlots,
                graph,
                TryAttestFixture,
                CaptureFixtureMaterials);
        }

        private bool TryAttestFixture(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest request)
        {
            if (_fixtureFamilies.TryGetValue(material, out family))
            {
                request = family == CapturedAlphaMaterialFamily.Poiyomi
                    ? PoiyomiMaterialSemantics.AlphaEvidenceRequest
                    : LilToonMaterialSemantics.AlphaEvidenceRequest;
                return true;
            }

            request = null;
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
            return new LiveClipObservation(
                "swap",
                isSpecialMotion,
                Array.Empty<LiveFloatObservation>(),
                new[]
                {
                    new LiveObjectObservation(
                        "Body",
                        typeof(SkinnedMeshRenderer).FullName,
                        "m_Materials.Array.data[0]",
                        materials.Cast<UnityEngine.Object>().ToArray()),
                });
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
