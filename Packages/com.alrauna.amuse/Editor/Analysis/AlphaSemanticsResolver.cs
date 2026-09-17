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
        MissingTextureEvidence,
    }

    /// <summary>
    /// Host-supplied lookup of immutable, predicate-equivalent scalar evidence.
    /// It returns false unless the provider can prove, for the named source and
    /// channel and for <em>every level of the returned chain</em> over that
    /// level's texel domain in bottom-to-top order, that every effective per-texel
    /// scalar value is finite and within [0, 1], that byte 255 marks exactly the
    /// texels whose value is exactly 1, and that every other byte marks a value
    /// strictly below 1. Under Point or Bilinear sampling those facts give the
    /// classifier its predicate: the sampled value is 1 exactly when every
    /// positive-weight contributing texel is 255. The source need not itself be an
    /// uncompressed 8-bit b/255 field. The resolver never opens an asset.
    /// <para>
    /// It further attests that the chain is the source's <strong>complete declared
    /// mip chain</strong>, mip 0 first. The hardware may select any level for a
    /// given fragment and the resolver cannot know which, so an incomplete chain
    /// would let an unexamined level escape the proof.
    /// <see cref="AlphaMipChain"/> validates shape only and cannot check
    /// completeness; the provider owns it.
    /// </para>
    /// </summary>
    internal delegate bool AlphaFieldProvider(
        TextureSourceId source,
        TextureChannel channel,
        out AlphaMipChain chain);

    /// <summary>
    /// One immutable decision about how a normalized Alpha semantic value may be
    /// proven: a uniform outcome that needs no geometry, an exact classifier
    /// configuration, or a named refusal that yields no outcome.
    /// </summary>
    internal sealed class AlphaResolution
    {
        private readonly bool _isUniform;
        private readonly TriangleAlphaOutcome _uniformOutcome;
        private readonly AlphaMipChain _chain;
        private readonly AlphaSamplingSettings _sampling;
        // Only a classified resolution consults texture contents, so only it
        // carries the noise density percent. Uniform, product, and refused
        // resolutions store zero and ignore it: a constant alpha is not
        // texture noise, and a product's factors each carry their own.
        private readonly int _maxNoiseTexelPercent;
        private readonly UvMapping _mapping;
        private readonly AlphaResolution _firstFactor;
        private readonly AlphaResolution _secondFactor;
        private readonly bool _isProduct;
        private readonly bool _isDisjunction;

        private AlphaResolution(
            bool isResolved,
            AlphaResolutionFailure failure,
            bool isUniform,
            TriangleAlphaOutcome uniformOutcome,
            AlphaMipChain chain,
            AlphaSamplingSettings sampling,
            int maxNoiseTexelPercent,
            UvMapping mapping,
            AlphaResolution firstFactor,
            AlphaResolution secondFactor,
            bool isProduct,
            bool isDisjunction = false)
        {
            // Invariants: a resolved value carries no failure, a refusal
            // carries one, and a classified value always has its field.
            if (isResolved != (failure == AlphaResolutionFailure.None))
            {
                throw new ArgumentException(
                    "A resolution is resolved exactly when it has no failure.",
                    nameof(failure));
            }
            if (isResolved && !isUniform && !isProduct && chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            IsResolved = isResolved;
            Failure = failure;
            _isUniform = isUniform;
            _uniformOutcome = uniformOutcome;
            _chain = chain;
            _sampling = sampling;
            _maxNoiseTexelPercent = maxNoiseTexelPercent;
            _mapping = mapping;
            _firstFactor = firstFactor;
            _secondFactor = secondFactor;
            _isProduct = isProduct;
            _isDisjunction = isDisjunction;
        }

        internal bool IsResolved { get; }
        internal AlphaResolutionFailure Failure { get; }

        internal static AlphaResolution Refused(AlphaResolutionFailure failure)
        {
            return new AlphaResolution(
                false, failure, false, default, null, default, 0, default,
                null, null, false);
        }

        internal static AlphaResolution Uniform(TriangleAlphaOutcome outcome)
        {
            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                true,
                outcome,
                null,
                default,
                0,
                default,
                null,
                null,
                false);
        }

        internal static AlphaResolution Classified(
            AlphaMipChain chain,
            AlphaSamplingSettings sampling,
            UvMapping mapping,
            int maxNoiseTexelPercent)
        {
            // The resolver accepts the mesh UV channels carried by
            // `TriangleAlphaInput`. Enforce that boundary here so a direct
            // caller cannot make classification read a coordinate set that
            // the proof input does not represent.
            if (mapping.Channel < 0 || mapping.Channel > 3)
            {
                throw new ArgumentException(
                    "A classified resolution's mapping must name a mesh " +
                    "UV channel the proof carries (0 to 3).",
                    nameof(mapping));
            }

            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                false,
                default,
                chain,
                sampling,
                maxNoiseTexelPercent,
                mapping,
                null,
                null,
                false);
        }

        /// <summary>
        /// The conjunction of two independently classified factors. The
        /// factors keep their own fields, samplings, and mappings, so each
        /// one classifies the triangle under its own coordinate transform.
        /// The fold is the absorbing outcome lattice:
        /// <c>MustRemainTransparent</c> wins over everything,
        /// <c>Unknown</c> wins over <c>ProvenOpaque</c>, and
        /// <c>ProvenOpaque</c> needs both factors proven.
        /// <para>
        /// The exactness argument is the float product lemma: both factors
        /// are bounded in [0, 1] by the field contract, and a product of
        /// such values rounds to one only when both factors are one, so
        /// per-triangle conjunction of the per-factor predicates is the
        /// product's predicate.
        /// </para>
        /// <para>
        /// Both factors must be classified, never uniform: the resolver
        /// resolves constant factors before it constructs a product, and a
        /// uniform factor folded in here would hide that decision inside
        /// an opaque composite. A composite exposes no uniform outcome, so
        /// it merges with nothing in deduplication, like every classified
        /// resolution.
        /// </para>
        /// </summary>
        internal static AlphaResolution Product(
            AlphaResolution first,
            AlphaResolution second)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));
            if (!first.IsResolved || !second.IsResolved ||
                first.TryGetUniformOutcome(out _) ||
                second.TryGetUniformOutcome(out _))
            {
                throw new ArgumentException(
                    "A product composes two classified resolutions.");
            }

            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                false,
                default,
                null,
                default,
                0,
                default,
                first,
                second,
                true);
        }


        /// <summary>
        /// The disjunction of two independently classified factors, the dual
        /// of <see cref="Product"/>: the sum saturate(a + b) reaches exactly
        /// one on a triangle exactly when either factor is one there, because
        /// each factor is bounded in [0,1] by the field contract. The fold
        /// mirrors the product's absorbing lattice: ProvenOpaque wins over
        /// Unknown, Unknown wins over MustRemainTransparent, and
        /// MustRemainTransparent needs both factors non-opaque.
        /// <para>
        /// Both factors must be classified, never uniform: the resolver
        /// resolves uniform factors before it constructs a disjunction.
        /// </para>
        /// </summary>
        internal static AlphaResolution Or(
            AlphaResolution first,
            AlphaResolution second)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));
            if (!first.IsResolved || !second.IsResolved ||
                first.TryGetUniformOutcome(out _) ||
                second.TryGetUniformOutcome(out _))
            {
                throw new ArgumentException(
                    "A disjunction composes two classified resolutions.");
            }

            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                false,
                default,
                null,
                default,
                0,
                default,
                first,
                second,
                true,
                true);
        }

        /// <summary>
        /// Reports the stored uniform outcome, if this resolution has one.
        /// <para>
        /// This exposes an existing immutable fact so a consumer can recognize
        /// the uniform case exactly. It is deliberately the whole of that
        /// surface: the field, the sampling settings, and any general notion of
        /// "kind" stay private, and the type still has no equality of its own.
        /// A refused resolution and a classified one both answer <c>false</c>;
        /// a caller that must tell those two apart already has
        /// <see cref="IsResolved"/> and <see cref="Failure"/>.
        /// </para>
        /// <para>
        /// The alternative — inferring uniformity from what
        /// <see cref="Classify"/> returns for some triangle — is unsound, not
        /// merely indirect. A classified resolution can return the same outcome
        /// as a uniform one for any finite set of sampled triangles while
        /// disagreeing elsewhere, so a consumer relying on that inference would
        /// treat a varying resolution as constant. In the deduplication
        /// consumer that is an over-merge, which shrinks a later intersection
        /// without proof.
        /// </para>
        /// </summary>
        internal bool TryGetUniformOutcome(out TriangleAlphaOutcome outcome)
        {
            if (IsResolved && _isUniform)
            {
                outcome = _uniformOutcome;
                return true;
            }

            // Not `default`. `TriangleAlphaOutcome.ProvenOpaque` is the zero
            // value, so defaulting would hand a caller who ignored the bool the
            // least conservative answer in the lattice — from a method whose
            // entire purpose is soundness. `Unknown` fails closed instead.
            outcome = TriangleAlphaOutcome.Unknown;
            return false;
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

            if (_isUniform)
            {
                return _uniformOutcome;
            }

            if (_isProduct && !_isDisjunction)
            {
                var first = _firstFactor.Classify(triangle);
                if (first == TriangleAlphaOutcome.MustRemainTransparent)
                {
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }

                var second = _secondFactor.Classify(triangle);
                if (second == TriangleAlphaOutcome.MustRemainTransparent)
                {
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }

                return first == TriangleAlphaOutcome.ProvenOpaque &&
                       second == TriangleAlphaOutcome.ProvenOpaque
                    ? TriangleAlphaOutcome.ProvenOpaque
                    : TriangleAlphaOutcome.Unknown;
            }

            if (_isDisjunction)
            {
                var first = _firstFactor.Classify(triangle);
                if (first == TriangleAlphaOutcome.ProvenOpaque)
                {
                    return TriangleAlphaOutcome.ProvenOpaque;
                }

                var second = _secondFactor.Classify(triangle);
                if (second == TriangleAlphaOutcome.ProvenOpaque)
                {
                    return TriangleAlphaOutcome.ProvenOpaque;
                }

                return first == TriangleAlphaOutcome
                           .MustRemainTransparent &&
                       second == TriangleAlphaOutcome
                           .MustRemainTransparent
                    ? TriangleAlphaOutcome.MustRemainTransparent
                    : TriangleAlphaOutcome.Unknown;
            }


            // A mip chain is alternative evidence about one configuration, not a
            // set of admitted configurations: the hardware may select any level and
            // AMUSE cannot know which, so one non-opaque level refutes the proof.
            // MustRemainTransparent is absorbing, so returning on it cannot change
            // the result. Unknown must NOT exit early - a later level may be
            // MustRemainTransparent, which outranks it. A level flagged without
            // evidence - a non-resident consulted mip - refutes the proof for every
            // triangle exactly as an Unknown verdict would, whatever its placeholder
            // grid contains, so its provenance is consulted before the grid.
            // Identity remains on the historical classifier path. A
            // nonidentity UV0 mapping uses the affine helper before every mip
            // is considered. The identity test checks scale and offset only.
            // A nonzero channel reaches the selection below with identity
            // scale and offset because the layer frontend enforces that
            // boundary.
            var transformed = triangle;
            var envelope = AlphaUvEnvelope.Zero;
            if (_mapping.Scale.x != 1f ||
                _mapping.Scale.y != 1f ||
                _mapping.Offset.x != 0f ||
                _mapping.Offset.y != 0f)
            {
                if (!AffineUvTransform.TryTransform(
                        _mapping, triangle, out transformed, out envelope))
                {
                    return TriangleAlphaOutcome.Unknown;
                }
            }

            if (_mapping.Channel != 0)
            {
                // A layer channel mapping is exact identity in scale and
                // offset by the frontend boundary, so the channel selection
                // substitutes the named channel's vertex coordinates and the
                // classifier reads them as plain uv0. A channel the mesh
                // does not carry invalidates only this triangle's proof.
                if (!triangle.TryGetUvSet(_mapping.Channel,
                        out var channelA, out var channelB,
                        out var channelC))
                {
                    return TriangleAlphaOutcome.Unknown;
                }

                transformed = TriangleAlphaInput.WithUv0(
                    triangle.Position0, triangle.Position1,
                    triangle.Position2, channelA, channelB, channelC);
            }

            var sawUnknown = false;
            for (var index = 0; index < _chain.Count; index++)
            {
                if (_chain.IsLevelWithoutEvidence(index))
                {
                    sawUnknown = true;
                    continue;
                }

                var outcome = TriangleAlphaClassifier.Classify(
                    transformed, _chain[index], _sampling, envelope,
                    _maxNoiseTexelPercent);
                if (outcome == TriangleAlphaOutcome.MustRemainTransparent)
                {
                    return TriangleAlphaOutcome.MustRemainTransparent;
                }
                if (outcome == TriangleAlphaOutcome.Unknown)
                {
                    sawUnknown = true;
                }
            }

            // Never vacuous: AlphaMipChain forbids an empty chain, so the loop body
            // ran at least once.
            return sawUnknown
                ? TriangleAlphaOutcome.Unknown
                : TriangleAlphaOutcome.ProvenOpaque;
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
            AlphaFieldProvider fieldProvider,
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
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.TextureSampleTimesConstant:
                    return ResolveScaledSample(
                        value.GetTextureSample(),
                        value.GetChannel(),
                        value.GetMultiplier(),
                        fieldProvider,
                        maxNoiseTexelPercent);
                case ScalarSemanticValueKind.ProductChainOfTextureSamples:
                    return ResolveProductChain(
                        value, fieldProvider, maxNoiseTexelPercent);
                case ScalarSemanticValueKind.SaturatingSum:
                    return ResolveSaturatingSum(
                        value, fieldProvider, maxNoiseTexelPercent);
                case ScalarSemanticValueKind.SaturatingDifference:
                    return ResolveSaturatingDifference(
                        value, fieldProvider, maxNoiseTexelPercent);
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
            int maxNoiseTexelPercent)
        {
            if (multiplier > 1f)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedMultiplier);
            }

            if (multiplier == 1f)
            {
                return ResolveSampled(
                    sample, channel, fieldProvider, maxNoiseTexelPercent);
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
            int maxNoiseTexelPercent)
        {
            var multiplier = value.GetProductMultiplier();
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
                var factor = ResolveSampled(
                    value.GetChainSample(index),
                    value.GetChainChannel(index),
                    fieldProvider,
                    maxNoiseTexelPercent);
                if (!factor.IsResolved)
                {
                    return factor;
                }

                conjoined = conjoined == null
                    ? factor
                    : AlphaResolution.Product(conjoined, factor);
            }

            return conjoined;
        }

        /// <summary>
        /// alpha = saturate(first + second). A side attested exactly one
        /// decides the sum alone, because the other side's field contract
        /// bounds it in [0,1] and the saturate clamps at one. Two constants
        /// fold through the saturate. Anything else stays conservative: the
        /// sum reaches one on some triangle exactly when either side is one
        /// there, and cross-field correlation between two sampled terms is
        /// unknowable from two independent [0,1] contracts, so no per-triangle
        /// proof exists without an additive classifier.
        /// </summary>
        private static AlphaResolution ResolveSaturatingSum(
            ScalarSemanticValue value,
            AlphaFieldProvider fieldProvider,
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
                fieldProvider, maxNoiseTexelPercent);
            if (!firstResolution.IsResolved)
            {
                return firstResolution;
            }

            var secondResolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(second),
                fieldProvider, maxNoiseTexelPercent);
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
            if (firstTransparent)
            {
                return secondResolution;
            }

            if (secondTransparent)
            {
                return firstResolution;
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
                    fieldProvider, maxNoiseTexelPercent);
            }

            var subtrahendResolution = AlphaSemanticsResolver.Resolve(
                SemanticOutput<ScalarSemanticValue>.Complete(subtrahend),
                fieldProvider, maxNoiseTexelPercent);
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
            int maxNoiseTexelPercent)
        {
            if (!IsSupportedMapping(sample.Coordinates))
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.UnsupportedUvMapping);
            }

            var sampling = new AlphaSamplingSettings(
                sample.Sampling.Filter,
                sample.Sampling.Wrap,
                sample.Sampling.Aniso);

            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            return AlphaResolution.Classified(
                chain, sampling, sample.Coordinates, maxNoiseTexelPercent);
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
