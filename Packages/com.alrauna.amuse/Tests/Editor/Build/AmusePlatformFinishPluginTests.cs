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
                var context = AvatarProcessor.ProcessAvatar(root, TestVrchatPlatform.Instance);

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

                context.GetState<ProducerProbe>().Produced = true;
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
        }

        public sealed class ObserverProbe
        {
            public bool SawProducerAndAmuse { get; set; }
        }

        private sealed class SyntheticPluginScope : System.IDisposable
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

        private sealed class TestVrchatPlatform : INDMFPlatformProvider
        {
            internal static readonly TestVrchatPlatform Instance = new TestVrchatPlatform();

            public string QualifiedName => WellKnownPlatforms.VRChatAvatar30;
            public string DisplayName => "AMUSE test VRChat";
        }
    }
}
