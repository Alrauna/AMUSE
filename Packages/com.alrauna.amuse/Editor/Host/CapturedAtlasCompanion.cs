using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// One companion route texture of an admitted use as immutable
    /// evidence. It holds numbers and names only, never a live Unity
    /// reference. The companion page uses the main page layout shifted
    /// right by <see cref="ScaleShift"/>.
    /// </summary>
    internal sealed class CapturedAtlasCompanion
    {
        internal string Property { get; }

        internal int SourceTextureInstanceId { get; }

        internal GraphicsFormat SourceFormat { get; }

        internal int SourceWidth { get; }

        internal int SourceHeight { get; }

        internal int SourceMipCount { get; }

        /// <summary>
        /// The sampler the shader reads the texture with. It is the
        /// texture's own sampler, the member sampler, or the inline
        /// sampler of the route.
        /// </summary>
        internal Semantics.AtlasSamplerFacts Sampler { get; }

        /// <summary>
        /// The power of two between the sizes: the main width is the
        /// companion width shifted left by this value, and the same
        /// holds for the heights.
        /// </summary>
        internal int ScaleShift { get; }

        internal CapturedAtlasCompanion(
            string property,
            int sourceTextureInstanceId,
            GraphicsFormat sourceFormat,
            int sourceWidth,
            int sourceHeight,
            int sourceMipCount,
            Semantics.AtlasSamplerFacts sampler,
            int scaleShift)
        {
            Property = property;
            SourceTextureInstanceId = sourceTextureInstanceId;
            SourceFormat = sourceFormat;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            SourceMipCount = sourceMipCount;
            Sampler = sampler;
            ScaleShift = scaleShift;
        }
    }
}
