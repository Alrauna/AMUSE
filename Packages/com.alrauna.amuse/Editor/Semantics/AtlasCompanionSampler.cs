namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// The sampler state that the shader uses to read one companion
    /// route texture.
    /// </summary>
    internal enum AtlasCompanionSampler
    {
        /// <summary>The texture's own sampler state.</summary>
        Own = 0,

        /// <summary>
        /// The sampler state of the member texture. The shader reads
        /// the texture with <c>sampler_MainTex</c>.
        /// </summary>
        Member = 1,

        /// <summary>
        /// A trilinear Repeat inline sampler without anisotropy. lilToon
        /// names it <c>lil_sampler_linear_repeat</c>.
        /// </summary>
        InlineTrilinearRepeat = 2,
    }
}
