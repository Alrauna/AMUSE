namespace Alrauna.Amuse.Research.Census
{
    /// <summary>
    /// The census vocabulary: local mirrors of the categories AMUSE analysis
    /// produces, plus the two the census derives for itself.
    /// <para>
    /// Every mirror here is a <em>snapshot</em> of AMUSE's vocabulary taken when
    /// it was written, not a live view of it. This assembly deliberately holds
    /// no reference to AMUSE, so it cannot track a production enum, and it must
    /// not appear to: a census that silently absorbed a new AMUSE value would
    /// report it under an existing category or drop it, and either outcome is a
    /// miscount in a tool whose whole value is trustworthy counting.
    /// </para>
    /// <para>
    /// A new AMUSE value is therefore required to surface as a loud failure
    /// demanding an explicit schema decision. Half of that lives here, in
    /// <c>CensusCategorySnapshotTests</c>, which pins every member set so no
    /// census-side edit is silent. The other half — parity against AMUSE's own
    /// enums, and an exhaustive mapping with no guessing default arm — lives in
    /// the collector's <c>CensusVocabularyTests</c>, where the friend grant
    /// makes those enums visible at compile time.
    /// </para>
    /// <para>
    /// That second half was incomplete for <see cref="RendererRefusal"/> until
    /// it was repaired: parity was asserted, but nothing drove every AMUSE
    /// refusal through the mapping, so three production refusals were added
    /// without a mirror and only the parity assertion noticed.
    /// <c>EveryAmuseRefusalMapsToTheSameCensusName</c> now closes that.
    /// </para>
    /// </summary>
    public enum RendererRefusal
    {
        None,
        UnsupportedRendererType,
        MaterialPropertyOverridesPresent,
        UnrecognizedAnimatedMaterialBinding,
        MissingMesh,
        UnprovenMaterialSlotMapping,
        UnsupportedTopology,
        MalformedMeshData,
        AnimatedMeshReplacement,
        AnimatedMaterialSlotCount,
        AdmittedStateBudgetExceeded,
        AnimatedPropertyAbsentFromAdmittedMaterial,
    }

    /// <summary>
    /// Mirrors AMUSE's <c>AlphaResolutionFailure</c>. Snapshot, not live; see
    /// <see cref="RendererRefusal"/>.
    /// </summary>
    public enum AlphaResolutionFailure
    {
        None,
        SemanticsUnknown,
        UnsupportedMultiplier,
        UnsupportedUvMapping,
        UnsupportedSampling,
        MissingTextureEvidence,
    }

    /// <summary>
    /// Mirrors AMUSE's <c>SubmeshSeparationDisposition</c>. Snapshot, not live;
    /// see <see cref="RendererRefusal"/>.
    /// </summary>
    public enum SeparationDisposition
    {
        Unchanged,
        WhollyOpaqueCandidate,
        Split,
    }

    /// <summary>
    /// Which shader frontend attested a material, or none. This is a census
    /// concept rather than an AMUSE type: AMUSE's frontends each report whether
    /// they support a material, and the collector records which one answered.
    /// Poiyomi and lilToon are public products and may be named; every other
    /// family is <see cref="None"/> here and is grouped anonymously downstream.
    /// </summary>
    public enum ShaderFamilyAttestation
    {
        None,
        Poiyomi,
        LilToon,
    }

    /// <summary>
    /// The renderer kinds the census distinguishes. A closed enum rather than a
    /// type name, so a third-party renderer type name can never reach an
    /// anonymized record. <see cref="Other"/> is first, and therefore the
    /// default, because it is the conservative answer; AMUSE refuses everything
    /// that is not a mesh or skinned mesh renderer as
    /// <see cref="RendererRefusal.UnsupportedRendererType"/> anyway.
    /// </summary>
    public enum RendererKind
    {
        Other,
        MeshRenderer,
        SkinnedMeshRenderer,
    }
}
