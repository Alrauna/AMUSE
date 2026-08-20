using System;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// The anonymizer's observable contract: ordinal identity, avatar scoping,
    /// and faithful pass-through of everything that is not an identifier.
    /// </summary>
    public sealed class CensusAnonymizerTests
    {
        private static ObservedSubmesh Material(
            string name,
            string shader,
            ShaderFamilyAttestation attestation = ShaderFamilyAttestation.None,
            int slot = 0)
        {
            return Submesh(
                submeshIndex: slot,
                materialSlotIndex: slot,
                materialName: name,
                materialAssetPath: "Assets/" + name + ".mat",
                materialAssetGuid: name + "-guid",
                shaderName: shader,
                attestation: attestation,
                provenOpaque: 1);
        }

        [Test]
        public void AvatarsAreNumberedInInputOrder()
        {
            var result = CensusAnonymizer.Anonymize(Set(
                Avatar(avatarName: "first"),
                Avatar(avatarName: "second")));

            Assert.AreEqual("Avatar-01", result.Avatars[0].Id);
            Assert.AreEqual("Avatar-02", result.Avatars[1].Id);
        }

        [Test]
        public void RendererIdentityIsScopedToItsAvatar()
        {
            var result = CensusAnonymizer.Anonymize(Set(
                Avatar(renderers: new[] { Renderer(), Renderer() }),
                Avatar(renderers: new[] { Renderer() })));

            Assert.AreEqual("Renderer-01-001", result.Avatars[0].Renderers[0].Id);
            Assert.AreEqual("Renderer-01-002", result.Avatars[0].Renderers[1].Id);
            Assert.AreEqual("Renderer-02-001", result.Avatars[1].Renderers[0].Id);
        }

        [Test]
        public void OneMaterialUsedTwiceInAnAvatarKeepsOneIdentity()
        {
            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Material("skin", "Shader"),
                    Material("hair", "Shader", slot: 1),
                    Material("skin", "Shader", slot: 2),
                }),
            })));

            var submeshes = result.Avatars[0].Renderers[0].Submeshes;
            Assert.AreEqual("Material-01-001", submeshes[0].MaterialId);
            Assert.AreEqual("Material-01-002", submeshes[1].MaterialId);
            Assert.AreEqual("Material-01-001", submeshes[2].MaterialId);
        }

        [Test]
        public void TheSameMaterialInTwoAvatarsIsNotRecordedAsShared()
        {
            var shared = Material("shared", "Shader");
            var result = CensusAnonymizer.Anonymize(Set(
                Avatar(renderers: new[] { Renderer(submeshes: new[] { shared }) }),
                Avatar(renderers: new[] { Renderer(submeshes: new[] { shared }) })));

            Assert.AreEqual(
                "Material-01-001",
                result.Avatars[0].Renderers[0].Submeshes[0].MaterialId);
            Assert.AreEqual(
                "Material-02-001",
                result.Avatars[1].Renderers[0].Submeshes[0].MaterialId);
        }

        [Test]
        public void AnEmptySlotGetsNoFabricatedMaterialIdentity()
        {
            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Submesh(
                        hasMaterial: false,
                        materialName: null,
                        materialAssetPath: null,
                        materialAssetGuid: null,
                        shaderName: null),
                }),
            })));

            var submesh = result.Avatars[0].Renderers[0].Submeshes[0];
            Assert.IsFalse(submesh.HasMaterial);
            Assert.IsNull(submesh.MaterialId);
            Assert.IsNull(submesh.ShaderFamily);
        }

        [Test]
        public void AttestedShaderFamiliesAreNamed()
        {
            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Material("a", ".poiyomi/Toon", ShaderFamilyAttestation.Poiyomi),
                    Material("b", "lilToon", ShaderFamilyAttestation.LilToon, slot: 1),
                }),
            })));

            var submeshes = result.Avatars[0].Renderers[0].Submeshes;
            Assert.AreEqual("Poiyomi", submeshes[0].ShaderFamily);
            Assert.AreEqual("LilToon", submeshes[1].ShaderFamily);
        }

        [Test]
        public void UnattestedFamiliesAreLetteredInFirstAppearanceOrder()
        {
            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                Renderer(submeshes: new[]
                {
                    Material("a", "Secret/Custom"),
                    Material("b", "Other/Thing", slot: 1),
                    Material("c", "Secret/Custom", slot: 2),
                }),
            })));

            var submeshes = result.Avatars[0].Renderers[0].Submeshes;
            Assert.AreEqual("UnknownFamily-A", submeshes[0].ShaderFamily);
            Assert.AreEqual("UnknownFamily-B", submeshes[1].ShaderFamily);
            Assert.AreEqual("UnknownFamily-A", submeshes[2].ShaderFamily);
        }

        [Test]
        public void UnattestedFamiliesAreNumberedAcrossTheWholeRun()
        {
            var result = CensusAnonymizer.Anonymize(Set(
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[] { Material("a", "Secret/Custom") }),
                }),
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[] { Material("b", "Secret/Custom") }),
                })));

            Assert.AreEqual(
                "UnknownFamily-A",
                result.Avatars[0].Renderers[0].Submeshes[0].ShaderFamily);
            Assert.AreEqual(
                "UnknownFamily-A",
                result.Avatars[1].Renderers[0].Submeshes[0].ShaderFamily);
        }

        [Test]
        public void UnattestedFamilyLetteringSurvivesPastTwentySix()
        {
            var submeshes = new ObservedSubmesh[27];
            for (var index = 0; index < submeshes.Length; index++)
                submeshes[index] = Material("m" + index, "Family" + index, slot: index);

            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                Renderer(submeshes: submeshes),
            })));

            var anonymized = result.Avatars[0].Renderers[0].Submeshes;
            Assert.AreEqual("UnknownFamily-Z", anonymized[25].ShaderFamily);
            Assert.AreEqual("UnknownFamily-AA", anonymized[26].ShaderFamily);
        }

        [Test]
        public void MeasurementsPassThroughUnchanged()
        {
            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                Renderer(kind: RendererKind.MeshRenderer, submeshes: new[]
                {
                    Submesh(
                        submeshIndex: 4,
                        materialSlotIndex: 5,
                        alphaFailure: AlphaResolutionFailure.SemanticsUnknown,
                        disposition: SeparationDisposition.Split,
                        provenOpaque: 7,
                        mustRemainTransparent: 2,
                        unknown: 1),
                }),
            })));

            var renderer = result.Avatars[0].Renderers[0];
            var submesh = renderer.Submeshes[0];

            Assert.AreEqual(RendererKind.MeshRenderer, renderer.Kind);
            Assert.AreEqual(RendererRefusal.None, renderer.Refusal);
            Assert.AreEqual(1, renderer.SubmeshCount);
            Assert.AreEqual(10, renderer.TriangleCount);
            Assert.AreEqual(4, submesh.SubmeshIndex);
            Assert.AreEqual(5, submesh.MaterialSlotIndex);
            Assert.AreEqual(AlphaResolutionFailure.SemanticsUnknown, submesh.AlphaFailure);
            Assert.AreEqual(SeparationDisposition.Split, submesh.Disposition);
            Assert.AreEqual(10, submesh.TriangleCount);
            Assert.AreEqual(7, submesh.ProvenOpaqueTriangleCount);
            Assert.AreEqual(2, submesh.MustRemainTransparentTriangleCount);
            Assert.AreEqual(1, submesh.UnknownTriangleCount);
        }

        [Test]
        public void UnreachableMeshCountsStayNullThroughAnonymization()
        {
            var result = CensusAnonymizer.Anonymize(Set(Avatar(renderers: new[]
            {
                RefusedRenderer(RendererRefusal.UnsupportedRendererType),
            })));

            var renderer = result.Avatars[0].Renderers[0];
            Assert.AreEqual(RendererRefusal.UnsupportedRendererType, renderer.Refusal);
            Assert.IsNull(renderer.SubmeshCount);
            Assert.IsNull(renderer.TriangleCount);
            Assert.AreEqual(0, renderer.Submeshes.Count);
        }

        [Test]
        public void AnonymizingAnEmptyRunProducesAnEmptyResult()
        {
            var result = CensusAnonymizer.Anonymize(Set());

            Assert.AreEqual(0, result.Avatars.Count);
        }

        [Test]
        public void AnonymizeRejectsANullObservationSet()
        {
            Assert.Throws<ArgumentNullException>(
                () => CensusAnonymizer.Anonymize(null));
        }
    }
}
