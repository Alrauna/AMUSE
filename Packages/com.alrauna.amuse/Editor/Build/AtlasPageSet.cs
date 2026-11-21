namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The report facts of one page set: the pages of one group that
    /// the atlas pass built and kept. Every use of the set shares one
    /// group key, and each page has one material, so the set is
    /// merge-ready by construction (design section 6). The sources
    /// stay in the avatar, so the page bytes are the allocation
    /// change (design section 10).
    /// </summary>
    internal sealed class AtlasPageSet
    {
        internal AtlasPageSet(
            int groupIndex,
            string shaderRowName,
            int pageCount,
            int useCount,
            long pageBytes,
            int sourceCount,
            long sourceBytes)
        {
            GroupIndex = groupIndex;
            ShaderRowName = shaderRowName;
            PageCount = pageCount;
            UseCount = useCount;
            PageBytes = pageBytes;
            SourceCount = sourceCount;
            SourceBytes = sourceBytes;
        }

        /// <summary>The plan index of the group. Page names use it.</summary>
        internal int GroupIndex { get; }

        internal string ShaderRowName { get; }

        internal int PageCount { get; }

        /// <summary>How many uses moved onto the pages of the set.</summary>
        internal int UseCount { get; }

        /// <summary>The texture bytes of every page of the set.</summary>
        internal long PageBytes { get; }

        /// <summary>How many distinct source textures the set copies.</summary>
        internal int SourceCount { get; }

        /// <summary>The texture bytes of the distinct sources.</summary>
        internal long SourceBytes { get; }
    }
}
