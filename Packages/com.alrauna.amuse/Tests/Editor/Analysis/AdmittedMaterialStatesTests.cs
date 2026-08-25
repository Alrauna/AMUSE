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

        [Test]
        public void SmallProductsAreBudgeted()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 2, 3, 4 }, out var size), Is.True);
            Assert.That(size, Is.EqualTo(24));
        }

        /// <summary>
        /// The cap is inclusive: the design refuses a product <em>above</em>
        /// the cap, so a product exactly equal to it is still budgeted. The
        /// number is asserted here, and only here, because this fixture owns
        /// the implementation parameter.
        /// </summary>
        [Test]
        public void AProductExactlyAtTheCapIsBudgeted()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 64, 64 }, out var size), Is.True);
            Assert.That(size, Is.EqualTo(4096));
        }

        [Test]
        public void AProductOneAboveTheCapIsRefused()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 4097 }, out _), Is.False);
        }

        [Test]
        public void AnOversizedProductExitsBeforeTheRemainingFactors()
        {
            var counts = new int[64];
            for (var index = 0; index < counts.Length; index++)
            {
                counts[index] = 4;
            }

            Assert.That(AdmittedMaterialStates.TryBudgetProduct(counts, out _),
                Is.False);
        }

        /// <summary>
        /// The first case exits on the leading factor and so proves only that
        /// a huge count is refused. The second is the one that pins the
        /// accumulator width: the running product must already be above one
        /// when the huge factor arrives, or the multiplication that can wrap
        /// never executes. Under a 32-bit accumulator <c>2 * int.MaxValue</c>
        /// wraps to <c>-2</c>, which is not above the cap, and an unbounded
        /// product would be accepted.
        /// </summary>
        [Test]
        public void BudgetingDoesNotOverflowOnHugeCounts()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { int.MaxValue, int.MaxValue }, out _), Is.False);
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 2, int.MaxValue }, out _), Is.False);
        }

        [Test]
        public void AZeroCountYieldsAnEmptyProduct()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 3, 0 }, out var size), Is.True);
            Assert.That(size, Is.Zero);
        }

        /// <summary>
        /// Load-bearing. A single left-to-right pass that returns false as soon
        /// as the running product exceeds the cap reports an oversized product
        /// for <c>[int.MaxValue, 0]</c> while accepting <c>[0, int.MaxValue]</c>
        /// — the same multiset, two answers. The empty product is a property of
        /// the factors, not of their order.
        /// </summary>
        [Test]
        public void AZeroCountEmptiesTheProductWhereverItOccurs()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { int.MaxValue, 0 }, out var zeroLast), Is.True);
            Assert.That(zeroLast, Is.Zero);

            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 0, int.MaxValue }, out var zeroFirst), Is.True);
            Assert.That(zeroFirst, Is.Zero);
        }

        /// <summary>
        /// A renderer with no material slots has one state — the empty tuple —
        /// not zero. This is the multiplicative identity an accumulator seeded
        /// at one naturally represents, and no repository rule assigns zero
        /// slots a different meaning.
        /// </summary>
        [Test]
        public void AnEmptyListIsTheMultiplicativeIdentity()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                Array.Empty<int>(), out var size), Is.True);
            Assert.That(size, Is.EqualTo(1));
        }

        /// <summary>
        /// Singleton admitted properties contribute a factor of one, so this is
        /// the shape the singleton rule actually produces.
        /// </summary>
        [Test]
        public void FactorsOfOneLeaveTheProductAtOne()
        {
            Assert.That(AdmittedMaterialStates.TryBudgetProduct(
                new[] { 1, 1, 1 }, out var size), Is.True);
            Assert.That(size, Is.EqualTo(1));
        }

        /// <summary>
        /// A negative admitted-state count is not a supported-domain outcome:
        /// no slot can admit fewer than zero materials, so it is an internal
        /// invariant violation. The trailing cases prove a zero elsewhere in
        /// the list cannot short-circuit past the invalid evidence — in either
        /// order, since an implementation that returns the empty product as
        /// soon as it sees a zero still validates whatever preceded it.
        /// </summary>
        [Test]
        public void ANegativeCountIsAProgrammingDefect()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AdmittedMaterialStates.TryBudgetProduct(new[] { -1 }, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AdmittedMaterialStates.TryBudgetProduct(
                    new[] { -1, 0 }, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                AdmittedMaterialStates.TryBudgetProduct(
                    new[] { 0, -1 }, out _));
        }

        [Test]
        public void ANullCountListIsAProgrammingDefect()
        {
            Assert.Throws<ArgumentNullException>(() =>
                AdmittedMaterialStates.TryBudgetProduct(null, out _));
        }
    }
}
