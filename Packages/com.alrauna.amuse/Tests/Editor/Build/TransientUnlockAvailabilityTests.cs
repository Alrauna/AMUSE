using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The transient unlock window's machine-side availability. The
    /// attestation rule is testable without a vendor install, because
    /// this repository installs no vendor shader, so the locator sees
    /// zero ShaderOptimizer MonoScripts and the fail-closed answer is
    /// the observed one. The production restore reconstructs the
    /// unlocked form in memory and never resolves a vendor seam.
    /// </summary>
    public sealed class TransientUnlockAvailabilityTests
    {
        private readonly List<UnityEngine.Object> _tracked =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            TransientUnlockAvailability.ClearCache();
            Thry.ThryEditor.ShaderOptimizer.Reset();
            TransientUnlockTestLifecycle.OpenFixtureScene();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var tracked in _tracked)
            {
                if (tracked != null)
                {
                    UnityEngine.Object.DestroyImmediate(tracked);
                }
            }

            _tracked.Clear();
            TransientUnlockTestLifecycle.DiscardFixtureScene();
            TransientUnlockAvailability.ClearCache();
            Thry.ThryEditor.ShaderOptimizer.Reset();
        }

        [Test]
        public void ThePinnedDigestTableCarriesTheTwoRecordedToolDigests()
        {
            // The two values were recorded on 2026-09-21 from freshly
            // fetched vendor archives whose archive SHA-256 matched the
            // pins in the 2026-09-19 characterization. First the
            // Editor/ShaderOptimizer.cs embedded in com.poiyomi.toon
            // 9.3.64, then the one shipped in com.poiyomi.thryeditor
            // 2.74.2. The literals are pinned exactly, so any table edit
            // is a visible decision, as for the lilToon source pins.
            Assert.That(
                TransientUnlockAvailability.PinnedSourceDigests,
                Is.EqualTo(new[]
                {
                    "9000377ab486863e20d25b8bd025863c7590354ea3cdb1a5ca493d8e5045f3e5",
                    "7c1ffe78c872288ec605b5bd742467401c952562701aa171fead0df4ae1883ee",
                }));
            // This repository installs no vendor shader, so the locator
            // sees zero ShaderOptimizer MonoScripts and the machine-side
            // answer stays fail closed here.
            Assert.That(
                TransientUnlockAvailability.ThrySourceAttested(),
                Is.False,
                "a machine with zero locator hits must attest nothing");
        }

        [Test]
        public void SourceDigestIsTheSha256OfTheFileBytes()
        {
            var path = Path.Combine(
                Application.temporaryCachePath,
                "amuse-unlock-digest-probe.txt");
            try
            {
                File.WriteAllText(
                    path, "amuse transient unlock digest probe\n");
                var first =
                    TransientUnlockAvailability.SourceDigestOfFile(path);
                var second =
                    TransientUnlockAvailability.SourceDigestOfFile(path);

                Assert.That(first, Is.EqualTo(
                    "1447c52fde9397b260179817f0355a5660fb2a0abf33336932d2" +
                    "02dcc3d987cb"));
                Assert.That(first, Is.EqualTo(second),
                    "the digest must be deterministic for one file");
                Assert.That(first.Length, Is.EqualTo(64));
                Assert.That(first, Is.EqualTo(first.ToLowerInvariant()));

                File.WriteAllText(path, "a different file\n");
                var changed =
                    TransientUnlockAvailability.SourceDigestOfFile(path);
                Assert.That(changed, Is.Not.EqualTo(first),
                    "a different file must not attest as the same digest");
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// The production restore reconstructs the unlocked form in
        /// memory: the clone rebinds the recorded original shader with
        /// the lock flag cleared, with no vendor install and no vendor
        /// call.
        /// </summary>
        [Test]
        public void TheProductionRestoreReconstructsTheUnlockedForm()
        {
            var clone = TransientUnlockTestLifecycle.LockedMaterial(
                "SeamProbe");
            _tracked.Add(clone);

            var restore =
                TransientUnlockAvailability.CreateProductionRestore();
            Assert.That(restore, Is.Not.Null);

            var outcome = restore(clone);

            Assert.That(outcome,
                Is.EqualTo(TransientUnlockVendorOutcome.Succeeded));
            Assert.That(
                clone.shader.name,
                Is.EqualTo(
                    TransientUnlockTestLifecycle
                        .OriginalStandInShaderName),
                "the reconstruction must rebind the recorded original " +
                "shader");
            Assert.That(
                clone.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f));
        }
    }
}
