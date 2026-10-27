namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>The block forms the aligner records (A3.7).</summary>
    internal enum LockedBlockKind
    {
        Ifex,
        Ifdef,
        Ifndef
    }

    /// <summary>The four outcomes one block can take (A3.7).</summary>
    internal enum LockedBlockOutcome
    {
        Kept,
        Removed,
        Unobservable,
        NotEvaluated
    }

    /// <summary>
    /// One block record with its kind, its outcome, its condition or
    /// keyword text, the zero-and-zero marker of Y3, and the 1-based
    /// original line of the block start. The aligner fills the list
    /// during the block walk (A3.7).
    /// </summary>
    internal readonly struct LockedBlockRecord
    {
        internal LockedBlockRecord(
            LockedBlockKind kind,
            LockedBlockOutcome outcome,
            string conditionOrKeyword,
            bool zeroAndZero,
            int originalLine)
        {
            Kind = kind;
            Outcome = outcome;
            ConditionOrKeyword = conditionOrKeyword;
            ZeroAndZero = zeroAndZero;
            OriginalLine = originalLine;
        }

        internal LockedBlockKind Kind { get; }

        internal LockedBlockOutcome Outcome { get; }

        internal string ConditionOrKeyword { get; }

        /// <summary>
        /// Marks a zero-and-zero record of Y3 in the bundled locked
        /// original admission design. Only the wrapper record of a
        /// both-zero tie carries the marker. The strict check of the
        /// condition evaluator selects on it.
        /// </summary>
        internal bool ZeroAndZero { get; }

        internal int OriginalLine { get; }
    }
}
