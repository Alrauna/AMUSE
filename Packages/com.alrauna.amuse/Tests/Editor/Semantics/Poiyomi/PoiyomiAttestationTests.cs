using System;
using System.IO;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The source digest memo joins the per-build clear list. The build
    /// boundary drops every memoized digest, so the next attestation read
    /// re-reads the shader asset from disk instead of trusting a digest
    /// memoized by an earlier build.
    /// </summary>
    public sealed class PoiyomiAttestationTests
    {
        private const string TempFolder = "Assets/AmuseTests_Temp";

        // A synthetic Poiyomi-shaped source. The memo hashes raw file text,
        // so the fixture needs no installed Poiyomi package.
        private const string FixtureSource =
            "Shader \"Poiyomi/Toon/AttestationFixture\"\n" +
            "{\n" +
            "    SubShader\n" +
            "    {\n" +
            "        Pass\n" +
            "        {\n" +
            "            CGPROGRAM\n" +
            "            #pragma vertex vert\n" +
            "            #pragma fragment frag\n" +
            "            #include \"UnityCG.cginc\"\n" +
            "            float4 vert(float4 v : POSITION) : SV_POSITION\n" +
            "            {\n" +
            "                return UnityObjectToClipPos(v);\n" +
            "            }\n" +
            "            fixed4 frag() : SV_Target\n" +
            "            {\n" +
            "                return fixed4(1, 0, 0, 1);\n" +
            "            }\n" +
            "            ENDCG\n" +
            "        }\n" +
            "    }\n" +
            "}\n";

        [Test]
        public void TheSourceHashMemoClearsAtTheBuildBoundary()
        {
            Directory.CreateDirectory(TempFolder);
            var fixturePath = Path.Combine(
                TempFolder,
                "attestation_memo_" + Guid.NewGuid().ToString("N") + ".shader");
            try
            {
                File.WriteAllText(fixturePath, FixtureSource);
                // The platform stores file times with microsecond
                // precision. Round the stamp through the same write path
                // first. The later set then restores a value the file
                // system returns unchanged.
                File.SetLastWriteTimeUtc(
                    fixturePath, File.GetLastWriteTimeUtc(fixturePath));
                var stamp = File.GetLastWriteTimeUtc(fixturePath);

                var primed = PoiyomiSourceAttestation
                    .TryReadNormalizedSourceHashCached(
                        fixturePath, out var seedDigest);
                Assert.That(primed, Is.True);

                var dropped = PoiyomiSourceAttestation.ClearSourceHashMemo();
                Assert.That(
                    dropped,
                    Is.True,
                    "the clear must drop the primed memo entry");

                // The content changes while the write time stays pinned.
                // A stale memo would return the seed digest. A cleared
                // memo must re-read the source and hash the new text.
                var editedSource = FixtureSource + "// after clear\n";
                File.WriteAllText(fixturePath, editedSource);
                File.SetLastWriteTimeUtc(fixturePath, stamp);

                var second = PoiyomiSourceAttestation
                    .TryReadNormalizedSourceHashCached(
                        fixturePath, out var reDigest);
                Assert.That(second, Is.True);
                Assert.That(
                    reDigest,
                    Is.EqualTo(PoiyomiSourceAttestation
                        .ComputeNormalizedSourceHash(editedSource)),
                    "a cleared memo must re-read the source from disk");
            }
            finally
            {
                File.Delete(fixturePath);
            }
        }
    }
}
