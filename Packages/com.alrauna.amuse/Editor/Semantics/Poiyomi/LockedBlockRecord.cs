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
    /// keyword text, and the 1-based original line of the block start.
    /// Task 2 fills no records. The aligner returns an empty list until
    /// block hypotheses land (A3.7).
    /// </summary>
    internal readonly struct LockedBlockRecord
    {
        internal LockedBlockRecord(
            LockedBlockKind kind,
            LockedBlockOutcome outcome,
            string conditionOrKeyword,
            int originalLine)
        {
            Kind = kind;
            Outcome = outcome;
            ConditionOrKeyword = conditionOrKeyword;
            OriginalLine = originalLine;
        }

        internal LockedBlockKind Kind { get; }

        internal LockedBlockOutcome Outcome { get; }

        internal string ConditionOrKeyword { get; }

        internal int OriginalLine { get; }
    }
}
