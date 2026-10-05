using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf;
using NUnit.Framework;
using UnityEditor;
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
    /// <para>
    /// The fixture itself - the band model, the authored-triangle
    /// registry, the texture imports, the material builders, the animation
    /// fixture, the optimizer configuration, and the renderer factory -
    /// lives in <see cref="MergedConsumptionFixture"/> and is shared with
    /// the Avatar Optimizer merged-consumption suite. The expectation
    /// enum keeps the fixture's AAO member names:
    /// <see cref="MergedConsumptionFixture.TriangleExpectation.ConvertsToOpaque"/>
    /// carries this suite's former MovesToGeneratedShader meaning and
    /// <see cref="MergedConsumptionFixture.TriangleExpectation.StaysTransparent"/>
    /// the former StaysOnSourceShader meaning, because the shared factory
    /// registers the same band-to-expectation mapping.
    /// </para>
    /// </summary>
    public sealed class DaoMergedConsumptionTests
        : MergedConsumptionFixture
    {
        private const string DaoIntegrationEnvironmentVariable =
            "AMUSE_DAO_INTEGRATION";

        protected override string TempFolder =>
            "Assets/AmuseTests_DaoMerge";

        [TearDown]
        public void TearDownDaoOutput()
        {
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
                        optimizeTexture: false,
                        allowShuffleMaterialSlots: true);
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
                    new[] { 0, 0, 0 },
                    firstTriangleIndex: 0);
                CreateSkinnedRenderer(
                    root, "AMUSE dao cutout renderer",
                    "AMUSE dao cutout mesh",
                    new[] { cutout },
                    new[] { Band.Partial },
                    new[] { 0 },
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
                            == TriangleExpectation.ConvertsToOpaque;
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
