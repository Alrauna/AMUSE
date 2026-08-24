using Alrauna.Amuse.Editor.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.fluent;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.ZzzAnonymousOptimizingProducerPlugin))]
[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.AfterAmusePlatformFinishObserverPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class AmusePlatformFinishPluginTests
    {
        [Test]
        public void PlatformFinishBarrierRunsAfterAnonymousOptimizingProducer()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE NDMF phase fixture");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);

                Assert.That(context.GetState<ProducerProbe>().Produced, Is.True);
                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.HasExecuted, Is.True);
                Assert.That(amuse.Lifecycle, Is.Not.Null);
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero);
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
                Assert.That(context.GetState<ObserverProbe>().SawProducerAndAmuse, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InjectedExactLifecycleAnalyzesAnonymousOptimizingOutput()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE produced-state fixture");
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            var sourceMesh = new Mesh();
            sourceMesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
            };
            sourceMesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 0);
            renderer.sharedMesh = sourceMesh;
            var sourceMaterial = new Material(Shader.Find("Unlit/Color"));
            renderer.sharedMaterials = new[] { sourceMaterial };
            BuildContext context = null;

            try
            {
                context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                Assert.That(context.GetState<ProducerProbe>().Produced, Is.True);

                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.Lifecycle.MayUsePositiveMutation, Is.True);
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1));
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                if (context != null)
                {
                    var probe = context.GetState<ProducerProbe>();
                    Object.DestroyImmediate(probe.ProducedMesh);
                    Object.DestroyImmediate(probe.ProducedMaterial);
                }

                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sourceMesh);
                Object.DestroyImmediate(sourceMaterial);
            }
        }

        [Test]
        public void ExactLifecyclePermitCountsAnUnsupportedRendererAsRefused()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE semantic refusal fixture");
            root.AddComponent<LineRenderer>();

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);

                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero);
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(1));
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnsupportedLifecycleDoesNotInspectAnyRenderer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE lifecycle refusal fixture");
            root.AddComponent<LineRenderer>();

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(unityVersion: "2022.3.22f2"));

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.Lifecycle.MayUsePositiveMutation, Is.False);
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero);
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static HostLifecycleFacts SupportedFacts(
            string unityVersion = "2022.3.22f1")
        {
            return new HostLifecycleFacts(
                unityVersion,
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

        [RunsOnAllPlatforms]
        public sealed class ZzzAnonymousOptimizingProducerPlugin : Plugin<ZzzAnonymousOptimizingProducerPlugin>
        {
            protected override void Configure()
            {
                InPhase(BuildPhase.Optimizing)
                    .Run("AMUSE test anonymous optimizing producer", Execute);
            }

            private static void Execute(BuildContext context)
            {
                if (!SyntheticPluginScope.IsArmed)
                {
                    return;
                }

                var probe = context.GetState<ProducerProbe>();
                probe.Produced = true;
                var renderer = context.AvatarRootObject
                    .GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (renderer == null)
                {
                    return;
                }

                var mesh = new Mesh();
                mesh.vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.up,
                };
                mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
                var material = new Material(Shader.Find("Unlit/Color"));
                renderer.sharedMesh = mesh;
                renderer.sharedMaterials = new[] { material };

                probe.ProducedMesh = mesh;
                probe.ProducedMaterial = material;
            }
        }

        public sealed class AfterAmusePlatformFinishObserverPlugin : Plugin<AfterAmusePlatformFinishObserverPlugin>
        {
            protected override void Configure()
            {
                InPhase(BuildPhase.PlatformFinish)
                    .AfterPlugin("com.alrauna.amuse")
                    .Run("AMUSE test PlatformFinish observer", Execute);
            }

            private static void Execute(BuildContext context)
            {
                if (!SyntheticPluginScope.IsArmed)
                {
                    return;
                }

                context.GetState<ObserverProbe>().SawProducerAndAmuse =
                    context.GetState<ProducerProbe>().Produced &&
                    context.GetState<AmusePlatformFinishState>().HasExecuted;
            }
        }

        public sealed class ProducerProbe
        {
            public bool Produced { get; set; }
            public Mesh ProducedMesh { get; set; }
            public Material ProducedMaterial { get; set; }
        }

        public sealed class ObserverProbe
        {
            public bool SawProducerAndAmuse { get; set; }
        }

        internal sealed class SyntheticPluginScope : System.IDisposable
        {
            private readonly bool previous;

            private SyntheticPluginScope()
            {
                previous = IsArmed;
                IsArmed = true;
            }

            internal static bool IsArmed { get; private set; }

            internal static SyntheticPluginScope Arm()
            {
                return new SyntheticPluginScope();
            }

            public void Dispose()
            {
                IsArmed = previous;
            }
        }

        internal sealed class TestVrchatPlatform : INDMFPlatformProvider
        {
            internal static readonly TestVrchatPlatform Instance = new TestVrchatPlatform();

            public string QualifiedName => WellKnownPlatforms.VRChatAvatar30;
            public string DisplayName => "AMUSE test VRChat";
        }

        private sealed class TestGenericPlatform : INDMFPlatformProvider
        {
            internal static readonly TestGenericPlatform Instance =
                new TestGenericPlatform();

            public string QualifiedName => "nadena.dev.ndmf.generic";
            public string DisplayName => "AMUSE test generic";
        }
    }
}
