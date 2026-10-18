using System;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Analysis
{
    internal static class AlphaSemanticsResolver
    {
        internal static AlphaResolution Resolve(
            SemanticOutput<ScalarSemanticValue> alpha,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
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
                        fieldProvider,
                        samplingPolicy,
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.TextureSampleTimesConstant:
                    return ResolveScaledSample(
                        value.GetTextureSample(),
                        value.GetChannel(),
                        value.GetMultiplier(),
                        fieldProvider,
                        samplingPolicy,
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.MappedTextureSample:
                    return ResolveMappedSample(
                        value.GetTextureSample(),
                        value.GetChannel(),
                        value.GetMap(),
                        fieldProvider,
                        samplingPolicy,
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.ProductChainOfTextureSamples:
                    return ResolveProductChain(
                        value, fieldProvider, samplingPolicy,
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.SaturatingSum:
                    return ResolveSaturatingSum(
                        value, fieldProvider, samplingPolicy,
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.SaturatingDifference:
                    return ResolveSaturatingDifference(
                        value, fieldProvider, samplingPolicy,
                        maxNoiseTexelPercent);
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
        /// <para>
        /// The evidence contract bounds the sampled value to [0, 1] at
        /// <em>every</em> level of the chain, so the bound holds whichever level
        /// the hardware selects: the lemma is strengthened, not weakened, and still
        /// needs no byte of the contents.
        /// </para>
        /// </summary>
        private static AlphaResolution ResolveScaledSample(
            TextureSample sample,
            TextureChannel channel,
            float multiplier,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            if (float.IsNaN(multiplier))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

            if (multiplier > 1f)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

            if (multiplier == 1f)
            {
                return ResolveSampled(
                    sample, channel, fieldProvider, samplingPolicy,
                    maxNoiseTexelPercent);
            }

            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            return AlphaResolution.Uniform(
                TriangleAlphaOutcome.MustRemainTransparent);
        }

        /// <summary>
        /// alpha = saturate(s * scale + value) over a sampled field bounded
        /// in [0, 1] by the contract. The map's endpoint evaluations decide
        /// two uniform arms with no texel: a lower bound of one at both red
        /// bounds proves the term one at every reachable sample, and an
        /// upper bound below one at both proves the opposite. Everything
        /// else classifies the chain under the map.
        /// </summary>
        private static AlphaResolution ResolveMappedSample(
            TextureSample sample,
            TextureChannel channel,
            AffineAlphaMap map,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            var atZero = map.Evaluate(0f);
            var atOne = map.Evaluate(1f);
            if (atZero.Lower == 1f && atOne.Lower == 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.ProvenOpaque);
            }
            if (atZero.Upper < 1f && atOne.Upper < 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.MustRemainTransparent);
            }

            // The uniform arms above are coordinate-independent, but the
            // classified arm feeds `Classified`, which by contract rejects
            // mappings the proof does not carry. Refuse by name, as the
            // plain sample path does, instead of letting the factory throw.
            if (!IsSupportedMapping(sample.Coordinates))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedUvMapping);
            }

            return AlphaResolution.Classified(
                chain,
                samplingPolicy.ForProof(sample.Sampling),
                sample.Coordinates,
                maxNoiseTexelPercent,
                map);
        }

        /// <summary>
        /// alpha = (k * f0) * f1 * ... * fn over any number of sampled terms
        /// bounded in [0,1] by the field contract. The multiplier lemmas are
        /// the two-factor ones generalized to any arity: a product of values
        /// in [0,1] rounds to one only when every factor is one, so a leading
        /// constant below one keeps the product below one everywhere, and a
        /// constant of one makes the product's predicate the conjunction of
        /// the per-factor predicates.
        /// </summary>
        private static AlphaResolution ResolveProductChain(
            ScalarSemanticValue value,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            var multiplier = value.GetProductMultiplier();
            if (float.IsNaN(multiplier))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

            if (multiplier > 1f)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

            if (multiplier < 1f)
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.MustRemainTransparent);
            }

            AlphaResolution conjoined = null;
            for (var index = 0; index < value.GetChainFactorCount(); index++)
            {
                var map = value.GetChainMap(index);
                var factor = map == null
                    ? ResolveSampled(
                        value.GetChainSample(index),
                        value.GetChainChannel(index),
                        fieldProvider,
                        samplingPolicy,
                        maxNoiseTexelPercent)
                    : ResolveMappedSample(
                        value.GetChainSample(index),
                        value.GetChainChannel(index),
                        map.Value,
                        fieldProvider,
                        samplingPolicy,
                        maxNoiseTexelPercent);
                if (!factor.IsResolved)
                {
                    return factor;
                }

                if (factor.TryGetUniformOutcome(out var uniform))
                {
                    if (uniform == TriangleAlphaOutcome.MustRemainTransparent)
                    {
                        return AlphaResolution.Uniform(
                            TriangleAlphaOutcome.MustRemainTransparent);
                    }

                    // A uniformly opaque factor multiplies by exactly one
                    // and drops out of the conjunction.
                    continue;
                }

                conjoined = conjoined == null
                    ? factor
                    : AlphaResolution.Product(conjoined, factor);
            }

            return conjoined ?? AlphaResolution.Uniform(
                TriangleAlphaOutcome.ProvenOpaque);
        }

        /// <summary>
        /// alpha = saturate(first + second). Each side is bounded in [0,1]
        /// by the field contract, so the sum reaches one whenever the sides
        /// add to at least one. Complementary masks with sub-one witnesses
        /// at different texels sum to identically one. A side attested
        /// exactly one decides the sum alone, because the saturate clamps
        /// at one. Two constants fold through the saturate. Every other
        /// composition answers Unknown: a side proven below one says
        /// nothing about the sum, and cross-field correlation between two
        /// sampled terms is unknowable without an additive classifier.
        /// </summary>
        private static AlphaResolution ResolveSaturatingSum(
            ScalarSemanticValue value,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            var first = value.GetSumFirst();
            var second = value.GetSumSecond();
            if (first.Kind == ScalarSemanticValueKind.Constant &&
                second.Kind == ScalarSemanticValueKind.Constant)
            {
                var sum = first.GetConstantValue() + second.GetConstantValue();
                return ResolveScalar(sum > 1f ? 1f : sum);
            }

            var firstResolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(first),
                fieldProvider, samplingPolicy, maxNoiseTexelPercent);
            if (!firstResolution.IsResolved)
            {
                return firstResolution;
            }

            var secondResolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(second),
                fieldProvider, samplingPolicy, maxNoiseTexelPercent);
            if (!secondResolution.IsResolved)
            {
                return secondResolution;
            }

            if (IsUniformlyProvenOpaque(firstResolution))
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.ProvenOpaque);
            }

            if (IsUniformlyProvenOpaque(secondResolution))
            {
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.ProvenOpaque);
            }

            var firstTransparent = firstResolution.TryGetUniformOutcome(
                out var firstOutcome) &&
                firstOutcome == TriangleAlphaOutcome.MustRemainTransparent;
            var secondTransparent = secondResolution.TryGetUniformOutcome(
                out var secondOutcome) &&
                secondOutcome == TriangleAlphaOutcome.MustRemainTransparent;
            if (firstTransparent || secondTransparent)
            {
                // No common-point additive argument exists. A sub-one witness
                // for one factor says nothing about the sum at that texel.
                return AlphaResolution.Uniform(
                    TriangleAlphaOutcome.Unknown);
            }

            return AlphaResolution.Or(firstResolution, secondResolution);
        }

        /// <summary>
        /// alpha = saturate(minuend - subtrahend). A zero constant subtrahend
        /// preserves the minuend exactly. Two constants fold through the
        /// saturate. Everything else stays conservative: m - s reaches one
        /// only at m = 1 and s = 0, the field contract attests "strictly
        /// below one" and never "exactly zero", so a sampled subtrahend can
        /// never be proven away.
        /// </summary>
        private static AlphaResolution ResolveSaturatingDifference(
            ScalarSemanticValue value,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            var minuend = value.GetMinuend();
            var subtrahend = value.GetSubtrahend();
            if (minuend.Kind == ScalarSemanticValueKind.Constant &&
                subtrahend.Kind == ScalarSemanticValueKind.Constant)
            {
                var difference =
                    minuend.GetConstantValue() - subtrahend.GetConstantValue();
                return ResolveScalar(difference < 0f ? 0f : difference);
            }

            if (IsProvenExactlyZero(subtrahend))
            {
                // saturate(m - 0) is m: a zero constant, or a sampled term
                // scaled by exactly zero, is exactly zero at every reachable
                // sample because the field contract bounds the sample finite
                // in [0,1] and fl(0 * s) is zero for every finite s.
                return AlphaSemanticsResolver.Resolve(
                    SemanticOutput<ScalarSemanticValue>.Complete(minuend),
                    fieldProvider, samplingPolicy, maxNoiseTexelPercent);
            }

            var subtrahendResolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(subtrahend),
                fieldProvider, samplingPolicy, maxNoiseTexelPercent);
            if (!subtrahendResolution.IsResolved)
            {
                return subtrahendResolution;
            }

            return AlphaResolution.Uniform(
                TriangleAlphaOutcome.MustRemainTransparent);
        }

        private static bool IsUniformlyProvenOpaque(AlphaResolution resolution)
        {
            return resolution.TryGetUniformOutcome(out var outcome) &&
                outcome == TriangleAlphaOutcome.ProvenOpaque;
        }

        /// <summary>
        /// A closed form whose value is exactly zero at every reachable
        /// sample: the zero constant, or a sampled term scaled by exactly
        /// zero. The field contract bounds the sampled factor finite in
        /// [0,1], and a binary32 multiply of zero by any finite value is
        /// exactly zero, so the lemma needs no texel.
        /// </summary>
        private static bool IsProvenExactlyZero(ScalarSemanticValue value)
        {
            if (value.Kind == ScalarSemanticValueKind.Constant)
            {
                return value.GetConstantValue() == 0f;
            }

            return value.Kind == ScalarSemanticValueKind
                .TextureSampleTimesConstant &&
                value.GetMultiplier() == 0f;
        }

        private static AlphaResolution ResolveSampled(
            TextureSample sample,
            TextureChannel channel,
            AlphaFieldProvider fieldProvider,
            AlphaSamplingPolicy samplingPolicy,
            int maxNoiseTexelPercent)
        {
            if (!IsSupportedMapping(sample.Coordinates))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedUvMapping);
            }

            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            return AlphaResolution.Classified(
                chain,
                samplingPolicy.ForProof(sample.Sampling),
                sample.Coordinates,
                maxNoiseTexelPercent);
        }

        /// <summary>
        /// The resolver admits the four mesh UV channels carried by the
        /// triangle input. The frontend must prove the mapping form before it
        /// reaches this boundary.
        /// </summary>
        private static bool IsSupportedMapping(UvMapping mapping)
        {
            // Channels one to three are the stage B layer channels: the mesh
            // extraction carries them and the classifier selects the
            // mapping's channel directly. Channel four is the view-dependent
            // matcap coordinate and stays refused.
            return mapping.Channel >= 0 && mapping.Channel <= 3;
        }

        /// <summary>
        /// The multiplier lemma for a value already known to lie in [0, 1]
        /// before scaling. Exactly one is opaque; anything below one can never
        /// reach one; anything above one has no defined opacity meaning because
        /// the semantic model states no clamp or saturate behavior. A NaN
        /// multiplier refuses as unsupported in every arm. NaN fails every
        /// comparison here, so the arms must test it by name and refuse.
        /// </summary>
        private static AlphaResolution ResolveScalar(float scalar)
        {
            if (float.IsNaN(scalar))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

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
