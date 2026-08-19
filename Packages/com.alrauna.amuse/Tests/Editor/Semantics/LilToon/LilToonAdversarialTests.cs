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
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
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
        public void DefaultMaterial_IsFullyComplete()
        {
            var material = NewFixtureMaterial();

            var result = Interpret(material);

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Diagnostics, Is.Empty);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void UnknownBaseColor_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetVector("_MainTexHSVG", new Vector4(0.5f, 1f, 1f, 1f));

            var result = Interpret(material);

            Assert.That(result.IsSupportedMaterial, Is.True);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void UnknownAlpha_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UDIMDiscardCompile", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void UnknownEmission_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            material.SetFloat("_EmissionBlendMode", 3f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.True);
        }

        [Test]
        public void UnknownNormal_DoesNotInvalidateOtherOutputs()
        {
            var material = NewFixtureMaterial();
            material.SetTexture("_MainTex", ImportTexture("mainok"));
            material.SetTexture("_BumpMap", ImportTexture("badnormal"));
            material.SetFloat("_UseBumpMap", 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void SharedGate_ReportsOneDiagnosticPerAffectedOutput()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseParallax", 1f);

            var result = Interpret(material);

            // _UseParallax gates BaseColor and Normal, but not Alpha or
            // Emission, and each affected output records exactly one reason.
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.BaseColor).Count,
                Is.EqualTo(1));
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Normal).Count,
                Is.EqualTo(1));
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Alpha).Count,
                Is.EqualTo(0));
            Assert.That(
                DiagnosticsFor(result, LilToonSemanticOutput.Emission).Count,
                Is.EqualTo(0));
            Assert.That(result.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(result.Semantics.Emission.IsComplete, Is.True);
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
        /// material which interprets to four Complete outputs through the
        /// verified seam yields four Unknown outputs and a single
        /// material-scoped diagnostic through the public entry. Attestation
        /// cannot be bypassed, and no output interpreter runs without it.
        /// </summary>
        [Test]
        public void PublicEntry_RefusesTheSameMaterialTheSeamFullyInterprets()
        {
            var material = NewFixtureMaterial();

            var viaSeam = Interpret(material);
            Assert.That(viaSeam.Semantics.BaseColor.IsComplete, Is.True);
            Assert.That(viaSeam.Semantics.Alpha.IsComplete, Is.True);
            Assert.That(viaSeam.Semantics.Emission.IsComplete, Is.True);
            Assert.That(viaSeam.Semantics.Normal.IsComplete, Is.True);

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
            Assert.That(viaPublicEntry.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(viaPublicEntry.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(viaPublicEntry.Semantics.Emission.IsComplete, Is.False);
            Assert.That(viaPublicEntry.Semantics.Normal.IsComplete, Is.False);
        }
    }
}
