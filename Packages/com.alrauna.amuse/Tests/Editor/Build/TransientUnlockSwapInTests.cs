using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Tests.Editor.Shared;
using nadena.dev.ndmf;
using UnityEditor;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The swap-in service of the transient unlock window. Each falsifier
    /// is one named case against one named wrong implementation, with a
    /// no-op guard, and every case drives the real service through the
    /// window test lifecycle with only the falsified seam injected. The
    /// vendor never installs: the stand-in vendor type mirrors the
    /// verified signatures and scripts each outcome.
    /// </summary>
    public sealed class TransientUnlockSwapInTests
    {
        private readonly TestTransientScope _scope =
            new TestTransientScope(allowDestroyingAssets: true);

        [SetUp]
        public void SetUp()
        {
            TransientUnlockTestKnobs.Reset();
            TransientUnlockTestLifecycle.DeleteTempFolder();
            AssetDatabase.Refresh();
            TransientUnlockTestKnobs.DisableAaoForFixture();
            TransientUnlockTestLifecycle.OpenFixtureScene();
        }

        [TearDown]
        public void TearDown()
        {
            TransientUnlockTestKnobs.RestoreAaoAfterFixture();
            _scope.TearDown();
            TransientUnlockTestLifecycle.DiscardFixtureScene();
            TransientUnlockTestKnobs.Reset();
            TransientUnlockTestLifecycle.DeleteTempFolder();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            if (obj != null)
            {
                _scope.Track(obj);
            }

            return obj;
        }

        /// <summary>
        /// F1. A restore that fails its verification is caught on facts.
        /// The wrong implementation is a verifier that trusts the
        /// delegate's outcome: it swaps an unlocked clone into the slot
        /// and ships it. The verification contract must catch the
        /// failure on facts, refuse the slot by name, and never swap
        /// in. The restore is the in-memory reconstruction, so the
        /// failure is injected through an unresolvable recorded GUID.
        /// </summary>
        [Test]
        public void NoOpRestoreIsCaughtByTheVerificationContract()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE no-op restore fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "NoOpCape"));
            // The scripted vendor restore mode is superseded: the restore
            // is the in-memory reconstruction, so this member fails for
            // real through an unresolvable recorded GUID.
            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                "0123456789abcdef0123456789abcdef");
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            TransientUnlockTestLifecycle.AddRenderer(root, mesh, locked);

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();

            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.EqualTo(1),
                "the clone step runs; the verification is what refuses");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.Zero,
                "a no-op restore must never swap in");
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRestoreMismatch),
                Is.EqualTo(1),
                "the no-op restore must take the named slot refusal");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs,
                Is.Empty,
                "a refused clone never stays open in the window");
            Assert.That(root.GetComponentInChildren<SkinnedMeshRenderer>()
                    .sharedMaterials[0],
                Is.EqualTo(locked),
                "the slot keeps its original locked material");
        }

        /// <summary>
        /// F2. A partial restore failure shares the no-op detection
        /// machinery: the named refusal fires, the slot keeps L, and
        /// nothing throws, so the failure never becomes a closure
        /// failure. The restore is the in-memory reconstruction, so the
        /// failure is injected through an unresolvable recorded GUID.
        /// </summary>
        [Test]
        public void PartialRestoreProducesTheNamedRefusal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE partial restore fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "PartialCape"));
            // The scripted vendor restore mode is superseded: the restore
            // is the in-memory reconstruction, so this member fails for
            // real through an unresolvable recorded GUID.
            locked.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                "0123456789abcdef0123456789abcdef");
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);

            var context = default(BuildContext);
            Assert.DoesNotThrow(
                () => context = AvatarProcessor.ProcessAvatar(
                    root, TransientUnlockTestPlatform.Instance),
                "a restore mismatch is a named refusal, never a closure " +
                "failure");

            var state = context.GetState<AmusePlatformFinishState>();
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .TransientUnlockRestoreMismatch),
                Is.EqualTo(1),
                "the partial restore must take the named slot refusal");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.RestoreMismatchSlots,
                Is.EqualTo(1));
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.Zero,
                "a partially restored clone never swaps in");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "the slot keeps its original locked material");
        }

        /// <summary>
        /// F3. No step of the whole cycle deletes or mutates L, its
        /// generated shader asset, or L's tags. The named wrong
        /// implementation is a restore path that restores L in place
        /// instead of the clone, which mutates exactly these facts.
        /// </summary>
        [Test]
        public void NoCycleStepDeletesOrMutatesTheLockedOriginal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE untouched original fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "UntouchedCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);

            var shaderBefore = locked.shader;
            var shaderAssetPath = AssetDatabase.GetAssetPath(shaderBefore);
            var generatedDigestBefore =
                Sha256OfFile(shaderAssetPath);
            var originalShaderTagBefore = locked.GetTag(
                LockedMaterialIdentity.OriginalShaderTagName, true, null);
            var originalGuidTagBefore = locked.GetTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                true, null);
            var allGuidsTagBefore = locked.GetTag(
                LockedMaterialIdentity.AllLockedGuidsTagName, true, null);
            var flagBefore = locked.GetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName);
            var nameBefore = locked.name;

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(locked, Is.Not.Null,
                "the locked original is never destroyed");
            Assert.That(locked.name, Is.EqualTo(nameBefore));
            Assert.That(locked.shader, Is.EqualTo(shaderBefore),
                "L's shader reference never moves");
            Assert.That(locked.GetTag(
                    LockedMaterialIdentity.OriginalShaderTagName,
                    true, null),
                Is.EqualTo(originalShaderTagBefore),
                "L's identity tags never change");
            Assert.That(locked.GetTag(
                    LockedMaterialIdentity.OriginalShaderGuidTagName,
                    true, null),
                Is.EqualTo(originalGuidTagBefore));
            Assert.That(locked.GetTag(
                    LockedMaterialIdentity.AllLockedGuidsTagName,
                    true, null),
                Is.EqualTo(allGuidsTagBefore));
            Assert.That(locked.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(flagBefore),
                "L's lock flag never changes");
            Assert.That(Sha256OfFile(shaderAssetPath),
                Is.EqualTo(generatedDigestBefore),
                "the generated locked shader asset is byte-identical " +
                "after the whole cycle");

            // The shipped slot holds the locked original L: the close
            // reverts the pair, which is exactly why L had to stay
            // untouched.
            var shipped = renderer.sharedMaterials[0];
            Assert.That(shipped, Is.EqualTo(locked));
        }

        /// <summary>
        /// F7. An unlocked material with stale tags is not admitted: the
        /// locked name prefix and all three tags ride on a material whose
        /// optimizer flag is zero, so the two-signal identity rule keeps
        /// it out of the window entirely.
        /// </summary>
        [Test]
        public void UnlockedMaterialWithStaleTagsIsNotAdmitted()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE stale tags fixture"));
            var stale = Track(
                TransientUnlockTestLifecycle.StaleTaggedUnlockedMaterial());
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, stale);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.Zero,
                "an unlocked material is never cloned into the window");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(stale),
                "the slot keeps the unlocked material untouched");
        }

        /// <summary>
        /// The availability resolution succeeds with no vendor seam
        /// present, on the play path like on every path: the restore
        /// reconstructs in memory, so the machine-side gate carries no
        /// vendor-shaped answer and nothing can shut the window for a
        /// vendor's absence. The named wrong implementation is the
        /// production factory consulting a vendor readiness answer,
        /// which answered unattested on a machine without the vendor
        /// and shut the window on every path.
        /// </summary>
        [Test]
        public void TheAvailabilityResolvesWithoutAnyVendorSeamPresent()
        {
            // The play path label, which once decided the answer, must
            // no longer change it: resolution is vendor-free on every
            // path.
            TransientUnlockTestKnobs.BuildPath =
                AmuseBuildPath.ApplyOnPlay;

            var restore =
                TransientUnlockAvailability.CreateProductionRestore();

            Assert.That(restore, Is.Not.Null,
                "the resolved availability must carry the in-memory " +
                "restore, with no vendor-shaped gate left to refuse it");
        }

        /// <summary>
        /// F9. The vendor lock filter skips clones without an asset path,
        /// so a swap-in that skips persistence gets a silent no-op restore
        /// back. The clone the window holds must be persisted through the
        /// build's own asset saver, which is what makes the restore real.
        /// </summary>
        [Test]
        public void TheRestoreConsumesAPersistedClone()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE persisted clone fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "PersistedCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            TransientUnlockTestLifecycle.AddRenderer(root, mesh, locked);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.EqualTo(1),
                "the stand-in restore with the vendor filter must have " +
                "restored the persisted clone");
            var clone = TransientUnlockTestKnobs
                .Window.OpenPairs[0].UnlockedClone;
            var clonePath = AssetDatabase.GetAssetPath(clone);
            Assert.That(clonePath, Does.StartWith(
                    TransientUnlockTestLifecycle.TempFolder),
                "the window's clone must be persisted through the build " +
                "asset saver, or the vendor filter would have skipped it");
        }

        /// <summary>
        /// F12. The clone keeps L's exact name. Name parity is
        /// load-bearing: the rename-animated property suffix derives from
        /// the material name, so a clone renamed for diagnostics orphans
        /// every suffixed binding at the reversion.
        /// </summary>
        [Test]
        public void CloneKeepsTheExactNameOfTheOriginal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE name parity fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "ParityCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            TransientUnlockTestLifecycle.AddRenderer(root, mesh, locked);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            var clone = TransientUnlockTestKnobs
                .Window.OpenPairs[0].UnlockedClone;
            Assert.That(clone.name, Is.EqualTo(locked.name),
                "the clone must keep the exact material name");
            Assert.That(clone.name, Is.EqualTo("ParityCape"));
        }

        /// <summary>
        /// One unlocked clone exists per locked material across the whole
        /// avatar, whatever number of slots and renderers hold it.
        /// </summary>
        [Test]
        public void OneCloneServesEverySlotHoldingTheSameLockedMaterial()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE one clone fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "SharedCape"));
            var firstMesh =
                Track(TransientUnlockTestLifecycle.TwoSlotMesh());
            var first = TransientUnlockTestLifecycle.AddRenderer(
                root, firstMesh, locked, locked);
            var secondMesh =
                Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var second = TransientUnlockTestLifecycle.AddRenderer(
                root, secondMesh, locked);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.EqualTo(1),
                "one locked material yields exactly one clone");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.EqualTo(3),
                "every slot holding the locked material swaps to the " +
                "same clone");
            // A completed build holds zero open pairs: the close pass
            // reverted and closed the one pair the swap-in opened.
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);

            // Every slot ships the locked original L, and all three
            // slots hold one and the same material.
            var shipped = first.sharedMaterials[0];
            Assert.That(shipped, Is.EqualTo(locked),
                "the close reverts the pair, so the locked original " +
                "ships");
            Assert.That(shipped.name, Is.EqualTo("SharedCape"),
                "the shipped original keeps its exact material name");
            Assert.That(shipped.shader.name, Does.StartWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix),
                "the shipped shader must be a locked form");
            Assert.That(first.sharedMaterials[1], Is.EqualTo(shipped));
            Assert.That(second.sharedMaterials[0], Is.EqualTo(shipped));
        }

        /// <summary>
        /// The consent gate. A window that opens without a per-build
        /// grant is the named wrong implementation. Without the grant the
        /// service refuses before anything else runs, whatever the avatar
        /// carries.
        /// </summary>
        [Test]
        public void WindowWithoutConsentGrantNeverOpens()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.ConsentGranted = false;
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE consent gate fixture"));
            var locked = Track(TransientUnlockTestLifecycle.LockedMaterial(
                "UngrantedCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, locked);

            AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);

            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ConsentGatePassed,
                Is.False,
                "the gate must stop the swap-in before anything else");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.Zero);
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
            Assert.That(renderer.sharedMaterials[0], Is.EqualTo(locked),
                "without a grant the avatar keeps its locked materials");
        }

        /// <summary>
        /// The alignment refusal of the reconstruction reaches the slot
        /// refusal with its own sentence (A8.2, A8.3). The named wrong
        /// implementation records the generic mismatch sentence instead
        /// of the reconstruction sentence. The pair refuses at the
        /// generated label line, so the recorded detail must equal the
        /// aligner's line sentence.
        /// </summary>
        [Test]
        public void AlignmentRefusalDetailReachesTheSlotRefusal()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE alignment refusal fixture"));
            var fixture = new LockedSourceFixture();
            fixture.WithGeneratedLine(
                "shader_master_label",
                "        shader_master_label " +
                "(\"Another Label\", Float) = 0");
            var locked = Track(fixture.Locked("AlignmentRefusalCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            TransientUnlockTestLifecycle.AddRenderer(root, mesh, locked);

            var reports = ErrorReport.CaptureErrors(() =>
                AvatarProcessor.ProcessAvatar(
                    root, TransientUnlockTestPlatform.Instance));
            var messageFound = false;
            foreach (var report in reports)
            {
                if (report.TheError is SimpleError simple &&
                    simple.ToMessage().Contains(
                        "A generated line does not match the " +
                        "expected line."))
                {
                    messageFound = true;
                }
            }

            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.RestoreMismatchSlots,
                Is.EqualTo(1),
                "the refused restore must take the mismatch slot");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.Zero,
                "an unaligned pair never swaps in");
            Assert.That(messageFound, Is.True,
                "the slot refusal must carry the reconstruction " +
                "sentence");
        }

        /// <summary>
        /// The seventh fact catches a consistency break after a restore
        /// that reported success (A8.3). The delegate runs the production
        /// reconstruction and then drops the keyword define the pair
        /// carries, so A5.9 breaks on the finished clone. The named wrong
        /// implementation trusts the success outcome and swaps the broken
        /// clone in.
        /// </summary>
        [Test]
        public void SeventhFactCatchesAConsistencyBreakAfterRestore()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE seventh fact fixture"));
            var fixture = new LockedSourceFixture();
            fixture.WithAddedDefine("AMUSE_SEVENTH_MARKER");
            var locked = Track(fixture.Locked("SeventhCape"));
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var renderer = TransientUnlockTestLifecycle.AddRenderer(
                root, mesh, locked);

            // The scripted restore: the real reconstruction first, then
            // the break. The reported outcome stays success.
            TransientUnlockTestKnobs.RestoreOverride = clone =>
            {
                var production =
                    TransientUnlockAvailability.CreateProductionRestore();
                var result = production(clone);
                if (result.Outcome ==
                    TransientUnlockRestoreOutcome.Failed)
                {
                    return result;
                }

                clone.shaderKeywords = System.Array.Empty<string>();
                return TransientUnlockRestoreResult.Succeeded();
            };

            try
            {
                AvatarProcessor.ProcessAvatar(
                    root, TransientUnlockTestPlatform.Instance);
            }
            finally
            {
                TransientUnlockTestKnobs.RestoreOverride = null;
            }

            Assert.That(renderer, Is.Not.Null,
                "fixture guard: the renderer must survive the build");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.RestoreMismatchSlots,
                Is.EqualTo(1),
                "the seventh fact must catch the consistency break");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.Zero,
                "a broken clone never swaps in");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs, Is.Empty);
        }

        // --- Falsifier: a gate that asks only the renderer it sits on
        // admits a shared locked material whose other renderer carries
        // the block ---

        /// <summary>
        /// A locked material shared by two renderers where only one
        /// renderer carries a property block must refuse on both
        /// renderers (A9, Review Focus 4). The decision runs per locked
        /// material over every collected slot. The clean locked material
        /// on the third renderer is the no-op guard: the same build
        /// swaps it in, so the fixture reaches the gate.
        /// </summary>
        [Test]
        public void
            PropertyBlockOnOneRendererRefusesEverySlotOfThatLockedMaterial()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE property block fixture"));
            var blocked = Track(
                TransientUnlockTestLifecycle.LockedMaterial("BlockedCape"));
            var clean = Track(
                TransientUnlockTestLifecycle.LockedMaterial("CleanCape"));

            // No-op guard before the build: the fixture is a recognized
            // locked material, and exactly one renderer of its material
            // carries the block.
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(blocked),
                Is.True,
                "fixture guard: the material must be a recognized " +
                "locked material");
            var mesh = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var blockedRenderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, blocked);
            var secondMesh =
                Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var blockedSibling =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, secondMesh, blocked);
            var thirdMesh =
                Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var cleanRenderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, thirdMesh, clean);
            var block = new MaterialPropertyBlock();
            block.SetColor("_Color", new Color(1f, 0f, 0f, 1f));
            blockedRenderer.SetPropertyBlock(block);
            Assert.That(
                blockedRenderer.HasPropertyBlock(),
                Is.True,
                "fixture guard: the first renderer must carry the block");
            Assert.That(
                blockedSibling.HasPropertyBlock(),
                Is.False,
                "fixture guard: the second renderer of the same locked " +
                "material must carry no block");

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();

            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .LockedMaterialPropertyBlock),
                Is.EqualTo(2),
                "both slots of the shared locked material must record " +
                "the refusal");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.EqualTo(1),
                "fixture guard: the clean locked material must open its " +
                "pair in the same build");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.EqualTo(1),
                "fixture guard: the clean locked material must swap in");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs,
                Has.Count.EqualTo(1),
                "a property block refusal never opens a pair for its " +
                "locked material");
            Assert.That(blockedRenderer.sharedMaterials[0],
                Is.EqualTo(blocked),
                "the blocked slot keeps its original locked material");
            Assert.That(blockedSibling.sharedMaterials[0],
                Is.EqualTo(blocked),
                "the clean renderer of the same locked material keeps " +
                "it too");
            Assert.That(cleanRenderer.sharedMaterials[0],
                Is.Not.SameAs(clean),
                "the clean locked material still swapped to its clone");
        }

        // --- Falsifier: a gate that reads the saved names of the
        // locked material admits a curve on a renamed name ---

        /// <summary>
        /// A float curve that binds the renamed name of a renamed
        /// property refuses the whole locked material, and every slot of
        /// it records the refusal (A10). The locked material reads that
        /// property under the renamed name only, so the unlocked copy
        /// would honor a curve the locked original never reads. The
        /// clean locked material without the binding is the no-op guard.
        /// </summary>
        [Test]
        public void AnimationMismatchRecordsTheRefusalOnEverySlot()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE animation mismatch fixture"));
            var fixture = new LockedSourceFixture();
            fixture.WithRename("S");
            var mismatched = Track(fixture.Locked("MismatchCape"));
            var clean = Track(fixture.Locked("CleanCape"));

            // No-op guard before the build: both materials must be
            // recognized locked materials, so the swap-in discovery can
            // collect them.
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(mismatched),
                Is.True,
                "fixture guard: the mismatched material must be a " +
                "recognized locked material");
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(clean),
                Is.True,
                "fixture guard: the clean material must be a recognized " +
                "locked material");

            var mesh = Track(TransientUnlockTestLifecycle.TwoSlotMesh());
            var mismatchRenderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, mesh, mismatched, mismatched);
            var secondMesh =
                Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var cleanRenderer =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, secondMesh, clean);

            // Distinct transform paths. The default fixture hosts share
            // one name, and the admission gate collects the bindings of
            // one path, so a shared path would make the clean material
            // read the mismatched material's curve.
            mismatchRenderer.gameObject.name = "AMUSE mismatch host";
            cleanRenderer.gameObject.name = "AMUSE clean host";

            var clip = TransientUnlockTestLifecycle.AddMaterialSwapAnimation(
                root, mismatchRenderer, 0, mismatched);
            TransientUnlockTestLifecycle.AddSuffixedFloatBinding(
                clip, root, mismatchRenderer, "material._AlphaForceOpaque_S",
                1f);

            // No-op guard before the build: the clip must carry the
            // renamed binding, so the collection reads a real curve. The
            // bound property is a float property under its renamed name,
            // so the commit writes no error log.
            var bindingFound = false;
            foreach (var binding in AnimationUtility
                         .GetCurveBindings(clip))
            {
                if (binding.propertyName == "material._AlphaForceOpaque_S")
                {
                    bindingFound = true;
                }
            }

            Assert.That(
                bindingFound,
                Is.True,
                "fixture guard: the clip must carry the renamed " +
                "binding");

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();

            // Fixture guards first: the clean locked material must swap
            // in the same build, so a zero refusal count can only mean
            // the gate skipped the mismatched material.
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.EqualTo(1),
                "fixture guard: the clean locked material must open its " +
                "pair in the same build");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.EqualTo(1),
                "fixture guard: the clean locked material must swap in");
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .LockedMaterialAnimationMismatch),
                Is.EqualTo(2),
                "both slots of the mismatched locked material must " +
                "record the refusal");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs,
                Has.Count.EqualTo(1),
                "an animation mismatch never opens a pair for its " +
                "locked material");
            Assert.That(mismatchRenderer.sharedMaterials[0],
                Is.EqualTo(mismatched),
                "the first slot keeps its original locked material");
            Assert.That(mismatchRenderer.sharedMaterials[1],
                Is.EqualTo(mismatched),
                "the second slot of the same locked material keeps it " +
                "too");
            Assert.That(cleanRenderer.sharedMaterials[0],
                Is.Not.SameAs(clean),
                "the clean locked material still swapped to its clone");
        }

        // --- Falsifier: a gate that collects every binding of a clip
        // instead of the bindings on the renderer path refuses a locked
        // material for a curve that animates another renderer ---

        /// <summary>
        /// One clip animates a renamed property on the path of renderer C
        /// and swaps the slot of renderer A. The locked material on path
        /// A must swap in, and the same binding on its own path must
        /// refuse. The named wrong implementation collects every binding
        /// of every clip that touches the renderer path, so the other
        /// renderer's curve refuses this locked material beyond the
        /// designed A10.4 cost.
        /// </summary>
        [Test]
        public void ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial()
        {
            using var assets = new OverrideTemporaryDirectoryScope(
                TransientUnlockTestLifecycle.TempFolder);
            TransientUnlockTestKnobs.SkipClose = true;

            var root = Track(FixtureAvatarIdentity.CreateOptedRoot("AMUSE multi path fixture"));
            var fixture = new LockedSourceFixture();
            fixture.WithRename("S");
            var admitted = Track(fixture.Locked("PathACape"));
            var refused = Track(fixture.Locked("PathCCape"));

            // No-op guard before the build: both materials must be
            // recognized locked materials.
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(admitted),
                Is.True,
                "fixture guard: the admitted material must be a " +
                "recognized locked material");
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(refused),
                Is.True,
                "fixture guard: the refused material must be a " +
                "recognized locked material");

            var meshA = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var rendererA =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, meshA, admitted);
            var meshC = Track(TransientUnlockTestLifecycle.OneSlotMesh());
            var rendererC =
                TransientUnlockTestLifecycle.AddRenderer(
                    root, meshC, refused);
            rendererA.gameObject.name = "AMUSE path A host";
            rendererC.gameObject.name = "AMUSE path C host";

            // One clip with two paths: the material swap on the path of
            // A, and the renamed float binding on the path of C.
            var clip = Track(new AnimationClip
            {
                name = "AMUSE multi path curves",
            });
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(
                    "AMUSE path A host",
                    typeof(SkinnedMeshRenderer),
                    "m_Materials.Array.data[0]"),
                new[]
                {
                    new ObjectReferenceKeyframe
                    {
                        time = 0f,
                        value = admitted,
                    },
                });
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(
                    "AMUSE path C host",
                    typeof(SkinnedMeshRenderer),
                    "material._AlphaForceOpaque_S"),
                AnimationCurve.Constant(0f, 1f, 1f));
            AlphaSeparationTestLifecycle.NewController(
                root, "AMUSE multi path graph", clip);

            var context = AvatarProcessor.ProcessAvatar(
                root, TransientUnlockTestPlatform.Instance);
            var state = context.GetState<AmusePlatformFinishState>();

            // Guard: the same binding on the locked material's own path
            // must refuse, so the fixture reaches the gate with a real
            // curve.
            Assert.That(
                state.SlotRefusalCount(
                    AlphaSeparationSlotRefusal
                        .LockedMaterialAnimationMismatch),
                Is.EqualTo(1),
                "fixture guard: the binding on the locked material's " +
                "own path must refuse");
            Assert.That(rendererC.sharedMaterials[0],
                Is.EqualTo(refused),
                "the refused slot keeps its original locked material");

            // The subject: the locked material on the untouched path
            // swaps in, because the clip's other path is not its
            // admission set.
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.ClonesCreated,
                Is.EqualTo(1),
                "fixture guard: the untouched locked material must open " +
                "its pair");
            Assert.That(
                TransientUnlockTestKnobs.LastSwapSummary.SlotsSwapped,
                Is.EqualTo(1),
                "the locked material on the untouched path must swap in");
            Assert.That(rendererA.sharedMaterials[0],
                Is.Not.SameAs(admitted),
                "the locked material on the untouched path swapped to " +
                "its clone");
            Assert.That(
                TransientUnlockTestKnobs.Window.OpenPairs,
                Has.Count.EqualTo(1),
                "only the untouched locked material opens a pair");
        }

        private static string Sha256OfFile(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
