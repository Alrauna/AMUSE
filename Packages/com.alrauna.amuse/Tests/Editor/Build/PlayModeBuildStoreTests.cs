using System;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class PlayModeBuildStoreTests
    {
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
                        kind = MaterialTransformationRowKind.Renderer,
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

        // Mirrors the store's private "Amuse_PlayMode_v2_" session prefix.
        private static string AvatarKey() => "MatrixScene::Avatar";

        private static string Fingerprint() => "fingerprint_matrix";

        private static string StoreKey() => "Amuse_PlayMode_v2_" + AvatarKey();

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
                kind = MaterialTransformationRowKind.Slot,
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
                        kind = MaterialTransformationRowKind.Slot,
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

        private const string OwnedSceneFolder = "Assets/AmuseTests_PlayOwnership";

        private static SerializedTransformationRow SentenceRow(string sentence)
        {
            return new SerializedTransformationRow
            {
                kind = MaterialTransformationRowKind.Banner,
                sentence = sentence
            };
        }

        [Test]
        public void TwoSameNamedSavedRootsKeepTheirOwnRows()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var firstKey = "";
            var secondKey = "";
            try
            {
                scope.EnsureTempFolder();
                var first = scope.CreateSavedSceneRoot("AMUSE Probe Shared Name");
                var second = scope.CreateSavedSceneRoot("AMUSE Probe Shared Name");
                var firstOptimizer = first.AddComponent<AmuseAvatarOptimizer>();
                var secondOptimizer = second.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                firstKey = PlayModeBuildStore.ComputeAvatarKey(first.transform);
                secondKey = PlayModeBuildStore.ComputeAvatarKey(second.transform);
                Assert.That(firstKey, Is.Not.Empty, "a saved root has a proved key");
                Assert.That(secondKey, Is.Not.Empty, "a saved root has a proved key");
                Assert.That(
                    secondKey, Is.Not.EqualTo(firstKey),
                    "two same-named saved roots must not share one record key");

                // equal policy on both roots, distinct rows per root
                var fingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                    firstOptimizer, EditorUserBuildSettings.activeBuildTarget);
                Assert.That(
                    PlayModeBuildStore.ComputePolicyFingerprint(
                        secondOptimizer, EditorUserBuildSettings.activeBuildTarget),
                    Is.EqualTo(fingerprint), "the two roots carry an equal policy");

                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = firstKey,
                    policyFingerprint = fingerprint,
                    avatarRows = new[] { SentenceRow("First result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = secondKey,
                    policyFingerprint = fingerprint,
                    avatarRows = new[] { SentenceRow("Second result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });

                var loadedFirst = PlayModeBuildStore.TryLoad(firstKey, fingerprint);
                var loadedSecond = PlayModeBuildStore.TryLoad(secondKey, fingerprint);
                Assert.That(loadedFirst.avatarRows[0].sentence, Is.EqualTo("First result."));
                Assert.That(loadedSecond.avatarRows[0].sentence, Is.EqualTo("Second result."));

                PlayModeBuildStore.Clear(firstKey);
                var loadedSecondAfterFirstClear =
                    PlayModeBuildStore.TryLoad(secondKey, fingerprint);
                Assert.That(loadedSecondAfterFirstClear, Is.Not.Null);
                Assert.That(
                    loadedSecondAfterFirstClear.avatarRows[0].sentence,
                    Is.EqualTo("Second result."));
            }
            finally
            {
                // erase only the computed owned record keys before the scene
                // teardown, so a failing assertion leaks no session rows
                if (firstKey != "") PlayModeBuildStore.Clear(firstKey);
                if (secondKey != "") PlayModeBuildStore.Clear(secondKey);
                scope.TearDown();
            }
        }

        [Test]
        public void UnsavedRootsNeverBorrowARecord()
        {
            var container = default(Scene);
            var ownsScene = false;
            var prior = default(Scene);
            var first = (GameObject)null;
            var second = (GameObject)null;
            try
            {
                // container: an already-loaded normal unsaved scene, or one
                // this test creates when none exists. The scene is never saved.
                for (var index = 0; index < SceneManager.sceneCount; index++)
                {
                    var candidate = SceneManager.GetSceneAt(index);
                    if (candidate.path == "" && candidate.isLoaded &&
                        !EditorSceneManager.IsPreviewScene(candidate))
                    {
                        container = candidate;
                        break;
                    }
                }
                if (!container.IsValid())
                {
                    // no untitled normal scene is loaded, so the additive
                    // create cannot hit the untitled-scene refusal
                    container = EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    ownsScene = true;
                }
                Assert.That(
                    container.path, Is.Empty,
                    "the container must be a real unsaved scene");
                prior = SceneManager.GetActiveScene();
                SceneManager.SetActiveScene(container);
                first = new GameObject("AMUSE Probe Unsaved");
                second = new GameObject("AMUSE Probe Unsaved");

                // fixture guards: with no scene asset, the committed identity
                // contract is missing its scene identity part
                var firstKey = PlayModeBuildStore.ComputeAvatarKey(first.transform);
                var secondKey = PlayModeBuildStore.ComputeAvatarKey(second.transform);
                Assert.That(
                    firstKey, Is.Empty,
                    "an unsaved root has no proved authored identity");
                Assert.That(
                    secondKey, Is.Empty,
                    "an unsaved root has no proved authored identity");

                // an empty key authorizes no save, load, or clear, so neither
                // root can hold or take the rows of the other
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = firstKey,
                    policyFingerprint = "fingerprint_unsaved",
                    avatarRows = new[] { SentenceRow("Unsaved first result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = secondKey,
                    policyFingerprint = "fingerprint_unsaved",
                    avatarRows = new[] { SentenceRow("Unsaved second result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                Assert.That(
                    PlayModeBuildStore.TryLoad(firstKey, "fingerprint_unsaved"),
                    Is.Null, "an unsaved root never loads a record");
                Assert.That(
                    PlayModeBuildStore.TryLoad(secondKey, "fingerprint_unsaved"),
                    Is.Null, "an unsaved root never loads the record of the other root");
                PlayModeBuildStore.Clear(firstKey);
                PlayModeBuildStore.Clear(secondKey);
                Assert.That(
                    PlayModeBuildStore.TryLoad(firstKey, "fingerprint_unsaved"),
                    Is.Null);
                Assert.That(
                    PlayModeBuildStore.TryLoad(secondKey, "fingerprint_unsaved"),
                    Is.Null);
            }
            finally
            {
                if (first != null) UnityEngine.Object.DestroyImmediate(first);
                if (second != null) UnityEngine.Object.DestroyImmediate(second);
                if (prior.IsValid() && prior.isLoaded &&
                    SceneManager.GetActiveScene() == container)
                {
                    SceneManager.SetActiveScene(prior);
                }
                // only a scene this test created is ever closed, discarded
                if (ownsScene && container.IsValid() && container.isLoaded)
                {
                    if (SceneManager.GetActiveScene() == container)
                    {
                        for (var index = 0; index < SceneManager.sceneCount; index++)
                        {
                            var candidate = SceneManager.GetSceneAt(index);
                            if (candidate.path != container.path && candidate.isLoaded)
                            {
                                SceneManager.SetActiveScene(candidate);
                                break;
                            }
                        }
                    }
                    EditorSceneManager.CloseScene(container, true);
                }
            }
        }

        [Test]
        public void RenameAfterSaveKeepsTheRecord()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var key = "";
            try
            {
                scope.EnsureTempFolder();
                var root = scope.CreateSavedSceneRoot("AMUSE Probe Rename");
                root.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                key = PlayModeBuildStore.ComputeAvatarKey(root.transform);
                Assert.That(key, Is.Not.Empty, "a saved root has a proved key");
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = key,
                    policyFingerprint = "fingerprint_rename",
                    avatarRows = new[] { SentenceRow("Rename result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });

                root.name = "AMUSE Probe Renamed";
                scope.SaveOwnedScene();
                Assert.That(
                    PlayModeBuildStore.ComputeAvatarKey(root.transform),
                    Is.EqualTo(key), "a rename must not move the record key");
                Assert.That(
                    PlayModeBuildStore.TryLoad(key, "fingerprint_rename")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Rename result."));
            }
            finally
            {
                if (key != "") PlayModeBuildStore.Clear(key);
                scope.TearDown();
            }
        }

        [Test]
        public void ReorderAfterSaveKeepsTheRecord()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var key = "";
            try
            {
                scope.EnsureTempFolder();
                var first = scope.CreateSavedSceneRoot("AMUSE Probe Reorder A");
                var second = scope.CreateSavedSceneRoot("AMUSE Probe Reorder B");
                first.AddComponent<AmuseAvatarOptimizer>();
                second.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                key = PlayModeBuildStore.ComputeAvatarKey(first.transform);
                Assert.That(key, Is.Not.Empty, "a saved root has a proved key");
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = key,
                    policyFingerprint = "fingerprint_reorder",
                    avatarRows = new[] { SentenceRow("Reorder result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });

                first.transform.SetSiblingIndex(1);
                scope.SaveOwnedScene();
                Assert.That(
                    PlayModeBuildStore.ComputeAvatarKey(first.transform),
                    Is.EqualTo(key), "a reorder must not move the record key");
                Assert.That(
                    PlayModeBuildStore.TryLoad(key, "fingerprint_reorder")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Reorder result."));
            }
            finally
            {
                if (key != "") PlayModeBuildStore.Clear(key);
                scope.TearDown();
            }
        }

        [Test]
        public void DestroyedRootComputeTouchesNoSurvivor()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var secondKey = "";
            try
            {
                scope.EnsureTempFolder();
                var first = scope.CreateSavedSceneRoot("AMUSE Probe Doomed");
                var second = scope.CreateSavedSceneRoot("AMUSE Probe Doomed");
                first.AddComponent<AmuseAvatarOptimizer>();
                second.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                var secondKeyComputed =
                    PlayModeBuildStore.ComputeAvatarKey(second.transform);
                secondKey = secondKeyComputed;
                Assert.That(secondKey, Is.Not.Empty, "a saved root has a proved key");
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = secondKey,
                    policyFingerprint = "fingerprint_survivor",
                    avatarRows = new[] { SentenceRow("Second result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });

                var retained = first.transform;
                UnityEngine.Object.DestroyImmediate(first.gameObject);
                scope.SaveOwnedScene();

                // the destroyed root proves no key, so a load or a clear
                // through the retained reference is a no-op for the survivor
                Assert.That(
                    PlayModeBuildStore.ComputeAvatarKey(retained), Is.Empty,
                    "a destroyed root has no proved authored identity");
                Assert.That(
                    PlayModeBuildStore.TryLoad(
                        PlayModeBuildStore.ComputeAvatarKey(retained),
                        "fingerprint_survivor"),
                    Is.Null);
                PlayModeBuildStore.Clear(
                    PlayModeBuildStore.ComputeAvatarKey(retained));

                Assert.That(
                    PlayModeBuildStore.TryLoad(secondKey, "fingerprint_survivor")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Second result."));
            }
            finally
            {
                if (secondKey != "") PlayModeBuildStore.Clear(secondKey);
                scope.TearDown();
            }
        }

        [Test]
        public void ReplacedRootDoesNotJoinTheSurvivor()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var survivorKey = "";
            try
            {
                scope.EnsureTempFolder();
                var replaced = scope.CreateSavedSceneRoot("AMUSE Probe Replace");
                var survivor = scope.CreateSavedSceneRoot("AMUSE Probe Survivor");
                replaced.AddComponent<AmuseAvatarOptimizer>();
                survivor.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                survivorKey = PlayModeBuildStore.ComputeAvatarKey(survivor.transform);
                Assert.That(survivorKey, Is.Not.Empty, "a saved root has a proved key");
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = survivorKey,
                    policyFingerprint = "fingerprint_replaced",
                    avatarRows = new[] { SentenceRow("Survivor result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });

                var retained = replaced.transform;
                UnityEngine.Object.DestroyImmediate(replaced.gameObject);
                var replacement = scope.CreateSavedSceneRoot("AMUSE Probe Replace");
                replacement.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                // the destroyed root proves no key, and the replacement never
                // joins the survivor's key; opaque-key eviction is not required
                Assert.That(
                    PlayModeBuildStore.ComputeAvatarKey(retained), Is.Empty);
                var replacementKey =
                    PlayModeBuildStore.ComputeAvatarKey(replacement.transform);
                Assert.That(
                    replacementKey, Is.Not.EqualTo(survivorKey),
                    "a replacement root must not join the survivor key");
                Assert.That(
                    PlayModeBuildStore.TryLoad(survivorKey, "fingerprint_replaced")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Survivor result."));
            }
            finally
            {
                if (survivorKey != "") PlayModeBuildStore.Clear(survivorKey);
                scope.TearDown();
            }
        }

        [Test]
        public void SamePrefabTwinsKeepTheirOwnRows()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var firstKey = "";
            var secondKey = "";
            try
            {
                scope.EnsureTempFolder();
                var source = scope.CreateSavedSceneRoot("AMUSE Probe Twin Source");
                source.AddComponent<AmuseAvatarOptimizer>();
                var asset = PrefabUtility.SaveAsPrefabAsset(
                    source, OwnedSceneFolder + "/ProbeTwin.prefab");
                UnityEngine.Object.DestroyImmediate(source);
                Assert.That(asset, Is.Not.Null, "the twin prefab asset exists");

                var first = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                var second = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                first.name = "AMUSE Probe Twin";
                second.name = "AMUSE Probe Twin";
                Assert.That(
                    first.scene == second.scene,
                    Is.True, "both twins must live in the owned scene");
                scope.SaveOwnedScene();

                firstKey = PlayModeBuildStore.ComputeAvatarKey(first.transform);
                secondKey = PlayModeBuildStore.ComputeAvatarKey(second.transform);
                Assert.That(firstKey, Is.Not.Empty, "a saved root has a proved key");
                Assert.That(secondKey, Is.Not.Empty, "a saved root has a proved key");
                Assert.That(
                    secondKey, Is.Not.EqualTo(firstKey),
                    "same-prefab twins must not share one record key");

                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = firstKey,
                    policyFingerprint = "fingerprint_twins",
                    avatarRows = new[] { SentenceRow("First result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = secondKey,
                    policyFingerprint = "fingerprint_twins",
                    avatarRows = new[] { SentenceRow("Second result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                Assert.That(
                    PlayModeBuildStore.TryLoad(firstKey, "fingerprint_twins")
                        .avatarRows[0].sentence,
                    Is.EqualTo("First result."));
                Assert.That(
                    PlayModeBuildStore.TryLoad(secondKey, "fingerprint_twins")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Second result."));
            }
            finally
            {
                if (firstKey != "") PlayModeBuildStore.Clear(firstKey);
                if (secondKey != "") PlayModeBuildStore.Clear(secondKey);
                scope.TearDown();
            }
        }

        [Test]
        public void DuplicateSceneNamesKeepTheirOwnRows()
        {
            var scopeA = new TestTransientScope("Assets/AmuseTests_PlayOwnershipA");
            var scopeB = new TestTransientScope("Assets/AmuseTests_PlayOwnershipB");
            var firstKey = "";
            var secondKey = "";
            try
            {
                scopeA.EnsureTempFolder();
                scopeB.EnsureTempFolder();
                // both copies carry the same scene file name in different
                // folders, so the scene names match while the identities do not
                var first = scopeA.CreateSavedSceneRoot("AMUSE Probe Dup");
                var second = scopeB.CreateSavedSceneRoot("AMUSE Probe Dup");
                first.AddComponent<AmuseAvatarOptimizer>();
                second.AddComponent<AmuseAvatarOptimizer>();
                scopeA.SaveOwnedScene();
                scopeB.SaveOwnedScene();

                firstKey = PlayModeBuildStore.ComputeAvatarKey(first.transform);
                secondKey = PlayModeBuildStore.ComputeAvatarKey(second.transform);
                Assert.That(firstKey, Is.Not.Empty, "a saved root has a proved key");
                Assert.That(secondKey, Is.Not.Empty, "a saved root has a proved key");
                Assert.That(
                    secondKey, Is.Not.EqualTo(firstKey),
                    "duplicate scene names must not share one record key");

                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = firstKey,
                    policyFingerprint = "fingerprint_duplicate",
                    avatarRows = new[] { SentenceRow("First result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = secondKey,
                    policyFingerprint = "fingerprint_duplicate",
                    avatarRows = new[] { SentenceRow("Second result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                Assert.That(
                    PlayModeBuildStore.TryLoad(firstKey, "fingerprint_duplicate")
                        .avatarRows[0].sentence,
                    Is.EqualTo("First result."));
                Assert.That(
                    PlayModeBuildStore.TryLoad(secondKey, "fingerprint_duplicate")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Second result."));
            }
            finally
            {
                if (firstKey != "") PlayModeBuildStore.Clear(firstKey);
                if (secondKey != "") PlayModeBuildStore.Clear(secondKey);
                try
                {
                    scopeA.TearDown();
                }
                finally
                {
                    scopeB.TearDown();
                }
            }
        }

        [Test]
        public void NestedSavedRootKeepsItsRecord()
        {
            var scope = new TestTransientScope(OwnedSceneFolder);
            var key = "";
            try
            {
                scope.EnsureTempFolder();
                var parent = scope.CreateSavedSceneRoot("AMUSE Probe Parent");
                var nested = new GameObject("AMUSE Probe Nested");
                nested.transform.SetParent(parent.transform, false);
                nested.AddComponent<AmuseAvatarOptimizer>();
                scope.SaveOwnedScene();

                key = PlayModeBuildStore.ComputeAvatarKey(nested.transform);
                Assert.That(key, Is.Not.Empty, "a nested saved root has a proved key");
                PlayModeBuildStore.Save(new PlayModeBuildRecord
                {
                    avatarKey = key,
                    policyFingerprint = "fingerprint_nested",
                    avatarRows = new[] { SentenceRow("Nested result.") },
                    rendererRows = Array.Empty<SerializedTransformationRow>()
                });
                Assert.That(
                    PlayModeBuildStore.TryLoad(key, "fingerprint_nested")
                        .avatarRows[0].sentence,
                    Is.EqualTo("Nested result."));
            }
            finally
            {
                if (key != "") PlayModeBuildStore.Clear(key);
                scope.TearDown();
            }
        }
    }
}
