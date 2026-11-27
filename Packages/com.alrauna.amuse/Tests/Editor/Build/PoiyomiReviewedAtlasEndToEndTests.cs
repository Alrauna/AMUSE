using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The end to end atlas path of the reviewed Poiyomi 9.3 route.
    /// One case per feature group, on each bundled row. Two
    /// materials turn on the features of one group with companion
    /// textures at selector 0, the build runs the production pass
    /// sequence, and both slots must share one page set. The source
    /// and the built slot then render on the render harness of the
    /// live value oracles. The largest channel difference of the
    /// two frames must stay at one of 255 or less. The render mesh
    /// is a dome patch that faces the camera. The sun and the view
    /// change its terms, so the comparison cannot pass blind.
    /// </summary>
    public sealed class PoiyomiReviewedAtlasEndToEndTests
    {
        // A 512 pixel frame over the dome samples only mip level 0 of the
        // 64 texel fixture textures. The final dome and frame were measured
        // on 2026-11-27. The protected count stays at 2.
        private const int EndToEndFrameSize = 512;
        private const string TempFolder =
            "Assets/AmuseTests_PoiyomiReviewedEndToEnd";

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        private Func<BuildTarget> _originalTarget;

        [SetUp]
        public void SetUp()
        {
            _originalTarget =
                AmusePlatformFinishPlugin.ActiveBuildTargetForTests;
            AmusePlatformFinishPlugin.ActiveBuildTargetForTests =
                () => BuildTarget.StandaloneWindows64;
            AtlasEndToEndTests.AtlasEndToEndPlugin.PreAtlasHook = null;
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
            AtlasEndToEndTests.AtlasEndToEndPlugin.PreAtlasHook = null;
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

        // --- the fixtures ----------------------------------------------

        private static Shader LoadInstalledPoiyomiShader(bool twoPass)
        {
            var guid = twoPass
                ? PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid
                : PoiyomiSourceAttestation.CanonicalShaderGuid;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the bundled shader must " +
                "resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the bundled shader must load");
            return shader;
        }

        /// <summary>
        /// One mipmapped RGBA32 source. Each channel changes linearly from
        /// the first color at one corner to the second color at the other
        /// corner. The step is at most two levels per texel, so the half
        /// precision rounding of the vendor shader moves a sample by at most
        /// one level. Bilinear filtering and no anisotropy keep the render
        /// comparison stable.
        /// </summary>
        private static Texture2D ImportPatternTexture(
            string name, Color32 first, Color32 last, int size = 64)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var t = (x + y) / (2f * (size - 1));
                    pixels[y * size + x] = new Color32(
                        (byte)Mathf.Round(Mathf.Lerp(first.r, last.r, t)),
                        (byte)Mathf.Round(Mathf.Lerp(first.g, last.g, t)),
                        (byte)Mathf.Round(Mathf.Lerp(first.b, last.b, t)),
                        255);
                }
            }

            // Not tracked: an imported asset leaves through the
            // folder deletion, never through DestroyImmediate.
            return TestTextureImport.WritePng(
                TempFolder + "/" + name + ".png",
                size,
                size,
                pixels,
                importer =>
                {
                    importer.mipmapEnabled = true;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.anisoLevel = 0;
                });
        }

        /// <summary>
        /// One material of the group case on one bundled row with a
        /// 64 texel member source. The features of the group turn
        /// on after the caller assigns the companion textures.
        /// </summary>
        private Material NewGroupMaterial(
            bool twoPass,
            string name,
            Color32 even,
            Color32 odd)
        {
            var material = Track(new Material(
                LoadInstalledPoiyomiShader(twoPass)));
            material.SetTexture(
                "_MainTex",
                ImportPatternTexture(
                    name, even, odd, 64));
            return material;
        }

        /// <summary>
        /// One new copy of the subject dome. The dome is a 35 degree
        /// cap of a sphere that faces the camera. Its normals curve
        /// across the frame, and the sun crosses the cap at a grazing
        /// angle, so the shadow terminator crosses the patch. The cap
        /// stays off the limb, so the sampled mip levels stay low. Its
        /// UVs fill the unit square, so the build sees no UV outside it.
        /// </summary>
        private Mesh NewSubjectMesh()
        {
            const float capAngle = 35f * Mathf.Deg2Rad;
            const int rings = 24;
            const int segments = 64;
            const float radius = 0.5f;
            var capRadius = Mathf.Sin(capAngle);
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var normals = new Vector3[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (var r = 0; r <= rings; r++)
            {
                var theta = capAngle * r / rings;
                for (var s = 0; s <= segments; s++)
                {
                    var phi = 2f * Mathf.PI * s / segments;
                    var planar = new Vector2(
                        Mathf.Cos(phi), Mathf.Sin(phi)) * Mathf.Sin(theta);
                    var normal = new Vector3(
                        planar.x, planar.y, -Mathf.Cos(theta));
                    var index = r * (segments + 1) + s;
                    normals[index] = normal;
                    vertices[index] = radius * normal;
                    uv[index] = new Vector2(
                        Mathf.Clamp01(0.5f + 0.5f * planar.x / capRadius),
                        Mathf.Clamp01(0.5f + 0.5f * planar.y / capRadius));
                }
            }
            var triangles = new List<int>();
            for (var r = 0; r < rings; r++)
            {
                for (var s = 0; s < segments; s++)
                {
                    var a = r * (segments + 1) + s;
                    var b = a + segments + 1;
                    AddFrontFacing(triangles, vertices, normals, a, b, a + 1);
                    AddFrontFacing(triangles, vertices, normals, b, b + 1, a + 1);
                }
            }
            var mesh = Track(new Mesh());
            mesh.name = "AMUSE poiyomi subject dome";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Adds one triangle wound to face the outward normals.
        /// </summary>
        private static void AddFrontFacing(
            List<int> triangles,
            Vector3[] vertices,
            Vector3[] normals,
            int a,
            int b,
            int c)
        {
            var facing = Vector3.Cross(
                vertices[b] - vertices[a], vertices[c] - vertices[a]);
            var outward = normals[a] + normals[b] + normals[c];
            if (Vector3.Dot(facing, outward) < 0f)
            {
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(b);
                return;
            }
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>
        /// One avatar root with alpha separation off and atlasing
        /// on, so the only moving part is the atlas path.
        /// </summary>
        private static GameObject NewAtlasOnlyRoot(string name)
        {
            var root = new GameObject(name);
            root.AddComponent<
                Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(
                root.GetComponent<
                    Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>());
            var alphaSwitch =
                serialized.FindProperty("_alphaSeparatorEnabled");
            Assert.That(alphaSwitch, Is.Not.Null,
                "AmuseAvatarOptimizer._alphaSeparatorEnabled field" +
                " pin");
            alphaSwitch.boolValue = false;
            // The largest count that the atlas budget admits. The final
            // dome samples only mip level 0 of these fixture textures.
            const int measuredProtectedMipCount = 2;
            var mipLevels =
                serialized.FindProperty("_protectedMipLevelCount");
            Assert.That(mipLevels, Is.Not.Null,
                "AmuseAvatarOptimizer._protectedMipLevelCount field pin");
            mipLevels.intValue = measuredProtectedMipCount;
            serialized.ApplyModifiedProperties();
            FixtureProofScope.PinAllSizes(root);
            return root;
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

        private static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        /// <summary>
        /// The largest per channel difference of two renders, in
        /// units of one of 255.
        /// </summary>
        private static int LargestChannelDifference(
            Color32[] before,
            Color32[] after)
        {
            Assert.That(
                before.Length, Is.EqualTo(after.Length),
                "fixture precondition: the two frames must match");
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

        /// <summary>
        /// The frame must show the pattern subject. A frame of pure
        /// background proves nothing, so the comparison guard fails
        /// on it.
        /// </summary>
        private static void AssertFrameShowsTheSubject(
            Color32[] frame, string context)
        {
            var visible = 0;
            foreach (var pixel in frame)
            {
                if (Math.Abs(pixel.r - pixel.g) > 30 ||
                    Math.Abs(pixel.r - pixel.b) > 30)
                {
                    visible++;
                }
            }

            Assert.That(
                visible, Is.GreaterThanOrEqualTo(8),
                "the frame must show the colored subject. The case" +
                " '" + context + "' rendered a blind frame");
        }

        /// <summary>
        /// The one build of the case through the dedicated end to
        /// end platform, whose plugin runs the production pass
        /// sequence in the production order.
        /// </summary>
        private static BuildContext Build(GameObject root)
        {
            return AvatarProcessor.ProcessAvatar(
                root,
                AtlasEndToEndTests.AtlasEndToEndPlatform.Instance);
        }

        /// <summary>
        /// Destroys every live object the build state says AMUSE
        /// owns. The tear down folder deletion then removes any
        /// persisted remains through the asset database.
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
                if (owned != null &&
                    !EditorUtility.IsPersistent(owned))
                {
                    UnityEngine.Object.DestroyImmediate(owned);
                }
            }
        }

        /// <summary>
        /// The shared body of one group case without saved assets.
        /// </summary>
        private void RunGroupCase(
            bool twoPass,
            string caseName,
            Action<Material, PoiyomiReviewedEndToEndTextures> setup,
            params string[] companionProperties)
        {
            RunGroupCase(
                twoPass, caseName, false, setup, companionProperties);
        }

        /// <summary>
        /// The shared body of one group case. It assigns the
        /// companion textures and the keywords through the setup
        /// action on both materials, builds the avatar, asserts the
        /// one shared page set with its named pages, and proves that
        /// each built slot renders as its source did. Each companion
        /// must change the source frame when its texture changes, so
        /// a wrong page mapping cannot pass unseen. A saved case also
        /// proves that the build leaves both source assets unchanged.
        /// </summary>
        private void RunGroupCase(
            bool twoPass,
            string caseName,
            bool saveAssets,
            Action<Material, PoiyomiReviewedEndToEndTextures> setup,
            params string[] companionProperties)
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = NewAtlasOnlyRoot("AMUSE reviewed atlas " + caseName);
            AmusePlatformFinishState state = null;
            try
            {
                using var capture = new PoiyomiReviewedFixture();
                var texturesA = new PoiyomiReviewedEndToEndTextures(
                    this, twoPass, caseName + " first");
                var texturesB = new PoiyomiReviewedEndToEndTextures(
                    this, twoPass, caseName + " second");
                var firstMaterial = texturesA.Material;
                var secondMaterial = texturesB.Material;
                string assetPath = null;
                string secondAssetPath = null;
                if (saveAssets)
                {
                    assetPath = TempFolder + "/" + caseName + " first.mat";
                    secondAssetPath =
                        TempFolder + "/" + caseName + " second.mat";
                    firstMaterial = new Material(firstMaterial);
                    AssetDatabase.CreateAsset(firstMaterial, assetPath);
                    secondMaterial = new Material(secondMaterial);
                    AssetDatabase.CreateAsset(secondMaterial, secondAssetPath);
                }

                setup(firstMaterial, texturesA);
                setup(secondMaterial, texturesB);
                // Asset imports can reset material keywords. Apply the
                // setup again after both sets of textures are imported.
                setup(firstMaterial, texturesA);
                setup(secondMaterial, texturesB);

                foreach (var property in companionProperties)
                {
                    var route = AtlasRouteTable.PoiyomiReviewedMainColor
                        .CompanionRoutes.Single(entry => entry.Property == property);
                    Assert.That(
                        route.UseOf(firstMaterial, false),
                        Is.EqualTo(AtlasRouteUse.Uv0),
                        "The first material must move " + property +
                        ". Its keywords are " +
                        string.Join(", ", firstMaterial.shaderKeywords));
                    Assert.That(
                        route.UseOf(secondMaterial, false),
                        Is.EqualTo(AtlasRouteUse.Uv0),
                        "The second material must move " + property +
                        ". Its keywords are " +
                        string.Join(", ", secondMaterial.shaderKeywords));
                }

                var firstUse = capture.CaptureSingle(firstMaterial).Use;
                var secondUse = capture.CaptureSingle(secondMaterial).Use;
                Assert.That(firstUse, Is.Not.Null);
                Assert.That(secondUse, Is.Not.Null);
                var firstParts = firstUse.MaterialKey.Split(';');
                var secondParts = secondUse.MaterialKey.Split(';');
                Assert.That(
                    firstUse.MaterialKey, Is.EqualTo(secondUse.MaterialKey),
                    "The material keys differ. First only: " +
                    string.Join(", ", firstParts.Except(secondParts)) +
                    ". Second only: " +
                    string.Join(", ", secondParts.Except(firstParts)));
                Assert.That(
                    firstUse.RouteSignature,
                    Is.EqualTo(secondUse.RouteSignature),
                    "The companion route signatures differ");

                var firstMesh = NewSubjectMesh();
                var secondMesh = NewSubjectMesh();
                var firstRenderer = NewQuadRenderer(
                    root, caseName + " first", firstMesh, firstMaterial);
                var secondRenderer = NewQuadRenderer(
                    root, caseName + " second", secondMesh,
                    secondMaterial);

                using var harness =
                    new PoiyomiRenderHarness(EndToEndFrameSize);
                // The harness ambient sits at black for the toon
                // light clamp. The dim grey keeps the shaded base
                // color visible in the oracle frame.
                RenderSettings.ambientLight =
                    new Color(0.35f, 0.35f, 0.35f);
                // The first render of each row compiles the variants.
                harness.Render(firstMaterial, firstMesh);
                harness.Render(secondMaterial, secondMesh);
                var firstBefore =
                    harness.Render(firstMaterial, firstMesh);
                var secondBefore =
                    harness.Render(secondMaterial, secondMesh);
                AssertFrameShowsTheSubject(
                    firstBefore, caseName + " first source");
                AssertFrameShowsTheSubject(
                    secondBefore, caseName + " second source");

                // Each companion must change the source frame when its
                // texture changes. Back-face companions render from the
                // back. Every failing companion is listed in one assertion.
                var failures = new List<string>();
                foreach (var property in companionProperties.Where(
                    p => !CompanionLooks.ContainsKey(p)))
                {
                    var back = IsBackFaceCompanion(property);
                    harness.SetViewYaw(back ? 180f : 0f);
                    var probe = Track(new Material(firstMaterial));
                    probe.SetTexture(property, texturesA.Alternate(property));
                    var before = harness.Render(firstMaterial, firstMesh);
                    var swapped = harness.Render(probe, firstMesh);
                    var change = LargestChannelDifference(before, swapped);
                    var failure = SensitivityFailure(
                        caseName, property, change, string.Empty);
                    if (failure != null)
                    {
                        failures.Add(failure);
                    }
                }
                harness.SetViewYaw(0f);
                Assert.That(
                    failures, Is.Empty,
                    "These companions must change the source frame:\n" +
                    string.Join("\n", failures));

                byte[] assetBefore = null;
                byte[] secondAssetBefore = null;
                string[] keywordsSaved = null;
                if (saveAssets)
                {
                    AssetDatabase.SaveAssetIfDirty(firstMaterial);
                    AssetDatabase.SaveAssetIfDirty(secondMaterial);
                    Assert.That(EditorUtility.IsDirty(firstMaterial), Is.False,
                        "The first material must be saved before the build");
                    Assert.That(EditorUtility.IsDirty(secondMaterial), Is.False,
                        "The second material must be saved before the build");
                    keywordsSaved = firstMaterial.shaderKeywords
                        .OrderBy(keyword => keyword).ToArray();
                    var savedFirst = capture.CaptureSingle(firstMaterial).Use;
                    var savedSecond = capture.CaptureSingle(secondMaterial).Use;
                    Assert.That(savedFirst, Is.Not.Null);
                    Assert.That(savedSecond, Is.Not.Null);
                    Assert.That(
                        EditorUtility.IsDirty(firstMaterial) ||
                        EditorUtility.IsDirty(secondMaterial), Is.False,
                        "The saved captures must not dirty the materials");
                    Assert.That(
                        savedFirst.MaterialKey,
                        Is.EqualTo(savedSecond.MaterialKey),
                        "The saved material keys differ");
                    Assert.That(
                        savedFirst.RouteSignature,
                        Is.EqualTo(savedSecond.RouteSignature));
                    assetBefore = File.ReadAllBytes(assetPath);
                    secondAssetBefore = File.ReadAllBytes(secondAssetPath);
                }

                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();
                if (saveAssets)
                {
                    Assert.That(
                        assetBefore.SequenceEqual(File.ReadAllBytes(assetPath)),
                        Is.True,
                        "The first source asset file changed in the build");
                    Assert.That(
                        secondAssetBefore.SequenceEqual(
                            File.ReadAllBytes(secondAssetPath)),
                        Is.True,
                        "The second source asset file changed in the build");
                    Assert.That(
                        firstMaterial.shaderKeywords
                            .OrderBy(keyword => keyword)
                            .SequenceEqual(keywordsSaved),
                        Is.True,
                        "The build changed the keywords of the source");
                    Assert.That(
                        EditorUtility.IsDirty(firstMaterial) ||
                        EditorUtility.IsDirty(secondMaterial), Is.False,
                        "The build dirtied a source material");
                }

                Assert.That(context.Successful, Is.True,
                    "fixture precondition: the build must complete");
                Assert.That(
                    state.AtlasUseRefusals.Select(record =>
                        record.Cause + ": " + record.Detail),
                    Is.Empty,
                    "the two group uses must admit");

                var pageSets = state.AtlasPageSets;
                Assert.That(
                    pageSets, Has.Count.EqualTo(1),
                    "both slots must share one page set");
                var pageSet = pageSets[0];
                Assert.That(
                    pageSet.UseCount, Is.EqualTo(2),
                    "both uses must move onto the pages");
                Assert.That(
                    pageSet.Pages.Count,
                    Is.EqualTo(1 + companionProperties.Length),
                    "the set must carry the main page and one page" +
                    " per moved companion");
                var pageRoutes = pageSet.Pages
                    .Select(page => page.RouteProperty)
                    .ToList();
                foreach (var companion in companionProperties)
                {
                    Assert.That(
                        pageRoutes, Does.Contain(companion),
                        "the page set must carry a page for the" +
                        " companion '" + companion + "'");
                }

                var pageMaterial = firstRenderer.sharedMaterial;
                Assert.That(
                    secondRenderer.sharedMaterial,
                    Is.SameAs(pageMaterial),
                    "both slots must carry the one shared page" +
                    " material");
                foreach (var companion in companionProperties)
                {
                    Assert.That(
                        pageMaterial.GetTexture(companion),
                        Is.Not.Null,
                        "the page material must carry the page of" +
                        " '" + companion + "'");
                }

                var firstOutput = SharedMeshOf(firstRenderer);
                var secondOutput = SharedMeshOf(secondRenderer);
                Assert.That(
                    firstOutput.vertexCount,
                    Is.EqualTo(firstMesh.vertexCount),
                    "the built mesh must keep the vertex count");
                Assert.That(
                    secondOutput.vertexCount,
                    Is.EqualTo(secondMesh.vertexCount),
                    "the built mesh must keep the vertex count");

                harness.Render(pageMaterial, firstOutput);
                harness.Render(pageMaterial, secondOutput);
                var firstAfter =
                    harness.Render(pageMaterial, firstOutput);
                var secondAfter =
                    harness.Render(pageMaterial, secondOutput);
                AssertFrameShowsTheSubject(
                    firstAfter, caseName + " first build");
                AssertFrameShowsTheSubject(
                    secondAfter, caseName + " second build");

                Assert.That(
                    LargestChannelDifference(firstBefore, firstAfter),
                    Is.LessThanOrEqualTo(1),
                    "the built first slot must render as its source did");
                Assert.That(
                    LargestChannelDifference(secondBefore, secondAfter),
                    Is.LessThanOrEqualTo(1),
                    "the built second slot must render as its source did");
                // A companion with its own look shows under that look. A look
                // changes no texture and no UV. The same overrides apply to the
                // source clone and to the built clone, so the built slot still
                // meets the bound of one level under the look.
                var lookFailures = new List<string>();
                foreach (var property in companionProperties.Where(
                    p => CompanionLooks.ContainsKey(p)))
                {
                    harness.SetSunEuler(LookSunOf(property));
                    harness.SetViewYaw(
                        IsBackFaceCompanion(property) ? 180f : 0f);
                    var source = Track(new Material(firstMaterial));
                    CompanionLooks[property](source);
                    var before = harness.Render(source, firstMesh);
                    var probe = Track(new Material(source));
                    probe.SetTexture(property, texturesA.Alternate(property));
                    var change = LargestChannelDifference(
                        before, harness.Render(probe, firstMesh));
                    var lookFailure = SensitivityFailure(
                        caseName, property, change, " under its look");
                    if (lookFailure != null)
                    {
                        lookFailures.Add(lookFailure);
                    }

                    var built = Track(new Material(pageMaterial));
                    CompanionLooks[property](built);
                    var builtFrame = harness.Render(built, firstOutput);
                    AssertFrameShowsTheSubject(
                        before, caseName + " " + property + " source");
                    AssertFrameShowsTheSubject(
                        builtFrame, caseName + " " + property + " build");
                    var builtChange = LargestChannelDifference(
                        before, builtFrame);
                    if (builtChange > 1)
                    {
                        lookFailures.Add(property + " built slot differs by " +
                            builtChange + " under its look");
                    }

                    harness.SetSunEuler(PoiyomiRenderHarness.DefaultSunEuler);
                    harness.SetViewYaw(0f);
                }

                Assert.That(
                    lookFailures, Is.Empty,
                    "These companions must pass under their look:\n" +
                    string.Join("\n", lookFailures));
                if (companionProperties.Any(IsBackFaceCompanion))
                {
                    // The back face values show only on the back faces. The
                    // front comparison above still runs, so a front face
                    // mapping error still fails. This pass compares the
                    // source and the built slots from behind.
                    harness.SetViewYaw(180f);
                    var firstBack = harness.Render(firstMaterial, firstMesh);
                    var secondBack = harness.Render(secondMaterial, secondMesh);
                    var firstBuiltBack =
                        harness.Render(pageMaterial, firstOutput);
                    var secondBuiltBack =
                        harness.Render(pageMaterial, secondOutput);
                    harness.SetViewYaw(0f);
                    AssertFrameShowsTheSubject(
                        firstBack, caseName + " first source from behind");
                    AssertFrameShowsTheSubject(
                        secondBack, caseName + " second source from behind");
                    AssertFrameShowsTheSubject(
                        firstBuiltBack, caseName + " first build from behind");
                    AssertFrameShowsTheSubject(
                        secondBuiltBack,
                        caseName + " second build from behind");
                    Assert.That(
                        LargestChannelDifference(firstBack, firstBuiltBack),
                        Is.LessThanOrEqualTo(1),
                        "the built first slot must render as its source" +
                        " did from behind");
                    Assert.That(
                        LargestChannelDifference(secondBack, secondBuiltBack),
                        Is.LessThanOrEqualTo(1),
                        "the built second slot must render as its source" +
                        " did from behind");
                }
            }
            finally
            {
                DestroyGenerated(state);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// The textures of one material of a group case. Each call
        /// imports its own pattern asset, so the two materials of a
        /// case never share a source texture.
        /// </summary>
        private sealed class PoiyomiReviewedEndToEndTextures
        {
            private readonly PoiyomiReviewedAtlasEndToEndTests owner;
            private readonly bool twoPass;
            private readonly string prefix;
            private readonly Dictionary<string, Texture2D> companions =
                new Dictionary<string, Texture2D>();

            internal Material Material { get; }

            internal PoiyomiReviewedEndToEndTextures(
                PoiyomiReviewedAtlasEndToEndTests owner,
                bool twoPass,
                string prefix)
            {
                this.owner = owner;
                this.twoPass = twoPass;
                this.prefix = prefix;
                Material = owner.NewGroupMaterial(
                    twoPass,
                    prefix + " main",
                    new Color32(210, 45, 45, 255),
                    new Color32(45, 45, 210, 255));
            }

            // The companion pattern is a green to yellow ramp.
            private static readonly Color32 CompanionFirst =
                new Color32(30, 160, 90, 255);
            private static readonly Color32 CompanionLast =
                new Color32(200, 170, 30, 255);

            internal Texture2D Companion(string name)
            {
                if (!companions.TryGetValue(name, out var texture))
                {
                    texture = ImportPatternTexture(
                        prefix + " " + name, CompanionFirst, CompanionLast);
                    companions.Add(name, texture);
                }

                return texture;
            }

            // The alternate is the inverse of the companion pattern: each
            // color value is 255 minus the companion value. The swap moves
            // every texel as far as one linear pattern allows. The import
            // writes alpha 255, so only the color channels invert.
            internal Texture2D Alternate(string property)
            {
                var name = property + " alternate";
                if (!companions.TryGetValue(name, out var texture))
                {
                    texture = ImportPatternTexture(
                        prefix + " " + name,
                        Inverse(CompanionFirst),
                        Inverse(CompanionLast));
                    companions.Add(name, texture);
                }

                return texture;
            }

            private static Color32 Inverse(Color32 color)
            {
                return new Color32(
                    (byte)(255 - color.r),
                    (byte)(255 - color.g),
                    (byte)(255 - color.b),
                    255);
            }
        }

        // --- the four group cases ----------------------------------

        [Test]
        public void MainTextureOnlySharesOnePageSet(
            [Values(false, true)] bool twoPass)
        {
            RunGroupCase(twoPass, "main only", (material, textures) => { });
        }

        private static readonly string[] LightingModeKeywords =
        {
            "_LIGHTINGMODE_TEXTURERAMP",
            "_LIGHTINGMODE_MULTILAYER_MATH",
            "_LIGHTINGMODE_SHADEMAP",
            "_LIGHTINGMODE_REALISTIC",
            "_LIGHTINGMODE_WRAPPED",
            "_LIGHTINGMODE_SKIN",
            "_LIGHTINGMODE_FLAT",
            "_LIGHTINGMODE_CLOTH",
            "_LIGHTINGMODE_SDF",
        };

        private static void SetMultilayerLighting(Material material)
        {
            foreach (var mode in LightingModeKeywords)
            {
                material.DisableKeyword(mode);
            }
            material.SetFloat("_LightingMode", 1f);
            material.EnableKeyword("_LIGHTINGMODE_MULTILAYER_MATH");
            material.SetFloat("_ShadowBorderMapToggle", 1f);
            // The shadow colors are white. A dark color mutes the shadow
            // color textures, so their samples barely move the frame. The
            // third layer border sits at 0.4. A border of 1 covers the
            // first two layers, and the default border covers no region
            // of the dome.
            var shadow = Color.white;
            material.SetColor("_ShadowColor", shadow);
            material.SetColor("_Shadow2ndColor", shadow);
            material.SetColor("_Shadow3rdColor", shadow);
            material.SetFloat("_Shadow3rdBorder", 0.4f);
        }
        private static void SetMatcapMask(
            Material material, PoiyomiReviewedEndToEndTextures textures)
        {
            material.SetTexture(
                "_MatcapMask", textures.Companion("matcap"));
            material.SetFloat("_MatcapEnable", 1f);
            material.EnableKeyword("POI_MATCAP0");
        }

        [Test]
        public void MatcapMaskRendersLikeItsSource()
        {
            RunGroupCase(false, "matcap mask", SetMatcapMask, "_MatcapMask");
        }

        [Test]
        public void AssetMatcapMaskRendersLikeItsSource()
        {
            RunGroupCase(
                false, "asset matcap mask", true, SetMatcapMask,
                "_MatcapMask");
        }

        [TestCase("_MultilayerMathBlurMap", "_LIGHTINGMODE_MULTILAYER_MATH")]
        [TestCase("_ShadowBorderMask", "_LIGHTINGMODE_MULTILAYER_MATH")]
        [TestCase("_ShadowColorTex", "_LIGHTINGMODE_MULTILAYER_MATH")]
        [TestCase("_Shadow2ndColorTex", "_LIGHTINGMODE_MULTILAYER_MATH")]
        [TestCase("_Shadow3rdColorTex", "_LIGHTINGMODE_MULTILAYER_MATH")]
        [TestCase("_ShadowStrengthMask", "_LIGHTINGMODE_MULTILAYER_MATH")]
        [TestCase("_MatcapMask", "POI_MATCAP0")]
        [TestCase("_EmissionMap", "_EMISSION")]
        [TestCase("_EmissionMask", "_EMISSION")]
        [TestCase("_RimMask", "_GLOSSYREFLECTIONS_OFF")]
        [TestCase("_RimTex", "_GLOSSYREFLECTIONS_OFF")]
        [TestCase("_MainColorAdjustTexture", "COLOR_GRADING_HDR")]
        [TestCase("_DetailTex", "FINALPASS")]
        [TestCase("_DetailMask", "FINALPASS")]
        [TestCase("_DetailNormalMap", "FINALPASS")]
        [TestCase("_PPMask", "POSTPROCESS")]
        [TestCase("_VoronoiMask", "POI_VORONOI")]
        [TestCase("_VoronoiNoise", "POI_VORONOI")]
        [TestCase("_BackFaceTexture", "POI_BACKFACE")]
        [TestCase("_BackFaceMask", "POI_BACKFACE")]
        [TestCase("_PathingMap", "POI_PATHING")]
        [TestCase("_PathingColorMap", "POI_PATHING")]
        public void OneCompanionRendersLikeItsSource(
            string property, string keyword)
        {
            RunGroupCase(
                false, property,
                (material, textures) =>
                {
                    material.SetTexture(property, textures.Companion("single"));
                    material.SetFloat("_ShadowBorderMapToggle", 1f);
                    material.SetFloat("_ShadowMaskType", 0f);
                    // The lighting family starts from the lighting group base:
                    // the border map toggle, the mask type, and multilayer math.
                    // Other families keep their own pins.
                    if (!LightingFamily.Contains(property))
                    {
                        material.SetFloat("_ShadowStrength", 1f);
                        material.SetFloat("_RimStyle", 0f);
                        material.EnableKeyword("_RIMSTYLE_POIYOMI");
                        material.SetFloat("_PathPointSampling", 1f);
                        material.SetFloat("_ColorGradingToggle", 1f);
                    }
                    SetMultilayerLighting(material);
                    switch (keyword)
                    {
                        case "POI_MATCAP0": material.SetFloat("_MatcapEnable", 1f); break;
                        case "_EMISSION":
                            material.SetFloat("_EnableEmission", 1f);
                            material.SetFloat("_EmissionStrength", 1f);
                            material.SetColor("_EmissionColor", Color.white);
                            break;
                        case "_GLOSSYREFLECTIONS_OFF":
                            material.SetFloat("_EnableRimLighting", 1f);
                            material.SetFloat("_RimStrength", 1f);
                            material.SetFloat("_RimBrightness", 0.4f);
                            material.SetFloat("_RimShadowToggle", 1f);
                            break;
                        case "COLOR_GRADING_HDR":
                            material.SetFloat("_MainColorAdjustToggle", 1f);
                            material.SetFloat("_MainGradationStrength", 0.5f);
                            material.SetFloat("_MainBrightness", 0.5f);
                            break;
                        case "FINALPASS":
                            material.SetFloat("_DetailEnabled", 1f);
                            material.SetFloat("_DetailTexIntensity", 1f);
                            break;
                        case "POSTPROCESS":
                            material.SetFloat("_PostProcess", 1f);
                            material.SetFloat("_PPBrightness", 1.5f);
                            // The mask texture keyword is on when the texture
                            // is assigned, as the drawer sets it.
                            material.EnableKeyword("PROP_PPMASK");
                            break;
                        case "POI_VORONOI":
                            SetVoronoi(material);
                            break;
                        case "POI_BACKFACE":
                            material.SetFloat("_BackFaceEnabled", 1f);
                            // The back face companions show only from behind,
                            // and the back face feature is double sided. Its
                            // vendor note requires Cull Off.
                            material.SetFloat("_Cull", 0f);
                            break;
                        case "POI_PATHING":
                            material.SetFloat("_EnablePathing", 1f);
                            PinPathFill(material);
                            break;
                    }
                    material.EnableKeyword(keyword);
                },
                property);
        }

        /// <summary>
        /// The always on companions move at their default selector 0
        /// with no feature keyword, because their texture assignment
        /// is the gate.
        /// </summary>
        [Test]
        public void AlwaysOnCompanionsShareOnePageSet(
            [Values(false, true)] bool twoPass)
        {
            var ambient = RenderSettings.ambientLight;
            try
            {
                RunGroupCase(
                    twoPass,
                    "always on",
                    (material, textures) =>
                    {
                        // The always on live cases turn every group keyword
                        // on and pin the ambient light. The occlusion factor
                        // multiplies the indirect light, and the default
                        // ambient is black.
                        foreach (var keyword in
                                 PoiyomiAlwaysOnRoutes.AllowedKeywords)
                        {
                            material.EnableKeyword(keyword);
                        }
                        RenderSettings.ambientLight =
                            new Color(0.35f, 0.35f, 0.35f);
                        material.SetTexture(
                            "_BumpMap", textures.Companion("bump"));
                        material.SetFloat("_BumpMapUV", 0f);
                        material.SetTexture(
                            "_LightingAOMaps",
                            textures.Companion("ao"));
                        material.SetFloat("_LightingAOMapsUV", 0f);
                        material.SetTexture(
                            "_LightingShadowMasks",
                            textures.Companion("shadow mask"));
                        material.SetFloat("_LightingShadowMasksUV", 0f);
                        material.EnableKeyword("PROP_LIGHTINGSHADOWMASKS");
                        material.EnableKeyword("PROP_LIGHTINGAOMAPS");
                        // The texture ramp mode multiplies the occlusion into
                        // the shadow term. The multilayer mode does not read it.
                        SetTextureRampLighting(material);
                    },
                    "_BumpMap",
                    "_LightingAOMaps",
                    "_LightingShadowMasks");
            }
            finally
            {
                RenderSettings.ambientLight = ambient;
            }
        }

        /// <summary>
        /// The multilayer math lighting mode pages its six masks and
        /// colors. The border mask toggle turns its sample on. The
        /// fresh flat mode keyword leaves first, because the mode
        /// keywords form one enum.
        /// </summary>
        [Test]
        public void LightingCompanionsShareOnePageSet(
            [Values(false, true)] bool twoPass)
        {
            RunGroupCase(
                twoPass,
                "lighting",
                (material, textures) =>
                {
                    material.SetTexture(
                        "_MultilayerMathBlurMap",
                        textures.Companion("blur"));
                    material.SetTexture(
                        "_ShadowBorderMask",
                        textures.Companion("border"));
                    material.SetFloat("_ShadowBorderMapToggle", 1f);
                    material.SetTexture(
                        "_ShadowColorTex",
                        textures.Companion("shadow color"));
                    material.SetTexture(
                        "_Shadow2ndColorTex",
                        textures.Companion("shadow 2nd"));
                    material.SetTexture(
                        "_Shadow3rdColorTex",
                        textures.Companion("shadow 3rd"));
                    material.SetTexture(
                        "_ShadowStrengthMask",
                        textures.Companion("strength"));
                    SetMultilayerLighting(material);
                },
                "_MultilayerMathBlurMap",
                "_ShadowBorderMask",
                "_ShadowColorTex",
                "_Shadow2ndColorTex",
                "_Shadow3rdColorTex",
                "_ShadowStrengthMask");
        }

        /// <summary>
        /// The matcap mask, the emission pair, and the rim pair of
        /// the master rim style of a fresh material move into
        /// companion pages.
        /// </summary>
        [Test]
        public void MatcapRimEmissionCompanionsShareOnePageSet(
            [Values(false, true)] bool twoPass)
        {
            RunGroupCase(
                twoPass,
                "matcap rim emission",
                (material, textures) =>
                {
                    material.SetTexture(
                        "_MatcapMask", textures.Companion("matcap"));
                    material.SetFloat("_MatcapMaskUV", 0f);
                    material.SetTexture(
                        "_EmissionMap", textures.Companion("emission"));
                    material.SetFloat("_EmissionMapUV", 0f);
                    material.SetTexture(
                        "_EmissionMask", textures.Companion("emission" +
                        " mask"));
                    material.SetFloat("_EmissionMaskUV", 0f);
                    material.SetFloat("_EmissionCenterOutEnabled", 0f);
                    material.SetFloat("_EmissionScrollingUseCurve", 0f);
                    material.SetTexture(
                        "_RimMask", textures.Companion("rim"));
                    material.SetFloat("_RimMaskUV", 0f);
                    material.SetTexture(
                        "_RimTex", textures.Companion("rim tex"));
                    material.SetFloat("_RimTexUV", 0f);
                    material.SetFloat("_MatcapEnable", 1f);
                    material.SetFloat("_EnableEmission", 1f);
                    material.SetFloat("_EnableRimLighting", 1f);
                    SetMultilayerLighting(material);
                    material.EnableKeyword("POI_MATCAP0");
                    material.EnableKeyword("_EMISSION");
                    material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                    material.SetFloat("_EmissionStrength", 1f);
                    material.SetColor("_EmissionColor", Color.white);
                    material.SetFloat("_RimStrength", 1f);
                    material.SetFloat("_RimBrightness", 0.4f);
                    material.SetFloat("_RimShadowToggle", 1f);
                },
                "_MatcapMask",
                "_EmissionMap",
                "_EmissionMask",
                "_RimMask",
                "_RimTex");
        }

        /// <summary>
        /// Six color, effect, and UV features move their companions
        /// into pages. Every condition of the cases holds the safe
        /// value the review names.
        /// </summary>
        [Test]
        public void ColorEffectsCompanionsShareOnePageSet(
            [Values(false, true)] bool twoPass)
        {
            RunGroupCase(
                twoPass,
                "color effects",
                (material, textures) =>
                {
                    material.SetTexture(
                        "_MainColorAdjustTexture",
                        textures.Companion("adjust"));
                    material.SetFloat("_MainColorAdjustTextureUV", 0f);
                    material.SetFloat("_MainBrightness", 1.5f);
                    material.SetTexture(
                        "_DetailTex", textures.Companion("detail"));
                    material.SetFloat("_DetailTexUV", 0f);
                    material.SetTexture(
                        "_DetailMask", textures.Companion("detail" +
                        " mask"));
                    material.SetFloat("_DetailMaskUV", 0f);
                    material.SetTexture(
                        "_DetailNormalMap",
                        textures.Companion("detail normal"));
                    material.SetFloat("_DetailNormalMapUV", 0f);
                    material.SetTexture(
                        "_PPMask", textures.Companion("pp"));
                    material.SetFloat("_PPMaskUV", 0f);
                    material.SetTexture(
                        "_VoronoiMask", textures.Companion("voronoi" +
                        " mask"));
                    material.SetFloat("_VoronoiMaskUV", 0f);
                    material.SetTexture(
                        "_VoronoiNoise", textures.Companion("voronoi" +
                        " noise"));
                    material.SetFloat("_VoronoiNoiseUV", 0f);
                    material.SetTexture(
                        "_BackFaceTexture",
                        textures.Companion("backface"));
                    material.SetFloat("_BackFaceTextureUV", 0f);
                    material.SetFloat("_MainColorAdjustToggle", 1f);
                    material.SetFloat("_MainGradationStrength", 0.5f);
                    SetMultilayerLighting(material);
                    material.SetTexture(
                        "_BackFaceMask", textures.Companion("backface" +
                        " mask"));
                    material.SetFloat("_BackFaceMaskUV", 0f);
                    material.SetTexture(
                        "_PathingMap", textures.Companion("pathing"));
                    material.SetFloat("_PathPointSampling", 1f);
                    material.SetTexture(
                        "_PathingColorMap",
                        textures.Companion("pathing color"));
                    material.SetFloat("_MainColorAdjustToggle", 1f);
                    material.SetFloat("_DetailEnabled", 1f);
                    material.SetFloat("_PostProcess", 1f);
                    material.SetFloat("_VoronoiEnabled", 1f);
                    material.SetFloat("_BackFaceEnabled", 1f);
                    material.SetFloat("_EnablePathing", 1f);
                    PinPathFill(material);
                    material.EnableKeyword("COLOR_GRADING_HDR");
                    material.EnableKeyword("FINALPASS");
                    material.EnableKeyword("POSTPROCESS");
                    material.EnableKeyword("POI_VORONOI");
                    material.SetFloat("_PPBrightness", 1.5f);
                    material.EnableKeyword("PROP_PPMASK");
                    SetVoronoi(material);
                    material.SetFloat("_MainGradationStrength", 0.5f);
                    // The back face companions show only from behind, and
                    // the back face feature is double sided. Its vendor note
                    // requires Cull Off.
                    material.SetFloat("_Cull", 0f);
                    material.EnableKeyword("POI_BACKFACE");
                    material.EnableKeyword("POI_PATHING");
                },
                "_MainColorAdjustTexture",
                "_DetailTex",
                "_DetailMask",
                "_DetailNormalMap",
                "_PPMask",
                "_VoronoiMask",
                "_VoronoiNoise",
                "_BackFaceTexture",
                "_BackFaceMask",
                "_PathingMap",
                "_PathingColorMap");
        }

        /// <summary>
        /// The look of each companion whose feature needs a material
        /// override to show. A look changes floats, colors, or the lighting
        /// mode of the material. No look changes a texture or a UV.
        /// </summary>
        private static readonly Dictionary<string, Action<Material>>
            CompanionLooks = new Dictionary<string, Action<Material>>
            {
                // The detail normal reads in the shade map mode.
                ["_DetailNormalMap"] = SetShadeMapLighting,
                // The blur map reads only when the border map toggle is off.
                ["_MultilayerMathBlurMap"] = material =>
                {
                    material.SetFloat("_ShadowBorderMapToggle", 0f);
                    material.SetFloat("_ShadowBlur", 1f);
                },
                ["_ShadowColorTex"] = BorderMapOff,
                ["_Shadow2ndColorTex"] = BorderMapOff,
                // The path map decides where the path covers the subject. Its
                // fill edge sits in the middle of the map range.
                ["_PathingMap"] = PathingMapLook,
                // The path covers the whole subject, so that the path color
                // map shows on it.
                ["_PathingColorMap"] = PathingColorMapLook,
                // The strength stays off full, so the mask term shows.
                ["_ShadowBorderMask"] = ShadowStrengthLook,
                ["_ShadowStrengthMask"] = ShadowStrengthLook,
            };

        /// <summary>
        /// The companions of the lighting family. Each starts from the base
        /// of the lighting group, so that the single case and the group share
        /// one base.
        /// </summary>
        private static readonly HashSet<string> LightingFamily =
            new HashSet<string>
            {
                "_MultilayerMathBlurMap",
                "_ShadowBorderMask",
                "_ShadowColorTex",
                "_Shadow2ndColorTex",
                "_Shadow3rdColorTex",
                "_ShadowStrengthMask",
            };

        /// <summary>
        /// The dated named exceptions of the sensitivity checks. A key names
        /// the case and the companion, as case|companion. A listed companion
        /// must stay at one level or below. A change above one level fails
        /// the test, so the list gets reviewed.
        /// </summary>
        private static readonly Dictionary<string, string> NamedLookGaps =
            new Dictionary<string, string>
            {
                ["_MultilayerMathBlurMap|_MultilayerMathBlurMap"] =
                    LightingGapReason,
                ["_ShadowBorderMask|_ShadowBorderMask"] =
                    LightingGapReason,
                ["_ShadowStrengthMask|_ShadowStrengthMask"] =
                    LightingGapReason,
                ["_DetailMask|_DetailMask"] = DetailMaskGapReason,
            };

        // The shadow and blur members of the lighting family. The single
        // case shows no change under its look. The lighting group case passes
        // for the same member under the same look.
        private const string LightingGapReason =
            "2026-11-27: the single case shows no change under its look. " +
            "The lighting group passes this member under the same look. " +
            "Gates: MultilayerMath.poiTemplateCollection lines 237, 265, " +
            "293, 308, 316, 325. " +
            "Live proof: VignetteMaskedMembers in PoiyomiLiveValues.cs " +
            "(lines 3927, 3969, 3991) and the VIGNETTE_MASKED oracle in " +
            "PoiyomiSharedLiveValuesRenderTests.cs.";

        // The detail mask of the color effects family. The single case leaves
        // the detail texture at its white default, so the mask has no visible
        // effect there.
        private const string DetailMaskGapReason =
            "2026-11-27: the single case shows a change of 1 level under the " +
            "default check. The color effects group passes this member with a " +
            "detail texture present, and its change is 45 levels in both " +
            "variants. Gates: VRLT_PoiDetails.poiTemplateCollection lines 111 " +
            "and 112. Live proof: none in the shared live oracle. The color " +
            "effects group case proves the visibility and the mapping of this " +
            "member.";

        /// <summary>
        /// The verdict of one sensitivity check. A companion must change the
        /// frame by more than one level. A named gap must stay at one level
        /// or below, and a change above one level fails, so the named list
        /// gets reviewed. Returns null when the check passes.
        /// </summary>
        private static string SensitivityFailure(
            string caseName, string property, int change, string context)
        {
            if (NamedLookGaps.ContainsKey(caseName + "|" + property))
            {
                return change > 1
                    ? property + " now changes by " + change + context +
                      ". Review the named gap"
                    : null;
            }

            return change <= 1
                ? property + " changed by " + change + context
                : null;
        }

        /// <summary>
        /// The sun angle of the looks that need the shadow terminator to
        /// cross the subject. The default angle is the harness framing.
        /// </summary>
        private static readonly Dictionary<string, Vector3> CompanionSuns =
            new Dictionary<string, Vector3>
            {
                ["_ShadowBorderMask"] = new Vector3(60f, 60f, 0f),
                ["_ShadowStrengthMask"] = new Vector3(60f, 60f, 0f),
                ["_MultilayerMathBlurMap"] = new Vector3(60f, 60f, 0f),
            };

        private static Vector3 LookSunOf(string property)
        {
            return CompanionSuns.TryGetValue(property, out var sun)
                ? sun
                : PoiyomiRenderHarness.DefaultSunEuler;
        }

        private static void BorderMapOff(Material material)
        {
            material.SetFloat("_ShadowBorderMapToggle", 0f);
        }

        // The shadow masks multiply the shadow terms only with post AO on
        // and a strength below full, so the look sets both.
        private static void ShadowStrengthLook(Material material)
        {
            material.SetFloat("_ShadowPostAO", 1f);
            material.SetFloat("_ShadowStrength", 0.5f);
        }

        // The path map decides where the path covers the subject. The fill
        // edge sits at the fixed time 0.2, low in the map range. The path then
        // covers only the low part of the map, and the subject stays colored.
        // The map moves the edge on both sides. Full path color alpha lets the
        // path show.
        private static void PathingMapLook(Material material)
        {
            SetPathFill(material, 0.2f);
        }

        // The default white path map is remapped by the range 0 .. 2, so
        // every texel becomes 0.5. The fixed fill time 0.75 is past that
        // value, so the fill covers the whole subject, and the path color map
        // shows on all of it.
        private static void PathingColorMapLook(Material material)
        {
            material.SetFloat("_EnablePathRemapping", 1f);
            var remap = new Vector4(0f, 2f, 0f, 1f);
            material.SetVector("_PathRemapR", remap);
            material.SetVector("_PathRemapG", remap);
            material.SetVector("_PathRemapB", remap);
            material.SetVector("_PathRemapA", remap);
            SetPathFill(material, 0.75f);
        }

        // The fill type at one fixed time and full path color alpha. The look
        // sets floats and colors only.
        private static void SetPathFill(Material material, float time)
        {
            material.SetFloat("_EnablePathing", 1f);
            var fillTime = new Vector4(time, time, time, time);
            material.SetVector("_PathTime", fillTime);
            material.SetFloat("_PathTypeR", 0f);
            material.SetFloat("_PathTypeG", 0f);
            material.SetFloat("_PathTypeB", 0f);
            material.SetFloat("_PathTypeA", 0f);
            var opaque = new Color(1f, 1f, 1f, 1f);
            material.SetColor("_PathColorR", opaque);
            material.SetColor("_PathColorG", opaque);
            material.SetColor("_PathColorB", opaque);
            material.SetColor("_PathColorA", opaque);
        }

        private static bool IsBackFaceCompanion(string property)
        {
            return property.StartsWith("_BackFace", StringComparison.Ordinal);
        }

        private static void SetShadeMapLighting(Material material)
        {
            foreach (var mode in LightingModeKeywords)
            {
                material.DisableKeyword(mode);
            }
            material.SetFloat("_LightingMode", 4f);
            material.EnableKeyword("_LIGHTINGMODE_SHADEMAP");
        }

        // The path types pin to Fill, as the live oracle does. The default
        // Loop type reads time, so the path alpha of a still frame is
        // unreliable.
        private static void PinPathFill(Material material)
        {
            material.SetFloat("_PathTypeR", 0f);
            material.SetFloat("_PathTypeG", 0f);
            material.SetFloat("_PathTypeB", 0f);
            material.SetFloat("_PathTypeA", 0f);
            var pathColor = new Color(1f, 1f, 1f, 0.2f);
            material.SetColor("_PathColorR", pathColor);
            material.SetColor("_PathColorG", pathColor);
            material.SetColor("_PathColorB", pathColor);
            material.SetColor("_PathColorA", pathColor);
        }

        private static void SetTextureRampLighting(Material material)
        {
            foreach (var mode in LightingModeKeywords)
            {
                material.DisableKeyword(mode);
            }
            material.SetFloat("_LightingMode", 0f);
            material.EnableKeyword("_LIGHTINGMODE_TEXTURERAMP");
        }

        private static void SetVoronoi(Material material)
        {
            material.SetFloat("_VoronoiEnabled", 1f);
            material.EnableKeyword("POI_VORONOI");
            material.SetFloat("_VoronoiBlend", 1f);
            material.SetFloat("_VoronoiOuterEmissionStrength", 1f);
            material.SetFloat("_VoronoiInnerEmissionStrength", 1f);
        }
    }
}
