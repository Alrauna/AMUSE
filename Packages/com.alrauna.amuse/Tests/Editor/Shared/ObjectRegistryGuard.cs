using System;
using nadena.dev.ndmf;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Saves the active <see cref="ObjectRegistry"/>, installs a fresh
    /// isolated registry, and restores the saved one on dispose. Dispose in
    /// teardown or in a finally block; the surrounding setup and teardown
    /// shapes stay owned by the calling fixture.
    /// </summary>
    internal sealed class ObjectRegistryGuard : IDisposable
    {
        private readonly IObjectRegistry _previous;

        internal ObjectRegistryGuard()
        {
            _previous = ObjectRegistry.ActiveRegistry;
            ObjectRegistry.ActiveRegistry = new ObjectRegistry(null);
        }

        public void Dispose()
        {
            ObjectRegistry.ActiveRegistry = _previous;
        }
    }
}
