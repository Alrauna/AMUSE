using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The real SDK-sequence comparison for d4rkAvatarOptimizer, executed
    /// through the installed VRCSDK callback dispatcher exactly as an
    /// avatar build drives it. Avatar Optimizer is absent in the DAO-only
    /// case and present in the combined case, so the two runs bracket the
    /// real combined pipeline.
    /// <para>
    /// These tests refuse to run unless the environment variable
    /// <c>AMUSE_DAO_INTEGRATION=1</c> is set. d4rkAvatarOptimizer writes
    /// and deletes project-level output under Assets, so they are allowed
    /// only in a disposable public integration project - never in a
    /// development or private project that happens to have the package.
    /// </para>
    /// <para>
    /// Ordering note: without Modular Avatar, d4rkAvatarOptimizer's
    /// preprocess callback and NDMF's optimizing hook share callback order
    /// -1025, so the DAO-versus-AMUSE order is undefined by the SDK. The
    /// oracle below is deliberately order-agnostic: whatever the executed
    /// order is, the final avatar must keep every authored triangle on a
    /// slot that satisfies its band's expectation. The soundness contract
    /// does not depend on which optimizer merged first.
    /// </para>
    /// </summary>
    public sealed class DaoMergedConsumptionTests
    {
        private const string TempFolder = "Assets/AmuseTests_DaoMerge";
        private const string LilToonTransparentShaderName =
            "Hidden/lilToonTransparent";
        private const string LilToonCutoutShaderName =
            "Hidden/lilToonCutout";
        private const string DaoComponentTypeName =
            "d4rkAvatarOptimizer, d4rkpl4y3r.d4rkavataroptimizer.Editor";
        private const string TraceAndOptimizeTypeName =
            "Anatawa12.AvatarOptimizer.TraceAndOptimize,"
            + "com.anatawa12.avatar-optimizer.runtime";
        private const string DaoIntegrationEnvironmentVariable =
            "AMUSE_DAO_INTEGRATION";

        private enum Band
        {
            Transparent,
            Partial,
            Opaque,
        }

        private enum TriangleExpectation
        {
            StaysOnSourceShader,
            MovesToGeneratedShader,
        }

        private sealed class AuthoredTriangle
        {
            internal string Key;
            internal TriangleExpectation Expectation;
            internal int Remaining;
        }

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();
        private readonly Dictionary<string, AuthoredTriangle> authored =
            new Dictionary<string, AuthoredTriangle>();

        private static readonly Vector2[] TransparentBandUv =
        {
            new Vector2(0.02f, 0.25f),
            new Vector2(0.12f, 0.25f),
            new Vector2(0.02f, 0.45f),
        };

        private static readonly Vector2[] PartialBandUv =
        {
            new Vector2(0.28f, 0.25f),
            new Vector2(0.44f, 0.25f),
            new Vector2(0.28f, 0.45f),
        };

        private static readonly Vector2[] OpaqueBandUv =
        {
            new Vector2(0.56f, 0.25f),
            new Vector2(0.94f, 0.25f),
            new Vector2(0.56f, 0.45f),
        };

        private T Track<T>(T asset) where T : UnityEngine.Object
        {
            tracked.Add(asset);
            return asset;
        }

        [TearDown]
        public void TearDown()
        {
            OptimizerMergeObservation.Reset();
            foreach (var asset in tracked)
            {
                if (asset == null) continue;
                if (AssetDatabase.Contains(asset)) continue;
                UnityEngine.Object.DestroyImmediate(asset);
            }

            tracked.Clear();
            authored.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
            }

            // d4rkAvatarOptimizer writes generated output under this
            // project-level folder during optimization; a disposable
            // integration project owns every byte of it.
            const string daoOutputFolder = "Assets/d4rkAvatarOptimizer";
            if (AssetDatabase.IsValidFolder(daoOutputFolder))
            {
                AssetDatabase.DeleteAsset(daoOutputFolder);
            }
        }

        /// <summary>
        /// The DAO-only SDK sequence: the avatar carries the AMUSE
        /// component and d4rkAvatarOptimizer's defaults (mesh merging on,
        /// static property writes off, different-property material
        /// merging off, same-dimension texture merging off, apply on
        /// upload explicitly on). The installed callback dispatcher must
        /// run the whole preprocess chain, d4rkAvatarOptimizer must merge
        /// the two source renderers into one, and the soundness oracle
        /// must hold on the final avatar.
        /// </summary>
        [Test]
        public void DaoMergeRunsInTheSdkSequenceAndPreservesSoundness()
        {
            RunDaoSequence(requireAao: false);
        }

        /// <summary>
        /// The combined sequence: Avatar Optimizer's Trace and Optimize
        /// (mesh merging on, texture optimization off) and
        /// d4rkAvatarOptimizer are both configured. Both optimizers and
        /// AMUSE run inside one SDK callback dispatch, in the vendor
        /// order that this SDK version defines, and the soundness oracle
        /// must still hold on the final avatar.
        /// </summary>
        [Test]
        public void AaoAndDaoCombinedKeepsTheMergedSoundness()
        {
            RunDaoSequence(requireAao: true);
        }

        private void RunDaoSequence(bool requireAao)
        {
            // The disposable-project guard: these tests mutate and delete
            // project-level d4rkAvatarOptimizer output, so they run only
            // when the caller explicitly opted the project in.
            Assume.That(
                Environment.GetEnvironmentVariable(
                    DaoIntegrationEnvironmentVariable),
                Is.EqualTo("1"),
                "Set " + DaoIntegrationEnvironmentVariable + "=1 in a"
                + " disposable public integration project to run these"
                + " tests. They delete project-level d4rkAvatarOptimizer"
                + " output and must never run elsewhere.");

            var daoType = Type.GetType(DaoComponentTypeName);
            Assume.That(daoType, Is.Not.Null,
                "the d4rkpl4y3r.d4rkavataroptimizer package is not"
                + " installed in this project; install it to run them.");
            RequireLilToonEnvironment(out var transparentShader,
                out var cutoutShader);
            var traceAndOptimizeType = Type.GetType(TraceAndOptimizeTypeName);
            if (requireAao && traceAndOptimizeType == null)
            {
                Assert.Ignore(
                    "The com.anatawa12.avatar-optimizer package is not"
                    + " installed in this project; the combined sequence"
                    + " cannot be exercised. Install it to run them.");
            }

            AssetDatabase.CreateFolder("Assets", "AmuseTests_DaoMerge");

            var root = new GameObject(requireAao
                ? "AMUSE AAO DAO combined"
                : "AMUSE DAO only");
            try
            {
                FixtureAvatarIdentity.AttachVrcDescriptor(root);
                root.AddComponent<AmuseAvatarOptimizer>();
                if (requireAao)
                {
                    var traceAndOptimize =
                        root.AddComponent(traceAndOptimizeType);
                    ConfigureTraceAndOptimize(
                        traceAndOptimize, mergeSkinnedMesh: true,
                        optimizeTexture: false);
                }

                root.AddComponent(daoType);
                AttachAnimationFixture(root,
                    requireAao ? "aao-dao" : "dao");

                var texture =
                    Track(ImportBandedAlphaTexture("banded_dao"));
                var transparent = Track(NewTransparentMaterial(
                    transparentShader, texture));
                var cutout = Track(NewCutoutMaterial(
                    cutoutShader, texture, 0.25f));

                CreateSkinnedRenderer(
                    root, "AMUSE dao transparent renderer",
                    "AMUSE dao transparent mesh",
                    new[] { transparent },
                    new[]
                    {
                        Band.Transparent, Band.Partial, Band.Opaque,
                    },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE dao cutout renderer",
                    "AMUSE dao cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial },
                    firstTriangleIndex: 3);

                OptimizerMergeObservation.Reset();
                OptimizerMergeObservation.Enabled = true;
                try
                {
#if AMUSE_VRCSDK3_AVATARS
                    var accepted = VRC.SDKBase.Editor.BuildPipeline
                        .VRCBuildPipelineCallbacks.OnPreprocessAvatar(root);
                    Assert.That(
                        accepted,
                        Is.True,
                        "the SDK preprocess chain must accept the fixture"
                        + " avatar");
#else
                    Assert.Ignore(
                        "The VRChat SDK is not installed in this project,"
                        + " so the installed callback dispatcher cannot be"
                        + " exercised.");
#endif

                    AssertMergedFinalRenderer(root);
                    AssertBuiltTrianglesMatchAuthoring(root);
                }
                finally
                {
                    OptimizerMergeObservation.Reset();
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void RequireLilToonEnvironment(
            out Shader transparentShader, out Shader cutoutShader)
        {
            transparentShader = Shader.Find(LilToonTransparentShaderName);
            cutoutShader = Shader.Find(LilToonCutoutShaderName);
            if (transparentShader != null && cutoutShader != null) return;
            Assert.Ignore(
                "The jp.lilxyzw.liltoon package is not installed in this"
                + " project; the vendor shader fixtures cannot resolve."
                + " Install it to run them.");
        }

        private void ConfigureTraceAndOptimize(
            Component traceAndOptimize,
            bool mergeSkinnedMesh,
            bool optimizeTexture)
        {
            var serialized = new SerializedObject(traceAndOptimize);
            var merge = serialized.FindProperty("mergeSkinnedMesh");
            var texture = serialized.FindProperty("optimizeTexture");
            Assert.That(merge, Is.Not.Null,
                "TraceAndOptimize.mergeSkinnedMesh field (AAO version pin)");
            Assert.That(texture, Is.Not.Null,
                "TraceAndOptimize.optimizeTexture field (AAO version pin)");
            merge.boolValue = mergeSkinnedMesh;
            texture.boolValue = optimizeTexture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void AttachAnimationFixture(
            GameObject root, string assetSuffix)
        {
            var anchor = new GameObject("ProbeAnchor");
            anchor.transform.SetParent(root.transform, false);
            var clip = Track(new AnimationClip
            {
                name = "probe-clip-" + assetSuffix,
            });
            clip.SetCurve(
                "ProbeAnchor", typeof(Transform), "m_LocalPosition.z",
                AnimationCurve.Constant(0f, 1f, 1f));

            var controller = AnimatorController.CreateAnimatorControllerAtPath(
                TempFolder + "/probe-" + assetSuffix + ".controller");
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Probe");
            state.motion = clip;
            stateMachine.defaultState = state;

            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;

#if AMUSE_VRCSDK3_AVATARS
            var descriptor = root.GetComponent<
                VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (descriptor == null) return;
            descriptor.customizeAnimationLayers = true;
            var baseLayer =
                new VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                    .CustomAnimLayer
            {
                type = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                    .AnimLayerType.Base,
                animatorController = controller,
                isDefault = false,
                isEnabled = true,
            };
            var layers = descriptor.baseAnimationLayers;
            if (layers == null || layers.Length == 0)
            {
                descriptor.baseAnimationLayers = new[] { baseLayer };
            }
            else
            {
                layers[0] = baseLayer;
                descriptor.baseAnimationLayers = layers;
            }

            if (descriptor.specialAnimationLayers == null)
            {
                descriptor.specialAnimationLayers = new[]
                {
                    FallbackLayer(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                        .AnimLayerType.Sitting),
                    FallbackLayer(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                        .AnimLayerType.TPose),
                    FallbackLayer(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                        .AnimLayerType.IKPose),
                };
            }
#endif
        }

        private static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
            .CustomAnimLayer FallbackLayer(
            VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.AnimLayerType type)
        {
            return new VRC.SDK3.Avatars.Components.VRCAvatarDescriptor
                .CustomAnimLayer
            {
                type = type,
                isDefault = true,
                isEnabled = true,
            };
        }

        private void CreateSkinnedRenderer(
            GameObject root,
            string holderName,
            string meshName,
            Material[] materials,
            Band[] bands,
            int firstTriangleIndex)
        {
            var holder = new GameObject(holderName);
            holder.transform.SetParent(root.transform, false);
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = meshName };
            Track(mesh);

            var vertices = new Vector3[bands.Length * 3];
            var uvs = new Vector2[bands.Length * 3];
            var submeshTriangles = new List<int>[materials.Length];
            for (var submesh = 0; submesh < materials.Length; submesh++)
            {
                submeshTriangles[submesh] = new List<int>();
            }

            for (var i = 0; i < bands.Length; i++)
            {
                var index = firstTriangleIndex + i;
                var v0 = i * 3;
                vertices[v0] = new Vector3(4 * index, 0f, 0f);
                vertices[v0 + 1] = new Vector3(4 * index + 1, 0f, 0f);
                vertices[v0 + 2] = new Vector3(4 * index, 1f, 0f);
                var triplet = BandUv(bands[i]);
                uvs[v0] = triplet[0];
                uvs[v0 + 1] = triplet[1];
                uvs[v0 + 2] = triplet[2];
                submeshTriangles[0].AddRange(new[] { v0, v0 + 1, v0 + 2 });
                var authoredKey = OptimizerMergeObservation.TriangleKey(
                    vertices[v0], vertices[v0 + 1], vertices[v0 + 2],
                    root.transform);
                if (!authored.TryGetValue(authoredKey, out var entry))
                {
                    entry = new AuthoredTriangle
                    {
                        Key = authoredKey,
                        Expectation = bands[i] == Band.Opaque
                            ? TriangleExpectation.MovesToGeneratedShader
                            : TriangleExpectation.StaysOnSourceShader,
                    };
                    authored.Add(authoredKey, entry);
                }

                entry.Remaining++;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.subMeshCount = materials.Length;
            for (var submesh = 0; submesh < materials.Length; submesh++)
            {
                mesh.SetTriangles(submeshTriangles[submesh], submesh);
            }

            mesh.bindposes = new[] { Matrix4x4.identity };
            var weights = new BoneWeight[mesh.vertexCount];
            var normals = new Vector3[mesh.vertexCount];
            for (var i = 0; i < mesh.vertexCount; i++)
            {
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1f };
                normals[i] = Vector3.forward;
            }

            mesh.boneWeights = weights;
            mesh.normals = normals;
            mesh.RecalculateBounds();

            // d4rkAvatarOptimizer merges renderers with matching
            // renderer properties too; one shared bound box keeps the
            // two source renderers in the same merge candidate set.
            var sharedBounds = new Bounds(
                new Vector3(2f, 0.5f, 0f), new Vector3(64f, 8f, 8f));
            mesh.bounds = sharedBounds;

            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            renderer.bones = new[] { root.transform };
            renderer.rootBone = root.transform;
            renderer.probeAnchor = root.transform;
            renderer.localBounds = sharedBounds;
        }

        private static Vector2[] BandUv(Band band)
        {
            switch (band)
            {
                case Band.Transparent: return TransparentBandUv;
                case Band.Partial: return PartialBandUv;
                case Band.Opaque: return OpaqueBandUv;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band));
            }
        }

        private Material NewTransparentMaterial(
            Shader shader, Texture texture)
        {
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetFloat("_Cutoff", 0.01f);
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 1f);
            material.SetTexture("_AlphaMask", texture);
            material.SetTexture("_MainTex", texture);
            return Track(material);
        }

        private Material NewCutoutMaterial(
            Shader shader, Texture texture, float cutoff)
        {
            var material = new Material(shader);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_AlphaMaskMode", 2f);
            material.SetFloat("_AlphaMaskScale", 1f);
            material.SetFloat("_AlphaMaskValue", 1f);
            material.SetTexture("_AlphaMask", texture);
            material.SetTexture("_MainTex", texture);
            return Track(material);
        }

        private Texture2D ImportBandedAlphaTexture(string assetName)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var alpha = x < size / 4
                        ? (byte)0
                        : x < size / 2 ? (byte)128 : (byte)255;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var cpu = new Texture2D(size, size, TextureFormat.RGBA32, false);
            cpu.SetPixels32(pixels);
            cpu.Apply(false, false);
            var encoded = cpu.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(cpu);

            var path = TempFolder + "/" + assetName + ".png";
            File.WriteAllBytes(path, encoded);
            AssetDatabase.ImportAsset(path);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            importer.mipmapEnabled = true;
            importer.isReadable = true;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, "fixture precondition");
            return texture;
        }

        private static void AssertMergedFinalRenderer(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<
                SkinnedMeshRenderer>(true);
            Assert.That(
                renderers.Length, Is.EqualTo(1),
                "d4rkAvatarOptimizer must merge the two source renderers"
                + " into one before the SDK build continues");
            var mesh = renderers[0].sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            var triangleCount = 0;
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                triangleCount += mesh.GetTriangles(submesh).Length / 3;
            }

            Assert.That(
                triangleCount, Is.EqualTo(4),
                "the merged renderer must carry every authored triangle");
        }

        /// <summary>
        /// The order-agnostic soundness oracle. d4rkAvatarOptimizer may
        /// run before or after the NDMF pipeline in this SDK version, so
        /// a moved triangle is identified by its final slot's shader: the
        /// canonical opaque conversion carries a shader that is neither
        /// source shader, and every kept triangle sits on its source
        /// shader. Combined with the exact triangle multiset accounting,
        /// this pins the per-predicate outcomes for either executed
        /// order.
        /// </summary>
        private void AssertBuiltTrianglesMatchAuthoring(GameObject root)
        {
            var sourceShaders = new HashSet<string>
            {
                LilToonTransparentShaderName,
                LilToonCutoutShaderName,
            };

            foreach (var renderer in root.GetComponentsInChildren<
                         SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                var materials = renderer.sharedMaterials;
                if (mesh == null || materials == null
                    || materials.Length == 0) continue;

                var vertices = mesh.vertices;
                var subMeshCount = Math.Min(mesh.subMeshCount,
                    materials.Length);
                for (var subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    var material = materials[subMesh];
                    var onGenerated = material != null
                        && material.shader != null
                        && !sourceShaders.Contains(material.shader.name);
                    var triangles = mesh.GetTriangles(subMesh);
                    for (var t = 0; t < triangles.Length; t += 3)
                    {
                        var key = OptimizerMergeObservation.TriangleKey(
                            renderer.transform.TransformPoint(
                                vertices[triangles[t]]),
                            renderer.transform.TransformPoint(
                                vertices[triangles[t + 1]]),
                            renderer.transform.TransformPoint(
                                vertices[triangles[t + 2]]),
                            root.transform);
                        if (!authored.TryGetValue(key, out var match)
                            || match.Remaining <= 0)
                        {
                            Assert.Fail(
                                "output triangle " + key + " must match"
                                + " exactly one unmatched authored triangle"
                                + " (renderer " + renderer.name + " slot "
                                + subMesh + ")");
                        }

                        match.Remaining--;
                        var expectGenerated = match.Expectation
                            == TriangleExpectation.MovesToGeneratedShader;
                        Assert.That(
                            onGenerated,
                            Is.EqualTo(expectGenerated),
                            "triangle " + key + " violated its soundness"
                            + " expectation: slot material "
                            + (material != null ? material.name : "<null>")
                            + (onGenerated ? " is" : " is not")
                            + " off its source shader");
                    }
                }
            }

            var unmatched = authored.Values.Where(entry =>
                entry.Remaining > 0).ToList();
            Assert.That(
                unmatched, Is.Empty,
                "authored triangles missing from the built avatar: "
                + string.Join("; ", unmatched.Select(entry => entry.Key)));
        }
    }
}
