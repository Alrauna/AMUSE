using System.Globalization;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Mints the in-build identity of an admitted Avatar Optimizer
    /// atlas. The value derives from the live object's own instance id,
    /// so distinct objects cannot collide and one object mints one id
    /// for the whole build. No claim table and no poison table are
    /// needed, unlike the source-keyed replacement identity. The id is
    /// per-build by design; it never names a project asset and needs
    /// no session clear.
    /// </summary>
    // Upstream path (spec 2026-10-04, section 8): an atlas that Avatar
    // Optimizer registers in the NDMF object registry, or persists into
    // a build container, reads through an existing identity form and
    // retires this mint together with the admission.
    internal static class AaoAtlasIdentity
    {
        private const string Prefix = "unity-aao-atlas:";

        internal static bool TryMint(
            Texture2D atlas, out TextureSourceId id)
        {
            id = default;
            if (atlas == null)
            {
                return false;
            }
            id = new TextureSourceId(
                Prefix + atlas.GetInstanceID()
                    .ToString(CultureInfo.InvariantCulture));
            return true;
        }
    }
}
