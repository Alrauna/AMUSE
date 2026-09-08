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
                out var chainHigh);

            Assert.That(okHigh, Is.True);
            Assert.That(chainHigh[0].IsFullyNonOpaque, Is.True);

            // Threshold 0.4 is lower than 0.502 -> texels become opaque.
            var okLow = UnityGeneratedTextureEvidence.TryCapture(
                halfAlphaTexture,
                TextureChannel.Alpha,
                0.4f,
                out var chainLow);

            Assert.That(okLow, Is.True);
            Assert.That(chainLow[0].IsFullyOpaque, Is.True);
        }

        [Test]
        public void SessionCache_ReusesPreviouslyCapturedChain()
        {
            var ok1 = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                out var chain1);

            var ok2 = UnityGeneratedTextureEvidence.TryCapture(
                _streamingSubTex,
                TextureChannel.Alpha,
                1.0f,
                out var chain2);

            Assert.That(ok1, Is.True);
            Assert.That(ok2, Is.True);
            Assert.That(ReferenceEquals(chain1, chain2), Is.True);
        }
    }
}
