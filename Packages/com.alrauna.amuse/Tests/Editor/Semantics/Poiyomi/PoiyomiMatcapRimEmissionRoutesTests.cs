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
    /// The capture tests of the 'Matcap rim emission masks' group of
    /// the reviewed Poiyomi 9.3 route. The record at
    /// docs/superpowers/investigations/2026-11-23-poiyomi-reviewed-route-
    /// keywords.md, sections 4.1, 4.3, 5, and 8, is the source. The
    /// tests capture real uses through the route table, so a route
    /// that never fires fails here. The four matcap textures stay
    /// neutral. The scrolling curves never move. Every falsifier
    /// asserts its admitted twin first.
    /// </summary>
    public sealed class PoiyomiMatcapRimEmissionRoutesTests
    {
        private const string GroupName = "Matcap rim emission masks";

        /// <summary>
        /// The four neutral matcap textures of the group. The record
        /// classes them neutral in section 4.3 and section 9. Their
        /// sample coordinates come from view vectors. The blend rule
        /// guards the one UV read in the matcap path.
        /// </summary>
        private static readonly string[] NeutralMatcapTextures =
        {
            "_Matcap",
            "_Matcap2",
            "_Matcap3",
            "_Matcap4",
        };

        /// <summary>
        /// The rows of the matcap blend rule, record section 5. Each
        /// row names one matcap slot with its keywords and its two
        /// rule properties.
        /// </summary>
        public sealed class MatcapSlotRow
        {
            internal string Label;
            internal string Keyword;
            internal string UvToBlend;
            internal string BlendVector;
            internal string Mask;

            public override string ToString()
            {
                return Label;
            }
        }

        private static IEnumerable<MatcapSlotRow> MatcapSlots()
        {
            yield return new MatcapSlotRow
            {
                Label = "matcap slot 0",
                Keyword = "POI_MATCAP0",
                UvToBlend = "_MatcapUVToBlend",
                BlendVector = "_MatCapBlendUV1",
                Mask = "_MatcapMask",
            };
            yield return new MatcapSlotRow
            {
                Label = "matcap slot 1",
                Keyword = "COLOR_GRADING_HDR_3D",
                UvToBlend = "_Matcap2UVToBlend",
                BlendVector = "_MatCap2ndBlendUV1",
                Mask = "_Matcap2Mask",
            };
            yield return new MatcapSlotRow
            {
                Label = "matcap slot 2",
                Keyword = "POI_MATCAP2",
                UvToBlend = "_Matcap3UVToBlend",
                BlendVector = "_MatCap3rdBlendUV1",
                Mask = "_Matcap3Mask",
            };
            yield return new MatcapSlotRow
            {
                Label = "matcap slot 3",
                Keyword = "POI_MATCAP3",
                UvToBlend = "_Matcap4UVToBlend",
                BlendVector = "_MatCap4thBlendUV1",
                Mask = "_Matcap4Mask",
            };
        }

        /// <summary>
        /// The rows of the emission rules, record section 5. Each row
        /// names one emission slot with its keyword, its center out
        /// rule property, its scrolling curve rule property, and its
        /// three companion textures. The curve of the slot follows
        /// the map selector and never moves.
        /// </summary>
        public sealed class EmissionSlotRow
        {
            internal string Label;
            internal string Keyword;
            internal string CenterOut;
            internal string UseCurve;
            internal string Mask;
            internal string MaskUv;
            internal string Map;
            internal string MapUv;
            internal string Curve;

            public override string ToString()
            {
                return Label;
            }
        }

        private static IEnumerable<EmissionSlotRow> EmissionSlots()
        {
            yield return new EmissionSlotRow
            {
                Label = "emission slot 0",
                Keyword = "_EMISSION",
                CenterOut = "_EmissionCenterOutEnabled",
                UseCurve = "_EmissionScrollingUseCurve",
                Mask = "_EmissionMask",
                MaskUv = "_EmissionMaskUV",
                Map = "_EmissionMap",
                MapUv = "_EmissionMapUV",
                Curve = "_EmissionScrollingCurve",
            };
            yield return new EmissionSlotRow
            {
                Label = "emission slot 1",
                Keyword = "POI_EMISSION_1",
                CenterOut = "_EmissionCenterOutEnabled1",
                UseCurve = "_EmissionScrollingUseCurve1",
                Mask = "_EmissionMask1",
                MaskUv = "_EmissionMask1UV",
                Map = "_EmissionMap1",
                MapUv = "_EmissionMap1UV",
                Curve = "_EmissionScrollingCurve1",
            };
            yield return new EmissionSlotRow
            {
                Label = "emission slot 2",
                Keyword = "POI_EMISSION_2",
                CenterOut = "_EmissionCenterOutEnabled2",
                UseCurve = "_EmissionScrollingUseCurve2",
                Mask = "_EmissionMask2",
                MaskUv = "_EmissionMask2UV",
                Map = "_EmissionMap2",
                MapUv = "_EmissionMap2UV",
                Curve = "_EmissionScrollingCurve2",
            };
            yield return new EmissionSlotRow
            {
                Label = "emission slot 3",
                Keyword = "POI_EMISSION_3",
                CenterOut = "_EmissionCenterOutEnabled3",
                UseCurve = "_EmissionScrollingUseCurve3",
                Mask = "_EmissionMask3",
                MaskUv = "_EmissionMask3UV",
                Map = "_EmissionMap3",
                MapUv = "_EmissionMap3UV",
                Curve = "_EmissionScrollingCurve3",
            };
        }

        /// <summary>
        /// The rows of the global mask split rule, record section 5.
        /// Each row names one global mask slot with its split flag,
        /// its three split transforms, and its three split pans.
        /// </summary>
        public sealed class GlobalMaskSlotRow
        {
            internal string Label;
            internal string Keyword;
            internal string Texture;
            internal string TextureUv;
            internal string Split;
            internal string[] SplitTransforms;
            internal string[] SplitPans;

            public override string ToString()
            {
                return Label;
            }
        }

        private static IEnumerable<GlobalMaskSlotRow> GlobalMaskSlots()
        {
            for (var slot = 0; slot < 4; slot++)
            {
                yield return new GlobalMaskSlotRow
                {
                    Label = "global mask slot " + slot,
                    Keyword = "POI_GLOBALMASK_TEXTURES",
                    Texture = "_GlobalMaskTexture" + slot,
                    TextureUv = "_GlobalMaskTexture" + slot + "UV",
                    Split = "_GlobalMaskTexture" + slot + "Split",
                    SplitTransforms = new[]
                    {
                        "_GlobalMaskTexture" + slot +
                        "SplitTilingOffset_G",
                        "_GlobalMaskTexture" + slot +
                        "SplitTilingOffset_B",
                        "_GlobalMaskTexture" + slot +
                        "SplitTilingOffset_A",
                    },
                    SplitPans = new[]
                    {
                        "_GlobalMaskTexture" + slot + "SplitPan_G",
                        "_GlobalMaskTexture" + slot + "SplitPan_B",
                        "_GlobalMaskTexture" + slot + "SplitPan_A",
                    },
                };
            }
        }

        // --- the inventory --------------------------------------------

        /// <summary>
        /// The group inventory test. The allowed list holds exactly
        /// the 21 keywords of the group in the record. The neutral
        /// list holds exactly the four matcap textures. The route set
        /// matches the shared inventory helper, so one route object
        /// must exist for every companion row of the group with the
        /// verified sampler of the record.
        /// </summary>
        [Test]
        public void GroupInventoryIsRouted()
        {
            var keywords = PoiyomiReviewedInventory.Keywords
                .Where(row => row.Group == GroupName)
                .Select(row => row.Keyword)
                .ToList();
            Assert.That(
                keywords, Has.Count.EqualTo(21),
                "fixture precondition: the record lists 21 keywords" +
                " for the group '" + GroupName + "'");
            Assert.That(
                PoiyomiMatcapRimEmissionRoutes.AllowedKeywords,
                Is.EquivalentTo(keywords),
                "the allowed keyword list holds exactly the keywords" +
                " of the group in the record");
            Assert.That(
                PoiyomiMatcapRimEmissionRoutes.NeutralTextures,
                Is.EquivalentTo(NeutralMatcapTextures),
                "the four matcap textures of the group stay neutral");

            PoiyomiReviewedInventory.AssertRoutesMatchInventory(
                GroupName,
                PoiyomiMatcapRimEmissionRoutes.Routes());
        }

        // --- the admitted features ------------------------------------

        /// <summary>
        /// Each test of this block enables one feature of the group,
        /// assigns each companion of the feature at selector 0, and
        /// captures a real use. The companions must have moved into
        /// companion pages. Admission alone is not proof, because an
        /// inert use is admitted too. The textures import before any
        /// keyword enables.
        /// </summary>
        [Test]
        public void MatcapMaskIsAdmitted([Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture("_MatcapMask", fixture.NewRouteTexture(64));
            material.SetFloat("_MatcapMaskUV", 0f);
            material.EnableKeyword("POI_MATCAP0");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_MatcapMask");
        }

        [Test]
        public void Matcap0CustomNormalIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Matcap0NormalMap", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap0NormalMapUV", 0f);
            material.EnableKeyword("POI_MATCAP0");
            material.EnableKeyword("POI_MATCAP0_CUSTOM_NORMAL");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Matcap0NormalMap");
        }

        [Test]
        public void MatcapSlot1MaskIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture("_Matcap2Mask", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap2MaskUV", 0f);
            material.EnableKeyword("COLOR_GRADING_HDR_3D");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_Matcap2Mask");
        }

        [Test]
        public void Matcap1CustomNormalIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Matcap1NormalMap", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap1NormalMapUV", 0f);
            material.EnableKeyword("COLOR_GRADING_HDR_3D");
            material.EnableKeyword("POI_MATCAP1_CUSTOM_NORMAL");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Matcap1NormalMap");
        }

        [Test]
        public void MatcapSlot2MaskIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture("_Matcap3Mask", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap3MaskUV", 0f);
            material.EnableKeyword("POI_MATCAP2");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_Matcap3Mask");
        }

        [Test]
        public void Matcap2CustomNormalIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Matcap2NormalMap", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap2NormalMapUV", 0f);
            material.EnableKeyword("POI_MATCAP2");
            material.EnableKeyword("POI_MATCAP2_CUSTOM_NORMAL");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Matcap2NormalMap");
        }

        [Test]
        public void MatcapSlot3MaskIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture("_Matcap4Mask", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap4MaskUV", 0f);
            material.EnableKeyword("POI_MATCAP3");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(outcome, "_Matcap4Mask");
        }

        [Test]
        public void Matcap3CustomNormalIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Matcap3NormalMap", fixture.NewRouteTexture(64));
            material.SetFloat("_Matcap3NormalMapUV", 0f);
            material.EnableKeyword("POI_MATCAP3");
            material.EnableKeyword("POI_MATCAP3_CUSTOM_NORMAL");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Matcap3NormalMap");
        }

        /// <summary>
        /// The emission slot test. The mask and the map move into
        /// companion pages. The scrolling curve stays bound. The
        /// record classes the curve companion, but its exceptions
        /// column says it never moves. Its sample follows the map
        /// selector and the route value rule refuses an assigned
        /// curve in use.
        /// </summary>
        [Test]
        public void EmissionSlotIsAdmitted(
            [ValueSource(nameof(EmissionSlots))] EmissionSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Map, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Curve, fixture.NewRouteTexture(64));
            material.SetFloat(slot.MaskUv, 0f);
            material.SetFloat(slot.MapUv, 0f);
            material.SetFloat(slot.CenterOut, 0f);
            material.SetFloat(slot.UseCurve, 0f);
            material.EnableKeyword(slot.Keyword);

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, slot.Mask, slot.Map);
            var moved = outcome.Use.Companions
                .Select(companion => companion.Property)
                .ToList();
            Assert.That(
                moved, Does.Not.Contain(slot.Curve),
                "the scrolling curve '" + slot.Curve +
                "' never moves. It stays bound.");
        }

        /// <summary>
        /// The rim test. The master rim keyword and the style keyword
        /// are both on. The companions of the style move into
        /// companion pages.
        /// </summary>
        [Test]
        public void RimPoiyomiStyleIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture("_RimMask", fixture.NewRouteTexture(64));
            material.SetTexture("_RimTex", fixture.NewRouteTexture(64));
            material.SetFloat("_RimMaskUV", 0f);
            material.SetFloat("_RimTexUV", 0f);
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            material.EnableKeyword("_RIMSTYLE_POIYOMI");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_RimMask", "_RimTex");
        }

        [Test]
        public void RimUts2StyleIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Set_RimLightMask", fixture.NewRouteTexture(64));
            material.SetFloat("_Set_RimLightMaskUV", 0f);
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            material.EnableKeyword("_RIMSTYLE_UTS2");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Set_RimLightMask");
        }

        [Test]
        public void RimLilToonStyleIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_RimColorTex", fixture.NewRouteTexture(64));
            material.SetFloat("_RimColorTexUV", 0f);
            material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
            material.EnableKeyword("_RIMSTYLE_LILTOON");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_RimColorTex");
        }

        [Test]
        public void Rim2PoiyomiStyleIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture("_Rim2Mask", fixture.NewRouteTexture(64));
            material.SetTexture("_Rim2Tex", fixture.NewRouteTexture(64));
            material.SetFloat("_Rim2MaskUV", 0f);
            material.SetFloat("_Rim2TexUV", 0f);
            material.EnableKeyword("POI_RIM2");
            material.EnableKeyword("_RIM2STYLE_POIYOMI");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Rim2Mask", "_Rim2Tex");
        }

        [Test]
        public void Rim2Uts2StyleIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Set_Rim2LightMask", fixture.NewRouteTexture(64));
            material.SetFloat("_Set_Rim2LightMaskUV", 0f);
            material.EnableKeyword("POI_RIM2");
            material.EnableKeyword("_RIM2STYLE_UTS2");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Set_Rim2LightMask");
        }

        [Test]
        public void Rim2LilToonStyleIsAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(
                "_Rim2ColorTex", fixture.NewRouteTexture(64));
            material.SetFloat("_Rim2ColorTexUV", 0f);
            material.EnableKeyword("POI_RIM2");
            material.EnableKeyword("_RIM2STYLE_LILTOON");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome, "_Rim2ColorTex");
        }

        /// <summary>
        /// The global mask test. All four mask textures move into
        /// companion pages at selector 0 with the split sampling off.
        /// </summary>
        [Test]
        public void GlobalMaskTexturesAreAdmitted(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            for (var slot = 0; slot < 4; slot++)
            {
                material.SetTexture(
                    "_GlobalMaskTexture" + slot,
                    fixture.NewRouteTexture(64));
                material.SetFloat("_GlobalMaskTexture" + slot + "UV", 0f);
                material.SetFloat("_GlobalMaskTexture" + slot + "Split", 0f);
            }

            material.EnableKeyword("POI_GLOBALMASK_TEXTURES");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                outcome,
                "_GlobalMaskTexture0",
                "_GlobalMaskTexture1",
                "_GlobalMaskTexture2",
                "_GlobalMaskTexture3");
        }

        // --- Falsifier 5: MatcapTextureIsNeutralAtAnySelector --------
        /// <summary>
        /// The matcap texture stays bound and is no companion at any
        /// selector of the blend rule. The blend vector stays zero,
        /// so the use holds the rule at every selector value. The
        /// matcap textures have no selector property of their own.
        /// Their transforms move the matcap coordinate, which no page
        /// ever moves.
        /// </summary>
        [Test]
        public void MatcapTextureIsNeutralAtAnySelector(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            foreach (var texture in NeutralMatcapTextures)
            {
                material.SetTexture(
                    texture, fixture.NewRouteTexture(64));
            }

            material.SetTexture("_MatcapMask", fixture.NewRouteTexture(64));
            material.SetTexture("_Matcap2Mask", fixture.NewRouteTexture(64));
            material.SetTexture("_Matcap3Mask", fixture.NewRouteTexture(64));
            material.SetTexture("_Matcap4Mask", fixture.NewRouteTexture(64));
            material.SetFloat("_MatcapMaskUV", 0f);
            material.SetFloat("_Matcap2MaskUV", 0f);
            material.SetFloat("_Matcap3MaskUV", 0f);
            material.SetFloat("_Matcap4MaskUV", 0f);
            material.SetVector("_MatCapBlendUV1", Vector4.zero);
            material.SetVector("_MatCap2ndBlendUV1", Vector4.zero);
            material.SetVector("_MatCap3rdBlendUV1", Vector4.zero);
            material.SetVector("_MatCap4thBlendUV1", Vector4.zero);
            material.EnableKeyword("POI_MATCAP0");
            material.EnableKeyword("COLOR_GRADING_HDR_3D");
            material.EnableKeyword("POI_MATCAP2");
            material.EnableKeyword("POI_MATCAP3");

            var slots = MatcapSlots().ToList();
            foreach (var selector in new float[]
                     { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f })
            {
                foreach (var slot in slots)
                {
                    material.SetFloat(slot.UvToBlend, selector);
                }

                var outcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertAdmitted(outcome);
                var moved = outcome.Use.Companions
                    .Select(companion => companion.Property)
                    .ToList();
                Assert.That(
                    moved, Does.Contain("_MatcapMask"),
                    "the matcap mask of slot 0 must move");
                Assert.That(
                    moved, Does.Contain("_Matcap2Mask"),
                    "the matcap mask of slot 1 must move");
                Assert.That(
                    moved, Does.Contain("_Matcap3Mask"),
                    "the matcap mask of slot 2 must move");
                Assert.That(
                    moved, Does.Contain("_Matcap4Mask"),
                    "the matcap mask of slot 3 must move");
                foreach (var texture in NeutralMatcapTextures)
                {
                    Assert.That(
                        moved, Does.Not.Contain(texture),
                        "the matcap texture '" + texture +
                        "' stays bound at blend selector " + selector);
                }
            }
        }

        // --- the falsifiers -------------------------------------------

        /// <summary>
        /// Each falsifier of this block breaks one table rule of the
        /// group and refuses with RequiredValueNotHeld. The no-op
        /// guard asserts the admitted twin first, so a refusal that
        /// comes from an unrelated gate cannot pass the test.
        /// </summary>

        // --- Falsifier 1: MatcapBlendSelectorRefuses ------------------
        [Test]
        public void MatcapBlendSelectorRefuses(
            [ValueSource(nameof(MatcapSlots))] MatcapSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetFloat(slot.Mask + "UV", 0f);
            material.EnableKeyword(slot.Keyword);

            // The common admitted twin. The vendor default selector
            // is 1, and a UV blend at the exact selector 1 with a
            // non-zero blend vector admits.
            material.SetFloat(slot.UvToBlend, 1f);
            material.SetVector(
                slot.BlendVector, new Vector4(0.5f, 0.5f, 0f, 0f));
            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(twin, slot.Mask);

            // The zero selector twin. The shader reads only the x
            // and y of the blend vector, so a non-zero z and w with
            // x and y at zero admits.
            material.SetFloat(slot.UvToBlend, 0f);
            material.SetVector(
                slot.BlendVector, new Vector4(0f, 0f, 1f, 1f));
            var zeroTwin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(zeroTwin, slot.Mask);

            // Every selector that is not an exact allow list value
            // refuses, because the shader truncates the float to an
            // array index. A fraction, a negative value, and a NaN
            // read UV0, the polar UV, or the distorted UV.
            material.SetVector(
                slot.BlendVector, new Vector4(1f, 1f, 1f, 1f));
            foreach (var selector in new float[]
                     {
                         0f,
                         6f,
                         7f,
                         0.5f,
                         -0.5f,
                         6.5f,
                         7.5f,
                         float.NaN,
                     })
            {
                material.SetFloat(slot.UvToBlend, selector);
                var outcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertRefused(
                    outcome,
                    AtlasUseRefusal.RequiredValueNotHeld,
                    "'" + slot.UvToBlend + "'");
            }
        }

        // --- Falsifier 1 scope: the rule follows its keyword ---------
        [Test]
        public void MatcapBlendRuleIsKeywordScoped(
            [ValueSource(nameof(MatcapSlots))] MatcapSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            // The keyword stays off. The broken rule value must not
            // refuse, because the rule follows its keyword.
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetFloat(slot.Mask + "UV", 0f);
            material.SetFloat(slot.UvToBlend, 0f);
            material.SetVector(
                slot.BlendVector, new Vector4(1f, 1f, 1f, 1f));

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        // --- Falsifier 2: EmissionCenterOutRefuses --------------------
        [Test]
        public void EmissionCenterOutRefuses(
            [ValueSource(nameof(EmissionSlots))] EmissionSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Map, fixture.NewRouteTexture(64));
            material.SetFloat(slot.MaskUv, 0f);
            material.SetFloat(slot.MapUv, 0f);
            material.SetFloat(slot.CenterOut, 0f);
            material.EnableKeyword(slot.Keyword);

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, slot.Mask, slot.Map);

            material.SetFloat(slot.CenterOut, 1f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'" + slot.CenterOut + "'");
        }

        // --- Falsifier 2 scope: the rule follows its keyword ---------
        [Test]
        public void EmissionCenterOutRuleIsKeywordScoped(
            [ValueSource(nameof(EmissionSlots))] EmissionSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            // The keyword stays off. The broken center out value
            // with an assigned map on selector 0 must not refuse.
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Map, fixture.NewRouteTexture(64));
            material.SetFloat(slot.MaskUv, 0f);
            material.SetFloat(slot.MapUv, 0f);
            material.SetFloat(slot.CenterOut, 1f);

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        // --- Falsifier 3: EmissionScrollingCurveRefuses ---------------
        [Test]
        public void EmissionScrollingCurveRefuses(
            [ValueSource(nameof(EmissionSlots))] EmissionSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Map, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Curve, fixture.NewRouteTexture(64));
            material.SetFloat(slot.MaskUv, 0f);
            material.SetFloat(slot.MapUv, 0f);
            material.SetFloat(slot.CenterOut, 0f);
            material.SetFloat(slot.UseCurve, 0f);
            material.EnableKeyword(slot.Keyword);

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(
                twin, slot.Mask, slot.Map);

            material.SetFloat(slot.UseCurve, 1f);
            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.RequiredValueNotHeld,
                "'" + slot.UseCurve + "'");
        }

        // --- Falsifier 3 scope: the rule follows its keyword ---------
        [Test]
        public void EmissionScrollingRuleIsKeywordScoped(
            [ValueSource(nameof(EmissionSlots))] EmissionSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            // The keyword stays off. The broken use curve value with
            // an assigned curve must not refuse.
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Mask, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Map, fixture.NewRouteTexture(64));
            material.SetTexture(slot.Curve, fixture.NewRouteTexture(64));
            material.SetFloat(slot.MaskUv, 0f);
            material.SetFloat(slot.MapUv, 0f);
            material.SetFloat(slot.CenterOut, 0f);
            material.SetFloat(slot.UseCurve, 1f);

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        // --- Falsifier 4: GlobalMaskSplitRefuses ----------------------
        [Test]
        public void GlobalMaskSplitRefuses(
            [ValueSource(nameof(GlobalMaskSlots))] GlobalMaskSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Texture, fixture.NewRouteTexture(64));
            material.SetFloat(slot.TextureUv, 0f);
            material.SetFloat(slot.Split, 1f);
            foreach (var transform in slot.SplitTransforms)
            {
                material.SetVector(
                    transform, new Vector4(1f, 1f, 0f, 0f));
            }

            foreach (var pan in slot.SplitPans)
            {
                material.SetVector(pan, Vector4.zero);
            }

            material.EnableKeyword(slot.Keyword);

            var twin = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(twin, slot.Texture);

            // The split flag at zero is a safe state. All three
            // split transforms may hold junk then, because the split
            // sample never runs.
            material.SetFloat(slot.Split, 0f);
            foreach (var transform in slot.SplitTransforms)
            {
                material.SetVector(
                    transform, new Vector4(2f, 3f, 0f, 0f));
            }

            var safe = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertCompanion(safe, slot.Texture);

            material.SetFloat(slot.Split, 1f);
            foreach (var transform in slot.SplitTransforms)
            {
                material.SetVector(
                    transform, new Vector4(1f, 1f, 0f, 0f));
            }

            foreach (var pan in slot.SplitPans)
            {
                material.SetVector(pan, Vector4.zero);
            }

            // A split read refuses every channel transform that is
            // not the identity, one channel at a time.
            foreach (var transform in slot.SplitTransforms)
            {
                material.SetVector(
                    transform, new Vector4(2f, 3f, 0f, 0f));
                var outcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertRefused(
                    outcome,
                    AtlasUseRefusal.RequiredValueNotHeld,
                    "'" + transform + "'");
                material.SetVector(
                    transform, new Vector4(1f, 1f, 0f, 0f));
            }

            // A split read refuses every channel pan that is not
            // zero, one channel at a time.
            foreach (var pan in slot.SplitPans)
            {
                material.SetVector(
                    pan, new Vector4(1f, 0f, 0f, 0f));
                var panOutcome = fixture.CaptureSingle(material);
                PoiyomiReviewedFixture.AssertRefused(
                    panOutcome,
                    AtlasUseRefusal.RequiredValueNotHeld,
                    "'" + pan + "'");
                material.SetVector(pan, Vector4.zero);
            }
        }

        // --- Falsifier 4 scope: the rule follows its keyword ---------
        [Test]
        public void GlobalMaskSplitRuleIsKeywordScoped(
            [ValueSource(nameof(GlobalMaskSlots))] GlobalMaskSlotRow slot,
            [Values(false, true)] bool twoPass)
        {
            // The keyword stays off. The broken split flag with junk
            // split values and an assigned mask must not refuse.
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.SetTexture(slot.Texture, fixture.NewRouteTexture(64));
            material.SetFloat(slot.TextureUv, 0f);
            material.SetFloat(slot.Split, 1f);
            foreach (var transform in slot.SplitTransforms)
            {
                material.SetVector(
                    transform, new Vector4(2f, 3f, 0f, 0f));
            }

            foreach (var pan in slot.SplitPans)
            {
                material.SetVector(pan, Vector4.zero);
            }

            material.SetVector(
                slot.SplitPans[0], new Vector4(1f, 0f, 0f, 0f));

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertAdmitted(outcome);
        }

        // --- the rule property guards --------------------------------

        /// <summary>
        /// An animation on a rule property refuses the use. The
        /// capture reads the rule values from the material. A clip
        /// that drives one could hold a broken value at runtime.
        /// </summary>
        [Test]
        public void AnimatedRulePropertyRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.EnableKeyword("_EMISSION");
            fixture.AnimateScalar(fixture.Root, "_EmissionScrollingUseCurve");

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.AnimatedRouteProperty,
                "_EmissionScrollingUseCurve");
        }

        /// <summary>
        /// A property block on a rule property refuses the use. The
        /// block twin of the animated case.
        /// </summary>
        [Test]
        public void BlockedRulePropertyRefuses(
            [Values(false, true)] bool twoPass)
        {
            using var fixture = new PoiyomiReviewedFixture();
            var material = fixture.NewMaterial(twoPass);
            material.SetTexture("_MainTex", fixture.NewRouteTexture(64));
            material.EnableKeyword("_EMISSION");
            var renderer = fixture.Root
                .GetComponentInChildren<SkinnedMeshRenderer>();
            var block = new MaterialPropertyBlock();
            block.SetFloat("_EmissionScrollingUseCurve", 1f);
            renderer.SetPropertyBlock(block);

            var outcome = fixture.CaptureSingle(material);
            PoiyomiReviewedFixture.AssertRefused(
                outcome,
                AtlasUseRefusal.PropertyBlockOnRouteProperty,
                "_EmissionScrollingUseCurve");
        }
    }
}
