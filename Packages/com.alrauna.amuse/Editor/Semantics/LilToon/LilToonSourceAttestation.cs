using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Editor.Host;
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

        internal IReadOnlyList<(string RelativePath, string Hash)> Files { get; }

        private LilToonIncludeTree(
            IReadOnlyList<(string RelativePath, string Hash)> files,
            Dictionary<string, string> byFullPath)
        {
            Files = files;
            _byFullPath = byFullPath;
        }

        internal static LilToonIncludeTree Empty()
        {
            return new LilToonIncludeTree(
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

            return new LilToonIncludeTree(files, byFullPath);
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

            return new LilToonIncludeTree(files, byFullPath);
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
        internal LilToonCanonicalizationAnalysis ShaderCanonicalization { get; }
        internal LilToonCanonicalizationAnalysis PassCanonicalization { get; }

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
            IReadOnlyCollection<string> compiledFeatures,
            LilToonCanonicalizationAnalysis shaderCanonicalization,
            LilToonCanonicalizationAnalysis passCanonicalization)
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
            if (compiledFeatures == null)
            {
                throw new ArgumentNullException(nameof(compiledFeatures));
            }

            CompiledFeatures = new ReadOnlyCollection<string>(
                new List<string>(compiledFeatures));
            ShaderCanonicalization = shaderCanonicalization;
            PassCanonicalization = passCanonicalization;
        }
    }

    /// <summary>
    /// One Multi container row presented to the verify conjunction: the
    /// container shader name, its asset GUID, and the canonical source
    /// digest the resolution point bound to the identified version. The
    /// production resolver builds the row from the admitted Multi table;
    /// the test seam injects its own row, which is why the digest is a
    /// constructor argument and not a table lookup. A row carries no
    /// render-mode field. The mode half of a Multi material is decided by
    /// the keyword-derivation mode-consistency gate, never by a LIL_RENDER
    /// scan. A Multi container also declares no pass asset, so no pass
    /// identity exists to pin.
    /// </summary>
    internal sealed class LilToonMultiContainerProfile
    {
        internal LilToonMultiContainerProfile(
            string shaderName,
            string shaderGuid,
            string shaderCanonicalDigest)
        {
            ShaderName = shaderName;
            ShaderGuid = shaderGuid;
            ShaderCanonicalDigest = shaderCanonicalDigest;
        }

        internal string ShaderName { get; }
        internal string ShaderGuid { get; }
        internal string ShaderCanonicalDigest { get; }
    }

    internal enum LilToonRemovedRecordKind
    {
        Define,
        SkipVariants,
    }

    internal readonly struct LilToonRemovedRecord
    {
        internal int LineIndex { get; }
        internal int OffsetInRegion { get; }
        internal LilToonRemovedRecordKind Kind { get; }
        internal string Text { get; }

        internal LilToonRemovedRecord(
            int lineIndex,
            int offsetInRegion,
            LilToonRemovedRecordKind kind,
            string text)
        {
            LineIndex = lineIndex;
            OffsetInRegion = offsetInRegion;
            Kind = kind;
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }

    internal sealed class LilToonRemovedRegion
    {
        internal int HlslIncludeOrdinal { get; }
        internal int HlslIncludeLineIndex { get; }
        internal IReadOnlyList<LilToonRemovedRecord> Records { get; }

        internal LilToonRemovedRegion(
            int hlslIncludeOrdinal,
            int hlslIncludeLineIndex,
            IEnumerable<LilToonRemovedRecord> records)
        {
            if (records == null)
            {
                throw new ArgumentNullException(nameof(records));
            }

            HlslIncludeOrdinal = hlslIncludeOrdinal;
            HlslIncludeLineIndex = hlslIncludeLineIndex;
            Records = new ReadOnlyCollection<LilToonRemovedRecord>(
                new List<LilToonRemovedRecord>(records));
        }
    }

    internal readonly struct LilToonActivatorOccurrence
    {
        internal int LineIndex { get; }
        internal string Identifier { get; }

        /// <summary>
        /// True when the occurrence sits in a slot the generator is proven
        /// to write: inside the verified setting run, or — for the LTCGI
        /// define — directly before the LIL_PASS_FORWARD anchor. An
        /// occurrence outside those slots is a tamper signal.
        /// </summary>
        internal bool ProvenSlot { get; }

        internal LilToonActivatorOccurrence(
            int lineIndex,
            string identifier,
            bool provenSlot)
        {
            LineIndex = lineIndex;
            Identifier = identifier
                ?? throw new ArgumentNullException(nameof(identifier));
            ProvenSlot = provenSlot;
        }
    }

    internal sealed class LilToonCanonicalizationAnalysis
    {
        internal string CanonicalSource { get; }
        internal IReadOnlyList<LilToonRemovedRegion> RemovedRegions { get; }
        internal IReadOnlyList<LilToonActivatorOccurrence> Activators { get; }

        internal LilToonCanonicalizationAnalysis(
            string canonicalSource,
            IEnumerable<LilToonRemovedRegion> removedRegions,
            IEnumerable<LilToonActivatorOccurrence> activators)
        {
            if (removedRegions == null)
            {
                throw new ArgumentNullException(nameof(removedRegions));
            }
            if (activators == null)
            {
                throw new ArgumentNullException(nameof(activators));
            }

            CanonicalSource = canonicalSource
                ?? throw new ArgumentNullException(nameof(canonicalSource));
            RemovedRegions = new ReadOnlyCollection<LilToonRemovedRegion>(
                new List<LilToonRemovedRegion>(removedRegions));
            Activators = new ReadOnlyCollection<LilToonActivatorOccurrence>(
                new List<LilToonActivatorOccurrence>(activators));
        }
    }

    /// <summary>
    /// Attestation for the admitted lilToon versions, 2.3.0 through 2.4.1.
    /// lilToon regenerates its shader assets from
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
        internal const string PackageVersion = "2.4.1"; // newest admitted. The set is AdmittedPackageVersions (S11)
        internal const float ShaderFormatVersion = 45f; // re-confirmed on installed 2.4.0, 2026-10-19
        internal const int OpaqueRenderMode = 0;

        /// <summary>
        /// Owner decision 2026-10-19: every lilToon version from the floor up
        /// to, but excluding, the bound is approved. Approved means the build
        /// never asks and analysis runs under the current pins. On the build
        /// path, capturer admission and the conversion boundary skip the
        /// identity checks for an approved member with unattested bytes. The
        /// attestation itself still fails closed on changed bytes.
        /// </summary>
        internal static readonly string ApprovedVersionFloor = "2.3.0";
        internal static readonly string ApprovedVersionUpperBound = "2.5.0";

        // Measured by Task 0 on 2026-08-18 from a scratch
        // jp.lilxyzw.liltoon@2.3.4 install and cross-checked between default and
        // stripped shader settings. Never re-derive these from the lilToon
        // repository, whose committed generated shaders are stale relative to
        // their own tag's generator.
        internal const string ShaderCanonicalDigest =
            "5206bec25e82db5f8009b27fcc5ba94d7c41113031d4b6b0a2c25ca324a9c704";
        // The pass digest was re-measured on 2026-09-07 under the R4/R5/R6
        // canonicalization from two real shapes - the shipped 2.3.4 VPM bytes
        // (zip sha256 34d172761c51aa9469a904704109086aafa6125a4fa0e058766e2ddc73d3b303)
        // and a regenerated LTCGI plus AudioLink install. The canonical bytes
        // of the two shapes agree on one digest per file.
        internal const string PassCanonicalDigest =
            "aee1ea0c1fd0ae26f561fbade4c23309bb62ed8aff0c9221d1cba7c74a31d9d1";
        // The newest admitted tree digest (2.4.1). Computed from the
        // official 2.4.1 tag tree on 2026-10-20 by the recipe below,
        // licensed by reproducing the 2.4.0 pin from the 2.4.0 tag tree
        // (spec B1). The 2.4.0 digest lives in the AdmittedPackageVersions
        // row literal. The installed-shape status is recorded in the row
        // block below.
        internal const string IncludeTreeDigest =
            "063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625";

        // Admitted package versions and the include-tree digest each ships
        // (S11). Three evidence sources, named per the file's comment
        // discipline: 2.3.0-2.3.4 shader and pass identity rests on the
        // per-tag raw sha256 comparison of the official tag artifacts
        // (inherited from the original measurement); the 2.4.0 shader and
        // pass digests reproduce the pinned constants on two installed 2.4.0
        // shapes (2026-10-19); the three alpha-cited includes are identical
        // between the 2.3.4 tag tree and the installed 2.4.0 shape under
        // the pinned source normalization, which ignores CRLF versus LF
        // line endings only (2026-10-19, licensed by reproducing the pinned
        // 2.3.4 tree digest from that tree). The include tree is the only
        // per-version bytes the identity conjunction checks. 2.3.1-2.3.3
        // ship identical trees and share a row. The 2.4.0 tree differs by
        // the VRC Light Volumes 3 rewrite and the APV sampling refactor.
        // The 2.4.1 row carries the one upstream patch past it. The 2.4.0
        // to 2.4.1 diff changed two changelogs, the package version
        // fields, and Shader/Includes/openlit_core.hlsl. The 2.4.1 row is
        // computed from the official 2.4.1 tag tree by the recipe below.
        // The anchor reproduced the 2.4.0 digest from the 2.4.0 tag tree
        // on 2026-10-20. The production gather from one installed
        // 2.4.1 shape on that day confirmed this include tree digest and
        // held the GUIDs and the `_lilToonVersion` stamp. The 2.4.1 Multi
        // row below records its measured columns from that shape. Still
        // outstanding when a second settings shape is obtainable: the
        // regular shader, pass, and extended family digests, and the
        // opaque-target tuple per the mandate style at
        // Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:26-30.
        // Never re-derive the generated shader and pass digests from the
        // lilToon repository, whose committed generated shaders are stale
        // relative to their own tag's generator. Include-tree rows may
        // cite the official tag tree when the anchor reproduces the
        // previous row's digest.
        private static readonly (string Version, string IncludeDigest)[]
            AdmittedPackageVersions =
            {
                ("2.3.0",
                    "bba3205a08b2bd56b4d2c69b8de3144377ec0b4ca4c44cc9d71b1341e62c94a0"),
                ("2.3.1",
                    "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
                ("2.3.2",
                    "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
                ("2.3.3",
                    "154aa68f85633b643f27a82309dc9d755e1955f0e8de7d21c9f62120c6628416"),
                ("2.3.4",
                    "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46"),
                ("2.4.0",
                    "549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670"),
                (PackageVersion, IncludeTreeDigest),
            };

        /// <summary>
        /// Read-only snapshot of the admitted version rows for first-party
        /// test and research consumers (T1, R1). Returns a copy, so a caller
        /// can never observe or cause in-place mutation.
        /// </summary>
        internal static IReadOnlyList<(string Version, string IncludeDigest)> AdmittedRows()
        {
            return AdmittedPackageVersions.ToArray();
        }

        private static bool IsAdmittedPackageVersion(string packageVersion)
        {
            foreach (var row in AdmittedPackageVersions)
            {
                if (string.Equals(
                        row.Version, packageVersion,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True for a canonical M.m.p version inside the approved range
        /// [floor, bound). The strict parse and the numeric-triple
        /// comparison are the D1 pins. The grammar-equivalence pin holds
        /// the range edges to the host grammar.
        /// </summary>
        internal static bool IsApprovedPackageVersion(string packageVersion)
        {
            if (!TryParseCanonicalVersion(
                    packageVersion, out var components))
            {
                return false;
            }

            return IsWithinApprovedRange(components);
        }

        /// <summary>
        /// The one approval question every routing site asks: a loose
        /// install carries no package version, so HasPackage == false
        /// never approves (spec D1).
        /// </summary>
        internal static bool IsApprovedPackageEvidence(
            bool hasPackage, string packageVersion)
        {
            return hasPackage && IsApprovedPackageVersion(packageVersion);
        }

        // The approved range's parsed boundaries. The string constants are
        // literals, so the parse cannot fail. A failure would throw here
        // and fail every test that touches the class.
        private static readonly (int Major, int Minor, int Patch)
            ApprovedFloorComponents =
                ParseApprovedBoundary(ApprovedVersionFloor);

        private static readonly (int Major, int Minor, int Patch)
            ApprovedBoundComponents =
                ParseApprovedBoundary(ApprovedVersionUpperBound);

        private static (int Major, int Minor, int Patch) ParseApprovedBoundary(
            string boundary)
        {
            if (!TryParseCanonicalVersion(boundary, out var parsed))
            {
                throw new InvalidOperationException(
                    "The approved range boundaries must parse.");
            }

            return parsed;
        }

        /// <summary>
        /// The approved-range question over one parsed version: the
        /// numeric triple sits in [floor, bound), and every component
        /// stays two digits wide. The width rule keeps versions no real
        /// release line could ship outside the range the measured pins
        /// could ever cover. The pin is the D1 range-grammar case
        /// "2.3.100". The comparison itself is the numeric triple, never
        /// ordinal (spec D1).
        /// </summary>
        private static bool IsWithinApprovedRange(
            (int Major, int Minor, int Patch) components)
        {
            if (components.Major > 99 ||
                components.Minor > 99 ||
                components.Patch > 99)
            {
                return false;
            }

            return CompareTriples(components, ApprovedFloorComponents) >= 0
                && CompareTriples(components, ApprovedBoundComponents) < 0;
        }

        private static int CompareTriples(
            (int Major, int Minor, int Patch) left,
            (int Major, int Minor, int Patch) right)
        {
            if (left.Major != right.Major)
            {
                return left.Major < right.Major ? -1 : 1;
            }

            if (left.Minor != right.Minor)
            {
                return left.Minor < right.Minor ? -1 : 1;
            }

            if (left.Patch != right.Patch)
            {
                return left.Patch < right.Patch ? -1 : 1;
            }

            return 0;
        }

        /// <summary>
        /// The strict canonical M.m.p parse (spec D1): exactly three
        /// dot-separated components, each a digit run of one to nine
        /// digits, a multi-digit run never starting with '0', and nothing
        /// trailing the third component. The nine-digit run cap is an
        /// overflow guard: nine digits fill at most 999,999,999, so the
        /// int accumulation cannot wrap, and a longer run is unparseable
        /// before any arithmetic happens. The host grammar's parser is
        /// private to the Build layer and accepts leading zeros, so the
        /// attestation class carries its own. The grammar-equivalence pin
        /// holds the two together on shared canonical forms.
        /// </summary>
        private static bool TryParseCanonicalVersion(
            string packageVersion,
            out (int Major, int Minor, int Patch) components)
        {
            components = default;
            if (string.IsNullOrEmpty(packageVersion))
            {
                return false;
            }

            var parsed = new int[3];
            var pos = 0;
            for (var index = 0; index < parsed.Length; index++)
            {
                if (index > 0 &&
                    !TryReadExpectedDot(packageVersion, ref pos))
                {
                    return false;
                }

                if (!TryReadCanonicalDigitRun(
                        packageVersion, ref pos, out parsed[index]))
                {
                    return false;
                }
            }

            if (pos != packageVersion.Length)
            {
                return false;
            }

            components = (parsed[0], parsed[1], parsed[2]);
            return true;
        }

        private static bool TryReadExpectedDot(string version, ref int pos)
        {
            if (pos < version.Length && version[pos] == '.')
            {
                pos++;
                return true;
            }

            return false;
        }

        private static bool TryReadCanonicalDigitRun(
            string version, ref int pos, out int value)
        {
            value = 0;
            var start = pos;
            var digits = 0;
            while (pos < version.Length)
            {
                var digit = version[pos] - '0';
                if (digit < 0 || digit > 9)
                {
                    break;
                }

                digits++;
                if (digits > 9)
                {
                    return false;
                }

                value = value * 10 + digit;
                pos++;
            }

            if (digits == 0 || (digits > 1 && version[start] == '0'))
            {
                pos = start;
                return false;
            }

            return true;
        }

        /// <summary>
        /// The nearest-row pairing fallback (spec D2): for an approved,
        /// in-range, non-row version, the greatest admitted row at or
        /// below it, scanning the rows as a numeric-triple list. A version
        /// below the floor or at or above the bound pairs nothing, exactly
        /// as at the base commit. The fallback scans its own array, so a
        /// future row insertion needs no pairing change.
        /// </summary>
        private static (string Version, string IncludeDigest)
            NearestAdmittedRow(string packageVersion)
        {
            var nearest = default((string Version, string IncludeDigest));
            var nearestComponents =
                default((int Major, int Minor, int Patch));
            if (!TryParseCanonicalVersion(
                    packageVersion, out var components) ||
                !IsWithinApprovedRange(components))
            {
                return nearest;
            }

            foreach (var row in AdmittedPackageVersions)
            {
                if (!TryParseCanonicalVersion(
                        row.Version, out var rowComponents))
                {
                    continue;
                }

                if (CompareTriples(rowComponents, components) <= 0 &&
                    (nearest.Version == null ||
                     CompareTriples(rowComponents, nearestComponents) > 0))
                {
                    nearest = row;
                    nearestComponents = rowComponents;
                }
            }

            return nearest;
        }

        /// <summary>
        /// The same pairing shape over the admitted Multi rows, for the
        /// Multi column lookup.
        /// </summary>
        private static
            (string Version, string MultiDigest, string MultiOutlineDigest)
            NearestAdmittedMultiRow(string packageVersion)
        {
            var nearest = default(
                (string Version, string MultiDigest,
                    string MultiOutlineDigest));
            var nearestComponents =
                default((int Major, int Minor, int Patch));
            if (!TryParseCanonicalVersion(
                    packageVersion, out var components) ||
                !IsWithinApprovedRange(components))
            {
                return nearest;
            }

            foreach (var row in AdmittedMultiVersions)
            {
                if (!TryParseCanonicalVersion(
                        row.Version, out var rowComponents))
                {
                    continue;
                }

                if (CompareTriples(rowComponents, components) <= 0 &&
                    (nearest.Version == null ||
                     CompareTriples(rowComponents, nearestComponents) > 0))
                {
                    nearest = row;
                    nearestComponents = rowComponents;
                }
            }

            return nearest;
        }

        private static string IncludeDigestForVersion(string packageVersion)
        {
            foreach (var row in AdmittedPackageVersions)
            {
                if (string.Equals(
                        row.Version, packageVersion,
                        StringComparison.Ordinal))
                {
                    return row.IncludeDigest;
                }
            }

            // D2 pairing: an approved non-row version borrows the nearest
            // admitted row below it. Everything else stays null exactly as
            // at the base commit.
            return NearestAdmittedRow(packageVersion).IncludeDigest;
        }

        // Cutout source identity (spec §6 R3). Measured by the merged B1
        // characterization on 2026-08-30 from an installed
        // jp.lilxyzw.liltoon@2.3.4 and cross-checked against default shader
        // settings. Never re-derive these from the lilToon repository, whose
        // committed generated shaders are stale relative to their own tag's
        // generator.
        internal const string CutoutShaderName = "Hidden/lilToonCutout";
        internal const string CutoutShaderGuid =
            "85d6126cae43b6847aff4b13f4adb8ec";
        internal const string CutoutPassShaderName = "Hidden/ltspass_cutout";
        internal const string CutoutPassShaderGuid =
            "ad219df2a46e841488aee6a013e84e36";
        internal const int CutoutRenderMode = 1;
        internal const string CutoutShaderCanonicalDigest =
            "c83d73a26ab86e933f8cacb8c71307d8715fcc1693cdc08d209011bb0f836178";
        internal const string CutoutPassCanonicalDigest =
            "91563265289452c61e50203235792fadba17c828dbbf59c9f8ce6013538b15fc";
        // Transparent source identity (design §6). The shader digest was
        // measured on 2026-09-01 from an installed jp.lilxyzw.liltoon@2.3.4 in
        // a throwaway project outside AMUSE, using a byte-identical copy of
        // this file, in a run that first reproduced all five digests already
        // pinned above, and were identical across two independent Editor
        // sessions (T1 §3.4). The pass digest was re-measured on 2026-09-07
        // like the opaque pass digest above, with both shapes agreeing. Never
        // re-derive these from the lilToon repository: the generator rewrites
        // every ltspass_*.shader at import.
        internal const string TransparentShaderName =
            "Hidden/lilToonTransparent";
        internal const string TransparentShaderGuid =
            "165365ab7100a044ca85fc8c33548a62";
        internal const string TransparentPassShaderName =
            "Hidden/ltspass_transparent";
        internal const string TransparentPassShaderGuid =
            "2683fad669f20ec49b8e9656954a33a8";
        internal const int TransparentRenderMode = 2;
        internal const string TransparentShaderCanonicalDigest =
            "ea247d3cd6ecb09ad4aeefdcad37480c0dffa40d594a3b457624097f2372ba13";
        internal const string TransparentPassCanonicalDigest =
            "b60c492d4fa407b3ae4158d22891159be5f4a73a1f7b6925b6ab3359cdac7762";
        // Outline wrapper source identities (S8). The three outline shaders
        // are thin UsePass wrappers over the family pass assets already
        // pinned above: each declares no inline pass except a LightMode
        // Never dummy, so its passes, render mode, and recipe property
        // surface come from the family pass file. The wrappers carry one
        // generator-varied region of their own except the Tags line, which
        // gains one LTCGI token when LTCGI is installed; R6 removes that
        // token, so the digest pins are unchanged from their 2026-09-06
        // measurement. Digests were measured on 2026-09-06 by invoking the
        // production ComputeNormalizedSourceHash on the official tag 2.3.4
        // source zip (sha256
        // e81579d355878ed73880d99a68ab30a8552d55051be603c450f56491bdc66322;
        // the VPM release zip of the same version hashes to 34d172761c51aa94
        // 6904704109086aafa6125a4fa0e058766e2ddc73d3b303 and carries
        // identical lts_*.shader bytes) and cross-checked against an
        // independent sha256 of the same bytes; the two computations agree.
        // The pass-shader half of each profile is inherited from the family
        // profile. The 2026-09-07 re-measurement confirmed every wrapper
        // shader digest across both generator shapes.
        internal const string OutlineShaderName = "Hidden/lilToonOutline";
        internal const string OutlineShaderGuid =
            "efa77a80ca0344749b4f19fdd5891cbe";
        internal const string OutlineShaderCanonicalDigest =
            "bbd886afd367d73ba3e2208aa42086e9149ccf826564ce4bb4f571d16861aa36";
        internal const string OutlineCutoutShaderName =
            "Hidden/lilToonCutoutOutline";
        internal const string OutlineCutoutShaderGuid =
            "3b4aa19949601f046a20ca8bdaee929f";
        internal const string OutlineCutoutShaderCanonicalDigest =
            "9fa9e7e7be55d29851fe4dd5cf2078e259a0b439dd1b61075a7c0448c176a9ec";
        internal const string OutlineTransparentShaderName =
            "Hidden/lilToonTransparentOutline";
        internal const string OutlineTransparentShaderGuid =
            "3c79b10c7e0b2784aaa4c2f8dd17d55e";
        internal const string OutlineTransparentShaderCanonicalDigest =
            "d105a112d4b8c984baf00ccbffd56213d1e399ef7d4de122b2f39442d2ac198f";

        // One-pass and two-pass transparent wrapper identities (S9). The
        // one-pass shader imports FORWARD, SHADOW_CASTER, and META from the
        // pinned transparent pass asset; the two-pass shader adds the
        // FORWARD_BACK backface pre-pass and FORWARD_ADD. Their outline
        // wrappers import the outline pass set instead. All four declare no
        // inline pass except a LightMode Never dummy and share the family
        // property block byte for byte, so the canonical opaque recipe
        // applies unchanged and the wrapper bytes are the tag's bytes.
        // Digests were measured on 2026-09-06 exactly like the S8 digests
        // above: production ComputeNormalizedSourceHash and an independent
        // sha256 agree on the official tag 2.3.4 files.
        internal const string OnePassTransparentShaderName =
            "Hidden/lilToonOnePassTransparent";
        internal const string OnePassTransparentShaderGuid =
            "b269573b9937b8340b3e9e191a3ba5a8";
        internal const string OnePassTransparentShaderCanonicalDigest =
            "4a5bbd07997e3150bf904aae89223b207c2628e2f1e2d7fe690bcdb76c0d7143";
        internal const string TwoPassTransparentShaderName =
            "Hidden/lilToonTwoPassTransparent";
        internal const string TwoPassTransparentShaderGuid =
            "6a77405f7dfdc1447af58854c7f43f39";
        internal const string TwoPassTransparentShaderCanonicalDigest =
            "c06143d3d345efc1c1dcc8128193a3bb9b1535e4c640b024562923b7b7008074";
        internal const string OnePassTransparentOutlineShaderName =
            "Hidden/lilToonOnePassTransparentOutline";
        internal const string OnePassTransparentOutlineShaderGuid =
            "7171688840c632447b22ec14e2bdef7e";
        internal const string OnePassTransparentOutlineShaderCanonicalDigest =
            "7d87faf6ad3f8217f86b91330d6b1767b75fd91d689ae6f7243c6808b3009f80";
        internal const string TwoPassTransparentOutlineShaderName =
            "Hidden/lilToonTwoPassTransparentOutline";
        internal const string TwoPassTransparentOutlineShaderGuid =
            "9cf054060007d784394b8b0bb703e441";
        internal const string TwoPassTransparentOutlineShaderCanonicalDigest =
            "af28ad17be74f0077a5a4b154a81a70d3dd98bc0e387b93f859b97dd4c505974";

        internal const string ShaderFormatVersionProperty = "_lilToonVersion";
        private const string IncludeFolderName = "Includes";

        /// <summary>
        /// The one pinned identity a profile attests (spec §6 R3). Package
        /// name/version, the shader-format stamp, and the include-tree digest
        /// are deliberately not profile fields: one lilToon 2.3.4 frontend,
        /// one <c>Shader/Includes</c> tree, shared by every profile.
        /// </summary>
        private sealed class LilToonSourceProfile
        {
            internal LilToonSourceProfile(
                string shaderName,
                string shaderGuid,
                string passShaderName,
                string passShaderGuid,
                int renderMode,
                string shaderCanonicalDigest,
                string passCanonicalDigest)
            {
                ShaderName = shaderName;
                ShaderGuid = shaderGuid;
                PassShaderName = passShaderName;
                PassShaderGuid = passShaderGuid;
                RenderMode = renderMode;
                ShaderCanonicalDigest = shaderCanonicalDigest;
                PassCanonicalDigest = passCanonicalDigest;
            }

            internal string ShaderName { get; }
            internal string ShaderGuid { get; }
            internal string PassShaderName { get; }
            internal string PassShaderGuid { get; }
            internal int RenderMode { get; }
            internal string ShaderCanonicalDigest { get; }
            internal string PassCanonicalDigest { get; }
        }

        private static readonly LilToonSourceProfile OpaqueProfile =
            new LilToonSourceProfile(
                SupportedShaderName,
                SupportedShaderGuid,
                PassShaderName,
                PassShaderGuid,
                OpaqueRenderMode,
                ShaderCanonicalDigest,
                PassCanonicalDigest);

        private static readonly LilToonSourceProfile CutoutProfile =
            new LilToonSourceProfile(
                CutoutShaderName,
                CutoutShaderGuid,
                CutoutPassShaderName,
                CutoutPassShaderGuid,
                CutoutRenderMode,
                CutoutShaderCanonicalDigest,
                CutoutPassCanonicalDigest);

        private static readonly LilToonSourceProfile TransparentProfile =
            new LilToonSourceProfile(
                TransparentShaderName,
                TransparentShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                TransparentShaderCanonicalDigest,
                TransparentPassCanonicalDigest);


        private static readonly LilToonSourceProfile OutlineOpaqueProfile =
            new LilToonSourceProfile(
                OutlineShaderName,
                OutlineShaderGuid,
                PassShaderName,
                PassShaderGuid,
                OpaqueRenderMode,
                OutlineShaderCanonicalDigest,
                PassCanonicalDigest);

        private static readonly LilToonSourceProfile OutlineCutoutProfile =
            new LilToonSourceProfile(
                OutlineCutoutShaderName,
                OutlineCutoutShaderGuid,
                CutoutPassShaderName,
                CutoutPassShaderGuid,
                CutoutRenderMode,
                OutlineCutoutShaderCanonicalDigest,
                CutoutPassCanonicalDigest);

        private static readonly LilToonSourceProfile OutlineTransparentProfile =
            new LilToonSourceProfile(
                OutlineTransparentShaderName,
                OutlineTransparentShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                OutlineTransparentShaderCanonicalDigest,
                TransparentPassCanonicalDigest);

        private static readonly LilToonSourceProfile OnePassTransparentProfile =
            new LilToonSourceProfile(
                OnePassTransparentShaderName,
                OnePassTransparentShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                OnePassTransparentShaderCanonicalDigest,
                TransparentPassCanonicalDigest);

        private static readonly LilToonSourceProfile TwoPassTransparentProfile =
            new LilToonSourceProfile(
                TwoPassTransparentShaderName,
                TwoPassTransparentShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                TwoPassTransparentShaderCanonicalDigest,
                TransparentPassCanonicalDigest);

        private static readonly LilToonSourceProfile
            OnePassTransparentOutlineProfile =
            new LilToonSourceProfile(
                OnePassTransparentOutlineShaderName,
                OnePassTransparentOutlineShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                OnePassTransparentOutlineShaderCanonicalDigest,
                TransparentPassCanonicalDigest);

        private static readonly LilToonSourceProfile
            TwoPassTransparentOutlineProfile =
            new LilToonSourceProfile(
                TwoPassTransparentOutlineShaderName,
                TwoPassTransparentOutlineShaderGuid,
                TransparentPassShaderName,
                TransparentPassShaderGuid,
                TransparentRenderMode,
                TwoPassTransparentOutlineShaderCanonicalDigest,
                TransparentPassCanonicalDigest);

        /// <summary>
        /// Every admitted transparent-family source profile: the plain
        /// source, its outline wrapper (S8), and the one-pass and two-pass
        /// transparent variants with their outline wrappers (S9). Identity
        /// verification and evidence gathering both consume this list, so a
        /// new wrapper is admitted everywhere or nowhere.
        /// </summary>
        private static readonly LilToonSourceProfile[]
            TransparentFamilyProfiles =
            {
                TransparentProfile,
                OutlineTransparentProfile,
                OnePassTransparentProfile,
                TwoPassTransparentProfile,
                OnePassTransparentOutlineProfile,
                TwoPassTransparentOutlineProfile,
            };

        /// <summary>
        /// The pinned opaque family: the plain source and its outline
        /// wrapper (S8), over the same pinned opaque pass asset.
        /// </summary>
        private static readonly LilToonSourceProfile[] OpaqueFamilyProfiles =
        {
            OpaqueProfile,
            OutlineOpaqueProfile,
        };

        /// <summary>
        /// The pinned cutout family: the plain source and its outline
        /// wrapper (S8), over the same pinned cutout pass asset.
        /// </summary>
        private static readonly LilToonSourceProfile[] CutoutFamilyProfiles =
        {
            CutoutProfile,
            OutlineCutoutProfile,
        };

        /// <summary>
        /// The literal name-to-family-set dispatch, the one mapping both
        /// profile lookup and identity verification read: each admitted
        /// shader name maps to its own family's profile set. Opaque names
        /// map to the opaque set, cutout names to the cutout set, and every
        /// transparent-family name to the transparent set. Dictionary
        /// lookup is ordinal, so the mapping matches exactly the string
        /// comparisons it replaced.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, LilToonSourceProfile[]>
            ShaderNameFamilySets =
                new Dictionary<string, LilToonSourceProfile[]>
                {
                    [SupportedShaderName] = OpaqueFamilyProfiles,
                    [OutlineShaderName] = OpaqueFamilyProfiles,
                    [CutoutShaderName] = CutoutFamilyProfiles,
                    [OutlineCutoutShaderName] = CutoutFamilyProfiles,
                    [TransparentShaderName] = TransparentFamilyProfiles,
                    [OutlineTransparentShaderName] =
                        TransparentFamilyProfiles,
                    [OnePassTransparentShaderName] =
                        TransparentFamilyProfiles,
                    [TwoPassTransparentShaderName] =
                        TransparentFamilyProfiles,
                    [OnePassTransparentOutlineShaderName] =
                        TransparentFamilyProfiles,
                    [TwoPassTransparentOutlineShaderName] =
                        TransparentFamilyProfiles,
                };
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

        private static readonly Regex ExternalActivatorDefine = new Regex(
            @"^#define\s+(?<identifier>" +
            @"LIL_FEATURE_VRCLIGHTVOLUMES|" +
            @"LIL_FEATURE_AUDIOLINK_PACKAGE|" +
            @"LIL_FEATURE_LTCGI)(?:\s.*)?$",
            RegexOptions.Compiled);

        // Closed BuildShaderSettingString/BuildShaderSettingStringMulti domain
        // for official lilToon 2.3.4, in generator order. Prefix membership is
        // intentionally insufficient.
        private static readonly string[] OfficialSettingIdentifiers =
        {
            "LIL_FEATURE_ANIMATE_MAIN_UV",
            "LIL_FEATURE_MAIN_TONE_CORRECTION",
            "LIL_FEATURE_MAIN_GRADATION_MAP",
            "LIL_FEATURE_MAIN2ND",
            "LIL_FEATURE_MAIN3RD",
            "LIL_FEATURE_DECAL",
            "LIL_FEATURE_ANIMATE_DECAL",
            "LIL_FEATURE_LAYER_DISSOLVE",
            "LIL_FEATURE_ALPHAMASK",
            "LIL_FEATURE_SHADOW",
            "LIL_FEATURE_RECEIVE_SHADOW",
            "LIL_FEATURE_SHADOW_3RD",
            "LIL_FEATURE_SHADOW_LUT",
            "LIL_FEATURE_RIMSHADE",
            "LIL_FEATURE_EMISSION_1ST",
            "LIL_FEATURE_EMISSION_2ND",
            "LIL_FEATURE_ANIMATE_EMISSION_UV",
            "LIL_FEATURE_ANIMATE_EMISSION_MASK_UV",
            "LIL_FEATURE_EMISSION_GRADATION",
            "LIL_FEATURE_NORMAL_1ST",
            "LIL_FEATURE_NORMAL_2ND",
            "LIL_FEATURE_ANISOTROPY",
            "LIL_FEATURE_REFLECTION",
            "LIL_FEATURE_MATCAP",
            "LIL_FEATURE_MATCAP_2ND",
            "LIL_FEATURE_RIMLIGHT",
            "LIL_FEATURE_RIMLIGHT_DIRECTION",
            "LIL_FEATURE_GLITTER",
            "LIL_FEATURE_BACKLIGHT",
            "LIL_FEATURE_PARALLAX",
            "LIL_FEATURE_POM",
            "LIL_FEATURE_CLIPPING_CANCELLER",
            "LIL_FEATURE_DISTANCE_FADE",
            "LIL_FEATURE_AUDIOLINK",
            "LIL_FEATURE_AUDIOLINK_VERTEX",
            "LIL_FEATURE_AUDIOLINK_LOCAL",
            "LIL_FEATURE_DISSOLVE",
            "LIL_FEATURE_DITHER",
            "LIL_FEATURE_IDMASK",
            "LIL_FEATURE_UDIMDISCARD",
            "LIL_FEATURE_OUTLINE_TONE_CORRECTION",
            "LIL_FEATURE_OUTLINE_RECEIVE_SHADOW",
            "LIL_FEATURE_ANIMATE_OUTLINE_UV",
            "LIL_FEATURE_FUR_COLLISION",
            "LIL_FEATURE_MainGradationTex",
            "LIL_FEATURE_MainColorAdjustMask",
            "LIL_FEATURE_Main2ndTex",
            "LIL_FEATURE_Main2ndBlendMask",
            "LIL_FEATURE_Main2ndDissolveMask",
            "LIL_FEATURE_Main2ndDissolveNoiseMask",
            "LIL_FEATURE_Main3rdTex",
            "LIL_FEATURE_Main3rdBlendMask",
            "LIL_FEATURE_Main3rdDissolveMask",
            "LIL_FEATURE_Main3rdDissolveNoiseMask",
            "LIL_FEATURE_AlphaMask",
            "LIL_FEATURE_BumpMap",
            "LIL_FEATURE_Bump2ndMap",
            "LIL_FEATURE_Bump2ndScaleMask",
            "LIL_FEATURE_AnisotropyTangentMap",
            "LIL_FEATURE_AnisotropyScaleMask",
            "LIL_FEATURE_AnisotropyShiftNoiseMask",
            "LIL_FEATURE_ShadowBorderMask",
            "LIL_FEATURE_ShadowBlurMask",
            "LIL_FEATURE_ShadowStrengthMask",
            "LIL_FEATURE_ShadowColorTex",
            "LIL_FEATURE_Shadow2ndColorTex",
            "LIL_FEATURE_Shadow3rdColorTex",
            "LIL_FEATURE_RimShadeMask",
            "LIL_FEATURE_BacklightColorTex",
            "LIL_FEATURE_SmoothnessTex",
            "LIL_FEATURE_MetallicGlossMap",
            "LIL_FEATURE_ReflectionColorTex",
            "LIL_FEATURE_ReflectionCubeTex",
            "LIL_FEATURE_MatCapTex",
            "LIL_FEATURE_MatCapBlendMask",
            "LIL_FEATURE_MatCapBumpMap",
            "LIL_FEATURE_MatCap2ndTex",
            "LIL_FEATURE_MatCap2ndBlendMask",
            "LIL_FEATURE_MatCap2ndBumpMap",
            "LIL_FEATURE_RimColorTex",
            "LIL_FEATURE_GlitterColorTex",
            "LIL_FEATURE_GlitterShapeTex",
            "LIL_FEATURE_EmissionMap",
            "LIL_FEATURE_EmissionBlendMask",
            "LIL_FEATURE_EmissionGradTex",
            "LIL_FEATURE_Emission2ndMap",
            "LIL_FEATURE_Emission2ndBlendMask",
            "LIL_FEATURE_Emission2ndGradTex",
            "LIL_FEATURE_ParallaxMap",
            "LIL_FEATURE_AudioLinkMask",
            "LIL_FEATURE_AudioLinkLocalMap",
            "LIL_FEATURE_DissolveMask",
            "LIL_FEATURE_DissolveNoiseMask",
            "LIL_FEATURE_OutlineTex",
            "LIL_FEATURE_OutlineWidthMask",
            "LIL_FEATURE_OutlineVectorTex",
            "LIL_FEATURE_FurNoiseMask",
            "LIL_FEATURE_FurMask",
            "LIL_FEATURE_FurLengthMask",
            "LIL_FEATURE_FurVectorTex",
            "LIL_OPTIMIZE_APPLY_SHADOW_FA",
            "LIL_OPTIMIZE_USE_FORWARDADD",
            "LIL_OPTIMIZE_USE_FORWARDADD_SHADOW",
            "LIL_OPTIMIZE_USE_VERTEXLIGHT",
            "LIL_OPTIMIZE_USE_LIGHTMAP",
            "LIL_FEATURE_VRCLIGHTVOLUMES",
            "LIL_FEATURE_VRCLIGHTVOLUMES_WITHOUTPACKAGE",
            "LIL_FEATURE_AUDIOLINK_PACKAGE",
            "LIL_INPUT_OPTIMIZED",
        };

        private static readonly string[] OfficialSkipVariantRecords =
        {
            "#pragma skip_variants _REFLECTION_PROBE_BOX_PROJECTION",
            "#pragma skip_variants LIGHTPROBE_SH",
            "#pragma skip_variants _MIXED_LIGHTING_SUBTRACTIVE",
        };

        // The closed skip_variants token domain of attested lilToon 2.3.4: the
        // union of the generator's fixed Get* literals and every token in the
        // shipped package bytes. The unpacker's dedup pass redistributes these
        // tokens across slots, so content, not position, decides whether a
        // skip line is generator output.
        private static readonly string[] OfficialSkipVariantVocabulary =
        {
            "DECALS_3RT",
            "DECALS_4RT",
            "DECALS_OFF",
            "DECAL_SURFACE_GRADIENT",
            "DIRLIGHTMAP_COMBINED",
            "DYNAMICLIGHTMAP_ON",
            "LIGHTMAP_ON",
            "LIGHTMAP_SHADOW_MIXING",
            "LIGHTPROBE_SH",
            "PROBE_VOLUMES_L1",
            "PROBE_VOLUMES_L2",
            "PROBE_VOLUMES_OFF",
            "SCREEN_SPACE_SHADOWS_ON",
            "SHADOWS_SCREEN",
            "SHADOWS_SHADOWMASK",
            "SHADOW_HIGH",
            "SHADOW_LOW",
            "SHADOW_MEDIUM",
            "SHADOW_VERY_HIGH",
            "USE_CLUSTERED_LIGHTLIST",
            "USE_FPTL_LIGHTLIST",
            "VERTEXLIGHT_ON",
            "_ADDITIONAL_LIGHT_SHADOWS",
            "_DBUFFER_MRT1",
            "_DBUFFER_MRT2",
            "_DBUFFER_MRT3",
            "_MAIN_LIGHT_SHADOWS",
            "_MAIN_LIGHT_SHADOWS_CASCADE",
            "_MAIN_LIGHT_SHADOWS_SCREEN",
            "_MIXED_LIGHTING_SUBTRACTIVE",
            "_REFLECTION_PROBE_BLENDING",
            "_REFLECTION_PROBE_BOX_PROJECTION",
            "_SCREEN_SPACE_OCCLUSION",
        };

        private static readonly HashSet<string> SkipVariantVocabulary =
            new HashSet<string>(
                OfficialSkipVariantVocabulary,
                StringComparer.Ordinal);

        // R2 anchor: the fixed terminal line of the BRP lil_multi_compile_forward
        // expansion. The template places lil_skip_variants_{base,outline}_shadows
        // immediately after it, so this line uniquely locates that slot.
        private const string ShadowSlotAnchor = "#define LIL_PASS_FORWARD";

        // The Multi container define. Only a SubShader-scope include block
        // that carries it is a generated Multi container, so only there
        // does the settings walk extend past the fixed pragma lines (spec
        // F9). A regular asset never carries it, so its region records stay
        // byte-identical to the pre-F9 scan.
        private const string MultiContainerDefine = "#define LIL_MULTI";

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
        /// of its UTF-8 bytes.
        /// </summary>
        internal static string ComputeNormalizedSourceHash(string rawSource)
        {
            return NormalizedSourceHash.Compute(rawSource);
        }

        /// <summary>
        /// Removes exactly the text lilToon's generator is proven to vary, so
        /// the remainder can be hashed against a pin. R1 drops the
        /// setting-substituted feature block inside an HLSLINCLUDE run; R2 drops
        /// the shadow skip-variant expansion at its one substitution slot; R3
        /// normalizes an include path only when it provably resolves into the
        /// attested tree. R5 drops a skip_variants line whose tokens all belong
        /// to the generator vocabulary, wherever the dedup pass leaves it; R4
        /// drops the LTCGI define at its forward-block anchor; R6 removes the
        /// LTCGI token a SubShader Tags line can gain. Everything else — pass
        /// bodies, other tag text, blend state, other pragmas, other includes,
        /// blank lines, and every valued define — is retained, so any hand edit
        /// or custom-shader injection changes the digest.
        /// </summary>

        internal static LilToonCanonicalizationAnalysis AnalyzeCanonicalization(
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

            var lines = NormalizedSourceHash.NormalizeText(rawShaderSource).Split('\n');
            var regions = new List<LilToonRemovedRegion>();
            var activators = new List<LilToonActivatorOccurrence>();


            // Mark the setting region before emitting, so a same-shaped line
            // outside it can never be dropped.
            var inSettingRegion = new bool[lines.Length];
            var hlslIncludeOrdinal = 0;
            var inSubShaderScope = false;
            for (var i = 0; i < lines.Length; i++)
            {
                // SubShader scope is the first gate of the block walk
                // extension (spec F9); only a block that also carries the
                // Multi container define extends its walk. A Shader-scope
                // block always keeps the leading-run discipline.
                if (lines[i].Trim().StartsWith(
                        "SubShader", StringComparison.Ordinal))
                {
                    inSubShaderScope = true;
                }

                // Region A: after HLSLINCLUDE, the maximal run of D1/D2 lines. A
                // blank line does not extend the run, and a valued define ends
                // it immediately — which is why the Shader-scope block holding
                // `#define LIL_RENDER 0` has an empty region A.
                if (!string.Equals(
                        lines[i].Trim(), "HLSLINCLUDE", StringComparison.Ordinal))
                {
                    continue;
                }

                var records = new List<LilToonRemovedRecord>();
                // The block walk below extends only into a generated Multi
                // container: a SubShader-scope include block that carries
                // the LIL_MULTI define. Every other block keeps the pre-F9
                // scan, so its removed-region records are byte-identical.
                var isMultiContainerBlock = false;
                if (inSubShaderScope)
                {
                    for (var k = i + 1; k < lines.Length; k++)
                    {
                        var probe = lines[k].Trim();
                        if (string.Equals(
                                probe, "ENDHLSL", StringComparison.Ordinal))
                        {
                            break;
                        }

                        if (string.Equals(
                                probe, MultiContainerDefine,
                                StringComparison.Ordinal))
                        {
                            isMultiContainerBlock = true;
                            break;
                        }
                    }
                }

                for (var j = i + 1; j < lines.Length; j++)
                {
                    var candidate = lines[j].Trim();
                    var isDefine = SettingDefine.IsMatch(candidate);
                    var isSkipVariants = SkipVariants.IsMatch(candidate);
                    if (!isDefine && !isSkipVariants)
                    {
                        // Inside a generated Multi container's include block
                        // a valueless D1/D2 line is region content wherever
                        // it sits, so the scan walks to the end of the
                        // block. Every other block keeps the maximal run
                        // directly after HLSLINCLUDE, and any later
                        // valueless define stays hashed.
                        if (!isMultiContainerBlock ||
                            string.Equals(
                                candidate, "ENDHLSL", StringComparison.Ordinal))
                        {
                            break;
                        }

                        continue;
                    }

                    inSettingRegion[j] = true;
                    records.Add(new LilToonRemovedRecord(
                        j,
                        records.Count,
                        isDefine
                            ? LilToonRemovedRecordKind.Define
                            : LilToonRemovedRecordKind.SkipVariants,
                        candidate));
                }

                regions.Add(new LilToonRemovedRegion(
                    hlslIncludeOrdinal++, i, records));
            }

            // Collect the known external-activation defines with their slot
            // provenance. The generator emits these defines only inside the
            // verified setting run, or — for LTCGI — directly before the
            // LIL_PASS_FORWARD anchor. An occurrence anywhere else is a
            // tamper signal.
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                var activator = ExternalActivatorDefine.Match(trimmed);
                if (!activator.Success)
                {
                    continue;
                }

                var identifier = activator.Groups["identifier"].Value;
                var proven = inSettingRegion[i]
                    || (string.Equals(
                            identifier,
                            "LIL_FEATURE_LTCGI",
                            StringComparison.Ordinal)
                        && i + 1 < lines.Length
                        && string.Equals(
                            lines[i + 1].Trim(),
                            ShadowSlotAnchor,
                            StringComparison.Ordinal));
                activators.Add(new LilToonActivatorOccurrence(
                    i, identifier, proven));
            }

            var builder = new StringBuilder(rawShaderSource.Length);
            var first = true;
            var ltcgiTagStripScope = ComputeLtcgiTagStripScope(lines);
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

                if (IsVocabularySkipVariantsLine(trimmed))
                {
                    continue;
                }

                if (IsLtcgiForwardAnchorDefine(lines, i, trimmed))
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
                        ltcgiTagStripScope[i]
                            ? RemoveAppendedLtcgiTagToken(line, trimmed)
                            : line,
                        shaderDirectory, projectRoot, includeTree));
            }

            return new LilToonCanonicalizationAnalysis(
                builder.ToString(), regions, activators);
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
        /// R5. Every token on the line must belong to the closed generator
        /// vocabulary. A skip line only prunes compile variants and cannot
        /// change an AMUSE decision, so content decides removal wherever the
        /// dedup pass leaves the line. A line with an unknown token stays
        /// hashed.
        /// </summary>
        private static bool IsVocabularySkipVariantsLine(string trimmed)
        {
            if (!SkipVariants.IsMatch(trimmed))
            {
                return false;
            }

            var tokens = trimmed.Split(
                (char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 3)
            {
                return false;
            }

            for (var i = 2; i < tokens.Length; i++)
            {
                if (!SkipVariantVocabulary.Contains(tokens[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// R4. The container generator inserts this define directly before
        /// the LIL_PASS_FORWARD terminal line of the built-in-RP forward block
        /// when LTCGI is installed. The anchor is read from the raw line
        /// array, so an earlier removal can never shift it.
        /// </summary>
        private static bool IsLtcgiForwardAnchorDefine(
            string[] lines,
            int index,
            string trimmed)
        {
            return index + 1 < lines.Length &&
                string.Equals(
                    trimmed,
                    "#define LIL_FEATURE_LTCGI",
                    StringComparison.Ordinal) &&
                string.Equals(
                    lines[index + 1].Trim(),
                    ShadowSlotAnchor,
                    StringComparison.Ordinal);
        }

        /// <summary>
        /// R6. The LTCGI integration appends exactly one token to a SubShader
        /// Tags line. The token is removed only from the final position of a
        /// Tags line; any other edit to the line stays hashed.
        /// </summary>
        private static string RemoveAppendedLtcgiTagToken(
            string line,
            string trimmed)
        {
            const string token = " \"LTCGI\"=\"ALWAYS\"}";
            if (!trimmed.StartsWith("Tags {", StringComparison.Ordinal) ||
                !trimmed.EndsWith(token, StringComparison.Ordinal))
            {
                return line;
            }

            var tokenIndex = line.LastIndexOf(token, StringComparison.Ordinal);
            return tokenIndex < 0
                ? line
                : line.Remove(tokenIndex, token.Length - 1);
        }

        /// <summary>
        /// Marks the lines where the R6 LTCGI tag strip may run: a Tags line
        /// whose innermost open brace scope is a SubShader block. One forward
        /// walk tracks brace depth. A SubShader keyword whose brace opens on a
        /// later line, the shipped Allman shape, holds its kind pending until
        /// the brace line. Blank lines and comments between keyword and brace
        /// keep the pair. Any other content line between them breaks the pair,
        /// so the strip under-fires. A comment-only line carries no braces.
        /// The anchor may under-strip: a Tags line that keeps its token
        /// contributes its original text to the digest and the material
        /// refuses. It must never widen the strip beyond SubShader scope.
        /// </summary>
        private static bool[] ComputeLtcgiTagStripScope(string[] lines)
        {
            var eligible = new bool[lines.Length];
            var scopeKinds = new List<string>();
            var pendingSubShader = false;
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                var isComment = trimmed.StartsWith("//", StringComparison.Ordinal);
                var firstToken = trimmed.Length == 0 || isComment
                    ? string.Empty
                    : trimmed.Split(' ')[0];

                eligible[i] = !isComment &&
                    trimmed.StartsWith("Tags {", StringComparison.Ordinal) &&
                    scopeKinds.Count > 0 &&
                    scopeKinds[scopeKinds.Count - 1] == "SubShader";

                if (isComment)
                {
                    continue;
                }

                var opens = trimmed.Count(character => character == '{');
                var closes = trimmed.Count(character => character == '}');
                for (var open = 0; open < opens; open++)
                {
                    var opensSubShader = open == 0 &&
                        (firstToken == "SubShader" ||
                            (pendingSubShader &&
                                trimmed.StartsWith(
                                    "{", StringComparison.Ordinal)));
                    scopeKinds.Add(opensSubShader ? "SubShader" : "other");
                }

                if (opens > 0)
                {
                    pendingSubShader = false;
                }
                else if (firstToken == "SubShader")
                {
                    pendingSubShader = true;
                }
                else if (pendingSubShader && trimmed.Length > 0)
                {
                    // A content line between the keyword and its brace breaks
                    // the Allman pair. The strip under-fires and the material
                    // refuses, which is the safe direction.
                    pendingSubShader = false;
                }

                for (var close = 0; close < closes; close++)
                {
                    if (scopeKinds.Count > 0)
                    {
                        scopeKinds.RemoveAt(scopeKinds.Count - 1);
                    }
                }
            }

            return eligible;
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
            return NormalizedSourceHash.Compute(string.Join("\n", rows));
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

            foreach (var rawLine in NormalizedSourceHash.NormalizeText(passShaderSource).Split('\n'))
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
            foreach (var rawLine in NormalizedSourceHash.NormalizeText(passShaderSource).Split('\n'))
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

        private static bool TryVerifyStandaloneCanonicalizationProvenance(
            LilToonCanonicalizationAnalysis shader,
            LilToonCanonicalizationAnalysis pass,
            LilToonSourceProfile profile,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (shader == null || pass == null)
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                    (shader == null
                        ? profile.ShaderName
                        : profile.PassShaderName) +
                    " canonicalization provenance");
                return false;
            }

            foreach (var occurrence in shader.Activators.Concat(pass.Activators))
            {
                if (occurrence.ProvenSlot)
                {
                    continue;
                }

                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedShaderVariant,
                    occurrence.Identifier);
                return false;
            }

            if (shader.RemovedRegions.Count != 0 ||
                pass.RemovedRegions.Count != 2 ||
                pass.RemovedRegions[0].HlslIncludeOrdinal != 0 ||
                pass.RemovedRegions[1].HlslIncludeOrdinal != 1 ||
                pass.RemovedRegions[0].Records.Count != 0 ||
                !TryVerifyOfficialSettingRecord(pass.RemovedRegions[1]))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.ModifiedShaderSource,
                    (shader.RemovedRegions.Count != 0
                        ? profile.ShaderName
                        : profile.PassShaderName) + " canonicalization provenance");
                return false;
            }

            diagnostic = null;
            return true;
        }

        private static bool TryVerifyOfficialSettingRecord(
            LilToonRemovedRegion region)
        {
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var pragmas = new HashSet<string>(StringComparer.Ordinal);
            var lastDefineOrder = -1;
            var lastPragmaOrder = -1;
            var sawPragma = false;

            for (var i = 0; i < region.Records.Count; i++)
            {
                var record = region.Records[i];
                if (record.OffsetInRegion != i ||
                    record.LineIndex != region.HlslIncludeLineIndex + i + 1)
                {
                    return false;
                }

                if (record.Kind == LilToonRemovedRecordKind.Define)
                {
                    const string prefix = "#define ";
                    if (!record.Text.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    var identifier = record.Text.Substring(prefix.Length);
                    var order = Array.IndexOf(OfficialSettingIdentifiers, identifier);
                    if (order < 0 ||
                        !string.Equals(
                            record.Text, prefix + identifier, StringComparison.Ordinal) ||
                        !identifiers.Add(identifier))
                    {
                        return false;
                    }

                    if (string.Equals(
                            identifier, "LIL_INPUT_OPTIMIZED",
                            StringComparison.Ordinal))
                    {
                        if (i != region.Records.Count - 1)
                        {
                            return false;
                        }
                    }
                    else if (sawPragma || order <= lastDefineOrder)
                    {
                        return false;
                    }

                    lastDefineOrder = order;
                    continue;
                }

                if (record.Kind != LilToonRemovedRecordKind.SkipVariants)
                {
                    return false;
                }

                sawPragma = true;
                var pragmaOrder = Array.IndexOf(
                    OfficialSkipVariantRecords, record.Text);
                if (pragmaOrder < 0 ||
                    pragmaOrder <= lastPragmaOrder ||
                    !pragmas.Add(record.Text))
                {
                    return false;
                }
                lastPragmaOrder = pragmaOrder;
            }

            return
                identifiers.Contains("LIL_FEATURE_Main2ndDissolveNoiseMask") &&
                identifiers.Contains("LIL_FEATURE_Main3rdDissolveNoiseMask") &&
                identifiers.Contains("LIL_FEATURE_DissolveNoiseMask") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_DECAL",
                    "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_ANIMATE_DECAL",
                    "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_LAYER_DISSOLVE",
                    "LIL_FEATURE_MAIN2ND", "LIL_FEATURE_MAIN3RD") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_RECEIVE_SHADOW",
                    "LIL_FEATURE_SHADOW") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_SHADOW_3RD",
                    "LIL_FEATURE_SHADOW") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_SHADOW_LUT",
                    "LIL_FEATURE_SHADOW") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_ANIMATE_EMISSION_UV",
                    "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_ANIMATE_EMISSION_MASK_UV",
                    "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_EMISSION_GRADATION",
                    "LIL_FEATURE_EMISSION_1ST", "LIL_FEATURE_EMISSION_2ND") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_RIMLIGHT_DIRECTION",
                    "LIL_FEATURE_RIMLIGHT") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_POM", "LIL_FEATURE_PARALLAX") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_AUDIOLINK_VERTEX",
                    "LIL_FEATURE_AUDIOLINK") &&
                HasRequiredParent(
                    identifiers, "LIL_FEATURE_AUDIOLINK_LOCAL",
                    "LIL_FEATURE_AUDIOLINK") &&
                identifiers.Contains("LIL_FEATURE_REFLECTION") ==
                !pragmas.Contains(OfficialSkipVariantRecords[0]) &&
                identifiers.Contains("LIL_OPTIMIZE_USE_VERTEXLIGHT") ==
                !pragmas.Contains(OfficialSkipVariantRecords[1]) &&
                identifiers.Contains("LIL_OPTIMIZE_USE_LIGHTMAP") ==
                !pragmas.Contains(OfficialSkipVariantRecords[2]);
        }

        private static bool HasRequiredParent(
            HashSet<string> identifiers,
            string child,
            params string[] parents)
        {
            if (!identifiers.Contains(child))
            {
                return true;
            }

            foreach (var parent in parents)
            {
                if (identifiers.Contains(parent))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verifies the pinned opaque family identity against either admitted
        /// opaque shader: the plain source or its outline wrapper (S8). The
        /// wrapper is a UsePass wrapper over the same pinned pass asset, so
        /// the only profile-specific checks that differ are the wrapper's own
        /// name, GUID, and canonical digest. Mismatch fails closed with a
        /// diagnostic; there is no name-only fallback.
        /// </summary>
        internal static bool TryVerifyLilToonIdentity(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            return VerifyFamily(
                evidence,
                OpaqueFamilyProfiles,
                out diagnostic);
        }

        /// <summary>
        /// Verifies the pinned cutout family identity (spec §6 R3) against
        /// either admitted cutout shader: the plain source or its outline
        /// wrapper (S8), over the same pinned cutout pass asset. Mismatch
        /// fails closed with a diagnostic; there is no name-only fallback.
        /// </summary>
        internal static bool TryVerifyLilToonCutoutIdentity(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            return VerifyFamily(
                evidence,
                CutoutFamilyProfiles,
                out diagnostic);
        }

        /// <summary>
        /// Verifies the pinned regular Transparent Normal identity (design
        /// §6) against every admitted transparent shader: the plain source,
        /// its outline wrapper (S8), and the one-pass and two-pass variants
        /// with their outline wrappers (S9), all over the same pinned
        /// transparent pass asset. Mismatch fails closed with a diagnostic;
        /// there is no name-only fallback.
        /// </summary>
        internal static bool TryVerifyLilToonTransparentIdentity(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            return VerifyFamily(
                evidence,
                TransparentFamilyProfiles,
                out diagnostic);
        }

        /// <summary>
        /// Selects the family profile whose pinned shader name is exactly the
        /// evidence's shader name, then runs the identity conjunction once.
        /// A shader name outside the family refuses before any digest work,
        /// with the same diagnostic the single-profile path produced.
        /// </summary>
        private static bool VerifyFamily(
            LilToonSourceEvidence evidence,
            LilToonSourceProfile[] familyProfiles,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            foreach (var profile in familyProfiles)
            {
                if (string.Equals(
                        evidence.ShaderName,
                        profile.ShaderName,
                        StringComparison.Ordinal))
                {
                    return Verify(evidence, profile, out diagnostic);
                }
            }

            diagnostic = MaterialDiagnostic(
                LilToonSemanticDiagnosticCode.UnsupportedShader,
                $"shader name '{evidence.ShaderName}'");
            return false;
        }

        /// <summary>
        /// Reports whether the shader name is a pinned name, and yields the
        /// matching profile when it is. An unmatched name returns false with
        /// a null profile. No gather runs for an unmatched name.
        /// </summary>
        private static bool TryGetProfileForShaderName(
            string shaderName,
            out LilToonSourceProfile profile)
        {
            profile = null;
            if (shaderName == null ||
                !ShaderNameFamilySets.TryGetValue(shaderName, out var family))
            {
                return false;
            }

            foreach (var candidate in family)
            {
                if (string.Equals(
                        shaderName,
                        candidate.ShaderName,
                        StringComparison.Ordinal))
                {
                    profile = candidate;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gathers identity evidence for the shader's pinned profile. An
        /// unmatched shader name returns false with a refusal. No gather
        /// runs for that name.
        /// </summary>
        internal static bool TryGatherSourceEvidenceForShaderName(
            Shader shader,
            CapturedMaterialEvidence captured,
            out LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic refusal)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (captured == null)
            {
                throw new ArgumentNullException(nameof(captured));
            }

            evidence = null;
            if (!TryGetProfileForShaderName(shader.name, out var profile))
            {
                refusal = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedShader,
                    $"shader name '{shader.name}'");
                return false;
            }

            refusal = null;
            evidence = Gather(shader, captured, profile, shader.name);
            return true;
        }

        /// <summary>
        /// Verifies identity evidence with the family the evidence's shader
        /// name selects, across every admitted lilToon shader. Opaque names
        /// verify against the opaque family set, cutout names against the
        /// cutout family set, and every other name falls through to the
        /// transparent family set, whose name loop refuses it as an
        /// unsupported shader.
        /// </summary>
        internal static bool TryVerifyIdentityForShaderName(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            var family = evidence.ShaderName != null &&
                         ShaderNameFamilySets.TryGetValue(
                             evidence.ShaderName, out var matched)
                ? matched
                : TransparentFamilyProfiles;
            return VerifyFamily(evidence, family, out diagnostic);
        }

        /// <summary>
        /// Resolves the canonical opaque target shader name for a source
        /// shader name. A Multi container resolves to itself: the clone
        /// stays on its container asset. Outline wrapper sources move to
        /// the outline opaque shader. All other sources move to the plain
        /// opaque shader.
        /// </summary>
        internal static string ResolveCanonicalTargetShaderName(
            string sourceShaderName)
        {
            if (LilToonMultiResolution.IsMultiContainerShaderName(
                    sourceShaderName))
            {
                return sourceShaderName;
            }

            return IsOutlineWrapperShaderName(sourceShaderName)
                ? OutlineShaderName
                : SupportedShaderName;
        }

        private static bool IsOutlineWrapperShaderName(string shaderName)
        {
            return string.Equals(
                       shaderName, OutlineShaderName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       shaderName, OutlineCutoutShaderName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       shaderName, OutlineTransparentShaderName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       shaderName, OnePassTransparentOutlineShaderName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       shaderName, TwoPassTransparentOutlineShaderName,
                       StringComparison.Ordinal);
        }

        /// <summary>
        /// The identity conjunction, parameterized by profile. Purely
        /// mechanical: the check order, every diagnostic code and detail
        /// string, and the verdicts are exactly the ones the opaque path has
        /// always produced.
        /// </summary>
        private static bool Verify(
            LilToonSourceEvidence evidence,
            LilToonSourceProfile profile,
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
                    profile.ShaderName,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.UnsupportedShader,
                    $"shader name '{evidence.ShaderName}'");
                return false;
            }

            if (!string.Equals(
                    evidence.AssetGuid,
                    profile.ShaderGuid,
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

                // Admitted, or approved inside the range (D2): the
                // disjunct keeps the row set exact and sends an approved
                // non-row version to the tree check instead of refusing.
                if (!(IsAdmittedPackageVersion(evidence.PackageVersion) ||
                      IsApprovedPackageVersion(evidence.PackageVersion)))
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
                    profile.PassShaderGuid,
                    StringComparison.Ordinal))
            {
                diagnostic = MaterialDiagnostic(
                    LilToonSemanticDiagnosticCode.MissingSourceEvidence,
                    profile.PassShaderName);
                return false;
            }

            if (!TryVerifyStandaloneCanonicalizationProvenance(
                    evidence.ShaderCanonicalization,
                    evidence.PassCanonicalization,
                    profile,
                    out diagnostic))
            {
                return false;
            }

            // 5. Source digests, include tree first: it is the only
            // per-version bytes the conjunction checks (S11), keyed by the
            // package version when installed as a package and by the
            // admitted-digest set alone for loose installs.
            if (!TryVerifyIncludeTreeForVersion(
                    evidence.HasPackage
                        ? evidence.PackageVersion
                        : null,
                    evidence.IncludeTreeDigest,
                    out diagnostic) ||
                !TryMatchDigest(
                    evidence.ShaderCanonicalDigest,
                    profile.ShaderCanonicalDigest,
                    profile.ShaderName,
                    out diagnostic) ||
                !TryMatchDigest(
                    evidence.PassCanonicalDigest,
                    profile.PassCanonicalDigest,
                    profile.PassShaderName,
                    out diagnostic))
            {
                return false;
            }

            // 6. Render mode as the current pass declares it, not as the pass
            //    asset's historical name implies.
            if (!evidence.HasRenderMode ||
                evidence.RenderMode != profile.RenderMode)
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
        /// The include tree is the only per-version bytes the identity
        /// conjunction checks (S11). A packaged install must present the
        /// tree its own version ships; a loose install carries no version
        /// label, so an admitted tree alone identifies the shipped version
        /// and anything else fails closed.
        /// </summary>
        private static bool TryVerifyIncludeTreeForVersion(
            string packageVersion,
            string includeDigest,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (!string.IsNullOrEmpty(packageVersion))
            {
                return TryMatchDigest(
                    includeDigest,
                    IncludeDigestForVersion(packageVersion),
                    IncludeFolderName,
                    out diagnostic);
            }

            foreach (var row in AdmittedPackageVersions)
            {
                if (string.Equals(
                        row.IncludeDigest, includeDigest,
                        StringComparison.Ordinal))
                {
                    diagnostic = null;
                    return true;
                }
            }

            diagnostic = MaterialDiagnostic(
                LilToonSemanticDiagnosticCode.ModifiedShaderSource,
                IncludeFolderName);
            return false;
        }

        /// <summary>
        /// The Multi container identity conjunction, parameterized by one
        /// injected row. The terms are the container shader name, the asset
        /// GUID, the material shader-format stamp, the package version when
        /// the package is installed, the include tree, and the canonical
        /// source digest. Every mismatch refuses with
        /// <see cref="LilToonMultiResolutionRefusal.AttestationFailed"/>;
        /// the closed refusal vocabulary carries no per-term values. No
        /// LIL_RENDER scan and no pass check runs: the mode half of a Multi
        /// material is the keyword-derivation mode-consistency gate's input,
        /// and a Multi container declares no pass asset.
        /// </summary>
        internal static bool TryVerifyMultiContainer(
            LilToonSourceEvidence evidence,
            LilToonMultiContainerProfile profile,
            out LilToonMultiResolutionRefusal refusal)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            // 1. Container identity. Exact name and asset GUID; there is no
            //    name-only fallback.
            if (!string.Equals(
                    evidence.ShaderName,
                    profile.ShaderName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    evidence.AssetGuid,
                    profile.ShaderGuid,
                    StringComparison.Ordinal))
            {
                refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                return false;
            }

            // 2. Material shader-format stamp, compared exactly. A malformed
            //    or nearby value must never be normalized into the pinned
            //    one.
            if (!evidence.HasShaderFormatVersion ||
                float.IsNaN(evidence.ShaderFormatVersion) ||
                float.IsInfinity(evidence.ShaderFormatVersion) ||
                evidence.ShaderFormatVersion != ShaderFormatVersion)
            {
                refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                return false;
            }

            // 3. Package identity, when installed as a package. The version
            //    must be an admitted one that carries an admitted Multi row
            //    (A2), or an approved version, which pairs the nearest
            //    admitted Multi row at or below it. A version with no Multi
            //    row at or below it refuses right here: the container
            //    attestation fails closed. An approved version below the
            //    oldest Multi row gets its ask suppressed and reports
            //    UnsupportedMultiState slot rows without separating. A
            //    loose install carries no version label, so an admitted
            //    tree alone identifies the shipped version below.
            if (evidence.HasPackage &&
                (!string.Equals(
                        evidence.PackageName, PackageName,
                        StringComparison.Ordinal) ||
                 !(IsAdmittedPackageVersion(evidence.PackageVersion) ||
                   IsApprovedPackageVersion(evidence.PackageVersion)) ||
                 !TryMultiColumnDigest(
                     evidence.PackageVersion,
                     LilToonMultiContainerColumn.Base,
                     out _)))
            {
                refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                return false;
            }

            // 4. The include tree. A packaged install must present the
            //    tree its own version ships; a loose install carries no
            //    version label, so an admitted tree alone identifies the
            //    shipped version and anything else fails closed. The
            //    identified row binds the digest term below.
            string multiRowVersion;
            if (evidence.HasPackage)
            {
                if (!TryVerifyIncludeTreeForVersion(
                        evidence.PackageVersion,
                        evidence.IncludeTreeDigest,
                        out _))
                {
                    refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                    return false;
                }

                multiRowVersion = evidence.PackageVersion;
            }
            else
            {
                if (!TryIdentifyMultiVersionByTree(
                        evidence.IncludeTreeDigest,
                        out multiRowVersion))
                {
                    refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                    return false;
                }
            }

            // 5. The container canonical digest, over the Task 1
            //    canonicalization. A packaged install compares against the
            //    row the resolver injected, which the resolution point
            //    bound to the identified version. A loose install has no
            //    version label, so the digest compares against the
            //    tree-identified row's column for this container. Missing
            //    or mismatched source refuses.
            if (string.IsNullOrEmpty(evidence.ShaderCanonicalDigest))
            {
                refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                return false;
            }

            var expectedDigest = evidence.HasPackage
                ? profile.ShaderCanonicalDigest
                : (TryMultiColumnDigest(
                        multiRowVersion,
                        ColumnForProfile(profile),
                        out var rowDigest)
                    ? rowDigest
                    : null);
            if (!string.Equals(
                    evidence.ShaderCanonicalDigest,
                    expectedDigest,
                    StringComparison.Ordinal))
            {
                refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                return false;
            }

            refusal = default;
            return true;
        }


        /// <summary>
        /// Resolves one live shader's Multi container row and gathers its
        /// source evidence. The row must match the live shader's name and
        /// asset GUID exactly, and the gathered evidence must identify one
        /// admitted Multi version - by its package version when the package
        /// is installed, by its include tree alone when the install is
        /// loose. The container digest the row carries is the identified
        /// version's column from the admitted Multi table, so the verify
        /// binds version, tree, and digest as a unit. Fail-closed: an
        /// unidentified version refuses every Multi container with
        /// <see cref="LilToonMultiResolutionRefusal.AttestationFailed"/>.
        /// The verify itself is the parameterized conjunction, so the
        /// resolution point can store the verified gather without a second
        /// canonicalization pass. When the digest term refuses, the out
        /// evidence keeps the gathered facts (the profile stays null), so
        /// the refused record can carry the captured package identity.
        /// </summary>
        internal static bool TryResolveMultiContainer(
            Shader shader,
            CapturedMaterialEvidence evidence,
            out LilToonSourceEvidence sourceEvidence,
            out LilToonMultiContainerProfile profile,
            out LilToonMultiResolutionRefusal refusal)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                shader, out var assetGuid, out long _);
            var shaderName = shader.name;
            var normalizedGuid = assetGuid?.ToLowerInvariant();

            foreach (var row in MultiContainerProfiles)
            {
                if (string.Equals(
                        shaderName, row.ShaderName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        normalizedGuid, row.ShaderGuid,
                        StringComparison.Ordinal))
                {
                    sourceEvidence =
                        GatherMultiContainerEvidence(shader, evidence);
                    if (TryResolveMultiRowDigest(
                            sourceEvidence, row.Column, out var digest))
                    {
                        profile = new LilToonMultiContainerProfile(
                            row.ShaderName, row.ShaderGuid, digest);
                        refusal = default;
                        return true;
                    }

                    // The gather survives the refusal: the resolution point
                    // stores its captured package facts on the refused
                    // record, so the consent layer reads the captured
                    // version without a second canonicalization pass (D3).
                    profile = null;
                    refusal = LilToonMultiResolutionRefusal.AttestationFailed;
                    return false;
                }
            }

            sourceEvidence = null;
            profile = null;
            refusal = LilToonMultiResolutionRefusal.AttestationFailed;
            return false;
        }

        /// <summary>
        /// The container digest for one resolved container: the package
        /// version names the admitted Multi row when the package is
        /// installed; a loose install carries no version label, so its
        /// include tree identifies the shipped version.
        /// </summary>
        private static bool TryResolveMultiRowDigest(
            LilToonSourceEvidence sourceEvidence,
            LilToonMultiContainerColumn column,
            out string digest)
        {
            if (sourceEvidence.HasPackage &&
                (IsAdmittedPackageVersion(sourceEvidence.PackageVersion) ||
                 IsApprovedPackageVersion(sourceEvidence.PackageVersion)) &&
                TryMultiColumnDigest(
                    sourceEvidence.PackageVersion, column, out digest))
            {
                return true;
            }

            digest = null;
            return TryIdentifyMultiVersionByTree(
                    sourceEvidence.IncludeTreeDigest, out var version) &&
                TryMultiColumnDigest(version, column, out digest);
        }

        // Which admit column a container row reads: the base container's
        // digest column or the outline wrapper's.
        private enum LilToonMultiContainerColumn
        {
            Base,
            Outline,
        }

        // (A2) Version-aware Multi admission. Version, tree, and container
        // digest bind as a unit. No cross combination attests. The 2.4.0
        // outline digest differs from 2.3.4 because upstream fix 435 changed
        // the ltsmulti_o queue tag; measured 2026-10-19 from installed shapes
        // per the two-shape rule. The 2.3.4 row carries the 2026-10-01 pins
        // unchanged. The 2.4.1 row is measured from the installed 2.4.1
        // shape on 2026-10-20 by the production gather: the include tree
        // matches the tag-derived row, and both columns equal the 2.4.0
        // columns because no Multi template changed upstream. Single-shape
        // measurement, cross-checked against the official tag
        // byte-equality.
        private static readonly
            (string Version, string MultiDigest, string MultiOutlineDigest)[]
            AdmittedMultiVersions =
            {
                ("2.3.4",
                    "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                    "225af2c6eba7a017593ca8a0f38114a98580ed67844a4d8465fed2c96f7cb75f"),
                ("2.4.0",
                    "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                    "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85"),
                ("2.4.1",
                    "346528b53eaa04b495b87e3faf583a79d6e0cbb1d35d7f3881c442b0a39ddf8f",
                    "f33e846c45eca0c62d343526fc9c3ee72d0e8585cc42b372c0fde9460ef18c85"),
            };

        /// <summary>
        /// Read-only snapshot of the admitted Multi version rows for
        /// first-party test and research consumers, with the same copy
        /// discipline as <see cref="AdmittedRows"/>. Returns a copy, so a
        /// caller can never observe or cause in-place mutation.
        /// </summary>
        internal static IReadOnlyList<(string Version, string MultiDigest, string MultiOutlineDigest)>
            AdmittedMultiRows()
        {
            return AdmittedMultiVersions.ToArray();
        }

        /// <summary>
        /// The pinned Multi container rows: the base container and its
        /// outline wrapper, one row each. Each row resolves by container
        /// name and asset GUID and maps the container to its digest column;
        /// the digests themselves live in <see cref="AdmittedMultiVersions"/>,
        /// one column value per admitted version. Every digest there is
        /// a measured value, and the comment on that row list owns each
        /// row's evidence status. No placeholder digest ships here.
        /// </summary>
        private static readonly
            (string ShaderName, string ShaderGuid, LilToonMultiContainerColumn Column)[]
            MultiContainerProfiles =
            {
                // Measured on 2026-10-01 through the production
                // AnalyzeCanonicalization + ComputeNormalizedSourceHash from two real
                // installed settings shapes of a scratch 2.3.4 project
                // (default import, then vertex-light and anisotropy
                // settings applied): both shapes produce one digest per
                // container, and the same run reproduced every regular
                // pin above byte-for-byte. Never re-derive these from the
                // lilToon repository, whose committed generated shaders
                // are stale relative to their own tag's generator.
                ("_lil/lilToonMulti",
                    "9294844b15dca184d914a632279b24e1",
                    LilToonMultiContainerColumn.Base),
                ("Hidden/lilToonMultiOutline",
                    "51b2dee0ab07bd84d8147601ff89e511",
                    LilToonMultiContainerColumn.Outline),
            };

        /// <summary>
        /// Loose installs carry no version label, so an admitted Multi
        /// version's tree alone identifies the shipped version: the row
        /// whose include-tree digest the evidence presents. Mirrors the
        /// regular path's include-tree wording. The digest comparison then
        /// runs against that identified row's column.
        /// </summary>
        private static bool TryIdentifyMultiVersionByTree(
            string includeTreeDigest,
            out string version)
        {
            foreach (var row in AdmittedMultiVersions)
            {
                if (string.Equals(
                        includeTreeDigest,
                        IncludeDigestForVersion(row.Version),
                        StringComparison.Ordinal))
                {
                    version = row.Version;
                    return true;
                }
            }

            version = null;
            return false;
        }

        /// <summary>
        /// The admitted digest for one version and one container column;
        /// false for a version with no admitted Multi row.
        /// </summary>
        private static bool TryMultiColumnDigest(
            string version,
            LilToonMultiContainerColumn column,
            out string digest)
        {
            foreach (var row in AdmittedMultiVersions)
            {
                if (string.Equals(
                        row.Version, version,
                        StringComparison.Ordinal))
                {
                    digest = column == LilToonMultiContainerColumn.Outline
                        ? row.MultiOutlineDigest
                        : row.MultiDigest;
                    return true;
                }
            }

            // D2 pairing: an approved non-row version pairs the nearest
            // admitted Multi row below it. A version with no Multi row at
            // or below it has no column, exactly as at the base commit.
            var nearest = NearestAdmittedMultiRow(version);
            if (nearest.Version == null)
            {
                digest = null;
                return false;
            }

            digest = column == LilToonMultiContainerColumn.Outline
                ? nearest.MultiOutlineDigest
                : nearest.MultiDigest;
            return true;
        }

        private static LilToonMultiContainerColumn ColumnForProfile(
            LilToonMultiContainerProfile profile)
        {
            foreach (var row in MultiContainerProfiles)
            {
                if (string.Equals(
                        profile.ShaderName, row.ShaderName,
                        StringComparison.Ordinal))
                {
                    return row.Column;
                }
            }

            // An unknown name maps to the base column; the name and GUID
            // terms already refuse unknown containers before the digest
            // term reads the column.
            return LilToonMultiContainerColumn.Base;
        }

        /// <summary>
        /// Reads identity evidence from a live shader and already-captured
        /// material facts. Every filesystem access resolves through an explicit
        /// project root derived from
        /// <see cref="Application.dataPath"/>; nothing relies on the process
        /// working directory. Unreadable evidence is omitted rather than
        /// guessed, so the conjunction refuses.
        /// </summary>
        internal static LilToonSourceEvidence GatherSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return Gather(shader, evidence, OpaqueProfile);
        }

        /// <summary>
        /// Gathers identity evidence for the pinned cutout identity: the
        /// material shader is read directly and only the pass the cutout
        /// profile names (<c>Hidden/ltspass_cutout</c>) is resolved. A pass
        /// that does not resolve is omitted rather than guessed, so
        /// verification fails closed.
        /// </summary>
        internal static LilToonSourceEvidence GatherCutoutSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return Gather(shader, evidence, CutoutProfile);
        }

        /// <summary>
        /// Gathers identity evidence for the pinned transparent identity: the
        /// material shader is read directly and only the pass the transparent
        /// profile names (<c>Hidden/ltspass_transparent</c>) is resolved. A
        /// pass that does not resolve is omitted rather than guessed, so
        /// verification fails closed.
        /// </summary>
        internal static LilToonSourceEvidence GatherTransparentSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            return Gather(shader, evidence, TransparentProfile);
        }

        /// <summary>
        /// Evidence common to every lilToon shader gather.
        /// </summary>
        private readonly struct GatherFrontHalf
        {
            internal readonly string AssetGuid;
            internal readonly bool HasVersion;
            internal readonly float Version;
            internal readonly UnityEditor.PackageManager.PackageInfo Package;
            internal readonly string ProjectRoot;
            internal readonly LilToonIncludeTree IncludeTree;
            internal readonly string IncludeDigest;
            internal readonly LilToonCanonicalizationAnalysis ShaderAnalysis;
            internal readonly string ShaderDigest;

            internal GatherFrontHalf(
                string assetGuid,
                bool hasVersion,
                float version,
                UnityEditor.PackageManager.PackageInfo package,
                string projectRoot,
                LilToonIncludeTree includeTree,
                string includeDigest,
                LilToonCanonicalizationAnalysis shaderAnalysis,
                string shaderDigest)
            {
                AssetGuid = assetGuid;
                HasVersion = hasVersion;
                Version = version;
                Package = package;
                ProjectRoot = projectRoot;
                IncludeTree = includeTree;
                IncludeDigest = includeDigest;
                ShaderAnalysis = shaderAnalysis;
                ShaderDigest = shaderDigest;
            }
        }

        // --- Source gather memo ---------------------------------------------

        /// <summary>
        /// One gather serves every material on one unchanged shader until
        /// the build boundary. The key is the shader asset's project-relative
        /// path, the pass slot the profile names (null for the Multi
        /// container gather), the shader name override, and the shader
        /// format stamp the gather embeds, read from the captured material
        /// and normalized to 0 when absent so the key stays equality-safe.
        /// The guard is the UTC write times of the shader file and of every
        /// non-meta file under the include folder. The shader file's write
        /// time alone never guards an entry: the include tree is digested
        /// evidence, so an include edit must invalidate too. The shape is
        /// the corrected Poiyomi source-hash memo: identity plus write
        /// times plus the per-build clear.
        /// </summary>
        private static readonly Dictionary<
            (string ShaderAssetPath, string PassShaderName,
                string ShaderNameOverride, bool HasVersion, float Version),
            (string WriteTimes, LilToonSourceEvidence Evidence)>
            GatherMemo = new();

        /// <summary>
        /// Drops every memoized gather. The per-build clear sites call this,
        /// so the next build re-gathers after a mid-session package swap or
        /// a source edit.
        /// </summary>
        internal static void ClearGatherMemo()
        {
            GatherMemo.Clear();
        }

        /// <summary>
        /// The include folder beside one shader asset, in the absolute form
        /// every System.IO call consumes. Null when the shader has no
        /// absolute path, so callers fail closed.
        /// </summary>
        private static string IncludeFolderFullPath(string shaderFullPath)
        {
            var shaderDirectory = shaderFullPath == null
                ? null
                : Path.GetDirectoryName(shaderFullPath);
            return shaderDirectory == null
                ? null
                : Path.Combine(shaderDirectory, IncludeFolderName);
        }

        /// <summary>
        /// The write-time guard for one shader gather: the shader file's UTC
        /// write time, then every non-meta file's UTC write time under the
        /// include folder, sorted by path. A missing or unreadable folder
        /// contributes no entries, matching the empty tree the gather builds
        /// for it. Statting files is far cheaper than reading, hashing, and
        /// canonicalizing them.
        /// </summary>
        private static string GatherWriteTimes(
            string shaderFullPath,
            string includeFolderFullPath)
        {
            var writeTimes = new StringBuilder();
            AppendWriteTime(writeTimes, shaderFullPath);
            try
            {
                if (includeFolderFullPath != null &&
                    Directory.Exists(includeFolderFullPath))
                {
                    var paths = Directory.GetFiles(
                        includeFolderFullPath,
                        "*",
                        SearchOption.AllDirectories);
                    Array.Sort(paths, StringComparer.Ordinal);
                    foreach (var path in paths)
                    {
                        if (path.EndsWith(".meta", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        AppendWriteTime(writeTimes, path);
                    }
                }
            }
            catch (IOException)
            {
                // An unreadable folder matches the gather's empty tree.
            }
            catch (UnauthorizedAccessException)
            {
                // An unreadable folder matches the gather's empty tree.
            }

            return writeTimes.ToString();
        }

        private static void AppendWriteTime(
            StringBuilder writeTimes,
            string fullPath)
        {
            if (fullPath == null)
            {
                // No absolute path: nothing to stamp. The gather for an
                // in-memory shader bypasses the memo on identity anyway.
                return;
            }

            writeTimes.Append(fullPath)
                .Append('|')
                .Append(File.GetLastWriteTimeUtc(fullPath).Ticks)
                .Append('\n');
        }

        /// <summary>
        /// The shader format stamp one gather embeds, read from the captured
        /// material. Absent normalizes to 0. The tuple key compares the
        /// float with float.Equals, where NaN does equal NaN, so the raw
        /// stamp would still match. The normalization is canonicalization:
        /// it keeps the special float out of the key, so the key's
        /// matchability never depends on NaN equality semantics.
        /// </summary>
        private static (bool HasVersion, float Version) ShaderFormatStamp(
            CapturedMaterialEvidence evidence)
        {
            return evidence.TryGetScalar(
                ShaderFormatVersionProperty, out var version)
                ? (true, version)
                : (false, 0f);
        }

        private static bool TryReadGatheredEvidence(
            (string ShaderAssetPath, string PassShaderName,
                string ShaderNameOverride, bool HasVersion,
                float Version) key,
            string writeTimes,
            out LilToonSourceEvidence evidence)
        {
            evidence = null;
            if (string.IsNullOrEmpty(key.ShaderAssetPath))
            {
                // No asset identity: an in-memory shader has nothing to
                // memoize against, so it gathers fresh every time.
                return false;
            }

            if (GatherMemo.TryGetValue(key, out var cached) &&
                cached.WriteTimes == writeTimes)
            {
                evidence = cached.Evidence;
                return true;
            }

            return false;
        }

        private static void StoreGatheredEvidence(
            (string ShaderAssetPath, string PassShaderName,
                string ShaderNameOverride, bool HasVersion,
                float Version) key,
            string writeTimes,
            LilToonSourceEvidence evidence)
        {
            if (string.IsNullOrEmpty(key.ShaderAssetPath))
            {
                return;
            }

            GatherMemo[key] = (writeTimes, evidence);
        }

        /// <summary>
        /// Gathers identity evidence common to both standard and Multi container lilToon shaders.
        /// </summary>
        private static GatherFrontHalf GatherSharedFrontHalf(
            Shader shader,
            CapturedMaterialEvidence evidence,
            (bool HasVersion, float Version) formatStamp,
            string shaderAssetPath,
            string projectRoot,
            string shaderFullPath)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                shader, out var assetGuid, out long _);

            var package = UnityEditor.PackageManager.PackageInfo
                .FindForAssetPath(shaderAssetPath);

            var shaderDirectory = shaderFullPath == null
                ? null
                : Path.GetDirectoryName(shaderFullPath);
            var includeFolder = IncludeFolderFullPath(shaderFullPath);

            var includeTree = LilToonIncludeTree.Enumerate(
                includeFolder, ReadTextOrNull, ComputeNormalizedSourceHash);

            var includeDigest = includeTree.Files.Count == 0
                ? null
                : ComputeIncludeTreeDigest(includeTree.Files);

            var shaderText = ReadTextOrNull(shaderFullPath);
            var shaderAnalysis = shaderText == null
                ? null
                : AnalyzeCanonicalization(
                    shaderText, shaderDirectory, projectRoot, includeTree);
            var shaderDigest = shaderAnalysis == null
                ? null
                : NormalizedSourceHash.Compute(shaderAnalysis.CanonicalSource);

            return new GatherFrontHalf(
                assetGuid,
                formatStamp.HasVersion,
                formatStamp.Version,
                package,
                projectRoot,
                includeTree,
                includeDigest,
                shaderAnalysis,
                shaderDigest);
        }

        /// <summary>
        /// The gather, parameterized by profile: only the pass asset the
        /// profile names is resolved. Purely mechanical; every read, fallback,
        /// and omission rule is exactly the one the opaque path has always
        /// applied. One unchanged shader gathers once per build; the memo
        /// serves the stored evidence until the write times move.
        /// </summary>
        private static LilToonSourceEvidence Gather(
            Shader shader,
            CapturedMaterialEvidence evidence,
            LilToonSourceProfile profile,
            string shaderNameOverride = null)
        {
            // Project-relative: Unity asset APIs consume this form directly.
            var shaderAssetPath = AssetDatabase.GetAssetPath(shader);

            // Absolute: every System.IO call below uses this form only.
            var projectRoot = TryGetProjectRoot();
            var shaderFullPath = ToAbsolute(projectRoot, shaderAssetPath);

            var formatStamp = ShaderFormatStamp(evidence);
            var memoKey = (shaderAssetPath, profile.PassShaderName,
                shaderNameOverride, formatStamp.HasVersion,
                formatStamp.Version);
            var writeTimes = GatherWriteTimes(
                shaderFullPath, IncludeFolderFullPath(shaderFullPath));
            if (TryReadGatheredEvidence(memoKey, writeTimes, out var memoized))
            {
                return memoized;
            }

            var gathered = GatherCore(
                shader,
                evidence,
                profile,
                shaderNameOverride,
                formatStamp,
                shaderAssetPath,
                projectRoot,
                shaderFullPath);
            StoreGatheredEvidence(memoKey, writeTimes, gathered);
            return gathered;
        }

        /// <summary>
        /// The uncached gather core behind the memo: reads and digests the
        /// shader and its include tree, then resolves only the pass asset
        /// the profile names. The fingerprint is taken before the reads, so
        /// a file edited mid-gather carries a newer write time than the
        /// stored guard and the next gather re-runs.
        /// </summary>
        private static LilToonSourceEvidence GatherCore(
            Shader shader,
            CapturedMaterialEvidence evidence,
            LilToonSourceProfile profile,
            string shaderNameOverride,
            (bool HasVersion, float Version) formatStamp,
            string shaderAssetPath,
            string projectRoot,
            string shaderFullPath)
        {
            var frontHalf = GatherSharedFrontHalf(
                shader,
                evidence,
                formatStamp,
                shaderAssetPath,
                projectRoot,
                shaderFullPath);

            var passShader = Shader.Find(profile.PassShaderName);
            string passGuid = null;
            string passDigest = null;
            string passText = null;
            LilToonCanonicalizationAnalysis passAnalysis = null;
            if (passShader != null)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    passShader, out passGuid, out long _);

                var passFullPath = ToAbsolute(
                    frontHalf.ProjectRoot, AssetDatabase.GetAssetPath(passShader));
                passText = ReadTextOrNull(passFullPath);
                if (passText != null)
                {
                    // The pass resolves its own includes relative to its own
                    // directory, which need not be the material shader's.
                    passAnalysis = AnalyzeCanonicalization(
                        passText,
                        Path.GetDirectoryName(passFullPath),
                        frontHalf.ProjectRoot,
                        frontHalf.IncludeTree);
                    passDigest = NormalizedSourceHash.Compute(passAnalysis.CanonicalSource);
                }
            }

            var hasRenderMode = TryScanRenderMode(passText, out var renderMode);

            return new LilToonSourceEvidence(
                shaderNameOverride ??
                    (evidence.HasShaderName ? evidence.ShaderName : null),
                frontHalf.AssetGuid?.ToLowerInvariant(),
                frontHalf.HasVersion,
                frontHalf.Version,
                frontHalf.Package != null,
                frontHalf.Package?.name,
                frontHalf.Package?.version,
                passGuid?.ToLowerInvariant(),
                frontHalf.ShaderDigest,
                passDigest,
                frontHalf.IncludeDigest,
                hasRenderMode,
                renderMode,
                ScanCompiledFeatures(passText),
                frontHalf.ShaderAnalysis,
                passAnalysis);
        }

        /// <summary>
        /// Gathers Multi container identity evidence. The material shader is
        /// read directly, and no pass asset is resolved, because a Multi
        /// container declares no pass half. No LIL_RENDER scan runs: the
        /// material's mode is the mode-consistency gate's input, not source
        /// evidence. Unreadable evidence is omitted rather than guessed, so
        /// the conjunction refuses.
        /// </summary>
        private static LilToonSourceEvidence GatherMultiContainerEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            // Project-relative: Unity asset APIs consume this form directly.
            var shaderAssetPath = AssetDatabase.GetAssetPath(shader);

            // Absolute: every System.IO call below uses this form only.
            var projectRoot = TryGetProjectRoot();
            var shaderFullPath = ToAbsolute(projectRoot, shaderAssetPath);

            // Null pass slot and null override: the Multi gather resolves no
            // pass half and never takes a shader name override.
            var formatStamp = ShaderFormatStamp(evidence);
            var memoKey = (shaderAssetPath, (string)null, (string)null,
                formatStamp.HasVersion, formatStamp.Version);
            var writeTimes = GatherWriteTimes(
                shaderFullPath, IncludeFolderFullPath(shaderFullPath));
            if (TryReadGatheredEvidence(memoKey, writeTimes, out var memoized))
            {
                return memoized;
            }

            var frontHalf = GatherSharedFrontHalf(
                shader,
                evidence,
                formatStamp,
                shaderAssetPath,
                projectRoot,
                shaderFullPath);

            var gathered = new LilToonSourceEvidence(
                evidence.HasShaderName ? evidence.ShaderName : null,
                frontHalf.AssetGuid?.ToLowerInvariant(),
                frontHalf.HasVersion,
                frontHalf.Version,
                frontHalf.Package != null,
                frontHalf.Package?.name,
                frontHalf.Package?.version,
                null, // No pass asset: no pass identity exists to resolve.
                frontHalf.ShaderDigest,
                null,
                frontHalf.IncludeDigest,
                false, // No LIL_RENDER scan: the mode gate owns the mode half.
                0,
                ScanCompiledFeatures(null), // No pass text: no pass features.
                frontHalf.ShaderAnalysis,
                null);
            StoreGatheredEvidence(memoKey, writeTimes, gathered);
            return gathered;
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
