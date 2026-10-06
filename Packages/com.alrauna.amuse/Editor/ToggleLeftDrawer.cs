using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Editor
{
    /// <summary>
    /// Draws a marked bool as a toggle whose checkbox sits left of its
    /// label. BeginProperty keeps the prefab override menu and the
    /// override display that a plain PropertyField provides. The
    /// mixed-value mark stays on, because a multi-object selection
    /// must keep the fidelity it had before this drawer existed.
    /// </summary>
    [CustomPropertyDrawer(typeof(ToggleLeftAttribute))]
    internal sealed class ToggleLeftDrawer : PropertyDrawer
    {
        public override void OnGUI(
            Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            var value = EditorGUI.ToggleLeft(position, label,
                property.boolValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.boolValue = value;
            }

            EditorGUI.EndProperty();
        }
    }
}
