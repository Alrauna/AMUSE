using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class CommittedControllerGraphTests
    {
        [Test]
        public void OverrideControllerIsRefusedByForm()
        {
            var root = new GameObject("override form");
            var baseController = new AnimatorController();
            var over = new AnimatorOverrideController(baseController);
            try
            {
                baseController.AddLayer("base");
                root.AddComponent<Animator>().runtimeAnimatorController = over;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(over));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnsupportedAnimatorControllerForm));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(over);
                DestroyController(baseController);
            }
        }

        [Test]
        public void PlainControllerLayersAndClipsAreEnumerated()
        {
            var root = new GameObject("plain controller");
            var controller = new AnimatorController { name = "plain" };
            var clip = new AnimationClip { name = "enumerated" };
            try
            {
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("S0").motion = clip;
                var layers = controller.layers;
                layers[0].blendingMode = AnimatorLayerBlendingMode.Additive;
                controller.layers = layers;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller, true));

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers.Count, Is.EqualTo(1));
                Assert.That(result.Layers[0].ControllerName, Is.EqualTo("plain"));
                Assert.That(result.Layers[0].LayerIndex, Is.Zero);
                Assert.That(result.Layers[0].BlendingMode,
                    Is.EqualTo(AnimatorLayerBlendingMode.Additive));
                Assert.That(result.Layers[0].Clips.Single(), Is.SameAs(clip));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
                DestroyController(controller);
            }
        }

        [Test]
        public void SyncedLayerReturnsNamedRefusalAndNoPartialGraph()
        {
            var root = new GameObject("synced layer");
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("source");
                controller.AddLayer("synced");
                var layers = controller.layers;
                layers[1].syncedLayerIndex = 0;
                controller.layers = layers;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal,
                    Is.EqualTo(AvatarAnimationRefusal.UnsupportedSyncedLayerOverrides));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                DestroyController(controller);
            }
        }

        [Test]
        public void NestedBehaviourFreeGraphEnumeratesClipsOnce()
        {
            var root = new GameObject("nested graph");
            var controller = new AnimatorController();
            var sharedClip = new AnimationClip { name = "shared" };
            var nestedClip = new AnimationClip { name = "nested" };
            var outerTree = new BlendTree { blendType = BlendTreeType.Simple1D };
            var innerTree = new BlendTree { blendType = BlendTreeType.Simple1D };
            try
            {
                controller.AddLayer("L0");
                var machine = controller.layers[0].stateMachine;
                machine.AddState("duplicate").motion = sharedClip;
                var nestedMachine = machine.AddStateMachine("nested machine");
                var nestedState = nestedMachine.AddState("nested state");
                innerTree.AddChild(sharedClip);
                innerTree.AddChild(nestedClip);
                outerTree.AddChild(innerTree);
                nestedState.motion = outerTree;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers.Single().Clips,
                    Is.EquivalentTo(new[] { sharedClip, nestedClip }));
                Assert.That(result.Layers.Single().Clips.Count, Is.EqualTo(2),
                    "the same clip reference must be emitted only once per layer");
                Assert.That(result.Layers.Single().Behaviours, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(outerTree);
                Object.DestroyImmediate(innerTree);
                Object.DestroyImmediate(sharedClip);
                Object.DestroyImmediate(nestedClip);
                DestroyController(controller);
            }
        }

        [Test]
        public void StateBehaviourReturnsAvatarRefusalAndNoPartialGraph()
        {
            var root = new GameObject("state behaviour");
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("clean");
                controller.layers[0].stateMachine.AddState("clean state");
                controller.AddLayer("behaviour");
                var state = controller.layers[1].stateMachine.AddState("S0");
                Assert.That(AttachBehaviour(state), Is.Not.Null,
                    "fixture state behaviour must be attached");

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                DestroyController(controller);
            }
        }

        [Test]
        public void StateMachineBehaviourReturnsAvatarRefusalAndNoPartialGraph()
        {
            var root = new GameObject("state machine behaviour");
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("L0");
                var machine = controller.layers[0].stateMachine;
                Assert.That(AttachBehaviour(machine), Is.Not.Null,
                    "fixture state-machine behaviour must be attached");

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                DestroyController(controller);
            }
        }

        [Test]
        public void BehaviourOnNestedStateMachineCannotBypassAuthorization()
        {
            var root = new GameObject("nested state machine behaviour");
            var controller = new AnimatorController();
            try
            {
                controller.AddLayer("L0");
                var nested = controller.layers[0].stateMachine
                    .AddStateMachine("nested");
                Assert.That(AttachBehaviour(nested), Is.Not.Null,
                    "fixture nested state-machine behaviour must be attached");

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                DestroyController(controller);
            }
        }

        [Test]
        public void EventBearingClipReturnsAvatarRefusalAndNoPartialGraph()
        {
            var root = new GameObject("event bearing clip");
            var controller = new AnimatorController();
            var cleanClip = new AnimationClip { name = "clean" };
            var eventClip = new AnimationClip { name = "event bearing" };
            try
            {
                controller.AddLayer("clean");
                controller.layers[0].stateMachine.AddState("clean state").motion =
                    cleanClip;
                controller.AddLayer("events");
                controller.layers[1].stateMachine.AddState("event state").motion =
                    eventClip;
                AttachEvents(eventClip, 1);

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.AnimationEventPresent));
                Assert.That(result.Layers, Is.Empty,
                    "an avatar-scoped refusal must not expose the accumulated prefix");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cleanClip);
                Object.DestroyImmediate(eventClip);
                DestroyController(controller);
            }
        }

        [Test]
        public void MultipleEventsRefuseIdenticallyToOne()
        {
            var root = new GameObject("multiple events");
            var controller = new AnimatorController();
            var eventClip = new AnimationClip { name = "many events" };
            try
            {
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("S0").motion = eventClip;
                AttachEvents(eventClip, 3);

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.AnimationEventPresent));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(eventClip);
                DestroyController(controller);
            }
        }

        [Test]
        public void EventOnNestedBlendTreeClipCannotBypassDetection()
        {
            var root = new GameObject("nested event clip");
            var controller = new AnimatorController();
            var plainClip = new AnimationClip { name = "plain" };
            var eventClip = new AnimationClip { name = "nested event bearing" };
            var outerTree = new BlendTree { blendType = BlendTreeType.Simple1D };
            var innerTree = new BlendTree { blendType = BlendTreeType.Simple1D };
            try
            {
                controller.AddLayer("L0");
                var machine = controller.layers[0].stateMachine;
                machine.AddState("plain state").motion = plainClip;
                var nestedMachine = machine.AddStateMachine("nested machine");
                innerTree.AddChild(eventClip);
                outerTree.AddChild(innerTree);
                nestedMachine.AddState("nested state").motion = outerTree;
                AttachEvents(eventClip, 1);

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.AnimationEventPresent));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(outerTree);
                Object.DestroyImmediate(innerTree);
                Object.DestroyImmediate(plainClip);
                Object.DestroyImmediate(eventClip);
                DestroyController(controller);
            }
        }

        [Test]
        public void SpecialMotionStateDoesNotAuthorizeEvents()
        {
            var root = new GameObject("special motion event clip");
            var controller = new AnimatorController();
            var eventClip = new AnimationClip { name = "special event bearing" };
            try
            {
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("S0").motion = eventClip;
                AttachEvents(eventClip, 1);
                var bindings = new StubBindings(controller, specialMotions: true);
                Assert.That(
                    ((IPlatformAnimatorBindings)bindings).IsSpecialMotion(eventClip),
                    Is.True,
                    "the fixture host must actually report the clip as special");

                var result = CommittedControllerGraph.Enumerate(root, bindings);

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.AnimationEventPresent),
                    "host special/marker motion state is diagnostic, not authorization");
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(eventClip);
                DestroyController(controller);
            }
        }

        [Test]
        public void EventFreeNestedGraphRemainsAccepted()
        {
            var root = new GameObject("event free graph");
            var controller = new AnimatorController();
            var plainClip = new AnimationClip { name = "plain" };
            var nestedClip = new AnimationClip { name = "nested" };
            var tree = new BlendTree { blendType = BlendTreeType.Simple1D };
            try
            {
                controller.AddLayer("L0");
                var machine = controller.layers[0].stateMachine;
                machine.AddState("plain state").motion = plainClip;
                tree.AddChild(nestedClip);
                machine.AddStateMachine("nested machine")
                    .AddState("nested state").motion = tree;
                Assert.That(plainClip.events, Is.Empty);
                Assert.That(nestedClip.events, Is.Empty);

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers.Single().Clips,
                    Is.EquivalentTo(new[] { plainClip, nestedClip }));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tree);
                Object.DestroyImmediate(plainClip);
                Object.DestroyImmediate(nestedClip);
                DestroyController(controller);
            }
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void DirectBlendTreeNormalizationIsReadFromCommittedSerialization(
            bool normalized, bool expectedUnnormalized)
        {
            var root = new GameObject("direct blend tree");
            var controller = new AnimatorController();
            var clip = new AnimationClip();
            var direct = new BlendTree { blendType = BlendTreeType.Direct };
            try
            {
                direct.AddChild(clip);
                var serialized = new SerializedObject(direct);
                var property = serialized.FindProperty("m_NormalizedBlendValues");
                Assert.That(property, Is.Not.Null,
                    "Unity 2022.3.22f1 must expose the planned serialized field");
                property.boolValue = normalized;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("direct").motion = direct;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers.Single().HasUnnormalizedDirectBlendTree,
                    Is.EqualTo(expectedUnnormalized));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(direct);
                Object.DestroyImmediate(clip);
                DestroyController(controller);
            }
        }

        [Test]
        public void NullVirtualizedControllerContributesNothing()
        {
            var root = new GameObject("null virtualized controller");
            try
            {
                root.AddComponent<VirtualizedControllerProbe>();

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings());

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ValidVirtualizedControllerIsEnumerated()
        {
            var root = new GameObject("valid virtualized controller");
            var controller = new AnimatorController();
            var clip = new AnimationClip();
            try
            {
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("S0").motion = clip;
                root.AddComponent<VirtualizedControllerProbe>().AnimatorController =
                    controller;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings());

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers.Single().Clips.Single(), Is.SameAs(clip));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(clip);
                DestroyController(controller);
            }
        }

        [Test]
        public void UnsupportedVirtualizedControllerReturnsNamedRefusal()
        {
            var root = new GameObject("unsupported virtualized controller");
            var baseController = new AnimatorController();
            var over = new AnimatorOverrideController(baseController);
            try
            {
                root.AddComponent<VirtualizedControllerProbe>().AnimatorController = over;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings());

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnsupportedAnimatorControllerForm));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(over);
                DestroyController(baseController);
            }
        }

        [Test]
        public void NullVirtualizedMotionContributesNothing()
        {
            var root = new GameObject("null virtualized motion");
            try
            {
                root.AddComponent<VirtualizedMotionProbe>();

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings());

                Assert.That(result.Refusal, Is.EqualTo(AvatarAnimationRefusal.None));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NonNullVirtualizedMotionRefusesWithoutPartialGraph()
        {
            var root = new GameObject("unresolved virtualized motion");
            var controller = new AnimatorController();
            var committedClip = new AnimationClip();
            var standaloneClip = new AnimationClip();
            try
            {
                controller.AddLayer("L0");
                controller.layers[0].stateMachine.AddState("committed").motion =
                    committedClip;
                root.AddComponent<VirtualizedMotionProbe>().Motion = standaloneClip;

                var result = CommittedControllerGraph.Enumerate(
                    root, new StubBindings(controller));

                Assert.That(result.Refusal, Is.EqualTo(
                    AvatarAnimationRefusal.UnresolvedVirtualizedMotionContext));
                Assert.That(result.Layers, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(committedClip);
                Object.DestroyImmediate(standaloneClip);
                DestroyController(controller);
            }
        }

        private static void DestroyController(AnimatorController controller)
        {
            foreach (var layer in controller.layers)
                DestroyStateMachine(layer.stateMachine);
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

        private static void AttachEvents(AnimationClip clip, int count)
        {
            var events = new AnimationEvent[count];
            for (var index = 0; index < count; index++)
                events[index] = new AnimationEvent { functionName = "Probe" + index };
            AnimationUtility.SetAnimationEvents(clip, events);
            Assert.That(clip.events.Length, Is.EqualTo(count),
                "fixture clip must actually carry the animation events");
        }

        private static StateMachineBehaviour AttachBehaviour(Object owner)
        {
            var type = System.Type.GetType(
                "Alrauna.Amuse.TestFixtures.AMUSETask7StateMachineBehaviourProbe, " +
                "Assembly-CSharp",
                true);

            if (owner is AnimatorState state)
                return state.AddStateMachineBehaviour(type);
            return ((AnimatorStateMachine)owner).AddStateMachineBehaviour(type);
        }

        private sealed class StubBindings : IPlatformAnimatorBindings
        {
            private readonly RuntimeAnimatorController controller;
            private readonly bool overridden;
            private readonly bool specialMotions;

            internal StubBindings(
                RuntimeAnimatorController controller = null,
                bool overridden = false,
                bool specialMotions = false)
            {
                this.controller = controller;
                this.overridden = overridden;
                this.specialMotions = specialMotions;
            }

            public bool IsSpecialMotion(Motion m)
            {
                return specialMotions;
            }

            public IEnumerable<(object, RuntimeAnimatorController, bool)>
                GetInnateControllers(GameObject root)
            {
                if (controller != null)
                    yield return (root, controller, overridden);
            }
        }
    }

    internal sealed class VirtualizedControllerProbe : MonoBehaviour,
        IVirtualizeAnimatorController
    {
        public RuntimeAnimatorController AnimatorController { get; set; }
        public object TargetControllerKey => null;

        public string GetMotionBasePath(object ndmfBuildContext, bool clearPath = true)
        {
            return string.Empty;
        }
    }

    internal sealed class VirtualizedMotionProbe : MonoBehaviour, IVirtualizeMotion
    {
        public Motion Motion { get; set; }

        public string GetMotionBasePath(object ndmfBuildContext, bool clearPath = true)
        {
            return string.Empty;
        }
    }
}
