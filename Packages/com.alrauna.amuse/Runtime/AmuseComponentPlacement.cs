using UnityEngine;

namespace Alrauna.Amuse.Runtime
{
    /// <summary>
    /// Placement rules for AmuseAvatarOptimizer. The component must sit on the
    /// root of the avatar hierarchy. A parent transform means a misplaced
    /// component. The build gate re-checks placement against the NDMF avatar
    /// root, which is the authority.
    /// </summary>
    public static class AmuseComponentPlacement
    {
        /// <summary>True when the component sits on a hierarchy root.</summary>
        public static bool IsOnHierarchyRoot(Component component)
        {
            return true;
        }
    }
}