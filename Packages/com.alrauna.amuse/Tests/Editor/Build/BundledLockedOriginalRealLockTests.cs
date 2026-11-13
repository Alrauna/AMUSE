using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.DryRun;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The gated real lock acceptance suite for the two bundled 9.3 rows
    /// (Y9, docs/superpowers/specs/2026-10-25-bundled-locked-original-
    /// admission-design.md). One test per shape of record section 4.1.
    /// Each test locks a real material with the installed vendor lock
    /// tool, aligns the pair through the production aligner, and drives
    /// the production swap-in service by direct call. No NDMF build runs
    /// (spec Validation 4).
    /// <para>
    /// Gate. Every test starts with the opt-in gate. The suite runs only
    /// in a disposable dev project. It must never run in the Census Lab
    /// project (Y9 item 8). An unset gate reports a named skip.
    /// </para>
    /// <para>
    /// Network disclosure (Y9 item 7). The vendor lock calls the vendor
    /// usage counter once per editor session, unless the editor
    /// preferences already record the counts. The counter sends one user
    /// count and one project count to vendor web endpoints.
    /// </para>
    /// <para>
    /// Vendor binding. The suite resolves the real tool by
    /// assembly-qualified reflection, because the test assembly
    /// references no vendor assembly and the old test stand-in type is
    /// deleted. A guard proves the binding before any call. The suite
    /// has no version gate (Y9 item 9). It runs against whatever
    /// thryeditor is installed and never skips for a version reason.
    /// </para>
    /// <para>
    /// Attestation seam. The row flag of the two bundled rows is false
    /// until stage 7. The suite passes a test scoped attestation
    /// delegate to the swap-in. The delegate answers the production
    /// answer for every non-bundled row and for unrelated shaders, and
    /// adds true only for a locked material whose resolved original is
    /// one of the two bundled rows. One pinned assertion proves the
    /// fidelity of the seam.
    /// </para>
    /// <para>
    /// Limits (record section 8). Every case except the two
    /// assigned-texture cases uses default textures. The two
    /// assigned-texture cases carry one disposable generated texture
    /// with the spec values of LA8. The color space is the one of the
    /// dev project. The vendor version is the installed one. These
    /// limits are measurement conditions, not gates.
    /// </para>
    /// <para>
    /// Cleanup waits for vendor callbacks before and after it deletes
    /// owned assets, under one watchdog. It compares the final cache
    /// with the pre-lock snapshot only after the drains (LA9).
    /// </para>
    /// </summary>
    public sealed class BundledLockedOriginalRealLockTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_RealLock";
        private const string BuildOutputFolder = FixtureFolder + "/BuildOutput";

        private enum LockIsolationRefusal
        {
            None,
            CacheNotEmpty,
            MetadataMismatch,
            FixtureExists,
            UnrelatedDirtyAssets,
            BlockedOwnership,
        }

        private sealed class LockIsolationSnapshot
        {
            internal bool RootExists;
            internal bool RootIsFile;
            internal bool RootMetadataExists;
            internal byte[] RootMetadata;
            internal readonly Dictionary<string, string> Files =
                new Dictionary<string, string>(StringComparer.Ordinal);
            internal readonly HashSet<string> Directories =
                new HashSet<string>(StringComparer.Ordinal);
        }

        private static LockIsolationSnapshot activeOwnership;
        private static Type activeVendorTool;
        private static bool blockedOwnership;

        private static LockIsolationRefusal EvaluateLockIsolation(
            LockIsolationSnapshot snapshot, bool fixtureExists, bool unrelatedDirtyAssets)
        {
            if (blockedOwnership || activeOwnership != null)
                return LockIsolationRefusal.BlockedOwnership;
            if (fixtureExists)
                return LockIsolationRefusal.FixtureExists;
            if (snapshot.RootIsFile)
                return LockIsolationRefusal.CacheNotEmpty;
            if (snapshot.RootExists != snapshot.RootMetadataExists)
                return LockIsolationRefusal.MetadataMismatch;
            if (snapshot.Directories.Count != 0 ||
                (snapshot.Files.Count != 0 &&
                 !(snapshot.Files.Count == 2 &&
                   snapshot.Files.ContainsKey("README.txt") &&
                   snapshot.Files.ContainsKey("README.txt.meta"))))
                return LockIsolationRefusal.CacheNotEmpty;
            if (unrelatedDirtyAssets)
                return LockIsolationRefusal.UnrelatedDirtyAssets;
            return LockIsolationRefusal.None;
        }

        private static LockIsolationSnapshot CaptureLockIsolation()
        {
            var root = RealVendorLockTestSupport.CacheRoot;
            var snapshot = new LockIsolationSnapshot
            {
                RootExists = Directory.Exists(root),
                RootIsFile = File.Exists(root),
                RootMetadataExists = File.Exists(root + ".meta"),
            };
            if (snapshot.RootMetadataExists)
                snapshot.RootMetadata = File.ReadAllBytes(root + ".meta");
            if (!snapshot.RootExists)
                return snapshot;

            foreach (var path in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
                snapshot.Directories.Add(path.Substring(root.Length + 1).Replace('\\', '/'));
            using (var hash = SHA256.Create())
            {
                foreach (var path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    using (var stream = File.OpenRead(path))
                        snapshot.Files.Add(path.Substring(root.Length + 1).Replace('\\', '/'),
                            Convert.ToBase64String(hash.ComputeHash(stream)));
                }
            }
            return snapshot;
        }

        // The shape inputs of record section 4.1. A tag value of 1 keeps
        // the property live under its plain name. A tag value of 2
        // renames it with the cleaned material name as suffix.
        private const string BaseColorAnimatedTag = "_ColorAnimated";

        private const string EmissionColorAnimatedTag =
            "_EmissionColorAnimated";

        private const string LightVolumesProperty =
            "_LightingEnableLightVolumes";

        private const string AudioLinkProperty = "_EnableAudioLink";

        // The assigned-texture shape inputs of LA8. The names are the
        // spec names, and the transform and texel literals are the
        // installed-era substitutions the vendor output must carry.
        private const string MainTextureProperty = "_MainTex";

        private const string AlphaMaskProperty = "_AlphaMask";

        private const string MainPixelModeProperty = "_MainPixelMode";

        private const string MainAlphaMaskModeProperty =
            "_MainAlphaMaskMode";

        private const string AlphaForceOpaqueProperty =
            "_AlphaForceOpaque";

        private const string MainTextureStName = "_MainTex_ST";

        private const string AlphaMaskStName = "_AlphaMask_ST";

        private const string MainTextureTexelName = "_MainTex_TexelSize";

        private const string TransformLiteral = "float4(2,0.5,0.25,0.125)";

        private const string TexelLiteral = "float4(0.25,0.5,4,2)";

        // The gate disclosure. It names what the tests do, the disposable
        // dev project requirement, the vendor usage counter call, and the
        // Census Lab rule (Y9 items 7 and 8).
        private const string GateDisclosure =
            "Set " + VendorOptInGate.RealThryLock + "=1 in a disposable"
            + " dev project to run these tests. The tests lock real"
            + " materials with the installed vendor lock tool. The lock"
            + " writes vendor-owned cache data under " + RealVendorLockTestSupport.CacheRoot
            + ". The lock calls the vendor usage counter once per editor"
            + " session unless the editor preferences already record the"
            + " counts (spec Y9 item 7). The tests must never run in the"
            + " Census Lab project. They must never run in a project"
            + " whose state matters.";

        // The operator must keep other lock and build operations stopped.

        // The fidelity pin runs once per suite run, in the first test
        // that reaches the seam.
        private static bool fidelityPinned;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            fidelityPinned = false;
        }

        [Test]
        public void ExistingCacheEntryRefusesLockIsolation()
        {
            var snapshot = BaselineSnapshot();
            snapshot.Files.Add("other.shader", "digest");
            Assert.That(EvaluateLockIsolation(snapshot, false, false),
                Is.EqualTo(LockIsolationRefusal.CacheNotEmpty));
        }

        [Test]
        public void OnlyReadmePairAllowsLockIsolation()
        {
            Assert.That(EvaluateLockIsolation(BaselineSnapshot(), false, false),
                Is.EqualTo(LockIsolationRefusal.None));
        }

        [Test]
        public void AbsentCacheWithoutMetadataAllowsLockIsolation()
        {
            Assert.That(EvaluateLockIsolation(new LockIsolationSnapshot(), false, false),
                Is.EqualTo(LockIsolationRefusal.None));
        }

        [Test]
        public void CacheDirectoryRefusesLockIsolation()
        {
            var snapshot = BaselineSnapshot();
            snapshot.Directories.Add("nested");
            Assert.That(EvaluateLockIsolation(snapshot, false, false),
                Is.EqualTo(LockIsolationRefusal.CacheNotEmpty));
        }

        [Test]
        public void CaseChangedReadmeIsNotTheOwnedBaseline()
        {
            var snapshot = BaselineSnapshot();
            snapshot.Files.Remove("README.txt");
            snapshot.Files.Add("Readme.txt", "digest");
            Assert.That(EvaluateLockIsolation(snapshot, false, false),
                Is.EqualTo(LockIsolationRefusal.CacheNotEmpty));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void UnpairedCacheRootRefusesLockIsolation(bool rootExists, bool metadataExists)
        {
            var snapshot = new LockIsolationSnapshot
            {
                RootExists = rootExists,
                RootMetadataExists = metadataExists,
            };
            Assert.That(EvaluateLockIsolation(snapshot, false, false),
                Is.EqualTo(LockIsolationRefusal.MetadataMismatch));
        }

        [Test]
        public void ExistingFixtureOrOrphanMetadataRefusesLockIsolation()
        {
            Assert.That(EvaluateLockIsolation(BaselineSnapshot(), true, false),
                Is.EqualTo(LockIsolationRefusal.FixtureExists));
        }

        [Test]
        public void UnrelatedDirtyAssetRefusesLockIsolation()
        {
            Assert.That(EvaluateLockIsolation(BaselineSnapshot(), false, true),
                Is.EqualTo(LockIsolationRefusal.UnrelatedDirtyAssets));
        }

        [Test]
        public void BlockedOwnershipRemainsBlockedForLaterCases()
        {
            var previous = blockedOwnership;
            blockedOwnership = true;
            try
            {
                Assert.That(EvaluateLockIsolation(BaselineSnapshot(), false, false),
                    Is.EqualTo(LockIsolationRefusal.BlockedOwnership));
                Assert.That(EvaluateLockIsolation(new LockIsolationSnapshot(), false, false),
                    Is.EqualTo(LockIsolationRefusal.BlockedOwnership));
                Assert.That(blockedOwnership, Is.True);
            }
            finally
            {
                blockedOwnership = previous;
            }
        }

        // The swap-in window activation seam. The gated harness and the
        // dependency test share one activate path and one deactivate
        // path.
        // NDMF resolves dependency contexts only through the recursive
        // activation. The animator services context declares the
        // virtual controller context as a dependency, so the plain
        // call fails and the recursive call is correct.
        private static void ActivateSwapInAnimatorWindow(BuildContext context)
        {
            context.ActivateExtensionContextRecursive
                <AnimatorServicesContext>();
        }

        // The deactivation order is the reverse of the activation
        // order. Deactivation of an inactive context is a safe no-op.
        private static void DeactivateSwapInAnimatorWindow(BuildContext context)
        {
            context.DeactivateExtensionContext<AnimatorServicesContext>();
            context.DeactivateExtensionContext<VirtualControllerContext>();
        }

        // The probe mirrors the non-perturbing lifecycle check of the
        // reactivation characterization suite. Extension<T> reads only
        // the active set and throws when the context is absent. The
        // catch observes without a state change.
        private static bool ProbeExtensionActive<T>(BuildContext context)
            where T : IExtensionContext
        {
            try
            {
                context.Extension<T>();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The dependency regression. The swap-in window needs the
        // animator services context. That context needs the virtual
        // controller context. Plain activation does not resolve
        // dependency contexts. Recursive activation brings both up.
        // The paired deactivation takes both down.
        [Test]
        public void SwapInWindowActivationActivatesTheVirtualControllerDependency()
        {
            var root = new GameObject(
                "AMUSE swap-in activation dependency probe root");
            try
            {
                root.AddComponent<SkinnedMeshRenderer>();
                using var assets = new OverrideTemporaryDirectoryScope(null);
                var context = new BuildContext(root, null);
                try
                {
                    ActivateSwapInAnimatorWindow(context);
                    Assert.That(
                        ProbeExtensionActive<VirtualControllerContext>(context),
                        Is.True,
                        "the virtual controller context stayed inactive after"
                        + " the swap-in window activation");
                    Assert.That(
                        context.Extension<AnimatorServicesContext>()
                            .ControllerContext,
                        Is.Not.Null);
                }
                finally
                {
                    DeactivateSwapInAnimatorWindow(context);
                }
                Assert.That(
                    ProbeExtensionActive<AnimatorServicesContext>(context),
                    Is.False,
                    "the animator services context stayed active after the"
                    + " swap-in window deactivation");
                Assert.That(
                    ProbeExtensionActive<VirtualControllerContext>(context),
                    Is.False,
                    "the virtual controller context stayed active after the"
                    + " swap-in window deactivation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // The recovery drain regression: an owned import queues a real vendor recovery pass that cleanup must not race.
        [Test]
        public void OwnedMaterialImportQueuesRecoveryWorkThePendingCheckMustSee()
        {
            VendorOptInGate.Require(
                VendorOptInGate.RealThryLock, GateDisclosure);
            var vendorTool = RealVendorLockTestSupport.VendorToolOrNull();
            if (vendorTool == null)
            {
                Assert.Ignore(
                    "the thryeditor package is not installed in this"
                    + " project. The suite cannot resolve the vendor"
                    + " recovery callback. Install thryeditor to run"
                    + " this test");
            }

            var snapshot = CaptureLockIsolation();
            var fixtureExists = Directory.Exists(FixtureFolder) ||
                File.Exists(FixtureFolder) ||
                File.Exists(FixtureFolder + ".meta");
            var dirty = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Any(IsBlockingDirtyAsset);
            for (var index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                dirty |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty;
            Assert.That(EvaluateLockIsolation(snapshot, fixtureExists, dirty),
                Is.EqualTo(LockIsolationRefusal.None), "Real lock isolation preflight refused: "
                + EvaluateLockIsolation(snapshot, fixtureExists, dirty)
                + ", fixtureExists=" + fixtureExists + ", dirty=" + dirty + ", " + DescribeDirtyAssets() + ".");
            // The positive guard must belong to this import, not to work queued earlier in the session.
            Assert.That(RecoveryDelayCallQueued(vendorTool), Is.False,
                "A vendor recovery callback is already queued.");
            Assert.That(BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode,
                Is.False, "Another editor operation is active.");
            Assert.That(EffectiveTeardownTimeout(), Is.GreaterThan(10000),
                "The teardown timeout must exceed the drain watchdog.");

            activeOwnership = snapshot;
            activeVendorTool = vendorTool;
            RealVendorLockTestSupport.EnsureFolder("Assets", "AmuseTests_RealLock");

            var shader = Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null,
                "The built-in standard shader must resolve for the probe.");
            var materialPath = FixtureFolder + "/RealLockRecoveryProbe.mat";
            var material = new Material(shader)
            {
                name = "RealLockRecoveryProbe",
            };
            try
            {
                AssetDatabase.CreateAsset(material, materialPath);
                // The vendor recovery hook runs after an import batch, so the probe completes the batch with a refresh.
                AssetDatabase.Refresh();
            }
            catch
            {
                if (!EditorUtility.IsPersistent(material))
                    UnityEngine.Object.DestroyImmediate(material);
                throw;
            }

            Assert.That(
                AssetDatabase.GetAssetPath(material), Is.EqualTo(materialPath),
                "The probe material must sit on the owned fixture path.");
            Assert.That(RecoveryDelayCallQueued(vendorTool), Is.True,
                "The owned material import must queue the real vendor"
                + " recovery pass on the delayed editor call queue.");
            Assert.That(HasPendingVendorWork(vendorTool), Is.True,
                "The pending-work check must see the queued vendor"
                + " recovery pass, so cleanup cannot race it.");
        }

        // The preflight guard, exercised for real. A healthy set passes the actual guard, and a transient
        // locked orphan whose original resolves trips the actual refusal. The vendor Partition runs for
        // real, so no vendor logic is copied, and the guard must leave both transients unchanged.
        [Test]
        public void RecoveryPreflightRefusesLockedOrphanAndAllowsHealthyMaterial()
        {
            VendorOptInGate.Require(
                VendorOptInGate.RealThryLock, GateDisclosure);
            var vendorTool = RealVendorLockTestSupport.VendorToolOrNull();
            if (vendorTool == null)
            {
                Assert.Ignore(
                    "the thryeditor package is not installed in this"
                    + " project. The suite cannot resolve the vendor"
                    + " recovery callback. Install thryeditor to run"
                    + " this test");
            }

            var standardShader = Shader.Find("Standard");
            Assert.That(standardShader, Is.Not.Null,
                "The built-in standard shader must resolve for the control.");
            var errorShader = Shader.Find("Hidden/InternalErrorShader");
            Assert.That(errorShader, Is.Not.Null,
                "The built-in error shader must resolve for the control.");
            var healthy = new Material(standardShader)
            {
                name = "RealLockRecoveryPreflightHealthy",
            };
            var orphan = new Material(errorShader)
            {
                name = "RealLockRecoveryPreflightOrphan",
            };
            try
            {
                orphan.SetOverrideTag(
                    LockedMaterialIdentity.OriginalShaderTagName,
                    PoiyomiSourceAttestation.PoiyomiToonShaderName);
                orphan.SetOverrideTag(
                    LockedMaterialIdentity.OriginalShaderGuidTagName,
                    PoiyomiSourceAttestation.CanonicalShaderGuid);

                // The healthy set passes the actual guard.
                AssertNoRecoverableRecoveryTarget(
                    vendorTool, new[] { healthy });
                // The orphan trips the actual guard refusal.
                Assert.Throws<AssertionException>(() =>
                    AssertNoRecoverableRecoveryTarget(
                        vendorTool, new[] { orphan }));
                // The guard is read-only over its inputs.
                Assert.That(healthy.shader, Is.EqualTo(standardShader),
                    "The guard changed the healthy control.");
                Assert.That(orphan.shader, Is.EqualTo(errorShader),
                    "The guard changed the orphan control.");
                Assert.That(orphan.GetTag(
                        LockedMaterialIdentity.OriginalShaderTagName, false, null),
                    Is.EqualTo(PoiyomiSourceAttestation.PoiyomiToonShaderName),
                    "The guard changed the orphan control tags.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(healthy);
                UnityEngine.Object.DestroyImmediate(orphan);
            }
        }

        private static LockIsolationSnapshot BaselineSnapshot()
        {
            var snapshot = new LockIsolationSnapshot
            {
                RootExists = true,
                RootMetadataExists = true,
            };
            snapshot.Files.Add("README.txt", "digest");
            snapshot.Files.Add("README.txt.meta", "digest");
            return snapshot;
        }

        // Toon row, record case a.
        [Test]
        public void RealToonAllDefaultsAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonDefaults",
            });
        }

        // Toon row. One renamed property. The base color tag stays live
        // and the emission color tag renames, the shape of record case c.
        [Test]
        public void RealToonOneRenamedPropertyAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonOneRenamed",
                BaseColorKeptLive = true,
                EmissionColorRenamed = true,
            });
        }

        // Toon row. Two renamed properties.
        [Test]
        public void RealToonTwoRenamedPropertiesAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonTwoRenamed",
                BaseColorRenamed = true,
                EmissionColorRenamed = true,
            });
        }

        // Toon row. The material name carries spaces and symbols. The
        // vendor cleans that name into the rename suffix. The shape of
        // record case b.
        [Test]
        public void RealToonSymbolMaterialNameAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "Real Lock Acceptance !?* Toon",
                FileStem = "RealLockToonSymbolName",
                BaseColorKeptLive = true,
                EmissionColorRenamed = true,
            });
        }

        // Toon row. Light volumes on. The value is the declared default.
        [Test]
        public void RealToonLightVolumesOnAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonLightVolumesOn",
                LightVolumesSet = true,
                LightVolumesValue = 1f,
            });
        }

        // Toon row. Light volumes off.
        [Test]
        public void RealToonLightVolumesOffAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonLightVolumesOff",
                LightVolumesSet = true,
                LightVolumesValue = 0f,
            });
        }

        // Toon row. AudioLink enabled. The generated text keeps
        // digit-led literals, so the case exercises Y5 on real text.
        [Test]
        public void RealToonAudioLinkEnabledAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonAudioLink",
                AudioLinkEnabled = true,
            });
        }

        // Two Pass row, record case f.
        [Test]
        public void RealTwoPassAllDefaultsAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Two Pass",
                ShaderGuid =
                    PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceTwoPass",
                FileStem = "RealLockTwoPassDefaults",
            });
        }

        // Two Pass row. One renamed property. The shape of record case g.
        [Test]
        public void RealTwoPassOneRenamedPropertyAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Two Pass",
                ShaderGuid =
                    PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceTwoPass",
                FileStem = "RealLockTwoPassOneRenamed",
                BaseColorKeptLive = true,
                EmissionColorRenamed = true,
            });
        }

        // Two Pass row. AudioLink enabled.
        [Test]
        public void RealTwoPassAudioLinkEnabledAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Two Pass",
                ShaderGuid =
                    PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceTwoPass",
                FileStem = "RealLockTwoPassAudioLink",
                AudioLinkEnabled = true,
            });
        }

        // Toon row. One assigned texture drives the main texture and the
        // alpha mask with the exact LA8 scale, offset, and mode values.
        [Test]
        public void RealToonAssignedTextureAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Toon",
                ShaderGuid = PoiyomiSourceAttestation.CanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceToon",
                FileStem = "RealLockToonAssignedTexture",
                AssignedTexture = true,
            });
        }

        // Two Pass row. One assigned texture drives the main texture and
        // the alpha mask with the exact LA8 scale, offset, and mode
        // values.
        [Test]
        public void RealTwoPassAssignedTextureAlignsAndRestores()
        {
            RunRealLockCase(new LockCase
            {
                RowLabel = "Two Pass",
                ShaderGuid =
                    PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                MaterialName = "RealLockAcceptanceTwoPass",
                FileStem = "RealLockTwoPassAssignedTexture",
                AssignedTexture = true,
            });
        }

        private sealed class LockCase
        {
            internal string RowLabel;

            internal string ShaderGuid;

            internal string MaterialName;

            internal string FileStem;

            internal bool BaseColorKeptLive;

            internal bool BaseColorRenamed;

            internal bool EmissionColorRenamed;

            internal bool LightVolumesSet;

            internal float LightVolumesValue;

            internal bool AudioLinkEnabled;

            // The LA8 shape. True stages one disposable linear 4-by-2
            // RGBA texture on the main texture and the alpha mask with
            // the exact scale, offset, and mode values.
            internal bool AssignedTexture;
        }

        // The one case runner behind all twelve tests. It carries the
        // full acceptance order of Y9 item 5, the per-phase times of Y10
        // item 2, and the cleanup order of Y9 item 6.
        private static void RunRealLockCase(LockCase lockCase)
        {
            VendorOptInGate.Require(
                VendorOptInGate.RealThryLock, GateDisclosure);

            ResolveRealTool(
                out var lockMethod, out var unlockMethod,
                out var progressBarNone);
            var shader = LoadBundledShader(
                lockCase.ShaderGuid, lockCase.RowLabel);
            var runDate = DateTime.UtcNow.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture);

            var vendorTool = RealVendorLockTestSupport.VendorToolOrNull();
            var snapshot = CaptureLockIsolation();
            var fixtureExists = Directory.Exists(FixtureFolder) ||
                File.Exists(FixtureFolder) || File.Exists(FixtureFolder + ".meta");
            var dirty = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Any(IsBlockingDirtyAsset);
            for (var index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                dirty |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty;
            Assert.That(EvaluateLockIsolation(snapshot, fixtureExists, dirty),
                Is.EqualTo(LockIsolationRefusal.None), "Real lock isolation preflight refused: "
                + EvaluateLockIsolation(snapshot, fixtureExists, dirty)
                + ", fixtureExists=" + fixtureExists + ", dirty=" + dirty + ", " + DescribeDirtyAssets() + ".");
            Assert.That(BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling ||
                EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode,
                Is.False, "Another editor operation is active.");
            Assert.That(EffectiveTeardownTimeout(), Is.GreaterThan(10000),
                "The teardown timeout must exceed the drain watchdog.");
            Assert.That(HasPendingVendorWork(vendorTool), Is.False,
                "Vendor work is already pending.");
            // The exact RecoverAll material set.
            AssertNoRecoverableRecoveryTarget(vendorTool,
                AssetDatabase.FindAssets("t:Material")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => !string.IsNullOrEmpty(path))
                    .Select(AssetDatabase.LoadAssetAtPath<Material>)
                    .Where(material => material != null));

            activeOwnership = snapshot;
            activeVendorTool = vendorTool;
            RealVendorLockTestSupport.EnsureFolder("Assets", "AmuseTests_RealLock");
            var materialPath =
                FixtureFolder + "/" + lockCase.FileStem + ".mat";
            var material = new Material(shader)
            {
                name = lockCase.MaterialName,
            };
            Texture2D assignedTexture = null;
            try
            {
                ApplyShape(material, lockCase);
                if (lockCase.AssignedTexture)
                {
                    // The LA8 fixture. Only the created texture persists,
                    // under the owned fixture folder, and no import
                    // setting of an existing texture changes.
                    assignedTexture = CreateAssignedInputTexture(
                        lockCase.FileStem + "_Input");
                    AssignTextureFixture(material, assignedTexture);
                }
                AssetDatabase.CreateAsset(material, materialPath);
            }
            catch
            {
                if (!EditorUtility.IsPersistent(material))
                    UnityEngine.Object.DestroyImmediate(material);
                throw;
            }

            Material timingClone = null;

            // The measured phases. A phase that never ran stays below
            // zero, so the report line names the reach of every run.
            var lockMs = -1.0;
            var alignMs = -1.0;
            var restoreMs = -1.0;
            var consistencyMs = -1.0;
            var coldAlignMs = -1.0;
            var generatedBytes = -1L;

            try
            {
                // The real lock. The reflection call passes both
                // arguments, so the invocation reaches the vendor
                // overload the build path uses.
                var lockWatch = Stopwatch.StartNew();
                var locked = (bool)lockMethod.Invoke(null, new object[]
                {
                    new List<Material> { material },
                    progressBarNone,
                });
                lockWatch.Stop();
                lockMs = lockWatch.Elapsed.TotalMilliseconds;
                Assert.That(
                    locked, Is.True,
                    RealVendorLockTestSupport.Dated("the vendor lock call returned false",
                        runDate, null));

                // The locked identity. The vendor lock writes the
                // locked name prefix, the flag, and the identity tags,
                // so the production classifier recognizes the material.
                Assert.That(
                    LockedMaterialIdentity.RecognizedLockedIdentity(
                        material),
                    Is.True,
                    RealVendorLockTestSupport.Dated("the locked identity of the real lock is not"
                        + " recognized", runDate, null));
                Assert.That(
                    material.shader.name.StartsWith(
                        LockedMaterialIdentity.LockedShaderNamePrefix,
                        StringComparison.Ordinal),
                    Is.True,
                    RealVendorLockTestSupport.Dated("the locked shader name does not carry the"
                        + " vendor locked prefix, got "
                        + material.shader.name, runDate, null));

                PinFidelityOnce(shader, runDate);

                var generatedPath =
                    AssetDatabase.GetAssetPath(material.shader);
                Assert.That(
                    generatedPath.StartsWith(
                        RealVendorLockTestSupport.CacheRoot + "/", StringComparison.Ordinal),
                    Is.True,
                    RealVendorLockTestSupport.Dated("the locked shader asset does not live under"
                        + " the vendor cache root, got " + generatedPath,
                        runDate, null));

                var generatedText = RealVendorLockTestSupport.ReadText(generatedPath);
                generatedBytes = new FileInfo(generatedPath).Length;
                var originalText = RealVendorLockTestSupport.ReadText(
                    AssetDatabase.GetAssetPath(shader));
                var properties = LockedSourceAlignmentMemo.PropertiesOf(
                    shader, originalText);

                // The LA8 pre-alignment guard. The fixture must reach
                // the intended vendor outputs before alignment, and the
                // observations stand even when alignment later refuses.
                if (lockCase.AssignedTexture)
                {
                    AssertAssignedTextureVendorOutputs(
                        generatedText, originalText, runDate);
                }

                // Phase one. The align time of a full successful walk,
                // on a cleared memo (A8.4). The memo entry this call
                // stores serves the swap-in and the reconstruction below
                // without a second walk.
                LockedSourceAlignmentMemo.Clear();
                var alignWatch = Stopwatch.StartNew();
                var aligned = LockedSourceAlignmentMemo.GetOrAlign(
                    material.shader, shader, properties);
                alignWatch.Stop();
                alignMs = alignWatch.Elapsed.TotalMilliseconds;
                Assert.That(
                    aligned.Succeeded, Is.True,
                    RealVendorLockTestSupport.Dated("the real lock output refused the production"
                        + " aligner at original line "
                        + aligned.OriginalLine + " with text "
                        + RealVendorLockTestSupport.OriginalLineText(
                            originalText, aligned.OriginalLine),
                        runDate, aligned.RefusalDetail));
                Assert.That(
                    aligned.Source, Is.Not.Null,
                    RealVendorLockTestSupport.Dated("the real alignment carries no source",
                        runDate, null));

                // The LA8 success guard. The baked facts, the plan
                // transforms, and the texture identities of the
                // assigned-texture shape.
                if (lockCase.AssignedTexture)
                {
                    AssertAssignedTextureFacts(
                        aligned.Source, material, properties,
                        assignedTexture, runDate);
                }

                // The swap-in harness. Direct call on a public
                // constructor build context, with the animator extension
                // active, consent granted, and the production restore
                // delegate (spec Validation 4).
                var root = new GameObject(
                    "AMUSE real lock acceptance root");

                try
                {
                    var renderer = root.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMaterials = new[] { material };
                    RealVendorLockTestSupport.EnsureFolder(FixtureFolder, "BuildOutput");
                    using var assets = new OverrideTemporaryDirectoryScope(BuildOutputFolder);
                    var context = new BuildContext(root, BuildOutputFolder);
                    // No guard around the teardown below. A partial
                    // activation can leave only the dependency context
                    // active. Deactivating an inactive context is a safe
                    // no-op in NDMF.
                    try
                    {
                        ActivateSwapInAnimatorWindow(context);
                        var window =
                            context.GetState<TransientUnlockWindowState>();
                        window.ConsentGranted = true;
                        var finishState =
                            context.GetState<AmusePlatformFinishState>();

                        var summary = TransientUnlockSwapIn.SwapIn(
                            context,
                            window,
                            TransientUnlockAvailability
                                .CreateProductionRestore(),
                            TestScopedAttestation);

                        Assert.That(
                            summary.ConsentGatePassed, Is.True,
                            RealVendorLockTestSupport.Dated("the swap-in consent gate did not"
                                + " pass", runDate, null));
                        Assert.That(
                            summary.RestoreMismatchSlots,
                            Is.EqualTo(0),
                            RealVendorLockTestSupport.Dated("the swap-in restored the clone with"
                                + " a mismatch, so the seventh fact or"
                                + " the consistency check failed",
                                runDate, null));
                        Assert.That(
                            summary.ClonesCreated, Is.EqualTo(1),
                            RealVendorLockTestSupport.Dated("the swap-in created no clone for the"
                                + " locked material", runDate, null));
                        Assert.That(
                            summary.SlotsSwapped, Is.EqualTo(1),
                            RealVendorLockTestSupport.Dated("the swap-in swapped no slot",
                                runDate, null));
                        AssertNoSlotRefusal(finishState, runDate);

                        var swapped = renderer.sharedMaterials[0];
                        Assert.That(
                            swapped, Is.Not.EqualTo(material),
                            RealVendorLockTestSupport.Dated("the slot still holds the locked"
                                + " material after the swap-in",
                                runDate, null));
                        Assert.That(
                            swapped.shader, Is.EqualTo(shader),
                            RealVendorLockTestSupport.Dated("the swapped clone does not sit on"
                                + " the bundled original shader",
                                runDate, null));
                        Assert.That(
                            swapped.GetFloat(
                                LockedMaterialIdentity
                                    .OptimizerEnabledPropertyName),
                            Is.EqualTo(0f),
                            RealVendorLockTestSupport.Dated("the swapped clone carries the lock"
                                + " flag", runDate, null));
                    }
                    finally
                    {
                        DeactivateSwapInAnimatorWindow(context);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                // Phase two. The clone and restore time around the
                // standalone reconstruction on a fresh clone. The
                // stopwatch starts before the clone, so the phase covers
                // the clone and the restore (Y10 item 2).
                var restoreWatch = Stopwatch.StartNew();
                timingClone = UnityEngine.Object.Instantiate(material);
                timingClone.name = material.name;
                var restored = LockedMaterialReconstruction.TryApply(
                    timingClone, material, _ => true);
                restoreWatch.Stop();
                restoreMs = restoreWatch.Elapsed.TotalMilliseconds;
                Assert.That(
                    restored.Outcome,
                    Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded),
                    RealVendorLockTestSupport.Dated("the standalone reconstruction refused the"
                        + " clone", runDate, restored.Detail));

                // The consistency call on the finished clone (A5.9).
                var consistencyWatch = Stopwatch.StartNew();
                var consistency = LockedSourceReproduction
                    .CheckConsistency(
                        aligned.Source,
                        properties,
                        new UnityLockedMaterialValues(timingClone));
                consistencyWatch.Stop();
                consistencyMs =
                    consistencyWatch.Elapsed.TotalMilliseconds;
                Assert.That(
                    consistency, Is.Null,
                    RealVendorLockTestSupport.Dated("the consistency check refused the restored"
                        + " clone", runDate, consistency));

                // Phase three. The cold alignment time. One fresh
                // alignment call on a cleared memo. This is the shared
                // cold alignment cost that every consumer pays once per
                // distinct generated shader, file reads and digests
                // included. It is not the full dry-run time: the real
                // dry-run preview runs in phase four.
                LockedSourceAlignmentMemo.Clear();
                var coldAlignWatch = Stopwatch.StartNew();
                var coldAligned = LockedSourceAlignmentMemo.GetOrAlign(
                    material.shader, shader, properties);
                coldAlignWatch.Stop();
                coldAlignMs = coldAlignWatch.Elapsed.TotalMilliseconds;
                Assert.That(
                    coldAligned.Succeeded, Is.True,
                    RealVendorLockTestSupport.Dated("the cold align refused the same pair",
                        runDate, coldAligned.RefusalDetail));

                // Phase four. The gated integration proof while the
                // locked material is alive. The normal dry-run preview
                // runs first, on an owned disposable avatar with
                // production defaults and both admission flags false.
                // The mirror proof follows it.
                RunGatedPreviewProofs(material, runDate);
            }
            finally
            {
                ReportPhaseTimes(
                    lockCase, runDate, generatedBytes, lockMs, alignMs,
                    restoreMs, consistencyMs, coldAlignMs);
                if (timingClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(timingClone);
                }

                LockedSourceAlignmentMemo.Clear();
            }
        }

        // The attestation seam. Since the stage 7 flag change of
        // 2026-10-30 the bundled rows admit through production, so the
        // seam is the production check itself. The earlier override
        // that simulated admission for the bundled rows is gone. A
        // production regression can no longer hide behind the seam.
        internal static bool TestScopedAttestation(Material material)
        {
            return LockedMaterialIdentity.OriginalShaderAttested(material);
        }

        // The fidelity pin. One assertion per suite run. Since the
        // stage 7 flag change of 2026-10-30 the seam is the production
        // check, so this pin guards against a future seam that answers
        // more than production. A material whose recorded original
        // names the 9.3.64 release row resolves on this install to the
        // bundled file, so the production name rule answers false for
        // it.
        private static void PinFidelityOnce(
            Shader bundledShader, string runDate)
        {
            if (fidelityPinned)
            {
                return;
            }

            fidelityPinned = true;
            var probe = new Material(bundledShader)
            {
                name = "RealLockAcceptanceFidelityProbe",
            };
            try
            {
                probe.SetOverrideTag(
                    LockedMaterialIdentity.OriginalShaderTagName,
                    PoiyomiSourceAttestation.PoiyomiToonShaderName);
                probe.SetOverrideTag(
                    LockedMaterialIdentity.OriginalShaderGuidTagName,
                    PoiyomiSourceAttestation.CanonicalShaderGuid);

                Assert.That(
                    TestScopedAttestation(probe),
                    Is.EqualTo(
                        LockedMaterialIdentity.OriginalShaderAttested(
                            probe)),
                    RealVendorLockTestSupport.Dated("the attestation seam must answer exactly"
                        + " what the production check answers for a"
                        + " material whose recorded original names the"
                        + " 9.3.64 release row", runDate, null));
                Assert.That(
                    TestScopedAttestation(probe), Is.False,
                    RealVendorLockTestSupport.Dated("the attestation seam must refuse the release"
                        + " recorded name on this install, because the"
                        + " production name rule refuses it",
                        runDate, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        // The vendor binding rule. The suite skips with a named reason
        // when the vendor is absent, and the shared guard proves the
        // binding before any call.
        private static void ResolveRealTool(
            out MethodInfo lockMethod,
            out MethodInfo unlockMethod,
            out object progressBarNone)
        {
            var tool = RealVendorLockTestSupport.VendorToolOrNull();
            if (tool == null)
            {
                Assert.Ignore(
                    "the thryeditor package is not installed in this"
                    + " project. The suite cannot resolve the real lock"
                    + " tool. Install thryeditor to run these tests");
            }

            RealVendorLockTestSupport.ResolveRealTool(
                tool, out lockMethod, out unlockMethod,
                out progressBarNone);
        }

        // The pinned row load. The shader resolves from the row GUID of
        // PoiyomiSourceAttestation.AdmittedSources(), never by name
        // lookup (Y9 item 3). A name lookup would answer a 10.x install
        // with the 10.0 shader.
        private static Shader LoadBundledShader(
            string shaderGuid, string rowLabel)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(shaderGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                Assert.Ignore(
                    "the bundled 9.3 shader asset of the " + rowLabel
                    + " row does not resolve from its pinned row GUID."
                    + " Install the Poiyomi package to run these tests");
                return null;
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader == null)
            {
                Assert.Ignore(
                    "the bundled 9.3 shader asset of the " + rowLabel
                    + " row does not load at its resolved path. Install"
                    + " the Poiyomi package to run these tests");
                return null;
            }

            return shader;
        }

        private static void ApplyShape(
            Material material, LockCase lockCase)
        {
            if (lockCase.BaseColorKeptLive)
            {
                material.SetOverrideTag(BaseColorAnimatedTag, "1");
            }

            if (lockCase.BaseColorRenamed)
            {
                material.SetOverrideTag(BaseColorAnimatedTag, "2");
            }

            if (lockCase.EmissionColorRenamed)
            {
                material.SetOverrideTag(EmissionColorAnimatedTag, "2");
            }

            if (lockCase.LightVolumesSet)
            {
                material.SetFloat(
                    LightVolumesProperty, lockCase.LightVolumesValue);
            }

            if (lockCase.AudioLinkEnabled)
            {
                material.SetFloat(AudioLinkProperty, 1f);
            }
        }

        private static void AssertNoSlotRefusal(
            AmusePlatformFinishState finishState, string runDate)
        {
            foreach (var reason in Enum.GetValues(
                typeof(AlphaSeparationSlotRefusal)))
            {
                var slotRefusal =
                    (AlphaSeparationSlotRefusal)reason;
                if (slotRefusal == AlphaSeparationSlotRefusal.None)
                {
                    continue;
                }

                Assert.That(
                    finishState.SlotRefusalCount(slotRefusal),
                    Is.EqualTo(0),
                    RealVendorLockTestSupport.Dated("the slot refusal " + slotRefusal
                        + " fired, and this case expects no refusal",
                        runDate, null));
            }
        }

        private static bool BlocksDirtyAsset(bool persistent, bool dirty, bool native, Type type)
        {
            // The owner approved this narrow waiver for the dev editor only.
            return persistent && dirty && native && type != typeof(PlayerSettings);
        }

        private static bool IsBlockingDirtyAsset(UnityEngine.Object asset)
        {
            return asset != null && BlocksDirtyAsset(
                EditorUtility.IsPersistent(asset), EditorUtility.IsDirty(asset),
                AssetDatabase.IsNativeAsset(asset), asset.GetType());
        }

        // Names the types of the blocking dirty assets and the dirty scenes, so a refused
        // preflight on a runner says which asset type held the guard.
        private static string DescribeDirtyAssets()
        {
            var types = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(IsBlockingDirtyAsset)
                .Select(asset => asset.GetType().Name + "@" + AssetDatabase.GetAssetPath(asset))
                .Distinct().Take(8);
            var scenes = 0;
            for (var index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty) scenes++;
            return "dirtyAssets=[" + string.Join(", ", types) + "], dirtyScenes=" + scenes;
        }

        private static int EffectiveTeardownTimeout()
        {
            var type = typeof(UnityTestAttribute).Assembly.GetType(
                "UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext", true);
            var context = type.GetProperty("CurrentContext", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (context == null)
                throw new InvalidOperationException("The test timeout cannot be observed.");
            var property = type.GetProperty("TestCaseTimeout", BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                throw new InvalidOperationException("The test timeout cannot be observed.");
            var timeout = (int)property.GetValue(context);
            return timeout == 0 ? 180000 : timeout;
        }

        // The vendor recovery postprocessor binding. It refuses to answer when the installed vendor recovery cannot be observed.
        private static Type VendorRecoveryPostprocessor(Type vendorTool)
        {
            if (vendorTool == null)
                throw new InvalidOperationException("The vendor tool cannot be observed.");
            var recoveryType = vendorTool.Assembly.GetType(
                "Thry.ThryEditor.LockedShaderRecovery", false);
            var postprocessorType = recoveryType == null
                ? null
                : recoveryType.GetNestedType("Postprocessor", BindingFlags.NonPublic);
            if (postprocessorType == null)
                throw new InvalidOperationException("The vendor recovery interface cannot be observed.");
            return postprocessorType;
        }

        // Guarded read of the delayed call subscriptions. Never invokes, unregisters, or clears vendor work.
        private static bool RecoveryDelayCallQueued(Type vendorTool)
        {
            var postprocessorType = VendorRecoveryPostprocessor(vendorTool);
            var runMethod = postprocessorType.GetMethod(
                "Run", BindingFlags.Static | BindingFlags.NonPublic);
            if (runMethod == null)
                throw new InvalidOperationException("The vendor recovery callback cannot be observed.");
            var field = typeof(EditorApplication).GetField("delayCall",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType))
                throw new InvalidOperationException("The editor delayed call subscriptions cannot be observed.");
            var callbacks = (Delegate)field.GetValue(null);
            return callbacks != null && callbacks.GetInvocationList().Any(callback =>
                callback.Method.DeclaringType == postprocessorType &&
                callback.Method.Name == "Run");
        }

        // The queued and full scan flags of the real recovery postprocessor, read only.
        private static bool RecoveryFlagsRaised(Type postprocessorType)
        {
            var queuedField = postprocessorType.GetField("s_queued",
                BindingFlags.Static | BindingFlags.NonPublic);
            var fullScanField = postprocessorType.GetField("s_needsFullScan",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (queuedField == null || fullScanField == null ||
                queuedField.FieldType != typeof(bool) ||
                fullScanField.FieldType != typeof(bool))
                throw new InvalidOperationException("The vendor recovery state cannot be observed.");
            return (bool)queuedField.GetValue(null) ||
                (bool)fullScanField.GetValue(null);
        }

        // Any real vendor recovery work: the delayed call subscription, or a raised recovery flag.
        private static bool RecoveryWorkPending(Type vendorTool)
        {
            var postprocessorType = VendorRecoveryPostprocessor(vendorTool);
            return RecoveryFlagsRaised(postprocessorType) ||
                RecoveryDelayCallQueued(vendorTool);
        }

        // The recovery preflight guard. It runs the real vendor Partition over the given set and refuses
        // a recoverable target, because a later recovery pass could restore it outside this case.
        // Read-only.
        private static void AssertNoRecoverableRecoveryTarget(
            Type vendorTool, IEnumerable<Material> materials)
        {
            var recoveryType = vendorTool.Assembly.GetType(
                "Thry.ThryEditor.LockedShaderRecovery", false);
            if (recoveryType == null)
                throw new InvalidOperationException("The vendor recovery interface cannot be observed.");
            var partitionMethod = recoveryType.GetMethod("Partition",
                BindingFlags.Public | BindingFlags.Static);
            if (partitionMethod == null)
                throw new InvalidOperationException("The vendor recovery partition cannot be observed.");
            var arguments = new object[] { materials, null, null };
            partitionMethod.Invoke(null, arguments);
            // A boolean emptiness answer, so a refusal never prints an exact material count.
            Assert.That(((List<Material>)arguments[1]).Count == 0, Is.True,
                "the recovery preflight found recoverable locked materials, so a later recovery"
                + " pass could restore an unrelated target outside this case. Resolve them"
                + " before the lock");
        }

        private static bool HasPendingVendorWork(Type vendorTool)
        {
            if (vendorTool == null)
                throw new InvalidOperationException("The vendor tool cannot be observed.");
            var names = new[] { "VerifyLockedShaders", "QueueSaveAfterLockUnlock", "SaveAfterLockUnlock" };
            foreach (var name in names)
                if (vendorTool.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic) == null)
                    throw new InvalidOperationException("The vendor callback interface cannot be observed.");
            var field = typeof(EditorApplication).GetField("update",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !typeof(Delegate).IsAssignableFrom(field.FieldType))
                throw new InvalidOperationException("The editor update subscriptions cannot be observed.");
            var callbacks = (Delegate)field.GetValue(null);
            return ShaderUtil.anythingCompiling || RecoveryWorkPending(vendorTool) ||
                (callbacks != null && callbacks.GetInvocationList().Any(callback =>
                    callback.Method.DeclaringType == vendorTool && names.Contains(callback.Method.Name)));
        }

        [UnityTearDown]
        public IEnumerator FinishOwnedLockCase()
        {
            if (activeOwnership == null)
                yield break;
            var before = activeOwnership;
            // One deadline serves both drains, so the inherited test timeout bounds the whole teardown.
            var deadline = EditorApplication.timeSinceStartup + 10.0;
            var drained = false;
            var cleanupFailures = new List<Exception>();
            try
            {
                // The pre-delete drain. The case work must finish before the owned assets go away.
                while (HasPendingVendorWork(activeVendorTool))
                {
                    if (EditorApplication.timeSinceStartup >= deadline)
                        Assert.Fail("Vendor work did not drain before the watchdog.");
                    yield return null;
                }

                blockedOwnership = true;
                cleanupFailures = DeleteOwnedAssets(before);

                // The vendor postprocessor receives the deletions only at the end of the import batch,
                // so the batch must complete before an absence of pending work means anything.
                AssetDatabase.Refresh();

                // The post-delete drain. The deletions schedule recovery and update work, and the final
                // snapshot waits for it, so no pass can run after the boundary claim.
                while (HasPendingVendorWork(activeVendorTool))
                {
                    if (EditorApplication.timeSinceStartup >= deadline)
                        Assert.Fail("Vendor work did not drain before the watchdog.");
                    yield return null;
                }

                try
                {
                    AssertOwnedCleanupFinished(before);
                }
                catch (Exception error)
                {
                    cleanupFailures.Add(error);
                }

                drained = true;
            }
            finally
            {
                blockedOwnership = true;
                if (drained)
                {
                    if (cleanupFailures.Count != 0)
                        throw new AggregateException("Owned cleanup failed.", cleanupFailures);
                    activeOwnership = null;
                    activeVendorTool = null;
                    blockedOwnership = false;
                }
            }
        }

        // The deletion attempts. Each attempt is error-safe, and the cache deletion runs even when the
        // fixture deletion throws.
        private static List<Exception> DeleteOwnedAssets(LockIsolationSnapshot before)
        {
            var failures = new List<Exception>();
            try
            {
                if (Directory.Exists(FixtureFolder))
                {
                    foreach (var path in Directory.GetFiles(FixtureFolder, "*", SearchOption.AllDirectories))
                    {
                        var assetPath = path.Replace('\\', '/');
                        var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                        if (type == typeof(Material) || (type != null && typeof(Texture).IsAssignableFrom(type)))
                            AssetDatabase.DeleteAsset(assetPath);
                    }
                    AssetDatabase.DeleteAsset(FixtureFolder);
                }
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
            finally
            {
                try
                {
                    var root = RealVendorLockTestSupport.CacheRoot;
                    if (!before.RootExists)
                    {
                        if (Directory.Exists(root))
                            AssetDatabase.DeleteAsset(root);
                    }
                    else if (Directory.Exists(root))
                    {
                        var after = CaptureLockIsolation();
                        var added = after.Files.Keys.Where(path => !before.Files.ContainsKey(path) &&
                                !path.EndsWith(".meta", StringComparison.Ordinal))
                            .Concat(after.Directories.Where(path => !before.Directories.Contains(path)))
                            .OrderByDescending(path => path.Count(character => character == '/'))
                            .ToArray();
                        foreach (var path in added)
                            AssetDatabase.DeleteAsset(root + "/" + path);
                    }
                }
                catch (Exception error)
                {
                    failures.Add(error);
                }
            }
            return failures;
        }

        // The final boundary. It runs only after the post-delete drain, so it claims fixture absence and
        // cache equality once no vendor work is observed.
        private static void AssertOwnedCleanupFinished(LockIsolationSnapshot before)
        {
            var final = CaptureLockIsolation();
            Assert.That(Directory.Exists(FixtureFolder) || File.Exists(FixtureFolder) ||
                File.Exists(FixtureFolder + ".meta"), Is.False, "The fixture remains after cleanup.");
            Assert.That(before.RootExists == final.RootExists &&
                before.RootIsFile == final.RootIsFile &&
                before.RootMetadataExists == final.RootMetadataExists &&
                ((before.RootMetadata == null && final.RootMetadata == null) ||
                 (before.RootMetadata != null && final.RootMetadata != null &&
                  before.RootMetadata.SequenceEqual(final.RootMetadata))) &&
                before.Directories.SetEquals(final.Directories) &&
                before.Files.Count == final.Files.Count &&
                before.Files.All(entry => final.Files.TryGetValue(entry.Key, out var digest) &&
                    digest == entry.Value), Is.True, "The cache differs from the pre-lock snapshot.");
        }

        // The LA8 fixture texture. One disposable linear 4-by-2 RGBA
        // texture with alternating zero and one red and alpha values.
        // The binary-exact values avoid incidental float rounding. The
        // texture sits under the owned fixture folder, and only it
        // persists.
        private static Texture2D CreateAssignedInputTexture(string name)
        {
            const int Width = 4;
            const int Height = 2;
            var pixels = new Color32[Width * Height];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var one = (x + y) % 2 == 0;
                    var channel = (byte)(one ? 255 : 0);
                    pixels[y * Width + x] =
                        new Color32(channel, 0, 0, channel);
                }
            }

            return TestTextureImport.WritePng(
                FixtureFolder + "/" + name + ".png", Width, Height,
                pixels, importer =>
                {
                    importer.sRGBTexture = false;
                    importer.mipmapEnabled = false;
                    importer.textureCompression =
                        TextureImporterCompression.Uncompressed;
                    importer.filterMode = FilterMode.Point;
                });
        }

        // The LA8 material shape. The exact spec values on both texture
        // slots. A missing property fails the fixture loudly, because a
        // silent write on an absent slot would stage no shape at all.
        private static void AssignTextureFixture(
            Material material, Texture2D texture)
        {
            Assert.That(
                material.HasProperty(MainTextureProperty) &&
                material.HasProperty(AlphaMaskProperty) &&
                material.HasProperty(MainPixelModeProperty) &&
                material.HasProperty(MainAlphaMaskModeProperty) &&
                material.HasProperty(AlphaForceOpaqueProperty),
                Is.True,
                "the bundled shader of this case misses an assigned-texture"
                + " property of the LA8 shape");
            var scale = new Vector2(2f, 0.5f);
            var offset = new Vector2(0.25f, 0.125f);
            material.SetTexture(MainTextureProperty, texture);
            material.SetTextureScale(MainTextureProperty, scale);
            material.SetTextureOffset(MainTextureProperty, offset);
            material.SetTexture(AlphaMaskProperty, texture);
            material.SetTextureScale(AlphaMaskProperty, scale);
            material.SetTextureOffset(AlphaMaskProperty, offset);
            material.SetFloat(MainPixelModeProperty, 1f);
            material.SetFloat(MainAlphaMaskModeProperty, 2f);
            material.SetFloat(AlphaForceOpaqueProperty, 0f);
        }

        // The LA8 pre-alignment vendor-output guard. It observes the
        // generated text in memory and anchors every required
        // generated substitution to its actual site, read from the 9.3
        // vendor source both rows carry. An actual define line is
        // required, not a comment or a guard token. The original
        // nucleus proves the fixture reaches the expression, and the
        // anchored generated pattern proves the substitution sits at
        // that exact site, so an unrelated occurrence of the same
        // literal cannot satisfy the guard. The generated side is
        // matched with spaces removed, because the vendor prints
        // vector literals without spaces, and the baked selector and
        // renamed neighbors stay flexible.
        private static void AssertAssignedTextureVendorOutputs(
            string generatedText, string originalText, string runDate)
        {
            Assert.That(
                Regex.IsMatch(
                    generatedText, @"(?m)^\s*#define\s+PROP_ALPHAMASK\b"),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the generated text carries no actual alpha mask"
                    + " define line", runDate, null));

            // The main transform site: the mainUV assignment through
            // poiUV on the uv selector.
            AssertSubstitutedExpression(
                generatedText, originalText,
                ".xy, " + MainTextureStName + ")",
                CompactRegex("float2mainUV=poiUV(poiMesh.uv[")
                + "[^\\]]*"
                + CompactRegex("].xy,")
                + CompactRegex(TransformLiteral)
                + CompactRegex(");"),
                runDate, "main texture scale and offset");

            // The alpha mask site: the alphaMask assignment through
            // POI2D_SAMPLER_PAN, with its two flexible name arguments.
            AssertSubstitutedExpression(
                generatedText, originalText,
                "], " + AlphaMaskStName + ")",
                CompactRegex("alphaMask=POI2D_SAMPLER_PAN(")
                + "[^,]*,[^,]*,"
                + CompactRegex("poiUV(poiMesh.uv[")
                + "[^\\]]*"
                + CompactRegex("],")
                + CompactRegex(TransformLiteral)
                + CompactRegex("),"),
                runDate, "alpha mask scale and offset");

            // The texel site: the mainUV assignment through sharpSample.
            AssertSubstitutedExpression(
                generatedText, originalText,
                "sharpSample(" + MainTextureTexelName + ", mainUV)",
                CompactRegex("mainUV=sharpSample(")
                + CompactRegex(TexelLiteral)
                + CompactRegex(",mainUV);"),
                runDate, "main texture texel size");
        }

        // The generated side matches compact text, so the vendor print
        // spacing is not the fact under test.
        private static string CompactRegex(string text)
        {
            return Regex.Escape(
                text.Replace(" ", string.Empty));
        }

        private static void AssertSubstitutedExpression(
            string generatedText,
            string originalText,
            string originalNucleus,
            string generatedPattern,
            string runDate,
            string label)
        {
            // The original side tolerates whitespace, so a spacing
            // difference between vendor revisions cannot hide the
            // expression.
            var pattern = string.Join(
                @"\s*", originalNucleus.Split(' ').Select(Regex.Escape));
            Assert.That(
                Regex.IsMatch(originalText, pattern), Is.True,
                RealVendorLockTestSupport.Dated(
                    "the original text carries no " + label
                    + " expression, so the fixture does not exercise the"
                    + " substituted site", runDate, null));
            Assert.That(
                Regex.IsMatch(
                    generatedText.Replace(" ", string.Empty),
                    generatedPattern),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the generated text does not carry the " + label
                    + " substitution at the anchored site",
                    runDate, null));
        }

        // The LA8 success guard. The main texel-size baked fact, both
        // transform facts, the reproduction Build validation with the
        // created texture dimensions, and both planned transforms and
        // texture identities.
        private static void AssertAssignedTextureFacts(
            AlignedLockedSource source,
            Material locked,
            IReadOnlyList<LockedSourceProperty> properties,
            Texture2D assignedTexture,
            string runDate)
        {
            Assert.That(
                source.States[MainTextureTexelName],
                Is.EqualTo(LockedPropertyState.Baked),
                RealVendorLockTestSupport.Dated(
                    "the main texel-size fact is not baked", runDate, null));
            Assert.That(
                source.BakedLiterals[MainTextureTexelName],
                Is.EqualTo(TexelLiteral),
                RealVendorLockTestSupport.Dated(
                    "the main texel-size baked fact does not match the"
                    + " created texture", runDate, null));
            Assert.That(
                source.States[MainTextureStName],
                Is.EqualTo(LockedPropertyState.Baked),
                RealVendorLockTestSupport.Dated(
                    "the main texture transform fact is not baked",
                    runDate, null));
            Assert.That(
                source.BakedLiterals[MainTextureStName],
                Is.EqualTo(TransformLiteral),
                RealVendorLockTestSupport.Dated(
                    "the main texture transform fact does not carry the"
                    + " spec scale and offset", runDate, null));
            Assert.That(
                source.States[AlphaMaskStName],
                Is.EqualTo(LockedPropertyState.Baked),
                RealVendorLockTestSupport.Dated(
                    "the alpha mask transform fact is not baked",
                    runDate, null));
            Assert.That(
                source.BakedLiterals[AlphaMaskStName],
                Is.EqualTo(TransformLiteral),
                RealVendorLockTestSupport.Dated(
                    "the alpha mask transform fact does not carry the"
                    + " spec scale and offset", runDate, null));

            var reproduction = LockedSourceReproduction.Build(
                source, properties, new UnityLockedMaterialValues(locked));
            Assert.That(
                reproduction.Succeeded, Is.True,
                RealVendorLockTestSupport.Dated(
                    "the reproduction build refused the assigned-texture"
                    + " lock", runDate, reproduction.RefusalDetail));
            var expectedTransform = new Vector4(2f, 0.5f, 0.25f, 0.125f);
            Assert.That(
                reproduction.Plan.TextureScaleOffsetWrites[
                    MainTextureProperty],
                Is.EqualTo(expectedTransform),
                RealVendorLockTestSupport.Dated(
                    "the planned main texture transform does not match"
                    + " the spec values", runDate, null));
            Assert.That(
                reproduction.Plan.TextureScaleOffsetWrites[
                    AlphaMaskProperty],
                Is.EqualTo(expectedTransform),
                RealVendorLockTestSupport.Dated(
                    "the planned alpha mask transform does not match"
                    + " the spec values", runDate, null));
            var expectedToken =
                UnityLockedMaterialValues.TextureTokenOf(assignedTexture);
            AssertPlanTextureIdentity(
                reproduction.Plan.TextureWrites, MainTextureProperty,
                expectedToken, runDate);
            AssertPlanTextureIdentity(
                reproduction.Plan.TextureWrites, AlphaMaskProperty,
                expectedToken, runDate);
        }

        private static void AssertPlanTextureIdentity(
            IReadOnlyDictionary<string, LockedTextureValue> textureWrites,
            string propertyName,
            string expectedToken,
            string runDate)
        {
            Assert.That(
                textureWrites.TryGetValue(propertyName, out var texture),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the plan carries no texture identity for "
                    + propertyName, runDate, null));
            Assert.That(
                texture.IdentityToken, Is.EqualTo(expectedToken),
                RealVendorLockTestSupport.Dated(
                    "the planned " + propertyName
                    + " identity is not the created texture", runDate, null));
            Assert.That(
                texture.Width == 4 && texture.Height == 2, Is.True,
                RealVendorLockTestSupport.Dated(
                    "the planned " + propertyName
                    + " identity does not carry the created texture"
                    + " dimensions", runDate, null));
        }

        // The gated integration proof of one successful case, while the
        // locked material is alive. The normal dry-run preview runs on
        // an owned disposable avatar with production defaults and both
        // admission flags false. The mirror proof follows the normal
        // gate, through the private mirror invocation, with no
        // production seam.
        private static void RunGatedPreviewProofs(
            Material material, string runDate)
        {
            DryRunCache.Clear();
            var avatarRoot = new GameObject(
                "AMUSE real lock preview avatar");
            avatarRoot.hideFlags = HideFlags.HideAndDontSave;
            var previewMesh = MinimalPreviewMesh();
            try
            {
                var body = new GameObject("AMUSE real lock preview body");
                body.hideFlags = HideFlags.HideAndDontSave;
                body.transform.SetParent(avatarRoot.transform, false);
                var renderer = body.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = previewMesh;
                renderer.sharedMaterials = new[] { material };
                var optimizer =
                    avatarRoot.AddComponent<AmuseAvatarOptimizer>();
                var dryRun = new AlphaSeparationDryRun(
                    optimizer,
                    () => EditorUserBuildSettings.activeBuildTarget);
                dryRun.Begin();
                for (var guard = 0; guard < 64 && !dryRun.Done; guard++)
                {
                    dryRun.Step();
                }

                Assert.That(
                    dryRun.Done, Is.True,
                    RealVendorLockTestSupport.Dated(
                        "the normal dry-run preview did not finish",
                        runDate, null));
                AssertPreviewShowsDeferredConversion(dryRun, runDate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatarRoot);
                UnityEngine.Object.DestroyImmediate(previewMesh);
                DryRunCache.Clear();
            }

            AssertLockedMirrorAccepts(material, runDate);
        }

        // The valid minimal preview mesh: one triangle with UVs, the
        // shape the dry-run compute fixtures use.
        private static Mesh MinimalPreviewMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
            };
            mesh.uv = new[]
            {
                new Vector2(0.25f, 0.25f),
                new Vector2(0.75f, 0.25f),
                new Vector2(0.25f, 0.75f),
            };
            mesh.subMeshCount = 1;
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            return mesh;
        }

        // Since the stage 7 flag change of 2026-10-30 the bundled rows
        // admit, so the preview shows the deferred conversion and no
        // named bundled-original refusal. Both row lists hold only this
        // run, because the session cache starts and ends cleared.
        private static void AssertPreviewShowsDeferredConversion(
            AlphaSeparationDryRun dryRun, string runDate)
        {
            var namedRefusal = AmuseReportStrings.Format(
                AmuseReportStrings.RendererKey(
                    RendererAnalysisRefusal
                        .LockedPoiyomiOriginalShaderUnattested));
            var rows = dryRun.RendererRows.Concat(dryRun.AvatarRows)
                .ToList();
            Assert.That(
                rows.Any(row =>
                    row.Kind == MaterialTransformationRowKind.Renderer &&
                    row.Sentence.Contains(namedRefusal)),
                Is.False,
                RealVendorLockTestSupport.Dated(
                    "the normal preview still carries the named"
                    + " bundled-original refusal after admission",
                    runDate, null));
            Assert.That(
                rows.Any(row => row.Sentence.StartsWith(
                    "Deferred:", StringComparison.Ordinal)),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the normal preview shows no deferred conversion"
                    + " after admission", runDate, null));
            var slotRows = rows.Where(row =>
                row.Kind == MaterialTransformationRowKind.Slot).ToList();
            Assert.That(
                slotRows.Count, Is.EqualTo(1),
                RealVendorLockTestSupport.Dated(
                    "the normal preview shows an unexpected slot row"
                    + " count", runDate, null));
            Assert.That(
                slotRows[0].Sentence,
                Is.EqualTo(RowText.DeferredSlotSentence(1)),
                RealVendorLockTestSupport.Dated(
                    "the locked slot does not show the deferred"
                    + " sentence after admission", runDate, null));
        }

        // The mirror proof. The private mirror invocation reaches the
        // actual private mirror through reflection and inspects its
        // reproduction and plan-view outcome. The aligned-facts guard
        // runs first, so a false answer from an unavailable pair cannot
        // pass as an acceptance.
        private static void AssertLockedMirrorAccepts(
            Material locked, string runDate)
        {
            var factsMethod = typeof(AlphaSeparationDryRun).GetMethod(
                "AlignedFactsOf",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(
                factsMethod, Is.Not.Null,
                "the dry-run aligned-facts method cannot be resolved for"
                + " the mirror proof");
            var arguments = new object[] { locked, null, null };
            var outcome = factsMethod.Invoke(null, arguments);
            Assert.That(
                outcome, Is.Not.Null,
                RealVendorLockTestSupport.Dated(
                    "the aligned-facts guard returned nothing", runDate,
                    null));
            Assert.That(
                outcome.ToString(), Is.EqualTo("Aligned"),
                RealVendorLockTestSupport.Dated(
                    "the aligned-facts guard does not report aligned"
                    + " facts, so the mirror answer could not separate"
                    + " an unavailable pair", runDate, null));
            Assert.That(
                arguments[1], Is.Not.Null,
                RealVendorLockTestSupport.Dated(
                    "the aligned facts carry no source", runDate, null));
            var properties =
                (IReadOnlyList<LockedSourceProperty>)arguments[2];
            Assert.That(
                properties, Is.Not.Null,
                RealVendorLockTestSupport.Dated(
                    "the aligned facts carry no properties", runDate,
                    null));
            Assert.That(
                properties.Count, Is.GreaterThan(0),
                RealVendorLockTestSupport.Dated(
                    "the aligned facts carry no property", runDate, null));

            var mirrorMethod = typeof(AlphaSeparationDryRun).GetMethod(
                "LockedMirrorRefuses",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(
                mirrorMethod, Is.Not.Null,
                "the dry-run mirror method cannot be resolved for the"
                + " mirror proof");
            var notedKinds = new List<NoteKind>();
            var refuses = (bool)mirrorMethod.Invoke(null, new object[]
            {
                locked,
                (Action<NoteKind>)(kind => notedKinds.Add(kind)),
            });
            Assert.That(
                refuses, Is.False,
                RealVendorLockTestSupport.Dated(
                    "the mirror refuses the locked pair", runDate, null));
            Assert.That(
                notedKinds, Has.No.Member(NoteKind.LockedMaterialNotReproducible),
                RealVendorLockTestSupport.Dated(
                    "the mirror noted the pair not reproducible",
                    runDate, null));

            // The reproduction and plan-view outcome the mirror runs.
            var lockedView = new UnityLockedMaterialValues(locked);
            var aligned = (AlignedLockedSource)arguments[1];
            var reproduction = LockedSourceReproduction.Build(
                aligned, properties, lockedView);
            Assert.That(
                reproduction.Succeeded, Is.True,
                RealVendorLockTestSupport.Dated(
                    "the mirror reproduction build refused", runDate,
                    reproduction.RefusalDetail));
            Assert.That(
                LockedSourceReproduction.CheckConsistency(
                    aligned,
                    properties,
                    new LockedPlanView(lockedView, reproduction.Plan)),
                Is.Null,
                RealVendorLockTestSupport.Dated(
                    "the mirror plan view refused the consistency check",
                    runDate, null));
        }

        // The per-phase times of one case, printed for the dated record.
        // A phase that never ran prints as not reached, so the line
        // names the reach of a refused run too.
        private static void ReportPhaseTimes(
            LockCase lockCase,
            string runDate,
            long generatedBytes,
            double lockMs,
            double alignMs,
            double restoreMs,
            double consistencyMs,
            double coldAlignMs)
        {
            var totalMs = lockMs > 0 && alignMs > 0
                ? lockMs + alignMs + Math.Max(restoreMs, 0)
                : -1.0;
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "RealLockAcceptance {0} {1} {2}: genBytes {3},"
                + " lock {4}, align {5}, cloneRestore {6},"
                + " consistency {7}, coldAlign {8},"
                + " lockAlignRestore total {9}",
                runDate,
                lockCase.RowLabel,
                lockCase.FileStem,
                generatedBytes >= 0
                    ? generatedBytes.ToString(CultureInfo.InvariantCulture)
                    : "not reached",
                PhaseText(lockMs),
                PhaseText(alignMs),
                PhaseText(restoreMs),
                PhaseText(consistencyMs),
                PhaseText(coldAlignMs),
                totalMs > 0
                    ? string.Format(
                        CultureInfo.InvariantCulture, "{0:F0} ms", totalMs)
                    : "not reached");
            TestContext.Progress.WriteLine(line);

            // The console copy of the same line, so a run record can
            // cite the numbers after the test output is gone. The suite
            // is opt-in, so the gated run owns this console line.
            UnityEngine.Debug.Log(line);
        }

        private static string PhaseText(double milliseconds)
        {
            return milliseconds >= 0
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:F0} ms", milliseconds)
                : "not reached";
        }
    }
}
