using System;
using System.Collections;
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
using UnityEngine.TestTools;

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
    /// two frames must stay at one of 255 or less. The render meshes
    /// carry one fixed rotation, so the subject faces the harness
    /// sun and the comparison cannot pass blind.
    /// </summary>
    public sealed class PoiyomiReviewedAtlasEndToEndTests
    {
        private const string TempFolder =
            "Assets/AmuseTests_PoiyomiReviewedEndToEnd";

        /// <summary>
        /// The fixed rotation of the render meshes. The subject quad
        /// starts in the XY plane with its normal on Z. The rotation
        /// turns the normal toward the harness sun and camera, so a
        /// black frame fails the visibility guard instead of passing
        /// the comparison.
        /// </summary>
        private static readonly Quaternion RenderRotation =
            Quaternion.Euler(20f, -60f, 0f);

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
        /// One mipmapped RGBA32 source with a checker of 16 texel
        /// blocks. Bilinear filtering and no anisotropy keep the
        /// render comparison stable.
        /// </summary>
        private static Texture2D ImportPatternTexture(
            string name, Color32 even, Color32 odd, int size = 64)
        {
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    pixels[y * size + x] =
                        ((x / 16 + y / 16) % 2 == 0) ? even : odd;
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
                normals = new[]
                {
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                    Vector3.forward,
                },
                tangents = new[]
                {
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
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
        /// One copy of the mesh with every position, normal, and
        /// tangent turned by the fixed render rotation. The UVs stay,
        /// so the source frame samples the unit square and the build
        /// frame samples the remapped route of the output mesh.
        /// </summary>
        private static Mesh NewRenderMesh(Mesh source)
        {
            var render = new Mesh();
            var vertices = source.vertices;
            var rotated = new Vector3[vertices.Length];
            for (var index = 0; index < vertices.Length; index++)
            {
                rotated[index] = RenderRotation * vertices[index];
            }

            render.vertices = rotated;

            var normals = source.normals;
            if (normals == null || normals.Length == 0)
            {
                normals = new Vector3[vertices.Length];
                for (var index = 0; index < normals.Length; index++)
                {
                    normals[index] = Vector3.forward;
                }
            }

            var rotatedNormals = new Vector3[normals.Length];
            for (var index = 0; index < normals.Length; index++)
            {
                rotatedNormals[index] = RenderRotation * normals[index];
            }

            render.normals = rotatedNormals;

            var tangents = source.tangents;
            if (tangents != null && tangents.Length > 0)
            {
                var rotatedTangents = new Vector4[tangents.Length];
                for (var index = 0; index < tangents.Length; index++)
                {
                    var turned = RenderRotation * new Vector3(
                        tangents[index].x,
                        tangents[index].y,
                        tangents[index].z);
                    rotatedTangents[index] = new Vector4(
                        turned.x, turned.y, turned.z,
                        tangents[index].w);
                }

                render.tangents = rotatedTangents;
            }

            render.uv = source.uv;
            render.triangles = source.triangles;
            return render;
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

        private static string DifferenceMap(Color32[] before, Color32[] after)
        {
            var size = (int)Math.Sqrt(before.Length);
            var count = 0;
            var minX = size;
            var minY = size;
            var maxX = -1;
            var maxY = -1;
            for (var i = 0; i < before.Length; i++)
            {
                if (before[i].Equals(after[i])) continue;
                count++;
                var x = i % size;
                var y = i / size;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
            return count + " of " + before.Length + " pixels differ. Bounds " +
                minX + "," + minY + " to " + maxX + "," + maxY;
        }

        private static string MaterialDifferences(Material source, Material page)
        {
            var differences = new List<string>();
            if (source.shader != page.shader) differences.Add("shader");
            if (source.renderQueue != page.renderQueue) differences.Add("render queue");
            if (!source.shaderKeywords.OrderBy(x => x).SequenceEqual(
                    page.shaderKeywords.OrderBy(x => x)))
                differences.Add("keywords");
            for (var i = 0; i < source.shader.passCount; i++)
            {
                var pass = source.GetPassName(i);
                if (source.GetShaderPassEnabled(pass) != page.GetShaderPassEnabled(pass))
                    differences.Add("pass " + pass);
            }
            for (var i = 0; i < source.shader.GetPropertyCount(); i++)
            {
                var name = source.shader.GetPropertyName(i);
                var type = source.shader.GetPropertyType(i);
                switch (type)
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        if (source.GetTexture(name) != page.GetTexture(name))
                            differences.Add("texture " + name);
                        if (source.GetTextureScale(name) != page.GetTextureScale(name) ||
                            source.GetTextureOffset(name) != page.GetTextureOffset(name))
                            differences.Add("texture transform " + name);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        if (source.GetColor(name) != page.GetColor(name))
                            differences.Add("color " + name);
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        if (source.GetVector(name) != page.GetVector(name))
                            differences.Add("vector " + name);
                        break;
                    default:
                        if (source.GetFloat(name) != page.GetFloat(name))
                            differences.Add("scalar " + name + " " +
                                source.GetFloat(name) + " to " + page.GetFloat(name));
                        break;
                }
            }
            using var first = new SerializedObject(source);
            using var second = new SerializedObject(page);
            var firstInts = first.FindProperty("m_SavedProperties.m_Ints");
            var secondInts = second.FindProperty("m_SavedProperties.m_Ints");
            if (firstInts != null && secondInts != null)
            {
                var a = new Dictionary<string, int>();
                var b = new Dictionary<string, int>();
                for (var i = 0; i < firstInts.arraySize; i++)
                {
                    var item = firstInts.GetArrayElementAtIndex(i);
                    a[item.FindPropertyRelative("first").stringValue] =
                        item.FindPropertyRelative("second").intValue;
                }
                for (var i = 0; i < secondInts.arraySize; i++)
                {
                    var item = secondInts.GetArrayElementAtIndex(i);
                    b[item.FindPropertyRelative("first").stringValue] =
                        item.FindPropertyRelative("second").intValue;
                }
                foreach (var name in a.Keys.Union(b.Keys))
                    if (!a.TryGetValue(name, out var av) ||
                        !b.TryGetValue(name, out var bv) || av != bv)
                        differences.Add("saved int " + name);
            }
            return string.Join(", ", differences);
        }

        private static string YamlChanges(byte[] before, byte[] after)
        {
            var oldLines = System.Text.Encoding.UTF8.GetString(before).Split('\n');
            var newLines = System.Text.Encoding.UTF8.GetString(after).Split('\n');
            var section = "";
            var keywordSection = "";
            foreach (var line in oldLines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("m_") && trimmed.EndsWith(":"))
                    section = trimmed;
                if (trimmed == "- POI_MATCAP0")
                    keywordSection = section;
            }
            string Sanitize(string line)
            {
                line = System.Text.RegularExpressions.Regex.Replace(
                    line, @"\b[0-9a-fA-F]{32}\b", "<identifier>");
                return System.Text.RegularExpressions.Regex.Replace(
                    line, @"fileID:\s*-?\d+", "fileID: <identifier>").Trim();
            }
            return "keyword section " + keywordSection +
                ". Removed " + string.Join(" | ", oldLines.Except(newLines).Select(Sanitize)) +
                ". Added " + string.Join(" | ", newLines.Except(oldLines).Select(Sanitize));
        }


        private static Color32[] ReadTexture(Texture texture)
        {
            var target = RenderTexture.GetTemporary(
                texture.width, texture.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previous = RenderTexture.active;
            var readback = new Texture2D(
                texture.width, texture.height, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(texture, target);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readback.Apply();
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static string ComparePageTexels(
            Texture source, Texture page, Vector2[] uv)
        {
            var sourcePixels = ReadTexture(source);
            var pagePixels = ReadTexture(page);
            var different = 0;
            var first = "";
            for (var y = 0; y < source.height; y++)
            {
                for (var x = 0; x < source.width; x++)
                {
                    var u = Mathf.Lerp(uv[0].x, uv[3].x, (x + 0.5f) / source.width);
                    var v = Mathf.Lerp(uv[0].y, uv[3].y, (y + 0.5f) / source.height);
                    var actual = pagePixels[Mathf.FloorToInt(v * page.height) * page.width +
                        Mathf.FloorToInt(u * page.width)];
                    var expected = sourcePixels[y * source.width + x];
                    if (actual.Equals(expected)) continue;
                    different++;
                    if (first.Length == 0)
                    {
                        first = " First source " + expected + " page " + actual;
                    }
                }
            }
            return different + " of " + sourcePixels.Length + " texels differ." + first;
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
        /// The shared body of one group case. It assigns the
        /// companion textures and the keywords through the setup
        /// action on both materials, builds the avatar, asserts the
        /// one shared page set with its named pages, and proves that
        /// each built slot renders as its source did.
        /// </summary>
        private void RunGroupCase(
            bool twoPass,
            string caseName,
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
                var assetPath = caseName == "asset matcap mask"
                    ? TempFolder + "/matcap probe.mat" : null;
                var secondAssetPath = assetPath == null
                    ? null : TempFolder + "/matcap probe second.mat";
                byte[] assetBefore = null;
                byte[] secondAssetBefore = null;
                if (assetPath != null)
                {
                    firstMaterial = new Material(firstMaterial);
                    AssetDatabase.CreateAsset(firstMaterial, assetPath);
                    secondMaterial = new Material(secondMaterial);
                    AssetDatabase.CreateAsset(secondMaterial, secondAssetPath);
                    assetBefore = File.ReadAllBytes(assetPath);
                    secondAssetBefore = File.ReadAllBytes(secondAssetPath);
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

                var firstMesh = Track(NewQuadMesh());
                var secondMesh = Track(NewQuadMesh());
                var firstRenderer = NewQuadRenderer(
                    root, caseName + " first", firstMesh, firstMaterial);
                var secondRenderer = NewQuadRenderer(
                    root, caseName + " second", secondMesh,
                    secondMaterial);

                using var harness = new PoiyomiRenderHarness();
                harness.FrameUnitQuad(RenderRotation);
                // The harness ambient sits at black for the toon
                // light clamp. The dim grey keeps the shaded base
                // color visible in the oracle frame.
                RenderSettings.ambientLight =
                    new Color(0.35f, 0.35f, 0.35f);
                var firstRenderMesh = Track(NewRenderMesh(firstMesh));
                var secondRenderMesh = Track(NewRenderMesh(secondMesh));
                // The first render of each row compiles the variants.
                harness.Render(firstMaterial, firstRenderMesh);
                harness.Render(secondMaterial, secondRenderMesh);
                var firstBefore =
                    harness.Render(firstMaterial, firstRenderMesh);
                var secondBefore =
                    harness.Render(secondMaterial, secondRenderMesh);
                AssertFrameShowsTheSubject(
                    firstBefore, caseName + " first source");
                AssertFrameShowsTheSubject(
                    secondBefore, caseName + " second source");
                var materialBefore = EditorJsonUtility.ToJson(firstMaterial);
                var keywordsBefore = firstMaterial.shaderKeywords;
                var positionsBefore = firstMesh.vertices;
                var normalsBefore = firstMesh.normals;
                var uvBefore = firstMesh.uv;
                var ambientBefore = RenderSettings.ambientLight;
                var ambientModeBefore = RenderSettings.ambientMode;
                var ambientProbeBefore = RenderSettings.ambientProbe;
                var reflectionBefore = Shader.GetGlobalTexture("unity_SpecCube0");
                var colorSpaceBefore = QualitySettings.activeColorSpace;
                var activeTextureBefore = RenderTexture.active;
                var audioBefore = Shader.GetGlobalTexture("_AudioTexture");
                var bloomBefore = Shader.GetGlobalTexture("_BloomPrePassTexture");
                var depthBefore = Shader.GetGlobalTexture("_CameraDepthTexture");
                var timeBefore = Shader.GetGlobalVector("_Time");
                var audioSizeBefore = Shader.GetGlobalVector("_AudioTexture_TexelSize");
                var lightsBefore = UnityEngine.Object.FindObjectsOfType<Light>().Length;
                var camerasBefore = UnityEngine.Object.FindObjectsOfType<Camera>().Length;
                var objectsBefore = UnityEngine.Object.FindObjectsOfType<GameObject>().Length;
                if (companionProperties.Length == 1 &&
                    companionProperties[0] == "_MatcapMask")
                {
                    var stable = harness.Render(firstMaterial, firstRenderMesh);
                    Assert.That(LargestChannelDifference(firstBefore, stable), Is.Zero,
                        "The source render must stay stable before the build");
                }

                if (assetPath != null)
                {
                    AssetDatabase.SaveAssetIfDirty(firstMaterial);
                    AssetDatabase.SaveAssetIfDirty(secondMaterial);
                    Assert.That(EditorUtility.IsDirty(firstMaterial), Is.False,
                        "The first material must be saved before the build");
                    Assert.That(EditorUtility.IsDirty(secondMaterial), Is.False,
                        "The second material must be saved before the build");
                    Assert.That(firstMaterial.IsKeywordEnabled("POI_MATCAP0"), Is.True,
                        "The first saved material must keep the probe keyword");
                    Assert.That(secondMaterial.IsKeywordEnabled("POI_MATCAP0"), Is.True,
                        "The second saved material must keep the probe keyword");
                    var savedFirst = capture.CaptureSingle(firstMaterial).Use;
                    Assert.That(firstMaterial.IsKeywordEnabled("POI_MATCAP0") &&
                        secondMaterial.IsKeywordEnabled("POI_MATCAP0") &&
                        !EditorUtility.IsDirty(firstMaterial) &&
                        !EditorUtility.IsDirty(secondMaterial), Is.True,
                        "The first saved capture changed the asset state");
                    var savedSecond = capture.CaptureSingle(secondMaterial).Use;
                    Assert.That(firstMaterial.IsKeywordEnabled("POI_MATCAP0") &&
                        secondMaterial.IsKeywordEnabled("POI_MATCAP0") &&
                        !EditorUtility.IsDirty(firstMaterial) &&
                        !EditorUtility.IsDirty(secondMaterial), Is.True,
                        "The second saved capture changed the asset state");
                    Assert.That(savedFirst, Is.Not.Null);
                    Assert.That(savedSecond, Is.Not.Null);
                    var savedFirstParts = savedFirst.MaterialKey.Split(';');
                    var savedSecondParts = savedSecond.MaterialKey.Split(';');
                    var changedFields = savedFirstParts.Except(savedSecondParts)
                        .Concat(savedSecondParts.Except(savedFirstParts))
                        .Select(part => part.Split('=')[0]).Distinct();
                    Assert.That(savedFirst.MaterialKey == savedSecond.MaterialKey,
                        Is.True, "Saved material key fields differ: " +
                        string.Join(", ", changedFields));
                    Assert.That(savedFirst.RouteSignature,
                        Is.EqualTo(savedSecond.RouteSignature));
                    assetBefore = File.ReadAllBytes(assetPath);
                    secondAssetBefore = File.ReadAllBytes(secondAssetPath);
                }
                var originalIdentity = firstMaterial.GetInstanceID();
                var context = Build(root);
                state = context.GetState<AmusePlatformFinishState>();
                var assetChanged = assetPath != null &&
                    (!assetBefore.SequenceEqual(File.ReadAllBytes(assetPath)) ||
                     !secondAssetBefore.SequenceEqual(File.ReadAllBytes(secondAssetPath)));
                if (assetPath != null)
                {
                    var firstKeyword = firstMaterial.IsKeywordEnabled("POI_MATCAP0");
                    var secondKeyword = secondMaterial.IsKeywordEnabled("POI_MATCAP0");
                    var firstDirty = EditorUtility.IsDirty(firstMaterial);
                    var secondDirty = EditorUtility.IsDirty(secondMaterial);
                    Assert.That(!assetChanged && firstKeyword && secondKeyword &&
                        !firstDirty && !secondDirty, Is.True,
                        "The source asset state changed. Disk: " + assetChanged +
                        ". Keywords: " + firstKeyword + ", " + secondKeyword +
                        ". Dirty: " + firstDirty + ", " + secondDirty +
                        ". First YAML changes: " +
                        YamlChanges(assetBefore, File.ReadAllBytes(assetPath)) +
                        ". Second YAML changes: " +
                        YamlChanges(secondAssetBefore, File.ReadAllBytes(secondAssetPath)));
                }
                var materialAfter = EditorJsonUtility.ToJson(firstMaterial);
                var globalsChanged = new List<string>();
                if (ambientBefore != RenderSettings.ambientLight) globalsChanged.Add("ambient color");
                if (ambientModeBefore != RenderSettings.ambientMode) globalsChanged.Add("ambient mode");
                if (!ambientProbeBefore.Equals(RenderSettings.ambientProbe)) globalsChanged.Add("ambient probe");
                if (reflectionBefore != Shader.GetGlobalTexture("unity_SpecCube0")) globalsChanged.Add("reflection texture");
                if (colorSpaceBefore != QualitySettings.activeColorSpace) globalsChanged.Add("color space");
                if (activeTextureBefore != RenderTexture.active) globalsChanged.Add("active render texture");
                if (audioBefore != Shader.GetGlobalTexture("_AudioTexture")) globalsChanged.Add("audio texture");
                if (bloomBefore != Shader.GetGlobalTexture("_BloomPrePassTexture")) globalsChanged.Add("bloom texture");
                if (depthBefore != Shader.GetGlobalTexture("_CameraDepthTexture")) globalsChanged.Add("depth texture");
                if (timeBefore != Shader.GetGlobalVector("_Time")) globalsChanged.Add("time");
                if (audioSizeBefore != Shader.GetGlobalVector("_AudioTexture_TexelSize")) globalsChanged.Add("audio texture size");
                if (lightsBefore != UnityEngine.Object.FindObjectsOfType<Light>().Length) globalsChanged.Add("light count");
                if (camerasBefore != UnityEngine.Object.FindObjectsOfType<Camera>().Length) globalsChanged.Add("camera count");
                if (objectsBefore != UnityEngine.Object.FindObjectsOfType<GameObject>().Length) globalsChanged.Add("object count");

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

                var firstBuildRender =
                    Track(NewRenderMesh(firstOutput));
                var secondBuildRender =
                    Track(NewRenderMesh(secondOutput));
                harness.Render(pageMaterial, firstBuildRender);
                harness.Render(pageMaterial, secondBuildRender);
                var firstAfter =
                    harness.Render(pageMaterial, firstBuildRender);
                var secondAfter =
                    harness.Render(pageMaterial, secondBuildRender);
                AssertFrameShowsTheSubject(
                    firstAfter, caseName + " first build");
                AssertFrameShowsTheSubject(
                    secondAfter, caseName + " second build");
                var routeFacts = string.Join(" | ",
                    companionProperties.Select(property =>
                    {
                        var source = firstMaterial.GetTexture(property);
                        var page = pageMaterial.GetTexture(property);
                        return property + " selector " +
                            firstMaterial.GetFloat(property + "UV") +
                            " source scale " + firstMaterial.GetTextureScale(property) +
                            " source offset " + firstMaterial.GetTextureOffset(property) +
                            " page scale " + pageMaterial.GetTextureScale(property) +
                            " page offset " + pageMaterial.GetTextureOffset(property) +
                            " source format " + source.graphicsFormat +
                            " page format " + page.graphicsFormat +
                            " source sampler " + source.filterMode + "/" + source.wrapMode +
                            " page sampler " + page.filterMode + "/" + page.wrapMode;
                    }));
                var outputUv = firstOutput.uv;
                var evidence = " Routes: " + routeFacts +
                    ". Output UV: " + string.Join(", ", outputUv);
                if (companionProperties.Length == 1 &&
                    companionProperties[0] == "_MatcapMask" &&
                    LargestChannelDifference(firstBefore, firstAfter) > 1)
                {
                    harness.Render(firstMaterial, firstRenderMesh);
                    var sourceAfter = harness.Render(firstMaterial, firstRenderMesh);
                    var copy = Track(new Material(firstMaterial));
                    harness.Render(copy, firstRenderMesh);
                    var copyAfter = harness.Render(copy, firstRenderMesh);
                    var alphaChanges = firstBefore.Zip(firstAfter,
                        (before, after) => before.a != after.a).Count(changed => changed);
                    evidence += ". Material differences: " +
                        MaterialDifferences(firstMaterial, pageMaterial) +
                        ". Source before and after build: " +
                        DifferenceMap(firstBefore, sourceAfter) +
                        ". Copy after build: " + DifferenceMap(sourceAfter, copyAfter) +
                        ". Original material serialized state changed: " +
                        (materialBefore != materialAfter) +
                        ". Original keywords removed: " +
                        string.Join(", ", keywordsBefore.Except(firstMaterial.shaderKeywords)) +
                        ". Original keywords added: " +
                        string.Join(", ", firstMaterial.shaderKeywords.Except(keywordsBefore)) +
                        ". Original positions changed: " +
                        !positionsBefore.SequenceEqual(firstMesh.vertices) +
                        ". Original normals changed: " +
                        !normalsBefore.SequenceEqual(firstMesh.normals) +
                        ". Original UV changed: " +
                        !uvBefore.SequenceEqual(firstMesh.uv) +
                        ". Globals changed: " + string.Join(", ", globalsChanged) +
                        ". Renderer uses page: " + ReferenceEquals(firstRenderer.sharedMaterial, pageMaterial) +
                        ". Input object is unchanged: " + (firstMaterial.GetInstanceID() == originalIdentity) +
                        ". Asset bytes changed: " + assetChanged +
                        ". Alpha changed in " + alphaChanges + " pixels" +
                        ". First pixel source " + firstBefore[0] +
                        " build " + firstAfter[0] +
                        ". Second output UV: " + string.Join(", ", secondOutput.uv);
                    evidence += ". " + DifferenceMap(firstBefore, firstAfter) +
                        ". Mask channel " + firstMaterial.GetFloat("_MatcapMaskChannel") +
                        ". Mask invert " + firstMaterial.GetFloat("_MatcapMaskInvert") +
                        ". " + ComparePageTexels(
                            firstMaterial.GetTexture("_MatcapMask"),
                            pageMaterial.GetTexture("_MatcapMask"), outputUv);
                }


                Assert.That(
                    LargestChannelDifference(firstBefore, firstAfter),
                    Is.LessThanOrEqualTo(1),
                    "the built first slot must render as its source" +
                    " did." + evidence);
                Assert.That(
                    LargestChannelDifference(
                        secondBefore, secondAfter),
                    Is.LessThanOrEqualTo(1),
                    "the built second slot must render as its" +
                    " source did");
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

            internal Texture2D Companion(string name)
            {
                if (!companions.TryGetValue(name, out var texture))
                {
                    texture = ImportPatternTexture(
                        prefix + " " + name,
                        new Color32(30, 160, 90, 255),
                        new Color32(200, 170, 30, 255));
                    companions.Add(name, texture);
                }

                return texture;
            }

            internal Texture2D ConstantMask()
            {
                const string name = "constant mask";
                if (!companions.TryGetValue(name, out var texture))
                {
                    var gray = new Color32(128, 128, 128, 128);
                    texture = ImportPatternTexture(prefix + " " + name, gray, gray);
                    companions.Add(name, texture);
                }
                return texture;
            }
        }

        // --- the four group cases ----------------------------------

        [Test]
        public void MainTextureOnlySharesOnePageSet(
            [Values(false, true)] bool twoPass)
        {
            RunGroupCase(twoPass, "main only", (material, textures) => { });
        }

        [UnityTest]
        public IEnumerator SourceAcquisitionKeepsLoadedMaterialKeywords() =>
            ProbeSourceAcquisition(false);

        [UnityTest]
        public IEnumerator SourceAcquisitionKeepsBoundMaterialKeywords() =>
            ProbeSourceAcquisition(true);

        [UnityTest]
        public IEnumerator BoundAcquisitionWithNoSelectionKeepsKeywords() =>
            ProbeSourceAcquisition(true, 0);

        [UnityTest]
        public IEnumerator BoundAcquisitionWithSelectedRendererKeepsKeywords() =>
            ProbeSourceAcquisition(true, 1);

        [UnityTest]
        public IEnumerator UnrelatedAcquisitionKeepsBoundMaterialKeywords() =>
            ProbeSourceAcquisition(true, 0, true);

        [UnityTest]
        public IEnumerator BoundWithoutAcquisitionKeepsKeywords() =>
            ProbeSourceAcquisition(true, 0, false, false);

        private IEnumerator ProbeSourceAcquisition(
            bool bind, int selection = -1, bool unrelated = false,
            bool acquire = true)
        {
            var source = ImportPatternTexture(
                "acquisition probe", new Color32(30, 160, 90, 255),
                new Color32(200, 170, 30, 255));
            var acquired = unrelated
                ? ImportPatternTexture("unrelated probe",
                    new Color32(60, 80, 100, 255),
                    new Color32(120, 140, 160, 255))
                : source;
            var material = Track(new Material(LoadInstalledPoiyomiShader(false)));
            var previousSelection = Selection.objects;
            GameObject selectedRenderer = null;
            if (bind)
            {
                var root = Track(new GameObject("Acquisition probe"));
                var mesh = Track(NewQuadMesh());
                selectedRenderer = NewQuadRenderer(root, "Subject", mesh, material).gameObject;
                material.SetTexture("_MatcapMask", source);
            }
            if (selection == 0) Selection.activeObject = null;
            if (selection == 1) Selection.activeObject = selectedRenderer;
            try
            {
            material.SetFloat("_MatcapEnable", 1f);
            material.EnableKeyword("POI_MATCAP0");
            Assert.That(material.IsKeywordEnabled("POI_MATCAP0"), Is.True);
            var beforeDelayCallbacks = EditorCallbacks("delayCall");
            var beforeUpdateCallbacks = EditorCallbacks("update");
            if (acquire)
                Assert.That(UnityStreamingTextureEvidence.TryAcquireAtlasSource(
                    acquired, 1, out _, out _), Is.True);
            var immediately = material.IsKeywordEnabled("POI_MATCAP0");
            var delayCallbacks = EditorCallbacks("delayCall");
            var updateCallbacks = EditorCallbacks("update");
            var delayed = false;
            EditorApplication.delayCall += () => delayed = true;
            while (!delayed) yield return null;
            var afterDelay = material.IsKeywordEnabled("POI_MATCAP0");
            Assert.That(immediately && afterDelay, Is.True,
                "The keyword after acquisition was " + immediately +
                ". After the editor update it was " + afterDelay +
                ". New delay callbacks: " +
                string.Join(", ", delayCallbacks.Except(beforeDelayCallbacks)) +
                ". New update callbacks: " +
                string.Join(", ", updateCallbacks.Except(beforeUpdateCallbacks)) +
                ". Delay callbacks: " + string.Join(", ", delayCallbacks));
            }
            finally
            {
                Selection.objects = previousSelection;
            }
        }

        private static string[] EditorCallbacks(string fieldName)
        {
            var field = typeof(EditorApplication).GetField(fieldName,
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            var callbacks = field?.GetValue(null) as Delegate;
            return callbacks == null ? Array.Empty<string>() :
                callbacks.GetInvocationList().Select(callback =>
                    callback.Method.DeclaringType?.FullName + "." +
                    callback.Method.Name).ToArray();
        }

        [Test]
        public void ConstantMatcapMaskRendersLikeItsSource()
        {
            RunGroupCase(
                false, "constant matcap mask",
                (material, textures) =>
                {
                    material.SetTexture("_MatcapMask", textures.ConstantMask());
                    material.SetFloat("_MatcapEnable", 1f);
                    material.EnableKeyword("POI_MATCAP0");
                },
                "_MatcapMask");
        }

        [Test]
        public void AssetMatcapMaskRendersLikeItsSource()
        {
            RunGroupCase(
                false, "asset matcap mask",
                (material, textures) =>
                {
                    material.SetTexture("_MatcapMask", textures.ConstantMask());
                    material.SetFloat("_MatcapEnable", 1f);
                    material.EnableKeyword("POI_MATCAP0");
                },
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
                    material.SetFloat("_PathPointSampling", 1f);
                    material.SetFloat("_ColorGradingToggle", 1f);
                    if (keyword.StartsWith("_LIGHTINGMODE_"))
                    {
                        material.SetFloat("_LightingMode", 1f);
                        foreach (var mode in LightingModeKeywords)
                        {
                            material.DisableKeyword(mode);
                        }
                    }
                    switch (keyword)
                    {
                        case "POI_MATCAP0": material.SetFloat("_MatcapEnable", 1f); break;
                        case "_EMISSION": material.SetFloat("_EnableEmission", 1f); break;
                        case "_GLOSSYREFLECTIONS_OFF": material.SetFloat("_EnableRimLighting", 1f); break;
                        case "COLOR_GRADING_HDR": material.SetFloat("_MainColorAdjustToggle", 1f); break;
                        case "FINALPASS":
                            material.SetFloat("_DetailEnabled", 1f);
                            material.SetFloat("_DetailTexIntensity", 0.25f);
                            break;
                        case "POSTPROCESS": material.SetFloat("_PostProcess", 1f); break;
                        case "POI_VORONOI":
                            material.SetFloat("_VoronoiEnabled", 1f);
                            material.SetFloat("_VoronoiBlend", 1f);
                            break;
                        case "POI_BACKFACE": material.SetFloat("_BackFaceEnabled", 1f); break;
                        case "POI_PATHING":
                            material.SetFloat("_EnablePathing", 1f);
                            var pathColor = new Color(1f, 1f, 1f, 0.2f);
                            material.SetColor("_PathColorR", pathColor);
                            material.SetColor("_PathColorG", pathColor);
                            material.SetColor("_PathColorB", pathColor);
                            material.SetColor("_PathColorA", pathColor);
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
            RunGroupCase(
                twoPass,
                "always on",
                (material, textures) =>
                {
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
                },
                "_BumpMap",
                "_LightingAOMaps",
                "_LightingShadowMasks");
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
                    foreach (var mode in LightingModeKeywords)
                    {
                        material.DisableKeyword(mode);
                    }
                    material.SetFloat("_LightingMode", 1f);
                    material.EnableKeyword("_LIGHTINGMODE_MULTILAYER_MATH");
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
                    material.EnableKeyword("POI_MATCAP0");
                    material.EnableKeyword("_EMISSION");
                    material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
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
                    material.EnableKeyword("COLOR_GRADING_HDR");
                    material.EnableKeyword("FINALPASS");
                    material.EnableKeyword("POSTPROCESS");
                    material.EnableKeyword("POI_VORONOI");
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
        /// The mode keywords of the lighting mode enum. A material
        /// with two holds no compiled variant, so the lighting case
        /// turns every mode off before it turns its one mode on.
        /// </summary>
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
    }
}
