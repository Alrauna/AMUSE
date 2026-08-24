using System.Linq;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityAnimationEvidenceCaptureTests
    {
        private static readonly EditorCurveBinding FloatBinding =
            EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "material._Cutoff");

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
    }
}
