using System;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Pins the mirrored AMUSE vocabulary. These enums are a snapshot taken at
    /// the commit that introduced them, never a live view of AMUSE, so any edit
    /// to one must be a deliberate schema decision rather than a silent
    /// absorption of a new production value. Failing here is the intended way
    /// to force that decision into review.
    /// <para>
    /// The matching parity check against AMUSE's own enums belongs to the
    /// collector increment, where the friend grant makes them visible at
    /// compile time. Until then drift is undetected, which is acceptable only
    /// because nothing consumes these records yet.
    /// </para>
    /// </summary>
    public sealed class CensusCategorySnapshotTests
    {
        [Test]
        public void RendererRefusalMirrorsAmuseVocabulary()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "None",
                    "UnsupportedRendererType",
                    "MaterialPropertyOverridesPresent",
                    "MissingMesh",
                    "UnprovenMaterialSlotMapping",
                    "UnsupportedTopology",
                    "MalformedMeshData",
                },
                Enum.GetNames(typeof(RendererRefusal)));
        }

        [Test]
        public void AlphaResolutionFailureMirrorsAmuseVocabulary()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "None",
                    "SemanticsUnknown",
                    "UnsupportedMultiplier",
                    "UnsupportedUvMapping",
                    "UnsupportedSampling",
                    "MissingTextureEvidence",
                },
                Enum.GetNames(typeof(AlphaResolutionFailure)));
        }

        [Test]
        public void SeparationDispositionMirrorsAmuseVocabulary()
        {
            CollectionAssert.AreEqual(
                new[] { "Unchanged", "WhollyOpaqueCandidate", "Split" },
                Enum.GetNames(typeof(SeparationDisposition)));
        }

        [Test]
        public void ShaderFamilyAttestationCoversTheAttestedFrontendsAndNothingElse()
        {
            CollectionAssert.AreEqual(
                new[] { "None", "Poiyomi", "LilToon" },
                Enum.GetNames(typeof(ShaderFamilyAttestation)));
        }

        [Test]
        public void RendererKindCollapsesUnsupportedTypesIntoOther()
        {
            CollectionAssert.AreEqual(
                new[] { "Other", "MeshRenderer", "SkinnedMeshRenderer" },
                Enum.GetNames(typeof(RendererKind)));
        }
    }
}
