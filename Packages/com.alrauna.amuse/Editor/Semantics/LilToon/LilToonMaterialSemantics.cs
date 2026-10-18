using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using UnityEngine;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;
using static Alrauna.Amuse.Editor.Semantics.LilToon.LilToonUnknownRecord;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Semantic role a diagnostic is scoped to. The declared order is the
    /// deterministic diagnostic order: material-wide first, then each output.
    /// </summary>
    internal enum LilToonSemanticOutput
    {
        Material,
        Alpha,
    }

    /// <summary>
    /// Closed set of diagnostic reasons. A small fixed vocabulary, not a
    /// logging framework: no severities, no free-form categories. It is
    /// deliberately lilToon-specific; the Poiyomi frontend keeps its own.
    /// </summary>
    internal enum LilToonSemanticDiagnosticCode
    {
        UnsupportedShader,
        UnsupportedShaderVariant,
        UnsupportedVersion,
        ModifiedShaderSource,
        MissingSourceEvidence,
        MissingFeatureCompilation,
        UnsupportedFeature,
        UnsupportedUv,
        UnsupportedSampling,
        UnstableTextureIdentity,
        UnsupportedColorSpace,
        UnsupportedTextureImport,

        /// <summary>
        /// A supported vendor feature is active, and the rule for it is
        /// retention, not refusal. The property still names the fact, and the
        /// alpha answer stays Unknown because no triangle can move.
        /// </summary>
        FeatureRetention,
    }

    /// <summary>
    /// One deterministic reason that a material is unsupported or that an
    /// output is <c>Unknown</c>. Diagnostics are data; the frontend never
    /// writes the Unity Console.
    /// </summary>
    internal sealed class LilToonSemanticDiagnostic
    {
        internal LilToonSemanticOutput Output { get; }
        internal LilToonSemanticDiagnosticCode Code { get; }
        internal string Detail { get; }

        internal LilToonSemanticDiagnostic(
            LilToonSemanticOutput output,
            LilToonSemanticDiagnosticCode code,
            string detail)
        {
            Output = output;
            Code = code;
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }
    }

    /// <summary>
    /// Immutable outcome of interpreting one base material: whether the
    /// material's source identity is supported, the normalized semantics, and
    /// deterministic output-scoped diagnostics.
    /// </summary>
    internal sealed class LilToonSemanticResult
    {
        internal bool IsSupportedMaterial { get; }
        internal MaterialSemantics Semantics { get; }
        internal IReadOnlyList<LilToonSemanticDiagnostic> Diagnostics { get; }

        internal LilToonSemanticResult(
            bool isSupportedMaterial,
            MaterialSemantics semantics,
            IReadOnlyList<LilToonSemanticDiagnostic> diagnostics)
        {
            if (semantics == null)
            {
                throw new ArgumentNullException(nameof(semantics));
            }

            if (diagnostics == null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            IsSupportedMaterial = isSupportedMaterial;
            Semantics = semantics;

            var copy = new LilToonSemanticDiagnostic[diagnostics.Count];
            for (var i = 0; i < diagnostics.Count; i++)
            {
                copy[i] = diagnostics[i]
                    ?? throw new ArgumentException(
                        "Diagnostics must not contain null entries.",
                        nameof(diagnostics));
            }

            Diagnostics =
                new ReadOnlyCollection<LilToonSemanticDiagnostic>(copy);
        }
    }

    /// <summary>
    /// Conservative Editor-only interpreter for the canonical generated lilToon
    /// 2.3.4 base opaque shader. It attests source identity, then maps one
    /// supplied base <see cref="Material"/> into the immutable
    /// <see cref="MaterialSemantics"/> vocabulary. Behavior the attested source
    /// cannot prove is returned as an <c>Unknown</c> output with a deterministic
    /// diagnostic; it is never guessed, and additional uncertainty never widens
    /// a supported claim.
    /// </summary>
    internal static class LilToonMaterialSemantics
    {
        /// <summary>
        /// Analyzes the current values of one supplied base material. It does
        /// not assert that later animation, material swaps, modifier
        /// processing, or renderer overrides leave that state effective.
        /// </summary>
        internal static LilToonSemanticResult AnalyzeBaseMaterial(Material material)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, AlphaEvidenceRequest),
            })[0];
            var evidence = LilToonSourceAttestation.GatherSourceEvidence(
                material.shader, captured);
            if (!LilToonSourceAttestation.TryVerifyLilToonIdentity(
                    evidence, out var diagnostic))
            {
                return Unsupported(diagnostic);
            }

            return InterpretVerifiedMaterial(
                material,
                QualitySettings.activeColorSpace,
                evidence.CompiledFeatures,
                captured);
        }

        /// <summary>
        /// Narrow friend-test seam. The caller must already have established
        /// that the material's source identity is attested. The explicit color
        /// space and compiled-feature set are accepted for seam parity only;
        /// the alpha verdict is a function of the captured runtime gates, so
        /// deterministic tests can vary both and observe an identical alpha
        /// output without an installed lilToon package or a project-wide
        /// color-space change.
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures)
        {
            RequireAnalyzableMaterial(material);
            if (compiledFeatures == null)
            {
                throw new ArgumentNullException(nameof(compiledFeatures));
            }

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material, AlphaEvidenceRequest),
            })[0];
            return InterpretVerifiedMaterial(
                material,
                activeColorSpace,
                compiledFeatures,
                captured);
        }

        /// <summary>
        /// Interprets one captured material. The caller must have captured
        /// the evidence under <see cref="AlphaEvidenceRequest"/> and
        /// must have established admission upstream. The reads below consume
        /// only the captured snapshot. They never touch the live material. A
        /// captured read the evidence cannot answer refuses with a diagnostic
        /// that names the property.
        /// </summary>
        internal static LilToonSemanticResult InterpretVerifiedMaterialFromEvidence(
            CapturedMaterialEvidence captured)
        {
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            // A verified material is a supported material; the alpha output is
            // proven and stays Unknown, with a diagnostic, when its equation is
            // not representable.
            var diagnostics = new List<LilToonSemanticDiagnostic>();

            var alpha = InterpretAlpha(captured, diagnostics);

            return new LilToonSemanticResult(
                true,
                new MaterialSemantics(alpha),
                diagnostics);
        }

        private static LilToonSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            IReadOnlyCollection<string> compiledFeatures,
            CapturedMaterialEvidence captured)
        {
            // material is the capture's source. The interpreters read only the
            // captured snapshot, so a later mutation of the live material
            // cannot change an already-captured answer. activeColorSpace and
            // compiledFeatures are accepted for seam parity only; the alpha
            // verdict never reads either fact.
            _ = material;
            _ = activeColorSpace;
            _ = compiledFeatures;
            return InterpretVerifiedMaterialFromEvidence(captured);
        }

        // On LIL_RENDER 0 the alpha value is forced to exactly one after every
        // alpha-writing block (lil_pass_forward_normal.hlsl:393-396), and the
        // whole subpass alpha path is excluded by #if LIL_RENDER > 0. Only
        // mechanisms that remove fragments can still change effective coverage.
        // The vendor IDMask block drops fragments at runtime. An enabled mask
        // changes coverage without writing the alpha value, so every IDMask
        // input gates the proof like _Invisible does.
        private static readonly string[] AlphaCoverageGates =
        {
            "_Invisible",
            "_UDIMDiscardCompile",
            "_IDMask1",
            "_IDMask2",
            "_IDMask3",
            "_IDMask4",
            "_IDMask5",
            "_IDMask6",
            "_IDMask7",
            "_IDMask8",
            "_IDMaskControlsDissolve",
        };

        internal static MaterialEvidenceRequest AlphaEvidenceRequest { get; } =
            new MaterialEvidenceRequest(
                shaderName: true,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: new[]
                {
                    LilToonSourceAttestation.ShaderFormatVersionProperty,
                    "_Invisible",
                    "_UDIMDiscardCompile",
                    "_IDMask1",
                    "_IDMask2",
                    "_IDMask3",
                    "_IDMask4",
                    "_IDMask5",
                    "_IDMask6",
                    "_IDMask7",
                    "_IDMask8",
                    "_IDMaskControlsDissolve",
                },
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties:
                    Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// Proves the normalized alpha term. The attested opaque variant forces
        /// alpha to one, so the value is independent of <c>_Color.a</c>,
        /// <c>_MainTex</c> alpha, <c>_AlphaMaskMode</c>, <c>_Cutoff</c>, and
        /// <c>_UseDither</c>. Eleven coverage gates remain because they remove
        /// fragments rather than change the value.
        /// </summary>
        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var diagnostics = new List<LilToonSemanticDiagnostic>();
            var alpha = InterpretAlpha(evidence, diagnostics);
            unknownReason = AlphaUnknownReasonFor(diagnostics);
            return alpha;
        }

        /// <summary>
        /// The first Alpha-scoped diagnostic of one interpretation, as the
        /// reason the alpha answer is Unknown. The first failure is the
        /// reported one: the gates refuse at the first fact they cannot
        /// prove, so later diagnostics describe consequences, not causes.
        /// Null when the alpha answer is complete or no Alpha diagnostic
        /// was recorded.
        /// </summary>
        internal static AlphaUnknownReason AlphaUnknownReasonFor(
            IReadOnlyList<LilToonSemanticDiagnostic> diagnostics)
        {
            if (diagnostics == null)
            {
                return null;
            }

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Output != LilToonSemanticOutput.Alpha)
                {
                    continue;
                }

                return diagnostic.Code ==
                    LilToonSemanticDiagnosticCode.FeatureRetention
                    ? AlphaUnknownReason.FeatureRetention(
                          FeatureLabelFor(diagnostic.Detail),
                          diagnostic.Detail)
                    : AlphaUnknownReason.UnsupportedFeature(
                          FeatureLabelFor(diagnostic.Detail),
                          diagnostic.Detail);
            }

            return null;
        }

        /// <summary>
        /// The plain-words name of the shader feature one property carries,
        /// for the slot-refusal report. Keys are the exact property names
        /// the alpha gates record. A property without an entry reports its
        /// own name alone, which stays exact; the label is only wording.
        /// </summary>
        internal static string FeatureLabelFor(string property)
        {
            if (property == null)
            {
                return null;
            }

            if (property.StartsWith("_IDMask", StringComparison.Ordinal))
            {
                return "ID mask";
            }

            return FeatureLabels.TryGetValue(property, out var label)
                ? label
                : null;
        }

        private static readonly Dictionary<string, string> FeatureLabels =
            new(StringComparer.Ordinal)
            {
                ["_Invisible"] = "Invisible mode",
                ["_UDIMDiscardCompile"] = "UDIM discard",
                ["_UDIMDiscardMode"] = "UDIM discard",
                ["_ShiftBackfaceUV"] = "Backface UV shift",
                ["_UseParallax"] = "Parallax",
                ["_UseDither"] = "Dither",
                ["_IDMaskControlsDissolve"] = "ID mask dissolve control",
                ["_DissolveParams"] = "Dissolve",
                ["_MainTex_ScrollRotate"] = "UV scroll or rotate",
                ["_Main2ndTex_ScrollRotate"] =
                    "Second layer UV scroll or rotate",
                ["_Main3rdTex_ScrollRotate"] =
                    "Third layer UV scroll or rotate",
                ["_UseMain2ndTex"] = "Second layer",
                ["_UseMain3rdTex"] = "Third layer",
                ["_Main2ndDistanceFade"] = "Second layer distance fade",
                ["_Main3rdDistanceFade"] = "Third layer distance fade",
                ["_DistanceFade"] = "Distance fade",
                ["_Main2ndDissolveParams"] = "Second layer dissolve",
                ["_Main3rdDissolveParams"] = "Third layer dissolve",
                ["_AudioLink2Main2nd"] = "Audio Link on the second layer",
                ["_AudioLink2Main3rd"] = "Audio Link on the third layer",
                ["_Main2ndTexIsMSDF"] = "Second layer MSDF",
                ["_Main3rdTexIsMSDF"] = "Third layer MSDF",
                ["_Main2ndTexAngle"] = "Second layer angle",
                ["_Main3rdTexAngle"] = "Third layer angle",
                ["_AlphaMaskMode"] = "Alpha mask",
                ["_AlphaMaskValue"] = "Alpha mask",
                ["_AlphaMaskScale"] = "Alpha mask",
                ["_Cutoff"] = "Cutoff",
                ["_AlphaBoostFA"] = "Alpha boost",
                ["_SubpassCutoff"] = "Subpass cutoff",
                ["_MainTex_ST"] = "Main texture scale or offset",
                ["_MainTex"] = "Main texture",
            };

        private static SemanticOutput<ScalarSemanticValue> InterpretAlpha(
            CapturedMaterialEvidence evidence,
            List<LilToonSemanticDiagnostic> diagnostics)
        {
            var coverageGate = FirstFailedZeroGate(evidence, AlphaCoverageGates);
            if (coverageGate != null)
            {
                return RecordUnknown<ScalarSemanticValue>(
                    diagnostics,
                    LilToonSemanticOutput.Alpha,
                    LilToonSemanticDiagnosticCode.UnsupportedFeature,
                    coverageGate);
            }

            return SemanticOutput<ScalarSemanticValue>.Complete(
                ScalarSemanticValue.Constant(1f));
        }

        private static LilToonSemanticResult Unsupported(
            LilToonSemanticDiagnostic diagnostic)
        {
            return new LilToonSemanticResult(
                false,
                AllUnknown(),
                new[] { diagnostic });
        }
    }
}
