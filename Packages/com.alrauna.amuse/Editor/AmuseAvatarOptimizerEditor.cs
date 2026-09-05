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

            if (AmuseBuildStatusStore.TryGet(
                    component.gameObject.GetInstanceID(), out var status))
            {
                EditorGUILayout.HelpBox(status, MessageType.None);
            }
        }
    }
}