using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;

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
    /// Limits (record section 8). Every case uses default textures. The
    /// color space is the one of the dev project. The vendor version is
    /// the installed one. These limits are measurement conditions, not
    /// gates.
    /// </para>
    /// <para>
    /// Cleanup order. The finally block deletes the materials the suite
    /// created first, then only the cache entries that appeared under
    /// Assets/_LockedShaderCache during the test. The suite snapshots
    /// that folder set before the first lock and asserts the set after
    /// cleanup equals the snapshot. It never deletes an entry it did not
    /// create, because a deleted entry breaks every other locked
    /// material that holds it (V13.2).
    /// </para>
    /// </summary>
    public sealed class BundledLockedOriginalRealLockTests
    {
        private const string FixtureFolder = "Assets/AmuseTests_RealLock";

        // The shape inputs of record section 4.1. A tag value of 1 keeps
        // the property live under its plain name. A tag value of 2
        // renames it with the cleaned material name as suffix.
        private const string BaseColorAnimatedTag = "_ColorAnimated";

        private const string EmissionColorAnimatedTag =
            "_EmissionColorAnimated";

        private const string LightVolumesProperty =
            "_LightingEnableLightVolumes";

        private const string AudioLinkProperty = "_EnableAudioLink";

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

        // The cache set before the first lock of the run. The cleanup of
        // every test must leave the set equal to this snapshot.
        private static HashSet<string> cacheSnapshotBeforeFirstLock =
            new HashSet<string>(StringComparer.Ordinal);

        // The fidelity pin runs once per suite run, in the first test
        // that reaches the seam.
        private static bool fidelityPinned;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            cacheSnapshotBeforeFirstLock = SnapshotCacheRoot();
            fidelityPinned = false;
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
        }

        // The one case runner behind all ten tests. It carries the full
        // acceptance order of Y9 item 5, the per-phase times of Y10
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

            RealVendorLockTestSupport.EnsureFolder("Assets", "AmuseTests_RealLock");
            var materialPath =
                FixtureFolder + "/" + lockCase.FileStem + ".mat";
            var material = new Material(shader)
            {
                name = lockCase.MaterialName,
            };
            ApplyShape(material, lockCase);
            // The lock processes a persistent material only, so the
            // fixture material becomes a project asset first. Default
            // textures only, per the record section 8 limits.
            AssetDatabase.CreateAsset(material, materialPath);

            // The cache set before this test locks. The cleanup diff of
            // this test reads only the entries this test created.
            var cacheSetBeforeLock = SnapshotCacheRoot();
            Material timingClone = null;

            // The measured phases. A phase that never ran stays below
            // zero, so the report line names the reach of every run.
            var lockMs = -1.0;
            var alignMs = -1.0;
            var restoreMs = -1.0;
            var consistencyMs = -1.0;
            var dryMs = -1.0;
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

                // The swap-in harness. Direct call on a public
                // constructor build context, with the animator extension
                // active, consent granted, and the production restore
                // delegate (spec Validation 4).
                var root = new GameObject(
                    "AMUSE real lock acceptance root");
                var renderer = root.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMaterials = new[] { material };
                try
                {
                    using var assets = new OverrideTemporaryDirectoryScope(
                        TransientUnlockTestLifecycle.TempFolder);
                    var context = new BuildContext(
                        root, TransientUnlockTestLifecycle.TempFolder);
                    try
                    {
                        context.ActivateExtensionContext
                            <AnimatorServicesContext>();
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
                        context.DeactivateExtensionContext
                            <AnimatorServicesContext>();
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
                finally
                {
                    AssetDatabase.DeleteAsset(
                        TransientUnlockTestLifecycle.TempFolder);
                }

                // Phase two. The clone and restore time around the
                // standalone reconstruction on a fresh clone. The
                // stopwatch starts before the clone, so the phase covers
                // the clone and the restore (Y10 item 2).
                var restoreWatch = Stopwatch.StartNew();
                timingClone = UnityEngine.Object.Instantiate(material);
                timingClone.name = material.name;
                var restored = LockedMaterialReconstruction.TryApply(
                    timingClone, _ => true);
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

                // Phase three. The dry-run align time. The dry run reads
                // the same memo and pays exactly this cost, file reads
                // and digests included, once per distinct generated
                // shader (Y10 item 2).
                LockedSourceAlignmentMemo.Clear();
                var dryWatch = Stopwatch.StartNew();
                var dryAligned = LockedSourceAlignmentMemo.GetOrAlign(
                    material.shader, shader, properties);
                dryWatch.Stop();
                dryMs = dryWatch.Elapsed.TotalMilliseconds;
                Assert.That(
                    dryAligned.Succeeded, Is.True,
                    RealVendorLockTestSupport.Dated("the dry-run align refused the same pair",
                        runDate, dryAligned.RefusalDetail));
            }
            finally
            {
                ReportPhaseTimes(
                    lockCase, runDate, generatedBytes, lockMs, alignMs,
                    restoreMs, consistencyMs, dryMs);
                if (timingClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(timingClone);
                }

                CleanUpToSnapshot(cacheSetBeforeLock);
                LockedSourceAlignmentMemo.Clear();
            }
        }

        // The test scoped attestation delegate. It answers the
        // production answer for every non-bundled row and for unrelated
        // shaders, and adds true only for a locked material whose
        // resolved original is one of the two bundled rows. The row flag
        // of the bundled rows is false until stage 7 (Y9).
        internal static bool TestScopedAttestation(Material material)
        {
            return LockedMaterialIdentity.OriginalShaderAttested(material)
                || ResolvesToBundledRow(material);
        }

        // The bundled row override. It applies the same conjunction as
        // the production seam (K8.3) and flips only the row admission
        // bit. A material whose recorded original names a release row
        // fails the name rule on this install, so the override answers
        // false for it and the seam stays faithful.
        private static bool ResolvesToBundledRow(Material material)
        {
            if (material == null)
            {
                return false;
            }

            var recordedName = material.GetTag(
                LockedMaterialIdentity.OriginalShaderTagName, true, null);
            var recordedGuid = material.GetTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                true, null);
            if (string.IsNullOrEmpty(recordedName) ||
                string.IsNullOrEmpty(recordedGuid))
            {
                return false;
            }

            var resolvedPath =
                AssetDatabase.GUIDToAssetPath(recordedGuid);
            var resolved = string.IsNullOrEmpty(resolvedPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Shader>(resolvedPath);
            if (resolved == null)
            {
                return false;
            }

            // The name rule of the production seam (K8.2).
            if (!string.Equals(
                    recordedName, resolved.name,
                    StringComparison.Ordinal))
            {
                return false;
            }

            var sourceText = LockedMaterialIdentity.ReadOriginalText(
                resolved);
            if (sourceText == null)
            {
                return false;
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    resolved, out var guid, out long _))
            {
                return false;
            }

            return PoiyomiSourceAttestation.TryMatchAdmittedSource(
                resolved.name,
                guid.ToLowerInvariant(),
                PoiyomiSourceAttestation.ComputeNormalizedSourceHash(
                    sourceText),
                out var row)
                && !row.LockedOriginalAdmitted;
        }

        // The fidelity pin. One assertion per suite run. A material
        // whose recorded original names the 9.3.64 release row resolves
        // on this install to the bundled file, so the production name
        // rule answers false. The seam must answer exactly what the
        // production check answers for it, and true for the bundled row
        // of the live lock.
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

        // The cleanup of one test. The order follows the shared planner
        // of the characterization: the test's own folders first, then
        // only the cache entries that appeared during the test, deepest
        // first. The final assert compares the cache set against the
        // snapshot of the run.
        private static void CleanUpToSnapshot(
            HashSet<string> cacheSetBeforeLock)
        {
            var after = SnapshotCacheRoot();
            var newEntries = after
                .Where(path => !cacheSetBeforeLock.Contains(path))
                .OrderByDescending(DirectoryDepth)
                .ToList();
            var testFolders = new List<string>
            {
                FixtureFolder,
                TransientUnlockTestLifecycle.TempFolder,
            };

            var plan = RealThryLockCharacterizationTests
                .ResidueCleanupPlan(testFolders, newEntries);
            foreach (var path in plan)
            {
                if (File.Exists(path) || Directory.Exists(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

            var final = SnapshotCacheRoot();
            Assert.That(
                final.SetEquals(cacheSnapshotBeforeFirstLock),
                Is.True,
                "the cleanup must leave the vendor cache root equal to"
                + " its snapshot. The run snapshot holds "
                + cacheSnapshotBeforeFirstLock.Count + " entries and the"
                + " final set holds " + final.Count);
            Assert.That(
                Directory.Exists(FixtureFolder), Is.False,
                "the cleanup left the test fixture folder behind");
        }

        private static HashSet<string> SnapshotCacheRoot()
        {
            var entries = new HashSet<string>(StringComparer.Ordinal);
            if (!Directory.Exists(RealVendorLockTestSupport.CacheRoot))
            {
                return entries;
            }

            foreach (var path in Directory.GetFileSystemEntries(
                RealVendorLockTestSupport.CacheRoot, "*", SearchOption.AllDirectories))
            {
                entries.Add(path.Replace('\\', '/'));
            }

            return entries;
        }

        private static int DirectoryDepth(string path)
        {
            return path.Count(character => character == '/');
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
            double dryMs)
        {
            var totalMs = lockMs > 0 && alignMs > 0
                ? lockMs + alignMs + Math.Max(restoreMs, 0)
                : -1.0;
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "RealLockAcceptance {0} {1} {2}: genBytes {3},"
                + " lock {4}, align {5}, cloneRestore {6},"
                + " consistency {7}, dryRunAlign {8},"
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
                PhaseText(dryMs),
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
