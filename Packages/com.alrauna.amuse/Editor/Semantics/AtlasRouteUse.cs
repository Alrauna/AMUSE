namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// How one material uses one companion route texture.
    /// </summary>
    internal enum AtlasRouteUse
    {
        /// <summary>
        /// No reachable code samples the texture. It leaves the
        /// material key and needs no page.
        /// </summary>
        Inert = 0,

        /// <summary>
        /// The shader samples the texture at the main UV0 coordinate
        /// with the texture's own identity transform. The texture
        /// moves into a companion page beside the main page.
        /// </summary>
        Uv0 = 1,

        /// <summary>
        /// The shader samples the texture at a coordinate the atlas
        /// does not move. The texture stays in the material key and
        /// stays bound as it is.
        /// </summary>
        OtherCoordinates = 2,
    }
}
