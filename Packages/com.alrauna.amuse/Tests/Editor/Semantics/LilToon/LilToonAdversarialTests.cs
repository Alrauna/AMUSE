using System;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Malformed-input contracts and, from Task 8, output independence.
    /// Malformed API or resolved host evidence throws; valid but unsupported
    /// state returns Unknown with a diagnostic.
    /// </summary>
    public sealed class LilToonAdversarialTests : LilToonFixtureTestBase
    {
        [Test]
        public void AnalyzeBaseMaterial_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => LilToonMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void AnalyzeBaseMaterial_DestroyedMaterial_Throws()
        {
            var material = new Material(Shader.Find(FixtureShaderName));
            UnityEngine.Object.DestroyImmediate(material);

            Assert.Throws<ArgumentException>(
                () => LilToonMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void AnalyzeBaseMaterial_NonLilToonShader_IsUnsupported()
        {
            var material = NewFixtureMaterial();

            var result = LilToonMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(result.IsSupportedMaterial, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Code,
                Is.EqualTo(LilToonSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
        }

        [Test]
        public void InterpretVerifiedMaterial_NullFeatures_Throws()
        {
            var material = NewFixtureMaterial();

            Assert.Throws<ArgumentNullException>(
                () => LilToonMaterialSemantics.InterpretVerifiedMaterial(
                    material, ColorSpace.Linear, null));
        }

        [Test]
        public void InterpretVerifiedMaterial_NullMaterial_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => LilToonMaterialSemantics.InterpretVerifiedMaterial(
                    null, ColorSpace.Linear, AllFeatures));
        }

        // --- output independence ---

        [Test]
        public void DefaultMaterial_AlphaIsComplete()
        {
            var material = NewFixtureMaterial();

            var result = Interpret(material);

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
        }

        [Test]
        public void UnknownAlpha_StaysUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UDIMDiscardCompile", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
        }

        [Test]
        public void Diagnostics_AreImmutable()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseParallax", 1f);

            var result = Interpret(material);

            Assert.That(
                result.Diagnostics,
                Is.InstanceOf<
                    System.Collections.ObjectModel.ReadOnlyCollection<
                        LilToonSemanticDiagnostic>>());
        }

        /// <summary>
        /// Architectural question — can interpretation be reached without
        /// attestation? Mirrors the Poiyomi frontend's
        /// <c>PublicEntry_UnattestedSchemaCompleteShader_IsRefusedBeforeInterpretation</c>.
        ///
        /// The contribution over
        /// <c>AnalyzeBaseMaterial_NonLilToonShader_IsUnsupported</c> is the
        /// direct contrast: this asserts, in one place, that the very same
        /// material which interprets to a Complete alpha through the
        /// verified seam yields an Unknown alpha and a single
        /// material-scoped diagnostic through the public entry. Attestation
        /// cannot be bypassed, and no output interpreter runs without it.
        /// </summary>
        [Test]
        public void PublicEntry_RefusesTheSameMaterialTheSeamFullyInterprets()
        {
            var material = NewFixtureMaterial();

            var viaSeam = Interpret(material);
            Assert.That(viaSeam.Semantics.Alpha.IsComplete, Is.True);

            var viaPublicEntry =
                LilToonMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(viaPublicEntry.IsSupportedMaterial, Is.False);
            Assert.That(
                viaPublicEntry.Diagnostics.Count,
                Is.EqualTo(1),
                "Exactly one diagnostic proves no output interpreter ran.");
            Assert.That(
                viaPublicEntry.Diagnostics[0].Output,
                Is.EqualTo(LilToonSemanticOutput.Material),
                "The refusal is material-scoped, not output-scoped.");
            Assert.That(viaPublicEntry.Semantics.Alpha.IsComplete, Is.False);
        }
    }
}
