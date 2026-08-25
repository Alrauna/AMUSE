using System;
using System.Collections.Generic;
using System.Reflection;
using Alrauna.Amuse.Editor.Analysis;
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
            var originalCapturedMaterial = extraction.Snapshot.Materials[0];

            renderer.sharedMesh = Triangle();
            var replacementMaterial = NewMaterial();
            replacementMaterial.color = new Color(1f, 1f, 1f, 0f);
            renderer.sharedMaterials = new[] { replacementMaterial };
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

            var freshExtraction = UnityRendererAlphaAnalysis.Capture(renderer);
            var replacementCapturedMaterial =
                freshExtraction.Snapshot.Materials[0];
            MaterialSemantics Resolve(CapturedAlphaMaterial material)
            {
                if (ReferenceEquals(material, originalCapturedMaterial))
                {
                    return ConstantAlpha(1f);
                }

                if (ReferenceEquals(material, replacementCapturedMaterial))
                {
                    return ConstantAlpha(0f);
                }

                return UnityMaterialSemantics.AllUnknown();
            }

            var captured = UnityRendererAlphaAnalysis.Analyze(
                extraction.Snapshot, Resolve);
            var fresh = UnityRendererAlphaAnalysis.Analyze(
                freshExtraction.Snapshot, Resolve);

            Assert.That(captured.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(
                fresh.Plan.OpaqueTriangleCount,
                Is.Zero,
                "A fresh capture must observe replacement material semantics.");
            Assert.That(
                fresh.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                fresh.Plan.Source.Submeshes[0].Outcomes,
                Is.EqualTo(new[]
                {
                    TriangleAlphaOutcome.MustRemainTransparent,
                }));
            Assert.That(
                fresh.Plan.TransparentTriangleCount,
                Is.EqualTo(1),
                "A fresh capture must also observe the one-triangle " +
                "replacement mesh.");
        }

        [Test]
        public void SnapshotTypesContainNoUnityObjectHandles()
        {
            AssertHasNoUnityObjectFields(typeof(UnityRendererAlphaSnapshot));
            AssertHasNoUnityObjectFields(typeof(UnitySubmeshAlphaSnapshot));
        }

        // Guard fixtures. Every one stores its data in an auto-property, so the
        // only instance fields are compiler-generated non-public backing fields:
        // a guard that inspected public fields alone would see nothing at all.

        private sealed class NestedHolder
        {
            internal InnerHolder Inner { get; }
        }

        private sealed class InnerHolder
        {
            internal Material Live { get; }
        }

        private sealed class ArrayHolder
        {
            internal InnerHolder[] Values { get; }
        }

        private sealed class GenericArgumentHolder
        {
            // An interface exposes no field path to its element type, so only
            // the declared generic argument reveals what this can hold.
            internal IReadOnlyList<InnerHolder> Values { get; }
        }

        private sealed class CycleA
        {
            internal CycleB Next { get; }
            internal int Depth { get; }
        }

        private sealed class CycleB
        {
            internal CycleA Back { get; }
            internal string Label { get; }
        }

        [Test]
        public void GuardCatchesUnityObjectsNestedBelowTheFirstLevel()
        {
            // The shallow guard passed this. The recursive guard must not.
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(NestedHolder)),
                Throws.InstanceOf<AssertionException>());
        }

        [Test]
        public void GuardCatchesUnityObjectsBehindAnArrayElementType()
        {
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(ArrayHolder)),
                Throws.InstanceOf<AssertionException>());
        }

        [Test]
        public void GuardCatchesUnityObjectsBehindADeclaredGenericArgument()
        {
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(GenericArgumentHolder)),
                Throws.InstanceOf<AssertionException>());
        }

        private abstract class InheritedPrivateBase
        {
            // Private, so GetFields on the derived type alone never returns it.
            private Material Live { get; }
        }

        private sealed class InheritedPrivateHolder : InheritedPrivateBase
        {
            internal int Depth { get; }
        }

        private sealed class ObjectFieldHolder
        {
            internal object Value { get; }
        }

        [Test]
        public void GuardCatchesUnityObjectsInAPrivateBaseClassField()
        {
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(InheritedPrivateHolder)),
                Throws.InstanceOf<AssertionException>());
        }

        [Test]
        public void GuardRejectsASystemObjectField()
        {
            // A field declared as object is a declared path to every Unity
            // object, so the theorem must treat it as a violation, not a leaf.
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(ObjectFieldHolder)),
                Throws.InstanceOf<AssertionException>());
        }

        [Test]
        public void GuardTerminatesOnACleanRecursiveTypeGraph()
        {
            // CycleA and CycleB reference each other and hold no Unity object.
            // Without visited-type protection this recurses forever.
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(CycleA)),
                Throws.Nothing);
        }

        [Test]
        public void CapturedAnimationEvidenceHoldsNoLiveUnityObject()
        {
            AssertHasNoUnityObjectFields(typeof(CapturedAnimationEvidence));
            AssertHasNoUnityObjectFields(typeof(CapturedClipEvidence));
            AssertHasNoUnityObjectFields(typeof(CapturedFloatBinding));
            AssertHasNoUnityObjectFields(typeof(CapturedObjectBinding));

            // Not reachable from CapturedAnimationEvidence, so it needs its own
            // root rather than riding along on the graph above.
            AssertHasNoUnityObjectFields(typeof(CapturedMaterialSlotEvidence));
        }

        [Test]
        public void TransientObservationIsDeliberatelyExemptAndDocumented()
        {
            // LiveObjectObservation intentionally holds live references and is
            // confined to host capture. This test pins that it is NOT evidence, so
            // a future refactor cannot quietly promote it into the proof path, and
            // it is the positive control proving the guard still detects a live
            // reference rather than passing every type in the project. This one
            // covers the generic-argument route; NestedHolder covers a directly
            // Unity-object-typed field.
            Assert.That(
                () => AssertHasNoUnityObjectFields(typeof(LiveObjectObservation)),
                Throws.InstanceOf<AssertionException>(),
                "LiveObjectObservation must remain transient host-only");
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

        private static MaterialSemantics ConstantAlpha(float alpha)
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<ScalarSemanticValue>.Complete(
                    ScalarSemanticValue.Constant(alpha)),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        /// <summary>
        /// Proves that no path through the declared type graph of captured
        /// evidence can reach a <see cref="UnityEngine.Object"/>. This inspects
        /// type structure only: it never instantiates anything and never reads a
        /// runtime value, so a field that merely happens to be null today cannot
        /// make a live-typed field look safe.
        /// <para>
        /// The claim is bounded by what a declared type can state. A field typed
        /// as a non-generic interface names no element type, so an implementation
        /// that wraps a live handle would pass. <c>System.Object</c> is the one
        /// such escape hatch this can name, and it is rejected outright.
        /// </para>
        /// </summary>
        private static void AssertHasNoUnityObjectFields(Type type)
        {
            AssertHasNoUnityObjectFields(type, type.Name, new HashSet<Type>());
        }

        private static void AssertHasNoUnityObjectFields(
            Type type,
            string path,
            HashSet<Type> visited)
        {
            // The visited set is per top-level invocation, so one test can never
            // suppress a later one, and it terminates recursive type graphs.
            if (type == null || !visited.Add(type))
            {
                return;
            }

            Assert.That(
                typeof(UnityEngine.Object).IsAssignableFrom(type),
                Is.False,
                $"{path} reaches the Unity object type {type.FullName}.");

            // object names no structure at all, so it is a declared path to
            // every Unity object rather than a leaf that holds nothing.
            Assert.That(
                type == typeof(object),
                Is.False,
                $"{path} is declared as System.Object, which can hold any " +
                "Unity object.");

            // Leaves that cannot carry a reference to anything else.
            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                return;
            }

            if (type.IsArray)
            {
                AssertHasNoUnityObjectFields(
                    type.GetElementType(), path + "[]", visited);
                return;
            }

            // Declared generic arguments are proof-relevant on their own: an
            // evidence field declared as IReadOnlyList<T> exposes no field path
            // to T at all, so only the argument itself shows what it can hold.
            foreach (var argument in type.GetGenericArguments())
            {
                AssertHasNoUnityObjectFields(
                    argument, $"{path}<{argument.Name}>", visited);
            }

            // GetFields does not return private fields of base types, so walk
            // the hierarchy explicitly and take each level's own declarations.
            for (var declaring = type;
                 declaring != null && declaring != typeof(object);
                 declaring = declaring.BaseType)
            {
                foreach (var field in declaring.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    AssertHasNoUnityObjectFields(
                        field.FieldType, $"{path}.{field.Name}", visited);
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
