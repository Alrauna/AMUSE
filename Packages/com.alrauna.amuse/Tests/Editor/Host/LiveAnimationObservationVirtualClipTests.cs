using System.Linq;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// The virtual-clip observation must produce the same facts as the
    /// real-clip observation: one float entry with the exact key values and
    /// the finite-exact flag, and one object entry with the referenced
    /// materials in key order.
    /// </summary>
    public sealed class LiveAnimationObservationVirtualClipTests
    {
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            _clip = new AnimationClip { name = "AMUSE virtual observe" };
            var curve = AnimationCurve.Linear(0f, 0.25f, 1f, 0.75f);
            _clip.SetCurve(
                "slot", typeof(SkinnedMeshRenderer),
                "material._Cutoff", curve);
            AnimationUtility.SetObjectReferenceCurve(
                _clip,
                EditorCurveBinding.PPtrCurve(
                    "slot", typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]"),
                new[]
                {
                    new ObjectReferenceKeyframe
                    {
                        time = 0f,
                        value = new Material(Shader.Find("Unlit/Color"))
                        {
                            name = "AMUSE observe variant",
                        },
                    },
                });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void ObserveVirtualClipMatchesObserveClipSemantics()
        {
            var cloneContext = new CloneContext(
                GenericPlatformAnimatorBindings.Instance);
            var virtualClip = VirtualClip.Clone(cloneContext, _clip);

            var fromReal = LiveAnimationObservation.ObserveClip(
                _clip, false);
            var fromVirtual = LiveAnimationObservation.ObserveVirtualClip(
                virtualClip, false);

            Assert.That(fromVirtual.Name, Is.EqualTo(fromReal.Name));
            Assert.That(fromVirtual.IsSpecialMotion,
                Is.EqualTo(fromReal.IsSpecialMotion));

            Assert.That(
                fromVirtual.Floats.Select(f => f.PropertyName).ToArray(),
                Is.EqualTo(
                    fromReal.Floats.Select(f => f.PropertyName).ToArray()));
            var realFloat = fromReal.Floats.Single();
            var virtualFloat = fromVirtual.Floats.Single();
            Assert.That(virtualFloat.IsFiniteExact,
                Is.EqualTo(realFloat.IsFiniteExact));
            Assert.That(virtualFloat.Values.ToArray(),
                Is.EqualTo(realFloat.Values.ToArray()));

            Assert.That(
                fromVirtual.Objects.Select(o => o.PropertyName).ToArray(),
                Is.EqualTo(
                    fromReal.Objects.Select(o => o.PropertyName).ToArray()));
            var realObject = fromReal.Objects.Single();
            var virtualObject = fromVirtual.Objects.Single();
            Assert.That(
                virtualObject.Values.Select(v => v.name).ToArray(),
                Is.EqualTo(
                    realObject.Values.Select(v => v.name).ToArray()));
        }

        [Test]
        public void SingleNonFiniteKeyIsNotFiniteExact()
        {
            var clip = new AnimationClip { name = "SingleNonFiniteKey" };
            var binding = EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "m_BlendShape.Weight");
            AnimationUtility.SetEditorCurve(
                clip, binding, new AnimationCurve(new Keyframe(0f, float.NaN)));

            var observed = LiveAnimationObservation.ObserveClip(clip, false);

            Assert.That(observed.Floats[0].IsFiniteExact, Is.False);
        }

        [Test]
        public void AllEqualInfiniteRunIsNotFiniteExact()
        {
            var clip = new AnimationClip { name = "AllEqualInfiniteRun" };
            var binding = EditorCurveBinding.FloatCurve(
                "Body", typeof(SkinnedMeshRenderer), "m_BlendShape.Weight");
            var curve = new AnimationCurve(
                new Keyframe(0f, float.PositiveInfinity),
                new Keyframe(1f, float.PositiveInfinity));
            AnimationUtility.SetEditorCurve(clip, binding, curve);

            var observed = LiveAnimationObservation.ObserveClip(clip, false);

            Assert.That(observed.Floats[0].IsFiniteExact, Is.False);
        }
    }
}
