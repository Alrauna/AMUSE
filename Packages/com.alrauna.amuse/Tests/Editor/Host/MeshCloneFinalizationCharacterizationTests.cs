using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Characterizes the one mechanism the alpha-separation vertical slice needs
    /// before its second animator-services window: retain full-fidelity mesh
    /// state during preparation, then finalize only the submesh/index layout once
    /// slot-local validation has fixed the surviving candidate set.
    ///
    /// <para>
    /// The candidate under test is deliberately the narrowest one available:
    /// <c>UnityEngine.Object.Instantiate(mesh)</c>, Unity's own native clone, plus
    /// a layout-only rewrite of the clone's submesh and index data. If that
    /// preserves the mesh state a skinned VRChat avatar renderer depends on,
    /// AMUSE needs no mesh copier, no reconstruction snapshot, and no late live
    /// read.
    /// </para>
    ///
    /// <para>
    /// This characterizes Unity, not AMUSE: no AMUSE production type
    /// participates, and nothing here is a contract AMUSE offers. Its scope is
    /// the fields named in <see cref="Describe"/> and asserted below; vertex
    /// layouts the fixture does not carry — notably modern variable bone weights
    /// via <c>GetAllBoneWeights</c>/<c>SetBoneWeights</c>, and UV channels other
    /// than 0, 3 and 7 — are deliberately out of scope and remain unmeasured.
    /// </para>
    ///
    /// <para>
    /// The fixture is adversarial rather than broad. It carries mixed-dimension
    /// UV channels, two unreferenced vertices, a multi-frame blend shape,
    /// deliberately authored mesh-level and per-submesh bounds, and a split
    /// submesh whose descriptor has a nonzero <c>baseVertex</c>, because those
    /// are the facts a plausible implementation drops silently.
    /// </para>
    /// </summary>
    public sealed class MeshCloneFinalizationCharacterizationTests
    {
        private readonly List<Object> tracked = new List<Object>();

        private T Track<T>(T value) where T : Object
        {
            tracked.Add(value);
            return value;
        }

        [TearDown]
        public void TearDown()
        {
            // Runs after a failing assertion too, so no synthetic mesh survives
            // a red test.
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                if (tracked[index] != null)
                {
                    Object.DestroyImmediate(tracked[index]);
                }
            }

            tracked.Clear();
        }

        private const int VertexCount = 9;

        /// <summary>
        /// Source submesh 0, which no separation decision touches. Its descriptor
        /// must survive finalization untouched.
        /// </summary>
        private static readonly int[] UntouchedTriangle = { 0, 1, 2 };

        /// <summary>
        /// Source submesh 1 is the split one, and it is authored with
        /// <c>baseVertex 4</c>, so its stored indices are local: {0,1,2,1,2,3}.
        /// These are the effective vertices those local indices resolve to.
        /// </summary>
        private const int SplitSubmeshBaseVertex = 4;

        private static readonly int[] SplitSubmeshLocalIndices =
            { 0, 1, 2, 1, 2, 3 };

        /// <summary>The split submesh's triangle that stays on alpha.</summary>
        private static readonly int[] PreservedAlphaTriangle = { 4, 5, 6 };

        /// <summary>The split submesh's triangle that moves to opaque.</summary>
        private static readonly int[] SeparatedOpaqueTriangle = { 5, 6, 7 };

        /// <summary>
        /// Authored per-submesh bounds, unrelated to the geometry so that any
        /// implicit recalculation is visible.
        /// </summary>
        private static Bounds AuthoredSubmeshBounds(int submesh)
        {
            return new Bounds(
                new Vector3(100 + submesh, 200 + submesh, 300 + submesh),
                new Vector3(2f, 2f, 2f));
        }

        private static readonly Bounds AuthoredMeshBounds = new Bounds(
            new Vector3(10f, 20f, 30f), new Vector3(40f, 50f, 60f));

        /// <summary>
        /// A skinned-avatar-shaped mesh. Vertices 3 and 8 are referenced by no
        /// triangle, so a finalization that compacts or reindexes vertices cannot
        /// pass unnoticed.
        /// </summary>
        private Mesh CreateAdversarialSource()
        {
            var mesh = Track(new Mesh { name = "amuse adversarial source" });

            // Set before any index data: a UInt32 buffer on a 9-vertex mesh is
            // not what Unity would choose on its own, so preserving it is a real
            // observation rather than a default agreeing with a default.
            mesh.indexFormat = IndexFormat.UInt32;

            var positions = new Vector3[VertexCount];
            var normals = new Vector3[VertexCount];
            var tangents = new Vector4[VertexCount];
            var colors = new Color32[VertexCount];
            var uv0 = new Vector2[VertexCount];
            var uv3 = new Vector3[VertexCount];
            var uv7 = new Vector4[VertexCount];
            var weights = new BoneWeight[VertexCount];

            for (var index = 0; index < VertexCount; index++)
            {
                positions[index] = new Vector3(index, index * 0.5f, -index);
                normals[index] = new Vector3(0f, 1f, 0f);
                tangents[index] = new Vector4(1f, 0f, 0f, -1f);
                colors[index] = new Color32(
                    (byte)index, (byte)(255 - index), 7, 200);
                uv0[index] = new Vector2(index * 0.125f, 0.25f);
                uv3[index] = new Vector3(index, 1f, 2f);
                uv7[index] = new Vector4(index, 3f, 4f, 5f);
                weights[index] = new BoneWeight
                {
                    boneIndex0 = 0,
                    boneIndex1 = 1,
                    weight0 = 0.75f,
                    weight1 = 0.25f,
                };
            }

            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.colors32 = colors;

            // Mixed dimensions across channels. A copier that assumes Vector2
            // everywhere loses the extra components without throwing.
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(3, uv3);
            mesh.SetUVs(7, uv7);

            mesh.boneWeights = weights;
            mesh.bindposes = new[]
            {
                Matrix4x4.Translate(new Vector3(1f, 2f, 3f)),
                Matrix4x4.Scale(new Vector3(2f, 2f, 2f)),
            };

            mesh.subMeshCount = 2;
            mesh.SetIndices(
                UntouchedTriangle, MeshTopology.Triangles, 0,
                calculateBounds: false, baseVertex: 0);

            // The submesh that will be split carries a nonzero base vertex, so
            // an implementation that ignores base vertex produces triangles over
            // the wrong vertices rather than merely a different representation.
            mesh.SetIndices(
                SplitSubmeshLocalIndices, MeshTopology.Triangles, 1,
                calculateBounds: false, baseVertex: SplitSubmeshBaseVertex);

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var descriptor = mesh.GetSubMesh(submesh);
                descriptor.bounds = AuthoredSubmeshBounds(submesh);
                mesh.SetSubMesh(
                    submesh, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            }

            // Two shapes, one of them multi-frame, because per-frame weights and
            // per-frame deltas are stored separately.
            mesh.AddBlendShapeFrame(
                "shape one", 50f, Deltas(1f), Deltas(2f), Deltas(3f));
            mesh.AddBlendShapeFrame(
                "shape one", 100f, Deltas(4f), Deltas(5f), Deltas(6f));
            mesh.AddBlendShapeFrame(
                "shape two", 100f, Deltas(7f), Deltas(8f), Deltas(9f));

            // Authored last and deliberately unrelated to the geometry.
            mesh.bounds = AuthoredMeshBounds;

            return mesh;
        }

        private static Vector3[] Deltas(float seed)
        {
            var deltas = new Vector3[VertexCount];
            for (var index = 0; index < VertexCount; index++)
            {
                deltas[index] = new Vector3(seed, seed + index, seed - index);
            }

            return deltas;
        }

        /// <summary>
        /// A selected structural digest of the facts this investigation claims
        /// must survive. It is not a byte comparison of the mesh, and a match
        /// means only that the characterized fields agree.
        /// </summary>
        private static string Describe(Mesh mesh)
        {
            var parts = new List<string>
            {
                "vertexCount=" + mesh.vertexCount,
                "indexFormat=" + mesh.indexFormat,
                "subMeshCount=" + mesh.subMeshCount,
                "bounds=" + Format(mesh.bounds),
                "positions=" + Join(mesh.vertices),
                "normals=" + Join(mesh.normals),
                "tangents=" + Join(mesh.tangents),
                "colors32=" + Join(mesh.colors32),
                "uv0=" + Join(ReadUv0(mesh)),
                "uv3=" + Join(ReadUv3(mesh)),
                "uv7=" + Join(ReadUv7(mesh)),
                "boneWeights=" + Join(mesh.boneWeights.Select(weight =>
                    weight.boneIndex0 + ":" + weight.weight0.ToString("R") + "," +
                    weight.boneIndex1 + ":" + weight.weight1.ToString("R"))),
                "bindposes=" + Join(mesh.bindposes),
            };

            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                parts.Add(
                    "submesh" + submesh + "=" + DescribeDescriptor(mesh, submesh) +
                    " effective=[" + Join(mesh.GetIndices(submesh)) + "]" +
                    " stored=[" + Join(mesh.GetIndices(submesh, false)) + "]");
            }

            parts.Add("blendShapes=" + DescribeBlendShapes(mesh));
            return string.Join("\n", parts);
        }

        /// <summary>
        /// The full <see cref="SubMeshDescriptor"/>, which effective indices
        /// alone do not reveal.
        /// </summary>
        private static string DescribeDescriptor(Mesh mesh, int submesh)
        {
            var descriptor = mesh.GetSubMesh(submesh);
            return
                "topology=" + descriptor.topology +
                " indexStart=" + descriptor.indexStart +
                " indexCount=" + descriptor.indexCount +
                " baseVertex=" + descriptor.baseVertex +
                " firstVertex=" + descriptor.firstVertex +
                " vertexCount=" + descriptor.vertexCount +
                " bounds=" + Format(descriptor.bounds);
        }

        private static string DescribeBlendShapes(Mesh mesh)
        {
            var described = new List<string>();
            for (var shape = 0; shape < mesh.blendShapeCount; shape++)
            {
                var frames = mesh.GetBlendShapeFrameCount(shape);
                for (var frame = 0; frame < frames; frame++)
                {
                    var deltaVertices = new Vector3[mesh.vertexCount];
                    var deltaNormals = new Vector3[mesh.vertexCount];
                    var deltaTangents = new Vector3[mesh.vertexCount];
                    mesh.GetBlendShapeFrameVertices(
                        shape, frame, deltaVertices, deltaNormals, deltaTangents);

                    described.Add(
                        mesh.GetBlendShapeName(shape) + "#" + frame + "@" +
                        mesh.GetBlendShapeFrameWeight(shape, frame)
                            .ToString("R") + ":" +
                        Join(deltaVertices) + "|" + Join(deltaNormals) + "|" +
                        Join(deltaTangents));
                }
            }

            return string.Join(";", described);
        }

        // Mesh.GetUVs has no generic overload: the channel's dimension is part
        // of the call, which is exactly the fact under test.
        private static List<Vector2> ReadUv0(Mesh mesh)
        {
            var values = new List<Vector2>();
            mesh.GetUVs(0, values);
            return values;
        }

        private static List<Vector3> ReadUv3(Mesh mesh)
        {
            var values = new List<Vector3>();
            mesh.GetUVs(3, values);
            return values;
        }

        private static List<Vector4> ReadUv7(Mesh mesh)
        {
            var values = new List<Vector4>();
            mesh.GetUVs(7, values);
            return values;
        }

        private static string Join<T>(IEnumerable<T> values)
        {
            return string.Join(",", values.Select(Format));
        }

        /// <summary>
        /// Round-trippable formatting. Unity's default vector ToString rounds to
        /// two decimals, which would let a real difference compare equal.
        /// </summary>
        private static string Format<T>(T value)
        {
            switch (value)
            {
                case Vector2 vector: return vector.ToString("R");
                case Vector3 vector: return vector.ToString("R");
                case Vector4 vector: return vector.ToString("R");
                case Matrix4x4 matrix: return matrix.ToString("R");
                case Bounds bounds:
                    return bounds.center.ToString("R") + "/" +
                           bounds.extents.ToString("R");
                case Color32 color:
                    return color.r + "/" + color.g + "/" + color.b + "/" +
                           color.a;
                default: return value.ToString();
            }
        }

        /// <summary>
        /// The layout-only finalization under test. Source submesh 1 is split:
        /// its preserved-alpha triangle stays in place and its separated triangle
        /// is appended as submesh 2. Source submesh 0 is never rewritten.
        ///
        /// <para>
        /// "Layout-only" is an intent, not something Unity gives for free.
        /// Raising <see cref="Mesh.subMeshCount"/> recalculates both the mesh
        /// bounds and every per-submesh descriptor bounds from the vertex buffer
        /// — measured, and pinned by
        /// <see cref="RaisingSubMeshCountRecalculatesMeshAndSubmeshBoundsSoFinalizationMustRestoreThem"/>.
        /// <c>calculateBounds: false</c> does not prevent it; it only stops
        /// <c>SetIndices</c> from adding a second recalculation, and on the
        /// appended submesh it leaves the descriptor bounds at zero. So both
        /// levels of bounds are captured first and written back last.
        /// </para>
        ///
        /// <para>
        /// The appended submesh inherits its source submesh's bounds. Its
        /// triangles are a subset of that submesh's, so the inherited bounds are
        /// a conservative superset — the safe direction, since bounds that are
        /// too large cost culling and bounds that are too small pop.
        /// </para>
        /// </summary>
        private static void FinalizeLayout(Mesh clone)
        {
            var meshBounds = clone.bounds;
            var sourceSubmeshBounds = new Bounds[clone.subMeshCount];
            for (var submesh = 0; submesh < clone.subMeshCount; submesh++)
            {
                sourceSubmeshBounds[submesh] = clone.GetSubMesh(submesh).bounds;
            }

            clone.subMeshCount = 3;
            clone.SetIndices(
                PreservedAlphaTriangle, MeshTopology.Triangles, 1,
                calculateBounds: false);
            clone.SetIndices(
                SeparatedOpaqueTriangle, MeshTopology.Triangles, 2,
                calculateBounds: false);

            // Output submesh -> the source submesh whose bounds it inherits.
            var inherited = new[]
            {
                sourceSubmeshBounds[0],
                sourceSubmeshBounds[1],
                sourceSubmeshBounds[1],
            };

            for (var submesh = 0; submesh < clone.subMeshCount; submesh++)
            {
                var descriptor = clone.GetSubMesh(submesh);
                descriptor.bounds = inherited[submesh];
                clone.SetSubMesh(
                    submesh, descriptor, MeshUpdateFlags.DontRecalculateBounds);
            }

            clone.bounds = meshBounds;
        }

        [Test]
        public void NativeCloneIsADistinctObjectAndLeavesTheSourceUnchanged()
        {
            var source = CreateAdversarialSource();
            var before = Describe(source);

            var clone = Track(Object.Instantiate(source));

            Assert.That(
                ReferenceEquals(clone, source), Is.False,
                "Instantiate returned the source instance itself.");
            Assert.That(
                clone.GetInstanceID(), Is.Not.EqualTo(source.GetInstanceID()),
                "The clone shares the source's instance id, so it is not a " +
                "distinct Unity object.");

            Assert.That(
                Describe(source), Is.EqualTo(before),
                "EVIDENCE BOUNDARY: cloning changed the source mesh's " +
                "characterized state.");

            TestContext.WriteLine(
                "source name='" + source.name + "' clone name='" + clone.name +
                "' (naming a generated asset is a consumer obligation)");
        }

        [Test]
        public void NativeCloneRetainsTheCharacterizedSkinnedAvatarMeshState()
        {
            var source = CreateAdversarialSource();
            var clone = Track(Object.Instantiate(source));

            Assert.That(clone.vertexCount, Is.EqualTo(source.vertexCount));
            Assert.That(clone.indexFormat, Is.EqualTo(IndexFormat.UInt32),
                "The clone did not retain the source's 32-bit index format.");
            Assert.That(clone.subMeshCount, Is.EqualTo(source.subMeshCount));

            CollectionAssert.AreEqual(source.vertices, clone.vertices,
                "vertex positions");
            CollectionAssert.AreEqual(source.normals, clone.normals, "normals");
            CollectionAssert.AreEqual(source.tangents, clone.tangents, "tangents");
            CollectionAssert.AreEqual(source.colors32, clone.colors32, "colors");

            CollectionAssert.AreEqual(
                ReadUv0(source), ReadUv0(clone), "uv0");
            CollectionAssert.AreEqual(
                ReadUv3(source), ReadUv3(clone),
                "uv3 lost its third component or its channel");
            CollectionAssert.AreEqual(
                ReadUv7(source), ReadUv7(clone),
                "uv7 lost its fourth component or its channel");

            CollectionAssert.AreEqual(
                source.boneWeights, clone.boneWeights, "bone weights");
            CollectionAssert.AreEqual(
                source.bindposes, clone.bindposes, "bindposes");
            Assert.That(clone.bounds, Is.EqualTo(source.bounds),
                "The clone recalculated or dropped the authored mesh bounds.");

            // Descriptors, not merely effective indices: base vertex, index
            // range, vertex range and per-submesh bounds all have to survive.
            for (var submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                Assert.That(
                    DescribeDescriptor(clone, submesh),
                    Is.EqualTo(DescribeDescriptor(source, submesh)),
                    "submesh " + submesh + " descriptor was not retained");
                CollectionAssert.AreEqual(
                    source.GetIndices(submesh), clone.GetIndices(submesh),
                    "submesh " + submesh + " effective indices");
                CollectionAssert.AreEqual(
                    source.GetIndices(submesh, false),
                    clone.GetIndices(submesh, false),
                    "submesh " + submesh + " stored indices");
            }

            Assert.That(
                clone.GetSubMesh(1).baseVertex,
                Is.EqualTo(SplitSubmeshBaseVertex),
                "the nonzero source base vertex was normalized away by the " +
                "clone itself, before any finalization");

            Assert.That(clone.blendShapeCount, Is.EqualTo(2));
            Assert.That(
                DescribeBlendShapes(clone), Is.EqualTo(DescribeBlendShapes(source)),
                "blend shape names, per-frame weights, or per-frame deltas were " +
                "not retained.");

            TestContext.WriteLine(Describe(clone));
        }

        [Test]
        public void
            LayoutOnlyFinalizationSplitsASubmeshWithoutTouchingTheSourceOrTheRetainedData()
        {
            var source = CreateAdversarialSource();
            var sourceBefore = Describe(source);

            var clone = Track(Object.Instantiate(source));
            FinalizeLayout(clone);

            // 1. The layout is what finalization asked for.
            Assert.That(clone.subMeshCount, Is.EqualTo(3),
                "the appended opaque submesh was not created");
            CollectionAssert.AreEqual(
                UntouchedTriangle, clone.GetIndices(0),
                "the untouched submesh changed");
            CollectionAssert.AreEqual(
                PreservedAlphaTriangle, clone.GetIndices(1),
                "submesh 1 does not carry exactly the preserved alpha indices");
            CollectionAssert.AreEqual(
                SeparatedOpaqueTriangle, clone.GetIndices(2),
                "the appended submesh does not carry exactly the opaque indices");
            Assert.That(clone.GetTopology(2), Is.EqualTo(MeshTopology.Triangles));

            // 2. Every triangle survives exactly once across the new layout, in
            //    effective vertex space, so no index was remapped and the
            //    appended submesh addresses the same vertices the source did.
            var finalized = Enumerable.Range(0, clone.subMeshCount)
                .SelectMany(submesh => clone.GetIndices(submesh))
                .ToArray();
            var original = Enumerable.Range(0, source.subMeshCount)
                .SelectMany(submesh => source.GetIndices(submesh))
                .ToArray();
            CollectionAssert.AreEquivalent(original, finalized,
                "finalization added, dropped, or remapped effective indices");

            // 3. The untouched submesh keeps its whole descriptor, including its
            //    authored per-submesh bounds.
            Assert.That(
                DescribeDescriptor(clone, 0),
                Is.EqualTo(DescribeDescriptor(source, 0)),
                "the untouched submesh's descriptor did not survive");

            // 4. The rewritten and appended submeshes have valid descriptors.
            foreach (var submesh in new[] { 1, 2 })
            {
                var descriptor = clone.GetSubMesh(submesh);
                var effective = clone.GetIndices(submesh);

                Assert.That(descriptor.indexCount, Is.EqualTo(effective.Length),
                    "submesh " + submesh + " index count disagrees with its " +
                    "indices");
                Assert.That(
                    descriptor.baseVertex + descriptor.firstVertex,
                    Is.EqualTo(effective.Min()),
                    "submesh " + submesh + " firstVertex does not name the " +
                    "lowest vertex it actually references, so CPU processing " +
                    "and skinning would read the wrong range");
                Assert.That(
                    descriptor.vertexCount,
                    Is.EqualTo(effective.Max() - effective.Min() + 1),
                    "submesh " + submesh + " vertexCount does not span the " +
                    "vertices it actually references");
                Assert.That(
                    descriptor.bounds, Is.EqualTo(AuthoredSubmeshBounds(1)),
                    "submesh " + submesh + " did not inherit its source " +
                    "submesh's bounds; a zero or recalculated value means the " +
                    "per-submesh bounds obligation was not met");
            }

            // 5. Nothing else on the clone moved.
            Assert.That(clone.vertexCount, Is.EqualTo(VertexCount),
                "finalization compacted or added vertices; vertices 3 and 8 " +
                "are referenced by no triangle and must still survive");
            Assert.That(clone.indexFormat, Is.EqualTo(IndexFormat.UInt32));
            Assert.That(clone.bounds, Is.EqualTo(source.bounds),
                "finalization did not end with the authored mesh bounds; " +
                "raising subMeshCount recalculates them and the restore did " +
                "not hold");
            CollectionAssert.AreEqual(source.vertices, clone.vertices);
            CollectionAssert.AreEqual(source.normals, clone.normals);
            CollectionAssert.AreEqual(source.tangents, clone.tangents);
            CollectionAssert.AreEqual(source.colors32, clone.colors32);
            CollectionAssert.AreEqual(ReadUv0(source), ReadUv0(clone));
            CollectionAssert.AreEqual(ReadUv3(source), ReadUv3(clone));
            CollectionAssert.AreEqual(ReadUv7(source), ReadUv7(clone));
            CollectionAssert.AreEqual(source.boneWeights, clone.boneWeights);
            CollectionAssert.AreEqual(source.bindposes, clone.bindposes);
            Assert.That(
                DescribeBlendShapes(clone), Is.EqualTo(DescribeBlendShapes(source)),
                "finalization disturbed blend shape data");

            // 6. The source's characterized state is exactly as it started.
            Assert.That(
                Describe(source), Is.EqualTo(sourceBefore),
                "EVIDENCE BOUNDARY: finalizing the clone changed the source " +
                "mesh's characterized state.");

            TestContext.WriteLine(Describe(clone));
        }

        /// <summary>
        /// The split submesh's nonzero base vertex is normalized to zero by the
        /// rewrite. That is a representation change, and it is only acceptable
        /// because the effective vertices are identical and the descriptor's
        /// vertex range stays truthful — both asserted here rather than assumed.
        /// </summary>
        [Test]
        public void FinalizationNormalizesBaseVertexWithoutChangingEffectiveVertices()
        {
            var source = CreateAdversarialSource();

            var sourceDescriptor = source.GetSubMesh(1);
            Assert.That(
                sourceDescriptor.baseVertex, Is.EqualTo(SplitSubmeshBaseVertex),
                "the fixture must actually author a nonzero base vertex, or " +
                "this proves nothing");
            CollectionAssert.AreNotEqual(
                source.GetIndices(1), source.GetIndices(1, false),
                "stored and effective indices must differ, or the base vertex " +
                "is not doing any work in the fixture");

            var clone = Track(Object.Instantiate(source));
            FinalizeLayout(clone);

            var finalized = clone.GetSubMesh(1);
            Assert.That(finalized.baseVertex, Is.EqualTo(0),
                "SetIndices no longer normalizes base vertex to zero. The " +
                "normalization is recorded as an intentional representation " +
                "change and must be re-justified if Unity stops doing it.");

            CollectionAssert.AreEqual(
                PreservedAlphaTriangle, clone.GetIndices(1),
                "normalization changed which vertices the submesh references, " +
                "which would make it a behavioural change rather than a " +
                "representation change");

            // baseVertex + firstVertex is the absolute first referenced vertex.
            // The source spends it on baseVertex; the rewrite spends it on
            // firstVertex. The sum is what CPU processing and skinning need.
            Assert.That(
                finalized.baseVertex + finalized.firstVertex,
                Is.EqualTo(PreservedAlphaTriangle.Min()),
                "the normalized descriptor no longer names the correct first " +
                "referenced vertex");

            TestContext.WriteLine(
                "source   " + DescribeDescriptor(source, 1) + "\n" +
                "finalized " + DescribeDescriptor(clone, 1));
        }

        /// <summary>
        /// The two non-obvious facts this route depends on. Without the
        /// compensation in <see cref="FinalizeLayout"/>, a split renderer would
        /// ship with mesh and per-submesh bounds AMUSE never authored, so if
        /// Unity ever stops doing this the compensation must be reconsidered
        /// rather than silently kept.
        /// </summary>
        [Test]
        public void
            RaisingSubMeshCountRecalculatesMeshAndSubmeshBoundsSoFinalizationMustRestoreThem()
        {
            var source = CreateAdversarialSource();
            var clone = Track(Object.Instantiate(source));

            Assert.That(clone.bounds, Is.EqualTo(AuthoredMeshBounds),
                "Instantiate itself did not preserve the authored mesh bounds.");
            Assert.That(
                clone.GetSubMesh(1).bounds, Is.EqualTo(AuthoredSubmeshBounds(1)),
                "Instantiate itself did not preserve the authored per-submesh " +
                "bounds.");

            clone.subMeshCount = 3;

            Assert.That(clone.bounds, Is.Not.EqualTo(AuthoredMeshBounds),
                "Mesh.subMeshCount no longer recalculates mesh bounds. The " +
                "restore in FinalizeLayout was written for that behaviour and " +
                "must be re-justified before it is kept.");
            Assert.That(
                clone.GetSubMesh(1).bounds,
                Is.Not.EqualTo(AuthoredSubmeshBounds(1)),
                "Mesh.subMeshCount no longer recalculates per-submesh bounds. " +
                "The per-submesh restore must be re-justified before it is kept.");

            // The appended submesh is created with zero bounds, and
            // calculateBounds:false leaves it that way — the second half of the
            // obligation.
            clone.SetIndices(
                SeparatedOpaqueTriangle, MeshTopology.Triangles, 2,
                calculateBounds: false);
            Assert.That(
                clone.GetSubMesh(2).bounds.extents, Is.EqualTo(Vector3.zero),
                "SetIndices with calculateBounds:false now populates the " +
                "appended submesh's bounds, so inheriting them explicitly may " +
                "no longer be required.");

            var beforeSecondWrite = clone.GetSubMesh(1).bounds;
            clone.SetIndices(
                PreservedAlphaTriangle, MeshTopology.Triangles, 1,
                calculateBounds: false);
            Assert.That(
                clone.GetSubMesh(1).bounds, Is.EqualTo(beforeSecondWrite),
                "SetIndices with calculateBounds:false changed a per-submesh " +
                "bounds, so the recalculation is not confined to the " +
                "subMeshCount setter.");

            clone.bounds = AuthoredMeshBounds;
            Assert.That(clone.bounds, Is.EqualTo(AuthoredMeshBounds),
                "The authored mesh bounds could not be written back.");

            TestContext.WriteLine(
                "authored mesh=" + Format(AuthoredMeshBounds) +
                " recalculated submesh1=" + Format(beforeSecondWrite));
        }

        [Test]
        public void DestroyingAnAbandonedCloneLeavesTheSourceIntact()
        {
            var source = CreateAdversarialSource();
            var before = Describe(source);

            var clone = Object.Instantiate(source);
            FinalizeLayout(clone);
            Object.DestroyImmediate(clone);

            Assert.That(clone == null, Is.True,
                "DestroyImmediate did not destroy the abandoned clone, so the " +
                "post-validation sweep cannot rely on it.");
            Assert.That(
                Describe(source), Is.EqualTo(before),
                "EVIDENCE BOUNDARY: destroying the clone changed the source " +
                "mesh's characterized state.");
        }
    }
}
