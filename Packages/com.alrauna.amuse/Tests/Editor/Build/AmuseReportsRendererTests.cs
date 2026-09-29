using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The renderer-scoped refusal reports. The mapping refusal must name
    /// both sides of the mismatch with the counts the refusal used, so
    /// the tests capture through
    /// <see cref="ErrorReport.CaptureErrors(Action)"/> and read the
    /// rendered message.
    /// </summary>
    public sealed class AmuseReportsRendererTests
    {
        private GameObject _root;
        private MeshRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AMUSE renderer report");
            _renderer = _root.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void MappingRefusalReportCarriesBothSlotCounts()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.RendererRefusal(
                    _renderer,
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping,
                    3,
                    1));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: an emitter that swaps the arguments reports the
            // mesh's count on the renderer and the renderer's count on
            // the mesh, so the reader fixes the wrong side.
            Assert.That(
                message, Does.Contain("mesh supports 3 material slots"));
            Assert.That(
                message, Does.Contain("renderer has 1 material slot"));
        }

        [Test]
        public void MappingRefusalWithUnknownCountStatesUnknownNotMinusOne()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.RendererRefusal(
                    _renderer,
                    RendererAnalysisRefusal.UnprovenMaterialSlotMapping,
                    2,
                    -1));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: an emitter that formats the raw sentinel prints
            // a negative count, and minus one is not a count a reader
            // can act on.
            Assert.That(
                message, Does.Contain("mesh supports 2 material slots"));
            Assert.That(message, Does.Contain(
                "an unknown number of material slots"));
            Assert.That(message, Does.Not.Contain("-1"));
        }
    }
}
