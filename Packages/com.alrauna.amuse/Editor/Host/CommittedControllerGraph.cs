using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using nadena.dev.ndmf.animator;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Host
{
    internal enum AvatarAnimationRefusal
    {
        None,
        UnsupportedAnimatorControllerForm,
        UnsupportedSyncedLayerOverrides,
        UnresolvedVirtualizedMotionContext,
        UnrecognizedStateMachineBehaviour,
        AnimationEventPresent,
    }

    internal sealed class CommittedLayer
    {
        internal CommittedLayer(
            string controllerName,
            int layerIndex,
            AnimatorLayerBlendingMode blendingMode,
            IList<AnimationClip> clips,
            IList<StateMachineBehaviour> behaviours,
            bool hasUnnormalizedDirectBlendTree)
        {
            ControllerName = controllerName;
            LayerIndex = layerIndex;
            BlendingMode = blendingMode;
            Clips = new ReadOnlyCollection<AnimationClip>(
                new List<AnimationClip>(clips));
            Behaviours = new ReadOnlyCollection<StateMachineBehaviour>(
                new List<StateMachineBehaviour>(behaviours));
            HasUnnormalizedDirectBlendTree = hasUnnormalizedDirectBlendTree;
        }

        internal string ControllerName { get; }
        internal int LayerIndex { get; }
        internal AnimatorLayerBlendingMode BlendingMode { get; }
        internal IReadOnlyList<AnimationClip> Clips { get; }
        internal IReadOnlyList<StateMachineBehaviour> Behaviours { get; }
        internal bool HasUnnormalizedDirectBlendTree { get; }
    }

    internal sealed class CommittedControllerGraphResult
    {
        internal CommittedControllerGraphResult(
            AvatarAnimationRefusal refusal,
            IList<CommittedLayer> layers)
        {
            Refusal = refusal;
            Layers = new ReadOnlyCollection<CommittedLayer>(
                new List<CommittedLayer>(layers));
        }

        internal AvatarAnimationRefusal Refusal { get; }
        internal IReadOnlyList<CommittedLayer> Layers { get; }
    }

    internal static class CommittedControllerGraph
    {
        private static readonly CommittedControllerGraphResult UnsupportedController =
            Refused(AvatarAnimationRefusal.UnsupportedAnimatorControllerForm);

        private static readonly CommittedControllerGraphResult UnsupportedSyncedLayer =
            Refused(AvatarAnimationRefusal.UnsupportedSyncedLayerOverrides);

        private static readonly CommittedControllerGraphResult UnresolvedMotion =
            Refused(AvatarAnimationRefusal.UnresolvedVirtualizedMotionContext);

        private static readonly CommittedControllerGraphResult UnrecognizedBehaviour =
            Refused(AvatarAnimationRefusal.UnrecognizedStateMachineBehaviour);

        private static readonly CommittedControllerGraphResult AnimationEventRefusal =
            Refused(AvatarAnimationRefusal.AnimationEventPresent);

        internal static CommittedControllerGraphResult Enumerate(
            GameObject avatarRoot,
            IPlatformAnimatorBindings bindings)
        {
            if (avatarRoot == null) throw new ArgumentNullException(nameof(avatarRoot));
            if (bindings == null) throw new ArgumentNullException(nameof(bindings));

            var controllers = new List<AnimatorController>();
            foreach (var entry in bindings.GetInnateControllers(avatarRoot))
            {
                if (!TryAddController(entry.Item2, controllers))
                    return UnsupportedController;
            }

            foreach (var source in avatarRoot
                         .GetComponentsInChildren<IVirtualizeAnimatorController>(true))
            {
                if (!TryAddController(source.AnimatorController, controllers))
                    return UnsupportedController;
            }

            foreach (var source in avatarRoot
                         .GetComponentsInChildren<IVirtualizeMotion>(true))
            {
                if (source.Motion != null) return UnresolvedMotion;
            }

            var result = new List<CommittedLayer>();
            foreach (var controller in controllers)
            {
                var layers = controller.layers;
                for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                {
                    var layer = layers[layerIndex];
                    if (layer.syncedLayerIndex >= 0) return UnsupportedSyncedLayer;
                    var committedLayer =
                        EnumerateLayer(controller.name, layerIndex, layer);
                    foreach (var behaviour in committedLayer.Behaviours)
                    {
                        var identity = BehaviourIdentity.Of(behaviour.GetType());
                        if (!BehaviourIdentity.IsAllowed(identity))
                            return UnrecognizedBehaviour;
                    }

                    // An AnimationEvent is not a float or object curve, so it lies
                    // outside the admitted animation value model: it invokes a method
                    // by name against the animated hierarchy, and its effect cannot be
                    // bounded to one layer, clip, renderer, material, or property.
                    //
                    // V1 has no transition, parameter, or state-machine reachability
                    // solver, so every clip this structural enumeration reaches is
                    // treated as potentially reachable. Refusing here does NOT claim
                    // the event definitely executes in VRChat; it records only that
                    // AMUSE cannot presently bound whether or how it can execute.
                    //
                    // Two pinned host facts explain why events are uncommon yet still
                    // possible in the committed graph: NDMF's ordinary clip cloning
                    // drops events because VirtualClip builds a fresh AnimationClip and
                    // copies curves rather than events, while special/marker motions
                    // can be committed by identity instead of cloned. Host special-
                    // motion state is diagnostic and never authorizes an event.
                    //
                    // Which named cause an avatar carrying several independent unsafe
                    // conditions reports is defined by this enumeration order alone
                    // and is not contractual; only the refusal itself is. Each cause
                    // stays separately named, and every one exposes empty Layers.
                    foreach (var clip in committedLayer.Clips)
                    {
                        if (clip.events.Length > 0) return AnimationEventRefusal;
                    }

                    result.Add(committedLayer);
                }
            }

            return new CommittedControllerGraphResult(
                AvatarAnimationRefusal.None, result);
        }

        private static bool TryAddController(
            RuntimeAnimatorController source,
            ICollection<AnimatorController> controllers)
        {
            if (source == null) return true;
            if (!(source is AnimatorController controller)) return false;
            controllers.Add(controller);
            return true;
        }

        private static CommittedLayer EnumerateLayer(
            string controllerName,
            int layerIndex,
            AnimatorControllerLayer layer)
        {
            var clips = new List<AnimationClip>();
            var clipSet = new HashSet<AnimationClip>(
                ReferenceComparer<AnimationClip>.Instance);
            var visitedMotions = new HashSet<Motion>(
                ReferenceComparer<Motion>.Instance);
            var behaviours = new List<StateMachineBehaviour>();
            var hasUnnormalizedDirectBlendTree = false;

            WalkStateMachine(
                layer.stateMachine,
                clips,
                clipSet,
                visitedMotions,
                behaviours,
                ref hasUnnormalizedDirectBlendTree);

            return new CommittedLayer(
                controllerName,
                layerIndex,
                layer.blendingMode,
                clips,
                behaviours,
                hasUnnormalizedDirectBlendTree);
        }

        private static void WalkStateMachine(
            AnimatorStateMachine machine,
            ICollection<AnimationClip> clips,
            ISet<AnimationClip> clipSet,
            ISet<Motion> visitedMotions,
            ICollection<StateMachineBehaviour> behaviours,
            ref bool hasUnnormalizedDirectBlendTree)
        {
            foreach (var behaviour in machine.behaviours)
                behaviours.Add(behaviour);

            foreach (var child in machine.states)
            {
                foreach (var behaviour in child.state.behaviours)
                    behaviours.Add(behaviour);
                WalkMotion(
                    child.state.motion,
                    clips,
                    clipSet,
                    visitedMotions,
                    ref hasUnnormalizedDirectBlendTree);
            }

            foreach (var child in machine.stateMachines)
            {
                WalkStateMachine(
                    child.stateMachine,
                    clips,
                    clipSet,
                    visitedMotions,
                    behaviours,
                    ref hasUnnormalizedDirectBlendTree);
            }
        }

        private static void WalkMotion(
            Motion motion,
            ICollection<AnimationClip> clips,
            ISet<AnimationClip> clipSet,
            ISet<Motion> visitedMotions,
            ref bool hasUnnormalizedDirectBlendTree)
        {
            if (motion == null || !visitedMotions.Add(motion)) return;

            if (motion is AnimationClip clip)
            {
                if (clipSet.Add(clip)) clips.Add(clip);
                return;
            }

            if (!(motion is BlendTree tree)) return;
            if (tree.blendType == BlendTreeType.Direct && !IsDirectTreeNormalized(tree))
                hasUnnormalizedDirectBlendTree = true;

            foreach (var child in tree.children)
            {
                WalkMotion(
                    child.motion,
                    clips,
                    clipSet,
                    visitedMotions,
                    ref hasUnnormalizedDirectBlendTree);
            }
        }

        private static bool IsDirectTreeNormalized(BlendTree tree)
        {
            var serialized = new SerializedObject(tree);
            serialized.Update();
            var property = serialized.FindProperty("m_NormalizedBlendValues");
            if (property == null || property.propertyType != SerializedPropertyType.Boolean)
            {
                throw new InvalidOperationException(
                    "Unity did not expose BlendTree.m_NormalizedBlendValues as a boolean.");
            }

            return property.boolValue;
        }

        private static CommittedControllerGraphResult Refused(
            AvatarAnimationRefusal refusal)
        {
            return new CommittedControllerGraphResult(
                refusal, Array.Empty<CommittedLayer>());
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T>
            where T : class
        {
            internal static readonly ReferenceComparer<T> Instance =
                new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
