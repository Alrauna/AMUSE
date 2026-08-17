using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    public sealed class PoiyomiMaterialSemanticsTests
    {
        private readonly List<UnityEngine.Object> _created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private Material NewMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test setup requires the built-in shader '{shaderName}'.");
            var material = new Material(shader);
            _created.Add(material);
            return material;
        }

        [Test]
        public void AnalyzeBaseMaterial_NullMaterial_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => PoiyomiMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void AnalyzeBaseMaterial_DestroyedMaterial_ThrowsArgumentException()
        {
            // A live C# reference to a destroyed Unity object is neither a
            // valid analyzable material nor a true null; it must be rejected
            // as a malformed argument, not analyzed.
            var material = NewMaterial("Unlit/Color");
            UnityEngine.Object.DestroyImmediate(material);
            _created.Remove(material);

            Assert.Throws<ArgumentException>(
                () => PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void AnalyzeBaseMaterial_ValidNonPoiyomiShader_IsUnsupported()
        {
            var material = NewMaterial("Unlit/Color");

            var result = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(result.IsSupportedMaterial, Is.False);
        }

        [Test]
        public void AnalyzeBaseMaterial_UnsupportedResult_HasFourUnknownOutputsAndOneMaterialDiagnostic()
        {
            var material = NewMaterial("Unlit/Color");

            var result = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);

            Assert.That(result.Semantics, Is.Not.Null);
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);

            Assert.That(result.Diagnostics, Is.Not.Null);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Output,
                Is.EqualTo(PoiyomiSemanticOutput.Material));
            Assert.That(
                result.Diagnostics[0].Detail,
                Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void AnalyzeBaseMaterial_Diagnostics_AreImmutableDefensiveCopy()
        {
            var material = NewMaterial("Unlit/Color");

            var result = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);
            var mutableView =
                result.Diagnostics as IList<PoiyomiSemanticDiagnostic>;

            Assert.That(
                mutableView,
                Is.Not.Null,
                "Diagnostics should be a concrete read-only list for this probe.");
            Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        }

        [Test]
        public void AnalyzeBaseMaterial_Result_DoesNotRetainInputMaterial()
        {
            var material = NewMaterial("Unlit/Color");

            var result = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);
            UnityEngine.Object.DestroyImmediate(material);
            _created.Remove(material);

            // The result is a self-contained snapshot: destroying the source
            // material leaves every field fully readable.
            Assert.That(result.IsSupportedMaterial, Is.False);
            Assert.That(result.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(
                result.Diagnostics[0].Output,
                Is.EqualTo(PoiyomiSemanticOutput.Material));
            Assert.That(result.Semantics.BaseColor.IsComplete, Is.False);
            Assert.That(result.Semantics.Alpha.IsComplete, Is.False);
            Assert.That(result.Semantics.Emission.IsComplete, Is.False);
            Assert.That(result.Semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void PoiyomiSemanticOutput_DeclaresDeterministicDiagnosticOrder()
        {
            // Diagnostics are ordered Material, BaseColor, Alpha, Emission,
            // Normal. The enum ordinal encodes that order.
            Assert.That(
                (int)PoiyomiSemanticOutput.Material,
                Is.LessThan((int)PoiyomiSemanticOutput.BaseColor));
            Assert.That(
                (int)PoiyomiSemanticOutput.BaseColor,
                Is.LessThan((int)PoiyomiSemanticOutput.Alpha));
            Assert.That(
                (int)PoiyomiSemanticOutput.Alpha,
                Is.LessThan((int)PoiyomiSemanticOutput.Emission));
            Assert.That(
                (int)PoiyomiSemanticOutput.Emission,
                Is.LessThan((int)PoiyomiSemanticOutput.Normal));
        }
    }

    /// <summary>
    /// Exact source attestation: the normalized-source hash and the identity
    /// conjunction. These exercise the pure evidence logic with original short
    /// strings and constructed evidence; they never require the real Poiyomi
    /// shader in the project.
    /// </summary>
    public sealed class PoiyomiSourceAttestationTests
    {
        private static string Hash(string source)
        {
            return PoiyomiMaterialSemantics.ComputeNormalizedSourceHash(source);
        }

        private static PoiyomiSourceEvidence Evidence(
            string shaderName = PoiyomiMaterialSemantics.PoiyomiToonShaderName,
            bool isLocked = false,
            bool hasReadableSource = true,
            string assetGuid = PoiyomiMaterialSemantics.CanonicalShaderGuid,
            string normalizedSourceHash =
                PoiyomiMaterialSemantics.CanonicalNormalizedSourceHash,
            bool hasPackage = true,
            string packageName = PoiyomiMaterialSemantics.PoiyomiPackageName,
            string packageVersion = PoiyomiMaterialSemantics.PoiyomiPackageVersion,
            bool hasRequiredSchema = true)
        {
            return new PoiyomiSourceEvidence(
                shaderName,
                isLocked,
                hasReadableSource,
                assetGuid,
                normalizedSourceHash,
                hasPackage,
                packageName,
                packageVersion,
                hasRequiredSchema);
        }

        [Test]
        public void NormalizedHash_IgnoresLeadingUtf8Bom()
        {
            Assert.That(Hash("﻿abc\ndef"), Is.EqualTo(Hash("abc\ndef")));
        }

        [Test]
        public void NormalizedHash_TreatsLfCrlfAndCrAsEqual()
        {
            var lf = Hash("a\nb\nc");

            Assert.That(Hash("a\r\nb\r\nc"), Is.EqualTo(lf));
            Assert.That(Hash("a\rb\rc"), Is.EqualTo(lf));
        }

        [Test]
        public void NormalizedHash_ChangesWhenOneCharacterChanges()
        {
            Assert.That(
                Hash("Poiyomi Toon"),
                Is.Not.EqualTo(Hash("Poiyomi Toom")));
        }

        [Test]
        public void NormalizedHash_IsLowercaseSha256Hex()
        {
            Assert.That(Hash("abc"), Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void Identity_CanonicalUnlockedEvidence_IsSupported()
        {
            var supported = PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                Evidence(),
                out var diagnostic);

            Assert.That(supported, Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Identity_LegacyInstallWithoutPackage_IsSupported()
        {
            // A legacy Assets/_PoiyomiShaders install has no package metadata;
            // the GUID and exact source hash remain the proof.
            var supported = PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                Evidence(hasPackage: false, packageName: null, packageVersion: null),
                out var diagnostic);

            Assert.That(supported, Is.True);
            Assert.That(diagnostic, Is.Null);
        }

        [Test]
        public void Identity_WrongShaderName_IsUnsupportedShader()
        {
            AssertUnsupported(
                Evidence(shaderName: "Standard"),
                PoiyomiSemanticDiagnosticCode.UnsupportedShader);
        }

        [Test]
        public void Identity_AlternateOfficialLookingName_IsUnsupportedShader()
        {
            // The Two Pass variant shares a prefix but is a different shader.
            AssertUnsupported(
                Evidence(shaderName: ".poiyomi/Poiyomi Toon Two Pass"),
                PoiyomiSemanticDiagnosticCode.UnsupportedShader);
        }

        [Test]
        public void Identity_LockedShader_IsUnsupportedShader()
        {
            AssertUnsupported(
                Evidence(isLocked: true),
                PoiyomiSemanticDiagnosticCode.UnsupportedShader);
        }

        [Test]
        public void Identity_UnreadableSource_IsMissingSourceEvidence()
        {
            AssertUnsupported(
                Evidence(
                    hasReadableSource: false,
                    assetGuid: null,
                    normalizedSourceHash: null),
                PoiyomiSemanticDiagnosticCode.MissingSourceEvidence);
        }

        [Test]
        public void Identity_WrongGuid_IsMissingSourceEvidence()
        {
            AssertUnsupported(
                Evidence(assetGuid: "00000000000000000000000000000000"),
                PoiyomiSemanticDiagnosticCode.MissingSourceEvidence);
        }

        [Test]
        public void Identity_WrongPackageName_IsMissingSourceEvidence()
        {
            AssertUnsupported(
                Evidence(packageName: "com.example.other"),
                PoiyomiSemanticDiagnosticCode.MissingSourceEvidence);
        }

        [Test]
        public void Identity_WrongPackageVersion_IsUnsupportedVersion()
        {
            AssertUnsupported(
                Evidence(packageVersion: "9.3.63"),
                PoiyomiSemanticDiagnosticCode.UnsupportedVersion);
        }

        [Test]
        public void Identity_ModifiedSourceHash_IsModifiedShaderSource()
        {
            AssertUnsupported(
                Evidence(
                    normalizedSourceHash:
                        "0000000000000000000000000000000000000000000000000000000000000000"),
                PoiyomiSemanticDiagnosticCode.ModifiedShaderSource);
        }

        [Test]
        public void Identity_MissingRequiredSchema_IsModifiedShaderSource()
        {
            // A missing required property is contradictory source evidence,
            // not a runtime exception.
            AssertUnsupported(
                Evidence(hasRequiredSchema: false),
                PoiyomiSemanticDiagnosticCode.ModifiedShaderSource);
        }

        private static void AssertUnsupported(
            PoiyomiSourceEvidence evidence,
            PoiyomiSemanticDiagnosticCode expectedCode)
        {
            var supported = PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                evidence,
                out var diagnostic);

            Assert.That(supported, Is.False);
            Assert.That(diagnostic, Is.Not.Null);
            Assert.That(diagnostic.Output, Is.EqualTo(PoiyomiSemanticOutput.Material));
            Assert.That(diagnostic.Code, Is.EqualTo(expectedCode));
            Assert.That(diagnostic.Detail, Is.Not.Null.And.Not.Empty);
        }
    }
}
