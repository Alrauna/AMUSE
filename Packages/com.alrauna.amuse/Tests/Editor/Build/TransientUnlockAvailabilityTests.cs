using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The transient unlock window's machine-side availability. The
    /// attestation rule is testable without a vendor install, because the
    /// pinned digest table is deliberately empty until the lab session
    /// records the two pin values, so the fail-closed answer is the
    /// observed one. The seam and the production delegates resolve against
    /// the stand-in vendor type, whose declaring full name and method
    /// shapes mirror the live-verified vendor signatures.
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
        public void ThePinnedDigestTableStaysEmptyUntilTheLabRecording()
        {
            // The pin values await recording against the verified vendor
            // archives in the lab session. Fail closed until then: an
            // empty table attests nothing, so every recognized locked
            // material refuses by name before any clone exists. Recording
            // a digest is a reviewed change to this one table, and this
            // guard is the friction that makes it a decision.
            Assert.That(
                TransientUnlockAvailability.PinnedSourceDigests,
                Is.Empty);
            Assert.That(
                TransientUnlockAvailability.ThrySourceAttested(),
                Is.False,
                "an empty pinned digest table must attest nothing");
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

        [Test]
        public void TheSeamResolvesOnlyMethodsWithTheVerifiedShape()
        {
            // The stand-in vendor type carries the verified two-method
            // shape plus a wrong-shape decoy sharing the lock name, so a
            // resolution that picked by name order alone would fail here.
            Assert.That(
                TransientUnlockAvailability.SeamResolved(),
                Is.True,
                "the stand-in vendor type must resolve by full name and " +
                "pass the verified shape check");
        }

        [Test]
        public void TheProductionRestoreRunsTheRealSeamPath()
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
                "the stand-in vendor restore must rebind the recorded " +
                "original shader");
            Assert.That(
                clone.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f));
        }

        [Test]
        public void AVendorThrowBecomesANamedFailureNotAnException()
        {
            Thry.ThryEditor.ShaderOptimizer.CurrentMode =
                Thry.ThryEditor.ShaderOptimizer.Mode.ThrowOnRelock;
            var clone = new Material(Shader.Find(
                TransientUnlockTestLifecycle.OriginalStandInShaderName));

            var relock =
                TransientUnlockAvailability.CreateProductionRelock();
            TransientUnlockVendorOutcome outcome = 0;
            Assert.DoesNotThrow(
                () => outcome = relock(new Material[] { clone }),
                "a vendor exception is a named vendor failure, never a " +
                "reason to abort the window");
            Assert.That(outcome,
                Is.EqualTo(TransientUnlockVendorOutcome.Failed));
        }

        [Test]
        public void TheRendererPreCheckNamesOnlyEligibleLockedMaterials()
        {
            var root = new GameObject(
                "AMUSE thry pre-check fixture");
            var renderer = root.AddComponent<MeshRenderer>();
            try
            {
                // An unlocked material never trips the Thry precondition,
                // even with stale tags and the locked name prefix.
                renderer.sharedMaterials = new[]
                {
                    TransientUnlockTestLifecycle.StaleTaggedUnlockedMaterial(),
                };
                Assert.That(
                    TransientUnlockAvailability.RendererPreCheckRefusal(
                        renderer, _ => true),
                    Is.EqualTo(RendererAnalysisRefusal.None));

                // A locked material whose original does not attest keeps
                // the earlier named refusal of the selection pre-check,
                // so the Thry precondition answers None here.
                renderer.sharedMaterials = new[]
                {
                    TransientUnlockTestLifecycle.LockedMaterial("Cape"),
                };
                Assert.That(
                    TransientUnlockAvailability.RendererPreCheckRefusal(
                        renderer, _ => false),
                    Is.EqualTo(RendererAnalysisRefusal.None));

                // A locked material with an attested original on this
                // machine, whose pinned digest table attests nothing,
                // refuses by name before any clone exists.
                Assert.That(
                    TransientUnlockAvailability.RendererPreCheckRefusal(
                        renderer, _ => true),
                    Is.EqualTo(RendererAnalysisRefusal
                        .LockedPoiyomiThryUnattested));
            }
            finally
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
