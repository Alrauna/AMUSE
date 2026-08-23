using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Renderer-level contract and refusal matrix. Meshes are built
    /// procedurally, so they are always readable and no asset is imported;
    /// import behaviour is characterized separately in
    /// <see cref="MeshReadabilityCharacterizationTests"/>.
    /// </summary>
    public sealed class UnityRendererAlphaAnalysisTests
    {
        private readonly List<Object> _transient = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _transient.Add(obj);
            return obj;
        }

        /// <summary>Two triangles over four vertices, UV0 present.</summary>
        private Mesh Quad(MeshTopology topology = MeshTopology.Triangles)
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.6f, 0.9f)
            };
            if (topology == MeshTopology.Triangles)
            {
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            }
            else
            {
                mesh.SetIndices(new[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0);
            }

            return mesh;
        }

        private Material NewMaterial()
        {
            return Track(new Material(Shader.Find("Unlit/Color")));
        }

        private SkinnedMeshRenderer NewSkinned(Mesh mesh, params Material[] slots)
        {
            var gameObject = Track(new GameObject("amuse-test"));
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = slots;
            return renderer;
        }

        /// <summary>
        /// A real, non-empty block overriding a property the fixture material
        /// genuinely declares. Overriding a declared property rather than an
        /// invented name keeps the fixture honest about what a block does.
        /// </summary>
        private static MaterialPropertyBlock ColorOverrideBlock()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", new Color(1f, 1f, 1f, 0.25f));
            return block;
        }

        [Test]
        public void UnsupportedRendererTypeRefusesWithoutAPlan()
        {
            var gameObject = Track(new GameObject("amuse-test-line"));
            var renderer = gameObject.AddComponent<LineRenderer>();

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnsupportedRendererType));
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Submeshes, Is.Empty);
        }

        /// <summary>
        /// A property block can override the very properties the shader
        /// frontends read to prove alpha, so a base-material ProvenOpaque
        /// conclusion could be false for this renderer. The guard reads the
        /// presence bit only; the block's contents are never inspected.
        /// </summary>
        [Test]
        public void APropertyBlockRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad(), NewMaterial());
            renderer.SetPropertyBlock(ColorOverrideBlock());

            Assert.That(
                renderer.HasPropertyBlock(),
                Is.True,
                "The fixture must attach a real block, or it proves nothing.");

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent));
            Assert.That(result.Plan, Is.Null);
        }

        /// <summary>
        /// Verifies the guard is not blind to a block attached to one material
        /// index rather than the whole renderer. If HasPropertyBlock() does not
        /// report this, the guard has a hole and implementation must stop for
        /// architectural review rather than reach for a wider API.
        /// </summary>
        [Test]
        public void APerMaterialIndexPropertyBlockAlsoRefuses()
        {
            var renderer = NewSkinned(Quad(), NewMaterial());
            renderer.SetPropertyBlock(ColorOverrideBlock(), 0);

            Assert.That(
                renderer.HasPropertyBlock(),
                Is.True,
                "STOP CONDITION: HasPropertyBlock() does not report a " +
                "per-material-index block, so the guard has a hole. Escalate " +
                "for architectural review; do not widen the API here.");

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(
                    RendererAnalysisRefusal.MaterialPropertyOverridesPresent));
        }

        [Test]
        public void SkinnedRendererWithoutAMeshRefusesWithMissingMesh()
        {
            var renderer = NewSkinned(null, NewMaterial());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.MissingMesh));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void MeshRendererWithoutAMeshFilterRefusesWithMissingMesh()
        {
            var gameObject = Track(new GameObject("amuse-test-mesh"));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { NewMaterial() };

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.MissingMesh));
        }

        /// <summary>
        /// Unity's behaviour for surplus materials is documented — the last
        /// submesh is drawn again for each one — but SubmeshSeparationInput
        /// carries exactly one SourceMaterialBindingIndex per source submesh, so
        /// AMUSE cannot represent those extra passes. Refusal, not a guess.
        /// </summary>
        [Test]
        public void MoreMaterialsThanSubmeshesRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad(), NewMaterial(), NewMaterial());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void FewerMaterialsThanSubmeshesRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnprovenMaterialSlotMapping));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void NonTriangleTopologyRefusesTheWholeRenderer()
        {
            var renderer = NewSkinned(Quad(MeshTopology.Quads), NewMaterial());

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.UnsupportedTopology));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void NullRendererThrowsBecauseItIsACallerDefect()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => UnityRendererAlphaAnalysis.Analyze(null));
        }

        /// <summary>
        /// A captured-material resolver that proves constant alpha 1 for the
        /// nominated slots and knows nothing about any other. A constant alpha is
        /// geometry-independent, so it isolates composition from evidence — and
        /// it is exactly the resolution that must still prove opacity when UV0
        /// is unavailable.
        /// </summary>
        private static RendererAlphaAnalysis AnalyzeOpaque(
            Renderer renderer,
            params int[] supportedSlots)
        {
            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);
            Assert.That(
                extraction.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None));
            var supported = new HashSet<CapturedAlphaMaterial>();
            foreach (var slot in supportedSlots)
            {
                supported.Add(extraction.Snapshot.Materials[slot]);
            }

            return UnityRendererAlphaAnalysis.Analyze(
                extraction.Snapshot,
                material => supported.Contains(material)
                    ? new MaterialSemantics(
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<ScalarSemanticValue>.Complete(
                        ScalarSemanticValue.Constant(1f)),
                    SemanticOutput<ColorSemanticValue>.Unknown(),
                    SemanticOutput<NormalSemanticValue>.Unknown())
                    : UnityMaterialSemantics.AllUnknown());
        }

        /// <summary>Two submeshes: one triangle, then two triangles.</summary>
        private Mesh TwoSubmeshMesh()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(2f, 0f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.6f, 0.9f),
                new Vector2(0.7f, 0.7f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 3, 1, 4, 2 }, 1);
            return mesh;
        }

        [Test]
        public void ConstantOpaqueAlphaMakesTheWholeSubmeshAnOpaqueCandidate()
        {
            var material = NewMaterial();
            var renderer = NewSkinned(Quad(), material);

            var result = AnalyzeOpaque(renderer, 0);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Plan, Is.Not.Null);
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(result.Plan.RequiresAnySplit, Is.False);
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(result.Submeshes[0].HasMaterial, Is.True);
        }

        [Test]
        public void MeshRendererPathReachesTheSamePlanAsTheSkinnedPath()
        {
            var material = NewMaterial();
            var gameObject = Track(new GameObject("amuse-test-mesh"));
            gameObject.AddComponent<MeshFilter>().sharedMesh = Quad();
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material };

            var result = AnalyzeOpaque(renderer, 0);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
        }

        [Test]
        public void AnUnsupportedSlotDoesNotPoisonItsSupportedNeighbour()
        {
            var supported = NewMaterial();
            var unsupported = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), unsupported, supported);

            var result = AnalyzeOpaque(renderer, 1);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
        }

        [Test]
        public void SubmeshAndMaterialSlotIndicesAgreeWithTheSourceOrder()
        {
            var supported = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), NewMaterial(), supported);

            var result = AnalyzeOpaque(renderer, 1);

            for (var index = 0; index < result.Submeshes.Count; index++)
            {
                Assert.That(result.Submeshes[index].SubmeshIndex, Is.EqualTo(index));
                Assert.That(
                    result.Submeshes[index].MaterialSlotIndex, Is.EqualTo(index));
                Assert.That(
                    result.Plan.Submeshes[index].SourceMaterialBindingIndex,
                    Is.EqualTo(index));
            }
        }

        [Test]
        public void ARepeatedMaterialIsAnalyzedIdenticallyInEverySubmesh()
        {
            var material = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), material, material);

            var result = AnalyzeOpaque(renderer, 0, 1);

            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(3));
        }

        [Test]
        public void ANullMaterialSlotIsRecordedAndPreserved()
        {
            // An explicit one-element array: `NewSkinned(Quad(), null)` would
            // bind null to the params array itself, not to a slot in it.
            var renderer = NewSkinned(Quad(), new Material[] { null });

            var result = UnityRendererAlphaAnalysis.Analyze(renderer);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(result.Submeshes[0].HasMaterial, Is.False);
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(0));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.False);
        }

        [Test]
        public void AnEmptySubmeshIsRepresentedWithoutShiftingItsNeighbour()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(1f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.9f, 0.9f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new int[0], 0);
            mesh.SetTriangles(new[] { 0, 1, 2 }, 1);
            var supported = NewMaterial();
            var renderer = NewSkinned(mesh, NewMaterial(), supported);

            var result = AnalyzeOpaque(renderer, 1);

            Assert.That(result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty);
            Assert.That(
                result.Plan.Submeshes[0].TransparentTriangleOrdinals, Is.Empty);
            Assert.That(
                result.Plan.Submeshes[1].SourceMaterialBindingIndex, Is.EqualTo(1));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(1));
        }

        /// <summary>
        /// The dependency rule: a constant alpha of exactly one cannot vary
        /// across a surface, so it needs no UV at all. Turning it into Unknown
        /// merely because the mesh carries no UV0 would discard a conclusion
        /// that never depended on the missing knowledge.
        /// </summary>
        [Test]
        public void MissingUv0DoesNotBlockUvIndependentConstantProof()
        {
            var mesh = Quad();
            mesh.uv = null;
            var material = NewMaterial();
            var renderer = NewSkinned(mesh, material);

            var result = AnalyzeOpaque(renderer, 0);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.OpaqueTriangleCount,
                Is.EqualTo(2),
                "A constant alpha of one is provable without UV0.");
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
        }

        /// <summary>
        /// The same dependency rule, at triangle scope: a non-finite UV makes
        /// UV0 unavailable for that one triangle, and a UV-independent proof
        /// must survive it. The finiteness screen exists because the classifier
        /// throws on non-finite UVs, not because non-finite implies Unknown.
        /// </summary>
        [Test]
        public void ANonFiniteUvDoesNotBlockUvIndependentConstantProof()
        {
            var mesh = TwoSubmeshMesh();
            var uv = mesh.uv;
            uv[4] = new Vector2(float.NaN, 0f);
            mesh.uv = uv;
            var material = NewMaterial();
            var renderer = NewSkinned(mesh, material, material);

            var result = AnalyzeOpaque(renderer, 0, 1);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Plan.OpaqueTriangleCount,
                Is.EqualTo(3),
                "Every triangle stays provable: none of them needed UV0.");
            Assert.That(result.Plan.TransparentTriangleCount, Is.EqualTo(0));
        }

        [Test]
        public void AnalyzingTheSameRendererTwiceProducesTheSameResult()
        {
            var material = NewMaterial();
            var renderer = NewSkinned(TwoSubmeshMesh(), material, material);

            var first = AnalyzeOpaque(renderer, 0, 1);
            var second = AnalyzeOpaque(renderer, 0, 1);

            Assert.That(
                second.Plan.OpaqueTriangleCount,
                Is.EqualTo(first.Plan.OpaqueTriangleCount));
            Assert.That(
                second.Plan.TransparentTriangleCount,
                Is.EqualTo(first.Plan.TransparentTriangleCount));
            Assert.That(
                second.Submeshes.Count, Is.EqualTo(first.Submeshes.Count));
            for (var index = 0; index < first.Submeshes.Count; index++)
            {
                Assert.That(
                    second.Submeshes[index].Failure,
                    Is.EqualTo(first.Submeshes[index].Failure));
                Assert.That(
                    second.Plan.Submeshes[index].Disposition,
                    Is.EqualTo(first.Plan.Submeshes[index].Disposition));
            }
        }
    }
}
