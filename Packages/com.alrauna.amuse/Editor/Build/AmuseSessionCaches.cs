using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Owns the fixed session-cache reset list for the build passes.
    /// <para>
    /// The reset runs at two moments. The build-entry call clears
    /// leftover evidence before the finish pass decides anything. A
    /// lifecycle refusal still leaves clean caches. The clone-sweep
    /// call clears the same caches again after teardown. A second
    /// build or an apply in the same session then starts without
    /// stale evidence.
    /// </para>
    /// <para>
    /// The members and their order are fixed. Do not add a registry
    /// or a cache-manager abstraction. The native Play identity
    /// snapshot is not a reset member. Store lifecycle keeps it alive
    /// through both moments until Play exit.
    /// </para>
    /// </summary>
    internal static class AmuseSessionCaches
    {
        /// <summary>
        /// Clears the seven session caches in their fixed order.
        /// </summary>
        internal static void Clear()
        {
            UnityGeneratedTextureEvidence.ClearCache();
            UnityAlphaFieldEvidence.ClearCache();
            ReplacementTextureIdentity.ClearSession();
            ReplacementTextureAttestation.Versions.ClearVersionCacheForSession();
            AaoAtlasTextureAttestation.Versions.ClearVersionCacheForSession();
            Semantics.LilToon.LilToonSourceAttestation.ClearGatherMemo();
            Semantics.Poiyomi.PoiyomiSourceAttestation.ClearSourceHashMemo();
        }
    }
}
