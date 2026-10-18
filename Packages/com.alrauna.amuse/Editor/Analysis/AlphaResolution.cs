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
        private readonly TextureSampling _sampling;
        // Only a classified resolution consults texture contents, so only it
        // carries the noise density percent. Uniform, product, and refused
        // resolutions store zero and ignore it: a constant alpha is not
        // texture noise, and a product's factors each carry their own.
        private readonly int _maxNoiseTexelPercent;
        // The mapping decoded once at construction. The per-triangle path
        // consumes this record; it never re-decodes the mapping's constants.
        private readonly AffineUvTransform.DecodedMapping _decodedMapping;
        private readonly AlphaResolution _firstFactor;
        private readonly AlphaResolution _secondFactor;
        private readonly bool _isProduct;
        private readonly bool _isDisjunction;
        // The exact affine image the sampler applies to this factor's
        // sample. Only a classified resolution carries one: a mapped
        // sample's uniform arms are decided from the map's endpoints once
        // at resolve time and must not re-read the map per level.
        private readonly AffineAlphaMap? _map;

        private AlphaResolution(
            bool isResolved,
            AlphaResolutionFailure failure,
            bool isUniform,
            TriangleAlphaOutcome uniformOutcome,
            AlphaMipChain chain,
            TextureSampling sampling,
            int maxNoiseTexelPercent,
            UvMapping mapping,
            AlphaResolution firstFactor,
            AlphaResolution secondFactor,
            bool isProduct,
            bool isDisjunction = false,
            AffineAlphaMap? map = null)
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
            _decodedMapping = AffineUvTransform.Decode(mapping);
            _firstFactor = firstFactor;
            _secondFactor = secondFactor;
            _isProduct = isProduct;
            _isDisjunction = isDisjunction;
            _map = map;
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
            TextureSampling sampling,
            UvMapping mapping,
            int maxNoiseTexelPercent,
            AffineAlphaMap? map = null)
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
                false,
                map: map);
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
        /// of <see cref="Product"/>: alpha is the sum saturate(a + b), and
        /// each factor is bounded in [0,1] by the field contract. The sum
        /// reaches one whenever the factors add to at least one. It does
        /// not need one factor to be one there. Complementary masks with
        /// sub-one witnesses at different texels sum to identically one.
        /// The opaque arm is sound: a factor proven exactly one decides the
        /// sum alone, because the saturate clamps at one. The absorbing arm
        /// returns Unknown: a factor proven below one says nothing about
        /// the sum, and cross-field correlation is unknowable without an
        /// additive classifier.
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
        /// Reports whether this resolution's proof sampling is anisotropic.
        /// A classified resolution reads its own sampling. A product or a
        /// disjunction is anisotropic when any factor is anisotropic. A
        /// uniform resolution and a refused resolution answer false because
        /// they consult no sampling.
        /// </summary>
        internal bool HasAnisotropicSampling
        {
            get
            {
                if (!IsResolved || _isUniform)
                {
                    return false;
                }

                if (_isProduct || _isDisjunction)
                {
                    return (_firstFactor != null &&
                            _firstFactor.HasAnisotropicSampling) ||
                           (_secondFactor != null &&
                            _secondFactor.HasAnisotropicSampling);
                }

                return _sampling.Aniso == TextureAnisoMode.Anisotropic;
            }
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

                return TriangleAlphaOutcome.Unknown;
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
            if (_decodedMapping.Channel != 0)
            {
                // A layer channel mapping selects the named channel's vertex
                // coordinates and the classifier reads them as plain uv0. A
                // channel the mesh does not carry invalidates only this
                // triangle's proof.
                if (!triangle.TryGetUvSet(_decodedMapping.Channel,
                        out var channelA, out var channelB,
                        out var channelC))
                {
                    return TriangleAlphaOutcome.Unknown;
                }

                transformed = TriangleAlphaInput.WithUv0(
                    triangle.Position0, triangle.Position1,
                    triangle.Position2, channelA, channelB, channelC);
            }

            if (!_decodedMapping.IsIdentity)
            {
                if (!AffineUvTransform.TryTransform(
                        _decodedMapping, transformed, out transformed, out envelope))
                {
                    return TriangleAlphaOutcome.Unknown;
                }
            }

            // Position facts do not vary across the chain: vertex finiteness
            // and the exact degeneracy verdict are decoded once per triangle
            // and the record threads through every level into the classifier.
            // The per-level UV decodes stay inside the classifier because
            // they depend on the level's texture window.
            var positionValidity = ExactUvGeometry.DecodePositionValidity(
                transformed);

            var sawUnknown = false;
            for (var index = 0; index < _chain.Count; index++)
            {
                if (_chain.IsLevelWithoutEvidence(index))
                {
                    sawUnknown = true;
                    continue;
                }

                // A mapped sample swaps only the classifier entry: the same
                // transform, envelope, provenance check, and absorbing fold
                // govern every level exactly as on the plain path.
                var outcome = _map == null
                    ? TriangleAlphaClassifier.Classify(
                        transformed, _chain[index], _sampling, envelope,
                        _maxNoiseTexelPercent, positionValidity)
                    : TriangleAlphaClassifier.ClassifyMapped(
                        transformed, _chain[index], _sampling, envelope,
                        _map.Value, positionValidity);
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
}
