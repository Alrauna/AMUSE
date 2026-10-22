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
        private const string CutoutMipCapPath =
            "_cutoutPreserveTransparencyMaxMipLevel";

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
            // A render can close the shared repaint host window through
            // RenderInHostWindow, so the truthiness read skips the
            // destroyed instance.
            if (_window)
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

        [Test]
        public void DefaultFoldoutStatesMatchSpecification()
        {
            var component = NewHiddenComponent();
            var editor = UnityEditor.Editor.CreateEditor(component);
            try
            {
                var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
                var settingsOpen = (bool)editorType.GetField("_settingsOpen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
                var alphaSeparatorOpen = (bool)editorType.GetField("_alphaSeparatorOpen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
                var materialTransformationsOpen = (bool)editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);
                var alphaSeparationTransformationsOpen = (bool)editorType.GetField("_alphaSeparationTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor);

                Assert.That(settingsOpen, Is.False, "settings foldout must not be expanded by default");
                Assert.That(alphaSeparatorOpen, Is.True, "alpha separator settings foldout must be expanded by default");
                Assert.That(materialTransformationsOpen, Is.False, "show material transformations foldout must not be expanded by default");
                Assert.That(alphaSeparationTransformationsOpen, Is.True, "alpha separation dropdown under material transformations must be expanded by default");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [Test]
        public void MaterialTransformationsInnerFoldoutHeaderIsNamedAlphaSeparator()
        {
            var component = NewHiddenComponent();
            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                UnityEditor.Editor.CreateEditor(component);
            var editorType = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);
            editorType.GetField("_materialTransformationsOpen", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(editor, true);

            var headersDrawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnFoldoutHeaderDrawnForTest =
                header => headersDrawn.Add(header);

            try
            {
                RenderInHostWindow(() => editor.OnInspectorGUI());

                Assert.That(headersDrawn, Contains.Item("Show Material Transformations"));
                Assert.That(headersDrawn, Contains.Item("Alpha Separator"));
                Assert.That(headersDrawn, Does.Not.Contain("Alpha Separation"));
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnFoldoutHeaderDrawnForTest = null;
                UnityEngine.Object.DestroyImmediate(editor);
            }
        }

        [UnityTest]
        public IEnumerator SubmenuHeadersDrawUnderTheReveal()
        {
            // The reveal is on, because the block stays hidden until
            // the Advanced Settings reveal turns on. The host window
            // opens the Settings foldout and the parent foldout.
            var component = NewHiddenComponent();
            var headersDrawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnFoldoutHeaderDrawnForTest =
                header => headersDrawn.Add(header);
            try
            {
                yield return RepaintThroughHostWindow(component);
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnFoldoutHeaderDrawnForTest = null;
            }

            Assert.That(headersDrawn, Contains.Item("Alpha Separator Settings"),
                "the parent foldout header must draw under the reveal");
            Assert.That(headersDrawn, Contains.Item("Transparent"),
                "the transparent submenu header must draw under the reveal");
            Assert.That(headersDrawn, Contains.Item("Cutout"),
                "the cutout submenu header must draw under the reveal");
        }

        /// <summary>
        /// --- Falsifier 2: an implementation that inverts the drawn value
        /// (draws the negation of the stored bool) or renames the stored
        /// field. The drawn-value assertions fail against the inversion,
        /// because stored true must draw checked. A rename fails the property
        /// lookup outright. ---
        /// </summary>
        [UnityTest]
        public IEnumerator OutOfRangeToggleDrawsTheStoredValueInTheAllowVoice()
        {
            var component = NewHiddenComponent();
            var drawn = new List<bool>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnOutOfRangeToggleDrawnForTest = value => drawn.Add(value);
            try
            {
                SetSwitch(component, "_ignoreOutOfRangeMaterialSlots", true);
                drawn.Clear();
                yield return RepaintThroughHostWindow(component);
                Assert.That(drawn, Does.Contain(true),
                    "stored true draws the Allow checkbox checked");
                Assert.That(
                    Stored(component, "_ignoreOutOfRangeMaterialSlots"),
                    Is.EqualTo(1), "a plain repaint writes nothing");

                SetSwitch(component, "_ignoreOutOfRangeMaterialSlots", false);
                drawn.Clear();
                yield return RepaintThroughHostWindow(component);
                Assert.That(drawn, Does.Contain(false),
                    "stored false draws the Allow checkbox unchecked");
                Assert.That(
                    Stored(component, "_ignoreOutOfRangeMaterialSlots"),
                    Is.EqualTo(0), "a plain repaint writes nothing");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnOutOfRangeToggleDrawnForTest = null;
            }
        }

        /// <summary>
        /// Known limit, recorded: SetSwitch applies modified properties,
        /// so the C# property and the serialized value agree in every
        /// state these tests arrange. The spec's gate-source rule (the
        /// master gate reads the serialized property, not the C#
        /// property) is enforced by transcribing the spec A3 code block
        /// verbatim. No test can discriminate the one-repaint lag
        /// without synthesizing a click through the drawn row, which
        /// these tests do not do. A second recorded fact: the host
        /// window paints several IMGUI events per repaint, so the
        /// assertions pin the distinct recorded (value, indent) tuples
        /// instead of a fire count, like the out-of-range model test
        /// pins Does.Contain.
        /// </summary>
        [UnityTest]
        public IEnumerator SubSwitchHooksFireWithStoredValuesWhenMasterSwitchIsOn()
        {
            var component = NewHiddenComponent();
            SetSwitch(component, "_alphaSeparatorEnabled", true);
            SetSwitch(component, "_cutoutSeparatorEnabled", true);
            SetSwitch(component, "_transparentSeparatorEnabled", false);
            var cutoutDrawn = new List<(bool Value, int Indent)>();
            var transparentDrawn = new List<(bool Value, int Indent)>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnCutoutSeparatorToggleDrawnForTest = value =>
                cutoutDrawn.Add((value, EditorGUI.indentLevel));
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnTransparentSeparatorToggleDrawnForTest = value =>
                transparentDrawn.Add((value, EditorGUI.indentLevel));
            try
            {
                yield return RepaintThroughHostWindow(component);

                Assert.That(cutoutDrawn, Is.Not.Empty,
                    "the cutout sub-switch hook never fired");
                Assert.That(cutoutDrawn.Distinct(),
                    Is.EqualTo(new[] { (true, 1) }),
                    "every cutout firing records the stored value, one " +
                    "indent level below the master row");
                Assert.That(transparentDrawn, Is.Not.Empty,
                    "the transparent sub-switch hook never fired");
                Assert.That(transparentDrawn.Distinct(),
                    Is.EqualTo(new[] { (false, 1) }),
                    "every transparent firing records the stored value, " +
                    "one indent level below the master row");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnCutoutSeparatorToggleDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransparentSeparatorToggleDrawnForTest = null;
            }
        }

        /// <summary>
        /// Known limit, recorded: each full GUI pass runs the whole
        /// OnInspectorGUI once, and both hooks sit outside PropertyField
        /// in the same unconditional block. Every pass therefore appends
        /// exactly one transparent and one cutout entry to the shared
        /// list. A harness change that breaks that ceiling fails the
        /// even-count assertion instead of passing silently. Each hook
        /// is invoked immediately after its own PropertyField, so hook
        /// fire order equals draw order.
        /// --- Falsifier 3: the base-commit draw order, which draws the
        /// cutout sub-switch first. Against that order the first pair
        /// assertion fails, because element 0 is cutout. An
        /// implementation that drops a hook fails the not-empty or the
        /// even-count assertion. ---
        /// </summary>
        [UnityTest]
        public IEnumerator TransparentSeparatorDrawsBeforeCutoutSeparator()
        {
            var component = NewHiddenComponent();
            SetSwitch(component, "_alphaSeparatorEnabled", true);
            SetSwitch(component, "_cutoutSeparatorEnabled", true);
            SetSwitch(component, "_transparentSeparatorEnabled", true);
            var drawn = new List<string>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnTransparentSeparatorToggleDrawnForTest = _ =>
                    drawn.Add("transparent");
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnCutoutSeparatorToggleDrawnForTest = _ =>
                    drawn.Add("cutout");
            try
            {
                yield return RepaintThroughHostWindow(component);

                Assert.That(drawn, Is.Not.Empty,
                    "no sub-switch hook ever fired");
                Assert.That(drawn.Count % 2, Is.EqualTo(0),
                    "every pass records one pair, so the count is even");
                for (var i = 0; i < drawn.Count; i += 2)
                {
                    Assert.That(drawn[i], Is.EqualTo("transparent"),
                        "the transparent sub-switch draws above the " +
                        "cutout sub-switch in every pass");
                    Assert.That(drawn[i + 1], Is.EqualTo("cutout"),
                        "the cutout sub-switch follows the transparent " +
                        "sub-switch in every pass");
                }
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransparentSeparatorToggleDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnCutoutSeparatorToggleDrawnForTest = null;
            }
        }

        [UnityTest]
        public IEnumerator SubSwitchHooksDoNotFireWhenMasterSwitchIsOff()
        {
            var component = NewHiddenComponent();
            SetSwitch(component, "_alphaSeparatorEnabled", false);
            var cutoutDrawn = new List<(bool Value, int Indent)>();
            var transparentDrawn = new List<(bool Value, int Indent)>();
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnCutoutSeparatorToggleDrawnForTest = value =>
                cutoutDrawn.Add((value, EditorGUI.indentLevel));
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnTransparentSeparatorToggleDrawnForTest = value =>
                transparentDrawn.Add((value, EditorGUI.indentLevel));
            try
            {
                yield return RepaintThroughHostWindow(component);

                Assert.That(cutoutDrawn, Is.Empty,
                    "the master switch off must hide the pair and fire " +
                    "no cutout hook");
                Assert.That(transparentDrawn, Is.Empty,
                    "the master switch off must hide the pair and fire " +
                    "no transparent hook");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnCutoutSeparatorToggleDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransparentSeparatorToggleDrawnForTest = null;
            }
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
        public IEnumerator OutOfBandCutoutMipCapStaysAndWarns()
        {
            var component = NewHiddenComponent();

            // A first round trip runs the one-time upgrade backfill,
            // so this serialized edit survives later applies.
            var primed = EditorJsonUtility.ToJson(component);
            EditorJsonUtility.FromJsonOverwrite(primed, component);

            SetStored(component, CutoutMipCapPath, 15);
            yield return RepaintThroughHostWindow(component);

            Assert.That(Stored(component, CutoutMipCapPath),
                Is.EqualTo(15),
                "a plain repaint rewrote the stored cutout mip cap");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .StoredValueIsOutOfBand(15, -1, 10),
                Is.True,
                "a stored cutout mip cap of 15 must count as out-of-band");
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
                    helpBoxesDrawn.Any(msg => msg.StartsWith("AMUSE is computing material transformations")),
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
            var scope = new TestTransientScope("Assets/AmuseTests_PlayRecordBanner");
            var avatarKey = "";
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor editor = null;
            try
            {
                scope.EnsureTempFolder();
                var root = scope.CreateSavedSceneRoot("AMUSE play record banner fixture");
                var component = root.AddComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                var reveal = new SerializedObject(component);
                reveal.FindProperty("_advancedSettingsRevealed")
                    .boolValue = true;
                reveal.ApplyModifiedPropertiesWithoutUndo();
                scope.SaveOwnedScene();

                avatarKey = PlayModeBuildStore.ComputeAvatarKey(component.transform);
                Assert.That(avatarKey, Is.Not.Empty, "a saved root has a proved key");
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
                            kind = MaterialTransformationRowKind.Note,
                            sentence = "Play mode note row."
                        }
                    },
                    rendererRows = new SerializedTransformationRow[0]
                };
                PlayModeBuildStore.Save(record);

                editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
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
                if (avatarKey != "") PlayModeBuildStore.Clear(avatarKey);
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor.OnHelpBoxDrawnForTest = null;
                if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
                scope.TearDown();
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
                        kind = MaterialTransformationRowKind.Note,
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
        public void StoredNumericRowsDrawTheirRowShapesThroughTheRealDrawer()
        {
            // The fixtures carry the wire numbers from the stored
            // record. They never name the enum. The enum names live in
            // the assertions only. This pins the stored payload
            // contract through the real reader and the real drawer.
            var rows = new List<MaterialTransformationRow>();
            foreach (var wireKind in new[] { 0, 1, 2, 3, 4 })
            {
                rows.Add(MaterialTransformationRow.FromSerialized(
                    JsonUtility.FromJson<SerializedTransformationRow>(
                        DryRun.MaterialTransformationRowTests
                            .SerializedRowJson(wireKind))));
            }

            var dummyRoot = new GameObject("NumericWireAvatarRoot").transform;
            _scope.Track(dummyRoot.gameObject);

            var drawn = new List<MaterialTransformationRow>();
            string pathLabel = null;
            GUIStyle pathStyle = null;
            var rendererFieldFired = false;
            var materialFieldFired = false;
            UnityEngine.Object drawnMaterial = null;

            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnTransformationRowDrawnForTest = (row, avatarRoot) =>
                {
                    // IMGUI paints one pass per event. Record the
                    // repaint pass only, so one drawn row maps to
                    // one record.
                    if (Event.current.type != EventType.Repaint)
                    {
                        return;
                    }
                    drawn.Add(row);
                };
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnRendererLabelDrawnForTest = (label, style) =>
                {
                    pathLabel = label;
                    pathStyle = style;
                };
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnRendererObjectFieldDrawnForTest =
                (obj, type, allowScene) => rendererFieldFired = true;
            Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                .OnMaterialObjectFieldDrawnForTest =
                (obj, type, allowScene) =>
                {
                    materialFieldFired = true;
                    drawnMaterial = obj;
                };

            try
            {
                var draw = typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                    .GetMethod(
                        "DrawTransformationRow",
                        BindingFlags.NonPublic | BindingFlags.Static);
                RenderInHostWindow(() =>
                {
                    foreach (var row in rows)
                    {
                        draw.Invoke(null, new object[] { row, dummyRoot });
                    }
                });

                Assert.That(
                    drawn.Select(row => row.Kind).ToArray(),
                    Is.EqualTo(new[]
                    {
                        MaterialTransformationRowKind.Banner,
                        MaterialTransformationRowKind.Note,
                        MaterialTransformationRowKind.Renderer,
                        MaterialTransformationRowKind.Slot,
                        MaterialTransformationRowKind.Errored
                    }),
                    "the real drawer must receive every stored numeric row " +
                    "as its typed kind");
                Assert.That(
                    pathLabel,
                    Is.EqualTo("Armature/Body"),
                    "the stored slot row must draw its renderer path label");
                Assert.That(
                    pathStyle,
                    Is.EqualTo(EditorStyles.boldLabel),
                    "a missing renderer must draw the bold path label");
                Assert.That(
                    rendererFieldFired,
                    Is.False,
                    "a missing renderer must draw no renderer object field");
                Assert.That(
                    materialFieldFired,
                    Is.True,
                    "the stored slot row must draw its material object field");
                Assert.That(
                    drawnMaterial,
                    Is.Null,
                    "an unloadable source path must draw no material");
            }
            finally
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransformationRowDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnRendererLabelDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnRendererObjectFieldDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnMaterialObjectFieldDrawnForTest = null;
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
                    row => row.Kind == MaterialTransformationRowKind
                        .Renderer),
                Is.False,
                "the separator-off view computed a renderer row");
        }

        // --- Policy validity --------------------------------------------------

        private static MeshRenderer AttachRefusedSlotRenderer(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer component,
            TestTransientScope scope)
        {
            var renderer = component.gameObject.AddComponent<MeshRenderer>();
            var filter = component.gameObject.AddComponent<MeshFilter>();
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                Vector3.zero, Vector3.up, Vector3.right
            };
            mesh.triangles = new int[] { 0, 1, 2 };
            scope.Track(mesh);
            filter.sharedMesh = mesh;
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            scope.Track(material);
            renderer.sharedMaterials = new Material[] { material };
            return renderer;
        }

        /// <summary>
        /// A policy change after a completed preview must reject the
        /// retained rows. The view must hide them and hide the
        /// Compute affordance. The known refused slot row of the
        /// unchanged fixture stands as the no-op guard.
        /// </summary>
        [UnityTest]
        public IEnumerator CompletedPreviewRejectsItsRowsAfterAPolicyChange()
        {
            var component = NewHiddenComponent();
            AttachRefusedSlotRenderer(component, _scope);

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            Assert.That(editor.DryRun, Is.Not.Null, "dry run must be instantiated");
            Assert.That(
                editor.DryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Slot),
                Is.True,
                "the unchanged fixture must paint its known slot row");

            SetSwitch(component, "_cutoutSeparatorEnabled", false);

            var drawn = new DrawnView();
            drawn.Install();
            try
            {
                _window.CompletionProbe = () => true;
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the rejected render never painted");
                Assert.That(
                    drawn.Rows.Any(
                        row => row.Kind == MaterialTransformationRowKind.Slot),
                    Is.False,
                    "a policy change must reject the completed run's rows");
                Assert.That(
                    drawn.Buttons,
                    Does.Not.Contain("Compute"),
                    "an invalidated run must hide its Compute affordance");
            }
            finally
            {
                drawn.Uninstall();
            }
        }

        /// <summary>
        /// A closed and reopened foldout must not clear the policy
        /// rejection. The rejected run keeps its rows hidden after
        /// the toggle.
        /// </summary>
        [UnityTest]
        public IEnumerator ReopeningTheFoldoutDoesNotClearThePolicyRejection()
        {
            var component = NewHiddenComponent();
            AttachRefusedSlotRenderer(component, _scope);

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            Assert.That(
                editor.DryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Slot),
                Is.True,
                "the unchanged fixture must paint its known slot row");

            SetSwitch(component, "_cutoutSeparatorEnabled", false);

            var editorType =
                typeof(Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor);

            var drawn = new DrawnView();
            drawn.Install();
            try
            {
                _window.CompletionProbe = () => true;

                // Render once while the foldout is still open. The
                // open view must latch the change and hide the old rows.
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the open render after the change never painted");
                Assert.That(
                    drawn.Rows.Any(
                        row => row.Kind == MaterialTransformationRowKind.Slot),
                    Is.False,
                    "the open view must hide the rejected rows");
                Assert.That(
                    editor.DryRun.PolicyInvalidated, Is.True,
                    "the open render must latch the rejection");

                editorType.GetField(
                        "_materialTransformationsOpen",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(editor, false);
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the closed-foldout render never painted");
                Assert.That(
                    drawn.Rows, Is.Empty,
                    "a closed foldout must paint no rows");

                editorType.GetField(
                        "_materialTransformationsOpen",
                        BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(editor, true);
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the reopened render never painted");
                Assert.That(
                    drawn.Rows.Any(
                        row => row.Kind == MaterialTransformationRowKind.Slot),
                    Is.False,
                    "reopening the foldout must not resurrect the " +
                    "rejected rows");
                Assert.That(
                    drawn.Buttons,
                    Does.Not.Contain("Compute"),
                    "the reopened view must keep the Compute affordance hidden");
                Assert.That(
                    editor.DryRun.PolicyInvalidated, Is.True,
                    "reopening the foldout must not clear the latch");
            }
            finally
            {
                drawn.Uninstall();
            }
        }

        /// <summary>
        /// The switched-off avatar stops at its banner before any
        /// compute. A policy change after that completed run must
        /// reject the retained banner row the same way it rejects
        /// result rows.
        /// </summary>
        [UnityTest]
        public IEnumerator AmuseOffPreviewRejectsItsBannerAfterAPolicyChange()
        {
            var component = NewHiddenComponent();
            SetSwitch(component, "_amuseDisabled", true);

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");
            Assert.That(
                editor.DryRun.Done, Is.True,
                "the switched-off run must complete at its banner");
            var offBanner = Alrauna.Amuse.Editor.DryRun.RowText.BannerSentence(
                Alrauna.Amuse.Editor.DryRun.BannerKind.AmuseOff);
            Assert.That(
                editor.DryRun.AvatarRows.Any(row => row.Sentence == offBanner),
                Is.True,
                "the switched-off banner must be the retained row");

            SetSwitch(component, "_cutoutSeparatorEnabled", false);

            var drawn = new DrawnView();
            drawn.Install();
            try
            {
                _window.CompletionProbe = () => true;
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the rejected render never painted");
                Assert.That(
                    drawn.Rows, Is.Empty,
                    "the stale banner must not repaint after a policy change");
                Assert.That(
                    drawn.Buttons,
                    Does.Not.Contain("Compute"),
                    "an invalidated run must hide its Compute affordance");
            }
            finally
            {
                drawn.Uninstall();
            }
        }

        /// <summary>
        /// The separator-off avatar stops at its banner before any
        /// compute. A policy change after that completed run must
        /// reject the retained banner row the same way it rejects
        /// result rows.
        /// </summary>
        [UnityTest]
        public IEnumerator SeparatorOffPreviewRejectsItsBannerAfterAPolicyChange()
        {
            var component = NewHiddenComponent();
            SetSwitch(component, "_alphaSeparatorEnabled", false);

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");
            Assert.That(
                editor.DryRun.Done, Is.True,
                "the separator-off run must complete at its banner");
            var offBanner = Alrauna.Amuse.Editor.DryRun.RowText.BannerSentence(
                Alrauna.Amuse.Editor.DryRun.BannerKind.SeparatorOff);
            Assert.That(
                editor.DryRun.AvatarRows.Any(row => row.Sentence == offBanner),
                Is.True,
                "the separator-off banner must be the retained row");

            SetSwitch(component, "_cutoutSeparatorEnabled", false);

            var drawn = new DrawnView();
            drawn.Install();
            try
            {
                _window.CompletionProbe = () => true;
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the rejected render never painted");
                Assert.That(
                    drawn.Rows, Is.Empty,
                    "the stale banner must not repaint after a policy change");
                Assert.That(
                    drawn.Buttons,
                    Does.Not.Contain("Compute"),
                    "an invalidated run must hide its Compute affordance");
            }
            finally
            {
                drawn.Uninstall();
            }
        }

        /// <summary>
        /// A component on a child stops at its placement banner before
        /// any compute. A policy change after that completed run must
        /// reject the retained banner row the same way it rejects
        /// result rows.
        /// </summary>
        [UnityTest]
        public IEnumerator ChildPlacementPreviewRejectsItsBannerAfterAPolicyChange()
        {
            var component = NewHiddenComponent();
            var child = new GameObject("misplaced fixture");
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(component.transform, false);
            var misplaced = child.AddComponent<
                Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();

            yield return RepaintThroughHostWindow(
                misplaced, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");
            Assert.That(
                editor.DryRun.Done, Is.True,
                "the misplaced run must complete at its banner");
            var placementBanner =
                Alrauna.Amuse.Editor.DryRun.RowText.BannerSentence(
                    Alrauna.Amuse.Editor.DryRun.BannerKind.RootRequired);
            Assert.That(
                editor.DryRun.AvatarRows.Any(
                    row => row.Sentence == placementBanner),
                Is.True,
                "the placement banner must be the retained row");

            SetSwitch(misplaced, "_cutoutSeparatorEnabled", false);

            var drawn = new DrawnView();
            drawn.Install();
            try
            {
                _window.CompletionProbe = () => true;
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the rejected render never painted");
                Assert.That(
                    drawn.Rows, Is.Empty,
                    "the stale banner must not repaint after a policy change");
                Assert.That(
                    drawn.Buttons,
                    Does.Not.Contain("Compute"),
                    "an invalidated run must hide its Compute affordance");
            }
            finally
            {
                drawn.Uninstall();
            }
        }

        /// <summary>
        /// A structural refusal stops at its avatar row before any
        /// compute. A policy change after that completed run must
        /// reject the retained structural row the same way it rejects
        /// result rows.
        /// </summary>
        [UnityTest]
        public IEnumerator StructuralRefusalPreviewRejectsItsRowAfterAPolicyChange()
        {
            var component = NewHiddenComponent();
            var clip = _scope.Track(new AnimationClip { name = "event-clip" });
            AnimationUtility.SetAnimationEvents(
                clip,
                new[]
                {
                    new AnimationEvent
                    {
                        time = 0f,
                        functionName = "AmuseInspectorPolicyEvent"
                    }
                });
            var controller = _scope.Track(
                new UnityEditor.Animations.AnimatorController { name = "event" });
            controller.AddLayer("L0");
            controller.layers[0].stateMachine.AddState("S0").motion = clip;
            var animator = component.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            yield return RepaintThroughHostWindow(component, DryRunFinished, true);

            var editor = (Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor)
                _hostedEditor;
            Assert.That(editor.DryRun, Is.Not.Null, "dry run must be created");
            Assert.That(
                editor.DryRun.Done, Is.True,
                "the structural run must complete at its refusal row");
            Assert.That(
                editor.DryRun.RendererRows, Is.Empty,
                "the structural refusal must stop before any renderer");
            Assert.That(
                editor.DryRun.AvatarRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Banner),
                Is.True,
                "the structural refusal must be the retained row");

            SetSwitch(component, "_cutoutSeparatorEnabled", false);

            var drawn = new DrawnView();
            drawn.Install();
            try
            {
                _window.CompletionProbe = () => true;
                _window.Painted = false;
                _window.Repaint();
                for (var frame = 0;
                     frame < 60 && !_window.Painted;
                     frame++)
                {
                    yield return null;
                }

                Assert.That(_window.Painted, Is.True,
                    "the rejected render never painted");
                Assert.That(
                    drawn.Rows, Is.Empty,
                    "the stale structural row must not repaint after " +
                    "a policy change");
                Assert.That(
                    drawn.Buttons,
                    Does.Not.Contain("Compute"),
                    "an invalidated run must hide its Compute affordance");
            }
            finally
            {
                drawn.Uninstall();
            }
        }

        /// <summary>
        /// --- Falsifier 1: an implementation that keeps the subtract rule,
        /// `ceil(widest) + 2 - indentLevel * 15`. It produced the truncation
        /// the owner reported, and it ignores the budget and the floor, so
        /// every assertion fails against it. An implementation that ignores
        /// the budget returns the text term everywhere and fails the last
        /// three. An implementation that drops the 1 pixel floor fails the
        /// third and fourth assertions with a zero or negative result. ---
        /// </summary>
        [Test]
        public void AlphaSeparatorLabelWidthBalancesTextAndControlBudget()
        {
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .AlphaSeparatorLabelWidth(200f, 60f, 500f),
                Is.EqualTo(262f),
                "a roomy row must fit the widest label plus the indent");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .AlphaSeparatorLabelWidth(200f, 60f, 200f),
                Is.EqualTo(126f),
                "a narrow row must cap the label at the control budget");
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .AlphaSeparatorLabelWidth(200f, 60f, 0f),
                Is.EqualTo(1f),
                "a starved row must keep the floor, so the width stays valid");
            // No-op guard: a negative budget must also floor, so a rewrite
            // cannot silently drop the floor term.
            Assert.That(
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .AlphaSeparatorLabelWidth(200f, 60f, -50f),
                Is.EqualTo(1f),
                "a negative budget must never produce an invalid width");
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
        /// The drawn rows and buttons one render pass collects through
        /// the existing editor hooks. Uninstall is mandatory in a
        /// finally block, so one test's draws never reach the next test.
        /// </summary>
        private sealed class DrawnView
        {
            internal readonly List<MaterialTransformationRow> Rows =
                new List<MaterialTransformationRow>();
            internal readonly List<string> Buttons = new List<string>();

            internal void Install()
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransformationRowDrawnForTest = (row, avatarRoot) =>
                        Rows.Add(row);
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnButtonDrawnForTest = name => Buttons.Add(name);
            }

            internal void Uninstall()
            {
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnTransformationRowDrawnForTest = null;
                Alrauna.Amuse.Editor.AmuseAvatarOptimizerEditor
                    .OnButtonDrawnForTest = null;
            }
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
