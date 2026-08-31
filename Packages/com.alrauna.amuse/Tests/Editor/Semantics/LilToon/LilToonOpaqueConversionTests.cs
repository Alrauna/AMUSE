using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Tests for the pinned lilToon opaque-conversion core.
    /// <para>
    /// The expected canonical tuple and conversion schema are stated literally
    /// here, transcribed from B1 §9 / spec §9.1, and never derived from the
    /// production constants. A test that read its expectation from
    /// <c>LilToonOpaqueConversion</c> would let a wrong production tuple test
    /// itself.
    /// </para>
    /// <para>
    /// The cutout stand-in shader does not declare the 18 conversion-tuple
    /// properties (it is the cutout contract, not the opaque one), so the
    /// conversion-eligible stand-ins are materials of the opaque stand-in
    /// shader <c>LilToonOpaqueConversionTest</c>, whose fresh defaults are
    /// canonical except <c>_AlphaToMask</c> (1, deliberately ungated) and
    /// which carries <c>_Cutoff = 0.5</c>. The cutout stand-in still plays
    /// itself where its missing tuple is the point: the
    /// <c>ConversionPropertyAbsent</c> row.
    /// </para>
    /// </summary>
    public sealed class LilToonOpaqueConversionTests : LilToonFixtureTestBase
    {
        /// <summary>
        /// The 19 properties conversion reads: the 18 canonical recipe
        /// properties plus the eligibility-only <c>_Cutoff</c>, which the
        /// recipe never writes.
        /// </summary>
        private static readonly string[] ExpectedConversionSchema =
        {
            "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite", "_ZTest",
            "_OffsetFactor", "_OffsetUnits", "_ColorMask",
            "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
            "_SrcBlendFA", "_DstBlendFA", "_SrcBlendAlphaFA", "_DstBlendAlphaFA",
            "_BlendOpFA", "_BlendOpAlphaFA",
            "_Cutoff",
        };

        /// <summary>
        /// The complete canonical Opaque tuple, transcribed from B1 §9 / spec
        /// §9.1: eighteen scalar writes measured from the installed lilToon
        /// 2.3.4 package. The render queue (2000) and the <c>RenderType</c>
        /// tag ("Opaque") are the recipe's other two actions and are asserted
        /// separately, because they are not material properties. No
        /// <c>_Cutoff</c> write: it is eligibility-read, never written.
        /// </summary>
        private static readonly (string Property, float Value)[] ExpectedCanonicalTuple =
        {
            ("_SrcBlend", 1f),
            ("_DstBlend", 0f),
            ("_AlphaToMask", 0f),
            ("_ZWrite", 1f),
            ("_ZTest", 4f),
            ("_OffsetFactor", 0f),
            ("_OffsetUnits", 0f),
            ("_ColorMask", 15f),
            ("_SrcBlendAlpha", 1f),
            ("_DstBlendAlpha", 10f),
            ("_BlendOp", 0f),
            ("_BlendOpAlpha", 0f),
            ("_SrcBlendFA", 1f),
            ("_DstBlendFA", 1f),
            ("_SrcBlendAlphaFA", 0f),
            ("_DstBlendAlphaFA", 1f),
            ("_BlendOpFA", 4f),
            ("_BlendOpAlphaFA", 4f),
        };

        /// <summary>
        /// The exact refusal vocabulary, stated literally so a silently added
        /// or removed member (in particular any <c>AlreadyOpaque</c>-flavored
        /// state, or a premultiply/outline member unreachable on the attested
        /// cutout shader) fails a test.
        /// </summary>
        private static readonly string[] ExpectedRefusalNames =
        {
            "None",
            "UnattestedMaterial",
            "ConversionPropertyAbsent",
            "ConversionPropertyNotFinite",
            "UnsupportedRenderQueue",
            "UnsupportedRenderType",
            "UnsupportedDepthComparison",
            "UnsupportedDepthWrite",
            "UnsupportedColorMask",
            "UnsupportedDepthOffset",
            "UnsupportedBlendEquation",
            "UnsupportedAlphaBlendEquation",
            "UnsupportedForwardAddBlendEquation",
            "ClipThresholdDiscardsOpaqueAlpha",
        };

        private const string ConversionTempFolder = "Assets/AmuseTests_LilToonConversion";

        // --- Tuple and request shape ----------------------------------------

        [Test]
        public void ExpectedCanonicalTuple_HasEighteenProperties()
        {
            Assert.That(ExpectedCanonicalTuple.Length, Is.EqualTo(18));
        }

        [Test]
        public void CanonicalOpaqueProperties_MatchTheIndependentlyStatedTuple()
        {
            var actual = LilToonOpaqueConversion.CanonicalOpaqueProperties;

            Assert.That(actual.Count, Is.EqualTo(18));
            CollectionAssert.AreEquivalent(
                ExpectedCanonicalTuple, actual.ToArray());
        }

        [Test]
        public void CanonicalNonPropertyFacts_AreQueueTwoThousandAndOpaqueTag()
        {
            Assert.That(
                LilToonOpaqueConversion.CanonicalOpaqueRenderQueue,
                Is.EqualTo(2000));
            Assert.That(
                LilToonOpaqueConversion.RenderTypeTagName,
                Is.EqualTo("RenderType"));
            Assert.That(
                LilToonOpaqueConversion.CanonicalOpaqueRenderType,
                Is.EqualTo("Opaque"));
        }

        [Test]
        public void MaxProvableCutoff_IsTheControllerFixedTwiceMargin()
        {
            Assert.That(
                LilToonOpaqueConversion.MaxProvableCutoff,
                Is.EqualTo(0.9999f));
        }

        [Test]
        public void OutcomeEnum_HasNoAlreadyOpaqueMember()
        {
            var names = Enum.GetNames(typeof(LilToonOpaqueConversionOutcome));

            Assert.That(
                names,
                Is.EqualTo(new[] { "Refused", "Convertible" }),
                "An attested cutout source is never canonical-opaque, so " +
                "AlreadyOpaque must not exist (spec §9.3).");
            Assert.That(names, Has.No.Member("AlreadyOpaque"));
        }

        [Test]
        public void RefusalEnum_MatchesTheIndependentlyStatedVocabulary()
        {
            Assert.That(
                Enum.GetNames(typeof(LilToonOpaqueConversionRefusal)),
                Is.EqualTo(ExpectedRefusalNames));
        }

        [Test]
        public void ConversionRequiredSchema_MatchesTheIndependentlyStatedSchema()
        {
            var actual = LilToonOpaqueConversion.ConversionRequiredSchemaProperties;

            Assert.That(actual.Count, Is.EqualTo(19));
            CollectionAssert.AreEquivalent(ExpectedConversionSchema, actual);
        }

        [Test]
        public void ConversionEvidenceRequest_RequestsExactlyTheConversionSchema()
        {
            var request = LilToonOpaqueConversion.ConversionEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            Assert.That(request.PresenceProperties.Count, Is.EqualTo(19));
            Assert.That(request.ScalarProperties.Count, Is.EqualTo(19));
            Assert.That(request.ScalarProperties, Has.Member("_Cutoff"));
            // lilToon's conversion path has no locked-flag scalar: the
            // request must not grow a Poiyomi-style
            // _ShaderOptimizerEnabled-equivalent without a design change.
            Assert.That(request.ScalarProperties, Has.No.Member("_ShaderOptimizerEnabled"));
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.PresenceProperties.ToArray());
            CollectionAssert.AreEquivalent(
                ExpectedConversionSchema, request.ScalarProperties.ToArray());
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
        }

        // --- Eligibility matrix -----------------------------------------------

        private static void AssertRefusal(
            LilToonOpaqueConversionEligibility result,
            LilToonOpaqueConversionRefusal expected)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Refused));
            Assert.That(result.Refusal, Is.EqualTo(expected));
        }

        private static void AssertConvertible(
            LilToonOpaqueConversionEligibility result)
        {
            Assert.That(
                result.Outcome,
                Is.EqualTo(LilToonOpaqueConversionOutcome.Convertible),
                "refusal was " + result.Refusal);
        }

        private static CapturedMaterialEvidence CaptureConversion(Material material)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, LilToonOpaqueConversion.ConversionEvidenceRequest),
            })[0];
        }

        private static LilToonOpaqueConversionEligibility EvaluateFor(Material material)
        {
            LilToonOpaqueConversion.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonOpaqueConversion.EvaluateVerifiedEligibility(
                CaptureConversion(material), queue, renderType);
        }

        /// <summary>
        /// Substitutes one captured scalar before evaluating. The evaluator is
        /// pure over captured evidence, so inputs it must handle need not be
        /// reachable by writing a live material - this is how non-finite
        /// values are supplied, using the existing evidence primitive rather
        /// than a new seam.
        /// </summary>
        private static LilToonOpaqueConversionEligibility EvaluateWith(
            Material material, string property, float value)
        {
            LilToonOpaqueConversion.ReadEffectiveRenderState(
                material, out var queue, out var renderType);
            return LilToonOpaqueConversion.EvaluateVerifiedEligibility(
                CaptureConversion(material).WithScalar(property, value),
                queue,
                renderType);
        }

        /// <summary>
        /// The conversion-eligible stand-in is the schema-complete cutout
        /// source. Its defaults are canonical except
        /// <c>_AlphaToMask = 1</c>, which conversion writes but deliberately
        /// does not gate (spec §9.3).
        /// </summary>
        private Material ConversionEligibleStandIn()
        {
            return NewCutoutFixtureMaterial();
        }

        /// <summary>
        /// RED against the Task 3 Step 1 refuse-all scaffold: the scaffold
        /// answers <c>ConversionPropertyNotFinite</c> for every schema-complete
        /// input, so this fails behaviorally until Step 3 implements the
        /// gates.
        /// </summary>
        [Test]
        public void CanonicalDefaultStandIn_IsConvertible()
        {
            AssertConvertible(EvaluateFor(ConversionEligibleStandIn()));
        }

        [Test]
        public void CustomRenderQueue_RefusesBeforeCanonicalization()
        {
            var material = ConversionEligibleStandIn();
            material.renderQueue = 3000;

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderQueue);
        }

        [Test]
        public void CustomRenderType_RefusesBeforeCanonicalization()
        {
            var material = ConversionEligibleStandIn();
            material.SetOverrideTag("RenderType", "Transparent");

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.UnsupportedRenderType);
        }

        /// <summary>
        /// <c>0.9999</c> is exactly the conversion-stage bound (gate 12, B2
        /// gap 4); Poiyomi's <c>&lt;= 1</c> rule must not be reused.
        /// </summary>
        [Test]
        public void ClipThresholdAtMaxProvableCutoff_IsConvertible()
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat("_Cutoff", LilToonOpaqueConversion.MaxProvableCutoff);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void ClipThresholdAtOne_Refuses()
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat("_Cutoff", 1f);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
        }

        /// <summary>
        /// 1.001 mirrors the vendor-declared maximum scenario: a declared
        /// range constrains the inspector widget, not what renders, and any
        /// threshold above the bound discards alpha exactly 1.
        /// </summary>
        [Test]
        public void ClipThresholdBeyondTheBound_Refuses()
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat("_Cutoff", 1.001f);

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ClipThresholdDiscardsOpaqueAlpha);
        }

        /// <summary>
        /// Gate order is load-bearing: finiteness (gate 2) precedes the clip
        /// gate (gate 12), so a non-finite cutoff is caught by the finiteness
        /// gate and named <c>ConversionPropertyNotFinite</c>, not
        /// <c>ClipThresholdDiscardsOpaqueAlpha</c>. (NaN would also fail the
        /// gate-12 comparison, but it must never reach it.)
        /// </summary>
        [Test]
        public void NonFiniteClipThreshold_RefusesAsNotFinite()
        {
            var material = ConversionEligibleStandIn();

            AssertRefusal(
                EvaluateWith(material, "_Cutoff", float.NaN),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        // --- Per-gate refusal matrix -----------------------------------------

        /// <summary>
        /// Gates 3-9 with their named refusal members, driven one perturbation
        /// at a time from the canonical-default stand-in so each row
        /// exercises exactly its own gate. These are the Step 3 GREEN
        /// targets: against the Step 1 refuse-all scaffold every row fails
        /// with <c>ConversionPropertyNotFinite</c> instead of its named
        /// refusal.
        /// </summary>
        [TestCase("_ZTest", 3f, LilToonOpaqueConversionRefusal.UnsupportedDepthComparison)]
        [TestCase("_ZWrite", 0f, LilToonOpaqueConversionRefusal.UnsupportedDepthWrite)]
        [TestCase("_ColorMask", 14f, LilToonOpaqueConversionRefusal.UnsupportedColorMask)]
        [TestCase("_OffsetFactor", 1f, LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_OffsetUnits", 1f, LilToonOpaqueConversionRefusal.UnsupportedDepthOffset)]
        [TestCase("_BlendOp", 1f, LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_SrcBlend", 2f, LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_DstBlend", 5f, LilToonOpaqueConversionRefusal.UnsupportedBlendEquation)]
        [TestCase("_BlendOpAlpha", 1f, LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_SrcBlendAlpha", 2f, LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_DstBlendAlpha", 1f, LilToonOpaqueConversionRefusal.UnsupportedAlphaBlendEquation)]
        [TestCase("_SrcBlendFA", 2f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        [TestCase("_DstBlendFA", 0f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        [TestCase("_BlendOpFA", 0f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        [TestCase("_BlendOpAlphaFA", 0f, LilToonOpaqueConversionRefusal.UnsupportedForwardAddBlendEquation)]
        public void NonCanonicalRenderState_RefusesWithTheNamedGate(
            string property, float value, object expected)
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(property, value);

            AssertRefusal(
                EvaluateFor(material),
                (LilToonOpaqueConversionRefusal)expected);
        }

        /// <summary>
        /// The accepted classes inside gates 7-9 (spec §9.3): <c>_SrcBlend</c>
        /// 5 (SrcAlpha) is a unit source factor at alpha 1 and
        /// <c>_DstBlend</c> 10 (OneMinusSrcAlpha) a zero destination factor,
        /// so those states are blend-equivalent to the canonical tuple the
        /// clone writes; likewise <c>_DstBlendAlpha</c> 0.
        /// </summary>
        [TestCase("_SrcBlend", 5f)]
        [TestCase("_DstBlend", 10f)]
        [TestCase("_DstBlendAlpha", 0f)]
        public void OpaqueEquivalentBlendState_IsConvertible(
            string property, float value)
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(property, value);

            AssertConvertible(EvaluateFor(material));
        }

        [Test]
        public void NonFiniteConversionProperty_RefusesAsNotFinite()
        {
            var material = ConversionEligibleStandIn();

            AssertRefusal(
                EvaluateWith(material, "_ZWrite", float.NaN),
                LilToonOpaqueConversionRefusal.ConversionPropertyNotFinite);
        }

        /// <summary>
        /// The legacy semantic stand-in declares none of the 18 recipe
        /// properties, so the schema check refuses before any gate runs.
        /// </summary>
        [Test]
        public void MaterialWithoutTheConversionSchema_RefusesAsAbsent()
        {
            var material = NewFixtureMaterial();

            AssertRefusal(
                EvaluateFor(material),
                LilToonOpaqueConversionRefusal.ConversionPropertyAbsent);
        }

        /// <summary>
        /// Written but deliberately ungated (spec §9.3): <c>_AlphaToMask</c>
        /// admits under any state because full coverage holds at a ≡ 1
        /// [B2 §3.4], so the fresh cutout default 1 is the common case; and
        /// the attested cutout FORWARD_ADD declares its alpha blend pair as
        /// the literal <c>Zero One</c> [B2 §3.1], making
        /// <c>_SrcBlendAlphaFA</c>/<c>_DstBlendAlphaFA</c> unused by the
        /// compiled pass, so their values cannot affect the outcome.
        /// Reintroducing a gate on any of the three fails these rows.
        /// </summary>
        [TestCase("_AlphaToMask", 1f)]
        [TestCase("_SrcBlendAlphaFA", 1f)]
        [TestCase("_DstBlendAlphaFA", 0f)]
        public void UngatedProperties_DoNotAffectEligibility(
            string property, float value)
        {
            var material = ConversionEligibleStandIn();
            material.SetFloat(property, value);

            AssertConvertible(EvaluateFor(material));
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

        /// <summary>
        /// Writes a distinct junk value (101..118, in recipe order) into every
        /// recipe property and non-canonical queue/tag, so a clone that
        /// inherited or half-rewrote the source cannot pass read-back.
        /// </summary>
        private static void Scramble(Material material)
        {
            Assert.That(
                ExpectedCanonicalTuple.Length, Is.EqualTo(18),
                "Junk values 101..118 depend on the 18-entry recipe.");
            for (var index = 0; index < ExpectedCanonicalTuple.Length; index++)
            {
                material.SetFloat(
                    ExpectedCanonicalTuple[index].Property, 101f + index);
            }

            material.renderQueue = 3000;
            material.SetOverrideTag("RenderType", "Transparent");
            material.name = "scrambled source";
        }

        [Test]
        public void PreparedClone_RewritesEveryScrambledFactToCanonical()
        {
            var source = ConversionEligibleStandIn();
            Scramble(source);
            var target = source.shader;

            var clone = Track(LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                source, target));

            foreach (var (property, value) in ExpectedCanonicalTuple)
            {
                Assert.That(
                    clone.GetFloat(property),
                    Is.EqualTo(value),
                    $"Prepared clone must carry canonical '{property}'.");
            }

            Assert.That(clone.renderQueue, Is.EqualTo(2000));
            Assert.That(clone.GetTag("RenderType", false), Is.EqualTo("Opaque"));
            Assert.That(clone.shader, Is.SameAs(target));
            Assert.That(
                clone.name, Is.Empty,
                "The clone is left unnamed; naming is the consumer's job.");
        }

        /// <summary>
        /// The clone is transient. Persistence belongs to assignment, which is
        /// the consumer's job, so preparation must not save anything.
        /// </summary>
        [Test]
        public void PreparedClone_IsNotPersisted()
        {
            var source = ConversionEligibleStandIn();

            var clone = Track(LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                source, source.shader));

            Assert.That(AssetDatabase.Contains(clone), Is.False);
            Assert.That(AssetDatabase.GetAssetPath(clone), Is.Empty);
        }

        /// <summary>
        /// The swap is asserted, not preservation (spec R5): the clone must
        /// carry the attested opaque target and must not keep the cutout
        /// source's shader. The cutout stand-in supplies the distinct source
        /// shader; the clone read-back still passes because the recipe writes
        /// are made against the swapped-in opaque target, which declares all
        /// 18 recipe properties.
        /// </summary>
        [Test]
        public void PreparedClone_SwapsTheShaderToTheAttestedTarget()
        {
            var source = NewCutoutFixtureMaterial();
            var before = SnapshotFloats(source);
            var queueBefore = source.renderQueue;
            var tagBefore = source.GetTag("RenderType", false);
            var target = Shader.Find(OpaqueConversionShaderName);
            Assert.That(
                target, Is.Not.Null,
                $"Fixture shader '{OpaqueConversionShaderName}' must import.");
            Assert.That(
                target, Is.Not.SameAs(source.shader),
                "The swap test needs two distinct fixture shaders.");

            var clone = Track(LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                source, target));

            Assert.That(clone.shader, Is.SameAs(target));
            Assert.That(clone.shader, Is.Not.SameAs(source.shader));
            foreach (var (property, value) in ExpectedCanonicalTuple)
            {
                Assert.That(
                    clone.GetFloat(property),
                    Is.EqualTo(value),
                    $"Prepared clone must carry canonical '{property}'.");
            }

            Assert.That(clone.renderQueue, Is.EqualTo(2000));
            Assert.That(clone.GetTag("RenderType", false), Is.EqualTo("Opaque"));
            Assert.That(clone.name, Is.Empty);
            AssertUnchanged(source, before, queueBefore, tagBefore);
        }

        // --- Source preservation ---------------------------------------------

        [Test]
        public void Preparation_LeavesTheScrambledSourceUntouched()
        {
            var source = ConversionEligibleStandIn();
            Scramble(source);
            var before = SnapshotFloats(source);
            var queueBefore = source.renderQueue;
            var tagBefore = source.GetTag("RenderType", false);
            var shaderBefore = source.shader;

            Track(LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                source, source.shader));

            AssertUnchanged(source, before, queueBefore, tagBefore);
            for (var index = 0; index < ExpectedCanonicalTuple.Length; index++)
            {
                Assert.That(
                    source.GetFloat(ExpectedCanonicalTuple[index].Property),
                    Is.EqualTo(101f + index),
                    $"Source property " +
                    $"'{ExpectedCanonicalTuple[index].Property}' was mutated.");
            }

            Assert.That(source.shader, Is.SameAs(shaderBefore));
        }

        // --- Validation failure policy ---------------------------------------

        private static int LoadedMaterialCount()
        {
            return Resources.FindObjectsOfTypeAll<Material>().Length;
        }

        /// <summary>
        /// Writes, imports, and returns a temp stand-in shader without
        /// authoring a .meta (Unity generates it on import). The caller owns
        /// the folder cleanup.
        /// </summary>
        private static Shader ImportTempShader(
            string shaderName, string fileName, string shaderText)
        {
            if (!AssetDatabase.IsValidFolder(ConversionTempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_LilToonConversion");
            }

            var path = ConversionTempFolder + "/" + fileName;
            File.WriteAllText(path, shaderText);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);

            var shader = Shader.Find(shaderName);
            Assert.That(
                shader, Is.Not.Null,
                $"Temp shader '{shaderName}' must import.");
            return shader;
        }

        private static void DeleteConversionTempFolder()
        {
            if (AssetDatabase.IsValidFolder(ConversionTempFolder))
            {
                AssetDatabase.DeleteAsset(ConversionTempFolder);
            }
        }

        /// <summary>
        /// A target that is missing one recipe property makes that write a
        /// silent no-op, so read-back disagrees and preparation must fail
        /// loudly: an <see cref="InvalidOperationException"/>, with the failed
        /// clone destroyed first so nothing leaks.
        /// </summary>
        [Test]
        public void CloneWithTargetMissingARecipeProperty_ThrowsAndDestroysTheClone()
        {
            var missingBlendOpFa =
                "Shader \"Hidden/Alrauna/AmuseTests/LilToonConversionMissingBlendOpFA\"\n" +
                "{\n" +
                "    Properties\n" +
                "    {\n" +
                "        _Cutoff (\"Cutoff\", Range(0,1)) = 0.5\n" +
                "        _SrcBlend (\"SrcBlend\", Float) = 1\n" +
                "        _DstBlend (\"DstBlend\", Float) = 0\n" +
                "        _AlphaToMask (\"AlphaToMask\", Float) = 0\n" +
                "        _ZWrite (\"ZWrite\", Float) = 1\n" +
                "        _ZTest (\"ZTest\", Float) = 4\n" +
                "        _OffsetFactor (\"OffsetFactor\", Float) = 0\n" +
                "        _OffsetUnits (\"OffsetUnits\", Float) = 0\n" +
                "        _ColorMask (\"ColorMask\", Float) = 15\n" +
                "        _SrcBlendAlpha (\"SrcBlendAlpha\", Float) = 1\n" +
                "        _DstBlendAlpha (\"DstBlendAlpha\", Float) = 10\n" +
                "        _BlendOp (\"BlendOp\", Float) = 0\n" +
                "        _BlendOpAlpha (\"BlendOpAlpha\", Float) = 0\n" +
                "        _SrcBlendFA (\"SrcBlendFA\", Float) = 1\n" +
                "        _DstBlendFA (\"DstBlendFA\", Float) = 1\n" +
                "        _SrcBlendAlphaFA (\"SrcBlendAlphaFA\", Float) = 0\n" +
                "        _DstBlendAlphaFA (\"DstBlendAlphaFA\", Float) = 1\n" +
                "        // _BlendOpFA deliberately absent.\n" +
                "    }\n" +
                "\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Tags { \"RenderType\" = \"Opaque\" }\n" +
                "        Pass\n" +
                "        {\n" +
                "            CGPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #include \"UnityCG.cginc\"\n" +
                "            float4 vert(float4 vertex : POSITION) : SV_POSITION\n" +
                "            { return UnityObjectToClipPos(vertex); }\n" +
                "            fixed4 frag() : SV_Target { return fixed4(1, 1, 1, 1); }\n" +
                "            ENDCG\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            try
            {
                var target = ImportTempShader(
                    "Hidden/Alrauna/AmuseTests/LilToonConversionMissingBlendOpFA",
                    "LilToonConversionMissingBlendOpFA.shader",
                    missingBlendOpFa);
                var source = Track(new Material(target));
                var materialsBefore = LoadedMaterialCount();

                Assert.Throws<InvalidOperationException>(
                    () => LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                        source, target));

                Assert.That(
                    LoadedMaterialCount(),
                    Is.EqualTo(materialsBefore),
                    "The failed clone must be destroyed before the throw.");
            }
            finally
            {
                DeleteConversionTempFolder();
            }
        }

        [Test]
        public void OpaqueTargetGather_UsesTargetNameNotCutoutSourceName()
        {
            var source = Track(ConversionEligibleStandIn());
            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    source,
                    LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
            })[0];
            var target = Shader.Find(OpaqueConversionShaderName);
            Assert.That(target, Is.Not.Null);

            var targetEvidence =
                LilToonSourceAttestation.GatherOpaqueTargetSourceEvidence(
                    target, captured);

            Assert.That(targetEvidence.ShaderName, Is.EqualTo(target.name));
            Assert.That(
                targetEvidence.ShaderName,
                Is.Not.EqualTo(captured.ShaderName));
        }

        /// <summary>
        /// The production wrapper refuses any shader named <c>lilToon</c>
        /// whose asset GUID is not the attested pin: the recipe is measured
        /// against the pinned 2.3.4 asset, so a wrong target is an
        /// environment regression, not a conversion input. The temp stand-in
        /// carries the right name and the wrong GUID, forcing the GUID

        /// mismatch arm; the throw must happen before any clone exists.
        /// </summary>
        [Test]
        public void ProductionWrapper_WrongGuidShader_ThrowsBeforeAnyClone()
        {
            var wrongGuidLilToon =
                "Shader \"lilToon\"\n" +
                "{\n" +
                "    SubShader\n" +
                "    {\n" +
                "        Pass\n" +
                "        {\n" +
                "            CGPROGRAM\n" +
                "            #pragma vertex vert\n" +
                "            #pragma fragment frag\n" +
                "            #include \"UnityCG.cginc\"\n" +
                "            float4 vert(float4 vertex : POSITION) : SV_POSITION\n" +
                "            { return UnityObjectToClipPos(vertex); }\n" +
                "            fixed4 frag() : SV_Target { return fixed4(1, 1, 1, 1); }\n" +
                "            ENDCG\n" +
                "        }\n" +
                "    }\n" +
                "}\n";

            try
            {
                ImportTempShader("lilToon", "lilToon.shader", wrongGuidLilToon);
                var source = Track(ConversionEligibleStandIn());
                var captured = UnityMaterialEvidenceCapture.Capture(new[]
                {
                    new MaterialEvidenceCaptureInput(
                        source,
                        LilToonCutoutMaterialSemantics.AlphaEvidenceRequest),
                })[0];
                var materialsBefore = LoadedMaterialCount();

                Assert.Throws<InvalidOperationException>(
                    () => LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                        source, captured));

                Assert.That(
                    LoadedMaterialCount(),
                    Is.EqualTo(materialsBefore),
                    "The wrapper must refuse before any clone exists.");
            }
            finally
            {
                DeleteConversionTempFolder();
            }
        }

        [Test]
        public void ProductionWrapper_NullTarget_ThrowsArgumentNullException()
        {
            var source = ConversionEligibleStandIn();

            Assert.Throws<ArgumentNullException>(
                () => LilToonOpaqueConversion.PrepareCanonicalOpaqueClone(
                    source, (Shader)null));
        }
    }
}
