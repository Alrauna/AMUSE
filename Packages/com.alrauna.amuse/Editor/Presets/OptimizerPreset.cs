using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// One parsed preset file. Immutable. The build never reads this
    /// type. The inspector compares it against the live component and
    /// applies it through the serialized object.
    /// </summary>
    internal sealed class OptimizerPreset
    {
        internal string Name { get; }
        internal string Description { get; }
        internal bool AlphaSeparatorEnabled { get; }
        internal int PreserveTransparencyMaxMipLevel { get; }
        internal int CutoutPreserveTransparencyMaxMipLevel { get; }
        internal int PreserveTransparencyMinTextureSize { get; }
        internal int CutoutPreserveTransparencyMinTextureSize { get; }
        internal int MinimumOpaqueCoveragePercent { get; }
        internal int CutoutMinimumOpaqueCoveragePercent { get; }
        internal int MinimumOpaqueCoverageToPreservePercent { get; }
        internal int CutoutMinimumOpaqueCoverageToPreservePercent { get; }
        internal int MinimumOpaqueAlphaPercent { get; }
        internal int PolygonAlphaUpperClampPercent { get; }
        internal int PolygonMinimumOpaqueCoveragePercent { get; }
        internal bool AllowDepthTestChange { get; }
        internal bool IgnoreOutOfRangeMaterialSlots { get; }
        internal bool AcceptDesktopAnisotropyDifferences { get; }
        internal bool AcceptMobileAnisotropyDifferences { get; }

        internal OptimizerPreset(
            string name,
            string description,
            bool alphaSeparatorEnabled,
            int preserveTransparencyMaxMipLevel,
            int cutoutPreserveTransparencyMaxMipLevel,
            int preserveTransparencyMinTextureSize,
            int cutoutPreserveTransparencyMinTextureSize,
            int minimumOpaqueCoveragePercent,
            int cutoutMinimumOpaqueCoveragePercent,
            int minimumOpaqueCoverageToPreservePercent,
            int cutoutMinimumOpaqueCoverageToPreservePercent,
            int minimumOpaqueAlphaPercent,
            int polygonAlphaUpperClampPercent,
            int polygonMinimumOpaqueCoveragePercent,
            bool allowDepthTestChange,
            bool ignoreOutOfRangeMaterialSlots,
            bool acceptDesktopAnisotropyDifferences,
            bool acceptMobileAnisotropyDifferences)
        {
            Name = name;
            Description = description;
            AlphaSeparatorEnabled = alphaSeparatorEnabled;
            PreserveTransparencyMaxMipLevel = preserveTransparencyMaxMipLevel;
            CutoutPreserveTransparencyMaxMipLevel =
                cutoutPreserveTransparencyMaxMipLevel;
            PreserveTransparencyMinTextureSize = preserveTransparencyMinTextureSize;
            CutoutPreserveTransparencyMinTextureSize =
                cutoutPreserveTransparencyMinTextureSize;
            MinimumOpaqueCoveragePercent = minimumOpaqueCoveragePercent;
            CutoutMinimumOpaqueCoveragePercent =
                cutoutMinimumOpaqueCoveragePercent;
            MinimumOpaqueCoverageToPreservePercent =
                minimumOpaqueCoverageToPreservePercent;
            CutoutMinimumOpaqueCoverageToPreservePercent =
                cutoutMinimumOpaqueCoverageToPreservePercent;
            MinimumOpaqueAlphaPercent = minimumOpaqueAlphaPercent;
            PolygonAlphaUpperClampPercent = polygonAlphaUpperClampPercent;
            PolygonMinimumOpaqueCoveragePercent = polygonMinimumOpaqueCoveragePercent;
            AllowDepthTestChange = allowDepthTestChange;
            IgnoreOutOfRangeMaterialSlots = ignoreOutOfRangeMaterialSlots;
            AcceptDesktopAnisotropyDifferences =
                acceptDesktopAnisotropyDifferences;
            AcceptMobileAnisotropyDifferences =
                acceptMobileAnisotropyDifferences;
        }

        /// <summary>
        /// True when the component equals this preset on all sixteen
        /// mapped fields. The pressed state is computed from this
        /// comparison and never stored, so a manual edit of one value
        /// leaves every button unpressed.
        /// </summary>
        internal bool Matches(AmuseAvatarOptimizer component)
        {
            return component.AlphaSeparatorEnabled == AlphaSeparatorEnabled
                && component.PreserveTransparencyMaxMipLevel
                    == PreserveTransparencyMaxMipLevel
                && component.CutoutPreserveTransparencyMaxMipLevel
                    == CutoutPreserveTransparencyMaxMipLevel
                && component.PreserveTransparencyMinTextureSize
                    == PreserveTransparencyMinTextureSize
                && component.CutoutPreserveTransparencyMinTextureSize
                    == CutoutPreserveTransparencyMinTextureSize
                && component.MinimumOpaqueCoveragePercent
                    == MinimumOpaqueCoveragePercent
                && component.CutoutMinimumOpaqueCoveragePercent
                    == CutoutMinimumOpaqueCoveragePercent
                && component.MinimumOpaqueCoverageToPreservePercent
                    == MinimumOpaqueCoverageToPreservePercent
                && component.CutoutMinimumOpaqueCoverageToPreservePercent
                    == CutoutMinimumOpaqueCoverageToPreservePercent
                && component.MinimumOpaqueAlphaPercent
                    == MinimumOpaqueAlphaPercent
                && component.PolygonAlphaUpperClampPercent
                    == PolygonAlphaUpperClampPercent
                && component.PolygonMinimumOpaqueCoveragePercent
                    == PolygonMinimumOpaqueCoveragePercent
                && component.AllowDepthTestChange == AllowDepthTestChange
                && component.IgnoreOutOfRangeMaterialSlots
                    == IgnoreOutOfRangeMaterialSlots
                && component.AcceptDesktopAnisotropyDifferences
                    == AcceptDesktopAnisotropyDifferences
                && component.AcceptMobileAnisotropyDifferences
                    == AcceptMobileAnisotropyDifferences;
        }
    }
}
