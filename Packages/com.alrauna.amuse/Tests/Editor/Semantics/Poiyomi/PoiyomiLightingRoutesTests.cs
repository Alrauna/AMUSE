using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The capture tests of the Lighting group of the reviewed
    /// Poiyomi 9.3 route. The review record of 2026-11-23, sections
    /// 4.1, 4.3, 5, and 8, is the source of truth. Every fixture
    /// drives the installed bundled shaders by their pinned GUIDs,
    /// so a wrong route fact refuses against the vendor shader.
    /// <para>
    /// The Lighting group holds 17 keywords and 23 companion
    /// textures. All 23 companion rows of the record carry a route,
    /// including the three rows that never move. A route that the
    /// record binds keeps its texture keyed and returns
    /// <c>OtherCoordinates</c>.
    /// </para>
    /// </summary>
    public sealed class PoiyomiLightingRoutesTests
    {
        private const int RouteTextureSize = 64;

        /// <summary>
        /// The companion textures of the group, as the inventory
        /// pins them. The tests derive the names from the inventory,
        /// so no test repeats the record rows.
        /// </summary>
        private static IReadOnlyList<string> GroupCompanionTextures()
        {
            return PoiyomiReviewedInventory.Textures
                .Where(row => row.Group == "Lighting" &&
                    row.Class == "companion")
                .Select(row => row.Texture)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The keywords of the group, as the inventory pins them.
        /// </summary>
        private static IReadOnlyList<string> GroupKeywords()
        {
            return PoiyomiReviewedInventory.Keywords
                .Where(row => row.Group == "Lighting")
                .Select(row => row.Keyword)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        private static List<string> MovedCompanions(
            AtlasUseOutcome outcome)
        {
            return outcome.Use.Companions
                .Select(companion => companion.Property)
                .ToList();
        }

        private static void AssignTexture(
            PoiyomiReviewedFixture fixture,
            Material material,
            string texture,
            bool hasSelector)
        {
            material.SetTexture(
                texture, fixture.NewRouteTexture(RouteTextureSize));
            if (hasSelector)
            {
                material.SetFloat(texture + "UV", 0f);
            }
        }

        /// <summary>
        /// One material for a capture. The capture needs the member
        /// texture on disk, and the vendor default holds no texture,
        /// so every captured material gets a route texture first.
        /// The import runs before any keyword turns on.
        /// </summary>
        private static Material NewCapturedMaterial(
            PoiyomiReviewedFixture fixture,
            bool twoPass)
        {
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture(
                "_MainTex", fixture.NewRouteTexture(RouteTextureSize));
            return material;
        }

        private static void EnableKeywords(
            Material material,
            params string[] keywords)
        {
            foreach (var keyword in keywords)
            {
                material.EnableKeyword(keyword);
            }
        }

        /// <summary>
        /// The use must be admitted, and no companion texture of the
        /// group may have moved. The texture ramp feature owns no
        /// companion texture, so its admitted test asserts the empty
        /// set of group companions.
        /// </summary>
        private static void AssertNoGroupTextureMoved(
            AtlasUseOutcome outcome)
        {
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
            var moved = MovedCompanions(outcome);
            foreach (var texture in GroupCompanionTextures())
            {
                Assert.That(
                    moved, Does.Not.Contain(texture),
                    "the group texture '" + texture +
                    "' must stay bound on this use");
            }
        }

        // --- the inventory --------------------------------------------

        /// <summary>
        /// The record rows of the group sit in the reviewed table.
        /// Every companion texture of the group carries a route, and
        /// the shared helper checks the sampler and the stochastic
        /// condition of every route against the record. No routed
        /// texture sits in the neutral list. Every keyword of the
        /// group sits in the allowed list of the table and in the
        /// allowed list of the group.
        /// </summary>
        [Test]
        public void GroupInventoryIsRouted()
        {
            var keywords = GroupKeywords();
            Assert.That(
                keywords, Has.Count.EqualTo(17),
                "fixture precondition: the record lists 17 keywords" +
                " for the Lighting group");
            foreach (var keyword in keywords)
            {
                Assert.That(
                    AtlasRouteTable.PoiyomiReviewedMainColor
                        .AllowedKeywords,
                    Does.Contain(keyword),
                    "the allowed list of the reviewed table must" +
                    " hold '" + keyword + "'");
            }

            Assert.That(
                PoiyomiLightingRoutes.AllowedKeywords,
                Is.EquivalentTo(keywords),
                "the allowed list of the group must match the" +
                " record rows of the group");

            var companions = GroupCompanionTextures();
            Assert.That(
                companions, Has.Count.EqualTo(23),
                "fixture precondition: the record lists 23 companion" +
                " textures for the Lighting group");
            Assert.That(
                PoiyomiLightingRoutes.NeutralTextures, Is.Empty,
                "the record lists no neutral texture for the" +
                " Lighting group");
            foreach (var texture in companions)
            {
                Assert.That(
                    AtlasRouteTable.PoiyomiReviewedMainColor
                        .NeutralProperties,
                    Does.Not.Contain(texture),
                    "the routed texture '" + texture +
                    "' must not sit in the neutral list of the" +
                    " reviewed table");
            }

            PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                "Lighting",
                PoiyomiLightingRoutes.Routes());
        }

        // --- the admitted features ------------------------------------

        /// <summary>
        /// The texture ramp mode owns no companion texture. The rule
        /// of the ramp row pick holds at the vendor defaults, so the
        /// use admits.
        /// </summary>
        [Test]
        public void TextureRampIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            EnableKeywords(material, "_LIGHTINGMODE_TEXTURERAMP");

            var outcome = fixture.CaptureSingle(material);
            AssertNoGroupTextureMoved(outcome);
        }

        [Test]
        public void MultilayerMathIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            foreach (var texture in new[]
                     {
                         "_MultilayerMathBlurMap",
                         "_ShadowBorderMask",
                         "_ShadowColorTex",
                         "_Shadow2ndColorTex",
                         "_Shadow3rdColorTex",
                         "_ShadowStrengthMask",
                     })
            {
                AssignTexture(fixture, material, texture, true);
            }

            // The toggle of one turns the border mask sample on. The
            // plain branch of the mask then pages the texture.
            material.SetFloat("_ShadowBorderMapToggle", 1f);
            EnableKeywords(material, "_LIGHTINGMODE_MULTILAYER_MATH");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_MultilayerMathBlurMap",
                "_ShadowBorderMask",
                "_ShadowColorTex",
                "_Shadow2ndColorTex",
                "_Shadow3rdColorTex",
                "_ShadowStrengthMask");
        }

        [Test]
        public void ShademapIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_1st_ShadeMap", true);
            AssignTexture(fixture, material, "_2nd_ShadeMap", true);
            EnableKeywords(material, "_LIGHTINGMODE_SHADEMAP");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_1st_ShadeMap",
                "_2nd_ShadeMap");
        }

        [Test]
        public void SkinIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_SkinThicknessMap", true);
            EnableKeywords(material, "_LIGHTINGMODE_SKIN");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_SkinThicknessMap");
        }

        [Test]
        public void ClothIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(
                fixture, material, "_ClothMetallicSmoothnessMap", true);
            EnableKeywords(material, "_LIGHTINGMODE_CLOTH");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_ClothMetallicSmoothnessMap");
        }

        /// <summary>
        /// The SDF shading texture never moves. The sample
        /// multiplies the coordinate x by a light driven flip, so
        /// no use may page the texture. The admitted use keeps it
        /// bound at selector 1, and the route must return
        /// `OtherCoordinates` then and `Inert` when the mode
        /// keyword sits off, because an inert texture leaves the
        /// key and two materials that differ only in the texture
        /// would share one page material.
        /// </summary>
        [Test]
        public void SdfShadingIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_SDFShadingTexture", true);
            material.SetFloat("_SDFShadingTextureUV", 1f);
            EnableKeywords(material, "_LIGHTINGMODE_SDF");

            var outcome = fixture.CaptureSingle(material);
            AssertNoGroupTextureMoved(outcome);

            foreach (var route in PoiyomiLightingRoutes.Routes())
            {
                if (route.Property != "_SDFShadingTexture")
                {
                    continue;
                }

                Assert.That(
                    route.UseOf(material, false),
                    Is.EqualTo(AtlasRouteUse.OtherCoordinates),
                    "the SDF texture must stay bound and keyed" +
                    " while the mode keyword is on");
                material.DisableKeyword("_LIGHTINGMODE_SDF");
                Assert.That(
                    route.UseOf(material, false),
                    Is.EqualTo(AtlasRouteUse.Inert),
                    "the SDF texture must leave the key while the" +
                    " mode keyword sits off");
            }
        }

        [Test]
        public void AnisotropicsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_AnisoColorMap", true);
            EnableKeywords(material, "POI_ANISOTROPICS");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_AnisoColorMap");
        }

        [Test]
        public void CubemapIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_CubeMapMask", true);
            EnableKeywords(material, "_CUBEMAP");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_CubeMapMask");
        }

        [Test]
        public void SubsurfaceScatteringIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_SSSThicknessMap", true);
            EnableKeywords(material, "POI_SUBSURFACESCATTERING");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_SSSThicknessMap");
        }

        /// <summary>
        /// The metallic and smoothness maps of Mochie admit with the
        /// split mask sample off. The falsifier of the split rule
        /// covers the split state.
        /// </summary>
        [Test]
        public void MochiePbrIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(
                fixture, material, "_MochieMetallicMaps", true);
            material.SetFloat("_MochieMetallicMapsStochastic", 0f);
            EnableKeywords(material, "MOCHIE_PBR");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_MochieMetallicMaps");
        }

        /// <summary>
        /// The anisotropy map sample sits inside the Mochie block
        /// under the GGX keyword. The use turns both keywords on.
        /// </summary>
        [Test]
        public void GgxAnisotropicsIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_AnisotropyMap", true);
            EnableKeywords(
                material, "GGX_ANISOTROPICS", "MOCHIE_PBR");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_AnisotropyMap");
        }

        [Test]
        public void ClearcoatIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_ClearCoatMaps", true);
            material.SetFloat("_ClearCoatMapsStochastic", 0f);
            EnableKeywords(material, "POI_CLEARCOAT");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_ClearCoatMaps");
        }

        [Test]
        public void EnviroRimIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            // The mask carries no transform property. The sample
            // reads the raw selected UV plus pan.
            AssignTexture(fixture, material, "_RimEnviroMask", true);
            EnableKeywords(material, "POI_ENVIRORIM");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_RimEnviroMask");
        }

        /// <summary>
        /// The four stylized specular textures of the group. Two
        /// sample inside the Unity Chan body and two inside the
        /// lilToon body. Both bodies sit under the stylized
        /// specular keyword, so the use turns all three keywords on.
        /// </summary>
        [Test]
        public void StylizedSpecularIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_HighColor_Tex", true);
            AssignTexture(fixture, material, "_Set_HighColorMask", true);
            AssignTexture(fixture, material, "_SmoothnessTex", false);
            AssignTexture(
                fixture, material, "_ReflectionColorTex", false);
            EnableKeywords(
                material,
                "POI_STYLIZED_StylizedSpecular",
                "_STYLIZEDREFLECTIONMODE_UNITYCHAN",
                "_STYLIZEDREFLECTIONMODE_LILTOON");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_HighColor_Tex",
                "_Set_HighColorMask",
                "_SmoothnessTex",
                "_ReflectionColorTex");
        }

        [Test]
        public void StylizedReflectionUnityChanIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_HighColor_Tex", true);
            AssignTexture(fixture, material, "_Set_HighColorMask", true);
            EnableKeywords(
                material,
                "POI_STYLIZED_StylizedSpecular",
                "_STYLIZEDREFLECTIONMODE_UNITYCHAN");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_HighColor_Tex",
                "_Set_HighColorMask");
        }

        /// <summary>
        /// The smoothness and reflection color textures sample raw
        /// UV0. They carry no selector, transform, or pan property,
        /// so the test assigns them with no selector.
        /// </summary>
        [Test]
        public void StylizedReflectionLilToonIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_SmoothnessTex", false);
            AssignTexture(
                fixture, material, "_ReflectionColorTex", false);
            EnableKeywords(
                material,
                "POI_STYLIZED_StylizedSpecular",
                "_STYLIZEDREFLECTIONMODE_LILTOON");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_SmoothnessTex",
                "_ReflectionColorTex");
        }

        [Test]
        public void BacklightIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_BacklightColorTex", true);
            EnableKeywords(material, "POI_BACKLIGHT");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_BacklightColorTex");
        }

        // --- Falsifier 1: TextureRampRowPickRefuses -------------------

        /// <summary>
        /// The ramp row pick reads a mesh UV as data when the ramp
        /// count sits above one and the ramp row selector sits at
        /// UV0 data. The coordinate rule of the spec admits only
        /// the selectors 1, 2, 3, 4, 5, 8, and 9. The selector 6
        /// holds the polar coordinate and the selector 7 holds the
        /// distorted UV, so both refuse. The rule also holds when
        /// the count sits at most 1, whatever the selector reads.
        /// The no-op guard asserts the twins first.
        /// </summary>
        [Test]
        public void TextureRampRowPickRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            EnableKeywords(material, "_LIGHTINGMODE_TEXTURERAMP");

            // The admitted twins. A count of at most 1 holds the
            // rule at any selector, and a selector of the allowed
            // set holds it at any count.
            material.SetFloat("_ToonRampCount", 1f);
            material.SetFloat("_ToonRampUVSelector", 0f);
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));

            material.SetFloat("_ToonRampCount", 3f);
            material.SetFloat("_ToonRampUVSelector", 1f);
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));

            material.SetFloat("_ToonRampUVSelector", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "The property '");
            var detail = outcome.Detail;
            Assert.That(
                detail.Contains("'_ToonRampCount'") ||
                    detail.Contains("'_ToonRampUVSelector'"),
                Is.True,
                "the refusal must name a property of the ramp row" +
                " rule, detail: " + detail);

            // The polar and distorted selectors read UV0 data, so
            // they refuse like selector 0.
            material.SetFloat("_ToonRampUVSelector", 6f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "The property '");

            material.SetFloat("_ToonRampUVSelector", 7f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "The property '");
        }

        // --- Falsifier 2: SdfShadingFlipRefuses -----------------------

        /// <summary>
        /// The SDF shading sample multiplies the coordinate x by a
        /// light driven flip. An assigned texture at selector 0
        /// refuses, because the texture must never move. The
        /// coordinate rule of the spec admits only the selectors 1,
        /// 2, 3, 4, 5, 8, and 9. The selector 6 holds the polar
        /// coordinate and the selector 7 holds the distorted UV, so
        /// both refuse, because the bound texture would read a
        /// changed coordinate after the build. The rule holds when
        /// the texture sits unassigned and when the mode keyword
        /// sits off. The no-op guard asserts the twins first.
        /// </summary>
        [Test]
        public void SdfShadingFlipRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_SDFShadingTexture", true);
            EnableKeywords(material, "_LIGHTINGMODE_SDF");

            // The admitted twins. An unassigned texture never
            // samples, and a mode keyword that sits off never reads
            // it, so both states hold the rule at selector 0.
            var sdfTexture = material.GetTexture("_SDFShadingTexture");
            material.SetFloat("_SDFShadingTextureUV", 1f);
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));

            material.SetTexture("_SDFShadingTexture", null);
            material.SetFloat("_SDFShadingTextureUV", 0f);
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));

            material.SetTexture("_SDFShadingTexture", sdfTexture);
            material.SetFloat("_SDFShadingTextureUV", 0f);
            material.DisableKeyword("_LIGHTINGMODE_SDF");
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));
            EnableKeywords(material, "_LIGHTINGMODE_SDF");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_SDFShadingTextureUV'");

            // The polar and distorted selectors read UV0 data, so
            // they refuse like selector 0.
            material.SetFloat("_SDFShadingTextureUV", 6f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_SDFShadingTextureUV'");

            material.SetFloat("_SDFShadingTextureUV", 7f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_SDFShadingTextureUV'");
        }

        // --- Falsifier 3: MultilayerBorderMaskLodRefuses --------------

        /// <summary>
        /// The border mask holds two branches with two samplers. The
        /// branch with the explicit derivatives refuses. The no-op
        /// guard asserts the twin first. The twin keeps the LOD at
        /// zero, turns the sample on, and pages the mask through the
        /// plain branch.
        /// </summary>
        [Test]
        public void MultilayerBorderMaskLodRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_ShadowBorderMask", true);
            material.SetFloat("_ShadowBorderMapToggle", 1f);
            EnableKeywords(material, "_LIGHTINGMODE_MULTILAYER_MATH");

            material.SetFloat("_ShadowBorderMaskLOD", 0f);
            PoiyomiReviewedFixture.AssertCompanion(
                fixture.CaptureSingle(material),
                "_ShadowBorderMask");

            material.SetFloat("_ShadowBorderMaskLOD", 0.37f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_ShadowBorderMaskLOD'");

            // The admitted twin of the keyword scope. The rule runs
            // on its own mode keyword only, so a mode keyword that
            // sits off holds the rule at a broken LOD.
            material.DisableKeyword("_LIGHTINGMODE_MULTILAYER_MATH");
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));
        }

        // --- Falsifier 4: MochieSplitMaskFactsRefuse ------------------

        /// <summary>
        /// The split mask sample reads the same texture through its
        /// own selector, the merged transform, the split pan, and
        /// the split stochastic flag. All four facts must hold when
        /// the split sample runs on selector 0. The falsifier
        /// breaks each fact in turn and refuses on every one. The
        /// rule runs only while the split toggle sits on, so a
        /// split toggle at 0 holds the rule at a broken transform.
        /// The no-op guard asserts the twin first. The twin holds
        /// every fact and pages the maps.
        /// </summary>
        [Test]
        public void MochieSplitMaskFactsRefuse(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(
                fixture, material, "_MochieMetallicMaps", true);
            material.SetFloat("_MochieMetallicMapsStochastic", 0f);
            EnableKeywords(material, "MOCHIE_PBR");

            material.SetFloat("_PBRSplitMaskSample", 1f);
            material.SetFloat("_MochieMetallicMasksUV", 0f);
            material.SetVector(
                "_PBRMaskScaleTiling", new Vector4(1f, 1f, 0f, 0f));
            material.SetVector(
                "_MochieMetallicMasksPan", Vector4.zero);
            material.SetFloat("_PBRSplitMaskStochastic", 0f);
            PoiyomiReviewedFixture.AssertCompanion(
                fixture.CaptureSingle(material),
                "_MochieMetallicMaps");

            material.SetFloat("_PBRSplitMaskStochastic", 1f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_PBRSplitMaskStochastic'");
            material.SetFloat("_PBRSplitMaskStochastic", 0f);

            material.SetFloat("_MochieMetallicMasksUV", 1f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_MochieMetallicMasksUV'");
            material.SetFloat("_MochieMetallicMasksUV", 0f);

            material.SetVector(
                "_PBRMaskScaleTiling", new Vector4(1.37f, 1f, 0f, 0f));
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_PBRMaskScaleTiling'");
            material.SetVector(
                "_PBRMaskScaleTiling", new Vector4(1f, 1f, 0f, 0f));

            material.SetVector(
                "_MochieMetallicMasksPan", new Vector4(0f, 0.37f, 0f, 0f));
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_MochieMetallicMasksPan'");
            material.SetVector("_MochieMetallicMasksPan", Vector4.zero);

            // The admitted twin of the toggle scope. The split
            // sample never runs while the toggle sits off, so no
            // split fact matters then.
            material.SetFloat("_PBRSplitMaskSample", 0f);
            material.SetVector(
                "_PBRMaskScaleTiling", new Vector4(2f, 2f, 0f, 0f));
            PoiyomiReviewedFixture.AssertCompanion(
                fixture.CaptureSingle(material),
                "_MochieMetallicMaps");
        }

        // --- Falsifier 4b: MochieSplitOnBoundMapsRefuses --------------
        /// <summary>
        /// The Mochie packed map on another coordinate stays bound. Its
        /// split mask sample reads the bound texture at its own
        /// selector, so that selector must sit on the allow list of 1,
        /// 2, 3, 4, 5, 8, and 9. Selector 0 refuses, and so do the
        /// selectors 6 and 7 and NaN. The falsifier asserts the admitted
        /// twins first.
        /// </summary>
        [Test]
        public void MochieSplitOnBoundMapsRefuses(
            [Values(0f, 6f, 7f, float.NaN)] float value,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(
                fixture, material, "_MochieMetallicMaps", true);
            EnableKeywords(material, "MOCHIE_PBR");
            material.SetFloat("_PBRSplitMaskSample", 1f);
            material.SetFloat("_MochieMetallicMapsUV", 1f);
            material.SetFloat("_MochieMetallicMasksUV", 1f);
            AssertNoGroupTextureMoved(fixture.CaptureSingle(material));

            material.SetFloat("_MochieMetallicMasksUV", 9f);
            AssertNoGroupTextureMoved(fixture.CaptureSingle(material));

            material.SetFloat("_MochieMetallicMasksUV", value);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_MochieMetallicMasksUV'");
        }

        /// <summary>
        /// The three two keyword routes keep the shared selector
        /// rule. The keyword gate decides only between inert and
        /// the selector rule, so a selector of the allowed set
        /// keeps the texture bound, and the selector 6 reads UV0
        /// data and refuses with the selector name.
        /// </summary>
        [Test]
        public void TwoKeywordRoutesKeepTheirSelectorRule(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            foreach (var texture in new[]
                     {
                         "_AnisotropyMap",
                         "_HighColor_Tex",
                         "_Set_HighColorMask",
                     })
            {
                AssignTexture(fixture, material, texture, true);
                material.SetFloat(texture + "UV", 1f);
            }

            EnableKeywords(
                material,
                "MOCHIE_PBR",
                "GGX_ANISOTROPICS",
                "POI_STYLIZED_StylizedSpecular",
                "_STYLIZEDREFLECTIONMODE_UNITYCHAN");
            AssertNoGroupTextureMoved(fixture.CaptureSingle(material));

            foreach (var route in PoiyomiLightingRoutes.Routes())
            {
                if (route.Property == "_AnisotropyMap")
                {
                    Assert.That(
                        route.UseOf(material, false),
                        Is.EqualTo(AtlasRouteUse.OtherCoordinates),
                        "the anisotropy map must stay bound at" +
                        " selector 1");
                }
            }

            material.SetFloat("_AnisotropyMapUV", 6f);
            PoiyomiReviewedFixture.AssertRefused(
                fixture.CaptureSingle(material),
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_AnisotropyMapUV'");
        }

        // --- Falsifier 5: TwoLightingModesNeedBothConditions ----------

        /// <summary>
        /// Review Focus 5. The realistic mode keyword and the SDF
        /// mode keyword sit on together. Each lighting rule runs on
        /// its own mode keyword, so the SDF rule still refuses the
        /// assigned SDF texture at selector 0. The same use admits
        /// at selector 1. The no-op guard asserts the admitted
        /// selector first.
        /// </summary>
        [Test]
        public void TwoLightingModesNeedBothConditions(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = NewCapturedMaterial(fixture, twoPass);
            AssignTexture(fixture, material, "_SDFShadingTexture", true);
            EnableKeywords(
                material,
                "_LIGHTINGMODE_REALISTIC",
                "_LIGHTINGMODE_SDF");

            material.SetFloat("_SDFShadingTextureUV", 1f);
            PoiyomiReviewedFixture.AssertAdmitted(
                fixture.CaptureSingle(material));

            material.SetFloat("_SDFShadingTextureUV", 0f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'_SDFShadingTextureUV'");
        }
    }
}
