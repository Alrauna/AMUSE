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
        private ScriptableObject _container;
        private Texture2D _subTexture;

        [SetUp]
        public void SetUp()
        {
            _container = ScriptableObject.CreateInstance<ScriptableObject>();
            AssetDatabase.CreateAsset(_container, TestContainerPath);

            _subTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            _subTexture.name = "AAO_Atlas_SubAsset";
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
        public void LooseInMemoryTexture_IsRefused()
        {
            var loose = new Texture2D(4, 4, TextureFormat.RGBA32, false);
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
