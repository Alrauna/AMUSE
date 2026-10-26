using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Runtime;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEngine;

[assembly: ExportsPlugin(
    typeof(Alrauna.Amuse.Tests.Editor.Build.OptimizerMergeObservationPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Test-only observation of the real optimizer merge boundary. The
    /// plugin registers two passes that snapshot the build avatar's
    /// renderers at the two points the merge tests must verify: directly
    /// after Avatar Optimizer's Optimizing passes (before AMUSE sees the
    /// avatar), and directly after AMUSE's PlatformFinish sequence.
    /// <para>
    /// Observation is explicitly opt-in: <see cref="Enabled"/> is false
    /// unless a running test set it, and every pass returns immediately
    /// when it is not. The plugin itself stays inert for the whole rest
    /// of the suite, so no production build changes behavior because this
    /// assembly is loaded.
    /// </para>
    /// </summary>
    internal static class OptimizerMergeObservation
    {
        internal enum Phase
        {
            AfterOptimizer,
            AfterAmuse,
        }

        /// <summary>Canonical geometry of one renderer: one entry per
        /// output triangle, each an order-independent position triple in
        /// avatar-root space, rounded to a fixed grid.</summary>
        internal sealed class RendererSnapshot
        {
            internal string RendererName;
            internal List<string> TriangleKeys = new List<string>();
            internal List<string> SlotShaderNames = new List<string>();
        }

        internal sealed class PhaseSnapshot
        {
            internal List<RendererSnapshot> Renderers =
                new List<RendererSnapshot>();

            /// <summary>Whether the AMUSE component is still on the
            /// avatar root at this boundary. The component-information
            /// contract promises it survives Avatar Optimizer until the
            /// AMUSE passes read it at platform finish.</summary>
            internal bool RootHasAmuseComponent;
        }

        /// <summary>One real Avatar Optimizer atlas observed on a slot at
        /// the post-optimizer boundary, with the admission answers the
        /// atlas ground check asserts.</summary>
        internal sealed class AtlasObservation
        {
            internal string Name;

            /// <summary>Whether the atlas already carries an asset path
            /// at the boundary. NDMF persists build objects after the
            /// last pass, so the answer must stay false here.</summary>
            internal bool HasAssetPath;

            /// <summary>The answer of
            /// AaoAtlasTextureAttestation.TryIdentifyAtlas for the atlas
            /// over the slot material that carries it.</summary>
            internal bool Admitted;

            /// <summary>The answer of
            /// GeneratedTextureAttestation.TryIdentifyRouteTexture for
            /// the atlas.</summary>
            internal bool ServedByGeneratedRoute;
        }

        /// <summary>Opt-in switch. A test sets this before processing the
        /// avatar and clears it afterwards.</summary>
        internal static bool Enabled;
        internal static PhaseSnapshot AfterOptimizer;
        internal static PhaseSnapshot AfterAmuse;

        /// <summary>Real atlases of the last enabled observation, one
        /// entry per slot that carried a pinned name marker.</summary>
        internal static readonly List<AtlasObservation> Atlases =
            new List<AtlasObservation>();

        internal static void Reset()
        {
            Enabled = false;
            AfterOptimizer = null;
            AfterAmuse = null;
            Atlases.Clear();
        }

        /// <summary>The canonical key of one output triangle: the three
        /// avatar-root-space corner positions rounded to the same fixed
        /// grid, in lexicographic order, so triangle identity does not
        /// depend on winding, vertex order, or the renderer that carries
        /// it. Rounding at 3 decimals absorbs optimizer float writes; it
        /// stays far below the fixture's 4-unit triangle spacing.</summary>
        internal static string TriangleKey(
            Vector3 a, Vector3 b, Vector3 c, Transform root)
        {
            return string.Join("|", new[]
                {
                    Key(a, root), Key(b, root), Key(c, root),
                }
                .OrderBy(key => key, StringComparer.Ordinal));
        }

        private static string Key(Vector3 world, Transform root)
        {
            var local = root.InverseTransformPoint(world);
            return string.Join(",",
                Round(local.x), Round(local.y), Round(local.z));
        }

        private static string Round(float value)
        {
            return Math.Round(value, 3).ToString(
                "0.###", CultureInfo.InvariantCulture);
        }

        internal static void Observe(
            BuildContext context, Phase phase)
        {
            if (!Enabled) return;

            var root = context.AvatarRootTransform;
            var snapshot = new PhaseSnapshot
            {
                RootHasAmuseComponent = root.GetComponent<
                    AmuseAvatarOptimizer>() != null,
            };
            foreach (var renderer in root.GetComponentsInChildren<
                         SkinnedMeshRenderer>(true))
            {
                var entry = new RendererSnapshot
                {
                    RendererName = renderer.name,
                };

                var mesh = renderer.sharedMesh;
                var materials = renderer.sharedMaterials;
                if (mesh != null && materials != null)
                {
                    foreach (var material in materials)
                    {
                        entry.SlotShaderNames.Add(
                            material != null && material.shader != null
                                ? material.shader.name
                                : "<null>");
                        if (phase == Phase.AfterOptimizer)
                        {
                            ObserveAtlas(material);
                        }
                    }
                }

                if (mesh != null)
                {
                    var vertices = mesh.vertices;
                    var subMeshCount = Math.Min(
                        mesh.subMeshCount,
                        materials == null ? 0 : materials.Length);
                    for (var subMesh = 0; subMesh < subMeshCount; subMesh++)
                    {
                        var triangles = mesh.GetTriangles(subMesh);
                        for (var t = 0; t < triangles.Length; t += 3)
                        {
                            entry.TriangleKeys.Add(TriangleKey(
                                renderer.transform.TransformPoint(
                                    vertices[triangles[t]]),
                                renderer.transform.TransformPoint(
                                    vertices[triangles[t + 1]]),
                                renderer.transform.TransformPoint(
                                    vertices[triangles[t + 2]]),
                                root));
                        }
                    }
                }

                snapshot.Renderers.Add(entry);
            }

            if (phase == Phase.AfterOptimizer)
            {
                AfterOptimizer = snapshot;
            }
            else
            {
                AfterAmuse = snapshot;
            }
        }

        /// <summary>Records the real Avatar Optimizer atlas of one slot
        /// at the post-optimizer boundary. The boundary sits before the
        /// AMUSE passes and before NDMF persists build objects, so the
        /// probe reads the same live in-memory texture and the same
        /// active object registry the production capture reads.</summary>
        private static void ObserveAtlas(Material material)
        {
            if (material == null || !material.HasProperty("_MainTex"))
            {
                return;
            }
            if (!(material.GetTexture("_MainTex") is Texture2D atlas))
            {
                return;
            }
            var name = atlas.name ?? string.Empty;
            var carriesMarker =
                name.EndsWith(
                    AaoAtlasTextureAttestation.AtlasNameSuffix,
                    StringComparison.Ordinal) ||
                name.StartsWith(
                    AaoAtlasTextureAttestation.MonotoneNamePrefix,
                    StringComparison.Ordinal);
            if (!carriesMarker)
            {
                return;
            }
            Atlases.Add(new AtlasObservation
            {
                Name = name,
                HasAssetPath = !string.IsNullOrEmpty(
                    AssetDatabase.GetAssetPath(atlas)),
                Admitted = AaoAtlasTextureAttestation.TryIdentifyAtlas(
                    atlas, material),
                ServedByGeneratedRoute =
                    GeneratedTextureAttestation.TryIdentifyRouteTexture(
                        atlas),
            });
        }
    }

    /// <summary>NDMF plugin registration for the two observation passes.
    /// The qualified name is test-scoped and never collides with a
    /// production plugin.</summary>
    internal sealed class OptimizerMergeObservationPlugin
        : Plugin<OptimizerMergeObservationPlugin>
    {
        public override string QualifiedName =>
            "com.alrauna.amuse.tests.merge-observation";

        public override string DisplayName =>
            "AMUSE test merge observation";

        protected override void Configure()
        {
            InPhase(BuildPhase.Optimizing)
                .AfterPlugin("com.anatawa12.avatar-optimizer")
                .Run("Observe post-optimizer geometry", context =>
                    OptimizerMergeObservation.Observe(
                        context,
                        OptimizerMergeObservation.Phase.AfterOptimizer));

            InPhase(BuildPhase.PlatformFinish)
                .AfterPlugin("com.alrauna.amuse")
                .Run("Observe post-AMUSE geometry", context =>
                    OptimizerMergeObservation.Observe(
                        context,
                        OptimizerMergeObservation.Phase.AfterAmuse));
        }
    }
}
