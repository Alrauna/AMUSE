namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// Why the avatar-level preparation preserved the input. The refusal is a
    /// closed set: every producer at the preparation boundary maps to exactly
    /// one member, and only a member of this enum may refuse a decision.
    /// </summary>
    internal enum PreparationRefusal
    {
        /// <summary>No refusal. <c>None</c> is never a refusal outcome: the
        /// factory maps it to the prepared, no-mutation decision.</summary>
        None,

        /// <summary>The live build target and acceptance settings no longer
        /// re-select the sampling policy the barrier captured, or the live
        /// target has no proof policy at all, so every prepared candidate is
        /// stale. An ordinary refusal, not a defect: the user or a foreign
        /// pass may legitimately have changed either between the capture and
        /// the apply pass.</summary>
        StaleSamplingPolicy,

        /// <summary>An admitted material's opaque conversion refused, so the
        /// slot keeps its original material. The display detail names the
        /// underlying conversion failure.</summary>
        OpaqueConversionRefused,
    }

    /// <summary>
    /// The result of preparing an AMUSE mutation. Only an explicit refusal is an
    /// ordinary conservative outcome; an unexpected defect during preparation is
    /// an exception, not a decision.
    /// </summary>
    internal readonly struct AmusePreparationDecision
    {
        private AmusePreparationDecision(
            bool isPrepared,
            bool hasMutation,
            PreparationRefusal refusal,
            string refusalDetail)
        {
            IsPrepared = isPrepared;
            HasMutation = hasMutation;
            Refusal = refusal;
            RefusalDetail = refusalDetail;
        }

        internal bool IsPrepared { get; }
        internal bool HasMutation { get; }
        internal PreparationRefusal Refusal { get; }

        /// <summary>
        /// The display text that explains the refusal at a report edge, or
        /// null when the refusal needs no detail beyond its member name.
        /// </summary>
        internal string RefusalDetail { get; }

        internal static AmusePreparationDecision Refused(
            PreparationRefusal reason,
            string detail = null)
        {
            if (reason == PreparationRefusal.None)
            {
                // None is not a refusal, so the factory answers the prepared,
                // no-mutation decision instead of an unexplained refusal.
                return NoMutation();
            }

            return new AmusePreparationDecision(false, false, reason, detail);
        }

        internal static AmusePreparationDecision NoMutation()
        {
            return new AmusePreparationDecision(
                true, false, PreparationRefusal.None, null);
        }

        internal static AmusePreparationDecision Ready()
        {
            return new AmusePreparationDecision(
                true, true, PreparationRefusal.None, null);
        }
    }
}
