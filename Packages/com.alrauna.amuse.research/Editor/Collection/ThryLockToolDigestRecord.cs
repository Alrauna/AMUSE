namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// The recorded digests of the vendor lock tool source file. Each
    /// digest attests one file. A digest is the lowercase hex SHA-256 of
    /// that file's bytes. The first digest is the
    /// <c>Editor/ShaderOptimizer.cs</c> source embedded in
    /// <c>com.poiyomi.toon</c> 9.3.64. The second digest is the source
    /// shipped in <c>com.poiyomi.thryeditor</c> 2.74.2.
    /// <para>
    /// The values were recorded on 2026-09-21 from freshly fetched vendor
    /// archives. Each archive SHA-256 matched the pins in the
    /// 2026-09-19 characterization.
    /// </para>
    /// <para>
    /// This record holds no consumer by design. It preserves measured
    /// evidence that the production code no longer computes. Deleting
    /// this record is a reviewed evidence decision. It is never a
    /// cleanup.
    /// </para>
    /// </summary>
    internal static class ThryLockToolDigestRecord
    {
        // 9.3.64 embedded tooling, era 1.
        internal const string EmbeddedToolDigest =
            "9000377ab486863e20d25b8bd025863c7590354ea3cdb1a5ca493d8e5045f3e5";

        // 2.74.2 standalone tooling, era 2.
        internal const string StandaloneToolDigest =
            "7c1ffe78c872288ec605b5bd742467401c952562701aa171fead0df4ae1883ee";
    }
}
