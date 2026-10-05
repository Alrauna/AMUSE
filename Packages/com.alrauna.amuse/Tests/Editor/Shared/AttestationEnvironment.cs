using System.IO;
using Alrauna.Amuse.Editor.Semantics.LilToon;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// The shared, filesystem-free attestation environment for the two
    /// lilToon attestation suites: a synthetic project root, the lilToon
    /// shader directory inside it, the include tree pinned for
    /// canonicalization, and the canonicalizer entry over all three. Path
    /// arithmetic only; nothing here touches the filesystem.
    /// </summary>
    internal static class AttestationEnvironment
    {
        internal static readonly string ProjectRoot =
            Path.Combine(Path.GetTempPath(), "AmuseTests", "Project");

        internal static readonly string ShaderDir = Path.Combine(
            ProjectRoot, "Packages", "jp.lilxyzw.liltoon", "Shader");

        /// <summary>
        /// Builds the pinned include tree: the base <c>lil_common.hlsl</c>
        /// stamp plus any suite-specific entries, in call order.
        /// </summary>
        internal static LilToonIncludeTree Tree(
            params (string RelativePath, string Stamp)[] additionalIncludes)
        {
            var entries =
                new (string RelativePath, string Stamp)[
                    additionalIncludes.Length + 1];
            entries[0] = ("lil_common.hlsl", "11");
            for (var i = 0; i < additionalIncludes.Length; i++)
            {
                entries[i + 1] = additionalIncludes[i];
            }

            return LilToonIncludeTree.ForTests(
                Path.Combine(ShaderDir, "Includes"), entries);
        }

        /// <summary>
        /// Canonicalizes against the shared root and shader directory. The
        /// project root is an explicit argument; canonicalization must
        /// never consult the process working directory.
        /// </summary>
        internal static string Canon(
            string source,
            params (string RelativePath, string Stamp)[] additionalIncludes)
        {
            return LilToonSourceAttestation.Canonicalize(
                source, ShaderDir, ProjectRoot, Tree(additionalIncludes));
        }
    }
}
