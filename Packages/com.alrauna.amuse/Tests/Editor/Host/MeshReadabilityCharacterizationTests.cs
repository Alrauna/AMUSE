using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Records whether a mesh imported with Read/Write Enabled off can be read
    /// from Editor code. That default is what real avatar models ship with, so
    /// the answer bounds how much of a real avatar renderer analysis can reach,
    /// and it decides whether the production read path needs any unreadable-mesh
    /// handling at all.
    /// <para>
    /// Unity documents that mesh data access is allowed from the Editor outside
    /// the game/rendering loop even when <c>Mesh.isReadable</c> is false, which
    /// is the regime both EditMode tests and a build-time NDMF pass run in. This
    /// test observes that claim in this project rather than assuming it, and it
    /// runs before any production read path is written so the policy follows the
    /// observation rather than the reverse.
    /// </para>
    /// <para>
    /// Each operation is exercised exactly once, inside its own
    /// <c>Assert.DoesNotThrow</c>, so a failure names the exact operation and
    /// NUnit reports the exact exception type. Nothing is re-read afterwards: a
    /// test characterizing whether a read can fail must not depend on repeating
    /// that read. No AMUSE production code is involved.
    /// </para>
    /// </summary>
    public sealed class MeshReadabilityCharacterizationTests
    {
        private const string TempFolder = "Assets/AmuseTests_MeshReadability";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_MeshReadability");
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        /// <summary>
        /// A one-triangle Wavefront OBJ with UV0. Generated rather than
        /// committed: deterministic, redistributable, and it leaves no binary
        /// fixture behind.
        /// </summary>
        private static Mesh ImportNonReadableTriangle()
        {
            var path = TempFolder + "/triangle.obj";
            File.WriteAllText(
                path,
                "o amuse_triangle\n" +
                "v 0.0 0.0 0.0\n" +
                "v 1.0 0.0 0.0\n" +
                "v 0.0 1.0 0.0\n" +
                "vt 0.6 0.6\n" +
                "vt 0.9 0.6\n" +
                "vt 0.6 0.9\n" +
                "f 1/1 2/2 3/3\n");

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.isReadable = false;
            importer.SaveAndReimport();

            // An imported model is a GameObject root with the Mesh as a
            // sub-asset, so the typed load can miss it depending on sub-asset
            // ordering; scan when it does.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null)
            {
                return mesh;
            }

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Mesh loaded)
                {
                    return loaded;
                }
            }

            return null;
        }

        [Test]
        public void NonReadableImportedMeshCanBeReadFromEditorCode()
        {
            var mesh = ImportNonReadableTriangle();
            Assert.That(mesh, Is.Not.Null, "The generated OBJ must import.");
            Assert.That(
                mesh.isReadable,
                Is.False,
                "The fixture must actually be non-readable, or it proves nothing.");

            var vertexCount = mesh.vertexCount;
            Assert.That(
                vertexCount,
                Is.GreaterThan(0),
                "The imported triangle must carry vertices.");

            Vector3[] positions = null;
            Vector2[] uv = null;
            int[] indices = null;

            Assert.DoesNotThrow(
                () => positions = mesh.vertices,
                "Mesh.vertices threw on a non-readable mesh in the Editor.");
            Assert.DoesNotThrow(
                () => uv = mesh.uv,
                "Mesh.uv threw on a non-readable mesh in the Editor.");
            Assert.DoesNotThrow(
                () => indices = mesh.GetIndices(0),
                "Mesh.GetIndices threw on a non-readable mesh in the Editor.");

            Assert.That(
                positions, Is.Not.Null.And.Length.EqualTo(vertexCount),
                "A non-readable mesh must yield complete positions or throw, " +
                "never a short or empty array.");
            Assert.That(
                uv, Is.Not.Null.And.Length.EqualTo(vertexCount),
                "The OBJ carries UV0, so a complete UV array is expected.");
            Assert.That(
                indices, Is.Not.Null, "GetIndices must not yield null.");
            Assert.That(
                indices.Length,
                Is.EqualTo(3),
                "One face means one triangle's worth of indices.");

            TestContext.WriteLine(
                "Observed: isReadable=False, vertexCount=" + vertexCount +
                ", vertices=" + positions.Length +
                ", uv=" + uv.Length +
                ", indices=" + indices.Length +
                ", topology=" + mesh.GetTopology(0) +
                ", subMeshCount=" + mesh.subMeshCount);
        }
    }
}
