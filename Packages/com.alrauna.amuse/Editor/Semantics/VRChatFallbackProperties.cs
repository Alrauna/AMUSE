using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The property names a VRChat fallback shader can read. VRChat
    /// swaps a material to a fallback shader by its VRCFallback tag.
    /// A saved value whose name matches a fallback shader property
    /// can change rendering on any row, whatever tag the material
    /// holds. So these names always count. They never turn stale,
    /// and they never join a dead family. The names come from the
    /// two Toon Standard shaders and the Unity Standard property
    /// surface.
    /// </summary>
    internal static class VRChatFallbackProperties
    {
        internal static readonly IReadOnlyCollection<string> Names =
            new[]
            {
                "_ALBarSmoothing",
                "_ALFallbackSpeed",
                "_ALIntensity",
                "_ALRimMaskSmoothing",
                "_ALRimMaskStrength",
                "_ALScrollScale",
                "_ALTint",
                "_AudioLinkMask",
                "_BumpMap",
                "_BumpScale",
                "_Color",
                "_ColorMask",
                "_ColorMaskColor1",
                "_ColorMaskColor2",
                "_ColorMaskColor3",
                "_ColorMaskColor4",
                "_ColorMaskEmissionStrength1",
                "_ColorMaskEmissionStrength2",
                "_ColorMaskEmissionStrength3",
                "_ColorMaskEmissionStrength4",
                "_Cutoff",
                "_MatCap",
                "_Matcap",
                "_Mode",
                "_DetailAlbedoMap",
                "_DetailHueShift",
                "_DetailMask",
                "_DetailNormalMap",
                "_DetailNormalMapScale",
                "_EmissionColor",
                "_EmissionHueShift",
                "_EmissionMap",
                "_EmissionStrength",
                "_GlossMap",
                "_GlossMapScale",
                "_GlossStrength",
                "_Glossiness",
                "_GlossyReflections",
                "_HueShift",
                "_HueShiftMask",
                "_MainTex",
                "_Matcap",
                "_MatcapMask",
                "_MatcapStrength",
                "_Metallic",
                "_MetallicGlossMap",
                "_MetallicMap",
                "_MetallicStrength",
                "_OcclusionMap",
                "_OcclusionStrength",
                "_OutlineColor",
                "_OutlineMask",
                "_Outline_fromAlbedo",
                "_Parallax",
                "_ParallaxMap",
                "_Ramp",
                "_Reflectance",
                "_RimAlbedoTint",
                "_RimColor",
                "_RimIntensity",
                "_RimRange",
                "_RimSharpness",
                "_ShadowAlbedo",
                "_ShadowBoost",
                "_SmoothnessTextureChannel",
                "_SpecColor",
                "_SpecGlossMap",
                "_SpecularHighlights",
                "_SpecularSharpness",
                "_UVSec",
            };
    }
}
