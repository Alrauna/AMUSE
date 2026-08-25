using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
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

        // ---------------------------------------------------------------
        // Per-slot admitted-state resolution (Task 19).
        //
        // Every resolution below runs through the real Poiyomi alpha
        // frontend via the verified-material seam. The fixture shader is a
        // schema-complete stand-in that cannot carry the pinned source hash,
        // so `UnityMaterialSemantics.AnalyzeAlphaMaterial` would answer
        // all-Unknown for it and every assertion here would pass vacuously.
        // Injecting the verified seam is the same construction
        // `RendererAlphaAnalysisIntegrationTests` already uses, and Task 24
        // passes the real resolver.
        // ---------------------------------------------------------------

        private const string RendererPath = "Body";

        private readonly List<UnityEngine.Object> _owned =
            new List<UnityEngine.Object>();

        [TearDown]
        public void DestroyOwnedFixtureObjects()
        {
            foreach (var value in _owned)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            _owned.Clear();
        }

        private static MaterialEvidenceRequest Relevance
            => PoiyomiMaterialSemantics.AlphaEvidenceRequest;

        private static bool NoAlphaFields(
            TextureSourceId source,
            TextureChannel channel,
            out AlphaTextureData field)
        {
            field = null;
            return false;
        }

        private static MaterialSemantics VerifiedAlphaOnly(
            CapturedAlphaMaterial material)
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                    material.Evidence),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        private Material NewFixtureMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            _owned.Add(material);
            return material;
        }

        /// <summary>_AlphaForceOpaque == 1 short-circuits alpha to a proven 1.</summary>
        private Material ForcedOpaqueMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        /// <summary>
        /// Not forced opaque, no main texture assigned, and a colour alpha
        /// strictly below one, so alpha is the proven constant 0.5.
        /// </summary>
        private Material TransparentMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            return material;
        }

        private Material MaterialWithForcedOpaque(bool forced)
        {
            return forced ? ForcedOpaqueMaterial() : TransparentMaterial();
        }

        /// <summary>
        /// A material whose shader declares neither _AlphaForceOpaque nor
        /// _MainTex, captured under the same closed request. Its requested
        /// entries are therefore present but valueless, which is the
        /// fail-closed absence case rather than an unrequested-name defect.
        /// </summary>
        private Material MaterialWithoutRequestedProperties()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            _owned.Add(material);
            return material;
        }

        private static IReadOnlyList<CapturedAlphaMaterial> Admitted(
            params Material[] materials)
        {
            var inputs = new MaterialEvidenceCaptureInput[materials.Length];
            for (var index = 0; index < materials.Length; index++)
            {
                inputs[index] = new MaterialEvidenceCaptureInput(
                    materials[index], Relevance);
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            var captured = new CapturedAlphaMaterial[materials.Length];
            for (var index = 0; index < materials.Length; index++)
            {
                captured[index] = new CapturedAlphaMaterial(
                    CapturedAlphaMaterialFamily.Unsupported,
                    evidence[index],
                    default(PoiyomiSourceEvidence),
                    null);
            }

            return captured;
        }

        private static SlotResolutionResult ResolveSlot(
            IReadOnlyList<CapturedAlphaMaterial> admitted,
            IReadOnlyList<int> admittedIndices,
            params (CapturedFloatBinding Binding,
                    AnimatedPropertyRef Reference)[] bindings)
        {
            return AdmittedMaterialStates.ResolveSlot(
                new CapturedMaterialSlotEvidence(0, admittedIndices),
                admitted,
                bindings,
                Relevance,
                NoAlphaFields,
                VerifiedAlphaOnly);
        }

        /// <summary>
        /// The proof-relevant reference is produced by Task 11's real
        /// resolver rather than hand-authored, so every test below depends on
        /// the same derivation the renderer path uses.
        /// </summary>
        private static (CapturedFloatBinding Binding,
                        AnimatedPropertyRef Reference) Animated(
            string property,
            params float[] values)
        {
            return Animated(property, true, values);
        }

        private static (CapturedFloatBinding Binding,
                        AnimatedPropertyRef Reference) Animated(
            string property,
            bool finiteExact,
            params float[] values)
        {
            var binding = new CapturedFloatBinding(
                RendererPath,
                typeof(SkinnedMeshRenderer).FullName,
                "material." + property,
                finiteExact,
                values);
            var resolution = UnityAnimationEvidenceCapture.ResolveProofRelevant(
                binding, RendererPath, Relevance, out var reference);
            Assert.That(
                resolution,
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide),
                "fixture binding must be proof-relevant: " + property);
            return (binding, reference);
        }

        private static TriangleAlphaInput AnyTriangle()
        {
            return TriangleAlphaInput.MissingUv0(
                Vector3.zero, Vector3.right, Vector3.up);
        }

        private static TriangleAlphaOutcome OutcomeOf(AlphaResolution resolution)
        {
            return resolution.Classify(AnyTriangle());
        }

        [Test]
        public void UnanimatedSlotResolvesItsCurrentMaterialOnly()
        {
            var admitted = Admitted(ForcedOpaqueMaterial(), TransparentMaterial());

            var result = ResolveSlot(admitted, new[] { 0 });

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Resolutions.Count, Is.EqualTo(1));
            Assert.That(
                OutcomeOf(result.Resolutions[0]),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        [Test]
        public void SwappedSlotResolvesEveryAdmittedMaterial()
        {
            var admitted = Admitted(ForcedOpaqueMaterial(), TransparentMaterial());

            var result = ResolveSlot(admitted, new[] { 0, 1 });

            Assert.That(result.IsResolved, Is.True);
            Assert.That(result.Resolutions.Count, Is.EqualTo(2));
            Assert.That(
                OutcomeOf(result.Resolutions[0]),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
            Assert.That(
                OutcomeOf(result.Resolutions[1]),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }

        /// <summary>
        /// The admitted material indices address
        /// <c>CapturedAnimationEvidence.AdmittedMaterials</c> directly, so a
        /// slot resolves exactly the materials it names, in the order it names
        /// them — never the slot's own index.
        /// </summary>
        [Test]
        public void AdmittedIndicesSelectMaterialsInTheOrderTheyAreNamed()
        {
            var admitted = Admitted(ForcedOpaqueMaterial(), TransparentMaterial());

            var result = ResolveSlot(admitted, new[] { 1, 0 });

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(result.Resolutions[0]),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
            Assert.That(
                OutcomeOf(result.Resolutions[1]),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        /// <summary>
        /// Two admitted materials whose serialized _AlphaForceOpaque values
        /// differ, and no animated property at all. A renderer-wide default
        /// would resolve both the same way.
        /// </summary>
        [Test]
        public void EachAdmittedMaterialUsesItsOwnSerializedDefaults()
        {
            var admitted = Admitted(
                MaterialWithForcedOpaque(true),
                MaterialWithForcedOpaque(false));

            var result = ResolveSlot(admitted, new[] { 0, 1 });

            Assert.That(result.IsResolved, Is.True);
            var outcomes = result.Resolutions
                .Select(OutcomeOf).Distinct().ToArray();
            Assert.That(
                outcomes.Length,
                Is.EqualTo(2),
                "the two admitted materials must resolve differently, proving " +
                "each used its own serialized defaults rather than one shared " +
                "default");
        }

        /// <summary>
        /// The animated singleton equals this material's captured default, so
        /// it is admitted. This exercises grouping, the per-material default
        /// lookup, admission, substitution, and resolution while leaving the
        /// admitted state exactly the default.
        /// </summary>
        [Test]
        public void ReAssertedAnimatedValueResolvesWithoutChangingTheAdmittedState()
        {
            var admitted = Admitted(MaterialWithForcedOpaque(true));
            var control = ResolveSlot(admitted, new[] { 0 });

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_AlphaForceOpaque", 1f));

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(OutcomeOf(control.Resolutions.Single())),
                "re-asserting the captured default must not change the outcome");
        }

        [Test]
        public void AnimatedValueDifferingFromTheAdmittedMaterialDefaultRefuses()
        {
            var admitted = Admitted(MaterialWithForcedOpaque(false));

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_AlphaForceOpaque", 1f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
            Assert.That(result.Resolutions, Is.Empty);
        }

        [Test]
        public void DisagreeingProofRelevantSourcesRefuseTheSlot()
        {
            var admitted = Admitted(MaterialWithForcedOpaque(true));

            var result = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_AlphaForceOpaque", 1f),
                Animated("_AlphaForceOpaque", 0f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
        }

        /// <summary>
        /// The curve form is unproven even though its sampled value agrees with
        /// the captured default. Finite-exactness is a precondition of
        /// admission, not a tie-breaker among agreeing values.
        /// </summary>
        [Test]
        public void ANonFiniteExactProofRelevantBindingRefusesTheSlot()
        {
            var admitted = Admitted(MaterialWithForcedOpaque(true));

            var result = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_AlphaForceOpaque", false, 1f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .UnsupportedAnimationCurveForm));
        }

        /// <summary>
        /// The property was requested by the closed schema but this admitted
        /// material has no value for it. Task 18's substitution deliberately
        /// preserves that absence; preserving it is never authorization to
        /// ignore the binding, so the slot fails closed before any admission,
        /// substitution, or resolution happens.
        /// </summary>
        [Test]
        public void AnAbsentProofRelevantScalarRefusesBeforeSubstitution()
        {
            var admitted = Admitted(MaterialWithoutRequestedProperties());

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_AlphaForceOpaque", 1f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedPropertyAbsentFromAdmittedMaterial));
            Assert.That(result.Resolutions, Is.Empty);
        }

        /// <summary>
        /// A name the closed schema never requested is a programming defect,
        /// not the absence outcome above. The two must stay distinguishable.
        /// </summary>
        [Test]
        public void AnUnrequestedPropertyNameRemainsAProgrammingDefect()
        {
            var admitted = Admitted(MaterialWithForcedOpaque(true));
            var binding = new CapturedFloatBinding(
                RendererPath,
                typeof(SkinnedMeshRenderer).FullName,
                "material._NeverRequested",
                true,
                new[] { 1f });
            var reference = new AnimatedPropertyRef(
                "_NeverRequested", AnimatedPropertyKind.Scalar, -1);

            Assert.Throws<ArgumentException>(() => AdmittedMaterialStates.ResolveSlot(
                new CapturedMaterialSlotEvidence(0, new[] { 0 }),
                admitted,
                new[] { (binding, reference) },
                Relevance,
                NoAlphaFields,
                VerifiedAlphaOnly));
        }

        /// <summary>
        /// The first admitted material resolves cleanly and the second refuses.
        /// A refusal is a statement about the whole slot, so the earlier
        /// resolution must not survive as a partial prefix.
        /// </summary>
        [Test]
        public void ALaterAdmittedMaterialFailureLeavesNoPartialResolutions()
        {
            var admitted = Admitted(
                MaterialWithForcedOpaque(true),
                MaterialWithForcedOpaque(false));

            var result = ResolveSlot(
                admitted, new[] { 0, 1 }, Animated("_AlphaForceOpaque", 1f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
            Assert.That(result.Resolutions, Is.Empty);
        }

        [Test]
        public void ReAssertedColourComponentResolves()
        {
            var admitted = Admitted(TransparentMaterial());
            var control = ResolveSlot(admitted, new[] { 0 });

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_Color.a", 0.5f));

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(OutcomeOf(control.Resolutions.Single())));
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent),
                "the control must itself be a proven outcome, not Unknown");
        }

        [Test]
        public void AColourComponentDifferingFromItsCapturedDefaultRefuses()
        {
            var admitted = Admitted(TransparentMaterial());

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_Color.a", 0.9f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
        }

        /// <summary>
        /// Both component bindings of one colour property take part in one
        /// admission decision against one captured default. Asserted in both
        /// orders so an implementation that considers only the first or only
        /// the last binding for a property fails.
        /// </summary>
        [Test]
        public void EveryComponentBindingOfOneColourPropertyIsAdmittedTogether()
        {
            var admitted = Admitted(TransparentMaterial());

            var agreeing = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_Color.r", 1f),
                Animated("_Color.a", 0.5f));
            Assert.That(agreeing.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(agreeing.Resolutions.Single()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));

            var differingLast = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_Color.r", 1f),
                Animated("_Color.a", 0.9f));
            var differingFirst = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_Color.a", 0.9f),
                Animated("_Color.r", 1f));

            Assert.That(
                differingLast.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
            Assert.That(
                differingFirst.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
        }

        /// <summary>
        /// _MainTexPan is a genuine <c>VectorProperties</c> request, unlike the
        /// derived _ST name exercised further below.
        /// </summary>
        [Test]
        public void ReAssertedVectorComponentResolves()
        {
            var material = TransparentMaterial();
            material.SetVector("_MainTexPan", new Vector4(0.25f, 0.5f, 0f, 0f));
            var admitted = Admitted(material);
            var control = ResolveSlot(admitted, new[] { 0 });

            var result = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_MainTexPan.x", 0.25f),
                Animated("_MainTexPan.y", 0.5f));

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(OutcomeOf(control.Resolutions.Single())));
        }

        [Test]
        public void AVectorComponentDifferingFromItsCapturedDefaultRefuses()
        {
            var material = TransparentMaterial();
            material.SetVector("_MainTexPan", new Vector4(0.25f, 0.5f, 0f, 0f));
            var admitted = Admitted(material);

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_MainTexPan.y", 0.25f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
        }

        // ---------------------------------------------------------------
        // Texture scale/offset is texture-owned evidence, not vector evidence.
        // ---------------------------------------------------------------

        /// <summary>
        /// Distinct scale and offset values, so any transposition of the
        /// _ST halves changes the answer instead of coinciding with it.
        /// </summary>
        private Material ForcedOpaqueMaterialWithScaleOffset()
        {
            var material = ForcedOpaqueMaterial();
            material.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            material.SetTextureOffset("_MainTex", new Vector2(4f, 5f));
            return material;
        }

        /// <summary>
        /// The derived name exists only because _MainTex was requested with
        /// <c>TextureEvidenceKinds.ScaleOffset</c>; it is deliberately not a
        /// vector request, and hand-authoring it into
        /// <c>VectorProperties</c> would make the whole group vacuous.
        /// </summary>
        [Test]
        public void TheDerivedScaleOffsetNameIsNotVectorEvidence()
        {
            Assert.That(Relevance.VectorProperties, Does.Not.Contain("_MainTex_ST"));
            var mainTex = Relevance.TextureProperties
                .Single(request => request.PropertyName == "_MainTex");
            Assert.That(
                mainTex.Evidence & TextureEvidenceKinds.ScaleOffset,
                Is.EqualTo(TextureEvidenceKinds.ScaleOffset));

            var (_, reference) = Animated("_MainTex_ST.x", 1f);

            Assert.That(
                reference.Kind,
                Is.EqualTo(AnimatedPropertyKind.TextureScaleOffsetComponent));
            Assert.That(reference.PropertyName, Is.EqualTo("_MainTex_ST"));
            Assert.That(reference.ComponentIndex, Is.Zero);
        }

        /// <summary>
        /// All four components re-asserted against a material whose scale and
        /// offset differ in every component. Because _MainTex_ST is never a
        /// vector request, an implementation reaching for
        /// <c>WithVector("_MainTex_ST", ...)</c> throws here rather than
        /// substituting, and one that transposes the scale and offset halves
        /// refuses instead of admitting.
        /// </summary>
        [Test]
        public void ReAssertedScaleOffsetComponentsResolve()
        {
            Assert.That(Relevance.VectorProperties, Does.Not.Contain("_MainTex_ST"));
            var admitted = Admitted(ForcedOpaqueMaterialWithScaleOffset());
            var control = ResolveSlot(admitted, new[] { 0 });

            var result = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_MainTex_ST.x", 2f),
                Animated("_MainTex_ST.y", 3f),
                Animated("_MainTex_ST.z", 4f),
                Animated("_MainTex_ST.w", 5f));

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(OutcomeOf(control.Resolutions.Single())));
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque),
                "the control must itself be a proven outcome, not Unknown");
        }

        [Test]
        public void ADifferingScaleComponentRefuses()
        {
            var admitted = Admitted(ForcedOpaqueMaterialWithScaleOffset());

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_MainTex_ST.y", 99f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
            Assert.That(result.Resolutions, Is.Empty);
        }

        [Test]
        public void ADifferingOffsetComponentRefuses()
        {
            var admitted = Admitted(ForcedOpaqueMaterialWithScaleOffset());

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_MainTex_ST.z", 99f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedMaterialPropertyNotSingleton));
            Assert.That(result.Resolutions, Is.Empty);
        }

        [Test]
        public void ANonFiniteExactScaleOffsetComponentRefuses()
        {
            var admitted = Admitted(ForcedOpaqueMaterialWithScaleOffset());

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_MainTex_ST.x", false, 2f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .UnsupportedAnimationCurveForm));
        }

        /// <summary>
        /// Presence for a texture's scale/offset is carried by
        /// <c>CapturedTextureAssignment</c>. An implementation asking
        /// <c>TryGetVector("_MainTex_ST", ...)</c> would raise
        /// <see cref="ArgumentException"/> for a name that was never requested
        /// as a vector, and reinterpreting that defect as this domain refusal
        /// is exactly what must not happen.
        /// </summary>
        [Test]
        public void AbsentScaleOffsetEvidenceRefuses()
        {
            var admitted = Admitted(MaterialWithoutRequestedProperties());
            Assert.That(
                admitted[0].Evidence.TryGetTexture("_MainTex", out _),
                Is.False,
                "the fixture must genuinely lack captured texture evidence");

            var result = ResolveSlot(
                admitted, new[] { 0 }, Animated("_MainTex_ST.x", 1f));

            Assert.That(result.IsResolved, Is.False);
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal
                    .AnimatedPropertyAbsentFromAdmittedMaterial));
            Assert.That(result.Resolutions, Is.Empty);
        }

        /// <summary>
        /// A composition smoke test: one slot carrying an _ST binding
        /// alongside ordinary scalar and colour admissions resolves through
        /// all three paths together.
        /// <para>
        /// It deliberately does <em>not</em> prove sibling preservation.
        /// Under V1 every admitted substitution writes back a value equal to
        /// the captured default, so <c>WithScalar</c> and <c>WithColor</c> are
        /// semantically no-ops and an _ST arm that reset the accumulated
        /// evidence to the original capture would produce an identical
        /// outcome. Sibling preservation is therefore structural here — the
        /// _ST arm assigns nothing at all — and becomes observable only if
        /// admission is ever widened past exact re-assertion. Writing an
        /// assertion that appeared to prove it today would be a false guard.
        /// </para>
        /// </summary>
        [Test]
        public void AScaleOffsetBindingComposesWithOrdinaryAdmittedProperties()
        {
            var material = TransparentMaterial();
            material.SetTextureScale("_MainTex", new Vector2(2f, 3f));
            material.SetTextureOffset("_MainTex", new Vector2(4f, 5f));
            var admitted = Admitted(material);

            var result = ResolveSlot(
                admitted,
                new[] { 0 },
                Animated("_Color.a", 0.5f),
                Animated("_MainTex_ST.z", 4f),
                Animated("_AlphaForceOpaque", 0f));

            Assert.That(result.IsResolved, Is.True);
            Assert.That(
                OutcomeOf(result.Resolutions.Single()),
                Is.EqualTo(TriangleAlphaOutcome.MustRemainTransparent));
        }
    }
}
