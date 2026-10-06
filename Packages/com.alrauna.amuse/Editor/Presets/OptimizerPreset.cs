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
        internal int PreserveTransparencyMinTextureSize { get; }
        internal int MinimumOpaqueCoveragePercent { get; }
        internal int MinimumOpaqueAlphaPercent { get; }
        internal int PolygonAlphaUpperClampPercent { get; }
        internal int PolygonMinimumOpaqueCoveragePercent { get; }
        internal bool AllowDepthTestChange { get; }
        internal bool IgnoreOutOfRangeMaterialSlots { get; }

        internal OptimizerPreset(
            string name,
            string description,
            bool alphaSeparatorEnabled,
            int preserveTransparencyMaxMipLevel,
            int preserveTransparencyMinTextureSize,
            int minimumOpaqueCoveragePercent,
            int minimumOpaqueAlphaPercent,
            int polygonAlphaUpperClampPercent,
            int polygonMinimumOpaqueCoveragePercent,
            bool allowDepthTestChange,
            bool ignoreOutOfRangeMaterialSlots)
        {
            Name = name;
            Description = description;
            AlphaSeparatorEnabled = alphaSeparatorEnabled;
            PreserveTransparencyMaxMipLevel = preserveTransparencyMaxMipLevel;
            PreserveTransparencyMinTextureSize = preserveTransparencyMinTextureSize;
            MinimumOpaqueCoveragePercent = minimumOpaqueCoveragePercent;
            MinimumOpaqueAlphaPercent = minimumOpaqueAlphaPercent;
            PolygonAlphaUpperClampPercent = polygonAlphaUpperClampPercent;
            PolygonMinimumOpaqueCoveragePercent = polygonMinimumOpaqueCoveragePercent;
            AllowDepthTestChange = allowDepthTestChange;
            IgnoreOutOfRangeMaterialSlots = ignoreOutOfRangeMaterialSlots;
        }

        /// <summary>
        /// True when the component equals this preset on all nine
        /// mapped fields. The pressed state is computed from this
        /// comparison and never stored, so a manual edit of one value
        /// leaves every button unpressed.
        /// </summary>
        internal bool Matches(AmuseAvatarOptimizer component)
        {
            return component.AlphaSeparatorEnabled == AlphaSeparatorEnabled
                && component.PreserveTransparencyMaxMipLevel
                    == PreserveTransparencyMaxMipLevel
                && component.PreserveTransparencyMinTextureSize
                    == PreserveTransparencyMinTextureSize
                && component.MinimumOpaqueCoveragePercent
                    == MinimumOpaqueCoveragePercent
                && component.MinimumOpaqueAlphaPercent
                    == MinimumOpaqueAlphaPercent
                && component.PolygonAlphaUpperClampPercent
                    == PolygonAlphaUpperClampPercent
                && component.PolygonMinimumOpaqueCoveragePercent
                    == PolygonMinimumOpaqueCoveragePercent
                && component.AllowDepthTestChange == AllowDepthTestChange
                && component.IgnoreOutOfRangeMaterialSlots
                    == IgnoreOutOfRangeMaterialSlots;
        }
    }
}
