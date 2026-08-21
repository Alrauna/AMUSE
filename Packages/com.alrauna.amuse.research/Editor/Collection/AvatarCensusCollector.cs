using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Census = Alrauna.Amuse.Research.Census;

namespace Alrauna.Amuse.Research.Collection
{
    /// <summary>
    /// Collect: the one census stage that touches Unity and AMUSE internals.
    /// It turns one caller-supplied avatar root into tier 1 records and stops
    /// there.
    /// <para>
    /// It does not anonymize, aggregate, export, serialize, persist, or
    /// transmit anything, and it opens no window and adds no menu item. Those
    /// are separate stages, and keeping them separate is what lets non-leakage
    /// be a unit test rather than a promise.
    /// </para>
    /// <para>
    /// It also does not discover. There is no zero-argument entry point, no
    /// scene scan, and no project search: the caller names the root, always.
    /// That is the privacy requirement expressed as a signature rather than as
    /// a rule someone has to remember.
    /// </para>
    /// <para>
    /// This method is the entire public surface of the assembly. There is
    /// deliberately no options object, no configuration, and no provider
    /// parameter; the semantics seam the calibration tests need lives one level
    /// down, internal to <c>RendererObservationBuilder</c>.
    /// </para>
    /// <para>
    /// Reads only. The analysis path uses <c>sharedMesh</c> and
    /// <c>sharedMaterials</c> exclusively, and so does everything added here.
    /// </para>
    /// </summary>
    public static class AvatarCensusCollector
    {
        /// <param name="root">
        /// The avatar root. Required; the collector never finds one itself.
        /// </param>
        /// <param name="creatorName">
        /// Tier 1 only, and caller-supplied because Unity has no such field -
        /// it is on neither a GameObject nor an avatar descriptor. Pass null
        /// when it is unknown.
        /// </param>
        public static Census.ObservedAvatar Collect(
            GameObject root, string creatorName)
        {
            if (ReferenceEquals(root, null))
            {
                throw new ArgumentNullException(nameof(root));
            }

            // Unity's overloaded equality reports a destroyed object as null.
            if (root == null)
            {
                throw new ArgumentException(
                    "The avatar root has been destroyed and cannot be observed.",
                    nameof(root));
            }

            // Inactive renderers are included: one still ships with the avatar
            // and an animation can re-enable it. Hierarchy order is
            // deterministic and is what fixes the renderer ordinals downstream.
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var families = new CensusShaderFamily();
            var observed = new List<Census.ObservedRenderer>(renderers.Length);

            foreach (var renderer in renderers)
            {
                observed.Add(RendererObservationBuilder.Build(
                    renderer,
                    RelativePath(root.transform, renderer.transform),
                    families));
            }

            return new Census.ObservedAvatar(
                root.name,
                creatorName,
                CensusAssetIdentity.PathOf(root),
                CensusAssetIdentity.GuidOf(root),
                observed);
        }

        /// <summary>
        /// The path from the collection root, exclusive of the root itself, so
        /// the record cannot leak the scene structure above the avatar. Empty
        /// for a renderer on the root.
        /// <para>
        /// Sibling GameObjects may share a name, so this is not unique. That is
        /// accepted: it is a debugging hint that nothing downstream indexes by,
        /// and adding sibling indices would sharpen a fingerprint for no
        /// analytical gain.
        /// </para>
        /// </summary>
        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            for (var node = target; node != null && node != root;
                 node = node.parent)
            {
                segments.Add(node.name);
            }

            var path = new StringBuilder();
            for (var index = segments.Count - 1; index >= 0; index--)
            {
                if (path.Length > 0)
                {
                    path.Append('/');
                }

                path.Append(segments[index]);
            }

            return path.ToString();
        }
    }
}
