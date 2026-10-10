using System;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// On LIL_RENDER 0 the forward pass assigns fd.col.a = 1.0 unconditionally,
    /// after every alpha-writing block, and the subpass alpha path is excluded
    /// entirely by #if LIL_RENDER &gt; 0. Alpha is therefore a constant from
    /// attested shader identity; only fragment-removing mechanisms remain.
    /// </summary>
    public sealed class LilToonAlphaTests : LilToonFixtureTestBase
    {
        [Test]
        public void OpaqueVariant_IsConstantOne()
        {
            var material = NewFixtureMaterial();

            var alpha = Interpret(material).Semantics.Alpha;

            Assert.That(alpha.IsComplete, Is.True);
            var value = alpha.GetCompleteValue();
            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [Test]
        public void OpaqueVariant_IgnoresColorAlphaAndMainTexAlpha()
        {
            var material = NewFixtureMaterial();
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));
            material.SetTexture("_MainTex", ImportTexture("alphatex"));

            var value = Interpret(material).Semantics.Alpha.GetCompleteValue();

            Assert.That(value.Kind, Is.EqualTo(ScalarSemanticValueKind.Constant));
            Assert.That(value.GetConstantValue(), Is.EqualTo(1f));
        }

        [TestCase("_Invisible")]
        [TestCase("_UDIMDiscardCompile")]
        public void CoverageMechanism_KeepsAlphaUnknown(string property)
        {
            var material = NewFixtureMaterial();
            material.SetFloat(property, 1f);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Alpha,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                property);
        }

        [Test]
        public void NonFiniteCoverageProperty_KeepsAlphaUnknown()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UDIMDiscardCompile", float.NaN);

            var result = Interpret(material);

            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            AssertSingleDiagnostic(
                result,
                LilToonSemanticOutput.Alpha,
                LilToonSemanticDiagnosticCode.UnsupportedFeature,
                "_UDIMDiscardCompile");
        }

        [Test]
        public void MainTexture_RequestedButAbsentFromEvidence_RefusesInsteadOfDeclaredDefault()
        {
            var evidence = CreateEvidenceWithMainTexEntryAbsent();

            var alpha = LilToonCutoutMaterialSemantics
                .InterpretVerifiedCutoutAlpha(evidence, out var unknownReason);

            Assert.That(alpha.IsComplete, Is.False);
            Assert.That(
                unknownReason.Kind,
                Is.EqualTo(AlphaUnknownKind.UnsupportedFeature));
            Assert.That(unknownReason.Property, Is.EqualTo("_MainTex"));
        }

        /// <summary>
        /// Synthetic evidence admitted in shape for the cutout alpha reads
        /// before the main texture: every coverage gate exactly zero, an
        /// inert dissolve, an inert scroll and rotate, a finite cutoff, an
        /// inert distance fade, a finite tint alpha, an off alpha mask, and
        /// both layer toggles off. The _MainTex entry is requested but holds
        /// no value, exactly as capture records a requested texture slot
        /// absent from the material.
        /// </summary>
        private static CapturedMaterialEvidence
            CreateEvidenceWithMainTexEntryAbsent()
        {
            var scalars = new[]
            {
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Invisible", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UDIMDiscardCompile", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UDIMDiscardMode", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_ShiftBackfaceUV", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UseParallax", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UseDither", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask1", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask2", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask3", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask4", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask5", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask6", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask7", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMask8", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_IDMaskControlsDissolve", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Cutoff", true, 0.5f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UseMain2ndTex", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main2ndTexAlphaMode", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_UseMain3rdTex", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_Main3rdTexAlphaMode", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_AlphaMaskMode", true, 0f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_AlphaMaskScale", true, 1f),
                new CapturedMaterialEvidence.ScalarEntry(
                    "_AlphaMaskValue", true, 0f),
            };

            var vectors = new[]
            {
                new CapturedMaterialEvidence.VectorEntry(
                    "_DissolveParams", true, Vector4.zero),
                new CapturedMaterialEvidence.VectorEntry(
                    "_MainTex_ScrollRotate", true, Vector4.zero),
                new CapturedMaterialEvidence.VectorEntry(
                    "_DistanceFade", true, Vector4.zero),
            };

            var colors = new[]
            {
                new CapturedMaterialEvidence.ColorEntry(
                    "_Color", true, Color.white),
            };

            var textureEntries = new[]
            {
                new CapturedMaterialEvidence.TextureEntry(
                    "_MainTex", false, default),
            };

            return new CapturedMaterialEvidence(
                hasShaderName: false,
                shaderName: null,
                hasActiveColorSpace: false,
                activeColorSpace: default,
                presence: Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                scalars: scalars,
                colors: colors,
                vectors: vectors,
                textureAssignments: textureEntries,
                textures: Array.Empty<CapturedTextureEvidence>());
        }
    }
}
