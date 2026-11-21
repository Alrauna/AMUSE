using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;


[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Build.AtlasEndToEndTests
        .AtlasEndToEndPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The end to end atlas path. Every test drives one build through
    /// <see cref="AvatarProcessor.ProcessAvatar"/> on a dedicated
    /// platform whose plugin runs the production pass sequence in the
    /// production order: the structural graph check, the bindings
    /// capture, the barrier, alpha separation apply, the unlock window
    /// close, then the atlas pass. The barrier runs the production
    /// entry, so every fixture material is a real installed Poiyomi
    /// material. Each test runs inside an override of the NDMF
    /// temporary directory and cleans its generated assets through the
    /// asset database.
    /// </summary>
    public sealed class AtlasEndToEndTests
    {
        private const string TempFolder = "Assets/AmuseTests_AtlasEndToEnd";

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        private Func<BuildTarget> _originalTarget;

        private Func<AtlasBudgetLimits> _originalBudget;

        [SetUp]
        public void SetUp()
        {
            _originalTarget =
                AmusePlatformFinishPlugin.ActiveBuildTargetForTests;
            _originalBudget = AtlasBudgetLimits.Provider;
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                () => BuildTarget.StandaloneWindows64;
            _scope.EnsureTempFolder();
        }

        [TearDown]
        public void TearDown()
        {
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                _originalTarget;
            AtlasPass.PageCheck = AtlasPageCheck.Verify;
            AtlasPass.AcquireSource =
                AtlasPass.AcquireThroughStreamingEvidence;
            AtlasEndToEndPlugin.PreAtlasHook = null;
            AtlasBudgetLimits.Provider = _originalBudget;
            _scope.TearDown();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
                AssetDatabase.Refresh();
            }
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
        }

        private static HostLifecycleFacts SupportedFacts()
        {
            return new HostLifecycleFacts(
                "2022.3.22f1",
                "1.14.4",
                "3.10.4",
                "3.10.4",
                WellKnownPlatforms.VRChatAvatar30,
                AmuseBuildPath.NonPlayNdmfBuild,
                hasAssetSaver: true,
                hasAssetContainer: true,
                hasObjectRegistry: true,
                hasErrorReport: true);
        }

        // --- the dedicated platform ---------------------------------------

        internal const string EndToEndPlatformName =
            "com.alrauna.amuse.tests.atlas-end-to-end";

        internal sealed class AtlasEndToEndPlatform : INDMFPlatformProvider
        {
            internal static readonly AtlasEndToEndPlatform Instance =
                new AtlasEndToEndPlatform();

            public string QualifiedName => EndToEndPlatformName;

            public string DisplayName => "AMUSE atlas end to end";
        }

        /// <summary>
        /// Records facts between passes that the test can only observe
        /// while the build still holds them.
        /// </summary>
        public sealed class AtlasEndToEndProbe
        {
            internal Mesh MeshCloneBeforeAtlas;

            internal Material OpaqueCloneBeforeAtlas;

            internal IObjectRegistry Registry;

            internal Mesh AtlasMeshAfter;

            internal UnityEngine.Object AtlasMeshReferenceObject;

            internal UnityEngine.Object CurveReferencedObject;
        }


        /// <summary>
        /// The production pass sequence on the dedicated platform: the
        /// structural graph check, one animator scope around the
        /// bindings capture, the production barrier, and apply, then
        /// the extension free window close and atlas pass, each in the
        /// production order. A probe before the atlas pass records the
        /// alpha separation outputs the atlas path replaces. A probe
        /// after it records the live object registry.
        /// </summary>
        [RunsOnPlatforms(EndToEndPlatformName)]
        public sealed class AtlasEndToEndPlugin : Plugin<AtlasEndToEndPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.atlas-end-to-end-plugin";

            /// <summary>
            /// A hook the pre atlas probe runs after it recorded the
            /// alpha separation outputs. A test uses it to change the
            /// committed graph between the window close and the atlas
            /// pass. It stays null outside that test.
            /// </summary>
            internal static Action<GameObject,
                AmusePlatformFinishState, AtlasEndToEndProbe>
                PreAtlasHook;

            protected override void Configure()
            {
                var sequence = InPhase(BuildPhase.PlatformFinish);
                sequence.Run(
                    "AMUSE test structural graph check",
                    AmuseStructuralGraphCheck.Execute);
                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner =>
                    {
                        inner.Run(
                            "AMUSE test bindings capture",
                            AmuseAnimatorBindingsCapture.Execute);
                        inner.Run(
                            "AMUSE test barrier",
                            context => AmusePlatformFinishPass.Execute(
                                context,
                                SupportedFacts(),
                                consentPresenter: subjects => true));
                        inner.Run(
                            AlphaSeparationApply.PassName,
                            context => AlphaSeparationApply.Execute(
                                context));
                    });
                sequence.Run(
                    TransientUnlockWindowClose.PassName,
                    context => TransientUnlockWindowClose.Execute(
                        context));
                sequence.Run("AMUSE test pre atlas probe", ProbeBeforeAtlas);
                sequence.Run(AtlasPass.PassName, AtlasPass.Execute);
                sequence.Run("AMUSE test post atlas probe", ProbeAfterAtlas);
            }

            private static void ProbeBeforeAtlas(BuildContext context)
            {
                var probe = context.GetState<AtlasEndToEndProbe>();
                var state = context.GetState<AmusePlatformFinishState>();
                if (state.Separation == null)
                {
                    return;
                }

                probe.MeshCloneBeforeAtlas =
                    state.Separation.Renderers.Count > 0
                        ? state.Separation.Renderers[0].MeshClone
                        : null;
                probe.OpaqueCloneBeforeAtlas =
                    state.Separation.CreatedClones.FirstOrDefault(
                        clone => clone != null);
                PreAtlasHook?.Invoke(
                    context.AvatarRootObject, state, probe);
            }


            private static void ProbeAfterAtlas(BuildContext context)
            {
                var probe = context.GetState<AtlasEndToEndProbe>();
                probe.Registry = ObjectRegistry.ActiveRegistry;

                var renderer = context.AvatarRootObject
                    .GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault();
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    probe.AtlasMeshAfter = skinned.sharedMesh;
                }
                else if (renderer != null)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    probe.AtlasMeshAfter =
                        filter != null ? filter.sharedMesh : null;
                }

                if (probe.Registry != null && probe.AtlasMeshAfter != null)
                {
                    probe.AtlasMeshReferenceObject = probe.Registry
                        .GetReference(probe.AtlasMeshAfter, false)?.Object;
                }
            }

            /// <summary>
            /// The injected page check failure is the one expected
            /// exception of this suite. The base handler logs it,
            /// which the test runner treats as an unexpected error
            /// log, so the injected failure is skipped and every
            /// other exception keeps the base behavior.
            /// </summary>
            protected override void OnUnhandledException(
                Exception exception)
            {
                if (exception != null &&
                    exception.Message.Contains(
                        "AMUSE atlas test injected page check"))
                {
                    return;
                }

                base.OnUnhandledException(exception);
            }
        }

        // --- fixtures ------------------------------------------------------

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

        /// <summary>
        /// One 64 by 64 mipmapped RGBA32 source with a checker pattern
        /// of 16 texel blocks. Bilinear filtering and no anisotropy
        /// keep the render comparison stable.
        /// </summary>
        private Texture2D ImportPatternTexture(
            string name,
            Color32 even,
            Color32 odd,
            FilterMode filter = FilterMode.Bilinear)
        {
            var pixels = new Color32[64 * 64];
            for (var y = 0; y < 64; y++)
            {
                for (var x = 0; x < 64; x++)
                {
                    pixels[y * 64 + x] =
                        ((x / 16 + y / 16) % 2 == 0) ? even : odd;
                }
            }

            // Not tracked: an imported asset leaves through the folder
            // deletion, never through DestroyImmediate.
            return TestTextureImport.WritePng(
                TempFolder + "/" + name + ".png",
                64,
                64,
                pixels,
                importer =>
                {
                    importer.mipmapEnabled = true;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.filterMode = filter;
                    importer.anisoLevel = 0;
                });
        }

        private Material NewAdmittedMaterial(
            string name,
            Color32 even,
            Color32 odd,
            FilterMode filter = FilterMode.Bilinear)
        {
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            material.SetTexture(
                "_MainTex",
                ImportPatternTexture(name, even, odd, filter));
            return material;
        }

        private static Mesh NewQuadMesh()
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
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                },
            };
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            return mesh;
        }

        /// <summary>
        /// An opted in avatar root with alpha separation off and
        /// atlasing on, so the only moving part is the atlas path.
        /// </summary>
        private GameObject NewAtlasOnlyRoot()
        {
            var root = new GameObject("AMUSE atlas end to end");
            var component =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(component);
            var alphaSwitch =
                serialized.FindProperty("_alphaSeparatorEnabled");
            Assert.That(alphaSwitch, Is.Not.Null,
                "AmuseAvatarOptimizer._alphaSeparatorEnabled field pin");
            alphaSwitch.boolValue = false;
            serialized.ApplyModifiedProperties();
            FixtureProofScope.PinAllSizes(root);
            return root;
        }

        /// <summary>
        /// The avatar root with both features on, so alpha separation
        /// runs before the atlas pass exactly as production orders it.
        /// </summary>
        private GameObject NewBothFeaturesRoot(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureProofScope.PinAllSizes(root);
            FixtureProofScope.AcceptDesktopAnisotropyDifferences(root);
            return root;
        }

        /// <summary>
        /// A real installed Poiyomi material in the convertible Fade
        /// render state whose alpha proof reads the texture.
        /// </summary>
        private Material NewRealPoiyomiTextureAlphaMaterial(
            Texture2D mainTexture)
        {
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            PoiyomiFixtureTestBase.ApplyConvertibleRenderState(material);
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", mainTexture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            return material;
        }

        /// <summary>
        /// A real installed Poiyomi material whose tint alpha is zero,
        /// so the whole slot stays transparent and alpha separation
        /// keeps it original. It carries no member texture, so the
        /// atlas capture refuses its use.
        /// </summary>
        private Material NewRealPoiyomiRetainedMaterial()
        {
            var material = Track(new Material(LoadInstalledPoiyomiShader()));
            PoiyomiFixtureTestBase.ApplyConvertibleRenderState(material);
            material.SetFloat("_AlphaForceOpaque", 0f);
            var tint = material.GetColor("_Color");
            tint.a = 0f;
            material.SetColor("_Color", tint);
            material.SetTexture("_MainTex", null);
            return material;
        }

        private static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private BuildContext Build(GameObject root)
        {
            return AvatarProcessor.ProcessAvatar(
                root, AtlasEndToEndPlatform.Instance);
        }

        // --- the render helper ---------------------------------------------

        /// <summary>
        /// Renders one quad through an orthographic camera into a 128
        /// by 128 target. The quad spans the unit square, so the camera
        /// sits at its center and maps one texel row per rendered row.
        /// </summary>
        private static Color32[] RenderQuad(MeshRenderer renderer)
        {
            var cameraObject = new GameObject(
                "AMUSE atlas render camera");
            RenderTexture target = null;
            Texture2D read = null;
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 0.5f;
                camera.transform.position = new Vector3(0.5f, 0.5f, -5f);
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.allowMSAA = false;
                target = new RenderTexture(
                    128, 128, 24, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                };
                camera.targetTexture = target;
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                try
                {
                    read = new Texture2D(
                        128, 128, TextureFormat.RGBA32, false);
                    read.ReadPixels(new Rect(0f, 0f, 128f, 128f), 0, 0);
                    read.Apply(false, false);
                    return read.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                }
            }
            finally
            {
                if (read != null)
                {
                    UnityEngine.Object.DestroyImmediate(read);
                }

                if (target != null)
                {
                    cameraObject.GetComponent<Camera>()
                        .targetTexture = null;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }

                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        /// <summary>
        /// The largest per channel difference of two renders, in units
        /// of one of 255.
        /// </summary>
        private static int LargestChannelDifference(
            Color32[] before,
            Color32[] after)
        {
            var largest = 0;
            for (var index = 0; index < before.Length; index++)
            {
                largest = Math.Max(largest,
                    Math.Abs(before[index].r - after[index].r));
                largest = Math.Max(largest,
                    Math.Abs(before[index].g - after[index].g));
                largest = Math.Max(largest,
                    Math.Abs(before[index].b - after[index].b));
                largest = Math.Max(largest,
                    Math.Abs(before[index].a - after[index].a));
            }

            return largest;
        }

        private static void RenderOnly(MeshRenderer visible)
        {
            foreach (var renderer in visible.transform.parent
                .GetComponentsInChildren<MeshRenderer>())
            {
                renderer.enabled = renderer == visible;
            }
        }

        private static void RenderAll(MeshRenderer visible)
        {
            foreach (var renderer in visible.transform.parent
                .GetComponentsInChildren<MeshRenderer>())
            {
                renderer.enabled = true;
            }
        }

        private static Color32[] RenderEachQuad(
            params MeshRenderer[] renderers)
        {
            var results = new List<Color32[]>();
            foreach (var renderer in renderers)
            {
                RenderOnly(renderer);
                results.Add(RenderQuad(renderer));
            }

            RenderAll(renderers[0]);
            return results.SelectMany(colors => colors).ToArray();
        }

        // --- the tests -----------------------------------------------------

        /// <summary>
        /// Two compatible quads share one page and one page material,
        /// and each renders as before within one of 255 per channel.
        /// </summary>
        [Test]
        public void TwoCompatibleMaterialsShareOnePageWithCorrectRendering()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var firstMaterial = NewAdmittedMaterial(
                    "shared first",
                    new Color32(220, 40, 40, 255),
                    new Color32(40, 40, 220, 255));
                var secondMaterial = NewAdmittedMaterial(
                    "shared second",
                    new Color32(40, 200, 60, 255),
                    new Color32(220, 200, 40, 255));
                var firstTexture =
                    (Texture2D)firstMaterial.GetTexture("_MainTex");
                var secondTexture =
                    (Texture2D)secondMaterial.GetTexture("_MainTex");
                var firstMesh = Track(NewQuadMesh());
                var secondMesh = Track(NewQuadMesh());
                var firstChild = new GameObject("first");
                firstChild.transform.SetParent(root.transform, false);
                var firstRenderer =
                    firstChild.AddComponent<MeshRenderer>();
                firstChild.AddComponent<MeshFilter>().sharedMesh =
                    firstMesh;
                firstRenderer.sharedMaterial = firstMaterial;
                var secondChild = new GameObject("second");
                secondChild.transform.SetParent(root.transform, false);
                var secondRenderer =
                    secondChild.AddComponent<MeshRenderer>();
                secondChild.AddComponent<MeshFilter>().sharedMesh =
                    secondMesh;
                secondRenderer.sharedMaterial = secondMaterial;

                var before = RenderEachQuad(firstRenderer, secondRenderer);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasPageCount, Is.EqualTo(1),
                    "two compatible uses of one format must share one" +
                    " page");
                Assert.That(state.AtlasAdmittedUseCount, Is.EqualTo(2),
                    "both uses must move onto the page");
                Assert.That(state.AtlasUseRefusals, Is.Empty,
                    "an admitted fixture refuses no use");

                var pageMaterial = firstRenderer.sharedMaterial;
                Assert.That(secondRenderer.sharedMaterial,
                    Is.SameAs(pageMaterial),
                    "both slots must carry the one shared page material");
                Assert.That(pageMaterial, Is.Not.SameAs(firstMaterial));
                Assert.That(pageMaterial, Is.Not.SameAs(secondMaterial));

                var page = (Texture2D)pageMaterial.GetTexture("_MainTex");
                Assert.That(page, Is.Not.Null,
                    "the page material must carry the page texture");
                Assert.That(page.isReadable, Is.False,
                    "the shipped page must not stay readable");
                Assert.That(pageMaterial.name,
                    Is.EqualTo("AMUSE atlas material 0-0"),
                    "the page material must carry its deterministic" +
                    " plan name");
                Assert.That(page.name,
                    Is.EqualTo("AMUSE atlas page 0-0"),
                    "the page texture must carry its deterministic" +
                    " plan name");

                Assert.That(firstMaterial.GetTexture("_MainTex"),
                    Is.SameAs(firstTexture),
                    "the first source must stay unchanged");
                Assert.That(secondMaterial.GetTexture("_MainTex"),
                    Is.SameAs(secondTexture),
                    "the second source must stay unchanged");
                Assert.That(firstRenderer.sharedMaterials,
                    Has.Length.EqualTo(1),
                    "the slot count must stay unchanged");
                Assert.That(secondRenderer.sharedMaterials,
                    Has.Length.EqualTo(1),
                    "the slot count must stay unchanged");

                var firstOutput = SharedMeshOf(firstRenderer);
                var secondOutput = SharedMeshOf(secondRenderer);
                Assert.That(firstOutput, Is.Not.SameAs(firstMesh),
                    "the pass must write a new mesh");
                Assert.That(firstOutput, Is.Not.SameAs(secondOutput),
                    "each renderer must get its own output mesh");
                var route = firstOutput.GetVertexAttributes()
                    .Single(attribute =>
                        attribute.attribute == VertexAttribute.TexCoord0);
                Assert.That(route.format,
                    Is.EqualTo(VertexAttributeFormat.Float32),
                    "the remapped route channel must be Float32");

                var after = RenderEachQuad(firstRenderer, secondRenderer);
                Assert.That(
                    LargestChannelDifference(before, after),
                    Is.LessThanOrEqualTo(1),
                    "the atlas page must render as the source did");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A fresh material on the GUID-loaded installed lilToon shader
        /// with a pattern source on the member texture.
        /// </summary>
        private Material NewAdmittedLilToonMaterial(
            string name,
            Color32 even,
            Color32 odd)
        {
            var path = AssetDatabase.GUIDToAssetPath(
                LilToonSourceAttestation.SupportedShaderGuid);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed lilToon shader must" +
                " load by its pinned GUID");
            var material = Track(new Material(shader));
            material.SetTexture(
                "_MainTex", ImportPatternTexture(name, even, odd));
            return material;
        }

        /// <summary>
        /// The lilToon row takes the same path as the Poiyomi row. Two
        /// compatible lilToon quads share one page and one page
        /// material on the lilToon shader, and each renders as before
        /// within one of 255 per channel.
        /// </summary>
        [Test]
        public void TwoLilToonMaterialsShareOnePageWithCorrectRendering()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var firstMaterial = NewAdmittedLilToonMaterial(
                    "liltoon first",
                    new Color32(220, 40, 40, 255),
                    new Color32(40, 40, 220, 255));
                var secondMaterial = NewAdmittedLilToonMaterial(
                    "liltoon second",
                    new Color32(40, 200, 60, 255),
                    new Color32(220, 200, 40, 255));
                var firstTexture =
                    (Texture2D)firstMaterial.GetTexture("_MainTex");
                var secondTexture =
                    (Texture2D)secondMaterial.GetTexture("_MainTex");
                var firstRenderer = NewQuadRenderer(
                    root, "liltoon first", Track(NewQuadMesh()),
                    firstMaterial);
                var secondRenderer = NewQuadRenderer(
                    root, "liltoon second", Track(NewQuadMesh()),
                    secondMaterial);

                var before = RenderEachQuad(firstRenderer, secondRenderer);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasUseRefusals, Is.Empty,
                    "an admitted lilToon fixture refuses no use");
                Assert.That(state.AtlasPageCount, Is.EqualTo(1),
                    "two compatible lilToon uses must share one page");
                Assert.That(state.AtlasAdmittedUseCount, Is.EqualTo(2),
                    "both uses must move onto the page");

                var pageMaterial = firstRenderer.sharedMaterial;
                Assert.That(secondRenderer.sharedMaterial,
                    Is.SameAs(pageMaterial),
                    "both slots must carry the one shared page material");
                Assert.That(pageMaterial.shader,
                    Is.SameAs(firstMaterial.shader),
                    "the page material must stay on the lilToon shader");
                Assert.That(pageMaterial.GetTexture("_MainTex"),
                    Is.Not.SameAs(firstTexture),
                    "the page material must carry the page texture");
                Assert.That(firstMaterial.GetTexture("_MainTex"),
                    Is.SameAs(firstTexture),
                    "the first source must stay unchanged");
                Assert.That(secondMaterial.GetTexture("_MainTex"),
                    Is.SameAs(secondTexture),
                    "the second source must stay unchanged");

                var route = SharedMeshOf(firstRenderer).GetVertexAttributes()
                    .Single(attribute =>
                        attribute.attribute == VertexAttribute.TexCoord0);
                Assert.That(route.format,
                    Is.EqualTo(VertexAttributeFormat.Float32),
                    "the remapped route channel must be Float32");

                var after = RenderEachQuad(firstRenderer, secondRenderer);
                Assert.That(
                    LargestChannelDifference(before, after),
                    Is.LessThanOrEqualTo(1),
                    "the lilToon atlas page must render as the source did");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// A manual bake wraps the whole build in an asset editing
        /// batch. The batch must not change the atlas outcome: both
        /// admitted uses still move onto the first page, both slots
        /// carry the page material, and the streaming scratch folder
        /// is gone. The manual bake root keeps its generated output,
        /// so the test removes it through the asset database.
        /// </summary>
        [Test]
        public void ManualBakeAtlasesLikeAnUnbatchedBuild()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            GameObject baked = null;
            try
            {
                var firstMaterial = NewAdmittedMaterial(
                    "manual bake first",
                    new Color32(220, 40, 40, 255),
                    new Color32(40, 40, 220, 255));
                var secondMaterial = NewAdmittedMaterial(
                    "manual bake second",
                    new Color32(40, 200, 60, 255),
                    new Color32(220, 200, 40, 255));
                var firstMesh = Track(NewQuadMesh());
                var secondMesh = Track(NewQuadMesh());
                var firstChild = new GameObject("first");
                firstChild.transform.SetParent(root.transform, false);
                var firstRenderer =
                    firstChild.AddComponent<MeshRenderer>();
                firstChild.AddComponent<MeshFilter>().sharedMesh =
                    firstMesh;
                firstRenderer.sharedMaterial = firstMaterial;
                var secondChild = new GameObject("second");
                secondChild.transform.SetParent(root.transform, false);
                var secondRenderer =
                    secondChild.AddComponent<MeshRenderer>();
                secondChild.AddComponent<MeshFilter>().sharedMesh =
                    secondMesh;
                secondRenderer.sharedMaterial = secondMaterial;

                baked = AvatarProcessor.ManualProcessAvatar(
                    root, AtlasEndToEndPlatform.Instance);

                Assert.That(
                    baked, Is.Not.Null,
                    "the manual bake must return the processed clone");
                var renderers = baked
                    .GetComponentsInChildren<MeshRenderer>();
                Assert.That(
                    renderers.Length, Is.EqualTo(2),
                    "the baked clone must keep both slots");
                var pageMaterial = renderers[0].sharedMaterial;
                Assert.That(
                    renderers[1].sharedMaterial,
                    Is.SameAs(pageMaterial),
                    "both slots must carry the one shared page material");
                Assert.That(
                    pageMaterial.name,
                    Is.EqualTo("AMUSE atlas material 0-0"),
                    "the first page must hold both uses, so the bake " +
                    "made one page");
                Assert.That(
                    pageMaterial, Is.Not.SameAs(firstMaterial));
                var page = (Texture2D)pageMaterial.GetTexture("_MainTex");
                Assert.That(
                    page, Is.Not.Null,
                    "the page material must carry the page texture");
                Assert.That(
                    page.name, Is.EqualTo("AMUSE atlas page 0-0"));

                Assert.That(
                    AssetDatabase.IsValidFolder(
                        "Assets/Amuse.StreamingReadback"),
                    Is.False,
                    "a manual bake must not leave the streaming " +
                    "scratch folder");
            }
            finally
            {
                if (baked != null)
                {
                    UnityEngine.Object.DestroyImmediate(baked);
                }
                if (AssetDatabase.IsValidFolder(
                        "Assets/Amuse.StreamingReadback"))
                {
                    AssetDatabase.DeleteAsset(
                        "Assets/Amuse.StreamingReadback");
                }
                if (AssetDatabase.IsValidFolder(
                        "Assets/ZZZ_GeneratedAssets"))
                {
                    AssetDatabase.DeleteAsset(
                        "Assets/ZZZ_GeneratedAssets");
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PlacementIsTheSameOnTwoBuilds()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var firstRoot = NewAtlasOnlyRoot();
            var secondRoot = NewAtlasOnlyRoot();
            AmusePlatformFinishState firstState = null;
            AmusePlatformFinishState secondState = null;
            try
            {
                NewSharedSourceFixture(firstRoot, "stable first");
                NewSharedSourceFixture(secondRoot, "stable second");

                var firstContext = Build(firstRoot);
                firstState =
                    firstContext.GetState<AmusePlatformFinishState>();
                var secondContext = Build(secondRoot);
                secondState =
                    secondContext.GetState<AmusePlatformFinishState>();

                Assert.That(firstContext.Successful, Is.True,
                    "fixture precondition: the first build must complete");
                Assert.That(secondContext.Successful, Is.True,
                    "fixture precondition: the second build must" +
                    " complete");
                Assert.That(firstState.AtlasPageCount, Is.EqualTo(1));
                Assert.That(secondState.AtlasPageCount, Is.EqualTo(1));
                Assert.That(
                    PlacementDigest(firstRoot),
                    Is.EqualTo(PlacementDigest(secondRoot)),
                    "two identical avatars must place onto the same" +
                    " page shape and the same route values");
            }
            finally
            {
                DestroyGenerated(firstState);
                DestroyGenerated(secondState);
                UnityEngine.Object.DestroyImmediate(firstRoot);
                UnityEngine.Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void ASecondBuildCreatesFreshOutputs()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var firstRoot = NewAtlasOnlyRoot();
            var secondRoot = NewAtlasOnlyRoot();
            AmusePlatformFinishState firstState = null;
            AmusePlatformFinishState secondState = null;
            try
            {
                var firstFixture = NewSharedSourceFixture(
                    firstRoot, "fresh first");
                var secondFixture = NewSharedSourceFixture(
                    secondRoot, "fresh second");

                var firstContext = Build(firstRoot);
                firstState =
                    firstContext.GetState<AmusePlatformFinishState>();
                var secondContext = Build(secondRoot);
                secondState =
                    secondContext.GetState<AmusePlatformFinishState>();

                Assert.That(firstContext.Successful, Is.True,
                    "fixture precondition: the first build must complete");
                Assert.That(secondContext.Successful, Is.True,
                    "fixture precondition: the second build must" +
                    " complete");

                var firstPage = (Texture2D)firstFixture.Renderer
                    .sharedMaterial.GetTexture("_MainTex");
                var secondPage = (Texture2D)secondFixture.Renderer
                    .sharedMaterial.GetTexture("_MainTex");
                Assert.That(secondPage, Is.Not.SameAs(firstPage),
                    "the second build must create a fresh page texture");
                Assert.That(secondFixture.Renderer.sharedMaterial,
                    Is.Not.SameAs(firstFixture.Renderer.sharedMaterial),
                    "the second build must create a fresh page material");
                Assert.That(SharedMeshOf(secondFixture.Renderer),
                    Is.Not.SameAs(SharedMeshOf(firstFixture.Renderer)),
                    "the second build must create a fresh output mesh");
                Assert.That(secondState.AtlasPageCount, Is.EqualTo(1),
                    "the second build must atlas again");
            }
            finally
            {
                DestroyGenerated(firstState);
                DestroyGenerated(secondState);
                UnityEngine.Object.DestroyImmediate(firstRoot);
                UnityEngine.Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void TwoRenderersOnOneSourceMeshGetTwoOutputMeshes()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var material = NewAdmittedMaterial(
                    "shared source",
                    new Color32(200, 60, 120, 255),
                    new Color32(60, 120, 200, 255));
                var mesh = Track(NewQuadMesh());
                var first = NewQuadRenderer(root, "first", mesh, material);
                var second = NewQuadRenderer(root, "second", mesh, material);
                var before = new[] { first, second }
                    .Select(SharedMeshOf).ToArray();

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasPageCount, Is.EqualTo(1),
                    "two uses of one material must share one page");
                var firstOutput = SharedMeshOf(first);
                var secondOutput = SharedMeshOf(second);
                Assert.That(firstOutput, Is.Not.SameAs(secondOutput),
                    "each renderer must get its own output mesh");
                Assert.That(firstOutput, Is.Not.SameAs(mesh),
                    "the source mesh must stay untouched");
                Assert.That(mesh.vertexCount, Is.EqualTo(before[0].vertexCount));
                Assert.That(mesh.uv.Length, Is.EqualTo(before[0].uv.Length));
                for (var index = 0; index < mesh.uv.Length; index++)
                {
                    Assert.That(mesh.uv[index],
                        Is.EqualTo(before[0].uv[index]),
                        "the source mesh route must stay unchanged at" +
                        " vertex " + index);
                }
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The split fixture: one real transparent Poiyomi slot over a
        /// split alpha texture, one retained transparent slot beside
        /// it. Alpha separation splits the first submesh, so the
        /// renderer carries the transparent original on slot zero and
        /// the opaque clone on the appended slot one. The split texture
        /// is 8 by 8 texels with anisotropy, so its page border of 16
        /// texels makes the page far larger than 5/4 of the source.
        /// These tests check grouping and cleanup, not the budget, so
        /// the fixture widens only the group factor. The tear down
        /// restores the provider.
        /// </summary>
        private (SkinnedMeshRenderer renderer, Mesh sourceMesh,
            Material transparent, Texture2D texture)
            NewSplitFixture(GameObject root)
        {
            AtlasBudgetLimits.Provider = () => new AtlasBudgetLimits(
                1000,
                1,
                AtlasBudgetLimits.Production.AvatarRetainedBytes,
                AtlasBudgetLimits.Production.PeakTemporaryBytes);
            AlphaSeparationSplitTests.EnsureSplitFolder();
            var texture = Track(
                AlphaSeparationSplitTests.ImportSplitAlphaTexture(
                    "atlas_end_to_end_split"));
            var transparent =
                NewRealPoiyomiTextureAlphaMaterial(texture);
            var retained = NewRealPoiyomiRetainedMaterial();
            var mesh = Track(
                AlphaSeparationSplitTests.CreateSplitSourceMesh());
            var renderer = AlphaSeparationTestLifecycle.AddRendererPadded(
                root, "body", mesh, transparent, retained);
            return (renderer, mesh, transparent, texture);
        }

        [Test]
        public void TransparentSlotAndItsOpaqueCloneLandInDifferentGroups()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewBothFeaturesRoot(
                "AMUSE atlas split groups");
            AmusePlatformFinishState state = null;
            try
            {
                var (renderer, sourceMesh, transparent, texture) =
                    NewSplitFixture(root);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(renderer.sharedMaterials,
                    Has.Length.EqualTo(3),
                    "fixture precondition: alpha separation must split" +
                    " one submesh and append one slot");

                Assert.That(state.AtlasPageCount, Is.EqualTo(2),
                    "the transparent original and its opaque clone must" +
                    " land in two groups, one page each");
                Assert.That(state.AtlasAdmittedUseCount, Is.EqualTo(2),
                    "the two admitted uses must move onto their pages");
                Assert.That(renderer.sharedMaterials[0],
                    Is.Not.SameAs(transparent),
                    "the transparent slot must carry its page material");
                Assert.That(renderer.sharedMaterials[1],
                    Is.Not.SameAs(renderer.sharedMaterials[0]),
                    "the two groups must not share one page material");
                Assert.That(renderer.sharedMaterials[1]
                    .GetTexture("_MainTex"),
                    Is.Not.SameAs(texture),
                    "the opaque clone slot must carry a page texture");
            }
            finally
            {
                AlphaSeparationTestLifecycle.DestroyGenerated(state);
                AlphaSeparationSplitTests.DeleteSplitFolder();
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RefusedAtlasUseKeepsItsAlphaSeparationOutput()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewBothFeaturesRoot(
                "AMUSE atlas refused use");
            AmusePlatformFinishState state = null;
            try
            {
                var texture = ImportPatternTexture(
                    "refused source",
                    new Color32(180, 180, 40, 255),
                    new Color32(40, 180, 180, 255));
                var material = Track(
                    new Material(LoadInstalledPoiyomiShader()));
                material.SetFloat("_AlphaForceOpaque", 1f);
                PoiyomiFixtureTestBase.ApplyConvertibleRenderState(material);
                material.SetTexture("_MainTex", texture);
                // A non allowed keyword row cannot pass a real alpha
                // build: the controlling property of the drawer
                // governed keyword also makes the alpha proof refuse
                // the slot, so no clone is written at all (observed:
                // the separation state stays null). A non identity
                // member transform refuses the atlas use instead,
                // through a clone alpha separation did write.
                material.SetTextureScale("_MainTex", new Vector2(2f, 2f));
                var mesh = Track(
                    AlphaSeparationTestLifecycle.SingleTriangleMesh());
                var renderer = AlphaSeparationTestLifecycle.AddRenderer(
                    root, "body", mesh, material);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                var clone = state.Separation.CreatedClones
                    .Single(created => created != null);
                Assert.That(renderer.sharedMaterials[0],
                    Is.SameAs(clone),
                    "a refused atlas use must keep the opaque clone that" +
                    " alpha separation wrote");
                Assert.That(state.AtlasPageCount, Is.EqualTo(0),
                    "a refused use must build no page");
                Assert.That(state.AtlasUseRefusals.Count,
                    Is.EqualTo(1),
                    "exactly the one refused use must be recorded");
                Assert.That(state.AtlasUseRefusals[0].renderer,
                    Is.SameAs(renderer));
                Assert.That(state.AtlasUseRefusals[0].slot,
                    Is.EqualTo(0));
                Assert.That(state.AtlasUseRefusals[0].cause,
                    Is.EqualTo(AtlasUseRefusal.NonIdentityTransform),
                    "the non identity member transform must refuse" +
                    " the use");
                Assert.That(SharedMeshOf(renderer), Is.SameAs(mesh),
                    "a refused use must keep its mesh");
            }
            finally
            {
                AlphaSeparationTestLifecycle.DestroyGenerated(state);
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InjectedInvariantFailureReachesTheBuild()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var material = NewAdmittedMaterial(
                    "injected failure",
                    new Color32(90, 90, 200, 255),
                    new Color32(200, 90, 90, 255));
                var renderer = NewQuadRenderer(
                    root, "body", Track(NewQuadMesh()), material);

                Texture2D failedPage = null;
                AtlasPass.PageCheck =
                    (page, plan, sources, protectedCount, ledger) =>
                    {
                        failedPage = page;
                        throw new InvalidOperationException(
                            "AMUSE atlas test injected page check" +
                            " failure.");
                    };

                var context = null as BuildContext;
                var reports = new List<ErrorContext>();
                UnityEngine.TestTools.LogAssert.Expect(
                    LogType.Exception,
                    new System.Text.RegularExpressions.Regex(
                        "AMUSE atlas test injected page check failure"));
                reports.AddRange(ErrorReport.CaptureErrors(
                    () => context = Build(root)));
                Assert.That(context, Is.Not.Null,
                    "fixture precondition: the build must finish");
                Assert.That(reports, Is.Not.Empty,
                    "the injected failure must produce an error entry");
                // The custom platform also raises the vendor
                // platform warnings, so only the presence of an
                // error entry is pinned here.
                state = context.GetState<AmusePlatformFinishState>();
                Assert.That(failedPage == null, Is.True,
                    "the failed page build must destroy the page" +
                    " texture it already made");
                Assert.That(state.AtlasPageCount, Is.EqualTo(0),
                    "the failed group must leave no page behind");
                Assert.That(renderer.sharedMaterial,
                    Is.SameAs(material),
                    "a failed page build must leave the renderer" +
                    " unchanged");
                Assert.That(SharedMeshOf(renderer).uv.Length,
                    Is.EqualTo(4),
                    "a failed page build must leave the source mesh in" +
                    " place");
            }
            finally
            {
                AtlasPass.PageCheck = AtlasPageCheck.Verify;
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AWholeGroupPlacementRefusalStillBuilds()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            var component =
                root.GetComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(component);
            var pageSize =
                serialized.FindProperty("_maximumAtlasPageSize");
            Assert.That(pageSize, Is.Not.Null,
                "AmuseAvatarOptimizer._maximumAtlasPageSize field pin");
            pageSize.intValue = 16;
            serialized.ApplyModifiedProperties();
            AmusePlatformFinishState state = null;
            try
            {
                var material = NewAdmittedMaterial(
                    "too large",
                    new Color32(60, 200, 200, 255),
                    new Color32(200, 60, 200, 255));
                var mesh = Track(NewQuadMesh());
                var renderer = NewQuadRenderer(
                    root, "body", mesh, material);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "a group that only refuses placement must not" +
                    " fail the build");
                Assert.That(renderer.sharedMaterial,
                    Is.SameAs(material),
                    "a refused placement must keep the slot material");
                Assert.That(SharedMeshOf(renderer), Is.SameAs(mesh),
                    "a refused placement must keep the mesh");
                Assert.That(state.AtlasPageCount, Is.EqualTo(0),
                    "a refused placement must build no page");
                Assert.That(state.AtlasUseRefusals.Count,
                    Is.EqualTo(1),
                    "exactly the one refused use must be recorded");
                Assert.That(state.AtlasUseRefusals[0].renderer,
                    Is.SameAs(renderer));
                Assert.That(state.AtlasUseRefusals[0].slot,
                    Is.EqualTo(0));
                Assert.That(state.AtlasUseRefusals[0].cause,
                    Is.EqualTo(AtlasUseRefusal.PlacementTooLarge),
                    "a source larger than the maximum page size must" +
                    " refuse its placement");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PageMaterialKeepsTheSourceEnabledKeywordSet()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var material = NewAdmittedMaterial(
                    "keyword sync",
                    new Color32(200, 120, 60, 255),
                    new Color32(60, 120, 200, 255));
                // The controlling property and the keyword disagree.
                // The capture sees the keyword off, so the use
                // admits. The copy constructor would re-enable it on
                // the page material without the explicit restore.
                material.SetFloat("_EnableUDIMDiscardOptions", 1f);
                material.DisableKeyword("POI_UDIMDISCARD");
                var renderer = NewQuadRenderer(
                    root, "body", Track(NewQuadMesh()), material);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasPageCount, Is.EqualTo(1),
                    "the keyword off material must admit");
                var pageMaterial = renderer.sharedMaterial;
                Assert.That(
                    new HashSet<string>(pageMaterial.shaderKeywords),
                    Is.EqualTo(
                        new HashSet<string>(material.shaderKeywords)),
                    "the page material must keep the source enabled" +
                    " keyword set");
                var pageKeywords =
                    new HashSet<string>(pageMaterial.shaderKeywords);
                Assert.That(pageKeywords.Contains("POI_UDIMDISCARD"),
                    Is.False,
                    "the page material must not re-enable the" +
                    " drawer governed keyword");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RegistryResolvesTheSourceMeshAfterCleanup()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewBothFeaturesRoot(
                "AMUSE atlas registry");
            AmusePlatformFinishState state = null;
            try
            {
                var (renderer, sourceMesh, transparent, texture) =
                    NewSplitFixture(root);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();
                var probe = context.GetState<AtlasEndToEndProbe>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                var atlasMesh = SharedMeshOf(renderer);
                Assert.That(atlasMesh,
                    Is.Not.SameAs(probe.MeshCloneBeforeAtlas),
                    "fixture precondition: the atlas pass must replace" +
                    " the owned mesh clone");
                Assert.That(probe.MeshCloneBeforeAtlas == null,
                    Is.True,
                    "the replaced owned mesh must be destroyed");
                Assert.That(probe.Registry, Is.Not.Null,
                    "fixture precondition: the build must hold the" +
                    " object registry");

                // Alpha separation registered source mesh to clone,
                // so the atlas registration resolves the atlas mesh
                // back to the authored source mesh. The owned clone
                // in between is destroyed, and the chain still
                // resolves.
                Assert.That(
                    ReferenceEquals(
                        probe.AtlasMeshReferenceObject, sourceMesh),
                    Is.True,
                    "the registry must resolve the atlas mesh back to" +
                    " the source mesh after the cleanup destroyed it." +
                    " Resolved: " +
                    (probe.AtlasMeshReferenceObject == null
                        ? "<null>"
                        : probe.AtlasMeshReferenceObject.name));
                var liveReference = probe.Registry.GetReference(
                    atlasMesh, false);
                Assert.That(liveReference, Is.Not.Null,
                    "the atlas mesh must carry a registry entry");
                Assert.That(
                    ReferenceEquals(liveReference.Object, sourceMesh),
                    Is.True,
                    "the registry must resolve the atlas mesh back to" +
                    " the source mesh after the cleanup destroyed it." +
                    " Resolved: " +
                    (liveReference == null || liveReference.Object == null
                        ? "<null>"
                        : liveReference.Object.name));
            }
            finally
            {
                AlphaSeparationTestLifecycle.DestroyGenerated(state);
                AlphaSeparationSplitTests.DeleteSplitFolder();
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AlphaCloneThatThePageReplacesIsDestroyed()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewBothFeaturesRoot(
                "AMUSE atlas owned cleanup");
            AmusePlatformFinishState state = null;
            try
            {
                var (renderer, sourceMesh, transparent, texture) =
                    NewSplitFixture(root);

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();
                var probe = context.GetState<AtlasEndToEndProbe>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(probe.OpaqueCloneBeforeAtlas == null,
                    Is.True,
                    "the opaque material the page replaced must be" +
                    " destroyed");
                Assert.That(probe.MeshCloneBeforeAtlas == null,
                    Is.True,
                    "the mesh clone the page replaced must be" +
                    " destroyed");
                Assert.That(transparent != null, Is.True,
                    "the user owned transparent original must stay" +
                    " alive");
            }
            finally
            {
                AlphaSeparationTestLifecycle.DestroyGenerated(state);
                AlphaSeparationSplitTests.DeleteSplitFolder();
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// An owned object a committed object reference curve names
        /// survives the atlas build. The curve rides a clip of the
        /// committed controller graph on a second renderer. Alpha
        /// apply pairs every clone it writes into a curve with a live
        /// slot on the same renderer, so a production written curve
        /// cannot leave a clone renderer unreferenced. The pre atlas
        /// hook therefore writes the keyframe the way apply writes
        /// mapped curves, and the atlas pass must honor it.
        /// </summary>
        [Test]
        public void CurveReferencedOwnedCloneSurvivesTheAtlasBuild()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewBothFeaturesRoot(
                "AMUSE atlas curve keep");
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            try
            {
                var (renderer, sourceMesh, transparent, texture) =
                    NewSplitFixture(root);
                var retained = NewRealPoiyomiRetainedMaterial();
                AlphaSeparationTestLifecycle.AddRenderer(
                    root,
                    "curved",
                    Track(AlphaSeparationTestLifecycle
                        .SingleTriangleMesh()),
                    retained);
                var swapClip = AlphaSeparationTestLifecycle.NewSwapClip(
                    "AMUSE atlas curve keep", "curved", 0,
                    (0f, retained));
                controller = AlphaSeparationTestLifecycle.NewController(
                    root, "AMUSE atlas curve keep graph", swapClip);

                AtlasEndToEndPlugin.PreAtlasHook =
                    (avatarRoot, hookState, probe) =>
                    {
                        var clip = AlphaSeparationTestLifecycle
                            .CommittedClipWithObjectBinding(
                                avatarRoot,
                                "curved",
                                "m_Materials.Array.data[0]");
                        if (clip == null || probe.OpaqueCloneBeforeAtlas ==
                            null)
                        {
                            throw new InvalidOperationException(
                                "fixture precondition: the committed" +
                                " swap clip and the opaque clone must" +
                                " exist before the atlas pass.");
                        }

                        probe.CurveReferencedObject =
                            probe.OpaqueCloneBeforeAtlas;
                        AnimationUtility.SetObjectReferenceCurve(
                            clip,
                            EditorCurveBinding.PPtrCurve(
                                "curved",
                                typeof(SkinnedMeshRenderer),
                                "m_Materials.Array.data[0]"),
                            new[]
                            {
                                new ObjectReferenceKeyframe
                                {
                                    time = 0f,
                                    value = probe.OpaqueCloneBeforeAtlas,
                                },
                            });
                    };

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();
                var probe = context.GetState<AtlasEndToEndProbe>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                var clone = probe.OpaqueCloneBeforeAtlas;
                Assert.That(clone, Is.Not.Null,
                    "fixture precondition: alpha separation must" +
                    " create the opaque clone");
                Assert.That(renderer.sharedMaterials[2],
                    Is.Not.SameAs(clone),
                    "fixture precondition: the atlas pass must replace" +
                    " the clone slot with its page material");
                Assert.That(clone != null, Is.True,
                    "the owned clone a committed curve names must" +
                    " survive the build");
                var committedClip = AlphaSeparationTestLifecycle
                    .CommittedClipWithObjectBinding(
                        root,
                        "curved",
                        "m_Materials.Array.data[0]");
                var curve = AnimationUtility.GetObjectReferenceCurve(
                    committedClip,
                    EditorCurveBinding.PPtrCurve(
                        "curved",
                        typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"));
                Assert.That(curve.Length, Is.EqualTo(1));
                Assert.That(curve[0].value, Is.SameAs(clone),
                    "the committed curve must still name the clone");
            }
            finally
            {
                AtlasEndToEndPlugin.PreAtlasHook = null;
                AlphaSeparationTestLifecycle.DestroyCommittedClone(
                    root, controller);
                AlphaSeparationTestLifecycle.DestroyGenerated(state);
                AlphaSeparationSplitTests.DeleteSplitFolder();
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AcquisitionFailureWithdrawsTheGroup()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var importableMaterial = NewAdmittedMaterial(
                    "acquisition fine",
                    new Color32(30, 160, 200, 255),
                    new Color32(200, 160, 30, 255));
                var fineRenderer = NewQuadRenderer(
                    root,
                    "fine",
                    Track(NewQuadMesh()),
                    importableMaterial);

                // The point filter puts the second source in its own
                // group, so one withdrawal cannot take the healthy
                // use with it.
                var withdrawingMaterial = NewAdmittedMaterial(
                    "acquisition fails",
                    new Color32(200, 60, 120, 255),
                    new Color32(60, 200, 120, 255),
                    FilterMode.Point);
                var withdrawingTexture =
                    (Texture2D)withdrawingMaterial.GetTexture("_MainTex");
                var withdrawSourceMesh = Track(NewQuadMesh());
                var withdrawingRenderer = NewQuadRenderer(
                    root,
                    "withdraws",
                    withdrawSourceMesh,
                    withdrawingMaterial);

                // Capture admits both sources. Only a runtime failure
                // after an admitted capture withdraws a group, and no
                // real fixture reaches it inside one build, so the
                // acquisition seam fails exactly the second source.
                var originalAcquisition = AtlasPass.AcquireSource;
                AtlasPass.AcquireSource =
                    (Texture2D source,
                        int protectedCount,
                        out AtlasSourceImage image) =>
                    {
                        if (source.GetInstanceID() ==
                            withdrawingTexture.GetInstanceID())
                        {
                            image = null;
                            return false;
                        }

                        return originalAcquisition(
                            source, protectedCount, out image);
                    };

                BuildContext context;
                try
                {
                    context = Build(root);
                }
                finally
                {
                    AtlasPass.AcquireSource = originalAcquisition;
                }
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasUseRefusals.Count,
                    Is.EqualTo(1),
                    "exactly the one withdrawn use must be recorded");
                Assert.That(state.AtlasUseRefusals[0].renderer,
                    Is.SameAs(withdrawingRenderer),
                    "the unacquirable source must withdraw its group");
                Assert.That(state.AtlasUseRefusals[0].slot,
                    Is.EqualTo(0));
                Assert.That(state.AtlasUseRefusals[0].cause,
                    Is.EqualTo(AtlasUseRefusal.SourceAcquisitionFailed),
                    "the withdrawal must name the acquisition failure");
                Assert.That(withdrawingRenderer.sharedMaterial,
                    Is.SameAs(withdrawingMaterial),
                    "a withdrawn group must leave its renderer" +
                    " unchanged");
                Assert.That(SharedMeshOf(withdrawingRenderer),
                    Is.SameAs(withdrawSourceMesh),
                    "a withdrawn group must change nothing on its" +
                    " renderer");
                Assert.That(state.AtlasPageCount, Is.EqualTo(1),
                    "the healthy group must still build its page");
                Assert.That(fineRenderer.sharedMaterial,
                    Is.Not.SameAs(importableMaterial),
                    "the healthy group must still move onto its page");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- report lines ---------------------------------------------------

        private static List<ErrorContext> EntriesWithKey(
            IEnumerable<ErrorContext> reports, string titleKey)
        {
            return reports
                .Where(entry => entry.TheError is SimpleError simple &&
                    simple.TitleKey == titleKey)
                .ToList();
        }

        private static List<ErrorContext> EntriesWithPrefix(
            IEnumerable<ErrorContext> reports, string prefix)
        {
            return reports
                .Where(entry => entry.TheError is SimpleError simple &&
                    simple.TitleKey.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();
        }

        private static long TextureBytesOf(Texture2D texture)
        {
            return AtlasBudget.TextureBytes(
                texture.width,
                texture.height,
                texture.graphicsFormat,
                texture.mipmapCount);
        }

        /// <summary>
        /// --- Falsifier 3: a report that prints nothing for a page set,
        /// or that counts the bytes of the sources as the change. The
        /// line must state the page bytes as the change, the source
        /// bytes as retained, the merge-ready result, and the draw call
        /// fact. The byte oracle reads the live textures, not the
        /// plan. ---
        /// </summary>
        [Test]
        public void TwoMaterialsReportOnePageSetLine()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var firstMaterial = NewAdmittedMaterial(
                    "report first",
                    new Color32(220, 40, 40, 255),
                    new Color32(40, 40, 220, 255));
                var secondMaterial = NewAdmittedMaterial(
                    "report second",
                    new Color32(40, 200, 60, 255),
                    new Color32(220, 200, 40, 255));
                var sourceBytes =
                    TextureBytesOf(
                        (Texture2D)firstMaterial.GetTexture("_MainTex")) +
                    TextureBytesOf(
                        (Texture2D)secondMaterial.GetTexture("_MainTex"));
                var firstRenderer = NewQuadRenderer(
                    root, "first", Track(NewQuadMesh()), firstMaterial);
                NewQuadRenderer(
                    root, "second", Track(NewQuadMesh()), secondMaterial);

                BuildContext context = null;
                var reports = ErrorReport.CaptureErrors(
                    () => context = Build(root));
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasPageCount, Is.EqualTo(1),
                    "fixture precondition: one page");
                var pageBytes = TextureBytesOf((Texture2D)firstRenderer
                    .sharedMaterial.GetTexture("_MainTex"));

                var lines = EntriesWithKey(reports, "amuse.atlas.PageSet");
                Assert.That(lines, Has.Count.EqualTo(1),
                    "one page set must report exactly one line");
                Assert.That(
                    EntriesWithPrefix(reports, "amuse.atlas."),
                    Has.Count.EqualTo(1),
                    "an admitted fixture reports no use refusal");
                var message = lines[0].TheError.ToMessage();
                Assert.That(message, Does.Contain(
                    "moves 2 material slots onto 1 page."));
                Assert.That(message, Does.Contain("merge-ready"));
                Assert.That(message, Does.Contain(
                    "The pages add " + pageBytes + " bytes"));
                Assert.That(message, Does.Contain(
                    "2 source textures with " + sourceBytes + " bytes"));
                Assert.That(message, Does.Contain(
                    "does not reduce draw calls"));
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// --- Falsifier 4: a report that keeps use refusals in state
        /// only, or that prints a line without the slot and the
        /// renderer. ---
        /// </summary>
        [Test]
        public void RefusedUseReportsOneLineWithItsSlot()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var material = NewAdmittedMaterial(
                    "report refused",
                    new Color32(180, 180, 40, 255),
                    new Color32(40, 180, 180, 255));
                material.SetTextureScale("_MainTex", new Vector2(2f, 2f));
                NewQuadRenderer(
                    root, "body", Track(NewQuadMesh()), material);

                BuildContext context = null;
                var reports = ErrorReport.CaptureErrors(
                    () => context = Build(root));
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                var lines = EntriesWithKey(
                    reports, "amuse.atlas.NonIdentityTransform");
                Assert.That(lines, Has.Count.EqualTo(1),
                    "one refused use must report exactly one line");
                Assert.That(
                    EntriesWithPrefix(reports, "amuse.atlas."),
                    Has.Count.EqualTo(1),
                    "a fixture with no admitted use reports no page set");
                Assert.That(lines[0].TheError.ToMessage(), Does.Contain(
                    "material slot 0 of renderer 'body'"));
                Assert.That(lines[0].TheError.ToMessage(),
                    Does.Not.Contain("{"),
                    "every placeholder of the line must be filled");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// --- Falsifier 5: a report that lists a page set for every
        /// planned group. A withdrawn group built no kept page, so it
        /// reports only its use lines. ---
        /// </summary>
        [Test]
        public void WithdrawnGroupReportsNoPageSetLine()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            var originalAcquisition = AtlasPass.AcquireSource;
            try
            {
                NewQuadRenderer(
                    root,
                    "fine",
                    Track(NewQuadMesh()),
                    NewAdmittedMaterial(
                        "report fine",
                        new Color32(30, 160, 200, 255),
                        new Color32(200, 160, 30, 255)));
                var withdrawingMaterial = NewAdmittedMaterial(
                    "report withdraws",
                    new Color32(200, 60, 120, 255),
                    new Color32(60, 200, 120, 255),
                    FilterMode.Point);
                var withdrawingTexture =
                    (Texture2D)withdrawingMaterial.GetTexture("_MainTex");
                NewQuadRenderer(
                    root,
                    "withdraws",
                    Track(NewQuadMesh()),
                    withdrawingMaterial);
                AtlasPass.AcquireSource =
                    (Texture2D source,
                        int protectedCount,
                        out AtlasSourceImage image) =>
                    {
                        if (source.GetInstanceID() ==
                            withdrawingTexture.GetInstanceID())
                        {
                            image = null;
                            return false;
                        }

                        return originalAcquisition(
                            source, protectedCount, out image);
                    };

                BuildContext context = null;
                var reports = ErrorReport.CaptureErrors(
                    () => context = Build(root));
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(
                    EntriesWithKey(reports, "amuse.atlas.PageSet"),
                    Has.Count.EqualTo(1),
                    "only the healthy group reports a page set");
                var withdrawn = EntriesWithKey(
                    reports, "amuse.atlas.SourceAcquisitionFailed");
                Assert.That(withdrawn, Has.Count.EqualTo(1),
                    "the withdrawn use reports one line");
                Assert.That(withdrawn[0].TheError.ToMessage(),
                    Does.Contain("renderer 'withdraws'"));
            }
            finally
            {
                AtlasPass.AcquireSource = originalAcquisition;
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// --- Falsifier 6: a budget line that names the number but not
        /// its value and limit (design section 10). The group factor
        /// 1 / 1000 refuses any page. Its limit is the floor of the
        /// source bytes divided by 1000. ---
        /// </summary>
        [Test]
        public void BudgetRefusalLineStatesTheValueAndTheLimit()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot();
            AmusePlatformFinishState state = null;
            try
            {
                var material = NewAdmittedMaterial(
                    "report budget",
                    new Color32(90, 200, 90, 255),
                    new Color32(200, 90, 200, 255));
                var limit = TextureBytesOf(
                    (Texture2D)material.GetTexture("_MainTex")) / 1000;
                NewQuadRenderer(
                    root, "body", Track(NewQuadMesh()), material);
                AtlasBudgetLimits.Provider = () => new AtlasBudgetLimits(
                    1, 1000, long.MaxValue, long.MaxValue);

                BuildContext context = null;
                var reports = ErrorReport.CaptureErrors(
                    () => context = Build(root));
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(state.AtlasUseRefusals, Has.Count.EqualTo(1));
                var value = state.AtlasUseRefusals[0].valueBytes;
                Assert.That(state.AtlasUseRefusals[0].limitBytes,
                    Is.EqualTo(limit));
                Assert.That(value, Is.GreaterThan(limit));
                var lines = EntriesWithKey(
                    reports, "amuse.atlas.GroupBudget");
                Assert.That(lines, Has.Count.EqualTo(1));
                var message = lines[0].TheError.ToMessage();
                Assert.That(message, Does.Contain(
                    "The value is " + value + " bytes."));
                Assert.That(message, Does.Contain(
                    "The limit is " + limit + " bytes."));
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Characterization, not a falsifier. Both switches off must
        /// leave every renderer and every report unchanged.
        /// </summary>
        [Test]
        public void BothSwitchesOffStillChangesNothing()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE atlas both off");
            var component =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(component);
            serialized.FindProperty("_alphaSeparatorEnabled")
                .boolValue = false;
            serialized.FindProperty("_atlasingEnabled").boolValue = false;
            serialized.ApplyModifiedProperties();
            var material = NewAdmittedMaterial(
                "both off",
                new Color32(120, 30, 200, 255),
                new Color32(30, 200, 120, 255));
            var mesh = Track(NewQuadMesh());
            var renderer = NewQuadRenderer(root, "body", mesh, material);
            AmusePlatformFinishState state = null;
            try
            {
                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(renderer.sharedMaterial,
                    Is.SameAs(material),
                    "both switches off must keep the material");
                Assert.That(SharedMeshOf(renderer), Is.SameAs(mesh),
                    "both switches off must keep the mesh");
                Assert.That(state.AtlasPageCount, Is.EqualTo(0),
                    "both switches off must build no page");
                Assert.That(state.AtlasUseRefusals, Is.Empty,
                    "both switches off must record no use refusal");
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- shared fixture helpers ---------------------------------------

        private sealed class QuadFixture
        {
            internal MeshRenderer Renderer;
        }

        /// <summary>
        /// One renderer with its own admitted material and quad mesh.
        /// </summary>
        private QuadFixture NewSharedSourceFixture(
            GameObject root, string name)
        {
            var material = NewAdmittedMaterial(
                name,
                new Color32(140, 200, 40, 255),
                new Color32(40, 200, 140, 255));
            var renderer = NewQuadRenderer(
                root, name, Track(NewQuadMesh()), material);
            return new QuadFixture { Renderer = renderer };
        }

        private static MeshRenderer NewQuadRenderer(
            GameObject root,
            string name,
            Mesh mesh,
            Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<MeshRenderer>();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>
        /// A digest of the placement: the page shape and every route
        /// channel value of every renderer, in capture order.
        /// </summary>
        private static string PlacementDigest(GameObject root)
        {
            var parts = new List<string>();
            foreach (var renderer in root
                .GetComponentsInChildren<Renderer>(true))
            {
                var texture = (Texture2D)renderer.sharedMaterials[0]
                    .GetTexture("_MainTex");
                parts.Add(texture != null
                    ? texture.width + "x" + texture.height
                    : "none");
                parts.AddRange(SharedMeshOf(renderer).uv
                    .Select(uv => uv.x.ToString("R") + "|" +
                        uv.y.ToString("R")));
            }

            return string.Join(";;", parts);
        }

        /// <summary>
        /// Destroys every live object the build state says AMUSE owns.
        /// The tear down folder deletion then removes any persisted
        /// remains through the asset database.
        /// </summary>
        private static void DestroyGenerated(
            AmusePlatformFinishState state)
        {
            if (state == null)
            {
                return;
            }

            foreach (var owned in state.AmuseOwnedObjects)
            {
                if (owned != null && !EditorUtility.IsPersistent(owned))
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }
        }
    }
}
