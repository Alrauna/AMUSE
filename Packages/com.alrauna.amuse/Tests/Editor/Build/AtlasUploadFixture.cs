using System;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Creates the Milestone 2 upload fixture avatar under a scratch
    /// folder that git already ignores. The owner then builds and
    /// uploads the avatar as public through the VRChat SDK, and records
    /// whether the page streams in the built bundle and whether VRChat
    /// accepts the page. The fixture holds one renderer with two slots
    /// of the installed Poiyomi main shader. Each slot material binds
    /// one deterministic 4096 by 4096 RGBA32 source with anisotropy
    /// level 16. The mesh maps a UV rectangle of 4064 by 4064 texels
    /// inside each source. The border of a placement is then 16 texels
    /// on each side, so each placement is 4096 by 4096 texels and the
    /// two placements share one page of 8192 by 4096 texels. The item
    /// lives in the test assembly, so it never ships. It writes only
    /// under the scratch folder and deletes a prior fixture first.
    /// </summary>
    internal static class AtlasUploadFixture
    {
        /// <summary>
        /// The scratch folder. The gitignore pattern
        /// `Assets/_PoiyomiShaders*` already ignores it.
        /// </summary>
        internal const string ScratchFolder =
            "Assets/_PoiyomiShadersAtlasUpload";

        internal const int SourceSize = 4096;

        internal const int BorderTexels = 16;

        internal const int RegionTexels = 4064;

        private const int CheckerTexels = 64;

        [MenuItem("AMUSE Tests/Create atlas upload fixture")]
        internal static void Create()
        {
            try
            {
                ResetScratchFolder();
                var sourceA = WriteSource("SourceA", 0);
                var sourceB = WriteSource("SourceB", 1);
                var shader = LoadInstalledPoiyomiShader();
                var materialA = NewMaterial(shader, sourceA, "MaterialA");
                var materialB = NewMaterial(shader, sourceB, "MaterialB");
                AssetDatabase.CreateAsset(
                    materialA, ScratchFolder + "/MaterialA.mat");
                AssetDatabase.CreateAsset(
                    materialB, ScratchFolder + "/MaterialB.mat");
                var mesh = NewQuadPairMesh();
                AssetDatabase.CreateAsset(
                    mesh, ScratchFolder + "/QuadPair.asset");
                var root = NewAvatarRoot(
                    new[] { materialA, materialB }, mesh);
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        ScratchFolder + "/AtlasUploadFixtureAvatar.prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    "AMUSE atlas upload fixture created under " +
                    ScratchFolder + ". Two 4096 sources, a 4064 texel UV" +
                    " rectangle each, one shared page of 8192 by 4096" +
                    " texels after the 16 texel border.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ResetScratchFolder()
        {
            if (AssetDatabase.IsValidFolder(ScratchFolder))
            {
                AssetDatabase.DeleteAsset(ScratchFolder);
            }

            AssetDatabase.CreateFolder(
                "Assets", ScratchFolder.Substring("Assets/".Length));
        }

        /// <summary>
        /// One deterministic source: a checker of 64 texel blocks in
        /// two material specific gray pairs, fully opaque. The phase
        /// offset tells the two sources apart on the shared page.
        /// </summary>
        private static Texture2D WriteSource(string name, int phase)
        {
            var pixels = new Color32[SourceSize * SourceSize];
            var light = phase == 0
                ? new Color32(235, 235, 240, 255)
                : new Color32(240, 233, 222, 255);
            var dark = phase == 0
                ? new Color32(35, 35, 40, 255)
                : new Color32(42, 35, 28, 255);
            for (var y = 0; y < SourceSize; y++)
            {
                for (var x = 0; x < SourceSize; x++)
                {
                    var cell =
                        (x / CheckerTexels + y / CheckerTexels + phase) % 2;
                    pixels[y * SourceSize + x] = cell == 0 ? light : dark;
                }
            }

            return TestTextureImport.WritePng(
                ScratchFolder + "/" + name + ".png",
                SourceSize,
                SourceSize,
                pixels,
                importer =>
                {
                    importer.maxTextureSize = SourceSize;
                    importer.mipmapEnabled = true;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.anisoLevel = BorderTexels;
                });
        }

        /// <summary>
        /// Loads the installed bundled Poiyomi Toon shader by its pinned
        /// GUID, never by name, so a stand-in cannot answer first.
        /// </summary>
        private static Shader LoadInstalledPoiyomiShader()
        {
            var path = AssetDatabase.GUIDToAssetPath(
                PoiyomiSourceAttestation.CanonicalShaderGuid);
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException(
                    "The installed bundled Poiyomi Toon shader did not" +
                    " resolve by its pinned GUID.");
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The installed bundled Poiyomi Toon shader at " +
                    path + " did not load.");
            }

            return shader;
        }

        /// <summary>
        /// One plain material of the row. Both materials differ only in
        /// their name and their member texture, so their serialized
        /// facts hold the same values and the two uses share one group.
        /// </summary>
        private static Material NewMaterial(
            Shader shader,
            Texture2D source,
            string name)
        {
            var material = new Material(shader) { name = name };
            material.SetTexture("_MainTex", source);
            return material;
        }

        /// <summary>
        /// Two quads in one mesh, one submesh each. Both quads map the
        /// same inset UV rectangle of 4064 by 4064 texels, so each
        /// region keeps a 16 texel margin on every side of its source.
        /// </summary>
        private static Mesh NewQuadPairMesh()
        {
            var min = BorderTexels / (float)SourceSize;
            var max = (SourceSize - BorderTexels) / (float)SourceSize;
            var mesh = new Mesh
            {
                name = "AtlasUploadQuadPair",
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f),
                    new Vector3(1f, 1f, 0f),
                    new Vector3(2f, 0f, 0f),
                    new Vector3(3f, 0f, 0f),
                    new Vector3(2f, 1f, 0f),
                    new Vector3(3f, 1f, 0f),
                },
                uv = new[]
                {
                    new Vector2(min, min),
                    new Vector2(max, min),
                    new Vector2(min, max),
                    new Vector2(max, max),
                    new Vector2(min, min),
                    new Vector2(max, min),
                    new Vector2(min, max),
                    new Vector2(max, max),
                },
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.SetTriangles(new[] { 4, 6, 5, 6, 7, 5 }, 1);
            return mesh;
        }

        private static GameObject NewAvatarRoot(
            Material[] materials,
            Mesh mesh)
        {
            var root = new GameObject("AMUSE Atlas Upload Fixture");
            root.AddComponent<Animator>();
            var component =
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            var serialized = new SerializedObject(component);
            var alphaSwitch =
                serialized.FindProperty("_alphaSeparatorEnabled");
            if (alphaSwitch == null)
            {
                throw new InvalidOperationException(
                    "AmuseAvatarOptimizer._alphaSeparatorEnabled field pin" +
                    " failed.");
            }

            alphaSwitch.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
#if AMUSE_VRCSDK3_AVATARS
            root.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
#endif
            var body = new GameObject("AtlasUploadBody");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            body.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return root;
        }
    }
}
