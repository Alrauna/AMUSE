using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The outcome of one alignment attempt. A refusal carries one fixed
    /// detail sentence and the 1-based line of the original file (A3.1).
    /// </summary>
    internal readonly struct LockedSourceAlignmentResult
    {
        internal LockedSourceAlignmentResult(
            bool succeeded,
            AlignedLockedSource source,
            string refusalDetail,
            int originalLine)
        {
            Succeeded = succeeded;
            Source = source;
            RefusalDetail = refusalDetail;
            OriginalLine = originalLine;
        }

        internal bool Succeeded { get; }

        internal AlignedLockedSource Source { get; }

        internal string RefusalDetail { get; }

        internal int OriginalLine { get; }
    }

    /// <summary>
    /// Emulates the closed list of vendor lock edits on the attested
    /// original and matches the result against the generated text (A3).
    /// The type is pure. It takes strings and plain records and never
    /// touches a Unity object. Blocks stay empty until block hypotheses
    /// land (A3.7). The walk runs forward over one line range and can be
    /// re-entered for a sub range, which is the shape block hypotheses
    /// need.
    /// </summary>
    internal static class PoiyomiLockedSourceAlignment
    {
        // One fixed detail sentence per rule (A3.9). No sentence names a
        // property value or a path.
        private const string LineMismatchDetail =
            "A generated line does not match the expected line.";

        private const string UnconsumedLineDetail =
            "The generated text holds a line that no original edit produces.";

        private const string TwoLiteralsDetail =
            "One property shows two different baked literals.";

        private const string MixedUsesDetail =
            "One property mixes live and baked uses.";

        private const string SuffixAmbiguousDetail =
            "The rename suffix of the generated names is ambiguous.";

        private const string InlinedMarkerDetail =
            "The generated text holds an inlined marker.";

        private const string IncludeDetail =
            "The original text includes a file outside the Unity include set.";

        private const string OutsideSetDetail =
            "The original text holds a construct outside the accepted set.";

        private const string MissingPassDetail =
            "The original text holds no pass.";

        private const string EnumWithoutBakeDetail =
            "A render state enum text names a property that is not baked.";

        private const string EraDisagreementDetail =
            "Aligning eras disagree on the aligned facts.";

        private const string BlockAlignmentDetail =
            "Neither outcome of a block matches the generated text.";

        private const string BlockTieDetail =
            "A block outcome stays undecided.";

        private const string BlockUnclosedDetail =
            "A block in the original text never closes.";

        private const string OptimizerMissingDetail =
            "A program block misses the optimizer define.";

        // The base lookahead window after a block in surviving original
        // lines (A3.7). The window grows by itself on a tie.
        private const int LookaheadWindow = 8;

        // The closed set of Unity include files the aligner accepts
        // (A3.8). Any other include refuses.
        private static readonly string[] UnityIncludes =
        {
            "UnityCG.cginc",
            "HLSLSupport.cginc",
            "UnityShaderVariables.cginc",
            "UnityInstancing.cginc",
            "UnityGBuffer.cginc",
            "UnityGlobalIllumination.cginc",
            "UnityStandardConfig.cginc",
            "UnityStandardUtils.cginc",
            "UnityStandardCore.cginc",
            "AutoLight.cginc",
            "Lighting.cginc",
            "Core.cginc",
            "Core.hlsl",
            "Common.hlsl"
        };

        private static readonly string[] CullEnumTexts = { "Off", "Front", "Back" };

        private static readonly (string Text, int Value)[] ColorMaskLetters =
        {
            ("R", 1),
            ("G", 2),
            ("B", 4),
            ("A", 8)
        };

        /// <summary>
        /// Lists the property names whose Properties line carries the
        /// Gamma attribute before the name. Unity 2022.3 has no Gamma
        /// member in ShaderPropertyFlags, so the original text is the
        /// only place the flag exists at align time (A3.1).
        /// </summary>
        internal static IReadOnlyCollection<string> GammaPropertyNames(string originalText)
        {
            if (originalText == null)
            {
                throw new ArgumentNullException(nameof(originalText));
            }

            var names = new List<string>();
            var raw = originalText.Split(new[] { '\r', '\n' });
            var inBlock = false;
            var depth = 0;
            foreach (var line in raw)
            {
                if (!inBlock)
                {
                    if (FirstToken(line.Trim()) == "Properties")
                    {
                        inBlock = true;
                        depth = CountChar(line, '{') - CountChar(line, '}');
                    }

                    continue;
                }

                var depthBefore = depth;
                depth += CountChar(line, '{') - CountChar(line, '}');
                var gammaIndex = line.IndexOf("[Gamma]", StringComparison.Ordinal);
                if (depthBefore > 0 && gammaIndex >= 0 && TryPropertiesName(line, out var name, out var nameStart))
                {
                    // The attribute must sit before the name, so a display
                    // string that only mentions the token does not count.
                    if (gammaIndex < nameStart && !names.Contains(name))
                    {
                        names.Add(name);
                    }
                }

                if (depth <= 0 && depthBefore > 0)
                {
                    inBlock = false;
                }
            }

            return names;
        }

        /// <summary>
        /// Runs every era of A2.2. The result succeeds only when at least
        /// one era aligns and every aligning era gives the same facts.
        /// When no era aligns, the refusal of the first canonical era
        /// comes back.
        /// </summary>
        internal static LockedSourceAlignmentResult Align(
            string originalText,
            string generatedText,
            IReadOnlyList<LockedSourceProperty> properties)
        {
            var eras = new[]
            {
                PoiyomiLockEra.Bundled9364,
                PoiyomiLockEra.Thry274To2742,
                PoiyomiLockEra.Thry2743
            };
            LockedSourceAlignmentResult? firstFailure = null;
            AlignedLockedSource first = null;
            foreach (var era in eras)
            {
                var result = AlignForEra(originalText, generatedText, properties, era);
                if (!result.Succeeded)
                {
                    if (firstFailure == null)
                    {
                        firstFailure = result;
                    }

                    continue;
                }

                if (first == null)
                {
                    first = result.Source;
                    continue;
                }

                if (!FactsEqual(first, result.Source))
                {
                    return new LockedSourceAlignmentResult(false, null, EraDisagreementDetail, 0);
                }
            }

            if (first == null)
            {
                return firstFailure.Value;
            }

            return new LockedSourceAlignmentResult(true, first, null, 0);
        }

        /// <summary>
        /// Aligns one pair under one vendor era (A2.2). A refusal exits
        /// through one fixed detail sentence and the 1-based original
        /// line that caused it.
        /// </summary>
        internal static LockedSourceAlignmentResult AlignForEra(
            string originalText,
            string generatedText,
            IReadOnlyList<LockedSourceProperty> properties,
            PoiyomiLockEra era)
        {
            if (originalText == null)
            {
                throw new ArgumentNullException(nameof(originalText));
            }

            if (generatedText == null)
            {
                throw new ArgumentNullException(nameof(generatedText));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            try
            {
                var source = AlignEraOrRefuse(originalText, generatedText, properties, era);
                return new LockedSourceAlignmentResult(true, source, null, 0);
            }
            catch (RefusalException refusal)
            {
                return new LockedSourceAlignmentResult(false, null, refusal.Detail, refusal.OriginalLine);
            }
        }

        private static AlignedLockedSource AlignEraOrRefuse(
            string originalText,
            string generatedText,
            IReadOnlyList<LockedSourceProperty> properties,
            PoiyomiLockEra era)
        {
            var state = new WalkState(originalText, SplitLines(originalText), SplitLines(generatedText), properties, era);
            RunPreChecks(state);
            state.Suffix = FindSuffix(state);
            WalkLines(state);
            FinishWalk(state);
            return BuildSource(state);
        }

        private static List<LineRef> SplitLines(string text)
        {
            var lines = new List<LineRef>();
            var raw = text.Split(new[] { '\r', '\n' });
            for (var i = 0; i < raw.Length; i++)
            {
                // A3.2 drops empty entries and whitespace-only lines on
                // both sides. Trailing whitespace inside a line counts.
                if (string.IsNullOrWhiteSpace(raw[i]))
                {
                    continue;
                }

                lines.Add(new LineRef(raw[i], i + 1));
            }

            return lines;
        }

        private static void RunPreChecks(WalkState state)
        {
            // Constructs outside the closed list refuse (A3.8).
            var sawPass = false;
            for (var i = 0; i < state.OriginalLines.Count; i++)
            {
                var line = state.OriginalLines[i];
                var trimmed = line.Text.Trim();
                if (FirstToken(trimmed) == "Pass")
                {
                    sawPass = true;
                }

                if (trimmed.StartsWith("#include", StringComparison.Ordinal) && !IsUnityInclude(trimmed))
                {
                    throw new RefusalException(IncludeDetail, line.Number);
                }

                if (trimmed.Contains("GrabPass")
                    || trimmed.StartsWith("#pragma geometry", StringComparison.Ordinal)
                    || trimmed.StartsWith("#pragma hull", StringComparison.Ordinal)
                    || trimmed.StartsWith("#pragma domain", StringComparison.Ordinal)
                    || trimmed.Contains("[maxtessfactor("))
                {
                    throw new RefusalException(OutsideSetDetail, line.Number);
                }
            }

            for (var i = 0; i < state.GeneratedLines.Count; i++)
            {
                if (state.GeneratedLines[i].Text.Contains("[Inlined]"))
                {
                    throw new RefusalException(InlinedMarkerDetail, 0);
                }
            }

            if (!sawPass)
            {
                throw new RefusalException(MissingPassDetail, 0);
            }
        }

        private static bool IsUnityInclude(string trimmedLine)
        {
            var open = trimmedLine.IndexOf('"');
            if (open < 0)
            {
                return false;
            }

            var close = trimmedLine.IndexOf('"', open + 1);
            if (close < 0)
            {
                return false;
            }

            var name = trimmedLine.Substring(open + 1, close - open - 1);
            foreach (var allowed in UnityIncludes)
            {
                if (string.Equals(name, allowed, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // --- A4.2: the suffix search ---------------------------------

        private static string FindSuffix(WalkState state)
        {
            var artifacts = CollectArtifacts(state);
            if (artifacts.Count == 0)
            {
                return null;
            }

            var intersection = SuffixCandidates(state, artifacts[0]);
            for (var i = 1; i < artifacts.Count && intersection.Count > 0; i++)
            {
                intersection.IntersectWith(SuffixCandidates(state, artifacts[i]));
            }

            if (intersection.Count != 1)
            {
                throw new RefusalException(SuffixAmbiguousDetail, 0);
            }

            string only = null;
            foreach (var value in intersection)
            {
                only = value;
            }

            return only;
        }

        private static List<string> CollectArtifacts(WalkState state)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            void Add(string candidate)
            {
                if (candidate.Length == 0
                    || state.Declared.ContainsKey(candidate)
                    || state.DerivedOf.ContainsKey(candidate))
                {
                    return;
                }

                // A name the original already holds as a whole token is no
                // rename artifact. A substring check would wrongly drop a
                // suffix form that only rides inside a longer name.
                if (ContainsToken(state.OriginalText, candidate))
                {
                    return;
                }

                if (seen.Add(candidate))
                {
                    found.Add(candidate);
                }
            }

            var propertiesLines = new HashSet<int>(PropertiesLineIndices(state.GeneratedLines));
            var inProgram = false;
            for (var i = 0; i < state.GeneratedLines.Count; i++)
            {
                var text = state.GeneratedLines[i].Text;
                var trimmed = text.Trim();
                if (trimmed == "CGPROGRAM" || trimmed == "CGINCLUDE")
                {
                    inProgram = true;
                    continue;
                }

                if (trimmed == "ENDCG")
                {
                    inProgram = false;
                    continue;
                }

                if (inProgram)
                {
                    // The vendor inserts define lines, so they hold no
                    // rename artifacts (fact 8).
                    if (trimmed.StartsWith("#define ", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var tokens = new List<string>();
                    CollectIdentifiers(text, tokens);
                    foreach (var token in tokens)
                    {
                        Add(token);
                    }

                    continue;
                }

                if (propertiesLines.Contains(i))
                {
                    if (TryPropertiesName(text, out var name, out _))
                    {
                        Add(name);
                    }

                    continue;
                }

                foreach (var name in BracketNamesOf(text))
                {
                    Add(name);
                }
            }

            return found;
        }

        private static HashSet<string> SuffixCandidates(WalkState state, string artifact)
        {
            // A4.2 splits every artifact at a declared original name. The
            // longest declared name that occurs in the artifact decides
            // the split point, so one artifact yields one candidate set.
            var longest = 0;
            foreach (var name in state.Declared.Keys)
            {
                if (name.Length > longest && artifact.IndexOf(name, StringComparison.Ordinal) >= 0)
                {
                    longest = name.Length;
                }
            }

            var set = new HashSet<string>(StringComparer.Ordinal);
            if (longest == 0)
            {
                return set;
            }

            foreach (var name in state.Declared.Keys)
            {
                if (name.Length != longest)
                {
                    continue;
                }

                var from = 0;
                while ((from = artifact.IndexOf(name, from, StringComparison.Ordinal)) >= 0)
                {
                    AddTailStripped(set, artifact.Substring(from + name.Length));
                    from += name.Length;
                }
            }

            return set;
        }

        private static void AddTailStripped(HashSet<string> set, string rest)
        {
            // A4.2 splits a declared original name, the underscore
            // separator, and a remainder. A tail without the separator is
            // no form the vendor rename can produce.
            if (rest.Length == 0 || rest[0] != '_')
            {
                return;
            }

            set.Add(rest);
            // Fact 7 renames P_ST and P_TexelSize behind the suffix, so a
            // known tail may ride behind one candidate.
            if (rest.EndsWith("_ST", StringComparison.Ordinal) && rest.Length > 3)
            {
                var stripped = rest.Substring(0, rest.Length - 3);
                if (stripped[0] == '_')
                {
                    set.Add(stripped);
                }
            }

            if (rest.EndsWith("_TexelSize", StringComparison.Ordinal) && rest.Length > 10)
            {
                var stripped = rest.Substring(0, rest.Length - 10);
                if (stripped[0] == '_')
                {
                    set.Add(stripped);
                }
            }
        }

        // --- A3.3: the forward walk ----------------------------------

        private static void WalkLines(WalkState state)
        {
            WalkRange(state, 0, state.OriginalLines.Count);
            state.FlushDefineRun();
        }

        /// <summary>
        /// Counts one surviving line against the active lookahead window
        /// and reports when the window runs out (A3.7). The counter is
        /// negative when no window is active, so the walk runs unbounded.
        /// </summary>
        private static bool LookaheadWindowRanOut(WalkState state)
        {
            if (state.LookaheadRemaining < 0)
            {
                return false;
            }

            state.LookaheadRemaining--;
            return state.LookaheadRemaining == 0;
        }

        /// <summary>
        /// Walks one original line range forward over the generated lines
        /// and returns the consumed generated position. Block starts hand
        /// over to the two hypothesis resolution (A3.7). When a lookahead
        /// window is active in the walk state, the walk stops after the
        /// window runs out of surviving lines.
        /// </summary>
        private static int WalkRange(WalkState state, int origStart, int origEnd)
        {
            var origIndex = origStart;
            while (origIndex < origEnd)
            {
                var line = state.OriginalLines[origIndex];
                var trimmed = line.Text.Trim();
                if (trimmed.StartsWith("/*", StringComparison.Ordinal))
                {
                    // A line that starts with the block comment opener
                    // drops the block up to the line that ends with the
                    // closer (fact 2).
                    origIndex++;
                    while (origIndex < origEnd
                        && !state.OriginalLines[origIndex].Text.TrimEnd().EndsWith("*/", StringComparison.Ordinal))
                    {
                        origIndex++;
                    }

                    origIndex++;
                    state.OriginalIndex = origIndex;
                    continue;
                }

                if (TryBlockStart(trimmed, out var blockKind, out var blockText))
                {
                    ResolveBlock(state, origIndex, blockKind, blockText);
                    origIndex = state.OriginalIndex;
                    continue;
                }

                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    // A comment line leaves nothing (fact 2).
                    origIndex++;
                    state.OriginalIndex = origIndex;
                    continue;
                }

                if (trimmed.StartsWith("#pragma shader_feature", StringComparison.Ordinal))
                {
                    // Every shader feature pragma line is deleted (fact 12).
                    origIndex++;
                    state.OriginalIndex = origIndex;
                    continue;
                }

                if (trimmed == "CGPROGRAM" || trimmed == "CGINCLUDE")
                {
                    ExpectGeneratedLine(state, line);
                    state.InProgram = true;
                    state.SurvivingLines.Add(line.Text);
                    state.LastConsumedOriginalNumber = line.Number;
                    state.GenIndex++;
                    state.OriginalIndex = origIndex + 1;
                    origIndex++;
                    if (trimmed == "CGPROGRAM")
                    {
                        state.FlushDefineRun();
                        state.OpenDefineRun = new List<string>();
                        ConsumeDefineRun(state);
                    }

                    if (LookaheadWindowRanOut(state))
                    {
                        return state.GenIndex;
                    }

                    continue;
                }

                if (trimmed == "ENDCG")
                {
                    ExpectGeneratedLine(state, line);
                    state.InProgram = false;
                    state.FlushDefineRun();
                    state.SurvivingLines.Add(line.Text);
                    state.LastConsumedOriginalNumber = line.Number;
                    state.GenIndex++;
                    state.OriginalIndex = origIndex + 1;
                    origIndex++;
                    if (LookaheadWindowRanOut(state))
                    {
                        return state.GenIndex;
                    }

                    continue;
                }

                WalkOrdinaryLine(state, origIndex);
                origIndex = state.OriginalIndex;
                if (LookaheadWindowRanOut(state))
                {
                    return state.GenIndex;
                }
            }

            state.OriginalIndex = origIndex;
            return state.GenIndex;
        }

        /// <summary>
        /// Walks one line that is no block marker and no dropped line. The
        /// program, Properties, and ShaderLab rules decide the expected
        /// generated text. A program line that turns whitespace only after
        /// the branch marker deletion drops and counts as no surviving
        /// line (A3.3).
        /// </summary>
        private static void WalkOrdinaryLine(WalkState state, int origIndex)
        {
            var line = state.OriginalLines[origIndex];
            if (state.InProgram)
            {
                // The branch markers are substrings that the vendor
                // deletes on program lines (fact 13).
                var edited = line.Text
                    .Replace("UNITY_BRANCH", string.Empty)
                    .Replace("[branch]", string.Empty);
                if (edited.Trim().Length == 0)
                {
                    state.OriginalIndex = origIndex + 1;
                    return;
                }

                MatchProgramLine(state, edited, line.Number);
            }
            else if (state.PropertiesLineSet.Contains(origIndex))
            {
                MatchPropertiesLine(state, line);
            }
            else
            {
                MatchShaderLabLine(state, line);
            }

            state.SurvivingLines.Add(line.Text);
            state.LastConsumedOriginalNumber = line.Number;
            state.GenIndex++;
            state.OriginalIndex = origIndex + 1;
        }

        /// <summary>
        /// Detects a block start line. An ifex marker is the comment form
        /// of fact 2. An ifdef or ifndef directive without an ampersand or
        /// a pipe is a block of fact 11. A directive with a join operator
        /// is no block and walks as a plain line (A3.7).
        /// </summary>
        private static bool TryBlockStart(string trimmed, out LockedBlockKind kind, out string conditionOrKeyword)
        {
            kind = LockedBlockKind.Ifex;
            conditionOrKeyword = null;
            if (IsIfexStart(trimmed))
            {
                kind = LockedBlockKind.Ifex;
                conditionOrKeyword = trimmed.Substring(6).TrimStart();
                return true;
            }

            var first = FirstToken(trimmed);
            if ((first == "#ifdef" || first == "#ifndef")
                && trimmed.IndexOf('&') < 0
                && trimmed.IndexOf('|') < 0)
            {
                kind = first == "#ifdef" ? LockedBlockKind.Ifdef : LockedBlockKind.Ifndef;
                conditionOrKeyword = trimmed.Substring(first.Length).Trim();
                return true;
            }

            return false;
        }

        private static bool IsIfexStart(string trimmed)
        {
            return trimmed.Length >= 6
                && trimmed.StartsWith("//ifex", StringComparison.Ordinal)
                && (trimmed.Length == 6 || trimmed[6] == ' ');
        }

        private static bool IsEndex(string trimmed)
        {
            return trimmed.Length >= 7
                && trimmed.StartsWith("//endex", StringComparison.Ordinal)
                && (trimmed.Length == 7 || trimmed[7] == ' ');
        }

        /// <summary>The line spans of one block in original line indices.</summary>
        private readonly struct BlockExtent
        {
            internal BlockExtent(int bodyStart, int bodyEnd, int elseIndex, int endIndex)
            {
                BodyStart = bodyStart;
                BodyEnd = bodyEnd;
                ElseIndex = elseIndex;
                EndIndex = endIndex;
            }

            internal int BodyStart { get; }

            /// <summary>Exclusive end of the body before an else line.</summary>
            internal int BodyEnd { get; }

            /// <summary>Index of the else line. Negative when none exists.</summary>
            internal int ElseIndex { get; }

            /// <summary>Index of the endif line of an ifdef block.</summary>
            internal int EndIndex { get; }
        }

        /// <summary>
        /// Finds the extent of one block. Nested directives deepen the
        /// count. An elif line is never special, so it stays part of the
        /// body before an else (fact 11).
        /// </summary>
        private static BlockExtent FindBlockExtent(WalkState state, int startIndex, LockedBlockKind kind)
        {
            if (kind == LockedBlockKind.Ifex)
            {
                var depth = 1;
                for (var i = startIndex + 1; i < state.OriginalLines.Count; i++)
                {
                    var trimmed = state.OriginalLines[i].Text.Trim();
                    if (IsIfexStart(trimmed))
                    {
                        depth++;
                        continue;
                    }

                    if (IsEndex(trimmed))
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return new BlockExtent(startIndex + 1, i, -1, i);
                        }
                    }
                }

                throw new RefusalException(BlockUnclosedDetail, state.OriginalLines[startIndex].Number, true);
            }

            var ifDepth = 1;
            var elseIndex = -1;
            for (var i = startIndex + 1; i < state.OriginalLines.Count; i++)
            {
                var first = FirstToken(state.OriginalLines[i].Text.Trim());
                if (first == "#ifdef" || first == "#ifndef" || first == "#if")
                {
                    ifDepth++;
                    continue;
                }

                if (first == "#endif")
                {
                    ifDepth--;
                    if (ifDepth == 0)
                    {
                        return new BlockExtent(startIndex + 1, elseIndex < 0 ? i : elseIndex, elseIndex, i);
                    }

                    continue;
                }

                if (first == "#else" && ifDepth == 1 && elseIndex < 0)
                {
                    elseIndex = i;
                }
            }

            throw new RefusalException(BlockUnclosedDetail, state.OriginalLines[startIndex].Number, true);
        }

        /// <summary>
        /// One candidate of the block resolution with its walk position
        /// inside the lookahead growth (A3.7).
        /// </summary>
        private sealed class BlockCandidate
        {
            internal BlockCandidate(WalkState state)
            {
                State = state;
            }

            internal WalkState State { get; }

            internal bool Dead { get; set; }

            internal bool AtEnd { get; set; }
        }

        /// <summary>
        /// Decides one block by two hypotheses (A3.7). The kept hypothesis
        /// walks the directive line, the body, and the endif line. The
        /// removed hypothesis skips the block and walks only the else body
        /// of an ifdef block. Each trial works on a clone, so the losing
        /// outcome commits nothing. When both align, a lookahead window of
        /// surviving lines decides. The window grows on a tie. A tie at
        /// the end of the file refuses.
        /// </summary>
        private static void ResolveBlock(WalkState state, int startIndex, LockedBlockKind kind, string conditionOrKeyword)
        {
            var startLine = state.OriginalLines[startIndex];
            var extent = FindBlockExtent(state, startIndex, kind);
            var enclosingLookahead = state.LookaheadRemaining;
            var inheritedBoundary = state.GrowthBoundary;

            // A trial must walk its whole body. The enclosing window count
            // returns after the adoption. The trials bound every nested
            // lookahead at this block, because the survival of the lines
            // past the block is the enclosing decision, not this one.
            state.LookaheadRemaining = -1;
            WalkState keptResult = null;
            try
            {
                var kept = state.Clone();
                kept.GrowthBoundary = extent.EndIndex;
                if (kind != LockedBlockKind.Ifex)
                {
                    WalkOrdinaryLine(kept, startIndex);
                }

                if (extent.BodyEnd > extent.BodyStart)
                {
                    WalkRange(kept, extent.BodyStart, extent.BodyEnd);
                }

                if (kind != LockedBlockKind.Ifex)
                {
                    if (extent.ElseIndex >= 0)
                    {
                        WalkOrdinaryLine(kept, extent.ElseIndex);
                        if (extent.EndIndex > extent.ElseIndex + 1)
                        {
                            WalkRange(kept, extent.ElseIndex + 1, extent.EndIndex);
                        }
                    }

                    WalkOrdinaryLine(kept, extent.EndIndex);
                }

                keptResult = kept;
                if (keptResult.OriginalIndex < extent.EndIndex + 1)
                {
                    keptResult.OriginalIndex = extent.EndIndex + 1;
                }
            }
            catch (RefusalException refusal)
            {
                // An undecided nested block kills no hypothesis. The
                // alignment refuses as a whole (A3.7).
                if (refusal.Undecided)
                {
                    throw;
                }
            }

            WalkState removedResult = null;
            try
            {
                var removed = state.Clone();
                removed.GrowthBoundary = extent.EndIndex;
                RecordSkippedBlocks(removed, extent.BodyStart, extent.ElseIndex < 0 ? extent.EndIndex : extent.ElseIndex);
                if (kind != LockedBlockKind.Ifex
                    && extent.ElseIndex >= 0
                    && extent.EndIndex > extent.ElseIndex + 1)
                {
                    WalkRange(removed, extent.ElseIndex + 1, extent.EndIndex);
                }

                removedResult = removed;
                removedResult.OriginalIndex = extent.EndIndex + 1;
            }
            catch (RefusalException refusal)
            {
                if (refusal.Undecided)
                {
                    throw;
                }
            }

            var candidates = new List<BlockCandidate>();
            if (keptResult != null)
            {
                candidates.Add(new BlockCandidate(keptResult));
            }

            if (removedResult != null)
            {
                candidates.Add(new BlockCandidate(removedResult));
            }

            if (candidates.Count == 0)
            {
                state.LookaheadRemaining = enclosingLookahead;
                throw new RefusalException(BlockAlignmentDetail, startLine.Number);
            }

            // Both outcomes consume nothing when the body holds no
            // surviving line. The block is then unobservable (A3.7).
            var bodySurviving = BodyHasSurvivingLine(state, extent.BodyStart, extent.BodyEnd);
            var winner = candidates[0].State;
            var decidedByLookahead = false;
            if (candidates.Count == 2 && bodySurviving)
            {
                decidedByLookahead = true;
                while (true)
                {
                    foreach (var candidate in candidates)
                    {
                        if (candidate.Dead || candidate.AtEnd)
                        {
                            continue;
                        }

                        // The lookahead judges the lines after this block.
                        // Their survival follows from the enclosing block,
                        // so the walk stops at the inherited boundary.
                        candidate.State.GrowthBoundary = inheritedBoundary;
                        candidate.State.LookaheadRemaining = LookaheadWindow;
                        try
                        {
                            var growthEnd = candidate.State.GrowthBoundary < 0
                                ? candidate.State.OriginalLines.Count
                                : candidate.State.GrowthBoundary;
                            WalkRange(candidate.State, candidate.State.OriginalIndex, growthEnd);
                        }
                        catch (RefusalException refusal)
                        {
                            if (refusal.Undecided)
                            {
                                state.LookaheadRemaining = enclosingLookahead;
                                throw;
                            }

                            candidate.Dead = true;
                            continue;
                        }

                        if (candidate.State.LookaheadRemaining > 0)
                        {
                            // The walk ended before the window ran out, so
                            // the candidate reached the end of the walk
                            // range.
                            candidate.AtEnd = true;
                        }
                    }

                    candidates.RemoveAll(candidate => candidate.Dead);
                    if (candidates.Count == 0)
                    {
                        state.LookaheadRemaining = enclosingLookahead;
                        throw new RefusalException(BlockAlignmentDetail, startLine.Number);
                    }

                    if (candidates.Count == 1)
                    {
                        winner = candidates[0].State;
                        break;
                    }

                    var allAtEnd = true;
                    foreach (var candidate in candidates)
                    {
                        if (!candidate.AtEnd)
                        {
                            allAtEnd = false;
                            break;
                        }
                    }

                    if (allAtEnd)
                    {
                        state.LookaheadRemaining = enclosingLookahead;
                        throw new RefusalException(BlockTieDetail, startLine.Number, true);
                    }
                }
            }

            LockedBlockOutcome outcome;
            if (candidates.Count == 2 && !bodySurviving)
            {
                outcome = LockedBlockOutcome.Unobservable;
            }
            else if (!decidedByLookahead)
            {
                outcome = keptResult != null ? LockedBlockOutcome.Kept : LockedBlockOutcome.Removed;
            }
            else
            {
                outcome = ReferenceEquals(winner, keptResult) ? LockedBlockOutcome.Kept : LockedBlockOutcome.Removed;
            }

            // The record holds the outcome with its condition or keyword
            // text and the 1 based original line of the block start. It
            // lands at the position of the block start, so the nested
            // records of the adopted winner follow it (A3.7, A4.4).
            var recordStart = state.Blocks.Count;
            winner.Blocks.Insert(recordStart, new LockedBlockRecord(kind, outcome, conditionOrKeyword, startLine.Number));
            state.AdoptFrom(winner);
            state.LookaheadRemaining = enclosingLookahead;
            state.GrowthBoundary = inheritedBoundary;
            if (state.OriginalIndex < extent.EndIndex + 1)
            {
                state.OriginalIndex = extent.EndIndex + 1;
            }
        }

        /// <summary>
        /// Records every nested block start inside a skipped removed body.
        /// A block inside a removed block is never evaluated (fact 9), so
        /// the record only marks it NotEvaluated (A3.7).
        /// </summary>
        private static void RecordSkippedBlocks(WalkState state, int bodyStart, int bodyEnd)
        {
            var inBlockComment = false;
            for (var i = bodyStart; i < bodyEnd; i++)
            {
                var trimmed = state.OriginalLines[i].Text.Trim();
                if (inBlockComment)
                {
                    if (trimmed.EndsWith("*/", StringComparison.Ordinal))
                    {
                        inBlockComment = false;
                    }

                    continue;
                }

                if (trimmed.StartsWith("/*", StringComparison.Ordinal))
                {
                    if (!trimmed.EndsWith("*/", StringComparison.Ordinal))
                    {
                        inBlockComment = true;
                    }

                    continue;
                }

                if (TryBlockStart(trimmed, out var kind, out var text))
                {
                    state.Blocks.Add(new LockedBlockRecord(
                        kind, LockedBlockOutcome.NotEvaluated, text, state.OriginalLines[i].Number));
                }
            }
        }

        /// <summary>
        /// Reports whether the block body holds at least one line that
        /// survives the deletion rules (A3.7). The rules mirror the drop
        /// paths of the walk.
        /// </summary>
        private static bool BodyHasSurvivingLine(WalkState state, int bodyStart, int bodyEnd)
        {
            var inBlockComment = false;
            for (var i = bodyStart; i < bodyEnd; i++)
            {
                var text = state.OriginalLines[i].Text;
                var trimmed = text.Trim();
                if (inBlockComment)
                {
                    if (trimmed.EndsWith("*/", StringComparison.Ordinal))
                    {
                        inBlockComment = false;
                    }

                    continue;
                }

                if (trimmed.StartsWith("/*", StringComparison.Ordinal))
                {
                    if (!trimmed.EndsWith("*/", StringComparison.Ordinal))
                    {
                        inBlockComment = true;
                    }

                    continue;
                }

                if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (trimmed.StartsWith("#pragma shader_feature", StringComparison.Ordinal))
                {
                    continue;
                }

                if (state.InProgram)
                {
                    var edited = text
                        .Replace("UNITY_BRANCH", string.Empty)
                        .Replace("[branch]", string.Empty);
                    if (edited.Trim().Length == 0)
                    {
                        continue;
                    }
                }

                return true;
            }

            return false;
        }

        private static void ExpectGeneratedLine(WalkState state, LineRef line)
        {
            if (state.GenIndex >= state.GeneratedLines.Count
                || state.GeneratedLines[state.GenIndex].Text != line.Text)
            {
                throw new RefusalException(LineMismatchDetail, line.Number);
            }
        }

        private static void ConsumeDefineRun(WalkState state)
        {
            while (state.GenIndex < state.GeneratedLines.Count
                && TryParseDefineLine(state.GeneratedLines[state.GenIndex].Text, out var name))
            {
                state.OpenDefineRun.Add(name);
                state.GenIndex++;
            }
        }

        private static bool TryParseDefineLine(string text, out string name)
        {
            name = null;
            const string prefix = " #define ";
            if (!text.StartsWith(prefix, StringComparison.Ordinal) || !text.EndsWith(" ", StringComparison.Ordinal))
            {
                return false;
            }

            var body = text.Substring(prefix.Length, text.Length - prefix.Length - 1);
            if (body.Length == 0 || !IsIdentifier(body))
            {
                return false;
            }

            name = body;
            return true;
        }

        private static void MatchPropertiesLine(WalkState state, LineRef line)
        {
            if (state.GenIndex >= state.GeneratedLines.Count)
            {
                throw new RefusalException(LineMismatchDetail, line.Number);
            }

            var edited = ApplyLockButtonDefault(line.Text);
            var gen = state.GeneratedLines[state.GenIndex].Text;
            var hasName = TryPropertiesName(line.Text, out var name, out var nameStart);
            if (hasName && state.Declared.ContainsKey(name))
            {
                var decided = state.RenamedDecided.TryGetValue(name, out var value) ? (bool?)value : null;
                if (decided == false)
                {
                    if (gen != edited)
                    {
                        throw new RefusalException(LineMismatchDetail, line.Number);
                    }

                    return;
                }

                if (decided == true)
                {
                    if (gen == RenameLineWithNames(state, edited, null))
                    {
                        return;
                    }

                    // The duplicate set keeps the plain line and inserts
                    // the renamed copy after it (fact 7, A3.6).
                    if (state.Suffix != null
                        && gen == edited
                        && KeepsPlainAndRenamedCopy(line.Text, nameStart, name.Length)
                        && RenamedCopyFollows(state, edited, name))
                    {
                        state.GenIndex++;
                        return;
                    }

                    throw new RefusalException(LineMismatchDetail, line.Number);
                }

                if (gen == edited)
                {
                    // The duplicate set keeps the plain line and inserts
                    // the renamed copy right after it. Only a one line
                    // definition with a one word type gets the copy.
                    // Range types and multi line definitions keep only the
                    // renamed line (fact 7, A3.6).
                    if (state.Suffix != null
                        && KeepsPlainAndRenamedCopy(line.Text, nameStart, name.Length)
                        && RenamedCopyFollows(state, edited, name))
                    {
                        state.GenIndex++;
                        state.DecideRenamed(name);
                        return;
                    }

                    state.DecideNotRenamed(name);
                    return;
                }

                if (gen == RenameLineWithNames(state, edited, new[] { name }))
                {
                    state.DecideRenamed(name);
                    return;
                }
            }
            else if (gen == edited)
            {
                return;
            }

            throw new RefusalException(LineMismatchDetail, line.Number);
        }

        /// <summary>
        /// Reports whether the next generated line is the exact renamed
        /// copy of the plain line (A3.6).
        /// </summary>
        private static bool RenamedCopyFollows(WalkState state, string editedLine, string name)
        {
            var next = state.GenIndex + 1;
            if (next >= state.GeneratedLines.Count)
            {
                return false;
            }

            return state.GeneratedLines[next].Text == RenameLineWithNames(state, editedLine, new[] { name });
        }

        /// <summary>
        /// Reports whether the Properties definition is one line with a
        /// one word type. Only such a definition keeps the plain line and
        /// the renamed copy. A Range type and a multi line definition keep
        /// only the renamed line (fact 7, A3.6).
        /// </summary>
        private static bool KeepsPlainAndRenamedCopy(string lineText, int nameStart, int nameLength)
        {
            var open = lineText.IndexOf('(', nameStart + nameLength);
            if (open < 0)
            {
                return false;
            }

            var depth = 0;
            var close = -1;
            for (var i = open; i < lineText.Length; i++)
            {
                if (lineText[i] == '(')
                {
                    depth++;
                    continue;
                }

                if (lineText[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        close = i;
                        break;
                    }
                }
            }

            if (close < 0)
            {
                // The display text closes on a later line, so the
                // definition spans lines.
                return false;
            }

            if (lineText.IndexOf('=', close + 1) < 0)
            {
                // The default value sits on a later line, so the
                // definition spans lines.
                return false;
            }

            var inner = lineText.Substring(open + 1, close - open - 1);
            var comma = inner.IndexOf(',');
            if (comma < 0)
            {
                return false;
            }

            var type = inner.Substring(comma + 1).TrimStart();
            var typeEnd = 0;
            while (typeEnd < type.Length && (char.IsLetterOrDigit(type[typeEnd]) || type[typeEnd] == '_'))
            {
                typeEnd++;
            }

            if (typeEnd == 0)
            {
                return false;
            }

            // One word means the type token takes the whole rest of the
            // display text. A Range form holds parentheses after it.
            return type.Substring(typeEnd).Trim().Length == 0;
        }

        private static void MatchShaderLabLine(WalkState state, LineRef line)
        {
            if (state.GenIndex >= state.GeneratedLines.Count)
            {
                throw new RefusalException(LineMismatchDetail, line.Number);
            }

            var text = line.Text;
            var trimmed = text.Trim();
            if (trimmed.StartsWith("Shader \"", StringComparison.Ordinal))
            {
                // The segment after the locked name prefix is a hole that
                // records nothing (A3.4). A sub-asset suffix rides in it.
                var segments = new List<Seg>
                {
                    Seg.Literal("Shader \"Hidden/Locked/" + ShaderNameOf(text) + "/"),
                    Seg.FreeHole(),
                    Seg.Literal("\"")
                };
                if (!MatchSegments(state, state.GeneratedLines[state.GenIndex].Text, segments, line.Number, out _))
                {
                    throw new RefusalException(LineMismatchDetail, line.Number);
                }

                return;
            }

            if (TryMatchGenericShaderLab(state, text))
            {
                return;
            }

            var first = FirstToken(trimmed);
            if ((first == "Cull" || first == "ColorMask") && TryMatchRenderStateEnum(state, text, first))
            {
                return;
            }

            throw new RefusalException(LineMismatchDetail, line.Number);
        }

        private static List<string> UndecidedDeclaredInLine(WalkState state, string text)
        {
            var result = new List<string>();
            foreach (var property in state.Declared.Keys)
            {
                if (state.RenamedDecided.ContainsKey(property))
                {
                    continue;
                }

                if (ContainsToken(text, property))
                {
                    result.Add(property);
                }
            }

            result.Sort((a, b) => b.Length.CompareTo(a.Length));
            return result;
        }

        private static bool ContainsToken(string text, string name)
        {
            var i = 0;
            while ((i = text.IndexOf(name, i, StringComparison.Ordinal)) >= 0)
            {
                var end = i + name.Length;
                var leftOk = i == 0 || !IsWordChar(text[i - 1]);
                var rightOk = end >= text.Length || !IsWordChar(text[end]);
                if (leftOk && rightOk)
                {
                    return true;
                }

                i = end;
            }

            return false;
        }

        private static bool TryMatchGenericShaderLab(WalkState state, string text)
        {
            var edited = ApplyLockButtonDefault(text);
            var gen = state.GeneratedLines[state.GenIndex].Text;
            if (gen == RenameLineWithNames(state, edited, null))
            {
                RecordBracketReads(state, text);
                return true;
            }

            foreach (var name in UndecidedDeclaredInLine(state, text))
            {
                if (gen != RenameLineWithNames(state, edited, new[] { name }))
                {
                    continue;
                }

                state.DecideRenamed(name);
                RecordBracketReads(state, text);
                return true;
            }

            return false;
        }

        private static void RecordBracketReads(WalkState state, string text)
        {
            foreach (var name in BracketNamesOf(text))
            {
                var key = state.DerivedOf.TryGetValue(name, out var texture) ? texture : name;
                if (!state.Declared.ContainsKey(key))
                {
                    continue;
                }

                if (!state.RenamedDecided.ContainsKey(key))
                {
                    // A bracket that survived plain proves the property
                    // kept its plain name.
                    state.RenamedDecided[key] = false;
                }

                state.BracketReads[name] = state.RenamedNames.TryGetValue(name, out var renamed) ? renamed : name;
            }
        }

        private static bool TryMatchRenderStateEnum(WalkState state, string text, string token)
        {
            var bracketStart = text.IndexOf('[');
            var bracketEnd = text.IndexOf(']', bracketStart + 1);
            if (bracketStart < 0 || bracketEnd < 0)
            {
                return false;
            }

            var name = text.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            if (!state.Declared.ContainsKey(name) || state.DerivedOf.ContainsKey(name))
            {
                return false;
            }

            // Only a trailing bracket becomes enum text (fact 13).
            if (text.Substring(bracketEnd + 1).Trim().Length != 0)
            {
                return false;
            }

            var gen = state.GeneratedLines[state.GenIndex].Text;
            var prefix = text.Substring(0, bracketStart);
            if (token == "Cull")
            {
                foreach (var candidate in CullEnumTexts)
                {
                    if (MatchesAt(gen, prefix.Length, candidate) && gen.Length == prefix.Length + candidate.Length)
                    {
                        state.EnumValues[name] = Array.IndexOf(CullEnumTexts, candidate);
                        return true;
                    }
                }

                return false;
            }

            if (gen.Length < prefix.Length)
            {
                return false;
            }

            var suffix = gen.Substring(prefix.Length);
            var value = ParseColorMask(suffix);
            if (value == null || gen.Length != prefix.Length + suffix.Length)
            {
                return false;
            }

            state.EnumValues[name] = value.Value;
            return true;
        }

        private static int? ParseColorMask(string text)
        {
            // The vendor mask texts are letter sets or plain numbers.
            var value = 0;
            var seen = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var matched = false;
                foreach (var letter in ColorMaskLetters)
                {
                    if (text[i] != letter.Text[0])
                    {
                        continue;
                    }

                    if ((seen & letter.Value) != 0)
                    {
                        return null;
                    }

                    seen |= letter.Value;
                    value += letter.Value;
                    matched = true;
                    break;
                }

                if (!matched)
                {
                    return null;
                }
            }

            if (value > 0)
            {
                return value;
            }

            if (int.TryParse(text, out var number) && number >= 0 && number <= 255)
            {
                return number;
            }

            return null;
        }

        private static void MatchProgramLine(WalkState state, string text, int originalNumber)
        {
            if (state.GenIndex >= state.GeneratedLines.Count)
            {
                // A generated text that ends before the emulation needs it
                // refuses. It never crashes the walk.
                throw new RefusalException(LineMismatchDetail, originalNumber);
            }

            var renamedText = RenameLineWithNames(state, text, null);
            var holes = new List<HoleSpan>();
            foreach (var name in state.UndecidedNames())
            {
                ScanOccurrences(renamedText, name, holes);
            }

            // A declaration keeps its identifier (fact 4). A storage
            // class before the type keyword leaves the line a
            // declaration line, so this read matches FindIntDeclared.
            var declarationLine = IsDeclarationLine(renamedText.Trim());
            for (var i = holes.Count - 1; i >= 0; i--)
            {
                if (declarationLine && FollowsDeclarationPosition(renamedText, holes[i].End))
                {
                    holes.RemoveAt(i);
                }
            }

            holes.Sort((a, b) => a.Start.CompareTo(b.Start));
            var segments = new List<Seg>();
            var cursor = 0;
            foreach (var hole in holes)
            {
                if (hole.Start < cursor)
                {
                    continue;
                }

                if (hole.Start > cursor)
                {
                    segments.Add(Seg.Literal(renamedText.Substring(cursor, hole.Start - cursor)));
                }

                segments.Add(Seg.Hole(hole.Name));
                cursor = hole.End;
            }

            segments.Add(Seg.Literal(renamedText.Substring(cursor)));
            if (!MatchSegments(state, state.GeneratedLines[state.GenIndex].Text, segments, originalNumber, out _))
            {
                throw new RefusalException(LineMismatchDetail, originalNumber);
            }
        }

        private static bool MatchSegments(
            WalkState state,
            string gen,
            List<Seg> segments,
            int originalNumber,
            out int endPosition)
        {
            var pos = 0;
            foreach (var segment in segments)
            {
                if (segment.IsFree)
                {
                    // The shader id hole takes any nonempty text up to the
                    // closing quote and records nothing (A3.4).
                    if (gen.Length <= pos + 1 || gen[gen.Length - 1] != '"')
                    {
                        endPosition = pos;
                        return false;
                    }

                    pos = gen.Length - 1;
                    continue;
                }

                if (!segment.IsHole)
                {
                    if (pos + segment.Text.Length > gen.Length
                        || string.CompareOrdinal(gen, pos, segment.Text, 0, segment.Text.Length) != 0)
                    {
                        endPosition = pos;
                        return false;
                    }

                    pos += segment.Text.Length;
                    continue;
                }

                if (!TryMatchHole(state, gen, ref pos, segment.HoleName, originalNumber))
                {
                    endPosition = pos;
                    return false;
                }
            }

            endPosition = pos;
            return pos == gen.Length;
        }

        private static bool TryMatchHole(WalkState state, string gen, ref int pos, string name, int originalNumber)
        {
            if (state.DerivedOf.TryGetValue(name, out var texture))
            {
                if (state.Suffix != null && !state.RenamedDecided.ContainsKey(texture))
                {
                    // The derived name rides behind the renamed texture
                    // name (fact 7).
                    var renamedDerived = texture + state.Suffix + name.Substring(texture.Length);
                    if (MatchesAt(gen, pos, renamedDerived))
                    {
                        pos += renamedDerived.Length;
                        state.DecideRenamed(texture);
                        state.MarkUse(name, MatchKind.Identifier, null, originalNumber);
                        return true;
                    }
                }

                if (MatchesAt(gen, pos, name))
                {
                    if (!state.RenamedDecided.ContainsKey(texture))
                    {
                        state.RenamedDecided[texture] = false;
                    }

                    pos += name.Length;
                    state.MarkUse(name, MatchKind.Identifier, null, originalNumber);
                    return true;
                }
            }
            else
            {
                if (state.Suffix != null && !state.RenamedDecided.ContainsKey(name))
                {
                    var renamed = name + state.Suffix;
                    if (MatchesAt(gen, pos, renamed))
                    {
                        pos += renamed.Length;
                        state.DecideRenamed(name);
                        state.MarkUse(name, MatchKind.Identifier, null, originalNumber);
                        return true;
                    }
                }

                if (MatchesAt(gen, pos, name))
                {
                    if (!state.RenamedDecided.ContainsKey(name))
                    {
                        state.RenamedDecided[name] = false;
                    }

                    pos += name.Length;
                    state.MarkUse(name, MatchKind.Identifier, null, originalNumber);
                    return true;
                }
            }

            var shape = LiteralShapeOf(state, name);
            if (shape != LiteralShape.None && TryReadLiteral(state, gen, ref pos, name, shape, out var literal))
            {
                state.MarkUse(name, MatchKind.Literal, literal, originalNumber);
                if (!state.DerivedOf.ContainsKey(name) && !state.RenamedDecided.ContainsKey(name))
                {
                    // A baked property never carries the rename.
                    state.RenamedDecided[name] = false;
                }

                return true;
            }

            return false;
        }

        private static LiteralShape LiteralShapeOf(WalkState state, string name)
        {
            if (state.DerivedOf.ContainsKey(name))
            {
                // Scale offset and texel size print as float4 (fact 5).
                return LiteralShape.Float4;
            }

            if (!state.Declared.TryGetValue(name, out var property))
            {
                return LiteralShape.None;
            }

            if (property.Type == ShaderPropertyType.Texture)
            {
                // A texture never bakes (fact 8 keeps it live or renamed).
                return LiteralShape.None;
            }

            if (state.IntDeclared.Contains(name))
            {
                // A property whose first program declaration is int
                // prints with the F0 form (fact 5).
                return LiteralShape.F0;
            }

            if (property.Type == ShaderPropertyType.Color || property.Type == ShaderPropertyType.Vector)
            {
                return LiteralShape.Float4;
            }

            return LiteralShape.ScalarFloat;
        }

        private static bool TryReadLiteral(
            WalkState state,
            string gen,
            ref int pos,
            string name,
            LiteralShape shape,
            out string literal)
        {
            literal = null;
            var work = pos;
            // Developer mode inserts a comment before the literal (fact 5).
            var devPrefix = "/*" + name + "*/";
            if (MatchesAt(gen, work, " " + devPrefix))
            {
                work += devPrefix.Length + 1;
            }
            else if (MatchesAt(gen, work, devPrefix))
            {
                work += devPrefix.Length;
            }

            string read;
            switch (shape)
            {
                case LiteralShape.Float4:
                    if (!TryReadFloat4(gen, work, out read, out work))
                    {
                        return false;
                    }

                    break;
                case LiteralShape.F0:
                    if (!TryReadScalar(gen, work, false, false, out read, out work))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!TryReadScalar(gen, work, true, state.Era != PoiyomiLockEra.Bundled9364, out read, out work))
                    {
                        return false;
                    }

                    break;
            }

            pos = work;
            literal = read;
            return true;
        }

        private static bool TryReadScalar(
            string text,
            int pos,
            bool requireDecimalPoint,
            bool allowParenWrap,
            out string literal,
            out int next)
        {
            literal = null;
            next = pos;
            if (allowParenWrap && pos < text.Length && text[pos] == '(')
            {
                // 2.74 wraps a negative float in parentheses (fact 5).
                var close = text.IndexOf(')', pos + 1);
                if (close < 0)
                {
                    return false;
                }

                if (!TryReadBareScalar(text, pos + 1, requireDecimalPoint, out var inner, out var innerNext)
                    || innerNext != close
                    || !inner.StartsWith("-", StringComparison.Ordinal))
                {
                    return false;
                }

                literal = inner;
                next = close + 1;
                return true;
            }

            if (!TryReadBareScalar(text, pos, requireDecimalPoint, out var bare, out next))
            {
                return false;
            }

            literal = bare;
            return true;
        }

        private static bool TryReadBareScalar(
            string text,
            int pos,
            bool requireDecimalPoint,
            out string literal,
            out int next)
        {
            literal = null;
            next = pos;
            var i = pos;
            if (i < text.Length && text[i] == '-')
            {
                i++;
            }

            var digitsStart = i;
            while (i < text.Length && char.IsDigit(text[i]))
            {
                i++;
            }

            if (i == digitsStart)
            {
                return false;
            }

            if (requireDecimalPoint)
            {
                // The float form prints one digit group, a point, and up
                // to twenty decimals (fact 5).
                if (i >= text.Length || text[i] != '.')
                {
                    return false;
                }

                i++;
                var fractionStart = i;
                while (i < text.Length && char.IsDigit(text[i]))
                {
                    i++;
                }

                var fraction = i - fractionStart;
                if (fraction < 1 || fraction > 20)
                {
                    return false;
                }
            }
            else if (i < text.Length && text[i] == '.')
            {
                // The F0 form prints no decimal point (fact 5).
                return false;
            }

            literal = text.Substring(pos, i - pos);
            next = i;
            return true;
        }

        private static bool TryReadFloat4(string text, int pos, out string literal, out int next)
        {
            literal = null;
            next = pos;
            const string head = "float4(";
            if (!MatchesAt(text, pos, head))
            {
                return false;
            }

            var i = pos + head.Length;
            for (var component = 0; component < 4; component++)
            {
                var digits = 0;
                if (i < text.Length && text[i] == '-')
                {
                    i++;
                }

                while (i < text.Length && char.IsDigit(text[i]))
                {
                    i++;
                    digits++;
                }

                if (i < text.Length && text[i] == '.')
                {
                    i++;
                    while (i < text.Length && char.IsDigit(text[i]))
                    {
                        i++;
                        digits++;
                    }
                }

                if (digits == 0)
                {
                    return false;
                }

                if (component < 3)
                {
                    if (i >= text.Length || text[i] != ',')
                    {
                        return false;
                    }

                    i++;
                }
            }

            if (i >= text.Length || text[i] != ')')
            {
                return false;
            }

            literal = text.Substring(pos, i + 1 - pos);
            next = i + 1;
            return true;
        }

        // --- A3.5: the rename emulation -------------------------------

        private static string RenameLineWithNames(WalkState state, string text, ICollection<string> additionalNames)
        {
            if (state.Suffix == null)
            {
                return text;
            }

            var pairs = new List<KeyValuePair<string, string>>();
            foreach (var property in state.RenamedDecided.Keys)
            {
                if (!state.RenamedDecided[property])
                {
                    continue;
                }

                pairs.Add(new KeyValuePair<string, string>(property, state.RenamedNames[property]));
                AppendDerivedPairs(state, property, pairs);
            }

            if (additionalNames != null)
            {
                foreach (var name in additionalNames)
                {
                    if (state.RenamedDecided.TryGetValue(name, out var decided) && decided)
                    {
                        continue;
                    }

                    pairs.Add(new KeyValuePair<string, string>(name, name + state.Suffix));
                    if (state.IsTexture(name))
                    {
                        pairs.Add(new KeyValuePair<string, string>(name + "_ST", name + state.Suffix + "_ST"));
                        pairs.Add(new KeyValuePair<string, string>(
                            name + "_TexelSize", name + state.Suffix + "_TexelSize"));
                    }
                }
            }

            if (pairs.Count == 0)
            {
                return text;
            }

            // The longest names go first, so a derived rename runs before
            // the bare name rule.
            pairs.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            var result = text;
            foreach (var pair in pairs)
            {
                result = ReplaceName(result, pair.Key, pair.Value, state.Era);
            }

            return result;
        }

        private static void AppendDerivedPairs(
            WalkState state,
            string property,
            List<KeyValuePair<string, string>> pairs)
        {
            if (!state.IsTexture(property))
            {
                return;
            }

            pairs.Add(new KeyValuePair<string, string>(property + "_ST", state.RenamedNames[property + "_ST"]));
            pairs.Add(new KeyValuePair<string, string>(
                property + "_TexelSize", state.RenamedNames[property + "_TexelSize"]));
        }

        private static string ReplaceName(string text, string name, string renamed, PoiyomiLockEra era)
        {
            var builder = new StringBuilder(text.Length);
            var copied = 0;
            var i = 0;
            while ((i = text.IndexOf(name, i, StringComparison.Ordinal)) >= 0)
            {
                var end = i + name.Length;
                var rightOk = end >= text.Length || !IsWordChar(text[end]);
                var leftOk = true;
                if (era != PoiyomiLockEra.Bundled9364)
                {
                    // 2.74 keeps a left word boundary (fact 7). Only
                    // 2.74.3 also renames the sampler form samplerP.
                    leftOk = i == 0 || !IsWordChar(text[i - 1]);
                    if (!leftOk && era == PoiyomiLockEra.Thry2743 && i >= 7
                        && string.CompareOrdinal(text, i - 7, "sampler", 0, 7) == 0)
                    {
                        leftOk = i - 8 < 0 || !IsWordChar(text[i - 8]);
                    }
                }

                if (leftOk && rightOk)
                {
                    builder.Append(text, copied, i - copied);
                    builder.Append(renamed);
                    copied = end;
                }

                i = end;
            }

            builder.Append(text, copied, text.Length - copied);
            return builder.ToString();
        }

        // --- walk end and the record ----------------------------------

        private static void FinishWalk(WalkState state)
        {
            if (state.GenIndex != state.GeneratedLines.Count)
            {
                throw new RefusalException(UnconsumedLineDetail, state.LastConsumedOriginalNumber);
            }

            // Every program block must carry the optimizer define. A lock
            // made under another render pipeline bakes nothing and refuses
            // (A2.4).
            foreach (var run in state.DefineRuns)
            {
                if (!run.Contains("OPTIMIZER_ENABLED"))
                {
                    throw new RefusalException(OptimizerMissingDetail, 0);
                }
            }

            // The vendor writes enum text only for a baked property
            // (fact 13). A5.6 also accepts enum text for a property with
            // no surviving use, so only a present non literal use refuses.
            foreach (var key in state.EnumValues.Keys)
            {
                if (state.MatchKinds.TryGetValue(key, out var kind) && kind != MatchKind.Literal)
                {
                    throw new RefusalException(EnumWithoutBakeDetail, 0);
                }
            }
        }

        private static AlignedLockedSource BuildSource(WalkState state)
        {
            var states = new Dictionary<string, LockedPropertyState>();
            foreach (var property in state.Declared.Keys)
            {
                states[property] = state.StateOf(property);
            }

            foreach (var derived in state.DerivedOf.Keys)
            {
                states[derived] = state.StateOf(derived);
            }

            var defineRuns = new List<IReadOnlyList<string>>();
            foreach (var run in state.DefineRuns)
            {
                defineRuns.Add(new List<string>(run));
            }

            return new AlignedLockedSource(
                state.Era,
                state.Suffix,
                states,
                new Dictionary<string, string>(state.RenamedNames),
                new Dictionary<string, string>(state.Literals),
                new Dictionary<string, int>(state.EnumValues),
                new Dictionary<string, string>(state.BracketReads),
                defineRuns,
                new List<LockedBlockRecord>(state.Blocks),
                string.Join("\n", state.SurvivingLines));
        }

        private static bool FactsEqual(AlignedLockedSource left, AlignedLockedSource right)
        {
            // A2.2: every era that aligns must give the same facts. The
            // era field itself is not a fact.
            return string.Equals(left.Suffix, right.Suffix, StringComparison.Ordinal)
                && string.Equals(left.SurvivingText, right.SurvivingText, StringComparison.Ordinal)
                && SamePairs(left.States, right.States)
                && SamePairs(left.RenamedNames, right.RenamedNames)
                && SamePairs(left.BakedLiterals, right.BakedLiterals)
                && SamePairs(left.EnumValues, right.EnumValues)
                && SamePairs(left.BracketReads, right.BracketReads)
                && SameRuns(left.DefineRuns, right.DefineRuns)
                && SameBlocks(left.Blocks, right.Blocks);
        }

        private static bool SameBlocks(
            IReadOnlyList<LockedBlockRecord> left,
            IReadOnlyList<LockedBlockRecord> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].Kind != right[i].Kind
                    || left[i].Outcome != right[i].Outcome
                    || left[i].OriginalLine != right[i].OriginalLine
                    || !string.Equals(left[i].ConditionOrKeyword, right[i].ConditionOrKeyword, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SamePairs<T>(
            IReadOnlyDictionary<string, T> left,
            IReadOnlyDictionary<string, T> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var value)
                    || !EqualityComparer<T>.Default.Equals(pair.Value, value))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameRuns(IReadOnlyList<IReadOnlyList<string>> left, IReadOnlyList<IReadOnlyList<string>> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i].Count != right[i].Count)
                {
                    return false;
                }

                for (var j = 0; j < left[i].Count; j++)
                {
                    if (left[i][j] != right[i][j])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // --- shared text helpers --------------------------------------

        private static bool TryPropertiesName(string line, out string name, out int nameStart)
        {
            name = null;
            nameStart = -1;
            var trimmed = line.TrimStart();
            var pos = 0;
            while (pos < trimmed.Length && trimmed[pos] == '[')
            {
                var close = trimmed.IndexOf(']', pos + 1);
                if (close < 0)
                {
                    return false;
                }

                pos = close + 1;
                while (pos < trimmed.Length && trimmed[pos] == ' ')
                {
                    pos++;
                }
            }

            var start = pos;
            while (pos < trimmed.Length && (char.IsLetterOrDigit(trimmed[pos]) || trimmed[pos] == '_'))
            {
                pos++;
            }

            if (pos == start)
            {
                return false;
            }

            name = trimmed.Substring(start, pos - start);
            nameStart = line.Length - trimmed.Length + start;
            return true;
        }

        private static List<int> PropertiesLineIndices(IReadOnlyList<LineRef> lines)
        {
            var result = new List<int>();
            var inBlock = false;
            var depth = 0;
            for (var i = 0; i < lines.Count; i++)
            {
                var text = lines[i].Text;
                if (!inBlock)
                {
                    if (FirstToken(text.Trim()) == "Properties")
                    {
                        inBlock = true;
                        depth = CountChar(text, '{') - CountChar(text, '}');
                    }

                    continue;
                }

                var depthBefore = depth;
                depth += CountChar(text, '{') - CountChar(text, '}');
                if (depthBefore > 0)
                {
                    result.Add(i);
                }

                if (depth <= 0 && depthBefore > 0)
                {
                    inBlock = false;
                }
            }

            return result;
        }

        private static void CollectIdentifiers(string line, ICollection<string> into)
        {
            var i = 0;
            while (i < line.Length)
            {
                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    var start = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                    {
                        i++;
                    }

                    into.Add(line.Substring(start, i - start));
                    continue;
                }

                i++;
            }
        }

        private static List<string> BracketNamesOf(string text)
        {
            var names = new List<string>();
            var i = 0;
            while ((i = text.IndexOf('[', i)) >= 0)
            {
                var j = i + 1;
                while (j < text.Length && (char.IsLetterOrDigit(text[j]) || text[j] == '_'))
                {
                    j++;
                }

                if (j > i + 1 && j < text.Length && text[j] == ']')
                {
                    names.Add(text.Substring(i + 1, j - i - 1));
                }

                i = j;
            }

            return names;
        }

        private static void ScanOccurrences(string text, string name, List<HoleSpan> into)
        {
            var i = 0;
            while ((i = text.IndexOf(name, i, StringComparison.Ordinal)) >= 0)
            {
                var end = i + name.Length;
                var leftOk = i == 0 || !IsWordChar(text[i - 1]);
                var rightOk = end >= text.Length || !IsWordChar(text[end]);
                if (leftOk && rightOk)
                {
                    into.Add(new HoleSpan(i, end, name));
                }

                i = end;
            }
        }

        /// <summary>
        /// True when the line starts with an optional run of storage
        /// class keywords followed by a type keyword. This matches the
        /// declaration read of FindIntDeclared (fact 4). Any other
        /// first token is no declaration.
        /// </summary>
        private static bool IsDeclarationLine(string trimmedText)
        {
            var position = 0;
            while (position < trimmedText.Length)
            {
                var start = position;
                while (start < trimmedText.Length
                    && char.IsWhiteSpace(trimmedText[start]))
                {
                    start++;
                }

                var end = start;
                while (end < trimmedText.Length
                    && !char.IsWhiteSpace(trimmedText[end]))
                {
                    end++;
                }

                if (end == start)
                {
                    break;
                }

                var token = trimmedText.Substring(start, end - start);
                if (TypeKeywords.Contains(token))
                {
                    return true;
                }

                if (token != "uniform" && token != "static" && token != "const")
                {
                    return false;
                }

                position = end;
            }

            return false;
        }

        private static bool FollowsDeclarationPosition(string text, int end)
        {
            var i = end;
            while (i < text.Length && text[i] == ' ')
            {
                i++;
            }

            if (i >= text.Length)
            {
                return false;
            }

            var c = text[i];
            return c == ';' || c == '=' || c == ',' || c == ')' || c == ':';
        }

        private static string ApplyLockButtonDefault(string text)
        {
            if (!text.Contains("[ThryShaderOptimizerLockButton]"))
            {
                return text;
            }

            var eq = text.LastIndexOf('=');
            if (eq < 0)
            {
                return text;
            }

            // The lock button default becomes 1 (fact 13).
            return text.Substring(0, eq + 1) + " 1";
        }

        private static string ShaderNameOf(string text)
        {
            var open = text.IndexOf('"');
            var close = text.LastIndexOf('"');
            return text.Substring(open + 1, close - open - 1);
        }

        private static bool MatchesAt(string text, int pos, string candidate)
        {
            if (pos + candidate.Length > text.Length)
            {
                return false;
            }

            return string.CompareOrdinal(text, pos, candidate, 0, candidate.Length) == 0;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static bool IsIdentifier(string text)
        {
            if (text.Length == 0 || (!char.IsLetter(text[0]) && text[0] != '_'))
            {
                return false;
            }

            foreach (var c in text)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static string FirstToken(string text)
        {
            var start = 0;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            var end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '{' && text[end] != '"')
            {
                end++;
            }

            return text.Substring(start, end - start);
        }

        private static int CountChar(string text, char c)
        {
            var count = 0;
            foreach (var current in text)
            {
                if (current == c)
                {
                    count++;
                }
            }

            return count;
        }

        // The declaration type keywords of fact 4. The closed list keeps
        // declaration lines out of the bake holes.
        private static readonly HashSet<string> TypeKeywords = BuildTypeKeywords();

        private static HashSet<string> BuildTypeKeywords()
        {
            var set = new HashSet<string>(StringComparer.Ordinal)
            {
                "float", "half", "fixed", "int", "uint", "bool",
                "sampler", "sampler1D", "sampler2D", "sampler3D", "samplerCUBE",
                "sampler2D_float", "sampler2D_half", "sampler2D_array",
                "sampler3D_float", "sampler3D_half", "sampler_state"
            };
            foreach (var baseType in new[] { "float", "half", "fixed" })
            {
                for (var rows = 1; rows <= 4; rows++)
                {
                    set.Add(baseType + rows);
                    for (var columns = 1; columns <= 4; columns++)
                    {
                        set.Add(baseType + rows + "x" + columns);
                    }
                }
            }

            return set;
        }

        private enum MatchKind
        {
            None,
            Identifier,
            Literal
        }

        private enum LiteralShape
        {
            None,
            ScalarFloat,
            F0,
            Float4
        }

        private readonly struct LineRef
        {
            internal LineRef(string text, int number)
            {
                Text = text;
                Number = number;
            }

            internal string Text { get; }

            internal int Number { get; }
        }

        private readonly struct HoleSpan
        {
            internal HoleSpan(int start, int end, string name)
            {
                Start = start;
                End = end;
                Name = name;
            }

            internal int Start { get; }

            internal int End { get; }

            internal string Name { get; }
        }

        private readonly struct Seg
        {
            private Seg(bool isHole, bool isFree, string text, string holeName)
            {
                IsHole = isHole;
                IsFree = isFree;
                Text = text;
                HoleName = holeName;
            }

            internal static Seg Literal(string text)
            {
                return new Seg(false, false, text, null);
            }

            internal static Seg Hole(string holeName)
            {
                return new Seg(true, false, null, holeName);
            }

            internal static Seg FreeHole()
            {
                return new Seg(false, true, null, null);
            }

            internal bool IsHole { get; }

            internal bool IsFree { get; }

            internal string Text { get; }

            internal string HoleName { get; }
        }

        private sealed class RefusalException : Exception
        {
            internal RefusalException(string detail, int originalLine)
                : this(detail, originalLine, false)
            {
            }

            internal RefusalException(string detail, int originalLine, bool undecided)
                : base(detail)
            {
                Detail = detail;
                OriginalLine = originalLine;
                Undecided = undecided;
            }

            internal string Detail { get; }

            internal int OriginalLine { get; }

            /// <summary>
            /// True when the refusal says the text cannot decide an
            /// outcome. A hypothesis trial never swallows it, because an
            /// undecided nested block must refuse the whole alignment
            /// (A3.7).
            /// </summary>
            internal bool Undecided { get; }
        }

        /// <summary>
        /// The mutable state of one era walk. The walk is a plain forward
        /// pass over the original line list, so a later change can run it
        /// over a line range for block hypotheses (A3.7).
        /// </summary>
        private sealed class WalkState
        {
            internal WalkState(
                string originalText,
                IReadOnlyList<LineRef> originalLines,
                IReadOnlyList<LineRef> generatedLines,
                IReadOnlyList<LockedSourceProperty> properties,
                PoiyomiLockEra era)
            {
                OriginalText = originalText;
                OriginalLines = originalLines;
                GeneratedLines = generatedLines;
                Era = era;
                Declared = new Dictionary<string, LockedSourceProperty>(StringComparer.Ordinal);
                TextureNames = new HashSet<string>(StringComparer.Ordinal);
                DerivedOf = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in properties)
                {
                    Declared[property.Name] = property;
                    if (property.Type == ShaderPropertyType.Texture)
                    {
                        TextureNames.Add(property.Name);
                        DerivedOf[property.Name + "_ST"] = property.Name;
                        DerivedOf[property.Name + "_TexelSize"] = property.Name;
                    }
                }

                IntDeclared = FindIntDeclared(originalLines, Declared);
                renamedDecided = new Dictionary<string, bool>(StringComparer.Ordinal);
                renamedNames = new Dictionary<string, string>(StringComparer.Ordinal);
                matchKinds = new Dictionary<string, MatchKind>(StringComparer.Ordinal);
                literals = new Dictionary<string, string>(StringComparer.Ordinal);
                enumValues = new Dictionary<string, int>(StringComparer.Ordinal);
                bracketReads = new Dictionary<string, string>(StringComparer.Ordinal);
                defineRuns = new List<List<string>>();
                survivingLines = new List<string>();
                blocks = new List<LockedBlockRecord>();
                PropertiesLineSet = new HashSet<int>(PropertiesLineIndices(originalLines));
                LookaheadRemaining = -1;
                GrowthBoundary = -1;
            }

            internal string OriginalText { get; }

            internal IReadOnlyList<LineRef> OriginalLines { get; }

            internal IReadOnlyList<LineRef> GeneratedLines { get; }

            internal PoiyomiLockEra Era { get; }

            internal string Suffix { get; set; }

            internal Dictionary<string, LockedSourceProperty> Declared { get; }

            internal HashSet<string> TextureNames { get; }

            internal Dictionary<string, string> DerivedOf { get; }

            internal HashSet<string> IntDeclared { get; }

            /// <summary>The indices of the Properties block lines.</summary>
            internal HashSet<int> PropertiesLineSet { get; }

            private Dictionary<string, bool> renamedDecided;

            internal Dictionary<string, bool> RenamedDecided
            {
                get { return renamedDecided; }
            }

            private Dictionary<string, string> renamedNames;

            internal Dictionary<string, string> RenamedNames
            {
                get { return renamedNames; }
            }

            private Dictionary<string, MatchKind> matchKinds;

            internal Dictionary<string, MatchKind> MatchKinds
            {
                get { return matchKinds; }
            }

            private Dictionary<string, string> literals;

            internal Dictionary<string, string> Literals
            {
                get { return literals; }
            }

            private Dictionary<string, int> enumValues;

            internal Dictionary<string, int> EnumValues
            {
                get { return enumValues; }
            }

            private Dictionary<string, string> bracketReads;

            internal Dictionary<string, string> BracketReads
            {
                get { return bracketReads; }
            }

            private List<List<string>> defineRuns;

            internal List<List<string>> DefineRuns
            {
                get { return defineRuns; }
            }

            internal List<string> OpenDefineRun { get; set; }

            // The surviving line list is shared between hypothesis clones
            // until one of them adds a line. Copy on write keeps the clone
            // cost flat for the big shader texts (A3.7).
            private List<string> survivingLines;

            private bool survivingLinesShared;

            internal List<string> SurvivingLines
            {
                get
                {
                    if (survivingLinesShared)
                    {
                        survivingLines = new List<string>(survivingLines);
                        survivingLinesShared = false;
                    }

                    return survivingLines;
                }
            }

            // The block record list shares the same copy on write rule.
            private List<LockedBlockRecord> blocks;

            private bool blocksShared;

            internal List<LockedBlockRecord> Blocks
            {
                get
                {
                    if (blocksShared)
                    {
                        blocks = new List<LockedBlockRecord>(blocks);
                        blocksShared = false;
                    }

                    return blocks;
                }
            }

            /// <summary>The index of the next unwalked original line.</summary>
            internal int OriginalIndex { get; set; }

            /// <summary>
            /// Remaining surviving lines of the active lookahead window.
            /// Negative when no window is active (A3.7).
            /// </summary>
            internal int LookaheadRemaining { get; set; }

            /// <summary>
            /// The index that bounds a lookahead walk. A nested block never
            /// judges lines whose survival the enclosing block decides.
            /// Negative when no boundary exists (A3.7).
            /// </summary>
            internal int GrowthBoundary { get; set; }

            internal int GenIndex { get; set; }

            internal int LastConsumedOriginalNumber { get; set; }

            internal bool InProgram { get; set; }

            /// <summary>
            /// Copies the walk state for one block hypothesis trial. The
            /// line lists and the property tables are immutable after the
            /// start, so the clone shares them. The mutable containers get
            /// fresh copies (A3.7).
            /// </summary>
            internal WalkState Clone()
            {
                var copy = (WalkState)MemberwiseClone();
                copy.renamedDecided = new Dictionary<string, bool>(renamedDecided, StringComparer.Ordinal);
                copy.renamedNames = new Dictionary<string, string>(renamedNames, StringComparer.Ordinal);
                copy.matchKinds = new Dictionary<string, MatchKind>(matchKinds, StringComparer.Ordinal);
                copy.literals = new Dictionary<string, string>(literals, StringComparer.Ordinal);
                copy.enumValues = new Dictionary<string, int>(enumValues, StringComparer.Ordinal);
                copy.bracketReads = new Dictionary<string, string>(bracketReads, StringComparer.Ordinal);
                copy.defineRuns = new List<List<string>>(defineRuns);
                copy.OpenDefineRun = OpenDefineRun == null ? null : new List<string>(OpenDefineRun);
                copy.survivingLines = survivingLines;
                copy.survivingLinesShared = true;
                survivingLinesShared = true;
                copy.blocks = blocks;
                copy.blocksShared = true;
                blocksShared = true;
                return copy;
            }

            /// <summary>
            /// Takes every mutable field from the winning hypothesis trial
            /// (A3.7).
            /// </summary>
            internal void AdoptFrom(WalkState winner)
            {
                renamedDecided = winner.renamedDecided;
                renamedNames = winner.renamedNames;
                matchKinds = winner.matchKinds;
                literals = winner.literals;
                enumValues = winner.enumValues;
                bracketReads = winner.bracketReads;
                defineRuns = winner.defineRuns;
                OpenDefineRun = winner.OpenDefineRun;
                survivingLines = winner.survivingLines;
                survivingLinesShared = winner.survivingLinesShared;
                blocks = winner.blocks;
                blocksShared = winner.blocksShared;
                OriginalIndex = winner.OriginalIndex;
                LookaheadRemaining = winner.LookaheadRemaining;
                GenIndex = winner.GenIndex;
                LastConsumedOriginalNumber = winner.LastConsumedOriginalNumber;
                InProgram = winner.InProgram;
            }

            internal bool IsTexture(string name)
            {
                return TextureNames.Contains(name);
            }

            internal void DecideNotRenamed(string property)
            {
                RenamedDecided[property] = false;
            }

            internal void DecideRenamed(string property)
            {
                RenamedDecided[property] = true;
                RenamedNames[property] = property + Suffix;
                if (IsTexture(property))
                {
                    // Fact 7 renames the derived texture names with the
                    // suffix before their tail.
                    RenamedNames[property + "_ST"] = property + Suffix + "_ST";
                    RenamedNames[property + "_TexelSize"] = property + Suffix + "_TexelSize";
                }
            }

            internal void MarkUse(string name, MatchKind kind, string literal, int originalNumber)
            {
                if (!MatchKinds.TryGetValue(name, out var existing))
                {
                    MatchKinds[name] = kind;
                    if (kind == MatchKind.Literal)
                    {
                        Literals[name] = literal;
                    }

                    return;
                }

                if (existing == kind)
                {
                    if (kind == MatchKind.Literal && Literals[name] != literal)
                    {
                        throw new RefusalException(TwoLiteralsDetail, originalNumber);
                    }

                    return;
                }

                throw new RefusalException(MixedUsesDetail, originalNumber);
            }

            internal LockedPropertyState StateOf(string name)
            {
                if (RenamedNames.ContainsKey(name))
                {
                    return LockedPropertyState.Renamed;
                }

                if (MatchKinds.TryGetValue(name, out var kind))
                {
                    if (kind == MatchKind.Literal)
                    {
                        return LockedPropertyState.Baked;
                    }

                    if (kind == MatchKind.Identifier)
                    {
                        return LockedPropertyState.Live;
                    }
                }

                return LockedPropertyState.Unused;
            }

            internal List<string> UndecidedNames()
            {
                // A property that decided against the rename still takes
                // identifier and literal holes. Only a decided renamed
                // property leaves the hole set, because the line rename
                // already rewrote its occurrences.
                var names = new List<string>();
                foreach (var property in Declared.Keys)
                {
                    if (!(RenamedDecided.TryGetValue(property, out var decided) && decided))
                    {
                        names.Add(property);
                    }
                }

                foreach (var derived in DerivedOf.Keys)
                {
                    var texture = DerivedOf[derived];
                    if (!(RenamedDecided.TryGetValue(texture, out var textureDecided) && textureDecided))
                    {
                        names.Add(derived);
                    }
                }

                names.Sort((a, b) => b.Length.CompareTo(a.Length));
                return names;
            }

            internal void FlushDefineRun()
            {
                if (OpenDefineRun == null)
                {
                    return;
                }

                DefineRuns.Add(OpenDefineRun);
                OpenDefineRun = null;
            }

            private static HashSet<string> FindIntDeclared(
                IReadOnlyList<LineRef> lines,
                Dictionary<string, LockedSourceProperty> declared)
            {
                var result = new HashSet<string>(StringComparer.Ordinal);
                var inProgram = false;
                foreach (var line in lines)
                {
                    var trimmed = line.Text.Trim();
                    if (trimmed == "CGPROGRAM" || trimmed == "CGINCLUDE")
                    {
                        inProgram = true;
                        continue;
                    }

                    if (trimmed == "ENDCG")
                    {
                        inProgram = false;
                        continue;
                    }

                    if (!inProgram)
                    {
                        continue;
                    }

                    var tokens = trimmed.Split(SpaceSeparators, System.StringSplitOptions.RemoveEmptyEntries);
                    var index = 0;
                    while (index < tokens.Length
                        && (tokens[index] == "uniform" || tokens[index] == "static" || tokens[index] == "const"))
                    {
                        index++;
                    }

                    if (index + 1 >= tokens.Length || tokens[index] != "int")
                    {
                        continue;
                    }

                    var second = tokens[index + 1].TrimEnd(';', ',', '=', '[', ']');
                    if (declared.ContainsKey(second))
                    {
                        result.Add(second);
                    }
                }

                return result;
            }

            private static readonly char[] SpaceSeparators = { ' ', '\t' };
        }
    }
}
