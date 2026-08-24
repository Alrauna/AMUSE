using System;
using System.Collections.Generic;
using Alrauna.Amuse.Tests.Editor.Build;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Host.AnimatorBindingsLifetimeGateTests.BindingsLifetimeGatePlugin))]

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class AnimatorBindingsLifetimeGateTests
    {
        internal sealed class GateProbe
        {
            internal IPlatformAnimatorBindings Captured;
            internal bool CaptureRan;
            internal bool ContextActiveAtCapture;
            internal bool ObserveRan;
            internal bool ContextWasInactiveAtObserve;
            internal bool IsSpecialMotionUsable;
            internal bool GetInnateControllersUsable;
            internal int InnateControllerCount;
            internal Exception Failure;
        }

        internal sealed class BindingsLifetimeGatePlugin : Plugin<BindingsLifetimeGatePlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.bindings-lifetime-gate";

            protected override void Configure()
            {
                var sequence = InPhase(BuildPhase.PlatformFinish);

                // The real production declaration mechanism. This is what puts
                // AnimatorServicesContext into SolverPass.RequiredExtensions and
                // makes the resolver activate it before, and deactivate it after.
                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run("capture bindings", Capture));

                // Declared outside the scope: no animator extension required, so
                // the resolver deactivates and commits before this pass.
                sequence.Run("observe after commit", Observe);
            }

            private static void Capture(BuildContext context)
            {
                if (!AmusePlatformFinishPluginTests.SyntheticPluginScope.IsArmed) return;
                var probe = context.GetState<GateProbe>();
                var services = context.Extension<AnimatorServicesContext>();
                probe.ContextActiveAtCapture = true;
                probe.Captured = services.ControllerContext.PlatformBindings;
                probe.CaptureRan = true;
            }

            private static void Observe(BuildContext context)
            {
                if (!AmusePlatformFinishPluginTests.SyntheticPluginScope.IsArmed) return;
                var probe = context.GetState<GateProbe>();
                probe.ObserveRan = true;

                // Non-perturbing inactivity check. BuildContext.Extension<T> only
                // reads _activeExtensions and throws when absent; it never
                // activates (Editor/API/BuildContext.cs:105-112). Catching that
                // throw therefore observes without changing lifecycle state.
                try
                {
                    context.Extension<AnimatorServicesContext>();
                    probe.ContextWasInactiveAtObserve = false;
                }
                catch (Exception)
                {
                    probe.ContextWasInactiveAtObserve = true;
                }

                try
                {
                    var motion = new AnimationClip { name = "gate probe clip" };
                    probe.IsSpecialMotionUsable =
                        probe.Captured.IsSpecialMotion(motion) == false;
                    UnityEngine.Object.DestroyImmediate(motion);

                    var innate = new List<(object, RuntimeAnimatorController, bool)>(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    probe.InnateControllerCount = innate.Count;
                    probe.GetInnateControllersUsable = true;
                }
                catch (Exception exception)
                {
                    probe.Failure = exception;
                }
            }
        }

        [Test]
        public void CapturedBindingsRemainUsableAfterContextDeactivation()
        {
            using var armed = AmusePlatformFinishPluginTests.SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            var root = new GameObject("AMUSE bindings lifetime gate");

            try
            {
                var context = AvatarProcessor.ProcessAvatar(
                    root, AmusePlatformFinishPluginTests.TestVrchatPlatform.Instance);
                var probe = context.GetState<GateProbe>();

                Assert.That(probe.CaptureRan, Is.True, "capture pass did not run");
                Assert.That(probe.ContextActiveAtCapture, Is.True,
                    "WithRequiredExtension did not activate AnimatorServicesContext");
                Assert.That(probe.ObserveRan, Is.True, "observe pass did not run");
                Assert.That(probe.Captured, Is.Not.Null,
                    "PlatformBindings was not obtainable while the context was active");
                Assert.That(probe.ContextWasInactiveAtObserve, Is.True,
                    "ARCHITECTURE GATE: the animator context was still active in a " +
                    "pass that declares no extension, so NDMF did not commit first");
                Assert.That(probe.Failure, Is.Null,
                    "ARCHITECTURE GATE: the retained bindings threw after " +
                    "deactivation: " + probe.Failure);
                Assert.That(probe.IsSpecialMotionUsable, Is.True,
                    "ARCHITECTURE GATE: IsSpecialMotion did not behave correctly " +
                    "after deactivation");
                Assert.That(probe.GetInnateControllersUsable, Is.True,
                    "ARCHITECTURE GATE: GetInnateControllers threw after deactivation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
