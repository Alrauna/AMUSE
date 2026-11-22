using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The detail sentences of use-scope atlas refusals. Each method
    /// returns one or more finished sentences that name the exact
    /// value that refused, so a user can find it without a debugger.
    /// The sentences are code literals at each refusal site, because
    /// one cause can refuse for several different values.
    /// <para>
    /// Every number prints in the invariant culture, so the text is the
    /// same on every machine. A float prints with up to six decimals,
    /// and NaN prints as <c>NaN</c>.
    /// </para>
    /// </summary>
    internal static class AtlasRefusalDetail
    {
        internal static string Number(float value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        internal static string Number(double value)
        {
            return value.ToString("0.######", CultureInfo.InvariantCulture);
        }

        internal static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        internal static string Vector(Vector4 value)
        {
            return "(" + Number(value.x) + ", " + Number(value.y) + ", " +
                   Number(value.z) + ", " + Number(value.w) + ")";
        }

        internal static string UnsupportedRenderer(Renderer renderer)
        {
            return "The renderer is a " + renderer.GetType().Name + ".";
        }

        internal static string MissingMesh(Mesh mesh)
        {
            return mesh == null
                ? "The renderer has no mesh."
                : "The mesh '" + mesh.name + "' has no parts.";
        }

        internal static string NonTriangleTopology(
            Mesh mesh, int submeshIndex, MeshTopology topology)
        {
            return "Part " + Number(submeshIndex) + " of the mesh '" +
                   mesh.name + "' uses " + topology + ".";
        }

        internal static string AnimatedProperty(string property)
        {
            return "An animation drives '" + property + "'.";
        }

        internal static string SlotSharesSubmesh(
            int slotCount, Mesh mesh, int submeshCount)
        {
            return "The renderer has " + Number(slotCount) +
                   " material slots. The mesh '" + mesh.name + "' has " +
                   Number(submeshCount) + " parts.";
        }

        internal static string Shader(Shader shader)
        {
            return shader == null
                ? "The material has no shader."
                : "The shader is '" + shader.name + "'.";
        }

        internal static string KeywordNotAllowed(IList<string> keywords)
        {
            return "The material turns on these keywords outside the" +
                   " route: " + string.Join(", ", keywords) + ".";
        }

        internal static string UnlistedTextures(
            IList<(string Property, Texture Texture)> textures)
        {
            var parts = new string[textures.Count];
            for (var index = 0; index < textures.Count; index++)
            {
                parts[index] = "'" + textures[index].Property + "' ('" +
                               textures[index].Texture.name + "')";
            }

            return "The material assigns these textures outside the" +
                   " route: " + string.Join(", ", parts) + ".";
        }

        internal static string PropertyBlock(
            string property, bool rendererWide, int slotIndex)
        {
            return rendererWide
                ? "The property block of the whole renderer sets '" +
                  property + "'."
                : "The property block of slot " + Number(slotIndex) +
                  " sets '" + property + "'.";
        }

        internal static string MissingMainTexture(string memberProperty)
        {
            return "The property '" + memberProperty +
                   "' holds no texture.";
        }

        internal static string UnsupportedTextureDimension(Texture texture)
        {
            return "The texture '" + texture.name + "' is a " +
                   texture.dimension + " texture.";
        }

        internal static string SourceOnlyInMemory(Texture texture)
        {
            return "The texture '" + texture.name +
                   "' exists only in memory.";
        }

        internal static string SourceWithoutImporter(string path)
        {
            return "The file '" + path + "' has no texture importer.";
        }

        internal static string SourceNotMainAsset(
            Texture texture, string path)
        {
            return "The texture '" + texture.name +
                   "' is not the main asset of '" + path + "'.";
        }

        internal static string UnsupportedFormat(Texture2D texture)
        {
            return "The texture '" + texture.name + "' has the format " +
                   texture.format + ".";
        }

        internal static string UnsupportedWrap(Texture texture)
        {
            return "The texture '" + texture.name + "' uses " +
                   texture.wrapModeU + " wrap on U, " + texture.wrapModeV +
                   " wrap on V, and the " + texture.filterMode + " filter.";
        }

        internal static string SourceMipChainTooShort(
            Texture2D texture, int protectedMipLevelCount)
        {
            return "The texture '" + texture.name + "' has " +
                   Number(texture.mipmapCount) +
                   " mip levels. The protected mip level count is " +
                   Number(protectedMipLevelCount) + ".";
        }

        internal static string SourceNotAligned(
            Texture2D texture, int protectedMipLevelCount, int block)
        {
            var shift = protectedMipLevelCount - 1;
            return "The texture '" + texture.name + "' is " +
                   Number(texture.width) + " x " + Number(texture.height) +
                   ". At protected level " + Number(protectedMipLevelCount) +
                   " it is " + Number(texture.width >> shift) + " x " +
                   Number(texture.height >> shift) + ". The format " +
                   texture.format + " needs a multiple of " + Number(block) +
                   ".";
        }

        internal static string NonIdentityTransform(
            string property, Vector4 value, Vector4 identity)
        {
            return "The property '" + property + "' is " + Vector(value) +
                   ". The route needs " + Vector(identity) + ".";
        }

        internal static string MissingRouteChannel(Mesh mesh)
        {
            return "The mesh '" + mesh.name + "' has no UV0 channel.";
        }

        internal static string EmptySubmesh(Mesh mesh, int submeshIndex)
        {
            return "Part " + Number(submeshIndex) + " of the mesh '" +
                   mesh.name + "' has no triangles.";
        }

        internal static string UvOutsideUnitSquare(
            int vertexIndex, Vector2 uv)
        {
            return "Vertex " + Number(vertexIndex) + " has the UV0 value (" +
                   Number(uv.x) + ", " + Number(uv.y) + ").";
        }

        internal static string PlacementTooLarge(
            int regionWidth, int regionHeight, int maximumPageSize)
        {
            return "The region needs " + Number(regionWidth) + " x " +
                   Number(regionHeight) +
                   " texels with its border. The maximum page size is " +
                   Number(maximumPageSize) + ".";
        }

        internal static string ClampEdgeOnBc7(Texture texture)
        {
            return "The texture '" + (texture == null ? "" : texture.name) +
                   "' is BC7, uses Clamp wrap, and its UV region reaches" +
                   " the texture edge.";
        }

        internal static string SourceReadFailed(Texture texture)
        {
            return "AMUSE could not read the texture '" + texture.name + "'.";
        }

        internal static string SourceChanged()
        {
            return "A source texture of this group changed during the build.";
        }
    }
}
