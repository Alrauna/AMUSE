using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Draws a marked bool as a toggle whose checkbox sits left of its
    /// label. BeginProperty keeps the prefab override menu and the
    /// override display that a plain PropertyField provides.
    /// </summary>
    [CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
    internal sealed class ToggleLeftDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.ToggleLeft(position, label,
                property.boolValue);
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = value;
            }

            EditorGUI.EndProperty();
        }
    }
}
