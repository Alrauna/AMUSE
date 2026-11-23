using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Data tests for the reviewed live value model of the bundled
    /// Poiyomi 9.3 rows. The model drops dead families from the
    /// material key, so a missing or wrong name can merge two
    /// materials that render differently. Every test checks the
    /// pinned data against the installed shaders rather than against
    /// the model itself.
    /// </summary>
    public sealed class PoiyomiLiveValuesTests
    {
        private static readonly string[] BundledShaderGuids =
        {
            PoiyomiSourceAttestation.CanonicalShaderGuid,
            PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
        };

        /// <summary>
        /// Every refused keyword of the model must sit on the record
        /// inventory as a refused row, so the lists can never drift
        /// apart.
        /// </summary>
        [Test]
        public void RefusedKeywordsMatchTheInventory()
        {
            var refused = PoiyomiReviewedInventory.Keywords
                .Where(row => row.Verdict == "Refuse")
                .Select(row => row.Keyword)
                .ToList();
            Assert.That(
                PoiyomiLiveValues.RefusedKeywords,
                Is.EquivalentTo(refused),
                "the refused keyword list is the refused rows of" +
                " the record inventory");
            Assert.That(
                PoiyomiLiveValues.Model.Families
                    .Where(family =>
                        PoiyomiLiveValues.RefusedKeywords.Contains(
                            family.Name))
                    .Select(family => family.Name),
                Is.EquivalentTo(PoiyomiLiveValues.RefusedKeywords),
                "the model holds one family per refused keyword");
        }

        /// <summary>
        /// Every uniform declaration name, every texture declaration
        /// name, and every Properties entry name of both installed
        /// bundled files is a declared name of the model. A missing
        /// name could hide a live saved value behind a stale name.
        /// </summary>
        [Test]
        public void DeclaredUniformsCoverTheSource()
        {
            var declared = new HashSet<string>(
                PoiyomiLiveValues.Model.DeclaredUniforms,
                StringComparer.Ordinal);
            var missing = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var guid in BundledShaderGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(
                    path, Is.Not.Empty,
                    "fixture precondition: the bundled shader must " +
                    "resolve by its pinned GUID " + guid);
                foreach (var name in DeclaredNamesOf(
                    File.ReadAllText(path)))
                {
                    if (!declared.Contains(name))
                    {
                        missing.Add(System.IO.Path.GetFileName(path) +
                                    ": " + name);
                    }
                }
            }

            Assert.That(
                declared.Count, Is.GreaterThan(3000),
                "the pinned list must hold the full source surface");
            Assert.That(
                missing, Is.Empty,
                "DeclaredUniforms must cover every declared name of" +
                " both bundled files. Missing: " +
                string.Join(", ", missing));
        }

        /// <summary>
        /// A family member that neither row declares as a property
        /// and that no declared name covers can never appear in a
        /// saved material. It would widen the model for no gain and
        /// hide a typo in the data.
        /// </summary>
        [Test]
        public void FamiliesNameOnlyDeclaredProperties()
        {
            var rowProperties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in BundledShaderGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var shader =
                    AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.That(
                    shader, Is.Not.Null,
                    "fixture precondition: the bundled shader must " +
                    "load");
                var count = shader.GetPropertyCount();
                for (var index = 0; index < count; index++)
                {
                    rowProperties.Add(shader.GetPropertyName(index));
                }
            }

            var declared = new HashSet<string>(
                PoiyomiLiveValues.Model.DeclaredUniforms,
                StringComparer.Ordinal);
            var unknown = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var family in PoiyomiLiveValues.Model.Families)
            {
                foreach (var property in family.Properties)
                {
                    if (!rowProperties.Contains(property) &&
                        !declared.Contains(property))
                    {
                        unknown.Add(family.Name + ": " + property);
                    }
                }
            }

            Assert.That(
                unknown, Is.Empty,
                "every family member must be a property of an" +
                " admitted row or a declared name. Unknown: " +
                string.Join(", ", unknown));
        }

        /// <summary>
        /// A family that holds its own switch would drop the switch
        /// from the key, so two materials with different switch
        /// values could share one page.
        /// </summary>
        [Test]
        public void FamiliesNeverHoldASwitch()
        {
            var switches = new HashSet<string>(StringComparer.Ordinal);
            foreach (var family in PoiyomiLiveValues.Model.Families)
            {
                switches.UnionWith(family.Switches);
            }

            var offenders = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var family in PoiyomiLiveValues.Model.Families)
            {
                foreach (var property in family.Properties)
                {
                    if (switches.Contains(property))
                    {
                        offenders.Add(family.Name + ": " + property);
                    }
                }
            }

            Assert.That(
                offenders, Is.Empty,
                "no family may hold a switch. Offenders: " +
                string.Join(", ", offenders));
        }

        /// <summary>
        /// An assigned texture of a refused feature with the keyword
        /// off is a dead value. The use is admitted, and the key
        /// equals the key of the same material without the texture,
        /// so both can share one page.
        /// </summary>
        [Test]
        public void RefusedFeatureTextureWithItsKeywordOffLeavesTheKey(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var main = fixture.NewRouteTexture(64);
            var withDecal = fixture.NewMaterial(twoPass);
            withDecal.SetTexture("_MainTex", main);
            foreach (var keyword in PoiyomiLiveValues.RefusedKeywords)
            {
                Assert.That(
                    withDecal.IsKeywordEnabled(keyword), Is.False,
                    "fixture precondition: the keyword '" + keyword +
                    "' must be off on a fresh material");
            }

            var decal = fixture.NewRouteTexture(64);
            withDecal.SetTexture("_DecalTexture", decal);
            withDecal.SetTextureScale(
                "_DecalTexture", new Vector2(2f, 3f));
            withDecal.SetTextureOffset(
                "_DecalTexture", new Vector2(0.25f, 0.5f));
            var outcomeWith = fixture.CaptureSingle(withDecal);
            PoiyomiReviewedFixture.AssertAdmitted(outcomeWith);

            var withoutDecal = fixture.NewMaterial(twoPass);
            withoutDecal.SetTexture("_MainTex", main);
            withoutDecal.SetTextureScale(
                "_DecalTexture", new Vector2(2f, 3f));
            withoutDecal.SetTextureOffset(
                "_DecalTexture", new Vector2(0.25f, 0.5f));
            var outcomeWithout = fixture.CaptureSingle(withoutDecal);
            PoiyomiReviewedFixture.AssertAdmitted(outcomeWithout);

            Assert.That(
                outcomeWith.Use.MaterialKey,
                Is.EqualTo(outcomeWithout.Use.MaterialKey),
                "the dead decal texture and its transform leave" +
                " the material key");
        }

        // --- Falsifier 8: RefusedKeywordOnStillRefuses ---------------
        [Test]
        public void RefusedKeywordOnStillRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_DecalTexture", fixture.NewRouteTexture(64));
            material.EnableKeyword("GEOM_TYPE_BRANCH");

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.KeywordNotAllowed,
                "GEOM_TYPE_BRANCH");
        }

        // --- the source extraction --------------------------------------

        private static readonly Regex PropertiesEntry = new Regex(
            @"^[ \t]*(?:(?:\[[^\]]*\])[ \t]*)*(?://[ \t]*)?" +
            @"(_[A-Za-z0-9_]+)[ \t]*\(",
            RegexOptions.Multiline);
        private static readonly Regex UniformDeclaration = new Regex(
            @"^[ \t]*(?:uniform[ \t]+)?(?:static[ \t]+)?(?:const[ \t]+)?" +
            @"(?:float|half|fixed|int|uint|bool)[1-4]?(?:x[1-4])?" +
            @"[ \t]+(_[A-Za-z0-9_]+)[ \t]*(\[[0-9]+\])?[ \t]*;",
            RegexOptions.Multiline);
        private static readonly Regex TextureDeclaration = new Regex(
            @"^[ \t]*(?://[ \t]*)?(?:Texture2D|Texture2DArray|" +
            @"Texture3D|TextureCube|sampler2D|sampler3D|samplerCUBE|" +
            @"SamplerState|uniform[ \t]+sampler2D|uniform[ \t]+" +
            @"SamplerState)[ \t]*(?:[<][^>]*[>])?[ \t]*" +
            @"([A-Za-z_][A-Za-z0-9_]*)[ \t]*(\[[0-9]+\])?[ \t]*;",
            RegexOptions.Multiline);

        /// <summary>
        /// Every name that either bundled file declares: the
        /// Properties entries, the uniform declarations, and the
        /// texture and sampler declarations. The extraction covers
        /// commented declarations too, because a material can still
        /// carry a saved value under such a name.
        /// </summary>
        private static IEnumerable<string> DeclaredNamesOf(
            string source)
        {
            foreach (var match in PropertiesEntry.Matches(source)
                .Cast<Match>())
            {
                yield return match.Groups[1].Value;
            }

            foreach (var match in UniformDeclaration.Matches(source)
                .Cast<Match>())
            {
                yield return match.Groups[1].Value;
            }

            foreach (var match in TextureDeclaration.Matches(source)
                .Cast<Match>())
            {
                yield return match.Groups[1].Value;
            }
        }
    }
}
