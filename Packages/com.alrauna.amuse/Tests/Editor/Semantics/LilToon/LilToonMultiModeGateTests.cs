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

        private CapturedMaterialEvidence GateEvidence(
            string[] keywords,
            float clippingCanceller,
            float asOverlay)
        {
            var material = NewMaterial();
            material.shaderKeywords = keywords;
            material.SetFloat("_UseClippingCanceller", clippingCanceller);
            material.SetFloat("_AsOverlay", asOverlay);
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
        /// scalars the mode-consistency rules read, plus the keyword set.
        /// Every fixture sets exactly these material properties, so no
        /// fixture depends on stand-in shader defaults.
        /// </summary>
        private static MaterialEvidenceRequest GateRequest()
        {
            return new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: new[] { "_UseClippingCanceller", "_AsOverlay" },
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties: Array.Empty<TexturePropertyEvidenceRequest>(),
                captureKeywords: true);
        }
    }
}
