using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using nadena.dev.ndmf;
using UnityEditor;
using UnityEditor.Animations;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The window close pass: one batched re-lock, batch verification
    /// after the call, and the per-pair fallback that inverts the whole
    /// swap-in remap before any clone is destroyed. Every case drives the
    /// real pass through the window test lifecycle with only the falsified
    /// seam injected.
    /// </summary>
    public sealed class TransientUnlockWindowCloseTests
    {
        private readonly List<Object> tracked = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            TransientUnlockTestKnobs.Reset();
            DeleteTempFolder();
            AssetDatabase.Refresh();
            TransientUnlockTestKnobs.DisableAaoForFixture();
            TransientUnlockTestLifecycle.OpenFixtureScene();
        }

        [TearDown]
        public void TearDown()
        {
            TransientUnlockTestKnobs.RestoreAaoAfterFixture();
            DestroyTracked();
            TransientUnlockTestLifecycle.DiscardFixtureScene();
            TransientUnlockTestKnobs.Reset();
            DeleteTempFolder();
        }

        /// <summary>
        /// Deletes the test-owned persistence folder if present. Called
        /// from SetUp and TearDown: a container left by an earlier build
        /// must never leak into the next build's asset-saver state.
        /// </summary>
        internal static void DeleteTempFolder()
        {
            if (AssetDatabase.IsValidFolder(
                    TransientUnlockTestLifecycle.TempFolder))
            {
                AssetDatabase.DeleteAsset(
                    TransientUnlockTestLifecycle.TempFolder);
            }
        }


        private T Track<T>(T obj) where T : Object
        {
            if (obj != null)
            {
                tracked.Add(obj);
            }

            return obj;
        }

        private void DestroyTracked()
        {
            for (var index = 0; index < tracked.Count; index++)
            {
                if (tracked[index] != null)
                {
                    Object.DestroyImmediate(tracked[index], true);
                }
            }

            tracked.Clear();
        }

        private GameObject BuildAvatarRoot(string name)
        {
            FixtureAvatarIdentity.RequireVrcDescriptorSupport();
            var root = Track(new GameObject(name));
            root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
            FixtureAvatarIdentity.AttachVrcDescriptor(root);
            FixtureProofScope.PinAllSizes(root);
            return root;
        }

        /// <summary>
        /// The clip the build committed back onto the avatar's animator.
        /// Virtualization may commit a clone rather than the authored
        /// instance, so curve assertions must read through the animator,
        /// never through the fixture's original clip reference. Blend tree
        /// states are descended, because a material swap can live there.
        /// </summary>
        private static AnimationClip CommittedClip(GameObject root)
        {
            var animator = root.GetComponentInChildren<Animator>();
            var controller =
                animator.runtimeAnimatorController as AnimatorController;
            if (controller == null)
            {
                return null;
            }

            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    var clip = ClipInMotion(state.state.motion);
                    if (clip != null)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

        private static AnimationClip ClipInMotion(Motion motion)
        {
            if (motion is AnimationClip clip)
            {
                return clip;
            }

            if (motion is BlendTree blendTree)
            {
                foreach (var child in blendTree.children)
                {
                    var found = ClipInMotion(child.motion);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Every material keyframe on the given clip, in binding order.
        /// The real clip is read back after the build, so the assertion
        /// sees what the shipped animation actually holds.
        /// </summary>
        private static List<Material> CurveMaterials(AnimationClip clip)
        {
            var materials = new List<Material>();
            foreach (var binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                foreach (var keyframe in AnimationUtility
                             .GetObjectReferenceCurve(clip, binding))
                {
                    if (keyframe.value is Material material)
                    {
                        materials.Add(material);
                    }
                }
            }

            return materials;
        }

        /// <summary>
        /// F10. The close pass re-locks every open pair through one batch
        /// call and verifies the batch after it, so the pair ships
        /// re-locked and the window ends empty.
        /// </summary>
        [Test]
        public void CloseRelocksEveryOpenPairAndShipsNoneUnlocked()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE close success fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "RelockedCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);
            TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();

            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.LockCallCount,
                Is.GreaterThanOrEqualTo(1),
                "the close pass must call the vendor lock");
            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped, Is.Not.EqualTo(locked),
                "the verified pair ships the re-locked clone, not L");
            Assert.That(shipped.name, Is.EqualTo("RelockedCape"));
            Assert.That(shipped.shader.name, Does.StartWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix),
                "the shipped shader must be a locked form");
            Assert.That(shipped.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                "the shipped clone must be locked again");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty,
                "a build that completes holds zero open pairs");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRelockFailed),
                Is.Zero,
                "a verified close records no fallback refusal");
        }

        /// <summary>
        /// The impostor half of F10. A pair that already looks locked on
        /// its lock flag but carries an unlocked shader name must still go
        /// through the batch call and the post-call verification. A close
        /// pass that skips already-locked-looking pairs without
        /// verification ships the impostor with an unlocked shader name,
        /// and the locked-name assertion fails on exactly that.
        /// </summary>
        [Test]
        public void AnAlreadyLockedLookingPairStillTakesTheBatchCall()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE impostor pair fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "ImpostorCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);
            TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);

            var flagName =
                LockedMaterialIdentity.OptimizerEnabledPropertyName;
            TransientUnlockTestKnobs.BeforeClose = context =>
            {
                var pair = context
                    .GetState<TransientUnlockWindowState>()
                    .OpenPairs[0];
                // The impostor: flag at one, shader still the unlocked
                // original. Only the vendor lock can close this pair.
                pair.UnlockedClone.SetFloat(flagName, 1f);
            };

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped, Is.Not.EqualTo(locked),
                "a pair the vendor locked in the batch ships the clone");
            Assert.That(shipped.shader.name, Does.StartWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix),
                "a close pass that skipped the impostor on its flag " +
                "alone would ship the unlocked shader name here");
        }

        /// <summary>
        /// F11. A forced re-lock failure inverts the whole swap-in remap,
        /// slot arrays and every recorded closure reference, back to L,
        /// and records the named refusal per affected slot. No surviving
        /// reference to the clone remains, and no exception escapes.
        /// </summary>
        [Test]
        public void ForcedRelockFailureInvertsTheRemapBackToL()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.FailRelock;

            var root = BuildAvatarRoot("AMUSE relock failure fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "FallbackCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);
            TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);

            var cloneRef = default(Object);
            TransientUnlockTestKnobs.BeforeClose = context =>
            {
                cloneRef = context
                    .GetState<TransientUnlockWindowState>()
                    .OpenPairs[0].UnlockedClone;
            };

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();
            var clip = CommittedClip(root);

            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "the fallback puts the original locked material back in " +
                "the slot");
            // Unity destroys the clone natively, so the captured wrapper
            // is fake null: destroyed but not managed null. The Unity
            // equality operator is the check that observes the destroy;
            // NUnit's Is.Null would demand a managed null no destroyed
            // Unity object can ever satisfy.
            Assert.That(cloneRef == null, Is.True,
                "the fallback destroys the clone, the only destroyable " +
                "side of the window, so no reference to U survives");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the material-swap curve must survive the fallback");
            Assert.That(curveMaterials, Has.All.EqualTo(locked),
                "no surviving curve reference may point at the clone");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRelockFailed),
                Is.EqualTo(1),
                "the fallback records the named refusal per affected " +
                "slot");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
        }

        /// <summary>
        /// The F11 family, blend-tree coverage. A material swap clip that
        /// lives under a blend tree state is rewritten by the swap-in
        /// through the animation index like any other object curve, so the
        /// fallback must invert it on the committed clips too: every
        /// keyframe back to L, no surviving reference to the clone, and
        /// the clone destroyed only after the inversion.
        /// </summary>
        [Test]
        public void ABlendTreeResidentSwapClipFallsBackToL()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.FailRelock;

            var root = BuildAvatarRoot("AMUSE blend tree fallback fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "TreeCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);
            TransientUnlockTestLifecycle.AddBlendTreeSwapAnimation(
                root, renderer, 0, locked);

            var cloneRef = default(Object);
            TransientUnlockTestKnobs.BeforeClose = context =>
            {
                cloneRef = context
                    .GetState<TransientUnlockWindowState>()
                    .OpenPairs[0].UnlockedClone;
            };

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();
            var clip = CommittedClip(root);

            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "the fallback puts the original locked material back in " +
                "the slot");
            Assert.That(cloneRef == null, Is.True,
                "the fallback destroys the clone, but only after every " +
                "reference is back on L");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the blend-tree-resident material-swap curve must " +
                "survive the fallback");
            Assert.That(curveMaterials, Has.All.EqualTo(locked),
                "no surviving curve reference may point at the clone");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRelockFailed),
                Is.EqualTo(1),
                "the fallback records the named refusal per affected " +
                "slot");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
        }

        /// <summary>
        /// The exception half of F11. A vendor lock that throws is a named
        /// vendor failure: the production delegate converts it, the
        /// fallback fires, and the build never ships an unlocked clone.
        /// </summary>
        [Test]
        public void AThrowingVendorRelockStillFallsBackToL()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.ThrowOnRelock;

            var root = BuildAvatarRoot("AMUSE relock throw fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "ThrownCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);

            var context = default(BuildContext);
            Assert.DoesNotThrow(
                () => context = AvatarProcessor.ProcessAvatar(
                    root, TransientUnlockTestPlatform.Instance),
                "a converted vendor failure must fall back, never abort " +
                "the window");

            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked));
            var state = context.GetState<AmusePlatformFinishState>();
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRelockFailed),
                Is.EqualTo(1));
        }

        /// <summary>
        /// A bindings stand-in whose committed-graph enumeration refuses:
        /// the innate-controller entry carries an override controller,
        /// which is a runtime controller but not an AnimatorController,
        /// so the graph reports an unsupported controller form. This is
        /// the shape the close pass must treat as "a committed curve may
        /// still reference the clone": the fallback cannot prove the
        /// curve inversion complete, so the clone must stay alive, and
        /// the retention must be named.
        /// </summary>
        private sealed class RefusingStubBindings :
            nadena.dev.ndmf.animator.IPlatformAnimatorBindings
        {
            private readonly AnimatorOverrideController controller;

            internal RefusingStubBindings(
                AnimatorOverrideController trackedController)
            {
                controller = trackedController;
            }

            public bool IsSpecialMotion(Motion motion)
            {
                return false;
            }

            public System.Collections.Generic.IEnumerable<
                (object, RuntimeAnimatorController, bool)>
                GetInnateControllers(GameObject root)
            {
                yield return (null, controller, false);
            }

            public void CommitControllers(
                GameObject root,
                System.Collections.Generic.IDictionary<
                    object, RuntimeAnimatorController> controllers)
            {
                throw new System.InvalidOperationException(
                    "the close pass never commits controllers");
            }
        }

        /// <summary>
        /// The third outcome of the close, named. A pair whose re-lock
        /// failed and whose committed-curve inversion cannot be proven
        /// complete keeps the unlocked clone alive, because a destroyed
        /// material that a committed curve still references would
        /// serialize as a missing reference. The named wrong
        /// implementation is the close pass that retains the clone
        /// silently: the slot reverts to L and no record names the
        /// retained unlocked reference.
        /// </summary>
        [Test]
        public void ARelockFailureWithoutProvenInversionNamesTheRetainedClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.FailRelock;

            var root = BuildAvatarRoot("AMUSE retained clone fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "RetainedCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);
            TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);

            var cloneRef = default(Object);
            var stub = new RefusingStubBindings(
                Track(new AnimatorOverrideController()));
            TransientUnlockTestKnobs.BeforeClose = context =>
            {
                var pair = context
                    .GetState<TransientUnlockWindowState>()
                    .OpenPairs[0];
                cloneRef = pair.UnlockedClone;
                // Replace the retained host bindings after the swap-in,
                // so the close pass's committed-graph enumeration refuses
                // and the curve inversion cannot be proven complete.
                context.GetState<AmusePlatformFinishState>()
                    .AnimatorBindings = stub;
            };

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();
            var clip = CommittedClip(root);

            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "the fallback puts the original locked material back in " +
                "the slot");
            Assert.That(cloneRef == null, Is.False,
                "the close pass must keep the clone alive when a " +
                "committed curve may still reference it");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Has.Some.EqualTo(cloneRef),
                "the fixture precondition: a committed curve still " +
                "references the clone, which is why the clone stays " +
                "alive");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRelockFailed),
                Is.EqualTo(1),
                "the re-lock failure stays named");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockCloneRetained),
                Is.EqualTo(1),
                "the retention of the unlocked clone must be named, " +
                "never pass silently");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
        }

        /// <summary>
        /// F13. The fallback object is never destroyed; only references
        /// drop, and the clone is the only destroyable side. The named
        /// wrong implementation is a sweep that destroys the fallback.
        /// </summary>
        [Test]
        public void TheFallbackNeverDestroysTheLockedOriginal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.FailRelock;

            var root = BuildAvatarRoot("AMUSE fallback survival fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "SurvivorCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var shaderBefore = locked.shader;
            var flagBefore = locked.GetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName);
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(locked, Is.Not.Null,
                "the fallback is never destroyed");
            Assert.That(locked.shader, Is.EqualTo(shaderBefore),
                "the fallback's shader reference never moves");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(flagBefore),
                "the fallback's lock flag never changes");
        }

        /// <summary>
        /// A close pass over an empty window is a counted no-op: the
        /// avatar keeps its materials and the vendor lock is never called.
        /// </summary>
        [Test]
        public void CloseWithoutOpenPairsIsANoOp()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipSwapIn = true;

            var root = BuildAvatarRoot("AMUSE empty window fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "UncycledCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked));
            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.LockCallCount, Is.Zero,
                "an empty window never calls the vendor lock");
        }

        /// <summary>
        /// The behavior-neutral round trip, and a frozen regression: a
        /// locked material goes in locked, comes out re-locked, with the
        /// marker at one, a locked shader name, name parity per F12, its
        /// suffixed clip binding still live, and every material that was
        /// never locked untouched. Increment 3 flips the admission stance;
        /// this test must still pass unchanged then, so it depends only on
        /// build outcomes, never on where the swap-in sits in the pass
        /// list.
        /// </summary>
        [Test]
        public void LockedMaterialRoundTripsToReLockedEquivalent()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE round trip fixture");
            var body = Track(new GameObject("Body"));
            body.transform.SetParent(root.transform, false);
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "Cape"));
            var unLocked = Track(new Material(
                Shader.Find("Unlit/Color")) { name = "Plain" });
            var mesh = Track(TransientUnlockTestLifecycle.TwoSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    body, mesh, locked, unLocked);
            var clip = TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);
            TransientUnlockTestLifecycle.AddSuffixedFloatBinding(
                clip, root, renderer, "material._MainTex_Cape", 2f);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            clip = CommittedClip(root);
            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");

            Assert.That(TransientUnlockTestKnobs.LastSwapError, Is.Null,
                "swap-in threw: " + TransientUnlockTestKnobs.LastSwapError);
            Assert.That(TransientUnlockTestKnobs.LastCloseError, Is.Null,
                "close threw: " + TransientUnlockTestKnobs.LastCloseError);
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.EqualTo(1),
                "swap-in must have swapped the slot");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty,
                "the window must drain");

            // The swapped pair went in locked and comes out re-locked.
            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped, Is.Not.EqualTo(locked),
                "the cycle runs through the unlocked clone");
            Assert.That(shipped.name, Is.EqualTo("Cape"),
                "name parity per F12: the clone keeps the exact name");
            Assert.That(shipped.shader.name, Does.StartWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix),
                "the shader name is a locked form after the re-lock");
            Assert.That(shipped.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                "the lock marker equals one after the re-lock");

            // The locked original came through untouched and alive.
            Assert.That(locked, Is.Not.Null);
            Assert.That(locked.name, Is.EqualTo("Cape"));
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f));

            // The suffixed float binding stays live across the cycle:
            // same property, same constant value.
            var bodyPath = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);
            var suffixed = AnimationUtility.GetEditorCurve(
                clip, EditorCurveBinding.FloatCurve(
                    bodyPath,
                    typeof(SkinnedMeshRenderer),
                    "material._MainTex_Cape"));
            Assert.That(suffixed, Is.Not.Null,
                "the suffixed binding must survive the cycle");
            Assert.That(suffixed.keys[suffixed.keys.Length - 1].value,
                Is.EqualTo(2f));

            // The material-swap curve references the re-locked clone, so
            // the animation the user authored keeps working. The non-empty
            // guard keeps the all-equal constraint from passing vacuously.
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the committed animator must carry the material swap");
            Assert.That(curveMaterials, Has.All.EqualTo(shipped));

            // The material that was never locked is untouched.
            Assert.That(renderer.sharedMaterials[1], Is.EqualTo(unLocked),
                "analyzed and unlocked materials stay untouched");
        }
    }
}
