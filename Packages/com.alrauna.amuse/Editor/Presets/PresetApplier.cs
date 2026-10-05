using UnityEditor;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Writes a preset onto the component through the serialized
    /// object, so Undo and Reset treat the change like every other
    /// control. The apply covers exactly the nine mapped fields and
    /// never the master switch or the inspector-only reveal flag.
    /// </summary>
    internal static class PresetApplier
    {
        internal static void Apply(
            OptimizerPreset preset,
            SerializedObject serializedObject)
        {
            serializedObject.FindProperty("_alphaSeparatorEnabled")
                .boolValue = preset.AlphaSeparatorEnabled;
            serializedObject.FindProperty(
                    "_preserveTransparencyMaxMipLevel")
                .intValue = preset.PreserveTransparencyMaxMipLevel;
            serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize")
                .intValue = preset.PreserveTransparencyMinTextureSize;
            serializedObject.FindProperty("_minimumOpaqueCoveragePercent")
                .intValue = preset.MinimumOpaqueCoveragePercent;
            serializedObject.FindProperty("_minimumOpaqueAlphaPercent")
                .intValue = preset.MinimumOpaqueAlphaPercent;
            serializedObject.FindProperty(
                    "_polygonAlphaUpperClampPercent")
                .intValue = preset.PolygonAlphaUpperClampPercent;
            serializedObject.FindProperty(
                    "_polygonMinimumOpaqueCoveragePercent")
                .intValue = preset.PolygonMinimumOpaqueCoveragePercent;
            serializedObject.FindProperty("_allowDepthTestChange")
                .boolValue = preset.AllowDepthTestChange;
            serializedObject.FindProperty("_ignoreOutOfRangeMaterialSlots")
                .boolValue = preset.IgnoreOutOfRangeMaterialSlots;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
