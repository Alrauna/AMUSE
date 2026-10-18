using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

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
        RedChannel = 1 << 7,
        BoundedColorRange = 1 << 8,
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
            TextureEvidenceKinds.AlphaChannel |
            TextureEvidenceKinds.RedChannel |
            TextureEvidenceKinds.BoundedColorRange;

        internal string PropertyName { get; }
        internal TextureEvidenceKinds Evidence { get; }

        /// <summary>
        /// The shader property whose value is this texture's alpha cutoff,
        /// or null when the alpha is read unclipped. A declared cutoff
        /// below one binarizes the capture by the cutoff and keeps the
        /// alpha policy inert for the source: a texel between the noise
        /// gate and the shader cutoff is discarded at runtime and must
        /// never read opaque (spec section 3).
        /// </summary>
        internal string CutoffScalarProperty { get; }

        internal TexturePropertyEvidenceRequest(
            string propertyName,
            TextureEvidenceKinds evidence,
            string cutoffScalarProperty = null)
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
            if (cutoffScalarProperty != null &&
                string.IsNullOrWhiteSpace(cutoffScalarProperty))
            {
                throw new ArgumentException(
                    "A declared cutoff property name must be non-empty.",
                    nameof(cutoffScalarProperty));
            }

            PropertyName = propertyName;
            Evidence = evidence;
            CutoffScalarProperty = cutoffScalarProperty;
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

        /// <summary>
        /// Whether this request gathers the material's shader keyword set.
        /// The request model is closed: keyword facts exist on the captured
        /// evidence only when a request declared them.
        /// </summary>
        internal bool CaptureKeywords { get; }

        /// <summary>
        /// Whether the capture may take the streaming route's disk-fallback
        /// clone when the direct source-image read fails. The request model
        /// is closed: the switch rides the request, never process state, so
        /// a disallowing capture is never served a fallback-provenance
        /// cache entry.
        /// </summary>
        internal bool AllowDiskFallback { get; }

        /// <summary>
        /// The raw policy mip cap the GPU alpha route reads back through:
        /// the highest declared level the proof may consult.
        /// <see cref="int.MaxValue"/> keeps the uncapped full-chain
        /// capture. The request model is closed: the cap rides the request,
        /// never process state, so a differently capped capture is never
        /// served another cap's cached chain.
        /// </summary>
        internal int RawMipCap { get; }

        internal MaterialEvidenceRequest(
            bool shaderName,
            bool activeColorSpace,
            IEnumerable<string> presenceProperties,
            IEnumerable<string> scalarProperties,
            IEnumerable<string> colorProperties,
            IEnumerable<string> vectorProperties,
            IEnumerable<TexturePropertyEvidenceRequest> textureProperties,
            bool captureKeywords = false,
            bool allowDiskFallback = true,
            int rawMipCap = int.MaxValue)
        {
            if (rawMipCap < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rawMipCap),
                    "The raw mip cap must be a level index of at least " +
                    "zero, or int.MaxValue for no cap.");
            }

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
            CaptureKeywords = captureKeywords;
            AllowDiskFallback = allowDiskFallback;
            RawMipCap = rawMipCap;

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
            var captureKeywords = false;
            // The disk-fallback switch combines under the strictest rule:
            // one constituent request disallowing the route disallows it
            // for the combined request.
            var allowDiskFallback = true;
            // The raw mip cap combines under the same strictest rule: the
            // smallest cap reads back the fewest levels, so one capped
            // constituent caps the combined request.
            var rawMipCap = int.MaxValue;
            var presence = new SortedSet<string>(StringComparer.Ordinal);
            var scalars = new SortedSet<string>(StringComparer.Ordinal);
            var colors = new SortedSet<string>(StringComparer.Ordinal);
            var vectors = new SortedSet<string>(StringComparer.Ordinal);
            var textures = new SortedDictionary<string, TextureEvidenceKinds>(
                StringComparer.Ordinal);
            var textureCutoffs = new SortedDictionary<string, string>(
                StringComparer.Ordinal);

            foreach (var request in requests)
            {
                if (request == null)
                {
                    throw new ArgumentNullException(nameof(requests));
                }

                shaderName |= request.ShaderName;
                activeColorSpace |= request.ActiveColorSpace;
                captureKeywords |= request.CaptureKeywords;
                allowDiskFallback &= request.AllowDiskFallback;
                rawMipCap = Math.Min(rawMipCap, request.RawMipCap);
                presence.UnionWith(request.PresenceProperties);
                scalars.UnionWith(request.ScalarProperties);
                colors.UnionWith(request.ColorProperties);
                vectors.UnionWith(request.VectorProperties);
                foreach (var texture in request.TextureProperties)
                {
                    textures.TryGetValue(
                        texture.PropertyName, out var existing);
                    textures[texture.PropertyName] = existing | texture.Evidence;
                    if (texture.CutoffScalarProperty == null)
                    {
                        continue;
                    }

                    if (textureCutoffs.TryGetValue(
                            texture.PropertyName, out var declared) &&
                        declared != texture.CutoffScalarProperty)
                    {
                        throw new ArgumentException(
                            "One texture property cannot declare two " +
                            "different cutoff properties.",
                            nameof(requests));
                    }

                    textureCutoffs[texture.PropertyName] =
                        texture.CutoffScalarProperty;
                }
            }

            var textureRequests = new List<TexturePropertyEvidenceRequest>(
                textures.Count);
            foreach (var texture in textures)
            {
                textureCutoffs.TryGetValue(texture.Key, out var cutoff);
                textureRequests.Add(new TexturePropertyEvidenceRequest(
                    texture.Key, texture.Value, cutoff));
            }

            return new MaterialEvidenceRequest(
                shaderName,
                activeColorSpace,
                presence,
                scalars,
                colors,
                vectors,
                textureRequests,
                captureKeywords,
                allowDiskFallback,
                rawMipCap);
        }

        /// <summary>
        /// The same request with the raw policy mip cap set. The orchestrator
        /// that knows the build's optimizer policy applies it here, so the
        /// cap rides the request into the closed capture exactly like the
        /// disk-fallback switch. The uncapped request returns unchanged.
        /// </summary>
        internal MaterialEvidenceRequest WithRawMipCap(int rawMipCap)
        {
            if (rawMipCap < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rawMipCap),
                    "The raw mip cap must be a level index of at least " +
                    "zero, or int.MaxValue for no cap.");
            }

            if (rawMipCap == RawMipCap)
            {
                return this;
            }

            return new MaterialEvidenceRequest(
                ShaderName,
                ActiveColorSpace,
                PresenceProperties,
                ScalarProperties,
                ColorProperties,
                VectorProperties,
                TextureProperties,
                CaptureKeywords,
                AllowDiskFallback,
                rawMipCap);
        }

        /// <summary>
        /// The same request with the disk-fallback route withdrawn. The
        /// DryRun capture uses this so its no-asset-writes guarantee rides
        /// the request instead of process state.
        /// </summary>
        internal MaterialEvidenceRequest DisallowingDiskFallback()
        {
            return new MaterialEvidenceRequest(
                ShaderName,
                ActiveColorSpace,
                PresenceProperties,
                ScalarProperties,
                ColorProperties,
                VectorProperties,
                TextureProperties,
                CaptureKeywords,
                allowDiskFallback: false,
                rawMipCap: RawMipCap);
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

        /// <summary>The schema this capture gathers: in the closed batch
        /// path, the union of every admitted family's request, so one
        /// capture serves all readers.</summary>
        internal MaterialEvidenceRequest Request { get; }

        /// <summary>
        /// The source material's own family alpha request, or null when the
        /// caller captured outside the closed batch. Texture cutoff
        /// declarations are per-family predicates, so the batch union's
        /// declaration for a texture property binarizes a field only for a
        /// material whose own family declared it. Without this gate, one
        /// cutout material in a merged renderer would binarize every
        /// sibling's field for the same texture property, and the sibling's
        /// exact-alpha interpretation would read those verdict bytes as
        /// "alpha exactly one".
        /// </summary>
        internal MaterialEvidenceRequest PredicateRequest { get; }

        internal MaterialEvidenceCaptureInput(
            Material sourceMaterial,
            MaterialEvidenceRequest request)
        {
            SourceMaterial = sourceMaterial;
            Request = request;
            PredicateRequest = null;
        }

        internal MaterialEvidenceCaptureInput(
            Material sourceMaterial,
            MaterialEvidenceRequest request,
            MaterialEvidenceRequest predicateRequest)
        {
            SourceMaterial = sourceMaterial;
            Request = request;
            PredicateRequest = predicateRequest;
        }
    }
}
