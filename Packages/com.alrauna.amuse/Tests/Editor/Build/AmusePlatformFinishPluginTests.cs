using System.Collections.Generic;
using System.Reflection;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.fluent;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.ZzzAnonymousOptimizingProducerPlugin))]
[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.AfterAmusePlatformFinishObserverPlugin))]
[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.AmusePlatformFinishPluginTests.BarrierUnderActiveExtensionProbePlugin))]

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
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.Lifecycle.MayUsePositiveMutation, Is.True);
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1));
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1));
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
                SeedRetainedHostBindings(context);

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

                // The SDK-free public project resolves this host platform to
                // NDMF's exact GenericPlatformAnimatorBindings singleton. This
                // assertion pins the retained value; the sibling active-context
                // mutation test pins that AMUSE acquired it through
                // AnimatorServicesContext rather than constructing a stand-in.
                //
                // LIMITATION, stated so this is not read as more than it proves:
                // NDMF picks VRChatPlatformAnimatorBindings only for a root
                // carrying a VRCAvatarDescriptor under NDMF_VRCSDK3_AVATARS, and
                // the public project has no VRChat SDK, so every reachable branch
                // yields the GenericPlatformAnimatorBindings singleton. Reference
                // identity therefore cannot alone distinguish a context read from
                // hard-coding; CaptureRequiresTheActiveAnimatorServicesContext
                // remains the acquisition-route proof.
                Assert.That(captured,
                    Is.SameAs(GenericPlatformAnimatorBindings.Instance),
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

        [Test]
        public void BarrierRefusesToRunWhileAnimatorServicesContextIsStillActive()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE active-extension barrier fixture");

            try
            {
                // The probe plugin invokes the real barrier from inside its own
                // WithRequiredExtension scope. That is precisely the shape of the
                // mutation that moves AMUSE's barrier inside the extension: the
                // controllers NDMF commits on deactivation have not been written
                // back yet, so anything the barrier concluded about them would be
                // drawn from pre-commit state.
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestActiveExtensionPlatform.Instance);
                var probe = context.GetState<ActiveExtensionBarrierProbe>();

                Assert.That(probe.Ran, Is.True, "the probe pass did not run");
                Assert.That(probe.Failure, Is.Not.Null,
                    "the barrier ran to completion while AnimatorServicesContext " +
                    "was still active, so nothing prevents it from reasoning " +
                    "about uncommitted controllers");
                Assert.That(probe.Failure,
                    Is.TypeOf<System.InvalidOperationException>(),
                    "an active animator extension at the barrier is an " +
                    "implementation defect and must not be reported as any other " +
                    "failure type");
                Assert.That(probe.Failure.Message,
                    Does.Contain("AnimatorServicesContext"),
                    "the defect diagnostic does not name the lifecycle it pins");

                // The defect must abort the barrier outright rather than being
                // absorbed into the domain vocabulary or a half-run pass.
                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.HasExecuted, Is.False,
                    "the barrier marked itself executed before asserting its own " +
                    "lifecycle precondition");
                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "an implementation defect was converted into an avatar refusal");
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero);
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealBuildReachesTheLifecycleGateWithoutTrippingTheInactivityInvariant()
        {
            using var armed = SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE correct-placement control fixture");

            try
            {
                // Correct-placement control. A real build must reach the lifecycle
                // gate and stand down there for the missing VRChat SDK, WITHOUT the
                // inactivity invariant firing. ProcessAvatar rethrows a pass
                // failure, so an invariant that fired here would surface as a build
                // failure rather than a quiet assertion miss.
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestVrchatPlatform.Instance);
                var amuse = context.GetState<AmusePlatformFinishState>();

                Assert.That(amuse.HasExecuted, Is.True,
                    "the barrier did not run to completion under correct placement");
                Assert.That(amuse.Lifecycle, Is.Not.Null);

                // Stated as the limitation it is, not as a claim about AMUSE: this
                // public project ships no VRChat SDK, so the only platform the
                // plugin runs on can never satisfy HostLifecycleCapability here.
                Assert.That(amuse.Lifecycle.MayUsePositiveMutation, Is.False,
                    "unexpected: the public project appears to satisfy " +
                    "HostLifecycleCapability, which would change what every " +
                    "injected-facts fixture in this file is standing in for");
                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "a lifecycle stand-down must not be recorded as an avatar " +
                    "animation refusal");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnallowlistedBehaviourRefusesTheWholeAvatarWithoutAnalysis()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE unallowlisted behaviour fixture");
            var controller = new AnimatorController { name = "unallowlisted" };
            var fixture = default(AnalyzableRendererFixture);

            try
            {
                controller.AddLayer("L0");
                var state = controller.layers[0].stateMachine.AddState("S0");
                var behaviour = AttachProbeBehaviour(state);
                Assert.That(behaviour, Is.Not.Null,
                    "fixture precondition: Unity did not attach the probe behaviour");
                Assert.That(state.behaviours.Length, Is.EqualTo(1),
                    "fixture precondition: the behaviour is not on the state");

                root.AddComponent<Animator>().runtimeAnimatorController = controller;

                // An otherwise analyzable renderer, so a zero analyzed count means
                // the avatar refusal stopped analysis rather than there being
                // nothing to analyze. BehaviourFreeCommittedGraphIsNotRefused
                // proves this same renderer does analyze when nothing refuses.
                fixture = AddAnalyzableRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AvatarRefusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour),
                    "the exact avatar-scoped refusal from the real committed " +
                    "controller graph was not preserved");
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero,
                    "an avatar-scoped refusal must analyze no renderer");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "an avatar-scoped refusal must not be counted as a " +
                    "renderer-scoped refusal");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
                foreach (RendererAnalysisRefusal reason in
                         System.Enum.GetValues(typeof(RendererAnalysisRefusal)))
                {
                    Assert.That(amuse.RendererRefusalCount(reason), Is.Zero,
                        "an avatar-scoped refusal left per-renderer accounting " +
                        "for " + reason);
                }
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void BehaviourFreeCommittedGraphIsNotRefused()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE clean committed graph fixture");
            var controller = new AnimatorController { name = "clean" };
            var fixture = default(AnalyzableRendererFixture);

            try
            {
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("S0");
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
                fixture = AddVerifiedOpaqueRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "a behaviour-free committed graph must not be refused");
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "renderer analysis must continue past an admitted graph");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void AnimatedRendererWithUnclosedEvidenceRefusesWithoutPartialCurrentStateAnalysis()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE unclosed animation evidence fixture");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;
            Material swap = null;

            try
            {
                fixture = AddAnalyzableRenderer(root);
                swap = VerifiedOpaqueMaterial();
                controller = AddAnimatedMaterialAssignment(root, swap, out clip);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero,
                    "a failed animation-evidence closure was salvaged as " +
                    "current-state-only analysis");
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.EqualTo(1));
                Assert.That(amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .MaterialDependencyClosureFailed),
                    Is.EqualTo(1),
                    "the production barrier did not consume the closed-evidence " +
                    "result from the real graph capture route");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                if (swap != null) Object.DestroyImmediate(swap);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void VerifiedFixture_AnimatedSwapToTransparentMaterialRemovesOpacity()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var control = new GameObject("AMUSE admitted opaque reassertion");
            var swapped = new GameObject("AMUSE admitted transparent swap");
            var controlFixture = default(AnalyzableRendererFixture);
            var swappedFixture = default(AnalyzableRendererFixture);
            AnimatorController controlController = null;
            AnimatorController swappedController = null;
            AnimationClip controlClip = null;
            AnimationClip swappedClip = null;
            Material transparent = null;

            try
            {
                // The public project cannot publish vendor shader source assets.
                // This uses the already-accepted schema-complete Poiyomi fixture
                // and substitutes only source attestation/verified interpretation;
                // graph enumeration, clip observation, closure, slot admission,
                // resolution, classification, and intersection remain real.
                controlFixture = AddVerifiedOpaqueRenderer(control);
                controlController = AddAnimatedMaterialAssignment(
                    control, controlFixture.Material, out controlClip);

                swappedFixture = AddVerifiedOpaqueRenderer(swapped);
                transparent = VerifiedTransparentMaterial();
                swappedController = AddAnimatedMaterialAssignment(
                    swapped, transparent, out swappedClip);

                var controlState = ExecuteVerifiedPlatformFinish(control);
                var swappedState = ExecuteVerifiedPlatformFinish(swapped);

                Assert.That(controlState.AnimatorBindings,
                    Is.SameAs(GenericPlatformAnimatorBindings.Instance));
                Assert.That(controlState.AnalyzedRendererCount, Is.EqualTo(1));
                Assert.That(controlState.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(controlState.OpaqueCandidateTriangleCount,
                    Is.GreaterThan(0),
                    "the same orchestration route proved no opaque control");

                Assert.That(swappedState.AnimatorBindings,
                    Is.SameAs(GenericPlatformAnimatorBindings.Instance));
                Assert.That(swappedState.AnalyzedRendererCount, Is.EqualTo(1));
                Assert.That(swappedState.SemanticallyRefusedRendererCount, Is.Zero);
                Assert.That(swappedState.OpaqueCandidateTriangleCount, Is.Zero,
                    "a face opaque only in the current state must not be counted");

                Assert.That(
                    AnimationUtility.GetObjectReferenceCurveBindings(controlClip),
                    Has.Length.EqualTo(1));
                Assert.That(
                    AnimationUtility.GetObjectReferenceCurveBindings(swappedClip)[0]
                        .propertyName,
                    Is.EqualTo("m_Materials.Array.data[0]"),
                    "the production-entry theorem lost the real material binding");
            }
            finally
            {
                DisposeAnalyzableRenderer(controlFixture);
                DisposeAnalyzableRenderer(swappedFixture);
                if (transparent != null) Object.DestroyImmediate(transparent);
                DestroyCommittedClone(control, controlController);
                DestroyCommittedClone(swapped, swappedController);
                Object.DestroyImmediate(control);
                Object.DestroyImmediate(swapped);
                if (controlClip != null) Object.DestroyImmediate(controlClip);
                if (swappedClip != null) Object.DestroyImmediate(swappedClip);
                if (controlController != null)
                    DestroyControllerGraph(controlController);
                if (swappedController != null)
                    DestroyControllerGraph(swappedController);
            }
        }

        [Test]
        public void RuntimeStateProductionEntry_RefusalCountsOnceAndContinuesLaterRenderer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE runtime-state accounting");
            var refusedObject = new GameObject("refused");
            refusedObject.transform.SetParent(root.transform);
            var laterObject = new GameObject("later");
            laterObject.transform.SetParent(root.transform);
            var refused = default(AnalyzableRendererFixture);
            var later = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                refused = AddVerifiedOpaqueRenderer(refusedObject);
                later = AddVerifiedOpaqueRenderer(laterObject);
                clip = new AnimationClip { name = "AMUSE one refused renderer" };
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "refused",
                        typeof(SkinnedMeshRenderer),
                        "material._AlphaForceOpaque"),
                    AnimationCurve.Constant(0f, 1f, 0f));
                controller = new AnimatorController { name = "AMUSE accounting graph" };
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("S0").motion = clip;
                root.AddComponent<Animator>().runtimeAnimatorController = controller;

                var order = root.GetComponentsInChildren<Renderer>(true);
                Assert.That(order, Has.Length.EqualTo(2));
                Assert.That(order[0], Is.SameAs(refused.Renderer),
                    "fixture precondition: the refusing renderer must run first");
                Assert.That(order[1], Is.SameAs(later.Renderer),
                    "fixture precondition: the successful renderer must run later");

                var amuse = ExecuteVerifiedPlatformFinish(root);

                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(1));
                Assert.That(amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AnimatedMaterialPropertyNotSingleton),
                    Is.EqualTo(1));
                foreach (RendererAnalysisRefusal reason in
                         System.Enum.GetValues(typeof(RendererAnalysisRefusal)))
                {
                    if (reason == RendererAnalysisRefusal.None ||
                        reason == RendererAnalysisRefusal
                            .AnimatedMaterialPropertyNotSingleton)
                    {
                        continue;
                    }

                    Assert.That(amuse.RendererRefusalCount(reason), Is.Zero,
                        "unexpected refusal bucket " + reason);
                }

                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "the Task-24 refusal stopped the later renderer");
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.EqualTo(1),
                    "the refused renderer contributed opacity or the later " +
                    "renderer failed to contribute its one opaque triangle");
            }
            finally
            {
                DisposeAnalyzableRenderer(refused);
                DisposeAnalyzableRenderer(later);
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AllUnknownCurrentMaterialRefusesExactlyWithOrWithoutUnrelatedClip(
            bool addUnrelatedClip)
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE all-Unknown envelope");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                if (addUnrelatedClip)
                {
                    clip = new AnimationClip { name = "unrelated transform" };
                    AnimationUtility.SetEditorCurve(
                        clip,
                        EditorCurveBinding.FloatCurve(
                            "missing",
                            typeof(Transform),
                            "m_LocalPosition.x"),
                        AnimationCurve.Constant(0f, 1f, 2f));
                    controller = new AnimatorController { name = "unrelated graph" };
                    controller.AddLayer("L0");
                    controller.layers[0].stateMachine.AddState("S0").motion = clip;
                    root.AddComponent<Animator>().runtimeAnimatorController = controller;
                }

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);
                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    _ => UnityMaterialSemantics.AllUnknown());
                var amuse = context.GetState<AmusePlatformFinishState>();

                Assert.That(amuse.AnalyzedRendererCount, Is.Zero);
                Assert.That(amuse.SemanticallyRefusedRendererCount,
                    Is.EqualTo(1));
                Assert.That(amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AdmittedMaterialSemanticsUnknown),
                    Is.EqualTo(1));
                Assert.That(amuse.OpaqueCandidateTriangleCount, Is.Zero);
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                DestroyCommittedClone(root, controller);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_AnimatedMeshReplacementWinsBeforeAdmission()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE structural mesh replacement");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AddDifferingAnimatedProperty(clip);
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        string.Empty,
                        typeof(SkinnedMeshRenderer),
                        "m_Mesh"),
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f,
                            value = fixture.Mesh,
                        },
                    });

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.Clips[0].ObjectBindings,
                    Has.Some.Property("PropertyName").EqualTo("m_Mesh"),
                    "fixture precondition: the structural binding was not captured");
                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Some.Property("PropertyName")
                        .EqualTo("material._AlphaForceOpaque"),
                    "fixture precondition: the downstream disagreement is absent");
                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.AnimatedMeshReplacement),
                    "structural refusal did not win before property admission");
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_AnimatedMaterialSlotCountRefusesExactly()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE structural slot count");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.size"),
                    AnimationCurve.Constant(0f, 1f, 1f));

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Some.Property("PropertyName")
                        .EqualTo("m_Materials.Array.size"),
                    "fixture precondition: the slot-count binding was not captured");
                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.AnimatedMaterialSlotCount));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_OverBudgetWinsBeforeGeometry()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE admitted-state budget");
            var materials = new List<Material>();
            AnimatorController controller = null;
            AnimationClip clip = null;
            Mesh mesh = null;

            try
            {
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                mesh = new Mesh
                {
                    vertices = new[]
                    {
                        Vector3.zero,
                        Vector3.right,
                    },
                    subMeshCount = 8,
                };
                for (var slot = 0; slot < mesh.subMeshCount; slot++)
                {
                    // Deliberately unsupported geometry. Correct ordering returns
                    // the budget refusal before this is inspected.
                    mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, slot);
                }

                renderer.sharedMesh = mesh;
                var current = VerifiedOpaqueMaterial();
                materials.Add(current);
                var currentSlots = new Material[mesh.subMeshCount];
                for (var slot = 0; slot < currentSlots.Length; slot++)
                    currentSlots[slot] = current;
                renderer.sharedMaterials = currentSlots;

                controller = AddOverBudgetMaterialAssignments(
                    root, mesh.subMeshCount, materials, out clip);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);
                var evidence = CaptureVerifiedRuntimeStateEvidence(root, renderer);
                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);
                var amuse = context.GetState<AmusePlatformFinishState>();

                Assert.That(evidence.IsClosed, Is.True);
                Assert.That(evidence.Clips[0].ObjectBindings,
                    Has.Count.EqualTo(mesh.subMeshCount));
                Assert.That(amuse.RendererRefusalCount(
                        RendererAnalysisRefusal.AdmittedStateBudgetExceeded),
                    Is.EqualTo(1),
                    "geometry ran before the admitted-state budget");
                Assert.That(amuse.AnalyzedRendererCount, Is.Zero);
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(1));
            }
            finally
            {
                foreach (var material in materials)
                {
                    if (material != null) Object.DestroyImmediate(material);
                }

                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_AdditiveLayerWithRelevantPropertyRefuses()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE additive material property");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AddAgreeingAnimatedProperty(clip);
                SetFirstLayerBlendingMode(
                    controller, AnimatorLayerBlendingMode.Additive);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.HasAdditiveLayer, Is.True,
                    "fixture precondition: graph capture missed the additive layer");
                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Some.Property("PropertyName")
                        .EqualTo("material._AlphaForceOpaque"),
                    "fixture precondition: no relevant material property was captured");
                Assert.That(result.Refusal, Is.EqualTo(
                    RendererAnalysisRefusal
                        .AdditiveLayerWithProofRelevantMaterialProperty));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_UnnormalizedDirectWithRelevantPropertyRefuses()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE direct material property");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;
            BlendTree tree = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AddAgreeingAnimatedProperty(clip);
                tree = ReplaceFirstStateWithUnnormalizedDirectTree(
                    controller, clip);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.HasUnnormalizedDirectBlendTree, Is.True,
                    "fixture precondition: graph capture missed the Direct tree");
                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Some.Property("PropertyName")
                        .EqualTo("material._AlphaForceOpaque"),
                    "fixture precondition: no relevant material property was captured");
                Assert.That(result.Refusal, Is.EqualTo(
                    RendererAnalysisRefusal
                        .UnnormalizedDirectBlendTreeWithProofRelevantMaterialProperty));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (tree != null) Object.DestroyImmediate(tree);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_AdditiveFlagAloneDoesNotRefuseRenderer()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE additive flag only");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                SetFirstLayerBlendingMode(
                    controller, AnimatorLayerBlendingMode.Additive);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.HasAdditiveLayer, Is.True,
                    "fixture precondition: graph capture missed the additive layer");
                Assert.That(evidence.Clips[0].FloatBindings, Is.Empty,
                    "fixture precondition: the control unexpectedly has a material " +
                    "property binding");
                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.None),
                    "a graph-global flag refused a renderer without a proof-relevant " +
                    "animated material property");
                Assert.That(result.OpaqueCandidateTriangleCount, Is.GreaterThan(0));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_AgreeingPropertyRemainsOpaque()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE agreeing property");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AddAgreeingAnimatedProperty(clip);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Some.Property("PropertyName")
                        .EqualTo("material._AlphaForceOpaque"));
                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.None));
                Assert.That(result.OpaqueCandidateTriangleCount, Is.GreaterThan(0),
                    "an exact singleton reassertion invalidated proven opacity");
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_DifferingPropertyRefusesExactly()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE differing property");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AddDifferingAnimatedProperty(clip);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out _);

                Assert.That(result.Refusal, Is.EqualTo(
                    RendererAnalysisRefusal
                        .AnimatedMaterialPropertyNotSingleton));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_NonFiniteExactCurveRefusesExactly()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE non-finite-exact property");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(SkinnedMeshRenderer),
                        "material._AlphaForceOpaque"),
                    new AnimationCurve(
                        new Keyframe(0f, 1f) { outTangent = 5f },
                        new Keyframe(1f, 1f) { inTangent = -5f }));

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Some.Property("IsFiniteExact").False,
                    "fixture precondition: the curve was not captured as unsupported");
                Assert.That(result.Refusal, Is.EqualTo(
                    RendererAnalysisRefusal.UnsupportedAnimationCurveForm));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_AbsentPropertyInSwapRefusesExactly()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE absent property in swap");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;
            Material withoutProperty = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                withoutProperty = new Material(Shader.Find("Unlit/Color"));
                controller = AddAnimatedMaterialAssignment(
                    root, withoutProperty, out clip);
                AddAgreeingAnimatedProperty(clip);

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(evidence.AdmittedMaterials, Has.Count.EqualTo(2));
                Assert.That(result.Refusal, Is.EqualTo(
                    RendererAnalysisRefusal
                        .AnimatedPropertyAbsentFromAdmittedMaterial));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                if (withoutProperty != null)
                    Object.DestroyImmediate(withoutProperty);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_TextureScaleUsesCapturedClosedRequest()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE closed texture request");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(SkinnedMeshRenderer),
                        "material._MainTex_ST.x"),
                    AnimationCurve.Constant(0f, 1f, 1f));

                var result = AnalyzeVerifiedRuntimeStates(
                    root, fixture.Renderer, out var evidence);

                Assert.That(
                    UnityAnimationEvidenceCapture
                        .DeriveTextureScaleOffsetProperties(
                            evidence.RelevanceRequest),
                    Contains.Item("_MainTex_ST"),
                    "fixture precondition: only the closed request can own _MainTex_ST");
                Assert.That(evidence.Clips[0].FloatBindings,
                    Has.Count.EqualTo(1),
                    "fixture precondition: the real _ST curve was not captured");
                Assert.That(
                    UnityAnimationEvidenceCapture.ResolveProofRelevant(
                        evidence.Clips[0].FloatBindings[0],
                        string.Empty,
                        evidence.RelevanceRequest,
                        out var reference),
                    Is.EqualTo(ProofRelevantBindingResolution.RendererWide),
                    "fixture precondition: the closed request did not resolve " +
                    "the captured _ST owner");
                Assert.That(reference.Kind,
                    Is.EqualTo(AnimatedPropertyKind.TextureScaleOffsetComponent));
                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.None));
                Assert.That(result.OpaqueCandidateTriangleCount, Is.GreaterThan(0));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStateIntegration_ClosedEvidenceDoesNotRecaptureLiveMaterials()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE immutable material evidence");
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip);
                var evidence = CaptureVerifiedRuntimeStateEvidence(
                    root, fixture.Renderer);

                Assert.That(evidence.IsClosed, Is.True);
                Assert.That(evidence.CurrentMaterialIndices, Has.Count.EqualTo(1));
                var path = AnimationUtility.CalculateTransformPath(
                    fixture.Renderer.transform, root.transform);
                var snapshot = CaptureRuntimeStateGeometry(
                    fixture.Renderer, evidence);

                // Once capture has closed, the live material array is outside the
                // runtime-state proof boundary. Clearing it makes any later
                // renderer.sharedMaterials read observable without changing the
                // captured slot facts or the geometry under proof.
                fixture.Renderer.sharedMaterials = System.Array.Empty<Material>();

                var result = AmusePlatformFinishPass.AnalyzeRuntimeStatesForTests(
                    path,
                    evidence,
                    snapshot,
                    VerifiedAlphaOnly);

                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.None),
                    "closed evidence was replaced by a second live material snapshot");
                Assert.That(result.OpaqueCandidateTriangleCount, Is.GreaterThan(0));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void RuntimeStatePureEntryAcceptsOnlyImmutableProofInputs()
        {
            var method = typeof(AmusePlatformFinishPass).GetMethod(
                "AnalyzeRuntimeStatesForTests",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(string),
                    typeof(CapturedAnimationEvidence),
                    typeof(UnityRendererAlphaSnapshot),
                    typeof(CapturedAlphaMaterialSemanticsResolver),
                },
                null);

            Assert.That(method, Is.Not.Null,
                "the pure runtime-state entry still accepts a live host object");
            foreach (var parameter in method.GetParameters())
            {
                Assert.That(
                    typeof(UnityEngine.Object).IsAssignableFrom(
                        parameter.ParameterType),
                    Is.False,
                    parameter.Name + " accepts a live Unity object");
            }

            Assert.That(
                method.ReturnType.GetGenericArguments(),
                Has.None.AssignableTo<UnityEngine.Object>());
        }

        [Test]
        public void ClosedPathAndGeometryIgnoreLaterRendererRenameAndMeshReplacement()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE immutable runtime-state host");
            var child = new GameObject("Body");
            child.transform.SetParent(root.transform, false);
            var fixture = default(AnalyzableRendererFixture);
            AnimatorController controller = null;
            AnimationClip clip = null;
            Mesh replacement = null;

            try
            {
                fixture = AddVerifiedOpaqueRenderer(child);
                controller = AddAnimatedMaterialAssignment(
                    root, fixture.Material, out clip, "Body");
                var rendererPath = AnimationUtility.CalculateTransformPath(
                    fixture.Renderer.transform, root.transform);
                var evidence = CaptureVerifiedRuntimeStateEvidence(
                    root, fixture.Renderer);
                var current = new[]
                {
                    evidence.AdmittedMaterials[
                        evidence.CurrentMaterialIndices[0]],
                };
                var extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                    fixture.Renderer, current);
                Assert.That(extraction.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.None));

                child.name = "RenamedAfterClosure";
                child.transform.SetParent(null, false);
                replacement = new Mesh();
                replacement.vertices = new[] { Vector3.zero, Vector3.right };
                replacement.SetIndices(
                    new[] { 0, 1 }, MeshTopology.Lines, 0);
                fixture.Renderer.sharedMesh = replacement;

                var result = AmusePlatformFinishPass.AnalyzeRuntimeStatesForTests(
                    rendererPath,
                    evidence,
                    extraction.Snapshot,
                    VerifiedAlphaOnly);

                Assert.That(result.Refusal,
                    Is.EqualTo(RendererAnalysisRefusal.None));
                Assert.That(result.OpaqueCandidateTriangleCount,
                    Is.EqualTo(1));
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                if (replacement != null) Object.DestroyImmediate(replacement);
                Object.DestroyImmediate(root);
                if (child != null) Object.DestroyImmediate(child);
                if (clip != null) Object.DestroyImmediate(clip);
                if (controller != null) DestroyControllerGraph(controller);
            }
        }

        [Test]
        public void PositiveLifecycleWithoutRetainedBindingsIsAnImplementationDefect()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE missing bindings fixture");

            try
            {
                // Deliberately NOT seeded. Supported lifecycle facts assert the
                // premise that Task 23A's capture already ran, so their absence is
                // an integration defect in the caller, never a domain refusal.
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);

                var failure = Assert.Throws<System.InvalidOperationException>(
                    () => AmusePlatformFinishPass.Execute(context, SupportedFacts()),
                    "a positive lifecycle permission with no retained animator " +
                    "bindings was tolerated");
                Assert.That(failure.Message, Does.Contain("animator bindings"));

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "a missing capability was converted into an avatar refusal");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "a missing capability was counted as a renderer refusal");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExactRendererRefusalReasonIsCounted()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE exact renderer refusal fixture");
            root.AddComponent<LineRenderer>();

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal.UnsupportedRendererType),
                    Is.EqualTo(1),
                    "the exact renderer refusal reason was not counted");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(1),
                    "the total refused count drifted from the per-reason buckets");
                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "a renderer-scoped refusal escalated to avatar scope");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DifferentRendererRefusalReasonsRemainDistinct()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE distinct refusal reasons fixture");
            root.AddComponent<LineRenderer>();
            var missingMesh = new GameObject("no mesh");
            missingMesh.transform.SetParent(root.transform);
            missingMesh.AddComponent<SkinnedMeshRenderer>();

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(context, SupportedFacts());

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal.UnsupportedRendererType),
                    Is.EqualTo(1),
                    "the LineRenderer's reason was not counted in its own bucket");
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal.MissingMesh),
                    Is.EqualTo(1),
                    "the meshless renderer's reason was not counted in its own " +
                    "bucket, so the two reasons have been collapsed");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererRefusalDoesNotStopLaterRenderers()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE refusal continuation fixture");

            // The analyzable renderer is surrounded by refusing ones, so it is
            // counted only if analysis continued past a refusal — whatever order
            // GetComponentsInChildren happens to use.
            root.AddComponent<LineRenderer>();
            var analyzable = new GameObject("analyzable");
            analyzable.transform.SetParent(root.transform);
            var trailingRefusal = new GameObject("trailing refusal");
            trailingRefusal.transform.SetParent(root.transform);
            trailingRefusal.AddComponent<LineRenderer>();
            var fixture = default(AnalyzableRendererFixture);

            try
            {
                fixture = AddVerifiedOpaqueRenderer(analyzable);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.EqualTo(2));
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "a renderer-scoped refusal stopped the whole avatar instead of " +
                    "only that renderer");
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NoneIsNeverCountedAsARefusal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE none-is-not-a-refusal fixture");
            var fixture = default(AnalyzableRendererFixture);

            try
            {
                fixture = AddVerifiedOpaqueRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);

                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "control: this renderer must actually analyze");
                Assert.That(
                    amuse.RendererRefusalCount(RendererAnalysisRefusal.None),
                    Is.Zero,
                    "None was exposed as a refusal bucket");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero);

                // The accounting API refuses to record None at all, so no future
                // call site can quietly turn a success into a refusal.
                Assert.Throws<System.ArgumentException>(
                    () => amuse.RecordRendererRefusal(
                        RendererAnalysisRefusal.None),
                    "None was accepted as a recordable refusal reason");
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RendererAnalysisRaisesDefectsInsteadOfNamingThemRefusals()
        {
            var root = new GameObject("AMUSE renderer defect fixture");
            var renderer = root.AddComponent<SkinnedMeshRenderer>();

            try
            {
                // A destroyed renderer is a programming defect in the caller, not
                // an unsupported avatar construct. Renderer analysis is otherwise
                // deliberately fail-closed — every malformed mesh, slot-count, and
                // topology case returns a named RendererAnalysisRefusal — so this
                // is the one defect its own contract raises, and it must stay an
                // exception rather than joining the refusal vocabulary.
                Object.DestroyImmediate(renderer);

                var failure = Assert.Throws<System.ArgumentException>(
                    () => UnityRendererAlphaAnalysis.Capture(renderer),
                    "renderer analysis absorbed a destroyed renderer instead of " +
                    "raising it");
                Assert.That(failure.Message, Does.Contain("destroyed"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BarrierPreconditionDefectsPropagateOutOfThePublicEntrypoint()
        {
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE defect propagation fixture");
            var fixture = default(AnalyzableRendererFixture);

            try
            {
                // Scope, stated exactly: this pins that a defect raised in the
                // barrier's own precondition path leaves the public entrypoint as
                // an exception, rather than becoming a refusal, a counter, or a
                // silent return. It does NOT pin catch-freedom inside the renderer
                // loop — the once-guard throws from PendingState, before the loop
                // is entered. That remains unpinned by construction: renderer
                // analysis is deliberately fail-closed, so no avatar content
                // reaches it with a defect (probed: a destroyed shader, a
                // destroyed texture, and a null material slot all refuse or
                // analyze cleanly). The two defects analysis does raise —
                // Capture(destroyed renderer) and Analyze(null) — are unreachable
                // from the loop, which is why the sibling test pins the analysis
                // contract directly instead.
                fixture = AddVerifiedOpaqueRenderer(root);

                var context = AvatarProcessor.ProcessAvatar(
                    root, TestGenericPlatform.Instance);
                SeedRetainedHostBindings(context);
                AmusePlatformFinishPass.Execute(
                    context,
                    SupportedFacts(),
                    TryAttestVerifiedFixture,
                    CaptureVerifiedFixtureMaterials,
                    VerifiedAlphaOnly);

                var amuse = context.GetState<AmusePlatformFinishState>();
                Assert.That(amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    "precondition: the renderer loop must have been reached");

                var failure = Assert.Throws<System.InvalidOperationException>(
                    () => AmusePlatformFinishPass.Execute(
                        context, SupportedFacts()),
                    "an invariant defect raised inside the barrier was swallowed");
                Assert.That(failure.Message, Does.Contain("more than once"));

                Assert.That(amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    "a defect was converted into an avatar refusal");
                Assert.That(amuse.SemanticallyRefusedRendererCount, Is.Zero,
                    "a defect was counted as a renderer refusal");
                foreach (RendererAnalysisRefusal reason in
                         System.Enum.GetValues(typeof(RendererAnalysisRefusal)))
                {
                    Assert.That(amuse.RendererRefusalCount(reason), Is.Zero,
                        "a defect was counted in the " + reason + " bucket");
                }
            }
            finally
            {
                DisposeAnalyzableRenderer(fixture);
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// NDMF's own generic-platform bindings, seeded where a fixture supplies
        /// exact <em>supported</em> lifecycle facts. Supported facts assert the
        /// premise that Task 23A's capture pass already ran, and this is not a test
        /// double: it is the exact <see cref="IPlatformAnimatorBindings"/>
        /// implementation that capture empirically obtains in this SDK-free public
        /// project. Production never falls back to it.
        /// </summary>
        private static void SeedRetainedHostBindings(BuildContext context)
        {
            context.GetState<AmusePlatformFinishState>().AnimatorBindings =
                GenericPlatformAnimatorBindings.Instance;
        }

        private static AmusePlatformFinishState ExecuteVerifiedPlatformFinish(
            GameObject root)
        {
            var context = AvatarProcessor.ProcessAvatar(
                root, TestGenericPlatform.Instance);
            SeedRetainedHostBindings(context);
            AmusePlatformFinishPass.Execute(
                context,
                SupportedFacts(),
                TryAttestVerifiedFixture,
                CaptureVerifiedFixtureMaterials,
                VerifiedAlphaOnly);
            return context.GetState<AmusePlatformFinishState>();
        }

        private static StateMachineBehaviour AttachProbeBehaviour(
            AnimatorState state)
        {
            // Task 7 established that an Editor-assembly StateMachineBehaviour is
            // not reliably attachable on Unity 2022.3.22f1, hence the existing
            // runtime-compatible Assembly-CSharp probe, attached through Unity's
            // real AddStateMachineBehaviour(Type). BehaviourIdentity's allowlist is
            // empty, so any behaviour type refuses.
            return state.AddStateMachineBehaviour(System.Type.GetType(
                "Alrauna.Amuse.TestFixtures.AMUSETask7StateMachineBehaviourProbe, " +
                "Assembly-CSharp",
                true));
        }

        private struct AnalyzableRendererFixture
        {
            internal SkinnedMeshRenderer Renderer;
            internal Mesh Mesh;
            internal Material Material;
        }

        private static AnalyzableRendererFixture AddAnalyzableRenderer(
            GameObject root)
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            var material = new Material(Shader.Find("Unlit/Color"));
            var renderer = root.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = new[] { material };
            return new AnalyzableRendererFixture
            {
                Renderer = renderer,
                Mesh = mesh,
                Material = material,
            };
        }

        private static AnalyzableRendererFixture AddVerifiedOpaqueRenderer(
            GameObject root)
        {
            var fixture = AddAnalyzableRenderer(root);
            Object.DestroyImmediate(fixture.Material);
            fixture.Material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            fixture.Material.SetFloat("_AlphaForceOpaque", 1f);
            fixture.Renderer.sharedMaterials = new[] { fixture.Material };
            return fixture;
        }

        private static Material VerifiedTransparentMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetColor("_Color", new Color(1f, 1f, 1f, 0.5f));
            return material;
        }

        private static Material VerifiedOpaqueMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        private static void AddDifferingAnimatedProperty(AnimationClip clip)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    string.Empty,
                    typeof(SkinnedMeshRenderer),
                    "material._AlphaForceOpaque"),
                AnimationCurve.Constant(0f, 1f, 0f));
        }

        private static void AddAgreeingAnimatedProperty(AnimationClip clip)
        {
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    string.Empty,
                    typeof(SkinnedMeshRenderer),
                    "material._AlphaForceOpaque"),
                AnimationCurve.Constant(0f, 1f, 1f));
        }

        private static void SetFirstLayerBlendingMode(
            AnimatorController controller,
            AnimatorLayerBlendingMode blendingMode)
        {
            var layers = controller.layers;
            layers[0].blendingMode = blendingMode;
            controller.layers = layers;
        }

        private static BlendTree ReplaceFirstStateWithUnnormalizedDirectTree(
            AnimatorController controller,
            AnimationClip clip)
        {
            var tree = new BlendTree
            {
                name = "AMUSE unnormalized Direct",
                blendType = BlendTreeType.Direct,
            };
            tree.AddChild(clip);

            var serialized = new SerializedObject(tree);
            serialized.Update();
            var normalized = serialized.FindProperty("m_NormalizedBlendValues");
            Assert.That(normalized, Is.Not.Null,
                "fixture precondition: Unity did not expose Direct normalization");
            normalized.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            controller.layers[0].stateMachine.states[0].state.motion = tree;
            return tree;
        }

        private static AnimatorController AddOverBudgetMaterialAssignments(
            GameObject root,
            int slotCount,
            ICollection<Material> ownedMaterials,
            out AnimationClip clip)
        {
            clip = new AnimationClip { name = "AMUSE over-budget assignments" };
            for (var slot = 0; slot < slotCount; slot++)
            {
                var first = VerifiedOpaqueMaterial();
                var second = VerifiedOpaqueMaterial();
                ownedMaterials.Add(first);
                ownedMaterials.Add(second);
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        string.Empty,
                        typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[" + slot + "]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe
                        {
                            time = 0f,
                            value = first,
                        },
                        new ObjectReferenceKeyframe
                        {
                            time = 1f,
                            value = second,
                        },
                    });
            }

            var controller = new AnimatorController { name = "AMUSE budget graph" };
            controller.AddLayer("L0");
            controller.layers[0].stateMachine.AddState("S0").motion = clip;
            root.AddComponent<Animator>().runtimeAnimatorController = controller;
            return controller;
        }

        private static AnimatorController AddAnimatedMaterialAssignment(
            GameObject root,
            Material material,
            out AnimationClip clip,
            string path = "")
        {
            clip = new AnimationClip { name = "AMUSE material assignment" };
            AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        path,
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]"),
                new[]
                {
                    new ObjectReferenceKeyframe
                    {
                        time = 0f,
                        value = material,
                    },
                });

            var controller = new AnimatorController { name = "AMUSE material graph" };
            controller.AddLayer("L0");
            controller.layers[0].stateMachine.AddState("S0").motion = clip;
            root.AddComponent<Animator>().runtimeAnimatorController = controller;
            return controller;
        }

        private static (RendererAnalysisRefusal Refusal,
                        int OpaqueCandidateTriangleCount)
            AnalyzeVerifiedRuntimeStates(
                GameObject root,
                Renderer renderer,
                out CapturedAnimationEvidence evidence)
        {
            evidence = CaptureVerifiedRuntimeStateEvidence(root, renderer);
            var path = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);
            var snapshot = CaptureRuntimeStateGeometry(renderer, evidence);
            return AmusePlatformFinishPass.AnalyzeRuntimeStatesForTests(
                path, evidence, snapshot, VerifiedAlphaOnly);
        }

        private static UnityRendererAlphaSnapshot CaptureRuntimeStateGeometry(
            Renderer renderer,
            CapturedAnimationEvidence evidence)
        {
            var current = new CapturedAlphaMaterial[
                evidence.CurrentMaterialIndices.Count];
            for (var slot = 0; slot < current.Length; slot++)
            {
                current[slot] = evidence.AdmittedMaterials[
                    evidence.CurrentMaterialIndices[slot]];
            }

            var extraction = UnityRendererAlphaAnalysis.CaptureGeometry(
                renderer, current);
            Assert.That(extraction.Refusal,
                Is.EqualTo(RendererAnalysisRefusal.None));
            return extraction.Snapshot;
        }

        private static CapturedAnimationEvidence
            CaptureVerifiedRuntimeStateEvidence(
                GameObject root,
                Renderer renderer)
        {
            var graph = CommittedControllerGraph.Enumerate(
                root, GenericPlatformAnimatorBindings.Instance);
            Assert.That(graph.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
            Assert.That(graph.Layers, Has.Count.EqualTo(1));
            Assert.That(graph.Layers[0].Clips, Has.Count.EqualTo(1));

            return UnityAnimationEvidenceCapture.CaptureGraphForTests(
                renderer.sharedMaterials,
                graph,
                GenericPlatformAnimatorBindings.Instance,
                TryAttestVerifiedFixture,
                CaptureVerifiedFixtureMaterials);
        }

        private static bool TryAttestVerifiedFixture(
            Material material,
            out CapturedAlphaMaterialFamily family,
            out MaterialEvidenceRequest request)
        {
            family = CapturedAlphaMaterialFamily.Poiyomi;
            request = PoiyomiMaterialSemantics.AlphaEvidenceRequest;
            return material != null;
        }

        private static bool CaptureVerifiedFixtureMaterials(
            IReadOnlyList<Material> materials,
            IReadOnlyList<CapturedAlphaMaterialFamily> families,
            MaterialEvidenceRequest request,
            out IReadOnlyList<CapturedAlphaMaterial> captured)
        {
            var inputs = new MaterialEvidenceCaptureInput[materials.Count];
            for (var index = 0; index < materials.Count; index++)
            {
                inputs[index] = new MaterialEvidenceCaptureInput(
                    materials[index], request);
            }

            var evidence = UnityMaterialEvidenceCapture.Capture(inputs);
            var result = new CapturedAlphaMaterial[materials.Count];
            for (var index = 0; index < result.Length; index++)
            {
                result[index] = new CapturedAlphaMaterial(
                    families[index],
                    evidence[index],
                    default(PoiyomiSourceEvidence),
                    null);
            }

            captured = result;
            return true;
        }

        private static MaterialSemantics VerifiedAlphaOnly(
            CapturedAlphaMaterial material)
        {
            return new MaterialSemantics(
                SemanticOutput<ColorSemanticValue>.Unknown(),
                PoiyomiMaterialSemantics.InterpretVerifiedAlpha(
                    material.Evidence),
                SemanticOutput<ColorSemanticValue>.Unknown(),
                SemanticOutput<NormalSemanticValue>.Unknown());
        }

        private static void DisposeAnalyzableRenderer(
            AnalyzableRendererFixture fixture)
        {
            if (fixture.Mesh != null) Object.DestroyImmediate(fixture.Mesh);
            if (fixture.Material != null) Object.DestroyImmediate(fixture.Material);
        }

        /// <summary>
        /// NDMF virtualizes and commits animator controllers whenever an active
        /// extension deactivates, so the Animator ends the build holding a clone
        /// rather than the fixture's own controller. Nothing else owns that clone
        /// under a null temporary-asset directory.
        /// </summary>
        private static void DestroyCommittedClone(
            GameObject root, AnimatorController original)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null) return;

            var committed = animator.runtimeAnimatorController;
            if (committed == null || ReferenceEquals(committed, original)) return;

            animator.runtimeAnimatorController = null;
            if (committed is AnimatorController controller)
            {
                DestroyControllerGraph(controller);
                return;
            }

            Object.DestroyImmediate(committed);
        }

        private static void DestroyControllerGraph(AnimatorController controller)
        {
            foreach (var layer in controller.layers)
            {
                DestroyStateMachine(layer.stateMachine);
            }

            Object.DestroyImmediate(controller);
        }

        private static void DestroyStateMachine(AnimatorStateMachine machine)
        {
            foreach (var child in machine.stateMachines)
                DestroyStateMachine(child.stateMachine);
            foreach (var child in machine.states)
            {
                foreach (var behaviour in child.state.behaviours)
                    Object.DestroyImmediate(behaviour);
                Object.DestroyImmediate(child.state);
            }

            foreach (var behaviour in machine.behaviours)
                Object.DestroyImmediate(behaviour);
            Object.DestroyImmediate(machine);
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
                var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
                material.SetFloat("_AlphaForceOpaque", 1f);
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

        [RunsOnPlatforms(ActiveExtensionProbePlatformName)]
        public sealed class BarrierUnderActiveExtensionProbePlugin :
            Plugin<BarrierUnderActiveExtensionProbePlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.barrier-under-active-extension";

            protected override void Configure()
            {
                // Declaring an extension is evaluated in Configure, not in the
                // pass body, so an arming flag could not stop this plugin from
                // activating and committing AnimatorServicesContext on every
                // build in the session. It is confined to its own platform
                // instead, which no other fixture uses.
                InPhase(BuildPhase.PlatformFinish)
                    .WithRequiredExtension(
                        typeof(AnimatorServicesContext),
                        inner => inner.Run(
                            "AMUSE test barrier under active extension", Execute));
            }

            private static void Execute(BuildContext context)
            {
                var probe = context.GetState<ActiveExtensionBarrierProbe>();
                probe.Ran = true;

                // Caught here only so the assertions can inspect the failure. The
                // barrier itself performs no such translation: NDMF would otherwise
                // abort the whole build, which is the intended production outcome.
                try
                {
                    AmusePlatformFinishPass.Execute(context);
                }
                catch (System.Exception exception)
                {
                    probe.Failure = exception;
                }
            }
        }

        public sealed class ActiveExtensionBarrierProbe
        {
            public bool Ran { get; set; }
            public System.Exception Failure { get; set; }
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

        internal const string ActiveExtensionProbePlatformName =
            "com.alrauna.amuse.tests.active-extension";

        internal sealed class TestActiveExtensionPlatform : INDMFPlatformProvider
        {
            internal static readonly TestActiveExtensionPlatform Instance =
                new TestActiveExtensionPlatform();

            public string QualifiedName => ActiveExtensionProbePlatformName;
            public string DisplayName => "AMUSE test active-extension probe";
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
