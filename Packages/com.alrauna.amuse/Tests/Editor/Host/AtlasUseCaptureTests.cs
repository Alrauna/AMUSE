using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// One capture row per use-scope atlas gate. Every fixture drives the
    /// installed bundled Poiyomi 9.3 shader through its pinned GUID, so a
    /// wrong route fact refuses against the vendor shader and not against
    /// a stand-in. The default fixture is a fresh material with a 64 by
    /// 64 imported PNG on the member texture. The five vendor defaults
    /// of the neutral list stay assigned, because a fresh material
    /// carries them. Each refusal test changes exactly one fact, so the
    /// first-refusal-wins order stays observable.
    /// </summary>
    public sealed class AtlasUseCaptureTests
    {
        private const string TempFolder = "Assets/AmuseTests_AtlasUseCapture";

        private const string LockedStandInShaderName =
            "Hidden/Locked/Alrauna/AmuseTests/AtlasUseCaptureLockedStandIn";

        private static readonly string[] AllowedKeywords =
        {
            "BSSBLOOMFOGTYPE_HEIGHT",
            "VIGNETTE_MASKED",
            "_LIGHTINGMODE_FLAT",
            "_RIMSTYLE_POIYOMI",
            "_RIM2STYLE_POIYOMI",
            "_STOCHASTICMODE_DELIOT_HEITZ",
            "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
        };

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        [SetUp]
        public void SetUp()
        {
            _scope.EnsureTempFolder();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
        }

        // --- the default fixture -----------------------------------------

        /// <summary>
        /// One admitted use on demand: a fresh Poiyomi material, a 64 by
        /// 64 mipmapped imported PNG on the member texture, and one quad
        /// submesh that covers the unit square.
        /// </summary>
        private sealed class AtlasFixture
        {
            internal GameObject Root;
            internal SkinnedMeshRenderer Renderer;
            internal Material Material;
            internal Mesh Mesh;
            internal Texture2D MainTexture;
        }

        private AtlasFixture NewDefaultFixture()
        {
            var root = Track(new GameObject("atlas capture root"));
            var material = NewAdmittedMaterial("main");
            var mesh = Track(NewQuadMesh());
            var renderer =
                AddSkinnedRenderer(root, "Body", mesh, new[] { material });
            return new AtlasFixture
            {
                Root = root,
                Renderer = renderer,
                Material = material,
                Mesh = mesh,
                MainTexture = (Texture2D)material.GetTexture("_MainTex"),
            };
        }

        private static Shader LoadInstalledPoiyomiShader()
        {
            var path = AssetDatabase.GUIDToAssetPath(
                PoiyomiSourceAttestation.CanonicalShaderGuid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed bundled Poiyomi " +
                "Toon shader asset must resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed bundled Poiyomi " +
                "Toon shader asset must load");
            return shader;
        }

        private Material NewAdmittedMaterial(Texture2D mainTexture)
        {
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture("_MainTex", mainTexture);
            return material;
        }

        private Material NewAdmittedMaterial(string textureName)
        {
            return NewAdmittedMaterial(ImportMainTexture(textureName));
        }

        private static Texture2D ImportMainTexture(
            string name,
            int width = 64,
            int height = 64,
            Action<TextureImporter> configure = null)
        {
            var staging = new Texture2D(
                width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(110, 80, 60, 255);
            }

            staging.SetPixels32(pixels);
            staging.Apply(false, false);
            var path = TempFolder + "/" + name + ".png";
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            configure?.Invoke(importer);
            importer.SaveAndReimport();
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(
                loaded, Is.Not.Null,
                "fixture precondition: the atlas source must import");
            return loaded;
        }

        private static Texture2D NewNativeTextureAsset(
            string name, Texture2D texture)
        {
            var path = TempFolder + "/" + name + ".asset";
            AssetDatabase.CreateAsset(texture, path);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(
                loaded, Is.Not.Null,
                "fixture precondition: the native texture asset must load");
            return loaded;
        }

        // --- meshes and renderers ----------------------------------------

        private static Vector2[] FullSquareQuadUvs()
        {
            return new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
            };
        }

        private static Mesh NewQuadMesh(Vector2[] uvs = null)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    Vector3.right + Vector3.up,
                },
                uv = uvs ?? FullSquareQuadUvs(),
            };
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            return mesh;
        }

        /// <summary>
        /// A quad mesh with UV1 and no UV0. The route channel is absent,
        /// so the capture must refuse for the missing channel and not
        /// read the spare channel.
        /// </summary>
        private static Mesh NewQuadWithUv1Only()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                    Vector3.right + Vector3.up,
                },
            };
            mesh.SetUVs(1, new List<Vector2>(FullSquareQuadUvs()));
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            return mesh;
        }

        /// <summary>
        /// A quad mesh with both UV0 and UV1. The spare channel is
        /// present, so a UV1 selector fact is the only thing that can
        /// refuse.
        /// </summary>
        private static Mesh NewQuadWithUv0AndUv1()
        {
            var mesh = NewQuadMesh();
            mesh.SetUVs(1, new List<Vector2>(FullSquareQuadUvs()));
            return mesh;
        }

        /// <summary>
        /// A mesh of one triangle per submesh. Every UV stays inside the
        /// unit square, so a renderer with this mesh and one material per
        /// submesh is admitted.
        /// </summary>
        private static Mesh NewTriangleMesh(int submeshCount)
        {
            var vertices = new Vector3[submeshCount * 3];
            var uv = new Vector2[submeshCount * 3];
            for (var index = 0; index < submeshCount; index++)
            {
                var corner = index * 3;
                vertices[corner] = new Vector3(index * 2f, 0f, 0f);
                vertices[corner + 1] =
                    new Vector3(index * 2f + 1f, 0f, 0f);
                vertices[corner + 2] =
                    new Vector3(index * 2f + 0.5f, 1f, 0f);
                uv[corner] = new Vector2(0.25f, 0.25f);
                uv[corner + 1] = new Vector2(0.75f, 0.25f);
                uv[corner + 2] = new Vector2(0.5f, 0.75f);
            }

            var mesh = new Mesh { vertices = vertices, uv = uv };
            mesh.subMeshCount = submeshCount;
            for (var index = 0; index < submeshCount; index++)
            {
                var corner = index * 3;
                mesh.SetTriangles(
                    new[] { corner, corner + 1, corner + 2 }, index);
            }

            return mesh;
        }

        /// <summary>
        /// A mesh whose only submesh is lines. The route moves triangles,
        /// so a non-triangle topology must refuse.
        /// </summary>
        private static Mesh NewLineMesh()
        {
            var mesh = NewQuadMesh();
            mesh.Clear(false);
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                Vector3.right + Vector3.up,
            };
            mesh.uv = FullSquareQuadUvs();
            mesh.subMeshCount = 1;
            mesh.SetIndices(
                new[] { 0, 1 }, MeshTopology.Lines, 0);
            return mesh;
        }

        private SkinnedMeshRenderer AddSkinnedRenderer(
            GameObject root,
            string childName,
            Mesh mesh,
            Material[] materials)
        {
            var child = Track(new GameObject(childName));
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private MeshRenderer AddMeshRenderer(
            GameObject root,
            string childName,
            Mesh mesh,
            Material[] materials)
        {
            var child = Track(new GameObject(childName));
            child.transform.SetParent(root.transform, false);
            var filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            return renderer;
        }

        // --- the animation graph -----------------------------------------

        private sealed class StubBindings : IPlatformAnimatorBindings
        {
            private readonly RuntimeAnimatorController controller;

            internal StubBindings(
                RuntimeAnimatorController controller = null)
            {
                this.controller = controller;
            }

            public bool IsSpecialMotion(Motion motion)
            {
                return false;
            }

            public IEnumerable<(object, RuntimeAnimatorController, bool)>
                GetInnateControllers(GameObject root)
            {
                if (controller != null)
                {
                    yield return (root, controller, false);
                }
            }
        }

        private static CommittedControllerGraphResult EmptyGraph(
            GameObject root)
        {
            return CommittedControllerGraph.Enumerate(
                root, new StubBindings());
        }

        private CommittedControllerGraphResult GraphOf(
            GameObject root,
            params AnimationClip[] clips)
        {
            Assert.That(
                clips, Is.Not.Empty,
                "fixture precondition: the graph fixture needs a clip");
            var controller =
                Track(new AnimatorController { name = "AtlasUseGraph" });
            controller.AddLayer("Route");
            Track(controller.layers[0].stateMachine);
            foreach (var clip in clips)
            {
                var state = controller.layers[0].stateMachine
                    .AddState("State" + clip.name);
                state.motion = clip;
            }

            return CommittedControllerGraph.Enumerate(
                root, new StubBindings(controller));
        }

        private static void AnimateScalar(
            AnimationClip clip,
            string path,
            string property,
            float value = 1f)
        {
            var binding = EditorCurveBinding.FloatCurve(
                path, typeof(SkinnedMeshRenderer), property);
            AnimationUtility.SetEditorCurve(
                clip, binding, AnimationCurve.Constant(0f, 1f, value));
        }

        private static void AnimateSwap(
            AnimationClip clip,
            string path,
            int slot,
            UnityEngine.Object value)
        {
            var binding = EditorCurveBinding.PPtrCurve(
                path,
                typeof(SkinnedMeshRenderer),
                "m_Materials.Array.data[" + slot + "]");
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                binding,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = value } });
        }

        // --- capture and assertion helpers -------------------------------

        private static IReadOnlyList<AtlasUseOutcome> CaptureUses(
            GameObject root,
            CommittedControllerGraphResult graph = null,
            int protectedMipLevelCount = 1)
        {
            return AtlasUseCapture.Capture(
                root,
                graph ?? EmptyGraph(root),
                new StubBindings(),
                protectedMipLevelCount);
        }

        private static AtlasUseOutcome SingleOutcome(
            IReadOnlyList<AtlasUseOutcome> outcomes)
        {
            Assert.That(outcomes, Has.Count.EqualTo(1));
            return outcomes[0];
        }

        private static void AssertAdmitted(AtlasUseOutcome outcome)
        {
            Assert.That(
                outcome.Refusal, Is.EqualTo(AtlasUseRefusal.None),
                "the use must be admitted");
            Assert.That(outcome.Use, Is.Not.Null);
        }

        private static void AssertRefused(
            AtlasUseOutcome outcome,
            AtlasUseRefusal expected)
        {
            Assert.That(
                outcome.Refusal, Is.EqualTo(expected),
                "the use must refuse with the named cause");
            Assert.That(outcome.Use, Is.Null);
        }

        // --- admission ---------------------------------------------------

        [Test]
        public void FreshPoiyomiMaterialWithItsDefaultTexturesIsAdmitted()
        {
            var fixture = NewDefaultFixture();

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
            Assert.That(outcome.Renderer, Is.SameAs(fixture.Renderer));
            Assert.That(outcome.SlotIndex, Is.Zero);
            var use = outcome.Use;
            Assert.That(use.RendererIndex, Is.Zero);
            Assert.That(use.SlotIndex, Is.Zero);
            Assert.That(use.SubmeshIndex, Is.Zero);
            Assert.That(
                use.ShaderRowName,
                Is.EqualTo(".poiyomi/Old Versions/9.3/Poiyomi Toon"));
            Assert.That(use.SourceWidth, Is.EqualTo(64));
            Assert.That(use.SourceHeight, Is.EqualTo(64));
            Assert.That(
                use.SourceTextureInstanceId,
                Is.EqualTo(fixture.MainTexture.GetInstanceID()));
            Assert.That(use.SourceMipCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(
                use.UvRect, Is.EqualTo(new AtlasUvRect(0, 0, 1, 1)));
            Assert.That(use.MaterialKey, Is.Not.Empty);
            Assert.That(use.AnimatedPropertyNames, Is.Empty);
            Assert.That(use.CaptureOrder, Is.Zero);
        }

        [Test]
        public void RequiredValueNotHeldRefuses(
            [Values(
                "_MainTexUV",
                "_MainPixelMode",
                "_MainTexStochastic",
                "_RenderingAOBlockerEnabled")] string property)
        {
            var fixture = NewDefaultFixture();
            fixture.Material.SetFloat(property, 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(
                outcome.Detail,
                Is.EqualTo(
                    "The property '" + property + "' is 1. The route" +
                    " needs 0."),
                "the detail must name the property and its value");
        }

        [Test]
        public void SharpenedA2CWithCoverageRefuses()
        {
            var fixture = NewDefaultFixture();
            fixture.Material.SetFloat("_AlphaSharpenedA2C", 1f);
            fixture.Material.SetFloat("_AlphaToCoverage", 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(outcome.Detail, Does.Contain("are both on"));
        }

        [Test]
        public void SharpenedA2CWithoutCoverageIsAdmitted()
        {
            // The disjunction holds when either side is zero, so the
            // sharpened flag alone must not refuse.
            var fixture = NewDefaultFixture();
            fixture.Material.SetFloat("_AlphaSharpenedA2C", 1f);
            fixture.Material.SetFloat("_AlphaToCoverage", 0f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
        }

        [Test]
        public void NonAllowedKeywordRefuses()
        {
            var fixture = NewDefaultFixture();
            fixture.Material.EnableKeyword("POI_UDIMDISCARD");

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.KeywordNotAllowed);
            Assert.That(outcome.Detail, Does.Contain("POI_UDIMDISCARD"));
        }

        [Test]
        public void AllowedKeywordSubsetIsAdmitted()
        {
            var fixture = NewDefaultFixture();
            foreach (var keyword in AllowedKeywords)
            {
                fixture.Material.DisableKeyword(keyword);
            }

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
        }

        [Test]
        public void EachAllowedKeywordIsAdmitted(
            [ValueSource(nameof(AllowedKeywords))] string keyword)
        {
            var fixture = NewDefaultFixture();
            foreach (var other in AllowedKeywords)
            {
                fixture.Material.DisableKeyword(other);
            }

            fixture.Material.EnableKeyword(keyword);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
        }

        [Test]
        public void UnlistedAssignedTextureRefuses()
        {
            var fixture = NewDefaultFixture();
            fixture.Material.SetTexture(
                "_MatcapMask", fixture.MainTexture);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.UnlistedTexture);
            Assert.That(outcome.Detail, Does.Contain("'_MatcapMask'"));
        }

        // --- the texture transform ----------------------------------------

        [Test]
        public void NonIdentityScaleRefuses()
        {
            var fixture = NewDefaultFixture();
            fixture.Material.SetTextureScale(
                "_MainTex", new Vector2(2f, 1f));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.NonIdentityTransform);
            Assert.That(
                outcome.Detail,
                Is.EqualTo(
                    "The property '_MainTex_ST' is (2, 1, 0, 0). The" +
                    " route needs (1, 1, 0, 0)."));
            Assert.That(
                outcome.Material, Is.SameAs(fixture.Material),
                "a refused outcome must keep its slot material");
            Assert.That(
                outcome.MemberTexture, Is.SameAs(fixture.MainTexture),
                "a refusal after the texture read must keep the texture");
        }

        [Test]
        public void ScaleOneFloatStepOffUnityRefuses()
        {
            // The literal 1.0000001f is the next representable float
            // above 1f. Unity's Vector4 equality calls it equal to
            // the identity, and the scale setter collapses it, so the
            // row writes the transform vector and only an exact
            // component compare refuses it.
            var fixture = NewDefaultFixture();
            fixture.Material.SetVector(
                "_MainTex_ST", new Vector4(1.0000001f, 1f, 0f, 0f));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.NonIdentityTransform);
        }

        [Test]
        public void NonZeroPanRefuses()
        {
            var fixture = NewDefaultFixture();
            fixture.Material.SetVector(
                "_MainTexPan", new Vector4(0.25f, 0f, 0f, 0f));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.NonIdentityTransform);
        }

        // --- the route channel --------------------------------------------

        [Test]
        public void MainTextureOnUv1Refuses()
        {
            var root = Track(new GameObject("uv1 selector root"));
            var material = NewAdmittedMaterial("uv1 main");
            var mesh = Track(NewQuadWithUv0AndUv1());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });
            material.SetFloat("_MainTexUV", 1f);

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
        }

        [Test]
        public void MissingUv0Refuses()
        {
            var root = Track(new GameObject("missing uv0 root"));
            var material = NewAdmittedMaterial("missing uv0 main");
            var mesh = Track(NewQuadWithUv1Only());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.MissingRouteChannel);
        }

        [Test]
        public void UvOutsideTheUnitSquareRefuses()
        {
            var root = Track(new GameObject("outside uv root"));
            var material = NewAdmittedMaterial("outside main");
            var uvs = FullSquareQuadUvs();
            uvs[1] = new Vector2(1.0001f, 0f);
            var mesh = Track(NewQuadMesh(uvs));
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UvOutsideUnitSquare);
            Assert.That(outcome.Detail, Does.Contain("(1.0001, 0)"));
        }

        [Test]
        public void VertexExactlyAtOneIsAdmitted()
        {
            var root = Track(new GameObject("edge uv root"));
            var material = NewAdmittedMaterial("edge main");
            var mesh = Track(NewQuadMesh(FullSquareQuadUvs()));
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertAdmitted(outcome);
            Assert.That(
                outcome.Use.UvRect,
                Is.EqualTo(new AtlasUvRect(0, 0, 1, 1)));
        }

        [Test]
        public void NaNUvRefuses()
        {
            var root = Track(new GameObject("nan uv root"));
            var material = NewAdmittedMaterial("nan main");
            var uvs = FullSquareQuadUvs();
            uvs[0] = new Vector2(float.NaN, 0f);
            var mesh = Track(NewQuadMesh(uvs));
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UvOutsideUnitSquare);
        }

        // --- renderer and slot shape --------------------------------------

        [Test]
        public void NullSlotMaterialRefuses()
        {
            var root = Track(new GameObject("null material root"));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new Material[] { null });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.MissingMaterial);
        }

        [Test]
        public void NullMainTextureRefuses()
        {
            var root = Track(new GameObject("null main root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.MissingMainTexture);
        }

        [Test]
        public void RenderTextureSourceRefuses()
        {
            var root = Track(new GameObject("render texture root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            var renderTexture = Track(new RenderTexture(
                64, 64, 0, RenderTextureFormat.ARGB32));
            material.SetTexture("_MainTex", renderTexture);
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(
                outcome, AtlasUseRefusal.UnsupportedTextureDimension);
        }

        [Test]
        public void NullMeshRefuses()
        {
            var root = Track(new GameObject("null mesh root"));
            var material = NewAdmittedMaterial("null mesh main");
            AddSkinnedRenderer(root, "Body", null, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.MissingMesh);
        }

        [Test]
        public void ParticleRendererRefuses()
        {
            var root = Track(new GameObject("particle root"));
            var material = NewAdmittedMaterial("particle main");
            var child = Track(new GameObject("Dust"));
            child.transform.SetParent(root.transform, false);
            child.AddComponent<ParticleSystem>();
            var particleRenderer =
                child.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterials = new[] { material };

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UnsupportedRenderer);
        }

        [Test]
        public void NonTriangleTopologyRefuses()
        {
            var root = Track(new GameObject("lines root"));
            var material = NewAdmittedMaterial("lines main");
            var mesh = Track(NewLineMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.NonTriangleTopology);
        }

        [Test]
        public void EmptySubmeshRefuses()
        {
            var root = Track(new GameObject("empty submesh root"));
            var material = NewAdmittedMaterial("empty submesh main");
            var mesh = Track(NewQuadMesh());
            mesh.SetTriangles(new int[0], 0);
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.EmptySubmesh);
        }

        [Test]
        public void ExtraSlotsRefuseTheLastSubmeshUses()
        {
            var root = Track(new GameObject("extra slots root"));
            var first = NewAdmittedMaterial("extra main a");
            var second = NewAdmittedMaterial("extra main b");
            var third = NewAdmittedMaterial("extra main c");
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(
                root, "Body", mesh, new[] { first, second, third });

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(3));
            AssertAdmitted(outcomes[0]);
            AssertRefused(outcomes[1], AtlasUseRefusal.SlotSharesSubmesh);
            AssertRefused(outcomes[2], AtlasUseRefusal.SlotSharesSubmesh);
        }

        // --- property blocks ------------------------------------------------

        [Test]
        public void SlotPropertyBlockOnTheMainTextureRefusesOnlyThatSlot()
        {
            var root = Track(new GameObject("slot block root"));
            var first = NewAdmittedMaterial("slot block a");
            var second = NewAdmittedMaterial("slot block b");
            var mesh = Track(NewTriangleMesh(2));
            var renderer =
                AddMeshRenderer(root, "Body", mesh, new[] { first, second });

            var block = new MaterialPropertyBlock();
            block.SetTexture("_MainTex", Texture2D.whiteTexture);
            renderer.SetPropertyBlock(block, 0);

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertRefused(
                outcomes[0], AtlasUseRefusal.PropertyBlockOnRouteProperty);
            AssertAdmitted(outcomes[1]);
        }

        [Test]
        public void RendererWidePropertyBlockRefusesEverySlot()
        {
            var root = Track(new GameObject("wide block root"));
            var first = NewAdmittedMaterial("wide block a");
            var second = NewAdmittedMaterial("wide block b");
            var mesh = Track(NewTriangleMesh(2));
            var renderer =
                AddMeshRenderer(root, "Body", mesh, new[] { first, second });

            var block = new MaterialPropertyBlock();
            block.SetFloat("_MainTexUV", 1f);
            renderer.SetPropertyBlock(block);

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertRefused(
                outcomes[0], AtlasUseRefusal.PropertyBlockOnRouteProperty);
            AssertRefused(
                outcomes[1], AtlasUseRefusal.PropertyBlockOnRouteProperty);
        }

        // --- animation -----------------------------------------------------

        [Test]
        public void AnimatedMainTextureTransformRefuses()
        {
            var fixture = NewDefaultFixture();
            var clip = Track(new AnimationClip { name = "Pan" });
            AnimateScalar(clip, "Body", "material._MainTex_ST.z", 0.5f);
            var graph = GraphOf(fixture.Root, clip);

            var outcome = SingleOutcome(CaptureUses(fixture.Root, graph));

            AssertRefused(outcome, AtlasUseRefusal.AnimatedRouteProperty);
        }

        [Test]
        public void AnimatedConditionPropertyRefuses()
        {
            var fixture = NewDefaultFixture();
            var clip = Track(new AnimationClip { name = "PixelMode" });
            AnimateScalar(clip, "Body", "material._MainPixelMode");
            var graph = GraphOf(fixture.Root, clip);

            var outcome = SingleOutcome(CaptureUses(fixture.Root, graph));

            AssertRefused(outcome, AtlasUseRefusal.AnimatedRouteProperty);
        }

        [Test]
        public void SwappedSlotRefuses()
        {
            var fixture = NewDefaultFixture();
            var swapTarget = NewAdmittedMaterial("swap target");
            var clip = Track(new AnimationClip { name = "Swap" });
            AnimateSwap(clip, "Body", 0, swapTarget);
            var graph = GraphOf(fixture.Root, clip);

            var outcome = SingleOutcome(CaptureUses(fixture.Root, graph));

            AssertRefused(outcome, AtlasUseRefusal.AnimatedMaterialSlot);
        }

        [Test]
        public void SwapCurveOnAnAppendedSlotRefuses()
        {
            // The renderer holds two original slots. Alpha apply appended
            // a third slot, and a committed clip swaps it. The graph the
            // caller passes was enumerated after apply, so the capture
            // must see the swap curve on slot 2. A stale graph from
            // before apply would admit slot 2.
            var root = Track(new GameObject("appended slot root"));
            var materials = new[]
            {
                NewAdmittedMaterial("appended a"),
                NewAdmittedMaterial("appended b"),
                NewAdmittedMaterial("appended c"),
            };
            var mesh = Track(NewTriangleMesh(3));
            AddSkinnedRenderer(root, "Body", mesh, materials);
            var clip = Track(new AnimationClip { name = "AppendedSwap" });
            AnimateSwap(clip, "Body", 2, materials[0]);
            var graph = GraphOf(root, clip);

            var outcomes = CaptureUses(root, graph);

            Assert.That(outcomes, Has.Count.EqualTo(3));
            AssertAdmitted(outcomes[0]);
            AssertAdmitted(outcomes[1]);
            AssertRefused(outcomes[2], AtlasUseRefusal.AnimatedMaterialSlot);
        }

        [Test]
        public void AnimatedMeshRefusesEveryUse()
        {
            var root = Track(new GameObject("mesh swap root"));
            var first = NewAdmittedMaterial("mesh swap a");
            var second = NewAdmittedMaterial("mesh swap b");
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(root, "Body", mesh, new[] { first, second });
            var clip = Track(new AnimationClip { name = "MeshSwap" });
            var binding = EditorCurveBinding.PPtrCurve(
                "Body", typeof(SkinnedMeshRenderer), "m_Mesh");
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                binding,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = mesh } });
            var graph = GraphOf(root, clip);

            var outcomes = CaptureUses(root, graph);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertRefused(outcomes[0], AtlasUseRefusal.AnimatedRendererShape);
            AssertRefused(outcomes[1], AtlasUseRefusal.AnimatedRendererShape);
        }

        [Test]
        public void AnimatedSlotCountRefusesEveryUse()
        {
            var root = Track(new GameObject("slot count root"));
            var first = NewAdmittedMaterial("slot count a");
            var second = NewAdmittedMaterial("slot count b");
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(root, "Body", mesh, new[] { first, second });
            var clip = Track(new AnimationClip { name = "SlotCount" });
            AnimateScalar(clip, "Body", "m_Materials.Array.size", 3f);
            var graph = GraphOf(root, clip);

            var outcomes = CaptureUses(root, graph);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertRefused(outcomes[0], AtlasUseRefusal.AnimatedRendererShape);
            AssertRefused(outcomes[1], AtlasUseRefusal.AnimatedRendererShape);
        }

        [Test]
        public void RefusedGraphRefusesEveryUse()
        {
            var fixture = NewDefaultFixture();
            var graph = new CommittedControllerGraphResult(
                AvatarAnimationRefusal.UnsupportedAnimatorControllerForm,
                new List<CommittedLayer>());

            var outcome = SingleOutcome(CaptureUses(fixture.Root, graph));

            AssertRefused(outcome, AtlasUseRefusal.AnimationGraphUnavailable);
        }

        [Test]
        public void NullGraphRefusesEveryUse()
        {
            var fixture = NewDefaultFixture();

            var outcomes = AtlasUseCapture.Capture(
                fixture.Root, null, new StubBindings(), 1);

            AssertRefused(SingleOutcome(outcomes),
                AtlasUseRefusal.AnimationGraphUnavailable);
        }

        [Test]
        public void NullBindingsAndNullGraphRefuseEveryUse()
        {
            var fixture = NewDefaultFixture();

            var outcomes = AtlasUseCapture.Capture(
                fixture.Root, null, null, 1);

            AssertRefused(SingleOutcome(outcomes),
                AtlasUseRefusal.AnimationGraphUnavailable);
        }

        [Test]
        public void ZeroSubmeshMeshRefuses()
        {
            var root = Track(new GameObject("empty mesh root"));
            var material = NewAdmittedMaterial("empty mesh main");
            var mesh = Track(new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                },
            });
            mesh.subMeshCount = 0;
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.MissingMesh);
        }

        // --- the shader row -------------------------------------------------

        [Test]
        public void LockedMaterialRefuses()
        {
            var root = Track(new GameObject("locked root"));
            var shader = TestShaderWriter.WriteStandInShader(
                TempFolder + "/AtlasLockedStandIn.shader",
                LockedStandInShaderName,
                "_ShaderOptimizerEnabled (\"Lock\", Float) = 0\n" +
                "_MainTex (\"Main\", 2D) = \"white\" {}");
            var material = Track(new Material(shader));
            material.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.LockedShader);
        }

        [Test]
        public void StandardShaderRefuses()
        {
            var root = Track(new GameObject("standard root"));
            var shader = Shader.Find("Standard");
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the built-in Standard shader " +
                "must resolve");
            var material = Track(new Material(shader));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UnattestedShader);
        }

        // --- the member texture ---------------------------------------------

        [Test]
        public void MirrorWrapRefuses()
        {
            var root = Track(new GameObject("mirror root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                ImportMainTexture(
                    "mirror main",
                    configure: importer => importer.wrapMode =
                        TextureWrapMode.Mirror));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UnsupportedWrap);
            Assert.That(outcome.Detail, Does.Contain("Mirror"));
        }

        [Test]
        public void CrunchedSourceRefuses()
        {
            var root = Track(new GameObject("crunch root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                ImportMainTexture(
                    "crunch main",
                    configure: importer =>
                    {
                        importer.textureCompression =
                            TextureImporterCompression.Compressed;
                        importer.crunchedCompression = true;
                    }));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UnsupportedFormat);
        }

        [Test]
        public void SingleChannelSourceRefuses()
        {
            var root = Track(new GameObject("single channel root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                ImportMainTexture(
                    "single channel main",
                    configure: importer => importer.textureType =
                        TextureImporterType.SingleChannel));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UnsupportedFormat);
        }

        [Test]
        public void GeneratedTextureWithoutAnAssetRefuses()
        {
            var root = Track(new GameObject("no asset root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                Track(new Texture2D(64, 64, TextureFormat.RGBA32, false)));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.SourceNotAnAsset);
        }

        [Test]
        public void NativeAssetTextureSourceRefuses()
        {
            var root = Track(new GameObject("native asset root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            var texture = NewNativeTextureAsset(
                "native asset main",
                new Texture2D(64, 64, TextureFormat.RGBA32, true));
            material.SetTexture("_MainTex", texture);
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.SourceNotAnAsset);
        }

        [Test]
        public void SubAssetTextureSourceRefuses()
        {
            var root = Track(new GameObject("sub asset root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            // The texture is a sub-asset of a native material asset,
            // so its path names a file no texture importer owns.
            var container = new Material(LoadInstalledPoiyomiShader());
            var containerPath = TempFolder + "/sub asset container.asset";
            AssetDatabase.CreateAsset(container, containerPath);
            var texture = new Texture2D(
                64, 64, TextureFormat.RGBA32, true)
            {
                name = "sub asset main",
            };
            AssetDatabase.AddObjectToAsset(texture, containerPath);
            AssetDatabase.ImportAsset(
                containerPath, ImportAssetOptions.ForceSynchronousImport);
            material.SetTexture("_MainTex", texture);
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.SourceNotAnAsset);
        }

        [Test]
        public void SourceWithFewerLevelsThanProtectedRefuses()
        {
            var root = Track(new GameObject("short mips root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                ImportMainTexture(
                    "short mips main",
                    configure: importer => importer.mipmapEnabled =
                        false));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(
                CaptureUses(root, protectedMipLevelCount: 2));

            AssertRefused(outcome, AtlasUseRefusal.SourceMipChainTooShort);
        }

        [Test]
        public void TransparentSlotAndItsOpaqueCloneHaveDifferentMaterialKeys()
        {
            var root = Track(new GameObject("key root"));
            var mainTexture = ImportMainTexture("key main");
            var transparent = NewAdmittedMaterial(mainTexture);
            transparent.renderQueue = 3000;
            transparent.SetOverrideTag("RenderType", "Transparent");
            var opaque = NewAdmittedMaterial(mainTexture);
            opaque.renderQueue = 2000;
            opaque.SetOverrideTag("RenderType", "Opaque");
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(
                root, "Body", mesh, new[] { transparent, opaque });

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertAdmitted(outcomes[0]);
            AssertAdmitted(outcomes[1]);
            Assert.That(
                outcomes[0].Use.MaterialKey,
                Is.Not.EqualTo(outcomes[1].Use.MaterialKey),
                "render queue and RenderType are material facts, so " +
                "the keys must differ");
        }

        [Test]
        public void MaterialsThatDifferOnlyInMainTextureShareAKey()
        {
            // The member texture moves into the page, so its reference
            // stays out of the key. A falsifier that kept the reference
            // would split these two uses for no reason.
            var root = Track(new GameObject("member key root"));
            var first = NewAdmittedMaterial("member key a");
            var second = NewAdmittedMaterial("member key b");
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(
                root, "Body", mesh, new[] { first, second });

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertAdmitted(outcomes[0]);
            AssertAdmitted(outcomes[1]);
            Assert.That(
                outcomes[0].Use.MaterialKey,
                Is.EqualTo(outcomes[1].Use.MaterialKey),
                "the member texture reference must not enter the key");
        }

        [Test]
        public void IdenticalMaterialsShareAKey()
        {
            var root = Track(new GameObject("identical key root"));
            var mainTexture = ImportMainTexture("identical key main");
            var first = NewAdmittedMaterial(mainTexture);
            var second = NewAdmittedMaterial(mainTexture);
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(
                root, "Body", mesh, new[] { first, second });

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertAdmitted(outcomes[0]);
            AssertAdmitted(outcomes[1]);
            Assert.That(
                outcomes[0].Use.MaterialKey,
                Is.EqualTo(outcomes[1].Use.MaterialKey),
                "two materials with the same serialized facts share " +
                "one key");
        }

        // --- the generic key tier ----------------------------------------

        private const string ShaderWithADeclaredFoo = @"
Shader ""Hidden/Alrauna/AmuseTests/AtlasUseCaptureFooDeclared""
{
    Properties
    {
        _Foo (""Foo"", Float) = 0
    }
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include ""UnityCG.cginc""
            float _Foo;
            float4 frag(v2f_img image) : SV_Target
            {
                return float4(_Foo, 1, 0, 1);
            }
            ENDCG
        }
    }
}";

        private const string ShaderWithoutADeclaredFoo = @"
Shader ""Hidden/Alrauna/AmuseTests/AtlasUseCaptureFooUndeclared""
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include ""UnityCG.cginc""
            float _Foo;
            float4 frag(v2f_img image) : SV_Target
            {
                return float4(_Foo, 1, 0, 1);
            }
            ENDCG
        }
    }
}";

        [Test]
        public void StaleSavedValueDoesNotReachAnUndeclaredUniform()
        {
            // Unity pins this fact on this editor. A saved value for
            // a property that the shader no longer lists stays in
            // the serialized data, but it never reaches the uniform
            // that the shader code declares. The green channel is a
            // shader constant. It proves that the pass ran, so the
            // black red channel means the uniform read zero and not
            // a broken pass. The key still counts the entry until a
            // reviewed model declares it dead.
            var withProperty = Track(ShaderUtil.CreateShaderAsset(
                ShaderWithADeclaredFoo));
            var withoutProperty = Track(ShaderUtil.CreateShaderAsset(
                ShaderWithoutADeclaredFoo));
            var material = Track(new Material(withProperty));
            material.SetFloat("_Foo", 1f);
            material.shader = withoutProperty;

            Assert.That(
                material.HasProperty("_Foo"), Is.False,
                "fixture guard: the second shader must not declare _Foo");
            var color = BlitColorOf(material);

            Assert.That(color.r, Is.EqualTo(0f).Within(0.001f));
            Assert.That(color.g, Is.EqualTo(1f).Within(0.001f));
            Assert.That(color.b, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DeclaredSavedValueReachesTheShader()
        {
            // The control of the test above. The same uniform on the
            // shader that lists the property reads the saved value.
            // The green channel is a shader constant. It proves that
            // the pass ran.
            var withProperty = Track(ShaderUtil.CreateShaderAsset(
                ShaderWithADeclaredFoo));
            var material = Track(new Material(withProperty));
            material.SetFloat("_Foo", 1f);

            Assert.That(
                material.HasProperty("_Foo"), Is.True,
                "fixture guard: the first shader must declare _Foo");
            var color = BlitColorOf(material);

            Assert.That(color.r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(color.g, Is.EqualTo(1f).Within(0.001f));
            Assert.That(color.b, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ExplicitQueueEqualToTheShaderQueueLeavesTheKey()
        {
            var plain = NewLilToonFixture();
            var queued = NewLilToonFixture();
            queued.Material.renderQueue =
                queued.Material.shader.renderQueue;

            var plainOutcome = CaptureLilToon(plain);
            var queuedOutcome = CaptureLilToon(queued);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(queuedOutcome);
            Assert.That(
                CustomRenderQueueOf(queued.Material),
                Is.Not.EqualTo(CustomRenderQueueOf(plain.Material)),
                "fixture guard: the explicit queue must be serialized");
            Assert.That(
                queuedOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "an explicit queue that equals the shader queue is the " +
                "same render state");
        }

        // --- Falsifier 1: a queue that differs from the shader queue
        // changes rendering, so it must change the key. ---
        [Test]
        public void ExplicitQueueDifferentFromTheShaderQueueChangesTheKey()
        {
            var plain = NewLilToonFixture();
            var queued = NewLilToonFixture();
            queued.Material.renderQueue = 2001;

            var plainOutcome = CaptureLilToon(plain);
            var queuedOutcome = CaptureLilToon(queued);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(queuedOutcome);
            Assert.That(
                queuedOutcome.Use.MaterialKey,
                Is.Not.EqualTo(plainOutcome.Use.MaterialKey),
                "a queue of 2001 draws after a queue of 2000");
        }

        [Test]
        public void InvalidKeywordLeavesTheKey()
        {
            var plain = NewLilToonFixture();
            var flagged = NewLilToonFixture();
            flagged.Material.EnableKeyword("AMUSE_TEST_UNDECLARED");

            var plainOutcome = CaptureLilToon(plain);
            var flaggedOutcome = CaptureLilToon(flagged);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(flaggedOutcome);
            Assert.That(
                InvalidKeywordsOf(flagged.Material),
                Does.Contain("AMUSE_TEST_UNDECLARED"),
                "fixture guard: the keyword must be serialized as " +
                "invalid");
            Assert.That(
                InvalidKeywordsOf(plain.Material),
                Is.Empty,
                "fixture guard: the plain fixture holds no invalid " +
                "keyword");
            Assert.That(
                flaggedOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "a keyword that the shader does not declare cannot " +
                "change rendering");
        }

        // --- Falsifier 2: no reviewed model has declared the stale
        // entry dead, so it must still change the key. ---
        [Test]
        public void StaleSavedEntryStillChangesTheKeyWithoutAReviewedModel()
        {
            var root = Track(new GameObject("stale key root"));
            var mainTexture = ImportMainTexture("stale key main");
            var plain = NewAdmittedMaterial(mainTexture);
            var stale = NewAdmittedMaterial(mainTexture);
            WriteStaleFloat(stale, "_DetailMask", 1f);
            var mesh = Track(NewTriangleMesh(2));
            AddSkinnedRenderer(
                root, "Body", mesh, new[] { plain, stale });

            var outcomes = CaptureUses(root);

            Assert.That(outcomes, Has.Count.EqualTo(2));
            AssertAdmitted(outcomes[0]);
            AssertAdmitted(outcomes[1]);
            Assert.That(
                outcomes[1].Use.MaterialKey,
                Is.Not.EqualTo(outcomes[0].Use.MaterialKey),
                "the Poiyomi row has no dead property set, so the " +
                "stale saved entry still counts");
        }

        [Test]
        public void SavedPropertyOrderDoesNotShiftLaterTokens()
        {
            var plain = NewLilToonFixture();
            var reordered = NewLilToonFixture();
            InsertStaleTexEnvBeforeMainTex(
                reordered.Material, "_AmuseStaleBeforeMainTex");

            var plainOutcome = CaptureLilToon(plain);
            var reorderedOutcome = CaptureLilToon(reordered);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(reorderedOutcome);
            Assert.That(
                SingleMainTexTokenOf(reorderedOutcome.Use.MaterialKey),
                Is.EqualTo(SingleMainTexTokenOf(plainOutcome.Use.MaterialKey)),
                "a stale entry before the member must not shift the " +
                "token of the member");
        }

        private static Color BlitColorOf(Material material)
        {
            var renderTexture = RenderTexture.GetTemporary(
                4, 4, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D read = null;
            try
            {
                Graphics.Blit(
                    Texture2D.whiteTexture, renderTexture, material);
                RenderTexture.active = renderTexture;
                read = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, 4, 4), 0, 0, false);
                read.Apply(false, false);
                return read.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(renderTexture);
                if (read != null)
                {
                    UnityEngine.Object.DestroyImmediate(read);
                }
            }
        }

        private static int CustomRenderQueueOf(Material material)
        {
            using var serialized = new SerializedObject(material);
            return serialized.FindProperty("m_CustomRenderQueue").intValue;
        }

        private static List<string> InvalidKeywordsOf(Material material)
        {
            using var serialized = new SerializedObject(material);
            var keywords = serialized.FindProperty("m_InvalidKeywords");
            var names = new List<string>();
            for (var index = 0; index < keywords.arraySize; index++)
            {
                names.Add(
                    keywords.GetArrayElementAtIndex(index).stringValue);
            }

            return names;
        }

        private const string SavedTexEnvsPath =
            "m_SavedProperties.m_TexEnvs";

        private const string SavedFloatsPath =
            "m_SavedProperties.m_Floats";

        private static void WriteStaleFloat(
            Material material, string name, float value)
        {
            using var serialized = new SerializedObject(material);
            var floats = serialized.FindProperty(SavedFloatsPath);
            var index = floats.arraySize;
            floats.arraySize = index + 1;
            var entry = floats.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("first").stringValue = name;
            entry.FindPropertyRelative("second").floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            using var verify = new SerializedObject(material);
            var verified = verify.FindProperty(SavedFloatsPath);
            Assert.That(
                SavedFloatNamesOf(verified), Does.Contain(name),
                "fixture guard: the stale float entry must persist");
        }

        private static List<string> SavedFloatNamesOf(
            SerializedProperty floats)
        {
            var names = new List<string>();
            for (var index = 0; index < floats.arraySize; index++)
            {
                names.Add(SavedNameOf(
                    floats.GetArrayElementAtIndex(index)));
            }

            return names;
        }

        private static void InsertStaleTexEnvBeforeMainTex(
            Material material, string name)
        {
            using var serialized = new SerializedObject(material);
            var texEnvs = serialized.FindProperty(SavedTexEnvsPath);
            var mainIndex = SavedTexEnvIndexOf(texEnvs, "_MainTex");
            Assert.That(
                mainIndex, Is.GreaterThanOrEqualTo(0),
                "fixture precondition: the member entry must exist");
            var staleIndex = texEnvs.arraySize;
            texEnvs.arraySize = staleIndex + 1;
            var entry = texEnvs.GetArrayElementAtIndex(staleIndex);
            entry.FindPropertyRelative("first").stringValue = name;
            entry.FindPropertyRelative("second.m_Texture")
                .objectReferenceValue = null;
            entry.FindPropertyRelative("second.m_Scale").vector2Value =
                Vector2.one;
            entry.FindPropertyRelative("second.m_Offset").vector2Value =
                Vector2.zero;
            texEnvs.MoveArrayElement(staleIndex, mainIndex);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            using var verify = new SerializedObject(material);
            var verified = verify.FindProperty(SavedTexEnvsPath);
            Assert.That(
                SavedTexEnvIndexOf(verified, name),
                Is.LessThan(SavedTexEnvIndexOf(verified, "_MainTex")),
                "fixture guard: the stale entry must sit before the " +
                "member entry");
        }

        private static int SavedTexEnvIndexOf(
            SerializedProperty texEnvs, string name)
        {
            for (var index = 0; index < texEnvs.arraySize; index++)
            {
                if (string.Equals(
                        SavedNameOf(texEnvs.GetArrayElementAtIndex(index)),
                        name,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string SavedNameOf(SerializedProperty element)
        {
            var first = element.FindPropertyRelative("first");
            return first == null ? null : first.stringValue;
        }

        private static string SingleMainTexTokenOf(string key)
        {
            var tokens = key
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.Contains("_MainTex="))
                .ToList();
            Assert.That(
                tokens, Has.Count.EqualTo(1),
                "the key must hold exactly one saved token for _MainTex");
            return tokens[0];
        }

        // --- lilToon rows ----------------------------------------------------

        private const string LilToonPackageShaderFolder =
            "Packages/jp.lilxyzw.liltoon/Shader/";

        private static Shader LoadInstalledLilToonShader(string assetGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed lilToon shader must " +
                "resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed lilToon shader must " +
                "load");
            return shader;
        }

        private static Shader LoadLilToonPackageShader(string fileName)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                LilToonPackageShaderFolder + fileName);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed lilToon package " +
                "must hold " + fileName);
            return shader;
        }

        /// <summary>
        /// One use on a fresh lilToon material with a 64 by 64 imported
        /// PNG on the member texture. A fresh material assigns no other
        /// texture, so the neutral list of the row is empty.
        /// </summary>
        private AtlasFixture NewLilToonFixture(Shader shader)
        {
            var root = Track(new GameObject("lilToon capture root"));
            var material = Track(new Material(shader));
            material.SetTexture("_MainTex", ImportMainTexture("liltoon main"));
            var mesh = Track(NewQuadMesh());
            var renderer =
                AddSkinnedRenderer(root, "Body", mesh, new[] { material });
            return new AtlasFixture
            {
                Root = root,
                Renderer = renderer,
                Material = material,
                Mesh = mesh,
                MainTexture = (Texture2D)material.GetTexture("_MainTex"),
            };
        }

        private AtlasFixture NewLilToonFixture()
        {
            return NewLilToonFixture(LoadInstalledLilToonShader(
                LilToonSourceAttestation.SupportedShaderGuid));
        }

        [TestCase(LilToonSourceAttestation.SupportedShaderGuid)]
        [TestCase(LilToonSourceAttestation.CutoutShaderGuid)]
        [TestCase(LilToonSourceAttestation.TransparentShaderGuid)]
        [TestCase(LilToonSourceAttestation.OutlineShaderGuid)]
        [TestCase(LilToonSourceAttestation.OutlineCutoutShaderGuid)]
        [TestCase(LilToonSourceAttestation.OutlineTransparentShaderGuid)]
        [TestCase(LilToonSourceAttestation.OnePassTransparentShaderGuid)]
        [TestCase(LilToonSourceAttestation.TwoPassTransparentShaderGuid)]
        [TestCase(
            LilToonSourceAttestation.OnePassTransparentOutlineShaderGuid)]
        [TestCase(
            LilToonSourceAttestation.TwoPassTransparentOutlineShaderGuid)]
        public void FreshLilToonMaterialIsAdmittedOnEveryRow(string shaderGuid)
        {
            var shader = LoadInstalledLilToonShader(shaderGuid);
            var fixture = NewLilToonFixture(shader);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
            Assert.That(outcome.Use.ShaderRowName, Is.EqualTo(shader.name));
            Assert.That(
                outcome.Use.SourceTextureInstanceId,
                Is.EqualTo(fixture.MainTexture.GetInstanceID()));
        }

        [Test]
        public void LilToonRequiredValueNotHeldRefuses(
            [Values(
                "_ShiftBackfaceUV",
                "_UseParallax",
                "_UseMain3rdTex",
                "_UseGlitter",
                "_UseAudioLink")] string property)
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat(property, 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
        }

        [Test]
        public void LilToonNaNFlagRefuses()
        {
            // The shader branches on a nonzero flag, and NaN is not
            // zero. An implementation that admits a flag unless it
            // reads exactly 1 fails this row.
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseGlitter", float.NaN);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
        }

        [TestCase(1f)]
        [TestCase(0.5f)]
        [TestCase(-0.5f)]
        [TestCase(float.NaN)]
        public void LilToonDissolveModeRefuses(float mode)
        {
            var fixture = NewLilToonFixture();
            var dissolve = fixture.Material.GetVector("_DissolveParams");
            dissolve.x = mode;
            fixture.Material.SetVector("_DissolveParams", dissolve);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(outcome.Detail, Does.Contain("_DissolveParams.x"));
        }

        [Test]
        public void LilToonDissolveModeBelowHalfIsAdmitted()
        {
            var fixture = NewLilToonFixture();
            var dissolve = fixture.Material.GetVector("_DissolveParams");
            dissolve.x = 0.49f;
            fixture.Material.SetVector("_DissolveParams", dissolve);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
        }

        private static IEnumerable<string> UdimDiscardRowFlags()
        {
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    yield return "_UDIMDiscardRow" + row + "_" + column;
                }
            }
        }

        [Test]
        public void LilToonUdimDiscardFlagOnUv0Refuses(
            [ValueSource(nameof(UdimDiscardRowFlags))] string flag)
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UDIMDiscardUV", 0f);
            fixture.Material.SetFloat(flag, 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
        }

        [Test]
        public void LilToonUdimDiscardOnUv1IsAdmitted()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UDIMDiscardUV", 1f);
            fixture.Material.SetFloat("_UDIMDiscardRow0_0", 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
        }

        private static IEnumerable<string> IdMaskFlags()
        {
            for (var index = 1; index <= 8; index++)
            {
                yield return "_IDMask" + index;
                yield return "_IDMaskPrior" + index;
            }
        }

        [Test]
        public void LilToonIdMaskFlagFromUv0Refuses(
            [ValueSource(nameof(IdMaskFlags))] string flag)
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_IDMaskFrom", 0f);
            fixture.Material.SetFloat(flag, 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
        }

        [Test]
        public void LilToonFractionalIdMaskSourceRefuses()
        {
            // The shader reads the source selector as an unsigned
            // integer. A value of one half can convert to zero and read
            // UV0, so only a selector of at least one is safe.
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_IDMaskFrom", 0.5f);
            fixture.Material.SetFloat("_IDMask1", 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
        }

        [Test]
        public void LilToonIdMaskFromUv1IsAdmitted()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_IDMaskFrom", 1f);
            fixture.Material.SetFloat("_IDMask1", 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertAdmitted(outcome);
        }

        [Test]
        public void LilToonNonIdentityScaleRefuses()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetTextureScale(
                "_MainTex", new Vector2(2f, 1f));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.NonIdentityTransform);
        }

        [Test]
        public void LilToonScrollRotateRefuses()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetVector(
                "_MainTex_ScrollRotate", new Vector4(0f, 0f, 0.25f, 0f));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.NonIdentityTransform);
        }

        // --- Falsifier 9: the second emission layer is on, so its
        // texture is live and no route lists it. A dead texture must
        // admit, but a live unlisted texture must still refuse. ---
        [Test]
        public void LiveUnlistedTextureStillRefuses()
        {
            var fixture = NewLilToonFixture();
            // The second emission layer has no companion route.
            fixture.Material.SetFloat("_UseEmission2nd", 1f);
            fixture.Material.SetTexture("_Emission2ndMap", fixture.MainTexture);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.UnlistedTexture);
            Assert.That(outcome.Detail, Does.Contain("'_Emission2ndMap'"));
        }

        [Test]
        public void LilToonAnimatedConditionPropertyRefuses()
        {
            var fixture = NewLilToonFixture();
            var clip = Track(new AnimationClip { name = "Glitter" });
            AnimateScalar(clip, "Body", "material._UseGlitter");
            var graph = GraphOf(fixture.Root, clip);

            var outcome = SingleOutcome(CaptureUses(fixture.Root, graph));

            AssertRefused(outcome, AtlasUseRefusal.AnimatedRouteProperty);
        }

        [Test]
        public void LilToonPropertyBlockOnConditionRefuses()
        {
            var fixture = NewLilToonFixture();
            var block = new MaterialPropertyBlock();
            block.SetFloat("_ShiftBackfaceUV", 0f);
            fixture.Renderer.SetPropertyBlock(block);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(
                outcome, AtlasUseRefusal.PropertyBlockOnRouteProperty);
        }

        [Test]
        public void LilToonMultiRefusesWithoutARouteTable()
        {
            var fixture = NewLilToonFixture(
                LoadLilToonPackageShader("ltsmulti.shader"));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.NoRouteTable);
        }

        [Test]
        public void LilToonFurShaderRefuses()
        {
            var fixture = NewLilToonFixture(
                LoadLilToonPackageShader("lts_fur.shader"));

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.UnattestedShader);
        }

        // --- lilToon companion routes -------------------------------------

        private static Texture2D ImportRouteTexture(
            string name,
            int size,
            Action<TextureImporter> configure = null)
        {
            return ImportMainTexture(name, size, size, importer =>
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                configure?.Invoke(importer);
            });
        }

        private static AtlasUseOutcome CaptureLilToon(AtlasFixture fixture)
        {
            return SingleOutcome(CaptureUses(fixture.Root));
        }

        [Test]
        public void LilToonBaseMapCopiesAdmitAndLeaveTheKey()
        {
            var plain = NewLilToonFixture();
            var copied = NewLilToonFixture();
            var other = ImportRouteTexture("base map copy", 64);
            copied.Material.SetTexture("_BaseMap", other);
            copied.Material.SetTexture("_BaseColorMap", other);

            var plainOutcome = CaptureLilToon(plain);
            var copiedOutcome = CaptureLilToon(copied);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(copiedOutcome);
            Assert.That(
                copiedOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "a texture no shader code samples must not split groups");
            Assert.That(
                copiedOutcome.Use.PageAliasProperties,
                Is.EqualTo(new[] { "_BaseColorMap", "_BaseMap" }));
            Assert.That(copiedOutcome.Use.Companions, Is.Empty);
        }

        [Test]
        public void LilToonMatCapTexturesAdmitAsNeutral()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            first.Material.SetFloat("_UseMatCap", 1f);
            second.Material.SetFloat("_UseMatCap", 1f);
            first.Material.SetTexture(
                "_MatCapTex", ImportRouteTexture("matcap first", 64));
            second.Material.SetTexture(
                "_MatCapTex", ImportRouteTexture("matcap second", 64));

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(firstOutcome.Use.Companions, Is.Empty);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "a neutral texture stays bound, so it must split groups");
        }

        // --- Falsifier 11: the bump route is inert, but a VRChat
        // fallback shader still reads the texture under the name
        // _BumpMap. An implementation that drops the texture from
        // the key would let one page carry the texture of the first
        // member. ---
        [Test]
        public void InertFallbackRouteTextureStillChangesTheKey()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            first.Material.SetFloat("_UseBumpMap", 0f);
            second.Material.SetFloat("_UseBumpMap", 0f);
            first.Material.SetTexture(
                "_BumpMap", ImportRouteTexture("inert bump first", 64));
            second.Material.SetTexture(
                "_BumpMap", ImportRouteTexture("inert bump second", 64));

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(firstOutcome.Use.Companions, Is.Empty);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "the fallback shader shows the bump map, so the texture " +
                "must split groups even behind an off switch");
        }

        [Test]
        public void BumpMapAtHalfSizeBecomesACompanion()
        {
            var fixture = NewLilToonFixture();
            var bump = ImportRouteTexture("half bump", 32);
            fixture.Material.SetFloat("_UseBumpMap", 1f);
            fixture.Material.SetTexture("_BumpMap", bump);

            var outcome = CaptureLilToon(fixture);

            AssertAdmitted(outcome);
            Assert.That(outcome.Use.Companions, Has.Count.EqualTo(1));
            var companion = outcome.Use.Companions[0];
            Assert.That(companion.Property, Is.EqualTo("_BumpMap"));
            Assert.That(
                companion.SourceTextureInstanceId,
                Is.EqualTo(bump.GetInstanceID()));
            Assert.That(companion.ScaleShift, Is.EqualTo(1));
            Assert.That(
                companion.Sampler, Is.EqualTo(outcome.Use.Sampler),
                "the bump map reads with the member sampler");
        }

        [TestCase(48)]
        [TestCase(128)]
        public void CompanionSizeOffThePowerOfTwoLadderRefuses(int size)
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseBumpMap", 1f);
            fixture.Material.SetTexture(
                "_BumpMap", ImportRouteTexture("bump " + size, size));

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.CompanionSizeMismatch);
            Assert.That(outcome.Detail, Does.StartWith(
                "This applies to the route texture '_BumpMap'."));
        }

        [Test]
        public void CompanionTilingRefuses()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseBumpMap", 1f);
            fixture.Material.SetTexture(
                "_BumpMap", ImportRouteTexture("tiled bump", 64));
            fixture.Material.SetTextureScale("_BumpMap", new Vector2(2f, 2f));

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.NonIdentityTransform);
            Assert.That(outcome.Detail, Does.StartWith(
                "This applies to the route texture '_BumpMap'."));
        }

        [Test]
        public void EmissionOnUv1StaysKeyedAndAdmits()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            foreach (var fixture in new[] { first, second })
            {
                fixture.Material.SetFloat("_UseEmission", 1f);
                fixture.Material.SetFloat("_EmissionMap_UVMode", 1f);
            }

            first.Material.SetTexture(
                "_EmissionMap", ImportRouteTexture("uv1 emission first", 64));
            second.Material.SetTexture(
                "_EmissionMap", ImportRouteTexture("uv1 emission second", 64));

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            Assert.That(firstOutcome.Use.Companions, Is.Empty);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "a texture on another UV channel stays bound, so it must" +
                " split groups");
        }

        [Test]
        public void ShadowBorderMaskLodRefuses()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseShadow", 1f);
            fixture.Material.SetTexture(
                "_ShadowBorderMask", ImportRouteTexture("border mask", 64));
            fixture.Material.SetFloat("_ShadowBorderMaskLOD", 0.5f);

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(outcome.Detail, Does.Contain("_ShadowBorderMaskLOD"));
        }

        [Test]
        public void ShadowBorderMaskReadsWithTheInlineSampler()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseShadow", 1f);
            fixture.Material.SetTexture(
                "_ShadowBorderMask", ImportRouteTexture("inline mask", 16));

            var outcome = CaptureLilToon(fixture);

            AssertAdmitted(outcome);
            var companion = outcome.Use.Companions.Single();
            Assert.That(companion.ScaleShift, Is.EqualTo(2));
            Assert.That(
                companion.Sampler.Filter, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(
                companion.Sampler.WrapU,
                Is.EqualTo(UnityEngine.TextureWrapMode.Repeat));
            Assert.That(companion.Sampler.AnisoLevel, Is.Zero);
        }

        [Test]
        public void OutlineTextureEqualToMainIsAnAlias()
        {
            var fixture = NewLilToonFixture(LoadInstalledLilToonShader(
                LilToonSourceAttestation.OutlineShaderGuid));
            fixture.Material.SetTexture("_OutlineTex", fixture.MainTexture);

            var outcome = CaptureLilToon(fixture);

            AssertAdmitted(outcome);
            Assert.That(outcome.Use.Companions, Is.Empty);
            Assert.That(
                outcome.Use.PageAliasProperties,
                Is.EqualTo(new[] { "_BaseColorMap", "_BaseMap", "_OutlineTex" }));
        }

        [Test]
        public void OutlineSamplerMismatchRefuses()
        {
            var fixture = NewLilToonFixture(LoadInstalledLilToonShader(
                LilToonSourceAttestation.OutlineShaderGuid));
            fixture.Material.SetFloat("_AlphaMaskMode", 1f);
            fixture.Material.SetTexture(
                "_AlphaMask", ImportRouteTexture("outline alpha mask", 64));
            fixture.Material.SetTexture(
                "_OutlineTex",
                ImportRouteTexture(
                    "clamped outline",
                    64,
                    importer => importer.wrapMode =
                        UnityEngine.TextureWrapMode.Clamp));

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.RouteSamplerMismatch);
        }

        [Test]
        public void AnimatedGateRefusesOnlyWithAnAssignedCompanion(
            [Values(true, false)] bool assigned)
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseBumpMap", 1f);
            if (assigned)
            {
                fixture.Material.SetTexture(
                    "_BumpMap", ImportRouteTexture("animated gate bump", 64));
            }

            var clip = Track(new AnimationClip { name = "BumpGate" });
            AnimateScalar(clip, "Body", "material._UseBumpMap");
            var graph = GraphOf(fixture.Root, clip);

            var outcome = SingleOutcome(CaptureUses(fixture.Root, graph));

            if (assigned)
            {
                AssertRefused(outcome, AtlasUseRefusal.AnimatedRouteProperty);
            }
            else
            {
                AssertAdmitted(outcome);
            }
        }

        [Test]
        public void Main2ndOnUv0BecomesACompanion()
        {
            var fixture = NewLilToonFixture();
            var layer = ImportRouteTexture("main2nd layer", 64);
            fixture.Material.SetFloat("_UseMain2ndTex", 1f);
            fixture.Material.SetTexture("_Main2ndTex", layer);

            var outcome = CaptureLilToon(fixture);

            AssertAdmitted(outcome);
            var companion = outcome.Use.Companions.Single();
            Assert.That(companion.Property, Is.EqualTo("_Main2ndTex"));
            Assert.That(
                companion.SourceTextureInstanceId,
                Is.EqualTo(layer.GetInstanceID()));
        }

        [Test]
        public void Main2ndDecalRefuses()
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseMain2ndTex", 1f);
            fixture.Material.SetFloat("_Main2ndTexIsDecal", 1f);

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(outcome.Detail, Does.Contain("_Main2ndTexIsDecal"));
        }

        [Test]
        public void Main2ndAnimationFrameZeroSpeedRefuses()
        {
            // A w of 0 selects frame z, and frame 1 shifts V by 1.
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseMain2ndTex", 1f);
            fixture.Material.SetVector(
                "_Main2ndTexDecalAnimation", new Vector4(1f, 1f, 1f, 0f));

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(
                outcome.Detail, Does.Contain("_Main2ndTexDecalAnimation"));
        }

        [Test]
        public void Main2ndDissolveRefusesOnEveryUvMode(
            [Values(0f, 1f)] float uvMode)
        {
            var fixture = NewLilToonFixture();
            fixture.Material.SetFloat("_UseMain2ndTex", 1f);
            fixture.Material.SetFloat("_Main2ndTex_UVMode", uvMode);
            var dissolve = fixture.Material.GetVector("_Main2ndDissolveParams");
            dissolve.x = 1f;
            fixture.Material.SetVector("_Main2ndDissolveParams", dissolve);

            var outcome = CaptureLilToon(fixture);

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
            Assert.That(
                outcome.Detail, Does.Contain("_Main2ndDissolveParams.x"));
        }

        [Test]
        public void Bc5MainTextureIsAdmitted()
        {
            var texture = ImportMainTexture("bc5 main", 64, 64, importer =>
            {
                importer.sRGBTexture = false;
                importer.SetPlatformTextureSettings(
                    new TextureImporterPlatformSettings
                    {
                        name = "Standalone",
                        overridden = true,
                        maxTextureSize = 2048,
                        format = TextureImporterFormat.BC5,
                    });
            });
            Assert.That(
                texture.format, Is.EqualTo(TextureFormat.BC5),
                "fixture precondition: the import must land on BC5");
            var root = Track(new GameObject("bc5 root"));
            AddSkinnedRenderer(
                root,
                "Body",
                Track(NewQuadMesh()),
                new[] { NewAdmittedMaterial(texture) });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertAdmitted(outcome);
            Assert.That(
                outcome.Use.SourceFormat,
                Is.EqualTo(UnityEngine.Experimental.Rendering.GraphicsFormat
                    .RG_BC5_UNorm));
        }

        // --- the reviewed live value model (lilToon 2.4.1) ----------

        [Test]
        public void DeadFamilyValueLeavesTheKey()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            first.Material.SetFloat("_UseRim", 0f);
            second.Material.SetFloat("_UseRim", 0f);
            second.Material.SetFloat("_RimBorder", 0.9f);

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.EqualTo(firstOutcome.Use.MaterialKey),
                "a rim color behind an off switch must not split groups");
        }

        [Test]
        public void DeadFamilyTextureAdmitsAndLeavesTheKey()
        {
            var plain = NewLilToonFixture();
            var glitter = NewLilToonFixture();
            glitter.Material.SetFloat("_UseGlitter", 0f);
            glitter.Material.SetTexture(
                "_GlitterShapeTex", ImportRouteTexture("dead glitter", 64));

            var plainOutcome = CaptureLilToon(plain);
            var glitterOutcome = CaptureLilToon(glitter);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(glitterOutcome);
            Assert.That(glitterOutcome.Use.Companions, Is.Empty);
            Assert.That(
                glitterOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "a texture behind an off switch must neither refuse the " +
                "slot nor split groups");
        }

        // --- Falsifier 3: the same rim color difference with the
        // switch on changes rendering, so it must change the key. ---
        [Test]
        public void LiveFamilyValueChangesTheKey()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            first.Material.SetFloat("_UseRim", 1f);
            second.Material.SetFloat("_UseRim", 1f);
            second.Material.SetFloat("_RimBorder", 0.9f);

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "a rim color behind an on switch must split groups");
        }

        // --- Falsifier 4: an animated switch may hold any value at
        // runtime, so the family must stay live. The falsifier
        // assigns no rim texture, because a rim texture would make
        // the switch a route condition and refuse the slot. ---
        [Test]
        public void AnimatedSwitchKeepsTheFamilyLive()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            foreach (var fixture in new[] { first, second })
            {
                fixture.Material.SetFloat("_UseRim", 0f);
            }

            second.Material.SetFloat("_RimBorder", 0.9f);
            var clip = Track(new AnimationClip { name = "RimSwitch" });
            AnimateScalar(clip, "Body", "material._UseRim");
            var graph = GraphOf(first.Root, clip);
            var secondGraph = GraphOf(second.Root, clip);

            var firstOutcome =
                SingleOutcome(CaptureUses(first.Root, graph));
            var secondOutcome =
                SingleOutcome(CaptureUses(second.Root, secondGraph));

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "an animated switch keeps the rim color live");
        }

        [Test]
        public void PropertyBlockOnASwitchKeepsTheFamilyLive()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            foreach (var fixture in new[] { first, second })
            {
                fixture.Material.SetFloat("_UseRim", 0f);
                var block = new MaterialPropertyBlock();
                block.SetFloat("_UseRim", 0f);
                fixture.Renderer.SetPropertyBlock(block);
            }

            second.Material.SetFloat("_RimBorder", 0.9f);

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "a property block on the switch keeps the rim color live");
        }

        [Test]
        public void StaleUndeclaredEntryLeavesTheKey()
        {
            var plain = NewLilToonFixture();
            var stale = NewLilToonFixture();
            WriteStaleFloat(stale.Material, "_AmuseTestStale", 1f);

            var plainOutcome = CaptureLilToon(plain);
            var staleOutcome = CaptureLilToon(stale);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(staleOutcome);
            Assert.That(
                staleOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "a saved float that no shader declares cannot change " +
                "rendering");
        }

        // --- Falsifier 5: a stale float under a declared uniform
        // name may reach the shader, so it must change the key. ---
        [Test]
        public void StaleEntryWithADeclaredUniformNameChangesTheKey()
        {
            const string name = "_MatCapMul";
            var plain = NewLilToonFixture();
            Assert.That(
                plain.Material.HasProperty(name), Is.False,
                "fixture precondition: the row must not declare the " +
                "name as a property");
            Assert.That(
                LilToonLiveValues.Model.DeclaredUniforms,
                Does.Contain(name),
                "fixture precondition: the model must declare the " +
                "uniform name");
            var stale = NewLilToonFixture();
            WriteStaleFloat(stale.Material, name, 1f);

            var plainOutcome = CaptureLilToon(plain);
            var staleOutcome = CaptureLilToon(stale);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(staleOutcome);
            Assert.That(
                staleOutcome.Use.MaterialKey,
                Is.Not.EqualTo(plainOutcome.Use.MaterialKey),
                "a declared uniform name stays live even when the row " +
                "has no such property");
        }

        // --- Falsifier 6: a VRChat fallback shader reads
        // _Glossiness, so the saved float must change the key. ---
        [Test]
        public void FallbackNameAlwaysCounts()
        {
            var plain = NewLilToonFixture();
            var stale = NewLilToonFixture();
            WriteStaleFloat(stale.Material, "_Glossiness", 0.75f);

            var plainOutcome = CaptureLilToon(plain);
            var staleOutcome = CaptureLilToon(stale);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(staleOutcome);
            Assert.That(
                staleOutcome.Use.MaterialKey,
                Is.Not.EqualTo(plainOutcome.Use.MaterialKey),
                "a fallback shader property name never counts as stale");
        }

        [Test]
        public void NullFallbackTextureEqualsAnAbsentOne()
        {
            var plain = NewLilToonFixture();
            var stale = NewLilToonFixture();
            AppendStaleTexEnv(
                stale.Material, "_DetailAlbedoMap", null,
                new Vector2(2f, 2f), Vector2.zero);

            var plainOutcome = CaptureLilToon(plain);
            var staleOutcome = CaptureLilToon(stale);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(staleOutcome);
            Assert.That(
                staleOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "a fallback texture entry with no texture is the same " +
                "state as no entry");
        }

        // --- Falsifier 7: a toon standard fallback tag makes VRChat
        // swap the shader, so every value must stay in the key. ---
        [Test]
        public void ToonStandardFallbackTagKeepsEveryValue()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            foreach (var fixture in new[] { first, second })
            {
                fixture.Material.SetFloat("_UseRim", 0f);
                fixture.Material.SetOverrideTag(
                    "VRCFallback", "toonstandard");
            }

            second.Material.SetFloat("_RimBorder", 0.9f);

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "the fallback shader reads the rim color, so the tag " +
                "keeps every value live");
        }

        [Test]
        public void NaNSwitchKeepsTheFamilyLive()
        {
            // The source compares a switch with not equal to zero,
            // and NaN is not zero. The family stays live.
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            first.Material.SetFloat("_UseRim", float.NaN);
            second.Material.SetFloat("_UseRim", float.NaN);
            second.Material.SetFloat("_RimBorder", 0.9f);

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "a NaN switch counts as on, so the rim border stays " +
                "live");
        }

        [Test]
        public void OutlineValuesChangeTheKeyOnlyOnOutlineRows()
        {
            var plain = NewLilToonFixture();
            var plainWide = NewLilToonFixture();
            plainWide.Material.SetFloat("_OutlineWidth", 0.9f);
            var outlineRow = NewLilToonFixture(
                LoadInstalledLilToonShader(
                    LilToonSourceAttestation.OutlineShaderGuid));
            var outlineRowWide = NewLilToonFixture(
                LoadInstalledLilToonShader(
                    LilToonSourceAttestation.OutlineShaderGuid));
            outlineRowWide.Material.SetFloat("_OutlineWidth", 0.9f);

            var plainOutcome = CaptureLilToon(plain);
            var plainWideOutcome = CaptureLilToon(plainWide);
            var outlineOutcome = CaptureLilToon(outlineRow);
            var outlineWideOutcome = CaptureLilToon(outlineRowWide);

            AssertAdmitted(plainOutcome);
            AssertAdmitted(plainWideOutcome);
            AssertAdmitted(outlineOutcome);
            AssertAdmitted(outlineWideOutcome);
            Assert.That(
                plainWideOutcome.Use.MaterialKey,
                Is.EqualTo(plainOutcome.Use.MaterialKey),
                "a row without an outline pass never reads the width");
            Assert.That(
                outlineWideOutcome.Use.MaterialKey,
                Is.Not.EqualTo(outlineOutcome.Use.MaterialKey),
                "an outline row reads the width in its vertex code");
        }

        // --- Falsifier 10: a fallback name stays live even inside a
        // dead family, because the fallback swap ignores the row. ---
        [Test]
        public void FallbackNameInADeadFamilyStillCounts()
        {
            var first = NewLilToonFixture();
            var second = NewLilToonFixture();
            first.Material.SetFloat("_UseReflection", 0f);
            second.Material.SetFloat("_UseReflection", 0f);
            second.Material.SetFloat("_Metallic", 0.8f);

            var firstOutcome = CaptureLilToon(first);
            var secondOutcome = CaptureLilToon(second);

            AssertAdmitted(firstOutcome);
            AssertAdmitted(secondOutcome);
            Assert.That(
                secondOutcome.Use.MaterialKey,
                Is.Not.EqualTo(firstOutcome.Use.MaterialKey),
                "a fallback shader reads _Metallic, so it stays live " +
                "inside the dead reflection family");
        }

        private static void AppendStaleTexEnv(
            Material material,
            string name,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            using var serialized = new SerializedObject(material);
            var texEnvs = serialized.FindProperty(SavedTexEnvsPath);
            var index = texEnvs.arraySize;
            texEnvs.arraySize = index + 1;
            var entry = texEnvs.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("first").stringValue = name;
            entry.FindPropertyRelative("second.m_Texture")
                .objectReferenceValue = texture;
            entry.FindPropertyRelative("second.m_Scale").vector2Value =
                scale;
            entry.FindPropertyRelative("second.m_Offset").vector2Value =
                offset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            using var verify = new SerializedObject(material);
            var verified = verify.FindProperty(SavedTexEnvsPath);
            Assert.That(
                SavedTexEnvIndexOf(verified, name),
                Is.GreaterThanOrEqualTo(0),
                "fixture guard: the stale texture entry must persist");
        }
    }
}
