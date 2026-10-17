using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
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
    /// <para>
    /// Two fold fixtures drive the real preserve gate through the barrier,
    /// so the folded disclosures select their sentences by the recorded
    /// fold cause and not by one shared text.
    /// </para>
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
        public void ClosureRefusedSlotEntryCarriesTheWaySentence()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    1,
                    RendererAnalysisRefusal.MaterialDependencyClosureFailed,
                    rendererName: "Body",
                    closureWaySentence: AmuseReports.ClosureFailureSentence(
                        MaterialDependencyClosureFailure.InvalidSwapValue)));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("1"),
                "the report must name the refused slot index");
            Assert.That(message, Does.Contain("Body"),
                "the report must name the renderer");
            Assert.That(message, Does.Contain("MaterialDependencyClosureFailed"));
            Assert.That(message, Does.Contain("not"),
                "the way sentence must name the non-material swap, not the " +
                "bare refusal name");
        }

        [Test]
        public void PhantomClosureSlotEntryCarriesTheOutOfRangeWay()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    5,
                    RendererAnalysisRefusal.MaterialDependencyClosureFailed,
                    rendererName: "Body",
                    closureWaySentence: AmuseReports.ClosureFailureSentence(
                        MaterialDependencyClosureFailure.SlotOutOfRange)));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("5"),
                "the report must name the phantom slot index");
            Assert.That(message, Does.Contain("slot the renderer does not have"),
                "the way sentence must name the out-of-range binding");
        }

        [Test]
        public void SlotAnalysisRefusalEmbedsCaptureRefusalFacts()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    3,
                    RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton,
                    captureRefusals: new[] { refusal }));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("_MainTex"));
            Assert.That(message, Does.Contain("Alpha"));
            Assert.That(message, Does.Contain("UnavailableCapture"));
            Assert.That(message, Does.Contain("no source identity"));
        }

        [Test]
        public void SlotAnalysisRefusalEmbedsEveryCaptureRefusal()
        {
            var alpha = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);
            var red = new TextureCaptureRefusal(
                "_MaskTex",
                true,
                default,
                TextureChannel.Red,
                TextureCaptureRefusalReason.NonResidentMips);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotAnalysisRefusal(
                    _renderer,
                    1,
                    RendererAnalysisRefusal.AnimatedMaterialPropertyNotSingleton,
                    captureRefusals: new[] { alpha, red }));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("_MainTex"));
            Assert.That(message, Does.Contain("UnavailableCapture"));
            Assert.That(message, Does.Contain("no source identity"));
            Assert.That(message, Does.Contain("has a source identity"),
                "a fold that hardcodes one identity side loses facts");
            Assert.That(message, Does.Contain("_MaskTex"),
                "a fold that keeps only the first refusal loses facts");
            Assert.That(message, Does.Contain("NonResidentMips"));
        }

        [Test]
        public void UnsupportedFormatEntryNamesFormatAndTexture()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                true,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnsupportedFormat,
                formatName: "DXT1Crunched",
                textureName: "synthetic atlas copy");

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(
                    _renderer, 5, refusal));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("DXT1Crunched"));
            Assert.That(message, Does.Contain("synthetic atlas copy"));
        }

        [Test]
        public void TextureEntriesNeverClaimWhereTrianglesLanded()
        {
            var reasons = new[]
            {
                TextureCaptureRefusalReason.UnavailableCapture,
                TextureCaptureRefusalReason.NonResidentMips,
                TextureCaptureRefusalReason.UnsupportedFormat,
            };

            foreach (var reason in reasons)
            {
                var refusal = new TextureCaptureRefusal(
                    "_MainTex",
                    true,
                    default,
                    TextureChannel.Alpha,
                    reason,
                    formatName: "DXT1Crunched",
                    textureName: "synthetic atlas copy");

                var errors = ErrorReport.CaptureErrors(() =>
                    AmuseReports.TextureCaptureRefusal(
                        _renderer, 5, refusal));

                var message = errors[0].TheError.ToMessage();

                // A resolved slot's proof may not need the refused chain,
                // so the entry states the lost evidence, never the slot's
                // outcome. All three changed texture entries carry the
                // invariant.
                Assert.That(
                    message, Does.Not.Contain("stay on the original material"));
                Assert.That(
                    message, Does.Not.Contain("kept the affected triangles"));
                Assert.That(message, Does.Contain("cannot use this capture"));
            }
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
        public void AnisotropyDifferencesRefusalReportsInformationEntry()
        {
            var material = NewFixtureMaterial("AMUSE reported material");
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.AnisotropyDifferences,
                    "TestRenderer",
                    material));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo("amuse.slotSeparation.AnisotropyDifferences"));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("TestRenderer"));
            Assert.That(message, Does.Contain("0"));
            Assert.That(message, Does.Contain("anisotropic sampling"));
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
        public void TextureCaptureRefusal_StatesReasonAndMissingIdentity()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                false,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.UnavailableCapture);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(_renderer, 3, refusal));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("UnavailableCapture"),
                "the report must name the exact refusal reason family");
            Assert.That(message, Does.Contain("has no source identity"),
                "the report must state the identity fact");
        }

        [Test]
        public void TextureCaptureRefusal_StatesReasonAndPresentIdentity()
        {
            var refusal = new TextureCaptureRefusal(
                "_MainTex",
                true,
                default,
                TextureChannel.Alpha,
                TextureCaptureRefusalReason.NonResidentMips);

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.TextureCaptureRefusal(_renderer, 1, refusal));

            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("NonResidentMips"),
                "the report must name the exact refusal reason family");
            Assert.That(message, Does.Contain("has a source identity"),
                "the report must state the identity fact");
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

        [Test]
        public void SlotSeparationRefusalNamesTheRendererWithoutAnExplicitName()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // After play mode ends the build-copy renderer is destroyed
            // and the console's object link is dead, so the report text
            // is the only place that still names the renderer. NDMF
            // renders a null substitution as the literal "<missing>".
            Assert.That(message, Does.Contain("AMUSE slot report"),
                "the report must name the refused renderer");
            Assert.That(message, Does.Not.Contain("<missing>"),
                "a named renderer must never render the missing " +
                "placeholder");
        }

        [Test]
        public void SlotSeparationRefusalEmptyRendererNameRendersEmptyNotMissing()
        {
            _root.name = "";

            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    _renderer,
                    0,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: an implementation that treats an empty name as
            // a missing name prints the placeholder for a renderer that
            // has a name, however short. An empty string is not null.
            Assert.That(message, Does.Not.Contain("<missing>"));
        }

        [Test]
        public void SlotSeparationRefusalWithoutRendererKeepsTheMissingPlaceholder()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationRefusal(
                    null,
                    0,
                    AlphaSeparationSlotRefusal.OpaqueCoverageBelowMinimum));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();

            // Falsifier: the fallback must not throw on an absent
            // renderer, and the placeholder stays the truthful rendering
            // when no renderer exists to name.
            Assert.That(message, Does.Contain("<missing>"));
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

        private static CapturedAlphaMaterial OffenderMaterial(string name)
        {
            return new CapturedAlphaMaterial(
                CapturedAlphaMaterialFamily.Unsupported,
                new CapturedMaterialEvidence(
                    false, null, false, default(ColorSpace),
                    new CapturedMaterialEvidence.PresenceEntry[0],
                    new CapturedMaterialEvidence.ScalarEntry[0],
                    new CapturedMaterialEvidence.ColorEntry[0],
                    new CapturedMaterialEvidence.VectorEntry[0],
                    new CapturedMaterialEvidence.TextureEntry[0],
                    new CapturedTextureEvidence[0]),
                default(Alrauna.Amuse.Editor.Semantics.Poiyomi
                    .PoiyomiSourceEvidence),
                null,
                materialName: name);
        }

        [Test]
        public void SlotResolutionRefusedNamesSlotRendererAndMaterial()
        {
            var failures = new[]
            {
                AlphaResolutionFailure.UnsupportedMultiplier,
                AlphaResolutionFailure.MissingTextureEvidence,
                AlphaResolutionFailure.UnsupportedUvMapping,
            };

            foreach (var failure in failures)
            {
                var errors = ErrorReport.CaptureErrors(() =>
                    AmuseReports.SlotResolutionRefused(
                        _renderer,
                        1,
                        "Body",
                        OffenderMaterial("AMUSE refused material"),
                        failure));

                Assert.That(errors, Has.Count.EqualTo(1));
                var simple = errors[0].TheError as SimpleError;
                Assert.That(simple, Is.Not.Null);
                Assert.That(
                    simple.TitleKey,
                    Is.EqualTo(AmuseReportStrings.SlotResolutionKey(failure)),
                    "the entry must use the exact key for " + failure);
                var message = errors[0].TheError.ToMessage();
                Assert.That(message, Does.Contain("1"),
                    "the report must name the refused slot index");
                Assert.That(message, Does.Contain("Body"),
                    "the report must name the renderer");
                Assert.That(message, Does.Contain("AMUSE refused material"),
                    "the report must name the offender material");
            }
        }

        [Test]
        public void SlotResolutionRefusedBuildersThrowOnNoneAndSemanticsUnknown()
        {
            var offender = OffenderMaterial("AMUSE refused material");

            // None never names a fact. SemanticsUnknown promotes to a
            // slot refusal at ResolveSlot and never reaches this path.
            Assert.Throws<InvalidOperationException>(() =>
                AmuseReports.SlotResolutionRefused(
                    _renderer, 0, "Body", offender,
                    AlphaResolutionFailure.None));
            Assert.Throws<InvalidOperationException>(() =>
                AmuseReports.SlotResolutionRefused(
                    _renderer, 0, "Body", offender,
                    AlphaResolutionFailure.SemanticsUnknown));
        }

        [Test]
        public void TextureCaptureRefusalThrowsOnNoneCause()
        {
            // The record's constructor rejects None, but a default
            // record carries it. The report method is the last guard
            // before the dead key "amuse.texture.None" would render.
            Assert.That(
                () => AmuseReports.TextureCaptureRefusal(
                    _renderer, 0, default(TextureCaptureRefusal)),
                Throws.InvalidOperationException);
        }

        // Characterization: the two separation helper reporters and the
        // None guards on the two slot refusal reporters already exist.
        // These tests pin the current behavior; they are not RED-first.

        [Test]
        public void SlotSeparationDivergenceNamesKeySlotAndRenderer()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationDivergence(
                    _renderer, 4, "Body"));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo(AmuseReportStrings.SlotSeparationDivergenceKey),
                "the entry must use the exact divergence key");

            // Falsifier: an emitter that swaps the two arguments names
            // the renderer as the slot and the slot as the renderer.
            Assert.That(
                errors[0].TheError.ToMessage(),
                Does.Contain("slot 4 of renderer 'Body'"));
        }

        [Test]
        public void SlotSeparationPremultiplyNormalizationNamesKeySlotAndRenderer()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationPremultiplyNormalization(
                    _renderer, 2, "Body"));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo(
                    AmuseReportStrings
                        .SlotSeparationPremultiplyNormalizationKey),
                "the entry must use the exact premultiply key");

            // Falsifier: an emitter that swaps the two arguments names
            // the renderer as the slot and the slot as the renderer.
            Assert.That(
                errors[0].TheError.ToMessage(),
                Does.Contain("slot 2 of renderer 'Body'"));
        }

        [Test]
        public void SlotSeparationForcedOpaqueNamesRendererSlotCountCauseAndSetting()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationForcedOpaque(
                    _renderer, 3, 7, "Body"));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo(AmuseReportStrings.SlotSeparationForcedOpaqueKey),
                "the entry must use the exact forced-opaque key");

            // Falsifier: an emitter that swaps the two arguments names
            // the renderer as the slot and the slot as the renderer.
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("slot 3 of renderer 'Body'"),
                "the report must name the renderer and the slot");
            Assert.That(message, Does.Contain("7 unproven triangles"),
                "the report must name the exact unproven triangle count");
            Assert.That(
                message,
                Does.Contain("The moved triangles have no opacity proof."),
                "the report must state the forced premise");
            Assert.That(
                message,
                Does.Contain("Minimum Opaque Coverage to Preserve"),
                "the report must name the exact setting");

            // NDMF renders the hint outside ToMessage, so the actionable
            // guidance is checked through the formatted hint of the same
            // report entry.
            var hint = simple.FormatHint();
            Assert.That(hint, Is.Not.Null);
            Assert.That(
                hint,
                Does.Contain(
                    "Lower Minimum Opaque Coverage to Preserve on the " +
                    "AMUSE Avatar Optimizer component"),
                "the hint must name the setting and the component");
        }

        [Test]
        public void SlotSeparationForcedOpaqueRendersTheSingularCount()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationForcedOpaque(
                    _renderer, 0, 1, "Body"));

            Assert.That(errors, Has.Count.EqualTo(1));
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("1 unproven triangle of"),
                "the report must name the exact unproven triangle count");
            Assert.That(
                message, Does.Not.Contain("1 unproven triangles"),
                "the count phrase must read as plain English for one " +
                "triangle");
        }

        [Test]
        public void SlotSeparationPremultiplyNormalizationSelectsTheFoldedEntries()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationPremultiplyNormalization(
                    _renderer, 2, "Body", slotHasFoldedSubmeshes: true));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo(
                    AmuseReportStrings
                        .SlotSeparationPremultiplyNormalizationFoldedKey),
                "a folded slot must select the folded premultiply entries");

            // Falsifier: an emitter that swaps the two arguments names
            // the renderer as the slot and the slot as the renderer.
            var message = errors[0].TheError.ToMessage();
            Assert.That(message, Does.Contain("slot 2 of renderer 'Body'"));
            Assert.That(
                message, Does.Contain("Minimum Opaque Coverage to Preserve"),
                "the folded entry must name the preserve setting");
            Assert.That(
                message,
                Does.Not.Contain(
                    "The opaque material reproduces those colors exactly."),
                "a folded slot moved unproven triangles, so the entry " +
                "must not claim exact colors");
        }

        [Test]
        public void SlotSeparationPremultiplyNormalizationKeepsTheProvenEntries()
        {
            var errors = ErrorReport.CaptureErrors(() =>
                AmuseReports.SlotSeparationPremultiplyNormalization(
                    _renderer, 2, "Body"));

            Assert.That(errors, Has.Count.EqualTo(1));
            var simple = errors[0].TheError as SimpleError;
            Assert.That(simple, Is.Not.Null);
            Assert.That(
                simple.TitleKey,
                Is.EqualTo(
                    AmuseReportStrings
                        .SlotSeparationPremultiplyNormalizationKey),
                "a slot without a folded submesh keeps the proven entries");
            Assert.That(
                errors[0].TheError.ToMessage(),
                Does.Contain(
                    "The opaque material reproduces those colors exactly."),
                "a genuinely proven slot keeps the exact-color sentence");
        }

        [Test]
        public void SlotAnalysisRefusalThrowsOnNoneCause()
        {
            Assert.That(
                () => AmuseReports.SlotAnalysisRefusal(
                    _renderer, 0, RendererAnalysisRefusal.None),
                Throws.InvalidOperationException);
        }

        [Test]
        public void SlotSeparationRefusalThrowsOnNoneCause()
        {
            Assert.That(
                () => AmuseReports.SlotSeparationRefusal(
                    _renderer, 0, AlphaSeparationSlotRefusal.None),
                Throws.InvalidOperationException);
        }

        // --- Folded disclosure fixtures ---------------------------------

        private static SimpleError SelectForcedEntry(
            List<SimpleError> reports)
        {
            return reports.Single(e => e.ToMessage().Contains(
                "The moved triangles have no opacity proof."));
        }

        private static SimpleError SelectFoldedPremultiplyEntry(
            List<SimpleError> reports)
        {
            return reports.Single(e => e.ToMessage().Contains(
                "may differ from the original colors"));
        }

        /// <summary>
        /// One folded fixture run for the disclosure pin. The arm chooses
        /// the texture half, the recorded membership, and the coverage
        /// fields, so both fold tests share one builder. The run owns its
        /// transient objects and destroys them on dispose. The factory
        /// asserts the fixture preconditions, so a folded preparation is
        /// never silently absent from a disclosure pin.
        /// </summary>
        private sealed class FoldRun : IDisposable
        {
            private GameObject root;
            private Material material;
            private Mesh mesh;
            private IDisposable assets;
            private IDisposable filtering;

            internal AmusePlatformFinishState Amuse { get; private set; }

            internal List<SimpleError> Reports { get; private set; }

            internal static FoldRun CutoutArm()
            {
                var run = new FoldRun();
                run.assets = new OverrideTemporaryDirectoryScope(null);
                run.filtering = new AnisotropicFilteringPin();
                run.BuildRoot("AMUSE cutout fold report");
                run.SetCoverage(
                    ("_minimumOpaqueCoveragePercent", 0),
                    ("_minimumOpaqueCoverageToPreservePercent", 0),
                    ("_cutoutMinimumOpaqueCoveragePercent", 0),
                    ("_cutoutMinimumOpaqueCoverageToPreservePercent", 60));
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = ImportFoldTexture("cutout_fold_report", 0);
                    run.material = PoiyomiFixtureTestBase
                        .CreateVerifiedMaterial();
                    PoiyomiFixtureTestBase.ApplyConvertibleRenderState(
                        run.material);
                    run.material.SetFloat("_AlphaForceOpaque", 0f);
                    run.material.SetFloat("_MainAlphaMaskMode", 0f);
                    run.material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                    run.material.SetFloat("_Mode", 1f);
                    run.material.SetFloat("_Cutoff", 0.5f);
                    run.material.SetFloat("_AlphaPremultiply", 1f);
                    run.material.SetTexture("_MainTex", texture);
                    run.material.SetTextureScale("_MainTex", Vector2.one);
                    run.material.SetTextureOffset("_MainTex", Vector2.zero);
                    run.BuildRendererAndRun(FoldTwoTriangleMesh());
                }
                catch
                {
                    run.Dispose();
                    throw;
                }

                return run;
            }

            internal static FoldRun TransparentArm()
            {
                var run = new FoldRun();
                run.assets = new OverrideTemporaryDirectoryScope(null);
                run.filtering = new AnisotropicFilteringPin();
                run.BuildRoot("AMUSE transparent fold report");
                run.SetCoverage(
                    ("_minimumOpaqueCoveragePercent", 0),
                    ("_minimumOpaqueCoverageToPreservePercent", 60),
                    ("_cutoutMinimumOpaqueCoveragePercent", 0),
                    ("_cutoutMinimumOpaqueCoverageToPreservePercent", 0));
                AlphaSeparationSplitTests.EnsureSplitFolder();
                try
                {
                    var texture = ImportFoldTexture(
                        "transparent_fold_report", 200);
                    run.material = AlphaSeparationSplitTests
                        .SplitAlphaMaterial(texture);
                    run.material.SetFloat("_AlphaPremultiply", 1f);
                    run.BuildRendererAndRun(FoldTwoTriangleMesh());
                }
                catch
                {
                    run.Dispose();
                    throw;
                }

                return run;
            }

            private void BuildRoot(string name)
            {
                root = new GameObject(name);
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                FixtureProofScope.PinAllSizes(root);
                FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);
            }

            private void SetCoverage(
                params (string property, int percent)[] fields)
            {
                foreach (var field in fields)
                {
                    var optimizer = root.GetComponent<
                        Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                    var serialized = new SerializedObject(optimizer);
                    serialized.FindProperty(field.property).intValue =
                        field.percent;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            private void BuildRendererAndRun(Mesh builtMesh)
            {
                mesh = builtMesh;
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { material };

                AmusePlatformFinishState amuse = null;
                Reports = ErrorReport.CaptureErrors(
                    () => amuse = RunFoldBarrier(root))
                    .Select(e => e.TheError)
                    .OfType<SimpleError>()
                    .ToList();
                Amuse = amuse;
                Assert.That(Amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "fixture precondition: the fold fixture must refuse " +
                    "nothing");
                Assert.That(
                    Amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "fixture precondition: the renderer must be " +
                    "analyzable");
                Assert.That(Amuse.Separation, Is.Not.Null,
                    "fixture precondition: the slot must prepare under " +
                    "the fold");
                Assert.That(
                    Amuse.Separation.Renderers[0].Plan.Submeshes
                        .Single().Disposition,
                    Is.EqualTo(
                        SubmeshSeparationDisposition.WhollyOpaqueCandidate),
                    "fixture precondition: the arm's own preserve value " +
                    "must fold the split");
                foreach (AlphaSeparationSlotRefusal reason in
                         Enum.GetValues(
                             typeof(AlphaSeparationSlotRefusal)))
                {
                    if (reason == AlphaSeparationSlotRefusal.None)
                    {
                        continue;
                    }

                    Assert.That(
                        Amuse.SlotRefusalCount(reason), Is.Zero,
                        "fixture precondition: the folded slot must " +
                        "prepare with no refusal of any kind: " + reason);
                }
            }

            public void Dispose()
            {
                AlphaSeparationTestLifecycle.DestroyGenerated(Amuse);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                AlphaSeparationSplitTests.DeleteSplitFolder();
                filtering?.Dispose();
                assets?.Dispose();
            }
        }

        /// <summary>
        /// An 8x8 mip-free RGBA32 asset with Point/Clamp sampling. The
        /// left half carries full alpha. The right half carries
        /// <paramref name="rightAlpha"/>, so one recipe serves both proof
        /// arms: the cutout arm reads zero as a clean discarded half, and
        /// the exact arm reads 200 as an unproven translucent half.
        /// </summary>
        private static Texture2D ImportFoldTexture(
            string name,
            byte rightAlpha)
        {
            var pixels = new Color32[64];
            for (var y = 0; y < 8; y++)
            {
                for (var x = 0; x < 8; x++)
                {
                    pixels[y * 8 + x] = new Color32(
                        64, 32, 16, (byte)(x < 4 ? 255 : rightAlpha));
                }
            }

            return TestTextureImport.WritePng(
                AlphaSeparationSplitTests.SplitTempFolder + "/" + name +
                ".png",
                8,
                8,
                pixels,
                importer =>
                {
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                    importer.mipMapBias = 0f;
                    importer.anisoLevel = 1;
                    importer.streamingMipmaps = false;
                });
        }

        /// <summary>
        /// One submesh of two triangles. Ordinal 0 samples wholly inside
        /// the opaque left half, and ordinal 1 samples wholly inside the
        /// other half, so the submesh holds a 50 percent share under
        /// either arm's proof and folds at a preserve value of 60.
        /// </summary>
        private static Mesh FoldTwoTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                },
                uv = new[]
                {
                    new Vector2(0.1f, 0.1f),
                    new Vector2(0.4f, 0.1f),
                    new Vector2(0.1f, 0.4f),
                    new Vector2(0.6f, 0.6f),
                    new Vector2(0.9f, 0.6f),
                    new Vector2(0.6f, 0.9f),
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5 }, 0);
            return mesh;
        }

        // The barrier pieces mirror the preparation suite's runner. Every
        // suite that drives the barrier keeps one private copy, so this
        // class follows that shape instead of reaching into another
        // suite's private helpers.

        private static AmusePlatformFinishState RunFoldBarrier(
            GameObject root)
        {
            FixtureAvatarIdentity.RequireVrcDescriptorSupport();
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            var context = AvatarProcessor.ProcessAvatar(
                root, FoldReportPlatform.Instance);
            context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                GenericPlatformAnimatorBindings.Instance;
            AmuseStructuralGraphCheck.Execute(context);
            AmusePlatformFinishPass.Execute(
                context,
                FoldReportFacts(),
                VerifiedLilToonTestSeams.SelectVerifiedFixtureRequest,
                VerifiedLilToonTestSeams.CaptureVerifiedFixtureMaterials,
                VerifiedLilToonTestSeams.VerifiedAlphaOnly,
                VerifiedPoiyomiTestSeams.VerifiedConversion,
                VerifiedLilToonTestSeams.VerifiedConversion);
            return context.GetState<AmusePlatformFinishState>();
        }

        private static HostLifecycleFacts FoldReportFacts()
        {
            return new HostLifecycleFacts(
                "2022.3.22f1",
                "1.14.4",
                "3.10.4",
                "3.10.4",
                WellKnownPlatforms.VRChatAvatar30,
                AmuseBuildPath.NonPlayNdmfBuild,
                hasAssetSaver: true,
                hasAssetContainer: true,
                hasObjectRegistry: true,
                hasErrorReport: true);
        }

        private sealed class FoldReportPlatform : INDMFPlatformProvider
        {
            internal static readonly FoldReportPlatform Instance =
                new FoldReportPlatform();

            public string QualifiedName => "nadena.dev.ndmf.generic";
            public string DisplayName => "AMUSE slot report fold fixtures";
        }

        /// <summary>
        /// A fold whose cause is the Cutout submenu's own preserve value
        /// must select the cutout variants of both folded disclosures.
        /// The recorded membership names the cause, because two presets
        /// can honestly store equal percent values. An implementation
        /// that keeps one variant or that reads the cause off a percent
        /// comparison fails the key assertions.
        /// </summary>
        [Test]
        public void CutoutFoldReportsTheCutoutVariant()
        {
            SimpleError forcedEntry;
            SimpleError foldedEntry;
            using (var run = FoldRun.CutoutArm())
            {
                forcedEntry = SelectForcedEntry(run.Reports);
                foldedEntry = SelectFoldedPremultiplyEntry(run.Reports);
            }

            Assert.That(
                forcedEntry.TitleKey,
                Is.EqualTo("amuse.slotSeparation.ForcedOpaqueCutout"),
                "a fold caused by the cutout preserve value must select " +
                "the Cutout submenu's forced entries");
            Assert.That(
                forcedEntry.ToMessage(),
                Does.Contain("Cutout submenu"),
                "the cutout forced entry must reference the Cutout " +
                "submenu");
            Assert.That(
                forcedEntry.ToMessage(),
                Does.Contain("Minimum Opaque Coverage to Preserve"),
                "the cutout forced entry must name the preserve setting");
            Assert.That(
                forcedEntry.FormatHint(),
                Does.Contain("Cutout submenu"),
                "the cutout forced hint must point at the Cutout submenu");

            Assert.That(
                foldedEntry.TitleKey,
                Is.EqualTo(
                    "amuse.slotSeparation." +
                    "PremultiplyNormalizationFoldedCutout"),
                "a fold caused by the cutout preserve value must select " +
                "the Cutout submenu's folded premultiply entries");
            Assert.That(
                foldedEntry.ToMessage(),
                Does.Contain("Cutout submenu"),
                "the cutout folded entry must reference the Cutout " +
                "submenu");
            Assert.That(
                foldedEntry.ToMessage(),
                Does.Contain("Minimum Opaque Coverage to Preserve"),
                "the cutout folded entry must name the preserve setting");
            Assert.That(
                foldedEntry.ToMessage(),
                Does.Not.Contain(
                    "The opaque material reproduces those colors " +
                    "exactly."),
                "a folded slot moved unproven triangles, so the entry " +
                "must not claim exact colors");
            Assert.That(
                foldedEntry.FormatHint(),
                Does.Contain("Cutout submenu"),
                "the cutout folded hint must point at the Cutout submenu");
        }

        /// <summary>
        /// A fold whose cause is the transparent preserve value must
        /// render byte-identical base output on both folded disclosures.
        /// The key assertions, the base sentences, and the absent submenu
        /// reference pin the text. At the base commit one variant exists,
        /// so both arms render one text. The contrast assertions fail
        /// first on that single-variant state, and after the change they
        /// keep a hardcoded variant selection from silencing the pins
        /// above.
        /// </summary>
        [Test]
        public void TransparentFoldReportsByteIdenticalBaseOutput()
        {
            SimpleError transparentForced;
            SimpleError transparentFolded;
            SimpleError cutoutForced;
            SimpleError cutoutFolded;
            using (var transparentRun = FoldRun.TransparentArm())
            {
                transparentForced =
                    SelectForcedEntry(transparentRun.Reports);
                transparentFolded =
                    SelectFoldedPremultiplyEntry(transparentRun.Reports);
            }

            using (var cutoutRun = FoldRun.CutoutArm())
            {
                cutoutForced = SelectForcedEntry(cutoutRun.Reports);
                cutoutFolded = SelectFoldedPremultiplyEntry(cutoutRun.Reports);
            }

            Assert.That(
                transparentForced.TitleKey,
                Is.EqualTo("amuse.slotSeparation.ForcedOpaque"),
                "the transparent fold must keep the base forced key");
            Assert.That(
                transparentForced.ToMessage(),
                Does.Contain(
                    "sits below your Minimum Opaque Coverage to " +
                    "Preserve setting"),
                "the transparent fold must keep the base forced text");
            Assert.That(
                transparentForced.ToMessage(),
                Does.Not.Contain("Cutout"),
                "the transparent fold must not reference the Cutout " +
                "submenu");
            Assert.That(
                transparentForced.FormatHint(),
                Does.Contain(
                    "Lower Minimum Opaque Coverage to Preserve on the " +
                    "AMUSE Avatar Optimizer component"),
                "the transparent fold must keep the base forced hint");
            Assert.That(
                transparentForced.FormatHint(),
                Does.Not.Contain("Cutout"),
                "the transparent fold's hint must not reference the " +
                "Cutout submenu");

            Assert.That(
                transparentFolded.TitleKey,
                Is.EqualTo(
                    "amuse.slotSeparation.PremultiplyNormalizationFolded"),
                "the transparent fold must keep the base folded key");
            Assert.That(
                transparentFolded.ToMessage(),
                Does.Contain(
                    "sits below your Minimum Opaque Coverage to " +
                    "Preserve setting"),
                "the transparent fold must keep the base folded text");
            Assert.That(
                transparentFolded.ToMessage(),
                Does.Contain("may differ from the original colors"),
                "the transparent fold must keep the honest color premise");
            Assert.That(
                transparentFolded.ToMessage(),
                Does.Not.Contain("Cutout"),
                "the transparent fold must not reference the Cutout " +
                "submenu");
            Assert.That(
                transparentFolded.FormatHint(),
                Does.Not.Contain("Cutout"),
                "the transparent fold's hint must not reference the " +
                "Cutout submenu");

            Assert.That(
                transparentForced.ToMessage(),
                Is.Not.EqualTo(cutoutForced.ToMessage()),
                "the two arms' folds must render different texts");
            Assert.That(
                transparentFolded.ToMessage(),
                Is.Not.EqualTo(cutoutFolded.ToMessage()),
                "the two arms' folded premultiply texts must differ");
            Assert.That(
                transparentForced.FormatHint(),
                Is.Not.EqualTo(cutoutForced.FormatHint()),
                "the two arms' forced hints must differ");
        }
    }
}
