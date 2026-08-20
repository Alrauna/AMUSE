using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Determinism comes from input ordering alone. There is no seed, no hash,
    /// no clock, and no machine-specific value anywhere in the anonymizer, so
    /// equal input must give byte-identical output — including across two
    /// separately constructed but equal observation sets, which is the case a
    /// single repeated call would not catch.
    /// </summary>
    public sealed class CensusAnonymizerDeterminismTests
    {
        private static CensusObservationSet Observations()
        {
            return Set(
                Avatar(
                    avatarName: "one",
                    renderers: new[]
                    {
                        Renderer(submeshes: new[]
                        {
                            Submesh(
                                materialName: "skin",
                                shaderName: "Custom/A",
                                provenOpaque: 3),
                            Submesh(
                                submeshIndex: 1,
                                materialSlotIndex: 1,
                                materialName: "hair",
                                shaderName: ".poiyomi/Toon",
                                attestation: ShaderFamilyAttestation.Poiyomi,
                                mustRemainTransparent: 2),
                        }),
                        RefusedRenderer(RendererRefusal.MissingMesh),
                    }),
                Avatar(
                    avatarName: "two",
                    renderers: new[]
                    {
                        Renderer(submeshes: new[]
                        {
                            Submesh(
                                materialName: "cloth",
                                shaderName: "Custom/B",
                                unknown: 5),
                        }),
                    }));
        }

        [Test]
        public void RepeatedCallsOnOneInputAgree()
        {
            var input = Observations();

            Assert.AreEqual(
                CensusReflection.Describe(CensusAnonymizer.Anonymize(input)),
                CensusReflection.Describe(CensusAnonymizer.Anonymize(input)));
        }

        [Test]
        public void SeparatelyConstructedEqualInputsAgree()
        {
            Assert.AreEqual(
                CensusReflection.Describe(CensusAnonymizer.Anonymize(Observations())),
                CensusReflection.Describe(CensusAnonymizer.Anonymize(Observations())));
        }

        [Test]
        public void ReorderingAvatarsChangesOnlyTheirOrdinals()
        {
            var forward = CensusAnonymizer.Anonymize(Set(
                Avatar(avatarName: "one"),
                Avatar(avatarName: "two")));
            var reversed = CensusAnonymizer.Anonymize(Set(
                Avatar(avatarName: "two"),
                Avatar(avatarName: "one")));

            // Ordinals track position, which is the whole identity scheme: they
            // are run-local and carry no meaning across runs.
            Assert.AreEqual("Avatar-01", forward.Avatars[0].Id);
            Assert.AreEqual("Avatar-01", reversed.Avatars[0].Id);
            Assert.AreEqual(
                CensusReflection.Describe(forward),
                CensusReflection.Describe(reversed));
        }
    }
}
