using System;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// One use-scope atlas refusal: the renderer, the slot, the cause,
    /// the slot material, the member texture, and a detail sentence
    /// that names the exact value that refused. A budget refusal also
    /// holds the value and the limit of its number in bytes. Every
    /// other cause holds zero bytes.
    /// <para>
    /// TRANSIENT HOST DATA: the record keeps live references, the same
    /// way <see cref="Host.AtlasUseOutcome"/> does, so the report can
    /// make the renderer, the material, and the texture clickable. No
    /// consumer stores it past the build or the inspector view.
    /// </para>
    /// </summary>
    internal sealed class AtlasUseRefusalRecord
    {
        internal AtlasUseRefusalRecord(
            Renderer renderer,
            int slot,
            AtlasUseRefusal cause,
            Material material,
            Texture texture,
            string detail,
            long valueBytes = 0,
            long limitBytes = 0)
        {
            if (cause == AtlasUseRefusal.None)
            {
                throw new ArgumentException(
                    "AtlasUseRefusal.None is not a refusal.",
                    nameof(cause));
            }

            Renderer = renderer;
            Slot = slot;
            Cause = cause;
            Material = material;
            Texture = texture;
            Detail = detail ?? string.Empty;
            ValueBytes = valueBytes;
            LimitBytes = limitBytes;
        }

        internal Renderer Renderer { get; }

        internal int Slot { get; }

        internal AtlasUseRefusal Cause { get; }

        /// <summary>The slot material, or null when the slot has none.</summary>
        internal Material Material { get; }

        /// <summary>
        /// The member texture when the capture read it, or null.
        /// </summary>
        internal Texture Texture { get; }

        /// <summary>
        /// One or more sentences that name the exact bad value. It is
        /// empty when the cause has no detail.
        /// </summary>
        internal string Detail { get; }

        internal long ValueBytes { get; }

        internal long LimitBytes { get; }
    }
}
