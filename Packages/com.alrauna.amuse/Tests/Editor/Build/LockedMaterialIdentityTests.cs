using NUnit.Framework;
using Alrauna.Amuse.Editor.Build;

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

        // --- The D8 grant composition on the original-attestation gate -----

        private const string LockedStandInShaderName =
            "Hidden/Locked/Alrauna/AmuseTests/LockedStandIn";
        private const string GrantedOriginalName =
            ".poiyomi/Old Versions/9.0/Poiyomi Toon";

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

        /// <summary>
        /// A live locked material over the committed locked stand-in: the
        /// locked name prefix, the optimizer flag at exactly one, and an
        /// original-shader override tag naming the granted name. The
        /// recorded GUID is deliberately unresolvable, so the base
        /// attestation answers false and only the grant can answer true.
        /// </summary>
        private UnityEngine.Material LockedFixture()
        {
            var shader = UnityEngine.Shader.Find(LockedStandInShaderName);
            Assert.That(shader, Is.Not.Null,
                "fixture precondition: the committed locked stand-in " +
                "shader must load");
            var material = new UnityEngine.Material(shader);
            _owned.Add(material);
            material.SetFloat(
                LockedMaterialIdentity.OptimizerEnabledPropertyName, 1f);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderTagName,
                GrantedOriginalName);
            material.SetOverrideTag(
                LockedMaterialIdentity.OriginalShaderGuidTagName,
                "0123456789abcdef0123456789abcdef");
            Assert.That(
                LockedMaterialIdentity.RecognizedLockedIdentity(material),
                Is.True,
                "fixture precondition: the fixture must classify locked");
            return material;
        }

        [Test]
        public void GrantedAwareAttestation_WithoutGrant_StaysWithTheBasePredicate()
        {
            var locked = LockedFixture();
            System.Func<UnityEngine.Material, bool> baseAttestsFalse =
                _ => false;

            var withNullGrant = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(baseAttestsFalse, null);
            var withEmptyGrant = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(
                    baseAttestsFalse, new string[0]);

            // The original stand-in is unattested by construction, so a
            // base predicate answering false is the whole truth. A null or
            // empty grant set must keep that base behavior unchanged.
            Assert.That(withNullGrant(locked), Is.False);
            Assert.That(withEmptyGrant(locked), Is.False);
        }

        [Test]
        public void GrantedAwareAttestation_GrantedRecordedOriginal_Attests()
        {
            var locked = LockedFixture();

            var predicate = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(
                    null,
                    new[] { GrantedOriginalName });

            // The base predicate alone refuses this lock: the recorded
            // original is unattested. The grant for exactly the recorded
            // original name is the answered risk the build accepted.
            Assert.That(
                LockedMaterialIdentity.OriginalShaderAttested(locked),
                Is.False,
                "fixture precondition: the base attestation must refuse");
            Assert.That(predicate(locked), Is.True);
        }

        [Test]
        public void GrantedAwareAttestation_GrantMustMatchTheRecordedOriginal()
        {
            var locked = LockedFixture();

            var predicate = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(
                    null,
                    new[] { ".poiyomi/Poiyomi Toon" });

            // A grant for a different name must not open this lock: the
            // composition reads the serialization's recorded original, not
            // the live shader name or any other string.
            Assert.That(predicate(locked), Is.False);
        }

        [Test]
        public void GrantedAwareAttestation_BasePredicateStillAnswersFirst()
        {
            var locked = LockedFixture();
            System.Func<UnityEngine.Material, bool> baseAttestation =
                LockedMaterialIdentity.OriginalShaderAttested;

            var predicate = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(
                    baseAttestation,
                    new System.Collections.Generic.List<string>());

            Assert.That(predicate, Is.SameAs(baseAttestation),
                "an empty grant set must hand back the base predicate " +
                "itself, not a wrapper around it");
        }

        [Test]
        public void WindowEligibility_GrantedOriginal_OpensAndUngrantedStaysClosed()
        {
            var locked = LockedFixture();
            var grantedPredicate = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(
                    null,
                    new[] { GrantedOriginalName });
            var ungrantedPredicate = LockedMaterialIdentity
                .GrantedAwareOriginalAttestation(null, null);

            Assert.That(
                TransientUnlockAvailability.WindowEligibleForConsent(
                    new[] { locked }, grantedPredicate),
                Is.True,
                "the granted original must open the unlock window");
            Assert.That(
                TransientUnlockAvailability.WindowEligibleForConsent(
                    new[] { locked }, ungrantedPredicate),
                Is.False,
                "without the grant the window must stay closed");
        }
    }
}
