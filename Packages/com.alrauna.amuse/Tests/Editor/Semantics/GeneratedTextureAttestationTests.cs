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
            ReplacementTextureAttestation.ResetForTests();
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(TestContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(TestContainerPath);
            }

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(ArbitraryContainerPath) != null)
            {
                AssetDatabase.DeleteAsset(ArbitraryContainerPath);
            }
        }

        private static void InstallAdmittedCompressorVersion()
        {
            ReplacementTextureAttestation.ResetForTests();
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "0.9.0";
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
        public void SubAssetInDerivedSubAssetContainer_IsRefused()
        {
            var derivedContainer = ScriptableObject.CreateInstance<ForeignNamespace.DerivedSubAssetContainer>();
            AssetDatabase.CreateAsset(derivedContainer, ArbitraryContainerPath);

            var textureInDerived = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            textureInDerived.name = "MainTex (AAO UV Packed)";
            AssetDatabase.AddObjectToAsset(textureInDerived, ArbitraryContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                textureInDerived, out var producer);

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

        [Test]
        public void PersistedCopyWithUnadmittedProducerVersion_IsRefused()
        {
            var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            copy.name = "MainTex_compressed";
            AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
            AssetDatabase.SaveAssets();
            ReplacementTextureAttestation.ResetForTests();
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull =
                _ => "0.9.0";
            ReplacementTextureAttestation.SetAdmittedVersionsForTests();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                copy, out var producer);

            Assert.That(isCharacterized, Is.False);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
        }

        [Test]
        public void PersistedCopyWithUninstalledProducer_IsRefused()
        {
            var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            copy.name = "MainTex_compressed";
            AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
            AssetDatabase.SaveAssets();
            ReplacementTextureAttestation.ResetForTests();
            ReplacementTextureAttestation.ReadInstalledPackageVersionOrNull = _ => null;

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                copy, out var producer);

            Assert.That(isCharacterized, Is.False);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
        }

        [Test]
        public void PersistedCompressedCopyInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
        {
            InstallAdmittedCompressorVersion();
            var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            copy.name = "MainTex_compressed";
            AssetDatabase.AddObjectToAsset(copy, TestContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                copy, out var producer);

            Assert.That(isCharacterized, Is.True);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.LimitexTextureCompressor));
        }

        [Test]
        public void PersistedBakedCopyInNdmfContainer_IsIdentifiedAsCharacterizedProducer()
        {
            InstallAdmittedCompressorVersion();
            var baked = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            baked.name = "MainTex_baked";
            AssetDatabase.AddObjectToAsset(baked, TestContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                baked, out var producer);

            Assert.That(isCharacterized, Is.True);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.LimitexTextureCompressor));
        }

        [Test]
        public void PersistedCompressedCopyInDerivedContainer_IsRefused()
        {
            InstallAdmittedCompressorVersion();
            var derived = ScriptableObject.CreateInstance<ForeignNamespace.DerivedSubAssetContainer>();
            AssetDatabase.CreateAsset(derived, ArbitraryContainerPath);
            var copy = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            copy.name = "MainTex_compressed";
            AssetDatabase.AddObjectToAsset(copy, ArbitraryContainerPath);
            AssetDatabase.SaveAssets();

            var isCharacterized = GeneratedTextureAttestation.TryIdentifyProducer(
                copy, out var producer);

            Assert.That(isCharacterized, Is.False);
            Assert.That(producer, Is.EqualTo(GeneratedTextureProducer.None));
        }

        [Test]
        public void InBuildBakedOutputWithoutRegistration_StaysRefused()
        {
            InstallAdmittedCompressorVersion();
            var baked = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            baked.name = "MainTex_baked";

            var isReplacement = ReplacementTextureAttestation.TryIdentifyReplacement(
                baked, out var source);

            Assert.That(isReplacement, Is.False);
            Assert.That(source, Is.Null);
        }
    }
}

namespace Alrauna.Amuse.Tests.Editor.Semantics.ForeignNamespace
{
    public class SubAssetContainer : ScriptableObject
    {
    }

    public class DerivedSubAssetContainer : nadena.dev.ndmf.runtime.SubAssetContainer
    {
    }
}
