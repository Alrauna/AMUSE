using NUnit.Framework;
using Alrauna.Amuse.Editor.Build;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The dry run draws refusal sentences into the inspector through
    /// the pure formatting helper, not through NDMF error objects. An
    /// unknown key must yield the empty string, because the dry run
    /// guards with Has before it draws. Every supplied number must land
    /// where the sentence expects it.
    /// </summary>
    public sealed class AmuseReportStringFormatTests
    {
        [Test]
        public void FormatReplacesNumberedTokens()
        {
            var formatted = AmuseReportStrings.Format(
                "amuse.consent.Subject",
                "2022.3.5");

            // Falsifier: a helper that leaves the number token in place
            // draws raw braces into the inspector row, and the reader
            // cannot see the version the check questioned.
            Assert.That(
                formatted, Is.EqualTo("Unverified version: 2022.3.5"));

            // A token beyond the supplied arguments stays untouched.
            var sparse = AmuseReportStrings.Format(
                "amuse.renderer.MaterialDependencyClosureFailed");
            Assert.That(
                sparse, Does.Contain("renderer '{2}'"));
        }

        [Test]
        public void FormatOfUnknownKeyIsEmpty()
        {
            // The dry run guards with Has, so an unknown key must give
            // the empty string and never a null or an exception.
            Assert.That(
                AmuseReportStrings.Format("amuse.no.such.key"),
                Is.Empty);
        }
    }
}
