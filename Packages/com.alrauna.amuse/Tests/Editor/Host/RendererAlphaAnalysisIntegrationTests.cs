using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// The renderer-to-plan vertical slice: a real Renderer, a real Mesh with
    /// two submeshes and two material slots, a real imported Texture2D, the real
    /// request-selective captured alpha evidence, the real
    /// AlphaSemanticsResolver, the real exact TriangleAlphaClassifier, and the
    /// real MeshSeparationPlanner.
    /// <para>
    /// Exactly one link is substituted: vendor shader source attestation. The
    /// public development project installs neither Poiyomi nor lilToon, so no
    /// material here can be attested. The semantics used are nonetheless
    /// genuine PoiyomiMaterialSemantics output, obtained over a real Material
    /// through that frontend's existing InterpretVerifiedMaterial seam. This
    /// test does not exercise, and does not claim to exercise, vendor frontend
    /// dispatch.
    /// </para>
    /// <para>
    /// The fixture is asymmetric on every axis that could silently compensate
    /// for a wiring error: the opaque-looking geometry sits on the
    /// <em>unsupported</em> slot, the two submeshes have different triangle
    /// counts, and the single non-opaque texel sits in a corner, so a swapped
    /// slot mapping, an off-by-one submesh index, or a flipped row order each
    /// change the expected result.
    /// </para>
    /// </summary>
    public sealed class RendererAlphaAnalysisIntegrationTests
    {
        private const string TempFolder = "Assets/AmuseTests_RendererIntegration";
        private const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";
        private const int Size = 4;

        private readonly List<Object> _transient = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.CreateFolder(
                    "Assets", "AmuseTests_RendererIntegration");
            }
        }

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
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }
        }

        private T Track<T>(T obj) where T : Object
        {
            _transient.Add(obj);
            return obj;
        }

        /// <summary>
        /// 4x4 RGBA32, uncompressed, no mips, Point/Clamp, with exactly one
        /// non-opaque texel at (0,0). Under Point/Clamp that texel owns
        /// UV [0, 0.25) x [0, 0.25). A uniform texture would short-circuit on
        /// IsFullyOpaque before geometry was examined and would pass even if the
        /// wiring were wrong.
        /// </summary>
        private static Texture2D ImportTexture(string name, bool readable)
        {
            var path = TempFolder + "/" + name + ".png";
            var staging = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var pixels = new Color32[Size * Size];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(64, 32, 16, 255);
            }

            pixels[0] = new Color32(64, 32, 16, 128);
            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(path, staging.EncodeToPNG());
            Object.DestroyImmediate(staging);

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"'{path}' must import.");
            return loaded;
        }

        /// <summary>
        /// The exact state PoiyomiMaterialSemantics.InterpretAlpha requires to
        /// reach ScalarSemanticValue.Texture(sample, Alpha): off the forced
        /// path, mask mode off, full colour alpha, and an assigned _MainTex with
        /// an identity-resolvable asset. Every other alpha gate is already zero
        /// at the stand-in shader's declared default; only _AlphaForceOpaque
        /// (declared 1) and _MainAlphaMaskMode (declared 2) need changing.
        /// </summary>
        private Material NewSampledAlphaMaterial(Texture2D mainTex)
        {
            var shader = Shader.Find(FixtureShaderName);
            Assert.That(
                shader, Is.Not.Null, $"'{FixtureShaderName}' must import.");
            var material = Track(new Material(shader));
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            return material;
        }

        /// <summary>
        /// Submesh 0: two triangles whose UVs lie in the fully opaque region,
        /// bound to the unsupported slot. Submesh 1: three triangles, two in the
        /// opaque region and one wholly inside the non-opaque texel, bound to
        /// the supported slot.
        /// </summary>
        private Mesh BuildFixtureMesh()
        {
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),   // 0: opaque region
                new Vector3(1f, 0f, 0f),   // 1
                new Vector3(1f, 1f, 0f),   // 2
                new Vector3(0f, 1f, 0f),   // 3
                new Vector3(2f, 0f, 0f),   // 4
                new Vector3(2f, 1f, 0f),   // 5
                new Vector3(3f, 0f, 0f),   // 6: non-opaque texel
                new Vector3(3f, 1f, 0f),   // 7
                new Vector3(4f, 0f, 0f)    // 8
            };
            mesh.uv = new[]
            {
                new Vector2(0.55f, 0.55f),
                new Vector2(0.9f, 0.55f),
                new Vector2(0.9f, 0.9f),
                new Vector2(0.55f, 0.9f),
                new Vector2(0.6f, 0.5f),
                new Vector2(0.85f, 0.8f),
                new Vector2(0.01f, 0.01f),
                new Vector2(0.2f, 0.01f),
                new Vector2(0.01f, 0.2f)
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.SetTriangles(new[] { 0, 4, 5, 6, 7, 8, 1, 2, 4 }, 1);
            return mesh;
        }

        private SkinnedMeshRenderer NewRenderer(Mesh mesh, params Material[] slots)
        {
            var gameObject = Track(new GameObject("amuse-integration"));
            var renderer = gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = slots;
            return renderer;
        }

        /// <summary>
        /// Captures the renderer normally, then substitutes the fixture's exact
        /// Poiyomi alpha request for nominated slots. Only immutable requested
        /// evidence and a captured-material resolver cross into analysis.
        /// </summary>
        private static RendererAlphaAnalysis AnalyzeVerified(
            Renderer renderer,
            params Material[] verifiedSlots)
        {
            var extraction = UnityRendererAlphaAnalysis.Capture(renderer);
            Assert.That(
                extraction.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                verifiedSlots.Length,
                Is.EqualTo(extraction.Snapshot.Materials.Count));

            var inputs = new List<MaterialEvidenceCaptureInput>();
            var slotIndices = new List<int>();
            for (var slot = 0; slot < verifiedSlots.Length; slot++)
            {
                if (verifiedSlots[slot] == null)
                {
                    continue;
                }

                inputs.Add(new MaterialEvidenceCaptureInput(
                    verifiedSlots[slot],
                    PoiyomiMaterialSemantics.AlphaEvidenceRequest));
                slotIndices.Add(slot);
            }

            var capturedEvidence = UnityMaterialEvidenceCapture.Capture(inputs);
            var materials = new CapturedAlphaMaterial[
                extraction.Snapshot.Materials.Count];
            for (var slot = 0; slot < materials.Length; slot++)
            {
                materials[slot] = extraction.Snapshot.Materials[slot];
            }

            var verified = new HashSet<CapturedAlphaMaterial>();
            for (var index = 0; index < capturedEvidence.Count; index++)
            {
                var material = new CapturedAlphaMaterial(
                    CapturedAlphaMaterialFamily.Unsupported,
                    capturedEvidence[index],
                    default(PoiyomiSourceEvidence),
                    null);
                materials[slotIndices[index]] = material;
                verified.Add(material);
            }

            var snapshot = new UnityRendererAlphaSnapshot(
                extraction.Snapshot.VertexCount,
                extraction.Snapshot.Positions,
                extraction.Snapshot.Uv0,
                extraction.Snapshot.HasUv0,
                extraction.Snapshot.Submeshes,
                materials);
            return UnityRendererAlphaAnalysis.Analyze(
                snapshot,
                material => verified.Contains(material)
                    ? new MaterialSemantics(
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                            material.Evidence),
                        SemanticOutput<ColorSemanticValue>.Unknown(),
                        SemanticOutput<NormalSemanticValue>.Unknown())
                    : UnityMaterialSemantics.AllUnknown());
        }

        [Test]
        public void RendererToPlanSeparatesProvenOpaqueGeometryFromPreservedGeometry()
        {
            var texture = ImportTexture("mixed", readable: true);
            var unsupported = Track(new Material(Shader.Find("Unlit/Color")));
            var supported = NewSampledAlphaMaterial(texture);
            var renderer = NewRenderer(
                BuildFixtureMesh(), unsupported, supported);

            var result = AnalyzeVerified(renderer, null, supported);

            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None),
                "The fixture renderer must be fully supported.");
            Assert.That(result.Plan, Is.Not.Null);

            // Slot 0 carries the opaque-looking geometry but no provable
            // semantics; a swapped slot mapping would turn this into a Split.
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.SemanticsUnknown));
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            Assert.That(
                result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty);

            // Slot 1 is the proven one, and it must split.
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Split));
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 0, 2 }),
                "Triangles 0 and 2 lie wholly in the opaque region.");
            Assert.That(
                result.Plan.Submeshes[1].TransparentTriangleOrdinals,
                Is.EqualTo(new[] { 1 }),
                "Triangle 1 lies wholly inside the one non-opaque texel; " +
                "proving it opaque would be a false positive.");

            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
            Assert.That(result.Plan.TransparentTriangleCount, Is.EqualTo(3));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
            Assert.That(result.Plan.RequiresAnySplit, Is.True);
        }

        /// <summary>
        /// The other half of the UV dependency rule: this equation genuinely
        /// samples a texture, so without UV0 the proof cannot be completed. The
        /// unit suite proves the constant-alpha half, where UV0 is irrelevant;
        /// neither half alone establishes the rule.
        /// <para>
        /// Note precisely what does and does not happen. The AlphaResolution
        /// stays <em>resolved</em> and SubmeshAlphaAnalysis.Failure stays None:
        /// the material and its evidence were proven perfectly well. Each
        /// triangle is then classified through MissingUv0, and the sampled
        /// classifier returns TriangleAlphaOutcome.Unknown because it has no
        /// coordinates to evaluate its predicate at. This is triangle-local
        /// uncertainty, not a resolution refusal, and the assertions below pin
        /// exactly that distinction.
        /// </para>
        /// </summary>
        [Test]
        public void MissingUv0MakesAUvDependentSampledProofUnknown()
        {
            var texture = ImportTexture("sampled_no_uv", readable: true);
            var supported = NewSampledAlphaMaterial(texture);
            var mesh = BuildFixtureMesh();
            mesh.uv = null;
            var renderer = NewRenderer(mesh, supported, supported);

            var result = AnalyzeVerified(renderer, supported, supported);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None),
                "The material still resolves; only the geometry lacks UV0.");
            Assert.That(
                result.Plan.OpaqueTriangleCount,
                Is.EqualTo(0),
                "A sampled alpha cannot be proven without the UVs it samples " +
                "at; the proof is blocked triangle-locally, not by a refusal.");
            Assert.That(
                result.Plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
        }

        /// <summary>
        /// The same, at triangle scope: one non-finite UV removes UV0 for that
        /// triangle alone, and under a sampled equation only that triangle
        /// becomes Unknown.
        /// </summary>
        [Test]
        public void ANonFiniteUvMakesOnlyItsOwnSampledTriangleUnknown()
        {
            var texture = ImportTexture("sampled_nan_uv", readable: true);
            var supported = NewSampledAlphaMaterial(texture);
            var mesh = BuildFixtureMesh();
            var uv = mesh.uv;
            uv[5] = new Vector2(float.NaN, 0f);   // used only by submesh 1, triangle 0
            mesh.uv = uv;
            var renderer = NewRenderer(mesh, supported, supported);

            var result = AnalyzeVerified(renderer, supported, supported);

            Assert.That(result.Refusal, Is.EqualTo(RendererAnalysisRefusal.None));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 2 }),
                "Triangle 0 lost its UVs; triangle 2 kept them and stays proven.");
        }

        /// <summary>
        /// The characterization the next branch's prioritization depends on. One
        /// slot's alpha texture is non-readable and the other's is not. The
        /// refusal must stay inside the slot that owns it, and a useful partial
        /// plan must survive.
        /// <para>
        /// The fixture deliberately creates the refusal. No importer state is
        /// toggled to work around it. Non-readable texture support is deferred,
        /// and this milestone measures the blast radius rather than removing it.
        /// </para>
        /// </summary>
        [Test]
        public void ANonReadableAlphaTextureRefusesOnlyItsOwnSubmesh()
        {
            var nonReadable = ImportTexture("non_readable", readable: false);
            var readable = ImportTexture("readable", readable: true);
            var blocked = NewSampledAlphaMaterial(nonReadable);
            var proven = NewSampledAlphaMaterial(readable);
            var renderer = NewRenderer(BuildFixtureMesh(), blocked, proven);

            var result = AnalyzeVerified(renderer, blocked, proven);

            // Where the refusal emerges, and its shape.
            Assert.That(
                result.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None),
                "A non-readable texture must not refuse the whole renderer.");
            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));

            // Its blast radius: that submesh only.
            Assert.That(
                result.Plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            Assert.That(
                result.Plan.Submeshes[0].OpaqueTriangleOrdinals, Is.Empty);

            // What survives it.
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.None));
            Assert.That(
                result.Plan.Submeshes[1].OpaqueTriangleOrdinals,
                Is.EqualTo(new[] { 0, 2 }));
            Assert.That(result.Plan.HasAnyOpaqueCandidates, Is.True);
            Assert.That(result.Plan.OpaqueTriangleCount, Is.EqualTo(2));
        }

        /// <summary>
        /// Analysis is observational. Every source object must be structurally
        /// identical afterwards, and the imported texture asset must not have
        /// been re-imported or rewritten.
        /// </summary>
        [Test]
        public void AnalysisLeavesEverySourceObjectUnchanged()
        {
            var texture = ImportTexture("immutable", readable: true);
            var texturePath = AssetDatabase.GetAssetPath(texture);
            var supported = NewSampledAlphaMaterial(texture);
            var mesh = BuildFixtureMesh();
            var renderer = NewRenderer(mesh, supported, supported);

            var beforeAssetHash =
                AssetDatabase.GetAssetDependencyHash(texturePath);
            var beforeReadable = texture.isReadable;
            var beforePixels = texture.GetPixels32(0);
            var beforeVertices = mesh.vertices;
            var beforeUv = mesh.uv;
            var beforeSubmesh0 = mesh.GetIndices(0);
            var beforeSubmesh1 = mesh.GetIndices(1);
            var beforeVertexCount = mesh.vertexCount;
            var beforeSubMeshCount = mesh.subMeshCount;
            var beforeMaterials = renderer.sharedMaterials;
            var beforeSharedMesh = renderer.sharedMesh;
            var beforeMainTex = supported.GetTexture("_MainTex");
            var beforeColor = supported.GetColor("_Color");

            AnalyzeVerified(renderer, supported, supported);

            Assert.That(
                AssetDatabase.GetAssetDependencyHash(texturePath),
                Is.EqualTo(beforeAssetHash),
                "Analysis must not re-import or rewrite the texture asset.");
            Assert.That(texture.isReadable, Is.EqualTo(beforeReadable));
            Assert.That(texture.GetPixels32(0), Is.EqualTo(beforePixels));
            Assert.That(mesh.vertexCount, Is.EqualTo(beforeVertexCount));
            Assert.That(mesh.subMeshCount, Is.EqualTo(beforeSubMeshCount));
            Assert.That(mesh.vertices, Is.EqualTo(beforeVertices));
            Assert.That(mesh.uv, Is.EqualTo(beforeUv));
            Assert.That(mesh.GetIndices(0), Is.EqualTo(beforeSubmesh0));
            Assert.That(mesh.GetIndices(1), Is.EqualTo(beforeSubmesh1));
            Assert.That(renderer.sharedMaterials, Is.EqualTo(beforeMaterials));
            Assert.That(
                renderer.sharedMesh, Is.SameAs(beforeSharedMesh),
                "Reading a mesh must not have instantiated a copy.");
            Assert.That(
                renderer.HasPropertyBlock(),
                Is.False,
                "Analysis must not have attached a property block.");
            Assert.That(
                supported.GetTexture("_MainTex"), Is.SameAs(beforeMainTex));
            Assert.That(supported.GetColor("_Color"), Is.EqualTo(beforeColor));
        }

        [Test]
        public void LegacySemanticsCannotConsumeUnrequestedTextureEvidence()
        {
            var texture = ImportTexture("legacy_unrequested", readable: true);
            var material = NewSampledAlphaMaterial(texture);
            var renderer = NewRenderer(BuildFixtureMesh(), material, material);

            var result = UnityRendererAlphaAnalysis.Analyze(
                renderer,
                value => PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                    value, ColorSpace.Linear).Semantics);

            Assert.That(
                result.Submeshes[0].Failure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
            Assert.That(
                result.Submeshes[1].Failure,
                Is.EqualTo(AlphaResolutionFailure.MissingTextureEvidence));
            Assert.That(result.Plan.OpaqueTriangleCount, Is.Zero);
        }
    }
}
