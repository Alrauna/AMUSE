using System;
using System.Collections.Generic;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Tier 2 construction. The anonymizer is the only supported producer of
    /// these records, but they are public and could be rebuilt by a later
    /// reader, so the states that would corrupt an aggregate must be
    /// unrepresentable rather than merely unproduced.
    /// </summary>
    public sealed class AnonymizedRecordTests
    {
        private static AnonymizedSubmesh Submesh(
            bool hasMaterial,
            string materialId,
            string shaderFamily)
        {
            return new AnonymizedSubmesh(
                0,
                0,
                hasMaterial,
                materialId,
                shaderFamily,
                AlphaResolutionFailure.None,
                SeparationDisposition.Unchanged,
                0,
                0,
                0,
                0);
        }

        [Test]
        public void ASubmeshWithAMaterialRequiresAnIdentity()
        {
            Assert.Throws<ArgumentException>(
                () => Submesh(true, null, "Poiyomi"));
        }

        [Test]
        public void ASubmeshWithAMaterialRequiresAShaderFamily()
        {
            Assert.Throws<ArgumentException>(
                () => Submesh(true, "Material-01-001", null));
        }

        [Test]
        public void ASubmeshWithoutAMaterialRejectsAFabricatedIdentity()
        {
            // An empty slot has no material. Recording one would invent a
            // distinct material that does not exist and inflate every count
            // derived from material identity.
            Assert.Throws<ArgumentException>(
                () => Submesh(false, "Material-01-001", null));
        }

        [Test]
        public void ASubmeshWithoutAMaterialRejectsAShaderFamily()
        {
            Assert.Throws<ArgumentException>(
                () => Submesh(false, null, "Poiyomi"));
        }

        [Test]
        public void AnEmptySlotIsRepresentableWithNeither()
        {
            var submesh = Submesh(false, null, null);

            Assert.IsFalse(submesh.HasMaterial);
            Assert.IsNull(submesh.MaterialId);
            Assert.IsNull(submesh.ShaderFamily);
        }

        [Test]
        public void ARendererCopiesItsSubmeshListDefensively()
        {
            var mutable = new List<AnonymizedSubmesh> { Submesh(false, null, null) };
            var renderer = new AnonymizedRenderer(
                "Renderer-01-001",
                RendererKind.MeshRenderer,
                RendererRefusal.None,
                1,
                0,
                mutable);

            mutable.Add(Submesh(false, null, null));

            Assert.AreEqual(1, renderer.Submeshes.Count);
        }

        [Test]
        public void AnAvatarCopiesItsRendererListDefensively()
        {
            var renderer = new AnonymizedRenderer(
                "Renderer-01-001",
                RendererKind.MeshRenderer,
                RendererRefusal.MissingMesh,
                null,
                null,
                new AnonymizedSubmesh[0]);
            var mutable = new List<AnonymizedRenderer> { renderer };
            var avatar = new AnonymizedAvatar("Avatar-01", mutable);

            mutable.Add(renderer);

            Assert.AreEqual(1, avatar.Renderers.Count);
        }

        [Test]
        public void ACensusCopiesItsAvatarListDefensively()
        {
            var avatar = new AnonymizedAvatar("Avatar-01", new AnonymizedRenderer[0]);
            var mutable = new List<AnonymizedAvatar> { avatar };
            var census = new AnonymizedCensus(mutable);

            mutable.Add(avatar);

            Assert.AreEqual(1, census.Avatars.Count);
        }

        [Test]
        public void AReportCopiesItsCategoryDictionariesDefensively()
        {
            var refusals = new Dictionary<RendererRefusal, int>
            {
                { RendererRefusal.None, 1 },
            };
            var report = CensusAggregator.Aggregate(
                new AnonymizedCensus(new AnonymizedAvatar[0]));

            // The report exposes read-only views, so a caller cannot reach in
            // and rewrite a published distribution.
            Assert.Throws<NotSupportedException>(
                () => ((IDictionary<RendererRefusal, int>)report.RendererCountByRefusal)
                    .Add(RendererRefusal.MissingMesh, 5));
            Assert.AreEqual(1, refusals.Count);
        }
    }
}
