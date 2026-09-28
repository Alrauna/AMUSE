using nadena.dev.ndmf;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Resolves a build-copy object to the source object a producer
    /// registered in NDMF's object registry. The lookup is read-only:
    /// with create:false an unregistered object answers null, while a
    /// creating lookup would fabricate an entry and block a later
    /// RegisterReplacedObject for that object, the failure class the
    /// 2026-09-08 registry design fixed inside AMUSE.
    /// </summary>
    internal static class RegisteredSourceIdentity
    {
        internal static UnityEngine.Object Resolve(
            UnityEngine.Object source)
        {
            if (source == null)
            {
                return null;
            }
            var reference = (ObjectRegistry.ActiveRegistry as IObjectRegistry)?
                .GetReference(source, false);
            if (reference == null)
            {
                return null;
            }
            var resolved = reference.Object;
            return resolved != null && resolved != source ? resolved : null;
        }
    }
}
