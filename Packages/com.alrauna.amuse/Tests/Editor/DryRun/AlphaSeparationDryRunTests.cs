using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Tests.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;

namespace Alrauna.Amuse.Tests.Editor.DryRun
{
    /// <summary>
    /// End-to-end behavior of the edit-time dry run orchestrator on scene
    /// fixtures. The compute fixtures stand in for the two vendor families
    /// the public suite can admit: the schema-complete cutout stand-in and
    /// the schema-complete opaque stand-in. Selection, closed capture, and
    /// resolution run through the dry run's own seam parameters, because no
    /// stand-in shader can pass vendor source attestation. Every other
    /// stage is the production pipeline: probe, fingerprints, cache,
    /// effective materials, runtime-state resolution, verdicts, rows.
    /// </summary>
    public sealed class AlphaSeparationDryRunTests
    {
        private const string TempFolder = "Assets/AmuseTests_DryRun";
        private const string CutoutShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonCutoutConversionTest";
        private const string OpaqueShaderName =
            "Hidden/Alrauna/AmuseTests/LilToonSemanticTest";
        private const string ReadbackFolder = "Assets/Amuse.StreamingReadback";
        private const int TextureSize = 8;
        private TestTransientScope _scope;
        private AnisotropicFilteringPin _filteringPin;

        private int _selectCalls;
        private int _captureCalls;
        private readonly List<MaterialEvidenceRequest> _seenRequests =
            new List<MaterialEvidenceRequest>();
        private Shader _productionNamedPoiyomiStandIn;

        [SetUp]
        public void SetUp()
        {
            _filteringPin = new AnisotropicFilteringPin();

            _scope = new TestTransientScope(TempFolder);
            _scope.EnsureTempFolder();
            _selectCalls = 0;
            _captureCalls = 0;
            _seenRequests.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            _filteringPin.Dispose();
            DryRunCache.Clear();
            _scope.TearDown();
        }

        // --- Fixture helpers -------------------------------------------------

        private static GameObject NewAvatarRoot(string name)
        {
            var root = new GameObject(name);
            root.hideFlags = HideFlags.HideAndDontSave;
            return root;
        }

        private static AmuseAvatarOptimizer AddOptimizer(GameObject owner)
        {
            var optimizer = owner.AddComponent<AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(optimizer);
            // The fixtures prove over small textures, so the size policy
            // pins to All Sizes. Desktop acceptance pins so the Windows
            // provider selects the accepted policy the fixtures assume.
            serialized.FindProperty("_preserveTransparencyMinTextureSize")
                .intValue = -1;
            serialized.FindProperty("_cutoutPreserveTransparencyMinTextureSize")
                .intValue = -1;
            serialized.FindProperty("_acceptDesktopAnisotropyDifferences")
                .boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return optimizer;
        }

        private static void SetSwitch(
            AmuseAvatarOptimizer optimizer, string field, bool value)
        {
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(
            AmuseAvatarOptimizer optimizer, string field, int value)
        {
            var serialized = new SerializedObject(optimizer);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// An 8x8 mip-free RGBA32 asset with Point/Clamp sampling. The left
        /// half (u &lt; 0.5) carries <paramref name="leftAlpha"/>; the right
        /// half is fully transparent.
        /// </summary>
        private Texture2D ImportHalfTexture(
            string name, byte leftAlpha, bool streaming = false)
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    pixels[y * TextureSize + x] = new Color32(
                        64, 32, 16, x < TextureSize / 2 ? leftAlpha : (byte)0);
                }
            }

            return TestTextureImport.WritePng(
                TempFolder + "/" + name + ".png",
                TextureSize, TextureSize, pixels, importer =>
                {
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                    importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                    importer.anisoLevel = 1;
                    importer.streamingMipmaps = streaming;
                });
        }

