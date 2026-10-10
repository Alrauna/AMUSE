using System.Collections;
using System.Reflection;
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

        [TearDown]
        public void TearDown()
        {
            if (_window != null)
            {
                _window.Close();
            }
            if (_hostedEditor != null)
            {
                Object.DestroyImmediate(_hostedEditor);
            }
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
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
                .OutOfBandWarning("Alpha Upper Clamp (Per Texture)",
                    150, 100);
            Assert.That(message, Does.Contain("150"),
                "the warning must name the stored value");
            Assert.That(message, Does.Contain("100"),
                "the warning must state the nearest legal value");
            Assert.That(message, Does.Contain(
                "only when you change this control"));
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

        /// <summary>
        /// One forced repaint through a real GUI host. A plain method
        /// call cannot run IMGUI code, because no GUI view exists
        /// outside OnGUI. The window opens its own editor instance
        /// with the settings foldouts open, so the alpha policy
        /// controls really draw. One painted pass is the repaint the
        /// assertions measure.
        /// </summary>
        private IEnumerator RepaintThroughHostWindow(
            Alrauna.Amuse.Runtime.AmuseAvatarOptimizer component)
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

            _window = EditorWindow.GetWindow<RepaintHostWindow>(
                true, "AMUSE repaint host", false);
            _window.HostedEditor = _hostedEditor;
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
        /// once and marks the pass. It exists so the tests can force
        /// an honest repaint without touching the user's inspector.
        /// </summary>
        private sealed class RepaintHostWindow : EditorWindow
        {
            internal UnityEditor.Editor HostedEditor;
            internal bool Painted;

            private void OnGUI()
            {
                if (HostedEditor != null)
                {
                    HostedEditor.OnInspectorGUI();
                    Painted = true;
                }
            }
        }
    }
}
