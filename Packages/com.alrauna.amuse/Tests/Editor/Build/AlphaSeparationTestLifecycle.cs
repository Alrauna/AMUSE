using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The shared NDMF apply-lifecycle fixture helpers for the
    /// alpha-separation suites. One copy of each helper lives here; the
    /// suites call it instead of holding private copies.
    /// <para>
    /// A few helpers have two variants. The variants keep the exact bodies
    /// the suites had before consolidation, because the differences are
    /// observable test contracts:
    /// <list type="bullet">
    /// <item><see cref="AddRendererPadded"/> pads the material list to the
    /// mesh sub-mesh count; <see cref="AddRenderer"/> does not.</item>
    /// <item><see cref="CommittedClipCarrying"/> returns the first match;
    /// <see cref="CommittedClipCarryingExactlyOne"/> throws when two
    /// committed clips carry one binding.</item>
    /// <item><see cref="DestroyCommittedClone"/> destroys each layer state
    /// machine; <see cref="DestroyCommittedCloneRecursive"/> also destroys
    /// the states, behaviours and sub-state-machines under them.</item>
    /// <item><see cref="DescribeCurve"/> formats key times with the current
    /// culture and renders a null object reference as <c>&lt;null&gt;</c>;
    /// <see cref="DescribeCurveInvariant"/> uses the invariant culture and
    /// renders a null as <c>null</c>.</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class AlphaSeparationTestLifecycle
    {
        /// <summary>
        /// A single triangle with no UV channel. It backs
        /// constant-material proofs only.
        /// </summary>
        internal static Mesh SingleTriangleMesh()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    Vector3.zero, Vector3.right, Vector3.up,
                },
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        /// <summary>
        /// A renderer on a named child transform. The material list is
        /// assigned as given: a shorter list leaves the later slots null.
        /// </summary>
        internal static SkinnedMeshRenderer AddRenderer(
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

        /// <summary>
        /// The apply-suite renderer fixture: when the material list is
        /// shorter than the mesh sub-mesh count, it is padded to the sub-mesh
        /// count by repeating the last material. A longer list, an empty
        /// list or a null mesh assigns the list unchanged.
        /// </summary>
        internal static SkinnedMeshRenderer AddRendererPadded(
            GameObject root,
            string name,
            Mesh mesh,
            params Material[] materials)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            if (mesh != null && materials != null && materials.Length > 0 &&
                materials.Length < mesh.subMeshCount)
            {
                var padded = new Material[mesh.subMeshCount];
                for (var index = 0; index < mesh.subMeshCount; index++)
                {
                    padded[index] =
                        materials[Math.Min(index, materials.Length - 1)];
                }

                renderer.sharedMaterials = padded;
            }
            else
            {
                renderer.sharedMaterials = materials;
            }

            return renderer;
        }

        internal static AnimationClip NewSwapClip(
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

        internal static AnimatorController NewController(
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

        /// <summary>
        /// Every committed clip reachable through an animator in the avatar
        /// root, in walk order, without duplicates.
        /// </summary>
        internal static IEnumerable<AnimationClip> CommittedClips(
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

        /// <summary>
        /// The first committed clip that carries the binding, or null.
        /// </summary>
        internal static AnimationClip CommittedClipWithObjectBinding(
            GameObject root,
            string rendererPath,
            string propertyName)
        {
            foreach (var animator in root
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
                        if (!(child.state.motion is AnimationClip clip))
                        {
                            continue;
                        }

                        foreach (var binding in AnimationUtility
                                     .GetObjectReferenceCurveBindings(clip))
                        {
                            if (binding.path == rendererPath &&
                                binding.propertyName == propertyName)
                            {
                                return clip;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The first committed clip that carries the binding, or null.
        /// Same walk as <see cref="CommittedClipWithObjectBinding"/>; the
        /// persistence suite reads the result under this name.
        /// </summary>
        internal static AnimationClip CommittedClipCarrying(
            GameObject root,
            string rendererPath,
            string propertyName)
        {
            foreach (var animator in root
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
                        if (!(child.state.motion is AnimationClip clip))
                        {
                            continue;
                        }

                        foreach (var binding in AnimationUtility
                                     .GetObjectReferenceCurveBindings(clip))
                        {
                            if (binding.path == rendererPath &&
                                binding.propertyName == propertyName)
                            {
                                return clip;
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The committed clip that carries the binding. When two committed
        /// clips carry the binding, this throws: the split suite treats a
        /// binding collision as a fixture failure, not as a first match.
        /// </summary>
        internal static AnimationClip CommittedClipCarryingExactlyOne(
            GameObject root,
            string rendererPath,
            string propertyName)
        {
            return CommittedClips(root).SingleOrDefault(clip =>
                AnimationUtility.GetObjectReferenceCurveBindings(clip).Any(
                    binding => binding.path == rendererPath &&
                               binding.propertyName == propertyName));
        }

        internal static void DestroyGenerated(AmusePlatformFinishState state)
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

        /// <summary>
        /// Destroys the committed controller clone and each layer state
        /// machine. States, transitions and behaviours under a state machine
        /// are not destroyed.
        /// </summary>
        internal static void DestroyCommittedClone(
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

        /// <summary>
        /// Destroys the committed controller clone and the graph under it:
        /// sub-state-machines, states, behaviours, then the machines and the
        /// controller. The finish-plugin suite destroys the full graph so a
        /// later assertion sees no leftover animator assets.
        /// </summary>
        internal static void DestroyCommittedCloneRecursive(
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

            UnityEngine.Object.DestroyImmediate(committed);
        }

        private static void DestroyControllerGraph(AnimatorController controller)
        {
            foreach (var layer in controller.layers)
            {
                DestroyStateMachine(layer.stateMachine);
            }

            UnityEngine.Object.DestroyImmediate(controller);
        }

        private static void DestroyStateMachine(AnimatorStateMachine machine)
        {
            foreach (var child in machine.stateMachines)
                DestroyStateMachine(child.stateMachine);
            foreach (var child in machine.states)
            {
                foreach (var behaviour in child.state.behaviours)
                    UnityEngine.Object.DestroyImmediate(behaviour);
                UnityEngine.Object.DestroyImmediate(child.state);
            }

            foreach (var behaviour in machine.behaviours)
                UnityEngine.Object.DestroyImmediate(behaviour);
            UnityEngine.Object.DestroyImmediate(machine);
        }

        /// <summary>
        /// The apply-suite curve rendering: key times in round-trip format
        /// under the current culture, a null object reference as
        /// <c>&lt;null&gt;</c>.
        /// </summary>
        internal static string DescribeCurve(ObjectReferenceKeyframe[] curve)
        {
            if (curve == null)
            {
                return "<null>";
            }

            return string.Join("|", curve.Select(key =>
                key.time.ToString("R") + "=>" + DescribeObject(
                    key.value as UnityEngine.Object)));
        }

        private static string DescribeObject(UnityEngine.Object obj)
        {
            return obj == null
                ? "<null>"
                : obj.name + "#" + obj.GetInstanceID();
        }

        /// <summary>
        /// The persistence-suite curve rendering: key times in round-trip
        /// format under the invariant culture, a null object reference as
        /// <c>null</c>.
        /// </summary>
        internal static string DescribeCurveInvariant(
            ObjectReferenceKeyframe[] curve)
        {
            return curve == null
                ? "<null>"
                : string.Join("|", curve.Select(key =>
                    key.time.ToString("R", CultureInfo.InvariantCulture) +
                    "=>" + (key.value == null
                        ? "null"
                        : key.value.name + "#" +
                          key.value.GetInstanceID())));
        }
    }
}
