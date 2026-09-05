using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The split disposition: appended submeshes, appended material slots,
    /// preserved mesh and per-submesh bounds, and the binding-identity matching
    /// the appended curve rewrite depends on.
    /// <para>
    /// The split fixtures live here because they encode subtle alpha-proof
    /// preconditions — texel-aligned split textures, nonzero base vertices,
    /// authored bounds unrelated to geometry — whose duplication could drift
    /// while still passing. The apply falsifier that observes a split across
    /// preparation consumes them from this class.
    /// </para>
    /// </summary>
    public sealed class AlphaSeparationSplitTests
    {
        // --- Split-only fixtures ---------------------------------------------

        internal const string SplitTempFolder = "Assets/AmuseTests_AlphaSplit";

        internal static void EnsureSplitFolder()
        {
            if (!AssetDatabase.IsValidFolder(SplitTempFolder))
            {
                AssetDatabase.CreateFolder("Assets", "AmuseTests_AlphaSplit");
            }
        }

        internal static void DeleteSplitFolder()
        {
            if (AssetDatabase.IsValidFolder(SplitTempFolder))
            {
                AssetDatabase.DeleteAsset(SplitTempFolder);
            }
        }

        /// <summary>
        /// An 8x8 mip-free, non-readable RGBA32 asset with Point/Clamp
        /// sampling: every acquisition gate admits it, and the single-level
        /// mip chain cannot refute a triangle whose support lies wholly inside
        /// one half. The left half (u &lt; 0.5) is opaque; the right half is
        /// translucent, so one submesh can carry one proven-opaque triangle
        /// and one proven-transparent triangle — a Split.
        /// </summary>
        internal static Texture2D ImportSplitAlphaTexture(string name)
        {
            var path = SplitTempFolder + "/" + name + ".png";
            var staging = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[64];
                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        pixels[y * 8 + x] = new Color32(
                            64, 32, 16, (byte)(x < 4 ? 255 : 200));
                    }
                }

                staging.SetPixels32(pixels);
                staging.Apply();
                File.WriteAllBytes(path, staging.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staging);
            }

            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.textureCompression =
                TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            importer.mipMapBias = 0f;
            importer.anisoLevel = 1;
            importer.streamingMipmaps = false;
            importer.SaveAndReimport();

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(loaded, Is.Not.Null, $"'{path}' must import.");
            return loaded;
        }

        /// <summary>
        /// A verified Poiyomi fixture material whose alpha comes from the
        /// split texture. Full colour alpha is set explicitly to pin the
        /// semantic precondition and stay aligned with the existing green
        /// sampled-alpha integration fixture. Its render state passes every
        /// conversion gate, so it is convertible.
        /// </summary>
        internal static Material SplitAlphaMaterial(Texture2D mainTex)
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 1f));
            material.SetTexture("_MainTex", mainTex);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            return material;
        }

        /// <summary>Authored mesh bounds, unrelated to the geometry.</summary>
        internal static readonly Bounds AuthoredMeshBounds =
            new Bounds(new Vector3(10f, 20f, 30f), new Vector3(40f, 50f, 60f));

        internal static Bounds AuthoredSubmeshBounds(int submesh)
        {
            return new Bounds(
                new Vector3(100 + submesh, 200 + submesh, 300 + submesh),
                new Vector3(2f, 2f, 2f));
        }

        /// <summary>
        /// Adversarial vertex data for the split fixtures: deterministic
        /// positions, normals, tangents, colours, one complete UV channel, and
        /// deliberately unrelated authored bounds, so a lossy rewrite cannot
        /// hide. Callers still set the UVs they need.
        /// </summary>
        private static void FillVertexAttributes(
            Mesh mesh,
            int vertexCount)
        {
            var positions = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var colors = new Color32[vertexCount];
            var uv = new Vector2[vertexCount];
            for (var index = 0; index < vertexCount; index++)
            {
                positions[index] =
                    new Vector3(index, index % 2, -index);
                normals[index] = new Vector3(0f, 1f, 0f);
                tangents[index] = new Vector4(1f, 0f, 0f, -1f);
                colors[index] = new Color32(
                    (byte)index, (byte)(255 - index), 7, 200);
                uv[index] = Vector2.zero;
            }

            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.colors32 = colors;
            mesh.uv = uv;
        }

        private static void AuthorBounds(Mesh mesh)
        {
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var descriptor = mesh.GetSubMesh(submesh);
                descriptor.bounds = AuthoredSubmeshBounds(submesh);
                mesh.SetSubMesh(
                    submesh, descriptor,
                    MeshUpdateFlags.DontRecalculateBounds);
            }

            mesh.bounds = AuthoredMeshBounds;
        }

        /// <summary>
        /// One split submesh authored with a nonzero base vertex (its stored
        /// indices are local: {0,1,2} opaque and {1,2,3} transparent over
        /// effective vertices 4-7), one untouched submesh on a uniformly
        /// transparent material, and two vertices no triangle references.
        /// Submesh 0 is the split; triangle ordinal 0 is the opaque one.
        /// </summary>
        internal static Mesh CreateSplitSourceMesh()
        {
            var mesh = new Mesh { name = "amuse split source" };
            mesh.indexFormat = IndexFormat.UInt32;
            FillVertexAttributes(mesh, 9);
            mesh.subMeshCount = 2;

            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.6f, 0.9f),
                new Vector2(0f, 0f),
                new Vector2(0.1f, 0.1f),
                new Vector2(0.4f, 0.1f),
                new Vector2(0.1f, 0.4f),
                new Vector2(0.6f, 0.35f),
                new Vector2(0f, 0f),
            };

            // The split submesh: baseVertex 4, stored (local) indices.
            mesh.SetIndices(
                new[] { 0, 1, 2, 1, 2, 3 },
                MeshTopology.Triangles, 0,
                calculateBounds: false, baseVertex: 4);
            // The untouched submesh: transparent material, baseVertex 0.
            mesh.SetIndices(
                new[] { 0, 1, 2 },
                MeshTopology.Triangles, 1,
                calculateBounds: false, baseVertex: 0);

            AuthorBounds(mesh);
            return mesh;
        }

        /// <summary>
        /// Submesh 0 is a uniformly opaque candidate (constant material), and
        /// submesh 1 is the split with the nonzero base vertex. This is the
        /// fixture for a wholly-opaque slot surviving beside a split slot.
        /// </summary>
        internal static Mesh CreateOpaqueAndSplitSourceMesh()
        {
            var mesh = new Mesh { name = "amuse opaque and split source" };
            mesh.indexFormat = IndexFormat.UInt32;
            FillVertexAttributes(mesh, 9);
            mesh.subMeshCount = 2;

            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0.1f, 0.1f),
                new Vector2(0.4f, 0.1f),
                new Vector2(0.1f, 0.4f),
                new Vector2(0.6f, 0.35f),
                new Vector2(0f, 0f),
            };

            mesh.SetIndices(
                new[] { 0, 1, 2 },
                MeshTopology.Triangles, 0,
                calculateBounds: false, baseVertex: 0);
            mesh.SetIndices(
                new[] { 0, 1, 2, 1, 2, 3 },
                MeshTopology.Triangles, 1,
                calculateBounds: false, baseVertex: 4);

            AuthorBounds(mesh);
            return mesh;
        }

        /// <summary>
        /// Two split submeshes, each with its own nonzero base vertex (4 and
        /// 8), plus one untouched submesh. Submeshes 0 and 1 are the splits;
        /// triangle ordinal 0 of each is the opaque one.
        /// </summary>
        internal static Mesh CreateTwoSplitSourceMesh()
        {
            var mesh = new Mesh { name = "amuse two split source" };
            mesh.indexFormat = IndexFormat.UInt32;
            FillVertexAttributes(mesh, 12);
            mesh.subMeshCount = 3;

            mesh.uv = new[]
            {
                new Vector2(0.6f, 0.6f),
                new Vector2(0.9f, 0.6f),
                new Vector2(0.6f, 0.9f),
                new Vector2(0f, 0f),
                new Vector2(0.1f, 0.1f),
                new Vector2(0.4f, 0.1f),
                new Vector2(0.1f, 0.4f),
                new Vector2(0.6f, 0.35f),
                new Vector2(0.15f, 0.15f),
                new Vector2(0.45f, 0.15f),
                new Vector2(0.15f, 0.45f),
                new Vector2(0.65f, 0.4f),
            };

            mesh.SetIndices(
                new[] { 0, 1, 2, 1, 2, 3 },
                MeshTopology.Triangles, 0,
                calculateBounds: false, baseVertex: 4);
            mesh.SetIndices(
                new[] { 0, 1, 2, 1, 2, 3 },
                MeshTopology.Triangles, 1,
                calculateBounds: false, baseVertex: 8);
            mesh.SetIndices(
                new[] { 0, 1, 2 },
                MeshTopology.Triangles, 2,
                calculateBounds: false, baseVertex: 0);

            AuthorBounds(mesh);
            return mesh;
        }

        /// <summary>
        /// The split submesh's two triangles as effective (absolute) index
        /// sets: ordinal 0 lies wholly inside the opaque texel columns;
        /// ordinal 1 crosses into the translucent half.
        /// </summary>
        internal static int[] SplitOpaqueEffectiveIndicesFor(int baseVertex)
        {
            return new[]
            {
                baseVertex + 0, baseVertex + 1, baseVertex + 2,
            };
        }

        internal static int[] SplitTransparentEffectiveIndicesFor(
            int baseVertex)
        {
            return new[]
            {
                baseVertex + 1, baseVertex + 2, baseVertex + 3,
            };
        }

        // --- Falsifier infrastructure ----------------------------------------

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null)
            {
                tracked.Add(obj);
            }

            return obj;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyTracked();
        }

        private void DestroyTracked()
        {
            for (var index = tracked.Count - 1; index >= 0; index--)
            {
                if (tracked[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(tracked[index]);
                }
            }

            tracked.Clear();
        }

        private static Material VerifiedOpaqueMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        private static Material VerifiedTransparentMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            return material;
        }

        private static SkinnedMeshRenderer AddRenderer(
            GameObject root,
            string name,
            Mesh mesh,
            params Material[] materials)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static AnimationClip NewSwapClip(
            string name,
            string rendererPath,
            int slotIndex,
            params (float time, Material value)[] keys)
        {
            var clip = new AnimationClip { name = name };
            var keyframes = new ObjectReferenceKeyframe[keys.Length];
            for (var index = 0; index < keys.Length; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = keys[index].time,
                    value = keys[index].value,
                };
            }

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath,
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[" + slotIndex + "]"),
                keyframes);
            return clip;
        }

        private static AnimatorController NewController(
            GameObject root,
            string name,
            params AnimationClip[] clips)
        {
            var controller = new AnimatorController { name = name };
            controller.AddLayer("L0");
            for (var index = 0; index < clips.Length; index++)
            {
                controller.layers[0].stateMachine
                    .AddState("S" + index).motion = clips[index];
            }

            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;
            return controller;
        }

        private static string DescribeObjectCurve(ObjectReferenceKeyframe[] curve)
        {
            if (curve == null)
            {
                return "<null>";
            }

            return string.Join("|", curve.Select(key =>
                key.time.ToString("R") + "=>" +
                (key.value == null
                    ? "null"
                    : key.value.name)));
        }

        private static string DescribeCommittedCurve(
            AnimationClip clip,
            string rendererPath,
            string propertyName)
        {
            return DescribeObjectCurve(AnimationUtility.GetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath, typeof(SkinnedMeshRenderer),
                    propertyName)));
        }

        private static string DescribeAuthoredCurve(
            AnimationClip clip,
            string rendererPath,
            string propertyName)
        {
            return DescribeObjectCurve(AnimationUtility.GetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath, typeof(SkinnedMeshRenderer),
                    propertyName)));
        }

        private static IEnumerable<AnimationClip> CommittedClips(
            GameObject avatarRoot)
        {
            var seen = new HashSet<AnimationClip>();
            foreach (var animator in avatarRoot
                         .GetComponentsInChildren<Animator>(true))
            {
                if (!(animator.runtimeAnimatorController is
                        AnimatorController controller))
                {
                    continue;
                }

                foreach (var layer in controller.layers)
                {
                    foreach (var child in layer.stateMachine.states)
                    {
                        if (child.state.motion is AnimationClip clip &&
                            seen.Add(clip))
                        {
                            yield return clip;
                        }
                    }
                }
            }
        }

        private static AnimationClip CommittedClipCarrying(
            GameObject root,
            string rendererPath,
            string propertyName)
        {
            return CommittedClips(root).SingleOrDefault(clip =>
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(
                    binding => binding.path == rendererPath &&
                               binding.propertyName == propertyName));
        }

        private static void DestroyCommittedClone(
            GameObject root,
            AnimatorController original)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            var committed = animator.runtimeAnimatorController;
            if (committed == null || ReferenceEquals(committed, original))
            {
                return;
            }

            animator.runtimeAnimatorController = null;
            if (committed is AnimatorController controller)
            {
                foreach (var layer in controller.layers)
                {
                    UnityEngine.Object.DestroyImmediate(layer.stateMachine);
                }
            }

            UnityEngine.Object.DestroyImmediate(committed);
        }

        private static void DestroyGenerated(AmusePlatformFinishState state)
        {
            if (state?.Separation == null)
            {
                return;
            }

            foreach (var clone in state.Separation.CreatedClones)
            {
                if (clone != null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }

            foreach (var prepared in state.Separation.Renderers)
            {
                if (prepared.MeshClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(prepared.MeshClone);
                }
            }
        }

        // --- Falsifier 2: the mixed split slot -------------------------------

        [Test]
        public void SplitSlotRetainsTransparentTriplesAndAppendsOpaqueTriples()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE split triples");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                EnsureSplitFolder();
                try
                {
                    var texture = Track(ImportSplitAlphaTexture("triples"));
                    var split = Track(SplitAlphaMaterial(texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var sourceMesh = Track(CreateSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, split, transparent);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, AlphaSeparationApplyTests.ApplyTestPlatform
                            .Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                        "fixture precondition: the renderer must analyze");
                    Assert.That(state.OpaqueCandidateTriangleCount,
                        Is.EqualTo(1),
                        "fixture precondition: exactly one opaque triangle " +
                        "must be a candidate");
                    Assert.That(state.Separation, Is.Not.Null);
                    var slot = state.Separation.Renderers[0].CandidateSlots
                        .Single();
                    Assert.That(
                        slot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition.Split),
                        "fixture precondition: the slot must be a Split, or " +
                        "the appended triples prove nothing");
                    Assert.That(state.Separation.CreatedClones,
                        Has.Count.EqualTo(1));
                    var clone = state.Separation.CreatedClones[0];

                    var splitMesh = renderer.sharedMesh;
                    Assert.That(splitMesh, Is.Not.SameAs(sourceMesh),
                        "the renderer must carry the finalized clone");
                    Assert.That(splitMesh.subMeshCount, Is.EqualTo(3));

                    CollectionAssert.AreEqual(
                        SplitTransparentEffectiveIndicesFor(4),
                        splitMesh.GetIndices(0),
                        "the source submesh must retain exactly the " +
                        "transparent triples");
                    CollectionAssert.AreEqual(
                        new[] { 0, 1, 2 },
                        splitMesh.GetIndices(1),
                        "the untouched submesh must be unchanged");
                    CollectionAssert.AreEqual(
                        SplitOpaqueEffectiveIndicesFor(4),
                        splitMesh.GetIndices(2),
                        "the appended submesh must carry exactly the opaque " +
                        "triples");

                    Assert.That(renderer.sharedMaterials[0], Is.SameAs(split),
                        "the split slot's alpha material must stay on its " +
                        "submesh");
                    Assert.That(renderer.sharedMaterials[1],
                        Is.SameAs(transparent));
                    Assert.That(renderer.sharedMaterials[2], Is.SameAs(clone),
                        "the appended slot must carry the opaque result");

                    // The rewritten submesh's base vertex is normalized to
                    // zero, an intentional characterized representation change;
                    // the descriptor must still name the vertices it actually
                    // references.
                    Assert.That(splitMesh.GetSubMesh(0).baseVertex,
                        Is.EqualTo(0),
                        "the characterized base-vertex normalization no " +
                        "longer holds; re-justify it");
                    var appended = splitMesh.GetSubMesh(2);
                    var appendedIndices = splitMesh.GetIndices(2);
                    Assert.That(
                        appended.baseVertex + appended.firstVertex,
                        Is.EqualTo(appendedIndices.Min()),
                        "the appended descriptor must name the lowest vertex " +
                        "it actually references");
                    Assert.That(
                        appended.vertexCount,
                        Is.EqualTo(
                            appendedIndices.Max() - appendedIndices.Min() + 1),
                        "the appended descriptor must span the vertices it " +
                        "actually references");

                    Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
                    Assert.That(state.AppliedOpaqueTriangleCount,
                        Is.EqualTo(1));
                }
                finally
                {
                    DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 5: wholly opaque survives a refused split sibling -----

        [Test]
        public void WhollyOpaqueSurvivesWhileItsSplitSiblingRefuses()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE split sibling refuses");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                EnsureSplitFolder();
                try
                {
                    var texture = Track(ImportSplitAlphaTexture("sibling"));
                    var opaque = Track(VerifiedOpaqueMaterial());
                    var splitRefused = Track(SplitAlphaMaterial(texture));
                    splitRefused.SetFloat("_EnableOutlines", 1f);
                    var sourceMesh = Track(CreateOpaqueAndSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, opaque, splitRefused);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, AlphaSeparationApplyTests.ApplyTestPlatform
                            .Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                        "fixture precondition: the renderer must analyze, " +
                        "or the surviving sibling proves nothing");
                    Assert.That(state.OpaqueCandidateTriangleCount,
                        Is.EqualTo(2),
                        "fixture precondition: both slots must produce " +
                        "opaque candidates, so the refusal is " +
                        "conversion-owned, not an alpha failure");
                    Assert.That(
                        state.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .OpaqueConversionRefused),
                        Is.EqualTo(1),
                        "exactly the outlines-enabled split slot may refuse");
                    Assert.That(
                        state.SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .RuntimeMaterialValueNotMapped),
                        Is.Zero);

                    var survivingSlot = state.Separation.Renderers[0]
                        .CandidateSlots.Single();
                    Assert.That(
                        survivingSlot.Plan.SourceMaterialBindingIndex,
                        Is.EqualTo(0),
                        "only the wholly opaque slot may survive");
                    Assert.That(
                        survivingSlot.Plan.Disposition,
                        Is.EqualTo(SubmeshSeparationDisposition
                            .WhollyOpaqueCandidate));

                    Assert.That(renderer.sharedMesh, Is.SameAs(sourceMesh),
                        "no surviving split means the renderer keeps its " +
                        "source mesh");
                    Assert.That(renderer.sharedMesh.subMeshCount,
                        Is.EqualTo(2));
                    Assert.That(
                        renderer.sharedMaterials[0],
                        Is.SameAs(survivingSlot.OpaqueOfAdmitted[opaque]),
                        "the wholly opaque slot must still apply");
                    Assert.That(renderer.sharedMaterials[1],
                        Is.SameAs(splitRefused),
                        "the refused slot's material must be untouched");
                    Assert.That(state.AppliedRendererCount, Is.EqualTo(1));
                    Assert.That(state.AppliedOpaqueTriangleCount,
                        Is.EqualTo(1));
                }
                finally
                {
                    DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 14: mesh and per-submesh bounds survive ---------------

        [Test]
        public void MeshAndEverySubmeshBoundsSurviveSplitFinalization()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE split bounds");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            try
            {
                EnsureSplitFolder();
                try
                {
                    var texture = Track(ImportSplitAlphaTexture("bounds"));
                    var split = Track(SplitAlphaMaterial(texture));
                    var transparent = Track(VerifiedTransparentMaterial());
                    var sourceMesh = Track(CreateSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, split, transparent);
                    Assert.That(
                        renderer.sharedMesh.bounds,
                        Is.EqualTo(AuthoredMeshBounds),
                        "fixture precondition: the authored mesh bounds must " +
                        "be unrelated to the geometry, or the restore " +
                        "proves nothing");
                    Assert.That(
                        renderer.sharedMesh.GetSubMesh(0).bounds,
                        Is.EqualTo(AuthoredSubmeshBounds(0)),
                        "fixture precondition: the authored submesh bounds " +
                        "must be unrelated to the geometry");
                    Assert.That(
                        renderer.sharedMesh.GetSubMesh(1).bounds,
                        Is.EqualTo(AuthoredSubmeshBounds(1)));

                    var context = AvatarProcessor.ProcessAvatar(
                        root, AlphaSeparationApplyTests.ApplyTestPlatform
                            .Instance);
                    state = context.GetState<AmusePlatformFinishState>();
                    Assert.That(state.Separation, Is.Not.Null,
                        "fixture precondition: the split slot must reach " +
                        "finalization, or the bounds prove nothing");

                    var splitMesh = renderer.sharedMesh;
                    Assert.That(splitMesh.subMeshCount, Is.EqualTo(3));
                    Assert.That(splitMesh.bounds, Is.EqualTo(AuthoredMeshBounds),
                        "the authored mesh bounds must survive split " +
                        "finalization; raising subMeshCount recalculates " +
                        "them and the restore did not hold");
                    Assert.That(
                        splitMesh.GetSubMesh(0).bounds,
                        Is.EqualTo(AuthoredSubmeshBounds(0)),
                        "the rewritten split submesh must inherit its " +
                        "source submesh's bounds");
                    Assert.That(
                        splitMesh.GetSubMesh(1).bounds,
                        Is.EqualTo(AuthoredSubmeshBounds(1)),
                        "the untouched submesh must be restored to its own " +
                        "authored bounds");
                    Assert.That(
                        splitMesh.GetSubMesh(2).bounds,
                        Is.EqualTo(AuthoredSubmeshBounds(0)),
                        "the appended submesh must inherit its source " +
                        "submesh's bounds; a zero or recalculated value " +
                        "means the per-submesh bounds obligation was not " +
                        "met");
                }
                finally
                {
                    DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // --- Falsifier 18: deterministic appended indices and binding --------

        [Test]
        public void
            TwoSurvivingSplitSlotsAppendDeterministicallyAndMatchBindingsByIdentity()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE split indices");
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            AmusePlatformFinishState state = null;
            AnimatorController controller = null;
            try
            {
                EnsureSplitFolder();
                try
                {
                    var texture = Track(ImportSplitAlphaTexture("indices"));
                    var first = Track(SplitAlphaMaterial(texture));
                    var second = Track(SplitAlphaMaterial(texture));
                    var firstSwap = Track(VerifiedOpaqueMaterial());
                    var secondSwap = Track(VerifiedOpaqueMaterial());
                    var transparent = Track(VerifiedTransparentMaterial());
                    first.name = "amuse split first";
                    second.name = "amuse split second";
                    firstSwap.name = "amuse split first swap";
                    secondSwap.name = "amuse split second swap";

                    var sourceMesh = Track(CreateTwoSplitSourceMesh());
                    var renderer = AddRenderer(
                        root, "body", sourceMesh, first, second, transparent);
                    var decoyRenderer = AddRenderer(
                        root, "decoy", Track(CreateSplitSourceMesh()),
                        transparent, transparent);

                    // Two clips deliberately share this display name.
                    const string collidingName = "AMUSE colliding clip";
                    var firstClip = new AnimationClip { name = collidingName };
                    SetCurve(
                        firstClip, "body", 0,
                        (0f, first), (1f, firstSwap));
                    var secondClip = new AnimationClip { name = collidingName };
                    SetCurve(
                        secondClip, "body", 1,
                        (0f, second), (1f, secondSwap));
                    // A decoy binding at another renderer path.
                    SetCurve(secondClip, "decoy", 0, (0f, first));
                    // A decoy binding at another slot of the same renderer.
                    SetCurve(secondClip, "body", 2, (0f, transparent));

                    var authoredFirst = DescribeAuthoredCurve(
                        firstClip, "body", "m_Materials.Array.data[0]");
                    var authoredDecoyPath = DescribeAuthoredCurve(
                        secondClip, "decoy", "m_Materials.Array.data[0]");
                    var authoredDecoySlot = DescribeAuthoredCurve(
                        secondClip, "body", "m_Materials.Array.data[2]");
                    var authoredSecond = DescribeAuthoredCurve(
                        secondClip, "body", "m_Materials.Array.data[1]");

                    controller = NewController(
                        root, "AMUSE indices graph",
                        firstClip, secondClip);

                    var context = AvatarProcessor.ProcessAvatar(
                        root, AlphaSeparationApplyTests.ApplyTestPlatform
                            .Instance);
                    state = context.GetState<AmusePlatformFinishState>();

                    Assert.That(state.AnalyzedRendererCount, Is.EqualTo(2),
                        "fixture precondition: both renderers must analyze " +
                        "with no refusal, or the surviving split proves " +
                        "nothing");
                    Assert.That(
                        state.Separation.Renderers, Has.Count.EqualTo(1),
                        "the decoy renderer proves no candidate and must " +
                        "not be retained");
                    Assert.That(state.Separation, Is.Not.Null);
                    var candidates = state.Separation.Renderers[0]
                        .CandidateSlots;
                    Assert.That(candidates, Has.Count.EqualTo(2),
                        "fixture precondition: both split slots must " +
                        "survive, or the appended indices prove nothing");
                    Assert.That(
                        candidates.Select(slot =>
                                slot.Plan.SourceMaterialBindingIndex),
                        Is.EqualTo(new[] { 0, 1 }));

                    // n = 3, k = 2, so the appended slots are 3 and 4 in
                    // ascending source-slot order.
                    Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(5));
                    Assert.That(renderer.sharedMaterials[0], Is.SameAs(first),
                        "the split slot's alpha material stays put");
                    Assert.That(renderer.sharedMaterials[1], Is.SameAs(second));
                    Assert.That(renderer.sharedMaterials[2],
                        Is.SameAs(transparent));
                    Assert.That(
                        renderer.sharedMaterials[3],
                        Is.SameAs(candidates[0].OpaqueOfAdmitted[first]),
                        "the first surviving split slot's opaque result " +
                        "must land on appended slot 3");
                    Assert.That(
                        renderer.sharedMaterials[4],
                        Is.SameAs(candidates[1].OpaqueOfAdmitted[second]),
                        "the second surviving split slot's opaque result " +
                        "must land on appended slot 4");

                    // The committed clone of the FIRST same-named clip is the
                    // only one carrying the new appended binding data[3].
                    var committedFirst = CommittedClipCarrying(
                        root, "body", "m_Materials.Array.data[3]");
                    Assert.That(committedFirst, Is.Not.Null,
                        "no committed clip carried the first appended " +
                        "binding");
                    Assert.That(
                        DescribeCommittedCurve(
                            committedFirst, "body",
                            "m_Materials.Array.data[0]"),
                        Is.EqualTo(authoredFirst),
                        "a surviving Split slot's own curve stays authored " +
                        "and unchanged; only the appended opaque slot " +
                        "receives the mapped curve");
                    Assert.That(
                        DescribeCommittedCurve(
                            committedFirst, "body",
                            "m_Materials.Array.data[3]"),
                        Is.EqualTo(MapNames(
                            authoredFirst,
                            new[]
                            {
                                (first, candidates[0].OpaqueOfAdmitted[first]),
                                (firstSwap,
                                    candidates[0].OpaqueOfAdmitted[firstSwap]),
                            })),
                        "the appended binding must carry identical times and " +
                        "mapped values");

                    // The committed clone of the SECOND same-named clip is
                    // the only one carrying data[4]. Association is by
                    // binding identity, never by the colliding display name.
                    var committedSecond = CommittedClipCarrying(
                        root, "body", "m_Materials.Array.data[4]");
                    Assert.That(committedSecond, Is.Not.Null,
                        "no committed clip carried the second appended " +
                        "binding");
                    Assert.That(
                        committedSecond, Is.Not.SameAs(committedFirst),
                        "two clips sharing a display name must remain " +
                        "distinct objects through the rewrite");
                    Assert.That(
                        DescribeCommittedCurve(
                            committedSecond, "body",
                            "m_Materials.Array.data[1]"),
                        Is.EqualTo(authoredSecond),
                        "a surviving Split slot's own curve stays authored " +
                        "and unchanged; only the appended opaque slot " +
                        "receives the mapped curve");

                    // Decoy bindings stay untouched.
                    Assert.That(
                        DescribeCommittedCurve(
                            committedSecond, "decoy",
                            "m_Materials.Array.data[0]"),
                        Is.EqualTo(authoredDecoyPath),
                        "a binding at another renderer path must stay " +
                        "untouched");
                    Assert.That(
                        DescribeCommittedCurve(
                            committedSecond, "body",
                            "m_Materials.Array.data[2]"),
                        Is.EqualTo(authoredDecoySlot),
                        "a binding at another slot must stay untouched");

                    // Source clips are unchanged.
                    Assert.That(
                        DescribeAuthoredCurve(
                            firstClip, "body",
                            "m_Materials.Array.data[0]"),
                        Is.EqualTo(authoredFirst),
                        "the first source clip must be unchanged");
                    Assert.That(
                        DescribeAuthoredCurve(
                            secondClip, "body",
                            "m_Materials.Array.data[1]"),
                        Is.EqualTo(authoredSecond),
                        "the second source clip must be unchanged");
                    Assert.That(
                        DescribeAuthoredCurve(
                            secondClip, "decoy",
                            "m_Materials.Array.data[0]"),
                        Is.EqualTo(authoredDecoyPath));
                    Assert.That(
                        DescribeAuthoredCurve(
                            secondClip, "body",
                            "m_Materials.Array.data[2]"),
                        Is.EqualTo(authoredDecoySlot));
                }
                finally
                {
                    DeleteSplitFolder();
                }
            }
            finally
            {
                DestroyCommittedClone(root, controller);
                DestroyGenerated(state);
                DestroyTracked();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void SetCurve(
            AnimationClip clip,
            string rendererPath,
            int slotIndex,
            params (float time, Material value)[] keys)
        {
            var keyframes = new ObjectReferenceKeyframe[keys.Length];
            for (var index = 0; index < keys.Length; index++)
            {
                keyframes[index] = new ObjectReferenceKeyframe
                {
                    time = keys[index].time,
                    value = keys[index].value,
                };
            }

            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    rendererPath,
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[" + slotIndex + "]"),
                keyframes);
        }

        /// <summary>
        /// Rewrites a described curve's material names to their mapped clones'
        /// names, preserving the described times, so an expected committed
        /// curve can be stated once and compared for both bindings.
        /// </summary>
        private static string MapNames(
            string describedCurve,
            IReadOnlyList<(Material source, Material mapped)> mapping)
        {
            var parts = describedCurve.Split('|');
            for (var index = 0; index < parts.Length; index++)
            {
                foreach (var (source, mapped) in mapping)
                {
                    if (parts[index].EndsWith("=>" + source.name,
                            StringComparison.Ordinal))
                    {
                        parts[index] =
                            parts[index].Substring(
                                0,
                                parts[index].Length - source.name.Length) +
                            mapped.name;
                        break;
                    }
                }
            }

            return string.Join("|", parts);
        }
    }
}
