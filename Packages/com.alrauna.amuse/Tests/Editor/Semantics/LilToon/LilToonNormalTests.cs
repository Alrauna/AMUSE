using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Normal is the first bump map unpacked at unit scale. Its UV is the affine
    /// composition of _MainTex_ST with _BumpMap_ST, and its sampler comes from
    /// the _MainTex asset, not from _BumpMap. Every traced normal writer is
    /// proven off before any claim, including the neutral Unmodified claims.
    /// </summary>
    public sealed class LilToonNormalTests : LilToonFixtureTestBase
    {
        private Material NormalMaterial(out Texture2D bump)
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("mainforsampler"));
            bump = ImportNormalMap("bump");
            material.SetTexture("_BumpMap", bump);
            material.SetFloat("_UseBumpMap", 1f);
            return material;
        }

        [Test]
        public void BumpMapDisabled_IsUnmodified()
        {
            var material = NewFixtureMaterial();

            var normal = Interpret(material).Semantics.Normal;

            Assert.That(normal.IsComplete, Is.True);
            Assert.That(
                normal.GetCompleteValue().Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
        }

        [Test]
        public void BumpMapEnabledWithoutTexture_IsUnmodified()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseBumpMap", 1f);

            var normal = Interpret(material).Semantics.Normal;

            // The "bump" default resolves to (0.5,0.5,1,0.5), which
            // lilUnpackNormalScale maps to exactly (0,0,1).
            Assert.That(normal.IsComplete, Is.True);
            Assert.That(
                normal.GetCompleteValue().Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
        }

        [Test]
        public void CanonicalBumpMap_IsTangentSpaceNormalMap()
        {
            var material = NormalMaterial(out var bump);

            var normal = Interpret(material).Semantics.Normal;

            Assert.That(normal.IsComplete, Is.True);
            var value = normal.GetCompleteValue();
            Assert.That(
                value.Kind,
                Is.EqualTo(NormalSemanticValueKind.TangentSpaceNormalMap));
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(bump, out var expectedId),
                Is.True);
            Assert.That(value.GetTextureSample().Source, Is.EqualTo(expectedId));
        }

        [Test]
        public void BumpMapUv_ComposesMainThenBumpAffineTransforms()
        {
            var material = NormalMaterial(out _);
            material.SetTextureScale("_MainTex", new Vector2(2f, 4f));
            material.SetTextureOffset("_MainTex", new Vector2(0.1f, 0.2f));
            material.SetTextureScale("_BumpMap", new Vector2(3f, 0.5f));
            material.SetTextureOffset("_BumpMap", new Vector2(0.5f, -0.25f));

            var sample = Interpret(material)
                .Semantics.Normal.GetCompleteValue().GetTextureSample();

            // uv = (uv0 * mainScale + mainOffset) * bumpScale + bumpOffset
            Assert.That(sample.Coordinates.Channel, Is.EqualTo(0));
            Assert.That(
                sample.Coordinates.Scale,
                Is.EqualTo(new Vector2(2f * 3f, 4f * 0.5f)));
            Assert.That(
                sample.Coordinates.Offset,
                Is.EqualTo(new Vector2(0.1f * 3f + 0.5f, 0.2f * 0.5f + -0.25f)));
        }

        [Test]
        public void BumpMapSampler_ComesFromMainTexNotBumpMap()
        {
            var material = NormalMaterial(out _);
            material.SetTexture(
                "_MainTex",
                ImportTexture("mippedmain", importer => importer.mipmapEnabled = true));

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [Test]
        public void MissingMainTex_LeavesNormalUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_BumpMap", ImportNormalMap("lonebump"));
            material.SetFloat("_UseBumpMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedSampling,
                "_MainTex");
        }

        [TestCase(0.5f)]
        [TestCase(-1f)]
        [TestCase(2f)]
        public void NonUnitBumpScale_IsUnknown(float scale)
        {
            var material = NormalMaterial(out _);
            material.SetFloat("_BumpScale", scale);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_BumpScale");
        }

        [TestCase("_UseBump2ndMap")]
        [TestCase("_UseAnisotropy")]
        [TestCase("_UseParallax")]
        [TestCase("_UsePOM")]
        [TestCase("_ShiftBackfaceUV")]
        public void EnabledNormalWriter_IsUnknown(string property)
        {
            var material = NormalMaterial(out _);
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        // --- the neutral path must not bypass the writer gates ---

        [TestCase("_UseBump2ndMap")]
        [TestCase("_UseAnisotropy")]
        [TestCase("_UseParallax")]
        [TestCase("_UsePOM")]
        [TestCase("_ShiftBackfaceUV")]
        public void EnabledNormalWriter_WithBumpMapDisabled_IsUnknown(string property)
        {
            // An independently enabled normal mechanism cannot coexist with an
            // Unmodified claim just because the first bump map is off.
            var material = NewFixtureMaterial();
            material.SetFloat("_UseBumpMap", 0f);
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void EnabledSecondNormal_WithNoFirstTexture_IsUnknown()
        {
            // The no-first-texture equivalent: _UseBumpMap is on but no map is
            // assigned, so the first normal is neutral, yet the second normal
            // mechanism is active.
            var material = NewFixtureMaterial();
            material.SetFloat("_UseBumpMap", 1f);
            material.SetFloat("_UseBump2ndMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_UseBump2ndMap");
        }

        [Test]
        public void NonCanonicalNormalImport_IsUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("mainok"));
            material.SetTexture("_BumpMap", ImportTexture("notanormal"));
            material.SetFloat("_UseBumpMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.UnsupportedTextureImport,
                "_BumpMap");
        }

        // --- compile-time feature evidence ---

        [TestCase("LIL_FEATURE_NORMAL_1ST")]
        [TestCase("LIL_FEATURE_BumpMap")]
        public void StrippedFeature_KeepsNormalUnknown(string missing)
        {
            var material = NormalMaterial(out _);
            var features = System.Array.FindAll(AllFeatures, f => f != missing);

            var result = Interpret(material, features);

            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Normal,
                LilToonSemanticDiagnosticCode.MissingFeatureCompilation,
                missing);
        }

        [Test]
        public void StrippedFeature_WithBumpMapDisabled_StaysUnmodified()
        {
            var material = NewFixtureMaterial();

            var result = Interpret(material, new string[0]);

            // Nothing is claimed, so no compile-time evidence is needed.
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
            Assert.That(
                result.Semantics.Normal.GetCompleteValue().Kind,
                Is.EqualTo(NormalSemanticValueKind.Unmodified));
        }
    }
}
