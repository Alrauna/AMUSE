using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    [Flags]
    internal enum TextureEvidenceKinds
    {
        None = 0,
        ScaleOffset = 1 << 0,
        SourceIdentity = 1 << 1,
        Sampling = 1 << 2,
        ColorInterpretation = 1 << 3,
        SampledAlphaIsOne = 1 << 4,
        CanonicalNormalMap = 1 << 5,
        AlphaChannel = 1 << 6,
    }

    internal readonly struct TexturePropertyEvidenceRequest
    {
        private const TextureEvidenceKinds AllEvidence =
            TextureEvidenceKinds.ScaleOffset |
            TextureEvidenceKinds.SourceIdentity |
            TextureEvidenceKinds.Sampling |
            TextureEvidenceKinds.ColorInterpretation |
            TextureEvidenceKinds.SampledAlphaIsOne |
            TextureEvidenceKinds.CanonicalNormalMap |
            TextureEvidenceKinds.AlphaChannel;

        internal string PropertyName { get; }
        internal TextureEvidenceKinds Evidence { get; }

        internal TexturePropertyEvidenceRequest(
            string propertyName,
            TextureEvidenceKinds evidence)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException(
                    "Texture property name must be non-empty.",
                    nameof(propertyName));
            }
            if ((evidence & ~AllEvidence) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(evidence));
            }

            PropertyName = propertyName;
            Evidence = evidence;
        }
    }

    internal sealed class MaterialEvidenceRequest
    {
        internal bool ShaderName { get; }
        internal bool ActiveColorSpace { get; }
        internal IReadOnlyCollection<string> PresenceProperties { get; }
        internal IReadOnlyCollection<string> ScalarProperties { get; }
        internal IReadOnlyCollection<string> ColorProperties { get; }
        internal IReadOnlyCollection<string> VectorProperties { get; }
        internal IReadOnlyList<TexturePropertyEvidenceRequest> TextureProperties { get; }

        internal MaterialEvidenceRequest(
            bool shaderName,
            bool activeColorSpace,
            IEnumerable<string> presenceProperties,
            IEnumerable<string> scalarProperties,
            IEnumerable<string> colorProperties,
            IEnumerable<string> vectorProperties,
            IEnumerable<TexturePropertyEvidenceRequest> textureProperties)
        {
            ShaderName = shaderName;
            ActiveColorSpace = activeColorSpace;
            PresenceProperties = CopyNames(
                presenceProperties, nameof(presenceProperties));
            ScalarProperties = CopyNames(
                scalarProperties, nameof(scalarProperties));
            ColorProperties = CopyNames(
                colorProperties, nameof(colorProperties));
            VectorProperties = CopyNames(
                vectorProperties, nameof(vectorProperties));
            TextureProperties = CopyTextures(textureProperties);

            var typed = new HashSet<string>(StringComparer.Ordinal);
            AddTyped(typed, ScalarProperties);
            AddTyped(typed, ColorProperties);
            AddTyped(typed, VectorProperties);
            foreach (var texture in TextureProperties)
            {
                if (!typed.Add(texture.PropertyName))
                {
                    throw new ArgumentException(
                        "A property cannot be requested under incompatible " +
                        "typed categories.",
                        nameof(textureProperties));
                }
            }
        }

        internal static MaterialEvidenceRequest Combine(
            params MaterialEvidenceRequest[] requests)
        {
            if (requests == null)
            {
                throw new ArgumentNullException(nameof(requests));
            }

            var shaderName = false;
            var activeColorSpace = false;
            var presence = new SortedSet<string>(StringComparer.Ordinal);
            var scalars = new SortedSet<string>(StringComparer.Ordinal);
            var colors = new SortedSet<string>(StringComparer.Ordinal);
            var vectors = new SortedSet<string>(StringComparer.Ordinal);
            var textures = new SortedDictionary<string, TextureEvidenceKinds>(
                StringComparer.Ordinal);

            foreach (var request in requests)
            {
                if (request == null)
                {
                    throw new ArgumentNullException(nameof(requests));
                }

                shaderName |= request.ShaderName;
                activeColorSpace |= request.ActiveColorSpace;
                presence.UnionWith(request.PresenceProperties);
                scalars.UnionWith(request.ScalarProperties);
                colors.UnionWith(request.ColorProperties);
                vectors.UnionWith(request.VectorProperties);
                foreach (var texture in request.TextureProperties)
                {
                    textures.TryGetValue(
                        texture.PropertyName, out var existing);
                    textures[texture.PropertyName] = existing | texture.Evidence;
                }
            }

            var textureRequests = new List<TexturePropertyEvidenceRequest>(
                textures.Count);
            foreach (var texture in textures)
            {
                textureRequests.Add(new TexturePropertyEvidenceRequest(
                    texture.Key, texture.Value));
            }

            return new MaterialEvidenceRequest(
                shaderName,
                activeColorSpace,
                presence,
                scalars,
                colors,
                vectors,
                textureRequests);
        }

        private static IReadOnlyCollection<string> CopyNames(
            IEnumerable<string> values,
            string parameterName)
        {
            if (values == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            var unique = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(
                        "Property names must be non-empty.", parameterName);
                }
                if (!unique.Add(value))
                {
                    throw new ArgumentException(
                        "A property cannot be requested twice in one category.",
                        parameterName);
                }
            }

            return new ReadOnlyCollection<string>(
                new List<string>(unique));
        }

        private static IReadOnlyList<TexturePropertyEvidenceRequest> CopyTextures(
            IEnumerable<TexturePropertyEvidenceRequest> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var unique = new SortedDictionary<
                string, TexturePropertyEvidenceRequest>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value.PropertyName))
                {
                    throw new ArgumentException(
                        "Texture property names must be non-empty.",
                        nameof(values));
                }
                if (unique.ContainsKey(value.PropertyName))
                {
                    throw new ArgumentException(
                        "A texture property cannot be requested twice.",
                        nameof(values));
                }

                unique.Add(value.PropertyName, value);
            }

            return new ReadOnlyCollection<TexturePropertyEvidenceRequest>(
                new List<TexturePropertyEvidenceRequest>(unique.Values));
        }

        private static void AddTyped(
            HashSet<string> typed,
            IEnumerable<string> properties)
        {
            foreach (var property in properties)
            {
                if (!typed.Add(property))
                {
                    throw new ArgumentException(
                        "A property cannot be requested under incompatible " +
                        "typed categories.");
                }
            }
        }
    }

    internal readonly struct MaterialEvidenceCaptureInput
    {
        internal Material SourceMaterial { get; }
        internal MaterialEvidenceRequest Request { get; }

        internal MaterialEvidenceCaptureInput(
            Material sourceMaterial,
            MaterialEvidenceRequest request)
        {
            SourceMaterial = sourceMaterial;
            Request = request;
        }
    }

    internal sealed class CapturedTextureEvidence
    {
        internal bool HasSourceIdentity { get; }
        internal TextureSourceId SourceIdentity { get; }
        internal bool HasSampling { get; }
        internal TextureSampling Sampling { get; }
        internal bool HasColorInterpretation { get; }
        internal TextureColorInterpretation ColorInterpretation { get; }
        internal bool SampledAlphaIsProvenOne { get; }
        internal bool IsCanonicalNormalMap { get; }
        internal bool HasAlphaChannel { get; }
        internal AlphaTextureData AlphaChannel { get; }

        internal CapturedTextureEvidence(
            bool hasSourceIdentity,
            TextureSourceId sourceIdentity,
            bool hasSampling,
            TextureSampling sampling,
            bool hasColorInterpretation,
            TextureColorInterpretation colorInterpretation,
            bool sampledAlphaIsProvenOne,
            bool isCanonicalNormalMap,
            bool hasAlphaChannel,
            AlphaTextureData alphaChannel)
        {
            HasSourceIdentity = hasSourceIdentity;
            SourceIdentity = sourceIdentity;
            HasSampling = hasSampling;
            Sampling = sampling;
            HasColorInterpretation = hasColorInterpretation;
            ColorInterpretation = colorInterpretation;
            SampledAlphaIsProvenOne = sampledAlphaIsProvenOne;
            IsCanonicalNormalMap = isCanonicalNormalMap;
            HasAlphaChannel = hasAlphaChannel;
            AlphaChannel = alphaChannel;
        }
    }

    internal readonly struct CapturedTextureAssignment
    {
        internal bool IsAssigned { get; }
        internal TextureEvidenceKinds RequestedEvidence { get; }
        internal bool HasScaleOffset { get; }
        internal Vector2 Scale { get; }
        internal Vector2 Offset { get; }
        internal CapturedTextureEvidence Texture { get; }

        internal CapturedTextureAssignment(
            bool isAssigned,
            TextureEvidenceKinds requestedEvidence,
            bool hasScaleOffset,
            Vector2 scale,
            Vector2 offset,
            CapturedTextureEvidence texture)
        {
            IsAssigned = isAssigned;
            RequestedEvidence = requestedEvidence;
            HasScaleOffset = hasScaleOffset;
            Scale = scale;
            Offset = offset;
            Texture = texture;
        }
    }

    internal sealed class CapturedMaterialEvidence
    {
        private readonly PresenceEntry[] _presence;
        private readonly ScalarEntry[] _scalars;
        private readonly ColorEntry[] _colors;
        private readonly VectorEntry[] _vectors;
        private readonly TextureEntry[] _textureAssignments;

        internal bool HasShaderName { get; }
        internal string ShaderName { get; }
        internal bool HasActiveColorSpace { get; }
        internal ColorSpace ActiveColorSpace { get; }
        internal IReadOnlyCollection<CapturedTextureEvidence> Textures { get; }

        internal CapturedMaterialEvidence(
            bool hasShaderName,
            string shaderName,
            bool hasActiveColorSpace,
            ColorSpace activeColorSpace,
            PresenceEntry[] presence,
            ScalarEntry[] scalars,
            ColorEntry[] colors,
            VectorEntry[] vectors,
            TextureEntry[] textureAssignments,
            IReadOnlyCollection<CapturedTextureEvidence> textures)
        {
            HasShaderName = hasShaderName;
            ShaderName = shaderName;
            HasActiveColorSpace = hasActiveColorSpace;
            ActiveColorSpace = activeColorSpace;
            _presence = presence;
            _scalars = scalars;
            _colors = colors;
            _vectors = vectors;
            _textureAssignments = textureAssignments;
            Textures = textures;
        }

        internal bool HasProperty(string name)
        {
            ValidateGetterName(name);
            foreach (var entry in _presence)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    return entry.IsPresent;
                }
            }

            throw Unrequested(name);
        }

        internal bool TryGetScalar(string name, out float value)
        {
            ValidateGetterName(name);
            foreach (var entry in _scalars)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return entry.HasValue;
                }
            }

            value = default;
            throw Unrequested(name);
        }

        internal bool TryGetColor(string name, out Color value)
        {
            ValidateGetterName(name);
            foreach (var entry in _colors)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return entry.HasValue;
                }
            }

            value = default;
            throw Unrequested(name);
        }

        internal bool TryGetVector(string name, out Vector4 value)
        {
            ValidateGetterName(name);
            foreach (var entry in _vectors)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return entry.HasValue;
                }
            }

            value = default;
            throw Unrequested(name);
        }

        internal bool TryGetTexture(
            string name,
            out CapturedTextureAssignment value)
        {
            ValidateGetterName(name);
            foreach (var entry in _textureAssignments)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    value = entry.Value;
                    return entry.HasValue;
                }
            }

            value = default;
            throw Unrequested(name);
        }

        private static void ValidateGetterName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Property name must be non-empty.", nameof(name));
            }
        }

        private static ArgumentException Unrequested(string name)
        {
            return new ArgumentException(
                "Property '" + name + "' was not requested.", nameof(name));
        }

        internal readonly struct PresenceEntry
        {
            internal string Name { get; }
            internal bool IsPresent { get; }

            internal PresenceEntry(string name, bool isPresent)
            {
                Name = name;
                IsPresent = isPresent;
            }
        }

        internal readonly struct ScalarEntry
        {
            internal string Name { get; }
            internal bool HasValue { get; }
            internal float Value { get; }

            internal ScalarEntry(string name, bool hasValue, float value)
            {
                Name = name;
                HasValue = hasValue;
                Value = value;
            }
        }

        internal readonly struct ColorEntry
        {
            internal string Name { get; }
            internal bool HasValue { get; }
            internal Color Value { get; }

            internal ColorEntry(string name, bool hasValue, Color value)
            {
                Name = name;
                HasValue = hasValue;
                Value = value;
            }
        }

        internal readonly struct VectorEntry
        {
            internal string Name { get; }
            internal bool HasValue { get; }
            internal Vector4 Value { get; }

            internal VectorEntry(string name, bool hasValue, Vector4 value)
            {
                Name = name;
                HasValue = hasValue;
                Value = value;
            }
        }

        internal readonly struct TextureEntry
        {
            internal string Name { get; }
            internal bool HasValue { get; }
            internal CapturedTextureAssignment Value { get; }

            internal TextureEntry(
                string name,
                bool hasValue,
                CapturedTextureAssignment value)
            {
                Name = name;
                HasValue = hasValue;
                Value = value;
            }
        }
    }

    internal static class UnityMaterialEvidenceCapture
    {
        internal static IReadOnlyList<CapturedMaterialEvidence> Capture(
            IReadOnlyList<MaterialEvidenceCaptureInput> inputs)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            var materials = new MaterialBuilder[inputs.Count];
            var identified = new Dictionary<TextureSourceId, SharedTextureBuilder>();
            for (var index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                if (input.Request == null)
                {
                    throw new ArgumentNullException(nameof(inputs));
                }

                materials[index] = CaptureAssignments(input);
                foreach (var texture in materials[index].Textures)
                {
                    if (texture.Texture == null ||
                        !UnityTextureEvidence.TryGetSourceId(
                            texture.Texture, out var source))
                    {
                        continue;
                    }

                    if (!identified.TryGetValue(source, out var shared))
                    {
                        shared = new SharedTextureBuilder(
                            texture.Texture, source);
                        identified.Add(source, shared);
                    }

                    shared.Evidence |= texture.RequestedEvidence;
                    texture.Shared = shared;
                }
            }

            foreach (var shared in identified.Values)
            {
                shared.Captured = CaptureTexture(
                    shared.Texture,
                    shared.Evidence,
                    true,
                    shared.Source);
            }

            var results = new CapturedMaterialEvidence[materials.Length];
            for (var index = 0; index < materials.Length; index++)
            {
                var material = materials[index];
                var textureEntries = new CapturedMaterialEvidence.TextureEntry[
                    material.Textures.Count];
                var distinctTextures = new List<CapturedTextureEvidence>();
                for (var textureIndex = 0;
                     textureIndex < material.Textures.Count;
                     textureIndex++)
                {
                    var texture = material.Textures[textureIndex];
                    if (!texture.HasValue)
                    {
                        textureEntries[textureIndex] =
                            new CapturedMaterialEvidence.TextureEntry(
                                texture.Name, false, default);
                        continue;
                    }

                    CapturedTextureEvidence capturedTexture = null;
                    if (texture.Texture != null)
                    {
                        capturedTexture = texture.Shared != null
                            ? texture.Shared.Captured
                            : CaptureTexture(
                                texture.Texture,
                                texture.RequestedEvidence,
                                false,
                                default);
                        if (!distinctTextures.Contains(capturedTexture))
                        {
                            distinctTextures.Add(capturedTexture);
                        }
                    }

                    var assignment = new CapturedTextureAssignment(
                        texture.Texture != null,
                        texture.RequestedEvidence,
                        texture.HasScaleOffset,
                        texture.Scale,
                        texture.Offset,
                        capturedTexture);
                    textureEntries[textureIndex] =
                        new CapturedMaterialEvidence.TextureEntry(
                            texture.Name, true, assignment);
                }

                results[index] = new CapturedMaterialEvidence(
                    material.HasShaderName,
                    material.ShaderName,
                    material.HasActiveColorSpace,
                    material.ActiveColorSpace,
                    material.Presence,
                    material.Scalars,
                    material.Colors,
                    material.Vectors,
                    textureEntries,
                    new ReadOnlyCollection<CapturedTextureEvidence>(
                        distinctTextures));
            }

            return new ReadOnlyCollection<CapturedMaterialEvidence>(results);
        }

        private static MaterialBuilder CaptureAssignments(
            MaterialEvidenceCaptureInput input)
        {
            var request = input.Request;
            var material = input.SourceMaterial;
            var isLive = material != null && material.shader != null;
            var facts = new Dictionary<string, PropertyFact>(
                StringComparer.Ordinal);

            foreach (var property in AllRequestedNames(request))
            {
                if (!isLive || !material.HasProperty(property))
                {
                    facts.Add(property, default);
                    continue;
                }

                var propertyIndex = material.shader.FindPropertyIndex(property);
                facts.Add(
                    property,
                    propertyIndex < 0
                        ? new PropertyFact(true, false, default)
                        : new PropertyFact(
                            true,
                            true,
                            material.shader.GetPropertyType(propertyIndex)));
            }

            var builder = new MaterialBuilder
            {
                HasShaderName = isLive && request.ShaderName,
                ShaderName = isLive && request.ShaderName
                    ? material.shader.name
                    : null,
                HasActiveColorSpace = isLive && request.ActiveColorSpace,
                ActiveColorSpace = isLive && request.ActiveColorSpace
                    ? QualitySettings.activeColorSpace
                    : default,
                Presence = CapturePresence(request, facts),
                Scalars = CaptureScalars(material, request, facts),
                Colors = CaptureColors(material, request, facts),
                Vectors = CaptureVectors(material, request, facts),
            };

            foreach (var textureRequest in request.TextureProperties)
            {
                var fact = facts[textureRequest.PropertyName];
                var hasValue = isLive && fact.HasType &&
                               fact.Type == ShaderPropertyType.Texture;
                var texture = hasValue
                    ? material.GetTexture(textureRequest.PropertyName)
                    : null;
                var hasScaleOffset = hasValue &&
                    (textureRequest.Evidence & TextureEvidenceKinds.ScaleOffset) != 0;
                builder.Textures.Add(new TextureAssignmentBuilder
                {
                    Name = textureRequest.PropertyName,
                    HasValue = hasValue,
                    RequestedEvidence = textureRequest.Evidence,
                    HasScaleOffset = hasScaleOffset,
                    Scale = hasScaleOffset
                        ? material.GetTextureScale(textureRequest.PropertyName)
                        : default,
                    Offset = hasScaleOffset
                        ? material.GetTextureOffset(textureRequest.PropertyName)
                        : default,
                    Texture = texture,
                });
            }

            return builder;
        }

        private static IEnumerable<string> AllRequestedNames(
            MaterialEvidenceRequest request)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            names.UnionWith(request.PresenceProperties);
            names.UnionWith(request.ScalarProperties);
            names.UnionWith(request.ColorProperties);
            names.UnionWith(request.VectorProperties);
            foreach (var texture in request.TextureProperties)
            {
                names.Add(texture.PropertyName);
            }

            return names;
        }

        private static CapturedMaterialEvidence.PresenceEntry[] CapturePresence(
            MaterialEvidenceRequest request,
            IReadOnlyDictionary<string, PropertyFact> facts)
        {
            var entries = new CapturedMaterialEvidence.PresenceEntry[
                request.PresenceProperties.Count];
            var index = 0;
            foreach (var name in request.PresenceProperties)
            {
                entries[index++] = new CapturedMaterialEvidence.PresenceEntry(
                    name, facts[name].IsPresent);
            }

            return entries;
        }

        private static CapturedMaterialEvidence.ScalarEntry[] CaptureScalars(
            Material material,
            MaterialEvidenceRequest request,
            IReadOnlyDictionary<string, PropertyFact> facts)
        {
            var entries = new CapturedMaterialEvidence.ScalarEntry[
                request.ScalarProperties.Count];
            var index = 0;
            foreach (var name in request.ScalarProperties)
            {
                var fact = facts[name];
                var hasValue = fact.HasType &&
                    (fact.Type == ShaderPropertyType.Float ||
                     fact.Type == ShaderPropertyType.Range ||
                     fact.Type == ShaderPropertyType.Int);
                var value = hasValue
                    ? fact.Type == ShaderPropertyType.Int
                        ? material.GetInteger(name)
                        : material.GetFloat(name)
                    : default;
                entries[index++] = new CapturedMaterialEvidence.ScalarEntry(
                    name, hasValue, value);
            }

            return entries;
        }

        private static CapturedMaterialEvidence.ColorEntry[] CaptureColors(
            Material material,
            MaterialEvidenceRequest request,
            IReadOnlyDictionary<string, PropertyFact> facts)
        {
            var entries = new CapturedMaterialEvidence.ColorEntry[
                request.ColorProperties.Count];
            var index = 0;
            foreach (var name in request.ColorProperties)
            {
                var hasValue = facts[name].HasType &&
                    facts[name].Type == ShaderPropertyType.Color;
                entries[index++] = new CapturedMaterialEvidence.ColorEntry(
                    name,
                    hasValue,
                    hasValue ? material.GetColor(name) : default);
            }

            return entries;
        }

        private static CapturedMaterialEvidence.VectorEntry[] CaptureVectors(
            Material material,
            MaterialEvidenceRequest request,
            IReadOnlyDictionary<string, PropertyFact> facts)
        {
            var entries = new CapturedMaterialEvidence.VectorEntry[
                request.VectorProperties.Count];
            var index = 0;
            foreach (var name in request.VectorProperties)
            {
                var hasValue = facts[name].HasType &&
                    facts[name].Type == ShaderPropertyType.Vector;
                entries[index++] = new CapturedMaterialEvidence.VectorEntry(
                    name,
                    hasValue,
                    hasValue ? material.GetVector(name) : default);
            }

            return entries;
        }

        private static CapturedTextureEvidence CaptureTexture(
            Texture texture,
            TextureEvidenceKinds evidence,
            bool hasKnownSource,
            TextureSourceId knownSource)
        {
            var hasSource = hasKnownSource &&
                (evidence & TextureEvidenceKinds.SourceIdentity) != 0;
            var source = hasSource ? knownSource : default;
            var sampling = default(TextureSampling);
            var hasSampling =
                (evidence & TextureEvidenceKinds.Sampling) != 0 &&
                UnityTextureEvidence.TryGetSampling(texture, out sampling);
            var colorInterpretation = default(TextureColorInterpretation);
            var hasColorInterpretation =
                (evidence & TextureEvidenceKinds.ColorInterpretation) != 0 &&
                UnityTextureEvidence.TryGetColorInterpretation(
                    texture, out colorInterpretation);
            var sampledAlphaIsOne =
                (evidence & TextureEvidenceKinds.SampledAlphaIsOne) != 0 &&
                UnityTextureEvidence.TryProveSampledAlphaIsOne(texture);
            var canonicalNormal =
                (evidence & TextureEvidenceKinds.CanonicalNormalMap) != 0 &&
                UnityTextureEvidence.IsCanonicalNormalMapImport(texture);
            AlphaTextureData alphaChannel = null;
            var hasAlphaChannel =
                (evidence & TextureEvidenceKinds.AlphaChannel) != 0 &&
                UnityAlphaFieldEvidence.TryCapture(
                    texture, out _, out alphaChannel);

            return new CapturedTextureEvidence(
                hasSource,
                source,
                hasSampling,
                sampling,
                hasColorInterpretation,
                colorInterpretation,
                sampledAlphaIsOne,
                canonicalNormal,
                hasAlphaChannel,
                alphaChannel);
        }

        private readonly struct PropertyFact
        {
            internal bool IsPresent { get; }
            internal bool HasType { get; }
            internal ShaderPropertyType Type { get; }

            internal PropertyFact(
                bool isPresent,
                bool hasType,
                ShaderPropertyType type)
            {
                IsPresent = isPresent;
                HasType = hasType;
                Type = type;
            }
        }

        private sealed class MaterialBuilder
        {
            internal bool HasShaderName;
            internal string ShaderName;
            internal bool HasActiveColorSpace;
            internal ColorSpace ActiveColorSpace;
            internal CapturedMaterialEvidence.PresenceEntry[] Presence;
            internal CapturedMaterialEvidence.ScalarEntry[] Scalars;
            internal CapturedMaterialEvidence.ColorEntry[] Colors;
            internal CapturedMaterialEvidence.VectorEntry[] Vectors;
            internal readonly List<TextureAssignmentBuilder> Textures =
                new List<TextureAssignmentBuilder>();
        }

        private sealed class TextureAssignmentBuilder
        {
            internal string Name;
            internal bool HasValue;
            internal TextureEvidenceKinds RequestedEvidence;
            internal bool HasScaleOffset;
            internal Vector2 Scale;
            internal Vector2 Offset;
            internal Texture Texture;
            internal SharedTextureBuilder Shared;
        }

        private sealed class SharedTextureBuilder
        {
            internal readonly Texture Texture;
            internal readonly TextureSourceId Source;
            internal TextureEvidenceKinds Evidence;
            internal CapturedTextureEvidence Captured;

            internal SharedTextureBuilder(
                Texture texture,
                TextureSourceId source)
            {
                Texture = texture;
                Source = source;
            }
        }
    }
}
