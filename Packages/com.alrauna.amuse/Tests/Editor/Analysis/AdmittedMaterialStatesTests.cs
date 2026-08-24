using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEngine;

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

        private static IReadOnlyDictionary<
            int, IReadOnlyList<CapturedFloatBinding>> Components(
            params (int Index, float Value)[] entries)
        {
            var components = new Dictionary<
                int, IReadOnlyList<CapturedFloatBinding>>();
            foreach (var entry in entries)
            {
                components.Add(
                    entry.Index,
                    new[] { Binding(true, entry.Value) });
            }

            return components;
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

        [Test]
        public void AnimatedAlphaReassertsItsDefaultAndPreservesTheWholeColor()
        {
            var serialized = new Color(0.1f, 0.2f, 0.3f, 0.4f);

            var outcome = AdmittedMaterialStates.AdmitColor(
                Components((3, 0.4f)), serialized, out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(serialized));
        }

        [Test]
        public void AnimatedAlphaDoesNotOverrideADifferentDefault()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitColor(
                    Components((3, 1f)),
                    new Color(0.1f, 0.2f, 0.3f, 0.4f),
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void DisagreeingColorComponentSourcesRefuseTheWholeColor()
        {
            var components = new Dictionary<
                int, IReadOnlyList<CapturedFloatBinding>>
            {
                [3] = new[] { Binding(true, 0.4f), Binding(true, 1f) },
            };

            Assert.That(
                AdmittedMaterialStates.AdmitColor(
                    components,
                    new Color(0.1f, 0.2f, 0.3f, 0.4f),
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void ComponentThreePreservesUnanimatedRgbComponents()
        {
            var outcome = AdmittedMaterialStates.AdmitColor(
                Components((3, 1f)),
                new Color(0.25f, 0.5f, 0.75f, 1f),
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(
                admitted,
                Is.EqualTo(new Color(0.25f, 0.5f, 0.75f, 1f)));
        }

        [Test]
        public void AnimatedVectorComponentsReassertTheirOwnDefaults()
        {
            var serialized = new Vector4(1f, 1f, 0f, 0f);

            var outcome = AdmittedMaterialStates.AdmitVector(
                Components((0, 1f), (1, 1f)),
                serialized,
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(serialized));
        }

        [Test]
        public void AnimatedVectorComponentDoesNotOverrideADifferentDefault()
        {
            Assert.That(
                AdmittedMaterialStates.AdmitVector(
                    Components((0, 2f)),
                    new Vector4(1f, 1f, 0f, 0f),
                    out _),
                Is.EqualTo(AdmittedPropertyOutcome.SourcesDisagree));
        }

        [Test]
        public void NonFiniteExactVectorComponentRefuses()
        {
            var components = new Dictionary<
                int, IReadOnlyList<CapturedFloatBinding>>
            {
                [0] = new[] { Binding(false, 1f) },
            };

            Assert.That(
                AdmittedMaterialStates.AdmitVector(
                    components, new Vector4(1f, 2f, 3f, 4f), out _),
                Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
        }

        [Test]
        public void UnanimatedVectorComponentsPreserveSerializedValues()
        {
            var outcome = AdmittedMaterialStates.AdmitVector(
                Components((0, 1f)),
                new Vector4(1f, 2f, 3f, 4f),
                out var admitted);

            Assert.That(outcome, Is.EqualTo(AdmittedPropertyOutcome.Singleton));
            Assert.That(admitted, Is.EqualTo(new Vector4(1f, 2f, 3f, 4f)));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidComponentIndexIsAProgrammingDefect(int component)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AdmittedMaterialStates.AdmitVector(
                    Components((component, 1f)), Vector4.one, out _));
        }

        [Test]
        public void NonFiniteExactPrecedesDisagreementRegardlessOfMapOrder()
        {
            var disagreementFirst = new Dictionary<
                int, IReadOnlyList<CapturedFloatBinding>>
            {
                [0] = new[] { Binding(true, 2f) },
                [3] = new[] { Binding(false, 4f) },
            };
            var nonFiniteFirst = new Dictionary<
                int, IReadOnlyList<CapturedFloatBinding>>
            {
                [3] = new[] { Binding(false, 4f) },
                [0] = new[] { Binding(true, 2f) },
            };
            var serialized = new Vector4(1f, 2f, 3f, 4f);

            Assert.That(
                AdmittedMaterialStates.AdmitVector(
                    disagreementFirst, serialized, out _),
                Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
            Assert.That(
                AdmittedMaterialStates.AdmitVector(
                    nonFiniteFirst, serialized, out _),
                Is.EqualTo(AdmittedPropertyOutcome.NotFiniteExact));
        }
    }
}
