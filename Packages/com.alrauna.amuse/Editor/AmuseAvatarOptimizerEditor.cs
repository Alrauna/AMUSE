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
        public override void OnInspectorGUI()
        {
            DrawHeader();

            var component = (AmuseAvatarOptimizer)target;
            if (AmuseComponentPlacement.IsOnHierarchyRoot(component))
            {
                EditorGUILayout.HelpBox(
                    "AMUSE will run on this avatar at upload. " +
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

            if (AmuseBuildStatusStore.TryGet(
                    component.gameObject.GetInstanceID(), out var status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }

        /// <summary>
        /// Centered product title with the package version underneath, in the
        /// style of the optimizers users already know, plus a one-click link
        /// to the issue tracker. The version is read from the installed
        /// package metadata, so a release never needs an inspector edit.
        /// </summary>
        private static void DrawHeader()
        {
            var centered = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
            };
            EditorGUILayout.LabelField("AMUSE", centered, GUILayout.Height(26));
            var version = UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                typeof(AmuseAvatarOptimizer).Assembly)?.version;
            var centeredSmall = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
            };
            EditorGUILayout.LabelField(
                string.IsNullOrEmpty(version) ? string.Empty : "v" + version,
                centeredSmall);

            if (GUILayout.Button("Report a bug"))
            {
                Application.OpenURL("https://github.com/Alrauna/AMUSE/issues");
            }

            EditorGUILayout.Space(4);
        }
    }
}