using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The locked identity classifier. The two falsifiers here are F7's two
    /// directions: stale identity tags on an unlocked material are never
    /// lock evidence, and removed tags never strip a locked material of its
    /// lock. Each one kills one named wrong implementation, so the classifier
    /// must read both required signals.
    /// </summary>
    public sealed class LockedMaterialIdentityTests
    {
        [Test]
        public void UnlockedMaterialWithStaleTagsIsNotLocked()
        {
            // F7, first direction. The shader name still carries the locked
            // prefix and the serialization still carries stale Thry tags,
            // but the optimizer flag is off. A classifier that reads the
            // name prefix alone reads this material as locked. That is
            // exactly the misidentification this falsifier kills.
            var identity = LockedMaterialIdentity.Classify(
                new LockedMaterialIdentity.Serialization(
                    LockedMaterialIdentity.LockedShaderNamePrefix +
                        ".poiyomi/Poiyomi Toon",
                    hasOptimizerEnabled: true,
                    optimizerEnabled: 0f,
                    originalShaderTag: ".poiyomi/Poiyomi Toon",
                    originalShaderGuidTag: "0123456789abcdef0123456789abcdef"));

            Assert.That(
                identity.IsLocked,
                Is.False,
                "stale tags are never lock evidence while the optimizer " +
                "flag is off");
        }

        [Test]
        public void LockedMaterialWithRemovedTagsIsStillLocked()
        {
            // F7, second direction. Both required signals hold, and every
            // identity tag is gone. A classifier that reads the tags alone
            // refuses this material its lock. That is the opposite
            // misidentification this falsifier kills.
            var identity = LockedMaterialIdentity.Classify(
                new LockedMaterialIdentity.Serialization(
                    LockedMaterialIdentity.LockedShaderNamePrefix +
                        ".poiyomi/Poiyomi Toon",
                    hasOptimizerEnabled: true,
                    optimizerEnabled: 1f,
                    originalShaderTag: null,
                    originalShaderGuidTag: null));

            Assert.That(
                identity.IsLocked,
                Is.True,
                "the locked name prefix plus the optimizer flag at one is " +
                "the lock, with or without tags");
        }

        [Test]
        public void LockedIdentityRecordsTheOriginalShaderFacts()
        {
            // The classification records the two identity tags verbatim,
            // because later increments restore against exactly these
            // facts.
            var identity = LockedMaterialIdentity.Classify(
                new LockedMaterialIdentity.Serialization(
                    LockedMaterialIdentity.LockedShaderNamePrefix +
                        ".poiyomi/Poiyomi Toon",
                    hasOptimizerEnabled: true,
                    optimizerEnabled: 1f,
                    originalShaderTag: ".poiyomi/Poiyomi Toon",
                    originalShaderGuidTag: "0123456789abcdef0123456789abcdef"));

            Assert.That(identity.IsLocked, Is.True);
            Assert.That(
                identity.OriginalShader, Is.EqualTo(".poiyomi/Poiyomi Toon"));
            Assert.That(
                identity.OriginalShaderGuid,
                Is.EqualTo("0123456789abcdef0123456789abcdef"));
        }

        [Test]
        public void UnlockedMaterialWithoutTagsRecordsNulls()
        {
            // Absent tags stay absent on the record. Nothing fabricates an
            // original shader the serialization never named.
            var identity = LockedMaterialIdentity.Classify(
                new LockedMaterialIdentity.Serialization(
                    "Unlit/Color",
                    hasOptimizerEnabled: false,
                    optimizerEnabled: 0f,
                    originalShaderTag: null,
                    originalShaderGuidTag: null));

            Assert.That(identity.IsLocked, Is.False);
            Assert.That(identity.OriginalShader, Is.Null);
            Assert.That(identity.OriginalShaderGuid, Is.Null);
        }

        // --- Attestation and pre-check on unverified original shaders -----

        private const string LockedStandInShaderName =
            "Hidden/Locked/Alrauna/AmuseTests/LockedStandIn";

        private readonly System.Collections.Generic.List<UnityEngine.Object>
            _owned = new System.Collections.Generic.List<UnityEngine.Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (var value in _owned)
            {
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            }

            _owned.Clear();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            _owned.Add(obj);
            return obj;
        }

        [Test]
        public void LockedPoiyomi_UnverifiedOriginalShaderAlwaysRefuses()
        {
            var shader = Shader.Find(LockedStandInShaderName) ??
                Shader.Find("Unlit/Color");
            var material = Track(new Material(shader));
            material.shader = shader;
            material.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                ".poiyomi/Old Versions/9.0/Poiyomi Toon");
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                "0123456789abcdef0123456789abcdef");

            var attests = LockedMaterialIdentity.OriginalShaderAttested(material);
            var refusal = LockedMaterialIdentity.PreCheckRefusal(material);

            Assert.That(attests, Is.False);
            Assert.That(
                refusal,
                Is.EqualTo(RendererAnalysisRefusal.LockedPoiyomiOriginalShaderUnattested));
        }
    }
}
