using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
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
            Action<TextureImporter> configure = null)
        {
            var staging = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
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
        }

        [Test]
        public void SharpenedA2CWithCoverageRefuses()
        {
            var fixture = NewDefaultFixture();
            fixture.Material.SetFloat("_AlphaSharpenedA2C", 1f);
            fixture.Material.SetFloat("_AlphaToCoverage", 1f);

            var outcome = SingleOutcome(CaptureUses(fixture.Root));

            AssertRefused(outcome, AtlasUseRefusal.RequiredValueNotHeld);
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
                    importer => importer.wrapMode =
                        TextureWrapMode.Mirror));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(CaptureUses(root));

            AssertRefused(outcome, AtlasUseRefusal.UnsupportedWrap);
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
                    importer =>
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
        public void R8SourceRefuses()
        {
            var root = Track(new GameObject("r8 root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            var texture = NewNativeTextureAsset(
                "r8 main", new Texture2D(4, 4, TextureFormat.R8, true));
            material.SetTexture("_MainTex", texture);
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
        public void SourceWithFewerLevelsThanProtectedRefuses()
        {
            var root = Track(new GameObject("short mips root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                ImportMainTexture(
                    "short mips main",
                    importer => importer.mipmapEnabled = false));
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(
                CaptureUses(root, protectedMipLevelCount: 2));

            AssertRefused(outcome, AtlasUseRefusal.SourceMipChainTooShort);
        }

        [Test]
        public void CompressedSourceNotOnTheBlockGridRefuses()
        {
            var root = Track(new GameObject("off grid root"));
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            // A 68 by 64 DXT1 chain: level 1 is 34 texels wide, which is
            // not a multiple of the block size 4.
            var texture = NewNativeTextureAsset(
                "off grid main",
                new Texture2D(68, 64, TextureFormat.DXT1, true));
            material.SetTexture("_MainTex", texture);
            var mesh = Track(NewQuadMesh());
            AddSkinnedRenderer(root, "Body", mesh, new[] { material });

            var outcome = SingleOutcome(
                CaptureUses(root, protectedMipLevelCount: 2));

            AssertRefused(outcome, AtlasUseRefusal.SourceNotAligned);
        }

        // --- the material key ---------------------------------------------

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
    }
}
