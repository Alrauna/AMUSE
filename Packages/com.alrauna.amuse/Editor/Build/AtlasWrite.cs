using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using UnityEngine;

namespace Alrauna.Amuse.Editor.Build
{
    /// <summary>
    /// One renderer the atlas pass touched. The mesh and the slot
    /// array are captured before any page is built, so the write can
    /// prove that nothing else moved the renderer between the capture
    /// and the write. The new mesh and the new slot array stay null
    /// until the pass filled them, and a null new slot array means
    /// the pass wrote nothing for this renderer.
    /// </summary>
    internal sealed class AtlasRendererOutput
    {
        internal Renderer Renderer { get; }

        /// <summary>The avatar root the pass captured.</summary>
        internal GameObject AvatarRoot { get; }

        internal Mesh Mesh { get; }

        internal Material[] Slots { get; }

        internal int SlotCount => Slots.Length;

        /// <summary>
        /// The use indices of this renderer, by their position in the
        /// admitted uses list, in capture order.
        /// </summary>
        internal List<int> UseIndices { get; } = new List<int>();

        internal Mesh NewMesh { get; set; }

        internal Material[] NewSlots { get; set; }

        internal AtlasRendererOutput(
            GameObject avatarRoot,
            Renderer renderer)
        {
            AvatarRoot = avatarRoot;
            Renderer = renderer;
            Mesh = AtlasWrite.SharedMeshOf(renderer);
            Slots = (Material[])renderer.sharedMaterials.Clone();
        }

        /// <summary>True when the pass has something to write.</summary>
        internal bool HasChange => NewSlots != null;
    }

    /// <summary>
    /// The write step of the atlas pass. The write proves the
    /// renderer still holds the captured mesh and slot array, then
    /// sets the mesh and the slot array, registers every replacement
    /// with the NDMF object registry, and destroys each replaced
    /// object the state owns and no renderer references.
    /// The registry step runs before any destroy, because NDMF
    /// resolves an error reference through the replaced object. A
    /// registry without resolve support keeps the replaced object
    /// alive instead, so a later report can still name it.
    /// <para>
    /// The destroyed set leaves out every object a renderer or a
    /// committed object reference curve still names.
    /// </para>
    /// </summary>
    internal static class AtlasWrite
    {
        internal static void Apply(
            AmusePlatformFinishState state,
            IReadOnlyList<AtlasRendererOutput> outputs,
            IReadOnlyCollection<UnityEngine.Object> curveReferenced)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            if (curveReferenced == null)
            {
                throw new ArgumentNullException(nameof(curveReferenced));
            }

            var replaced = new List<UnityEngine.Object>();
            foreach (var output in outputs)
            {
                if (!output.HasChange)
                {
                    continue;
                }

                VerifyIdentity(output);
                if (output.NewMesh != null)
                {
                    SetMesh(output.Renderer, output.NewMesh);
                    replaced.Add(output.Mesh);
                }

                output.Renderer.sharedMaterials = output.NewSlots;
                for (var slot = 0; slot < output.NewSlots.Length; slot++)
                {
                    if (!ReferenceEquals(
                            output.NewSlots[slot], output.Slots[slot]))
                    {
                        replaced.Add(output.Slots[slot]);
                    }
                }
            }

            var keepAlive = new HashSet<UnityEngine.Object>();
            RegisterReplacements(state, outputs, keepAlive);
            Cleanup(
                state, outputs, replaced, keepAlive, curveReferenced);
        }

        /// <summary>
        /// Proves the renderer still holds the captured facts. A
        /// difference means a foreign pass moved the renderer between
        /// the capture and the write, which the plan cannot honor.
        /// </summary>
        private static void VerifyIdentity(AtlasRendererOutput output)
        {
            var liveMesh = SharedMeshOf(output.Renderer);
            if (!ReferenceEquals(liveMesh, output.Mesh))
            {
                throw new InvalidOperationException(
                    "The renderer of the atlas output changed its mesh" +
                    " between the capture and the write.");
            }

            var liveSlots = output.Renderer.sharedMaterials;
            if (liveSlots.Length != output.SlotCount ||
                output.NewSlots.Length != output.SlotCount)
            {
                throw new InvalidOperationException(
                    "The renderer of the atlas output changed its slot" +
                    " count between the capture and the write.");
            }

            for (var slot = 0; slot < liveSlots.Length; slot++)
            {
                if (!ReferenceEquals(liveSlots[slot], output.Slots[slot]))
                {
                    throw new InvalidOperationException(
                        "The renderer of the atlas output changed the" +
                        " material of slot " + slot + " between the" +
                        " capture and the write.");
                }
            }
        }

