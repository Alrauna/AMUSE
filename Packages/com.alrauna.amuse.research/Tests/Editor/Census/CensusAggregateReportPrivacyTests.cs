using System.Text.RegularExpressions;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// Tier 3 is the tier that gets published, so it carries distributions and
    /// nothing else. An exact renderer or triangle count is a strong
    /// fingerprint for anyone already holding the avatar, and a per-entity row
    /// hands them one directly.
    /// </summary>
    public sealed class CensusAggregateReportPrivacyTests
    {
        /// <summary>
        /// Ordinal identity is safe in tier 2 and pointless in tier 3, where
        /// its only effect would be to re-introduce a per-entity row.
        /// </summary>
        private static readonly Regex OrdinalIdentity = new Regex(
            @"^(Avatar|Renderer|Material)-\d",
            RegexOptions.CultureInvariant);

        private static CensusAggregateReport Report()
        {
            return CensusAggregator.Aggregate(CensusAnonymizer.Anonymize(Set(
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[]
                    {
                        Submesh(materialName: "a", shaderName: ".poiyomi/Toon",
                            attestation: ShaderFamilyAttestation.Poiyomi,
                            provenOpaque: 12),
                        Submesh(submeshIndex: 1, materialSlotIndex: 1,
                            materialName: "b", shaderName: "Custom/Secret",
                            unknown: 8),
                    }),
                    RefusedRenderer(RendererRefusal.MissingMesh),
                }),
                Avatar(renderers: new[]
                {
                    Renderer(submeshes: new[]
                    {
                        Submesh(materialName: "c", shaderName: "lilToon",
                            attestation: ShaderFamilyAttestation.LilToon,
                            mustRemainTransparent: 4),
                    }),
                }))));
        }

        [Test]
        public void NoPerEntityRecordIsReachableFromTheReport()
        {
            foreach (var value in CensusReflection.ReachableObjects(Report()))
            {
                var name = value.GetType().Name;

                Assert.IsFalse(
                    name.StartsWith("Observed") || name.StartsWith("Anonymized"),
                    "A tier 3 report must carry distributions only, but a "
                    + name + " record is reachable from it.");
            }
        }

        [Test]
        public void NoOrdinalIdentityIsReachableFromTheReport()
        {
            foreach (var value in CensusReflection.ReachableStrings(Report()))
            {
                Assert.IsFalse(
                    OrdinalIdentity.IsMatch(value),
                    "A tier 3 report must not identify individual entities, but "
                    + "it contains the ordinal identity \"" + value + "\".");
            }
        }

        [Test]
        public void EveryStringInTheReportIsAShaderFamilyName()
        {
            // Family names are the one category tier 3 may name, and the
            // harness design's privacy review already passed them: they are
            // families rather than materials, and an unattested-family label
            // confirms nothing about any individual avatar.
            var permitted = new Regex(
                @"^(Poiyomi|LilToon|UnknownFamily-[A-Z]+)$",
                RegexOptions.CultureInvariant);

            foreach (var value in CensusReflection.ReachableStrings(Report()))
            {
                Assert.IsTrue(
                    permitted.IsMatch(value),
                    "A tier 3 report contains the string \"" + value
                    + "\", which is not a shader family name. Any new category "
                    + "needs privacy review before it reaches a published tier.");
            }
        }

        [Test]
        public void TheReportStillHasContentToInspect()
        {
            var report = Report();

            Assert.IsNotEmpty(CensusReflection.ReachableStrings(report));
            Assert.AreEqual(2, report.AvatarCount);
            Assert.AreEqual(24, report.ClassifiedTriangleCount);
        }
    }
}
