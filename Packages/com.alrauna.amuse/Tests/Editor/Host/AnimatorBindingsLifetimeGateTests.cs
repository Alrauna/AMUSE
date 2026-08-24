using System;
using System.Collections.Generic;
using System.Linq;
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
            internal string FirstEnumeration;
            internal string SecondEnumeration;
            internal string AnimatorAssignmentsBefore;
            internal string AnimatorAssignmentsAfter;
            internal bool EnumerationStable;
            internal bool ObservableStateUnchanged;
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

                    probe.AnimatorAssignmentsBefore =
                        DescribeAnimatorAssignments(context.AvatarRootObject);
                    var first = new List<(object, RuntimeAnimatorController, bool)>(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    var second = new List<(object, RuntimeAnimatorController, bool)>(
                        probe.Captured.GetInnateControllers(context.AvatarRootObject));
                    probe.AnimatorAssignmentsAfter =
                        DescribeAnimatorAssignments(context.AvatarRootObject);

                    probe.FirstEnumeration = DescribeEnumeration(first);
                    probe.SecondEnumeration = DescribeEnumeration(second);
                    probe.InnateControllerCount = first.Count;
                    probe.EnumerationStable = string.Equals(
                        probe.FirstEnumeration, probe.SecondEnumeration,
                        StringComparison.Ordinal);
                    probe.ObservableStateUnchanged = string.Equals(
                        probe.AnimatorAssignmentsBefore,
                        probe.AnimatorAssignmentsAfter,
                        StringComparison.Ordinal);
                    probe.GetInnateControllersUsable = true;
                }
                catch (Exception exception)
                {
                    probe.Failure = exception;
                }
            }
        }

        private static string DescribeEnumeration(
            IEnumerable<(object, RuntimeAnimatorController, bool)> innate)
        {
            return string.Join("|", innate.Select(entry =>
                DescribeKey(entry.Item1) + "=>" +
                (entry.Item2 == null
                    ? "null"
                    : entry.Item2.GetInstanceID().ToString()) +
                ":" + entry.Item3));
        }

        private static string DescribeKey(object key)
        {
            if (key == null) return "null";
            if (key is UnityEngine.Object unityObject)
                return unityObject.GetInstanceID().ToString();
            if (key is Enum enumValue)
                return enumValue.GetType().FullName + "=" +
                       Convert.ToInt64(enumValue);

            throw new InvalidOperationException(
                "The characterization has no stable representation for innate key type " +
                key?.GetType().FullName);
        }

        private static string DescribeAnimatorAssignments(GameObject root)
        {
            return string.Join("|", root.GetComponentsInChildren<Animator>(true)
                .Select(animator => animator.GetInstanceID() + "=>" +
                    (animator.runtimeAnimatorController == null
                        ? "null"
                        : animator.runtimeAnimatorController.GetInstanceID().ToString())));
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

        [Test]
        public void RepeatedInnateEnumerationIsSemanticallyIdempotentInImmediatePostDeactivationLifecycle()
        {
            using var armed = AmusePlatformFinishPluginTests.SyntheticPluginScope.Arm();
            using var assets = new OverrideTemporaryDirectoryScope(null);
            GameObject root = null;
            AnimatorController firstController = null;
            AnimatorController secondController = null;

            try
            {
                root = new GameObject("AMUSE innate enumeration safety");
                firstController = new AnimatorController { name = "first innate controller" };
                secondController = new AnimatorController { name = "second innate controller" };
                firstController.AddLayer("first layer");
                secondController.AddLayer("second layer");

                var firstChild = new GameObject("first assigned animator");
                firstChild.transform.SetParent(root.transform, false);
                var firstAnimator = firstChild.AddComponent<Animator>();
                firstAnimator.runtimeAnimatorController = firstController;

                var secondChild = new GameObject("second assigned animator");
                secondChild.transform.SetParent(root.transform, false);
                var secondAnimator = secondChild.AddComponent<Animator>();
                secondAnimator.runtimeAnimatorController = secondController;

                var nullChild = new GameObject("unassigned animator");
                nullChild.transform.SetParent(root.transform, false);
                var nullAnimator = nullChild.AddComponent<Animator>();

                Assert.That(firstController, Is.Not.SameAs(secondController),
                    "fixture controller identities must be distinct");

                var context = AvatarProcessor.ProcessAvatar(
                    root, AmusePlatformFinishPluginTests.TestVrchatPlatform.Instance);
                var probe = context.GetState<GateProbe>();

                Assert.That(probe.Failure, Is.Null);
                Assert.That(probe.ContextWasInactiveAtObserve, Is.True,
                    "the semantic-idempotence observation did not run after commit");
                Assert.That(firstAnimator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(secondAnimator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(firstAnimator.runtimeAnimatorController,
                    Is.Not.SameAs(secondAnimator.runtimeAnimatorController),
                    "committed controller identities must remain distinct");

                var expectedEnumeration =
                    firstAnimator.GetInstanceID() + "=>" +
                    firstAnimator.runtimeAnimatorController.GetInstanceID() + ":False|" +
                    secondAnimator.GetInstanceID() + "=>" +
                    secondAnimator.runtimeAnimatorController.GetInstanceID() + ":False";
                var expectedAssignments =
                    firstAnimator.GetInstanceID() + "=>" +
                    firstAnimator.runtimeAnimatorController.GetInstanceID() + "|" +
                    secondAnimator.GetInstanceID() + "=>" +
                    secondAnimator.runtimeAnimatorController.GetInstanceID() + "|" +
                    nullAnimator.GetInstanceID() + "=>null";

                Assert.That(probe.InnateControllerCount, Is.EqualTo(2),
                    "the generic fixture must exercise two innate controller tuples");
                Assert.That(probe.FirstEnumeration, Is.EqualTo(expectedEnumeration),
                    "the first ordered enumeration did not contain the complete expected " +
                    "key/controller/bool identities");
                Assert.That(probe.SecondEnumeration, Is.EqualTo(expectedEnumeration),
                    "the second ordered enumeration did not contain the complete expected " +
                    "key/controller/bool identities");
                Assert.That(probe.AnimatorAssignmentsBefore, Is.EqualTo(expectedAssignments),
                    "the pre-enumeration assignment snapshot did not include every Animator");
                Assert.That(probe.AnimatorAssignmentsAfter, Is.EqualTo(expectedAssignments),
                    "the post-enumeration assignment snapshot did not include every Animator");
                Assert.That(probe.EnumerationStable, Is.True,
                    "GetInnateControllers changed ordered tuple identities between " +
                    "immediate post-deactivation calls");
                Assert.That(probe.ObservableStateUnchanged, Is.True,
                    "GetInnateControllers changed Animator assignments between " +
                    "immediate post-deactivation calls");

                TestContext.WriteLine("First enumeration: " + probe.FirstEnumeration);
                TestContext.WriteLine("Second enumeration: " + probe.SecondEnumeration);
                TestContext.WriteLine(
                    "Animator assignments before: " + probe.AnimatorAssignmentsBefore);
                TestContext.WriteLine(
                    "Animator assignments after: " + probe.AnimatorAssignmentsAfter);
            }
            finally
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (firstController != null)
                    UnityEngine.Object.DestroyImmediate(firstController);
                if (secondController != null)
                    UnityEngine.Object.DestroyImmediate(secondController);
            }
        }
    }
}
