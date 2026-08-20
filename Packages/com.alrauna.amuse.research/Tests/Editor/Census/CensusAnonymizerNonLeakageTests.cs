using System.Collections.Generic;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Research.Census;
using NUnit.Framework;
using static Alrauna.Amuse.Research.Tests.Editor.Census.CensusObservationBuilders;

namespace Alrauna.Amuse.Research.Tests.Editor.Census
{
    /// <summary>
    /// The test the whole branch exists for. Anonymization is a pure function,
    /// so non-leakage is provable here rather than left to an operator being
    /// careful.
    /// </summary>
    public sealed class CensusAnonymizerNonLeakageTests
    {
        /// <summary>
        /// Distinctive enough that an accidental match would itself be a bug
        /// worth investigating. Each stands in for one class of real
        /// identifier the collector will see.
        /// </summary>
        private static readonly string[] SeededIdentifiers =
        {
            "ZQXCREATOR",
            "ZQXAVATARNAME",
            "ZQXAVATARPATH",
            "ZQXAVATARGUID",
            "ZQXHIERARCHYPATH",
            "ZQXGAMEOBJECT",
            "ZQXRENDERERTYPE",
            "ZQXMATERIALNAME",
            "ZQXMATERIALPATH",
            "ZQXMATERIALGUID",
            "ZQXSHADERNAME",
        };

        /// <summary>
        /// Every string tier 2 is permitted to contain. Ordinal identity and a
        /// closed shader-family vocabulary, and nothing else.
        /// <para>
        /// Widening this is a schema decision that needs the privacy review the
        /// harness design requires for any new category: which AMUSE decision
        /// it informs, the smallest population a bucket could hold, what an
        /// adversary holding one avatar learns, and whether tier 3 needs it at
        /// all. It is never the way to make a failing test pass.
        /// </para>
        /// </summary>
        private static readonly Regex PermittedTierTwoStrings = new Regex(
            @"^(Avatar-\d{2,}"
            + @"|Renderer-\d{2,}-\d{3,}"
            + @"|Material-\d{2,}-\d{3,}"
            + @"|Poiyomi|LilToon|UnknownFamily-[A-Z]+)$",
            RegexOptions.CultureInvariant);

        private static CensusObservationSet SeededObservations()
        {
            var submesh = Submesh(
                materialName: "ZQXMATERIALNAME",
                materialAssetPath: "Assets/ZQXMATERIALPATH/body.mat",
                materialAssetGuid: "ZQXMATERIALGUID",
                shaderName: "ZQXSHADERNAME/Custom Toon",
                attestation: ShaderFamilyAttestation.None,
                provenOpaque: 3,
                mustRemainTransparent: 1);

            var attested = Submesh(
                submeshIndex: 1,
                materialSlotIndex: 1,
                materialName: "ZQXMATERIALNAME Hair",
                materialAssetPath: "Assets/ZQXMATERIALPATH/hair.mat",
                materialAssetGuid: "ZQXMATERIALGUID-2",
                shaderName: ".poiyomi/ZQXSHADERNAME",
                attestation: ShaderFamilyAttestation.Poiyomi,
                unknown: 2);

            return Set(
                Avatar(
                    avatarName: "ZQXAVATARNAME",
                    creatorName: "ZQXCREATOR",
                    assetPath: "Assets/ZQXAVATARPATH/avatar.prefab",
                    assetGuid: "ZQXAVATARGUID",
                    renderers: new[]
                    {
                        Renderer(
                            hierarchyPath: "ZQXHIERARCHYPATH/Body",
                            gameObjectName: "ZQXGAMEOBJECT",
                            rendererTypeName: "ZQXRENDERERTYPE",
                            kind: RendererKind.SkinnedMeshRenderer,
                            submeshes: new[] { submesh, attested }),
                        RefusedRenderer(
                            RendererRefusal.UnsupportedRendererType,
                            hierarchyPath: "ZQXHIERARCHYPATH/Trail",
                            gameObjectName: "ZQXGAMEOBJECT Trail",
                            rendererTypeName: "ZQXRENDERERTYPE"),
                    }),
                Avatar(
                    avatarName: "ZQXAVATARNAME 2",
                    creatorName: "ZQXCREATOR",
                    assetPath: "Assets/ZQXAVATARPATH/second.prefab",
                    assetGuid: "ZQXAVATARGUID-2",
                    renderers: new[]
                    {
                        Renderer(
                            hierarchyPath: "ZQXHIERARCHYPATH/Second",
                            gameObjectName: "ZQXGAMEOBJECT",
                            rendererTypeName: "ZQXRENDERERTYPE",
                            submeshes: new[] { submesh }),
                    }));
        }

        [Test]
        public void TheSeedActuallyReachesTierOne()
        {
            // Without this, every other assertion here could pass vacuously
            // because the fixture stopped seeding anything.
            var strings = CensusReflection.ReachableStrings(SeededObservations());

            foreach (var identifier in SeededIdentifiers)
            {
                Assert.IsTrue(
                    Contains(strings, identifier),
                    "The fixture no longer seeds " + identifier
                    + ", so the non-leakage assertions would pass vacuously.");
            }
        }

        [Test]
        public void NoSeededIdentifierSurvivesAnonymization()
        {
            var anonymized = CensusAnonymizer.Anonymize(SeededObservations());
            var strings = CensusReflection.ReachableStrings(anonymized);

            foreach (var identifier in SeededIdentifiers)
            {
                Assert.IsFalse(
                    Contains(strings, identifier),
                    "Anonymized output leaked the identifier " + identifier + ".");
            }
        }

        [Test]
        public void EveryStringInTierTwoIsOrdinalIdentityOrAKnownShaderFamily()
        {
            var anonymized = CensusAnonymizer.Anonymize(SeededObservations());

            foreach (var value in CensusReflection.ReachableStrings(anonymized))
            {
                Assert.IsTrue(
                    PermittedTierTwoStrings.IsMatch(value),
                    "Anonymized output contains the string \"" + value
                    + "\", which is not ordinal identity or a known shader "
                    + "family. If this is a deliberate new category, it needs "
                    + "privacy review before the allow-list is widened.");
            }
        }

        [Test]
        public void AnonymizationStillProducedSomethingToInspect()
        {
            // The allow-list assertion is vacuously true over an empty graph,
            // so the walk has to be shown to reach real output.
            var anonymized = CensusAnonymizer.Anonymize(SeededObservations());

            Assert.IsNotEmpty(CensusReflection.ReachableStrings(anonymized));
            Assert.AreEqual(2, anonymized.Avatars.Count);
            Assert.AreEqual(2, anonymized.Avatars[0].Renderers[0].Submeshes.Count);
        }

        private static bool Contains(IReadOnlyList<string> strings, string identifier)
        {
            foreach (var value in strings)
            {
                if (value != null && value.Contains(identifier))
                    return true;
            }

            return false;
        }
    }
}
