namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Pins per-texture anisotropic filtering for the scope of one
    /// using block. The default mode follows each texture's own level.
    /// The forced mode raises every texture that declares a level below
    /// its own forced level. The dev editor persists the setting, so
    /// every test that needs it must restore the old value on exit.
    /// </summary>
    internal sealed class AnisotropicFilteringPin : System.IDisposable
    {
        private readonly UnityEngine.AnisotropicFiltering _saved;

        internal AnisotropicFilteringPin(
            UnityEngine.AnisotropicFiltering filtering =
                UnityEngine.AnisotropicFiltering.Enable)
        {
            _saved = UnityEngine.QualitySettings.anisotropicFiltering;
            UnityEngine.QualitySettings.anisotropicFiltering = filtering;
        }

        public void Dispose()
        {
            UnityEngine.QualitySettings.anisotropicFiltering = _saved;
        }
    }
}
