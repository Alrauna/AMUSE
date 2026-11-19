namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The avatar-scope refusals of the atlas pass.
    /// <see cref="None"/> is not a refusal: it means no atlas gate has
    /// refused yet. Milestone 2 appends the use-scope members.
    /// Design section 4 conditions 1 and 2.
    /// </summary>
    internal enum AtlasUseRefusal
    {
        None,

        /// <summary>The build target is not the Windows desktop target.</summary>
        UnsupportedBuildTarget,

        /// <summary>The maximum page size left the range 1 to 8192.</summary>
        MaximumPageSizeOutOfRange,

        /// <summary>The protected mip level count left the range 1 to 14.</summary>
        ProtectedMipLevelCountOutOfRange,
    }
}
