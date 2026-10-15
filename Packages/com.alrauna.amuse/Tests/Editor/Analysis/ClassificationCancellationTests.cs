using System;
using System.Collections.Generic;
using System.Threading;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    /// <summary>
    /// Verifies cancellation token behavior in the triangle classification pipeline.
    /// </summary>
    public sealed class ClassificationCancellationTests
    {
        [Test]
        public void ClassifyRuntimeStates_WithCancelledToken_ThrowsOperationCanceledException()
        {
            var (snapshot, slotResults) = CreateTestMesh(
                triangleCount: 4,
                resolvedAsOpaque: true);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                RuntimeStateResolution.ClassifyRuntimeStates(
                    snapshot,
                    slotResults,
                    cts.Token);
            });
        }

        [Test]
        public void ClassifyRuntimeStates_WithDefaultToken_CompletesNormallyAndProducesIdenticalResult()
        {
            var (snapshot, slotResults) = CreateTestMesh(
                triangleCount: 4,
                resolvedAsOpaque: true);

            var defaultPlan = RuntimeStateResolution.ClassifyRuntimeStates(
                snapshot,
                slotResults,
                default);

            var unspecifiedPlan = RuntimeStateResolution.ClassifyRuntimeStates(
                snapshot,
                slotResults);

            Assert.That(
                defaultPlan.OpaqueTriangleCount,
                Is.EqualTo(unspecifiedPlan.OpaqueTriangleCount));
            Assert.That(
                defaultPlan.Submeshes.Count,
                Is.EqualTo(unspecifiedPlan.Submeshes.Count));
            Assert.That(
                defaultPlan.Submeshes[0].Disposition,
                Is.EqualTo(unspecifiedPlan.Submeshes[0].Disposition));
        }

        [Test]
        public void Classify_WithCancelledToken_ThrowsOperationCanceledException()
        {
            var (indices, positions, uvs, resolution) = CreateDirectTestData(
                triangleCount: 2048);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
            {
                UnityRendererAlphaAnalysis.Classify(
                    indices,
                    positions,
                    uvs,
                    resolution,
                    Array.Empty<IReadOnlyList<Vector2>>(),
                    cts.Token);
            });
        }

        [Test]
        public void Classify_WithDefaultToken_CompletesNormally()
        {
            var (indices, positions, uvs, resolution) = CreateDirectTestData(
                triangleCount: 4);

            var outcomes = UnityRendererAlphaAnalysis.Classify(
                indices,
                positions,
                uvs,
                resolution,
                Array.Empty<IReadOnlyList<Vector2>>(),
                default);

            Assert.That(outcomes, Has.Length.EqualTo(4));
            Assert.That(outcomes, Has.All.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }

        private static (UnityRendererAlphaSnapshot Snapshot, SlotResolutionResult[] SlotResults) CreateTestMesh(
            int triangleCount,
            bool resolvedAsOpaque)
        {
            var vertexCount = triangleCount * 3;
            var positions = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var indices = new int[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                positions[i] = new Vector3(i, 0f, 0f);
                uvs[i] = new Vector2(0.5f, 0.5f);
                indices[i] = i;
            }

            var submesh = new UnitySubmeshAlphaSnapshot(0, 0, indices);
            var snapshot = new UnityRendererAlphaSnapshot(
                vertexCount,
                positions,
                uvs,
                true,
                new[] { submesh },
                Array.Empty<CapturedAlphaMaterial>());

            var slotResult = resolvedAsOpaque
                ? SlotResolutionResult.Resolved(new[]
                {
                    AlphaResolution.Uniform(TriangleAlphaOutcome.ProvenOpaque),
                })
                : SlotResolutionResult.Refused(
                    RendererAnalysisRefusal.MaterialDependencyClosureFailed);
            return (snapshot, new[] { slotResult });
        }

        private static (int[] Indices, Vector3[] Positions, Vector2[] Uvs, AlphaResolution Resolution) CreateDirectTestData(
            int triangleCount)
        {
            var vertexCount = triangleCount * 3;
            var positions = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var indices = new int[vertexCount];
            for (var i = 0; i < vertexCount; i++)
            {
                positions[i] = new Vector3(i, 0f, 0f);
                uvs[i] = new Vector2(0.5f, 0.5f);
                indices[i] = i;
            }

            var resolution = AlphaResolution.Uniform(
                TriangleAlphaOutcome.ProvenOpaque);

            return (indices, positions, uvs, resolution);
        }
    }
}
