namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Pins per-texture anisotropic filtering to Forced On for the scope of
    /// one using block. The dev editor persists the setting, so every test
    /// that needs it must restore the old value on exit.
    /// </summary>
    internal sealed class AnisotropicFilteringPin : System.IDisposable
    {
        private readonly UnityEngine.AnisotropicFiltering _saved;

        internal AnisotropicFilteringPin()
        {
            _saved = UnityEngine.QualitySettings.anisotropicFiltering;
            UnityEngine.QualitySettings.anisotropicFiltering =
                UnityEngine.AnisotropicFiltering.Enable;
        }

        public void Dispose()
        {
            UnityEngine.QualitySettings.anisotropicFiltering = _saved;
        }
    }
}
