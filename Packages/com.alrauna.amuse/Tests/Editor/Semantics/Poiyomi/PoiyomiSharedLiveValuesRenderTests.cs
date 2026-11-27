using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The render oracle for the dead value families that no feature
    /// group file carries: the eight refused keywords of the review
    /// and the fourteen KeyOnly keywords that the record gives dead
    /// properties. The model drops the values of a dead family from
    /// the material key. A wrong member would merge two materials
    /// that render differently. Each case renders a lit subject with
    /// the family switch off, changes only the family values, and
    /// renders again. The two readbacks must match byte for byte.
    /// The liveness case turns the family switch on and proves that
    /// the same change moves the render there, so an equal pair
    /// means a dead family and not a broken fixture.
    /// </summary>
    public sealed class PoiyomiSharedLiveValuesRenderTests
    {
        private const float PerturbStep = 0.37f;
        private const int PatternSize = 16;
        private const int NoiseSize = 4;

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private Texture2D patternTexture;
        private Texture2D noiseTexture;
        private Texture2DArray noiseArrayTexture;
        private Texture previousBloomTexture;
        private Vector4 previousFogRatio;
        private float previousFogOffset;
        private float previousFogAttenuation;
        private float previousFogHeightStart;
        private float previousFogHeight;
        private float previousLtcgiEnabled;
        private int previousLtcgiCount;
        private Texture previousLtcgiVertices;
        private Texture previousLtcgiMatrix;
        private Texture previousLtcgiMagnitude;

        /// <summary>
        /// The per family fixture overrides of this run. The case
        /// setup builds them once per test, because they assign the
        /// textures of the case.
        /// </summary>
        private Dictionary<string, Action<Material>> fixtureOverrides;

        [SetUp]
        public void SetUp()
        {
            previousBloomTexture = Shader.GetGlobalTexture("_BloomPrePassTexture");
            previousFogRatio = Shader.GetGlobalVector("_CustomFogTextureToScreenRatio");
            previousFogOffset = Shader.GetGlobalFloat("_CustomFogOffset");
            previousFogAttenuation = Shader.GetGlobalFloat("_CustomFogAttenuation");
            previousFogHeightStart = Shader.GetGlobalFloat("_CustomFogHeightFogStartY");
            previousFogHeight = Shader.GetGlobalFloat("_CustomFogHeightFogHeight");
            previousLtcgiEnabled = Shader.GetGlobalFloat("_Udon_LTCGI_GlobalEnable");
            previousLtcgiCount = Shader.GetGlobalInteger("_Udon_LTCGI_ScreenCount");
            previousLtcgiVertices = Shader.GetGlobalTexture("_Udon_LTCGI_static_uniforms");
            previousLtcgiMatrix = Shader.GetGlobalTexture("_Udon_LTCGI_lut1");
            previousLtcgiMagnitude = Shader.GetGlobalTexture("_Udon_LTCGI_lut2");
            patternTexture = Track(NewPatternTexture());
            noiseTexture = Track(NewNoiseTexture());
            noiseArrayTexture = Track(NewNoiseArrayTexture());
            fixtureOverrides = BuildFamilyFixtureOverrides();
        }

        [TearDown]
        public void TearDown()
        {
            Shader.SetGlobalTexture("_BloomPrePassTexture", previousBloomTexture);
            Shader.SetGlobalVector("_CustomFogTextureToScreenRatio", previousFogRatio);
            Shader.SetGlobalFloat("_CustomFogOffset", previousFogOffset);
            Shader.SetGlobalFloat("_CustomFogAttenuation", previousFogAttenuation);
            Shader.SetGlobalFloat("_CustomFogHeightFogStartY", previousFogHeightStart);
            Shader.SetGlobalFloat("_CustomFogHeightFogHeight", previousFogHeight);
            Shader.SetGlobalFloat("_Udon_LTCGI_GlobalEnable", previousLtcgiEnabled);
            Shader.SetGlobalInteger("_Udon_LTCGI_ScreenCount", previousLtcgiCount);
            Shader.SetGlobalTexture("_Udon_LTCGI_static_uniforms", previousLtcgiVertices);
            Shader.SetGlobalTexture("_Udon_LTCGI_lut1", previousLtcgiMatrix);
            Shader.SetGlobalTexture("_Udon_LTCGI_lut2", previousLtcgiMagnitude);
            foreach (var obj in tracked)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            tracked.Clear();
            patternTexture = null;
            noiseTexture = null;
            noiseArrayTexture = null;
        }

        /// <summary>
        /// The KeyOnly keywords that the record section 6 gives dead
        /// properties. The list comes from the model, so a new KeyOnly
        /// family gets its oracle case without an edit here.
        /// </summary>
        private static IEnumerable<string> KeyOnlyFamilyKeywords()
        {
            return PoiyomiLiveValues.KeyOnlyFamilies()
                .Select(family => family.Name);
        }

        private static IEnumerable<string> SharedFamilyNames()
        {
            return PoiyomiLiveValues.RefusedKeywords
                .Concat(KeyOnlyFamilyKeywords());
        }

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var family in SharedFamilies())
            {
                yield return new TestCaseData(family.Name, false)
                    .SetName("Shared dead family '" + family.Name +
                             "' holds on the bundled row");
                yield return new TestCaseData(family.Name, true)
                    .SetName("Shared dead family '" + family.Name +
                             "' holds on the two pass row");
            }
        }

        private static IEnumerable<TestCaseData> LiveFamilyCases()
        {
            foreach (var family in SharedFamilies())
            {
                yield return new TestCaseData(family.Name)
                    .SetName("Shared live family '" + family.Name +
                             "' changes the render");
            }
        }

        private static IEnumerable<AtlasDeadValueFamily> SharedFamilies()
        {
            var names = new HashSet<string>(SharedFamilyNames());
            foreach (var family in PoiyomiLiveValues.Model.Families)
            {
                if (names.Contains(family.Name))
                {
                    yield return family;
                }
            }
        }

        /// <summary>
        /// One dead family of the oracle on one bundled row. The
        /// case material holds the family switch off. The family
        /// values change. The render must stay byte for byte equal.
        /// The no-op guards prove that the dead set of the model
        /// holds every perturbed name, so the case can never pass
        /// by perturbing nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(DeadFamilyCases))]
        public void DeadFamilyValuesDoNotChangeTheRender(
            string familyName, bool twoPass)
        {
            var family = FamilyByName(familyName);
            var shader = LoadShader(twoPass);
            var material = NewCaseMaterial(shader, family);
            TurnTheFamilyOff(material, family);

            Assert.That(
                family.IsOff(material, shader.name),
                Is.True,
                "no-op guard: the family '" + familyName +
                "' must be off with its switch disabled");

            var perturbed = PerturbedNamesOf(
                material, family, shader.name);
            Assert.That(
                perturbed, Is.Not.Empty,
                "no-op guard: the dead set of the model must hold " +
                "at least one member of the family '" + familyName +
                "'");
            foreach (var name in perturbed)
            {
                Assert.That(
                    family.Properties, Does.Contain(name),
                    "no-op guard: every perturbed name must be a " +
                    "member of the family under test");
                Assert.That(
                    PoiyomiLiveValues.Model.DeadProperties(
                        material, shader.name, NeverDynamic),
                    Does.Contain(name),
                    "no-op guard: the dead set of the model must " +
                    "hold the perturbed name '" + name + "'");
            }

            if (DeclaredPerturbedCount(shader, perturbed) == 0)
            {
                Assert.Ignore(
                    "no perturbed name of the family '" + familyName +
                    "' is a property or a declared uniform of the " +
                    "row. The case would pass vacuously.");
            }

            Assert.That(
                AtlasRouteTable.PoiyomiReviewedMainColor
                    .UnheldRequiredValue(material),
                Is.Null,
                "fixture precondition: the case material must hold " +
                "every value that the reviewed route requires");

            using var harness = new PoiyomiRenderHarness();
            if (familyName == "_POI_DEPTH_RIMLIGHT")
                harness.EnableDepthTexture();
            if (familyName == "POI_LTCGI")
                harness.SetLtcgiScreen();
            PinAmbientLight();
            // The first render compiles the variants of the row. Its
            // result is discarded.
            harness.Render(material);
            var before = harness.Render(material);
            AssertFrameLooksRendered(
                before,
                "row '" + shader.name + "', family '" + familyName +
                "' with its switch off");
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = harness.Render(material);
            var differentPixels = CountDifferentPixels(before, after);
            if (differentPixels > 0)
            {
                var liveMembers = LiveMembersOf(
                    harness, shader, family, before, perturbed);
                Assert.Fail(
                    "The values of the dead family '" + familyName +
                    "' changed the render" + RowName(twoPass) + ". " +
                    differentPixels + " of " + before.Length +
                    " pixels differ. The largest channel difference " +
                    "is " + LargestChannelDifference(before, after) +
                    " of 255. The live members are: " +
                    string.Join(", ", liveMembers) + ". Move the " +
                    "live members out of the family.");
            }

            // The row control proves the case is not vacuous. The
            // main color is a live value of every admitted row.
            var color = material.GetColor("_Color");
            material.SetColor(
                "_Color",
                new Color(
                    color.r + PerturbStep,
                    color.g + PerturbStep,
                    color.b + PerturbStep,
                    color.a));
            var control = harness.Render(material);
            Assert.That(
                BytesEqual(before, control),
                Is.False,
                "row control: the main color must change the render" +
                RowName(twoPass) + ". The shader may not have " +
                "compiled, or the subject may be out of frame.");
        }

        /// <summary>
        /// The liveness case of one family. The case computes the
        /// perturbed members with the switch off, then turns the
        /// switch on. The same perturbation must change the render,
        /// so the compiled shader reads the family and the dead case
        /// cannot pass trivially.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(LiveFamilyCases))]
        public void LiveFamilyValueChangesTheRender(string familyName)
        {
            var family = FamilyByName(familyName);
            var shader = LoadShader(false);
            var material = NewCaseMaterial(shader, family);
            TurnTheFamilyOff(material, family);

            // The perturbed members are the dead members. The case
            // computes them with the switch off.
            var perturbed = PerturbedNamesOf(
                material, family, shader.name);
            TurnTheFamilyOn(material, family);

            Assert.That(
                family.IsOff(material, shader.name),
                Is.False,
                "no-op guard: the family '" + familyName +
                "' must be on with its switch enabled");

            Assert.That(
                perturbed, Is.Not.Empty,
                "no-op guard: the dead set of the model must hold " +
                "at least one member of the family '" + familyName +
                "'");

            using var harness = new PoiyomiRenderHarness();
            if (familyName == "_POI_DEPTH_RIMLIGHT")
                harness.EnableDepthTexture();
            if (familyName == "POI_LTCGI")
                harness.SetLtcgiScreen();
            PinAmbientLight();
            harness.Render(material);
            var before = harness.Render(material);
            AssertFrameLooksRendered(
                before,
                "row '" + shader.name + "', family '" + familyName +
                "' with its switch on");
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = harness.Render(material);
            if (BytesEqual(before, after))
            {
                Assert.Fail(
                    "liveness guard: no perturbation of the family '" +
                    familyName + "' changed the render with its " +
                    "switch on. The dead case of this family can " +
                    "pass trivially. The perturbed names are: " +
                    string.Join(", ", perturbed) + ". The largest " +
                    "channel difference is " +
                    LargestChannelDifference(before, after) + " of " +
                    "255.");
            }
        }

        // --- the case material ----------------------------------------

        /// <summary>
        /// One material on the row with the pattern texture, the
        /// vendor default keyword state, and the family fixture
        /// overrides of the case. The vendor default state carries
        /// the seven fresh keywords, among them the masked vignette
        /// and the flat lighting mode.
        /// </summary>
        private Material NewCaseMaterial(
            Shader shader, AtlasDeadValueFamily family)
        {
            var material = Track(new Material(shader));
            material.SetTexture("_MainTex", patternTexture);
            material.SetFloat("_LightingMapMode", 0f);
            PinTheCaseFixture(material, family);
            return material;
        }

        /// <summary>
        /// The mode keywords of the lighting mode enum. They form
        /// one enum, and a fresh material carries the flat mode. A
        /// material with two mode keywords holds no compiled
        /// variant. The case turns every mode off before it turns
        /// its one mode on.
        /// </summary>
        private static readonly string[] LightingModeKeywords =
        {
            "_LIGHTINGMODE_TEXTURERAMP",
            "_LIGHTINGMODE_MULTILAYER_MATH",
            "_LIGHTINGMODE_SHADEMAP",
            "_LIGHTINGMODE_REALISTIC",
            "_LIGHTINGMODE_WRAPPED",
            "_LIGHTINGMODE_SKIN",
            "_LIGHTINGMODE_FLAT",
            "_LIGHTINGMODE_CLOTH",
            "_LIGHTINGMODE_SDF",
        };

        /// <summary>
        /// The per family fixture overrides, keyed by family name.
        /// An override turns the feature of the case visible or
        /// pins the state that the feature needs to show. The
        /// override never widens the family under test. The
        /// overrides assign the pattern or noise textures of the
        /// case, so the features render with content.
        /// </summary>
        private Dictionary<string, Action<Material>>
            BuildFamilyFixtureOverrides()
        {
            return new Dictionary<string, Action<Material>>
            {
                // The main gradation strength must sit away from
                // zero, so the gradation texture sample reaches the
                // base color.
                ["PROP_MAINGRADATIONTEX"] = material =>
                {
                    material.SetTexture(
                        "_MainGradationTex", patternTexture);
                    material.SetFloat("_MainGradationStrength", 0.5f);
                    material.SetFloat("_MainColorAdjustToggle", 1f);
                    material.SetFloat("_ColorGradingToggle", 1f);
                    material.EnableKeyword("COLOR_GRADING_HDR");
                },

                // The black light masking thresholds sit away from
                // their ends, so a threshold step moves the mask.
                ["POI_BLACKLIGHTMASKING"] = material =>
                {
                    material.SetFloat("_EnableAudioLink", 1f);
                    material.EnableKeyword("POI_AUDIOLINK");
                    material.SetFloat("_MainColorAdjustToggle", 1f);
                    material.SetFloat("_ColorGradingToggle", 1f);
                    material.EnableKeyword("COLOR_GRADING_HDR");
                    material.SetFloat("_MainBrightness", 0.5f);
                    material.SetFloat("_MainBrightnessGlobalMask", 1f);
                    material.SetFloat("_BlackLightMasking0GlobalMaskIndex", 1f);
                    material.SetFloat("_BlackLightMasking0GlobalMaskBlendType", 0f);
                    material.SetFloat("_BlackLightMasking0Key", 0.5f);
                    material.SetVector(
                        "_BlackLightMasking0Range",
                        new Vector4(0.1f, 0.5f, 0f, 0f));
                },

                // The vertex glitch needs a strength and an audio
                // band state, so the density and threshold steps
                // move the displaced vertices.
                ["POI_VERTEX_GLITCHING"] = material =>
                {
                    material.SetFloat("_EnableAudioLink", 1f);
                    material.EnableKeyword("POI_AUDIOLINK");
                    material.SetFloat(
                        "_VertexGlitchingAudioLinkEnabled", 1f);
                    material.SetFloat("_VertexGlitchStrength", 10f);
                    material.SetFloat("_VertexGlitchThreshold", -1f);
                    material.SetFloat("_VertexGlitchDensity", 0.5f);
                    material.SetFloat("_VertexGlitchFrequency", 8f);
                },

                // The texture glitch needs the map and a strength,
                // so the map and pan speed steps move the vertices.
                ["POI_VERTEX_GLITCHING_TEXTURE"] = material =>
                {
                    material.SetTexture(
                        "_VertexGlitchMap", noiseTexture);
                    material.SetFloat("_VertexGlitchingEnabled", 1f);
                    material.EnableKeyword("POI_VERTEX_GLITCHING");
                    material.SetFloat("_VertexGlitchStrength", 10f);
                    material.SetFloat("_VertexGlitchThreshold", -1f);
                    material.SetFloat("_VertexGlitchDensity", 0.5f);
                },

                // The masked vignette mode gates the shaded lighting
                // block, whose steps the members of the family
                // cover. The shade map mode shows the shadow steps.
                ["VIGNETTE_MASKED"] = material =>
                    TurnLightingModeOn(
                        material, "_LIGHTINGMODE_SHADEMAP"),

                // The wrapped mode shows its wrap members through
                // the wrap lighting term.
                ["_LIGHTINGMODE_WRAPPED"] = material =>
                    TurnLightingModeOn(
                        material, "_LIGHTINGMODE_WRAPPED"),

                ["POI_LTCGI"] = material => PinLtcgi(material),
                // The normal correction strength sits away from both
                // ends, so the correction step moves the normals.
                ["POI_NORMALCORRECT"] = material =>
                {
                    material.SetFloat("_NormalCorrectAmount", 0.5f);
                    TurnLightingModeOn(material, "_LIGHTINGMODE_SHADEMAP");
                },
                ["POI_MIRROR"] = material =>
                {
                    material.SetFloat("_MirrorTextureEnabled", 1f);
                    material.SetFloat("_MirrorTextureForceEnabled", 1f);
                    material.SetTexture("_MirrorTexture", patternTexture);
                    material.SetColor("_MirrorColor", new Color(0.5f, 0.3f, 0.7f, 0.5f));
                    material.EnableKeyword("POI_MIRROR_TEXTURE");
                },
                ["_POI_DEPTH_RIMLIGHT"] = material =>
                {
                    material.SetFloat("_DepthRimWidth", 8f);
                    material.SetFloat("_DepthRimAdd", 1f);
                    material.SetColor("_DepthRimColor", Color.red);
                },
                ["_LIGHTINGMODE_FLAT"] = material =>
                {
                    material.SetFloat("_EnableRimLighting", 1f);
                    material.SetFloat("_RimShadowToggle", 1f);
                    material.SetFloat("_RimBrightness", 0.4f);
                    material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                },

                // The bloom fog of the enable keyword draws with the
                // bloom fog keyword of the same block.
                ["POIBS_ENABLE"] = material =>
                {
                    PinFog(material);
                    material.SetFloat("_BSSBloomfog", 1f);
                    material.EnableKeyword("POIBS_BLOOMFOG");
                },

                // The bloom fog members draw the fog falloff.
                ["POIBS_BLOOMFOG"] = material =>
                {
                    PinFog(material);
                    material.SetFloat("_BSSEnabled", 1f);
                    material.EnableKeyword("POIBS_ENABLE");
                },

                // The height fog type draws its falloff with the
                // bloom fog keywords of the same block.
                ["BSSBLOOMFOGTYPE_HEIGHT"] = material =>
                {
                    PinFog(material);
                    material.SetFloat("_BSSEnabled", 1f);
                    material.SetFloat("_BSSBloomfog", 1f);
                    material.EnableKeyword("POIBS_ENABLE");
                    material.EnableKeyword("POIBS_BLOOMFOG");
                },

                // The first decal draws its tint over the subject.
                ["GEOM_TYPE_BRANCH"] = material =>
                {
                    material.SetTexture(
                        "_DecalTexture", noiseTexture);
                    material.SetColor(
                        "_DecalColor", new Color(0.4f, 0.5f, 0.6f));
                },

                // The second decal draws its tint over the subject.
                ["GEOM_TYPE_BRANCH_DETAIL"] = material =>
                {
                    material.SetTexture(
                        "_DecalTexture1", noiseTexture);
                    material.SetColor(
                        "_DecalColor1", new Color(0.4f, 0.5f, 0.6f));
                },

                // The third decal draws its tint over the subject.
                ["GEOM_TYPE_FROND"] = material =>
                {
                    material.SetTexture(
                        "_DecalTexture2", noiseTexture);
                    material.SetColor(
                        "_DecalColor2", new Color(0.4f, 0.5f, 0.6f));
                },

                // The fourth decal draws its tint over the subject.
                ["DEPTH_OF_FIELD_COC_VIEW"] = material =>
                {
                    material.SetTexture(
                        "_DecalTexture3", noiseTexture);
                    material.SetColor(
                        "_DecalColor3", new Color(0.4f, 0.5f, 0.6f));
                },

                // The decal mask needs a drawn decal, so the override
                // turns the first decal keyword on next to the mask
                // keyword of the family.
                ["PROP_DECALMASK"] = material =>
                {
                    material.SetFloat("_DecalEnabled", 1f);
                    material.EnableKeyword("GEOM_TYPE_BRANCH");
                    material.SetFloat("_DecalBlendAlpha", 0.25f);
                    material.SetTexture(
                        "_DecalTexture", noiseTexture);
                    material.SetColor(
                        "_DecalColor", new Color(0.4f, 0.5f, 0.6f));
                    material.SetTexture("_DecalMask", noiseTexture);
                },

                // The internal parallax shows its layered map under
                // the view of the subject camera.
                ["POI_INTERNALPARALLAX"] = material =>
                {
                    material.SetTexture(
                        "_ParallaxInternalMap", noiseTexture);
                    material.SetColor(
                        "_ParallaxInternalMinColor",
                        new Color(0.2f, 0.3f, 0.4f));
                    material.SetColor(
                        "_ParallaxInternalMaxColor",
                        new Color(0.7f, 0.6f, 0.5f));
                },

                // The flipbook draws its frame texture over the
                // subject.
                ["_SUNDISK_HIGH_QUALITY"] = material =>
                {
                    material.SetTexture(
                        "_FlipbookTexArray", noiseArrayTexture);
                    material.SetColor(
                        "_FlipbookColor",
                        new Color(0.4f, 0.5f, 0.6f));
                },

                // The glitter draws its sparkles over the subject.
                ["_SUNDISK_SIMPLE"] = material =>
                {
                    material.SetTexture(
                        "_GlitterColorMap", noiseTexture);
                    material.SetColor(
                        "_GlitterColor",
                        new Color(0.4f, 0.5f, 0.6f));
                    material.SetFloat("_GlitterBrightness", 1f);
                },
            };
        }

        private void PinLtcgi(Material material)
        {
            // Avatar mode reads vertices from the renderer. It still
            // reads the two UV slots from this auxiliary texture.
            var auxiliary = Track(new Texture2D(
                6, 1, TextureFormat.RGBAFloat, false, true));
            auxiliary.SetPixels(new Color[6]);
            auxiliary.Apply(false, false);
            var matrix = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/at.pimaker.ltcgi/Lookup Tables/ltc_mat_hdr.exr");
            var magnitude = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Packages/at.pimaker.ltcgi/Lookup Tables/ltc_mag_hdr.exr");
            Assert.That(matrix, Is.Not.Null);
            Assert.That(magnitude, Is.Not.Null);
            Shader.SetGlobalTexture("_Udon_LTCGI_static_uniforms", auxiliary);
            Shader.SetGlobalTexture("_Udon_LTCGI_lut1", matrix);
            Shader.SetGlobalTexture("_Udon_LTCGI_lut2", magnitude);
            Shader.SetGlobalFloat("_Udon_LTCGI_GlobalEnable", 1f);
            Shader.SetGlobalInteger("_Udon_LTCGI_ScreenCount", 1);
            material.SetFloat("_LightingCap", 4f);
            material.SetColor("_LTCGI_DiffuseColor", new Color(0.3f, 0.3f, 0.3f));
        }

        private void PinFog(Material material)
        {
            Shader.SetGlobalTexture("_BloomPrePassTexture", patternTexture);
            Shader.SetGlobalVector("_CustomFogTextureToScreenRatio", Vector4.one);
            Shader.SetGlobalFloat("_CustomFogOffset", 0f);
            Shader.SetGlobalFloat("_CustomFogAttenuation", 0.2f);
            Shader.SetGlobalFloat("_CustomFogHeightFogStartY", -506f);
            Shader.SetGlobalFloat("_CustomFogHeightFogHeight", 4f);
            material.SetFloat("_EnableEmission", 1f);
            material.SetFloat("_EmissionStrength", 0.1f);
            material.EnableKeyword("_EMISSION");
        }

        private static void TurnLightingModeOn(
            Material material, string mode)
        {
            switch (mode)
            {
                case "_LIGHTINGMODE_TEXTURERAMP": material.SetFloat("_LightingMode", 0f); break;
                case "_LIGHTINGMODE_MULTILAYER_MATH": material.SetFloat("_LightingMode", 1f); break;
                case "_LIGHTINGMODE_WRAPPED": material.SetFloat("_LightingMode", 2f); break;
                case "_LIGHTINGMODE_SKIN": material.SetFloat("_LightingMode", 3f); break;
                case "_LIGHTINGMODE_SHADEMAP": material.SetFloat("_LightingMode", 4f); break;
                case "_LIGHTINGMODE_FLAT": material.SetFloat("_LightingMode", 5f); break;
                case "_LIGHTINGMODE_REALISTIC": material.SetFloat("_LightingMode", 6f); break;
                case "_LIGHTINGMODE_CLOTH": material.SetFloat("_LightingMode", 7f); break;
                case "_LIGHTINGMODE_SDF": material.SetFloat("_LightingMode", 8f); break;
            }
            foreach (var keyword in LightingModeKeywords)
            {
                material.DisableKeyword(keyword);
            }

            material.EnableKeyword(mode);
        }

        private void PinTheCaseFixture(
            Material material, AtlasDeadValueFamily family)
        {
            if (fixtureOverrides.TryGetValue(
                    family.Name, out var overrideAction))
            {
                overrideAction(material);
            }
        }

        private static void TurnTheFamilyOff(
            Material material, AtlasDeadValueFamily family)
        {
            foreach (var name in family.Switches)
            {
                if (name.StartsWith(
                        "_LIGHTINGMODE_", StringComparison.Ordinal))
                {
                    TurnLightingModeOn(material,
                        name == "_LIGHTINGMODE_FLAT"
                            ? "_LIGHTINGMODE_WRAPPED"
                            : "_LIGHTINGMODE_FLAT");
                }
                else
                {
                    SetFamilyToggle(material, name, false);
                    material.DisableKeyword(name);
                }
            }
        }

        private static void TurnTheFamilyOn(
            Material material, AtlasDeadValueFamily family)
        {
            foreach (var name in family.Switches)
            {
                if (name.StartsWith(
                        "_LIGHTINGMODE_", StringComparison.Ordinal))
                {
                    TurnLightingModeOn(material, name);
                    continue;
                }

                SetFamilyToggle(material, name, true);
                material.EnableKeyword(name);
            }
        }

        private static void SetFamilyToggle(
            Material material, string keyword, bool enabled)
        {
            string property;
            switch (keyword)
            {
                case "GEOM_TYPE_BRANCH": property = "_DecalEnabled"; break;
                case "GEOM_TYPE_BRANCH_DETAIL": property = "_DecalEnabled1"; break;
                case "GEOM_TYPE_FROND": property = "_DecalEnabled2"; break;
                case "DEPTH_OF_FIELD_COC_VIEW": property = "_DecalEnabled3"; break;
                case "POI_INTERNALPARALLAX": property = "_PoiInternalParallax"; break;
                case "_SUNDISK_HIGH_QUALITY": property = "_EnableFlipbook"; break;
                case "_SUNDISK_SIMPLE": property = "_GlitterEnable"; break;
                case "POI_BLACKLIGHTMASKING": property = "_BlackLightMaskingEnabled"; break;
                case "POI_VERTEX_GLITCHING": property = "_VertexGlitchingEnabled"; break;
                case "POI_VERTEX_GLITCHING_TEXTURE": property = "_VertexGlitchingUseTexture"; break;
                case "POI_LTCGI": property = "_LTCGIEnabled"; break;
                case "VIGNETTE_MASKED": property = "_ShadingEnabled"; break;
                case "_POI_DEPTH_RIMLIGHT": property = "_EnableDepthRimLighting"; break;
                case "POI_MIRROR": property = "_EnableMirrorOptions"; break;
                case "POI_NORMALCORRECT": property = "_NormalCorrect"; break;
                case "POIBS_ENABLE": property = "_BSSEnabled"; break;
                case "POIBS_BLOOMFOG": property = "_BSSBloomfog"; break;
                case "BSSBLOOMFOGTYPE_HEIGHT": property = "_BSSBloomfogType"; break;
                default: return;
            }
            material.SetFloat(property, enabled ? 1f : 0f);
        }

        /// <summary>
        /// The members that move the render on their own. The helper
        /// gives each member one fresh case material on the caller
        /// harness, so the result names the defect for the data fix.
        /// A second harness would place a second scene at the same
        /// world position, and the two spheres would depth tie.
        /// </summary>
        private List<string> LiveMembersOf(
            PoiyomiRenderHarness harness,
            Shader shader,
            AtlasDeadValueFamily family,
            Color32[] baseRender,
            List<string> perturbed)
        {
            var live = new List<string>();
            foreach (var name in perturbed)
            {
                var probe = NewCaseMaterial(shader, family);
                TurnTheFamilyOff(probe, family);
                PerturbOneValue(probe, name);
                if (!BytesEqual(baseRender, harness.Render(probe)))
                {
                    live.Add(name);
                }
            }

            return live;
        }

        // --- the perturbation -----------------------------------------

        /// <summary>
        /// The family members that the dead set of the model holds
        /// for the case material.
        /// </summary>
        private static List<string> PerturbedNamesOf(
            Material material,
            AtlasDeadValueFamily family,
            string rowName)
        {
            var dead = PoiyomiLiveValues.Model.DeadProperties(
                material, rowName, NeverDynamic);
            var names = new List<string>();
            foreach (var name in family.Properties)
            {
                if (dead.Contains(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static bool NeverDynamic(string name)
        {
            return false;
        }

        /// <summary>
        /// One value moves by a fixed step. A color or vector gains
        /// 0.37 on every channel. A texture is replaced by the noise
        /// texture of its dimension. An implicit texture name is not
        /// a material float or vector: the scale and offset of a
        /// texture move through the texture setting methods, and
        /// Unity owns the texel size and HDR names, so those two
        /// stay unperturbed. A texture selector compares exact
        /// values and stays at its pinned value.
        /// </summary>
        private void PerturbOneValue(Material material, string name)
        {
            if (name.EndsWith("_ST"))
            {
                var texture = name.Substring(0, name.Length - 3);
                var scale = material.GetTextureScale(texture);
                material.SetTextureScale(
                    texture,
                    new Vector2(
                        scale.x + PerturbStep,
                        scale.y + PerturbStep));
                var offset = material.GetTextureOffset(texture);
                material.SetTextureOffset(
                    texture,
                    new Vector2(
                        offset.x + PerturbStep,
                        offset.y + PerturbStep));
                return;
            }

            if (name.EndsWith("_TexelSize") || name.EndsWith("_HDR"))
            {
                return;
            }

            if (name.EndsWith("UV"))
            {
                var selector = material.GetFloat(name);
                material.SetFloat(name, selector == 0f ? 1f : 0f);
                return;
            }

            var index = material.shader.FindPropertyIndex(name);
            if (index < 0)
            {
                // HasFloat stays silent for a name that the shader
                // does not declare. GetFloat would log an error.
                var saved = material.HasFloat(name)
                    ? material.GetFloat(name)
                    : 0f;
                material.SetFloat(name, saved + PerturbStep);
                return;
            }

            switch (material.shader.GetPropertyType(index))
            {
                case ShaderPropertyType.Color:
                    var color = material.GetColor(name);
                    material.SetColor(
                        name,
                        new Color(
                            color.r + PerturbStep,
                            color.g + PerturbStep,
                            color.b + PerturbStep,
                            color.a + PerturbStep));
                    return;
                case ShaderPropertyType.Vector:
                    var vector = material.GetVector(name);
                    material.SetVector(
                        name,
                        new Vector4(
                            vector.x + PerturbStep,
                            vector.y + PerturbStep,
                            vector.z + PerturbStep,
                            vector.w + PerturbStep));
                    return;
                case ShaderPropertyType.Texture:
                    PerturbTexture(material, name, index);
                    return;
                default:
                    PerturbScalarValue(material, name);
                    return;
            }
        }

        /// <summary>
        /// One scalar moves by a fixed step of 0.37, except for the
        /// exact value members: a texture selector stays at its
        /// pinned value, and any other float flips between the two
        /// exact states when it holds one. A toggle, enable, or
        /// invert flips between 0 and 1, and a mode, type, blend
        /// type, channel, select-or-shift, color space, or theme
        /// index steps to the next value.
        /// </summary>
        private static void PerturbScalarValue(
            Material material, string name)
        {
            if (name.EndsWith("Toggle") || name.EndsWith("Enabled") ||
                name.EndsWith("Invert") || name == "_ForceFlatRampedLightmap")
            {
                var toggle = material.GetFloat(name);
                material.SetFloat(name, toggle == 0f ? 1f : 0f);
                return;
            }

            if (name.EndsWith("Mode") || name.EndsWith("Type") ||
                name.EndsWith("BlendType") ||
                name.EndsWith("Channel") ||
                name.EndsWith("SelectOrShift") ||
                name.EndsWith("ColorSpace") ||
                name.EndsWith("ThemeIndex"))
            {
                material.SetFloat(
                    name, material.GetFloat(name) + 1f);
                return;
            }

            material.SetFloat(
                name, material.GetFloat(name) + PerturbStep);
        }

        private void PerturbTexture(
            Material material, string name, int index)
        {
            var dimension =
                material.shader.GetPropertyTextureDimension(index);
            if (dimension == TextureDimension.Tex2D)
            {
                material.SetTexture(name, noiseTexture);
                return;
            }

            if (dimension == TextureDimension.Tex2DArray)
            {
                material.SetTexture(name, noiseArrayTexture);
                return;
            }

            Assert.Fail(
                "fixture gap: the texture property '" + name +
                "' of the row has no noise texture of its" +
                " dimension");
        }

        /// <summary>
        /// How many perturbed names the row declares as a property
        /// or the model declares as a uniform. A case with none is
        /// vacuous, because no shader can read the perturbed values.
        /// </summary>
        private static int DeclaredPerturbedCount(
            Shader shader, List<string> perturbed)
        {
            var count = 0;
            foreach (var name in perturbed)
            {
                if (shader.FindPropertyIndex(name) >= 0 ||
                    PoiyomiLiveValues.Model.Declares(name))
                {
                    count++;
                }
            }

            return count;
        }

        // --- the lookups ----------------------------------------------

        private static AtlasDeadValueFamily FamilyByName(string name)
        {
            return SharedFamilies()
                .Single(family => family.Name == name);
        }

        private static Shader LoadShader(bool twoPass)
        {
            var guid = twoPass
                ? PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid
                : PoiyomiSourceAttestation.CanonicalShaderGuid;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed bundled Poiyomi " +
                "shader asset must resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed bundled Poiyomi " +
                "shader asset must load");
            return shader;
        }

        private static string RowName(bool twoPass)
        {
            return twoPass
                ? " of the two pass row"
                : " of the bundled row";
        }

        /// <summary>
        /// The occlusion factor multiplies the indirect light. The
        /// harness ambient sits at black for the toon light clamp,
        /// so the cases raise the ambient to a dim grey. The harness
        /// restores the ambient when it is disposed.
        /// </summary>
        private static void PinAmbientLight()
        {
            RenderSettings.ambientLight =
                new Color(0.35f, 0.35f, 0.35f);
        }

        // --- the compare ----------------------------------------------

        private static bool BytesEqual(Color32[] first, Color32[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (var index = 0; index < first.Length; index++)
            {
                if (first[index].r != second[index].r ||
                    first[index].g != second[index].g ||
                    first[index].b != second[index].b ||
                    first[index].a != second[index].a)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountDifferentPixels(
            Color32[] first, Color32[] second)
        {
            var count = 0;
            for (var index = 0; index < first.Length; index++)
            {
                if (first[index].r != second[index].r ||
                    first[index].g != second[index].g ||
                    first[index].b != second[index].b ||
                    first[index].a != second[index].a)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Two cheap checks on the frame. The frame must hold more
        /// than one color, and it must hold no pure magenta pixel.
        /// A magenta frame means a shader variant failed, so the
        /// liveness compare would pass on two identical error
        /// frames.
        /// </summary>
        private static void AssertFrameLooksRendered(
            Color32[] render, string context)
        {
            var distinct = new HashSet<int>();
            var magenta = 0;
            foreach (var pixel in render)
            {
                distinct.Add(
                    pixel.r << 16 | pixel.g << 8 | pixel.b);
                if (pixel.r == 255 && pixel.g == 0 && pixel.b == 255)
                {
                    magenta++;
                }
            }

            Assert.That(
                distinct.Count,
                Is.GreaterThan(1),
                context + ": the frame must not be uniform. The " +
                "subject may be out of frame.");
            Assert.That(
                magenta,
                Is.EqualTo(0),
                context + ": the frame must not hold the magenta of " +
                "the error shader. The keyword set of the case may " +
                "name a variant that fails to compile.");
        }

        private static int LargestChannelDifference(
            Color32[] first, Color32[] second)
        {
            var largest = 0;
            for (var index = 0; index < first.Length; index++)
            {
                largest = Math.Max(
                    largest,
                    Math.Abs(first[index].r - second[index].r));
                largest = Math.Max(
                    largest,
                    Math.Abs(first[index].g - second[index].g));
                largest = Math.Max(
                    largest,
                    Math.Abs(first[index].b - second[index].b));
                largest = Math.Max(
                    largest,
                    Math.Abs(first[index].a - second[index].a));
            }

            return largest;
        }

        // --- the textures ---------------------------------------------

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            tracked.Add(obj);
            return obj;
        }

        /// <summary>
        /// A deterministic mid grey pattern. The varied pixels keep
        /// the render from saturating.
        /// </summary>
        private static Texture2D NewPatternTexture()
        {
            var texture = new Texture2D(
                PatternSize, PatternSize, TextureFormat.RGBA32, false)
            {
                name = "AMUSE poiyomi render oracle main texture",
            };
            var pixels = new Color32[PatternSize * PatternSize];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % PatternSize;
                var y = index / PatternSize;
                pixels[index] = new Color32(
                    (byte)(120 + x * 4),
                    (byte)(100 + y * 4),
                    (byte)(140 + ((x + y) & 7) * 8),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// A deterministic four by four noise. The perturbed texture
        /// members swap to it, so a texture member moves the sample.
        /// </summary>
        private static Texture2D NewNoiseTexture()
        {
            var texture = new Texture2D(
                NoiseSize, NoiseSize, TextureFormat.RGBA32, false)
            {
                name = "AMUSE poiyomi render oracle noise",
            };
            var pixels = new Color32[NoiseSize * NoiseSize];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % NoiseSize;
                var y = index / NoiseSize;
                pixels[index] = new Color32(
                    (byte)(30 + x * 40),
                    (byte)(200 - y * 30),
                    (byte)(90 + ((x + y) & 3) * 30),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// A deterministic four by four texture array of four
        /// slices. The flipbook texture member of the refused
        /// flipbook family swaps to it, because the vendor declares
        /// the flipbook source as a texture array.
        /// </summary>
        private static Texture2DArray NewNoiseArrayTexture()
        {
            var texture = new Texture2DArray(
                NoiseSize, NoiseSize, 4, TextureFormat.RGBA32, false)
            {
                name = "AMUSE poiyomi render oracle noise array",
            };
            for (var slice = 0; slice < 4; slice++)
            {
                var pixels = new Color32[NoiseSize * NoiseSize];
                for (var index = 0; index < pixels.Length; index++)
                {
                    var x = index % NoiseSize;
                    var y = index / NoiseSize;
                    pixels[index] = new Color32(
                        (byte)(40 + slice * 30 + x * 30),
                        (byte)(190 - slice * 20 - y * 25),
                        (byte)(80 + ((x + y + slice) & 3) * 35),
                        255);
                }

                texture.SetPixels32(pixels, slice);
            }

            texture.Apply(false, false);
            return texture;
        }
    }
}
