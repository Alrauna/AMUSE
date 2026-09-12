namespace Alrauna.Amuse.Editor.Host
{
    /// <summary>
    /// Why one texture field's capture produced no chain. Closed by the
    /// capture-soundness spec: a refused capture names its reason family, so a
    /// report can say what stopped instead of leaving a silent
    /// <c>HasAlphaChannel == false</c>.
    /// <para>
    /// These are capture facts, not transformation conditions. A refused field
    /// invalidates only the conclusions that consult it - the material and the
    /// slot whose proof samples it - never the renderer and never another slot
    /// or material. This vocabulary is therefore deliberately separate from
    /// <c>AlphaSeparationSlotRefusal</c>, whose buckets count dropped
    /// transformation slots, and from <c>RendererAnalysisRefusal</c>.
    /// </para>
    /// <para>
    /// The per-level mip degradation records its detail on the captured chain
    /// itself, as per-level provenance beside <see cref="NonResidentMips"/>; it
    /// does not rewrite this enum, and NonResidentMips stays the named refusal
    /// whenever a capture has no usable level at all.
    /// </para>
    /// </summary>
    internal enum TextureCaptureRefusalReason
    {
        /// <summary>The field captured a chain, or no capture was requested
        /// for it. Never a refusal.</summary>
        None,

        /// <summary>The capture route could not run for this texture at all:
        /// no resolvable source identity, a non-Texture2D object, an unadmitted
        /// build target, missing host capability, an unusable predicate
        /// shader, a failed level acquisition, or a destroyed object. Nothing
        /// usable exists, so there is nothing to degrade.</summary>
        UnavailableCapture,

        /// <summary>The effective mipmap limit left no resident level in the
        /// declared chain, so the capture has nothing usable to consult.
        /// When at least one level is resident there is no refusal: the
        /// chain carries the per-level provenance instead, and the levels
        /// the limit removed degrade to Unknown at the fold.</summary>
        NonResidentMips,

        /// <summary>The texture's storage format is outside the closed
        /// characterization allowlist (<see
        /// cref="UnityAlphaFieldEvidence.IsAdmittedFormat"/>), so no capture
        /// route may decode it.</summary>
        UnsupportedFormat,
    }
}
