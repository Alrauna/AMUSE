using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics
{
    internal readonly struct TextureSourceId : IEquatable<TextureSourceId>
    {
        internal string Value { get; }

        internal TextureSourceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "Texture source identity must be non-empty.",
                    nameof(value));
            }

            Value = value;
        }

        public bool Equals(TextureSourceId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextureSourceId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(Value);
        }
    }

    /// <summary>
    /// UV channel plus binary32 affine scale and offset. C4: a frontend may emit
    /// a non-identity mapping for an alpha-relevant sample only when its attested
    /// source proves the sampler coordinate is that binary32 affine image with no
    /// further unbounded fragment arithmetic.
    /// </summary>
    internal readonly struct UvMapping : IEquatable<UvMapping>
    {
        internal int Channel { get; }
        internal Vector2 Scale { get; }
        internal Vector2 Offset { get; }

        internal UvMapping(int channel, Vector2 scale, Vector2 offset)
        {
            if (channel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            ValidateFinite(scale, nameof(scale));
            ValidateFinite(offset, nameof(offset));

            Channel = channel;
            Scale = scale;
            Offset = offset;
        }

        public bool Equals(UvMapping other)
        {
            return Channel == other.Channel &&
                   Scale.Equals(other.Scale) &&
                   Offset.Equals(other.Offset);
        }

        public override bool Equals(object obj)
        {
            return obj is UvMapping other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Channel;
                hash = hash * 397 ^ Scale.GetHashCode();
                return hash * 397 ^ Offset.GetHashCode();
            }
        }

        private static void ValidateFinite(Vector2 value, string parameterName)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y))
            {
                throw new ArgumentException(
                    "UV mapping values must be finite.",
                    parameterName);
            }
        }
    }

    internal enum TextureFilterMode
    {
        Point,
        Bilinear,

        /// <summary>
        /// Bilinear within the selected level, plus a monotone blend of the
        /// two adjacent levels the hardware selects. The classifier models
        /// the within-level footprint as bilinear; the mip-chain conjunction
        /// supplies both levels' proofs.
        /// </summary>
        Trilinear
    }

    internal enum TextureWrapMode
    {
        Clamp,
        Repeat
    }

    /// <summary>
    /// Whether the texture is sampled anisotropically. Anisotropy averages an
    /// elongated footprint the classifier does not model, so an anisotropic
    /// sample is provable only where the proof no longer needs the footprint:
    /// a fully opaque level answers every possible footprint at once.
    /// </summary>
    internal enum TextureAnisoMode
    {
        None,
        Anisotropic
    }

    internal readonly struct TextureSampling : IEquatable<TextureSampling>
    {
        private const TextureAnisoMode DefaultAniso = TextureAnisoMode.None;

        internal TextureFilterMode Filter { get; }
        internal TextureWrapMode Wrap { get; }
        internal TextureAnisoMode Aniso { get; }

        /// <summary>
        /// The common sampling shape: no anisotropy.
        /// </summary>
        internal TextureSampling(
            TextureFilterMode filter,
            TextureWrapMode wrap)
            : this(filter, wrap, DefaultAniso)
        {
        }

        internal TextureSampling(
            TextureFilterMode filter,
            TextureWrapMode wrap,
            TextureAnisoMode aniso)
        {
            if (!Enum.IsDefined(typeof(TextureFilterMode), filter))
            {
                throw new ArgumentOutOfRangeException(nameof(filter));
            }
            if (!Enum.IsDefined(typeof(TextureWrapMode), wrap))
            {
                throw new ArgumentOutOfRangeException(nameof(wrap));
            }
            if (!Enum.IsDefined(typeof(TextureAnisoMode), aniso))
            {
                throw new ArgumentOutOfRangeException(nameof(aniso));
            }

            Filter = filter;
            Wrap = wrap;
            Aniso = aniso;
        }

        public bool Equals(TextureSampling other)
        {
            return Filter == other.Filter && Wrap == other.Wrap &&
                   Aniso == other.Aniso;
        }

        public override bool Equals(object obj)
        {
            return obj is TextureSampling other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (((int)Filter * 397) ^ (int)Wrap) * 397 ^ (int)Aniso;
        }
    }

    internal sealed record TextureSample
    {
        internal TextureSourceId Source { get; }
        internal UvMapping Coordinates { get; }
        internal TextureSampling Sampling { get; }

        internal TextureSample(
            TextureSourceId source,
            UvMapping coordinates,
            TextureSampling sampling)
        {
            if (string.IsNullOrWhiteSpace(source.Value))
            {
                throw new ArgumentException(
                    "Texture source identity must be initialized.",
                    nameof(source));
            }

            Source = source;
            Coordinates = coordinates;
            Sampling = sampling;
        }

    }

    internal enum TextureColorInterpretation
    {
        Linear,
        Srgb
    }

    internal enum TextureChannel
    {
        Red,
        Green,
        Blue,
        Alpha
    }

    internal enum ColorSemanticValueKind
    {
        Constant,
        TextureSample,
        TextureSampleTimesConstant
    }

    internal sealed record ColorSemanticValue
    {
        private readonly Vector3 _constantValue;
        private readonly TextureSample _sample;
        private readonly TextureColorInterpretation _interpretation;
        private readonly Vector3 _multiplier;

        internal ColorSemanticValueKind Kind { get; }

        private ColorSemanticValue(
            ColorSemanticValueKind kind,
            Vector3 constantValue,
            TextureSample sample,
            TextureColorInterpretation interpretation,
            Vector3 multiplier)
        {
            Kind = kind;
            _constantValue = constantValue;
            _sample = sample;
            _interpretation = interpretation;
            _multiplier = multiplier;
        }

        internal static ColorSemanticValue Constant(Vector3 value)
        {
            ValidateFinite(value, nameof(value));
            return new ColorSemanticValue(
                ColorSemanticValueKind.Constant,
                value,
                null,
                default,
                default);
        }

        internal static ColorSemanticValue Texture(
            TextureSample sample,
            TextureColorInterpretation interpretation)
        {
            ValidateTextureArguments(sample, interpretation);
            return new ColorSemanticValue(
                ColorSemanticValueKind.TextureSample,
                default,
                sample,
                interpretation,
                default);
        }

        internal static ColorSemanticValue TextureTimesConstant(
            TextureSample sample,
            TextureColorInterpretation interpretation,
            Vector3 multiplier)
        {
            ValidateTextureArguments(sample, interpretation);
            ValidateFinite(multiplier, nameof(multiplier));
            return new ColorSemanticValue(
                ColorSemanticValueKind.TextureSampleTimesConstant,
                default,
                sample,
                interpretation,
                multiplier);
        }

        internal Vector3 GetConstantValue()
        {
            if (Kind != ColorSemanticValueKind.Constant)
            {
                throw new InvalidOperationException(
                    "A constant value is not meaningful for this kind.");
            }

            return _constantValue;
        }

        internal TextureSample GetTextureSample()
        {
            if (Kind == ColorSemanticValueKind.Constant)
            {
                throw new InvalidOperationException(
                    "A texture sample is not meaningful for this kind.");
            }

            return _sample;
        }

        internal TextureColorInterpretation GetColorInterpretation()
        {
            if (Kind == ColorSemanticValueKind.Constant)
            {
                throw new InvalidOperationException(
                    "A color interpretation is not meaningful for this kind.");
            }

            return _interpretation;
        }

        internal Vector3 GetMultiplier()
        {
            if (Kind != ColorSemanticValueKind.TextureSampleTimesConstant)
            {
                throw new InvalidOperationException(
                    "A multiplier is not meaningful for this kind.");
            }

            return _multiplier;
        }

        private static void ValidateTextureArguments(
            TextureSample sample,
            TextureColorInterpretation interpretation)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }
            if (!Enum.IsDefined(
                    typeof(TextureColorInterpretation),
                    interpretation))
            {
                throw new ArgumentOutOfRangeException(nameof(interpretation));
            }
        }

        private static void ValidateFinite(Vector3 value, string parameterName)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) ||
                float.IsNaN(value.y) || float.IsInfinity(value.y) ||
                float.IsNaN(value.z) || float.IsInfinity(value.z))
            {
                throw new ArgumentException(
                    "Color values must be finite.",
                    parameterName);
            }
        }
    }

    internal enum ScalarSemanticValueKind
    {
        Constant,
        TextureSample,
        TextureSampleTimesConstant,
        MappedTextureSample,
        ProductChainOfTextureSamples,
        SaturatingSum,
        SaturatingDifference,
    }

    /// <summary>
    /// One normalized scalar built as a product chain of sampled texture
    /// terms under one leading constant multiplier. The represented value is
    /// the sampled factors multiplied left to right after the constant:
    /// <c>fl(fl(k * f0) * f1) ...</c>. The multiplier is the leading tint
    /// constant. The resolver owns the product lemmas and fails closed on a
    /// multiplier it cannot prove.
    /// </summary>
    internal sealed class ScalarSemanticValue : IEquatable<ScalarSemanticValue>
    {
        private readonly float _constantValue;
        private readonly TextureSample _sample;
        private readonly TextureChannel _channel;
        private readonly float _multiplier;
        private readonly TextureSample[] _chainSamples;
        private readonly TextureChannel[] _chainChannels;
        private readonly AffineAlphaMap?[] _chainMaps;
        private readonly ScalarSemanticValue _firstValue;
        private readonly ScalarSemanticValue _secondValue;
        private readonly AffineAlphaMap _map;

        internal ScalarSemanticValueKind Kind { get; }

        private ScalarSemanticValue(
            ScalarSemanticValueKind kind,
            float constantValue,
            TextureSample sample,
            TextureChannel channel,
            float multiplier)
            : this(
                kind,
                constantValue,
                sample,
                channel,
                multiplier,
                null,
                null,
                null,
                null,
                null,
                default)
        {
        }

        internal static ScalarSemanticValue Constant(float value)
        {
            ValidateFinite(value, nameof(value));
            return new ScalarSemanticValue(
                ScalarSemanticValueKind.Constant,
                value,
                null,
                default,
                default);
        }

        internal static ScalarSemanticValue Texture(
            TextureSample sample,
            TextureChannel channel)
        {
            ValidateTextureArguments(sample, channel);
            return new ScalarSemanticValue(
                ScalarSemanticValueKind.TextureSample,
                default,
                sample,
                channel,
                default);
        }

        internal static ScalarSemanticValue TextureTimesConstant(
            TextureSample sample,
            TextureChannel channel,
            float multiplier)
        {
            ValidateTextureArguments(sample, channel);
            ValidateFinite(multiplier, nameof(multiplier));
            return new ScalarSemanticValue(
                ScalarSemanticValueKind.TextureSampleTimesConstant,
                default,
                sample,
                channel,
                multiplier);
        }

        /// <summary>
        /// One normalized scalar built as saturate(sample * scale + value)
        /// at the sampled coordinate, with the exact map carrying the
        /// rounding argument. The identity pair never reaches this kind:
        /// those materials are the existing plain sample.
        /// </summary>
        internal static ScalarSemanticValue MappedTexture(
            TextureSample sample,
            TextureChannel channel,
            AffineAlphaMap? map)
        {
            ValidateTextureArguments(sample, channel);
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (map.Value.Equals(default(AffineAlphaMap)))
            {
                throw new ArgumentException(
                    "A never-constructed map cannot name a mapped sample: " +
                    "the zero scale pair routes to the constant arm and " +
                    "FromBinary32 refuses it.",
                    nameof(map));
            }
            return new ScalarSemanticValue(
                ScalarSemanticValueKind.MappedTextureSample,
                default,
                sample,
                channel,
                default,
                null,
                null,
                null,
                null,
                null,
                map.Value);
        }

        internal float GetConstantValue()
        {
            if (Kind != ScalarSemanticValueKind.Constant)
            {
                throw new InvalidOperationException(
                    "A constant value is not meaningful for this kind.");
            }

            return _constantValue;
        }

        internal TextureSample GetTextureSample()
        {
            if (Kind == ScalarSemanticValueKind.Constant ||
                Kind == ScalarSemanticValueKind.ProductChainOfTextureSamples ||
                Kind == ScalarSemanticValueKind.SaturatingSum ||
                Kind == ScalarSemanticValueKind.SaturatingDifference)
            {
                throw new InvalidOperationException(
                    "A single texture sample is not meaningful for this " +
                    "kind.");
            }

            return _sample;
        }

        internal TextureChannel GetChannel()
        {
            if (Kind == ScalarSemanticValueKind.Constant ||
                Kind == ScalarSemanticValueKind.ProductChainOfTextureSamples ||
                Kind == ScalarSemanticValueKind.SaturatingSum ||
                Kind == ScalarSemanticValueKind.SaturatingDifference)
            {
                throw new InvalidOperationException(
                    "A single texture channel is not meaningful for this " +
                    "kind.");
            }

            return _channel;
        }

        internal AffineAlphaMap GetMap()
        {
            RequireKind(ScalarSemanticValueKind.MappedTextureSample);
            return _map;
        }

        internal float GetMultiplier()
        {
            if (Kind != ScalarSemanticValueKind.TextureSampleTimesConstant)
            {
                throw new InvalidOperationException(
                    "A multiplier is not meaningful for this kind.");
            }

            return _multiplier;
        }


        internal float GetProductMultiplier()
        {
            if (Kind != ScalarSemanticValueKind.ProductChainOfTextureSamples)
            {
                throw new InvalidOperationException(
                    "A product multiplier is meaningful only for the " +
                    "chain kind.");
            }

            return _multiplier;
        }

        /// <summary>
        /// The product of two or more sampled terms under one leading constant
        /// multiplier. The represented value is the sampled factors multiplied
        /// left to right after the constant: <c>fl(fl(k * f0) * f1) ...</c>.
        /// Two factors are the historical product's exact shape; three or more
        /// carry layered alpha chains (main texture, layer textures, masks).
        /// The resolver owns the product lemmas and fails closed on a
        /// multiplier it cannot prove.
        /// </summary>
        internal static ScalarSemanticValue ProductChain(
            IReadOnlyList<TextureSample> samples,
            IReadOnlyList<TextureChannel> channels,
            float multiplier)
        {
            return ProductChain(samples, channels, multiplier, null);
        }

        /// <summary>
        /// The product chain above with one optional map per factor. A null
        /// entry is an identity factor; a mapped entry carries the exact
        /// affine image that factor's sampler applies. A single-factor chain
        /// is valid only when it carries a map, because the frontends produce
        /// that shape for a collapsed main constant times one mapped mask
        /// factor; a mapless single factor has its own kind.
        /// </summary>
        internal static ScalarSemanticValue ProductChain(
            IReadOnlyList<TextureSample> samples,
            IReadOnlyList<TextureChannel> channels,
            float multiplier,
            IReadOnlyList<AffineAlphaMap?> maps)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }
            if (channels == null)
            {
                throw new ArgumentNullException(nameof(channels));
            }
            if (samples.Count != channels.Count)
            {
                throw new ArgumentException(
                    "Every chain factor needs exactly one channel.",
                    nameof(channels));
            }
            if (maps != null && maps.Count != samples.Count)
            {
                throw new ArgumentException(
                    "Every chain factor needs exactly one map entry.",
                    nameof(maps));
            }
            if (samples.Count == 0)
            {
                throw new ArgumentException(
                    "A chain carries at least one sampled factor.",
                    nameof(samples));
            }
            if (samples.Count == 1 && !HasMappedFactor(maps))
            {
                throw new ArgumentException(
                    "A single sample has its own kind unless it carries a " +
                    "map.",
                    nameof(samples));
            }
            for (var index = 0; index < samples.Count; index++)
            {
                ValidateTextureArguments(samples[index], channels[index]);
            }
            ValidateFinite(multiplier, nameof(multiplier));

            var value = new ScalarSemanticValue(
                ScalarSemanticValueKind.ProductChainOfTextureSamples,
                default,
                null,
                default,
                multiplier,
                null,
                null,
                null,
                null,
                CopyMaps(maps, samples.Count),
                default);
            var sampleCopy = new TextureSample[samples.Count];
            var channelCopy = new TextureChannel[channels.Count];
            for (var index = 0; index < samples.Count; index++)
            {
                sampleCopy[index] = samples[index];
                channelCopy[index] = channels[index];
            }

            return value.WithChain(sampleCopy, channelCopy);
        }

        private static bool HasMappedFactor(
            IReadOnlyList<AffineAlphaMap?> maps)
        {
            if (maps == null)
            {
                return false;
            }
            for (var index = 0; index < maps.Count; index++)
            {
                if (maps[index] != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static AffineAlphaMap?[] CopyMaps(
            IReadOnlyList<AffineAlphaMap?> maps,
            int count)
        {
            if (maps == null)
            {
                return null;
            }
            var copy = new AffineAlphaMap?[count];
            for (var index = 0; index < count; index++)
            {
                copy[index] = maps[index];
            }
            return copy;
        }

        /// <summary>
        /// The saturating sum <c>saturate(first + second)</c> of two closed
        /// forms. lilToon's layer alpha modes 3 and 4 write exactly this shape
        /// over the prior alpha chain.
        /// </summary>
        internal static ScalarSemanticValue SaturatingSum(
            ScalarSemanticValue first,
            ScalarSemanticValue second)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }
            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            var value = new ScalarSemanticValue(
                ScalarSemanticValueKind.SaturatingSum,
                default,
                null,
                default,
                default);
            return value.WithChildren(first, second);
        }

        /// <summary>
        /// The saturating difference <c>saturate(minuend - subtrahend)</c> of
        /// two closed forms. lilToon's layer alpha mode 4 writes exactly this
        /// shape over the prior alpha chain.
        /// </summary>
        internal static ScalarSemanticValue SaturatingDifference(
            ScalarSemanticValue minuend,
            ScalarSemanticValue subtrahend)
        {
            if (minuend == null)
            {
                throw new ArgumentNullException(nameof(minuend));
            }
            if (subtrahend == null)
            {
                throw new ArgumentNullException(nameof(subtrahend));
            }

            var value = new ScalarSemanticValue(
                ScalarSemanticValueKind.SaturatingDifference,
                default,
                null,
                default,
                default);
            return value.WithChildren(minuend, subtrahend);
        }

        private ScalarSemanticValue WithChain(
            TextureSample[] samples,
            TextureChannel[] channels)
        {
            return new ScalarSemanticValue(
                Kind,
                _constantValue,
                _sample,
                _channel,
                _multiplier,
                samples,
                channels,
                _firstValue,
                _secondValue,
                _chainMaps,
                _map);
        }

        private ScalarSemanticValue WithChildren(
            ScalarSemanticValue first,
            ScalarSemanticValue second)
        {
            return new ScalarSemanticValue(
                Kind,
                _constantValue,
                _sample,
                _channel,
                _multiplier,
                _chainSamples,
                _chainChannels,
                first,
                second,
                _chainMaps,
                _map);
        }

        internal int GetChainFactorCount()
        {
            RequireChain();
            return _chainSamples.Length;
        }

        internal TextureSample GetChainSample(int index)
        {
            RequireChain();
            return _chainSamples[index];
        }

        internal TextureChannel GetChainChannel(int index)
        {
            RequireChain();
            return _chainChannels[index];
        }

        internal AffineAlphaMap? GetChainMap(int index)
        {
            RequireChain();
            if (index < 0 || index >= _chainSamples.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _chainMaps == null ? null : _chainMaps[index];
        }

        internal ScalarSemanticValue GetSumFirst()
        {
            RequireKind(ScalarSemanticValueKind.SaturatingSum);
            return _firstValue;
        }

        internal ScalarSemanticValue GetSumSecond()
        {
            RequireKind(ScalarSemanticValueKind.SaturatingSum);
            return _secondValue;
        }

        internal ScalarSemanticValue GetMinuend()
        {
            RequireKind(ScalarSemanticValueKind.SaturatingDifference);
            return _firstValue;
        }

        internal ScalarSemanticValue GetSubtrahend()
        {
            RequireKind(ScalarSemanticValueKind.SaturatingDifference);
            return _secondValue;
        }

        private void RequireChain()
        {
            if (Kind != ScalarSemanticValueKind.ProductChainOfTextureSamples)
            {
                throw new InvalidOperationException(
                    "Chain accessors are meaningful only for the chain " +
                    "kind.");
            }
        }

        private void RequireKind(ScalarSemanticValueKind kind)
        {
            if (Kind != kind)
            {
                throw new InvalidOperationException(
                    "This accessor is meaningful only for one kind.");
            }
        }

        private ScalarSemanticValue(
            ScalarSemanticValueKind kind,
            float constantValue,
            TextureSample sample,
            TextureChannel channel,
            float multiplier,
            TextureSample[] chainSamples,
            TextureChannel[] chainChannels,
            ScalarSemanticValue firstValue,
            ScalarSemanticValue secondValue,
            AffineAlphaMap?[] chainMaps,
            AffineAlphaMap map)
        {
            Kind = kind;
            _constantValue = constantValue;
            _sample = sample;
            _channel = channel;
            _multiplier = multiplier;
            _chainSamples = chainSamples;
            _chainChannels = chainChannels;
            _chainMaps = chainMaps;
            _firstValue = firstValue;
            _secondValue = secondValue;
            _map = map;
        }

        public bool Equals(ScalarSemanticValue other)
        {
            if (other == null || Kind != other.Kind)
            {
                return false;
            }

            switch (Kind)
            {
                case ScalarSemanticValueKind.Constant:
                    return _constantValue.Equals(other._constantValue);
                case ScalarSemanticValueKind.TextureSample:
                    return _sample.Equals(other._sample) &&
                           _channel == other._channel;
                case ScalarSemanticValueKind.TextureSampleTimesConstant:
                    return _sample.Equals(other._sample) &&
                           _channel == other._channel &&
                           _multiplier.Equals(other._multiplier);
                case ScalarSemanticValueKind.MappedTextureSample:
                    return _sample.Equals(other._sample) &&
                           _channel == other._channel &&
                           _map.Equals(other._map);
                case ScalarSemanticValueKind.ProductChainOfTextureSamples:
                    if (_multiplier.Equals(other._multiplier) == false ||
                        _chainSamples.Length != other._chainSamples.Length)
                    {
                        return false;
                    }
                    for (var index = 0; index < _chainSamples.Length; index++)
                    {
                        if (_chainSamples[index].Equals(
                                other._chainSamples[index]) == false ||
                            _chainChannels[index] != other._chainChannels[index])
                        {
                            return false;
                        }
                    }
                    return true;
                case ScalarSemanticValueKind.SaturatingSum:
                case ScalarSemanticValueKind.SaturatingDifference:
                    return _firstValue.Equals(other._firstValue) &&
                           _secondValue.Equals(other._secondValue);
                default:
                    return false;
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScalarSemanticValue);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                switch (Kind)
                {
                    case ScalarSemanticValueKind.Constant:
                        return hash * 397 ^ _constantValue.GetHashCode();
                    case ScalarSemanticValueKind.TextureSample:
                        hash = hash * 397 ^ _sample.GetHashCode();
                        return hash * 397 ^ (int)_channel;
                    case ScalarSemanticValueKind.TextureSampleTimesConstant:
                        hash = hash * 397 ^ _sample.GetHashCode();
                        hash = hash * 397 ^ (int)_channel;
                        return hash * 397 ^ _multiplier.GetHashCode();
                    case ScalarSemanticValueKind.MappedTextureSample:
                        hash = hash * 397 ^ _sample.GetHashCode();
                        hash = hash * 397 ^ (int)_channel;
                        return hash * 397 ^ _map.GetHashCode();
                    case ScalarSemanticValueKind.ProductChainOfTextureSamples:
                        hash = hash * 397 ^ _multiplier.GetHashCode();
                        for (var index = 0; index < _chainSamples.Length; index++)
                        {
                            hash = hash * 397 ^ _chainSamples[index].GetHashCode();
                            hash = hash * 397 ^ (int)_chainChannels[index];
                        }
                        return hash;
                    case ScalarSemanticValueKind.SaturatingSum:
                    case ScalarSemanticValueKind.SaturatingDifference:
                        hash = hash * 397 ^ _firstValue.GetHashCode();
                        return hash * 397 ^ _secondValue.GetHashCode();
                    default:
                        return hash;
                }
            }
        }

        private static void ValidateTextureArguments(
            TextureSample sample,
            TextureChannel channel)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }
            if (!Enum.IsDefined(typeof(TextureChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException(
                    "Scalar values must be finite.",
                    parameterName);
            }
        }
    }

    internal enum NormalSemanticValueKind
    {
        Unmodified,
        TangentSpaceNormalMap
    }

    internal sealed record NormalSemanticValue
    {
        private readonly TextureSample _sample;

        internal NormalSemanticValueKind Kind { get; }

        private NormalSemanticValue(
            NormalSemanticValueKind kind,
            TextureSample sample)
        {
            Kind = kind;
            _sample = sample;
        }

        internal static NormalSemanticValue Unmodified()
        {
            return new NormalSemanticValue(
                NormalSemanticValueKind.Unmodified,
                null);
        }

        internal static NormalSemanticValue TangentSpaceNormalMap(
            TextureSample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            return new NormalSemanticValue(
                NormalSemanticValueKind.TangentSpaceNormalMap,
                sample);
        }

        internal TextureSample GetTextureSample()
        {
            if (Kind != NormalSemanticValueKind.TangentSpaceNormalMap)
            {
                throw new InvalidOperationException(
                    "A texture sample is not meaningful for this kind.");
            }

            return _sample;
        }

    }

    internal readonly struct SemanticOutput<T> : IEquatable<SemanticOutput<T>>
        where T : class
    {
        private readonly T _value;

        internal bool IsComplete { get; }

        private SemanticOutput(bool isComplete, T value)
        {
            IsComplete = isComplete;
            _value = value;
        }

        internal static SemanticOutput<T> Complete(T value)
        {
            if (ReferenceEquals(value, null))
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new SemanticOutput<T>(true, value);
        }

        internal static SemanticOutput<T> Unknown()
        {
            return new SemanticOutput<T>(false, default);
        }

        internal T GetCompleteValue()
        {
            if (!IsComplete)
            {
                throw new InvalidOperationException("Semantic output is unknown.");
            }

            return _value;
        }

        public bool Equals(SemanticOutput<T> other)
        {
            return IsComplete == other.IsComplete &&
                   (!IsComplete ||
                    EqualityComparer<T>.Default.Equals(_value, other._value));
        }

        public override bool Equals(object obj)
        {
            return obj is SemanticOutput<T> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return !IsComplete || ReferenceEquals(_value, null)
                ? 0
                : EqualityComparer<T>.Default.GetHashCode(_value);
        }
    }

    internal sealed record MaterialSemantics
    {
        internal SemanticOutput<ColorSemanticValue> BaseColor { get; }
        internal SemanticOutput<ScalarSemanticValue> Alpha { get; }
        internal SemanticOutput<ColorSemanticValue> Emission { get; }
        internal SemanticOutput<NormalSemanticValue> Normal { get; }

        internal MaterialSemantics(
            SemanticOutput<ColorSemanticValue> baseColor,
            SemanticOutput<ScalarSemanticValue> alpha,
            SemanticOutput<ColorSemanticValue> emission,
            SemanticOutput<NormalSemanticValue> normal)
        {
            BaseColor = baseColor;
            Alpha = alpha;
            Emission = emission;
            Normal = normal;
        }

    }
}