        /// <summary>
        /// Registers each replaced mesh and each page material with
        /// the NDMF object registry. A page material registers against
        /// the material of its first use in capture order. The outputs
        /// run in capture order and their slots run in index order, so
        /// the first page material encounter is that first use.
        /// </summary>
        private static void RegisterReplacements(
            AmusePlatformFinishState state,
            IReadOnlyList<AtlasRendererOutput> outputs,
            HashSet<UnityEngine.Object> keepAlive)
        {
            var registry = ObjectRegistry.ActiveRegistry;
            var registeredPages = new HashSet<Material>();
            foreach (var output in outputs)
            {
                if (!output.HasChange)
                {
                    continue;
                }

                if (output.NewMesh != null)
                {
                    Register(
                        registry, state, output.Mesh, output.NewMesh,
                        keepAlive);
                }

                for (var slot = 0; slot < output.NewSlots.Length; slot++)
                {
                    if (ReferenceEquals(
                            output.NewSlots[slot], output.Slots[slot]))
                    {
                        continue;
                    }

                    if (!registeredPages.Add(output.NewSlots[slot]))
                    {
                        continue;
                    }

                    Register(
                        registry, state, output.Slots[slot],
                        output.NewSlots[slot], keepAlive);
                }
            }
        }

        /// <summary>
        /// Registers one replacement. A registry without resolve
        /// support returns no reference for the new object. That
        /// capability gap keeps the replaced owned object alive, so a
        /// later error report can still name it. A registry that
        /// throws is a defect and reaches the build uncaught.
        /// </summary>
        private static void Register(
            IObjectRegistry registry,
            AmusePlatformFinishState state,
            UnityEngine.Object oldObject,
            UnityEngine.Object newObject,
            HashSet<UnityEngine.Object> keepAlive)
        {
            ObjectRegistry.RegisterReplacedObject(
                oldObject, newObject);
            var resolved = registry != null
                ? registry.GetReference(newObject, false)
                : ObjectRegistry.GetReference(newObject);
            if (resolved == null)
            {
                keepAlive.Add(oldObject);
            }
        }

        /// <summary>
        /// Destroys each replaced object the state owns, that no
        /// renderer references, and that no committed object
        /// reference curve names. The renderer scan covers the whole
        /// avatar, because a shared clone can outlive the slot that
        /// first referenced it. The curve set follows the controller
        /// ruling: an object curve keyframe keeps its object alive,
        /// because a destroyed keyframe would serialize as a missing
        /// reference.
        /// </summary>
        private static void Cleanup(
            AmusePlatformFinishState state,
            IReadOnlyList<AtlasRendererOutput> outputs,
            List<UnityEngine.Object> replaced,
            HashSet<UnityEngine.Object> keepAlive,
            IReadOnlyCollection<UnityEngine.Object> curveReferenced)
        {
            if (replaced.Count == 0 || outputs.Count == 0)
            {
                return;
            }

            var referenced = new HashSet<UnityEngine.Object>();
            foreach (var renderer in outputs[0].AvatarRoot
                .GetComponentsInChildren<Renderer>(true))
            {
                var mesh = SharedMeshOf(renderer);
                if (mesh != null)
                {
                    referenced.Add(mesh);
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        referenced.Add(material);
                    }
                }
            }

            foreach (var candidate in replaced)
            {
                if (candidate == null || keepAlive.Contains(candidate) ||
                    referenced.Contains(candidate) ||
                    curveReferenced.Contains(candidate) ||
                    !state.AmuseOwnedObjects.Contains(candidate))
                {
                    continue;
                }

                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        private static void SetMesh(Renderer renderer, Mesh mesh)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                skinned.sharedMesh = mesh;
                return;
            }

            renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        internal static Mesh SharedMeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }
    }
}
