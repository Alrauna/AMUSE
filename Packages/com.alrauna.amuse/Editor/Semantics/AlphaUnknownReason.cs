namespace Alrauna.Amuse.Editor.Semantics
{
    /// <summary>
    /// Why a material's alpha has no proven answer. The report names this
    /// reason so the author can act on it; classification never reads it.
    /// Closed on purpose: every value has its own report sentence, and a
    /// new value without a sentence fails the report completeness tests.
    /// </summary>
    internal enum AlphaUnknownKind
    {
        /// <summary>
        /// The material's shader names no family AMUSE knows, so no frontend
        /// answers for it at all.
        /// </summary>
        UnsupportedShader,

        /// <summary>
        /// The material names a known family, but its source identity does
        /// not attest: a wrong version, a digest mismatch, or a locked
        /// serialization whose original shader cannot be resolved.
        /// </summary>
        UnattestedShader,

        /// <summary>
        /// An attested material uses a shader feature the family frontend
        /// does not model. <see cref="AlphaUnknownReason.Feature"/> names it
        /// in words and <see cref="AlphaUnknownReason.Property"/> names the
        /// exact shader property that carries it.
        /// </summary>
        UnsupportedFeature,

        /// <summary>
        /// A lilToon Multi material refused at the Multi resolution point.
        /// <see cref="AlphaUnknownReason.Feature"/> names the refusal value
        /// in words, for example "clipping canceller enabled".
        /// </summary>
        UnsupportedMultiState,
    }

    /// <summary>
    /// One immutable answer to "why is this alpha unknown". Built by the
    /// frontends when they answer Unknown, and carried unchanged to the
    /// slot-refusal report. A reason is evidence about a refusal, never an
    /// input to a proof.
    /// </summary>
    internal sealed record AlphaUnknownReason
    {
        internal AlphaUnknownKind Kind { get; }

        /// <summary>The feature in plain words, for example "Dissolve".
        /// For UnsupportedMultiState it names the refusal value in words.
        /// Null when the kind is not UnsupportedFeature or
        /// UnsupportedMultiState.</summary>
        internal string Feature { get; }

        /// <summary>The exact shader property that carries the feature, for
        /// example "_DissolveParams". Null when the kind is not
        /// UnsupportedFeature.</summary>
        internal string Property { get; }

        /// <summary>The live shader name captured at capture time. Set for
        /// the two shader kinds, null for UnsupportedFeature and
        /// UnsupportedMultiState.</summary>
        internal string ShaderName { get; }

        private AlphaUnknownReason(
            AlphaUnknownKind kind,
            string feature,
            string property,
            string shaderName)
        {
            Kind = kind;
            Feature = feature;
            Property = property;
            ShaderName = shaderName;
        }

        internal static AlphaUnknownReason UnsupportedShader(
            string shaderName)
        {
            return new AlphaUnknownReason(
                AlphaUnknownKind.UnsupportedShader, null, null, shaderName);
        }

        internal static AlphaUnknownReason UnattestedShader(
            string shaderName)
        {
            return new AlphaUnknownReason(
                AlphaUnknownKind.UnattestedShader, null, null, shaderName);
        }

        internal static AlphaUnknownReason UnsupportedFeature(
            string feature, string property)
        {
            return new AlphaUnknownReason(
                AlphaUnknownKind.UnsupportedFeature, feature, property, null);
        }

        internal static AlphaUnknownReason UnsupportedMultiState(
            string feature)
        {
            return new AlphaUnknownReason(
                AlphaUnknownKind.UnsupportedMultiState, feature, null, null);
        }
    }
}
