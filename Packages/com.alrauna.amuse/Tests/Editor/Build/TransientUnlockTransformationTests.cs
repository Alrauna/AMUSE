using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[assembly: ExportsPlugin(typeof(
    Alrauna.Amuse.Tests.Editor.Build
        .TransientUnlockTransformationTests
        .TransientUnlockTransformationTestPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Transformation inside the unlock window: the full production pass
    /// sequence on an apply-capable platform, with the swap-in placed
    /// before capture. Every case drives the real barrier, the real apply
    /// pass, and the real window close, substituting only the verified
    /// fixture seams, the consent presenter, and the vendor readiness
    /// seam. The vendor never installs; the stand-in vendor type scripts
    /// each outcome.
    /// </summary>
    public sealed class TransientUnlockTransformationTests
    {
        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            TransientUnlockTestKnobs.Reset();
            TransientUnlockTransformationTestPlugin.ResetProbeState();
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
            TransientUnlockTransformationTestPlugin.ResetProbeState();
            DeleteTempFolder();
        }

        /// <summary>
        /// Deletes the test-owned persistence folder if present. The
        /// window's clone persistence is load-bearing, so a container left
        /// by an earlier build must never leak into the next one.
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

        private T Track<T>(T obj) where T : UnityEngine.Object
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
                    UnityEngine.Object.DestroyImmediate(tracked[index], true);
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
        /// The verified opaque Poiyomi stand-in the apply fixtures use: an
        /// unlocked, schema-complete material whose conversion produces
        /// the real canonical opaque clone.
        /// </summary>
        private static Material VerifiedOpaqueFixtureMaterial()
        {
            var material = PoiyomiFixtureTestBase.CreateVerifiedMaterial();
            material.SetFloat("_AlphaForceOpaque", 1f);
            return material;
        }

        /// <summary>
        /// The committed clip the build shipped, read through the animator
        /// because virtualization may commit a clone of the authored
        /// instance.
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
                    if (state.state.motion is AnimationClip clip)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }

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
        /// The increment's core proof: the locked slot reaches the
        /// pipeline as its unlocked clone, so capture reads U, the
        /// frontend computes semantics from the full property table, the
        /// canonical opaque derives from U, and apply performs its single
        /// mutation sequence on the unlocked world. First observed green
        /// only after the capture carried the swapped view; against the
        /// pre-increment capture it refused the renderer by name, which
        /// the report records with the probe evidence.
        /// </summary>
        [Test]
        public void UnlockedSlotRunsTheWholePipelineInsideTheWindow()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE pipeline window fixture");
            var locked = Track(
                TransientUnlockTestLifecycle.LockedMaterialWithOriginal(
                    "PipelineCape",
                    "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                Track(TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked));
            TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);
            var lockedShaderBefore = locked.shader;
            var lockedFlagBefore = locked.GetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName);

            Material unlockedClone = null;
            TransientUnlockTestKnobs.BeforeClose = context =>
            {
                unlockedClone = context
                    .GetState<TransientUnlockWindowState>()
                    .OpenPairs[0].UnlockedClone;
            };

            var context = AvatarProcessor.ProcessAvatar(
                root,
                TransientUnlockTransformationTestPlugin.PlatformInstance);
            var state = context.GetState<AmusePlatformFinishState>();

            // No-op guards: the fixture must have exercised the whole
            // path, or the assertions below prove nothing.
            Assert.That(
                TransientUnlockTransformationTestPlugin.LastBarrierError,
                Is.Null,
                "the barrier threw: " +
                TransientUnlockTransformationTestPlugin.LastBarrierError);
            Assert.That(
                TransientUnlockTestKnobs.LastCloseError,
                Is.Null,
                "the window close threw: " +
                TransientUnlockTestKnobs.LastCloseError);
            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.UnlockCallCount,
                Is.EqualTo(1),
                "the window must restore exactly one clone");
            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.LockCallCount,
                Is.GreaterThanOrEqualTo(1),
                "the window must close over the swapped pair");
            Assert.That(unlockedClone, Is.Not.Null,
                "the swapped pair must hold its unlocked clone at close");
            Assert.That(
                TransientUnlockTransformationTestPlugin.ConsentSubjectsSeen,
                Has.Some.EqualTo(
                    TransientUnlockAvailability.WindowConsentSubject),
                "the eligible build must ask for the window consent");
            Assert.That(locked, Is.Not.Null,
                "the locked original must survive untouched");
            Assert.That(locked.shader, Is.EqualTo(lockedShaderBefore),
                "the locked original keeps its locked stand-in shader");
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(lockedFlagBefore),
                "the locked original keeps its lock flag at the locked " +
                "stand-in form");

            // The pipeline ran on the unlocked world.
            Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                "the renderer must analyze: after the swap-in its only " +
                "slot holds an unlocked material");
            Assert.That(state.SemanticallyRefusedRendererCount, Is.Zero,
                "a swapped renderer carries no renderer refusal");
            Assert.That(state.Separation, Is.Not.Null,
                "the unlocked slot must be prepared");
            Assert.That(state.Separation.CreatedClones, Has.Count.EqualTo(1),
                "the canonical opaque must derive from U through the " +
                "existing per-family conversion");
            Assert.That(state.AppliedRendererCount, Is.EqualTo(1),
                "apply must perform its single mutation sequence");
            Assert.That(
                state.Separation.OpaqueBySource.ContainsKey(locked),
                Is.False,
                "the canonical opaque derives from U, never from L");
            var canonical = state.Separation.OpaqueBySource[unlockedClone];
            Assert.That(canonical, Is.Not.Null,
                "the canonical opaque is keyed by U");

            // The shipped slot carries apply's recorded write. The close
            // pass re-asserts apply's finalization slot write for a
            // transformed pair, so the canonical material derived from U
            // ships and the slot agrees with the committed curve. The
            // named wrong implementation is a close pass that
            // unconditionally re-asserts the re-locked clone, which
            // overwrites apply's canonical write after the phase-end
            // rebind applied the authored clip's stale t=0 value.
            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped, Is.EqualTo(canonical),
                "the transformed slot ships apply's recorded write");
            Assert.That(shipped, Is.Not.EqualTo(locked),
                "the locked original must not ship");
            Assert.That(shipped, Is.Not.EqualTo(unlockedClone),
                "the transformed slot does not ship the unlocked clone");
            Assert.That(TransientUnlockTestKnobs.Window.OpenPairs,
                Is.Empty, "a completed build holds zero open pairs");
            Assert.That(state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal.TransientUnlockRelockFailed),
                Is.Zero, "a verified close records no fallback refusal");

            // The material-swap animation follows the transformation: the
            // committed curve references the canonical material derived
            // from U, through the L to U mapping composed with the
            // existing canonical mapping.
            var clip = CommittedClip(root);
            Assert.That(clip, Is.Not.Null,
                "the committed animator must hold a clip");
            var curveMaterials = CurveMaterials(clip);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the committed animator must carry the material swap");
            Assert.That(curveMaterials, Has.All.EqualTo(canonical),
                "the curve maps L to U and then U to the canonical " +
                "through the existing rewrite machinery");
        }

        /// <summary>
        /// F4. A mixed closed set degrades per slot: the member whose
        /// unlock fails keeps its original material while the sibling slot
        /// still optimizes. The named wrong implementation refuses the
        /// whole renderer on one member's failure, which kills the
        /// sibling's applied write.
        /// </summary>
        [Test]
        public void AMixedClosedSetDegradesPerSlot()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.NoOpRestore;

            var root = BuildAvatarRoot("AMUSE mixed closed set fixture");
            var failing = Track(
                TransientUnlockTestLifecycle.LockedMaterial("MismatchCape"));
            var sibling = Track(VerifiedOpaqueFixtureMaterial());
            var mesh = Track(TransientUnlockTestLifecycle.TwoSlotMesh());
            var renderer =
                Track(TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, failing, sibling));

            var context = AvatarProcessor.ProcessAvatar(
                root,
                TransientUnlockTransformationTestPlugin.PlatformInstance);
            var state = context.GetState<AmusePlatformFinishState>();

            // No-op guards.
            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.UnlockCallCount,
                Is.EqualTo(1),
                "the clone step must run; the verification is what " +
                "refuses the failing member");
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRestoreMismatch),
                Is.EqualTo(1),
                "the failing member must take the named slot refusal");
            Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                "the renderer must still analyze");
            Assert.That(state.Separation, Is.Not.Null,
                "the sibling slot must still be prepared");

            // The failing member keeps its original material.
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(failing),
                "the failing member keeps its original locked material");

            // The sibling slot still optimizes.
            Assert.That(renderer.sharedMaterials[1],
                Is.EqualTo(state.Separation.OpaqueBySource[sibling]),
                "the sibling slot must still optimize");
            Assert.That(state.AppliedRendererCount, Is.EqualTo(1),
                "the sibling's applied write must survive one member's " +
                "failure");
            Assert.That(
                state.Separation.OpaqueBySource.ContainsKey(failing),
                Is.False,
                "the failing member never converts");
        }

        /// <summary>
        /// F5. Suffixed clip bindings survive swap-in, transformation, and
        /// re-lock as live. The named wrong implementation renames the
        /// clone for diagnostics, which orphans every suffixed binding at
        /// the re-lock; the name parity assertion kills it.
        /// </summary>
        [Test]
        public void SuffixedBindingsSurviveSwapInTransformationAndRelock()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE suffixed binding fixture");
            var locked = Track(
                TransientUnlockTestLifecycle.LockedMaterial("ParityCape"));
            var sibling = Track(VerifiedOpaqueFixtureMaterial());
            var mesh = Track(TransientUnlockTestLifecycle.TwoSlotMesh());
            var renderer =
                Track(TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked, sibling));
            var clip = TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, renderer, 0, locked);
            TransientUnlockTestLifecycle.AddSuffixedFloatBinding(
                clip, root, renderer, "material._MainTex_ParityCape", 2f);

            var context = AvatarProcessor.ProcessAvatar(
                root,
                TransientUnlockTransformationTestPlugin.PlatformInstance);
            var state = context.GetState<AmusePlatformFinishState>();

            // No-op guards: the transformation and the re-lock ran.
            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.LockCallCount,
                Is.GreaterThanOrEqualTo(1),
                "the window must close over the swapped pair");
            Assert.That(state.AppliedRendererCount, Is.EqualTo(1),
                "the sibling slot must still optimize");
            Assert.That(renderer.sharedMaterials[1],
                Is.EqualTo(state.Separation.OpaqueBySource[sibling]),
                "the sibling slot must carry its canonical result");

            // Name parity per F12: the clone keeps the exact name, so the
            // suffixed binding resolves again after the re-lock.
            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped.name, Is.EqualTo("ParityCape"),
                "name parity holds: the clone keeps the exact name");
            Assert.That(shipped, Is.Not.EqualTo(locked),
                "the cycle runs through the unlocked clone");
            Assert.That(shipped.shader.name, Does.StartWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix),
                "the shipped shader must be a locked form");
            Assert.That(shipped.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                "the shipped clone must be locked again");

            // The suffixed binding survives the whole cycle: same
            // property, same constant value.
            var committed = CommittedClip(root);
            Assert.That(committed, Is.Not.Null,
                "the committed animator must hold a clip");
            var bodyPath = AnimationUtility.CalculateTransformPath(
                renderer.transform, root.transform);
            var suffixed = AnimationUtility.GetEditorCurve(
                committed,
                EditorCurveBinding.FloatCurve(
                    bodyPath,
                    typeof(SkinnedMeshRenderer),
                    "material._MainTex_ParityCape"));
            Assert.That(suffixed, Is.Not.Null,
                "the suffixed binding must survive the cycle");
            Assert.That(suffixed.keys[suffixed.keys.Length - 1].value,
                Is.EqualTo(2f));

            // The material-swap curve references the re-locked clone, so
            // the animation stays live against the shipped material.
            var curveMaterials = CurveMaterials(committed);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the committed animator must carry the material swap");
            Assert.That(curveMaterials, Has.All.EqualTo(shipped));

            Assert.That(TransientUnlockTestKnobs.Window.OpenPairs,
                Is.Empty, "a completed build holds zero open pairs");
        }

        /// <summary>
        /// F6. An unlock failure never widens refusal beyond the owning
        /// slot. The named wrong implementation records the restore
        /// mismatch as a renderer refusal, which populates the renderer
        /// buckets this test holds at zero.
        /// </summary>
        [Test]
        public void AnUnlockFailureNeverWidensRefusalBeyondTheOwningSlot()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.NoOpRestore;

            var root = BuildAvatarRoot("AMUSE slot scoped refusal fixture");
            var failing = Track(
                TransientUnlockTestLifecycle.LockedMaterial("OwnSlotCape"));
            var sibling = Track(VerifiedOpaqueFixtureMaterial());
            var mesh = Track(TransientUnlockTestLifecycle.TwoSlotMesh());
            var renderer =
                Track(TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, failing, sibling));

            var context = AvatarProcessor.ProcessAvatar(
                root,
                TransientUnlockTransformationTestPlugin.PlatformInstance);
            var state = context.GetState<AmusePlatformFinishState>();

            // The owning slot takes the named refusal, exactly once.
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRestoreMismatch),
                Is.EqualTo(1),
                "the owning slot takes the named slot refusal");
            foreach (AlphaSeparationSlotRefusal reason in Enum.GetValues(
                         typeof(AlphaSeparationSlotRefusal)))
            {
                if (reason ==
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRestoreMismatch ||
                    reason == AlphaSeparationSlotRefusal.None)
                {
                    continue;
                }

                Assert.That(
                    state.SlotRefusalCount(reason), Is.Zero,
                    "no other slot refusal bucket may fill: got " + reason);
            }

            // The refusal never widens to the renderer.
            Assert.That(state.SemanticallyRefusedRendererCount, Is.Zero,
                "one member's unlock failure must not refuse the " +
                "renderer");
            foreach (RendererAnalysisRefusal reason in Enum.GetValues(
                         typeof(RendererAnalysisRefusal)))
            {
                if (reason == RendererAnalysisRefusal.None)
                {
                    continue;
                }

                Assert.That(
                    state.RendererRefusalCount(reason), Is.Zero,
                    "no renderer refusal bucket may fill: got " + reason);
            }

            // The owning slot keeps its original material and the sibling
            // still optimizes.
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(failing),
                "the owning slot keeps its original locked material");
            Assert.That(renderer.sharedMaterials[1],
                Is.EqualTo(state.Separation.OpaqueBySource[sibling]),
                "the sibling slot must still optimize");
            Assert.That(state.AnalyzedRendererCount, Is.EqualTo(1),
                "the renderer must still analyze");
            Assert.That(state.AppliedRendererCount, Is.EqualTo(1),
                "the sibling's applied write must survive");
        }

        /// <summary>
        /// One U per L across slots and closure: a locked material held in
        /// one renderer's slot and a locked material referenced only by
        /// another renderer's clip each get exactly one clone, and the
        /// clip-only reference remaps and transforms with the rest. The
        /// named wrong implementation discovers locked materials from the
        /// slot arrays alone, which leaves the clip-only reference on L,
        /// refuses the referencing slot, and clones nothing for it.
        /// </summary>
        [Test]
        public void AClipOnlyLockedMaterialGetsItsCloneAndRemaps()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = BuildAvatarRoot("AMUSE clip only lock fixture");
            var slotLocked = Track(
                TransientUnlockTestLifecycle.LockedMaterialWithOriginal(
                    "SlotCape",
                    "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest"));
            var clipLocked = Track(
                TransientUnlockTestLifecycle.LockedMaterialWithOriginal(
                    "ClipCape",
                    "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest"));
            var sibling = Track(VerifiedOpaqueFixtureMaterial());
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var slotRenderer =
                Track(TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, slotLocked));
            var clipRenderer =
                Track(TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, sibling));
            TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, clipRenderer, 0, clipLocked);

            Material slotClone = null;
            Material clipClone = null;
            TransientUnlockTestKnobs.BeforeClose = context =>
            {
                var pairs = context
                    .GetState<TransientUnlockWindowState>().OpenPairs;
                slotClone = pairs[0].UnlockedClone;
                clipClone = pairs[1].UnlockedClone;
            };

            var context = AvatarProcessor.ProcessAvatar(
                root,
                TransientUnlockTransformationTestPlugin.PlatformInstance);
            var state = context.GetState<AmusePlatformFinishState>();

            // No-op guards: two locked materials, exactly two clones.
            Assert.That(
                Thry.ThryEditor.ShaderOptimizer.UnlockCallCount,
                Is.EqualTo(2),
                "one clone per locked material across slots and closure");
            Assert.That(slotClone, Is.Not.Null,
                "the slot-held locked material gets its clone");
            Assert.That(clipClone, Is.Not.Null,
                "a locked material referenced only by clips gets its " +
                "own clone");

            // Both renderers analyze and both slots optimize.
            Assert.That(state.AnalyzedRendererCount, Is.EqualTo(2),
                "the swapped world analyzes everywhere");
            Assert.That(state.AppliedRendererCount, Is.EqualTo(2),
                "both renderers take their applied write");
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRestoreMismatch),
                Is.Zero, "both restores verified");

            // The clip-only reference remapped to the same one U, and the
            // transformation then mapped it to the canonical of U.
            var committed = CommittedClip(root);
            Assert.That(committed, Is.Not.Null,
                "the committed animator must hold a clip");
            var curveMaterials = CurveMaterials(committed);
            Assert.That(curveMaterials, Is.Not.Empty,
                "the committed animator must carry the clip-only swap");
            Assert.That(curveMaterials, Has.All.EqualTo(
                    state.Separation.OpaqueBySource[clipClone]),
                "the clip-only reference rides the same L to U mapping " +
                "and then the canonical mapping");
            Assert.That(slotRenderer.sharedMaterials[0],
                Is.EqualTo(state.Separation.OpaqueBySource[slotClone]),
                "the slot-held reference ships the canonical of its own " +
                "clone");
            Assert.That(clipRenderer.sharedMaterials[0],
                Is.EqualTo(state.Separation.OpaqueBySource[sibling]),
                "the referencing renderer's live slot carries apply's " +
                "recorded write");
        }

        // --- Test-local platform and plugin --------------------------------

        internal const string TransformationPlatformName =
            "com.alrauna.amuse.tests.transient-unlock-transformation";

        /// <summary>
        /// The apply-capable window platform: the production plugin never
        /// runs here, so the test plugin below is the whole PlatformFinish
        /// phase, in the production order with the production topology.
        /// </summary>
        internal sealed class TransientUnlockTransformationTestPlatform :
            INDMFPlatformProvider
        {
            internal static readonly
                TransientUnlockTransformationTestPlatform Instance =
                new TransientUnlockTransformationTestPlatform();

            public string QualifiedName => TransformationPlatformName;

            public string DisplayName =>
                "AMUSE transient unlock transformation";
        }

        /// <summary>
        /// The full production pass sequence: structural check, then one
        /// animator scope over the bindings capture, the real barrier
        /// (whose swap-in now opens before capture), and the real apply,
        /// then the extension-free window close ordered last. The seams
        /// substitute the verified fixture family, the consent dialog, the
        /// original-shader attestation, and the vendor readiness answer.
        /// </summary>
        [RunsOnPlatforms(TransformationPlatformName)]
        public sealed class TransientUnlockTransformationTestPlugin :
            Plugin<TransientUnlockTransformationTestPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.transient-unlock-transformation-plugin";

            internal static readonly
                TransientUnlockTransformationTestPlatform PlatformInstance =
                TransientUnlockTransformationTestPlatform.Instance;

            internal static readonly List<string> ConsentSubjectsSeen =
                new List<string>();

            internal static Exception LastBarrierError;

            internal static void ResetProbeState()
            {
                ConsentSubjectsSeen.Clear();
                LastBarrierError = null;
            }

            protected override void Configure()
            {
                var sequence = InPhase(BuildPhase.PlatformFinish);

                sequence.Run(
                    "AMUSE test structural graph check",
                    AmuseStructuralGraphCheck.Execute);

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner =>
                    {
                        inner.Run(
                            AmusePlatformFinishPlugin.BindingsCapturePassName,
                            AmuseAnimatorBindingsCapture.Execute);
                        inner.Run(
                            AmusePlatformFinishPlugin.BarrierPassName,
                            BarrierPass);
                        inner.Run(
                            AlphaSeparationApply.PassName,
                            ctx => AlphaSeparationApply.Execute(ctx));
                    });

                // The window close mirrors the production topology: an
                // extension-free pass after the animator scope has closed.
                sequence.Run(
                    TransientUnlockWindowClose.PassName, ClosePass);
            }

            private static void BarrierPass(BuildContext context)
            {
                try
                {
                    AmusePlatformFinishPass.Execute(
                        context,
                        TransientUnlockTestLifecycle.SupportedFacts(),
                        VerifiedLilToonTestSeams.SelectVerifiedFixtureRequest,
                        VerifiedLilToonTestSeams.CaptureVerifiedFixtureMaterials,
                        VerifiedLilToonTestSeams.VerifiedAlphaOnly,
                        VerifiedPoiyomiTestSeams.VerifiedConversion,
                        null,
                        subjects =>
                        {
                            ConsentSubjectsSeen.AddRange(subjects);
                            return true;
                        },
                        TransientUnlockTestKnobs.OriginalAttestation ??
                            (_ => true),
                        () => TransientUnlockTestKnobs.ThryAttested);
                }
                catch (Exception exception)
                {
                    LastBarrierError = exception;
                    throw;
                }
            }

            private static void ClosePass(BuildContext context)
            {
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
    }
}
