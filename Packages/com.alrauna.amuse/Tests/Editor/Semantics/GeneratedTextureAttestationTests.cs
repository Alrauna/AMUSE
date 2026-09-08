using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    [TestFixture]
    public class GeneratedTextureAttestationTests
    {
        private const string TestContainerPath = "Assets/AmuseTests_AttestationContainer.asset";
        private const string ArbitraryContainerPath = "Assets/AmuseTests_ArbitraryContainer.asset";
        private ScriptableObject _container;
        private Texture2D _subTexture;

        [SetUp]
        public void SetUp()
        {
            _container = ScriptableObject.CreateInstance<nadena.dev.ndmf.runtime.SubAssetContainer>();
            AssetDatabase.CreateAsset(_container, TestContainerPath);

            _subTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            _subTexture.name = "MainTex (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(_subTexture, TestContainerPath);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(TestContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(TestContainerPath);
            }

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ArbitraryContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(ArbitraryContainerPath);
            }
        }

        [Test]
        public void SubAssetInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
        {
            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                _subTexture, out var producer);

            Assert.That(isCharacterized, Is.True);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.Anatawa12AvatarOptimizer));
        }

        [Test]
        public void MonotoneSubAssetInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
        {
            var monotoneTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            monotoneTexture.name = "AAO Monotone Color";
            AssetDatabase.AddObjectToAsset(monotoneTexture, TestContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                monotoneTexture, out var producer);

            Assert.That(isCharacterized, Is.True);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.Anatawa12AvatarOptimizer));
        }

        [Test]
        public void LooseInMemoryTexture_IsRefused()
        {
            var loose = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            loose.name = "MainTex (AAO UV Packed)";
            try
            {
                var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                    loose, out var producer);

                Assert.That(isCharacterized, Is.False);
                Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
            }
            finally
            {
                Object.DestroyImmediate(loose);
            }
        }

        [Test]
        public void UncharacterizedNameInNdmfContainer_IsRefused()
        {
            var uncharacterized = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            uncharacterized.name = "RandomSubAsset";
            AssetDatabase.AddObjectToAsset(uncharacterized, TestContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                uncharacterized, out var producer);

            Assert.That(isCharacterized, Is.False);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
        }

        [Test]
        public void SubAssetInArbitraryScriptableObjectContainer_IsRefused()
        {
            var arbitraryContainer = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(arbitraryContainer, ArbitraryContainerPath);

            var textureInArbitrary = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            textureInArbitrary.name = "MainTex (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(textureInArbitrary, ArbitraryContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                textureInArbitrary, out var producer);

            Assert.That(isCharacterized, Is.False);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
        }

        [Test]
        public void SubAssetInForeignSubAssetContainer_IsRefused()
        {
            var foreignContainer = ScriptableObject.CreateInstance<ForeignNamespace.SubAssetContainer>();
            AssetDatabase.CreateAsset(foreignContainer, ArbitraryContainerPath);

            var textureInForeign = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            textureInForeign.name = "MainTex (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(textureInForeign, ArbitraryContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                textureInForeign, out var producer);

            Assert.That(isCharacterized, Is.False);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
        }

        [Test]
        public void CharacterizedSubAsset_ResolvesSourceIdentityAndColorInterpretation()
        {
            Assert.That(
                UnityTextureEvidence.TryGetSourceId(_subTexture, out var sourceId),
                Is.True);
            Assert.That(sourceId.Value, Does.StartWith("unity-asset:"));

            Assert.That(
                UnityTextureEvidence.TryGetColorInterpretation(_subTexture, out var colorInterp),
                Is.True);
            Assert.That(colorInterp, Is.EqualTo(TextureColorInterpretation.Srgb));
        }
    }
}

namespace Alrauna.Amuse.Tests.Editor.Semantics.ForeignNamespace
{
    public class SubAssetContainer : ScriptableObject
    {
    }
}
