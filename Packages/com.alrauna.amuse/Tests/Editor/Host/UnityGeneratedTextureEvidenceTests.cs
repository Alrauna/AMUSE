using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    [TestFixture]
    public class UnityGeneratedTextureEvidenceTests
    {
        private const string ContainerPath = "Assets/AmuseTests_GeneratedEvidence.asset";
        private const string ArbitraryContainerPath = "Assets/AmuseTests_ArbitraryGeneratedEvidence.asset";
        private ScriptableObject _container;
        private Texture2D _streamingSubTex;

        [SetUp]
        public void SetUp()
        {
            _container = ScriptableObject.CreateInstance<nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(_container, ContainerPath);

            _streamingSubTex = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            var serializedTexture = new SerializedObject(_streamingSubTex);
            var streamingProperty = serializedTexture.FindProperty("m_StreamingMipmaps");
            if (streamingProperty != null)
            {
                streamingProperty.boolValue = true;
                serializedTexture.ApplyModifiedPropertiesWithoutUndo();
            }

            for (var m = 0; m < _streamingSubTex.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 8 >> m);
                var px = new Color32[dim * dim];
                for (var i = 0; i < px.Length; i++)
                {
                    px[i] = new Color32(255, 255, 255, 255);
                }

                _streamingSubTex.SetPixels32(px, m);
            }

            _streamingSubTex.Apply(false, false);
            _streamingSubTex.name = "MainTex (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(_streamingSubTex, ContainerPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(ContainerPath);
            }

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ArbitraryContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(ArbitraryContainerPath);
            }
        }

        [Test]
        public void StreamingGeneratedSubAsset_CapturesAlphaChainDirectlyFromLiveObject()
        {
            Assert.That(_streamingSubTex.streamingMipmaps, Is.True);

            var ok = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain.Count, Is.EqualTo(_streamingSubTex.mipmapCount));
            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void UnityAlphaFieldEvidence_CapturesCharacterizedStreamingSubAsset()
        {
            Assert.That(_streamingSubTex.streamingMipmaps, Is.True);

            var ok = UnityAlphaFieldEvidence.TryCapture(
                _streamingSubTex,
                out var source,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void NullTexture_ReturnsFalse()
        {
            var ok = UnityGeneratedTextureEvidence.TryCapture(
                null,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain);

            Assert.That(ok, Is.False);
            Assert.That(chain, Is.Null);
        }

        [Test]
        public void UnsupportedChannel_ReturnsFalse()
        {
            var ok = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Green,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain);

            Assert.That(ok, Is.False);
            Assert.That(chain, Is.Null);
        }

        [Test]
        public void UncharacterizedSubAsset_ReturnsFalse()
        {
            var uncharacterized = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            uncharacterized.name = "RegularSubAsset";
            AssetDatabase.AddObjectToAsset(uncharacterized, ContainerPath);
            AssetDatabase.SaveAssets();

            var ok = UnityGeneratedTextureEvidence.TryCapture(
                uncharacterized,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain);

            Assert.That(ok, Is.False);
            Assert.That(chain, Is.Null);
        }

        [Test]
        public void RedChannel_ExtractsRedTexels()
        {
            var redTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Red is fully opaque 255. Alpha is transparent 0.
                pixels[i] = new Color32(255, 0, 0, 0);
            }

            redTexture.SetPixels32(pixels);
            redTexture.Apply(false, false);
            redTexture.name = "AAO Monotone Red";
            AssetDatabase.AddObjectToAsset(redTexture, ContainerPath);
            AssetDatabase.SaveAssets();

            var ok = UnityGeneratedTextureEvidence.TryCapture(
                redTexture,
                TextureChannel.Red,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void CutoffThreshold_AppliesThresholdCorrectly()
        {
            var halfAlphaTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Alpha is 128 (approximately 0.502).
                pixels[i] = new Color32(255, 255, 255, 128);
            }

            halfAlphaTexture.SetPixels32(pixels);
            halfAlphaTexture.Apply(false, false);
            halfAlphaTexture.name = "AlphaMask (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(halfAlphaTexture, ContainerPath);
            AssetDatabase.SaveAssets();

            // Threshold 0.6 is higher than 0.502 -> texels become transparent.
            var okHigh = UnityGeneratedTextureEvidence.TryCapture(
                halfAlphaTexture,
                TextureChannel.Alpha,
                0.6f,
                AlphaPolicyBounds.Inert,
                out var chainHigh);

            Assert.That(okHigh, Is.True);
            Assert.That(chainHigh[0].IsFullyNonOpaque, Is.True);

            // Threshold 0.4 is lower than 0.502 -> texels become opaque.
            var okLow = UnityGeneratedTextureEvidence.TryCapture(
                halfAlphaTexture,
                TextureChannel.Alpha,
                0.4f,
                AlphaPolicyBounds.Inert,
                out var chainLow);

            Assert.That(okLow, Is.True);
            Assert.That(chainLow[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void DistinctMipLevels_AreCapturedWithoutCrossContamination()
        {
            var multiMipTex = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            for (var m = 0; m < multiMipTex.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 8 >> m);
                var px = new Color32[dim * dim];
                // Even mips are fully opaque 255. Odd mips are transparent 0.
                var alpha = (byte)(m % 2 == 0 ? 255 : 0);
                for (var i = 0; i < px.Length; i++)
                {
                    px[i] = new Color32(255, 255, 255, alpha);
                }

                multiMipTex.SetPixels32(px, m);
            }

            multiMipTex.Apply(false, false);
            multiMipTex.name = "MipIsolation (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(multiMipTex, ContainerPath);
            AssetDatabase.SaveAssets();

            var ok = UnityGeneratedTextureEvidence.TryCapture(
                multiMipTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain, Is.Not.Null);
            Assert.That(chain.Count, Is.EqualTo(multiMipTex.mipmapCount));
            Assert.That(chain[0].IsFullyOpaque, Is.True);
            Assert.That(chain[1].IsFullyNonOpaque, Is.True);
            Assert.That(chain[2].IsFullyOpaque, Is.True);
            Assert.That(chain[3].IsFullyNonOpaque, Is.True);
        }

        /// <summary>
        /// The generated route's per-level residency degradation, exercised
        /// through the simulated-limit parameter of the full overload. The
        /// live global mipmap limit cannot be induced in EditMode, so this is
        /// the same declared-state seam the gate predicates use: the capture
        /// must skip the non-resident prefix (no blit of a level the GPU does
        /// not hold), keep its declared shape as a flagged placeholder, and
        /// still capture the resident levels.
        /// </summary>
        [Test]
        public void ALimitedGeneratedCaptureSkipsNonResidentLevelsAndFlagsThem()
        {
            var multiMipTex = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            for (var m = 0; m < multiMipTex.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 8 >> m);
                var px = new Color32[dim * dim];
                // Even mips are fully opaque 255. Odd mips are transparent 0.
                var alpha = (byte)(m % 2 == 0 ? 255 : 0);
                for (var i = 0; i < px.Length; i++)
                {
                    px[i] = new Color32(255, 255, 255, alpha);
                }

                multiMipTex.SetPixels32(px, m);
            }

            multiMipTex.Apply(false, false);
            multiMipTex.name = "MipResidency (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(multiMipTex, ContainerPath);
            AssetDatabase.SaveAssets();

            var ok = UnityGeneratedTextureEvidence.TryCapture(
                multiMipTex,
                TextureChannel.Alpha,
                1.0f,
                UnityGeneratedTextureEvidence.IsStreamingMipmapResident,
                AlphaPolicyBounds.Inert,
                1,
                out var chain);

            Assert.That(ok, Is.True);
            Assert.That(chain.Count, Is.EqualTo(multiMipTex.mipmapCount));
            Assert.That(
                chain.IsLevelWithoutEvidence(0), Is.True,
                "the simulated limit removes the highest-resolution mip");
            for (var level = 1; level < chain.Count; level++)
            {
                Assert.That(
                    chain.IsLevelWithoutEvidence(level), Is.False,
                    "level " + level);
            }

            // The captured resident levels keep the mip-isolation pattern,
            // so the skip pinned the right prefix and captured the rest.
            Assert.That(chain[1].IsFullyNonOpaque, Is.True);
            Assert.That(chain[2].IsFullyOpaque, Is.True);
            Assert.That(chain[3].IsFullyNonOpaque, Is.True);
        }

        /// <summary>
        /// The active limit rides in the session key: a chain captured under
        /// one limit carries provenance for exactly that limit, so the
        /// unlimited capture that follows must not be served the flagged
        /// limited chain from the cache.
        /// </summary>
        [Test]
        public void DifferentLimitsDoNotShareTheGeneratedSessionCache()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            for (var m = 0; m < texture.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 8 >> m);
                var px = new Color32[dim * dim];
                for (var i = 0; i < px.Length; i++)
                {
                    px[i] = new Color32(255, 255, 255, 255);
                }

                texture.SetPixels32(px, m);
            }

            texture.Apply(false, false);
            texture.name = "CacheKey (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(texture, ContainerPath);
            AssetDatabase.SaveAssets();

            var limitedOk = UnityGeneratedTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                1.0f,
                UnityGeneratedTextureEvidence.IsStreamingMipmapResident,
                AlphaPolicyBounds.Inert,
                1,
                out var limited);
            var unlimitedOk = UnityGeneratedTextureEvidence.TryCapture(
                texture,
                TextureChannel.Alpha,
                1.0f,
                UnityGeneratedTextureEvidence.IsStreamingMipmapResident,
                AlphaPolicyBounds.Inert,
                0,
                out var unlimited);

            Assert.That(limitedOk, Is.True);
            Assert.That(unlimitedOk, Is.True);
            Assert.That(limited.IsLevelWithoutEvidence(0), Is.True);
            Assert.That(
                unlimited.IsLevelWithoutEvidence(0), Is.False,
                "The unlimited capture must not serve the limited chain.");
            Assert.That(unlimited[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void CutoffBoundary_StrictlyRefusesBelowCutoffByte()
        {
            var boundaryTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Byte 128 has normalized value 128 / 255 = 0.50196...
                pixels[i] = new Color32(255, 255, 255, 128);
            }

            boundaryTex.SetPixels32(pixels);
            boundaryTex.Apply(false, false);
            boundaryTex.name = "BoundaryCutoff (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(boundaryTex, ContainerPath);
            AssetDatabase.SaveAssets();

            // Threshold 0.502f is strictly greater than 128 / 255f.
            // Texels must be non-opaque.
            var okAbove = UnityGeneratedTextureEvidence.TryCapture(
                boundaryTex,
                TextureChannel.Alpha,
                0.502f,
                AlphaPolicyBounds.Inert,
                out var chainAbove);

            Assert.That(okAbove, Is.True);
            Assert.That(chainAbove[0].IsFullyNonOpaque, Is.True);

            // Threshold 0.501f is strictly less than 128 / 255f.
            // Texels must be opaque.
            var okBelow = UnityGeneratedTextureEvidence.TryCapture(
                boundaryTex,
                TextureChannel.Alpha,
                0.501f,
                AlphaPolicyBounds.Inert,
                out var chainBelow);

            Assert.That(okBelow, Is.True);
            Assert.That(chainBelow[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void SessionCache_ReusesPreviouslyCapturedChain()
        {
            var ok1 = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain1);

            var ok2 = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain2);

            Assert.That(ok1, Is.True);
            Assert.That(ok2, Is.True);
            Assert.That(ReferenceEquals(chain1, chain2), Is.True);
        }

        [Test]
        public void SessionCache_DoesNotReturnStaleResultAcrossSlightlyDifferentCutoffs()
        {
            var deltaTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Byte 128 has normalized value 128 / 255 = 0.50196078...
                pixels[i] = new Color32(255, 255, 255, 128);
            }

            deltaTex.SetPixels32(pixels);
            deltaTex.Apply(false, false);
            deltaTex.name = "DeltaCutoff (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(deltaTex, ContainerPath);
            AssetDatabase.SaveAssets();

            // Cutoff 0.501960f is slightly below 128/255. Texels must be opaque.
            var okBelow = UnityGeneratedTextureEvidence.TryCapture(
                deltaTex,
                TextureChannel.Alpha,
                0.501960f,
                AlphaPolicyBounds.Inert,
                out var chainBelow);

            Assert.That(okBelow, Is.True);
            Assert.That(chainBelow[0].IsFullyOpaque, Is.True);

            // Cutoff 0.501962f is slightly above 128/255. Texels must be non-opaque.
            // The delta is 0.000002f. Quantization would collide in cache.
            var okAbove = UnityGeneratedTextureEvidence.TryCapture(
                deltaTex,
                TextureChannel.Alpha,
                0.501962f,
                AlphaPolicyBounds.Inert,
                out var chainAbove);

            Assert.That(okAbove, Is.True);
            Assert.That(chainAbove[0].IsFullyNonOpaque, Is.True);
            Assert.That(ReferenceEquals(chainBelow, chainAbove), Is.False);
        }

        [TestCase(true, false, 0, false)]
        [TestCase(true, true, 1, false)]
        [TestCase(true, true, 0, true)]
        [TestCase(false, false, 1, true)]
        [TestCase(false, true, 0, true)]
        public void IsStreamingMipmapResident_EnforcesResidencyGate(
            bool streaming,
            bool requestedLoaded,
            int loadedMip,
            bool expected)
        {
            var resident = UnityGeneratedTextureEvidence.IsStreamingMipmapResident(
                streaming,
                requestedLoaded,
                loadedMip);

            Assert.That(resident, Is.EqualTo(expected));
        }

        [Test]
        public void TryCapture_RefusesWhenResidencyPredicateFails()
        {
            var ok = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                _ => false,
                AlphaPolicyBounds.Inert,
                _streamingSubTex.activeMipmapLimit,
                out var chain);

            Assert.That(ok, Is.False);
            Assert.That(chain, Is.Null);
        }

        [Test]
        public void HostCapabilitiesPass_RequiresAsyncReadbackAndSupportedFormats()
        {
            Assert.That(
                UnityGeneratedTextureEvidence.HostCapabilitiesPass(true, true, true),
                Is.True);
            Assert.That(
                UnityGeneratedTextureEvidence.HostCapabilitiesPass(false, true, true),
                Is.False);
            Assert.That(
                UnityGeneratedTextureEvidence.HostCapabilitiesPass(true, false, true),
                Is.False);
            Assert.That(
                UnityGeneratedTextureEvidence.HostCapabilitiesPass(true, true, false),
                Is.False);
        }

        [Test]
        public void IsExpectedTargetFormat_RequiresExactFormatMatch()
        {
            Assert.That(
                UnityGeneratedTextureEvidence.IsExpectedTargetFormat(
                    UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                    UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm),
                Is.True);
            Assert.That(
                UnityGeneratedTextureEvidence.IsExpectedTargetFormat(
                    UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB,
                    UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm),
                Is.False);
        }

        [Test]
        public void IsExpectedLevelSize_RequiresMatchingDimensions()
        {
            Assert.That(
                UnityGeneratedTextureEvidence.IsExpectedLevelSize(8, 8, 8, 8),
                Is.True);
            Assert.That(
                UnityGeneratedTextureEvidence.IsExpectedLevelSize(8, 4, 8, 8),
                Is.False);
        }

        [Test]
        public void IsExpectedBufferLength_RequiresExactProduct()
        {
            Assert.That(
                UnityGeneratedTextureEvidence.IsExpectedBufferLength(64, 8, 8),
                Is.True);
            Assert.That(
                UnityGeneratedTextureEvidence.IsExpectedBufferLength(32, 8, 8),
                Is.False);
        }

        [Test]
        public void ClearCache_EvictsPreviouslyCapturedChains()
        {
            var ok1 = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain1);

            Assert.That(ok1, Is.True);
            Assert.That(chain1, Is.Not.Null);

            UnityGeneratedTextureEvidence.ClearCache();

            var ok2 = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var chain2);

            Assert.That(ok2, Is.True);
            Assert.That(chain2, Is.Not.Null);
            Assert.That(ReferenceEquals(chain1, chain2), Is.False);
        }

        [Test]
        public void SessionCache_DoesNotServeInertEvidenceUnderGateOnBounds()
        {
            var strayTex = new Texture2D(4, 4, TextureFormat.RGBA32, true);
            for (var m = 0; m < strayTex.mipmapCount; m++)
            {
                var dim = Mathf.Max(1, 4 >> m);
                var px = new Color32[dim * dim];
                for (var i = 0; i < px.Length; i++)
                {
                    px[i] = new Color32(255, 255, 255, 255);
                }

                // One stray at byte 3: a witness under the inert
                // bounds, noise under a 2 percent gate.
                if (m == 0)
                {
                    px[0] = new Color32(255, 255, 255, 3);
                }

                strayTex.SetPixels32(px, m);
            }

            strayTex.Apply(false, false);
            strayTex.name = "AlphaMask (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(strayTex, ContainerPath);
            AssetDatabase.SaveAssets();

            var okInert = UnityGeneratedTextureEvidence.TryCapture(
                strayTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.Inert,
                out var inertChain);

            var okGateOn = UnityGeneratedTextureEvidence.TryCapture(
                strayTex,
                TextureChannel.Alpha,
                1.0f,
                AlphaPolicyBounds.From(100, 2),
                out var gateOnChain);

            Assert.That(okInert, Is.True);
            Assert.That(okGateOn, Is.True);
            Assert.That(ReferenceEquals(inertChain, gateOnChain), Is.False);

            // The gate-on chain carries the erased flag where the
            // inert chain carries a witness, so the second call
            // re-captured under its own policy instead of serving the
            // cached chain.
            Assert.That(inertChain[0].GetAlpha(0, 0), Is.EqualTo(0));
            Assert.That(
                gateOnChain[0].GetAlpha(0, 0),
                Is.EqualTo(AlphaTextureData.ErasedFlag));
        }
    }
}
