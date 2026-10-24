using System.Collections.Generic;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>The program state of one original property (A4.1).</summary>
    internal enum LockedPropertyState
    {
        Live,
        Renamed,
        Baked,
        Unused
    }

    /// <summary>
    /// The aligned facts of one lock (A4). The record is plain data and
    /// never touches a Unity object. One internal constructor takes every
    /// field, so later stages build instances by hand. Blocks stays empty
    /// until block hypotheses land (A3.7).
    /// </summary>
    internal sealed class AlignedLockedSource
    {
        internal AlignedLockedSource(
            PoiyomiLockEra era,
            string suffix,
            IReadOnlyDictionary<string, LockedPropertyState> states,
            IReadOnlyDictionary<string, string> renamedNames,
            IReadOnlyDictionary<string, string> bakedLiterals,
            IReadOnlyDictionary<string, int> enumValues,
            IReadOnlyDictionary<string, string> bracketReads,
            IReadOnlyList<IReadOnlyList<string>> defineRuns,
            IReadOnlyList<LockedBlockRecord> blocks,
            string survivingText)
        {
            Era = era;
            Suffix = suffix;
            States = states;
            RenamedNames = renamedNames;
            BakedLiterals = bakedLiterals;
            EnumValues = enumValues;
            BracketReads = bracketReads;
            DefineRuns = defineRuns;
            Blocks = blocks;
            SurvivingText = survivingText;
        }

        internal PoiyomiLockEra Era { get; }

        /// <summary>The rename suffix. Null when no rename happened (A4.2).</summary>
        internal string Suffix { get; }

        internal IReadOnlyDictionary<string, LockedPropertyState> States { get; }

        /// <summary>Original name to renamed name for renamed entries (A4.1).</summary>
        internal IReadOnlyDictionary<string, string> RenamedNames { get; }

        /// <summary>Original name to one literal text for baked entries (A4.1).</summary>
        internal IReadOnlyDictionary<string, string> BakedLiterals { get; }

        /// <summary>Property to the parsed enum value of a Cull or ColorMask read (A4.3).</summary>
        internal IReadOnlyDictionary<string, int> EnumValues { get; }

        /// <summary>Property to the name that a live ShaderLab bracket reads (A4.3).</summary>
        internal IReadOnlyDictionary<string, string> BracketReads { get; }

        /// <summary>One define name list per program block (A4.3, fact 8).</summary>
        internal IReadOnlyList<IReadOnlyList<string>> DefineRuns { get; }

        internal IReadOnlyList<LockedBlockRecord> Blocks { get; }

        /// <summary>The surviving original text for occurrence tests (A4.3).</summary>
        internal string SurvivingText { get; }
    }
}
