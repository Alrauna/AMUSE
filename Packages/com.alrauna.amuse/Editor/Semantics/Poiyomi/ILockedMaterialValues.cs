using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The identity of one texture on a material (A5.2). The identity token
    /// is an opaque string that the Unity view mints from the texture asset
    /// identity, or from the instance id for a texture without an asset.
    /// Null means no texture. Only the Unity view turns a token back into
    /// a texture. The record is plain data and never touches a Unity
    /// object.
    /// </summary>
    internal readonly struct LockedTextureValue
    {
        internal LockedTextureValue(
            string identityToken,
            int width,
            int height,
            Vector2 scale,
            Vector2 offset)
        {
            IdentityToken = identityToken;
            Width = width;
            Height = height;
            Scale = scale;
            Offset = offset;
        }

        internal string IdentityToken { get; }

        internal int Width { get; }

        internal int Height { get; }

        internal Vector2 Scale { get; }

        internal Vector2 Offset { get; }
    }

    /// <summary>
    /// Typed reads of the material values that the reproduction map needs
    /// (A5.2). The build reads the locked clone through the interface while
    /// the clone still has the generated shader. The consistency check
    /// reads the finished clone, and the dry run reads its plan view,
    /// through the same interface (A8.1). The interface never writes.
    /// </summary>
    internal interface ILockedMaterialValues
    {
        /// <summary>Reads one float by property name.</summary>
        bool TryGetFloat(string propertyName, out float value);

        /// <summary>Reads one vector by property name.</summary>
        bool TryGetVector(string propertyName, out Vector4 value);

        /// <summary>Reads one color by property name.</summary>
        bool TryGetColor(string propertyName, out Color value);

        /// <summary>
        /// Reads one texture with scale and offset by property name. The
        /// result is false when the name is no texture slot of the shader.
        /// A true result carries a null token for an empty slot.
        /// </summary>
        bool TryGetTexture(string propertyName, out LockedTextureValue value);

        /// <summary>The render queue of the material (A5.10).</summary>
        int RenderQueue { get; }

        /// <summary>The RenderType override tag of the material (A5.10).</summary>
        string RenderTypeTag { get; }

        /// <summary>The VRCFallback override tag of the material (A5.10).</summary>
        string VrcFallbackTag { get; }

        /// <summary>The OriginalKeywords tag keywords (A5.9, fact 14).</summary>
        IReadOnlyList<string> OriginalKeywordsTag { get; }

        /// <summary>The keywords of the material.</summary>
        IReadOnlyList<string> Keywords { get; }

        /// <summary>True when the project renders in linear color space.</summary>
        bool ProjectIsLinear { get; }
    }
}
