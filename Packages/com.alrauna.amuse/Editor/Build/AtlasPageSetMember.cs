using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// One material slot that moved onto a page of a page set.
    /// TRANSIENT HOST DATA: the member keeps live references, so the
    /// report and the inspector can make them clickable.
    /// </summary>
    internal sealed class AtlasPageSetMember
    {
        internal AtlasPageSetMember(
            Renderer renderer,
            int slotIndex,
            Material material,
            Texture texture,
            int textureWidth,
            int textureHeight,
            int pageIndex)
        {
            Renderer = renderer;
            SlotIndex = slotIndex;
            Material = material;
            Texture = texture;
            TextureWidth = textureWidth;
            TextureHeight = textureHeight;
            PageIndex = pageIndex;
        }

        internal Renderer Renderer { get; }

        internal int SlotIndex { get; }

        internal Material Material { get; }

        /// <summary>The member texture that the page copies.</summary>
        internal Texture Texture { get; }

        internal int TextureWidth { get; }

        internal int TextureHeight { get; }

        /// <summary>The index of the page in its set.</summary>
        internal int PageIndex { get; }
    }
}
