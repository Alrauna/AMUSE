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
    /// The window close pass: reference reversion as the only close
    /// outcome on every build path, and the fallback discipline that
    /// inverts the whole swap-in remap before the clone is destroyed.
    /// Every case drives the real pass through the window test
    /// lifecycle with only the falsified seam injected.
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
        /// The primary close outcome: every slot that
        /// held the unlocked clone holds the locked original again, every
        /// committed reference remapped to the clone names L, the clone is
        /// destroyed, and the vendor lock is never called. The
        /// plausible wrong implementation is the close this test
        /// replaced: it submitted U to the vendor, re-locked the clone,
        /// and shipped it, so the vendor-call and slot assertions both
        /// failed before the close changed.
        /// </summary>
        [Test]
        public void CloseRevertsEveryOpenPairToTheLockedOriginal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE close reversion fixture");
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "RevertedCape"));
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

            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.LockCallCount, Is.Zero,
                "the close pass must never lock through the vendor, for " +
                "the unlocked clone or for anything else");
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "every slot that held the unlocked clone holds L after " +
                "the close");
            // Unity destroys the clone natively, so the captured wrapper
            // is fake null: destroyed but not managed null.
            Assert.That(cloneRef == null, Is.True,
                "the close destroys U only after the inversion, and the " +
                "reversion is the primary path");
            var clip = CommittedClip(root);
            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the material-swap curve must survive the reversion");
            Assert.That(curveMaterials, Has.All.EqualTo(locked),
                "every committed reference remapped to U names L again");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty,
                "a build that completes holds zero open pairs");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockCloneRetained),
                Is.Zero,
                "a provably inverted reversion retains nothing");
        }

        /// <summary>
        /// The impostor half of the reversion. A pair whose unlocked
        /// clone already looks locked on its lock flag but still carries
        /// an unlocked shader name must not tempt the close into a vendor
        /// call: the reversion is the primary path, the impostor reverts
        /// to L, and no lock call runs. The wrong implementation this
        /// replaced submitted the impostor to the vendor and shipped the
        /// re-locked clone, so the vendor-call and slot assertions failed
        /// before the close changed.
        /// </summary>
        [Test]
        public void AnAlreadyLockedLookingCloneStillRevertsToTheLockedOriginal()
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
                // original. The close must not relock it; it reverts.
                pair.UnlockedClone.SetFloat(flagName, 1f);
            };

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.LockCallCount, Is.Zero,
                "the impostor clone must never reach the vendor");
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "the impostor pair reverts to the locked original, so " +
                "no unlocked shader name can ship");
        }

        /// <summary>
        /// The reversion inverts the whole swap-in remap through the
        /// fallback machinery, which is the primary close path now: slot
        /// arrays and every recorded closure reference back to L, and the
        /// clone destroyed only after the inversion. No surviving
        /// reference to the clone remains, no exception escapes, and no
        /// refusal is recorded, because a reverting close needs no vendor.
        /// </summary>
        [Test]
        public void CloseInvertsTheRemapBackToL()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE reversion fixture");
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
                "the reversion puts the original locked material back in " +
                "the slot");
            // Unity destroys the clone natively, so the captured wrapper
            // is fake null: destroyed but not managed null. The Unity
            // equality operator is the check that observes the destroy;
            // NUnit's Is.Null would demand a managed null no destroyed
            // Unity object can ever satisfy.
            Assert.That(cloneRef == null, Is.True,
                "the close destroys the clone only after every " +
                "reference is back on L, so no reference to U survives");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the material-swap curve must survive the reversion");
            Assert.That(curveMaterials, Has.All.EqualTo(locked),
                "no surviving curve reference may point at the clone");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockCloneRetained),
                Is.Zero,
                "a provably inverted reversion retains nothing");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
        }

        /// <summary>
        /// The reversion family, blend-tree coverage. A material swap clip
        /// that lives under a blend tree state is rewritten by the swap-in
        /// through the animation index like any other object curve, so the
        /// reversion must invert it on the committed clips too: every
        /// keyframe back to L, no surviving reference to the clone, and
        /// the clone destroyed only after the inversion.
        /// </summary>
        [Test]
        public void ABlendTreeResidentSwapClipRevertsToL()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE blend tree reversion fixture");
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
            var clip = CommittedClip(root);

            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "the reversion puts the original locked material back in " +
                "the slot");
            Assert.That(cloneRef == null, Is.True,
                "the close destroys the clone, but only after every " +
                "reference is back on L");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the blend-tree-resident material-swap curve must " +
                "survive the reversion");
            Assert.That(curveMaterials, Has.All.EqualTo(locked),
                "no surviving curve reference may point at the clone");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
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
        /// The third outcome of the close, named. A reverting pair whose
        /// committed-curve inversion cannot be proven complete keeps the
        /// unlocked clone alive, because a destroyed material that a
        /// committed curve still references would serialize as a missing
        /// reference. The named wrong implementation is the close pass
        /// that retains the clone silently: the slot reverts to L and no
        /// record names the retained unlocked reference.
        /// </summary>
        [Test]
        public void AReversionWithoutProvenInversionNamesTheRetainedClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

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
                        .TransientUnlockCloneRetained),
                Is.EqualTo(1),
                "the retention of the unlocked clone must be named, " +
                "never pass silently");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
        }

        /// <summary>
        /// F13. The locked original is never destroyed; only references
        /// drop, and the clone is the only destroyable side. The named
        /// wrong implementation is a sweep that destroys the original.
        /// </summary>
        [Test]
        public void TheFallbackNeverDestroysTheLockedOriginal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

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
        /// locked material goes in locked and the user's own locked
        /// original ships, with the marker at one, a locked shader name,
        /// its suffixed clip binding still live, and every material that
        /// was never locked untouched. Increment 3 flips the admission
        /// stance; this test must still pass unchanged then, so it
        /// depends only on build outcomes, never on where the swap-in
        /// sits in the pass list.
        /// </summary>
        [Test]
        public void LockedMaterialShipsTheUntouchedLockedOriginal()
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

            // The swapped pair went in locked and the locked original
            // ships: the close reverts the pair, so the shipped material
            // is L itself.
            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped, Is.EqualTo(locked),
                "the close reverts the pair, so the locked original " +
                "ships");
            Assert.That(shipped.name, Is.EqualTo("Cape"),
                "the shipped original keeps its exact name");
            Assert.That(shipped.shader.name, Does.StartWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix),
                "the shipped shader is the locked form");
            Assert.That(shipped.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                "the lock marker equals one on the shipped original");

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

            // The material-swap curve references the locked original the
            // build ships, so the animation the user authored keeps
            // working. The non-empty guard keeps the all-equal
            // constraint from passing vacuously.
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
