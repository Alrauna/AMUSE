using System;
using System.Collections.Generic;
using System.Reflection;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityRendererAlphaSnapshotTests
    {
        private readonly List<UnityEngine.Object> _transient =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _transient)
            {
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            _transient.Clear();
        }

        [Test]
        public void CapturedProofDoesNotChangeAfterEveryLiveInputChanges()
        {
            var originalMesh = Quad();
            var originalMaterial = NewMaterial();
            var renderer = NewRenderer(originalMesh, originalMaterial);

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);

            renderer.sharedMesh = Triangle();
            renderer.sharedMaterials = new[] { NewMaterial() };
            originalMesh.vertices = new[]
            {
                new Vector3(10f, 10f, 10f),
                new Vector3(11f, 10f, 10f),
                new Vector3(11f, 11f, 10f),
                new Vector3(10f, 11f, 10f),
            };
            originalMesh.uv = Array.Empty<Vector2>();
            originalMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            originalMaterial.color = new Color(1f, 1f, 1f, 0f);

            var captured = UnityRendererAlphaAnalysis.Analyze(
                extraction.Snapshot, ConstantOpaque);
            var fresh = UnityRendererAlphaAnalysis.Analyze(
                UnityRendererAlphaAnalysis.Capture(renderer).Snapshot,
                ConstantOpaque);

            Assert.That(captured.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(
                fresh.Plan.OpaqueTriangleCount,
                Is.EqualTo(1),
                "A fresh capture must observe the replacement mesh.");
        }

        [Test]
        public void SnapshotTypesContainNoUnityObjectHandles()
        {
            AssertHasNoUnityObjectFields(typeof(UnityRendererAlphaSnapshot));
            AssertHasNoUnityObjectFields(typeof(UnitySubmeshAlphaSnapshot));
        }

        [Test]
        public void MutationTargetHoldsOnlyTheAcceptedLiveHandles()
        {
            var mesh = Quad();
            var renderer = NewRenderer(mesh, NewMaterial());

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);

            Assert.That(extraction.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(extraction.Snapshot, Is.Not.Null);
            Assert.That(extraction.MutationTarget.Renderer, Is.SameAs(renderer));
            Assert.That(extraction.MutationTarget.ExpectedMesh, Is.SameAs(mesh));
            Assert.That(extraction.MutationTarget.ExpectedMaterialSlotCount, Is.EqualTo(1));
        }

        [Test]
        public void RefusalCarriesNeitherSnapshotNorMutationTarget()
        {
            var gameObject = Track(new GameObject("amuse-refused-snapshot-test"));
            var renderer = gameObject.AddComponent<LineRenderer>();

            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);

            Assert.That(
                extraction.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnsupportedRendererType));
            Assert.That(extraction.Snapshot, Is.Null);
            Assert.That(extraction.MutationTarget, Is.Null);
        }

        private static MaterialSemantics ConstantOpaque(
            CapturedAlphaMaterial material)
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(1f)),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        private static void AssertHasNoUnityObjectFields(Type type)
        {
            foreach (var field in type.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                Assert.That(
                    typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType),
                    Is.False,
                    $"{type.Name}.{field.Name} directly stores a Unity object.");
                foreach (var argument in field.FieldType.GetGenericArguments())
                {
                    Assert.That(
                        typeof(UnityEngine.Object).IsAssignableFrom(argument),
                        Is.False,
                        $"{type.Name}.{field.Name} stores Unity object elements.");
                }
            }
        }

        private SkinnedMeshRenderer NewRenderer(
            Mesh mesh,
            params Material[] materials)
        {
            var gameObject = Track(new GameObject("amuse-snapshot-test"));
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private Mesh Quad()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            return mesh;
        }

        private Mesh Triangle()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        private Material NewMaterial()
        {
            return Track(new Material(Shader.Find("Unlit/Color")));
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _transient.Add(value);
            return value;
        }
    }
}
