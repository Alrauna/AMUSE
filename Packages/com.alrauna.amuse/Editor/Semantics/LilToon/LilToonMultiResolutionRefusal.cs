namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// Why one material stayed outside the Multi follower. The vocabulary is
    /// closed across the whole Multi resolution path: every refusal value of
    /// the path lives here, and each value names the layer that produces it.
    /// <para>
    /// The mode-consistency gate produces only four of the seven values, and
    /// it checks them in this order: <see cref="ModeOutsideAdmittedSet"/>,
    /// then <see cref="ClippingCancellerEnabled"/>, then
    /// <see cref="OverlayPassEnableUnsupported"/>, then
    /// <see cref="KeywordModeMismatch"/>. The first refusal wins, so the
    /// gate is deterministic. The gate never produces the other three
    /// values. They stay in this enum so every Multi refusal caller shares
    /// one closed vocabulary.
    /// </para>
    /// </summary>
    internal enum LilToonMultiResolutionRefusal
    {
        /// <summary>
        /// Producing layer: the mode-consistency gate. The requested Multi
        /// mode is outside the vendor admitted set, which holds the modes
        /// zero to two: opaque, cutout, and transparent.
        /// </summary>
        ModeOutsideAdmittedSet,

        /// <summary>
        /// Producing layer: the container-identity resolver. The exact
        /// shader name and asset GUID identify a specialized Multi container
        /// the follower does not admit. The gate never returns this value.
        /// </summary>
        SpecializedContainer,

        /// <summary>
        /// Producing layer: the mode-consistency gate. The captured keyword
        /// set is not exactly a state the pinned vendor derivation produces
        /// from the captured scalars and the mode.
        /// </summary>
        KeywordModeMismatch,

        /// <summary>
        /// Producing layer: the mode-consistency gate. The captured
        /// <c>_UseClippingCanceller</c> is not zero. The feature has a
        /// render-time consumer (<c>LIL_MULTI_SHOULD_CLIPPING</c>), and the
        /// regular families carry no matching gate to inherit.
        /// </summary>
        ClippingCancellerEnabled,

        /// <summary>
        /// Producing layer: the mode-consistency gate. The captured
        /// <c>_AsOverlay</c> is not zero. The overlay form disables the
        /// vendor depth passes on the material, a form the regular families
        /// do not carry.
        /// </summary>
        OverlayPassEnableUnsupported,

        /// <summary>
        /// Producing layer: the source attestation layer. The material's
        /// shader source failed its digest or format attestation. The gate
        /// never returns this value.
        /// </summary>
        AttestationFailed,

        /// <summary>
        /// Producing layer: the capture request layer. The request did not
        /// ask for keywords, so no keyword set exists for the gate to
        /// compare. The evidence alone cannot tell this state apart from an
        /// empty captured set, so the caller owns the refusal. The gate
        /// never returns this value.
        /// </summary>
        MissingKeywordEvidence,
    }
}