        /// <summary>
        /// One submesh, two triangles: triangle A samples the left half,
        /// triangle B samples the fully transparent right half.
        /// </summary>
        private Mesh TwoTriangleMesh()
        {
            var mesh = _scope.Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f),
                new Vector3(2f, 0f, 0f),
                new Vector3(2f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f),
                new Vector2(0.55f, 0.55f),
                new Vector2(0.95f, 0.55f),
                new Vector2(0.55f, 0.95f)
            };
            mesh.subMeshCount = 1;
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5 }, 0);
            return mesh;
        }

        /// <summary>
        /// Many triangle pairs sharing the two-triangle UV footprint, each
        /// pair offset along X so the geometry stays finite and distinct.
        /// Classification cost scales with the triangle count, so the dry
        /// run's background task stays in flight long enough for a test to
        /// cancel while it runs.
        /// </summary>
        private Mesh ManyTriangleMesh(int groupCount)
        {
            var mesh = _scope.Track(new Mesh());
            var vertices = new Vector3[groupCount * 6];
            var uvs = new Vector2[groupCount * 6];
            var indices = new int[groupCount * 6];
            for (var group = 0; group < groupCount; group++)
            {
                var x = group * 0.001f;
                var v = group * 6;
                vertices[v] = new Vector3(x, 0f, 0f);
                vertices[v + 1] = new Vector3(1f + x, 0f, 0f);
                vertices[v + 2] = new Vector3(x, 1f, 0f);
                vertices[v + 3] = new Vector3(1f + x, 1f, 0f);
                vertices[v + 4] = new Vector3(2f + x, 0f, 0f);
                vertices[v + 5] = new Vector3(2f + x, 1f, 0f);
                uvs[v] = new Vector2(0.05f, 0.05f);
                uvs[v + 1] = new Vector2(0.45f, 0.05f);
                uvs[v + 2] = new Vector2(0.05f, 0.45f);
                uvs[v + 3] = new Vector2(0.55f, 0.55f);
                uvs[v + 4] = new Vector2(0.95f, 0.55f);
                uvs[v + 5] = new Vector2(0.55f, 0.95f);
                for (var corner = 0; corner < 6; corner++)
                {
                    indices[v + corner] = v + corner;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = 1;
            mesh.SetTriangles(indices, 0);
            return mesh;
        }

        private static Mesh OneTriangleMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f)
            };
            mesh.subMeshCount = 1;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        private Material NewCutoutMaterial(Texture2D mainTex, float cutoff)
        {
            var shader = Shader.Find(CutoutShaderName);
            Assert.That(shader, Is.Not.Null, CutoutShaderName + " must import.");
            var material = _scope.Track(new Material(shader));
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            material.SetFloat("_Cutoff", cutoff);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            return material;
        }

        private Material NewOpaqueMaterial(Texture2D mainTex)
        {
            var shader = Shader.Find(OpaqueShaderName);
            Assert.That(shader, Is.Not.Null, OpaqueShaderName + " must import.");
            var material = _scope.Track(new Material(shader));
            material.SetTexture("_MainTex", mainTex);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            return material;
        }

        private Material NewTransparentMaterial(Texture2D mainTex)
        {
            var shader = Shader.Find(
                LilToonFixtureTestBase.TransparentConversionShaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                LilToonFixtureTestBase.TransparentConversionShaderName +
                " must import.");
            var material = _scope.Track(new Material(shader));
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            return material;
        }

        /// <summary>
        /// Two submeshes, each one left-half triangle plus one
        /// right-half triangle, so each slot splits one proven triangle
        /// against one unproven triangle.
        /// </summary>
        private Mesh MixedTwoSlotMesh()
        {
            var mesh = _scope.Track(new Mesh());
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(4f, 0f, 0f),
                new Vector3(3f, 1f, 0f),
                new Vector3(6f, 0f, 0f),
                new Vector3(7f, 0f, 0f),
                new Vector3(6f, 1f, 0f),
                new Vector3(9f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(9f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f),
                new Vector2(0.55f, 0.55f),
                new Vector2(0.95f, 0.55f),
                new Vector2(0.55f, 0.95f),
                new Vector2(0.05f, 0.05f),
                new Vector2(0.45f, 0.05f),
                new Vector2(0.05f, 0.45f),
                new Vector2(0.55f, 0.55f),
                new Vector2(0.95f, 0.55f),
                new Vector2(0.55f, 0.95f),
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2, 3, 4, 5 }, 0);
            mesh.SetTriangles(new[] { 6, 7, 8, 9, 10, 11 }, 1);
            return mesh;
        }

        private SkinnedMeshRenderer AddSkinnedRenderer(
            GameObject owner, Mesh mesh, params Material[] slots)
        {
            var child = new GameObject("Body");
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(owner.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = slots;
            return _scope.Track(renderer);
        }

        private sealed class Fixture
        {
            internal GameObject Root;
            internal AmuseAvatarOptimizer Optimizer;
            internal Renderer Renderer;
        }

        private Fixture NewComputeFixture(
            byte leftAlpha,
            float cutoff,
            bool streaming = false,
            Texture2D mainTexOverride = null)
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var texture = mainTexOverride
                ?? ImportHalfTexture(
                    "half_" + leftAlpha + (streaming ? "_s" : ""),
                    leftAlpha,
                    streaming);
            var material = NewCutoutMaterial(texture, cutoff);
            var renderer = AddSkinnedRenderer(
                root, TwoTriangleMesh(), material);
            return new Fixture
            {
                Root = root, Optimizer = optimizer, Renderer = renderer
            };
        }

        // --- Fixture family seams -------------------------------------------

        private AlphaMaterialRequestSelector FixtureSelector() =>
            CountingSelector((
                Material material,
                out CapturedAlphaMaterialFamily family,
                out MaterialEvidenceRequest alpha,
                out MaterialEvidenceRequest capture) =>
            {
                if (material != null &&
                    material.shader == Shader.Find(CutoutShaderName))
                {
                    family = CapturedAlphaMaterialFamily.LilToonCutout;
                    alpha = LilToonCutoutMaterialSemantics.AlphaEvidenceRequest;
                    capture = MaterialEvidenceRequest.Combine(
                        alpha,
                        LilToonCutoutSourceEligibility
                            .ConversionEvidenceRequest);
                    return true;
                }

                if (material != null &&
                    material.shader == Shader.Find(OpaqueShaderName))
                {
                    family = CapturedAlphaMaterialFamily.LilToon;
                    alpha = LilToonMaterialSemantics.AlphaEvidenceRequest;
                    capture = alpha;
                    return true;
                }

                if (material != null &&
                    material.shader == Shader.Find(
                        LilToonFixtureTestBase
                            .TransparentConversionShaderName))
                {
                    family = CapturedAlphaMaterialFamily.LilToonTransparent;
                    alpha =
                        LilToonTransparentMaterialSemantics
                            .AlphaEvidenceRequest;
                    capture = MaterialEvidenceRequest.Combine(
                        alpha,
                        LilToonTransparentSourceEligibility
                            .ConversionEvidenceRequest);
                    return true;
                }

                if (material != null &&
                    material.shader ==
                        _productionNamedPoiyomiStandIn)
                {
                    return VerifiedPoiyomiTestSeams
                        .SelectVerifiedFixtureRequest(
                            material, out family, out alpha, out capture);
                }

                if (material != null &&
                    material.shader == Shader.Find(
                        PoiyomiFixtureTestBase.FixtureShaderName))
                {
                    return VerifiedPoiyomiTestSeams
                        .SelectVerifiedFixtureRequest(
                            material, out family, out alpha, out capture);
                }

                family = CapturedAlphaMaterialFamily.Unsupported;
                alpha = null;
                capture = null;
                return false;
            });

        private ClosedAlphaMaterialCapturer FixtureCapturer() =>
            CountingCapturer((
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                int cutoutRawMipCap,
                int exactRawMipCap,
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                var inputs = new MaterialEvidenceCaptureInput[materials.Count];
                var shaders = new Shader[materials.Count];
                var declaresCutoff = new bool[materials.Count];
                for (var index = 0; index < materials.Count; index++)
                {
                    shaders[index] = materials[index] == null
                        ? null
                        : materials[index].shader;
                    var predicate = UnityMaterialSemantics
                        .AlphaPredicateRequestFor(
                            materials[index], families[index]);
                    inputs[index] = new MaterialEvidenceCaptureInput(
                        materials[index],
                        request,
                        predicate);
                    declaresCutoff[index] =
                        UnityMaterialSemantics.PredicateDeclaresCutoff(
                            predicate);
                }

                var evidence =
                    UnityMaterialEvidenceCapture.Capture(
                        inputs, bounds,
                        cutoutRawMipCap, exactRawMipCap);
                var captured = new CapturedAlphaMaterial[materials.Count];
                for (var index = 0; index < captured.Length; index++)
                {
                    var family = families[index];
                    LilToonSourceEvidence lilToon = null;
                    PoiyomiSourceEvidence poiyomi =
                        default(PoiyomiSourceEvidence);
                    if (family == CapturedAlphaMaterialFamily.LilToonCutout)
                    {
                        lilToon = LilToonSourceAttestation
                            .GatherCutoutSourceEvidence(
                                shaders[index], evidence[index]);
                    }
                    else if (family == CapturedAlphaMaterialFamily.LilToon)
                    {
                        lilToon = LilToonSourceAttestation
                            .GatherSourceEvidence(
                                shaders[index], evidence[index]);
                    }
                    else if (family ==
                        CapturedAlphaMaterialFamily.LilToonTransparent)
                    {
                        lilToon = LilToonSourceAttestation
                            .GatherTransparentSourceEvidence(
                                shaders[index], evidence[index]);
                    }
                    else if (family ==
                        CapturedAlphaMaterialFamily.Poiyomi)
                    {
                        poiyomi = PoiyomiSourceAttestation
                            .GatherAlphaSourceEvidence(
                                shaders[index], evidence[index]);
                    }

                    var source = materials[index];
                    captured[index] = new CapturedAlphaMaterial(
                        family,
                        evidence[index],
                        poiyomi,
                        lilToon,
                        predicatesDeclareCutoff: declaresCutoff[index],
                        materialPath: source != null
                            ? AssetDatabase.GetAssetPath(source)
                            : null,
                        materialName: source != null ? source.name : null,
                        shaderName: source != null && source.shader != null
                            ? source.shader.name
                            : null);
                }

                outcome = new ClosedAlphaCaptureOutcome(
                    captured, Array.Empty<int>());
                return true;
            });

        private AlphaMaterialRequestSelector CountingSelector(
            AlphaMaterialRequestSelector inner)
        {
            return (
                Material material,
                out CapturedAlphaMaterialFamily family,
                out MaterialEvidenceRequest alpha,
                out MaterialEvidenceRequest capture) =>
            {
                _selectCalls++;
                return inner(material, out family, out alpha, out capture);
            };
        }

        private ClosedAlphaMaterialCapturer CountingCapturer(
            ClosedAlphaMaterialCapturer inner)
        {
            return (
                IReadOnlyList<Material> materials,
                IReadOnlyList<CapturedAlphaMaterialFamily> families,
                MaterialEvidenceRequest request,
                AlphaPolicyBounds bounds,
                int cutoutRawMipCap,
                int exactRawMipCap,
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                _captureCalls++;
                _seenRequests.Add(request);
                return inner(
                    materials, families, request, bounds,
                    cutoutRawMipCap, exactRawMipCap, out outcome);
            };
        }

        private AlphaSeparationDryRun NewDryRun(
            Fixture fixture,
            Func<BuildTarget> provider = null,
            AlphaMaterialRequestSelector selectRequest = null,
            ClosedAlphaMaterialCapturer capturer = null)
        {
            return new AlphaSeparationDryRun(
                fixture.Optimizer,
                provider ?? (() => BuildTarget.StandaloneWindows64),
                selectRequest,
                capturer);
        }

        private static void Run(AlphaSeparationDryRun dryRun)
        {
            dryRun.Begin();
            for (var guard = 0; guard < 16 && !dryRun.Done; guard++)
            {
                dryRun.Step();
            }

            Assert.That(dryRun.Done, Is.True, "the run must finish");
        }

        private static IReadOnlyList<MaterialTransformationRow> SlotRows(
            AlphaSeparationDryRun dryRun)
        {
            return dryRun.RendererRows
                .Where(row => row.Kind == MaterialTransformationRowKind.Slot)
                .ToList();
        }

        // --- Behavior sentences ---------------------------------------------

        [Test]
        public void CutoutSplitSlotProducesSplittableSentence()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var slot = SlotRows(dryRun);
            Assert.That(slot, Has.Count.EqualTo(1));
            var row = slot[0];
            Assert.That(row.SlotIndex, Is.EqualTo(0));
            Assert.That(row.ProvenTriangles, Is.EqualTo(1));
            Assert.That(row.TotalTriangles, Is.EqualTo(2));
            Assert.That(
                row.Sentence,
                Is.EqualTo("Splittable: 1 out of 2 polygons may be moved"));
            Assert.That(
                dryRun.RendererRows.Any(
                    item => item.Kind == MaterialTransformationRowKind.Renderer),
                Is.False,
                "a healthy renderer must emit no summary row");
        }

        [Test]
        public void UnknownStaysUntouchable()
        {
            var fixture = NewComputeFixture(0, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            Assert.That(
                dryRun.RendererRows.Any(row => row.Sentence.Contains(
                    "Splittable")),
                Is.False,
                "a fully transparent texture must never split");
            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Untouchable: 0 out of 2 polygons may be moved"));
            Assert.That(
                dryRun.RendererRows.Any(
                    item => item.Kind == MaterialTransformationRowKind.Renderer),
                Is.False,
                "a healthy renderer must emit no summary row");
        }

        [Test]
        public void CutoutSplitSlotAllProvenProducesConvertableSentence()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("all_proven", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            AddSkinnedRenderer(root, OneTriangleMesh(), material);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Convertable: 1 out of 1 polygons may be moved"));
            Assert.That(
                dryRun.RendererRows.Any(
                    item => item.Kind == MaterialTransformationRowKind.Renderer),
                Is.False);
        }

        [Test]
        public void FoldSlotProducesConvertableSentence()
        {
            var fixture = NewComputeFixture(255, 0.5f);

            // The fixture's slot records the cutout arm, so the fold
            // reads the cutout preserve value, not the transparent one.
            SetInt(
                fixture.Optimizer,
                "_cutoutMinimumOpaqueCoverageToPreservePercent",
                60);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Convertable: 2 out of 2 polygons may be moved"));
            Assert.That(
                dryRun.RendererRows.Any(
                    item => item.Kind == MaterialTransformationRowKind.Renderer),
                Is.False);
        }

        [Test]
        public void SeparatorOffCutoutSlotProducesSkippedSentence()
        {
            var fixture = NewComputeFixture(255, 0.5f);

            // The fixture's slot records the cutout arm, so the cutout
            // separator governs it. Turning it off skips the slot by
            // policy. This assertion is also the case-removal guard: the
            // default arm renders the Untouchable sentence instead.
            SetSwitch(fixture.Optimizer, "_cutoutSeparatorEnabled", false);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Skipped: the matching separator switch is off."));
            Assert.That(
                dryRun.RendererRows.Any(
                    item => item.Kind == MaterialTransformationRowKind.Renderer),
                Is.False);
        }

        /// <summary>
        /// The mixed renderer of the pair-selection suite runs through
        /// the dry run and produces the verdicts the build prepares for
        /// the same shape: the cutout preset slot reads Convertable
        /// under the cutout pair's own preserve value, with the
        /// unproven triangle still out of the proven count, and the
        /// plain preset slot reads Splittable under its own inert pair.
        /// The build-side outcomes for this renderer shape are pinned
        /// end to end by the pair-selection suite and the sibling pins.
        /// A dry run that keeps one coverage pair shows a different row
        /// for one slot.
        /// </summary>
        [Test]
        public void DryRunVerdictsMatchTheBuildOnAMixedRenderer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var texture = ImportHalfTexture("mixed_paths_halves", 255);

            // The dry run path. The slot pair is the proven Poiyomi
            // sibling recipe: a cutout preset slot and a plain preset
            // slot, so one slot reads the cutout pair and one reads the
            // transparent pair.
            var dryRunRoot = _scope.Track(NewAvatarRoot("amuse-dry-run-mixed"));
            var dryRunOptimizer = AddOptimizer(dryRunRoot);
            SetInt(
                dryRunOptimizer,
                "_minimumOpaqueCoverageToPreservePercent",
                0);
            SetInt(
                dryRunOptimizer,
                "_cutoutMinimumOpaqueCoverageToPreservePercent",
                60);
            var dryRunCutout =
                AlphaSeparationSplitTests.SplitAlphaMaterial(texture);
            dryRunCutout.SetFloat("_Mode", 1f);
            var dryRunTransparent =
                AlphaSeparationSplitTests.SplitAlphaMaterial(texture);
            AddSkinnedRenderer(
                dryRunRoot,
                MixedTwoSlotMesh(),
                dryRunCutout,
                dryRunTransparent);
            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = dryRunRoot,
                    Optimizer = dryRunOptimizer,
                    Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].SlotIndex, Is.EqualTo(0));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Convertable: 2 out of 2 polygons may be moved"),
                "the cutout slot folds under the cutout pair's own " +
                "preserve value");
            Assert.That(
                rows[0].ProvenTriangles, Is.EqualTo(1),
                "the fold verdict keeps the unproven triangle out of " +
                "the proven count");
            Assert.That(rows[1].SlotIndex, Is.EqualTo(1));
            Assert.That(
                rows[1].Sentence,
                Is.EqualTo("Splittable: 1 out of 2 polygons may be moved"),
                "the transparent slot splits under its own inert pair");
            Assert.That(
                dryRun.RendererRows.Any(
                    item => item.Kind == MaterialTransformationRowKind.Renderer),
                Is.False);
        }

        [Test]
        public void AnisotropyRefusalProducesRefusedSentence()
        {
            var fixture = NewComputeFixture(0, 0.5f);
            SetSwitch(
                fixture.Optimizer,
                "_acceptDesktopAnisotropyDifferences",
                false);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo(
                    "Refused: AMUSE currently does not support this material"));
        }

        [Test]
        public void UnresolvedSlotProducesRefusedSentence()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var material = _scope.Track(new Material(shader));
            AddSkinnedRenderer(root, OneTriangleMesh(), material);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo(
                    "Refused: AMUSE currently does not support this material"));
        }

        [Test]
        public void RendererRefusalShowsNamedSentence()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("tail", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            var tail = new GameObject("Tail");
            tail.hideFlags = HideFlags.HideAndDontSave;
            tail.transform.SetParent(root.transform, false);
            var trail = tail.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            _scope.Track(trail);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = trail
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var row = dryRun.RendererRows.Single(
                item => item.Kind == MaterialTransformationRowKind.Renderer);
            var key = AmuseReportStrings.RendererKey(
                RendererAnalysisRefusal.UnsupportedRendererType);
            Assert.That(AmuseReportStrings.Has(key), Is.True);
            Assert.That(
                row.Sentence,
                Is.EqualTo(
                    "Tail (UnityEngine.TrailRenderer): refused. " +
                    AmuseReportStrings.Format(key)));
            Assert.That(row.Sentence, Is.Not.Empty);
        }

        [Test]
        public void TextureRefusalProducesRefusedSlotRow()
        {
            var atlas = _scope.Track(new Texture2D(
                TextureSize, TextureSize, TextureFormat.RGBA32, false));
            atlas.name = "MainTex (AAO UV Packed)";
            var colors = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    colors[y * TextureSize + x] = new Color32(
                        64, 32, 16, x < TextureSize / 2 ? (byte)255 : (byte)0);
                }
            }

            atlas.SetPixels32(colors);
            atlas.Apply(false);
            atlas.filterMode = FilterMode.Point;
            atlas.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            AaoAtlasTextureAttestation.Versions.ResetForTests();
            AaoAtlasTextureAttestation.Versions.ReadInstalledPackageVersionOrNull =
                _ => "1.9.17";
            try
            {
                var fixture = NewComputeFixture(
                    255, 0.5f, mainTexOverride: atlas);
                var dryRun = NewDryRun(
                    fixture, null, FixtureSelector(), FixtureCapturer());
                Run(dryRun);

                var rows = SlotRows(dryRun);
                Assert.That(rows, Has.Count.EqualTo(1));
                Assert.That(
                    rows[0].Sentence,
                    Is.EqualTo(
                        "Refused: AMUSE currently does not support this material"));
                Assert.That(
                    dryRun.RendererRows.Any(
                        row => row.Kind == MaterialTransformationRowKind.Renderer),
                    Is.True);
            }
            finally
            {
                AaoAtlasTextureAttestation.Versions.ResetForTests();
            }
        }

        [Test]
        public void StructuralRefusalStopsWithStructuralRow()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var clip = _scope.Track(new AnimationClip { name = "event-clip" });
            AnimationUtility.SetAnimationEvents(
                clip,
                new[]
                {
                    new AnimationEvent
                    {
                        time = 0f,
                        functionName = "AmuseDryRunTestEvent"
                    }
                });
            var controller = _scope.Track(new AnimatorController { name = "event" });
            controller.AddLayer("L0");
            controller.layers[0].stateMachine.AddState("S0").motion = clip;
            var animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            Assert.That(dryRun.Done, Is.True);
            Assert.That(dryRun.RendererRows, Is.Empty);
            var key = AmuseReportStrings.AvatarKey(
                AvatarAnimationRefusal.AnimationEventPresent);
            Assert.That(AmuseReportStrings.Has(key), Is.True);
            var structural = dryRun.AvatarRows.Single(
                row => row.Sentence.Contains(AmuseReportStrings.Format(key)));
            Assert.That(
                structural.Kind,
                Is.EqualTo(MaterialTransformationRowKind.Banner));
        }

        [Test]
        public void MasterSwitchOffProducesBannerAndNoCompute()
        {
            // AMUSE off.
            var off = NewComputeFixture(255, 0.5f);
            SetSwitch(off.Optimizer, "_amuseDisabled", true);
            var offRun = NewDryRun(
                off, null, FixtureSelector(), FixtureCapturer());
            Run(offRun);
            Assert.That(
                offRun.AvatarRows.Single().Sentence,
                Is.EqualTo("AMUSE is off. Turn it on to see results."));
            Assert.That(offRun.RendererRows, Is.Empty);
            Assert.That(_selectCalls, Is.EqualTo(0));
            Assert.That(_captureCalls, Is.EqualTo(0));

            // Separator off, same shape.
            _selectCalls = 0;
            _captureCalls = 0;
            var separator = NewComputeFixture(255, 0.5f);
            SetSwitch(separator.Optimizer, "_alphaSeparatorEnabled", false);
            var separatorRun = NewDryRun(
                separator, null, FixtureSelector(), FixtureCapturer());
            Run(separatorRun);
            Assert.That(
                separatorRun.AvatarRows.Single().Sentence,
                Is.EqualTo(
                    "The Alpha Separator is off. Nothing will move. " +
                    "Turn it on to see results."));
            Assert.That(separatorRun.RendererRows, Is.Empty);
            Assert.That(_selectCalls, Is.EqualTo(0));
            Assert.That(_captureCalls, Is.EqualTo(0));
        }

        [Test]
        public void ChildPlacementProducesBanner()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var texture = ImportHalfTexture("child", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            AddSkinnedRenderer(root, TwoTriangleMesh(), material);
            var child = new GameObject("misplaced");
            child.hideFlags = HideFlags.HideAndDontSave;
            child.transform.SetParent(root.transform, false);
            var optimizer = AddOptimizer(child);
            _scope.Track(child);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            Assert.That(
                dryRun.AvatarRows.Single().Sentence,
                Is.EqualTo(
                    "This component must sit on the avatar root object."));
            Assert.That(dryRun.RendererRows, Is.Empty);
            Assert.That(_captureCalls, Is.EqualTo(0));
        }

        [Test]
        public void NonWindowsTargetProducesBannerAndContinues()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture,
                () => BuildTarget.Android,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            Assert.That(
                dryRun.AvatarRows.Any(row => row.Sentence ==
                    "The active build target is not Windows. Texture proof " +
                    "needs the Windows import. This view can prove fewer " +
                    "triangles than a Windows build."),
                Is.True,
                "the non-Windows banner must be among the avatar rows");
            Assert.That(
                dryRun.RendererRows,
                Is.Not.Empty,
                "the run must still compute rows on a mobile target");
        }

        [Test]
        public void SamplingBannerSuppressesResults()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture,
                () => BuildTarget.StandaloneLinux64,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            Assert.That(
                dryRun.AvatarRows.Single().Sentence,
                Is.EqualTo(
                    "The active build target supports no sampling policy. " +
                    "A build on this target applies nothing. This view " +
                    "shows no results for it."));
            Assert.That(dryRun.RendererRows, Is.Empty);
            Assert.That(_captureCalls, Is.EqualTo(0));
        }

        [Test]
        public void PropertyBlockChangesVerdictLikeTheBuild()
        {
            // The serialized texture proves one triangle. The renderer-wide
            // block swaps in a fully transparent texture, so the proof must
            // read the materialized override and prove nothing.
            var fixture = NewComputeFixture(255, 0.5f);
            var overrideTexture = ImportHalfTexture("block_transparent", 0);
            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", overrideTexture);
            fixture.Renderer.SetPropertyBlock(block);

            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Untouchable: 0 out of 2 polygons may be moved"));
            var material = (Material)fixture.Renderer.sharedMaterials[0];
            Assert.That(
                material.GetTexture("_MainTex"),
                Is.Not.SameAs(overrideTexture),
                "materialization must not mutate the original material");
        }

        [Test]
        public void AvatarCacheHitServesRenderersWithoutRecompute()
        {
            var texture = ImportHalfTexture("cache", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            var mesh = TwoTriangleMesh();
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var renderer = AddSkinnedRenderer(root, mesh, material);

            var fixture = new Fixture
            {
                Root = root, Optimizer = optimizer, Renderer = renderer
            };
            var first = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(first);
            Assert.That(_captureCalls, Is.EqualTo(1));
            var avatarSentences = first.AvatarRows.Select(row => row.Sentence)
                .ToList();
            var rendererSentences = first.RendererRows
                .Select(row => row.Sentence).ToList();

            var second = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(second);
            Assert.That(
                _captureCalls,
                Is.EqualTo(1),
                "the full compute must not run again for an unchanged fixture");
            Assert.That(
                second.AvatarRows.Select(row => row.Sentence).ToList(),
                Is.EqualTo(avatarSentences));
            Assert.That(
                second.RendererRows.Select(row => row.Sentence).ToList(),
                Is.EqualTo(rendererSentences));
        }

        [Test]
        public void StreamingTextureDecodesInMemoryWhenReadable()
        {
            var fixture = NewComputeFixture(255, 0.5f, streaming: true);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            Assert.That(
                dryRun.AvatarRows.Any(row => row.Sentence.Contains("streaming mipmaps")),
                Is.False,
                "the streaming note must not be emitted for in-memory decodable textures");
            Assert.That(
                _captureCalls,
                Is.GreaterThan(0),
                "the streaming texture must reach a capture dispatch");
            Assert.That(dryRun.RendererRows, Is.Not.Empty);
        }

        [Test]
        public void StreamingPngTextureDecodesInMemoryAndDoesNotRefuse()
        {
            var fixture = NewComputeFixture(255, 0.5f, streaming: true);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Sentence, Does.Not.Contain("Refused"));
            Assert.That(
                rows[0].Sentence,
                Does.Contain("Splittable").Or.Contain("Convertable"));
            Assert.That(
                dryRun.AvatarRows.Any(row => row.Sentence.Contains("streaming mipmaps")),
                Is.False,
                "note must not be emitted when streaming texture decodes in memory");
        }

        /// <summary>
        /// The dry run's no-asset-writes guarantee rides the capture
        /// request: every request the capturer receives disallows the
        /// streaming route's disk-fallback clone, instead of relying on
        /// process state around the call.
        /// </summary>
        [Test]
        public void DryRunCaptureRequestDisallowsDiskFallback()
        {
            var fixture = NewComputeFixture(255, 0.5f, streaming: true);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            Assert.That(
                _captureCalls,
                Is.GreaterThan(0),
                "fixture precondition: the run must dispatch a capture");
            Assert.That(_seenRequests, Is.Not.Empty);
            foreach (var request in _seenRequests)
            {
                Assert.That(
                    request.AllowDiskFallback, Is.False,
                    "the dry run capture request must disallow the " +
                    "disk-fallback route");
            }
        }

        [Test]
        public void NameTagOnlyLockedMaterialReportsRefusedSentence()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-locked-attested"));
            var optimizer = AddOptimizer(root);
            var locked = NewNameTagOnlyLockedMaterial();
            var mesh = OneTriangleMesh();
            AddSkinnedRenderer(root, mesh, locked);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root,
                    Optimizer = optimizer,
                    Renderer = null
                },
                null,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].ProvenTriangles, Is.EqualTo(-1),
                "a lock whose recorded identity is refused must keep the " +
                "refused display");
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Refused: AMUSE currently does not support this material"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.True,
                "the renderer refusal row must accompany the refused slot");
        }

        [Test]
        public void TrulyUnsupportedShaderReportsRefusedSentence()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-unsupported"));
            var optimizer = AddOptimizer(root);
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var material = _scope.Track(new Material(shader));
            AddSkinnedRenderer(root, OneTriangleMesh(), material);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root,
                    Optimizer = optimizer,
                    Renderer = null
                },
                null,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Refused: AMUSE currently does not support this material"));
        }

        private static Shader EnsurePoiyomiStandInShader()
        {
            var shader = Shader.Find(".poiyomi/Poiyomi Toon");
            if (shader == null)
            {
                shader = TestShaderWriter.WriteStandInShader(
                    TempFolder + "/poiyomi_toon.shader",
                    ".poiyomi/Poiyomi Toon",
                    "");
            }

            return shader;
        }

        private static Shader EnsureLockedPoiyomiStandInShader()
        {
            var shader = Shader.Find("Hidden/Locked/.poiyomi/Poiyomi Toon");
            if (shader == null)
            {
                shader = TestShaderWriter.WriteStandInShader(
                    TempFolder + "/locked_poiyomi_toon.shader",
                    "Hidden/Locked/.poiyomi/Poiyomi Toon",
                    "_ShaderOptimizerEnabled (\"Shader Optimizer Enabled\", Float) = 0");
            }

            return shader;
        }

        // --- Recorded-cause display precedence (PTM3) -----------------------

        /// <summary>
        /// The name-tag-only locked fixture material. The locked stand-in
        /// shader carries the optimizer flag, and the original shader tag
        /// names the vendor shader without a resolvable asset reference.
        /// The capture therefore records a refused locked identity.
        /// </summary>
        private Material NewNameTagOnlyLockedMaterial()
        {
            EnsurePoiyomiStandInShader();
            EnsureLockedPoiyomiStandInShader();
            var original = _scope.Track(
                new Material(Shader.Find(".poiyomi/Poiyomi Toon")));
            var locked = _scope.Track(new Material(original));
            locked.shader = Shader.Find("Hidden/Locked/.poiyomi/Poiyomi Toon");
            locked.SetFloat("_ShaderOptimizerEnabled", 1f);
            locked.SetOverrideTag("OriginalShader", ".poiyomi/Poiyomi Toon");
            return locked;
        }

        /// <summary>
        /// A streaming texture the closed capture cannot decode in memory.
        /// The import generates the alpha channel from grayscale, so the
        /// source reader refuses that channel.
        /// </summary>
        private Texture2D ImportUnreadableStreamingMaskTexture()
        {
            var pixels = new Color32[TextureSize * TextureSize];
            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    pixels[y * TextureSize + x] =
                        new Color32(255, 255, 255, 255);
                }
            }

            return TestTextureImport.WritePng(
                TempFolder + "/unreadable_streaming_mask.png",
                TextureSize, TextureSize, pixels, importer =>
                {
                    importer.mipmapEnabled = false;
                    importer.isReadable = false;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.streamingMipmaps = true;
                    importer.alphaSource =
                        TextureImporterAlphaSource.FromGrayScale;
                });
        }

        /// <summary>
        /// The schema-complete Poiyomi fixture shader copied under the
        /// vendor name the production selection map reads. The full
        /// property schema survives the copy, so a material on it resolves
        /// through the verified Poiyomi seam while the display-side
        /// production selection recognizes the shader name.
        /// </summary>
        private Shader EnsureProductionNamedPoiyomiStandInShader()
        {
            if (_productionNamedPoiyomiStandIn != null)
            {
                return _productionNamedPoiyomiStandIn;
            }

            var sourcePath = AssetDatabase.GetAssetPath(
                Shader.Find(PoiyomiFixtureTestBase.FixtureShaderName));
            Assert.That(sourcePath, Is.Not.Empty,
                "the schema-complete Poiyomi fixture shader must import");
            var renamed = File.ReadAllText(sourcePath).Replace(
                "Shader \"" + PoiyomiFixtureTestBase.FixtureShaderName + "\"",
                "Shader \".poiyomi/Poiyomi Toon\"");
            Assert.That(
                renamed,
                Does.Contain("Shader \".poiyomi/Poiyomi Toon\""),
                "the schema copy must carry the production shader name");
            _productionNamedPoiyomiStandIn = TestShaderWriter.WriteTestShader(
                TempFolder + "/poiyomi_production_named.shader", renamed);
            return _productionNamedPoiyomiStandIn;
        }

        /// <summary>
        /// A transparent Poiyomi fixture material on the production-named
        /// stand-in. Its unused alpha mask holds the unreadable streaming
        /// texture, so the material carries the display-side streaming
        /// shape while its own alpha still proves from the readable main
        /// texture.
        /// </summary>
        private Material NewProductionNamedStreamingCurrent(
            Texture2D mainTex)
        {
            var material = _scope.Track(new Material(
                EnsureProductionNamedPoiyomiStandInShader()));
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            material.SetFloat("_Mode", 2f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetTexture(
                "_AlphaMask", ImportUnreadableStreamingMaskTexture());
            return material;
        }

        /// <summary>
        /// A transparent Poiyomi fixture material on the production-named
        /// stand-in whose main texture is the unreadable streaming texture.
        /// The capture refuses that alpha field and names no frontend
        /// reason, which is the retained unnamed-cause shape of the
        /// streaming arm.
        /// </summary>
        private Material NewUnnamedStreamingCurrent()
        {
            var material = _scope.Track(new Material(
                EnsureProductionNamedPoiyomiStandInShader()));
            material.SetTexture(
                "_MainTex", ImportUnreadableStreamingMaskTexture());
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            material.SetFloat("_Mode", 2f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        /// <summary>
        /// One material-slot object curve on the Body renderer slot.
        /// </summary>
        private static void AddSlotSwap(
            AnimationClip clip, int slotIndex,
            params UnityEngine.Object[] values)
        {
            var keyframes = new ObjectReferenceKeyframe[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = index,
                    value = values[index],
                };
            }

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    "Body",
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[" + slotIndex + "]"),
                keyframes);
        }

        /// <summary>
        /// One controller layer whose default state plays the clip. The
        /// additive mode marks every clip it plays as additive evidence.
        /// </summary>
        private static AnimatorController NewSingleLayerController(
            string name, AnimationClip clip, bool additive)
        {
            var controller = new AnimatorController { name = name };
            controller.AddLayer("L0");
            var state = controller.layers[0].stateMachine.AddState("S0");
            state.motion = clip;
            controller.layers[0].stateMachine.defaultState = state;
            if (additive)
            {
                var layers = controller.layers;
                layers[0].blendingMode = AnimatorLayerBlendingMode.Additive;
                controller.layers = layers;
            }

            return controller;
        }

        [Test]
        public void InvalidSwapWithLockedCurrentReportsRefusedSentence()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-swap-locked"));
            var optimizer = AddOptimizer(root);
            var locked = NewNameTagOnlyLockedMaterial();
            var texture = ImportHalfTexture("swap_locked_main", 255);
            var swapCandidate = NewCutoutMaterial(texture, 0.5f);
            AddSkinnedRenderer(root, OneTriangleMesh(), locked);

            var clip = _scope.Track(
                new AnimationClip { name = "broken-swap-locked" });
            AddSlotSwap(clip, 0, swapCandidate, null);
            var controller = _scope.Track(NewSingleLayerController(
                "broken-swap-locked-controller", clip, additive: false));
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].SlotIndex, Is.EqualTo(0));
            Assert.That(
                rows[0].ProvenTriangles, Is.EqualTo(-1),
                "a recorded invalid-swap closure failure must not be " +
                "replaced by a promise about the locked current material");
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo(
                    "Refused: AMUSE currently does not support this material"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.True,
                "the renderer refusal row must accompany the refused slot");
        }

        [Test]
        public void InvalidSwapWithStreamingCurrentReportsRefusedSentence()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-swap-streaming"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("swap_streaming_main", 255);
            var current = NewProductionNamedStreamingCurrent(texture);
            AddSkinnedRenderer(root, OneTriangleMesh(), current);

            var clip = _scope.Track(
                new AnimationClip { name = "broken-swap-streaming" });
            AddSlotSwap(clip, 0, current, null);
            var controller = _scope.Track(NewSingleLayerController(
                "broken-swap-streaming-controller", clip, additive: false));
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].SlotIndex, Is.EqualTo(0));
            Assert.That(
                rows[0].ProvenTriangles, Is.EqualTo(-1),
                "a recorded invalid-swap closure failure must not be " +
                "replaced by a promise about the streaming current material");
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo(
                    "Refused: AMUSE currently does not support this material"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.True,
                "the renderer refusal row must accompany the refused slot");
            Assert.That(
                dryRun.AvatarRows.Any(
                    row => row.Sentence.Contains("streaming mipmaps")),
                Is.True,
                "the streaming disclosure must stay beside the refusal");
        }

        [Test]
        public void UnsupportedSwapWithStreamingCurrentReportsRefusedSentence()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-unsupported-swap"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("unsupported_swap_main", 255);
            var current = NewProductionNamedStreamingCurrent(texture);
            var swap = _scope.Track(new Material(
                Shader.Find("Hidden/InternalErrorShader")));
            AddSkinnedRenderer(root, OneTriangleMesh(), current);

            var clip = _scope.Track(
                new AnimationClip { name = "unsupported-swap" });
            AddSlotSwap(clip, 0, swap);
            var controller = _scope.Track(NewSingleLayerController(
                "unsupported-swap-controller", clip, additive: false));
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].SlotIndex, Is.EqualTo(0));
            Assert.That(
                rows[0].ProvenTriangles, Is.EqualTo(-1),
                "the permanently unsupported swap material is the recorded " +
                "blocker, so the streaming current material must not " +
                "replace the refusal with a promise about itself");
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo(
                    "Refused: AMUSE currently does not support this material"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.True,
                "the renderer refusal row must accompany the refused slot");
            Assert.That(
                dryRun.AvatarRows.Any(
                    row => row.Sentence.Contains("streaming mipmaps")),
                Is.True,
                "the streaming disclosure must stay beside the refusal");
        }

        [Test]
        public void MixedSiblingsKeepTheirOwnRowsBesideARefusedSlot()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-mixed-siblings"));
            var optimizer = AddOptimizer(root);
            var streaming = NewUnnamedStreamingCurrent();
            var texture = ImportHalfTexture("mixed_siblings_main", 255);
            var cutout = NewCutoutMaterial(texture, 0.5f);
            AddSkinnedRenderer(root, MixedTwoSlotMesh(), streaming, cutout);

            var clip = _scope.Track(
                new AnimationClip { name = "slot-one-broken-swap" });
            AddSlotSwap(clip, 1, cutout, null);
            var controller = _scope.Track(NewSingleLayerController(
                "slot-one-broken-swap-controller", clip, additive: false));
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That(rows[0].SlotIndex, Is.EqualTo(0));
            Assert.That(
                rows[0].Sentence, Does.StartWith("Untouchable:"),
                "the streaming sibling resolves and keeps its own verdict " +
                "row beside the refused slot");
            Assert.That(rows[0].ProvenTriangles, Is.EqualTo(0));
            var refusedRow = rows[1];
            Assert.That(refusedRow.SlotIndex, Is.EqualTo(1));
            Assert.That(refusedRow.ProvenTriangles, Is.EqualTo(-1));
            Assert.That(
                refusedRow.Sentence,
                Is.EqualTo(
                    "Refused: AMUSE currently does not support this material"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.False,
                "a renderer with a resolved slot has no renderer " +
                "refusal row");
        }

        [Test]
        public void EarlyRendererRefusalWithNoSlotResultsKeepsRendererRowOnly()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-early-refusal"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("early_refusal_main", 255);
            var cutout = NewCutoutMaterial(texture, 0.5f);
            AddSkinnedRenderer(root, OneTriangleMesh(), cutout);

            var clip = _scope.Track(
                new AnimationClip { name = "additive-swap" });
            AddSlotSwap(clip, 0, cutout);
            var controller = _scope.Track(NewSingleLayerController(
                "additive-swap-controller", clip, additive: true));
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            Assert.That(
                SlotRows(dryRun), Is.Empty,
                "an early renderer refusal carries no slot results");
            Assert.That(dryRun.RendererRows, Has.Count.EqualTo(1));
            var refusedRow = dryRun.RendererRows.Single();
            Assert.That(
                refusedRow.Kind,
                Is.EqualTo(MaterialTransformationRowKind.Renderer));
            Assert.That(refusedRow.ProvenTriangles, Is.EqualTo(-1));
        }

        /// <summary>
        /// The next two tests characterize the display gate directly. The
        /// slot results come from the existing factory, so they pin the
        /// gate's cause handling. They are not a production capture path
        /// and claim no production streaming or lock behavior.
        /// </summary>
        [Test]
        public void GateDefersRecordedSemanticsUnknownCauseForLockedCurrent()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-gate-locked"));
            var locked = NewNameTagOnlyLockedMaterial();
            var renderer = AddSkinnedRenderer(
                root, OneTriangleMesh(), locked);

            var slotResult = SlotResolutionResult.Refused(
                RendererAnalysisRefusal.AdmittedMaterialSemanticsUnknown);
            var slotEvidence = new CapturedMaterialSlotEvidence(
                0, new[] { 0 });

            Assert.That(
                AlphaSeparationDryRun.IsSlotDeferred(
                    renderer, 0, slotResult, slotEvidence),
                Is.True,
                "the locked arm defers a recorded semantics-unknown cause " +
                "behind the live attested locked identity");
        }

        [Test]
        public void GateRefusesRecordedLockCauseForLockedCurrent()
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-gate-lock-refusal"));
            var locked = NewNameTagOnlyLockedMaterial();
            var renderer = AddSkinnedRenderer(
                root, OneTriangleMesh(), locked);

            var slotResult = SlotResolutionResult.Refused(
                RendererAnalysisRefusal.LockedPoiyomiOriginalShaderUnattested);
            var slotEvidence = new CapturedMaterialSlotEvidence(
                0, new[] { 0 });

            Assert.That(
                AlphaSeparationDryRun.IsSlotDeferred(
                    renderer, 0, slotResult, slotEvidence),
                Is.False,
                "a recorded lock refusal is a permanent cause the build " +
                "does not recover, so the gate refuses it");
        }

        [Test]
        public void OpaqueFamilySlotShowsUntouchable()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-root"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("opaque", 255);
            var material = NewOpaqueMaterial(texture);
            AddSkinnedRenderer(root, OneTriangleMesh(), material);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = null
                },
                null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Untouchable: 0 out of 1 polygons may be moved"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.False);
        }

        /// <summary>
        /// Builds the approved-range avatar and runs the dry run over it
        /// with the production family map and the plain capturer. The
        /// approved-arm routing tests share this body, so one fixture
        /// shape feeds every assertion.
        /// </summary>
        private AlphaSeparationDryRun RunApprovedAvatarDryRun(
            VerifiedLilToonTestSeams.ApprovedRangeVendorScope vendor)
        {
            var root = _scope.Track(
                NewAvatarRoot("amuse-dry-run-approved"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("approved_range_dry", 255);
            var material = _scope.Track(
                new Material(vendor.CutoutShader));
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            AddSkinnedRenderer(root, TwoTriangleMesh(), material);

            // Null selector and null capturer: the production family
            // map and the dry run's plain capturer, which passes no
            // approval arm.
            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root, Optimizer = optimizer,
                    Renderer = null
                },
                null, null, null);
            Run(dryRun);
            return dryRun;
        }

        private IReadOnlyList<MaterialTransformationRow> RunApprovedAvatar(
            VerifiedLilToonTestSeams.ApprovedRangeVendorScope vendor)
        {
            return RunApprovedAvatarDryRun(vendor).AvatarRows;
        }

        /// <summary>
        /// The version the package database reports for the embedded
        /// vendor package, or null before registration. The client
        /// resolves over editor updates, and a re-install answers with
        /// the previous manifest's version first, so a registration wait
        /// reads this until it names the declared version.
        /// </summary>
        private static string VendorVersion()
        {
            var info = UnityEditor.PackageManager.PackageInfo
                .FindForAssetPath(
                    "Packages/" +
                    VerifiedLilToonTestSeams.ApprovedRangeVendorPackageName);
            return info == null ? null : info.version;
        }

        /// <summary>
        /// The dry run keeps strict attestation: the same byte-changed
        /// in-range install the build approves must stay unknown here,
        /// because the dry run's plain capturer carries no approval arm.
        /// The fixture shader presents an approved, non-row package version
        /// from the embedded vendor package, and its digests mismatch every
        /// pin, so this pins the conservative edit-time view against the
        /// version-keyed routing, not just against loose installs. The
        /// avatar-level note still names the true cause (spec B4).
        /// </summary>
        [UnityTest]
        public IEnumerator ApprovedVersionStaysUnknownInDryRun()
        {
            var vendor = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope();
            var installed = false;
            try
            {
                vendor.InstallCutoutShader();
                installed = true;
                vendor.RequestRegistration();
                var registrationGuard = 0;
                while ((!vendor.IsRegistered() ||
                    VendorVersion() !=
                        VerifiedLilToonTestSeams
                            .ApprovedRangeVendorVersion) &&
                    registrationGuard++ < 600)
                {
                    yield return null;
                }

                Assert.That(
                    vendor.IsRegistered(), Is.True,
                    "the embedded vendor package must register");

                var dryRun = RunApprovedAvatarDryRun(vendor);

                Assert.That(
                    dryRun.RendererRows.Any(row =>
                        row.Sentence.StartsWith("Splittable") ||
                        row.Sentence.StartsWith("Convertable")),
                    Is.False,
                    "the dry run's plain capturer must not admit the " +
                    "approved member, so no polygon may be proven " +
                    "splittable");
                foreach (var row in dryRun.RendererRows)
                {
                    Assert.That(
                        row.ProvenTriangles, Is.Not.GreaterThan(0),
                        "an unadmitted member proves no triangles");
                }

                Assert.That(
                    dryRun.RendererRows, Is.Not.Empty,
                    "the dry run must still report the unknown slot");

                Assert.That(
                    dryRun.AvatarRows.Count(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.ApprovedUnverifiedSource)),
                    Is.EqualTo(1),
                    "the approved unverified source names its cause once");
                Assert.That(
                    dryRun.AvatarRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.LockedMaterials)),
                    Is.False,
                    "the false locked-materials note must not fire for an " +
                    "approved unverified source");
            }
            finally
            {
                if (installed)
                {
                    vendor.TearDown();
                }
            }
        }

        /// <summary>
        /// The approved arm and the out-of-range arm route their notes to
        /// opposite rows. The two scope versions share one embedded package
        /// folder, and the manifest write early-returns when a manifest is
        /// present, so side-by-side scopes cannot work. The run installs,
        /// builds, asserts, and tears down, then installs the next version.
        /// A re-install answers with the previous manifest's version until
        /// the package client settles, so each wait reads the declared
        /// version before the avatar runs.
        /// <para>
        /// Attested avatars carry neither note by construction:
        /// RecordConsentFacts emits only from the locked-refusal branch and
        /// the Unsupported-sentinel branch, and an attested record matches
        /// neither (spec B4).
        /// </para>
        /// </summary>
        [UnityTest]
        public IEnumerator ApprovedAndOutOfRangeAvatarsRouteTheirNotes()
        {
            var vendor = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope();
            var outOfRange = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope("2.5.0");
            var vendorInstalled = false;
            var outOfRangeInstalled = false;
            try
            {
                vendor.InstallCutoutShader();
                vendorInstalled = true;
                vendor.RequestRegistration();
                var guard = 0;
                while ((!vendor.IsRegistered() ||
                    VendorVersion() !=
                        VerifiedLilToonTestSeams
                            .ApprovedRangeVendorVersion) &&
                    guard++ < 600)
                {
                    yield return null;
                }
                Assert.That(vendor.IsRegistered(), Is.True);

                var approvedRows = RunApprovedAvatar(vendor);
                Assert.That(
                    approvedRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.ApprovedUnverifiedSource)),
                    Is.True, "the approved avatar earns the named cause");
                Assert.That(
                    approvedRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.LockedMaterials)),
                    Is.False, "the approved avatar keeps no consent note");
                vendor.TearDown();
                vendorInstalled = false;

                outOfRange.InstallCutoutShader();
                outOfRangeInstalled = true;
                outOfRange.RequestRegistration();
                guard = 0;
                while ((!outOfRange.IsRegistered() ||
                    VendorVersion() != "2.5.0") &&
                    guard++ < 600)
                {
                    yield return null;
                }
                Assert.That(outOfRange.IsRegistered(), Is.True);

                var outOfRangeRows = RunApprovedAvatar(outOfRange);
                Assert.That(
                    outOfRangeRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.LockedMaterials)),
                    Is.True, "the out-of-range avatar keeps the consent note");
                Assert.That(
                    outOfRangeRows.Any(row =>
                        row.Sentence == RowText.NoteSentence(
                            NoteKind.ApprovedUnverifiedSource)),
                    Is.False, "the out-of-range avatar earns no named cause");
                outOfRange.TearDown();
                outOfRangeInstalled = false;
            }
            finally
            {
                if (outOfRangeInstalled)
                {
                    outOfRange.TearDown();
                }

                if (vendorInstalled)
                {
                    vendor.TearDown();
                }
            }
        }

        /// <summary>
        /// The avatar cache serves the stored avatar rows on a hit. The
        /// named cause rides those rows, so a second open shows it exactly
        /// once and never appends a duplicate note.
        /// </summary>
        [UnityTest]
        public IEnumerator CacheHitServesTheApprovedNoteOnce()
        {
            var vendor = new VerifiedLilToonTestSeams
                .ApprovedRangeVendorScope();
            var installed = false;
            try
            {
                vendor.InstallCutoutShader();
                installed = true;
                vendor.RequestRegistration();
                var guard = 0;
                while ((!vendor.IsRegistered() ||
                    VendorVersion() !=
                        VerifiedLilToonTestSeams
                            .ApprovedRangeVendorVersion) &&
                    guard++ < 600)
                {
                    yield return null;
                }
                Assert.That(vendor.IsRegistered(), Is.True);

                var root = _scope.Track(
                    NewAvatarRoot("amuse-dry-run-approved-cache"));
                var optimizer = AddOptimizer(root);
                var texture = ImportHalfTexture("approved_range_cache", 255);
                var material = _scope.Track(
                    new Material(vendor.CutoutShader));
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", Vector2.one);
                material.SetTextureOffset("_MainTex", Vector2.zero);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
                var renderer = AddSkinnedRenderer(
                    root, TwoTriangleMesh(), material);

                var fixture = new Fixture
                {
                    Root = root, Optimizer = optimizer, Renderer = renderer
                };
                var first = NewDryRun(fixture, null, null, null);
                Run(first);
                var second = NewDryRun(fixture, null, null, null);
                Run(second);

                var note = RowText.NoteSentence(
                    NoteKind.ApprovedUnverifiedSource);
                Assert.That(
                    first.AvatarRows.Count(row => row.Sentence == note),
                    Is.EqualTo(1),
                    "the first open names the cause once");
                Assert.That(
                    second.AvatarRows.Count(row => row.Sentence == note),
                    Is.EqualTo(1),
                    "the served rows name the cause once, not twice");
                Assert.That(
                    second.AvatarRows.Select(row => row.Sentence).ToList(),
                    Is.EqualTo(
                        first.AvatarRows.Select(row => row.Sentence)
                            .ToList()),
                    "the second open serves the cached avatar rows");
            }
            finally
            {
                if (installed)
                {
                    vendor.TearDown();
                }
            }
        }

        [Test]
        public void UnsupportedShaderWithStreamingTextureReportsRefusedAndEmitsRendererRefusalBox()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-unsupported-streaming"));
            var optimizer = AddOptimizer(root);
            var shader = Shader.Find("Hidden/InternalErrorShader");
            var material = _scope.Track(new Material(shader));
            var texture = ImportHalfTexture("unsupported_streaming", 255);
            material.mainTexture = texture;
            AddSkinnedRenderer(root, OneTriangleMesh(), material);

            var dryRun = NewDryRun(
                new Fixture
                {
                    Root = root,
                    Optimizer = optimizer,
                    Renderer = null
                },
                null,
                FixtureSelector(),
                FixtureCapturer());
            Run(dryRun);

            var rows = SlotRows(dryRun);
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(
                rows[0].Sentence,
                Is.EqualTo("Refused: AMUSE currently does not support this material"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.True,
                "renderer refusal box must NOT be suppressed when slot is truly refused");
        }

        // --- Policy validity --------------------------------------------------

        /// <summary>
        /// A policy change observed while the renderer classifies in the
        /// background must invalidate the run. The rows computed under
        /// the changed policy must never publish. Restoring the old
        /// value must not resurrect them in the rejected run. A
        /// separate light fixture runs under the restored policy and
        /// produces the known row, so the empty view proves the
        /// rejection and not a dead compute path.
        /// </summary>
        [Test]
        public void PolicyChangeDuringBackgroundWorkKeepsRowsRejectedAfterRestore()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-policy-flight"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("half_policy_flight", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            AddSkinnedRenderer(root, ManyTriangleMesh(16384), material);
            var fixture = new Fixture
            {
                Root = root, Optimizer = optimizer, Renderer = null
            };
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            dryRun.Step(synchronous: false);
            Assert.That(
                dryRun.State,
                Is.EqualTo(DryRunState.ClassifyingBackground),
                "the run must classify in the background before the change");
            var inFlight = InFlightTask(dryRun);
            Assert.That(
                inFlight, Is.Not.Null, "the run must own an in-flight task");
            WaitUntilRunning(inFlight);

            SetSwitch(fixture.Optimizer, "_cutoutSeparatorEnabled", false);

            for (var guard = 0; guard < 1000 && !dryRun.Done; guard++)
            {
                dryRun.Step(synchronous: false);
                Thread.Sleep(10);
            }

            Assert.That(
                dryRun.Done, Is.True,
                "the invalidated run must end its pending work");
            Assert.That(
                dryRun.PolicyInvalidated, Is.True,
                "the in-flight change must latch the rejection");
            Assert.That(
                SlotRows(dryRun), Is.Empty,
                "rows finished after a policy change must not publish");

            SetSwitch(fixture.Optimizer, "_cutoutSeparatorEnabled", true);
            dryRun.Step(synchronous: false);
            Assert.That(
                dryRun.ValidatePolicy(PlayModeBuildStore
                    .ComputePolicyFingerprint(
                        fixture.Optimizer,
                        BuildTarget.StandaloneWindows64)),
                Is.False,
                "restoring the old policy must keep the latch set");
            Assert.That(
                SlotRows(dryRun), Is.Empty,
                "restoring the old policy must not resurrect the " +
                "rejected rows");

            // No-op guard: the same policy on a light fixture must
            // compute the known row, so the rejection above names the
            // invalidated run and not a broken fixture.
            var freshFixture = NewComputeFixture(255, 0.5f);
            var fresh = NewDryRun(
                freshFixture, null, FixtureSelector(), FixtureCapturer());
            Run(fresh);
            var freshRows = SlotRows(fresh);
            Assert.That(freshRows, Has.Count.EqualTo(1));
            Assert.That(
                freshRows[0].ProvenTriangles, Is.EqualTo(1),
                "the restored policy must compute the known row again");
            Assert.That(
                freshRows[0].TotalTriangles, Is.EqualTo(2),
                "the restored policy must compute the known row again");
        }

        /// <summary>
        /// A target change observed between renderer steps must reject
        /// the rows the next renderer would publish under the new
        /// target beside the rows the old target already published.
        /// The first renderer's known row and triangle counts stand as
        /// the no-op guard.
        /// </summary>
        [Test]
        public void TargetChangeBetweenRendererStepsRejectsMixedTargetRows()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var texture = ImportHalfTexture("half_255_target_shift", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            var second = AddSkinnedRenderer(
                fixture.Root, TwoTriangleMesh(), material);
            second.gameObject.name = "Cape";
            var target = BuildTarget.StandaloneWindows64;
            var dryRun = NewDryRun(
                fixture, () => target, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            Assert.That(dryRun.Step(), Is.True, "the first renderer must step");
            var guarded = SlotRows(dryRun);
            Assert.That(guarded, Has.Count.EqualTo(1));
            Assert.That(guarded[0].ProvenTriangles, Is.EqualTo(1));
            Assert.That(guarded[0].TotalTriangles, Is.EqualTo(2));

            target = BuildTarget.Android;

            for (var guard = 0; guard < 16 && !dryRun.Done; guard++)
            {
                dryRun.Step();
            }

            Assert.That(
                SlotRows(dryRun), Has.Count.EqualTo(1),
                "a renderer stepped after a target change must not add " +
                "rows computed under a mixed target");
        }

        /// <summary>
        /// A renderer stepped after a policy change must not add its
        /// rows to the run that opened under the old policy, because
        /// the published view would mix two policies. The first
        /// renderer's known row and triangle counts stand as the
        /// no-op guard.
        /// </summary>
        [Test]
        public void IgnoreSlotsChangeBetweenRendererStepsRejectsMixedPolicyRows()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var texture = ImportHalfTexture("half_255_mixed_steps", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            var second = AddSkinnedRenderer(
                fixture.Root, TwoTriangleMesh(), material);
            second.gameObject.name = "Cape";

            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            Assert.That(dryRun.Step(), Is.True, "the first renderer must step");
            var guarded = SlotRows(dryRun);
            Assert.That(guarded, Has.Count.EqualTo(1));
            Assert.That(guarded[0].ProvenTriangles, Is.EqualTo(1));
            Assert.That(guarded[0].TotalTriangles, Is.EqualTo(2));

            SetSwitch(
                fixture.Optimizer, "_ignoreOutOfRangeMaterialSlots", false);

            for (var guard = 0; guard < 16 && !dryRun.Done; guard++)
            {
                dryRun.Step();
            }

            Assert.That(
                SlotRows(dryRun), Has.Count.EqualTo(1),
                "a renderer stepped after a policy change must not add " +
                "rows computed under a mixed policy");
        }

        /// <summary>
        /// Characterization of the mandated validation API, not a
        /// computed-row regression. The unsupported-target fixture is
        /// the existing stand-in: its run completes at the sampling
        /// banner before any step. A changed shared key must latch the
        /// run invalid, keep the rejection after the old key returns,
        /// and never let the completed run publish or recompute.
        /// </summary>
        [Test]
        public void CompletedUnsupportedTargetRunRejectsAChangedSharedKey()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture,
                () => BuildTarget.StandaloneLinux64,
                FixtureSelector(),
                FixtureCapturer());
            dryRun.Begin();

            Assert.That(
                dryRun.Done, Is.True,
                "the run must complete at its sampling banner");
            Assert.That(
                _captureCalls, Is.EqualTo(0),
                "the unsupported target must compute nothing");
            Assert.That(SlotRows(dryRun), Is.Empty);

            // Guard: the unchanged shared key must keep the run valid.
            Assert.That(
                dryRun.ValidatePolicy(PlayModeBuildStore
                    .ComputePolicyFingerprint(
                        fixture.Optimizer,
                        BuildTarget.StandaloneLinux64)),
                Is.True,
                "the unchanged key must keep the completed run valid");

            SetSwitch(fixture.Optimizer, "_cutoutSeparatorEnabled", false);
            Assert.That(
                dryRun.ValidatePolicy(PlayModeBuildStore
                    .ComputePolicyFingerprint(
                        fixture.Optimizer,
                        BuildTarget.StandaloneLinux64)),
                Is.False,
                "a changed shared key must invalidate the completed run");

            SetSwitch(fixture.Optimizer, "_cutoutSeparatorEnabled", true);
            Assert.That(
                dryRun.ValidatePolicy(PlayModeBuildStore
                    .ComputePolicyFingerprint(
                        fixture.Optimizer,
                        BuildTarget.StandaloneLinux64)),
                Is.False,
                "restoring the old key must not clear the latch");
            Assert.That(
                SlotRows(dryRun), Is.Empty,
                "the rejected run must not publish rows");
            Assert.That(
                _captureCalls, Is.EqualTo(0),
                "the rejected run must not recompute");
        }

        // --- No-persistence guard -------------------------------------------

        [Test]
        public void DryRunPersistsNothing()
        {
            var fixture = NewComputeFixture(153, 0.75f, streaming: true);
            // The block rides a name no fixture shader property declares,
            // so the block is present on the renderer while the proof
            // stays on the serialized material. The streaming texture is
            // the fact the guard exists for.
            var block = new MaterialPropertyBlock();
            block.SetFloat("_AmuseGuardUnused", 1f);
            fixture.Renderer.SetPropertyBlock(block);

            var materialsBefore =
                Resources.FindObjectsOfTypeAll<Material>().Length;
            var assetsBefore = AssetDatabase.FindAssets("", new[] { "Assets" })
                .OrderBy(path => path).ToList();

            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            Run(dryRun);

            // The guard must not pass vacuously: the run computed rows.
            Assert.That(
                dryRun.RendererRows,
                Is.Not.Empty,
                "the guard fixture must produce renderer rows before the deltas");
            Assert.That(
                Resources.FindObjectsOfTypeAll<Material>().Length,
                Is.EqualTo(materialsBefore),
                "the dry run must not leak loaded materials");
            Assert.That(
                AssetDatabase.FindAssets("", new[] { "Assets" })
                    .OrderBy(path => path).ToList(),
                Is.EqualTo(assetsBefore),
                "the dry run must not add, remove, or rename assets");
            Assert.That(
                AssetDatabase.IsValidFolder(ReadbackFolder),
                Is.False,
                "the streaming readback route must stay unreachable");
        }

        [Test]
        public void PumpAllAdvancesUntilDone()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();
            Assert.That(dryRun.Done, Is.False, "the run must not finish on begin");
            dryRun.PumpAll();
            Assert.That(dryRun.Done, Is.True, "the run must finish after PumpAll");
            Assert.That(dryRun.RendererRows, Is.Not.Empty, "PumpAll must compute renderer rows");
        }

        [Test]
        public void Step_SynchronousTrue_ExecutesToCompletionOnFixtureAvatar()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            Assert.That(dryRun.Done, Is.False);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Idle));
            Assert.That(dryRun.TotalRendererCount, Is.EqualTo(1));
            Assert.That(dryRun.CurrentRendererIndex, Is.EqualTo(0));

            var continues = dryRun.Step(synchronous: true);
            Assert.That(continues, Is.True);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Idle));
            Assert.That(dryRun.CurrentRendererIndex, Is.EqualTo(1));
            Assert.That(dryRun.RendererRows, Is.Not.Empty);

            continues = dryRun.Step(synchronous: true);
            Assert.That(continues, Is.False);
            Assert.That(dryRun.Done, Is.True);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Completed));
        }

        [Test]
        public void Step_SynchronousFalse_TransitionsThroughStates()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Idle));

            var continues = dryRun.Step(synchronous: false);
            Assert.That(continues, Is.True);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.ClassifyingBackground));
            Assert.That(dryRun.CurrentRendererIndex, Is.EqualTo(0));

            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (dryRun.State == DryRunState.ClassifyingBackground && DateTime.UtcNow < timeout)
            {
                Thread.Sleep(10);
                dryRun.Step(synchronous: false);
            }

            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Idle));
            Assert.That(dryRun.CurrentRendererIndex, Is.EqualTo(1));
            Assert.That(dryRun.RendererRows, Is.Not.Empty);

            continues = dryRun.Step(synchronous: false);
            Assert.That(continues, Is.False);
            Assert.That(dryRun.Done, Is.True);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Completed));
        }

        [Test]
        public void Cancel_AbortsInFlightRunAndSetsIsCancelled()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            Assert.That(dryRun.IsCancelled, Is.False);

            dryRun.Step(synchronous: false);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.ClassifyingBackground));

            dryRun.Cancel(timeoutMs: 500);

            Assert.That(dryRun.IsCancelled, Is.True);
            Assert.That(dryRun.Done, Is.True);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Completed));

            var continues = dryRun.Step(synchronous: false);
            Assert.That(continues, Is.False);
        }

        [Test]
        public void CancellingWhileInFlightDisposesTheSourceWhenTheTaskCompletes()
        {
            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-heavy-root"));
            var optimizer = AddOptimizer(root);
            var texture = ImportHalfTexture("half_heavy", 255);
            var material = NewCutoutMaterial(texture, 0.5f);
            var renderer = AddSkinnedRenderer(
                root, ManyTriangleMesh(16384), material);
            var fixture = new Fixture
            {
                Root = root, Optimizer = optimizer, Renderer = renderer
            };
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            dryRun.Step(synchronous: false);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.ClassifyingBackground));

            var source = InFlightSource(dryRun);
            Assert.That(source, Is.Not.Null, "the run must own a cancellation source");
            var inFlight = InFlightTask(dryRun);
            Assert.That(inFlight, Is.Not.Null, "the run must own an in-flight task");

            WaitUntilRunning(inFlight);

            dryRun.Cancel(timeoutMs: 0);
            Assert.That(dryRun.IsCancelled, Is.True);

            WaitUntilDisposed(source);

            Assert.That(
                TokenIsDisposed(source),
                Is.True,
                "the completion continuation owns disposal");
            Assert.DoesNotThrow(
                () => dryRun.Cancel(),
                "a later Cancel must not throw on the disposed source");
        }

        [Test]
        public void CancelAfterNormalCompletionToleratesDisposedSource()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            dryRun.Step(synchronous: false);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.ClassifyingBackground));

            var source = InFlightSource(dryRun);
            var inFlight = InFlightTask(dryRun);
            Assert.That(source, Is.Not.Null, "the run must own a cancellation source");

            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!inFlight.IsCompleted && DateTime.UtcNow < timeout)
            {
                Thread.Sleep(10);
            }

            Assert.That(inFlight.IsCompleted, Is.True, "the task must complete");
            WaitUntilDisposed(source);

            Assert.That(
                TokenIsDisposed(source),
                Is.True,
                "the completion continuation owns disposal");
            Assert.DoesNotThrow(
                () => dryRun.Cancel(),
                "teardown Cancel must tolerate the disposed source");
        }

        private static CancellationTokenSource InFlightSource(
            AlphaSeparationDryRun dryRun)
        {
            var field = typeof(AlphaSeparationDryRun).GetField(
                "_cts", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "the dry run must declare _cts");
            return (CancellationTokenSource)field.GetValue(dryRun);
        }

        private static Task InFlightTask(AlphaSeparationDryRun dryRun)
        {
            var field = typeof(AlphaSeparationDryRun).GetField(
                "_inFlightTask", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "the dry run must declare _inFlightTask");
            return (Task)field.GetValue(dryRun);
        }

        private static void WaitUntilRunning(Task inFlight)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (inFlight.Status != TaskStatus.Running &&
                   DateTime.UtcNow < timeout)
            {
            }

            Assert.That(
                inFlight.Status,
                Is.EqualTo(TaskStatus.Running),
                "the in-flight task must be running before Cancel");
        }

        private static void WaitUntilDisposed(CancellationTokenSource source)
        {
            var timeout = DateTime.UtcNow.AddSeconds(5);
            while (!TokenIsDisposed(source) && DateTime.UtcNow < timeout)
            {
                Thread.Sleep(10);
            }
        }

        private static bool TokenIsDisposed(CancellationTokenSource source)
        {
            try
            {
                _ = source.Token;
                return false;
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
        }

        [Test]
        public void PumpAll_CompletesMultiRendererAvatarsSynchronously()
        {
            var fixture = NewComputeFixture(255, 0.5f);
            var texture2 = ImportHalfTexture("half_255_second", 255);
            var material2 = NewCutoutMaterial(texture2, 0.5f);
            AddSkinnedRenderer(fixture.Root, TwoTriangleMesh(), material2);

            var dryRun = NewDryRun(
                fixture, null, FixtureSelector(), FixtureCapturer());
            dryRun.Begin();

            Assert.That(dryRun.TotalRendererCount, Is.EqualTo(2));
            Assert.That(dryRun.Done, Is.False);

            dryRun.PumpAll();

            Assert.That(dryRun.Done, Is.True);
            Assert.That(dryRun.State, Is.EqualTo(DryRunState.Completed));
            Assert.That(dryRun.CurrentRendererIndex, Is.EqualTo(2));
            Assert.That(dryRun.RendererRows.Count, Is.GreaterThanOrEqualTo(2));
        }
    }
}
