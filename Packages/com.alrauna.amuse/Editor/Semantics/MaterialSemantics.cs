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
        Bilinear
    }

    internal enum TextureWrapMode
    {
        Clamp,
        Repeat
    }

    internal readonly struct TextureSampling : IEquatable<TextureSampling>
    {
        internal TextureFilterMode Filter { get; }
        internal TextureWrapMode Wrap { get; }

        internal TextureSampling(
            TextureFilterMode filter,
            TextureWrapMode wrap)
        {
            if (!Enum.IsDefined(typeof(TextureFilterMode), filter))
            {
                throw new ArgumentOutOfRangeException(nameof(filter));
            }
            if (!Enum.IsDefined(typeof(TextureWrapMode), wrap))
            {
                throw new ArgumentOutOfRangeException(nameof(wrap));
            }

            Filter = filter;
            Wrap = wrap;
        }

        public bool Equals(TextureSampling other)
        {
            return Filter == other.Filter && Wrap == other.Wrap;
        }

        public override bool Equals(object obj)
        {
            return obj is TextureSampling other && Equals(other);
        }

        public override int GetHashCode()
        {
            return ((int)Filter * 397) ^ (int)Wrap;
        }
    }

    internal sealed class TextureSample : IEquatable<TextureSample>
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

        public bool Equals(TextureSample other)
        {
            return other != null &&
                   Source.Equals(other.Source) &&
                   Coordinates.Equals(other.Coordinates) &&
                   Sampling.Equals(other.Sampling);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TextureSample);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Source.GetHashCode();
                hash = hash * 397 ^ Coordinates.GetHashCode();
                return hash * 397 ^ Sampling.GetHashCode();
            }
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

    internal sealed class ColorSemanticValue : IEquatable<ColorSemanticValue>
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

        public bool Equals(ColorSemanticValue other)
        {
            if (other == null || Kind != other.Kind)
            {
                return false;
            }

            switch (Kind)
            {
                case ColorSemanticValueKind.Constant:
                    return _constantValue.Equals(other._constantValue);
                case ColorSemanticValueKind.TextureSample:
                    return _sample.Equals(other._sample) &&
                           _interpretation == other._interpretation;
                case ColorSemanticValueKind.TextureSampleTimesConstant:
                    return _sample.Equals(other._sample) &&
                           _interpretation == other._interpretation &&
                           _multiplier.Equals(other._multiplier);
                default:
                    return false;
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ColorSemanticValue);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                switch (Kind)
                {
                    case ColorSemanticValueKind.Constant:
                        return hash * 397 ^ _constantValue.GetHashCode();
                    case ColorSemanticValueKind.TextureSample:
                        hash = hash * 397 ^ _sample.GetHashCode();
                        return hash * 397 ^ (int)_interpretation;
                    case ColorSemanticValueKind.TextureSampleTimesConstant:
                        hash = hash * 397 ^ _sample.GetHashCode();
                        hash = hash * 397 ^ (int)_interpretation;
                        return hash * 397 ^ _multiplier.GetHashCode();
                    default:
                        return hash;
                }
            }
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
        TextureSampleTimesConstant
    }

    internal sealed class ScalarSemanticValue : IEquatable<ScalarSemanticValue>
    {
        private readonly float _constantValue;
        private readonly TextureSample _sample;
        private readonly TextureChannel _channel;
        private readonly float _multiplier;

        internal ScalarSemanticValueKind Kind { get; }

        private ScalarSemanticValue(
            ScalarSemanticValueKind kind,
            float constantValue,
            TextureSample sample,
            TextureChannel channel,
            float multiplier)
        {
            Kind = kind;
            _constantValue = constantValue;
            _sample = sample;
            _channel = channel;
            _multiplier = multiplier;
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
            if (Kind == ScalarSemanticValueKind.Constant)
            {
                throw new InvalidOperationException(
                    "A texture sample is not meaningful for this kind.");
            }

            return _sample;
        }

        internal TextureChannel GetChannel()
        {
            if (Kind == ScalarSemanticValueKind.Constant)
            {
                throw new InvalidOperationException(
                    "A texture channel is not meaningful for this kind.");
            }

            return _channel;
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

    internal sealed class NormalSemanticValue : IEquatable<NormalSemanticValue>
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

        public bool Equals(NormalSemanticValue other)
        {
            return other != null &&
                   Kind == other.Kind &&
                   (Kind == NormalSemanticValueKind.Unmodified ||
                    _sample.Equals(other._sample));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NormalSemanticValue);
        }

        public override int GetHashCode()
        {
            return Kind == NormalSemanticValueKind.Unmodified
                ? (int)Kind
                : ((int)Kind * 397) ^ _sample.GetHashCode();
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

    internal sealed class MaterialSemantics : IEquatable<MaterialSemantics>
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

        public bool Equals(MaterialSemantics other)
        {
            return other != null &&
                   BaseColor.Equals(other.BaseColor) &&
                   Alpha.Equals(other.Alpha) &&
                   Emission.Equals(other.Emission) &&
                   Normal.Equals(other.Normal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MaterialSemantics);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = BaseColor.GetHashCode();
                hash = hash * 397 ^ Alpha.GetHashCode();
                hash = hash * 397 ^ Emission.GetHashCode();
                return hash * 397 ^ Normal.GetHashCode();
            }
        }
    }
}
