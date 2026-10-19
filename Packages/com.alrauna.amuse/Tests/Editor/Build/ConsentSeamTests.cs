using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the headless consent seam (H1). The seam is null in
    /// production, the recorder answers decline and records subjects,
    /// the gate consults the seam, and the fixture restores null before
    /// any fallible teardown step.
    /// Spec: docs/superpowers/specs/2026-10-19-liltoon-2-4-0-admission-and-headless-tests-design.md
    /// </summary>
    public sealed class ConsentSeamTests
    {
        private sealed class SeamProbeFixture : MergedConsumptionFixture
        {
            protected override string TempFolder =>
                "Assets/AmuseTests_SeamProbe";
        }

        [Test]
        public void Recorder_AnswersDeclineAndRecordsSubjects()
        {
            var recorder = new RecordingDeclineConsentPresenter();
            var subjects = new[] { "Shader 'Hidden/lilToonTransparent' is not a verified version" };

            var answer = recorder.Present(subjects);

            Assert.That(answer, Is.False, "the recorder always declines");
            Assert.That(recorder.SubjectsSeen, Is.EqualTo(subjects),
                "the recorder keeps every subject it was asked");
        }

        [Test]
        public void ArmedSeam_GateConsultsItAndDeclines()
        {
            var recorder = new RecordingDeclineConsentPresenter();
            AmusePlatformFinishPass.PresenterForTests = recorder.Present;
            try
            {
                var subjects = new[]
                {
                    "Shader 'Hidden/lilToonTransparent' is not a verified version",
                };

                var proceed = VersionConsentDialog.ShouldProceed(
                    subjects, batchMode: false,
                    presenter: AmusePlatformFinishPass.ResolveConsentPresenter(
                        consentPresenter: null));

                Assert.That(proceed, Is.False,
                    "the seam's decline answer must reach the gate");
                Assert.That(recorder.SubjectsSeen, Is.EqualTo(subjects),
                    "the gate must consult the seam, not only the recorder");
            }
            finally
            {
                AmusePlatformFinishPass.PresenterForTests = null;
            }
        }

        [Test]
        public void FixtureSetUpTearDown_LeavesTheSeamNull()
        {
            var fixture = new SeamProbeFixture();
            fixture.PinPerTextureAnisotropicFiltering();
            try
            {
                Assert.That(AmusePlatformFinishPass.PresenterForTests,
                    Is.Not.Null,
                    "set-up arms the seam for the merged-consumption suites");
            }
            finally
            {
                fixture.TearDown();
            }

            Assert.That(AmusePlatformFinishPass.PresenterForTests, Is.Null,
                "tear-down restores null before any fallible teardown step");
        }
    }
}
