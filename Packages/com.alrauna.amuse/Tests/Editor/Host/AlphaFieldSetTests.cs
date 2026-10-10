using System;
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
            var evidence = EvidenceNamingAlphaSource(sourceId, chain);
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
        /// Evidence whose only texture assignment names
        /// <paramref name="source"/> with an alpha chain captured exact
        /// under the inert bounds, so the gate's own lookup would answer
        /// with the field when no guard refuses first.
        /// </summary>
        private static CapturedMaterialEvidence EvidenceNamingAlphaSource(
            TextureSourceId source, AlphaMipChain chain)
        {
            var texture = new CapturedTextureEvidence(
                true, source, 1f, AlphaPolicyBounds.Inert,
                false, default(TextureSampling),
                false, default(TextureColorInterpretation),
                false, false,
                false, true, chain,
                TextureCaptureRefusalReason.None,
                false, null,
                TextureCaptureRefusalReason.None);
            var assignment = new CapturedTextureAssignment(
                true, TextureEvidenceKinds.AlphaChannel,
                false, Vector2.one, Vector2.zero,
                texture, Array.Empty<TextureCaptureRefusal>());
            return new CapturedMaterialEvidence(
                false, null, false, default(ColorSpace),
                Array.Empty<CapturedMaterialEvidence.PresenceEntry>(),
                Array.Empty<CapturedMaterialEvidence.ScalarEntry>(),
                Array.Empty<CapturedMaterialEvidence.ColorEntry>(),
                Array.Empty<CapturedMaterialEvidence.VectorEntry>(),
                new[]
                {
                    new CapturedMaterialEvidence.TextureEntry(
                        "_MainTex", true, assignment),
                },
                new[] { texture });
        }
    }
}
