using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;

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
        private readonly List<UnityEngine.Object> _tracked =
            new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
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
            Thry.ThryEditor.ShaderOptimizer.Reset();
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
                Is.EqualTo(TransientUnlockRestoreOutcome.Succeeded));
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
