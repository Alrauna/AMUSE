using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// Falsifier fixtures for the Multi mode-consistency gate. Every input
    /// is built through the production capture path: a stand-in material
    /// whose keyword set and gate scalars are set explicitly, captured under
    /// a request that names exactly the facts the fixtures present. The mode
    /// arrives as the resolved integer the gate surface takes, so the
    /// fixtures never capture `_TransparentMode`; the captured scalars are
    /// the two gate rules read, and the derivation consults those captured
    /// scalars only - a fact the request does not carry is not shown to be
    /// on, mirroring the vendor fallback that treats a missing property as
    /// feature-off.
    /// --- Falsifier: a gate that trusts the shader name alone, or a gate that demands the animation-derived color keywords, fails these fixtures. ---
    /// </summary>
    public sealed class LilToonMultiModeGateTests
    {
        // The gate stand-in declares exactly the two scalar facts the gate
        // rules read, so SetFloat carries them and the capture records real
        // values. The shared LilToonSemanticTest stand-in declares neither,
        // and SetFloat on an undeclared property is a silent no-op that
        // leaves the captured scalars absent.
        private const string LilToonFixtureShader =
            "Hidden/Alrauna/AmuseTests/LilToonMultiModeGateTest";

        private readonly List<Material> _materials = new List<Material>();

        [TearDown]
        public void TearDown()
        {
            foreach (var material in _materials)
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }

            _materials.Clear();
        }

        // The vendor writes UNITY_UI_ALPHACLIP only at mode one
        // (lilMaterialUtils.cs:397), so at mode 0 the captured keyword set
        // is a state the pinned derivation cannot produce from the captured
        // scalars. A gate that trusts the shader name alone admits this
        // material; the gate must refuse it as a keyword and mode mismatch.
        [Test]
        public void KeywordOutsideTheOpaqueModeDerivationRefusesAsKeywordModeMismatch()
        {
            var evidence = GateEvidence(
                new[] { "UNITY_UI_ALPHACLIP" }, clippingCanceller: 0f, asOverlay: 0f);

            Assert.That(
                LilToonMultiModeGate.Evaluate(evidence, 0, out var refusal),
                Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.KeywordModeMismatch));
        }

        // GEOM_TYPE_LEAF and EFFECT_HUE_VARIATION are animation-derived
        // color keywords: rim-light direction and tone correction, not alpha
        // facts, and on the NDMF play-mode path the vendor preprocess
        // returns early so they never appear. The gate must admit either
        // state at mode 0 - presence must not need a producing condition,
        // and absence must not be demanded. A gate that requires the
        // animation-derived color keywords refuses the absent case; a gate
        // that refuses every captured keyword it cannot derive refuses the
        // present case.
        [TestCase(true)]
        [TestCase(false)]
        public void AnimationDerivedColorKeywordAdmitsAtOpaqueModePresentOrAbsent(
            bool present)
        {
            var keywords = present
                ? new[] { "EFFECT_HUE_VARIATION", "GEOM_TYPE_LEAF" }
                : Array.Empty<string>();
            var evidence = GateEvidence(
                keywords, clippingCanceller: 0f, asOverlay: 0f);

            Assert.That(
                LilToonMultiModeGate.Evaluate(evidence, 0, out _),
                Is.True);
        }

        // _UseClippingCanceller has a render-time consumer
        // (LIL_MULTI_SHOULD_CLIPPING) and the regular families carry no
        // matching gate to inherit, so the follower refuses the enabled
        // state outright, ahead of any keyword reasoning.
        [Test]
        public void EnabledClippingCancellerRefuses()
        {
            var evidence = GateEvidence(
                Array.Empty<string>(), clippingCanceller: 1f, asOverlay: 0f);

            Assert.That(
                LilToonMultiModeGate.Evaluate(evidence, 0, out var refusal),
                Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(LilToonMultiResolutionRefusal.ClippingCancellerEnabled));
        }

        // _AsOverlay disables the vendor depth passes on the material, a
        // form the regular families do not carry, so the enabled overlay
        // pass is refused as unsupported.
        [Test]
        public void EnabledOverlayPassRefuses()
        {
            var evidence = GateEvidence(
                Array.Empty<string>(), clippingCanceller: 0f, asOverlay: 1f);

            Assert.That(
                LilToonMultiModeGate.Evaluate(evidence, 0, out var refusal),
                Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(
                    LilToonMultiResolutionRefusal.OverlayPassEnableUnsupported));
        }

        // The all-off vendor state at mode 0 derives the empty keyword set,
        // so a material that presents no keywords and holds both gate
        // scalars at 0 admits.
        [Test]
        public void EmptyKeywordSetAtOpaqueModeAdmits()
        {
            var evidence = GateEvidence(
                Array.Empty<string>(), clippingCanceller: 0f, asOverlay: 0f);

            Assert.That(
                LilToonMultiModeGate.Evaluate(evidence, 0, out _),
                Is.True);
        }

        /// <summary>
        /// The writer writes feature keywords for every feature the
        /// material turns on, on every inspector save
        /// (Editor/lilMaterialUtils.cs:397-468 at the pin): shadow, rim
        /// shade, emission with a blend mask, emission second with a blend
        /// mask, normal maps, anisotropy, matcaps with custom normals, rim,
        /// glitter, audio link as local, backlight, parallax with POM,
        /// reflection, main second, and main third. A real avatar material
        /// therefore carries keyword state far beyond the mode rows, and an
        /// exact-set comparison against the mode rows alone refuses
        /// materials the vendor itself considers consistent. The full
        /// writer transcription must admit this state at mode 0 and derive
        /// the same keyword set the writer writes.
        /// </summary>
        // --- Falsifier: a gate whose keyword table covers only the mode rows refuses a real feature-loaded material the vendor writer accepts. ---
        [Test]
        public void FeatureLoadedOpaqueMaterialAdmitsAndDerivesTheWriterKeywords()
        {
            var evidence = GateEvidence(
                new[]
                {
                    "_REQUIRE_UV2", "AUTO_KEY_VALUE", "_EMISSION",
                    "GEOM_TYPE_BRANCH", "_SUNDISK_SIMPLE", "_NORMALMAP",
                    "EFFECT_BUMP", "SOURCE_GBUFFER",
                    "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                    "_SPECULARHIGHLIGHTS_OFF", "GEOM_TYPE_MESH",
                    "_METALLICGLOSSMAP", "_SPECGLOSSMAP",
                    "_MAPPING_6_FRAMES_LAYOUT", "_SUNDISK_HIGH_QUALITY",
                    "_COLORADDSUBDIFF_ON", "_COLORCOLOR_ON",
                    "ANTI_FLICKER", "_PARALLAXMAP",
                    "PIXELSNAP_ON", "_GLOSSYREFLECTIONS_OFF",
                },
                clippingCanceller: 0f,
                asOverlay: 0f,
                scalars: new Dictionary<string, float>
                {
                    { "_UseShadow", 1f },
                    { "_UseRimShade", 1f },
                    { "_UseEmission", 1f },
                    { "_UseEmission2nd", 1f },
                    { "_UseBumpMap", 1f },
                    { "_UseBump2ndMap", 1f },
                    { "_UseAnisotropy", 1f },
                    { "_UseMatCap", 1f },
                    { "_MatCapCustomNormal", 1f },
                    { "_UseMatCap2nd", 1f },
                    { "_MatCap2ndCustomNormal", 1f },
                    { "_UseRim", 1f },
                    { "_UseGlitter", 1f },
                    { "_UseAudioLink", 1f },
                    { "_AudioLinkAsLocal", 1f },
                    { "_UseBacklight", 1f },
                    { "_UseParallax", 1f },
                    { "_UsePOM", 1f },
                    { "_UseReflection", 1f },
                    { "_UseMain2ndTex", 1f },
                    { "_UseMain3rdTex", 1f },
                },
                textures: new Dictionary<string, Texture>
                {
                    { "_EmissionBlendMask", Texture2D.whiteTexture },
                    { "_Emission2ndBlendMask", Texture2D.whiteTexture },
                });

            Assert.That(
                LilToonMultiModeGate.Evaluate(evidence, 0, out var refusal),
                Is.True,
                "a feature-loaded material the vendor writer accepts must " +
                "admit: " + refusal);

            var derived = LilToonMultiModeGate.DeriveKeywordSet(evidence, 0);
            foreach (var keyword in new[]
                     {
                         "_REQUIRE_UV2", "AUTO_KEY_VALUE", "_EMISSION",
                         "GEOM_TYPE_BRANCH", "_SUNDISK_SIMPLE", "_NORMALMAP",
                         "EFFECT_BUMP", "SOURCE_GBUFFER",
                         "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                         "_SPECULARHIGHLIGHTS_OFF", "GEOM_TYPE_MESH",
                         "_METALLICGLOSSMAP", "_SPECGLOSSMAP",
                         "_MAPPING_6_FRAMES_LAYOUT", "_SUNDISK_HIGH_QUALITY",
                         "_COLORADDSUBDIFF_ON", "_COLORCOLOR_ON",
                         "ANTI_FLICKER", "_PARALLAXMAP",
                         "PIXELSNAP_ON", "_GLOSSYREFLECTIONS_OFF",
                     })
            {
                CollectionAssert.Contains(
                    derived, keyword, keyword + " must derive");
            }
        }

        private CapturedMaterialEvidence GateEvidence(
            string[] keywords,
            float clippingCanceller,
            float asOverlay,
            IReadOnlyDictionary<string, float> scalars = null,
            IReadOnlyDictionary<string, Vector4> vectors = null,
            IReadOnlyDictionary<string, Texture> textures = null)
        {
            var material = NewMaterial();
            material.shaderKeywords = keywords;
            material.SetFloat("_UseClippingCanceller", clippingCanceller);
            material.SetFloat("_AsOverlay", asOverlay);
            if (scalars != null)
            {
                foreach (var pair in scalars)
                {
                    material.SetFloat(pair.Key, pair.Value);
                }
            }
            if (vectors != null)
            {
                foreach (var pair in vectors)
                {
                    material.SetVector(pair.Key, pair.Value);
                }
            }
            if (textures != null)
            {
                foreach (var pair in textures)
                {
                    material.SetTexture(pair.Key, pair.Value);
                }
            }
            return Capture(material, GateRequest());
        }

        private Material NewMaterial()
        {
            var shader = Shader.Find(LilToonFixtureShader);
            Assert.That(shader, Is.Not.Null, LilToonFixtureShader);
            var material = new Material(shader);
            _materials.Add(material);
            return material;
        }

        private static CapturedMaterialEvidence Capture(
            Material material,
            MaterialEvidenceRequest request)
        {
            return UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, request),
            })[0];
        }

        /// <summary>
        /// The closed request every fixture captures under: the two gate
        /// scalars the mode-consistency rules read, every vendor
        /// keyword-writer condition input, and the keyword set. Every
        /// fixture sets exactly these material properties, so no fixture
        /// depends on stand-in shader defaults.
        /// </summary>
        private static MaterialEvidenceRequest GateRequest()
        {
            return new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: new[]
                {
                    "_UseClippingCanceller",
                    "_AsOverlay",
                    "_UseShadow",
                    "_UseRimShade",
                    "_UseEmission",
                    "_UseEmission2nd",
                    "_UseBumpMap",
                    "_UseBump2ndMap",
                    "_UseAnisotropy",
                    "_UseMatCap",
                    "_UseMatCap2nd",
                    "_MatCapCustomNormal",
                    "_MatCap2ndCustomNormal",
                    "_UseRim",
                    "_RimDirStrength",
                    "_UseGlitter",
                    "_UseAudioLink",
                    "_AudioLinkAsLocal",
                    "_UseBacklight",
                    "_UseParallax",
                    "_UsePOM",
                    "_UseReflection",
                    "_MainGradationStrength",
                    "_UseMain2ndTex",
                    "_UseMain3rdTex",
                    "_UseDither",
                    "_AlphaMaskMode",
                },
                colorProperties: Array.Empty<string>(),
                vectorProperties: new[]
                {
                    "_MainTexHSVG",
                    "_Main2ndTexDecalAnimation",
                    "_Main3rdTexDecalAnimation",
                    "_Main2ndDissolveParams",
                    "_Main3rdDissolveParams",
                    "_DissolveParams",
                    "_DistanceFade",
                    "_OutlineTexHSVG",
                },
                textureProperties: new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_EmissionBlendMask",
                        TextureEvidenceKinds.None),
                    new TexturePropertyEvidenceRequest(
                        "_Emission2ndBlendMask",
                        TextureEvidenceKinds.None),
                },
                captureKeywords: true);
        }
    }
}
