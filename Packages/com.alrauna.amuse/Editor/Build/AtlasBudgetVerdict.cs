namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The budget result of one group: the refusal, and for a refusal
    /// the value and the limit of the number that tripped. Design
    /// section 10 asks the report to state both. An admitted group
    /// holds <see cref="AtlasUseRefusal.None"/> and zero bytes.
    /// </summary>
    internal readonly struct AtlasBudgetVerdict
    {
        internal AtlasBudgetVerdict(
            AtlasUseRefusal refusal,
            long valueBytes,
            long limitBytes)
        {
            Refusal = refusal;
            ValueBytes = valueBytes;
            LimitBytes = limitBytes;
        }

        internal static AtlasBudgetVerdict Admitted =>
            new AtlasBudgetVerdict(AtlasUseRefusal.None, 0, 0);

        internal AtlasUseRefusal Refusal { get; }

        /// <summary>The bytes the tripped number compared.</summary>
        internal long ValueBytes { get; }

        /// <summary>
        /// The limit of the tripped number in bytes. The group limit
        /// is the floor of the source bytes times the factor. The
        /// value is an integer, so the floor states the limit
        /// exactly.
        /// </summary>
        internal long LimitBytes { get; }
    }
}
