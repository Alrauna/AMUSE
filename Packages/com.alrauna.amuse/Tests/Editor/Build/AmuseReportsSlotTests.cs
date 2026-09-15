using System;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The slot-scoped refusal reports. Every refused material slot must
    /// surface one exact report line: the slot index, the refusal cause,
    /// and (for texture capture) the property and channel. The reports ride
    /// the NDMF error report, so the tests capture through
    /// <see cref="ErrorReport.CaptureErrors(Action)"/>.
    /// </summary>
    public sealed class AmuseReportsSlotTests
    {
        private GameObject _root;
        private MeshRenderer _renderer;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("AMUSE slot report");
            _renderer = _root.AddComponent<MeshRenderer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void SlotAnalysisRefusalReportsSlotIndexAndCause()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    2,
                    RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("2"),
                "the report must name the refused slot index");
            Assert.That(message, Does.Contain("AnimatedMaterialPropertyNotSingleton"),
                "the report must name the exact refusal cause");
        }

        [Test]
        public void SlotSeparationRefusalReportsSlotIndexAndCause()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    1,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("1"),
                "the report must name the refused slot index");
            Assert.That(message, Does.Contain("OpaqueCoverageBelowMinimum"),
                "the report must name the exact refusal cause");
        }

        [Test]
        public void TextureCaptureRefusalReportsSlotPropertyAndChannel()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(
                    _renderer,
                    3,
                    refusal));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("3"),
                "the report must name the refused slot index");
            Assert.That(message, Does.Contain("Alpha"),
                "the report must name the sampled channel");
        }

        [Test]
        public void SlotSeparationRefusalReportsRendererAndMaterial()
        {
            var material = NewFixtureMaterial("AMUSE reported material");
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.RuntimeMaterialValueNotMapped,
                    "AMUSE reported renderer",
                    material));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("AMUSE reported renderer"),
                "the report must name the renderer");
            Assert.That(message, Does.Contain("AMUSE reported material"),
                "the report must name the offending material");
        }

        private static Material NewFixtureMaterial(string name)
        {
            var material = new Material(Shader.Find("Unlit/Color"))
            {
                name = name,
            };
            return material;
        }

        [Test]
        public void EverySlotSeparationCauseHasPlainEnglishStrings()
        {
            foreach (AlphaSeparationSlotRefusal cause in Enum.GetValues(
                         typeof(AlphaSeparationSlotRefusal)))
            {
                if (cause == AlphaSeparationSlotRefusal.None)
                {
                    continue;
                }

                var key = AmuseReportStrings.SlotSeparationKey(cause);
                Assert.That(
                    AmuseReportStrings.Has(key), Is.True,
                    "missing title for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":description"), Is.True,
                    "missing description for " + cause);
                Assert.That(
                    AmuseReportStrings.Has(key + ":hint"), Is.True,
                    "missing hint for " + cause);
            }
        }
    }
}
