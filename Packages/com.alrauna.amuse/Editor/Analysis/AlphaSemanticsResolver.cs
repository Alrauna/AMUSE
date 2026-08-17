using System;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    /// <summary>
    /// Closed set of reasons a normalized Alpha value cannot be proven against
    /// the exact classifier. A refusal is material-scoped and yields no triangle
    /// outcome; it is deliberately distinct from the classifier's own
    /// per-triangle <see cref="TriangleAlphaOutcome.Unknown"/>.
    /// </summary>
    internal enum AlphaResolutionFailure
    {
        None,
        SemanticsUnknown,
        UnsupportedMultiplier,
        UnsupportedUvMapping,
        UnsupportedSampling,
        MissingTextureEvidence,
    }

    /// <summary>
    /// Host-supplied lookup of immutable, predicate-equivalent scalar evidence.
    /// It returns false unless the provider can prove, for the named source and
    /// channel over the relevant base-level texel domain in bottom-to-top order,
    /// that every effective per-texel scalar value is finite and within [0, 1],
    /// that byte 255 marks exactly the texels whose value is exactly 1, and that
    /// every other byte marks a value strictly below 1. Under Point or Bilinear
    /// sampling those facts give the classifier its predicate: the sampled value
    /// is 1 exactly when every positive-weight contributing texel is 255. The
    /// source need not itself be an uncompressed 8-bit b/255 field. The resolver
    /// never opens an asset.
    /// </summary>
    internal delegate bool AlphaFieldProvider(
        TextureSourceId source,
        TextureChannel channel,
        out AlphaTextureData field);

    /// <summary>
    /// One immutable decision about how a normalized Alpha semantic value may be
    /// proven: a uniform outcome that needs no geometry, an exact classifier
    /// configuration, or a named refusal that yields no outcome.
    /// </summary>
    internal sealed class AlphaResolution
    {
        private readonly bool _isUniform;
        private readonly TriangleAlphaOutcome _uniformOutcome;
        private readonly AlphaTextureData _field;
        private readonly AlphaSamplingSettings _sampling;

        private AlphaResolution(
            bool isResolved,
            AlphaResolutionFailure failure,
            bool isUniform,
            TriangleAlphaOutcome uniformOutcome,
            AlphaTextureData field,
            AlphaSamplingSettings sampling)
        {
            // Invariants: a resolved value carries no failure, a refusal
            // carries one, and a classified value always has its field.
            if (isResolved != (failure == AlphaResolutionFailure.None))
            {
                throw new ArgumentException(
                    "A resolution is resolved exactly when it has no failure.",
                    nameof(failure));
            }
            if (isResolved && !isUniform && field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            IsResolved = isResolved;
            Failure = failure;
            _isUniform = isUniform;
            _uniformOutcome = uniformOutcome;
            _field = field;
            _sampling = sampling;
        }

        internal bool IsResolved { get; }
        internal AlphaResolutionFailure Failure { get; }

        internal static AlphaResolution Refused(AlphaResolutionFailure failure)
        {
            return new AlphaResolution(
                false, failure, false, default, null, default);
        }

        internal static AlphaResolution Uniform(TriangleAlphaOutcome outcome)
        {
            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                true,
                outcome,
                null,
                default);
        }

        internal static AlphaResolution Classified(
            AlphaTextureData field,
            AlphaSamplingSettings sampling)
        {
            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                false,
                default,
                field,
                sampling);
        }

        /// <summary>
        /// Classifies one triangle under this resolution. A uniform resolution is
        /// independent of geometry and UV data and ignores the triangle: a
        /// constant alpha cannot vary across the surface. A refused resolution
        /// exposes no outcome at all.
        /// </summary>
        internal TriangleAlphaOutcome Classify(TriangleAlphaInput triangle)
        {
            if (!IsResolved)
            {
                throw new InvalidOperationException(
                    "A refused alpha resolution has no triangle outcome.");
            }

            return _isUniform
                ? _uniformOutcome
                : TriangleAlphaClassifier.Classify(triangle, _field, _sampling);
        }
    }

    /// <summary>
    /// Shader-independent bridge from one normalized Alpha semantic value to the
    /// existing exact triangle alpha classifier. It selects evidence and
    /// sampling, decides what can be concluded without evidence contents, and
    /// refuses everything else. It performs no arithmetic on evidence, reads no
    /// asset, and knows no shader, property, mesh, or render state.
    /// </summary>
    internal static class AlphaSemanticsResolver
    {
        internal static AlphaResolution Resolve(
            SemanticOutput<ScalarSemanticValue> alpha,
            AlphaFieldProvider fieldProvider)
        {
            if (fieldProvider == null)
            {
                throw new ArgumentNullException(nameof(fieldProvider));
            }

            if (!alpha.IsComplete)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.SemanticsUnknown);
            }

            var value = alpha.GetCompleteValue();
            switch (value.Kind)
            {
                case ScalarSemanticValueKind.Constant:
                    return ResolveScalar(value.GetConstantValue());
                case ScalarSemanticValueKind.TextureSample:
                    return ResolveSampled(
                        value.GetTextureSample(),
                        value.GetChannel(),
                        fieldProvider);
                case ScalarSemanticValueKind.TextureSampleTimesConstant:
                    return ResolveScaledSample(
                        value.GetTextureSample(),
                        value.GetChannel(),
                        value.GetMultiplier(),
                        fieldProvider);
                default:
                    // A semantic form added later must fail closed here rather
                    // than fall into a wrong proof path.
                    return AlphaResolution.Refused(
                        AlphaResolutionFailure.SemanticsUnknown);
            }
        }

        /// <summary>
        /// alpha = s * k, where the evidence contract bounds the sampled value s
        /// to [0, 1] and bilinear filtering, being a convex combination,
        /// preserves that bound. k == 1 leaves the classifier's own "s == 1"
        /// predicate intact. k &lt; 1 forces alpha &lt;= max(0, k) &lt; 1 at every
        /// reachable sample, so the answer needs the evidence's range attestation
        /// but not one byte of its contents. k &gt; 1 would require proving
        /// s == 1/k, which the classifier cannot express, and would leave alpha
        /// above one, whose opacity meaning the semantic model deliberately does
        /// not define.
        /// </summary>
        private static AlphaResolution ResolveScaledSample(
            TextureSample sample,
            TextureChannel channel,
            float multiplier,
            AlphaFieldProvider fieldProvider)
        {
            if (multiplier > 1f)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

            if (multiplier == 1f)
            {
                return ResolveSampled(sample, channel, fieldProvider);
            }

            if (!fieldProvider(sample.Source, channel, out var field) ||
                field == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            return AlphaResolution.Uniform(
                TriangleAlphaOutcome.MustRemainTransparent);
        }

        private static AlphaResolution ResolveSampled(
            TextureSample sample,
            TextureChannel channel,
            AlphaFieldProvider fieldProvider)
        {
            if (!IsSupportedMapping(sample.Coordinates))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedUvMapping);
            }

            if (!TryMapSampling(sample.Sampling, out var sampling))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedSampling);
            }

            if (!fieldProvider(sample.Source, channel, out var field) ||
                field == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            return AlphaResolution.Classified(field, sampling);
        }

        /// <summary>
        /// The classifier's exact domain is the hull of the UV values it is
        /// given; it has no transform input and takes one supplied UV set. Only
        /// the identity mapping on UV set 0 can therefore be expressed without
        /// either rounding the transform into float or handing a caller an
        /// unenforceable obligation about which mesh UV set to supply. Anything
        /// else fails closed. Supporting a transform later requires proving with
        /// exact dyadic/rational arithmetic that the affine result is
        /// representable by the supplied binary32 value; wider floating point is
        /// not such a proof.
        /// </summary>
        private static bool IsSupportedMapping(UvMapping mapping)
        {
            return mapping.Channel == 0 &&
                   mapping.Scale.x == 1f &&
                   mapping.Scale.y == 1f &&
                   mapping.Offset.x == 0f &&
                   mapping.Offset.y == 0f;
        }

        /// <summary>
        /// Exhaustive translation between the two deliberately separate closed
        /// sampling vocabularies. An undefined value is unreachable through the
        /// validating semantic constructors; the arm exists so a future semantic
        /// mode fails closed instead of falling into a wrong classifier mode.
        /// </summary>
        private static bool TryMapSampling(
            TextureSampling semantic,
            out AlphaSamplingSettings sampling)
        {
            sampling = default;

            AlphaFilterMode filter;
            switch (semantic.Filter)
            {
                case TextureFilterMode.Point:
                    filter = AlphaFilterMode.Point;
                    break;
                case TextureFilterMode.Bilinear:
                    filter = AlphaFilterMode.Bilinear;
                    break;
                default:
                    return false;
            }

            AlphaWrapMode wrap;
            switch (semantic.Wrap)
            {
                case TextureWrapMode.Clamp:
                    wrap = AlphaWrapMode.Clamp;
                    break;
                case TextureWrapMode.Repeat:
                    wrap = AlphaWrapMode.Repeat;
                    break;
                default:
                    return false;
            }

            sampling = new AlphaSamplingSettings(filter, wrap);
            return true;
        }

        /// <summary>
        /// The multiplier lemma for a value already known to lie in [0, 1]
        /// before scaling. Exactly one is opaque; anything below one can never
        /// reach one; anything above one has no defined opacity meaning because
        /// the semantic model states no clamp or saturate behavior.
        /// </summary>
        private static AlphaResolution ResolveScalar(float scalar)
        {
            if (scalar == 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.ProvenOpaque);
            }

            if (scalar < 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.MustRemainTransparent);
            }

            return AlphaResolution.Refused(
                AlphaResolutionFailure.UnsupportedMultiplier);
        }
    }
}
