using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Frontend selection, and an explicit record of its public-project limit.
    /// <para>
    /// The public development project installs neither Poiyomi nor lilToon, so
    /// no material here can pass either frontend's source attestation. These
    /// tests therefore exercise the real refusal path on real Unity objects and
    /// make no claim about vendor dispatch, which remains a production
    /// capability the public suite cannot observe.
    /// </para>
    /// </summary>
    public sealed class UnityMaterialSemanticsTests
    {
        private const string TempFolder = "Assets/AmuseTests_MaterialDispatch";

        private Material _material;
        private readonly List<Material> _batchMaterials = new List<Material>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_MaterialDispatch");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_material != null)
            {
                UnityEngine.Object.DestroyImmediate(_material);
            }

            _material = null;

            foreach (var material in _batchMaterials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            _batchMaterials.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        private static void AssertAllUnknown(MaterialSemantics semantics)
        {
            Assert.That(semantics, Is.Not.Null);
            Assert.That(semantics.BaseColor.IsComplete, Is.False);
            Assert.That(semantics.Alpha.IsComplete, Is.False);
            Assert.That(semantics.Emission.IsComplete, Is.False);
            Assert.That(semantics.Normal.IsComplete, Is.False);
        }

        [Test]
        public void NullMaterialIsAllUnknownRatherThanAThrow()
        {
            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(null));
        }

        [Test]
        public void DestroyedMaterialIsAllUnknown()
        {
            var material = new Material(Shader.Find("Unlit/Color"));
            UnityEngine.Object.DestroyImmediate(material);

            AssertAllUnknown(UnityMaterialSemantics.AnalyzeBaseMaterial(material));
        }

        [Test]
        public void MaterialNeitherFrontendAttestsIsAllUnknown()
        {
            _material = new Material(Shader.Find("Unlit/Color"));

            AssertAllUnknown(
                UnityMaterialSemantics.AnalyzeBaseMaterial(_material));
        }

        [Test]
        public void AllUnknownIsUnknownInEveryOutput()
        {
            AssertAllUnknown(UnityMaterialSemantics.AllUnknown());
        }

        [Test]
        public void CaptureAlphaMaterialsKeepsFamilyRequestsIsolated()
        {
            var poiyomi = NewMaterial(
                "poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());
            var lilToon = NewMaterial(
                "liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(
                new[] { poiyomi, lilToon });

            Assert.That(captured.Count, Is.EqualTo(2));
            Assert.That(
                captured[0].Family, Is.EqualTo(CapturedAlphaMaterialFamily.Poiyomi));
            Assert.That(
                captured[1].Family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.Throws<ArgumentException>(
                () => captured[0].Evidence.TryGetScalar("_Invisible", out _));
            Assert.Throws<ArgumentException>(
                () => captured[1].Evidence.TryGetScalar(
                    "_AlphaForceOpaque", out _));

            var poiyomiAlpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                captured[0].Evidence);
            var lilToonAlpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                captured[1].Evidence);
            Assert.That(poiyomiAlpha.IsComplete, Is.True);
            Assert.That(
                poiyomiAlpha.GetCompleteValue().GetConstantValue(), Is.EqualTo(1f));
            Assert.That(lilToonAlpha.IsComplete, Is.True);
            Assert.That(
                lilToonAlpha.GetCompleteValue().GetConstantValue(),
                Is.EqualTo(1f));
        }

        /// <summary>
        /// Request selection identifies the family and hands back that family's
        /// existing alpha request. It deliberately does not attest the source:
        /// this material carries the supported shader name over a stand-in
        /// source no attestation can verify, and selection still succeeds.
        /// <see cref="ClosedCaptureRevalidatesSourceAttestation"/> is the pass
        /// that refuses it.
        /// </summary>
        [Test]
        public void SelectionIdentifiesPoiyomiWithoutAttestingSource()
        {
            var material = NewMaterial(
                "selected-poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequest(
                material, out var family, out var request);

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.Poiyomi));
            Assert.That(
                request,
                Is.SameAs(PoiyomiMaterialSemantics.AlphaEvidenceRequest),
                "selection must hand back the family's existing request");
        }

        [Test]
        public void SelectionIdentifiesLilToonWithoutAttestingSource()
        {
            var material = NewMaterial(
                "selected-liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequest(
                material, out var family, out var request);

            Assert.That(selected, Is.True);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.LilToon));
            Assert.That(
                request,
                Is.SameAs(LilToonMaterialSemantics.AlphaEvidenceRequest),
                "selection must hand back the family's existing request");
        }

        [Test]
        public void SelectionRejectsAnUnsupportedShaderFamily()
        {
            _material = new Material(Shader.Find("Unlit/Color"));

            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequest(
                _material, out var family, out var request);

            Assert.That(selected, Is.False);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported));
            Assert.That(request, Is.Null);
        }

        [Test]
        public void SelectionRejectsANullMaterial()
        {
            var selected = UnityMaterialSemantics.TrySelectAlphaMaterialRequest(
                null, out var family, out var request);

            Assert.That(selected, Is.False);
            Assert.That(
                family, Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported));
            Assert.That(request, Is.Null);
        }

        [Test]
        public void ClosedCaptureRevalidatesLilToonSourceAttestation()
        {
            var material = NewMaterial(
                "unattested-liltoon.shader",
                LilToonSourceAttestation.SupportedShaderName,
                LilToonProperties());

            var success = UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                new[] { material },
                new[] { CapturedAlphaMaterialFamily.LilToon },
                LilToonMaterialSemantics.AlphaEvidenceRequest,
                out var captured);

            Assert.That(success, Is.False);
            Assert.That(captured, Is.Null);
        }

        [Test]
        public void ClosedCaptureRevalidatesSourceAttestation()
        {
            var material = NewMaterial(
                "unattested-poiyomi.shader",
                PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                PoiyomiProperties());

            var success = UnityMaterialSemantics.TryCaptureClosedAlphaMaterials(
                new[] { material },
                new[] { CapturedAlphaMaterialFamily.Poiyomi },
                PoiyomiMaterialSemantics.AlphaEvidenceRequest,
                out var captured);

            Assert.That(success, Is.False);
            Assert.That(captured, Is.Null);
        }

        [Test]
        public void AnalyzeAlphaMaterialUnsupportedFamilyIsAllUnknown()
        {
            _material = new Material(Shader.Find("Unlit/Color"));
            var captured = UnityMaterialSemantics.CaptureAlphaMaterials(
                new[] { _material });

            Assert.That(captured.Count, Is.EqualTo(1));
            Assert.That(
                captured[0].Family,
                Is.EqualTo(CapturedAlphaMaterialFamily.Unsupported));
            AssertAllUnknown(
                UnityMaterialSemantics.AnalyzeAlphaMaterial(captured[0]));
        }

        private Material NewMaterial(
            string fileName,
            string shaderName,
            string properties)
        {
            var path = TempFolder + "/" + fileName;
            File.WriteAllText(
                path,
                "Shader \"" + shaderName + "\"\n" +
                "{\n    Properties\n    {" + properties +
                "\n    }\n    SubShader { Pass {} }\n}\n");
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            var material = new Material(shader);
            _batchMaterials.Add(material);
            return material;
        }

        private static string LilToonProperties()
        {
            return @"
        [HideInInspector] _lilToonVersion (""Version"", Int) = 45
        _Invisible (""Invisible"", Int) = 0
        _UDIMDiscardCompile (""UDIM"", Int) = 0";
        }

        private static string PoiyomiProperties()
        {
            return @"
        shader_master_label (""Master"", Float) = 0
        _ShaderOptimizerEnabled (""Locked"", Float) = 0
        _MainTex (""Main"", 2D) = ""white"" {}
        _Color (""Color"", Color) = (1,1,1,1)
        _BumpMap (""Bump"", 2D) = ""bump"" {}
        _EmissionMap (""Emission"", 2D) = ""white"" {}
        _EnableEmission (""Emission 0"", Float) = 0
        _EnableEmission1 (""Emission 1"", Float) = 0
        _EnableEmission2 (""Emission 2"", Float) = 0
        _EnableEmission3 (""Emission 3"", Float) = 0
        _AlphaForceOpaque (""Force Opaque"", Float) = 1
        _MainIgnoreTexAlpha (""Ignore Alpha"", Float) = 0
        _AlphaToCoverage (""Coverage"", Float) = 0
        _AlphaSharpenedA2C (""Sharpened"", Float) = 0
        _AlphaDithering (""Dither"", Float) = 0
        _EnableDissolve (""Dissolve"", Float) = 0
        _EnableUDIMDiscardOptions (""UDIM"", Float) = 0
        _AlphaMod (""Alpha Mod"", Float) = 0
        _MainAlphaMaskMode (""Mask Mode"", Float) = 0
        _AlphaDistanceFade (""Distance"", Float) = 0
        _AlphaFresnel (""Fresnel"", Float) = 0
        _AlphaAngular (""Angular"", Float) = 0
        _AlphaAudioLinkEnabled (""Audio Alpha"", Float) = 0
        _EnableAudioLink (""Audio"", Float) = 0
        _AlphaGlobalMask (""Global Mask"", Float) = 0
        _AlphaPremultiply (""Premultiply"", Float) = 0
        _BackFaceEnabled (""Backface"", Float) = 0
        _RGBMaskEnabled (""RGB Mask"", Float) = 0
        _DecalEnabled (""Decal 0"", Float) = 0
        _DecalEnabled1 (""Decal 1"", Float) = 0
        _DecalEnabled2 (""Decal 2"", Float) = 0
        _DecalEnabled3 (""Decal 3"", Float) = 0
        _EnableFlipbook (""Flipbook"", Float) = 0
        _EnableRimLighting (""Rim"", Float) = 0
        _EnableRim2Lighting (""Rim 2"", Float) = 0
        _EnableDepthRimLighting (""Depth Rim"", Float) = 0
        _EnableEnvironmentalRim (""Env Rim"", Float) = 0
        _VideoEffectsEnable (""Video"", Float) = 0
        _EnableTouchGlow (""Touch"", Float) = 0
        _MainVertexColoringEnabled (""Vertex"", Float) = 0
        _MainTexUV (""UV"", Float) = 0
        _MainTexPan (""Pan"", Vector) = (0,0,0,0)
        _MainPixelMode (""Pixel"", Float) = 0
        _MainTexStochastic (""Stochastic"", Float) = 0";
        }
    }
}
