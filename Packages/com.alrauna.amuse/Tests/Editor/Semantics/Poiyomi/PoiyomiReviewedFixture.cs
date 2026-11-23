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

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// One capture fixture per test. Every fixture drives the
    /// installed bundled Poiyomi 9.3 shaders through their pinned
    /// GUIDs, so a wrong route fact refuses against the vendor
    /// shader and not against a stand-in. The default fixture is a
    /// fresh material on a renderer with a quad submesh that covers
    /// the unit square. A fresh material carries the five vendor
    /// default textures and the seven vendor default keywords.
    /// </summary>
    public sealed class PoiyomiReviewedFixture : IDisposable
    {
        private const string TempFolder = "Assets/AmuseTests_PoiyomiReviewed";
        private const string RendererChildName = "Body";

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        private GameObject _root;
        private SkinnedMeshRenderer _renderer;
        private Mesh _defaultMesh;
        private CommittedControllerGraphResult _animatedGraph;

        public PoiyomiReviewedFixture()
        {
            _scope.EnsureTempFolder();
        }

        /// <summary>
        /// The capture root. It exists after the first capture. A
        /// test animates a route property against this root.
        /// </summary>
        internal GameObject Root
        {
            get
            {
                EnsureRoot();
                return _root;
            }
        }

        public void Dispose()
        {
            _scope.TearDown();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
        }

        /// <summary>
        /// A fresh material of one bundled row. The material carries
        /// the vendor defaults, because <c>new Material(shader)</c>
        /// copies them.
        /// </summary>
        internal Material NewMaterial(bool twoPass)
        {
            return Track(new Material(LoadInstalledShader(twoPass)));
        }

        private int _routeTextureCounter;

        /// <summary>
        /// An imported PNG asset of the given square size. The
        /// texture is a project asset with a texture importer and a
        /// deterministic pattern, so the capture accepts it as a
        /// route source. Each call imports its own file. A test
        /// imports its textures before it sets keyword state, and
        /// enables keywords after the last import.
        /// </summary>
        internal Texture2D NewRouteTexture(int size)
        {
            _routeTextureCounter++;
            var staging = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % size;
                var y = index / size;
                pixels[index] = new Color32(
                    (byte)(40 + x * 2),
                    (byte)(60 + y * 2),
                    (byte)(80 + ((x + y) & 15) * 4),
                    255);
            }

            staging.SetPixels32(pixels);
            staging.Apply(false, false);
            _scope.EnsureTempFolder();
            var path = TempFolder + "/route texture " + size +
                       " " + _routeTextureCounter + ".png";
            File.WriteAllBytes(path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(
                loaded, Is.Not.Null,
                "fixture precondition: the route texture must import");
            return loaded;
        }

        /// <summary>
        /// Captures the one slot of the fixture renderer with the
        /// material. The mesh defaults to the quad of the fixture.
        /// The graph defaults to the graph of the last
        /// <see cref="AnimateScalar"/> call, or to an empty graph.
        /// </summary>
        internal AtlasUseOutcome CaptureSingle(
            Material material,
            Mesh mesh = null,
            CommittedControllerGraphResult graph = null)
        {
            EnsureRoot();
            _renderer.sharedMesh = mesh ?? _defaultMesh;
            _renderer.sharedMaterials = new[] { material };

            var outcomes = AtlasUseCapture.Capture(
                _root,
                graph ?? _animatedGraph ?? EmptyGraph(_root),
                new StubBindings(null),
                1);
            Assert.That(
                outcomes, Has.Count.EqualTo(1),
                "fixture precondition: the capture sees one slot");
            return outcomes[0];
        }

        /// <summary>
        /// The material key of one admitted capture of the material.
        /// The key comes from the real capture, so the test never
        /// re-derives it.
        /// </summary>
        internal string MaterialKeyOf(Material material)
        {
            var outcome = CaptureSingle(material);
            AssertAdmitted(outcome);
            return outcome.Use.MaterialKey;
        }

        /// <summary>
        /// Animates one material property of the fixture renderer and
        /// keeps the committed graph for the next capture. The curve
        /// binds the renderer child that the capture reads.
        /// </summary>
        internal void AnimateScalar(GameObject root, string property)
        {
            var clip = Track(new AnimationClip
            {
                name = "animate " + property,
            });
            var binding = EditorCurveBinding.FloatCurve(
                RendererChildName,
                typeof(SkinnedMeshRenderer),
                "material." + property);
            AnimationUtility.SetEditorCurve(
                clip, binding, AnimationCurve.Constant(0f, 1f, 1f));

            var controller =
                Track(new AnimatorController { name = "PoiyomiReviewed" });
            controller.AddLayer("Route");
            Track(controller.layers[0].stateMachine);
            var state = controller.layers[0].stateMachine
                .AddState("State" + property);
            state.motion = clip;

            _animatedGraph =
                CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));
        }

        internal static void AssertAdmitted(AtlasUseOutcome outcome)
        {
            Assert.That(
                outcome.Refusal, Is.EqualTo(AtlasUseRefusal.None),
                "the use must be admitted, detail: " + outcome.Detail);
            Assert.That(outcome.Use, Is.Not.Null);
        }

        /// <summary>
        /// The use must be admitted, and every named texture property
        /// must have moved into a companion page. Admission alone
        /// does not prove that a route fired, because an inert route
        /// is admitted too. A group admitted test asserts the
        /// companions by name, so a wrong gate or a keyword that is
        /// off at capture time fails the test.
        /// </summary>
        internal static void AssertCompanion(
            AtlasUseOutcome outcome, params string[] properties)
        {
            AssertAdmitted(outcome);
            Assert.That(
                outcome.Use.Companions, Is.Not.Empty,
                "the admitted use must carry companions");
            var moved = outcome.Use.Companions
                .Select(companion => companion.Property)
                .ToList();
            foreach (var property in properties)
            {
                Assert.That(
                    moved, Does.Contain(property),
                    "the texture '" + property +
                    "' must have moved into a companion page");
            }
        }

        internal static void AssertRefused(
            AtlasUseOutcome outcome,
            AtlasUseRefusal expected,
            string detailFragment)
        {
            Assert.That(
                outcome.Refusal, Is.EqualTo(expected),
                "the use must refuse with the named cause, detail: " +
                outcome.Detail);
            Assert.That(outcome.Use, Is.Null);
            Assert.That(
                outcome.Detail, Does.Contain(detailFragment),
                "the refusal detail must name the bad fact");
        }

        private static Shader LoadInstalledShader(bool twoPass)
        {
            var guid = twoPass
                ? PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid
                : PoiyomiSourceAttestation.CanonicalShaderGuid;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed bundled Poiyomi " +
                "shader asset must resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed bundled Poiyomi " +
                "shader asset must load");
            return shader;
        }

        private void EnsureRoot()
        {
            if (_root != null)
            {
                return;
            }

            _root = Track(new GameObject("poiyomi reviewed capture root"));
            var child = Track(new GameObject(RendererChildName));
            child.transform.SetParent(_root.transform, false);
            _renderer = child.AddComponent<SkinnedMeshRenderer>();
            _defaultMesh = Track(NewQuadMesh());
            _renderer.sharedMesh = _defaultMesh;
            _renderer.sharedMaterials =
                new Material[] { Track(NewMaterial(false)) };
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

        private static CommittedControllerGraphResult EmptyGraph(
            GameObject root)
        {
            return CommittedControllerGraph.Enumerate(
                root, new StubBindings(null));
        }

        /// <summary>
        /// The stand-in bindings of the capture. The innate
        /// controller is null when the graph carries no clip.
        /// </summary>
        private sealed class StubBindings : IPlatformAnimatorBindings
        {
            private readonly RuntimeAnimatorController controller;

            internal StubBindings(
                RuntimeAnimatorController controller)
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
    }
}
