using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    internal enum CapturedAlphaMaterialFamily
    {
        Unsupported,
        Poiyomi,
        LilToon,
    }

    internal sealed class CapturedAlphaMaterial
    {
        internal CapturedAlphaMaterialFamily Family { get; }
        internal CapturedMaterialEvidence Evidence { get; }
        internal PoiyomiSourceEvidence PoiyomiEvidence { get; }
        internal LilToonSourceEvidence LilToonEvidence { get; }

        internal CapturedAlphaMaterial(
            CapturedAlphaMaterialFamily family,
            CapturedMaterialEvidence evidence,
            PoiyomiSourceEvidence poiyomiEvidence,
            LilToonSourceEvidence lilToonEvidence)
        {
            if (!Enum.IsDefined(typeof(CapturedAlphaMaterialFamily), family))
            {
                throw new ArgumentOutOfRangeException(nameof(family));
            }

            Family = family;
            Evidence = evidence
                ?? throw new ArgumentNullException(nameof(evidence));
            PoiyomiEvidence = poiyomiEvidence;
            LilToonEvidence = lilToonEvidence;
        }
    }

    /// <summary>
    /// Selects the shader frontend for one base material. Each frontend attests
    /// its own source identity, and no material can be attested by both, so
    /// selection is an exclusive trial rather than a dispatch table: a second
    /// place deciding "is this a Poiyomi material" could only disagree with the
    /// first. This is deliberately not an adapter interface, a registry, or a
    /// provider framework; with a third family it becomes a third branch, and
    /// that is when a registry earns its first honest argument.
    /// </summary>
    internal static class UnityMaterialSemantics
    {
        private static readonly MaterialEvidenceRequest EmptyEvidenceRequest =
            new MaterialEvidenceRequest(
                shaderName: false,
                activeColorSpace: false,
                presenceProperties: Array.Empty<string>(),
                scalarProperties: Array.Empty<string>(),
                colorProperties: Array.Empty<string>(),
                vectorProperties: Array.Empty<string>(),
                textureProperties:
                    Array.Empty<TexturePropertyEvidenceRequest>());

        /// <summary>
        /// Analyzes the current values of one supplied base material. It makes
        /// no claim about later animation, material swaps, property blocks, or
        /// modifier processing. A material no frontend attests is all-Unknown,
        /// which is the conservative answer and never a refusal to answer.
        /// </summary>
        internal static MaterialSemantics AnalyzeBaseMaterial(Material material)
        {
            // The frontends throw for these; the correct answer is Unknown, and
            // an unassigned or destroyed slot is an ordinary input. Unity's
            // overloaded equality reports a destroyed object as null.
            if (material == null || material.shader == null)
            {
                return AllUnknown();
            }

            var poiyomi = PoiyomiMaterialSemantics.AnalyzeBaseMaterial(material);
            if (poiyomi.IsSupportedMaterial)
            {
                return poiyomi.Semantics;
            }

            // An unsupported lilToon result is itself all-Unknown, which is
            // exactly the answer for a material neither frontend attests.
            return LilToonMaterialSemantics
                .AnalyzeBaseMaterial(material)
                .Semantics;
        }

        internal static IReadOnlyList<CapturedAlphaMaterial> CaptureAlphaMaterials(
            IReadOnlyList<Material> materials)
        {
            if (materials == null)
            {
                throw new ArgumentNullException(nameof(materials));
            }

            var families = new CapturedAlphaMaterialFamily[materials.Count];
            var shaders = new Shader[materials.Count];
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                var material = materials[index];
                var request = EmptyEvidenceRequest;
                if (material != null && material.shader != null)
                {
                    shaders[index] = material.shader;
                    var shaderName = material.shader.name;
                    if (string.Equals(
                            shaderName,
                            PoiyomiMaterialSemantics.PoiyomiToonShaderName,
                            StringComparison.Ordinal))
                    {
                        families[index] = CapturedAlphaMaterialFamily.Poiyomi;
                        request = PoiyomiMaterialSemantics.AlphaEvidenceRequest;
                    }
                    else if (string.Equals(
                                 shaderName,
                                 LilToonSourceAttestation.SupportedShaderName,
                                 StringComparison.Ordinal))
                    {
                        families[index] = CapturedAlphaMaterialFamily.LilToon;
                        request = LilToonMaterialSemantics.AlphaEvidenceRequest;
                    }
                }

                inputs[index] = new MaterialEvidenceCaptureInput(
                    material, request);
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            var results = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < results.Length; index++)
            {
                var poiyomi = default(PoiyomiSourceEvidence);
                LilToonSourceEvidence lilToon = null;
                if (families[index] == CapturedAlphaMaterialFamily.Poiyomi)
                {
                    poiyomi = PoiyomiMaterialSemantics.GatherAlphaSourceEvidence(
                        shaders[index], evidence[index]);
                }
                else if (families[index] == CapturedAlphaMaterialFamily.LilToon)
                {
                    lilToon = LilToonSourceAttestation.GatherSourceEvidence(
                        shaders[index], evidence[index]);
                }

                results[index] = new CapturedAlphaMaterial(
                    families[index], evidence[index], poiyomi, lilToon);
            }

            return new ReadOnlyCollection<CapturedAlphaMaterial>(results);
        }

        internal static MaterialSemantics AnalyzeAlphaMaterial(
            CapturedAlphaMaterial captured)
        {
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            SemanticOutput<ScalarSemanticValue> alpha;
            switch (captured.Family)
            {
                case CapturedAlphaMaterialFamily.Poiyomi:
                    if (!PoiyomiMaterialSemantics.TryVerifyPoiyomiIdentity(
                            captured.PoiyomiEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence);
                    break;
                case CapturedAlphaMaterialFamily.LilToon:
                    if (captured.LilToonEvidence == null ||
                        !LilToonSourceAttestation.TryVerifyLilToonIdentity(
                            captured.LilToonEvidence, out _))
                    {
                        return AllUnknown();
                    }

                    alpha = LilToonMaterialSemantics.InterpretVerifiedAlpha(
                        captured.Evidence);
                    break;
                default:
                    return AllUnknown();
            }

            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                alpha,
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        internal static MaterialSemantics AllUnknown()
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Unknown(),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }
    }
}
