using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Semantics;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal sealed class CapturedTextureEvidence
    {
        internal bool HasSourceIdentity { get; }
        internal TextureSourceId SourceIdentity { get; }

        /// <summary>
        /// The alpha arm's capture predicate: the declared shader cutoff
        /// this field was binarized by, clamped the way the capture routes
        /// clamp it, or one when the source was captured exact. The value
        /// rides beside the chain because the chain's bytes carry the
        /// predicate's meaning - 255 marks "satisfied this capture's
        /// opaque test" - and a field consumed under a different predicate
        /// would read that test's verdict as an exact-one fact.
        /// </summary>
        internal float CaptureThreshold { get; }

        /// <summary>
        /// The alpha policy bounds this capture ran under. With a declared
        /// cutoff below one the capture kept the policy inert for the
        /// source, which the field key derives from the threshold. The
        /// alpha arm keys these bounds, and the red arm keys the inert
        /// bounds because the capture pins red to them.
        /// </summary>
        internal AlphaPolicyBounds CaptureBounds { get; }

        internal bool HasSampling { get; }
        internal TextureSampling Sampling { get; }
        internal bool HasColorInterpretation { get; }
        internal TextureColorInterpretation ColorInterpretation { get; }
        internal bool SampledAlphaIsProvenOne { get; }
        internal bool IsCanonicalNormalMap { get; }

        /// <summary>
        /// True only when every effective sampled colour value of the source
        /// is finite and confined to [0,1]. The fact is a captured,
        /// request-scoped snapshot: the capture runs the import predicate at
        /// capture time, because the captured record carries no texture
        /// reference to ask later.
        /// </summary>
        internal bool ColorValuesProvenInUnitRange { get; }
        internal bool HasAlphaChannel { get; }
        internal AlphaMipChain AlphaChannel { get; }

        /// <summary>
        /// True only when every captured alpha mip carries usable evidence
        /// and every texel in every mip is exactly one. This is the
        /// geometry independent proof used by a time varying layer sample.
        /// </summary>
        internal bool AlphaChannelIsProvenFullyOpaque { get; }
        internal bool HasRedChannel { get; }
        internal AlphaMipChain RedChannel { get; }

        /// <summary>
        /// Why the requested alpha-field capture refused, or
        /// <see cref="TextureCaptureRefusalReason.None"/> when it captured or
        /// was not requested. The refusal is a fact about this texture's
        /// capture, recorded beside the absence it explains: a reader of
        /// <c>HasAlphaChannel == false</c> can name what stopped.
        /// </summary>
        internal TextureCaptureRefusalReason AlphaCaptureRefusal { get; }

        /// <summary>
        /// The red-channel counterpart of <see cref="AlphaCaptureRefusal"/>.
        /// The two channels fail independently - the per-channel host
        /// capability gate can refuse one and pass the other - so each
        /// carries its own reason.
        /// </summary>
        internal TextureCaptureRefusalReason RedCaptureRefusal { get; }

        internal CapturedTextureEvidence(
            bool hasSourceIdentity,
            TextureSourceId sourceIdentity,
            float captureThreshold,
            AlphaPolicyBounds captureBounds,
            bool hasSampling,
            TextureSampling sampling,
            bool hasColorInterpretation,
            TextureColorInterpretation colorInterpretation,
            bool sampledAlphaIsProvenOne,
            bool isCanonicalNormalMap,
            bool colorValuesProvenInUnitRange,
            bool hasAlphaChannel,
            AlphaMipChain alphaChannel,
            TextureCaptureRefusalReason alphaCaptureRefusal,
            bool hasRedChannel,
            AlphaMipChain redChannel,
            TextureCaptureRefusalReason redCaptureRefusal)
        {
            HasSourceIdentity = hasSourceIdentity;
            SourceIdentity = sourceIdentity;
            CaptureThreshold = captureThreshold;
            CaptureBounds = captureBounds;
            HasSampling = hasSampling;
            Sampling = sampling;
            HasColorInterpretation = hasColorInterpretation;
            ColorInterpretation = colorInterpretation;
            SampledAlphaIsProvenOne = sampledAlphaIsProvenOne;
            IsCanonicalNormalMap = isCanonicalNormalMap;
            ColorValuesProvenInUnitRange = colorValuesProvenInUnitRange;
            HasAlphaChannel = hasAlphaChannel;
            AlphaChannel = alphaChannel;
            AlphaChannelIsProvenFullyOpaque =
                IsProvenFullyOpaque(
                    captureThreshold, captureBounds,
                    hasAlphaChannel, alphaChannel);
            AlphaCaptureRefusal = alphaCaptureRefusal;
            HasRedChannel = hasRedChannel;
            RedChannel = redChannel;
            RedCaptureRefusal = redCaptureRefusal;
        }

        private static bool IsProvenFullyOpaque(
            float captureThreshold,
            AlphaPolicyBounds bounds,
            bool hasChannel,
            AlphaMipChain chain)
        {
            // A policy opaque verdict can include sub-one texels. Only the
            // inert exact-255 opaque bound gives an exact-one domain fact.
            if (captureThreshold != 1f ||
                bounds.OpaqueBound != byte.MaxValue ||
                !hasChannel ||
                chain == null)
            {
                return false;
            }

            for (var mip = 0; mip < chain.Count; mip++)
            {
                if (chain.IsLevelWithoutEvidence(mip) ||
                    !chain[mip].IsFullyOpaque)
                {
                    return false;
                }
            }

            return true;
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

        /// <summary>
        /// This assignment's named capture refusals: one per requested
        /// channel that refused, each naming this property, the texture's
        /// identity when the capture knows one, the channel, and the reason
        /// family. Empty when nothing was assigned or every requested field
        /// captured. The records are slot-scoped facts: they refuse nothing
        /// and exist so the slot resolution and the report can name the
        /// capture failure.
        /// </summary>
        internal IReadOnlyList<TextureCaptureRefusal> CaptureRefusals { get; }

        internal CapturedTextureAssignment(
            bool isAssigned,
            TextureEvidenceKinds requestedEvidence,
            bool hasScaleOffset,
            Vector2 scale,
            Vector2 offset,
            CapturedTextureEvidence texture,
            IReadOnlyList<TextureCaptureRefusal> captureRefusals)
        {
            if (captureRefusals == null)
            {
                throw new ArgumentNullException(nameof(captureRefusals));
            }

            IsAssigned = isAssigned;
            RequestedEvidence = requestedEvidence;
            HasScaleOffset = hasScaleOffset;
            Scale = scale;
            Offset = offset;
            Texture = texture;
            CaptureRefusals = captureRefusals;
        }
    }

    internal sealed class CapturedMaterialEvidence
    {
        private readonly PresenceEntry[] _presence;
        private readonly ScalarEntry[] _scalars;
        private readonly ColorEntry[] _colors;
        private readonly VectorEntry[] _vectors;
        private readonly TextureEntry[] _textureAssignments;
        private readonly string[] _keywords;

        internal bool HasShaderName { get; }
        internal string ShaderName { get; }
        internal bool HasActiveColorSpace { get; }
        internal ColorSpace ActiveColorSpace { get; }

        /// <summary>
        /// The material's shader keyword set in ordinal sort order, when the
        /// request asked for keywords; otherwise empty, never null. The array
        /// is adopted from the capture already sorted and deduplicated and is
        /// never mutated after construction, so derivations share it unchanged.
        /// </summary>
        internal IReadOnlyList<string> Keywords => _keywords;

        internal IReadOnlyCollection<CapturedTextureEvidence> Textures { get; }

        /// <summary>
        /// The named texture assignments this evidence was captured under,
        /// in request order. The unnamed <see cref="Textures"/> aggregate
        /// serves consumers that reason about texture contents; the named
        /// assignments serve consumers that must know which requested
        /// property produced a field - the predicate-scoped field lookup.
        /// The array is adopted, never mutated after construction.
        /// </summary>
        internal IReadOnlyList<TextureEntry> TextureAssignments
            => _textureAssignments;

        /// <summary>
        /// Every named capture refusal across this evidence's texture
        /// assignments, in assignment order, aggregated once from the
        /// assignments themselves. Derivations cannot drop or invent a
        /// refusal: the assignment array is shared unchanged and the
        /// aggregate is rebuilt from it on every construction.
        /// </summary>
        internal IReadOnlyList<TextureCaptureRefusal> CaptureRefusals { get; }

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
            IReadOnlyCollection<CapturedTextureEvidence> textures,
            string[] keywords = null)
        {
            HasShaderName = hasShaderName;
            ShaderName = shaderName;
            HasActiveColorSpace = hasActiveColorSpace;
            ActiveColorSpace = activeColorSpace;
            // The entry arrays are adopted, not copied, and are never written
            // in place after this point. Every capture fills them before
            // construction, and each With* derivation clones the one category
            // it replaces, so derived evidence can share the rest safely. An
            // in-place write added later would silently corrupt every object
            // sharing the array. The keyword array is adopted under the same
            // rule: the capture hands it over already ordinal-sorted and
            // deduplicated, and derivations share it unchanged.
            _presence = presence;
            _scalars = scalars;
            _colors = colors;
            _vectors = vectors;
            _textureAssignments = textureAssignments;
            _keywords = keywords ?? Array.Empty<string>();
            Textures = textures;
            CaptureRefusals = AggregateCaptureRefusals(textureAssignments);
        }

        /// <summary>
        /// One pass over the adopted assignment array. Unassigned slots and
        /// assignments without refusals contribute nothing.
        /// </summary>
        private static IReadOnlyList<TextureCaptureRefusal>
            AggregateCaptureRefusals(
                TextureEntry[] textureAssignments)
        {
            var refusals = new List<TextureCaptureRefusal>();
            foreach (var entry in textureAssignments)
            {
                if (!entry.HasValue)
                {
                    continue;
                }

                refusals.AddRange(entry.Value.CaptureRefusals);
            }

            return refusals;
        }

        internal bool HasProperty(string name)
        {
            ValidatePropertyName(name);
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
            ValidatePropertyName(name);
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
            ValidatePropertyName(name);
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
            ValidatePropertyName(name);
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
            ValidatePropertyName(name);
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

        /// <summary>
        /// Derives new evidence with one requested scalar's captured value
        /// replaced. The source is never mutated: only the scalar array is
        /// copied, and every other category is shared under the array-adoption
        /// rule recorded on the constructor.
        /// <para>
        /// This is a <em>primitive</em>, not an authorization path. It writes
        /// whatever value it is given and applies no admission policy: no
        /// finiteness test, no agreement test, and no comparison against the
        /// admitted material's serialized default. Those decisions belong to
        /// admission, upstream of every call.
        /// </para>
        /// <para>
        /// Presence is a captured fact, so <c>HasValue</c> is preserved exactly.
        /// A requested property the material does not have stays absent, and
        /// the supplied value is discarded rather than stored beside the absent
        /// flag: capture already guarantees that an entry without a value holds
        /// <c>default</c>, and a derivation must not become the one place where
        /// an unauthorized value hides behind <c>HasValue == false</c>. That
        /// preserved absence is <em>not</em> permission to ignore an animated
        /// binding for it: per the design's fail-closed branch, admitted-state
        /// resolution must return
        /// <c>RendererAnalysisRefusal.AnimatedPropertyAbsentFromAdmittedMaterial</c>
        /// for a proof-relevant animated property absent from the admitted
        /// material, before any substitution here is treated as authorized.
        /// </para>
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The name was not requested as a scalar. Substituting an unrequested
        /// property would invent an evidence dimension, which is a programming
        /// defect rather than a domain outcome; a name requested only under
        /// another typed category is equally unrequested here.
        /// </exception>
        internal CapturedMaterialEvidence WithScalar(string name, float value)
        {
            ValidatePropertyName(name);
            for (var index = 0; index < _scalars.Length; index++)
            {
                var entry = _scalars[index];
                if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var scalars = (ScalarEntry[])_scalars.Clone();
                scalars[index] = new ScalarEntry(
                    entry.Name,
                    entry.HasValue,
                    entry.HasValue ? value : entry.Value);
                return Derive(scalars, _colors, _vectors);
            }

            throw Unrequested(name);
        }

        /// <summary>
        /// The colour counterpart of <see cref="WithScalar"/>; the same
        /// primitive, presence, and authorization rules apply verbatim.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The name was not requested as a colour.
        /// </exception>
        internal CapturedMaterialEvidence WithColor(string name, Color value)
        {
            ValidatePropertyName(name);
            for (var index = 0; index < _colors.Length; index++)
            {
                var entry = _colors[index];
                if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var colors = (ColorEntry[])_colors.Clone();
                colors[index] = new ColorEntry(
                    entry.Name,
                    entry.HasValue,
                    entry.HasValue ? value : entry.Value);
                return Derive(_scalars, colors, _vectors);
            }

            throw Unrequested(name);
        }

        /// <summary>
        /// The vector counterpart of <see cref="WithScalar"/>; the same
        /// primitive, presence, and authorization rules apply verbatim.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// The name was not requested as a vector.
        /// </exception>
        internal CapturedMaterialEvidence WithVector(string name, Vector4 value)
        {
            ValidatePropertyName(name);
            for (var index = 0; index < _vectors.Length; index++)
            {
                var entry = _vectors[index];
                if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var vectors = (VectorEntry[])_vectors.Clone();
                vectors[index] = new VectorEntry(
                    entry.Name,
                    entry.HasValue,
                    entry.HasValue ? value : entry.Value);
                return Derive(_scalars, _colors, vectors);
            }

            throw Unrequested(name);
        }

        /// <summary>
        /// Rebuilds the whole evidence object around three typed categories, so
        /// no derivation can silently drop the shader name, colour space,
        /// keywords, presence, texture assignments, or shared texture evidence.
        /// </summary>
        private CapturedMaterialEvidence Derive(
            ScalarEntry[] scalars,
            ColorEntry[] colors,
            VectorEntry[] vectors)
        {
            return new CapturedMaterialEvidence(
                HasShaderName,
                ShaderName,
                HasActiveColorSpace,
                ActiveColorSpace,
                _presence,
                scalars,
                colors,
                vectors,
                _textureAssignments,
                Textures,
                _keywords);
        }

        private static void ValidatePropertyName(string name)
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
}
