using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Tests for the pinned Poiyomi opaque-conversion core.
    /// <para>
    /// The expected canonical tuple and conversion schema are stated literally
    /// here, transcribed from the design's verified vendor reading, and never
    /// derived from the production constants. A test that read its expectation
    /// from <c>PoiyomiOpaqueConversion</c> would let a wrong production tuple
    /// test itself.
    /// </para>
    /// </summary>
    public sealed class PoiyomiOpaqueConversionTests : PoiyomiFixtureTestBase
    {
        /// <summary>
        /// The 24 properties conversion reads: the 23 canonical recipe
        /// properties plus the eligibility-only <c>_EnableOutlines</c>.
        /// <c>_AddBlendOp</c> and <c>_AddBlendOpAlpha</c> are deliberately
        /// absent - the recipe never writes them, so the unchanged blend
        /// operation cancels once the factors are proven equivalent at alpha 1.
        /// </summary>
        private static readonly string[] ExpectedConversionSchema =
        {
            "_Mode", "_AlphaForceOpaque", "_BlendOp", "_BlendOpAlpha", "_Cutoff",
            "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha",
            "_AddSrcBlend", "_AddDstBlend", "_AddSrcBlendAlpha", "_AddDstBlendAlpha",
            "_AlphaToCoverage", "_ZWrite", "_ZTest", "_AlphaPremultiply",
            "_OutlineSrcBlend", "_OutlineDstBlend", "_OutlineSrcBlendAlpha",
            "_OutlineDstBlendAlpha", "_OutlineBlendOp", "_OutlineBlendOpAlpha",
            "_EnableOutlines",
        };

        /// <summary>
        /// The complete canonical Opaque tuple, transcribed from the pinned
        /// Poiyomi 9.3.64 `_Mode` preset 0 `on_value_actions` metadata: 22
        /// property actions plus the `_Mode` value the preset itself selects.
        /// The render queue (2000) and the `RenderType` tag ("Opaque") are the
        /// preset's other two actions and are asserted separately, because they
        /// are not material properties.
        /// </summary>
        private static readonly (string Property, float Value)[] ExpectedCanonicalTuple =
        {
            ("_Mode", 0f),
            ("_AlphaForceOpaque", 1f),
            ("_BlendOp", 0f),
            ("_BlendOpAlpha", 4f),
            ("_Cutoff", 0f),
            ("_SrcBlend", 1f),
            ("_DstBlend", 0f),
            ("_SrcBlendAlpha", 1f),
            ("_DstBlendAlpha", 1f),
            ("_AddSrcBlend", 1f),
            ("_AddDstBlend", 1f),
            ("_AddSrcBlendAlpha", 0f),
            ("_AddDstBlendAlpha", 1f),
            ("_AlphaToCoverage", 0f),
            ("_ZWrite", 1f),
            ("_ZTest", 4f),
            ("_AlphaPremultiply", 0f),
            ("_OutlineSrcBlend", 1f),
            ("_OutlineDstBlend", 0f),
            ("_OutlineSrcBlendAlpha", 1f),
            ("_OutlineDstBlendAlpha", 0f),
            ("_OutlineBlendOp", 0f),
            ("_OutlineBlendOpAlpha", 4f),
        };

        [Test]
        public void ExpectedCanonicalTuple_HasTwentyThreeProperties()
        {
            Assert.That(ExpectedCanonicalTuple.Length, Is.EqualTo(23));
        }

        [Test]
        public void CanonicalOpaqueProperties_MatchTheIndependentlyStatedTuple()
        {
            var actual = PoiyomiOpaqueConversion.CanonicalOpaqueProperties;

            Assert.That(actual.Count, Is.EqualTo(23));
            CollectionAssert.AreEquivalent(
                ExpectedCanonicalTuple, actual.ToArray());
        }

        [Test]
        public void CanonicalNonPropertyFacts_AreQueueTwoThousandAndOpaqueTag()
        {
            Assert.That(
                PoiyomiOpaqueConversion.CanonicalOpaqueRenderQueue,
                Is.EqualTo(2000));
            Assert.That(
                PoiyomiOpaqueConversion.RenderTypeTagName,
                Is.EqualTo("RenderType"));
            Assert.That(
                PoiyomiOpaqueConversion.CanonicalOpaqueRenderType,
                Is.EqualTo("Opaque"));
        }

        [Test]
        public void ConversionRequiredSchema_MatchesTheIndependentlyStatedSchema()
        {
            var actual = PoiyomiOpaqueConversion.ConversionRequiredSchemaProperties;

            Assert.That(actual.Count, Is.EqualTo(24));
            CollectionAssert.AreEquivalent(ExpectedConversionSchema, actual);
        }

        [Test]
        public void ConversionEvidenceRequest_RequestsTheSchemaPlusLockedFlag()
        {
            var request = PoiyomiOpaqueConversion.ConversionEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties.Count, Is.EqualTo(24));
            Assert.That(request.ScalarProperties.Count, Is.EqualTo(25));
            Assert.That(
                request.ScalarProperties,
                Has.Member("_ShaderOptimizerEnabled"));
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        /// <summary>
        /// The ForwardAdd blend operation and its alpha counterpart are never
        /// written by the canonical recipe, so they cancel from the comparison
        /// and are not conversion dependencies. Pinning their absence means
        /// reintroducing a gate on either one fails a test.
        /// </summary>
        [Test]
        public void ConversionEvidenceRequest_ExcludesTheUnwrittenAddBlendOps()
        {
            var request = PoiyomiOpaqueConversion.ConversionEvidenceRequest;

            Assert.That(request.ScalarProperties, Has.No.Member("_AddBlendOp"));
            Assert.That(request.ScalarProperties, Has.No.Member("_AddBlendOpAlpha"));
            Assert.That(request.PresenceProperties, Has.No.Member("_AddBlendOp"));
            Assert.That(request.PresenceProperties, Has.No.Member("_AddBlendOpAlpha"));
            Assert.That(
                PoiyomiOpaqueConversion.CanonicalOpaqueProperties.Select(p => p.Property),
                Has.No.Member("_AddBlendOp"));
            Assert.That(
                PoiyomiOpaqueConversion.CanonicalOpaqueProperties.Select(p => p.Property),
                Has.No.Member("_AddBlendOpAlpha"));
        }

        // --- Relevance isolation ---------------------------------------------

        private static CapturedFloatBinding Bound(string property)
        {
            return new CapturedFloatBinding(
                "Body",
                typeof(SkinnedMeshRenderer).FullName,
                property,
                true,
                new[] { 1f });
        }

        private static ProofRelevantBindingResolution ResolveUnder(
            string binding, MaterialEvidenceRequest relevance)
        {
            return UnityAnimationEvidenceCapture.ResolveProofRelevant(
                Bound(binding), "Body", relevance, out _);
        }

        /// <summary>
        /// The load-bearing separation. A curve on conversion-only render state
        /// must stay irrelevant to ordinary alpha analysis, which does not
        /// depend on it, while being relevant to conversion. Folding conversion
        /// state into the alpha request would make unrelated analysis refuse on
        /// state it never reads - a coverage regression, not a safety
        /// improvement.
        /// <para>
        /// This is the one place these tests reference both requests, and it
        /// does so to prove non-widening rather than to attest anything.
        /// </para>
        /// </summary>
        [TestCase("material._ZWrite")]
        [TestCase("material._EnableOutlines")]
        public void ConversionOnlyState_IsIrrelevantToAlphaButRelevantToConversion(
            string binding)
        {
            Assert.That(
                ResolveUnder(binding, PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
            Assert.That(
                ResolveUnder(binding, PoiyomiOpaqueConversion.ConversionEvidenceRequest),
                Is.EqualTo(ProofRelevantBindingResolution.RendererWide));
        }

        /// <summary>
        /// Conversion depends on <c>_AddBlendOp</c> in neither direction, so a
        /// curve on it is irrelevant to both.
        /// </summary>
        [TestCase("material._AddBlendOp")]
        [TestCase("material._AddBlendOpAlpha")]
        public void UnwrittenAddBlendOps_AreIrrelevantToBothRequests(string binding)
        {
            Assert.That(
                ResolveUnder(binding, PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
            Assert.That(
                ResolveUnder(binding, PoiyomiOpaqueConversion.ConversionEvidenceRequest),
                Is.EqualTo(ProofRelevantBindingResolution.Irrelevant));
        }

        // --- Clone preparation and validation --------------------------------

        /// <summary>
        /// Every float-valued property the shader declares, plus the two
        /// non-property facts. Used to prove the source is untouched.
        /// </summary>
        private static Dictionary<string, float> SnapshotFloats(Material material)
        {
            var snapshot = new Dictionary<string, float>();
            var count = ShaderUtil.GetPropertyCount(material.shader);
            for (var index = 0; index < count; index++)
            {
                var type = ShaderUtil.GetPropertyType(material.shader, index);
                if (type != ShaderUtil.ShaderPropertyType.Float &&
                    type != ShaderUtil.ShaderPropertyType.Range)
                {
                    continue;
                }

                var name = ShaderUtil.GetPropertyName(material.shader, index);
                snapshot[name] = material.GetFloat(name);
            }

            return snapshot;
        }

        private static void AssertUnchanged(
            Material material,
            Dictionary<string, float> before,
            int queueBefore,
            string renderTypeBefore)
        {
            var after = SnapshotFloats(material);
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (var entry in before)
            {
                Assert.That(
                    after[entry.Key],
                    Is.EqualTo(entry.Value),
                    $"Source property '{entry.Key}' was mutated.");
            }

            Assert.That(material.renderQueue, Is.EqualTo(queueBefore));
            Assert.That(
                material.GetTag("RenderType", false), Is.EqualTo(renderTypeBefore));
        }

        [Test]
        public void PreparedClone_CarriesEveryCanonicalFact()
        {
            var source = ConvertibleFade();

            var clone = Track(PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(source));

            foreach (var (property, value) in ExpectedCanonicalTuple)
            {
                Assert.That(
                    clone.GetFloat(property),
                    Is.EqualTo(value),
                    $"Prepared clone must carry canonical '{property}'.");
            }

            Assert.That(clone.renderQueue, Is.EqualTo(2000));
            Assert.That(clone.GetTag("RenderType", false), Is.EqualTo("Opaque"));
        }

        /// <summary>
        /// Per-field falsifiability. Perturbing any single canonical fact on an
        /// already-prepared clone must be detected AND named. Driven by the
        /// independently stated tuple, so a production recipe that dropped a
        /// field could not pass.
        /// </summary>
        [Test]
        public void TryFindNonCanonicalFact_DetectsAndNamesEveryPerturbedProperty()
        {
            foreach (var (property, value) in ExpectedCanonicalTuple)
            {
                var clone = Track(
                    PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(ConvertibleFade()));
                clone.SetFloat(property, value + 1f);

                var found = PoiyomiOpaqueConversion.TryFindNonCanonicalFact(
                    clone, out var fact);

                Assert.That(found, Is.True, $"Perturbed '{property}' went undetected.");
                Assert.That(fact, Is.EqualTo(property));
            }
        }

        [Test]
        public void TryFindNonCanonicalFact_DetectsAndNamesAPerturbedRenderQueue()
        {
            var clone = Track(
                PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(ConvertibleFade()));
            clone.renderQueue = 3000;

            var found = PoiyomiOpaqueConversion.TryFindNonCanonicalFact(
                clone, out var fact);

            Assert.That(found, Is.True);
            Assert.That(fact, Is.EqualTo("renderQueue"));
        }

        [Test]
        public void TryFindNonCanonicalFact_DetectsAndNamesAPerturbedRenderTypeTag()
        {
            var clone = Track(
                PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(ConvertibleFade()));
            clone.SetOverrideTag("RenderType", "Transparent");

            var found = PoiyomiOpaqueConversion.TryFindNonCanonicalFact(
                clone, out var fact);

            Assert.That(found, Is.True);
            Assert.That(fact, Is.EqualTo("RenderType"));
        }

        [Test]
        public void Preparation_LeavesTheSourceMaterialUntouched()
        {
            var source = ConvertibleFade();
            var before = SnapshotFloats(source);
            var queueBefore = source.renderQueue;
            var tagBefore = source.GetTag("RenderType", false);

            Track(PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(source));

            AssertUnchanged(source, before, queueBefore, tagBefore);
        }

        [Test]
        public void RefusedEvaluation_LeavesTheSourceMaterialUntouched()
        {
            var source = ConvertibleFade();
            source.SetFloat("_EnableOutlines", 1f);
            var before = SnapshotFloats(source);
            var queueBefore = source.renderQueue;
            var tagBefore = source.GetTag("RenderType", false);

            AssertRefusal(
                EvaluateFor(source),
                PoiyomiOpaqueConversionRefusal.OutlinesEnabled);

            AssertUnchanged(source, before, queueBefore, tagBefore);
        }

        [Test]
        public void PreparedClone_IsADistinctMaterialSharingTheSourceShader()
        {
            var source = ConvertibleFade();

            var clone = Track(PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(source));

            Assert.That(ReferenceEquals(clone, source), Is.False);
            Assert.That(clone.shader, Is.SameAs(source.shader));
        }

        /// <summary>
        /// The clone is transient. Persistence belongs to assignment, which is
        /// the consumer's job: NDMF serializes assets reachable from the avatar
        /// root at build end, while its cleanup destroys only unreferenced
        /// components and game objects, so a material saved eagerly and then
        /// abandoned is welded into the generated-asset container forever.
        /// </summary>
        [Test]
        public void PreparedClone_IsNotPersisted()
        {
            var clone = Track(
                PoiyomiOpaqueConversion.PrepareCanonicalOpaqueClone(ConvertibleFade()));

            Assert.That(AssetDatabase.Contains(clone), Is.False);
            Assert.That(AssetDatabase.GetAssetPath(clone), Is.Empty);
        }

        // --- Transformation gates -------------------------------------------

        /// <summary>
        /// A Fade-derived tuple: eligible, and deliberately NOT canonical
        /// (queue 3000, SrcAlpha/OneMinusSrcAlpha blend, depth write off,
        /// non-zero cutoff). Every gate test starts here so the row exercises
        /// its gate rather than the AlreadyOpaque classification.
        /// </summary>
        private Material ConvertibleFade()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_Mode", 2f);
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_Cutoff", 0.002f);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AddSrcBlend", 5f);
            material.SetFloat("_EnableOutlines", 0f);
            material.renderQueue = 3000;
            material.SetOverrideTag("RenderType", "Transparent");
            return material;
        }

        private static void AssertRefusal(
            PoiyomiOpaqueConversionEligibility result,
            PoiyomiOpaqueConversionRefusal expected)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.Refused));
            Assert.That(result.Refusal, Is.EqualTo(expected));
        }

        private static void AssertConvertible(
            PoiyomiOpaqueConversionEligibility result)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.Convertible),
                "refusal was " + result.Refusal);
        }

        [Test]
        public void ConvertibleFadeBaseline_IsConvertible()
        {
            AssertConvertible(EvaluateFor(ConvertibleFade()));
        }

        /// <summary>
        /// Every one of the 24 conversion-read properties must refuse when it is
        /// not finite, not just the few a hand-picked list happens to name. The
        /// cases are driven from the independently stated
        /// <see cref="ExpectedConversionSchema"/> rather than from the
        /// production request, so a property dropped from the production schema
        /// cannot also drop its own finiteness coverage.
        /// <para>
        /// Non-finite values are supplied through the captured-evidence
        /// primitive because the evaluator is pure over evidence: an input it
        /// must handle need not be reachable by writing a live material.
        /// </para>
        /// </summary>
        [Test]
        public void NonFiniteConversionProperty_RefusesForEverySchemaProperty()
        {
            var nonFinite = new[]
            {
                float.NaN, float.PositiveInfinity, float.NegativeInfinity,
            };

            Assert.That(ExpectedConversionSchema.Length, Is.EqualTo(24));

            foreach (var property in ExpectedConversionSchema)
            {
                var material = ConvertibleFade();
                foreach (var value in nonFinite)
                {
                    var result = EvaluateWith(material, property, value);

                    Assert.That(
                        result.Outcome,
                        Is.EqualTo(PoiyomiOpaqueConversionOutcome.Refused),
                        $"Non-finite '{property}' ({value}) must refuse.");
                    Assert.That(
                        result.Refusal,
                        Is.EqualTo(PoiyomiOpaqueConversionRefusal
                            .ConversionPropertyNotFinite),
                        $"Non-finite '{property}' ({value}) named the wrong refusal.");
                }
            }
        }

        [TestCase(1f)]
        [TestCase(0.5f)]
        [TestCase(0.005f)]
        [TestCase(-1f)]
        public void EnabledOutlines_RefuseConversion(float enableOutlines)
        {
            var material = ConvertibleFade();
            material.SetFloat("_EnableOutlines", enableOutlines);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.OutlinesEnabled);
        }

        /// <summary>
        /// A perfect base-alpha proof does not rescue enabled outlines. The
        /// vendor's outline pass writes alpha from outline texture/colour and an
        /// optional distance fade, none of which AMUSE models, and then
        /// <c>_Mode == Opaque</c> forces that alpha to 1 before the outline
        /// clip - resurrecting outline fragments the author faded away.
        /// </summary>
        [Test]
        public void EnabledOutlines_RefuseEvenWhenBaseAlphaIsExactlyOne()
        {
            var material = ConvertibleFade();
            material.SetFloat("_AlphaForceOpaque", 1f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetFloat("_EnableOutlines", 1f);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.OutlinesEnabled);
        }

        [Test]
        public void DisabledOutlines_AreConvertible()
        {
            var material = ConvertibleFade();
            material.SetFloat("_EnableOutlines", 0f);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void PremultipliedAlpha_Refuses()
        {
            var material = ConvertibleFade();
            material.SetFloat("_AlphaPremultiply", 1f);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.PremultipliedAlphaEnabled);
        }

        [Test]
        public void AlphaToCoverage_Refuses()
        {
            var material = ConvertibleFade();
            material.SetFloat("_AlphaToCoverage", 1f);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.AlphaToCoverageEnabled);
        }

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(3f)]
        [TestCase(5f)]
        [TestCase(6f)]
        [TestCase(7f)]
        [TestCase(8f)]
        public void NonLEqualDepthComparison_Refuses(float zTest)
        {
            var material = ConvertibleFade();
            material.SetFloat("_ZTest", zTest);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.UnsupportedDepthComparison);
        }

        [Test]
        public void LEqualDepthComparison_IsConvertible()
        {
            var material = ConvertibleFade();
            material.SetFloat("_ZTest", 4f);

            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, 1f, 10f)]
        [TestCase(0f, 5f, 0f)]
        [TestCase(0f, 5f, 10f)]
        public void OpaqueEquivalentBaseBlend_IsConvertible(
            float blendOp, float srcBlend, float dstBlend)
        {
            var material = ConvertibleFade();
            material.SetFloat("_BlendOp", blendOp);
            material.SetFloat("_SrcBlend", srcBlend);
            material.SetFloat("_DstBlend", dstBlend);

            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(1f, 1f, 0f, TestName = "BlendOpSubtract")]
        [TestCase(4f, 1f, 0f, TestName = "BlendOpMax")]
        [TestCase(0f, 1f, 1f, TestName = "Additive")]
        [TestCase(0f, 4f, 1f, TestName = "SoftAdditive")]
        [TestCase(0f, 2f, 0f, TestName = "Multiplicative")]
        [TestCase(0f, 2f, 3f, TestName = "TwoXMultiplicative")]
        [TestCase(0f, 7f, 0f, TestName = "DstAlphaSource")]
        public void NonOpaqueEquivalentBaseBlend_Refuses(
            float blendOp, float srcBlend, float dstBlend)
        {
            var material = ConvertibleFade();
            material.SetFloat("_BlendOp", blendOp);
            material.SetFloat("_SrcBlend", srcBlend);
            material.SetFloat("_DstBlend", dstBlend);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
        }

        [TestCase(1f)]
        [TestCase(5f)]
        public void AcceptedForwardAddSourceFactor_IsConvertible(float addSrcBlend)
        {
            var material = ConvertibleFade();
            material.SetFloat("_AddSrcBlend", addSrcBlend);

            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(0f)]
        [TestCase(2f)]
        [TestCase(4f)]
        [TestCase(10f)]
        public void OtherForwardAddSourceFactor_Refuses(float addSrcBlend)
        {
            var material = ConvertibleFade();
            material.SetFloat("_AddSrcBlend", addSrcBlend);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation);
        }

        [TestCase(0f)]
        [TestCase(7f)]
        [TestCase(10f)]
        public void NonOneForwardAddDestinationFactor_Refuses(float addDstBlend)
        {
            var material = ConvertibleFade();
            material.SetFloat("_AddDstBlend", addDstBlend);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation);
        }

        /// <summary>
        /// The recipe never writes <c>_AddBlendOp</c>, so the blend operation is
        /// identical on both sides of the conversion and cancels once the
        /// factors are proven equivalent at alpha 1. Every value must therefore
        /// leave the outcome untouched - including the vendor's own serialized
        /// default of 4 (Max). Reintroducing a gate on it fails this test.
        /// </summary>
        [TestCase(0f)]
        [TestCase(1f)]
        [TestCase(2f)]
        [TestCase(3f)]
        [TestCase(4f)]
        public void AddBlendOp_DoesNotAffectEligibility(float addBlendOp)
        {
            var material = ConvertibleFade();
            material.SetFloat("_AddBlendOp", addBlendOp);

            AssertConvertible(EvaluateFor(material));
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        [TestCase(-0.5f)]
        public void ClipThresholdThatKeepsAlphaOne_IsConvertible(float cutoff)
        {
            var material = ConvertibleFade();
            material.SetFloat("_Cutoff", cutoff);

            AssertConvertible(EvaluateFor(material));
        }

        /// <summary>
        /// 1.001 is the vendor's own declared maximum for <c>_Cutoff</c>. The
        /// shader clips with <c>clip(alpha - _Cutoff)</c>, which discards when
        /// the difference is negative, so a threshold above 1 discards alpha
        /// exactly 1. A declared range constrains the inspector widget, not what
        /// renders.
        /// </summary>
        [Test]
        public void ClipThresholdAtTheVendorDeclaredMaximum_Refuses()
        {
            var material = ConvertibleFade();
            material.SetFloat("_Cutoff", 1.001f);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
        }

        [Test]
        public void ClipThresholdBeyondTheDeclaredRange_Refuses()
        {
            AssertRefusal(
                EvaluateWith(ConvertibleFade(), "_Cutoff", 2f),
                PoiyomiOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
        }

        // --- Effective state beats _Mode -------------------------------------

        private Material Preset(
            float mode, int queue, string renderType, float forceOpaque,
            float cutoff, float srcBlend, float dstBlend, float zWrite,
            float premultiply, float addSrcBlend)
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_Mode", mode);
            material.SetFloat("_AlphaForceOpaque", forceOpaque);
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_SrcBlend", srcBlend);
            material.SetFloat("_DstBlend", dstBlend);
            material.SetFloat("_ZWrite", zWrite);
            material.SetFloat("_AlphaPremultiply", premultiply);
            material.SetFloat("_AddSrcBlend", addSrcBlend);
            material.SetFloat("_AddDstBlend", 1f);
            material.SetFloat("_AlphaToCoverage", 0f);
            material.SetFloat("_ZTest", 4f);
            material.SetFloat("_EnableOutlines", 0f);
            material.renderQueue = queue;
            material.SetOverrideTag("RenderType", renderType);
            return material;
        }

        [Test]
        public void PresetOpaque_IsAlreadyOpaque()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_EnableOutlines", 0f);

            Assert.That(
                EvaluateFor(material).Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque));
        }

        [Test]
        public void PresetCutout_IsConvertible()
        {
            AssertConvertible(EvaluateFor(Preset(
                1f, 2450, "TransparentCutout", 0f, 0.5f, 1f, 0f, 1f, 0f, 1f)));
        }

        [Test]
        public void PresetTransClipping_IsConvertible()
        {
            AssertConvertible(EvaluateFor(Preset(
                9f, 2460, "TransparentCutout", 0f, 0.01f, 5f, 10f, 1f, 0f, 5f)));
        }

        [Test]
        public void PresetFade_IsConvertible()
        {
            AssertConvertible(EvaluateFor(Preset(
                2f, 3000, "Transparent", 0f, 0.002f, 5f, 10f, 0f, 0f, 5f)));
        }

        [Test]
        public void PresetTransparent_RefusesOnPremultipliedAlpha()
        {
            AssertRefusal(
                EvaluateFor(Preset(
                    3f, 3000, "Transparent", 0f, 0f, 1f, 10f, 0f, 1f, 1f)),
                PoiyomiOpaqueConversionRefusal.PremultipliedAlphaEnabled);
        }

        [Test]
        public void PresetAdditive_RefusesOnBlendEquation()
        {
            AssertRefusal(
                EvaluateFor(Preset(
                    4f, 3000, "Transparent", 0f, 0f, 1f, 1f, 0f, 0f, 1f)),
                PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
        }

        [Test]
        public void PresetSoftAdditive_RefusesOnBlendEquation()
        {
            AssertRefusal(
                EvaluateFor(Preset(
                    5f, 3000, "Transparent", 0f, 0f, 4f, 1f, 0f, 0f, 4f)),
                PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
        }

        [Test]
        public void PresetMultiplicative_RefusesOnBlendEquation()
        {
            AssertRefusal(
                EvaluateFor(Preset(
                    6f, 3000, "Transparent", 0f, 0f, 2f, 0f, 0f, 0f, 2f)),
                PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
        }

        [Test]
        public void PresetTwoXMultiplicative_RefusesOnBlendEquation()
        {
            AssertRefusal(
                EvaluateFor(Preset(
                    7f, 3000, "Transparent", 0f, 0f, 2f, 3f, 0f, 0f, 2f)),
                PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
        }

        /// <summary>
        /// <c>_Mode</c> is a preset hint, not authoritative effective state.
        /// Real authored materials diverge from their declared preset, so
        /// eligibility must never consult it.
        /// </summary>
        [Test]
        public void ModeOpaqueWithAdditiveBlend_StillRefuses()
        {
            var material = ConvertibleFade();
            material.SetFloat("_Mode", 0f);
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_DstBlend", 1f);

            AssertRefusal(
                EvaluateFor(material),
                PoiyomiOpaqueConversionRefusal.UnsupportedBlendEquation);
        }

        [Test]
        public void ModeAdditiveWithOpaqueEquivalentState_IsConvertible()
        {
            var material = ConvertibleFade();
            material.SetFloat("_Mode", 4f);
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_DstBlend", 0f);
            material.SetFloat("_ZTest", 4f);
            material.SetFloat("_EnableOutlines", 0f);
            material.SetFloat("_AlphaPremultiply", 0f);

            AssertConvertible(EvaluateFor(material));
        }

        // --- Evaluation order: AlreadyOpaque precedes every gate -------------

        private static PoiyomiOpaqueConversionEligibility EvaluateFor(Material material)
        {
            PoiyomiOpaqueConversion.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(
                CaptureConversion(material), queue, renderType);
        }

        /// <summary>
        /// Substitutes one captured scalar before evaluating. The evaluator is
        /// pure over captured evidence, so inputs it must handle need not be
        /// reachable by writing a live material - this is how out-of-range and
        /// non-finite values are supplied, using the existing evidence
        /// primitive rather than a new seam.
        /// </summary>
        private static PoiyomiOpaqueConversionEligibility EvaluateWith(
            Material material, string property, float value)
        {
            PoiyomiOpaqueConversion.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(
                CaptureConversion(material).WithScalar(property, value),
                queue,
                renderType);
        }

        [Test]
        public void AlreadyOpaque_WhenEveryCanonicalFactMatches()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_EnableOutlines", 0f);

            Assert.That(
                EvaluateFor(material).Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque));
        }

        /// <summary>
        /// The outline hazard is a MUTATION hazard: writing <c>_Mode = 0</c>
        /// would force unmodelled outline alpha to 1 before the outline clip. A
        /// canonical material already has <c>_Mode == 0</c>, so that forcing is
        /// the author's existing state and nothing would be written. Refusing
        /// here would claim AMUSE declined to do something it was never going
        /// to do.
        /// </summary>
        [Test]
        public void AlreadyOpaque_EvenWithOutlinesEnabled()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_EnableOutlines", 1f);

            Assert.That(
                EvaluateFor(material).Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque));
        }

        [Test]
        public void AlreadyOpaque_EvenWithNonFiniteEnableOutlines()
        {
            var material = MakeCanonical(NewFixtureMaterial());

            Assert.That(
                EvaluateWith(material, "_EnableOutlines", float.NaN).Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque));
        }

        /// <summary>
        /// Direct falsification of the <c>AlreadyOpaque</c> classification.
        /// <see cref="TryFindNonCanonicalFact"/> and the evaluator's canonical
        /// comparison are separate code paths over separate inputs - a live
        /// material versus captured evidence - so the per-field tests above do
        /// not protect this one. Perturbing any single canonical fact must stop
        /// the material being classified as a no-op.
        /// <para>
        /// The replacement outcome is deliberately unconstrained. Depending on
        /// the property the correct answer is <c>Convertible</c> (for facts no
        /// gate reads, such as the outline blend fields) or a specific refusal
        /// (for facts a gate reads, such as <c>_ZTest</c>). The load-bearing
        /// assertion is only that a disagreement cannot silently pass the
        /// canonical-comparison path.
        /// </para>
        /// </summary>
        [Test]
        public void PerturbingAnyCanonicalProperty_PreventsAlreadyOpaque()
        {
            foreach (var (property, value) in ExpectedCanonicalTuple)
            {
                var material = MakeCanonical(NewFixtureMaterial());
                material.SetFloat("_EnableOutlines", 0f);
                material.SetFloat(property, value + 1f);

                var result = EvaluateFor(material);

                Assert.That(
                    result.Outcome,
                    Is.Not.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque),
                    $"Perturbed '{property}' was still classified AlreadyOpaque.");
            }
        }

        [Test]
        public void PerturbingTheEffectiveRenderQueue_PreventsAlreadyOpaque()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_EnableOutlines", 0f);
            material.renderQueue = 3000;

            Assert.That(
                EvaluateFor(material).Outcome,
                Is.Not.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque));
        }

        [Test]
        public void PerturbingTheRenderTypeTag_PreventsAlreadyOpaque()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_EnableOutlines", 0f);
            material.SetOverrideTag("RenderType", "Transparent");

            Assert.That(
                EvaluateFor(material).Outcome,
                Is.Not.EqualTo(PoiyomiOpaqueConversionOutcome.AlreadyOpaque));
        }

        /// <summary>
        /// The contrast. <c>_Cutoff</c> is itself a canonical recipe property
        /// whose canonical value is 0, so a material with <c>_Cutoff &gt; 1</c>
        /// cannot have all 25 facts matching: the comparison fails, evaluation
        /// proceeds through the transformation gates, and the answer is the clip
        /// refusal - never <c>AlreadyOpaque</c>.
        /// </summary>
        [Test]
        public void CanonicalExceptCutoffAboveOne_RefusesRatherThanReportingAlreadyOpaque()
        {
            var material = MakeCanonical(NewFixtureMaterial());
            material.SetFloat("_EnableOutlines", 0f);
            material.SetFloat("_Cutoff", 1.001f);

            var result = EvaluateFor(material);

            Assert.That(
                result.Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.Refused));
            Assert.That(
                result.Refusal,
                Is.EqualTo(PoiyomiOpaqueConversionRefusal
                    .ClipThresholdDiscardsOpaqueAlpha));
        }

        [Test]
        public void ConversionPropertyAbsent_IsCheckedBeforeTheCanonicalComparison()
        {
            var shader = Shader.Find("Unlit/Color");
            var material = Track(new Material(shader));

            PoiyomiOpaqueConversion.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            var result = PoiyomiOpaqueConversion.EvaluateVerifiedEligibility(
                CaptureConversion(material), queue, renderType);

            Assert.That(
                result.Outcome,
                Is.EqualTo(PoiyomiOpaqueConversionOutcome.Refused));
            Assert.That(
                result.Refusal,
                Is.EqualTo(PoiyomiOpaqueConversionRefusal.ConversionPropertyAbsent));
        }

        // --- Effective render state and the canonical-fact comparison --------

        /// <summary>
        /// Applies the independently stated canonical tuple plus the two
        /// non-property facts. Built from the literal test data, never from the
        /// production constants.
        /// </summary>
        private static Material MakeCanonical(Material material)
        {
            foreach (var (property, value) in ExpectedCanonicalTuple)
            {
                material.SetFloat(property, value);
            }

            material.renderQueue = 2000;
            material.SetOverrideTag("RenderType", "Opaque");
            return material;
        }

        [Test]
        public void TryFindNonCanonicalFact_CanonicalMaterialHasNoDisagreement()
        {
            var material = MakeCanonical(NewFixtureMaterial());

            var found = PoiyomiOpaqueConversion.TryFindNonCanonicalFact(
                material, out var fact);

            Assert.That(found, Is.False, "Unexpected disagreement: " + fact);
        }

        [Test]
        public void ReadEffectiveRenderState_ReportsTheDeclaredQueueWithoutAnOverride()
        {
            var material = NewFixtureMaterial();

            PoiyomiOpaqueConversion.ReadEffectiveRenderState(
                material, out var queue, out var renderType);

            Assert.That(queue, Is.Not.EqualTo(-1));
            Assert.That(queue, Is.EqualTo(2000));
            Assert.That(renderType, Is.EqualTo("Opaque"));
        }

        // --- Recipe re-attestation guard --------------------------------------

        /// <summary>
        /// The canonical Opaque tuple in <see cref="PoiyomiOpaqueConversion"/>
        /// was derived by reading the <c>_Mode</c> preset 0
        /// <c>on_value_actions</c> metadata inside the attested vendor shader
        /// whose identity these two constants pin. The repository ships no
        /// vendor source and no parser, so nothing else can notice that the
        /// pinned source changed underneath the recipe.
        /// <para>
        /// <strong>If either constant changes, the canonical Opaque tuple MUST
        /// be re-derived from the newly attested vendor source before this
        /// expectation is updated.</strong> Updating these literals to make the
        /// test pass, without re-reading the new preset actions, silently
        /// converts a drift signal into a stale recipe.
        /// </para>
        /// <para>
        /// Stated literally rather than read from production, so the pin tests
        /// the pin.
        /// </para>
        /// </summary>
        [Test]
        public void PinnedVendorSourceIdentity_IsUnchangedSinceTheRecipeWasDerived()
        {
            Assert.That(
                PoiyomiMaterialSemantics.CanonicalShaderGuid,
                Is.EqualTo("9444ce77bf4418748b1e8591b9d97f85"),
                "Re-derive the canonical Opaque tuple before changing this pin.");
            Assert.That(
                PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash,
                Is.EqualTo(
                    "31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755"),
                "Re-derive the canonical Opaque tuple before changing this pin.");
        }

        // --- Conversion source evidence -------------------------------------

        /// <summary>
        /// Captures one material with the conversion request ALONE. These tests
        /// must never use or combine
        /// <c>PoiyomiMaterialSemantics.AlphaEvidenceRequest</c> to make
        /// conversion attestation succeed: closure is exactly what they exist to
        /// prove. (The relevance-isolation tests below reference both requests,
        /// solely to prove non-widening.)
        /// </summary>
        private static CapturedMaterialEvidence CaptureConversion(Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, PoiyomiOpaqueConversion.ConversionEvidenceRequest),
            })[0];
        }

        [Test]
        public void ConversionSourceEvidence_FixtureSatisfiesTheConversionSchema()
        {
            var material = NewFixtureMaterial();

            var evidence = PoiyomiOpaqueConversion.GatherConversionSourceEvidence(
                material.shader, CaptureConversion(material));

            Assert.That(evidence.HasRequiredSchema, Is.True);
        }

        [Test]
        public void ConversionSourceEvidence_ReadsLockedStateFromItsOwnRequest()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_ShaderOptimizerEnabled", 1f);

            var evidence = PoiyomiOpaqueConversion.GatherConversionSourceEvidence(
                material.shader, CaptureConversion(material));

            Assert.That(evidence.IsLocked, Is.True);
        }

        [Test]
        public void ConversionSourceEvidence_ShaderWithoutSchemaHasNoRequiredSchema()
        {
            var shader = Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null, "Built-in Unlit/Color must exist.");
            var material = Track(new Material(shader));

            var evidence = PoiyomiOpaqueConversion.GatherConversionSourceEvidence(
                material.shader, CaptureConversion(material));

            Assert.That(evidence.HasRequiredSchema, Is.False);
        }

        /// <summary>
        /// The gathered evidence feeds the existing verifier unchanged. The
        /// public fixture is not the pinned vendor shader, so an identity
        /// failure is the expected and correct outcome. Which diagnostic the
        /// verifier emits, and in what order, is covered by the existing
        /// identity-verification tests and deliberately not duplicated here -
        /// the fixture fails on shader name and hash before reaching the locked
        /// or schema branches, so asserting that ordering here would encode an
        /// unreachable expectation.
        /// </summary>
        [Test]
        public void ConversionSourceEvidence_FeedsTheExistingVerifierUnchanged()
        {
            var material = NewFixtureMaterial();

            var evidence = PoiyomiOpaqueConversion.GatherConversionSourceEvidence(
                material.shader, CaptureConversion(material));

            var verified = PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                evidence, out var diagnostic);

            Assert.That(verified, Is.False);
            Assert.That(diagnostic, Is.Not.Null);
        }

        [Test]
        public void ExpectedConversionSchema_HasTwentyFourProperties()
        {
            Assert.That(ExpectedConversionSchema.Length, Is.EqualTo(24));
        }

        [Test]
        public void FixtureShader_DeclaresEveryConversionReadProperty()
        {
            var material = NewFixtureMaterial();

            foreach (var property in ExpectedConversionSchema)
            {
                Assert.That(
                    material.HasProperty(property),
                    Is.True,
                    $"Fixture shader must declare '{property}'.");
            }
        }

        /// <summary>
        /// Declared only so the conversion tests can vary it and prove it
        /// changes nothing. It is not a conversion-read property.
        /// </summary>
        [Test]
        public void FixtureShader_DeclaresAddBlendOpForNonDependencyTests()
        {
            var material = NewFixtureMaterial();

            Assert.That(material.HasProperty("_AddBlendOp"), Is.True);
            Assert.That(ExpectedConversionSchema, Has.No.Member("_AddBlendOp"));
        }
    }
}
