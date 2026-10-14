using System;
using Alrauna.Amuse.Tests.Editor.Analysis;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Direct coverage of the one consumer gate over gathered alpha and
    /// red fields. The poison-guard test pins the replacement-identity
    /// contract end to end: a poisoned identity fails closed with the
    /// missing-evidence path, through the same gate every consumer uses.
    /// </summary>
    public sealed class AlphaFieldSetTests
    {
        [SetUp]
        public void StartWithACleanSession()
        {
            ReplacementTextureIdentity.ClearSession();
        }

        [TearDown]
        public void LeaveACleanSession()
        {
            ReplacementTextureIdentity.ClearSession();
        }

        [Test]
        public void PoisonedReplacementIdentityFailsClosedAtTheConsumerGate()
        {
            // Build the set through the existing WithField seam, then poison
            // the id through the ledger, then assert TryGetFor refuses.
            var sourceId = new TextureSourceId("unity-replacement:poisoned:1");
            var chain = CapturedAlphaFieldBagLevelChainOrNull();
            var set = AlphaFieldSet.WithField(
                sourceId, TextureChannel.Alpha, 1f,
                AlphaPolicyBounds.Inert, chain);
            var evidence = AdmittedMaterialStatesTests.EvidenceNamingAlphaSource(sourceId, chain);
            ReplacementTextureIdentity.ClearSession();
            Assert.That(
                set.TryGetFor(
                    evidence, null, sourceId, TextureChannel.Alpha, out _),
                Is.True,
                "the unpoisoned field answers through the gate, so the"
                + " refusal below comes from the identity guard");
            // Poison without a mint: the ledger must answer unusable for an
            // id it has seen poisoned even when this test never minted it.
            ReplacementTextureIdentity.PoisonForTests(sourceId);
            Assert.That(
                set.TryGetFor(
                    evidence, null, sourceId, TextureChannel.Alpha, out _),
                Is.False,
                "a poisoned identity fails closed with the"
                + " missing-evidence path");
        }

        [Test]
        public void ForRed_KeysTheInertBounds()
        {
            var evidence = new CapturedTextureEvidence(
                true, new TextureSourceId(), 1f, AlphaPolicyBounds.From(80, 2),
                false, default, false, default, false, false,
                false, false, null, TextureCaptureRefusalReason.None,
                true, null, TextureCaptureRefusalReason.None);

            Assert.That(
                AlphaFieldKey.ForRed(evidence).Bounds,
                Is.EqualTo(AlphaPolicyBounds.Inert));
        }

        [Test]
        public void PredicateDisagreement_FailsClosed()
        {
            // One source, two deciding assignments in one material's own
            // evidence: one captured exact under the inert bounds and one
            // binarized by a declared cutoff. That is the two-predicate
            // shape the capture side produces when a union-schema evidence
            // names one source under two families' predicates. No predicate
            // request, so both entries decide, and the gather legitimately
            // holds a field for each predicate. The lookup must refuse
            // rather than hand over either predicate's bytes under the
            // other's meaning.
            var source = new TextureSourceId(
                "amuse-tests:field-set:disagreement:1");
            var chain = CapturedAlphaFieldBagLevelChainOrNull();
            var exactKey = new AlphaFieldKey(
                source, TextureChannel.Alpha, 1f, AlphaPolicyBounds.Inert);
            var cutoffKey = new AlphaFieldKey(
                source, TextureChannel.Alpha, 0.5f, AlphaPolicyBounds.Inert);
            var set = new AlphaFieldSet(
                new Dictionary<AlphaFieldKey, AlphaMipChain>
                {
                    [exactKey] = chain,
                    [cutoffKey] = chain,
                });
            var evidence = EvidenceNamingAlphaSourceUnderTwoPredicates(
                source, chain);

            Assert.That(
                set.TryGetFor(
                    evidence, null, source, TextureChannel.Alpha,
                    out var field),
                Is.False,
                "two deciding predicates for one source must refuse, not"
                + " answer with either predicate's field");
            Assert.That(
                field, Is.Null,
                "the disagreement path hands over no field");
        }

        [Test]
        public void ChannelNotAskedByTheProperty_SkipsTheEntry()
        {
            // The cutout-family shape from the gate's contract: the request
            // names the mask property but asks only its red channel, so the
            // mask's own captured alpha must not decide an alpha lookup's
            // predicate. The two siblings hold different predicates, so an
            // entry that wrongly decided would refuse the lookup as a
            // disagreement; with the skip in place the asked main texture's
            // own field answers.
            var source = new TextureSourceId(
                "amuse-tests:field-set:unasked-channel:1");
            var chain = CapturedAlphaFieldBagLevelChainOrNull();
            var set = AlphaFieldSet.WithField(
                source, TextureChannel.Alpha, 0.5f,
                AlphaPolicyBounds.Inert, chain);
            var evidence = EvidenceNamingAskedAndUnaskedAlphaSiblings(
                source, chain);
            var request = new MaterialEvidenceRequest(
                false, false,
                Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<string>(), Array.Empty<string>(),
                new[]
                {
                    new TexturePropertyEvidenceRequest(
                        "_MainTex", TextureEvidenceKinds.AlphaChannel),
                    new TexturePropertyEvidenceRequest(
                        "_AlphaMask", TextureEvidenceKinds.RedChannel),
                });

            Assert.That(
                set.TryGetFor(
                    evidence, request, source, TextureChannel.Alpha,
                    out var field),
                Is.True,
                "the asked main texture's field answers; the unasked mask"
                + " sibling must not decide the predicate");
            Assert.That(
                field, Is.SameAs(chain),
                "the answering field is the asked entry's own field");
        }

        [Test]
        public void MalformedLookups_Throw()
        {
            // A null evidence and a channel outside the alpha and red arms
            // are caller defects, not domain outcomes: the gate throws
            // instead of answering, because silence would hide the defect.
            var source = new TextureSourceId(
                "amuse-tests:field-set:malformed:1");
            var chain = CapturedAlphaFieldBagLevelChainOrNull();
            var set = AlphaFieldSet.WithField(
                source, TextureChannel.Alpha, 1f,
                AlphaPolicyBounds.Inert, chain);
            var evidence = AdmittedMaterialStatesTests.EvidenceNamingAlphaSource(source, chain);

            Assert.That(
                () => set.TryGetFor(
                    null, null, source, TextureChannel.Alpha, out _),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => set.TryGetFor(
                    evidence, null, source, (TextureChannel)99, out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /// <summary>
        /// One mip of fully opaque texels, the same shape the captured
        /// field bag hands consumers for an all-opaque source. Returns a
        /// chain, never null, when construction succeeds.
        /// </summary>
        private static AlphaMipChain CapturedAlphaFieldBagLevelChainOrNull()
        {
            return new AlphaMipChain(new[]
            {
                new AlphaTextureData(
                    2, 2, new byte[] { 255, 255, 255, 255 }),
            });
        }


        /// <summary>
        /// Evidence whose two deciding texture assignments name
        /// <paramref name="source"/> with alpha chains captured under two
        /// different predicates: one exact under the inert bounds and one
        /// binarized by a declared cutoff, which the capture keeps
        /// policy-inert. The union-schema shape one material's evidence can
        /// carry when two families' predicates name the same source.
        /// </summary>
        private static CapturedMaterialEvidence
            EvidenceNamingAlphaSourceUnderTwoPredicates(
                TextureSourceId source, AlphaMipChain chain)
        {
            var exact = new CapturedTextureEvidence(
                true, source, 1f, AlphaPolicyBounds.Inert,
                false, default(TextureSampling),
                false, default(TextureColorInterpretation),
                false, false,
                false, true, chain,
                TextureCaptureRefusalReason.None,
                false, null,
                TextureCaptureRefusalReason.None);
            var cutoff = new CapturedTextureEvidence(
                true, source, 0.5f, AlphaPolicyBounds.Inert,
                false, default(TextureSampling),
                false, default(TextureColorInterpretation),
                false, false,
                false, true, chain,
                TextureCaptureRefusalReason.None,
                false, null,
                TextureCaptureRefusalReason.None);
            var exactAssignment = new CapturedTextureAssignment(
                true, TextureEvidenceKinds.AlphaChannel,
                false, Vector2.one, Vector2.zero,
                exact, Array.Empty<TextureCaptureRefusal>());
            var cutoffAssignment = new CapturedTextureAssignment(
                true, TextureEvidenceKinds.AlphaChannel,
                false, Vector2.one, Vector2.zero,
                cutoff, Array.Empty<TextureCaptureRefusal>());
            return new CapturedMaterialEvidence(
                false, null, false, default(ColorSpace),
                Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                Array.Empty<CapturedMaterialEvidence.ScalarEntry>(),
                Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                new[]
                {
                    new CapturedMaterialEvidence.TextureEntry(
                        "_MainTex", true, exactAssignment),
                    new CapturedMaterialEvidence.TextureEntry(
                        "_AlphaMask", true, cutoffAssignment),
                },
                new[] { exact, cutoff });
        }

        /// <summary>
        /// Evidence naming one source twice: the main texture captured under
        /// its declared cutoff, and the mask sibling captured exact with its
        /// own alpha chain, as a union-schema capture does. The request for
        /// the lookup names the mask but asks only its red channel, so the
        /// mask's alpha side must stay out of the predicate decision.
        /// </summary>
        private static CapturedMaterialEvidence
            EvidenceNamingAskedAndUnaskedAlphaSiblings(
                TextureSourceId source, AlphaMipChain chain)
        {
            var main = new CapturedTextureEvidence(
                true, source, 0.5f, AlphaPolicyBounds.Inert,
                false, default(TextureSampling),
                false, default(TextureColorInterpretation),
                false, false,
                false, true, chain,
                TextureCaptureRefusalReason.None,
                false, null,
                TextureCaptureRefusalReason.None);
            var mask = new CapturedTextureEvidence(
                true, source, 1f, AlphaPolicyBounds.Inert,
                false, default(TextureSampling),
                false, default(TextureColorInterpretation),
                false, false,
                false, true, chain,
                TextureCaptureRefusalReason.None,
                false, null,
                TextureCaptureRefusalReason.None);
            var mainAssignment = new CapturedTextureAssignment(
                true, TextureEvidenceKinds.AlphaChannel,
                false, Vector2.one, Vector2.zero,
                main, Array.Empty<TextureCaptureRefusal>());
            var maskAssignment = new CapturedTextureAssignment(
                true, TextureEvidenceKinds.AlphaChannel,
                false, Vector2.one, Vector2.zero,
                mask, Array.Empty<TextureCaptureRefusal>());
            return new CapturedMaterialEvidence(
                false, null, false, default(ColorSpace),
                Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                Array.Empty<CapturedMaterialEvidence.ScalarEntry>(),
                Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                new[]
                {
                    new CapturedMaterialEvidence.TextureEntry(
                        "_MainTex", true, mainAssignment),
                    new CapturedMaterialEvidence.TextureEntry(
                        "_AlphaMask", true, maskAssignment),
                },
                new[] { main, mask });
        }
    }
}
