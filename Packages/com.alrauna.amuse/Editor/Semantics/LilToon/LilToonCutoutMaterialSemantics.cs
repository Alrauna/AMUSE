using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Alpha semantics for the attested regular no-outline lilToon 2.3.4
    /// cutout source (<c>Hidden/lilToonCutout</c>, LIL_RENDER 1), per the
    /// cutout-to-opaque conversion design (spec §8). The restricted theorem:
    /// with every optional alpha/coverage feature proven off, the coverage of
    /// a triangle is the cutout transform of the plain <c>_MainTex</c> alpha
    /// sample at identity UV0 times <c>_Color.a</c>, refused above
    /// <see cref="MaxProvableCutoff"/>. Everything below the interpretation is
    /// the shared <c>AlphaSemanticsResolver</c>; this type only decides
    /// whether the normalized value shape is representable. Behavior the
    /// captured evidence cannot prove stays <c>Unknown</c> with one
    /// deterministic diagnostic naming the offending property; it is never
    /// guessed.
    /// </summary>
    internal static class LilToonCutoutMaterialSemantics
    {
        private const string InvisibleProperty = "_Invisible";
        private const string UdimDiscardCompileProperty = "_UDIMDiscardCompile";
        private const string UdimDiscardModeProperty = "_UDIMDiscardMode";
        private const string ShiftBackfaceUvProperty = "_ShiftBackfaceUV";
        private const string UseParallaxProperty = "_UseParallax";
        private const string UseDitherProperty = "_UseDither";
        private const string IdMask1Property = "_IDMask1";
        private const string IdMask2Property = "_IDMask2";
        private const string IdMask3Property = "_IDMask3";
        private const string IdMask4Property = "_IDMask4";
        private const string IdMask5Property = "_IDMask5";
        private const string IdMask6Property = "_IDMask6";
        private const string IdMask7Property = "_IDMask7";
        private const string IdMask8Property = "_IDMask8";
        private const string IdMaskControlsDissolveProperty =
            "_IDMaskControlsDissolve";

        /// <summary>
        /// The controller-fixed twice-margin cutoff bound (spec §8.1 clause 2
        /// and §9.3 gate 12; B2 §3.4). At or below it the shadow and forward
        /// clip <c>1 - c</c> keeps every fully covered fragment; above it no
        /// triangle is provable, so the classification layer refuses before
        /// any triangle is called proven. A non-finite cutoff fails the
        /// finite check first. The shared interpreter receives this bound as
        /// an input; it never derives it.
        /// </summary>
        private const float MaxProvableCutoff = 0.9999f;

        /// <summary>
        /// Every runtime gate that can change cutout coverage. With any of
        /// these active the coverage is not the plain cutout transform of the
        /// main alpha sample (spec §8.1 clause 2), so the interpretation
        /// refuses. <see cref="IdMaskControlsDissolveProperty"/> is the
        /// adversarial-review gate: with it set, the vertex IDMask path can
        /// force the sampled alpha chain to zero even at dissolve mode zero
        /// (B2 §3.3.8, §5 clause 2). <c>_UseDither</c> is a gate here, unlike
        /// the transparent family, whose render mode compiles the runtime
        /// dither path out entirely. The gates are runtime captured facts —
        /// never the compiled feature set.
        /// </summary>
        private static readonly string[] AlphaCoverageGates =
        {
            InvisibleProperty,
            UdimDiscardCompileProperty,
            UdimDiscardModeProperty,
            ShiftBackfaceUvProperty,
            UseParallaxProperty,
            UseDitherProperty,
            IdMask1Property,
            IdMask2Property,
            IdMask3Property,
            IdMask4Property,
            IdMask5Property,
            IdMask6Property,
            IdMask7Property,
            IdMask8Property,
            IdMaskControlsDissolveProperty,
        };

        /// <summary>
        /// The cutout alpha evidence request (spec §8.2), built by the
        /// shared builder from its cutout delta table. Exact by contract: no
        /// fewer and no more; the contract doc lives on
        /// <see cref="LilToonAlphaEvidenceRequests.CutoutRequest"/>.
        /// </summary>
        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            LilToonAlphaEvidenceRequests.CutoutRequest();

        /// <summary>
        /// Interprets the alpha of evidence already admitted against
        /// <see cref="AlphaEvidenceRequest"/> (the verified seam; callers
        /// establish admission upstream, as for the opaque lilToon and
        /// Poiyomi frontends).
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue>
            InterpretVerifiedCutoutAlpha(CapturedMaterialEvidence evidence)
        {
            return InterpretVerifiedCutoutAlpha(evidence, out _);
        }

        internal static SemanticOutput<ScalarSemanticValue>
            InterpretVerifiedCutoutAlpha(
                CapturedMaterialEvidence evidence,
                out AlphaUnknownReason unknownReason)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var alpha = InterpretCutoutAlpha(evidence, diagnostics);
            unknownReason = LilToonMaterialSemantics
                .AlphaUnknownReasonFor(diagnostics);
            return alpha;
        }

        /// <summary>
        /// Verified-material seam mirroring
        /// <see cref="LilToonMaterialSemantics.InterpretVerifiedMaterial"/>
        /// so deterministic tests can exercise the cutout alpha on a material
        /// without vendor attestation. The cutout slice is an alpha-only
        /// frontend: every output other than Alpha stays <c>Unknown</c>.
        /// <para>
        /// <paramref name="activeColorSpace"/> and
        /// <paramref name="compiledFeatures"/> are accepted for seam parity
        /// only. The cutout verdict is a function of the captured runtime
        /// gates and never of color-space conversion or the compiled define
        /// set, so the invariance tests vary both and must observe an
        /// identical alpha output; no equation here reads either fact.
        /// </para>
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedCutoutMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures)
        {
            RequireAnalyzableMaterial(material);
            if (compiledFeatures == null)
            {
                throw new ArgumentNullException(nameof(compiledFeatures));
            }

            _ = activeColorSpace;

            // Deliberately unread; see the doc comment above.
            _ = compiledFeatures;

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(material, AlphaEvidenceRequest),
            })[0];

            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var alpha = InterpretCutoutAlpha(captured, diagnostics);

            return new LilToonSemanticResult(
                true,
                new MaterialSemantics(
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    alpha,
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<NormalSemanticValue>.Unknown()),
                diagnostics);
        }

        /// <summary>
        /// Binds the cutout family inputs to the shared interpreter: this
        /// file's pinned cutoff bound, this file's coverage-gate array (with
        /// the dither membership), and no transparent-only bounded gate.
        /// </summary>
        private static SemanticOutput<ScalarSemanticValue> InterpretCutoutAlpha(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            return LilToonAlphaInterpreter.Interpret(
                evidence,
                diagnostics,
                AlphaCoverageGates,
                MaxProvableCutoff,
                Array.Empty<LilToonAlphaGate>(),
                requiresExactMainField: false);
        }
    }
}
