using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The route table data tests of the reviewed Poiyomi 9.3 route.
    /// Every test pins the table against the installed bundled
    /// shaders and against the old plain table, so the reviewed
    /// table can never drift from the old one or from the record.
    /// </summary>
    public sealed class PoiyomiReviewedRouteTableTests
    {
        /// <summary>
        /// The Unity global keywords that the built-in render
        /// pipeline declares on every shader. They are not pragma
        /// keywords of the base pass, so the record does not class
        /// them. They stay outside both lists, and a material that
        /// enables one refuses as it does today.
        /// </summary>
        private static readonly string[] UnityGlobalKeywords =
        {
            "DIRECTIONAL",
            "DIRECTIONAL_COOKIE",
            "INSTANCING_ON",
            "LIGHTPROBE_SH",
            "POINT",
            "POINT_COOKIE",
            "SHADOWS_CUBE",
            "SHADOWS_DEPTH",
            "SHADOWS_SCREEN",
            "SHADOWS_SOFT",
            "SPOT",
            "STEREO_CUBEMAP_RENDER_ON",
            "STEREO_INSTANCING_ON",
            "STEREO_MULTIVIEW_ON",
            "UNITY_SINGLE_PASS_STEREO",
        };

        private static IEnumerable<Shader> BundledShaders()
        {
            yield return LoadBundledShader(false);
            yield return LoadBundledShader(true);
        }

        private static Shader LoadBundledShader(bool twoPass)
        {
            var guid = twoPass
                ? PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid
                : PoiyomiSourceAttestation.CanonicalShaderGuid;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the bundled shader must " +
                "resolve by its pinned GUID " + guid);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the bundled shader must load");
            return shader;
        }

        [Test]
        public void BundledRowsCarryTheReviewedFlag()
        {
            foreach (var row in PoiyomiSourceAttestation.AdmittedSources())
            {
                var bundled =
                    row.ShaderName.StartsWith(
                        ".poiyomi/Old Versions/9.3/",
                        StringComparison.Ordinal);
                Assert.That(
                    row.AtlasRouteReviewed, Is.EqualTo(bundled),
                    "the row '" + row.ShaderName + "' carries the" +
                    " reviewed flag exactly when the review of" +
                    " 2026-11-23 read its source");
            }
        }

        // --- Falsifier 1: ReleaseRowKeepsThePlainRoute ---------------
        // The dev project installs only the two shader files that the
        // Poiyomi 10 package bundles. The release 9.3.64 files are
        // not installed, and the pinned release digests never match
        // the bundled bytes, so no material can attest to a release
        // row here. The test stands the release row in at the table
        // mapping seam and asserts the exact branch that AttestRow
        // runs. The bundled path of the same branch is proven by the
        // capture tests against the real installed files.
        [Test]
        public void ReleaseRowKeepsThePlainRoute()
        {
            var releaseRow = PoiyomiSourceAttestation
                .AdmittedSources()
                .Single(row =>
                    row.ShaderName ==
                    PoiyomiSourceAttestation.PoiyomiToonShaderName);
            Assert.That(
                releaseRow.AtlasRouteReviewed, Is.False,
                "the release row carries no reviewed flag");

            Assert.That(
                AtlasUseCapture.PoiyomiTableOf(
                    releaseRow.AtlasRouteReviewed),
                Is.SameAs(AtlasRouteTable.PoiyomiMainColor),
                "a material whose attested row is a release row" +
                " keeps the plain route");
            Assert.That(
                AtlasUseCapture.PoiyomiTableOf(true),
                Is.SameAs(AtlasRouteTable.PoiyomiReviewedMainColor),
                "a material whose attested row is a bundled row" +
                " takes the reviewed route");
        }

        [Test]
        public void ShaderInventoryMatchesTheReviewedRecord(
            [ValueSource(nameof(BundledShaders))] Shader shader)
        {
            var keywordSpace = new HashSet<string>(
                shader.keywordSpace.keywordNames,
                StringComparer.Ordinal);
            var inventoryKeywords =
                new HashSet<string>(
                    PoiyomiReviewedInventory.Keywords.Select(
                        row => row.Keyword),
                    StringComparer.Ordinal);
            var engine = new HashSet<string>(
                PoiyomiReviewedInventory.EngineKeywords,
                StringComparer.Ordinal);
            var globals = new HashSet<string>(
                UnityGlobalKeywords, StringComparer.Ordinal);

            foreach (var keyword in inventoryKeywords)
            {
                Assert.That(
                    keywordSpace.Contains(keyword), Is.True,
                    "the declared keyword '" + keyword +
                    "' must be in the shader keyword space");
            }

            foreach (var keyword in engine)
            {
                Assert.That(
                    keywordSpace.Contains(keyword), Is.True,
                    "the engine keyword '" + keyword +
                    "' must be in the shader keyword space");
            }

            var unknown = keywordSpace
                .Except(inventoryKeywords)
                .Except(engine)
                .Except(globals)
                .ToList();
            Assert.That(
                unknown, Is.Empty,
                "every keyword of the keyword space must be on the" +
                " record list, the engine list, or the Unity global" +
                " list. Unknown: " + string.Join(", ", unknown));
            Assert.That(
                keywordSpace.Count,
                Is.EqualTo(
                    inventoryKeywords.Count +
                    engine.Count +
                    globals.Count),
                "the keyword space must hold exactly the reviewed" +
                " local keywords, the engine keywords, and the" +
                " Unity global keywords. A new keyword fails here" +
                " until a review classes it");

            var propertyNames = new HashSet<string>(
                TexturePropertyNames(shader), StringComparer.Ordinal);
            var inventoryTextures =
                new HashSet<string>(
                    PoiyomiReviewedInventory.Textures.Select(
                        row => row.Texture),
                    StringComparer.Ordinal);
            foreach (var name in propertyNames)
            {
                Assert.That(
                    inventoryTextures.Contains(name), Is.True,
                    "the texture property '" + name +
                    "' must be on the record list");
            }

            // The record counts 139 declaration lines. 137 name an
            // active property. The commented declaration of
            // _Udon_VideoTex has no active property, so the record
            // list holds exactly one name more than the shader.
            Assert.That(
                inventoryTextures.Count,
                Is.EqualTo(propertyNames.Count + 1),
                "the record list must hold the shader textures plus" +
                " the one commented declaration");
            Assert.That(
                inventoryTextures.Contains("_Udon_VideoTex"), Is.True,
                "the commented declaration stays on the record list");
        }

        [Test]
        public void ReviewedTableKeepsEveryPlainEntry()
        {
            var old = AtlasRouteTable.PoiyomiMainColor;
            var reviewed = AtlasRouteTable.PoiyomiReviewedMainColor;

            Assert.That(
                reviewed.MemberProperty,
                Is.EqualTo(old.MemberProperty),
                "the reviewed route keeps the member");
            Assert.That(
                reviewed.LiveValues, Is.Not.Null,
                "the reviewed route carries the live value model");
            Assert.That(
                old.LiveValues, Is.Null,
                "the plain route carries no live value model");

            var routes = reviewed.CompanionRoutes
                .Select(route => route.Property)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var neutral in old.NeutralProperties)
            {
                Assert.That(
                    reviewed.NeutralProperties.Contains(neutral) ||
                    routes.Contains(neutral),
                    Is.True,
                    "the old neutral '" + neutral +
                    "' stays in the reviewed table");
            }

            foreach (var keyword in old.AllowedKeywords)
            {
                Assert.That(
                    reviewed.AllowedKeywords.Contains(keyword),
                    Is.True,
                    "the old keyword '" + keyword +
                    "' stays allowed");
            }

            foreach (var condition in old.ConditionProperties)
            {
                Assert.That(
                    reviewed.ConditionProperties.Contains(condition),
                    Is.True,
                    "the old condition '" + condition +
                    "' stays a condition");
            }

            foreach (var transform in old.TransformProperties)
            {
                var kept = reviewed.TransformProperties.Any(entry =>
                    entry.Property == transform.Property &&
                    entry.Identity == transform.Identity);
                Assert.That(
                    kept, Is.True,
                    "the old transform '" + transform.Property +
                    "' stays with its identity");
            }
        }

        /// <summary>
        /// The old required value rule survives unchanged: the
        /// reviewed table refuses the same values with the same
        /// sentences.
        /// </summary>
        [Test]
        public void ReviewedTableKeepsThePlainRequiredValues(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            Assert.That(
                AtlasRouteTable.PoiyomiMainColor
                    .UnheldRequiredValue(material),
                Is.Null,
                "fixture precondition: a fresh material holds the" +
                " plain values");

            material.SetFloat("_MainPixelMode", 1f);
            Assert.That(
                AtlasRouteTable.PoiyomiReviewedMainColor
                    .UnheldRequiredValue(material),
                Is.EqualTo(
                    AtlasRouteTable.PoiyomiMainColor
                        .UnheldRequiredValue(material)),
                "the reviewed table refuses the same plain value" +
                " with the same sentence");
        }

        [Test]
        public void FreshBundledMaterialIsAdmittedOnBothRows(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));

            var outcome = fixture.CaptureSingle(material);

            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        private static IEnumerable<string> TexturePropertyNames(
            Shader shader)
        {
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var index = 0; index < count; index++)
            {
                if (ShaderUtil.GetPropertyType(shader, index) !=
                    UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    continue;
                }

                yield return ShaderUtil.GetPropertyName(shader, index);
            }
        }
    }
}
