using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The mesh output of the atlas route. The fixtures are meshes
    /// built in memory. The builder must copy every vertex attribute,
    /// bone weight, and blend shape frame. It must duplicate a shared
    /// vertex once per extra remap and write the route channel as
    /// Float32. The source mesh must stay byte for byte the same.
    /// </summary>
    [TestFixture]
    public sealed class AtlasMeshBuilderTests
    {
        private readonly List<Mesh> created = new List<Mesh>();

        private Mesh fixture;

        [SetUp]
        public void SetUp()
        {
            fixture = Track(CreateSkinnedFixture());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var mesh in created)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
            }

            created.Clear();
        }

        [Test]
        public void EveryAttributeBoneWeightAndBlendShapeSurvives()
        {
            var remaps = new List<AtlasSubmeshRemap>
            {
                new AtlasSubmeshRemap(0),
                new AtlasSubmeshRemap(1),
            };

            var built = Track(AtlasMeshBuilder.Build(fixture, remaps));

            Assert.AreEqual(4, built.vertexCount, "a kept mesh keeps its vertex count");
            CollectionAssert.AreEqual(fixture.vertices, built.vertices);
            CollectionAssert.AreEqual(fixture.normals, built.normals);
            CollectionAssert.AreEqual(fixture.tangents, built.tangents);
            CollectionAssert.AreEqual(fixture.colors32, built.colors32);
            CollectionAssert.AreEqual(fixture.uv, built.uv);
            CollectionAssert.AreEqual(fixture.uv2, built.uv2);
            CollectionAssert.AreEqual(fixture.uv3, built.uv3);
            CollectionAssert.AreEqual(fixture.uv4, built.uv4);
            Assert.AreEqual(
                IndexFormat.UInt16, built.indexFormat,
                "a small mesh keeps the source index format");
            Assert.AreEqual(fixture.subMeshCount, built.subMeshCount);
            CollectionAssert.AreEqual(fixture.GetTriangles(0), built.GetTriangles(0));
            CollectionAssert.AreEqual(fixture.GetTriangles(1), built.GetTriangles(1));
            Assert.AreEqual("AmuseAtlasFixture AMUSE atlas", built.name);
            Assert.AreEqual(fixture.bounds.center, built.bounds.center);
            Assert.AreEqual(fixture.bounds.extents, built.bounds.extents);
            AssertBindPosesMatch(fixture.bindposes, built.bindposes);
            AssertAttributeLayoutMatches(fixture, built);
            AssertBoneWeightsMatchSource(fixture, built, new[] { 0, 1, 2, 3 });
            AssertBlendShapesMatchSource(fixture, built, new[] { 0, 1, 2, 3 });
        }

        [Test]
        public void DuplicatedVertexCarriesItsBoneWeightsAndBlendShapeRows()
        {
            var remaps = new List<AtlasSubmeshRemap>
            {
                Placement(0, 16, 16),
                Placement(1, 1040, 16),
            };

            var built = Track(AtlasMeshBuilder.Build(fixture, remaps));

            Assert.AreEqual(6, built.vertexCount, "vertices 1 and 2 gain one copy each");
            AssertBoneWeightsMatchSource(fixture, built, new[] { 0, 1, 2, 3, 1, 2 });
            AssertBlendShapesMatchSource(fixture, built, new[] { 0, 1, 2, 3, 1, 2 });
        }

        [Test]
        public void Float16RouteChannelBecomesFloat32()
        {
            var source = Track(CreateHalfUvFixture());
            var remaps = new List<AtlasSubmeshRemap>
            {
                Placement(0, 16, 16),
                new AtlasSubmeshRemap(1),
            };

            var built = Track(AtlasMeshBuilder.Build(source, remaps));

            var route = FindAttribute(built, VertexAttribute.TexCoord0);
            Assert.AreEqual(
                VertexAttributeFormat.Float32, route.format,
                "the route channel becomes Float32");
            Assert.AreEqual(2, route.dimension, "the route channel keeps its dimension");
            Assert.AreEqual(6, built.vertexCount, "vertices 1 and 2 gain one copy each");

            var sourceUv = source.uv;
            Assert.AreEqual(MapUv(sourceUv[0], remaps[0]), built.uv[0], "a mapped vertex takes the map");
            Assert.AreEqual(sourceUv[3], built.uv[3], "a kept vertex keeps the decoded half value");
            Assert.AreEqual(sourceUv[1], built.uv[4], "the kept copy keeps the decoded half value");
            Assert.AreEqual(sourceUv[2], built.uv[5], "the kept copy keeps the decoded half value");
        }

        [Test]
        public void RemappedUvErrorStaysInsideTheBound()
        {
            const int pageSide = 8192;
            var source = Track(CreateBoundFixture());
            var remap = new AtlasSubmeshRemap(
                0, 4096, 4096, -4, -4, 64, 128, pageSide, pageSide);

            var built = Track(AtlasMeshBuilder.Build(
                source, new List<AtlasSubmeshRemap> { remap }));

            // The spec bound is S * 2^-25 page texels with S the
            // larger page dimension. A UV value is normalized, so the
            // texel error multiplies it by S.
            var texelBound = pageSide * Math.Pow(2.0, -25.0);
            var sourceUv = source.uv;
            var builtUv = built.uv;
            for (var i = 0; i < sourceUv.Length; i++)
            {
                var exactU =
                    (sourceUv[i].x * (double)remap.SourceWidth - remap.SourceX + remap.PageX)
                    / remap.PageWidth;
                var exactV =
                    (sourceUv[i].y * (double)remap.SourceHeight - remap.SourceY + remap.PageY)
                    / remap.PageHeight;
                Assert.LessOrEqual(
                    Math.Abs(builtUv[i].x - exactU) * pageSide, texelBound,
                    "the U error of vertex " + i + " must stay inside the bound");
                Assert.LessOrEqual(
                    Math.Abs(builtUv[i].y - exactV) * pageSide, texelBound,
                    "the V error of vertex " + i + " must stay inside the bound");
            }
        }

        [Test]
        public void SharedVertexBetweenAtlasedAndKeptSlotsIsDuplicated()
        {
            var remaps = new List<AtlasSubmeshRemap>
            {
                Placement(0, 16, 16),
                new AtlasSubmeshRemap(1),
            };

            var built = Track(AtlasMeshBuilder.Build(fixture, remaps));

            Assert.AreEqual(6, built.vertexCount, "vertices 1 and 2 gain one copy each");
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 }, built.GetTriangles(0),
                "the atlased slot keeps its indices");
            CollectionAssert.AreEqual(
                new[] { 5, 4, 3 }, built.GetTriangles(1),
                "the kept slot uses the appended copies");

            var sourceUv = fixture.uv;
            Assert.AreEqual(MapUv(sourceUv[0], remaps[0]), built.uv[0], "vertex 0 takes the map");
            Assert.AreEqual(MapUv(sourceUv[1], remaps[0]), built.uv[1], "the original slot takes the first map");
            Assert.AreEqual(MapUv(sourceUv[2], remaps[0]), built.uv[2], "the original slot takes the first map");
            Assert.AreEqual(sourceUv[3], built.uv[3], "a kept only vertex keeps its value");
            Assert.AreEqual(sourceUv[1], built.uv[4], "the copy keeps the source value of vertex 1");
            Assert.AreEqual(sourceUv[2], built.uv[5], "the copy keeps the source value of vertex 2");
        }

        [Test]
        public void SharedVertexBetweenTwoPlacementsIsDuplicated()
        {
            var first = Placement(0, 16, 16);
            var second = Placement(1, 1040, 16);
            var remaps = new List<AtlasSubmeshRemap> { first, second };

            var built = Track(AtlasMeshBuilder.Build(fixture, remaps));

            Assert.AreEqual(6, built.vertexCount, "vertices 1 and 2 gain one copy each");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, built.GetTriangles(0));
            CollectionAssert.AreEqual(
                new[] { 5, 4, 3 }, built.GetTriangles(1),
                "the second placement uses the appended copies");

            var sourceUv = fixture.uv;
            Assert.AreEqual(MapUv(sourceUv[0], first), built.uv[0], "vertex 0 takes the first map");
            Assert.AreEqual(MapUv(sourceUv[1], first), built.uv[1], "the original slot takes the first map");
            Assert.AreEqual(MapUv(sourceUv[2], first), built.uv[2], "the original slot takes the first map");
            Assert.AreEqual(MapUv(sourceUv[3], second), built.uv[3], "a vertex of the second slot only takes that map");
            Assert.AreEqual(MapUv(sourceUv[1], second), built.uv[4], "the copy takes the second map");
            Assert.AreEqual(MapUv(sourceUv[2], second), built.uv[5], "the copy takes the second map");
        }

        [Test]
        public void DuplicationPastSixteenBitsPromotesTheIndexFormat()
        {
            const int count = 33000;
            var mesh = new Mesh();
            mesh.name = "AmuseWideFixture";
            var positions = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[count];
            for (var i = 0; i < count; i++)
            {
                positions[i] = new Vector3(i % 1024, (i / 1024) * 0.5f, i * 0.001f);
                uvs[i] = new Vector2((i % 100) / 100.0f, (i % 97) / 97.0f);
                triangles[i] = i;
            }

            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(triangles, 0);
            mesh.SetTriangles(triangles, 1);
            mesh.RecalculateBounds();
            Track(mesh);

            Assert.AreEqual(
                IndexFormat.UInt16, mesh.indexFormat,
                "fixture precondition: the source fits in sixteen bits");

            var remaps = new List<AtlasSubmeshRemap>
            {
                Placement(0, 16, 16),
                Placement(1, 1040, 16),
            };

            var built = Track(AtlasMeshBuilder.Build(mesh, remaps));

            Assert.AreEqual(66000, built.vertexCount, "every vertex gains one copy");
            Assert.AreEqual(
                IndexFormat.UInt32, built.indexFormat,
                "the output passes 65535 vertices");
            Assert.AreEqual(2, built.subMeshCount);
            Assert.AreEqual(count, built.GetTriangles(0).Length);
            Assert.AreEqual(count, built.GetTriangles(1).Length);
            Assert.AreEqual(
                count + 1, built.GetTriangles(1)[1],
                "the second submesh uses the appended copy of vertex 1");
        }

        [Test]
        public void SourceMeshIsUnchanged()
        {
            var attributesBefore = fixture.GetVertexAttributes();
            var streamsBefore = VertexStreams(fixture);
            var indicesBefore = IndexWords(fixture);
            var remaps = new List<AtlasSubmeshRemap>
            {
                Placement(0, 16, 16),
                Placement(1, 1040, 16),
            };

            var built = Track(AtlasMeshBuilder.Build(fixture, remaps));

            Assert.AreEqual(6, built.vertexCount, "the build walked the duplication path");
            CollectionAssert.AreEqual(
                attributesBefore, fixture.GetVertexAttributes(),
                "the attributes are the same");
            var streamsAfter = VertexStreams(fixture);
            Assert.AreEqual(streamsBefore.Count, streamsAfter.Count);
            for (var stream = 0; stream < streamsBefore.Count; stream++)
            {
                CollectionAssert.AreEqual(
                    streamsBefore[stream], streamsAfter[stream],
                    "the bytes of stream " + stream + " are the same");
            }

            CollectionAssert.AreEqual(
                indicesBefore, IndexWords(fixture),
                "the index words are the same");
        }

        /// <summary>Keeps a mesh alive until the tear down destroys it.</summary>
        private Mesh Track(Mesh mesh)
        {
            created.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// A mapped remap of one submesh. The source rectangle sits at
        /// the placement origin with a border that crosses the source
        /// edge, so the source origin is negative.
        /// </summary>
        private static AtlasSubmeshRemap Placement(int submesh, int pageX, int pageY)
        {
            return new AtlasSubmeshRemap(
                submesh, 1024, 1024, -8, -8, pageX, pageY, 2048, 2048);
        }

        /// <summary>
        /// The expected route channel value. The formula runs in
        /// double precision and narrows to float at the end.
        /// </summary>
        private static Vector2 MapUv(Vector2 uv, AtlasSubmeshRemap remap)
        {
            return new Vector2(
                (float)((uv.x * (double)remap.SourceWidth - remap.SourceX + remap.PageX)
                    / remap.PageWidth),
                (float)((uv.y * (double)remap.SourceHeight - remap.SourceY + remap.PageY)
                    / remap.PageHeight));
        }

        /// <summary>
        /// A skinned quad with positions, normals, tangents, colors,
        /// four UV channels, bone weights, and two blend shapes with
        /// two frames each. Submesh 0 and submesh 1 share vertices
        /// 1 and 2.
        /// </summary>
        private static Mesh CreateSkinnedFixture()
        {
            var mesh = new Mesh();
            mesh.name = "AmuseAtlasFixture";
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0.1f),
                new Vector3(0.5f, -0.5f, 0.2f),
                new Vector3(-0.5f, 0.5f, 0.3f),
                new Vector3(0.5f, 0.5f, 0.4f),
            };
            mesh.normals = new[]
            {
                new Vector3(0f, 0f, -1f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, 0f, 1f),
            };
            mesh.tangents = new[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(0f, 1f, 0f, -1f),
                new Vector4(-1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 1f, 1f),
            };
            mesh.colors32 = new[]
            {
                new Color32(10, 20, 30, 255),
                new Color32(40, 50, 60, 128),
                new Color32(70, 80, 90, 200),
                new Color32(100, 110, 120, 7),
            };
            mesh.uv = new[]
            {
                new Vector2(0.1f, 0.2f),
                new Vector2(0.3f, 0.4f),
                new Vector2(0.5f, 0.6f),
                new Vector2(0.7f, 0.8f),
            };
            mesh.uv2 = new[]
            {
                new Vector2(0.01f, 0.02f),
                new Vector2(0.03f, 0.04f),
                new Vector2(0.05f, 0.06f),
                new Vector2(0.07f, 0.08f),
            };
            mesh.uv3 = new[]
            {
                new Vector2(0.11f, 0.12f),
                new Vector2(0.13f, 0.14f),
                new Vector2(0.15f, 0.16f),
                new Vector2(0.17f, 0.18f),
            };
            mesh.uv4 = new[]
            {
                new Vector2(0.21f, 0.22f),
                new Vector2(0.23f, 0.24f),
                new Vector2(0.25f, 0.26f),
                new Vector2(0.27f, 0.28f),
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 2, 1, 3 }, 1);

            var bonesPerVertex = new NativeArray<byte>(4, Allocator.Temp);
            bonesPerVertex[0] = 2;
            bonesPerVertex[1] = 1;
            bonesPerVertex[2] = 2;
            bonesPerVertex[3] = 1;
            var weights = new NativeArray<BoneWeight1>(6, Allocator.Temp);
            weights[0] = new BoneWeight1 { boneIndex = 0, weight = 0.7f };
            weights[1] = new BoneWeight1 { boneIndex = 1, weight = 0.3f };
            weights[2] = new BoneWeight1 { boneIndex = 1, weight = 1f };
            weights[3] = new BoneWeight1 { boneIndex = 2, weight = 0.4f };
            weights[4] = new BoneWeight1 { boneIndex = 0, weight = 0.6f };
            weights[5] = new BoneWeight1 { boneIndex = 3, weight = 1f };
            mesh.SetBoneWeights(bonesPerVertex, weights);
            bonesPerVertex.Dispose();
            weights.Dispose();

            mesh.bindposes = new[]
            {
                Matrix4x4.TRS(new Vector3(1f, 0f, 0f), Quaternion.identity, Vector3.one),
                Matrix4x4.TRS(
                    new Vector3(0f, 2f, 0f),
                    Quaternion.Euler(0f, 30f, 0f),
                    new Vector3(1f, 1f, 2f)),
                Matrix4x4.TRS(
                    new Vector3(0f, 0f, 3f),
                    Quaternion.Euler(10f, 0f, 0f),
                    Vector3.one),
                Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(2f, 2f, 2f)),
            };

            mesh.AddBlendShapeFrame("AmuseShapeA", 50f, Deltas(1), Deltas(2), Deltas(3));
            mesh.AddBlendShapeFrame("AmuseShapeA", 100f, Deltas(4), Deltas(5), Deltas(6));
            mesh.AddBlendShapeFrame("AmuseShapeB", 25f, Deltas(7), Deltas(8), Deltas(9));
            mesh.AddBlendShapeFrame("AmuseShapeB", 100f, Deltas(10), Deltas(11), Deltas(12));
            mesh.bounds = new Bounds(
                new Vector3(0f, 0f, 0.25f), new Vector3(1.1f, 1.2f, 0.6f));
            return mesh;
        }

        /// <summary>
        /// A mesh whose route channel is Float16. The UV values are
        /// exact half values, so the Float32 conversion keeps them.
        /// </summary>
        private static Mesh CreateHalfUvFixture()
        {
            var halfUv = new[]
            {
                new Vector2(0.5f, 0.25f),
                new Vector2(0.75f, 0.125f),
                new Vector2(0.0625f, 0.625f),
                new Vector2(0f, 1f),
            };

            var writable = Mesh.AllocateWritableMeshData(1);
            var data = writable[0];
            data.SetVertexBufferParams(
                4,
                new VertexAttributeDescriptor(
                    VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(
                    VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, 1));
            var positions = data.GetVertexData<Vector3>(0);
            positions[0] = new Vector3(0f, 0f, 0f);
            positions[1] = new Vector3(1f, 0f, 0.1f);
            positions[2] = new Vector3(0f, 1f, 0.2f);
            positions[3] = new Vector3(1f, 1f, 0.3f);
            var uvs = data.GetVertexData<ushort>(1);
            for (var i = 0; i < 4; i++)
            {
                uvs[2 * i] = ToHalf(halfUv[i].x);
                uvs[2 * i + 1] = ToHalf(halfUv[i].y);
            }

            data.SetIndexBufferParams(6, IndexFormat.UInt16);
            var indices = data.GetIndexData<ushort>();
            var words = new ushort[] { 0, 1, 2, 2, 1, 3 };
            for (var i = 0; i < words.Length; i++)
            {
                indices[i] = words[i];
            }

            data.subMeshCount = 2;
            data.SetSubMesh(0, new SubMeshDescriptor(0, 3, MeshTopology.Triangles));
            data.SetSubMesh(1, new SubMeshDescriptor(3, 3, MeshTopology.Triangles));

            var mesh = new Mesh();
            mesh.name = "AmuseHalfFixture";
            Mesh.ApplyAndDisposeWritableMeshData(
                writable, mesh, MeshUpdateFlags.DontRecalculateBounds);
            mesh.bounds = new Bounds(
                new Vector3(0.5f, 0.5f, 0.15f), new Vector3(1f, 1f, 0.3f));
            return mesh;
        }

        /// <summary>
        /// A fan of 64 vertices with UV values that spread over the
        /// source rectangle, including values that are not dyadic.
        /// </summary>
        private static Mesh CreateBoundFixture()
        {
            const int count = 64;
            var mesh = new Mesh();
            mesh.name = "AmuseBoundFixture";
            var positions = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[3 * (count - 2)];
            for (var i = 0; i < count; i++)
            {
                positions[i] = new Vector3(i % 8, i / 8, i * 0.01f);
                uvs[i] = new Vector2(
                    ((i * 37) % 256) / 256.0f,
                    ((i * 53) % 128) / 128.0f);
                if (i >= 2)
                {
                    triangles[3 * (i - 2)] = 0;
                    triangles[3 * (i - 2) + 1] = i - 1;
                    triangles[3 * (i - 2) + 2] = i;
                }
            }

            uvs[7] = new Vector2(0.1234567f, 0.7654321f);
            uvs[11] = new Vector2(4095f / 4096f, 1f / 3f);

            mesh.vertices = positions;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3[] Deltas(int seed)
        {
            return new[]
            {
                new Vector3(seed * 0.01f, (seed + 1) * 0.02f, (seed + 2) * 0.03f),
                new Vector3((seed + 3) * 0.01f, (seed + 4) * 0.02f, (seed + 5) * 0.03f),
                new Vector3((seed + 6) * 0.01f, (seed + 7) * 0.02f, (seed + 8) * 0.03f),
                new Vector3((seed + 9) * 0.01f, (seed + 10) * 0.02f, (seed + 11) * 0.03f),
            };
        }

        private static void AssertAttributeLayoutMatches(Mesh source, Mesh built)
        {
            var expected = source.GetVertexAttributes();
            var actual = built.GetVertexAttributes();
            Assert.AreEqual(expected.Length, actual.Length, "the attribute count is the same");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].attribute, actual[i].attribute, "attribute " + i);
                Assert.AreEqual(expected[i].dimension, actual[i].dimension, "dimension of attribute " + i);
                Assert.AreEqual(expected[i].stream, actual[i].stream, "stream of attribute " + i);
                if (expected[i].attribute == VertexAttribute.TexCoord0)
                {
                    Assert.AreEqual(
                        VertexAttributeFormat.Float32, actual[i].format,
                        "the route channel is Float32");
                }
                else
                {
                    Assert.AreEqual(expected[i].format, actual[i].format, "format of attribute " + i);
                }
            }
        }

        private static void AssertBindPosesMatch(Matrix4x4[] expected, Matrix4x4[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "the bind pose count is the same");
            for (var i = 0; i < expected.Length; i++)
            {
                for (var row = 0; row < 4; row++)
                {
                    Assert.AreEqual(
                        expected[i].GetRow(row), actual[i].GetRow(row),
                        "bind pose " + i + " row " + row);
                }
            }
        }

        /// <summary>
        /// Compares the bone weights of every output vertex against
        /// the source vertex that origin names.
        /// </summary>
        private static void AssertBoneWeightsMatchSource(
            Mesh source,
            Mesh built,
            int[] origin)
        {
            var expectedCounts = source.GetBonesPerVertex();
            var expectedWeights = source.GetAllBoneWeights();
            var counts = built.GetBonesPerVertex();
            var weights = built.GetAllBoneWeights();
            Assert.AreEqual(origin.Length, counts.Length, "every vertex has a bone count");
            var sourceOffsets = new int[expectedCounts.Length + 1];
            for (var v = 0; v < expectedCounts.Length; v++)
            {
                sourceOffsets[v + 1] = sourceOffsets[v] + expectedCounts[v];
            }

            var cursor = 0;
            for (var j = 0; j < origin.Length; j++)
            {
                var src = origin[j];
                Assert.AreEqual(
                    expectedCounts[src], counts[j], "the bone count of vertex " + j);
                for (var w = 0; w < counts[j]; w++)
                {
                    Assert.AreEqual(
                        expectedWeights[sourceOffsets[src] + w].boneIndex,
                        weights[cursor + w].boneIndex,
                        "the bone index of vertex " + j + " weight " + w);
                    Assert.AreEqual(
                        expectedWeights[sourceOffsets[src] + w].weight,
                        weights[cursor + w].weight,
                        "the bone weight of vertex " + j + " weight " + w);
                }

                cursor += counts[j];
            }
        }

        /// <summary>
        /// Compares every blend shape frame of every output vertex
        /// against the source vertex that origin names.
        /// </summary>
        private static void AssertBlendShapesMatchSource(
            Mesh source,
            Mesh built,
            int[] origin)
        {
            Assert.AreEqual(
                source.blendShapeCount, built.blendShapeCount,
                "the blend shape count is the same");
            for (var shape = 0; shape < source.blendShapeCount; shape++)
            {
                var name = source.GetBlendShapeName(shape);
                Assert.AreEqual(name, built.GetBlendShapeName(shape), "shape " + shape);
                var frames = source.GetBlendShapeFrameCount(shape);
                Assert.AreEqual(frames, built.GetBlendShapeFrameCount(shape), "frames of " + name);
                var sourceVertices = new Vector3[source.vertexCount];
                var sourceNormals = new Vector3[source.vertexCount];
                var sourceTangents = new Vector3[source.vertexCount];
                var builtVertices = new Vector3[built.vertexCount];
                var builtNormals = new Vector3[built.vertexCount];
                var builtTangents = new Vector3[built.vertexCount];
                for (var frame = 0; frame < frames; frame++)
                {
                    Assert.AreEqual(
                        source.GetBlendShapeFrameWeight(shape, frame),
                        built.GetBlendShapeFrameWeight(shape, frame),
                        "the weight of frame " + frame + " of " + name);
                    source.GetBlendShapeFrameVertices(
                        shape, frame, sourceVertices, sourceNormals, sourceTangents);
                    built.GetBlendShapeFrameVertices(
                        shape, frame, builtVertices, builtNormals, builtTangents);
                    for (var j = 0; j < origin.Length; j++)
                    {
                        var label = "the delta of vertex " + j + " in frame " + frame + " of " + name;
                        Assert.AreEqual(sourceVertices[origin[j]], builtVertices[j], label);
                        Assert.AreEqual(sourceNormals[origin[j]], builtNormals[j], label);
                        Assert.AreEqual(sourceTangents[origin[j]], builtTangents[j], label);
                    }
                }
            }
        }

        private static VertexAttributeDescriptor FindAttribute(
            Mesh mesh,
            VertexAttribute attribute)
        {
            foreach (var descriptor in mesh.GetVertexAttributes())
            {
                if (descriptor.attribute == attribute)
                {
                    return descriptor;
                }
            }

            Assert.Fail("the mesh lacks the attribute " + attribute);
            return default;
        }

        /// <summary>Encodes an exact half value. The fixture values are exact.</summary>
        private static ushort ToHalf(float value)
        {
            if (value == 0f)
            {
                return 0;
            }

            var bits = BitConverter.SingleToInt32Bits(value);
            Assert.AreEqual(
                0, bits & 0x1FFF,
                "the fixture uses half exact values: " + value);
            var exponent = ((bits >> 23) & 0xFF) - 112;
            Assert.Greater(exponent, 0, "the fixture uses normal half values: " + value);
            Assert.Less(exponent, 31, "the fixture uses normal half values: " + value);
            return (ushort)(
                ((bits >> 16) & 0x8000)
                | (exponent << 10)
                | ((bits & 0x7FFFFF) >> 13));
        }

        private static List<byte[]> VertexStreams(Mesh mesh)
        {
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                var streams = new List<byte[]>();
                for (var s = 0; s < data[0].vertexBufferCount; s++)
                {
                    streams.Add(data[0].GetVertexData<byte>(s).ToArray());
                }

                return streams;
            }
        }

        private static ushort[] IndexWords(Mesh mesh)
        {
            using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
            {
                return data[0].GetIndexData<ushort>().ToArray();
            }
        }
    }
}
