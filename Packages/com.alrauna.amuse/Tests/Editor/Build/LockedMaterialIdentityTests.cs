using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;

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

        private readonly TestTransientScope _scope =
            new TestTransientScope();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            _scope.TearDown();
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            return _scope.Track(obj);
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

        // --- Locked-original attestation (K8) ---------------------------

        /// <summary>
        /// Builds the evidence of one admitted row.
        /// </summary>
        private static PoiyomiSourceEvidence EvidenceOf(
            PoiyomiAdmittedSource row)
        {
            return new PoiyomiSourceEvidence(
                row.ShaderName,
                isLocked: false,
                hasReadableSource: true,
                assetGuid: row.AssetGuid,
                normalizedSourceHash: row.NormalizedSourceHash,
                hasRequiredSchema: true);
        }

        private static PoiyomiAdmittedSource ReleaseMainRow()
        {
            return PoiyomiSourceAttestation.AdmittedSources().Single(
                row => !row.IsTwoPass && row.LockedOriginalAdmitted);
        }

        private static PoiyomiAdmittedSource BundledMainRow()
        {
            return PoiyomiSourceAttestation.AdmittedSources().Single(
                row => !row.IsTwoPass && !row.LockedOriginalAdmitted);
        }

        [Test]
        public void ReleaseMainRowWithMatchingTagAttestsTheLockedOriginal()
        {
            var row = ReleaseMainRow();

            Assert.That(
                LockedMaterialIdentity.AttestsLockedOriginal(
                    row.ShaderName, EvidenceOf(row)),
                Is.True,
                "the 9.3.64 main row with a matching name tag is the " +
                "admitted locked original");
        }

        // --- Falsifier 7: a precondition that reuses the identity gate alone ---
        // The bundled rows attest through the identity gate now, so this
        // falsifier guards the row admission rule itself.
        [Test]
        public void BundledRowRefusesAsALockedOriginal()
        {
            var row = BundledMainRow();

            // No-op guard: the bundled row evidence must pass the identity
            // gate. Without this guard a broken identity gate would let the
            // refusal assertion pass while proving nothing.
            Assert.That(
                PoiyomiSourceAttestation.TryVerifyPoiyomiIdentity(
                    EvidenceOf(row), out _),
                Is.True,
                "the bundled row evidence must pass the identity gate, " +
                "or this falsifier proves nothing");

            Assert.That(
                LockedMaterialIdentity.AttestsLockedOriginal(
                    row.ShaderName, EvidenceOf(row)),
                Is.False,
                "a bundled row records no locked original admission, so " +
                "it refuses as a locked original");
        }

        // --- Falsifier 8: a precondition that ignores the recorded original name ---
        [Test]
        public void ReleaseMainEvidenceWithAnotherAdmittedTagRefuses()
        {
            // The tag names the 9.3.64 Two Pass shader. The resolved
            // evidence is the 9.3.64 main row. An implementation that
            // ignores the tag accepts this pair. The swap-in would then
            // refuse the restore it promised.
            var resolved = EvidenceOf(ReleaseMainRow());

            Assert.That(
                LockedMaterialIdentity.AttestsLockedOriginal(
                    PoiyomiSourceAttestation.PoiyomiTwoPassShaderName,
                    resolved),
                Is.False,
                "the name tag must equal the resolved shader name, so a " +
                "tag that names another admitted shader refuses");
        }

        [TestCase(null)]
        [TestCase("")]
        public void MissingOriginalShaderTagRefuses(string tag)
        {
            // The resolved evidence is the 9.3.64 main row. An absent or
            // empty tag names no original shader, so it never attests.
            var resolved = EvidenceOf(ReleaseMainRow());

            Assert.That(
                LockedMaterialIdentity.AttestsLockedOriginal(tag, resolved),
                Is.False,
                "an absent or empty name tag never attests a locked " +
                "original");
        }
    }
}
