using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Semantics.LilToon
{
    /// <summary>
    /// The attested lilToon include directory: its root, the digested file
    /// listing, and the one question R3 needs answered — does this resolved
    /// absolute path name a file inside the tree, and where inside it?
    /// Answering by identity rather than by basename is what stops a redirected
    /// include from canonicalizing to a trusted one.
    /// </summary>
    internal sealed class LilToonIncludeTree
    {
        private readonly Dictionary<string, string> _byFullPath;

        internal string RootFullPath { get; }
        internal IReadOnlyList<(string RelativePath, string Hash)> Files { get; }

        private LilToonIncludeTree(
            string rootFullPath,
            IReadOnlyList<(string RelativePath, string Hash)> files,
            Dictionary<string, string> byFullPath)
        {
            RootFullPath = rootFullPath;
            Files = files;
            _byFullPath = byFullPath;
        }

        internal static LilToonIncludeTree Empty()
        {
            return new LilToonIncludeTree(
                null,
                new (string, string)[0],
                new Dictionary<string, string>(PathComparer));
        }

        /// <summary>
        /// Enumerates an already-absolute include directory. A missing or
        /// unreadable directory yields an empty tree, which downstream becomes
        /// missing source evidence rather than silent acceptance.
        /// </summary>
        internal static LilToonIncludeTree Enumerate(
            string includeFolderFullPath,
            Func<string, string> readTextOrNull,
            Func<string, string> hash)
        {
            if (string.IsNullOrEmpty(includeFolderFullPath) ||
                !Path.IsPathRooted(includeFolderFullPath))
            {
                return Empty();
            }

            string root;
            string[] paths;
            try
            {
                if (!Directory.Exists(includeFolderFullPath))
                {
                    return Empty();
                }

                root = Path.GetFullPath(includeFolderFullPath);
                paths = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return Empty();
            }
            catch (UnauthorizedAccessException)
            {
                return Empty();
            }

            var files = new List<(string, string)>();
            var byFullPath = new Dictionary<string, string>(PathComparer);

            foreach (var path in paths)
            {
                if (path.EndsWith(".meta", StringComparison.Ordinal))
                {
                    continue;
                }

                var text = readTextOrNull(path);
                if (text == null)
                {
                    // An unreadable member makes the tree unusable rather than
                    // silently smaller: refuse instead of digesting a subset.
                    return Empty();
                }

                var full = Path.GetFullPath(path);
                var relative = full
                    .Substring(root.Length)
                    .TrimStart(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar)
                    .Replace(Path.DirectorySeparatorChar, '/');

                files.Add((relative, hash(text)));
                byFullPath[full] = relative;
            }

            return new LilToonIncludeTree(root, files, byFullPath);
        }

        /// <summary>Test seam: build a tree without touching the file system.</summary>
        internal static LilToonIncludeTree ForTests(
            string rootPath,
            IReadOnlyList<(string RelativePath, string Hash)> files)
        {
            var root = Path.GetFullPath(rootPath);
            var byFullPath = new Dictionary<string, string>(PathComparer);
            foreach (var file in files)
            {
                byFullPath[Path.GetFullPath(Path.Combine(root, file.RelativePath))] =
                    file.RelativePath;
            }

            return new LilToonIncludeTree(root, files, byFullPath);
        }

        internal bool TryGetRelativePath(string fullPath, out string relativePath)
        {
            relativePath = null;
            return fullPath != null &&
                   _byFullPath.TryGetValue(fullPath, out relativePath);
        }

        // Exact ordinal identity. A casing difference therefore fails to resolve
        // and the line stays unnormalized, so the digest refuses even on a
        // case-insensitive filesystem where both paths name one file. That false
        // negative is deliberate: case-insensitive matching would let
        // Includes/LIL_COMMON.HLSL assume the identity of an attested
        // Includes/lil_common.hlsl. No filesystem detection is added.
        private static StringComparer PathComparer => StringComparer.Ordinal;
    }

    /// <summary>
    /// Already-read lilToon identity evidence. Separating extraction from the
    /// identity decision keeps the conjunction deterministically testable
    /// without a live Unity asset or an installed lilToon package.
    /// </summary>
    internal sealed class LilToonSourceEvidence
    {
        internal string ShaderName { get; }
        internal string AssetGuid { get; }
        internal bool HasShaderFormatVersion { get; }
        internal float ShaderFormatVersion { get; }
        internal bool HasPackage { get; }
        internal string PackageName { get; }
        internal string PackageVersion { get; }
        internal string PassShaderGuid { get; }
        internal string ShaderCanonicalDigest { get; }
        internal string PassCanonicalDigest { get; }
        internal string IncludeTreeDigest { get; }
        internal bool HasRenderMode { get; }
        internal int RenderMode { get; }
        internal IReadOnlyCollection<string> CompiledFeatures { get; }

        internal LilToonSourceEvidence(
            string shaderName,
            string assetGuid,
            bool hasShaderFormatVersion,
            float shaderFormatVersion,
            bool hasPackage,
            string packageName,
            string packageVersion,
            string passShaderGuid,
            string shaderCanonicalDigest,
            string passCanonicalDigest,
            string includeTreeDigest,
            bool hasRenderMode,
            int renderMode,
            IReadOnlyCollection<string> compiledFeatures)
        {
            ShaderName = shaderName;
            AssetGuid = assetGuid;
            HasShaderFormatVersion = hasShaderFormatVersion;
            ShaderFormatVersion = shaderFormatVersion;
            HasPackage = hasPackage;
            PackageName = packageName;
            PackageVersion = packageVersion;
            PassShaderGuid = passShaderGuid;
            ShaderCanonicalDigest = shaderCanonicalDigest;
            PassCanonicalDigest = passCanonicalDigest;
            IncludeTreeDigest = includeTreeDigest;
            HasRenderMode = hasRenderMode;
            RenderMode = renderMode;
            CompiledFeatures = compiledFeatures
                ?? throw new ArgumentNullException(nameof(compiledFeatures));
        }
    }

    /// <summary>
    /// Attestation for lilToon 2.3.4. lilToon regenerates its shader assets from
    /// per-project settings, so a whole-file hash would refuse legitimate
    /// installs. Instead the two generated assets are hashed after
    /// canonicalizing exactly the regions the generator is proven to vary, the
    /// whole include directory is digested, and the render mode is read from the
    /// live pass rather than inferred from the asset's name.
    /// </summary>
    internal static class LilToonSourceAttestation
    {
        internal const string SupportedShaderName = "lilToon";
        internal const string SupportedShaderGuid =
            "df12117ecd77c31469c224178886498e";
        internal const string PassShaderName = "Hidden/ltspass_opaque";
        internal const string PassShaderGuid =
            "61b4f98a5d78b4a4a9d89180fac793fc";
        internal const string PackageName = "jp.lilxyzw.liltoon";
        internal const string PackageVersion = "2.3.4";
        internal const float ShaderFormatVersion = 45f;
        internal const int OpaqueRenderMode = 0;

        // Measured by Task 0 on 2026-08-18 from a scratch
        // jp.lilxyzw.liltoon@2.3.4 install and cross-checked between default and
        // stripped shader settings. Never re-derive these from the lilToon
        // repository, whose committed generated shaders are stale relative to
        // their own tag's generator.
        internal const string ShaderCanonicalDigest =
            "5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704";
        internal const string PassCanonicalDigest =
            "6b6c30c1cbe546fe753bcdc77f547441e3f9114ee80e9591bde2b8e6e7e5eb14";
        internal const string IncludeTreeDigest =
            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46";

        internal const string ShaderFormatVersionProperty = "_lilToonVersion";
        private const string IncludeFolderName = "Includes";

        // D1: a valueless define the *LIL_SHADER_SETTING* substitution can emit.
        // A define with a value, such as LIL_RENDER 0, never matches.
        private static readonly Regex SettingDefine = new Regex(
            @"^#define\s+(?:LIL_FEATURE_\w+|LIL_OPTIMIZE_\w+|LIL_INPUT_OPTIMIZED)\s*$",
            RegexOptions.Compiled);

        // D2: variant stripping, emitted by the setting substitution and by the
        // lil_skip_variants_* markers.
        private static readonly Regex SkipVariants = new Regex(
            @"^#pragma\s+skip_variants\s+\S",
            RegexOptions.Compiled);

        // R2 anchor: the fixed terminal line of the BRP lil_multi_compile_forward
        // expansion. The template places lil_skip_variants_{base,outline}_shadows
        // immediately after it, so this line uniquely locates that slot.
        private const string ShadowSlotAnchor = "#define LIL_PASS_FORWARD";

        // R2 keyword domain. GetSkipVariantsShadows() is a fixed literal ending
        // in SHADOW_VERY_HIGH, and UnpackContainer's dedup pass rewrites a
        // surviving skip_variants line to its final keyword alone, so this is
        // the entire set the generator can produce at the slot. A closed
        // literal, not a pattern; do not widen it into a variant system.
        private const string ShadowSlotKeyword = "SHADOW_VERY_HIGH";

        private static readonly Regex SingleKeywordSkipVariants = new Regex(
            @"^#pragma\s+skip_variants\s+(?<keyword>\w+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex FeatureDefine = new Regex(
            @"^#define\s+(LIL_FEATURE_\w+)\s*$",
            RegexOptions.Compiled);

        private static readonly Regex RenderDefine = new Regex(
            @"^#define\s+LIL_RENDER\s+(\S+)\s*$",
            RegexOptions.Compiled);

        // R3 matches only a whole-line include directive, live or commented. A
        // quoted string anywhere else is never rewritten.
        private static readonly Regex IncludeDirective = new Regex(
            "^(?<lead>\\s*(?://)?#include\\s+\")(?<path>[^\"]*)(?<tail>\"\\s*)$",
            RegexOptions.Compiled);

        /// <summary>
        /// Normalizes shader source (drop an optional leading UTF-8 BOM, then
        /// convert CRLF and lone CR to LF) and returns the lowercase-hex SHA-256
        /// of its UTF-8 bytes. The rule matches the Poiyomi frontend exactly.
        /// </summary>
        internal static string ComputeNormalizedSourceHash(string rawSource)
        {
            if (rawSource == null)
            {
                throw new ArgumentNullException(nameof(rawSource));
            }

            return Sha256(Normalize(rawSource));
        }

        private static string Normalize(string rawSource)
        {
            if (rawSource.Length > 0 && rawSource[0] == '﻿')
            {
                rawSource = rawSource.Substring(1);
            }

            return rawSource.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static string Sha256(string text)
        {
            var bytes = new UTF8Encoding(false).GetBytes(text);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Removes exactly the text lilToon's generator is proven to vary, so
        /// the remainder can be hashed against a pin. R1 drops the
        /// setting-substituted feature block inside an HLSLINCLUDE run; R2 drops
        /// the shadow skip-variant expansion at its one substitution slot; R3
        /// normalizes an include path only when it provably resolves into the
        /// attested tree. Everything else — pass bodies, tags, blend state,
        /// other pragmas, other includes, blank lines, and every valued define —
        /// is retained, so any hand edit or custom-shader injection changes the
        /// digest.
        /// </summary>
        internal static string Canonicalize(
            string rawShaderSource,
            string shaderDirectory,
            string projectRoot,
            LilToonIncludeTree includeTree)
        {
            if (rawShaderSource == null)
            {
                throw new ArgumentNullException(nameof(rawShaderSource));
            }
            if (includeTree == null)
            {
                throw new ArgumentNullException(nameof(includeTree));
            }

            var lines = Normalize(rawShaderSource).Split('\n');

            // Mark the setting region before emitting, so a same-shaped line
            // outside it can never be dropped.
            var inSettingRegion = new bool[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                // Region A: after HLSLINCLUDE, the maximal run of D1/D2 lines. A
                // blank line does not extend the run, and a valued define ends
                // it immediately — which is why the Shader-scope block holding
                // `#define LIL_RENDER 0` has an empty region A.
                if (!string.Equals(
                        lines[i].Trim(), "HLSLINCLUDE", StringComparison.Ordinal))
                {
                    continue;
                }

                for (var j = i + 1; j < lines.Length; j++)
                {
                    var candidate = lines[j].Trim();
                    if (!SettingDefine.IsMatch(candidate) &&
                        !SkipVariants.IsMatch(candidate))
                    {
                        break;
                    }

                    inSettingRegion[j] = true;
                }
            }

            var builder = new StringBuilder(rawShaderSource.Length);
            var first = true;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.Trim();

                if (inSettingRegion[i] &&
                    (SettingDefine.IsMatch(trimmed) || SkipVariants.IsMatch(trimmed)))
                {
                    continue;
                }

                if (IsShadowSlotExpansion(lines, i, trimmed))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append('\n');
                }

                first = false;
                builder.Append(
                    NormalizeIncludeLine(
                        line, shaderDirectory, projectRoot, includeTree));
            }

            return builder.ToString();
        }

        /// <summary>
        /// R2. All three conditions are required: the anchor line directly
        /// above, a single-keyword skip_variants directive, and the one keyword
        /// the generator can produce at this slot. An unrelated keyword after
        /// the correct anchor stays hashed. The anchor is read from the raw line
        /// array, so an earlier removal can never shift it.
        /// </summary>
        private static bool IsShadowSlotExpansion(
            string[] lines,
            int index,
            string trimmed)
        {
            if (index == 0 ||
                !string.Equals(
                    lines[index - 1].Trim(),
                    ShadowSlotAnchor,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var slot = SingleKeywordSkipVariants.Match(trimmed);
            return slot.Success &&
                   string.Equals(
                       slot.Groups["keyword"].Value,
                       ShadowSlotKeyword,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// R3. Rewrites an include directive only when its path is proven to
        /// resolve to a file inside the attested include tree, and preserves the
        /// file's path relative to that tree. A path that resolves outside the
        /// tree, resolves to nothing, or resolves ambiguously is returned
        /// byte-identical, so it contributes its original text to the digest and
        /// the material refuses.
        /// </summary>
        private static string NormalizeIncludeLine(
            string line,
            string shaderDirectory,
            string projectRoot,
            LilToonIncludeTree includeTree)
        {
            var match = IncludeDirective.Match(line);
            if (!match.Success)
            {
                return line;
            }

            var path = match.Groups["path"].Value;
            string resolved = null;

            // The project root is supplied explicitly. Resolving against "."
            // would couple attestation to the Editor process's working
            // directory, which is not a property of the shader being attested.
            foreach (var candidate in new[]
                     {
                         CombineFullPath(shaderDirectory, path),
                         CombineFullPath(projectRoot, path),
                     })
            {
                if (candidate == null ||
                    !includeTree.TryGetRelativePath(candidate, out var relative))
                {
                    continue;
                }

                if (resolved != null &&
                    !string.Equals(resolved, relative, StringComparison.Ordinal))
                {
                    // Ambiguous: two readings land on different attested files.
                    return line;
                }

                resolved = relative;
            }

            return resolved == null
                ? line
                : match.Groups["lead"].Value +
                  IncludeFolderName + "/" + resolved +
                  match.Groups["tail"].Value;
        }

        private static string CombineFullPath(string baseDirectory, string path)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(baseDirectory))
            {
                return null;
            }

            try
            {
                return Path.GetFullPath(Path.Combine(baseDirectory, path));
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
            catch (PathTooLongException)
            {
                return null;
            }
        }

        /// <summary>
        /// Digests the whole include directory listing. Enumerating the
        /// directory rather than a reachability-derived file list keeps include
        /// closure analysis out of the trusted computing base and also detects
        /// added files.
        /// </summary>
        internal static string ComputeIncludeTreeDigest(
            IReadOnlyList<(string RelativePath, string Hash)> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            var rows = new List<string>(files.Count);
            foreach (var file in files)
            {
                rows.Add(file.RelativePath + ":" + file.Hash);
            }

            rows.Sort(StringComparer.Ordinal);
            return Sha256(string.Join("\n", rows));
        }

        /// <summary>
        /// Collects the valueless <c>LIL_FEATURE_*</c> symbols the resolved pass
        /// defines. A literal line scan over a closed prefix: no conditional
        /// evaluation, no macro expansion, no HLSL grammar. lilToon's setting can
        /// strip a feature while its material property stays set, so an output
        /// that claims such a feature must see it here or stay Unknown.
        /// </summary>
        internal static IReadOnlyCollection<string> ScanCompiledFeatures(
            string passShaderSource)
        {
            var features = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(passShaderSource))
            {
                return features;
            }

            foreach (var rawLine in Normalize(passShaderSource).Split('\n'))
            {
                var match = FeatureDefine.Match(rawLine.Trim());
                if (match.Success)
                {
                    features.Add(match.Groups[1].Value);
                }
            }

            return features;
        }

        /// <summary>
        /// Reads the render mode the resolved pass currently declares. Requires
        /// exactly one <c>#define LIL_RENDER &lt;int&gt;</c>; zero, several, or a
        /// non-integer value cannot establish the fact.
        /// </summary>
        internal static bool TryScanRenderMode(
            string passShaderSource,
            out int renderMode)
        {
            renderMode = 0;
            if (string.IsNullOrEmpty(passShaderSource))
            {
                return false;
            }

            var found = false;
            foreach (var rawLine in Normalize(passShaderSource).Split('\n'))
            {
                var match = RenderDefine.Match(rawLine.Trim());
                if (!match.Success)
                {
                    continue;
                }

                if (found)
                {
                    return false;
                }

                if (!int.TryParse(
                        match.Groups[1].Value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out renderMode))
                {
                    return false;
                }

                found = true;
            }

            return found;
        }

        internal static bool TryVerifyLilToonIdentity(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            // 1. Shader identity. There is no family table: one supported
            //    shader, everything else refused.
            if (!string.Equals(
                    evidence.ShaderName,
                    SupportedShaderName,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedShader,
                    $"shader name '{evidence.ShaderName}'");
                return false;
            }

            if (!string.Equals(
                    evidence.AssetGuid,
                    SupportedShaderGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedShader,
                    "shader asset GUID");
                return false;
            }

            // 2. Material shader-format stamp, compared exactly. A malformed or
            //    nearby value must never be normalized into the supported one.
            if (!evidence.HasShaderFormatVersion ||
                float.IsNaN(evidence.ShaderFormatVersion) ||
                float.IsInfinity(evidence.ShaderFormatVersion) ||
                evidence.ShaderFormatVersion != ShaderFormatVersion)
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedVersion,
                    ShaderFormatVersionProperty);
                return false;
            }

            // 3. Package identity, when installed as a package.
            if (evidence.HasPackage)
            {
                if (!string.Equals(
                        evidence.PackageName, PackageName, StringComparison.Ordinal))
                {
                    diagnostic = MaterialDiagnostic(
                        LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                        $"package name '{evidence.PackageName}'");
                    return false;
                }

                if (!string.Equals(
                        evidence.PackageVersion,
                        PackageVersion,
                        StringComparison.Ordinal))
                {
                    diagnostic = MaterialDiagnostic(
                        LilToonSemanticDiagnosticCode.UnsupportedVersion,
                        $"package version '{evidence.PackageVersion}'");
                    return false;
                }
            }

            // 4. Resolved pass asset.
            if (!string.Equals(
                    evidence.PassShaderGuid,
                    PassShaderGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                    PassShaderName);
                return false;
            }

            // 5. Source digests.
            if (!TryMatchDigest(
                    evidence.IncludeTreeDigest,
                    IncludeTreeDigest,
                    IncludeFolderName,
                    out diagnostic) ||
                !TryMatchDigest(
                    evidence.ShaderCanonicalDigest,
                    ShaderCanonicalDigest,
                    SupportedShaderName,
                    out diagnostic) ||
                !TryMatchDigest(
                    evidence.PassCanonicalDigest,
                    PassCanonicalDigest,
                    PassShaderName,
                    out diagnostic))
            {
                return false;
            }

            // 6. Render mode as the current pass declares it, not as the pass
            //    asset's historical name implies.
            if (!evidence.HasRenderMode || evidence.RenderMode != OpaqueRenderMode)
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant,
                    evidence.HasRenderMode
                        ? $"LIL_RENDER {evidence.RenderMode}"
                        : "LIL_RENDER unreadable");
                return false;
            }

            diagnostic = null;
            return true;
        }

        private static bool TryMatchDigest(
            string actual,
            string expected,
            string detail,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (string.IsNullOrEmpty(actual))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                    detail);
                return false;
            }

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.ModifiedShaderSource,
                    detail);
                return false;
            }

            diagnostic = null;
            return true;
        }

        /// <summary>
        /// Reads identity evidence from a live material. Every filesystem access
        /// resolves through an explicit project root derived from
        /// <see cref="Application.dataPath"/>; nothing relies on the process
        /// working directory. Unreadable evidence is omitted rather than
        /// guessed, so the conjunction refuses.
        /// </summary>
        internal static LilToonSourceEvidence GatherSourceEvidence(Material material)
        {
            var shader = material.shader;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                shader, out var assetGuid, out long _);

            var hasVersion = material.HasProperty(ShaderFormatVersionProperty);
            var version = hasVersion
                ? material.GetFloat(ShaderFormatVersionProperty)
                : float.NaN;

            // Project-relative: Unity asset APIs consume this form directly.
            var shaderAssetPath = AssetDatabase.GetAssetPath(shader);
            var package = UnityEditor.PackageManager.PackageInfo
                .FindForAssetPath(shaderAssetPath);

            // Absolute: every System.IO call below uses this form only.
            var projectRoot = TryGetProjectRoot();
            var shaderFullPath = ToAbsolute(projectRoot, shaderAssetPath);
            var shaderDirectory = shaderFullPath == null
                ? null
                : Path.GetDirectoryName(shaderFullPath);
            var includeFolder = shaderDirectory == null
                ? null
                : Path.Combine(shaderDirectory, IncludeFolderName);

            var includeTree = LilToonIncludeTree.Enumerate(
                includeFolder, ReadTextOrNull, ComputeNormalizedSourceHash);

            var includeDigest = includeTree.Files.Count == 0
                ? null
                : ComputeIncludeTreeDigest(includeTree.Files);

            var shaderText = ReadTextOrNull(shaderFullPath);
            var shaderDigest = shaderText == null
                ? null
                : Sha256(Canonicalize(
                    shaderText, shaderDirectory, projectRoot, includeTree));

            var passShader = Shader.Find(PassShaderName);
            string passGuid = null;
            string passDigest = null;
            string passText = null;
            if (passShader != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    passShader, out passGuid, out long _);

                var passFullPath = ToAbsolute(
                    projectRoot, AssetDatabase.GetAssetPath(passShader));
                passText = ReadTextOrNull(passFullPath);
                if (passText != null)
                {
                    // The pass resolves its own includes relative to its own
                    // directory, which need not be the material shader's.
                    passDigest = Sha256(Canonicalize(
                        passText,
                        Path.GetDirectoryName(passFullPath),
                        projectRoot,
                        includeTree));
                }
            }

            var hasRenderMode = TryScanRenderMode(passText, out var renderMode);

            return new LilToonSourceEvidence(
                shader.name,
                assetGuid?.ToLowerInvariant(),
                hasVersion,
                version,
                package != null,
                package?.name,
                package?.version,
                passGuid?.ToLowerInvariant(),
                shaderDigest,
                passDigest,
                includeDigest,
                hasRenderMode,
                renderMode,
                ScanCompiledFeatures(passText));
        }

        /// <summary>
        /// The Unity project root: the parent of <c>Application.dataPath</c>.
        /// Never the process working directory.
        /// </summary>
        private static string TryGetProjectRoot()
        {
            try
            {
                return Directory.GetParent(Application.dataPath)?.FullName;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves a project-relative Unity asset path to an absolute path.
        /// Returns null when either part is missing, so callers fail closed.
        /// </summary>
        private static string ToAbsolute(string projectRoot, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            return Path.IsPathRooted(assetPath)
                ? CombineFullPath(Path.GetPathRoot(assetPath), assetPath)
                : CombineFullPath(projectRoot, assetPath);
        }

        private static string ReadTextOrNull(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath) || !Path.IsPathRooted(fullPath))
            {
                return null;
            }

            try
            {
                return File.Exists(fullPath)
                    ? File.ReadAllText(fullPath, Encoding.UTF8)
                    : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static LilToonSemanticDiagnostic MaterialDiagnostic(
            LilToonSemanticDiagnosticCode code,
            string detail)
        {
            return new LilToonSemanticDiagnostic(
                LilToonSemanticOutput.Material, code, detail);
        }
    }
}
