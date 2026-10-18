using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CoreWrapMode = Alrauna.Amuse.Editor.Semantics.TextureWrapMode;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Task 7 adversarial contract integration tests. They exercise the public
    /// entry point for attestation — an unattested schema-complete shader is
    /// refused before any equation runs — and the representation boundary that
    /// keeps an Unknown output from ever presenting a value a downstream
    /// opaque/separation consumer could read.
    ///
    /// Identity/attestation, hash normalization (line-ending equality),
    /// force-opaque-versus-coverage refusal, and per-output refusal cases are
    /// proven in the Task 2/4/5/6 suites and are deliberately not repeated here.
    /// </summary>
    public sealed class PoiyomiAdversarialTests : PoiyomiFixtureTestBase
    {
        // --- Attestation gates the public entry point ----------------------

        [Test]
        public void PublicEntry_UnattestedSchemaCompleteShader_IsRefusedBeforeInterpretation()
        {
            // The fixture exposes the full Poiyomi property schema but is not the
            // pinned source; the public entry point refuses it at the material
            // boundary with one material diagnostic and never reaches an equation.
            var material = NewFixtureMaterial();

            var result = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(result.IsSupportedMaterial, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Output,
                Is.EqualTo(PoiyomiSemanticOutput.Material));
            Assert.That(
                result.Diagnostics[0].Code,
                Is.EqualTo(PoiyomiSemanticDiagnosticCode.UnsupportedShader));
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
        }

        // --- Representation boundary ---------------------------------------

        [Test]
        public void UnknownOutput_PresentsNoValue_ForDownstreamOpaqueOrSeparationUse()
        {
            // Representation boundary only (the classifier and separation planner
            // are neither called nor modified): an unproven output exposes no
            // value a downstream consumer could read as a concrete opaque result.
            // Uncertainty stays uncertain; it never defaults to a more aggressive
            // claim.
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0.5f);

            var alpha = Interpret(material).Semantics.Alpha;

            Assert.That(alpha.IsComplete, Is.False);
            Assert.That(
                () => alpha.GetCompleteValue(),
                Throws.InvalidOperationException,
                "An unknown output must yield no value, forcing conservative "
                    + "handling downstream.");
        }
    }
}
