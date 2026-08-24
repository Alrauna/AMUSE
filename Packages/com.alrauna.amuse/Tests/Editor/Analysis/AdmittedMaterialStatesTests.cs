using System;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class AdmittedMaterialStatesTests
    {
        private static CapturedFloatBinding Binding(
            bool finiteExact,
            params float[] values)
        {
            return new CapturedFloatBinding(
                "Body",
                "SkinnedMeshRenderer",
                "material._Cutoff",
                finiteExact,
                values);
        }

        [Test]
        public void AgreeingSourcesAndDefaultAdmitTheSingleValue()
        {
            var outcome = AdmittedMaterialStates.AdmitScalar(
                new[] { Binding(true, 1f), Binding(true, 1f) },
                1f,
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(1f));
        }

        [Test]
        public void DisagreeingAnimationSourcesRefuse()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[] { Binding(true, 1f), Binding(true, 0f) },
                    1f,
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void AnimationDoesNotOverrideADifferentSerializedDefault()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[] { Binding(true, 1f) },
                    0f,
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void AnyNonFiniteExactBindingRefuses()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[]
                    {
                        Binding(true, 1f),
                        Binding(false, 1f),
                    },
                    1f,
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
        }

        [Test]
        public void MultipleValuesInOneFiniteExactBindingRefuse()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[] { Binding(true, 0f, 1f) },
                    1f,
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void RepeatedIdenticalValuesAdmit()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[] { Binding(true, 1f, 1f, 1f) },
                    1f,
                    out var admitted),
                Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(1f));
        }

        [Test]
        public void SerializedDefaultAloneAdmits()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    Array.Empty<CapturedFloatBinding>(),
                    0.25f,
                    out var admitted),
                Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(0.25f));
        }

        [Test]
        public void AdjacentRepresentableFloatsAreNotApproximatelyEqual()
        {
            var adjacent = BitConverter.Int32BitsToSingle(
                BitConverter.SingleToInt32Bits(1f) + 1);

            Assert.That(adjacent, Is.Not.EqualTo(1f));
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[] { Binding(true, adjacent) },
                    1f,
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void NaNDoesNotBecomeASingletonThroughFloatEqualsSemantics()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitScalar(
                    new[] { Binding(true, float.NaN) },
                    float.NaN,
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }
    }
}
