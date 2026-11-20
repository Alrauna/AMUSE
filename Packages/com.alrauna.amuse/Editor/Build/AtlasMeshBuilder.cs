using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// The remap of one submesh of the atlas route. A kept submesh
    /// carries no map. A mapped submesh rewrites the route channel
    /// with one affine map per axis. The map is newU = (u *
    /// SourceWidth - SourceX + PageX) / PageWidth. The V axis uses
    /// the same map with the Y and the height values.
    /// </summary>
    internal sealed class AtlasSubmeshRemap
    {
        internal int SubmeshIndex { get; }

        /// <summary>True when the submesh carries a map.</summary>
        internal bool HasRemap { get; }

        internal int SourceWidth { get; }
        internal int SourceHeight { get; }

        /// <summary>The bordered source origin. It may be negative.</summary>
        internal int SourceX { get; }
        internal int SourceY { get; }

        internal int PageX { get; }
        internal int PageY { get; }
        internal int PageWidth { get; }
        internal int PageHeight { get; }

        internal AtlasSubmeshRemap(int submeshIndex)
        {
            SubmeshIndex = submeshIndex;
        }

        internal AtlasSubmeshRemap(
            int submeshIndex,
            int sourceWidth,
            int sourceHeight,
            int sourceX,
            int sourceY,
            int pageX,
            int pageY,
            int pageWidth,
            int pageHeight)
        {
            SubmeshIndex = submeshIndex;
            HasRemap = true;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            SourceX = sourceX;
            SourceY = sourceY;
            PageX = pageX;
            PageY = pageY;
            PageWidth = pageWidth;
            PageHeight = pageHeight;
        }

        /// <summary>True when both maps produce the same route values.</summary>
        internal bool SameMap(AtlasSubmeshRemap other)
        {
            return SourceWidth == other.SourceWidth
                && SourceHeight == other.SourceHeight
                && SourceX == other.SourceX
                && SourceY == other.SourceY
                && PageX == other.PageX
                && PageY == other.PageY
                && PageWidth == other.PageWidth
                && PageHeight == other.PageHeight;
        }
    }

    /// <summary>
    /// Builds the atlas output mesh of one renderer. The builder
    /// never changes the source mesh. It copies every vertex
    /// attribute, rewrites the route channel as Float32, duplicates
    /// a shared vertex once per extra map, and carries the submesh
    /// topology, the bounds, the bind poses, the bone weights, and
    /// the blend shape frames over. A vertex keeps its original
    /// index for the first map that uses it. Each extra map appends
    /// one copy.
    /// </summary>
    internal static class AtlasMeshBuilder
    {
        /// <summary>The name suffix of every atlas mesh.</summary>
        private const string NameSuffix = " AMUSE atlas";

        /// <summary>The vertex count that forces a 32 bit index buffer.</summary>
        private const int SixteenBitLimit = 65535;

        internal static Mesh Build(
            Mesh current,
            IReadOnlyList<AtlasSubmeshRemap> remaps)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (remaps == null)
            {
                throw new ArgumentNullException(nameof(remaps));
            }

            var remapBySubmesh = ReadRemaps(current.subMeshCount, remaps);

            var sourceData = Mesh.AcquireReadOnlyMeshData(current);
            try
            {
                var source = sourceData[0];
                CheckRouteChannel(source, remapBySubmesh);

                var sourceVertexCount = source.vertexCount;
                var remapsPerVertex =
                    new List<AtlasSubmeshRemap>[sourceVertexCount];
                for (var s = 0; s < remapBySubmesh.Length; s++)
                {
                    ScanSubmesh(source, s, remapBySubmesh[s], remapsPerVertex);
                }

                var slots = AssignSlots(
                    remapsPerVertex, out var outputVertexCount);
                var vertexOrigin = new int[outputVertexCount];
                var vertexMap = new AtlasSubmeshRemap[outputVertexCount];
                for (var v = 0; v < remapsPerVertex.Length; v++)
                {
                    var vertexSlots = slots[v];
                    var list = remapsPerVertex[v];
                    var count = list == null ? 1 : list.Count;
                    for (var k = 0; k < count; k++)
                    {
                        var slot = vertexSlots[k];
                        vertexOrigin[slot] = v;
                        vertexMap[slot] = list == null ? null : list[k];
                    }
                }

                var writable = Mesh.AllocateWritableMeshData(1);
                var applied = false;
                try
                {
                    var output = writable[0];
                    var attributes = current.GetVertexAttributes();
                    SetOutputLayout(output, attributes, outputVertexCount);
                    CopyVertexStreams(
                        source, output, attributes, vertexOrigin, vertexMap);

                    var indices = RemappedIndices(
                        source, remapBySubmesh, remapsPerVertex, slots);
                    var indexFormat = outputVertexCount > SixteenBitLimit
                        ? IndexFormat.UInt32
                        : source.indexFormat;
                    output.SetIndexBufferParams(indices.Length, indexFormat);
                    WriteIndices(output, indices, indexFormat);
                    output.subMeshCount = remapBySubmesh.Length;
                    var indexStart = 0;
                    for (var s = 0; s < remapBySubmesh.Length; s++)
                    {
                        var submesh = source.GetSubMesh(s);
                        output.SetSubMesh(
                            s,
                            new SubMeshDescriptor(
                                indexStart, submesh.indexCount, submesh.topology));
                        indexStart += submesh.indexCount;
                    }

                    var built = new Mesh();
                    Mesh.ApplyAndDisposeWritableMeshData(
                        writable, built, MeshUpdateFlags.DontRecalculateBounds);
                    applied = true;
                    built.name = current.name + NameSuffix;
                    built.bounds = current.bounds;
                    built.bindposes = current.bindposes;
                    CopyBoneWeights(current, built, vertexOrigin);
                    CopyBlendShapes(current, built, vertexOrigin);
                    return built;
                }
                finally
                {
                    if (!applied)
                    {
                        writable.Dispose();
                    }
                }
            }
            finally
            {
                sourceData.Dispose();
            }
        }

        private static AtlasSubmeshRemap[] ReadRemaps(
            int submeshCount,
            IReadOnlyList<AtlasSubmeshRemap> remaps)
        {
            var bySubmesh = new AtlasSubmeshRemap[submeshCount];
            foreach (var remap in remaps)
            {
                if (remap == null)
                {
                    throw new ArgumentNullException(
                        nameof(remaps), "A remap entry is null.");
                }

                if (remap.SubmeshIndex < 0 || remap.SubmeshIndex >= submeshCount)
                {
                    throw new InvalidOperationException(
                        "The remap names submesh " + remap.SubmeshIndex +
                        " of a mesh with " + submeshCount + " submeshes.");
                }

                if (bySubmesh[remap.SubmeshIndex] != null)
                {
                    throw new InvalidOperationException(
                        "Two remaps name submesh " + remap.SubmeshIndex + ".");
                }

                if (remap.HasRemap &&
                    (remap.SourceWidth < 1 || remap.SourceHeight < 1 ||
                        remap.PageWidth < 1 || remap.PageHeight < 1))
                {
                    throw new InvalidOperationException(
                        "The remap of submesh " + remap.SubmeshIndex +
                        " has an empty source or page rectangle.");
                }

                bySubmesh[remap.SubmeshIndex] = remap;
            }

            return bySubmesh;
        }

        private static void CheckRouteChannel(
            Mesh.MeshData source,
            AtlasSubmeshRemap[] remapBySubmesh)
        {
            var needsRoute = false;
            for (var s = 0; s < remapBySubmesh.Length; s++)
            {
                if (remapBySubmesh[s] != null && remapBySubmesh[s].HasRemap)
                {
                    needsRoute = true;
                    break;
                }
            }

            if (needsRoute && !source.HasVertexAttribute(VertexAttribute.TexCoord0))
            {
                throw new InvalidOperationException(
                    "The mesh carries no route channel, so the atlas " +
                    "remap has nothing to map.");
            }
        }

        /// <summary>
        /// Records the distinct map of each vertex in submesh order.
        /// A kept submesh records null.
        /// </summary>
        private static void ScanSubmesh(
            Mesh.MeshData source,
            int submesh,
            AtlasSubmeshRemap remap,
            List<AtlasSubmeshRemap>[] remapsPerVertex)
        {
            var descriptor = source.GetSubMesh(submesh);
            var map = Normalize(remap);
            var end = descriptor.indexStart + descriptor.indexCount;
            if (source.indexFormat == IndexFormat.UInt16)
            {
                var indices = source.GetIndexData<ushort>();
                for (var i = descriptor.indexStart; i < end; i++)
                {
                    Remember(indices[i], map, remapsPerVertex);
                }
            }
            else
            {
                var indices = source.GetIndexData<uint>();
                for (var i = descriptor.indexStart; i < end; i++)
                {
                    Remember((int)indices[i], map, remapsPerVertex);
                }
            }
        }

        private static void Remember(
            int vertex,
            AtlasSubmeshRemap map,
            List<AtlasSubmeshRemap>[] remapsPerVertex)
        {
            var list = remapsPerVertex[vertex];
            if (list == null)
            {
                list = new List<AtlasSubmeshRemap>();
                remapsPerVertex[vertex] = list;
                list.Add(map);
                return;
            }

            foreach (var existing in list)
            {
                if (SameRemap(existing, map))
                {
                    return;
                }
            }

            list.Add(map);
        }

        /// <summary>
        /// Maps every vertex to its output slots. Slot zero is the
        /// original vertex index. Each extra map appends one slot.
        /// </summary>
        private static int[][] AssignSlots(
            List<AtlasSubmeshRemap>[] remapsPerVertex,
            out int outputVertexCount)
        {
            var extraCount = 0;
            for (var v = 0; v < remapsPerVertex.Length; v++)
            {
                var list = remapsPerVertex[v];
                extraCount += (list == null ? 1 : list.Count) - 1;
            }

            outputVertexCount = remapsPerVertex.Length + extraCount;
            var slots = new int[remapsPerVertex.Length][];
            var nextExtra = remapsPerVertex.Length;
            for (var v = 0; v < remapsPerVertex.Length; v++)
            {
                var list = remapsPerVertex[v];
                var count = list == null ? 1 : list.Count;
                var vertexSlots = new int[count];
                for (var k = 0; k < count; k++)
                {
                    if (k == 0)
                    {
                        vertexSlots[k] = v;
                    }
                    else
                    {
                        vertexSlots[k] = nextExtra;
                        nextExtra++;
                    }
                }

                slots[v] = vertexSlots;
            }

            return slots;
        }

        private static void SetOutputLayout(
            Mesh.MeshData output,
            VertexAttributeDescriptor[] attributes,
            int vertexCount)
        {
            var layout = new VertexAttributeDescriptor[attributes.Length];
            for (var i = 0; i < attributes.Length; i++)
            {
                var format = attributes[i].format;
                if (attributes[i].attribute == VertexAttribute.TexCoord0)
                {
                    format = VertexAttributeFormat.Float32;
                }

                layout[i] = new VertexAttributeDescriptor(
                    attributes[i].attribute,
                    format,
                    attributes[i].dimension,
                    attributes[i].stream);
            }

            output.SetVertexBufferParams(vertexCount, layout);
        }

        private static void CopyVertexStreams(
            Mesh.MeshData source,
            Mesh.MeshData output,
            VertexAttributeDescriptor[] attributes,
            int[] vertexOrigin,
            AtlasSubmeshRemap[] vertexMap)
        {
            var streamCount = source.vertexBufferCount;
            var sourceStreams = new NativeArray<byte>[streamCount];
            var outputStreams = new NativeArray<byte>[streamCount];
            for (var s = 0; s < streamCount; s++)
            {
                sourceStreams[s] = source.GetVertexData<byte>(s);
                outputStreams[s] = output.GetVertexData<byte>(s);
            }

            for (var i = 0; i < attributes.Length; i++)
            {
                var attribute = attributes[i];
                if (attribute.attribute == VertexAttribute.TexCoord0)
                {
                    continue;
                }

                CopyAttribute(
                    sourceStreams[attribute.stream],
                    outputStreams[attribute.stream],
                    source.GetVertexAttributeOffset(attribute.attribute),
                    output.GetVertexAttributeOffset(attribute.attribute),
                    source.GetVertexBufferStride(attribute.stream),
                    output.GetVertexBufferStride(attribute.stream),
                    FormatSize(attribute.format) * attribute.dimension,
                    vertexOrigin);
            }

            for (var i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].attribute == VertexAttribute.TexCoord0)
                {
                    CopyRouteChannel(
                        source, output, attributes[i], sourceStreams,
                        outputStreams, vertexOrigin, vertexMap);
                    break;
                }
            }
        }

        /// <summary>
        /// Copies one attribute of every output vertex byte for byte
        /// from the source vertex that the origin table names.
        /// </summary>
        private static void CopyAttribute(
            NativeArray<byte> from,
            NativeArray<byte> to,
            int fromOffset,
            int toOffset,
            int fromStride,
            int toStride,
            int byteCount,
            int[] vertexOrigin)
        {
            for (var j = 0; j < vertexOrigin.Length; j++)
            {
                var fromIndex = vertexOrigin[j] * fromStride + fromOffset;
                var toIndex = j * toStride + toOffset;
                for (var b = 0; b < byteCount; b++)
                {
                    to[toIndex + b] = from[fromIndex + b];
                }
            }
        }

        /// <summary>
        /// Writes the route channel of every output vertex as
        /// Float32. A kept slot keeps its decoded source value. A
        /// mapped slot takes the affine map of its axis. Components
        /// past the second keep their decoded source value.
        /// </summary>
        private static void CopyRouteChannel(
            Mesh.MeshData source,
            Mesh.MeshData output,
            VertexAttributeDescriptor descriptor,
            NativeArray<byte>[] sourceStreams,
            NativeArray<byte>[] outputStreams,
            int[] vertexOrigin,
            AtlasSubmeshRemap[] vertexMap)
        {
            var from = sourceStreams[descriptor.stream];
            var to = outputStreams[descriptor.stream];
            var fromOffset = source.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
            var toOffset = output.GetVertexAttributeOffset(VertexAttribute.TexCoord0);
            var fromStride = source.GetVertexBufferStride(descriptor.stream);
            var toStride = output.GetVertexBufferStride(descriptor.stream);
            var componentSize = FormatSize(descriptor.format);
            for (var j = 0; j < vertexOrigin.Length; j++)
            {
                var fromBase = vertexOrigin[j] * fromStride + fromOffset;
                var toBase = j * toStride + toOffset;
                var map = vertexMap[j];
                for (var c = 0; c < descriptor.dimension; c++)
                {
                    var value = ReadComponent(
                        from, fromBase + c * componentSize, descriptor.format);
                    if (map != null && c == 0)
                    {
                        value = (float)((value * (double)map.SourceWidth - map.SourceX + map.PageX)
                            / map.PageWidth);
                    }
                    else if (map != null && c == 1)
                    {
                        value = (float)((value * (double)map.SourceHeight - map.SourceY + map.PageY)
                            / map.PageHeight);
                    }

                    WriteFloat(to, toBase + 4 * c, value);
                }
            }
        }

        private static int[] RemappedIndices(
            Mesh.MeshData source,
            AtlasSubmeshRemap[] remapBySubmesh,
            List<AtlasSubmeshRemap>[] remapsPerVertex,
            int[][] slots)
        {
            var total = 0;
            for (var s = 0; s < remapBySubmesh.Length; s++)
            {
                total += source.GetSubMesh(s).indexCount;
            }

            var indices = new int[total];
            var cursor = 0;
            for (var s = 0; s < remapBySubmesh.Length; s++)
            {
                var descriptor = source.GetSubMesh(s);
                var map = Normalize(remapBySubmesh[s]);
                var end = descriptor.indexStart + descriptor.indexCount;
                if (source.indexFormat == IndexFormat.UInt16)
                {
                    var sourceIndices = source.GetIndexData<ushort>();
                    for (var i = descriptor.indexStart; i < end; i++)
                    {
                        indices[cursor++] = SlotOf(
                            sourceIndices[i], map, remapsPerVertex, slots);
                    }
                }
                else
                {
                    var sourceIndices = source.GetIndexData<uint>();
                    for (var i = descriptor.indexStart; i < end; i++)
                    {
                        indices[cursor++] = SlotOf(
                            (int)sourceIndices[i], map, remapsPerVertex, slots);
                    }
                }
            }

            return indices;
        }

        private static void WriteIndices(
            Mesh.MeshData output,
            int[] indices,
            IndexFormat format)
        {
            if (format == IndexFormat.UInt16)
            {
                var words = output.GetIndexData<ushort>();
                for (var i = 0; i < indices.Length; i++)
                {
                    words[i] = (ushort)indices[i];
                }
            }
            else
            {
                var words = output.GetIndexData<uint>();
                for (var i = 0; i < indices.Length; i++)
                {
                    words[i] = (uint)indices[i];
                }
            }
        }

        /// <summary>
        /// Expands the bone weights of the source into the output
        /// vertex order. A duplicated vertex repeats the weights of
        /// its source vertex.
        /// </summary>
        private static void CopyBoneWeights(Mesh source, Mesh target, int[] vertexOrigin)
        {
            var bonesPerVertex = source.GetBonesPerVertex();
            if (bonesPerVertex.Length == 0)
            {
                return;
            }

            var sourceWeights = source.GetAllBoneWeights();
            var sourceOffsets = new int[bonesPerVertex.Length + 1];
            for (var v = 0; v < bonesPerVertex.Length; v++)
            {
                sourceOffsets[v + 1] = sourceOffsets[v] + bonesPerVertex[v];
            }

            var totalWeights = 0;
            for (var j = 0; j < vertexOrigin.Length; j++)
            {
                totalWeights += bonesPerVertex[vertexOrigin[j]];
            }

            var targetCounts = new NativeArray<byte>(
                vertexOrigin.Length, Allocator.Temp);
            var targetWeights = new NativeArray<BoneWeight1>(
                totalWeights, Allocator.Temp);
            var cursor = 0;
            try
            {
                for (var j = 0; j < vertexOrigin.Length; j++)
                {
                    var src = vertexOrigin[j];
                    var count = bonesPerVertex[src];
                    targetCounts[j] = count;
                    for (var w = 0; w < count; w++)
                    {
                        targetWeights[cursor + w] = sourceWeights[sourceOffsets[src] + w];
                    }

                    cursor += count;
                }

                target.SetBoneWeights(targetCounts, targetWeights);
            }
            finally
            {
                targetCounts.Dispose();
                targetWeights.Dispose();
            }
        }

        /// <summary>
        /// Copies every blend shape frame into the output vertex
        /// order. A duplicated vertex repeats the deltas of its
        /// source vertex.
        /// </summary>
        private static void CopyBlendShapes(Mesh source, Mesh target, int[] vertexOrigin)
        {
            if (source.blendShapeCount == 0)
            {
                return;
            }

            var sourceVertices = new Vector3[source.vertexCount];
            var sourceNormals = new Vector3[source.vertexCount];
            var sourceTangents = new Vector3[source.vertexCount];
            var targetVertices = new Vector3[vertexOrigin.Length];
            var targetNormals = new Vector3[vertexOrigin.Length];
            var targetTangents = new Vector3[vertexOrigin.Length];
            for (var shape = 0; shape < source.blendShapeCount; shape++)
            {
                var name = source.GetBlendShapeName(shape);
                var frames = source.GetBlendShapeFrameCount(shape);
                for (var frame = 0; frame < frames; frame++)
                {
                    source.GetBlendShapeFrameVertices(
                        shape, frame, sourceVertices, sourceNormals, sourceTangents);
                    for (var j = 0; j < vertexOrigin.Length; j++)
                    {
                        var src = vertexOrigin[j];
                        targetVertices[j] = sourceVertices[src];
                        targetNormals[j] = sourceNormals[src];
                        targetTangents[j] = sourceTangents[src];
                    }

                    target.AddBlendShapeFrame(
                        name,
                        source.GetBlendShapeFrameWeight(shape, frame),
                        targetVertices,
                        targetNormals,
                        targetTangents);
                }
            }
        }

        private static int SlotOf(
            int vertex,
            AtlasSubmeshRemap map,
            List<AtlasSubmeshRemap>[] remapsPerVertex,
            int[][] slots)
        {
            var list = remapsPerVertex[vertex];
            for (var k = 0; k < list.Count; k++)
            {
                if (SameRemap(list[k], map))
                {
                    return slots[vertex][k];
                }
            }

            throw new InvalidOperationException(
                "The slot table lacks a remap of vertex " + vertex + ".");
        }

        private static bool SameRemap(AtlasSubmeshRemap a, AtlasSubmeshRemap b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            return a.SameMap(b);
        }

        private static AtlasSubmeshRemap Normalize(AtlasSubmeshRemap remap)
        {
            return remap != null && remap.HasRemap ? remap : null;
        }

        private static float ReadComponent(
            NativeArray<byte> stream,
            int index,
            VertexAttributeFormat format)
        {
            switch (format)
            {
                case VertexAttributeFormat.Float32:
                    return BitConverter.Int32BitsToSingle(
                        stream[index]
                        | (stream[index + 1] << 8)
                        | (stream[index + 2] << 16)
                        | (stream[index + 3] << 24));
                case VertexAttributeFormat.Float16:
                    return HalfToFloat(
                        (ushort)(stream[index] | (stream[index + 1] << 8)));
                case VertexAttributeFormat.UNorm16:
                    return (stream[index] | (stream[index + 1] << 8)) / 65535f;
                case VertexAttributeFormat.SNorm16:
                {
                    var raw = (short)(stream[index] | (stream[index + 1] << 8));
                    return Math.Max(raw / 32767f, -1f);
                }

                case VertexAttributeFormat.UNorm8:
                    return stream[index] / 255f;
                case VertexAttributeFormat.SNorm8:
                {
                    var raw = (sbyte)stream[index];
                    return Math.Max(raw / 127f, -1f);
                }

                default:
                    throw new InvalidOperationException(
                        "The route channel uses format " + format +
                        ". The mesh output supports Float32, Float16, and " +
                        "the normalized byte and word formats.");
            }
        }

        private static void WriteFloat(
            NativeArray<byte> stream,
            int index,
            float value)
        {
            var bits = BitConverter.SingleToInt32Bits(value);
            stream[index] = (byte)bits;
            stream[index + 1] = (byte)(bits >> 8);
            stream[index + 2] = (byte)(bits >> 16);
            stream[index + 3] = (byte)(bits >> 24);
        }

        /// <summary>Converts a half value to float.</summary>
        private static float HalfToFloat(ushort bits)
        {
            var sign = (bits & 0x8000) << 16;
            var exponent = (bits >> 10) & 0x1F;
            var mantissa = bits & 0x3FF;
            int result;
            if (exponent == 0)
            {
                result = sign;
                if (mantissa != 0)
                {
                    exponent = 113;
                    while ((mantissa & 0x400) == 0)
                    {
                        mantissa <<= 1;
                        exponent--;
                    }

                    result = sign | (exponent << 23) | ((mantissa & 0x3FF) << 13);
                }
            }
            else if (exponent == 0x1F)
            {
                result = sign | 0x7F800000 | (mantissa << 13);
            }
            else
            {
                result = sign | ((exponent - 15 + 127) << 23) | (mantissa << 13);
            }

            return BitConverter.Int32BitsToSingle(result);
        }

        private static int FormatSize(VertexAttributeFormat format)
        {
            switch (format)
            {
                case VertexAttributeFormat.UInt8:
                case VertexAttributeFormat.SInt8:
                case VertexAttributeFormat.UNorm8:
                case VertexAttributeFormat.SNorm8:
                    return 1;
                case VertexAttributeFormat.UInt16:
                case VertexAttributeFormat.SInt16:
                case VertexAttributeFormat.UNorm16:
                case VertexAttributeFormat.SNorm16:
                case VertexAttributeFormat.Float16:
                    return 2;
                case VertexAttributeFormat.UInt32:
                case VertexAttributeFormat.SInt32:
                case VertexAttributeFormat.Float32:
                    return 4;
                default:
                    throw new InvalidOperationException(
                        "The mesh output met the unknown vertex format " + format + ".");
            }
        }
    }
}
