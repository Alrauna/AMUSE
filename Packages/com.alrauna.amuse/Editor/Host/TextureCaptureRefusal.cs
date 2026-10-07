using System;
using Alrauna.Amuse.Editor.Semantics;

namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// One texture field's named capture refusal: which shader property and
    /// source identity it belongs to, which channel refused, and why no chain
    /// exists. It is a record about evidence, not a transformation condition:
    /// nothing reads it to refuse a renderer, to drop a slot conversion, or to
    /// invalidate another material, and the slot resolution carries it so a
    /// report can name the reason.
    /// <para>
    /// The identity is best effort. A texture whose identity gate refused has
    /// no <see cref="TextureSourceId"/>, and the shader property name is then
    /// the only handle a reader has.
    /// </para>
    /// <para>
    /// The record may carry the texture's display name and storage format
    /// for the report. It still holds no live Unity object.
    /// </para>
    /// </summary>
    internal readonly struct TextureCaptureRefusal
    {
        internal string PropertyName { get; }
        internal bool HasSourceIdentity { get; }
        internal TextureSourceId SourceIdentity { get; }
        internal TextureChannel Channel { get; }
        internal TextureCaptureRefusalReason Reason { get; }
        internal string FormatName { get; }
        internal string TextureName { get; }

        internal TextureCaptureRefusal(
            string propertyName,
            bool hasSourceIdentity,
            TextureSourceId sourceIdentity,
            TextureChannel channel,
            TextureCaptureRefusalReason reason,
            string formatName = null,
            string textureName = null)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException(
                    "A capture refusal must name its texture property.",
                    nameof(propertyName));
            }
            if (reason == TextureCaptureRefusalReason.None)
            {
                throw new ArgumentException(
                    "TextureCaptureRefusalReason.None is not a refusal.",
                    nameof(reason));
            }

            PropertyName = propertyName;
            HasSourceIdentity = hasSourceIdentity;
            SourceIdentity = hasSourceIdentity
                ? sourceIdentity
                : default;
            Channel = channel;
            Reason = reason;
            FormatName = formatName;
            TextureName = textureName;
        }
    }
}
