using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Semantics.LilToon
{
    /// <summary>
    /// The include-tree identity resolution on a real temp folder. Two
    /// include files drive <see cref="LilToonIncludeTree.Enumerate"/>
    /// through its injectable read delegates, and the digest must equal
    /// the <c>ForTests</c>-built tree for the same content. The path
    /// resolution refusals follow the pinned ordinal ruling: a casing
    /// difference is a redirected path and never resolves. The temp folder
    /// is the test's own and goes away on teardown.
    /// </summary>
    public sealed class LilToonIncludeTreeTests
    {
        private static readonly string Root = Path.Combine(
            Path.GetTempPath(), "AmuseTests_IncludeTree");

        private static readonly (string RelativePath, string Content)[]
            Sources =
            {
                ("lil_common.hlsl", "// common\nfloat lil Common();\n"),
                ("Sub/lil_extra.hlsl", "// extra\nvoid lil Extra();\n"),
            };

        [SetUp]
        public void SetUp()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }

            foreach (var (relativePath, content) in Sources)
            {
                var fullPath = Path.Combine(Root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                File.WriteAllText(fullPath, content);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        /// <summary>
        /// The injected digest: deterministic over the content, and
        /// independent of Unity so the fixture pins the tree plumbing, not
        /// the production hash.
        /// </summary>
        private static string Digest(string text)
        {
            var hash = 17u;
            foreach (var value in text)
            {
                hash = (hash * 31u) + value;
            }

            return hash.ToString("X8");
        }

        private static string ReadTextOrNull(string fullPath)
        {
            return File.Exists(fullPath)
                ? File.ReadAllText(fullPath)
                : null;
        }

        private static string FullName(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Root, relativePath));
        }

        [Test]
        public void Enumerate_MatchesTheForTestsTreeForTheSameContent()
        {
            var expectedFiles = new List<(string RelativePath, string Hash)>();
            foreach (var (relativePath, content) in Sources)
            {
                expectedFiles.Add((relativePath, Digest(content)));
            }

            var enumerated = LilToonIncludeTree.Enumerate(
                Root, ReadTextOrNull, Digest);
            var built = LilToonIncludeTree.ForTests(Root, expectedFiles);

            Assert.That(enumerated.Files, Has.Count.EqualTo(Sources.Length),
                "both include files must reach the tree");
            Assert.That(built.Files, Has.Count.EqualTo(Sources.Length));

            // Digest equality: per relative path, both trees carry the
            // same hash for the same content. The member order of a real
            // directory walk is not pinned, so the comparison is per path.
            var enumeratedByPath = ByRelativePath(enumerated);
            var builtByPath = ByRelativePath(built);
            Assert.That(
                enumeratedByPath.Keys, Is.EquivalentTo(builtByPath.Keys),
                "both trees must list the same include files");
            foreach (var entry in builtByPath)
            {
                Assert.That(
                    enumeratedByPath[entry.Key],
                    Is.EqualTo(entry.Value),
                    $"the digest of '{entry.Key}' must match the " +
                    "ForTests-built tree");
            }
        }

        [Test]
        public void TryGetRelativePath_ResolvesInsideAndRefusesOutsideAndRedirected()
        {
            var tree = LilToonIncludeTree.Enumerate(
                Root, ReadTextOrNull, Digest);

            // Inside: every member resolves to its relative form with the
            // forward-slash separators the tree stores.
            foreach (var (relativePath, _) in Sources)
            {
                Assert.That(
                    tree.TryGetRelativePath(
                        FullName(relativePath), out var resolved),
                    Is.True,
                    $"'{relativePath}' must resolve");
                Assert.That(resolved, Is.EqualTo(relativePath));
            }

            // Outside: a path on a sibling folder is no member.
            Assert.That(
                tree.TryGetRelativePath(
                    FullName(Path.Combine("..", "elsewhere.hlsl")),
                    out var outsideResolved),
                Is.False,
                "an outside path must be refused");
            Assert.That(outsideResolved, Is.Null);

            // Redirected: the pinned ordinal ruling turns a casing
            // difference into a refusal, so a redirected spelling can
            // never borrow an attested member's identity.
            Assert.That(
                tree.TryGetRelativePath(
                    FullName("LIL_COMMON.HLSL"), out var redirected),
                Is.False,
                "a case-different spelling must be refused");
            Assert.That(redirected, Is.Null);

            // A null path is refused, never resolved.
            Assert.That(
                tree.TryGetRelativePath(null, out var nullResolved),
                Is.False);
            Assert.That(nullResolved, Is.Null);
        }

        [Test]
        public void Empty_TreeHasNoFilesAndRefusesEveryPath()
        {
            var tree = LilToonIncludeTree.Empty();

            Assert.That(tree.Files, Is.Empty);
            Assert.That(
                tree.TryGetRelativePath(
                    FullName("lil_common.hlsl"), out var resolved),
                Is.False);
            Assert.That(resolved, Is.Null);
            Assert.That(
                tree.TryGetRelativePath(null, out _), Is.False);
        }

        private static Dictionary<string, string> ByRelativePath(
            LilToonIncludeTree tree)
        {
            var byPath = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (relativePath, hash) in tree.Files)
            {
                byPath[relativePath] = hash;
            }

            return byPath;
        }
    }
}
