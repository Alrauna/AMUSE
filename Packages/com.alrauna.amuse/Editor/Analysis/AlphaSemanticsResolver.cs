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
        private readonly UvMapping _mapping;

        private AlphaResolution(
            bool isResolved,
            AlphaResolutionFailure failure,
            bool isUniform,
            TriangleAlphaOutcome uniformOutcome,
            AlphaMipChain chain,
            AlphaSamplingSettings sampling,
            UvMapping mapping)
        {
            // Invariants: a resolved value carries no failure, a refusal
            // carries one, and a classified value always has its field.
            if (isResolved != (failure == AlphaResolutionFailure.None))
            {
                throw new ArgumentException(
                    "A resolution is resolved exactly when it has no failure.",
                    nameof(failure));
            }
            if (isResolved && !isUniform && chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            IsResolved = isResolved;
            Failure = failure;
            _isUniform = isUniform;
            _uniformOutcome = uniformOutcome;
            _chain = chain;
            _sampling = sampling;
            _mapping = mapping;
        }

        internal bool IsResolved { get; }
        internal AlphaResolutionFailure Failure { get; }

        internal static AlphaResolution Refused(AlphaResolutionFailure failure)
        {
            return new AlphaResolution(
                false, failure, false, default, null, default, default);
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
                default);
        }

        internal static AlphaResolution Classified(
            AlphaMipChain chain,
            AlphaSamplingSettings sampling,
            UvMapping mapping)
        {
            // Only `AlphaSemanticsResolver.IsSupportedMapping` decides which
            // mappings ever reach this factory, and it admits channel 0
            // only. Enforcing that here (rather than trusting the caller)
            // means `Classify`'s identity test below can check scale/offset
            // alone, per design §6.1 step 2: folding a channel test into
            // that OR would otherwise let a channel-non-zero mapping fall
            // through to the affine transform, which only ever reads
            // `TriangleAlphaInput.Uv0` — silently applying another UV set's
            // ST to UV0 instead of being rejected.
            if (mapping.Channel != 0)
            {
                throw new ArgumentException(
                    "A classified resolution's mapping must be for UV " +
                    "channel 0.",
                    nameof(mapping));
            }

            return new AlphaResolution(
                true,
                AlphaResolutionFailure.None,
                false,
                default,
                chain,
                sampling,
                mapping);
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

            // A mip chain is alternative evidence about one configuration, not a
            // set of admitted configurations: the hardware may select any level and
            // AMUSE cannot know which, so one non-opaque level refutes the proof.
            // MustRemainTransparent is absorbing, so returning on it cannot change
            // the result. Unknown must NOT exit early - a later level may be
            // MustRemainTransparent, which outranks it.
            // Identity remains structurally on the historical classifier path;
            // non-identity UV0 uses the affine helper's Lemma P exact result or
            // conservative envelope before every mip is considered. The
            // identity test below checks scale and offset only (design §6.1
            // step 2): the channel is not part of it, because `Classified`'s
            // constructor invariant already guarantees `_mapping.Channel == 0`
            // for every resolution that reaches here — `IsSupportedMapping` is
            // the only place that decides which channel is admitted. Folding a
            // channel test into this predicate would be redundant at best and,
            // for any future caller that relaxed the constructor invariant,
            // would silently apply a channel-non-zero mapping's scale/offset to
            // `TriangleAlphaInput.Uv0` instead of rejecting it.
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

            var sawUnknown = false;
            for (var index = 0; index < _chain.Count; index++)
            {
                var outcome = TriangleAlphaClassifier.Classify(
                    transformed, _chain[index], _sampling, envelope);
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

            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
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

            if (!fieldProvider(sample.Source, channel, out var chain) ||
                chain == null)
            {
                return AlphaResolution.Refused(
                    AlphaResolutionFailure.MissingTextureEvidence);
            }

            return AlphaResolution.Classified(
                chain, sampling, sample.Coordinates);
        }

        /// <summary>
        /// The resolver admits UV0 only. For a non-identity mapping, the
        /// implemented Lemma P predicate in the affine MainTex ST design proves
        /// the exact transformed domain or supplies the conservative envelope;
        /// channel selection remains a frontend-owned coordinate-set boundary.
        /// </summary>
        private static bool IsSupportedMapping(UvMapping mapping)
        {
            return mapping.Channel == 0;
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
