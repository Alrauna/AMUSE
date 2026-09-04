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
    /// Tests for the pinned lilToon opaque target: the canonical Opaque
    /// recipe, the transient validated clone, and the read-back validation.
    /// <para>
    /// The expected canonical tuple is stated literally here, transcribed
    /// from B1 §9 / spec §9.1, and never derived from the production
    /// constants. A test that read its expectation from
    /// <c>LilToonOpaqueTarget</c> would let a wrong production tuple test
    /// itself.
    /// </para>
    /// <para>
    /// The scrambled-and-cloned sources below are fixture stand-in
    /// materials; the tuple-carrying opaque stand-in shader
    /// <c>LilToonOpaqueConversionTest</c> supplies the attested-target
    /// stand-in for the swap tests.
    /// </para>
    /// </summary>
    public sealed class LilToonOpaqueTargetTests : LilToonFixtureTestBase
    {
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
            var actual = LilToonOpaqueTarget.CanonicalOpaqueProperties;

            Assert.That(actual.Count, Is.EqualTo(18));
            CollectionAssert.AreEquivalent(
                ExpectedCanonicalTuple, actual.ToArray());
        }

        [Test]
        public void CanonicalNonPropertyFacts_AreQueueTwoThousandAndOpaqueTag()
        {
            Assert.That(
                LilToonOpaqueTarget.CanonicalOpaqueRenderQueue,
                Is.EqualTo(2000));
            Assert.That(
                LilToonOpaqueTarget.RenderTypeTagName,
                Is.EqualTo("RenderType"));
            Assert.That(
                LilToonOpaqueTarget.CanonicalOpaqueRenderType,
                Is.EqualTo("Opaque"));
        }

        /// <summary>
        /// The target's request is the recipe and nothing else. Falsifies a
        /// split that moved the code but kept the "tuple + 1" schema, which
        /// would leave _Cutoff — a source-eligibility fact — inside the
        /// target's evidence contract.
        /// </summary>
        [Test]
        public void RecipeEvidenceRequest_IsExactlyTheEighteenRecipeProperties()
        {
            var request = LilToonOpaqueTarget.RecipeEvidenceRequest;

            Assert.That(request.ShaderName, Is.True);
            Assert.That(request.ActiveColorSpace, Is.False);
            CollectionAssert.AreEquivalent(
                ExpectedRecipeSchema, request.PresenceProperties);
            CollectionAssert.AreEquivalent(
                ExpectedRecipeSchema, request.ScalarProperties);
            Assert.That(request.ColorProperties, Is.Empty);
            Assert.That(request.VectorProperties, Is.Empty);
            Assert.That(request.TextureProperties, Is.Empty);
            CollectionAssert.DoesNotContain(
                request.ScalarProperties,
                "_Cutoff",
                "_Cutoff is source-eligibility evidence, not target evidence");
        }

        private static readonly string[] ExpectedRecipeSchema =
        {
            "_SrcBlend", "_DstBlend", "_AlphaToMask", "_ZWrite", "_ZTest",
            "_OffsetFactor", "_OffsetUnits", "_ColorMask",
            "_SrcBlendAlpha", "_DstBlendAlpha", "_BlendOp", "_BlendOpAlpha",
            "_SrcBlendFA", "_DstBlendFA", "_SrcBlendAlphaFA",
            "_DstBlendAlphaFA", "_BlendOpFA", "_BlendOpAlphaFA",
        };

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

            var clone = Track(LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
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

            var clone = Track(LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
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

            var clone = Track(LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
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

            Track(LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
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
                    () => LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
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
                    () => LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
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
                () => LilToonOpaqueTarget.PrepareCanonicalOpaqueClone(
                    source, (Shader)null));
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
    }
}
