using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Alrauna.Amuse.Tests.Editor
{
    public sealed class AmuseAvatarOptimizerEditorTests
    {
        private const string MipCapPath = "_preserveTransparencyMaxMipLevel";
        private const string MinTextureSizePath =
            "_preserveTransparencyMinTextureSize";
        private const string OpaqueAlphaPath = "_minimumOpaqueAlphaPercent";

        private GameObject _root;
        private RepaintHostWindow _window;
        private UnityEditor.Editor _hostedEditor;

        /// <summary>
        /// The mesh, material, and shader objects a fixture creates
        /// outside the scene hierarchy. Destroying the root does not
        /// destroy them, so the teardown does.
        /// </summary>
        private readonly TestTransientScope _scope =
            new TestTransientScope();

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                _window.Close();
            }
            if (_hostedEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(_hostedEditor);
            }
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            // The foldout view holds one session cache slot. A clear
            // here keeps one test's rows and errored marks out of the
            // next test's view.
            Alrauna.Amuse.Editor.DryRun.DryRunCache.Clear();
            _scope.TearDown();
        }

        [Test]
        public void PolygonClampNormalizationKeepsTheInertSentinel()
        {
            // A drawn clamp of 100 is the inert sentinel. The inspector
            // must store it unchanged, because the build maps it to the
            // inert noise bound. Pushing it to opaque minus one would
            // bake an erased mask at the defaults and break the
            // byte-identity contract.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(100, 100),
                Is.EqualTo(100));
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(50, 100),
                Is.EqualTo(100));

            // Below the sentinel the inspector clamp keeps the
            // tolerated band strictly below the opaque percent.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(100, 95),
                Is.EqualTo(95));
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(50, 80),
                Is.EqualTo(49));

            // A stored opaque clamp at or below zero reads as the
            // inert 100, so the drawn per-polygon clamp keeps its
            // value instead of collapsing to zero.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(0, 50),
                Is.EqualTo(50));
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .NormalizePolygonClamp(0, 100),
                Is.EqualTo(100));
        }

        [UnityTest]
        public IEnumerator PlainRepaintDoesNotRewriteStoredMipCap()
        {
            // The stored mip cap sits outside the drawn band. One
            // repaint with no user input must leave it alone.
            var component = NewHiddenComponent();
            SetStored(component, MipCapPath, 15);
            yield return RepaintThroughHostWindow(component);

            Assert.That(Stored(component, MipCapPath),
                Is.EqualTo(15),
                "a plain repaint rewrote the stored mip cap");
        }

        [UnityTest]
        public IEnumerator OutOfBandMipCapStaysAndWarns()
        {
            var component = NewHiddenComponent();
            SetStored(component, MipCapPath, 15);
            yield return RepaintThroughHostWindow(component);

            Assert.That(Stored(component, MipCapPath),
                Is.EqualTo(15),
                "a plain repaint rewrote the stored mip cap");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .StoredValueIsOutOfBand(15, -1, 10),
                Is.True,
                "a stored mip cap of 15 must count as out-of-band");
            var message = Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OutOfBandWarning("Smallest Tested Mipmap", 15,
                    Mathf.Clamp(15, -1, 10));
            Assert.That(message, Does.Contain("15"),
                "the warning must name the stored value");
            Assert.That(message, Does.Contain("10"),
                "the warning must state the nearest legal value");
            Assert.That(message, Does.Contain(
                "only when you change this control"));
        }

        [UnityTest]
        public IEnumerator OutOfBandTextureSizeStaysAndWarns()
        {
            var component = NewHiddenComponent();
            SetStored(component, MinTextureSizePath, 100);
            yield return RepaintThroughHostWindow(component);

            Assert.That(Stored(component, MinTextureSizePath),
                Is.EqualTo(100),
                "a plain repaint rewrote the stored texture size");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .TextureSizeIsOutOfBand(100),
                Is.True,
                "a stored size of 100 must count as out-of-band");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .TextureSizeIsOutOfBand(-1),
                Is.False, "All Sizes stays legal");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .TextureSizeIsOutOfBand(128),
                Is.False, "a legal power of two stays legal");
            var message = Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OutOfBandWarning("Smallest Tested Texture", 100,
                    Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                        .NearestLegalTextureSize(100));
            Assert.That(message, Does.Contain("100"),
                "the warning must name the stored value");
            Assert.That(message, Does.Contain("128"),
                "the warning must state the nearest legal value");
            Assert.That(message, Does.Contain(
                "only when you change this control"));
        }

        [UnityTest]
        public IEnumerator OutOfBandPercentStaysAndWarns()
        {
            var component = NewHiddenComponent();
            SetStored(component, OpaqueAlphaPath, 150);
            yield return RepaintThroughHostWindow(component);

            Assert.That(Stored(component, OpaqueAlphaPath),
                Is.EqualTo(150),
                "a plain repaint rewrote the stored percent");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .StoredValueIsOutOfBand(150, 0, 100),
                Is.True,
                "a stored percent of 150 must count as out-of-band");
            var message = Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OutOfBandWarning("Alpha Upper Clamp to Convert (Per Texture)",
                    150, 100);
            Assert.That(message, Does.Contain("150"),
                "the warning must name the stored value");
            Assert.That(message, Does.Contain("100"),
                "the warning must state the nearest legal value");
            Assert.That(message, Does.Contain(
                "only when you change this control"));
        }

        [UnityTest]
        public IEnumerator MaterialTransformationsFoldoutPaintsRows()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var dryRun = ((Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)_hostedEditor).DryRun;
            Assert.That(dryRun, Is.Not.Null, "dry run must be instantiated");
            Assert.That(dryRun.RendererRows.Any(r => r.Kind == MaterialTransformationRowKind.Slot),
                Is.True, "must produce at least one slot row");
        }

        [UnityTest]
        public IEnumerator MissingOrNullRendererDrawsSafelyDuringRepaint()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            // Destroy the renderer component so dynamic resolution yields null.
            UnityEngine.Object.DestroyImmediate(renderer);

            _window.Painted = false;
            _window.Repaint();
            for (var frame = 0; frame < 10 && !_window.Painted; frame++)
            {
                yield return null;
            }

            Assert.That(_window.Painted, Is.True,
                "the window must paint safely when a renderer is missing");
        }

        [Test]
        public void DrawTransformationRowAcceptsAvatarRoot()
        {
            var method = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                .GetMethod("DrawTransformationRow", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "DrawTransformationRow must exist");
            var parameters = method.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2),
                "DrawTransformationRow must accept two parameters");
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(MaterialTransformationRow)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(Transform)));
        }

        [Test]
        public void DrawTransformationRowRendersDeferredSlotRow()
        {
            var row = Alrauna.Amuse.Editor.DryRun.MaterialTransformationRow.Slot(
                "Body",
                "UnityEngine.SkinnedMeshRenderer",
                0,
                0,
                100,
                Alrauna.Amuse.Editor.DryRun.RowText.DeferredSlotSentence(100));
            Assert.That(row.Sentence, Does.Contain("Deferred:"));
            Assert.That(row.Sentence, Does.Contain("100"));
        }

        [Test]
        public void LazyLoadingWithholdsRowsUntilDone()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            var rowsDrawn = new List<MaterialTransformationRow>();
            var helpBoxesDrawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnTransformationRowDrawnForTest =
                (row, avatarRoot) => rowsDrawn.Add(row);
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest =
                (msg, type) => helpBoxesDrawn.Add(msg);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());

                Assert.That(
                    helpBoxesDrawn.Any(msg => msg.StartsWith("AMUSE computes material transformations")),
                    Is.True,
                    "computing status box must be drawn while dry run is not complete");
                Assert.That(
                    rowsDrawn.Count,
                    Is.EqualTo(0),
                    "transformation rows must be withheld while dry run is not complete");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnTransformationRowDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest = null;
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void PlayModeRecordDisplaysBannerAndBypassesDryRun()
        {
            var component = NewHiddenComponent();
            var avatarKey = PlayModeBuildStore.ComputeAvatarKey(component.transform);
            var fingerprint = PlayModeBuildStore.ComputePolicyFingerprint(
                component, EditorUserBuildSettings.activeBuildTarget);

            var record = new PlayModeBuildRecord
            {
                avatarKey = avatarKey,
                policyFingerprint = fingerprint,
                avatarRows = new SerializedTransformationRow[]
                {
                    new SerializedTransformationRow
                    {
                        kind = (int)MaterialTransformationRowKind.Note,
                        sentence = "Play mode note row."
                    }
                },
                rendererRows = new SerializedTransformationRow[0]
            };
            PlayModeBuildStore.Save(record);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            var helpBoxesDrawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest =
                (msg, type) => helpBoxesDrawn.Add(msg);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());

                Assert.That(
                    helpBoxesDrawn.Contains(
                        "This view shows accurate results from the Play mode build."),
                    Is.True,
                    "play mode accuracy banner must be drawn");
                Assert.That(
                    editor.DryRun,
                    Is.Null,
                    "dry run must be bypassed when a valid play mode record exists");
            }
            finally
            {
                PlayModeBuildStore.Clear(avatarKey);
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest = null;
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void TheParseCacheInvalidatesOnRecordChangeButNotOnFingerprintAlone()
        {
            var rawRecord = JsonUtility.ToJson(new PlayModeBuildRecord
            {
                avatarKey = "cache-probe",
                policyFingerprint = "fp-one",
                avatarRows = new[]
                {
                    new SerializedTransformationRow
                    {
                        kind = (int)MaterialTransformationRowKind.Note,
                        sentence = "cache probe note."
                    }
                },
                rendererRows = new SerializedTransformationRow[0]
            });
            var otherRecord = JsonUtility.ToJson(new PlayModeBuildRecord
            {
                avatarKey = "cache-probe",
                policyFingerprint = "fp-two",
                avatarRows = new SerializedTransformationRow[0],
                rendererRows = new SerializedTransformationRow[0]
            });

            var rows1 = PlayModeBuildStore.ParseCached(rawRecord);
            var rows2 = PlayModeBuildStore.ParseCached(rawRecord);
            Assert.That(rows2, Is.SameAs(rows1), "the same record string reuses the parse");
            var rows3 = PlayModeBuildStore.ParseCached(otherRecord);
            Assert.That(rows3, Is.Not.SameAs(rows1), "a changed record string reparses");
        }

        [Test]
        public void MissingRendererDrawsTextLabelFallback()
        {
            var row = MaterialTransformationRow.Slot(
                "Armature/MissingRenderer",
                "UnityEngine.SkinnedMeshRenderer",
                0,
                0,
                100,
                "Deferred: 0 out of 100 polygons may be moved",
                "Assets/MissingMaterial.mat");

            var dummyRoot = new GameObject("DummyAvatarRoot").transform;
            _scope.Track(dummyRoot.gameObject);

            string drawnLabel = null;
            GUIStyle drawnStyle = null;
            bool objectFieldDrawn = false;

            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnRendererLabelDrawnForTest =
                (label, style) =>
                {
                    drawnLabel = label;
                    drawnStyle = style;
                };
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnRendererObjectFieldDrawnForTest =
                (obj, type, allowScene) =>
                {
                    objectFieldDrawn = true;
                };

            try
            {
                var method = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor).GetMethod(
                    "DrawTransformationRow", BindingFlags.NonPublic | BindingFlags.Static);
                RenderInHostWindow(() =>
                {
                    method.Invoke(null, new object[] { row, dummyRoot });
                });

                Assert.That(
                    drawnLabel,
                    Is.EqualTo("Armature/MissingRenderer"),
                    "missing renderer must draw text label with RendererPath");
                Assert.That(
                    drawnStyle,
                    Is.EqualTo(EditorStyles.boldLabel),
                    "text label fallback must use bold label style");
                Assert.That(
                    objectFieldDrawn,
                    Is.False,
                    "missing renderer must not draw empty renderer ObjectField");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnRendererLabelDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnRendererObjectFieldDrawnForTest = null;
            }
        }

        [Test]
        public void DrawTransformationRowFallsBackToSourceMaterialPathWhenSlotMaterialNull()
        {
            var go = new GameObject("TestAvatar");
            var child = new GameObject("RendererObj");
            child.transform.SetParent(go.transform, false);
            var mr = child.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[1]; // slot 0 is null

            const string fallbackMatPath = "Assets/AmuseTests_FallbackMat.mat";
            var fallbackMat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(fallbackMat, fallbackMatPath);
            fallbackMat = AssetDatabase.LoadAssetAtPath<Material>(fallbackMatPath);

            UnityEngine.Object drawnMaterial = null;
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnMaterialObjectFieldDrawnForTest =
                (obj, type, allowScene) => drawnMaterial = obj;

            try
            {
                var row = MaterialTransformationRow.Slot(
                    "RendererObj", typeof(MeshRenderer).FullName,
                    0, 10, 10, "Splittable", fallbackMatPath);

                var drawMethod = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                    .GetMethod("DrawTransformationRow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                RenderInHostWindow(() =>
                {
                    drawMethod.Invoke(null, new object[] { row, go.transform });
                });

                Assert.That(drawnMaterial, Is.SameAs(fallbackMat));
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnMaterialObjectFieldDrawnForTest = null;
                AssetDatabase.DeleteAsset(fallbackMatPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void OnEditorUpdateStepsDryRunUntilDone()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());
                Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");

                var repainted = false;
                editor.HostRepaintOverride = () => repainted = true;

                for (var guard = 0; guard < 200 && !editor.DryRun.Done; guard++)
                {
                    editor.OnEditorUpdate();
                    if (!editor.DryRun.Done)
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }

                Assert.That(editor.DryRun.Done, Is.True, "editor update must advance dry run to completion");
                Assert.That(repainted, Is.True, "completion must request repaint");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void CancelledDryRunDisplaysCancelledMessageAndComputeButton()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            var helpBoxesDrawn = new List<string>();
            var buttonsDrawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest =
                (msg, type) => helpBoxesDrawn.Add(msg);
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnButtonDrawnForTest =
                name => buttonsDrawn.Add(name);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());
                Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");
                Assert.That(buttonsDrawn.Contains("Cancel"), Is.True, "cancel button must be drawn while computing");

                editor.DryRun.Cancel();
                Assert.That(editor.DryRun.IsCancelled, Is.True, "dry run must be cancelled");

                helpBoxesDrawn.Clear();
                buttonsDrawn.Clear();

                RenderInHostWindow(() => editor.OnInspectorGUI());

                const string cancelledSentence =
                    "AMUSE material transformation analysis was cancelled.";
                Assert.That(
                    helpBoxesDrawn.Contains(cancelledSentence),
                    Is.True,
                    "cancelled status box must be drawn when dry run is cancelled");
                Assert.That(
                    buttonsDrawn.Contains("Compute"),
                    Is.True,
                    "compute button must be drawn when dry run is cancelled");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnButtonDrawnForTest = null;
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void ClickingComputeResetsCancelledDryRunAndBeginsFreshRun()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());
                var oldDryRun = editor.DryRun;
                Assert.That(oldDryRun, Is.Not.Null, "initial dry run must be created");

                oldDryRun.Cancel();
                Assert.That(oldDryRun.IsCancelled, Is.True, "dry run must be cancelled");

                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.ButtonClickOverrideForTest =
                    btn => btn == "Compute";

                RenderInHostWindow(() => editor.OnInspectorGUI());

                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.ButtonClickOverrideForTest = null;

                RenderInHostWindow(() => editor.OnInspectorGUI());

                Assert.That(editor.DryRun, Is.Not.Null, "a fresh dry run must be created");
                Assert.That(editor.DryRun, Is.Not.SameAs(oldDryRun), "the dry run must be a new instance");
                Assert.That(editor.DryRun.IsCancelled, Is.False, "fresh dry run must not be cancelled");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.ButtonClickOverrideForTest = null;
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void OnEditorUpdateCallsAsynchronousStepWithoutThrowing()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());
                Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");

                Assert.DoesNotThrow(() => editor.OnEditorUpdate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void LifecycleEventsCancelAndNullDryRun()
        {
            var component = NewHiddenComponent();
            var root = component.gameObject;
            var renderer = root.AddComponent<MeshRenderer>();
            var filter = root.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            mesh.triangles = new int[] { 0, 1, 2 };
            _scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            _scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);
            editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());
                var dryRun1 = editor.DryRun;
                Assert.That(dryRun1, Is.Not.Null, "dry run must be created");

                editor.OnBeforeAssemblyReload();
                Assert.That(editor.DryRun, Is.Null, "assembly reload must null dry run");
                Assert.That(dryRun1.IsCancelled, Is.True, "assembly reload must cancel previous dry run");

                RenderInHostWindow(() => editor.OnInspectorGUI());
                var dryRun2 = editor.DryRun;
                Assert.That(dryRun2, Is.Not.Null, "fresh dry run must be created");

                editor.OnPlayModeStateChanged(PlayModeStateChange.ExitingEditMode);
                Assert.That(editor.DryRun, Is.Null, "exiting edit mode must null dry run");
                Assert.That(dryRun2.IsCancelled, Is.True, "exiting edit mode must cancel previous dry run");

                RenderInHostWindow(() => editor.OnInspectorGUI());
                var dryRun3 = editor.DryRun;
                Assert.That(dryRun3, Is.Not.Null, "fresh dry run must be created");

                editor.OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
                Assert.That(editor.DryRun, Is.SameAs(dryRun3), "other play mode states must leave dry run intact");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [UnityTest]
        public IEnumerator SeparatorOffPaintsBannerOnly()
        {
            // The separator switch off stops the run at the banner,
            // so the foldout must show that banner row and no
            // renderer row, after one repaint sequence.
            var component = NewHiddenComponent();
            SetSwitch(component, "_alphaSeparatorEnabled", false);
            yield return RepaintThroughHostWindow(
                component, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            var rows = editor.DryRun.AvatarRows.Concat(editor.DryRun.RendererRows);
            var separatorSentence = Alrauna.Amuse.Editor.DryRun.RowText
                .BannerSentence(
                    Alrauna.Amuse.Editor.DryRun.BannerKind.SeparatorOff);
            Assert.That(
                rows.Any(row => row.Sentence == separatorSentence),
                Is.True,
                "the foldout exposed no separator-off banner row");
            Assert.That(
                rows.Any(
                    row => row.Kind == Alrauna.Amuse.Editor.DryRun
                        .MaterialTransformationRowKind.Renderer),
                Is.False,
                "the separator-off view computed a renderer row");
        }

        /// <summary>
        /// A hidden GameObject holds the component, so the fixture
        /// never touches the scene the user keeps open. The fixture
        /// reveals the advanced settings, because the alpha policy
        /// controls only draw in that revealed state.
        /// </summary>
        private Alrauna.Amuse.Runtime.AmuseAvatarOptimizer
            NewHiddenComponent()
        {
            _root = new GameObject("AMUSE repaint host fixture");
            _root.hideFlags = HideFlags.HideAndDontSave;
            var component = _root.AddComponent<
                Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var reveal = new SerializedObject(component);
            reveal.FindProperty("_advancedSettingsRevealed")
                .boolValue = true;
            reveal.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        private static void SetStored(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer component,
            string propertyPath, int value)
        {
            var so = new SerializedObject(component);
            so.FindProperty(propertyPath).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Stored(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer component,
            string propertyPath)
        {
            var so = new SerializedObject(component);
            return so.FindProperty(propertyPath).intValue;
        }

        private static void SetSwitch(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer component,
            string propertyPath, bool value)
        {
            var so = new SerializedObject(component);
            so.FindProperty(propertyPath).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The completion probe the repaint host window calls after
        /// every painted pass. The run field starts null before the
        /// foldout drive creates it, so null reads as not done.
        /// </summary>
        private bool DryRunFinished()
        {
            var run = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                .GetField("_dryRun",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(_hostedEditor);
            return run != null
                && ((Alrauna.Amuse.Editor.DryRun.AlphaSeparationDryRun)run)
                    .Done;
        }

        /// <summary>
        /// One forced repaint through a real GUI host. A plain method
        /// call cannot run IMGUI code, because no GUI view exists
        /// outside OnGUI. The window opens its own editor instance
        /// with the settings foldouts open, so the alpha policy
        /// controls really draw. One painted pass is the repaint the
        /// assertions measure. A completion probe keeps the window
        /// repainting across passes until it returns true, which the
        /// foldout tests need, because their run advances one
        /// renderer per repaint.
        /// </summary>
        private IEnumerator RepaintThroughHostWindow(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer component,
            Func<bool> completionProbe = null,
            bool openTransformations = false)
        {
            _hostedEditor = UnityEditor.Editor.CreateEditor(component);
            var editorType =
                typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_settingsOpen",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_hostedEditor, true);
            editorType.GetField("_alphaSeparatorOpen",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_hostedEditor, true);
            if (openTransformations)
            {
                editorType.GetField("_materialTransformationsOpen",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(_hostedEditor, true);
                editorType.GetField("_alphaSeparationTransformationsOpen",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(_hostedEditor, true);
            }

            _window = EditorWindow.GetWindow<RepaintHostWindow>(
                true, "AMUSE repaint host", false);
            _window.HostedEditor = _hostedEditor;
            _window.CompletionProbe = completionProbe ?? (() => true);
            ((Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)_hostedEditor)
                .HostRepaintOverride = () => _window.Repaint();
            _window.Repaint();
            for (var frame = 0;
                 frame < 60 && !_window.Painted;
                 frame++)
            {
                yield return null;
            }
            Assert.That(_window.Painted, Is.True,
                "the repaint host window never painted");
        }

        /// <summary>
        /// A tiny utility window whose OnGUI draws the hosted editor
        /// and marks the pass once the completion probe returns true.
        /// It exists so the tests can force an honest repaint without
        /// touching the user's inspector. The default probe returns
        /// true at once, so the old tests keep their single-pass
        /// behavior, and a longer probe keeps calling and repainting
        /// until the hosted run reports done.
        /// </summary>
        private void RenderInHostWindow(Action guiAction)
        {
            var window = EditorWindow.GetWindow<RepaintHostWindow>(
                true, "AMUSE repaint host", false);
            try
            {
                window.CustomOnGUI = guiAction;
                window.RepaintImmediately();
            }
            finally
            {
                window.Close();
            }
        }

        private sealed class RepaintHostWindow : EditorWindow
        {
            internal UnityEditor.Editor HostedEditor;
            internal Func<bool> CompletionProbe = () => true;
            internal Action CustomOnGUI;
            internal bool Painted;

            private void OnGUI()
            {
                if (CustomOnGUI != null)
                {
                    CustomOnGUI();
                    Painted = true;
                    return;
                }

                if (HostedEditor == null)
                {
                    return;
                }

                HostedEditor.OnInspectorGUI();
                if (!CompletionProbe())
                {
                    Repaint();
                    return;
                }

                Painted = true;
            }

            internal void RepaintImmediately()
            {
                var parentField = typeof(EditorWindow).GetField(
                    "m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
                var parent = parentField?.GetValue(this);
                var method = parent?.GetType().GetMethod(
                    "RepaintImmediately",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                method?.Invoke(parent, null);
            }
        }
    }
}
