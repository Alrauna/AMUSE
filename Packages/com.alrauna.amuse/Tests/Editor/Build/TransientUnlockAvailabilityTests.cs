using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The transient unlock window's machine-side availability. The
    /// production restore is testable without a vendor install. It
    /// reconstructs the unlocked form in memory and never resolves a
    /// vendor seam.
    /// </summary>
    public sealed class TransientUnlockAvailabilityTests
    {
        private const string BundledRowLockedStandInShaderName =
            "Hidden/Locked/Alrauna/AmuseTests/BundledRowEligibilityStandIn";

        private readonly TestTransientScope _scope =
            new TestTransientScope(TransientUnlockTestLifecycle.TempFolder);

        [SetUp]
        public void SetUp()
        {
            TransientUnlockTestLifecycle.DeleteTempFolder();
            TransientUnlockTestLifecycle.OpenFixtureScene();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
            TransientUnlockTestLifecycle.DiscardFixtureScene();
            TransientUnlockTestLifecycle.DeleteTempFolder();
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
            _scope.Track(clone);

            var restore =
                TransientUnlockAvailability.CreateProductionRestore();
            Assert.That(restore, Is.Not.Null);

            var result = restore(clone);

            Assert.That(result.Outcome,
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded));
            Assert.That(result.Detail, Is.Null,
                "a completed reconstruction carries no detail");
            Assert.That(
                clone.shader.name,
                Is.EqualTo(
                    TransientUnlockTestLifecycle
                        .FixtureOriginalShaderName),
                "the reconstruction must rebind the recorded original " +
                "shader");
            Assert.That(
                clone.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f));
        }

        /// <summary>
        /// Y12 of the 2026-10-25 bundled locked original admission
        /// design, stage 6, rewritten for stage 7. The fixture carries
        /// the recognized locked identity, and its identity tags name
        /// the bundled admitted row. The row admits locked originals
        /// since the stage 7 flag change of 2026-10-30, so the material
        /// makes the window eligible. The eligibility gate is the only
        /// consent gate, so the true answer also pins the coupling.
        /// </summary>
        [Test]
        public void BundledRowLockedMaterialMakesTheWindowEligibleAfterAdmission()
        {
            var material = NewBundledRowLockedMaterial();

            Assert.That(
                TransientUnlockAvailability.WindowEligibleForConsent(
                    new[] { material },
                    LockedMaterialIdentity.OriginalShaderAttested),
                Is.True,
                "the bundled rows admit locked originals since stage 7 " +
                "of the 2026-10-25 bundled locked original admission " +
                "design, flipped on 2026-10-30");
        }

        /// <summary>
        /// The bundled row locked fixture material. The identity tags
        /// carry the bundled row's name and GUID, so the recorded
        /// original shader resolves by the pinned row GUID to the
        /// installed bundled file. The material's own shader needs the
        /// locked name prefix, and no in-memory shader can carry one,
        /// so the fixture writes one scratch stand-in shader asset. The
        /// scope deletes the folder with the asset and its meta after
        /// the test.
        /// </summary>
        private Material NewBundledRowLockedMaterial()
        {
            var row = PoiyomiSourceAttestation.AdmittedSources().Single(
                candidate =>
                    candidate.ShaderName ==
                        ".poiyomi/Old Versions/9.3/Poiyomi Toon");
            var assetPath = AssetDatabase.GUIDToAssetPath(row.AssetGuid);
            var bundledShader = string.IsNullOrEmpty(assetPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            if (bundledShader == null)
            {
                Assert.Ignore(
                    "the bundled 9.3 shader asset does not resolve from " +
                    "its pinned row GUID. Install the Poiyomi package to " +
                    "run this pin");
            }

            var lockedShader =
                Shader.Find(BundledRowLockedStandInShaderName);
            if (lockedShader == null)
            {
                _scope.EnsureTempFolder();
                lockedShader = TestShaderWriter.WriteStandInShader(
                    TransientUnlockTestLifecycle.TempFolder +
                    "/bundled_row_eligibility_stand_in.shader",
                    BundledRowLockedStandInShaderName,
                    "_ShaderOptimizerEnabled " +
                    "(\"Shader Optimizer Enabled\", Float) = 0");
            }

            var material = _scope.Track(new Material(lockedShader));
            material.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                row.ShaderName);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                row.AssetGuid);

            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(material),
                Is.True,
                "the fixture must carry the recognized locked identity, " +
                "or the pin proves nothing");

            return material;
        }
    }
}
