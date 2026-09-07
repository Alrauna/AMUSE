using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
        internal string Text { get; }

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
            string text,
            bool provenSlot)
        {
            LineIndex = lineIndex;
            Identifier = identifier
                ?? throw new ArgumentNullException(nameof(identifier));
            Text = text ?? throw new ArgumentNullException(nameof(text));
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
        internal const string PackageVersion = "2.3.4"; // newest admitted; the set is AdmittedPackageVersions (S11)
        internal const float ShaderFormatVersion = 45f;
        internal const int OpaqueRenderMode = 0;

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
        internal const string IncludeTreeDigest =
            "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46";

        // Admitted package versions and the include-tree digest each ships
        // (S11). The Shader and ltspass files are byte-identical across
        // 2.3.0-2.3.4 - per-tag raw sha256 and .meta GUID comparison on the
        // official tag artifacts - so the include tree is the only
        // per-version bytes the identity conjunction checks. 2.3.1, 2.3.2,
        // and 2.3.3 ship identical trees and share a row. The 2.3.4 digest
        // reproduces the pinned IncludeTreeDigest above, which anchors the
        // measurement method; the include diffs against 2.3.4 (an
        // additional-light mode constant, an APV light-direction helper)
        // touch nothing the alpha proofs cite.
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
                (PackageVersion, IncludeTreeDigest),
            };

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

            return null;
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
        /// attested tree. R5 drops a skip_variants line whose tokens all belong
        /// to the generator vocabulary, wherever the dedup pass leaves it; R4
        /// drops the LTCGI define at its forward-block anchor; R6 removes the
        /// LTCGI token a SubShader Tags line can gain. Everything else — pass
        /// bodies, other tag text, blend state, other pragmas, other includes,
        /// blank lines, and every valued define — is retained, so any hand edit
        /// or custom-shader injection changes the digest.
        /// </summary>
        internal static string Canonicalize(
            string rawShaderSource,
            string shaderDirectory,
            string projectRoot,
            LilToonIncludeTree includeTree)
        {
            return AnalyzeCanonicalization(
                rawShaderSource, shaderDirectory, projectRoot, includeTree)
                .CanonicalSource;
        }

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

            var lines = Normalize(rawShaderSource).Split('\n');
            var regions = new List<LilToonRemovedRegion>();
            var activators = new List<LilToonActivatorOccurrence>();


            // Mark the setting region before emitting, so a same-shaped line
            // outside it can never be dropped.
            var inSettingRegion = new bool[lines.Length];
            var hlslIncludeOrdinal = 0;
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

                var records = new List<LilToonRemovedRecord>();
                for (var j = i + 1; j < lines.Length; j++)
                {
                    var candidate = lines[j].Trim();
                    var isDefine = SettingDefine.IsMatch(candidate);
                    var isSkipVariants = SkipVariants.IsMatch(candidate);
                    if (!isDefine && !isSkipVariants)
                    {
                        break;
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
                    i, identifier, trimmed, proven));
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
                        RemoveAppendedLtcgiTagToken(line, trimmed),
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
                new[] { OpaqueProfile, OutlineOpaqueProfile },
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
                new[] { CutoutProfile, OutlineCutoutProfile },
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
        /// The admitted profile whose pinned shader name is exactly the live
        /// shader name, or the opaque profile when nothing matches; the
        /// family verify refuses the name, so the default only decides which
        /// pass asset a gather resolves for a shader that will refuse
        /// anyway.
        /// </summary>
        private static LilToonSourceProfile ProfileForShaderName(
            string shaderName)
        {
            if (string.Equals(
                    shaderName, OutlineShaderName, StringComparison.Ordinal))
            {
                return OutlineOpaqueProfile;
            }
            if (string.Equals(
                    shaderName,
                    OutlineCutoutShaderName,
                    StringComparison.Ordinal))
            {
                return OutlineCutoutProfile;
            }
            if (string.Equals(
                    shaderName,
                    OutlineTransparentShaderName,
                    StringComparison.Ordinal))
            {
                return OutlineTransparentProfile;
            }
            if (string.Equals(
                    shaderName,
                    CutoutShaderName,
                    StringComparison.Ordinal))
            {
                return CutoutProfile;
            }
            if (string.Equals(
                    shaderName,
                    OnePassTransparentShaderName,
                    StringComparison.Ordinal))
            {
                return OnePassTransparentProfile;
            }
            if (string.Equals(
                    shaderName,
                    TwoPassTransparentShaderName,
                    StringComparison.Ordinal))
            {
                return TwoPassTransparentProfile;
            }
            if (string.Equals(
                    shaderName,
                    OnePassTransparentOutlineShaderName,
                    StringComparison.Ordinal))
            {
                return OnePassTransparentOutlineProfile;
            }
            if (string.Equals(
                    shaderName,
                    TwoPassTransparentOutlineShaderName,
                    StringComparison.Ordinal))
            {
                return TwoPassTransparentOutlineProfile;
            }

            return OpaqueProfile;
        }

        /// <summary>
        /// Gathers identity evidence with the profile the shader's own live
        /// name selects, so a canonical clone that stays on its source's
        /// outline wrapper resolves the same pass asset the wrapper executes.
        /// </summary>
        internal static LilToonSourceEvidence GatherSourceEvidenceForShaderName(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            return Gather(
                shader, evidence, ProfileForShaderName(shader.name));
        }

        /// <summary>
        /// Verifies identity evidence with the family the evidence's shader
        /// name selects, across every admitted lilToon shader. An
        /// unrecognized name refuses as an unsupported shader.
        /// </summary>
        internal static bool TryVerifyIdentityForShaderName(
            LilToonSourceEvidence evidence,
            out LilToonSemanticDiagnostic diagnostic)
        {
            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (string.Equals(
                    evidence.ShaderName,
                    SupportedShaderName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    evidence.ShaderName,
                    OutlineShaderName,
                    StringComparison.Ordinal))
            {
                return VerifyFamily(
                    evidence,
                    new[] { OpaqueProfile, OutlineOpaqueProfile },
                    out diagnostic);
            }
            if (string.Equals(
                    evidence.ShaderName,
                    CutoutShaderName,
                    StringComparison.Ordinal) ||
                string.Equals(
                    evidence.ShaderName,
                    OutlineCutoutShaderName,
                    StringComparison.Ordinal))
            {
                return VerifyFamily(
                    evidence,
                    new[] { CutoutProfile, OutlineCutoutProfile },
                    out diagnostic);
            }

            return VerifyFamily(
                evidence,
                TransparentFamilyProfiles,
                out diagnostic);
        }

        /// <summary>
        /// Resolves the canonical opaque target shader name for a source
        /// shader name. An outline wrapper source keeps its own wrapper, so
        /// the moved triangles carry the outline passes with them; every
        /// other source - the plain sources and the plain one-pass and
        /// two-pass variants - moves onto the plain opaque shader.
        /// </summary>
        internal static string ResolveCanonicalTargetShaderName(
            string sourceShaderName)
        {
            return IsOutlineWrapperShaderName(sourceShaderName)
                ? sourceShaderName
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

                if (!IsAdmittedPackageVersion(evidence.PackageVersion))
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
        /// Gathers the pinned opaque target profile using the target shader's
        /// own live name rather than the cutout source name stored in the
        /// material evidence. The captured evidence supplies only the pinned
        /// shader-format version scalar.
        /// </summary>
        internal static LilToonSourceEvidence GatherOpaqueTargetSourceEvidence(
            Shader shader,
            CapturedMaterialEvidence evidence)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            return Gather(shader, evidence, OpaqueProfile, shader.name);
        }

        /// <summary>
        /// The gather, parameterized by profile: only the pass asset the
        /// profile names is resolved. Purely mechanical; every read, fallback,
        /// and omission rule is exactly the one the opaque path has always
        /// applied.
        /// </summary>
        private static LilToonSourceEvidence Gather(
            Shader shader,
            CapturedMaterialEvidence evidence,
            LilToonSourceProfile profile,
            string shaderNameOverride = null)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                shader, out var assetGuid, out long _);

            var hasVersion = evidence.TryGetScalar(
                ShaderFormatVersionProperty, out var version);
            if (!hasVersion)
            {
                version = float.NaN;
            }

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
            var shaderAnalysis = shaderText == null
                ? null
                : AnalyzeCanonicalization(
                    shaderText, shaderDirectory, projectRoot, includeTree);
            var shaderDigest = shaderAnalysis == null
                ? null
                : Sha256(shaderAnalysis.CanonicalSource);

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
                    projectRoot, AssetDatabase.GetAssetPath(passShader));
                passText = ReadTextOrNull(passFullPath);
                if (passText != null)
                {
                    // The pass resolves its own includes relative to its own
                    // directory, which need not be the material shader's.
                    passAnalysis = AnalyzeCanonicalization(
                        passText,
                        Path.GetDirectoryName(passFullPath),
                        projectRoot,
                        includeTree);
                    passDigest = Sha256(passAnalysis.CanonicalSource);
                }
            }

            var hasRenderMode = TryScanRenderMode(passText, out var renderMode);

            return new LilToonSourceEvidence(
                shaderNameOverride ??
                    (evidence.HasShaderName ? evidence.ShaderName : null),
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
                ScanCompiledFeatures(passText),
                shaderAnalysis,
                passAnalysis);
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
