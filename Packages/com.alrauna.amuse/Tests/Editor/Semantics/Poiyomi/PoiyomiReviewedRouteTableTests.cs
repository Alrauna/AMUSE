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
        /// enables one refuses the use with
        /// <c>KeywordNotAllowed</c>.
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

        // --- the completeness pins ------------------------------------

        /// <summary>
        /// The six condition names that no Properties entry declares.
        /// They are uniforms of the audio link block that a saved
        /// material can carry. The record section 5 note names them.
        /// A property lookup cannot see them, so the walk checks the
        /// declared name list of the model instead.
        /// </summary>
        private static readonly string[] UniformConditions =
        {
            "_AudioLinkCCStripY",
            "_DebugWaveform",
            "_DebugDFT",
            "_DebugCCStrip",
            "_DebugCCLights",
            "_DebugAutocorrelator",
        };

        /// <summary>
        /// Every texture property of the bundled row is the member,
        /// a neutral, a companion route, or a member of a refused
        /// family. The record classes no texture of this shader as a
        /// member copy. The inventory row of each texture must name
        /// the same class as the table realizes, so the table and
        /// the record can never drift apart. A new texture of a
        /// vendor update fails here until a review classes it.
        /// </summary>
        [Test]
        public void EveryTexturePropertyIsClassified(
            [ValueSource(nameof(BundledShaders))] Shader shader)
        {
            var table = AtlasRouteTable.PoiyomiReviewedMainColor;
            var routes = table.CompanionRoutes
                .Select(route => route.Property)
                .ToHashSet(StringComparer.Ordinal);
            var refusedKeywords =
                new HashSet<string>(
                    PoiyomiLiveValues.RefusedKeywords,
                    StringComparer.Ordinal);
            var refusedMembers = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (var family in PoiyomiLiveValues.Model.Families)
            {
                if (!refusedKeywords.Contains(family.Name))
                {
                    continue;
                }

                foreach (var member in family.Properties)
                {
                    refusedMembers.Add(member);
                }
            }

            var inventoryRows = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (var row in PoiyomiReviewedInventory.Textures)
            {
                inventoryRows[row.Texture] = row.Class;
            }

            foreach (var texture in TexturePropertyNames(shader))
            {
                var isMember = texture == table.MemberProperty;
                var isRoute = routes.Contains(texture);
                var isNeutral = table.NeutralProperties.Contains(texture);
                var isRefused = refusedMembers.Contains(texture);
                Assert.That(
                    isMember || isRoute || isNeutral || isRefused,
                    Is.True,
                    "the texture '" + texture + "' is unclassified." +
                    " It is no member, no companion route, no" +
                    " neutral, and no member of a refused family");

                Assert.That(
                    inventoryRows.TryGetValue(
                        texture, out var rowClass), Is.True,
                    "the texture '" + texture +
                    "' has no inventory row of the record");
                var realized = isMember
                    ? "member"
                    : isRoute
                        ? "companion"
                        : isNeutral
                            ? "neutral"
                            : "refused feature";
                Assert.That(
                    rowClass, Is.EqualTo(realized),
                    "the record classes the texture '" + texture +
                    "' as " + rowClass + " but the table realizes" +
                    " " + realized);
            }
        }

        /// <summary>
        /// The keyword space of the bundled row splits exactly into
        /// the allowed list and the refused list, once the two
        /// engine keywords and the Unity global keywords leave.
        /// Neither list may hold a keyword of the other list. Every
        /// inventory row must sit on the list that its verdict
        /// names.
        /// </summary>
        [Test]
        public void EveryVendorKeywordIsAllowedOrRefused(
            [ValueSource(nameof(BundledShaders))] Shader shader)
        {
            var table = AtlasRouteTable.PoiyomiReviewedMainColor;
            var allowed = table.AllowedKeywords;
            var refused = PoiyomiLiveValues.RefusedKeywords;

            var allowedSet = new HashSet<string>(
                allowed, StringComparer.Ordinal);
            var refusedSet = new HashSet<string>(
                refused, StringComparer.Ordinal);
            allowedSet.IntersectWith(refusedSet);
            Assert.That(
                allowedSet, Is.Empty,
                "no keyword may sit on the allowed list and the" +
                " refused list");

            var keywordSpace = new HashSet<string>(
                shader.keywordSpace.keywordNames,
                StringComparer.Ordinal);
            foreach (var keyword in allowed)
            {
                Assert.That(
                    keywordSpace.Contains(keyword), Is.True,
                    "the allowed keyword '" + keyword +
                    "' must sit in the keyword space of the row");
            }

            foreach (var keyword in refused)
            {
                Assert.That(
                    keywordSpace.Contains(keyword), Is.True,
                    "the refused keyword '" + keyword +
                    "' must sit in the keyword space of the row");
            }

            var unclassed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var keyword in keywordSpace)
            {
                if (PoiyomiReviewedInventory.EngineKeywords.Contains(
                        keyword))
                {
                    continue;
                }

                if (UnityGlobalKeywords.Contains(keyword))
                {
                    continue;
                }

                if (table.AllowedKeywords.Contains(keyword))
                {
                    continue;
                }

                if (PoiyomiLiveValues.RefusedKeywords.Contains(keyword))
                {
                    continue;
                }

                unclassed.Add(keyword);
            }

            Assert.That(
                unclassed, Is.Empty,
                "every local keyword of the keyword space must sit" +
                " on the allowed list or the refused list. A" +
                " keyword the review did not class refuses the use." +
                " Unclassed: " + string.Join(", ", unclassed));

            var refusedRows =
                PoiyomiReviewedInventory.Keywords
                    .Where(entry => entry.Verdict == "Refuse")
                    .Select(entry => entry.Keyword)
                    .ToList();
            Assert.That(
                refusedRows, Is.EquivalentTo(refused),
                "the refused list must be the refused rows of the" +
                " record inventory");
            var allowedRows =
                PoiyomiReviewedInventory.Keywords
                    .Where(entry => entry.Verdict != "Refuse")
                    .Select(entry => entry.Keyword)
                    .ToList();
            foreach (var keyword in allowedRows)
            {
                Assert.That(
                    table.AllowedKeywords.Contains(keyword), Is.True,
                    "the allowed inventory keyword '" + keyword +
                    "' must sit on the allowed list");
            }
        }

        /// <summary>
        /// Every selector, transform, pan, stochastic, and condition
        /// property of every route, and every property a table rule
        /// reads, exists on the bundled row with the kind the code
        /// reads. A transform that names the tiling and offset of a
        /// texture checks the texture entry instead, because Unity
        /// owns the tiling and offset outside the Properties block.
        /// An audio uniform checks the declared name list of the
        /// model. A vector condition must be a Vector property and
        /// every other condition must be a scalar property, so a
        /// misspelled or re-typed read fails here instead of
        /// reading a silent zero.
        /// </summary>
        [Test]
        public void EveryReadPropertyExists(
            [ValueSource(nameof(BundledShaders))] Shader shader)
        {
            var table = AtlasRouteTable.PoiyomiReviewedMainColor;
            var conditions = new SortedSet<string>(
                StringComparer.Ordinal);
            foreach (var condition in table.ConditionProperties)
            {
                conditions.Add(condition);
            }

            foreach (var route in table.CompanionRoutes)
            {
                foreach (var condition in route.ConditionProperties)
                {
                    conditions.Add(condition);
                }

                foreach (var transform in route.TransformProperties)
                {
                    conditions.Add(transform.Property);
                }
            }

            foreach (var name in conditions)
            {
                if (name.EndsWith("_ST", StringComparison.Ordinal))
                {
                    var stem = name.Substring(
                        0, name.Length - "_ST".Length);
                    Assert.That(
                        FindPropertyKind(shader, stem),
                        Is.EqualTo(PropertyKind.Texture),
                        "the transform '" + name +
                        "' needs the texture entry '" + stem +
                        "' on the row");
                    continue;
                }

                if (UniformConditions.Contains(name))
                {
                    Assert.That(
                        PoiyomiLiveValues.Model.Declares(name),
                        Is.True,
                        "the uniform condition '" + name +
                        "' must sit on the declared name list of" +
                        " the model");
                    continue;
                }

                var kind = FindPropertyKind(shader, name);
                Assert.That(
                    kind, Is.Not.EqualTo(PropertyKind.Missing),
                    "the condition '" + name +
                    "' must exist on the row");
                if (IsVectorCondition(name))
                {
                    Assert.That(
                        kind, Is.EqualTo(PropertyKind.Vector),
                        "the condition '" + name +
                        "' must be a Vector property but the row" +
                        " declares " + kind);
                }
                else
                {
                    Assert.That(
                        kind, Is.EqualTo(PropertyKind.Scalar),
                        "the condition '" + name +
                        "' must be a scalar property but the row" +
                        " declares " + kind);
                }
            }
        }

        /// <summary>
        /// The property kinds the walk reads from the row. Vector
        /// covers the Unity Vector entries. Scalar covers the Float,
        /// Range, and Int entries, because the code reads all three
        /// with <c>GetFloat</c>. The record names the scalar and
        /// vector kinds of the rule properties, and the vendor
        /// declares the scalar selectors as Float or Range entries
        /// with integer drawers.
        /// </summary>
        private enum PropertyKind
        {
            Missing,
            Scalar,
            Vector,
            Texture,
        }

        private static PropertyKind FindPropertyKind(
            Shader shader, string name)
        {
            var index = shader.FindPropertyIndex(name);
            if (index < 0)
            {
                return PropertyKind.Missing;
            }

            switch (ShaderUtil.GetPropertyType(shader, index))
            {
                case UnityEditor.ShaderUtil.ShaderPropertyType.Vector:
                    return PropertyKind.Vector;
                case UnityEditor.ShaderUtil.ShaderPropertyType.TexEnv:
                    return PropertyKind.Texture;
                default:
                    return PropertyKind.Scalar;
            }
        }

        /// <summary>
        /// Whether the code reads the condition as a Vector. The
        /// pans, the split tiling and pan entries, the packed map
        /// scale and offset entries, the matcap blend vectors, and
        /// the tiling and offset transforms are vectors. Every
        /// other condition is a scalar read.
        /// </summary>
        private static bool IsVectorCondition(string name)
        {
            return name.EndsWith("Pan", StringComparison.Ordinal) ||
                name.EndsWith("Pan_G", StringComparison.Ordinal) ||
                name.EndsWith("Pan_B", StringComparison.Ordinal) ||
                name.EndsWith("Pan_A", StringComparison.Ordinal) ||
                name.Contains("TilingOffset") ||
                name.EndsWith(
                    "ScaleOffset", StringComparison.Ordinal) ||
                name.EndsWith(
                    "PBRMaskScaleTiling", StringComparison.Ordinal) ||
                name.EndsWith("BlendUV1", StringComparison.Ordinal) ||
                name.EndsWith("_ST", StringComparison.Ordinal);
        }

        /// <summary>
        /// The three KeyOnly rows of the record section 6 with an
        /// empty family. The record gives them no dead properties,
        /// so the model holds no family for them, and every other
        /// keyword of the inventory holds one.
        /// </summary>
        private static readonly string[] EmptyRecordFamilies =
        {
            "GEOM_TYPE_MESH",
            "_LIGHTINGMODE_REALISTIC",
            "_STOCHASTICMODE_NONE",
        };

        /// <summary>
        /// Every allowed and refused keyword that the record gives
        /// dead properties holds a family in the model, and the
        /// three empty record rows hold none. A family that drops
        /// keeps its members in the key, so a missing family can
        /// only lose merge coverage. A family that a wrong member
        /// widens can merge two materials that render differently.
        /// </summary>
        [Test]
        public void EveryKeywordHasAFamily()
        {
            var familyNames = new HashSet<string>(
                PoiyomiLiveValues.Model.Families.Select(
                    family => family.Name),
                StringComparer.Ordinal);
            var empty = new HashSet<string>(
                EmptyRecordFamilies, StringComparer.Ordinal);
            foreach (var row in PoiyomiReviewedInventory.Keywords)
            {
                if (empty.Contains(row.Keyword))
                {
                    Assert.That(
                        familyNames.Contains(row.Keyword), Is.False,
                        "the keyword '" + row.Keyword +
                        "' holds an empty record family. The model" +
                        " must carry no family for it");
                    continue;
                }

                Assert.That(
                    familyNames.Contains(row.Keyword), Is.True,
                    "the keyword '" + row.Keyword +
                    "' holds record dead properties. The model" +
                    " must carry its family");
            }
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
