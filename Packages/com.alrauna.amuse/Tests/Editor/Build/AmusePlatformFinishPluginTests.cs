using Alrauna.Amuse.Editor.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
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

        [Test]
        public void CapturePassRetainsTheHostsExactAnimatorBindings()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE bindings capture fixture");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);

                var captured = context
                    .GetState<AmusePlatformFinishState>().AnimatorBindings;

                Assert.That(captured, Is.Not.Null,
                    "the capture pass did not retain the host's animator bindings");

                // The Task 1 lifetime gate independently reads
                // AnimatorServicesContext.ControllerContext.PlatformBindings from
                // its own extension-declaring pass in this same build. Comparing
                // against that observation proves AMUSE stored the host's own
                // object rather than a stub or a reconstructed stand-in.
                //
                // LIMITATION, stated so this is not read as more than it proves:
                // NDMF picks VRChatPlatformAnimatorBindings only for a root
                // carrying a VRCAvatarDescriptor under NDMF_VRCSDK3_AVATARS, and
                // the public project has no VRChat SDK, so every reachable branch
                // yields the GenericPlatformAnimatorBindings singleton. Reference
                // identity is therefore necessary but not sufficient here: it
                // cannot distinguish reading the context from hard-coding that
                // singleton. CaptureRequiresTheActiveAnimatorServicesContext pins
                // the acquisition route that this assertion cannot.
                var hostObserved = context
                    .GetState<Host.AnimatorBindingsLifetimeGateTests.GateProbe>()
                    .Captured;

                Assert.That(hostObserved, Is.Not.Null,
                    "the independent host observation did not run");
                Assert.That(captured, Is.SameAs(hostObserved),
                    "AMUSE did not retain the exact host binding reference");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CaptureRequiresTheActiveAnimatorServicesContext()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE capture acquisition-route fixture");

            try
            {
                // The generic platform skips the AMUSE plugin entirely, so no
                // capture has run and no animator extension is active here.
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);

                // BuildContext.Extension<T> raises a plain System.Exception, so
                // this pins the exact type NDMF actually throws rather than a
                // looser matcher that any incidental NullReferenceException would
                // also satisfy.
                var failure = Assert.Throws<System.Exception>(
                    () => AmuseAnimatorBindingsCapture.Execute(context),
                    "the capture did not acquire its bindings through the active " +
                    "AnimatorServicesContext, so it would silently produce a " +
                    "binding NDMF never handed it");

                Assert.That(failure.Message,
                    Does.Contain("AnimatorServicesContext"),
                    "the capture failed for some reason other than the animator " +
                    "extension being inactive");
                Assert.That(
                    context.GetState<AmusePlatformFinishState>().AnimatorBindings,
                    Is.Null,
                    "a failed acquisition must leave no binding behind");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheBarrierStillExecutesAlongsideTheNewCapturePass()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE two-pass fixture");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var amuse = context.GetState<AmusePlatformFinishState>();

                Assert.That(amuse.AnimatorBindings, Is.Not.Null,
                    "the capture pass did not run");
                Assert.That(amuse.HasExecuted, Is.True,
                    "adding the capture pass suppressed the existing barrier pass");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AnimatorServicesContextIsInactiveOnceTheAmusePassesHaveRun()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE post-barrier lifecycle fixture");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var observer = context.GetState<ObserverProbe>();

                Assert.That(observer.Ran, Is.True, "the observer pass did not run");
                Assert.That(observer.ContextWasInactive, Is.True,
                    "AnimatorServicesContext was still active after the AMUSE " +
                    "PlatformFinish passes, so AMUSE left the extension scope open " +
                    "and NDMF has not committed the animator graph");
                Assert.That(observer.RetainedBindings, Is.Not.Null,
                    "the retained bindings did not survive extension deactivation");

                // NOT PINNED HERE: that the BARRIER specifically is declared
                // outside the extension scope. This observer runs after the whole
                // AMUSE plugin, by which point the scope has closed either way, and
                // nothing the Task 23A barrier does depends on the extension. A
                // mutation moving the barrier inside WithRequiredExtension is
                // therefore invisible to every test in this file. It becomes
                // behaviourally load-bearing in Task 23, where the barrier
                // enumerates the COMMITTED graph and NDMF commits on deactivation:
                // inside the scope it would read uncommitted controllers. Task 23
                // must pin barrier placement there.
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RetainedAnimatorBindingsRemainOperationalAfterDeactivation()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE retained bindings operability fixture");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var observer = context.GetState<ObserverProbe>();

                Assert.That(observer.Ran, Is.True, "the observer pass did not run");
                Assert.That(observer.RetainedBindingsFailure, Is.Null,
                    "the retained bindings threw after deactivation: " +
                    observer.RetainedBindingsFailure);
                Assert.That(observer.RetainedBindingsOperable, Is.True,
                    "the retained bindings did not answer the narrowest " +
                    "non-mutating read after deactivation");
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

                var probe = context.GetState<ObserverProbe>();
                probe.Ran = true;
                probe.SawProducerAndAmuse =
                    context.GetState<ProducerProbe>().Produced &&
                    context.GetState<AmusePlatformFinishState>().HasExecuted;

                // Non-perturbing inactivity check, as established by the Task 1
                // lifetime gate: BuildContext.Extension<T> only reads the active
                // extension set and throws when absent, so catching that throw
                // observes the lifecycle without changing it.
                try
                {
                    context.Extension<AnimatorServicesContext>();
                    probe.ContextWasInactive = false;
                }
                catch (System.Exception)
                {
                    probe.ContextWasInactive = true;
                }

                probe.RetainedBindings = context
                    .GetState<AmusePlatformFinishState>().AnimatorBindings;
                if (probe.RetainedBindings == null)
                {
                    return;
                }

                // The narrowest non-mutating read approved by Tasks 1 and 6.
                var motion = new AnimationClip { name = "AMUSE observer probe clip" };
                try
                {
                    probe.RetainedBindingsOperable =
                        probe.RetainedBindings.IsSpecialMotion(motion) == false;
                }
                catch (System.Exception exception)
                {
                    probe.RetainedBindingsFailure = exception;
                }
                finally
                {
                    Object.DestroyImmediate(motion);
                }
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
            public bool Ran { get; set; }
            public bool SawProducerAndAmuse { get; set; }
            public bool ContextWasInactive { get; set; }
            public IPlatformAnimatorBindings RetainedBindings { get; set; }
            public bool RetainedBindingsOperable { get; set; }
            public System.Exception RetainedBindingsFailure { get; set; }
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
