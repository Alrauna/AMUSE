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

        /// <summary>The build path the lifecycle facts report for this
        /// build: the non-Play NDMF build by default, Apply on Play for
        /// the play-mode window cases.</summary>
        internal static AmuseBuildPath BuildPath =
            AmuseBuildPath.NonPlayNdmfBuild;

        /// <summary>The injected original-shader attestation, or null for
        /// the always-attested stand-in.</summary>
        internal static Func<Material, bool> OriginalAttestation;

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

        /// <summary>
        /// A callback between preparation and apply. Tests use it to model
        /// another pass changing the build copy after analysis.
        /// </summary>
        internal static Action<BuildContext> BeforeApply;

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
            BuildPath = AmuseBuildPath.NonPlayNdmfBuild;
            OriginalAttestation = null;
            SkipSwapIn = false;
            SkipClose = false;
            BeforeClose = null;
            BeforeApply = null;
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
    /// window close pass, both under an active animator extension, with
    /// the availability seam injected through the knobs. The production
    /// plugin never runs on this dedicated platform, so this plugin is
    /// the whole PlatformFinish phase.
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
                        TransientUnlockAvailability
                            .CreateProductionRestore());
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
                TransientUnlockWindowClose.Execute(context);
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
    /// <para>
    /// Scene hygiene: fixtures run in a dedicated scene that SetUp opens
    /// and TearDown discards without saving. A fixture that left the
    /// runner's own scene dirty once wedged the editor on Unity's
    /// scene-save dialog, so every window test class opens the fixture
    /// scene in SetUp and asserts, through <see
    /// cref="AssertNoOpenSceneDirty"/>, that no open scene reports dirty
    /// in TearDown.
    /// </para>
    /// </summary>
    internal static class TransientUnlockTestLifecycle
    {
        private static UnityEngine.SceneManagement.Scene fixtureScene;
        private static UnityEngine.SceneManagement.Scene priorScene;

        /// <summary>
        /// Opens a dedicated empty scene for one fixture and makes it
        /// active, so the fixture's GameObjects land there and the
        /// runner's own scene stays clean. An untitled active scene is
        /// already a throwaway the run-end hygiene swaps out, so the
        /// fixture runs in it instead: additive scene creation refuses
        /// next to an unsaved untitled scene.
        /// </summary>
        internal static void OpenFixtureScene()
        {
            priorScene = UnityEngine.SceneManagement.SceneManager
                .GetActiveScene();
            if (string.IsNullOrEmpty(priorScene.path))
            {
                fixtureScene = default;
                return;
            }

            fixtureScene = UnityEditor.SceneManagement.EditorSceneManager
                .NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager
                .SetActiveScene(fixtureScene);
        }

        /// <summary>
        /// Closes the dedicated fixture scene without saving, after the
        /// fixture objects are destroyed. Then clears every open scene's
        /// modified flag and asserts none reports dirty. Builds dirty
        /// NDMF's own preview scene through no fault of the fixtures, so
        /// the flag is cleared rather than saved: the dialog this hygiene
        /// exists to prevent fires on a modified scene, never on a clean
        /// one.
        /// </summary>
        internal static void DiscardFixtureScene()
        {
            if (fixtureScene.IsValid() && fixtureScene.isLoaded)
            {
                // CloseScene only removes the active scene, so the fixture
                // scene is activated for its own close, and the runner's
                // scene becomes active again afterwards.
                UnityEngine.SceneManagement.SceneManager
                    .SetActiveScene(fixtureScene);
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(
                    fixtureScene, true);
                if (priorScene.IsValid() && priorScene.isLoaded)
                {
                    UnityEngine.SceneManagement.SceneManager
                        .SetActiveScene(priorScene);
                }
            }

            fixtureScene = default;
            priorScene = default;
            AssertNoSavedSceneDirty();
        }

        /// <summary>
        /// The standing hygiene guard: discards every dirty open scene
        /// after a fixture ran, then re-scans and fails the offending
        /// class if any scene still reports dirty. Preview scenes close
        /// through their own API, saved scenes re-open from disk, and an
        /// untitled scene is replaced wholesale. A modified scene at a
        /// run boundary is what raised Unity's scene-save dialog over
        /// the editor and wedged it.
        /// </summary>
        internal static void AssertNoSavedSceneDirty()
        {
            // The discard mutates the scene collection (a wholesale
            // untitled replacement collapses it), so each mutation
            // restarts the scan instead of walking stale indices.
            var scans = 0;
            var discarded = true;
            while (discarded && scans < 8)
            {
                discarded = false;
                scans++;
                for (var index = UnityEngine.SceneManagement.SceneManager
                         .sceneCount - 1;
                     index >= 0;
                     index--)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager
                        .GetSceneAt(index);
                    if (!scene.isDirty)
                    {
                        continue;
                    }

                    if (UnityEditor.SceneManagement.EditorSceneManager
                        .IsPreviewScene(scene))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager
                            .ClosePreviewScene(scene);
                        discarded = true;
                    }
                    else if (!string.IsNullOrEmpty(scene.path))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager
                            .OpenScene(scene.path);
                        discarded = true;
                    }
                    else
                    {
                        UnityEditor.SceneManagement.EditorSceneManager
                            .NewScene(
                                UnityEditor.SceneManagement.NewSceneSetup
                                    .EmptyScene,
                                UnityEditor.SceneManagement.NewSceneMode
                                    .Single);
                        discarded = true;
                    }

                    break;
                }
            }

            // The tripwire. The discard above is best effort; a scene it
            // cannot clean must fail this class here instead of raising
            // the scene-save dialog over the editor later.
            for (var index = 0;
                 index < UnityEngine.SceneManagement.SceneManager.sceneCount;
                 index++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager
                    .GetSceneAt(index);
                Assert.That(
                    scene.isDirty,
                    Is.False,
                    "an open scene was left modified: " +
                    (string.IsNullOrEmpty(scene.path)
                        ? scene.name + " (untitled)"
                        : scene.path));
            }
        }

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
                TransientUnlockTestKnobs.BuildPath,
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
            return LockedMaterialWithOriginal(
                materialName, OriginalStandInShaderName);
        }

        /// <summary>
        /// A locked stand-in material whose recorded original shader is
        /// the named installed shader. The stand-in restore rebinds the
        /// clone to whatever the tag records, so a schema-complete fixture
        /// shader as the recorded original lets the pipeline read the
        /// restored clone through the full property table.
        /// </summary>
        internal static Material LockedMaterialWithOriginal(
            string materialName, string originalShaderName)
        {
            var shader = Shader.Find(LockedStandInShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The locked stand-in shader must import.");
            }

            var originalShader = Shader.Find(originalShaderName);
            if (originalShader == null)
            {
                // A preceding test's folder deletion can leave the asset
                // database mid-refresh, so the original stand-in shader
                // can briefly fail to resolve. Refresh and retry once.
                AssetDatabase.Refresh();
                originalShader = Shader.Find(originalShaderName);
            }

            var material = new Material(shader) { name = materialName };
            material.SetFloat(
                LockedMaterialIdentity
                    .OptimizerEnabledPropertyName, 1f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                originalShaderName);
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
                    "The recorded original shader must resolve so the " +
                    "recorded GUID tag can be written.");
            }

            material.SetOverrideTag(
                LockedMaterialIdentity.AllLockedGuidsTagName,
                "0123456789abcdef0123456789abcdef");
            return material;
        }

        /// <summary>
        /// Locks an already configured fixture material. This preserves its
        /// original shader properties for the unlock-window test.
        /// </summary>
        internal static Material LockConfiguredMaterial(
            string materialName, Material configuredMaterial)
        {
            if (configuredMaterial == null)
            {
                throw new ArgumentNullException(
                    nameof(configuredMaterial));
            }

            var originalShader = configuredMaterial.shader;
            if (originalShader == null)
            {
                throw new InvalidOperationException(
                    "The configured fixture material must have a shader.");
            }

            var lockedShader = Shader.Find(LockedStandInShaderName);
            if (lockedShader == null)
            {
                throw new InvalidOperationException(
                    "The locked stand-in shader must import.");
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    originalShader, out var guid, out long _))
            {
                throw new InvalidOperationException(
                    "The original fixture shader must have a GUID.");
            }

            configuredMaterial.name = materialName;
            configuredMaterial.shader = lockedShader;
            configuredMaterial.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            configuredMaterial.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                originalShader.name);
            configuredMaterial.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName, guid);
            configuredMaterial.SetOverrideTag(
                LockedMaterialIdentity.AllLockedGuidsTagName,
                "0123456789abcdef0123456789abcdef");
            return configuredMaterial;
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
