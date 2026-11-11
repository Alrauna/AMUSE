using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Runtime;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.fluent;
using nadena.dev.ndmf.platform;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

[assembly: ExportsPlugin(typeof(Alrauna.Amuse.Tests.Editor.Build.TransientUnlockRealLockTests.TransientUnlockRealLockPlugin))]

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The gated end-to-end proof for the transient unlock window on a
    /// real locked original (stage 4 task 4.5, plan of 2026-11-09). One
    /// test per bundled 9.3 Poiyomi row. Each test locks a real material
    /// with the installed vendor lock tool, runs one full build in the
    /// production pass order, and pins the window facts: the pair opens
    /// on the real locked original, the apply runs on the swapped world,
    /// and the close reverts every slot to the locked original.
    /// <para>
    /// Gate. Every test starts with the opt-in gate
    /// <see cref="VendorOptInGate.RealThryLock"/>. The lock writes
    /// vendor-owned cache data under Assets/_LockedShaderCache and calls
    /// the vendor usage counter once per editor session. An unset gate
    /// reports a named skip.
    /// </para>
    /// <para>
    /// Modal safety. The lock entry runs with dialogs off: the test
    /// invokes the two argument vendor overload with the none progress
    /// mode, and that overload passes show dialog false. A read of the
    /// vendor source on 2026-11-11 found every vendor dialog on menu or
    /// player build paths this route never reaches. The AMUSE consent
    /// dialog is unreachable, because the barrier passes an injected
    /// presenter that records the subjects and grants.
    /// </para>
    /// <para>
    /// Reuse. The suite resolves the real tool through
    /// <see cref="RealVendorLockTestSupport"/>, loads the bundled rows by
    /// the pinned GUIDs of <see cref="PoiyomiSourceAttestation"/>, rides
    /// the production pass bodies the same way the mirrored platform
    /// finish plugin does, and follows the proven lock case hygiene: a
    /// preflight that refuses an unclean editor, a teardown that drains
    /// the vendor callbacks, deletes the test materials before the test
    /// cache entries, and asserts the cache equals the pre lock snapshot.
    /// </para>
    /// <para>
    /// RED contract. A production apply that skips the revert in
    /// <see cref="TransientUnlockWindowClose"/> leaves the pair open and
    /// the slot off the locked original, so both tests fail on the pair
    /// count or the shipped slot assert.
    /// </para>
    /// </summary>
    public sealed class TransientUnlockRealLockTests
    {
        private const string FixtureFolderName = "AmuseTests_RealUnlock";

        private const string FixtureFolder = "Assets/" + FixtureFolderName;

        private const string ToonMaterialName = "RealUnlockToon";

        private const string TwoPassMaterialName = "RealUnlockTwoPass";

        private const string ToonRowLabel = "Toon";

        private const string TwoPassRowLabel = "Two Pass";

        // The gate disclosure. It names what the tests do, the disposable
        // dev project requirement, the vendor usage counter call, and the
        // Census Lab rule.
        private const string GateDisclosure =
            "Set " + VendorOptInGate.RealThryLock + "=1 in a disposable"
            + " dev project to run these tests. The tests lock real"
            + " materials with the installed vendor lock tool. The lock"
            + " writes vendor-owned cache data under "
            + RealVendorLockTestSupport.CacheRoot
            + ". The lock calls the vendor usage counter once per editor"
            + " session unless the editor preferences already record the"
            + " counts. The tests must never run in the Census Lab"
            + " project. They must never run in a project whose state"
            + " matters.";

        private Type _vendorTool;

        private CacheSnapshot _cacheSnapshot;

        private bool _armed;

        [SetUp]
        public void OpenFixtureScene()
        {
            TransientUnlockTestKnobs.Reset();
            TransientUnlockTestLifecycle.OpenFixtureScene();
        }

        [TearDown]
        public void DiscardFixtureScene()
        {
            TransientUnlockTestLifecycle.DiscardFixtureScene();
            TransientUnlockTestKnobs.Reset();
        }

        // Toon row. The main bundled shader of the installed bundle.
        [UnityTest]
        public IEnumerator RealLockedToonRowUnlocksAppliesAndRevertsThroughOneBuild()
        {
            yield return RunRealUnlockEndToEnd(
                PoiyomiSourceAttestation.CanonicalShaderGuid,
                ToonRowLabel,
                ToonMaterialName,
                assertConversion: true);
        }

        // Two Pass row. The second bundled shader of the installed
        // bundle.
        [UnityTest]
        public IEnumerator RealLockedTwoPassRowUnlocksAppliesAndRevertsThroughOneBuild()
        {
            yield return RunRealUnlockEndToEnd(
                PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                TwoPassRowLabel,
                TwoPassMaterialName,
                assertConversion: false);
        }

        // The one case runner behind both tests. The order is: the
        // bounded drain, the preflight, one real material, the real
        // lock, one full build in the production pass order, and the
        // teardown deletes the owned assets. The body after the drain
        // runs synchronously to the end.
        private IEnumerator RunRealUnlockEndToEnd(
            string shaderGuid,
            string rowLabel,
            string materialName,
            bool assertConversion)
        {
            // The opt-in gate. It fires before any vendor resolve, so an
            // unset gate reads as the named skip.
            VendorOptInGate.Require(
                VendorOptInGate.RealThryLock, GateDisclosure);

            ResolveRealTool();
            var runDate = DateTime.UtcNow.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var material = NewRealRowMaterial(
                shaderGuid, rowLabel, materialName);

            // The bounded drain. Vendor recovery work from an earlier
            // deletion in this session must finish before this case
            // starts, so a later recovery pass can never run inside the
            // case.
            var drainDeadline = EditorApplication.timeSinceStartup + 60.0;
            while (PendingVendorWork(_vendorTool))
            {
                if (EditorApplication.timeSinceStartup >= drainDeadline)
                {
                    Assert.Fail(
                        "vendor recovery work did not drain before the"
                        + " case");
                }

                yield return null;
            }

            PreflightOrRefuse();
            _armed = true;
            RealVendorLockTestSupport.EnsureFolder("Assets", FixtureFolderName);
            var materialPath =
                FixtureFolder + "/" + materialName + ".mat";
            AssetDatabase.CreateAsset(material, materialPath);

            // The real lock. The reflection call passes both arguments on
            // every call, so the invocation reaches the vendor overload
            // the build path uses. That overload passes show dialog false.
            var locked = (bool)_lockMethod.Invoke(null, new object[]
            {
                new List<Material> { material },
                _progressBarNone,
            });
            Assert.That(
                locked, Is.True,
                RealVendorLockTestSupport.Dated(
                    "the vendor lock call returned false", runDate, null));
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(material),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the locked identity of the real lock is not"
                    + " recognized", runDate, null));
            Assert.That(
                material.shader.name.StartsWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix,
                    StringComparison.Ordinal),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the locked shader name does not carry the vendor"
                    + " locked prefix, got " + material.shader.name,
                    runDate, null));
            Assert.That(
                material.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                RealVendorLockTestSupport.Dated(
                    "the lock left the optimizer flag clear", runDate, null));
            var lockedShaderPath = AssetDatabase.GetAssetPath(material.shader);
            Assert.That(
                lockedShaderPath.StartsWith(
                    RealVendorLockTestSupport.CacheRoot + "/",
                    StringComparison.Ordinal),
                Is.True,
                RealVendorLockTestSupport.Dated(
                    "the locked shader asset does not live under the"
                    + " vendor cache root, got " + lockedShaderPath,
                    runDate, null));

            // The build. The dedicated platform of this suite carries the
            // mirrored production pass list, so one build runs the real
            // structural check, the real capture, the real barrier with
            // the swap in, the real apply, and the real window close.
            AmusePlatformFinishState amuse = null;
            var mesh = AlphaSeparationTestLifecycle.SingleTriangleMesh();
            var root = new GameObject("AMUSE real unlock end to end root");
            try
            {
                root.AddComponent<Alrauna.Amuse.Runtime.AmuseAvatarOptimizer>();
                FixtureProofScope.PinAllSizes(root);
                var renderer = AlphaSeparationTestLifecycle.AddRendererPadded(
                    root, "body", mesh, material);

                BuildContext context;
                using (new OverrideTemporaryDirectoryScope(null))
                {
                    context = AvatarProcessor.ProcessAvatar(
                        root, RealUnlockWindowPlatform.Instance);
                }

                var probe = context.GetState<RealUnlockWindowProbe>();
                amuse = context.GetState<AmusePlatformFinishState>();
                var window = context.GetState<TransientUnlockWindowState>();

                Assert.That(
                    probe.Ran, Is.True,
                    "the barrier pass of the real unlock plugin did not"
                    + " run");
                Assert.That(
                    probe.Failure, Is.Null,
                    "the barrier pass threw: " + probe.Failure);
                Assert.That(
                    amuse.AvatarRefusal,
                    Is.EqualTo(AvatarAnimationRefusal.None),
                    RealVendorLockTestSupport.Dated(
                        "the build refused the avatar before the window",
                        runDate, null));

                // The mid window facts, recorded inside the barrier while
                // the pair is open. The close destroyed the clone, so the
                // open window shape cannot be read after the build.
                Assert.That(
                    probe.ConsentGranted, Is.True,
                    RealVendorLockTestSupport.Dated(
                        "the window did not grant consent on the attested"
                        + " bundled original", runDate, null));
                Assert.That(
                    probe.OpenPairCount, Is.EqualTo(1),
                    RealVendorLockTestSupport.Dated(
                        "the real locked material must open exactly one"
                        + " pair", runDate, null));
                Assert.That(
                    probe.SlotHeldClone, Is.True,
                    RealVendorLockTestSupport.Dated(
                        "the swapped slot did not hold the restored"
                        + " clone", runDate, null));
                Assert.That(
                    probe.RestoreMismatchCount, Is.EqualTo(0),
                    RealVendorLockTestSupport.Dated(
                        "the production restore must verify without a"
                        + " mismatch", runDate, null));
                Assert.That(
                    probe.ConsentSubjects, Is.Empty,
                    RealVendorLockTestSupport.Dated(
                        "a build over attested vendors and an admitted"
                        + " locked original raises no consent subject",
                        runDate, null));

                // The production accounting on the swapped world. The
                // renderer must analyze and no refusal may fire.
                Assert.That(
                    amuse.SemanticallyRefusedRendererCount, Is.EqualTo(0),
                    RealVendorLockTestSupport.Dated(
                        "the swapped renderer was refused", runDate, null));
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .LockedPoiyomiOriginalShaderUnattested),
                    Is.EqualTo(0),
                    RealVendorLockTestSupport.Dated(
                        "the admitted original must not carry the"
                        + " unattested refusal", runDate, null));
                Assert.That(
                    amuse.RendererRefusalCount(
                        RendererAnalysisRefusal
                            .AdmittedMaterialSemanticsUnknown),
                    Is.EqualTo(0),
                    RealVendorLockTestSupport.Dated(
                        "the admitted renderer must not answer unknown"
                        + " semantics", runDate, null));
                Assert.That(
                    amuse.AnalyzedRendererCount, Is.EqualTo(1),
                    RealVendorLockTestSupport.Dated(
                        "the admitted renderer must analyze", runDate, null));
                AssertSlotRefusals(amuse, runDate, assertConversion);

                // The apply on the Toon row. The restored clone carries
                // the convertible Fade state, so the apply must convert
                // it and replace the slot before the close reverts it.
                if (assertConversion)
                {
                    Assert.That(
                        amuse.Separation, Is.Not.Null,
                        RealVendorLockTestSupport.Dated(
                            "the apply pass prepared no separation for the"
                            + " convertible clone", runDate, null));
                    Assert.That(
                        amuse.Separation.CreatedClones, Has.Count.EqualTo(1),
                        RealVendorLockTestSupport.Dated(
                            "the unlocked clone must convert to one opaque"
                            + " output", runDate, null));
                    Assert.That(
                        amuse.AppliedRendererCount, Is.EqualTo(1),
                        RealVendorLockTestSupport.Dated(
                            "the apply pass must account the converted"
                            + " renderer", runDate, null));
                    Assert.That(
                        amuse.AppliedOpaqueTriangleCount, Is.EqualTo(1),
                        RealVendorLockTestSupport.Dated(
                            "the apply pass must account the opaque"
                            + " triangle", runDate, null));
                    Assert.That(
                        renderer.sharedMesh, Is.EqualTo(mesh),
                        RealVendorLockTestSupport.Dated(
                            "a wholly opaque slot must not touch the mesh",
                            runDate, null));
                    Assert.That(
                        mesh.subMeshCount, Is.EqualTo(1),
                        RealVendorLockTestSupport.Dated(
                            "a wholly opaque slot must not change the"
                            + " submesh count", runDate, null));
                }

                // The close outcome. The window holds zero open pairs and
                // the pair clone is destroyed. The shipped slot differs
                // by row: the apply transformed the Toon slot, so the
                // recorded apply write ships. The Two Pass family never
                // converts, so the close returns that slot to the real
                // locked original. A close that skips the revert leaves
                // the pair open and the clone alive, so both rows fail
                // here.
                Assert.That(
                    window.OpenPairs, Is.Empty,
                    RealVendorLockTestSupport.Dated(
                        "the window close must drain every open pair",
                        runDate, null));
                Assert.That(
                    probe.UnlockedClone == null, Is.True,
                    RealVendorLockTestSupport.Dated(
                        "the window close must destroy the unlocked"
                        + " clone", runDate, null));
                if (assertConversion)
                {
                    Assert.That(
                        renderer.sharedMaterials[0],
                        Is.EqualTo(
                            amuse.Separation.OpaqueBySource.Values.Single()),
                        RealVendorLockTestSupport.Dated(
                            "the transformed slot must ship the recorded"
                            + " apply write", runDate, null));
                    Assert.That(
                        LockedMaterialIdentity.RecognizedLockedIdentity(
                            material),
                        Is.True,
                        RealVendorLockTestSupport.Dated(
                            "the locked original must keep its untouched"
                            + " locked identity after the build", runDate,
                            null));
                }
                else
                {
                    Assert.That(
                        renderer.sharedMaterials[0], Is.EqualTo(material),
                        RealVendorLockTestSupport.Dated(
                            "the close must return the untransformed slot"
                            + " to the real locked original", runDate, null));
                }

                ReportRun(rowLabel, runDate, probe, amuse);
            }
            finally
            {
                AlphaSeparationTestLifecycle.DestroyGenerated(amuse);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        // --- The plugin and the platform of this suite ------------------

        internal const string RealUnlockPlatformName =
            "com.alrauna.amuse.tests.real-unlock-window";

        internal sealed class RealUnlockWindowPlatform : INDMFPlatformProvider
        {
            internal static readonly RealUnlockWindowPlatform Instance =
                new RealUnlockWindowPlatform();

            public string QualifiedName => RealUnlockPlatformName;
            public string DisplayName => "AMUSE real unlock window";
        }

        /// <summary>
        /// The mirrored production pass list of the platform finish
        /// plugin, plus the window close. The production plugin runs on
        /// the VRChat platform name only, so on this dedicated platform
        /// this plugin is the whole PlatformFinish phase. Vendor passes
        /// never select on it. The barrier passes fixture facts and an
        /// injected consent presenter, exactly the production entry the
        /// alpha separation suites drive under their production capture
        /// scope. Everything else is the production pass body.
        /// </summary>
        [RunsOnPlatforms(RealUnlockPlatformName)]
        public sealed class TransientUnlockRealLockPlugin :
            Plugin<TransientUnlockRealLockPlugin>
        {
            public override string QualifiedName =>
                "com.alrauna.amuse.tests.real-unlock-window-plugin";

            protected override void Configure()
            {
                var sequence = InPhase(BuildPhase.PlatformFinish);

                // Production pass one. The structural graph check runs
                // extension free and first.
                sequence.Run(
                    AmusePlatformFinishPlugin.StructuralGraphCheckPassName,
                    AmuseStructuralGraphCheck.Execute);

                // Production passes two to four. The capture and the
                // barrier share one animator scope. The apply reactivates
                // the scope, exactly the production topology.
                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner =>
                    {
                        inner.Run(
                            AmusePlatformFinishPlugin
                                .BindingsCapturePassName,
                            AmuseAnimatorBindingsCapture.Execute);
                        inner.Run(
                            AmusePlatformFinishPlugin.BarrierPassName,
                            BarrierPass);
                    });

                sequence.WithRequiredExtension(
                    typeof(AnimatorServicesContext),
                    inner => inner.Run(
                        AlphaSeparationApply.PassName,
                        ctx => AlphaSeparationApply.Execute(ctx)));

                // Production pass five. The window close runs extension
                // free, so its reference writes are the final word.
                sequence.Run(
                    TransientUnlockWindowClose.PassName,
                    ctx => TransientUnlockWindowClose.Execute(ctx));
            }

            private static void BarrierPass(BuildContext context)
            {
                var probe = context.GetState<RealUnlockWindowProbe>();
                probe.Ran = true;
                try
                {
                    // The production entry with fixture facts and one
                    // injected presenter. The presenter records the
                    // subjects and grants, so no native dialog can open.
                    // The selection, the capture, the resolution, the
                    // conversion, and the original attestation stay null,
                    // which means production runs them.
                    AmusePlatformFinishPass.Execute(
                        context,
                        TransientUnlockTestLifecycle.SupportedFacts(),
                        consentPresenter: subjects =>
                        {
                            probe.ConsentSubjects.AddRange(subjects);
                            return true;
                        });

                    // The mid window facts. The apply and the close run
                    // as later passes, so the pair is still open here.
                    var window =
                        context.GetState<TransientUnlockWindowState>();
                    probe.ConsentGranted = window.ConsentGranted;
                    probe.OpenPairCount = window.OpenPairs.Count;
                    if (probe.OpenPairCount > 0)
                    {
                        probe.UnlockedClone =
                            window.OpenPairs[0].UnlockedClone;
                        var renderer = context.AvatarRootObject
                            .GetComponentInChildren<SkinnedMeshRenderer>();
                        probe.SlotHeldClone = renderer != null
                            && renderer.sharedMaterials.Length > 0
                            && renderer.sharedMaterials[0]
                                == window.OpenPairs[0].UnlockedClone;
                    }

                    probe.RestoreMismatchCount = context
                        .GetState<AmusePlatformFinishState>()
                        .SlotRefusalCount(
                            AlphaSeparationSlotRefusal
                                .TransientUnlockRestoreMismatch);
                }
                catch (Exception exception)
                {
                    probe.Failure = exception;
                }
            }
        }

        /// <summary>The window facts the barrier pass records.</summary>
        public sealed class RealUnlockWindowProbe
        {
            public bool Ran;

            public Exception Failure;

            public bool ConsentGranted;

            public int OpenPairCount;

            public bool SlotHeldClone;

            public int RestoreMismatchCount;

            // The unlocked clone the pair held mid window. The close must
            // destroy it, so the post build assert reads it as destroyed.
            public Material UnlockedClone;

            public readonly List<string> ConsentSubjects =
                new List<string>();
        }

        // --- Fixture and asserts ----------------------------------------

        // A material on the GUID-loaded bundled row at the convertible
        // state of the proven real apply row: force opaque over the Fade
        // render state. Every written fact is a vendor property a fresh
        // installed material can carry.
        private static Material NewRealRowMaterial(
            string shaderGuid, string rowLabel, string materialName)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(shaderGuid);
            if (string.IsNullOrEmpty(assetPath))
            {
                Assert.Ignore(
                    "the bundled 9.3 shader asset of the " + rowLabel
                    + " row does not resolve from its pinned row GUID."
                    + " Install the Poiyomi package to run these tests");
            }

            var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (shader == null)
            {
                Assert.Ignore(
                    "the bundled 9.3 shader asset of the " + rowLabel
                    + " row does not load at its resolved path. Install"
                    + " the Poiyomi package to run these tests");
            }

            var material = new Material(shader) { name = materialName };
            material.SetFloat("_AlphaForceOpaque", 1f);
            PoiyomiFixtureTestBase.ApplyConvertibleRenderState(material);
            return material;
        }

        // The slot refusal expectations per row. The Toon row converts,
        // so no refusal may fire. The Two Pass family never converts, so
        // the apply records exactly the unsupported family refusal on
        // its slot and no other refusal may fire.
        private static void AssertSlotRefusals(
            AmusePlatformFinishState state,
            string runDate,
            bool assertConversion)
        {
            foreach (var reason in Enum.GetValues(
                typeof(AlphaSeparationSlotRefusal)))
            {
                var slotRefusal = (AlphaSeparationSlotRefusal)reason;
                if (slotRefusal == AlphaSeparationSlotRefusal.None)
                {
                    continue;
                }

                var expected = !assertConversion &&
                    slotRefusal ==
                    AlphaSeparationSlotRefusal
                        .OpaqueConversionUnsupportedFamily
                    ? 1
                    : 0;
                Assert.That(
                    state.SlotRefusalCount(slotRefusal),
                    Is.EqualTo(expected),
                    RealVendorLockTestSupport.Dated(
                        "the slot refusal " + slotRefusal + " counted "
                        + state.SlotRefusalCount(slotRefusal)
                        + " times, and this row expects " + expected,
                        runDate, null));
            }
        }

        // The run record line. It names the row, the mid window facts,
        // and the apply accounting, so the stage report can cite one
        // observed line per gated run.
        private static void ReportRun(
            string rowLabel,
            string runDate,
            RealUnlockWindowProbe probe,
            AmusePlatformFinishState amuse)
        {
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "RealUnlockWindow {0} {1}: pairs {2}, slotHeldClone {3},"
                + " consent {4}, subjects {5}, restoreMismatch {6},"
                + " analyzed {7}, appliedRenderers {8}, appliedTriangles {9}",
                runDate,
                rowLabel,
                probe.OpenPairCount,
                probe.SlotHeldClone,
                probe.ConsentGranted,
                probe.ConsentSubjects.Count,
                probe.RestoreMismatchCount,
                amuse.AnalyzedRendererCount,
                amuse.AppliedRendererCount,
                amuse.AppliedOpaqueTriangleCount);
            TestContext.Progress.WriteLine(line);
            UnityEngine.Debug.Log(line);
        }

        // --- Vendor binding ---------------------------------------------

        private MethodInfo _lockMethod;

        private MethodInfo _unlockMethod;

        private object _progressBarNone;

        // The vendor binding rule. The suite skips with a named reason
        // when the vendor is absent, and the shared guard proves the
        // binding before any call.
        private void ResolveRealTool()
        {
            _vendorTool = RealVendorLockTestSupport.VendorToolOrNull();
            if (_vendorTool == null)
            {
                Assert.Ignore(
                    "the thryeditor package is not installed in this"
                    + " project. The suite cannot resolve the real lock"
                    + " tool. Install thryeditor to run these tests");
            }

            RealVendorLockTestSupport.ResolveRealTool(
                _vendorTool, out _lockMethod, out _unlockMethod,
                out _progressBarNone);
        }

        // --- Lock case hygiene ------------------------------------------

        // One snapshot of the vendor cache root. The baseline of a clean
        // root holds at most the vendor readme pair.
        private sealed class CacheSnapshot
        {
            internal bool RootExists;

            internal bool RootIsFile;

            internal bool RootMetadataExists;

            internal byte[] RootMetadata;

            internal readonly HashSet<string> Directories =
                new HashSet<string>(StringComparer.Ordinal);

            internal readonly Dictionary<string, string> Files =
                new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private enum CacheIsolation
        {
            Clean,
            CacheNotEmpty,
            MetadataMismatch,
        }

        private static CacheIsolation EvaluateCacheIsolation(
            CacheSnapshot snapshot)
        {
            if (snapshot.RootExists != snapshot.RootMetadataExists ||
                snapshot.RootIsFile)
            {
                return snapshot.RootExists != snapshot.RootMetadataExists
                    ? CacheIsolation.MetadataMismatch
                    : CacheIsolation.CacheNotEmpty;
            }

            if (snapshot.Directories.Count != 0 ||
                (snapshot.Files.Count != 0 &&
                 !(snapshot.Files.Count == 2 &&
                   snapshot.Files.ContainsKey("README.txt") &&
                   snapshot.Files.ContainsKey("README.txt.meta"))))
            {
                return CacheIsolation.CacheNotEmpty;
            }

            return CacheIsolation.Clean;
        }

        private static CacheSnapshot CaptureCacheSnapshot()
        {
            var root = RealVendorLockTestSupport.CacheRoot;
            var snapshot = new CacheSnapshot
            {
                RootExists = Directory.Exists(root),
                RootIsFile = File.Exists(root),
                RootMetadataExists = File.Exists(root + ".meta"),
            };
            if (snapshot.RootMetadataExists)
            {
                snapshot.RootMetadata = File.ReadAllBytes(root + ".meta");
            }

            if (!snapshot.RootExists)
            {
                return snapshot;
            }

            foreach (var path in Directory.GetDirectories(
                root, "*", SearchOption.AllDirectories))
            {
                snapshot.Directories.Add(
                    path.Substring(root.Length + 1).Replace('\\', '/'));
            }

            using (var hash = SHA256.Create())
            {
                foreach (var path in Directory.GetFiles(
                    root, "*", SearchOption.AllDirectories))
                {
                    using (var stream = File.OpenRead(path))
                    {
                        snapshot.Files.Add(
                            path.Substring(root.Length + 1)
                                .Replace('\\', '/'),
                            Convert.ToBase64String(
                                hash.ComputeHash(stream)));
                    }
                }
            }

            return snapshot;
        }

        // The preflight. It refuses an unclean editor before any write.
        // A refusal fails the test loudly instead of running a lock in a
        // dirty project.
        private void PreflightOrRefuse()
        {
            var snapshot = CaptureCacheSnapshot();
            Assert.That(
                EvaluateCacheIsolation(snapshot),
                Is.EqualTo(CacheIsolation.Clean),
                "the vendor cache holds content this test did not"
                + " create. A lock on top of it could claim foreign"
                + " entries. Clean the cache outside the test run");
            var fixtureExists = Directory.Exists(FixtureFolder) ||
                File.Exists(FixtureFolder) ||
                File.Exists(FixtureFolder + ".meta");
            Assert.That(
                fixtureExists, Is.False,
                "a fixture folder from an earlier run still exists at "
                + FixtureFolder);
            var dirty =
                Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                    .Any(IsBlockingDirtyAsset);
            for (var index = 0; index <
                UnityEngine.SceneManagement.SceneManager.sceneCount;
                index++)
            {
                dirty |= UnityEngine.SceneManagement.SceneManager
                    .GetSceneAt(index).isDirty;
            }

            Assert.That(
                dirty, Is.False,
                "a native asset or an open scene is dirty. The owner"
                + " waiver excludes only the player settings, so a lock"
                + " must never run beside unsaved user work");
            Assert.That(
                ShaderUtil.anythingCompiling ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode,
                Is.False, "another editor operation is active");
            Assert.That(
                EffectiveTeardownTimeout(), Is.GreaterThan(10000),
                "the teardown timeout must exceed the drain watchdog");
            Assert.That(
                PendingVendorWork(_vendorTool), Is.False,
                "vendor lock work is already pending");
            AssertNoRecoverableLockedMaterial();
            _cacheSnapshot = snapshot;
        }

        // The dirty asset rule. The owner approved this narrow waiver
        // for the dev editor only. A dirty native asset blocks the case,
        // the player settings never do.
        private static bool IsBlockingDirtyAsset(UnityEngine.Object asset)
        {
            return asset != null &&
                EditorUtility.IsPersistent(asset) &&
                EditorUtility.IsDirty(asset) &&
                AssetDatabase.IsNativeAsset(asset) &&
                asset.GetType() != typeof(PlayerSettings);
        }

        private static int EffectiveTeardownTimeout()
        {
            var type = typeof(UnityTestAttribute).Assembly.GetType(
                "UnityEngine.TestRunner.NUnitExtensions.Runner"
                + ".UnityTestExecutionContext", true);
            var context = type.GetProperty(
                "CurrentContext", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);
            if (context == null)
            {
                throw new InvalidOperationException(
                    "The test timeout cannot be observed.");
            }

            var property = type.GetProperty(
                "TestCaseTimeout",
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "The test timeout cannot be observed.");
            }

            var timeout = (int)property.GetValue(context);
            return timeout == 0 ? 180000 : timeout;
        }

        // The recovery preflight guard. It runs the real vendor partition
        // over the project materials and refuses a recoverable target,
        // because a later recovery pass could restore an unrelated
        // locked material outside this case. The call is read only.
        private void AssertNoRecoverableLockedMaterial()
        {
            var recoveryType = _vendorTool.Assembly.GetType(
                "Thry.ThryEditor.LockedShaderRecovery", false);
            Assert.That(
                recoveryType, Is.Not.Null,
                "the vendor recovery interface cannot be observed");
            var partitionMethod = recoveryType.GetMethod(
                "Partition", BindingFlags.Public | BindingFlags.Static);
            Assert.That(
                partitionMethod, Is.Not.Null,
                "the vendor recovery partition cannot be observed");
            var materials = AssetDatabase.FindAssets("t:Material")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(material => material != null);
            var arguments = new object[] { materials.ToList(), null, null };
            partitionMethod.Invoke(null, arguments);
            var recoverable = (List<Material>)arguments[1];
            Assert.That(
                recoverable.Count == 0, Is.True,
                "the recovery preflight found recoverable locked"
                + " materials, so a later recovery pass could restore an"
                + " unrelated target outside this case. Resolve them"
                + " before the lock");
        }

        // The vendor callback binding. The names and the flags mirror the
        // pending work check of the bundled real lock acceptance suite.
        private static bool PendingVendorWork(Type vendorTool)
        {
            if (vendorTool == null)
            {
                throw new InvalidOperationException(
                    "The vendor tool cannot be observed.");
            }

            var names = new[]
            {
                "VerifyLockedShaders", "QueueSaveAfterLockUnlock",
                "SaveAfterLockUnlock",
            };
            foreach (var name in names)
            {
                if (vendorTool.GetMethod(
                    name, BindingFlags.Static | BindingFlags.NonPublic)
                    == null)
                {
                    throw new InvalidOperationException(
                        "The vendor callback interface cannot be"
                        + " observed.");
                }
            }

            var postprocessorType = VendorRecoveryPostprocessor(vendorTool);
            var flags = RecoveryFlagsRaised(postprocessorType) ||
                RecoveryDelayCallQueued(postprocessorType);
            if (flags)
            {
                return true;
            }

            var field = typeof(EditorApplication).GetField("update",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static);
            if (field == null ||
                !typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    "The editor update subscriptions cannot be"
                    + " observed.");
            }

            var callbacks = (Delegate)field.GetValue(null);
            return callbacks != null && callbacks.GetInvocationList().Any(
                callback =>
                    callback.Method.DeclaringType == vendorTool &&
                    names.Contains(callback.Method.Name)) ||
                ShaderUtil.anythingCompiling;
        }

        private static Type VendorRecoveryPostprocessor(Type vendorTool)
        {
            var recoveryType = vendorTool.Assembly.GetType(
                "Thry.ThryEditor.LockedShaderRecovery", false);
            if (recoveryType == null)
            {
                throw new InvalidOperationException(
                    "The vendor recovery interface cannot be observed.");
            }

            var postprocessorType = recoveryType.GetNestedType(
                "Postprocessor", BindingFlags.NonPublic);
            if (postprocessorType == null)
            {
                throw new InvalidOperationException(
                    "The vendor recovery interface cannot be observed.");
            }

            return postprocessorType;
        }

        private static bool RecoveryDelayCallQueued(Type postprocessorType)
        {
            var runMethod = postprocessorType.GetMethod(
                "Run", BindingFlags.Static | BindingFlags.NonPublic);
            if (runMethod == null)
            {
                throw new InvalidOperationException(
                    "The vendor recovery callback cannot be observed.");
            }

            var field = typeof(EditorApplication).GetField("delayCall",
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Static);
            if (field == null ||
                !typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                throw new InvalidOperationException(
                    "The editor delayed call subscriptions cannot be"
                    + " observed.");
            }

            var callbacks = (Delegate)field.GetValue(null);
            return callbacks != null && callbacks.GetInvocationList().Any(
                callback =>
                    callback.Method.DeclaringType == postprocessorType &&
                    callback.Method.Name == "Run");
        }

        private static bool RecoveryFlagsRaised(Type postprocessorType)
        {
            var queuedField = postprocessorType.GetField("s_queued",
                BindingFlags.Static | BindingFlags.NonPublic);
            var fullScanField = postprocessorType.GetField(
                "s_needsFullScan", BindingFlags.Static | BindingFlags.NonPublic);
            if (queuedField == null || fullScanField == null ||
                queuedField.FieldType != typeof(bool) ||
                fullScanField.FieldType != typeof(bool))
            {
                throw new InvalidOperationException(
                    "The vendor recovery state cannot be observed.");
            }

            return (bool)queuedField.GetValue(null) ||
                (bool)fullScanField.GetValue(null);
        }

        // The owned cleanup. It drains the vendor callbacks, deletes the
        // fixture folder before the cache additions, drains again, and
        // asserts the cache equals the pre lock snapshot. The deletion
        // order is the documented vendor rule: a cache entry a still
        // referencing material holds breaks that material when the entry
        // is deleted first.
        [UnityTearDown]
        public IEnumerator DeleteOwnedAssetsAfterDrain()
        {
            if (!_armed)
            {
                yield break;
            }

            var before = _cacheSnapshot;
            var deadline = EditorApplication.timeSinceStartup + 10.0;
            var drained = false;
            try
            {
                while (PendingVendorWork(_vendorTool))
                {
                    if (EditorApplication.timeSinceStartup >= deadline)
                    {
                        Assert.Fail(
                            "vendor work did not drain before the"
                            + " watchdog");
                    }

                    yield return null;
                }

                AssetDatabase.DeleteAsset(FixtureFolder);

                // The vendor postprocessor receives the deletions only at
                // the end of the import batch, so the batch must complete
                // before an absence of pending work means anything.
                AssetDatabase.Refresh();

                while (PendingVendorWork(_vendorTool))
                {
                    if (EditorApplication.timeSinceStartup >= deadline)
                    {
                        Assert.Fail(
                            "vendor work did not drain before the"
                            + " watchdog");
                    }

                    yield return null;
                }

                DeleteAddedCacheEntries(before);
                AssertCacheMatchesSnapshot(before);
                Assert.That(
                    Directory.Exists(FixtureFolder) ||
                    File.Exists(FixtureFolder) ||
                    File.Exists(FixtureFolder + ".meta"),
                    Is.False, "the fixture remains after cleanup");
                drained = true;
            }
            finally
            {
                if (drained)
                {
                    _armed = false;
                    _cacheSnapshot = null;
                }
            }
        }

        // The cache additions of this case, deepest first. The vendor
        // cache folder and its entries of the bundled rows carry vendor
        // names, so the plan computes them from the snapshot difference.
        private static void DeleteAddedCacheEntries(CacheSnapshot before)
        {
            var root = RealVendorLockTestSupport.CacheRoot;
            if (!Directory.Exists(root))
            {
                return;
            }

            var after = CaptureCacheSnapshot();
            var added = after.Files.Keys
                .Where(path => !before.Files.ContainsKey(path) &&
                    !path.EndsWith(".meta", StringComparison.Ordinal))
                .Concat(after.Directories
                    .Where(path => !before.Directories.Contains(path)))
                .OrderByDescending(
                    path => path.Count(character => character == '/'))
                .ToArray();
            foreach (var path in added)
            {
                AssetDatabase.DeleteAsset(root + "/" + path);
            }
        }

        private static void AssertCacheMatchesSnapshot(CacheSnapshot before)
        {
            var final = CaptureCacheSnapshot();
            var equal = before.RootExists == final.RootExists &&
                before.RootIsFile == final.RootIsFile &&
                before.RootMetadataExists == final.RootMetadataExists &&
                ((before.RootMetadata == null && final.RootMetadata == null) ||
                 (before.RootMetadata != null && final.RootMetadata != null &&
                  before.RootMetadata.SequenceEqual(final.RootMetadata))) &&
                before.Directories.SetEquals(final.Directories) &&
                before.Files.Count == final.Files.Count &&
                before.Files.All(entry =>
                    final.Files.TryGetValue(
                        entry.Key, out var digest) && digest == entry.Value);
            Assert.That(
                equal, Is.True,
                "the vendor cache differs from the pre lock snapshot");
        }
    }
}
