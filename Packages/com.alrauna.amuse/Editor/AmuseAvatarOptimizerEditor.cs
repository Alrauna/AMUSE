using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Editor.Build;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Inspector for AmuseAvatarOptimizer. Shows placement guidance. The last
    /// build status arrives with the report channel slice.
    /// </summary>
    [CustomEditor(typeof(AmuseAvatarOptimizer))]
    public sealed class AmuseAvatarOptimizerEditor : UnityEditor.Editor
    {
        private bool _advancedOpen;

        public override void OnInspectorGUI()
        {
            DrawHeader();

            var component = (AmuseAvatarOptimizer)target;
            if (AmuseComponentPlacement.IsOnHierarchyRoot(component))
            {
                EditorGUILayout.HelpBox(
                    "AMUSE will run when you upload this avatar and when " +
                    "you enter Play mode. " +
                    "It moves proven opaque parts of transparent materials onto opaque copies. " +
                    "Anything it cannot prove stays unchanged and gets reported.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "This component must sit on the root object of the avatar. " +
                    "Move it to the top object. " +
                    "The optimizer does not run while the component sits on a child.",
                    MessageType.Error);
            }

            serializedObject.Update();
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_amuseDisabled"),
                new GUIContent("Disable AMUSE",
                    "Treats this component as absent: nothing runs on build, " +
                    "in Play mode, or anywhere else, and nothing is reported."));
            serializedObject.ApplyModifiedProperties();

            DrawAdvancedSettings();

            if (AmuseBuildStatusStore.TryGet(
                    component.gameObject.GetInstanceID(), out var status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }


        /// <summary>
        /// The Advanced Settings foldout. It holds the proof-scope policy
        /// knobs: settings most users never touch, and that change what
        /// AMUSE treats as proven, never what it mutates. Every control
        /// carries its own plain-English tool tip, because a wrong value
        /// here changes conversion decisions, not safety.
        /// </summary>
        private void DrawAdvancedSettings()
        {
            _advancedOpen = EditorGUILayout.Foldout(
                _advancedOpen, "Advanced Settings", EditorStyles.foldoutHeader);
            if (!_advancedOpen)
            {
                return;
            }

            var property = serializedObject.FindProperty(
                "_preserveTransparencyMaxMipLevel");

            // Index 0 is "All Mips" (stored -1); index i maps to level i-1.
            var options = new[]
            {
                "All Mips", "Mip 0", "Mip 1", "Mip 2", "Mip 3", "Mip 4",
                "Mip 5", "Mip 6", "Mip 7", "Mip 8", "Mip 9", "Mip 10",
            };
            var stored = property.intValue;
            var index = stored < 0
                ? 0
                : Mathf.Min(stored + 1, options.Length - 1);
            var selected = EditorGUILayout.Popup(
                new GUIContent(
                    "Preserve Transparency Maximum Mipmap",
                    "A mipmap level is a smaller copy of the texture. " +
                    "Each level is half the size of the level before it. " +
                    "The GPU uses the small levels when the avatar is far " +
                    "away or small on screen.\n\n" +
                    "A transparent texture often fades at small levels. " +
                    "Averaging mixes transparent texels into texels that " +
                    "were solid. A texel is one pixel of a texture. " +
                    "AMUSE checks texture levels before it moves a " +
                    "triangle onto an opaque material. One faded texel in " +
                    "a checked level stops the move.\n\n" +
                    "This setting sets the largest level AMUSE checks. " +
                    "AMUSE ignores every level above it. Mip 4 fits most " +
                    "viewing distances and keeps most conversions. " +
                    "Select All Mips to check every level. This is the " +
                    "safest choice.\n\n" +
                    "Select a smaller level when a part looks solid at " +
                    "long distance where it should show through. " +
                    "Select a larger level when AMUSE moves too few " +
                    "triangles."),
                index,
                options);
            property.intValue = selected == 0 ? -1 : selected - 1;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots"),
                new GUIContent(
                    "Ignore Out-of-Range Material Slots",
                    "Some animation files animate material slots that do not exist on this mesh. " +
                    "By default, AMUSE refuses to optimize this mesh to prevent visual errors. " +
                    "Turn this setting on to ignore these extra slot animations and optimize the valid slots.\n\n" +
                    "Risk: If an animation later changes this mesh to have more material slots, " +
                    "or if another tool relies on the untouched slot count, visual errors can occur."));
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Centered product title with the package version to its right, in
        /// the style of the optimizers users already know, plus a one-click
        /// link to the issue tracker. The title is centered across the whole
        /// row, so its position never depends on the version width. The
        /// version is read from the installed package metadata, so a release
        /// never needs an inspector edit.
        /// </summary>
        private static void DrawHeader()
        {
            var row = EditorGUILayout.GetControlRect(GUILayout.Height(26));
            var centered = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
            GUI.Label(row, "AMUSE", centered);

            var version = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AmuseAvatarOptimizer).Assembly)?.version;
            if (!string.IsNullOrEmpty(version))
            {
                var right = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                };
                GUI.Label(row, "v" + version, right);
            }

            if (GUILayout.Button("Report a bug"))
            {
                Application.OpenURL("https://github.com/Alrauna/AMUSE/issues");
            }

            EditorGUILayout.Space(4);
        }
    }
}