using Alrauna.Amuse.Runtime;

namespace Alrauna.Amuse.Editor.Presets
{
    /// <summary>
    /// One parsed preset file. Immutable. The build never reads this
    /// type. The inspector compares it against the live component and
    /// applies it through the serialized object.
    /// </summary>
    internal sealed record OptimizerPreset(
        string Name,
        string Description,
        bool AlphaSeparatorEnabled,
        int PreserveTransparencyMaxMipLevel,
        int CutoutPreserveTransparencyMaxMipLevel,
        int PreserveTransparencyMinTextureSize,
        int CutoutPreserveTransparencyMinTextureSize,
        int MinimumOpaqueCoveragePercent,
        int CutoutMinimumOpaqueCoveragePercent,
        int MinimumOpaqueCoverageToPreservePercent,
        int CutoutMinimumOpaqueCoverageToPreservePercent,
        int MinimumOpaqueAlphaPercent,
        int PolygonAlphaUpperClampPercent,
        int PolygonMinimumOpaqueCoveragePercent,
        bool AllowDepthTestChange,
        bool IgnoreOutOfRangeMaterialSlots,
        bool AcceptDesktopAnisotropyDifferences,
        bool AcceptMobileAnisotropyDifferences)
    {
        internal string Name { get; } = Name;
        internal string Description { get; } = Description;
        internal bool AlphaSeparatorEnabled { get; } = AlphaSeparatorEnabled;
        internal int PreserveTransparencyMaxMipLevel { get; } =
            PreserveTransparencyMaxMipLevel;
        internal int CutoutPreserveTransparencyMaxMipLevel { get; } =
            CutoutPreserveTransparencyMaxMipLevel;
        internal int PreserveTransparencyMinTextureSize { get; } =
            PreserveTransparencyMinTextureSize;
        internal int CutoutPreserveTransparencyMinTextureSize { get; } =
            CutoutPreserveTransparencyMinTextureSize;
        internal int MinimumOpaqueCoveragePercent { get; } =
            MinimumOpaqueCoveragePercent;
        internal int CutoutMinimumOpaqueCoveragePercent { get; } =
            CutoutMinimumOpaqueCoveragePercent;
        internal int MinimumOpaqueCoverageToPreservePercent { get; } =
            MinimumOpaqueCoverageToPreservePercent;
        internal int CutoutMinimumOpaqueCoverageToPreservePercent { get; } =
            CutoutMinimumOpaqueCoverageToPreservePercent;
        internal int MinimumOpaqueAlphaPercent { get; } =
            MinimumOpaqueAlphaPercent;
        internal int PolygonAlphaUpperClampPercent { get; } =
            PolygonAlphaUpperClampPercent;
        internal int PolygonMinimumOpaqueCoveragePercent { get; } =
            PolygonMinimumOpaqueCoveragePercent;
        internal bool AllowDepthTestChange { get; } = AllowDepthTestChange;
        internal bool IgnoreOutOfRangeMaterialSlots { get; } =
            IgnoreOutOfRangeMaterialSlots;
        internal bool AcceptDesktopAnisotropyDifferences { get; } =
            AcceptDesktopAnisotropyDifferences;
        internal bool AcceptMobileAnisotropyDifferences { get; } =
            AcceptMobileAnisotropyDifferences;

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
