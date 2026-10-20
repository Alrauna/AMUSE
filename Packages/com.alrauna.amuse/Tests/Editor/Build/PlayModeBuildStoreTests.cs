using System;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class PlayModeBuildStoreTests
    {
        [Test]
        public void ComputeAvatarKeyStripsCloneSuffix()
        {
            var go = new GameObject("Avatar(Clone)");
            try
            {
                var key = PlayModeBuildStore.ComputeAvatarKey(go.transform);
                Assert.That(key, Does.EndWith("::Avatar"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputeAvatarKeyStripsCloneSuffixWithSpace()
        {
            var go = new GameObject("Avatar (Clone)");
            try
            {
                var key = PlayModeBuildStore.ComputeAvatarKey(go.transform);
                Assert.That(key, Does.EndWith("::Avatar"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputeAvatarKeyReturnsEmptyWhenNull()
        {
            var key = PlayModeBuildStore.ComputeAvatarKey(null);
            Assert.That(key, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ComputePolicyFingerprintReturnsEmptyWhenNull()
        {
            var fingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                null, BuildTarget.StandaloneWindows64);
            Assert.That(fingerprint, Is.EqualTo(string.Empty));
        }

        [Test]
        public void ComputePolicyFingerprintProducesConsistentString()
        {
            var go = new GameObject("Avatar");
            try
            {
                var component = go.AddComponent<AmuseAvatarOptimizer>();
                var fingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
                Assert.That(fingerprint, Does.StartWith("StandaloneWindows64;"));
                Assert.That(fingerprint.Split(';').Length, Is.EqualTo(20));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputePolicyFingerprintChangesWhenMasterSwitchesOrAnisotropyToggle()
        {
            var go = new GameObject("Avatar");
            try
            {
                var component = go.AddComponent<AmuseAvatarOptimizer>();
                var baseline = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);

                SetStoredBool(component, "_amuseDisabled", true);
                var disabledFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
                Assert.That(disabledFingerprint, Is.Not.EqualTo(baseline));

                SetStoredBool(component, "_amuseDisabled", false);
                SetStoredBool(component, "_alphaSeparatorEnabled", false);
                var separatorOffFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
                Assert.That(separatorOffFingerprint, Is.Not.EqualTo(baseline));

                SetStoredBool(component, "_alphaSeparatorEnabled", true);
                SetStoredBool(component, "_ignoreOutOfRangeMaterialSlots", false);
                var ignoreSlotsFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
                Assert.That(ignoreSlotsFingerprint, Is.Not.EqualTo(baseline));

                SetStoredBool(component, "_ignoreOutOfRangeMaterialSlots", true);
                SetStoredBool(component, "_acceptDesktopAnisotropyDifferences", false);
                var desktopAnisoFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
                Assert.That(desktopAnisoFingerprint, Is.Not.EqualTo(baseline));

                SetStoredBool(component, "_acceptDesktopAnisotropyDifferences", true);
                SetStoredBool(component, "_acceptMobileAnisotropyDifferences", false);
                var mobileAnisoFingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    component, BuildTarget.StandaloneWindows64);
                Assert.That(mobileAnisoFingerprint, Is.Not.EqualTo(baseline));

                SetStoredBool(component, "_acceptMobileAnisotropyDifferences", true);
                SetStoredBool(component, "_cutoutSeparatorEnabled", false);
                var cutoutSeparatorFingerprint =
                    PlayModeBuildStore.ComputePolicyFingerprint(
                        component, BuildTarget.StandaloneWindows64);
                Assert.That(cutoutSeparatorFingerprint, Is.Not.EqualTo(baseline));

                SetStoredBool(component, "_cutoutSeparatorEnabled", true);
                SetStoredBool(component, "_transparentSeparatorEnabled", false);
                var transparentSeparatorFingerprint =
                    PlayModeBuildStore.ComputePolicyFingerprint(
                        component, BuildTarget.StandaloneWindows64);
                Assert.That(transparentSeparatorFingerprint, Is.Not.EqualTo(baseline));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputePolicyFingerprintDistinguishesTransparentAndCutoutValues()
        {
            var first = new GameObject("AMUSE store policy first");
            var second = new GameObject("AMUSE store policy second");
            try
            {
                var firstComponent = first.AddComponent<AmuseAvatarOptimizer>();
                var secondComponent = second.AddComponent<AmuseAvatarOptimizer>();

                // A first serialization round trip runs the one-time
                // upgrade backfill. The marker is then set, so the
                // cutout edits below keep their stored values.
                var primedFirst = EditorJsonUtility.ToJson(firstComponent);
                EditorJsonUtility.FromJsonOverwrite(primedFirst, firstComponent);
                var primedSecond = EditorJsonUtility.ToJson(secondComponent);
                EditorJsonUtility.FromJsonOverwrite(primedSecond, secondComponent);

                SetStoredInt(
                    secondComponent, "_preserveTransparencyMaxMipLevel", 6);
                SetStoredInt(
                    secondComponent, "_preserveTransparencyMinTextureSize", 256);
                SetStoredInt(
                    secondComponent, "_cutoutPreserveTransparencyMaxMipLevel", 5);
                SetStoredInt(
                    secondComponent, "_cutoutPreserveTransparencyMinTextureSize", 320);
                SetStoredInt(
                    secondComponent, "_cutoutMinimumOpaqueCoveragePercent", 40);
                SetStoredInt(
                    secondComponent,
                    "_cutoutMinimumOpaqueCoverageToPreservePercent", 15);

                var firstFingerprint =
                    PlayModeBuildStore.ComputePolicyFingerprint(
                        firstComponent, BuildTarget.StandaloneWindows64);
                var secondFingerprint =
                    PlayModeBuildStore.ComputePolicyFingerprint(
                        secondComponent, BuildTarget.StandaloneWindows64);

                // The two records differ in their transparent values
                // and in their cutout values, so the store match
                // refuses the second record under the first
                // fingerprint.
                Assert.That(secondFingerprint, Is.Not.EqualTo(firstFingerprint));

                // Guard: a cutout-only edit alone moves the
                // fingerprint, so the store never folds the cutout arm
                // into the transparent one.
                SetStoredInt(
                    firstComponent, "_cutoutPreserveTransparencyMaxMipLevel", 5);
                Assert.That(
                    PlayModeBuildStore.ComputePolicyFingerprint(
                        firstComponent, BuildTarget.StandaloneWindows64),
                    Is.Not.EqualTo(firstFingerprint));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void SaveAndTryLoadRoundtripsThroughSessionState()
        {
            var record = new PlayModeBuildRecord
            {
                avatarKey = "TestScene::Avatar",
                policyFingerprint = "fingerprint_1",
                avatarRows = Array.Empty<SerializedTransformationRow>(),
                rendererRows = new[]
                {
                    new SerializedTransformationRow
                    {
                        kind = 2,
                        rendererPath = "Body",
                        rendererTypeName = "SkinnedMeshRenderer",
                        slotIndex = 0,
                        sentence = "Convertable: 100 out of 100 polygons may be moved"
                    }
                }
            };

            try
            {
                PlayModeBuildStore.Save(record);
                var loaded = PlayModeBuildStore.TryLoad("TestScene::Avatar", "fingerprint_1");
                Assert.That(loaded, Is.Not.Null);
                Assert.That(loaded.rendererRows.Length, Is.EqualTo(1));
                Assert.That(loaded.rendererRows[0].rendererPath, Is.EqualTo("Body"));
            }
            finally
            {
                PlayModeBuildStore.Clear("TestScene::Avatar");
            }
        }

        [Test]
        public void TryLoadReturnsNullOnFingerprintMismatch()
        {
            var record = new PlayModeBuildRecord
            {
                avatarKey = "TestScene::AvatarMismatch",
                policyFingerprint = "fingerprint_original",
                avatarRows = Array.Empty<SerializedTransformationRow>(),
                rendererRows = Array.Empty<SerializedTransformationRow>()
            };

            try
            {
                PlayModeBuildStore.Save(record);
                var loaded = PlayModeBuildStore.TryLoad(
                    "TestScene::AvatarMismatch", "fingerprint_different");
                Assert.That(loaded, Is.Null);
            }
            finally
            {
                PlayModeBuildStore.Clear("TestScene::AvatarMismatch");
            }
        }

        // Mirrors the store's private "Amuse_PlayMode_" session prefix.
        private static string AvatarKey() => "MatrixScene::Avatar";

        private static string Fingerprint() => "fingerprint_matrix";

        private static string StoreKey() => "Amuse_PlayMode_" + AvatarKey();

        private static void SetStoredInt(
            AmuseAvatarOptimizer component, string field, int value)
        {
            var serialized = new SerializedObject(component);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStoredBool(
            AmuseAvatarOptimizer component, string propertyName, bool value)
        {
            var serialized = new SerializedObject(component);
            var prop = serialized.FindProperty(propertyName)
                ?? serialized.FindProperty("_" + char.ToLowerInvariant(propertyName[0]) + propertyName.Substring(1));
            if (prop != null)
            {
                prop.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [Test]
        public void CorruptStoredPayloadsReadAsNoRecordAndEraseTheKey()
        {
            foreach (var payload in new[] { "not json at all", "", "{\"r\":0.5", "[1,2,3]", "{\"r\":0.5}junk", "null" })
            {
                SessionState.SetString(StoreKey(), payload);
                var record = PlayModeBuildStore.TryLoad(AvatarKey(), Fingerprint());
                Assert.That(record, Is.Null, $"corrupt data is data, not a defect: '{payload}'");
                Assert.That(SessionState.GetString(StoreKey(), null), Is.Empty, $"the corrupt payload is erased: '{payload}'");
            }
        }

        [Test]
        public void AWrongTypedFieldReadsAsNoRecordThroughTheFingerprintGuard()
        {
            SessionState.SetString(StoreKey(), "{\"policyFingerprint\":null}");
            Assert.That(PlayModeBuildStore.TryLoad(AvatarKey(), Fingerprint()), Is.Null);
        }

        [Test]
        public void TryLoadReturnsNullWhenKeyNotFound()
        {
            var loaded = PlayModeBuildStore.TryLoad(
                "NonExistentScene::Avatar", "fingerprint_1");
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void ClearRemovesSessionStateEntry()
        {
            var record = new PlayModeBuildRecord
            {
                avatarKey = "TestScene::AvatarToClear",
                policyFingerprint = "fingerprint_1",
                avatarRows = Array.Empty<SerializedTransformationRow>(),
                rendererRows = Array.Empty<SerializedTransformationRow>()
            };

            PlayModeBuildStore.Save(record);
            PlayModeBuildStore.Clear("TestScene::AvatarToClear");
            var loaded = PlayModeBuildStore.TryLoad(
                "TestScene::AvatarToClear", "fingerprint_1");
            Assert.That(loaded, Is.Null);
        }

        [Test]
        public void FromSerializedMapsTheKeptFieldsToMaterialTransformationRow()
        {
            var serialized = new SerializedTransformationRow
            {
                kind = 3,
                rendererPath = "Armature/Body",
                rendererTypeName = "UnityEngine.SkinnedMeshRenderer",
                sourceMaterialPath = "Assets/Materials/Body.mat",
                slotIndex = 1,
                movedTriangles = 50,
                provenTriangles = 50,
                totalTriangles = 100,
                sentence = "Convertable: 50 out of 100 polygons may be moved"
            };

            var row = MaterialTransformationRow.FromSerialized(serialized);
            Assert.That(row.Kind, Is.EqualTo(MaterialTransformationRowKind.Slot));
            Assert.That(row.RendererPath, Is.EqualTo("Armature/Body"));
            Assert.That(row.RendererTypeName, Is.EqualTo("UnityEngine.SkinnedMeshRenderer"));
            Assert.That(row.SourceMaterialPath, Is.EqualTo("Assets/Materials/Body.mat"));
            Assert.That(row.SlotIndex, Is.EqualTo(1));
            Assert.That(row.MovedTriangles, Is.EqualTo(50));
            Assert.That(row.ProvenTriangles, Is.EqualTo(50));
            Assert.That(row.TotalTriangles, Is.EqualTo(100));
            Assert.That(row.Sentence, Is.EqualTo("Convertable: 50 out of 100 polygons may be moved"));
        }

        [Test]
        public void SerializedTransformationRowRoundtripsScenePathAndMaterialPath()
        {
            var record = new PlayModeBuildRecord
            {
                avatarKey = "Scene::Avatar",
                policyFingerprint = "fp1",
                avatarRows = Array.Empty<SerializedTransformationRow>(),
                rendererRows = new[]
                {
                    new SerializedTransformationRow
                    {
                        kind = 3,
                        rendererPath = "SceneRoot/Accessory/Mesh",
                        rendererTypeName = "UnityEngine.SkinnedMeshRenderer",
                        sourceMaterialPath = "Assets/Materials/Accessory.mat",
                        slotIndex = 0,
                        movedTriangles = 10,
                        provenTriangles = 10,
                        totalTriangles = 20,
                        sentence = "Splittable: 10 out of 20 polygons may be moved"
                    }
                }
            };

            PlayModeBuildStore.Save(record);
            var loaded = PlayModeBuildStore.TryLoad("Scene::Avatar", "fp1");
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.rendererRows.Length, Is.EqualTo(1));
            Assert.That(loaded.rendererRows[0].rendererPath, Is.EqualTo("SceneRoot/Accessory/Mesh"));
            Assert.That(loaded.rendererRows[0].sourceMaterialPath, Is.EqualTo("Assets/Materials/Accessory.mat"));
        }
    }
}
