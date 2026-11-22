using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// The UV bounding rectangle of one use, in source texture space.
    /// The components are doubles, because the rectangle feeds the
    /// placement math and the page write, and a float bound would round
    /// a boundary vertex into the neighboring placement.
    /// </summary>
    internal readonly struct AtlasUvRect : IEquatable<AtlasUvRect>
    {
        internal double MinU { get; }
        internal double MinV { get; }
        internal double MaxU { get; }
        internal double MaxV { get; }

        internal AtlasUvRect(
            double minU,
            double minV,
            double maxU,
            double maxV)
        {
            MinU = minU;
            MinV = minV;
            MaxU = maxU;
            MaxV = maxV;
        }

        public bool Equals(AtlasUvRect other)
        {
            return MinU == other.MinU &&
                   MinV == other.MinV &&
                   MaxU == other.MaxU &&
                   MaxV == other.MaxV;
        }

        public override bool Equals(object obj)
        {
            return obj is AtlasUvRect other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = MinU.GetHashCode();
                hash = (hash * 397) ^ MinV.GetHashCode();
                hash = (hash * 397) ^ MaxU.GetHashCode();
                hash = (hash * 397) ^ MaxV.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(
            AtlasUvRect left,
            AtlasUvRect right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            AtlasUvRect left,
            AtlasUvRect right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// One admitted atlas use as immutable evidence. The record holds
    /// numbers, names, and strings only. It never holds a live Unity
    /// reference, so a later pass can group and place from it after the
    /// build moved on. The live renderer stays on the outcome that
    /// carries this record.
    /// </summary>
    internal sealed class CapturedAtlasUse
    {
        /// <summary>The index of the renderer in capture order.</summary>
        internal int RendererIndex { get; }

        /// <summary>The index of the material slot on the renderer.</summary>
        internal int SlotIndex { get; }

        /// <summary>The submesh the slot draws.</summary>
        internal int SubmeshIndex { get; }

        /// <summary>
        /// The shader row name of the attested source. The row binds the
        /// use to one digest-pinned shader file.
        /// </summary>
        internal string ShaderRowName { get; }

        internal int SourceTextureInstanceId { get; }

        /// <summary>
        /// The graphics format of the member source, with its sRGB or
        /// linear form. The page format copies it.
        /// </summary>
        internal GraphicsFormat SourceFormat { get; }

        internal int SourceWidth { get; }
        internal int SourceHeight { get; }
        internal int SourceMipCount { get; }

        /// <summary>The sampler facts of the member source.</summary>
        internal Semantics.AtlasSamplerFacts Sampler { get; }

        /// <summary>The UV bounding rectangle of the use.</summary>
        internal AtlasUvRect UvRect { get; }

        /// <summary>
        /// The deterministic material key of the slot material. Two
        /// uses with equal keys can share one page material.
        /// </summary>
        internal string MaterialKey { get; }

        /// <summary>
        /// The stripped property names of the animated material
        /// properties of the renderer, distinct, in observation order.
        /// Equal names do not prove equal animation. They only join the
        /// group key, so two uses whose animations differ land in
        /// different groups.
        /// </summary>
        internal IReadOnlyList<string> AnimatedPropertyNames { get; }

        /// <summary>
        /// The position of this use in the capture sequence. The
        /// sequence covers every renderer slot, so later passes tie-break
        /// placements deterministically with it.
        /// </summary>
        internal int CaptureOrder { get; }

        /// <summary>
        /// The companion route textures that move into companion pages,
        /// sorted by property, ordinal.
        /// </summary>
        internal IReadOnlyList<CapturedAtlasCompanion> Companions { get; }

        /// <summary>
        /// The texture properties the page material sets to the main
        /// page, sorted ordinal: the member copies the shader declares,
        /// and each companion that is the member texture itself under
        /// the member sampler.
        /// </summary>
        internal IReadOnlyList<string> PageAliasProperties { get; }

        /// <summary>
        /// One string for every fact of the aliases and the companions
        /// that shapes a page or the page material. Uses with different
        /// signatures never share a group.
        /// </summary>
        internal string RouteSignature { get; }

        internal CapturedAtlasUse(
            int rendererIndex,
            int slotIndex,
            int submeshIndex,
            string shaderRowName,
            int sourceTextureInstanceId,
            GraphicsFormat sourceFormat,
            int sourceWidth,
            int sourceHeight,
            int sourceMipCount,
            Semantics.AtlasSamplerFacts sampler,
            AtlasUvRect uvRect,
            string materialKey,
            IReadOnlyList<string> animatedPropertyNames,
            int captureOrder,
            IReadOnlyList<CapturedAtlasCompanion> companions = null,
            IReadOnlyList<string> pageAliasProperties = null)
        {
            RendererIndex = rendererIndex;
            SlotIndex = slotIndex;
            SubmeshIndex = submeshIndex;
            ShaderRowName = shaderRowName;
            SourceTextureInstanceId = sourceTextureInstanceId;
            SourceFormat = sourceFormat;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            SourceMipCount = sourceMipCount;
            Sampler = sampler;
            UvRect = uvRect;
            MaterialKey = materialKey;
            AnimatedPropertyNames = new ReadOnlyCollection<string>(
                new List<string>(animatedPropertyNames));
            CaptureOrder = captureOrder;

            var sortedCompanions = new List<CapturedAtlasCompanion>(
                companions ?? Array.Empty<CapturedAtlasCompanion>());
            sortedCompanions.Sort((left, right) =>
                string.CompareOrdinal(left.Property, right.Property));
            Companions =
                new ReadOnlyCollection<CapturedAtlasCompanion>(sortedCompanions);
            var sortedAliases = new List<string>(
                pageAliasProperties ?? Array.Empty<string>());
            sortedAliases.Sort(StringComparer.Ordinal);
            PageAliasProperties =
                new ReadOnlyCollection<string>(sortedAliases);

            var parts = new List<string>();
            foreach (var alias in sortedAliases)
            {
                parts.Add("alias:" + alias);
            }

            foreach (var companion in sortedCompanions)
            {
                parts.Add(
                    "page:" + companion.Property + ":" +
                    companion.SourceFormat + ":" + companion.ScaleShift +
                    ":" + companion.Sampler.Filter + ":" +
                    companion.Sampler.WrapU + ":" + companion.Sampler.WrapV +
                    ":" + companion.Sampler.AnisoLevel + ":" +
                    BitConverter.SingleToInt32Bits(companion.Sampler.MipBias)
                        .ToString("X8"));
            }

            RouteSignature = string.Join(";", parts);
        }
    }
}
