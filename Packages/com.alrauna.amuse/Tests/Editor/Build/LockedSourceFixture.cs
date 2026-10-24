using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Builds one locked shader pair for the reconstruction tests. The
    /// original is the shipped semantic test shader asset, so the seams
    /// that match that shader by identity keep working. The fixture reads
    /// its text and writes only the generated partner, named
    /// Hidden/Locked/&lt;original name&gt;/&lt;id&gt;, through the shared test
    /// shader writer. The partner emulates the vendor lock edits by hand
    /// from the spec rules; it is never a copy of vendor code.
    /// <para>
    /// The default partner drops comment, blank, and shader feature pragma
    /// lines, writes the locked shader name, and inserts the optimizer
    /// define after each program start. Every property stays live and no
    /// name renames. Options bake one program use, rename with a given
    /// suffix, add one define to each define run, change one generated
    /// line, or edit the original lines. An edited original text is
    /// written as its own shader asset, because the recorded original
    /// must resolve as an asset.
    /// </para>
    /// </summary>
    internal sealed class LockedSourceFixture
    {
        internal const string FixtureFolder = "Assets/AmuseTests_LockedSource";

        /// <summary>The shipped original shader asset of every pair.</summary>
        internal const string ShippedOriginalShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";

        // The lock button attribute the vendor default rule rewrites
        // (fact 13). The shipped original carries no such line, so the
        // default partner leaves its flag property line untouched.
        private const string LockButtonAttribute =
            "[ThryShaderOptimizerLockButton]";

        private const string OptimizerDefineLine =
            " #define OPTIMIZER_ENABLED ";

        // The type words that start a declaration line. A bake keeps the
        // identifier of a declaration, because the vendor keeps it too
        // (fact 4).
        private static readonly HashSet<string> TypeWords =
            new HashSet<string>
            {
                "sampler", "sampler2D", "sampler3D", "samplerCUBE",
                "struct", "void", "fixed", "fixed2", "fixed3", "fixed4",
                "half", "half2", "half3", "half4",
                "float", "float2", "float3", "float4", "float4x4",
                "int", "uint", "bool", "appdata", "v2f",
            };

        private readonly List<string> originalLineRemovals =
            new List<string>();

        private readonly List<string> programInsertions =
            new List<string>();

        private readonly List<string> fragmentInsertions =
            new List<string>();

        private readonly List<string> programHeadInsertions =
            new List<string>();

        private string renameSuffix;

        private readonly List<KeyValuePair<string, string>> bakes =
            new List<KeyValuePair<string, string>>();

        private readonly List<string> addedDefines = new List<string>();

        private readonly List<KeyValuePair<string, string>>
            generatedLineEdits =
                new List<KeyValuePair<string, string>>();

        private readonly string pinnedId;
        private bool built;
        private Shader original;
        private Shader partner;

        internal LockedSourceFixture(string id = null)
        {
            pinnedId = id;
        }

        internal Shader Original
        {
            get
            {
                Build();
                return original;
            }
        }

        internal Shader Partner
        {
            get
            {
                Build();
                return partner;
            }
        }

        /// <summary>The id that names the pair folder on disk.</summary>
        internal string Id
        {
            get { return pinnedId ?? "f" + Fnv1a(Signature()); }
        }

        /// <summary>
        /// Removes the first original line whose trimmed text starts with
        /// the prefix. The edited original text becomes its own asset.
        /// </summary>
        internal LockedSourceFixture WithoutOriginalLine(
            string trimmedPrefix)
        {
            originalLineRemovals.Add(trimmedPrefix);
            return this;
        }

        /// <summary>
        /// Inserts lines at the end of the program body of the original,
        /// before its end marker. The edited original text becomes its own
        /// asset.
        /// </summary>
        internal LockedSourceFixture WithProgramLines(
            params string[] lines)
        {
            programInsertions.AddRange(lines);
            return this;
        }

        /// <summary>
        /// Inserts lines at the end of the fragment function body of the
        /// original, before its closing brace. The edited original text
        /// becomes its own asset.
        /// </summary>
        internal LockedSourceFixture WithFragmentLines(
            params string[] lines)
        {
            fragmentInsertions.AddRange(lines);
            return this;
        }

        /// <summary>
        /// Inserts lines right after the first program start of the
        /// original, before any surviving statement. The edited original
        /// text becomes its own asset.
        /// </summary>
        internal LockedSourceFixture WithProgramHeadLines(
            params string[] lines)
        {
            programHeadInsertions.AddRange(lines);
            return this;
        }

        /// <summary>
        /// Renames every property of the partner with the suffix, joined
        /// by one underscore, the way the vendor rename writes the names
        /// (fact 7).
        /// </summary>
        internal LockedSourceFixture WithRename(string suffix)
        {
            renameSuffix = suffix;
            return this;
        }

        /// <summary>
        /// Replaces every program use of one property with the literal
        /// text, the way the vendor bake writes it (fact 4). The literal
        /// text must already carry the vendor print format.
        /// </summary>
        internal LockedSourceFixture WithBake(
            string propertyName, string literalText)
        {
            bakes.Add(new KeyValuePair<string, string>(
                propertyName, literalText));
            return this;
        }

        /// <summary>
        /// Adds one keyword define to every define run of the partner,
        /// before the optimizer define (fact 8).
        /// </summary>
        internal LockedSourceFixture WithAddedDefine(string name)
        {
            addedDefines.Add(name);
            return this;
        }

        /// <summary>
        /// Replaces the first generated line whose trimmed text starts
        /// with the prefix. A change the vendor never produces breaks the
        /// alignment, so refusal tests use this.
        /// </summary>
        internal LockedSourceFixture WithGeneratedLine(
            string startsWith, string replacement)
        {
            generatedLineEdits.Add(new KeyValuePair<string, string>(
                startsWith, replacement));
            return this;
        }

        /// <summary>
        /// Writes both rename suffix tags with one value that disagrees
        /// with the real suffix in the generated text. The reconstruction
        /// must ignore both, because the text is the only truth source
        /// (A1.1).
        /// </summary>
        internal void WriteDecoySuffixTags(
            Material locked, string tagValue)
        {
            locked.SetOverrideTag("thry_locked_rename_suffix", tagValue);
            locked.SetOverrideTag("thry_rename_suffix", tagValue);
        }

        /// <summary>
        /// Builds the locked material on the partner: the lock flag at
        /// one, and the identity tags that record the original name and
        /// GUID.
        /// </summary>
        internal Material Locked(string materialName)
        {
            Build();
            var locked = new Material(partner) { name = materialName };
            locked.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                original.name);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    original, out var guid, out long _))
            {
                throw new InvalidOperationException(
                    "The fixture original shader must have a GUID.");
            }

            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName, guid);
            return locked;
        }

        /// <summary>
        /// Deletes the fixture folder with every pair of this run.
        /// </summary>
        internal static void DeleteFolder()
        {
            if (AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.DeleteAsset(FixtureFolder);
            }
        }

        private void Build()
        {
            if (built)
            {
                return;
            }

            built = true;
            var originalLines = ShippedOriginalLines();
            var originalText = string.Join("\n", originalLines);
            if (originalLineRemovals.Count > 0 || programInsertions.Count > 0
                || fragmentInsertions.Count > 0
                || programHeadInsertions.Count > 0)
            {
                BuildVariantOriginal(originalLines);
                return;
            }

            original = Shader.Find(ShippedOriginalShaderName);
            if (original == null)
            {
                throw new InvalidOperationException(
                    "The shipped fixture original shader must import.");
            }

            BuildPartner(originalText);
        }

        /// <summary>
        /// The original text for a variant pair: the shipped lines with
        /// the option edits. The variant becomes its own asset, because
        /// the recorded original must resolve as an asset.
        /// </summary>
        private void BuildVariantOriginal(List<string> originalLines)
        {
            var text = ApplyOriginalEdits(originalLines);
            var lines = new List<string>(text.Split('\n'));
            var replaced = false;
            for (var index = 0; index < lines.Count; index++)
            {
                if (lines[index].StartsWith(
                        "Shader \"", StringComparison.Ordinal))
                {
                    lines[index] =
                        "Shader \"Hidden/Alrauna/AmuseTests/" +
                        "LockedSourceFixtureOriginal" + Id + "\"";
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
            {
                throw new InvalidOperationException(
                    "The original text must carry a shader line.");
            }

            var variantText = string.Join("\n", lines);
            EnsureFolders();
            original = TestShaderWriter.WriteTestShader(
                FixtureFolder + "/" + Id + "/Original.shader",
                variantText + "\n");
            BuildPartner(variantText);
        }

        private void BuildPartner(string originalText)
        {
            EnsureFolders();
            var lines = EmulateLockEdits(originalText);
            lines = ApplyGeneratedLineEdits(lines);
            partner = TestShaderWriter.WriteTestShader(
                FixtureFolder + "/" + Id + "/Generated.shader",
                string.Join("\n", lines) + "\n");
        }

        private static List<string> ShippedOriginalLines()
        {
            var shipped = Shader.Find(ShippedOriginalShaderName);
            if (shipped == null)
            {
                throw new InvalidOperationException(
                    "The shipped fixture original shader must import.");
            }

            var assetPath = AssetDatabase.GetAssetPath(shipped);
            var text = System.IO.File.ReadAllText(
                assetPath, System.Text.Encoding.UTF8);
            return new List<string>(text.Replace("\r", string.Empty)
                .Split('\n'));
        }

        private string ApplyOriginalEdits(List<string> lines)
        {
            var result = new List<string>(lines);
            foreach (var prefix in originalLineRemovals)
            {
                for (var index = 0; index < result.Count; index++)
                {
                    if (result[index].TrimStart()
                        .StartsWith(prefix, StringComparison.Ordinal))
                    {
                        result.RemoveAt(index);
                        break;
                    }
                }
            }

            var headInserted = false;
            for (var index = 0; index < result.Count; index++)
            {
                if (result[index].Trim() == "ENDCG")
                {
                    // The fragment lines land inside the function whose
                    // closing brace sits right before the end marker, so
                    // they must insert before the program lines.
                    if (index > 0 && fragmentInsertions.Count > 0)
                    {
                        result.InsertRange(
                            index - 1, fragmentInsertions);
                        index += fragmentInsertions.Count;
                    }

                    result.InsertRange(index, programInsertions);
                    break;
                }

                if (!headInserted &&
                    result[index].Trim() == "CGPROGRAM" &&
                    programHeadInsertions.Count > 0)
                {
                    result.InsertRange(
                        index + 1, programHeadInsertions);
                    index += programHeadInsertions.Count;
                    headInserted = true;
                }
            }

            return string.Join("\n", result);
        }

        /// <summary>
        /// Emulates the closed list of vendor lock edits on the original
        /// text (facts 1 to 8, 12, and 13): the locked shader name, the
        /// dropped comment, blank, and shader feature pragma lines, the
        /// lock button default, the bake replacements, the define runs,
        /// and the rename.
        /// </summary>
        private List<string> EmulateLockEdits(string originalText)
        {
            var sourceLines = originalText.Replace(
                "\r", string.Empty).Split('\n');
            var result = new List<string>(sourceLines.Length);
            var inProgram = false;
            foreach (var rawLine in sourceLines)
            {
                if (rawLine.Trim() == "CGPROGRAM" ||
                    rawLine.Trim() == "CGINCLUDE")
                {
                    inProgram = true;
                    result.Add(rawLine);
                    foreach (var define in addedDefines)
                    {
                        result.Add(" #define " + define + " ");
                    }

                    result.Add(OptimizerDefineLine);
                    continue;
                }

                if (rawLine.Trim() == "ENDCG")
                {
                    inProgram = false;
                    result.Add(rawLine);
                    continue;
                }

                var line = ApplyBakes(rawLine, inProgram);
                var trimmed = line.Trim();
                if (trimmed.Length == 0 ||
                    trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith(
                        "#pragma shader_feature", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("Shader \"", StringComparison.Ordinal))
                {
                    result.Add(
                        "Shader \"Hidden/Locked/" +
                        ShaderNameOfText(originalText) + "/" + Id + "\"");
                    continue;
                }

                if (line.Contains(LockButtonAttribute))
                {
                    // The lock button default becomes 1 (fact 13).
                    var equals = line.LastIndexOf('=');
                    result.Add(line.Substring(0, equals + 1) + " 1");
                    continue;
                }

                result.Add(line);
            }

            if (renameSuffix != null)
            {
                return RenameLines(result, renameSuffix);
            }

            return result;
        }

        /// <summary>
        /// The declared name of the shader line of one text, between the
        /// first and the last quote of that line.
        /// </summary>
        private static string ShaderNameOfText(string text)
        {
            foreach (var line in text.Split('\n'))
            {
                if (!line.StartsWith("Shader \"", StringComparison.Ordinal))
                {
                    continue;
                }

                var open = line.IndexOf('"');
                var close = line.LastIndexOf('"');
                return line.Substring(
                    open + 1, close - open - 1);
            }

            throw new InvalidOperationException(
                "The original text must carry a shader line.");
        }

        /// <summary>
        /// The bake of one property. The replacement runs on program lines
        /// only, and a declaration keeps its identifier (fact 4). A
        /// declaration is a line whose first word is a type word and whose
        /// second word is the baked name.
        /// </summary>
        private string ApplyBakes(string line, bool inProgram)
        {
            if (!inProgram || bakes.Count == 0)
            {
                return line;
            }

            foreach (var bake in bakes)
            {
                if (IsDeclarationOf(line, bake.Key))
                {
                    continue;
                }

                line = ReplaceWord(line, bake.Key, bake.Value);
            }

            return line;
        }

        private static bool IsDeclarationOf(string line, string name)
        {
            var trimmed = line.Trim();
            if (!trimmed.EndsWith(";", StringComparison.Ordinal))
            {
                return false;
            }

            var words = trimmed.Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 2 &&
                TypeWords.Contains(words[0]) &&
                words[1].TrimEnd(';') == name;
        }

        /// <summary>
        /// The rename of every kept line (fact 7). The derived texture
        /// names ride behind the suffix before their own tail, and a name
        /// renames only when no word character follows it.
        /// </summary>
        private static List<string> RenameLines(
            List<string> lines, string suffix)
        {
            var names = DeclaredNames(lines);
            var renamed = new List<string>(lines.Count);
            foreach (var line in lines)
            {
                var edited = line;
                foreach (var name in names)
                {
                    edited = ReplaceWord(
                        edited,
                        name + "_TexelSize",
                        name + "_" + suffix + "_TexelSize");
                    edited = ReplaceWord(
                        edited,
                        name + "_ST",
                        name + "_" + suffix + "_ST");
                }

                foreach (var name in names)
                {
                    edited = ReplaceWord(
                        edited, name, name + "_" + suffix);
                }

                renamed.Add(edited);
            }

            return renamed;
        }

        /// <summary>
        /// The declared property names of the original text, longest
        /// first, so a prefix family renames without damage. Attribute
        /// groups before the name are skipped.
        /// </summary>
        private static List<string> DeclaredNames(List<string> lines)
        {
            var names = new List<string>();
            foreach (var line in lines)
            {
                var trimmed = line.TrimStart();
                while (trimmed.StartsWith("[", StringComparison.Ordinal))
                {
                    var close = trimmed.IndexOf(']');
                    if (close < 0)
                    {
                        break;
                    }

                    trimmed = trimmed.Substring(close + 1).TrimStart();
                }

                if (!trimmed.StartsWith("_", StringComparison.Ordinal))
                {
                    continue;
                }

                var open = trimmed.IndexOf('(');
                if (open < 0)
                {
                    continue;
                }

                var candidate = trimmed.Substring(0, open).Trim();
                if (candidate.Length > 1 &&
                    candidate.IndexOf(' ') < 0 &&
                    !names.Contains(candidate) &&
                    // The optimizer flag never renames (fact 7). Only a
                    // property with the Animated tag 2 renames, and the
                    // lock button property is the vendor's own unlock
                    // handle, read under its plain name after the lock.
                    candidate !=
                    LockedMaterialIdentity.OptimizerEnabledPropertyName)
                {
                    names.Add(candidate);
                }
            }

            names.Sort((left, right) => right.Length.CompareTo(left.Length));
            return names;
        }

        /// <summary>
        /// Replaces every whole-word occurrence of one identifier. Both
        /// neighbors must sit outside the word characters, the way the
        /// vendor occurrence rule bounds a candidate (fact 4).
        /// </summary>
        private static string ReplaceWord(
            string text, string name, string replacement)
        {
            if (!text.Contains(name))
            {
                return text;
            }

            var builder = new System.Text.StringBuilder(text.Length);
            var copied = 0;
            var index = text.IndexOf(name, StringComparison.Ordinal);
            while (index >= 0)
            {
                var end = index + name.Length;
                var leftOk = index == 0 || !IsWordChar(text[index - 1]);
                var rightOk = end >= text.Length || !IsWordChar(text[end]);
                if (leftOk && rightOk)
                {
                    builder.Append(text, copied, index - copied);
                    builder.Append(replacement);
                    copied = end;
                }

                index = text.IndexOf(name, end, StringComparison.Ordinal);
            }

            builder.Append(text, copied, text.Length - copied);
            return builder.ToString();
        }

        private static bool IsWordChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private List<string> ApplyGeneratedLineEdits(List<string> lines)
        {
            foreach (var edit in generatedLineEdits)
            {
                for (var index = 0; index < lines.Count; index++)
                {
                    if (lines[index].TrimStart().StartsWith(
                            edit.Key, StringComparison.Ordinal))
                    {
                        lines[index] = edit.Value;
                        break;
                    }
                }
            }

            return lines;
        }

        private void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(FixtureFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_LockedSource");
            }

            var subFolder = FixtureFolder + "/" + Id;
            if (!AssetDatabase.IsValidFolder(subFolder))
            {
                AssetDatabase.CreateFolder(FixtureFolder, Id);
            }
        }

        private string Signature()
        {
            var bakeTexts = new List<string>();
            foreach (var bake in bakes)
            {
                bakeTexts.Add(bake.Key + "=" + bake.Value);
            }

            var editTexts = new List<string>();
            foreach (var edit in generatedLineEdits)
            {
                editTexts.Add(edit.Key + ">" + edit.Value);
            }

            var parts = new List<string>
            {
                "removals:" + string.Join("|", originalLineRemovals),
                "program:" + string.Join("|", programInsertions),
                "fragment:" + string.Join("|", fragmentInsertions),
                "head:" + string.Join("|", programHeadInsertions),
                "suffix:" + renameSuffix,
                "bakes:" + string.Join("|", bakeTexts),
                "defines:" + string.Join("|", addedDefines),
                "edits:" + string.Join("|", editTexts),
            };
            return string.Join(";", parts);
        }

        private static string Fnv1a(string text)
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var value in text)
                {
                    hash = (hash ^ value) * 16777619u;
                }

                return hash.ToString("X", CultureInfo.InvariantCulture);
            }
        }
    }
}
