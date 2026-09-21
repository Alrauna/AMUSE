using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Build
        .TransientUnlockWindowTestPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The scripted knobs the transient unlock window tests drive. The
    /// test plugin reads them while running the real swap-in service and
    /// the real window close pass, so every test injects exactly the seam
    /// it is falsifying and nothing else.
    /// </summary>
    internal static class TransientUnlockTestKnobs
    {
        /// <summary>Whether the window consent is granted for this build.</summary>
        internal static bool ConsentGranted = true;

        /// <summary>Whether the injected availability reports the Thry
        /// side ready.</summary>
        internal static bool ThryAttested = true;

        /// <summary>The injected original-shader attestation, or null for
        /// the always-attested stand-in.</summary>
        internal static Func<Material, bool> OriginalAttestation;

        /// <summary>The injected re-lock delegate, or null for the real
        /// production delegate over the stand-in vendor type.</summary>
        internal static TransientRelockDelegate RelockOverride;

        /// <summary>False runs the swap-in pass; true skips it, for close
        /// tests that arm their pairs through the swap-in and then need a
        /// second build for the close alone.</summary>
        internal static bool SkipSwapIn;

        /// <summary>False runs the close pass; true skips it, for swap-in
        /// tests that observe the open window before any close.</summary>
        internal static bool SkipClose;

        /// <summary>A callback the close pass runs before the window
        /// closes, for tests that must mutate build state between swap-in
        /// and close.</summary>
        internal static Action<BuildContext> BeforeClose;

        /// <summary>The window state the last pass run observed.</summary>
        internal static TransientUnlockWindowState Window;

        /// <summary>The swap-in summary the last swap-in produced.</summary>
        internal static TransientUnlockSwapIn.Summary
            LastSwapSummary;

        /// <summary>The exception the swap-in pass captured before it
        /// rethrew, so a failing test names the real defect.</summary>
        internal static Exception LastSwapError;

        /// <summary>The exception the close pass captured before it
        /// rethrew.</summary>
        internal static Exception LastCloseError;

        // NDMF's plugin disable mechanism: PluginDisablePrefs stores a
        // SessionState key per plugin id, and PluginResolver excludes the
        // disabled plugin's passes when it solves the pass graph. AAO's
        // Trace and Optimize passes execute on this test platform and
        // rewrite animator clips and material serialized state in the
        // Optimizing phase, before the window opens, so the window
        // fixtures opt out for their own duration and restore the prior
        // value afterwards.
        internal const string AaoPluginId =
            "com.anatawa12.avatar-optimizer";

        private const string AaoDisableKey =
            "nadena.dev.ndmf.plugin-disabled." + AaoPluginId;

        private static bool aaoPriorDisabled;

        internal static void DisableAaoForFixture()
        {
            aaoPriorDisabled = UnityEditor.SessionState.GetBool(
                AaoDisableKey, false);
            UnityEditor.SessionState.SetBool(AaoDisableKey, true);
        }

        internal static void RestoreAaoAfterFixture()
        {
            UnityEditor.SessionState.SetBool(
                AaoDisableKey, aaoPriorDisabled);
        }

        internal static void Reset()
        {
            ConsentGranted = true;
            ThryAttested = true;
            OriginalAttestation = null;
            RelockOverride = null;
            SkipSwapIn = false;
            SkipClose = false;
            BeforeClose = null;
            Window = null;
            LastSwapSummary = null;
            LastSwapError = null;
            LastCloseError = null;
            Thry.ThryEditor.ShaderOptimizer.Reset();
        }
    }

    internal sealed class TransientUnlockTestPlatform : INDMFPlatformProvider
    {
        internal const string QualifiedName =
            "com.alrauna.amuse.tests.transient-unlock";

        internal static readonly TransientUnlockTestPlatform Instance =
            new TransientUnlockTestPlatform();

        string INDMFPlatformProvider.QualifiedName => QualifiedName;

        string INDMFPlatformProvider.DisplayName =>
            "AMUSE transient unlock window";
    }

    /// <summary>
    /// The window test lifecycle: the real swap-in service and the real
    /// window close pass, both under an active animator extension, with the
    /// availability and re-lock seams injected through the knobs. The
    /// production plugin never runs on this dedicated platform, so this
    /// plugin is the whole PlatformFinish phase.
    /// </summary>
    [RunsOnPlatforms(TransientUnlockTestPlatform.QualifiedName)]
    public sealed class TransientUnlockWindowTestPlugin :
        Plugin<TransientUnlockWindowTestPlugin>
    {
        public override string QualifiedName =>
            "com.alrauna.amuse.tests.transient-unlock-plugin";

        protected override void Configure()
        {
            var sequence = InPhase(BuildPhase.PlatformFinish);
            sequence.WithRequiredExtension(
                typeof(AnimatorServicesContext),
                inner =>
                {
                    inner.Run(
                        "AMUSE test unlock swap-in", SwapInPass);
                });

            // The window close mirrors the production topology: an
            // extension-free pass after the animator scope has closed, so
            // its reference writes are the final word after the commit's
            // animator rebind.
            sequence.Run(
                TransientUnlockWindowClose.PassName, ClosePass);
        }

        private static void SwapInPass(BuildContext context)
        {
            var state = context.GetState<AmusePlatformFinishState>();
            state.Lifecycle = HostLifecycleCapability.Evaluate(
                TransientUnlockTestLifecycle.SupportedFacts());
            // The retained host bindings, exactly as the production
            // capture pass retains them: the extension-free close pass
            // reads the committed controller graph through them.
            state.AnimatorBindings = context
                .Extension<AnimatorServicesContext>()
                .ControllerContext.PlatformBindings;

            if (TransientUnlockTestKnobs.SkipSwapIn)
            {
                return;
            }

            try
            {
                var window =
                    context.GetState<TransientUnlockWindowState>();
                window.ConsentGranted =
                    TransientUnlockTestKnobs.ConsentGranted;
                var availability =
                    new TransientUnlockSwapIn.Availability(
                        TransientUnlockTestKnobs.ThryAttested,
                        TransientUnlockAvailability
                            .CreateProductionRestore(),
                        TransientUnlockAvailability
                            .CreateProductionRelock());
                TransientUnlockTestKnobs.LastSwapSummary =
                    TransientUnlockSwapIn.SwapIn(
                        context,
                        window,
                        availability,
                        TransientUnlockTestKnobs.OriginalAttestation ??
                            (_ => true));
                TransientUnlockTestKnobs.Window = window;
            }
            catch (Exception exception)
            {
                TransientUnlockTestKnobs.LastSwapError = exception;
                throw;
            }
        }

        private static void ClosePass(BuildContext context)
        {
            if (TransientUnlockTestKnobs.SkipClose)
            {
                TransientUnlockTestKnobs.Window = context
                    .GetState<TransientUnlockWindowState>();
                return;
            }

            try
            {
                TransientUnlockTestKnobs.BeforeClose?.Invoke(context);
                TransientUnlockWindowClose.Execute(
                    context,
                    TransientUnlockTestKnobs.RelockOverride ??
                        TransientUnlockAvailability
                            .CreateProductionRelock());
                TransientUnlockTestKnobs.Window = context
                    .GetState<TransientUnlockWindowState>();
            }
            catch (Exception exception)
            {
                TransientUnlockTestKnobs.LastCloseError = exception;
                throw;
            }
        }
    }

    /// <summary>
    /// Shared fixtures for the transient unlock window tests: the locked
    /// stand-in material with its recorded identity tags, one- and
    /// two-slot renderers, and a material-swap animation.
    /// </summary>
    internal static class TransientUnlockTestLifecycle
    {
        internal const string LockedStandInShaderName =
            "Hidden/Locked/Alrauna/AmuseTests/LockedStandIn";

        internal const string OriginalStandInShaderName =
            "Hidden/Alrauna/AmuseTests/LockedStandInOriginal";

        /// <summary>
        /// The one test-owned directory NDMF's persistence scope is
        /// pointed at, because the window's persistence is load-bearing.
        /// Deleted unconditionally in each test's finally.
        /// </summary>
        internal const string TempFolder =
            "Assets/AmuseTests_TransientUnlock";

        internal static HostLifecycleFacts SupportedFacts()
        {
            return new HostLifecycleFacts(
                "2022.3.22f1",
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

        /// <summary>
        /// A locked stand-in material: the locked shader name prefix, the
        /// optimizer flag at one, and all three identity tags. The
        /// recorded original shader names and GUIDs the original stand-in
        /// asset, so a real restore rebinds to a resolvable shader.
        /// </summary>
        internal static Material LockedMaterial(string materialName)
        {
            var shader = Shader.Find(LockedStandInShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The locked stand-in shader must import.");
            }

            var originalShader = Shader.Find(OriginalStandInShaderName);
            if (originalShader == null)
            {
                // A preceding test's folder deletion can leave the asset
                // database mid-refresh, so the original stand-in shader
                // can briefly fail to resolve. Refresh and retry once.
                AssetDatabase.Refresh();
                originalShader = Shader.Find(OriginalStandInShaderName);
            }

            var material = new Material(shader) { name = materialName };
            material.SetFloat(
                LockedMaterialIdentity
                    .OptimizerEnabledPropertyName, 1f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                OriginalStandInShaderName);
            if (originalShader != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    originalShader, out var guid, out long _))
            {
                material.SetOverrideTag(
                    LockedMaterialIdentity
                        .OriginalShaderGuidTagName,
                    guid);
            }
            else
            {
                throw new InvalidOperationException(
                    "The original stand-in shader must resolve so the " +
                    "recorded GUID tag can be written.");
            }

            material.SetOverrideTag(
                LockedMaterialIdentity.AllLockedGuidsTagName,
                "0123456789abcdef0123456789abcdef");
            return material;
        }

        /// <summary>
        /// An unlocked material wearing stale lock residue: the locked
        /// name prefix and all three identity tags, but the optimizer flag
        /// at zero. The classifier's own two-signal rule keeps it outside
        /// the window.
        /// </summary>
        internal static Material StaleTaggedUnlockedMaterial()
        {
            var shader = Shader.Find(LockedStandInShaderName);
            var material = new Material(shader) { name = "Stale" };
            material.SetFloat(
                LockedMaterialIdentity
                    .OptimizerEnabledPropertyName, 0f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                OriginalStandInShaderName);
            material.SetOverrideTag(
                LockedMaterialIdentity.AllLockedGuidsTagName,
                "0123456789abcdef0123456789abcdef");
            return material;
        }

        internal static Mesh OneSlotMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                Vector3.zero, Vector3.right, Vector3.up,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        internal static Mesh TwoSlotMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                Vector3.zero, Vector3.right, Vector3.up,
                Vector3.right, Vector3.forward, Vector3.up,
            };
            mesh.subMeshCount = 2;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 3, 4, 5 }, 1);
            return mesh;
        }

        internal static SkinnedMeshRenderer AddRenderer(
            GameObject root, Mesh mesh, params Material[] materials)
        {
            // Each renderer lives on its own child transform, the shape
            // real avatars have. Stacking a second renderer on the avatar
            // root itself is a degenerate shape and hits a persistent
            // native AddComponent refusal in the suite context: the first
            // AddComponent on a GameObject succeeds and later ones return
            // null, so the fixture never stacks renderers on one
            // GameObject. The settle loop stays as hardening.
            var host = new GameObject("AMUSE renderer host");
            host.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = null;
            for (var attempt = 0; attempt < 25 && renderer == null; attempt++)
            {
                try
                {
                    renderer = host.AddComponent<SkinnedMeshRenderer>();
                }
                catch (NullReferenceException)
                {
                }

                if (renderer == null)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            return renderer;
        }

        /// <summary>
        /// One material-swap object curve on one slot of one renderer, in
        /// one state of one layer. The clip also carries a suffixed float
        /// curve whose property name embeds the material name, because the
        /// rename suffix derives from the material name.
        /// </summary>
        internal static AnimationClip AddMaterialSwapAnimation(
            GameObject root,
            Renderer renderer,
            int slotIndex,
            params Material[] swapValues)
        {
            var clip = new AnimationClip { name = "AMUSE material swap" };
            var path = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);
            var keyframes = swapValues.Select((material, index) =>
                new ObjectReferenceKeyframe
                {
                    time = index,
                    value = material,
                }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    path,
                    renderer.GetType(),
                    "m_Materials.Array.data[" + slotIndex + "]"),
                keyframes);

            var controller = new AnimatorController
            {
                name = "AMUSE swap graph",
            };
            controller.AddLayer("L0");
            controller.layers[0].stateMachine.AddState("S0").motion = clip;
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;
            return clip;
        }

        /// <summary>
        /// The same material-swap object curve, but resident under a blend
        /// tree state instead of a direct clip state. The animation index
        /// the swap-in remaps through walks the whole virtual node graph,
        /// so this curve is rewritten like any other.
        /// </summary>
        internal static AnimationClip AddBlendTreeSwapAnimation(
            GameObject root,
            Renderer renderer,
            int slotIndex,
            params Material[] swapValues)
        {
            var clip = new AnimationClip { name = "AMUSE material swap" };
            var path = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);
            var keyframes = swapValues.Select((material, index) =>
                new ObjectReferenceKeyframe
                {
                    time = index,
                    value = material,
                }).ToArray();
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    path,
                    renderer.GetType(),
                    "m_Materials.Array.data[" + slotIndex + "]"),
                keyframes);

            var controller = new AnimatorController
            {
                name = "AMUSE swap graph",
            };
            controller.AddLayer("L0");
            controller.AddParameter(
                "Blend", AnimatorControllerParameterType.Float);
            var blendTree = new BlendTree
            {
                name = "AMUSE swap tree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Blend",
            };
            blendTree.AddChild(clip);
            controller.layers[0].stateMachine.AddState("S0").motion =
                blendTree;
            root.AddComponent<Animator>().runtimeAnimatorController =
                controller;
            return clip;
        }

        /// <summary>
        /// A float curve whose property name carries the rename suffix the
        /// stand-in material name produces. Written onto the swap clip, so
        /// the suffixed binding and the material swap travel together.
        /// </summary>
        internal static void AddSuffixedFloatBinding(
            AnimationClip clip,
            GameObject root,
            Renderer renderer,
            string propertyName,
            float value)
        {
            var path = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    path,
                    renderer.GetType(),
                    propertyName),
                AnimationCurve.Constant(0f, 1f, value));
        }

        internal static void DestroyControllerGraph(
            AnimatorController controller)
        {
            if (controller == null)
            {
                return;
            }

            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.motion is AnimationClip clip)
                    {
                        UnityEngine.Object.DestroyImmediate(clip);
                    }

                    UnityEngine.Object.DestroyImmediate(state.state);
                }

                UnityEngine.Object.DestroyImmediate(
                    layer.stateMachine);
            }

            UnityEngine.Object.DestroyImmediate(controller);
        }
    }
}
