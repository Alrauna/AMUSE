using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Makes the consent gate answer as an interactive editor for every Build
    /// test. A batch-mode run, such as the CI gate, declines each consent
    /// before any presenter runs. The tests that grant consent through a
    /// presenter would then fail for a reason that is not a product defect.
    /// A test that needs the batch-mode answer sets the seam itself.
    /// </summary>
    [SetUpFixture]
    public sealed class BuildBatchModeSetUpFixture
    {
        [OneTimeSetUp]
        public void ForceInteractiveConsentGate()
        {
            Alrauna.Amuse.Editor.Build.AmusePlatformFinishPass.BatchModeForTests =
                () => false;
        }

        [OneTimeTearDown]
        public void RestoreConsentGate()
        {
            Alrauna.Amuse.Editor.Build.AmusePlatformFinishPass.BatchModeForTests =
                null;
        }
    }
}
