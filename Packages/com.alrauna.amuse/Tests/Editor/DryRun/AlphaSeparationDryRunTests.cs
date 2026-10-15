using System;
using System.Collections.Generic;
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
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

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
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                var inputs = new MaterialEvidenceCaptureInput[materials.Count];
                var shaders = new Shader[materials.Count];
                for (var index = 0; index < materials.Count; index++)
                {
                    shaders[index] = materials[index] == null
                        ? null
                        : materials[index].shader;
                    inputs[index] = new MaterialEvidenceCaptureInput(
                        materials[index],
                        request,
                        UnityMaterialSemantics.AlphaPredicateRequestFor(
                            materials[index], families[index]));
                }

                var evidence =
                    UnityMaterialEvidenceCapture.Capture(inputs, bounds);
                var captured = new CapturedAlphaMaterial[materials.Count];
                for (var index = 0; index < captured.Length; index++)
                {
                    var family = families[index];
                    LilToonSourceEvidence lilToon;
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
                    else
                    {
                        lilToon = null;
                    }

                    var source = materials[index];
                    captured[index] = new CapturedAlphaMaterial(
                        family,
                        evidence[index],
                        default(PoiyomiSourceEvidence),
                        lilToon,
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
                out ClosedAlphaCaptureOutcome outcome) =>
            {
                _captureCalls++;
                _seenRequests.Add(request);
                return inner(
                    materials, families, request, bounds, out outcome);
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
                Is.EqualTo("Splittable: 1 of out 2 polygons may be moved"));
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
                Is.EqualTo("Untouchable: 0 of out 2 polygons may be moved"));
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
            SetInt(
                fixture.Optimizer,
                "_minimumOpaqueCoverageToPreservePercent",
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
            AaoAtlasTextureAttestation.ResetForTests();
            AaoAtlasTextureAttestation.ReadInstalledPackageVersionOrNull =
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
                AaoAtlasTextureAttestation.ResetForTests();
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
                Is.EqualTo("Untouchable: 0 of out 2 polygons may be moved"));
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
        public void AttestedLockedMaterialReportsDeferredSentenceWithPolygonCount()
        {
            EnsurePoiyomiStandInShader();
            EnsureLockedPoiyomiStandInShader();

            var root = _scope.Track(NewAvatarRoot("amuse-dry-run-locked-attested"));
            var optimizer = AddOptimizer(root);
            var originalShader = Shader.Find(".poiyomi/Poiyomi Toon");
            var original = _scope.Track(new Material(originalShader));
            var locked = _scope.Track(new Material(original));
            locked.shader = Shader.Find("Hidden/Locked/.poiyomi/Poiyomi Toon");
            locked.SetFloat("_ShaderOptimizerEnabled", 1f);
            locked.SetOverrideTag("OriginalShader", ".poiyomi/Poiyomi Toon");
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
                rows[0].Sentence,
                Is.EqualTo("Deferred: 0 of out 1 polygons may be moved (evaluates at build time)"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.False,
                "renderer refusal box must be suppressed when all slots are deferred");
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
                Is.EqualTo("Untouchable: 0 of out 1 polygons may be moved"));
            Assert.That(
                dryRun.RendererRows.Any(
                    row => row.Kind == MaterialTransformationRowKind.Renderer),
                Is.False);
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
            Assert.That(
                inFlight.IsCompleted,
                Is.False,
                "the task must still be running when Cancel abandons it");
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
