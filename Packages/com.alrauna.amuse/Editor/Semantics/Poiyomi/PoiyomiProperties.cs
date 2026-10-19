namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The Poiyomi Toon property names that more than one module reads.
    /// Each spelling matches the verified Poiyomi Toon 9.3.64 shader definition.
    /// A property that only one module reads retains its local spelling.
    /// </summary>
    internal static class PoiyomiProperties
    {
        internal const string ShaderOptimizerEnabled = "_ShaderOptimizerEnabled";
        internal const string Mode = "_Mode";
        internal const string TwoPassMode = "_ModeTwoPass";
        internal const string Cutoff = "_Cutoff";
        internal const string AlphaForceOpaque = "_AlphaForceOpaque";
        internal const string MainTex = "_MainTex";
        internal const string Color = "_Color";
        internal const string MainTexUv = "_MainTexUV";
        internal const string MainTexPan = "_MainTexPan";
        internal const string EnableOutlines = "_EnableOutlines";
        internal const string OutlineOverrideAlpha = "_OutlineOverrideAlpha";
        internal const string LineColor = "_LineColor";
        internal const string OutlineTexture = "_OutlineTexture";
        internal const string OutlineTextureUv = "_OutlineTextureUV";
        internal const string OutlineTexturePan = "_OutlineTexturePan";
        internal const string SrcBlend = "_SrcBlend";
        internal const string DstBlend = "_DstBlend";
        internal const string BlendOp = "_BlendOp";
        internal const string SrcBlendAlpha = "_SrcBlendAlpha";
        internal const string DstBlendAlpha = "_DstBlendAlpha";
        internal const string BlendOpAlpha = "_BlendOpAlpha";
        internal const string AlphaToCoverage = "_AlphaToCoverage";
    }
}
