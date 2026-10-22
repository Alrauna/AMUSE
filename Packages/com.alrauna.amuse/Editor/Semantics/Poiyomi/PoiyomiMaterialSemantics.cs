using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Alrauna.Amuse.Editor.Host;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Alrauna.Amuse.Editor.Semantics.EvidenceGates;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Conservative Editor-only interpreter for the canonical unlocked Poiyomi
    /// Toon Shader 9.3.64 source. It attests shader identity, then maps one
    /// supplied base <see cref="Material"/> into the immutable
    /// <see cref="MaterialSemantics"/> vocabulary. Behavior that the pinned
    /// source cannot prove is returned as an <c>Unknown</c> output with a
    /// deterministic diagnostic; it is never guessed, and additional
    /// uncertainty never widens a supported claim.
    /// </summary>
    internal static class PoiyomiMaterialSemantics
    {
        // --- Pinned canonical source identity (Poiyomi Toon Shader 9.3.64) ---
        // Tag commit e125e1c33cbfb860f59330799dd4d10a1097242d. See the design
        // doc's authoritative research basis before changing any constant.
        internal const string PoiyomiToonShaderName = PoiyomiSourceAttestation.PoiyomiToonShaderName;
        internal const string PoiyomiPackageName = PoiyomiSourceAttestation.PoiyomiPackageName;
        internal const string PoiyomiPackageVersion = PoiyomiSourceAttestation.PoiyomiPackageVersion;
        internal const string CanonicalShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid;
        internal const string CanonicalNormalizedSourceHash = PoiyomiSourceAttestation.CanonicalNormalizedSourceHash;
        internal const string PoiyomiTwoPassShaderName = PoiyomiSourceAttestation.PoiyomiTwoPassShaderName;
        internal const string TwoPassCanonicalShaderGuid = PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid;

        internal const string OutlineEnabledProperty =
            PoiyomiProperties.EnableOutlines;
        internal const string OutlineOverrideAlphaProperty =
            PoiyomiProperties.OutlineOverrideAlpha;
        internal const string LineColorProperty =
            PoiyomiProperties.LineColor;
        internal const string OutlineTextureProperty =
            PoiyomiProperties.OutlineTexture;
        internal const string OutlineTextureUvProperty =
            PoiyomiProperties.OutlineTextureUv;
        internal const string OutlineTexturePanProperty =
            PoiyomiProperties.OutlineTexturePan;

        // Minimum property schema the pinned source must expose. Later outputs
        // read more properties; each is added here as its interpreter needs it,
        // so a material missing a consumed property fails identity rather than
        // throwing during interpretation.
        private static readonly string[] RequiredSchemaProperties =
        {
            "shader_master_label",
            PoiyomiProperties.ShaderOptimizerEnabled,
            PoiyomiProperties.MainTex,
            PoiyomiProperties.Color,
            "_BumpMap",
            "_EmissionMap",
            "_EnableEmission",
            "_EnableEmission1",
            "_EnableEmission2",
            "_EnableEmission3",
        };

        // _MainTex sampling-mode flags whose enabled state changes sampling
        // beyond the single supported tap. Proven exactly off before a texture
        // sample is claimed for any output.
        private static readonly string[] MainSamplingModeGates =
        {
            "_MainPixelMode",
            "_MainTexStochastic",
        };

        // The four decal slots. An enabled slot writes the chain alpha only
        // inside its override-alpha block (pinned 9.3.64 source, vendor
        // lines 22958 to 22978), so a slot with the override-alpha mode
        // proven exactly zero composes color and emission and writes no
        // alpha. The enable float decides whether the block exists for the
        // slot, on the same float-as-evidence trust every other gate here
        // uses. Both name sets ride the alpha evidence request, so the
        // capture and the animation closure cover them.
        internal static MaterialEvidenceRequest AlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.AlphaEvidenceRequest;

        internal static MaterialEvidenceRequest PlainAlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.PlainAlphaEvidenceRequest;

        internal static MaterialEvidenceRequest TwoPassAlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.TwoPassAlphaEvidenceRequest;

        internal static MaterialEvidenceRequest PlainTwoPassAlphaEvidenceRequest =>
            PoiyomiAlphaInterpreter.PlainTwoPassAlphaEvidenceRequest;
        private static MaterialEvidenceRequest FullMaterialEvidenceRequest { get; } =
            MaterialEvidenceRequest.Combine(
                AlphaEvidenceRequest,
                new MaterialEvidenceRequest(
                    shaderName: false,
                    activeColorSpace: false,
                    presenceProperties: RequiredSchemaProperties,
                    scalarProperties: Array.Empty<string>(),
                    colorProperties: Array.Empty<string>(),
                    vectorProperties: Array.Empty<string>(),
                    textureProperties:
                        Array.Empty<TexturePropertyEvidenceRequest>()));

        internal static MaterialEvidenceRequest
            FullTwoPassMaterialEvidenceRequest { get; } =
            PoiyomiAlphaInterpreter.CreateTwoPassAlphaEvidenceRequest(FullMaterialEvidenceRequest);

        /// <summary>
        /// Analyzes the current values of one supplied base material. It does
        /// not assert that later animation, material swaps, modifier
        /// processing, or renderer overrides leave that state effective.
        /// </summary>
        internal static PoiyomiSemanticResult AnalyzeBaseMaterial(Material material)
        {
            RequireAnalyzableMaterial(material);

            var isTwoPass = material.shader != null &&
                string.Equals(
                    material.shader.name,
                    PoiyomiTwoPassShaderName,
                    StringComparison.Ordinal);

            var request = isTwoPass
                ? FullTwoPassMaterialEvidenceRequest
                : FullMaterialEvidenceRequest;

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    request,
                    AlphaPredicateRequestFor(material, isTwoPass)),
            })[0];
            var evidence = PoiyomiSourceAttestation.GatherSourceEvidence(
                material.shader, captured, RequiredSchemaProperties);
            if (!PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(evidence, out var diagnostic))
            {
                return Unsupported(diagnostic);
            }

            return InterpretVerifiedMaterial(
                material,
                QualitySettings.activeColorSpace,
                captured,
                isTwoPass);
        }

        /// <summary>
        /// Narrow friend-test seam. The caller must already have established
        /// that the material exposes the pinned property contract. The explicit
        /// color space is the resolved Unity fact required by the color
        /// equations; it lets deterministic tests exercise linear-light
        /// behavior without mutating the project's color-space setting.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    AlphaEvidenceRequest,
                    AlphaPredicateRequestFor(material, false)),
            })[0];
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured, false);
        }

        /// <summary>
        /// Narrow friend-test seam for the Two Pass family. Captures with the
        /// Two Pass request, so the second-family scalars are gathered, and
        /// interprets the alpha output the way the Two Pass family dispatch
        /// does. The other outputs read the material directly and do not
        /// depend on the request.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretVerifiedTwoPassMaterial(
            Material material,
            ColorSpace activeColorSpace)
        {
            RequireAnalyzableMaterial(material);

            var captured = UnityMaterialEvidenceCapture.Capture(new[]
            {
                new MaterialEvidenceCaptureInput(
                    material,
                    TwoPassAlphaEvidenceRequest,
                    AlphaPredicateRequestFor(material, true)),
            })[0];
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured, true);
        }

        /// <summary>
        /// Narrow friend-test seam over an already captured evidence: the
        /// caller owns the capture and its capture predicate, so the
        /// interpretation and its diagnostics answer exactly the evidence
        /// the caller hands in. The Two Pass family keeps its own entry
        /// point; this one always interprets the plain family.
        /// </summary>
        internal static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            CapturedMaterialEvidence captured)
        {
            RequireAnalyzableMaterial(material);
            return InterpretVerifiedMaterial(
                material, activeColorSpace, captured, false);
        }

        private static PoiyomiSemanticResult InterpretVerifiedMaterial(
            Material material,
            ColorSpace activeColorSpace,
            CapturedMaterialEvidence captured,
            bool interpretSecondAlphaFamily)
        {
            // A verified material is a supported material; each output is proven
            // independently and stays Unknown, with a diagnostic, when its
            // equation is not representable.
            var diagnostics = new List<PoiyomiSemanticDiagnostic>();

            var alpha = PoiyomiAlphaInterpreter.InterpretAlpha(
                captured, interpretSecondAlphaFamily, diagnostics);

            var semantics = new MaterialSemantics(alpha);
            return new PoiyomiSemanticResult(true, semantics, diagnostics);
        }

        internal static bool TryInterpretMainSample(
            CapturedMaterialEvidence evidence,
            PoiyomiSemanticOutput output,
            bool requireColorInterpretation,
            List<PoiyomiSemanticDiagnostic> diagnostics,
            out TextureSample sample,
            out TextureColorInterpretation interpretation)
        {
            sample = default;
            interpretation = default;

            if (!TryGetSupportedUvMapping(evidence, out var mapping))
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedUv,
                    PoiyomiProperties.MainTexUv);
                return false;
            }

            var failedGate = FirstFailedZeroGate(
                evidence, MainSamplingModeGates);
            if (failedGate != null)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                    failedGate);
                return false;
            }

            if (!evidence.TryGetTexture(
                    PoiyomiProperties.MainTex, out var assignment) ||
                !assignment.IsAssigned ||
                assignment.Texture == null ||
                !assignment.Texture.HasSourceIdentity)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnstableTextureIdentity,
                    PoiyomiProperties.MainTex);
                return false;
            }

            if (!assignment.Texture.HasSampling)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedSampling,
                    PoiyomiProperties.MainTex);
                return false;
            }

            if (requireColorInterpretation &&
                !assignment.Texture.HasColorInterpretation)
            {
                AddDiagnostic(
                    diagnostics,
                    output,
                    PoiyomiSemanticDiagnosticCode.UnsupportedTextureImport,
                    PoiyomiProperties.MainTex);
                return false;
            }

            interpretation = assignment.Texture.ColorInterpretation;
            sample = new TextureSample(
                assignment.Texture.SourceIdentity,
                mapping,
                assignment.Texture.Sampling);
            return true;
        }

        private static bool TryGetSupportedUvMapping(
            CapturedMaterialEvidence evidence,
            out UvMapping mapping)
        {
            mapping = default;
            if (!evidence.TryGetScalar(
                    PoiyomiProperties.MainTexUv, out var rawChannel) ||
                !IsFinite(rawChannel))
            {
                return false;
            }

            var channel = Mathf.RoundToInt(rawChannel);
            if (channel < 0 || channel > 3 || channel != rawChannel)
            {
                return false;
            }

            if (!evidence.TryGetVector(PoiyomiProperties.MainTexPan, out var pan) ||
                !IsFinite(pan) ||
                pan.x != 0f || pan.y != 0f || pan.z != 0f || pan.w != 0f)
            {
                return false;
            }

            if (!evidence.TryGetTexture(
                    PoiyomiProperties.MainTex, out var assignment) ||
                !assignment.HasScaleOffset ||
                !IsFinite(assignment.Scale) ||
                !IsFinite(assignment.Offset))
            {
                return false;
            }

            mapping = new UvMapping(
                channel, assignment.Scale, assignment.Offset);
            return true;
        }

        // --- Alpha equation (Task 4) ----------------------------------------

        /// <summary>
        /// Whether the material's own family runs the cutout preset on either
        /// family. The first family branches on <c>_Mode</c>, the second on
        /// <c>_ModeTwoPass</c> (note 4.4, Two Pass source line 68110). A
        /// missing or non-finite selector answers false: the material keeps
        /// the plain-clip rules, and the field-predicate agreement gate keeps
        /// a cutoff-binarized field unprovable, so an unknown preset never
        /// widens a claim. This read decides the capture predicate; the
        /// interpretation re-reads the captured scalars, so the two sides
        /// agree by construction.
        /// </summary>
        internal static bool DeclaresCutoutPreset(Material material)
        {
            return ReadsCutoutPreset(material, PoiyomiProperties.Mode) ||
                ReadsCutoutPreset(material, PoiyomiProperties.TwoPassMode);
        }

        private static bool ReadsCutoutPreset(
            Material material,
            string modeProperty)
        {
            if (material == null || !material.HasProperty(modeProperty))
            {
                return false;
            }

            var mode = material.GetFloat(modeProperty);
            return IsFinite(mode) && mode == 1f;
        }

        /// <summary>
        /// The capture predicate one Poiyomi material's own alpha capture
        /// runs under. A cutout preset selects the declaring request, whose
        /// main texture entry binarizes the alpha field by the _Cutoff value:
        /// that binarized field is the split route. Every other preset
        /// selects the plain-clip variant, which keeps the exact-255 field
        /// the exact-one rule reads. Both variants carry one schema, so the
        /// closed batch's union stays single and the choice is a per-material
        /// capture fact, never a second family schema.
        /// </summary>
        internal static MaterialEvidenceRequest AlphaPredicateRequestFor(
            Material material,
            bool secondAlphaFamily) =>
            PoiyomiAlphaInterpreter.AlphaPredicateRequestFor(material, secondAlphaFamily);

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason) =>
            PoiyomiAlphaInterpreter.InterpretVerifiedAlpha(evidence, out unknownReason);

        internal static SemanticOutput<ScalarSemanticValue> InterpretVerifiedTwoPassAlpha(
            CapturedMaterialEvidence evidence,
            out AlphaUnknownReason unknownReason) =>
            PoiyomiAlphaInterpreter.InterpretVerifiedTwoPassAlpha(evidence, out unknownReason);

        internal static string FeatureLabelFor(string property) =>
            PoiyomiAlphaInterpreter.FeatureLabelFor(property);

        internal static AlphaUnknownReason AlphaUnknownReasonFor(
            IReadOnlyList<PoiyomiSemanticDiagnostic> diagnostics) =>
            PoiyomiAlphaInterpreter.AlphaUnknownReasonFor(diagnostics);

        internal static SemanticOutput<T> RecordUnknown<T>(
            List<PoiyomiSemanticDiagnostic> diagnostics,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
            where T : class
        {
            AddDiagnostic(diagnostics, output, code, detail);
            return SemanticOutput<T>.Unknown();
        }

        internal static void AddDiagnostic(
            List<PoiyomiSemanticDiagnostic> diagnostics,
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            diagnostics.Add(
                new PoiyomiSemanticDiagnostic(output, code, detail));
        }

        // --- Texture evidence extraction (Task 3) ---------------------------

        /// <summary>
        /// Converts a texture property's UV channel and scale/offset into a
        /// <see cref="UvMapping"/> when they fit the supported form: an exact
        /// integer channel 0-3, exactly zero pan, and finite scale/offset.
        /// </summary>
        internal static bool TryGetSupportedUvMapping(
            Material material,
            string textureProperty,
            string uvChannelProperty,
            string panProperty,
            out UvMapping mapping)
        {
            mapping = default;

            if (!material.HasProperty(uvChannelProperty))
            {
                return false;
            }

            var rawChannel = material.GetFloat(uvChannelProperty);
            if (!IsFinite(rawChannel))
            {
                return false;
            }

            var channel = Mathf.RoundToInt(rawChannel);
            if (channel < 0 || channel > 3 || channel != rawChannel)
            {
                return false;
            }

            if (!material.HasProperty(panProperty))
            {
                return false;
            }

            var pan = material.GetVector(panProperty);
            if (!IsFinite(pan) ||
                pan.x != 0f || pan.y != 0f || pan.z != 0f || pan.w != 0f)
            {
                return false;
            }

            var scale = material.GetTextureScale(textureProperty);
            var offset = material.GetTextureOffset(textureProperty);
            if (!IsFinite(scale) || !IsFinite(offset))
            {
                return false;
            }

            mapping = new UvMapping(channel, scale, offset);
            return true;
        }


        /// <summary>
        /// Extracts the sampler declared by <c>_MainTex</c>, which the pinned
        /// shader uses for every assigned sample. Supported only for Point or
        /// Bilinear filtering with equal Clamp/Repeat wrap and no mipmapped,
        /// mip-biased, or anisotropic sampling. A missing <c>_MainTex</c> yields
        /// no sampler; the implicit white sampler is never promoted to a guess.
        /// </summary>
        internal static bool TryGetMainTextureSampling(
            Material material,
            out TextureSampling sampling)
        {
            // "The sampler always comes from _MainTex" is Poiyomi-specific
            // knowledge and stays here; only the texture-level fact is shared.
            var mainTexture = material.HasProperty(PoiyomiProperties.MainTex)
                ? material.GetTexture(PoiyomiProperties.MainTex)
                : null;
            return UnityTextureEvidence.TryGetSampling(mainTexture, out sampling);
        }


        internal static PoiyomiSemanticDiagnostic MaterialDiagnostic(
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            return new PoiyomiSemanticDiagnostic(
                PoiyomiSemanticOutput.Material,
                code,
                detail);
        }

        private static PoiyomiSemanticResult Unsupported(
            PoiyomiSemanticDiagnostic diagnostic)
        {
            return new PoiyomiSemanticResult(
                false,
                AllUnknown(),
                new[] { diagnostic });
        }

    }

    /// <summary>
    /// Immutable outcome of interpreting one base material: whether the
    /// material's source identity is supported, the normalized semantics, and
    /// deterministic output-scoped diagnostics.
    /// </summary>
    internal sealed class PoiyomiSemanticResult
    {
        internal bool IsSupportedMaterial { get; }
        internal MaterialSemantics Semantics { get; }
        internal IReadOnlyList<PoiyomiSemanticDiagnostic> Diagnostics { get; }

        internal PoiyomiSemanticResult(
            bool isSupportedMaterial,
            MaterialSemantics semantics,
            IReadOnlyList<PoiyomiSemanticDiagnostic> diagnostics)
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

            var copy = new PoiyomiSemanticDiagnostic[diagnostics.Count];
            for (var i = 0; i < diagnostics.Count; i++)
            {
                copy[i] = diagnostics[i]
                    ?? throw new ArgumentException(
                        "Diagnostics must not contain null entries.",
                        nameof(diagnostics));
            }

            Diagnostics = new ReadOnlyCollection<PoiyomiSemanticDiagnostic>(copy);
        }
    }

    /// <summary>
    /// One deterministic reason that a material is unsupported or that an output
    /// is <c>Unknown</c>. Diagnostics are data; the adapter never writes the
    /// Unity Console.
    /// </summary>
    internal sealed class PoiyomiSemanticDiagnostic
    {
        internal PoiyomiSemanticOutput Output { get; }
        internal PoiyomiSemanticDiagnosticCode Code { get; }
        internal string Detail { get; }

        internal PoiyomiSemanticDiagnostic(
            PoiyomiSemanticOutput output,
            PoiyomiSemanticDiagnosticCode code,
            string detail)
        {
            Output = output;
            Code = code;
            Detail = detail ?? throw new ArgumentNullException(nameof(detail));
        }
    }

    /// <summary>
    /// Semantic role a diagnostic is scoped to. The declared order is the
    /// deterministic diagnostic order: material-wide first, then each output.
    /// </summary>
    internal enum PoiyomiSemanticOutput
    {
        Material,
        Alpha,
    }

    /// <summary>
    /// Closed set of diagnostic reasons. This is a small fixed vocabulary, not
    /// a logging framework: no severities, no free-form categories.
    /// </summary>
    internal enum PoiyomiSemanticDiagnosticCode
    {
        UnsupportedShader,
        UnsupportedVersion,
        ModifiedShaderSource,
        MissingSourceEvidence,
        UnsupportedFeature,
        UnsupportedUv,
        UnsupportedSampling,
        UnstableTextureIdentity,
        UnsupportedColorSpace,
        UnsupportedTextureImport,
    }
}
