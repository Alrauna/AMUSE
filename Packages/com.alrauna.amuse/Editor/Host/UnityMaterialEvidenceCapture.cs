using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Host
{
    internal static class UnityMaterialEvidenceCapture
    {
        internal static IReadOnlyList<CapturedMaterialEvidence> Capture(
            IReadOnlyList<MaterialEvidenceCaptureInput> inputs)
        {
            // Semantics-interpretation captures keep the inert bounds, which
            // reproduce the base exact-255 contract: the policy widens the
            // proof's evidence, never an admission decision. The closed-batch
            // proof capture threads the active policy through the full
            // overload.
            return Capture(inputs, AlphaPolicyBounds.Inert);
        }

        internal static IReadOnlyList<CapturedMaterialEvidence> Capture(
            IReadOnlyList<MaterialEvidenceCaptureInput> inputs,
            AlphaPolicyBounds bounds)
        {
            // The family-schema routes keep the smallest-cap bucket merge:
            // conversion eligibility, attestation, and opaque-target reads
            // never carry the build's arm values.
            return CaptureCore(inputs, bounds, null, null);
        }

        /// <summary>
        /// The closed-batch capture with one cap per proof arm. Each shared
        /// bucket keys on the captured alpha threshold, so a bucket whose
        /// threshold sits below one reads the cutout arm's cap and a bucket
        /// at the exact threshold reads the exact arm's cap, in each case
        /// intersected with the request's own cap. A cutoff declared
        /// exactly one therefore captures with the exact arm, the way the
        /// exact-255 proof reads it. The unshared fallback keeps the
        /// request cap, which the caller stamps with the wider of the two
        /// arms, so the fallback over-captures and never under-captures.
        /// </summary>
        internal static IReadOnlyList<CapturedMaterialEvidence> Capture(
            IReadOnlyList<MaterialEvidenceCaptureInput> inputs,
            AlphaPolicyBounds bounds,
            int cutoutRawMipCap,
            int exactRawMipCap)
        {
            return CaptureCore(
                inputs, bounds, cutoutRawMipCap, exactRawMipCap);
        }

        private static IReadOnlyList<CapturedMaterialEvidence> CaptureCore(
            IReadOnlyList<MaterialEvidenceCaptureInput> inputs,
            AlphaPolicyBounds bounds,
            int? cutoutRawMipCap,
            int? exactRawMipCap)
        {
            if (inputs == null)
            {
                throw new ArgumentNullException(nameof(inputs));
            }

            var materials = new MaterialBuilder[inputs.Count];
            var identified = new Dictionary<
                TextureSourceId,
                Dictionary<float, SharedTextureBuilder>>();
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
                            texture.Texture, out var source,
                            input.SourceMaterial))
                    {
                        continue;
                    }

                    if (!identified.TryGetValue(source, out var sharedByThreshold))
                    {
                        sharedByThreshold =
                            new Dictionary<float, SharedTextureBuilder>();
                        identified.Add(source, sharedByThreshold);
                    }

                    if (!sharedByThreshold.TryGetValue(
                            texture.CutoutThreshold, out var shared))
                    {
                        shared = new SharedTextureBuilder(
                            texture.Texture, source, texture.CutoutThreshold,
                            input.SourceMaterial);
                        sharedByThreshold.Add(
                            texture.CutoutThreshold, shared);
                    }

                    // Evidence for one texture source at one alpha predicate
                    // is captured once for the whole batch, so this union is
                    // deliberately batch-wide within the bucket: the single
                    // captured object handed to every assignment of that
                    // source and threshold may carry facts a different
                    // material's request asked for. That is a capture-cost
                    // decision, not a widening of any request - a consumer
                    // must still read only the facts its own request named,
                    // exactly as the material-property getters enforce by
                    // throwing for an unrequested name. The binarization
                    // threshold is part of the field's meaning: a texel
                    // between two materials' declared cutoffs is opaque
                    // under the lower cutoff and not under the higher, so
                    // assignments with different thresholds must never
                    // share one field, whichever material was captured
                    // first.
                    shared.Evidence |= texture.RequestedEvidence;
                    shared.AllowDiskFallback &= input.Request.AllowDiskFallback;
                    if (cutoutRawMipCap == null)
                    {
                        // The strictest contributing request governs the
                        // shared capture: the smallest cap reads back the
                        // fewest levels for the whole bucket.
                        shared.RawMipCap = Math.Min(
                            shared.RawMipCap, input.Request.RawMipCap);
                    }
                    else
                    {
                        // The closed batch pins the bucket to its arm's
                        // cap, intersected with the request's own cap.
                        // The bucket threshold is the key, so every
                        // contributing input computes the same arm cap,
                        // and the narrower of the two caps governs.
                        var bucketCap = Math.Min(
                            texture.CutoutThreshold < 1f
                                ? cutoutRawMipCap.Value
                                : exactRawMipCap.Value,
                            input.Request.RawMipCap);
                        shared.RawMipCap = Math.Min(
                            shared.RawMipCap, bucketCap);
                    }
                    texture.Shared = shared;
                }
            }
            foreach (var sharedByThreshold in identified.Values)
            {
                foreach (var shared in sharedByThreshold.Values)
                {
                    shared.Captured = CaptureTexture(
                        shared.Texture,
                        shared.Evidence,
                        true,
                        shared.Source,
                        bounds,
                        shared.OriginMaterial,
                        shared.CutoutThreshold,
                        shared.AllowDiskFallback,
                        shared.RawMipCap);
                }
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
                                default,
                                bounds,
                                material.SourceMaterial,
                                texture.CutoutThreshold,
                                material.AllowDiskFallback,
                                material.RawMipCap);
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
                        capturedTexture,
                        BuildCaptureRefusals(
                            texture.Name,
                            texture.Texture,
                            capturedTexture));
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
                        distinctTextures),
                    material.Keywords);
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
                AllowDiskFallback = request.AllowDiskFallback,
                RawMipCap = request.RawMipCap,
                SourceMaterial = input.SourceMaterial,
                ShaderName = isLive && request.ShaderName
                    ? material.shader.name
                    : null,
                HasActiveColorSpace = isLive && request.ActiveColorSpace,
                ActiveColorSpace = isLive && request.ActiveColorSpace
                    ? QualitySettings.activeColorSpace
                    : default,
                Keywords = request.CaptureKeywords
                    ? CaptureKeywords(material, isLive)
                    : Array.Empty<string>(),
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
                // A family that declares its cutoff scalar hands the real
                // value to the capture, whose routes binarize by it and
                // keep the alpha policy inert for the source. A family
                // that declares none reads the unclipped exact-255 arm,
                // where the policy applies. The declaration is a
                // per-family predicate: in the closed batch the union
                // carries every family's schema, so a sibling family's
                // declaration must not binarize this material's field -
                // this material's own alpha request decides.
                var cutoutThreshold = 1.0f;
                if (textureRequest.CutoffScalarProperty != null &&
                    DeclaresCutoffFor(input, textureRequest.PropertyName))
                {
                    var cutoffFact =
                        facts[textureRequest.CutoffScalarProperty];
                    if (isLive && cutoffFact.HasType &&
                        (cutoffFact.Type == ShaderPropertyType.Float ||
                         cutoffFact.Type == ShaderPropertyType.Range))
                    {
                        cutoutThreshold = material.GetFloat(
                            textureRequest.CutoffScalarProperty);
                    }
                }

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
                    CutoutThreshold = cutoutThreshold,
                });
            }

            return builder;
        }

        /// <summary>
        /// Whether the input's own family alpha request declares a cutoff
        /// scalar for this texture property. A null predicate request -
        /// a capture outside the closed batch - keeps the union request's
        /// declaration, which is then the caller's own. The gate is what
        /// keeps one family's cutoff declaration from binarizing another
        /// family's field when both share one batch.
        /// </summary>
        private static bool DeclaresCutoffFor(
            MaterialEvidenceCaptureInput input,
            string propertyName)
        {
            var predicate = input.PredicateRequest;
            if (predicate == null)
            {
                return true;
            }

            foreach (var texture in predicate.TextureProperties)
            {
                if (string.Equals(
                        texture.PropertyName,
                        propertyName,
                        StringComparison.Ordinal))
                {
                    return texture.CutoffScalarProperty != null;
                }
            }

            return false;
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

        /// <summary>
        /// The material's live shader keyword set as one ordinal-sorted,
        /// deduplicated snapshot. Called only when the request asked for
        /// keywords; a dead material carries no keyword facts and yields an
        /// empty set, never null.
        /// </summary>
        private static string[] CaptureKeywords(
            Material material,
            bool isLive)
        {
            if (!isLive)
            {
                return Array.Empty<string>();
            }

            var unique = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var keyword in material.shaderKeywords)
            {
                unique.Add(keyword);
            }

            var keywords = new string[unique.Count];
            unique.CopyTo(keywords);
            return keywords;
        }

        private static CapturedTextureEvidence CaptureTexture(
            Texture texture,
            TextureEvidenceKinds evidence,
            bool hasKnownSource,
            TextureSourceId knownSource,
            AlphaPolicyBounds bounds,
            Material originMaterial,
            float cutoffThreshold = 1.0f,
            bool allowDiskFallback = true,
            int rawMipCap = int.MaxValue)
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
            var hasBoundedColorRange =
                (evidence & TextureEvidenceKinds.BoundedColorRange) != 0 &&
                UnityTextureEvidence.TryProveColorValuesInUnitRange(texture);
            var sampledAlphaIsOne =
                (evidence & TextureEvidenceKinds.SampledAlphaIsOne) != 0 &&
                UnityTextureEvidence.TryProveSampledAlphaIsOne(texture);
            var canonicalNormal =
                (evidence & TextureEvidenceKinds.CanonicalNormalMap) != 0 &&
                UnityTextureEvidence.IsCanonicalNormalMapImport(texture);
            AlphaMipChain alphaChannel = null;
            var alphaRefusal = TextureCaptureRefusalReason.None;
            // The alpha channel reads under the active policy. The red channel
            // reads under the inert bounds: the Analysis mapped arm evaluates a
            // red field sample at exactly 1, so byte 255 must mark exactly the
            // texels whose value is exactly 1. An active policy verdict on red
            // would store 255 for a sub-one texel and break the mapped contract.
            var hasAlphaChannel =
                (evidence & TextureEvidenceKinds.AlphaChannel) != 0 &&
                UnityAlphaFieldEvidence.TryCapture(
                    texture,
                    TextureChannel.Alpha,
                    cutoffThreshold,
                    bounds,
                    out _,
                    out alphaChannel,
                    out alphaRefusal,
                    originMaterial: originMaterial,
                    allowDiskFallback: allowDiskFallback,
                    rawMipCap: rawMipCap);
            AlphaMipChain redChannel = null;
            var redRefusal = TextureCaptureRefusalReason.None;
            var hasRedChannel =
                (evidence & TextureEvidenceKinds.RedChannel) != 0 &&
                UnityAlphaFieldEvidence.TryCapture(
                    texture,
                    TextureChannel.Red,
                    1.0f,
                    AlphaPolicyBounds.Inert,
                    out _,
                    out redChannel,
                    out redRefusal,
                    originMaterial: originMaterial,
                    allowDiskFallback: allowDiskFallback,
                    rawMipCap: rawMipCap);
            // The alpha arm's predicate rides beside the chain: the routes
            // binarize a declared cutoff into the stored verdicts, so the
            // field's key must carry the threshold the bytes mean. Clamped
            // exactly as the routes clamp it.
            var captureThreshold = Mathf.Clamp01(cutoffThreshold);
            return new CapturedTextureEvidence(
                hasSource,
                source,
                captureThreshold,
                bounds,
                hasSampling,
                sampling,
                hasColorInterpretation,
                colorInterpretation,
                sampledAlphaIsOne,
                canonicalNormal,
                hasBoundedColorRange,
                hasAlphaChannel,
                alphaChannel,
                alphaRefusal,
                hasRedChannel,
                redChannel,
                redRefusal);
        }

        /// <summary>
        /// Asserts the Analysis field contract on a red chain when its route
        /// produces and caches it, so each unique chain is checked once:
        /// byte 255 marks exactly the texels whose value is exactly 1
        /// and every other byte marks a value strictly below 1, so an inert
        /// red chain carries only 255 and 0. The inert bounds never erase,
        /// so the erased flag byte proves a capture seam defect. A violation
        /// throws instead of caching or serving evidence the proof core
        /// would misread.
        /// </summary>
        internal static void AssertRedFieldContract(AlphaMipChain chain)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }
            for (var mip = 0; mip < chain.Count; mip++)
            {
                if (chain.IsLevelWithoutEvidence(mip))
                {
                    continue;
                }
                var level = chain[mip];
                for (var y = 0; y < level.Height; y++)
                {
                    for (var x = 0; x < level.Width; x++)
                    {
                        var verdict = level.GetAlpha(x, y);
                        if (verdict == byte.MaxValue || verdict == 0)
                        {
                            continue;
                        }
                        throw new InvalidOperationException(
                            "Red evidence breaks the exact-255 field contract: " +
                            "level " + mip + " texel (" + x + ", " + y + ") stores " +
                            verdict + ", which is neither exactly one nor below one.");
                    }
                }
            }
        }

        /// <summary>
        /// The named refusals of one texture assignment: one record per
        /// requested channel whose capture refused, naming the property, the
        /// identity the capture knows, the channel, and the reason family. An
        /// unassigned slot captured nothing and refuses nothing, and a
        /// captured field never carries a refusal.
        /// </summary>
        private static IReadOnlyList<TextureCaptureRefusal> BuildCaptureRefusals(
            string propertyName,
            Texture texture,
            CapturedTextureEvidence captured)
        {
            if (texture == null || captured == null)
            {
                return Array.Empty<TextureCaptureRefusal>();
            }

            var textureName = texture.name;
            var formatName = texture is Texture2D texture2D
                ? texture2D.format.ToString()
                : null;

            var refusals = new List<TextureCaptureRefusal>(2);
            if (captured.AlphaCaptureRefusal !=
                TextureCaptureRefusalReason.None)
            {
                refusals.Add(new TextureCaptureRefusal(
                    propertyName,
                    captured.HasSourceIdentity,
                    captured.SourceIdentity,
                    TextureChannel.Alpha,
                    captured.AlphaCaptureRefusal,
                    formatName: formatName,
                    textureName: textureName));
            }

            if (captured.RedCaptureRefusal !=
                TextureCaptureRefusalReason.None)
            {
                refusals.Add(new TextureCaptureRefusal(
                    propertyName,
                    captured.HasSourceIdentity,
                    captured.SourceIdentity,
                    TextureChannel.Red,
                    captured.RedCaptureRefusal,
                    formatName: formatName,
                    textureName: textureName));
            }

            return refusals.Count == 0
                ? Array.Empty<TextureCaptureRefusal>()
                : new ReadOnlyCollection<TextureCaptureRefusal>(refusals);
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
            internal Material SourceMaterial;
            internal bool AllowDiskFallback = true;
            internal int RawMipCap = int.MaxValue;
            internal bool HasShaderName;
            internal string ShaderName;
            internal bool HasActiveColorSpace;
            internal ColorSpace ActiveColorSpace;
            internal string[] Keywords = Array.Empty<string>();
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
            internal float CutoutThreshold = 1.0f;
            internal SharedTextureBuilder Shared;
        }
        private sealed class SharedTextureBuilder
        {
            internal readonly Texture Texture;
            internal readonly TextureSourceId Source;
            internal readonly float CutoutThreshold;
            internal readonly Material OriginMaterial;
            internal TextureEvidenceKinds Evidence;
            // The strictest contributing request governs the shared
            // capture: one disallowing request in the bucket withdraws
            // the route for it, and the smallest cap in the bucket caps
            // the readback for it.
            internal bool AllowDiskFallback = true;
            internal int RawMipCap = int.MaxValue;
            internal CapturedTextureEvidence Captured;

            internal SharedTextureBuilder(
                Texture texture,
                TextureSourceId source,
                float cutoutThreshold,
                Material originMaterial)
            {
                Texture = texture;
                Source = source;
                CutoutThreshold = cutoutThreshold;
                OriginMaterial = originMaterial;
            }
        }
    }
}
