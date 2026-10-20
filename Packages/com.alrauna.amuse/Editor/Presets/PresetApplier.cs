using UnityEditor;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// Writes a preset onto the component through the serialized
    /// object, so Undo and Reset treat the change like every other
    /// control. The apply covers exactly the eighteen mapped fields and
    /// never the master switch or the inspector-only reveal flag.
    /// Applying a preset replaces both acceptance settings, so no
    /// earlier acceptance survives unless the selected preset grants it.
    /// </summary>
    internal static class PresetApplier
    {
        internal static void Apply(
            OptimizerPreset preset,
            SerializedObject serializedObject)
        {
            serializedObject.FindProperty("_alphaSeparatorEnabled")
                .boolValue = preset.AlphaSeparatorEnabled;
            serializedObject.FindProperty("_cutoutSeparatorEnabled")
                .boolValue = preset.CutoutSeparatorEnabled;
            serializedObject.FindProperty("_transparentSeparatorEnabled")
                .boolValue = preset.TransparentSeparatorEnabled;
            serializedObject.FindProperty(
                    "_preserveTransparencyMaxMipLevel")
                .intValue = preset.PreserveTransparencyMaxMipLevel;
            serializedObject.FindProperty(
                    "_cutoutPreserveTransparencyMaxMipLevel")
                .intValue = preset.CutoutPreserveTransparencyMaxMipLevel;
            serializedObject.FindProperty(
                    "_preserveTransparencyMinTextureSize")
                .intValue = preset.PreserveTransparencyMinTextureSize;
            serializedObject.FindProperty(
                    "_cutoutPreserveTransparencyMinTextureSize")
                .intValue = preset.CutoutPreserveTransparencyMinTextureSize;
            serializedObject.FindProperty("_minimumOpaqueCoveragePercent")
                .intValue = preset.MinimumOpaqueCoveragePercent;
            serializedObject.FindProperty(
                    "_cutoutMinimumOpaqueCoveragePercent")
                .intValue = preset.CutoutMinimumOpaqueCoveragePercent;
            serializedObject.FindProperty(
                    "_minimumOpaqueCoverageToPreservePercent")
                .intValue = preset.MinimumOpaqueCoverageToPreservePercent;
            serializedObject.FindProperty(
                    "_cutoutMinimumOpaqueCoverageToPreservePercent")
                .intValue =
                preset.CutoutMinimumOpaqueCoverageToPreservePercent;
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
            serializedObject.FindProperty(
                    "_acceptDesktopAnisotropyDifferences")
                .boolValue = preset.AcceptDesktopAnisotropyDifferences;
            serializedObject.FindProperty(
                    "_acceptMobileAnisotropyDifferences")
                .boolValue = preset.AcceptMobileAnisotropyDifferences;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
