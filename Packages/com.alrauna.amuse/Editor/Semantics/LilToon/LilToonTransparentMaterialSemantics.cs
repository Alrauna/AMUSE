using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Alpha semantics for the attested regular no-outline lilToon 2.3.4
    /// transparent source (<c>Hidden/lilToonTransparent</c>, LIL_RENDER 2),
    /// per the transparent-to-opaque conversion design (§7, §8; T1 §9.1).
    /// The restricted theorem: with every cutout-shared coverage feature
    /// proven off and each transparent-only post-clip writer proven an
    /// identity at unit alpha — the ForwardAdd premultiply, the subpass
    /// shadow clip, and distance fade — the coverage of a triangle is the
    /// plain clip of the <c>_MainTex</c> alpha sample at identity UV0 times
    /// <c>_Color.a</c>, refused above <see cref="MaxProvableCutoff"/>.
    /// Everything below the interpretation is the shared
    /// <c>AlphaSemanticsResolver</c>; this type only decides whether the
    /// normalized value shape is representable. Behavior the captured
    /// evidence cannot prove stays <c>Unknown</c> with one deterministic
    /// diagnostic naming the offending property; it is never guessed.
    /// </summary>
    internal static class LilToonTransparentMaterialSemantics
    {
        private const string InvisibleProperty = "_Invisible";
        private const string UdimDiscardCompileProperty = "_UDIMDiscardCompile";
        private const string UdimDiscardModeProperty = "_UDIMDiscardMode";
        private const string ShiftBackfaceUvProperty = "_ShiftBackfaceUV";
        private const string UseParallaxProperty = "_UseParallax";
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
        private const string AlphaBoostFaProperty = "_AlphaBoostFA";
        private const string SubpassCutoffProperty = "_SubpassCutoff";

        /// <summary>
        /// The transparent clip bound (design §9 gate 12; T1 §9.2). The
        /// transparent forward site is a plain clip(fd.col.a - _Cutoff), not
        /// the cutout coverage transform, so the cutout twice-margin bound
        /// 0.9999 is deliberately NOT reused: for any finite c &lt;= 1 the
        /// exact difference 1 - c is nonnegative, round-to-nearest preserves
        /// that sign, and clip keeps the fragment. At c == 1 the difference is
        /// exactly zero, which clip keeps. Above 1 the difference is a nonzero
        /// negative well above the underflow threshold, and clip discards.
        /// The shared interpreter receives this bound as an input; it never
        /// derives it.
        /// </summary>
        private const float MaxProvableCutoff = 1f;

        /// <summary>
        /// The ForwardAdd premultiply lower bound (T1 §5.3). The base pass
        /// premultiply is fd.col.rgb *= fd.col.a, the identity at a = 1; the
        /// ForwardAdd pass instead applies saturate(fd.col.a *
        /// _AlphaBoostFA), which is the identity at a = 1 only when the boost
        /// saturates to at least one.
        /// </summary>
        private const float MinProvableAlphaBoostFa = 1f;

        /// <summary>
        /// The subpass shadow clip bound (T1 §9.4, measured). At a = 1 the
        /// dither sample returns 1 at all sixteen positions of the
        /// _DitherMaskLOD slice the alpha selects, so the shadow clip reduces
        /// to clip(1 - _SubpassCutoff) and keeps by the same sign-preservation
        /// argument as the forward cutoff.
        /// </summary>
        private const float MaxProvableSubpassCutoff = 1f;

        /// <summary>
        /// Every runtime gate that can change transparent coverage. With any
        /// of these active the coverage is not the plain clip of the main
        /// alpha sample (design §7 clause 2), so the interpretation refuses.
        /// <see cref="IdMaskControlsDissolveProperty"/> is the
        /// adversarial-review gate: with it set, the vertex IDMask path can
        /// force the sampled alpha chain to zero even at dissolve mode zero
        /// (B2 §3.3.8, §5 clause 2). <c>_UseDither</c> is deliberately not a
        /// gate here: LIL_RENDER 2 compiles the runtime dither path out
        /// entirely (T1 §6 row 16), so an authored toggle is inert. The gates
        /// are runtime captured facts — never the compiled feature set.
        /// </summary>
        private static readonly string[] AlphaCoverageGates =
        {
            InvisibleProperty,
            UdimDiscardCompileProperty,
            UdimDiscardModeProperty,
            ShiftBackfaceUvProperty,
            UseParallaxProperty,
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
        /// The two transparent-only post-clip writers as bounded gates, in
        /// pinned order, each bound this file's own pinned constant. The
        /// shared interpreter walks them after the cutoff check; the cutout
        /// family passes none, because its theorem has no post-clip writer.
        /// </summary>
        private static readonly LilToonAlphaGate[] TransparentOnlyGates =
        {
            // (5) The ForwardAdd premultiply is the identity at a = 1 only
            // when the boost saturates to at least one (T1 §5.3).
            LilToonAlphaGate.Minimum(
                AlphaBoostFaProperty, MinProvableAlphaBoostFa),

            // (6) The subpass shadow clip keeps at a = 1 by the same
            // sign-preservation argument as the forward cutoff, up to the
            // measured bound (T1 §9.4).
            LilToonAlphaGate.Maximum(
                SubpassCutoffProperty, MaxProvableSubpassCutoff),
        };

        /// <summary>
        /// The transparent alpha evidence request (design §8), built by the
        /// shared builder from its transparent delta table. Exact by
        /// contract: no fewer and no more; the contract doc lives on
        /// <see cref="LilToonAlphaEvidenceRequests.TransparentRequest"/>.
        /// </summary>
        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            LilToonAlphaEvidenceRequests.TransparentRequest();

        /// <summary>
        /// Interprets the alpha of evidence already admitted against
        /// <see cref="AlphaEvidenceRequest"/> (the verified seam; callers
        /// establish admission upstream, as for the opaque lilToon and
        /// Poiyomi frontends).
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue>
            InterpretVerifiedTransparentAlpha(
                CapturedMaterialEvidence evidence)
        {
            return InterpretVerifiedTransparentAlpha(evidence, out _);
        }

        internal static SemanticOutput<ScalarSemanticValue>
            InterpretVerifiedTransparentAlpha(
                CapturedMaterialEvidence evidence,
                out AlphaUnknownReason unknownReason)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var alpha = InterpretTransparentAlpha(evidence, diagnostics);
            unknownReason = LilToonMaterialSemantics
                .AlphaUnknownReasonFor(diagnostics);
            return alpha;
        }

        /// <summary>
        /// Verified-material seam mirroring
        /// <see cref="LilToonMaterialSemantics.InterpretVerifiedMaterial"/>
        /// so deterministic tests can exercise the transparent alpha on a
        /// material without vendor attestation. The transparent slice is an
        /// alpha-only frontend: every output other than Alpha stays
        /// <c>Unknown</c>.
        /// <para>
        /// <paramref name="activeColorSpace"/> and
        /// <paramref name="compiledFeatures"/> are accepted for seam parity
        /// only. The transparent verdict is a function of the captured
        /// runtime gates and never of color-space conversion or the compiled
        /// define set, so the invariance tests vary both and must observe an
        /// identical alpha output; no equation here reads either fact.
        /// </para>
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedTransparentMaterial(
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
            var alpha = InterpretTransparentAlpha(captured, diagnostics);

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
        /// Binds the transparent family inputs to the shared interpreter:
        /// this file's pinned cutoff bound, this file's coverage-gate array
        /// (with the dither membership), and this file's two pinned
        /// transparent-only bounded gates with their bounds.
        /// </summary>
        private static SemanticOutput<ScalarSemanticValue>
            InterpretTransparentAlpha(
                CapturedMaterialEvidence evidence,
                List<LilToonSemanticDiagnostic> diagnostics)
        {
            return LilToonAlphaInterpreter.Interpret(
                evidence,
                diagnostics,
                AlphaCoverageGates,
                MaxProvableCutoff,
                TransparentOnlyGates);
        }
    }
}
