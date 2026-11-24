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
    /// The render oracle for the dead value families of the 'Color,
    /// effects, and UV' group of the reviewed Poiyomi 9.3 route. The
    /// model drops the values of a dead family from the material
    /// key. A wrong member would merge two materials that render
    /// differently. Each case renders a lit sphere with every group
    /// keyword of the record on, except the switch of the family. It
    /// changes only the family values and renders again. The two
    /// readbacks must match byte for byte. The liveness case turns
    /// the family switch on and proves that the same change moves
    /// the render there. So an equal pair means a dead family and
    /// not a broken fixture.
    /// </summary>
    public sealed class PoiyomiColorEffectsLiveValuesRenderTests
    {
        private const float PerturbStep = 0.37f;
        private const int PatternSize = 16;
        private const int NoiseSize = 4;

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private Texture2D patternTexture;
        private Texture2D noiseTexture;
        private DefaultReflectionMode previousDefaultReflectionMode;
        private Material previousSkybox;
        private float previousAudioLinkAnimToggle;
        private Texture previousAudioTexture;

        [SetUp]
        public void SetUp()
        {
            patternTexture = Track(NewPatternTexture());
            noiseTexture = Track(NewNoiseTexture());
            // The fixture pins the skybox reflection mode with an
            // empty skybox for the run and restores both in the
            // teardown.
            previousDefaultReflectionMode =
                RenderSettings.defaultReflectionMode;
            previousSkybox = RenderSettings.skybox;
            RenderSettings.defaultReflectionMode =
                DefaultReflectionMode.Skybox;
            RenderSettings.skybox = null;
            // The audio link availability pin of the decal and the
            // volume color cases sets these two globals. Both are
            // saved here and restored in the teardown.
            previousAudioLinkAnimToggle =
                Shader.GetGlobalFloat("_AudioLinkAnimToggle");
            previousAudioTexture =
                Shader.GetGlobalTexture("_AudioTexture");
        }

        [TearDown]
        public void TearDown()
        {
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
            // The audio link availability pin of the decal and the
            // volume color cases restores here.
            Shader.SetGlobalFloat(
                "_AudioLinkAnimToggle", previousAudioLinkAnimToggle);
            Shader.SetGlobalTexture(
                "_AudioTexture", previousAudioTexture);
            RenderSettings.skybox = previousSkybox;
            RenderSettings.defaultReflectionMode =
                previousDefaultReflectionMode;
        }

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var family in PoiyomiColorEffectsRoutes.Families())
            {
                foreach (var half in new[] { "A", "B" })
                {
                    yield return new TestCaseData(family.Name, false, half)
                        .SetName("Dead family '" + family.Name +
                                 "' half " + half +
                                 " holds on the bundled row");
                    yield return new TestCaseData(family.Name, true, half)
                        .SetName("Dead family '" + family.Name +
                                 "' half " + half +
                                 " holds on the two pass row");
                }
            }
        }

        private static IEnumerable<TestCaseData> LiveFamilyCases()
        {
            foreach (var family in PoiyomiColorEffectsRoutes.Families())
            {
                yield return new TestCaseData(family.Name)
                    .SetName("Live family '" + family.Name +
                             "' changes the render");
            }
        }

        /// <summary>
        /// The families whose live values this scene cannot show.
        /// Both entries are controller-ruled harness gaps: the
        /// harness camera renders no depth texture, and the subject
        /// sits far beyond the uzumore near plane window. Observed
        /// 2026-11-24 with the fix round 2 pins.
        /// </summary>
        private static readonly Dictionary<string, string>
            FamiliesTheOracleCannotShow =
                new Dictionary<string, string>
        {
            ["POI_DEPTHBULGE"] =
                "The depth bulge returns before it reads its values " +
                "when the scene holds no camera depth texture. The " +
                "harness camera renders no depth texture. Observed " +
                "2026-11-24.",
            ["POI_UZUMORE"] =
                "The push applies only to vertices nearer than the " +
                "near plane plus the bias. The subject sits far " +
                "beyond the near plane. Observed 2026-11-24.",
        };

        /// <summary>
        /// One dead family of the group on one bundled row, in two
        /// halves. Each half turns on half of the other group
        /// keywords, and every other keyword sits on in exactly one
        /// half, so the falsification power of the all-on fixture
        /// stays while the compiled variant carries roughly half the
        /// feature code per draw. The family values change. The
        /// render must stay byte for byte equal. The no-op guard
        /// proves that the dead set of the model holds every
        /// perturbed name, so the case can never pass by perturbing
        /// nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(DeadFamilyCases))]
        public void DeadFamilyValuesDoNotChangeTheRender(
            string familyName, bool twoPass, string half)
        {
            var family = FamilyByName(familyName);
            var shader = LoadShader(twoPass);
            var material = NewCaseMaterial(shader, family);
            TurnHalfOfTheOtherGroupKeywordsOn(
                material, family, half);
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
            // The first render compiles the variants of the row. Its
            // result is discarded. The second and third renders probe
            // the fixture: two unperturbed renders of the same state
            // must match, so a later difference comes from the family
            // perturbation and not from the fixture.
            harness.Render(material);
            var unperturbedOne = harness.Render(material);
            var unperturbedTwo = harness.Render(material);
            var probeStable = BytesEqual(
                unperturbedOne, unperturbedTwo);

            var before = unperturbedTwo;
            var movedNames = new List<string>();
            foreach (var name in perturbed)
            {
                if (PerturbOneValue(material, name))
                {
                    movedNames.Add(name);
                }
            }

            Assert.That(
                movedNames, Is.Not.Empty,
                "no-op guard: at least one member of the family '" +
                familyName + "' must move under the fixture. " +
                "Members that stay are the exact value members of " +
                "the perturbation table.");

            var after = harness.Render(material);
            var differentPixels = CountDifferentPixels(before, after);
            if (differentPixels > 0)
            {
                Assert.Fail(
                    "The values of the dead family '" + familyName +
                    "' changed the render" + RowName(twoPass) + ". " +
                    differentPixels + " of " + before.Length +
                    " pixels differ. The largest channel difference " +
                    "is " + LargestChannelDifference(before, after) +
                    " of 255. The unperturbed probe " +
                    (probeStable ? "was stable" : "was UNSTABLE") +
                    ". A member of the family is live under this " +
                    "state. Move the live member out of the family, " +
                    "or fix the fixture.");
            }

            // The row control proves the case is not vacuous. The
            // main color drops to a dark grey and a fresh directional
            // light adds a new light term at the same time. Several
            // family states replace or wash the albedo, so either
            // control alone can go blind. A frame that ignores both
            // is not a rendered subject. Ruling 3 of the fix round.
            material.SetColor(
                "_Color",
                new Color(
                    0.3f, 0.3f, 0.3f, material.GetColor("_Color").a));
            var controlLightObject = new GameObject(
                "AMUSE poiyomi control light");
            controlLightObject.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            var controlLight =
                controlLightObject.AddComponent<Light>();
            controlLight.type = LightType.Directional;
            controlLight.intensity = 1.5f;
            controlLight.shadows = LightShadows.None;
            Track(controlLightObject);
            var control = harness.Render(material);
            Assert.That(
                BytesEqual(before, control),
                Is.False,
                "row control: the main color must change the render" +
                RowName(twoPass) + ". The frame under test is " +
                Describe(control) + ". The shader may not have " +
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
            if (FamiliesTheOracleCannotShow.TryGetValue(
                    familyName, out var reason))
            {
                Assert.Ignore(
                    "the render oracle cannot show the family '" +
                    familyName + "'. " + reason);
            }

            var shader = LoadShader(false);
            var material = NewCaseMaterial(shader, family);
            TurnEveryGroupKeywordOn(material);
            TurnTheFamilyOff(material, family);

            // The perturbed members are the dead members. The case
            // computes them with the switch off. The fixture
            // overrides sit on the case material already, so the
            // dead and the live case share one fixture.
            var perturbed = PerturbedNamesOf(
                material, family, shader.name);
            TurnTheFamilyOn(material, family);
            // Every live case holds exactly one stochastic mode. Both
            // together compile both sampling stacks into the variant
            // and leave the hex tile macros as the last macro
            // definition.
            material.DisableKeyword("_STOCHASTICMODE_HEXTILE");
            material.EnableKeyword("_STOCHASTICMODE_DELIOT_HEITZ");
            if (family.Name == "_STOCHASTICMODE_HEXTILE")
            {
                material.DisableKeyword("_STOCHASTICMODE_DELIOT_HEITZ");
                material.EnableKeyword("_STOCHASTICMODE_HEXTILE");
            }

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
            harness.Render(material);
            var before = harness.Render(material);
            var movedNames = new List<string>();
            foreach (var name in perturbed)
            {
                if (PerturbOneValue(material, name))
                {
                    movedNames.Add(name);
                }
            }

            if (movedNames.Count == 0)
            {
                Assert.Ignore(
                    "every member of the family '" + familyName +
                    "' stays at an exact value under the fixture, so " +
                    "the liveness cannot be probed. Observed " +
                    "2026-11-24.");
            }

            var after = harness.Render(material);
            Assert.That(
                BytesEqual(before, after),
                Is.False,
                "liveness guard: no member of the family '" +
                familyName + "' moved the render with its switch " +
                "on. The dead case of this family can pass " +
                "trivially. The moved names are: " +
                string.Join(", ", movedNames) + ".");
        }

        // --- the case material ----------------------------------------

        private static string Describe(Color32[] frame)
        {
            var distinct = new HashSet<int>();
            long luma = 0;
            foreach (var pixel in frame)
            {
                distinct.Add(pixel.r << 16 | pixel.g << 8 | pixel.b);
                luma += pixel.r + pixel.g + pixel.b;
            }

            return "distinct=" + distinct.Count + " avgLuma=" +
                   (luma / frame.Length) + " center=" +
                   frame[(32 * 64) + 32];
        }

        // --- the case material ----------------------------------------

        /// <summary>
        /// One material on the row with the pattern texture. The safe
        /// values of the group let every conditional keyword of the
        /// group go on. The family fixture overrides run last, so the
        /// dead and the live case of a family share one fixture.
        /// </summary>
        private Material NewCaseMaterial(
            Shader shader, AtlasDeadValueFamily family)
        {
            var material = Track(new Material(shader));
            material.SetTexture("_MainTex", patternTexture);
            PinSafeGroupValues(material);
            if (FamilyFixtureOverrides.TryGetValue(
                    family.Name, out var overrideAction))
            {
                overrideAction(material);
            }

            return material;
        }

        /// <summary>
        /// The values that the table rules of the record need when the
        /// conditional keywords of the group go on. Each entry mirrors
        /// one rule of record section 5. The safe defaults that need no
        /// entry stay untouched.
        /// </summary>
        private static void PinSafeGroupValues(Material material)
        {
            // The distortion target writes the coordinate slot 7.
            material.SetFloat("_DistortionUvToDistort", 1f);
            // The text overlays read their selected mesh UVs as
            // data.
            material.SetFloat("_TextFPSUV", 1f);
            material.SetFloat("_TextPositionUV", 1f);
            material.SetFloat("_TextTimeUV", 1f);
            material.SetFloat("_TextNumericUV", 1f);
            // The parallax marches its height at another coordinate.
            material.SetFloat("_ParallaxUV", 1f);
            material.SetFloat("_HeightMapUV", 1f);
            material.SetFloat("_HeightmaskUV", 1f);
            // The truchet pattern builds its coordinate from another
            // mesh UV channel.
            material.SetFloat("_TruchetUV", 1f);
            // The audio link decal and the volume color read other
            // mesh UV channels.
            material.SetFloat("_ALDecalUV", 1f);
            material.SetFloat("_ALVolumeColorUV", 1f);
            material.SetFloat("_ALVolumeColorDirection", 1f);
            // The point sampling flag selects the recorded sampler
            // facts of the pathing map.
            material.SetFloat("_PathPointSampling", 1f);
            // The voronoi color and emission mode replaces the base
            // color with the voronoi cell pattern. The mask of the
            // pattern reads one with the mask texture unassigned, so
            // that mode blinds the row control to the main color in
            // every case where the voronoi sits on. The just emission
            // mode keeps the base color intact. Ruling 2 of GREEN
            // round 1.
            material.SetFloat("_VoronoiBlend", 1f);
            // The wind and the spectrum read world and mesh UV data.
            // They stay off, so the vertex reads stay static in
            // time.
            material.SetFloat("_VertexWindEnabled", 0f);
            material.SetFloat("_VertexSpectrumMotion", 0f);
        }

        /// <summary>
        /// The fixture overrides per family. The override runs on the
        /// case material of the dead and the live case alike, so both
        /// compare the same state and a dead case cannot pass on a
        /// blind frame. Each entry pins the values the family needs to
        /// reach the render. Ruling 3 of the fix round.
        /// </summary>
        private static readonly Dictionary<string, Action<Material>>
            FamilyFixtureOverrides =
                new Dictionary<string, Action<Material>>
        {
            ["USER_LUT"] = material =>
            {
                // The distortion writes the coordinate slot 7. The
                // color adjust texture reads that slot here, and the
                // brightness makes its output visible through the
                // hue and gamma pipeline. The member texture of the
                // route keeps its selector at 0.
                material.SetTexture(
                    "_MainColorAdjustTexture", StaticNoiseTexture());
                material.SetFloat("_MainColorAdjustTextureUV", 7f);
                material.SetFloat("_MainBrightness", 1.5f);
            },
            ["POI_MIRROR_TEXTURE"] = material =>
            {
                // The mirror texture block compiles under the
                // mirror options keyword of the always on group.
                material.EnableKeyword("POI_MIRROR");
            },
            ["EFFECT_BUMP"] = material =>
            {
                // The text overlay decodes an MSDF glyph array. The
                // font texture is a neutral of the route, so the
                // fixture pins an MSDF style glyph texture (a white
                // stroke band on a dark field) whose median crosses
                // the decode threshold. The overlay reads mesh UV0
                // on the subject, and the overlay enables stay at
                // their pinned 1 (the perturbation holds exact value
                // members).
                material.SetTexture("_TextGlyphs", StaticGlyphTexture());
                material.SetFloat("_TextFPSUV", 0f);
                material.SetFloat("_TextFPSEnabled", 1f);
                material.SetFloat("_TextPositionEnabled", 1f);
                material.SetFloat("_TextTimeEnabled", 1f);
                material.SetFloat("_TextNumericEnabled", 1f);
            },
            ["PROP_PPMASK"] = material =>
            {
                // The post process mask mixes two colors that sit
                // equal at the neutral post process settings. A
                // brightness value makes them differ, so the mask
                // mix shows.
                material.SetFloat("_PPBrightness", 1.5f);
            },
            ["DISTORT"] = material =>
            {
                // The dissolve shows through its edge emission on
                // an opaque subject. The edge needs a bright edge
                // color and an emission strength beside the
                // perturbed width.
                material.SetColor("_DissolveEdgeColor", Color.white);
                material.SetFloat("_DissolveEdgeEmission", 2f);
                material.SetFloat("_DissolveToEmissionStrength", 2f);
            },
            ["POI_BACKFACE"] = material =>
            {
                // The back face values show on the rasterized back
                // faces. The vendor cull state keeps them off, so
                // the fixture culls the front faces instead.
                material.SetFloat("_Cull", 1f);
            },
            ["POI_UZUMORE"] = material =>
            {
                // The push applies to vertices within the near
                // plane window of the camera. The vendor amount of
                // 0.1 leaves the subject outside that window, so
                // the fixture widens it past the subject distance.
                material.SetFloat("_UzumoreAmount", 3f);
            },
            ["POI_PATHING"] = material =>
            {
                // The path color application multiplies the path
                // alpha with the color alpha and replaces the albedo
                // with the result. The fill path type reads the map
                // directly, and white colors with a low alpha keep
                // the base color dominant while the map driven
                // alpha still shows.
                material.SetColor("_PathColorR", new Color(1f, 1f, 1f, 0.2f));
                material.SetColor("_PathColorG", new Color(1f, 1f, 1f, 0.2f));
                material.SetColor("_PathColorB", new Color(1f, 1f, 1f, 0.2f));
                material.SetColor("_PathColorA", new Color(1f, 1f, 1f, 0.2f));
                material.SetFloat("_PathTypeR", 0f);
                material.SetFloat("_PathTypeG", 0f);
                material.SetFloat("_PathTypeB", 0f);
                material.SetFloat("_PathTypeA", 0f);
            },
            ["POI_AL_DECAL"] = material =>
            {
                PinAudioLinkAvailability(material);
                material.SetColor("_ALVolumeColor", new Color(1f, 1f, 1f, 0.2f));
                // The decal line sits where the volume mask equals
                // the audio value. The pins hold the clip window
                // over the whole band and a line width that the
                // audio value crosses, so the decal emission shows.
                material.SetFloat("_ALDecalVolumeClipMin", 0f);
                material.SetFloat("_ALDecalVolumeClipMax", 1f);
                material.SetFloat("_ALDecalBandClipMin", 0f);
                material.SetFloat("_ALDecalBandClipMax", 1f);
                material.SetFloat("_ALDecalVolumeStep", 0f);
                material.SetFloat("_ALDecalBandStep", 0f);
                material.SetFloat("_ALDecalLineWidth", 0.3f);
            },
            ["POI_AL_VOLUMECOLOR"] = material =>
            {
                PinAudioLinkAvailability(material);
                material.SetColor("_ALVolumeColor", new Color(1f, 1f, 1f, 0.2f));
            },
            ["PROP_RGBMASK"] = PinColorAlphaConsumers,
            ["PROP_RGBAMETALLICMAPS"] = PinPackedMapConsumers,
            ["PROP_RGBASMOOTHNESSMAPS"] = PinPackedMapConsumers,
            ["PROP_REDTEXTURE"] = PinColorAlphaConsumers,
            ["PROP_RGBNORMALR"] = PinRgbNormalConsumers,
            ["PROP_GREENTEXTURE"] = PinColorAlphaConsumers,
            ["PROP_RGBNORMALG"] = PinRgbNormalConsumers,
            ["PROP_BLUETEXTURE"] = PinColorAlphaConsumers,
            ["PROP_RGBNORMALB"] = PinRgbNormalConsumers,
            ["PROP_ALPHATEXTURE"] = PinColorAlphaConsumers,
            ["PROP_RGBNORMALA"] = PinRgbNormalConsumers,
            ["_STOCHASTICMODE_DELIOT_HEITZ"] = PinStochasticReader,
            ["_STOCHASTICMODE_HEXTILE"] = PinStochasticReader,
        };

        /// <summary>
        /// A deterministic noise texture. Created once per session and
        /// reused by every later case.
        /// </summary>
        private static Texture2D StaticNoiseTexture()
        {
            if (staticNoiseTexture != null)
            {
                return staticNoiseTexture;
            }

            var texture = new Texture2D(
                NoiseSize, NoiseSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[NoiseSize * NoiseSize];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % NoiseSize;
                var y = index / NoiseSize;
                pixels[index] = new Color32(
                    (byte)(30 + x * 50),
                    (byte)(200 + y * 10),
                    (byte)(20 + ((x + y) & 3) * 60),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            staticNoiseTexture = texture;
            return texture;
        }

        private static Texture2D staticNoiseTexture;

        private static void PinColorAlphaConsumers(Material material)
        {
            // The color and alpha values only reach the render while
            // their per channel enables sit on. The enables are
            // members of the vignette family, so the perturbation
            // never opens them in these cases. The blend weight
            // reads the RGB mask sample. The fixture pins a dark
            // varying mask and dark channel colors, so the four
            // replace blends keep the base color dominant and the
            // row control survives while the blend stays visible.
            material.SetTexture("_RGBMask", StaticDarkMaskTexture());
            material.SetColor("_RedColor", new Color(0.15f, 0.15f, 0.15f, 1f));
            material.SetColor("_GreenColor", new Color(0.15f, 0.15f, 0.15f, 1f));
            material.SetColor("_BlueColor", new Color(0.15f, 0.15f, 0.15f, 1f));
            material.SetColor("_AlphaColor", new Color(0.15f, 0.15f, 0.15f, 1f));
            material.SetFloat("_RGBARedEnable", 1f);
            material.SetFloat("_RGBAGreenEnable", 1f);
            material.SetFloat("_RGBABlueEnable", 1f);
            material.SetFloat("_RGBAAlphaEnable", 1f);
        }

        /// <summary>
        /// A deterministic near black mask with a varying red
        /// channel. Created once per run.
        /// </summary>
        private static Texture2D StaticDarkMaskTexture()
        {
            if (staticDarkMaskTexture != null)
            {
                return staticDarkMaskTexture;
            }

            const int size = 32;
            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % size;
                var y = index / size;
                pixels[index] = new Color32(
                    (byte)(20 + x * 2), (byte)10, (byte)10, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            staticDarkMaskTexture = texture;
            return texture;
        }

        private static Texture2D staticDarkMaskTexture;

        /// <summary>
        /// An MSDF style glyph texture: a dark field with one white
        /// vertical band, like the letter strokes of the vendor font
        /// array. Created once per session and reused by every later
        /// case.
        /// </summary>
        private static Texture2D StaticGlyphTexture()
        {
            if (staticGlyphTexture != null)
            {
                return staticGlyphTexture;
            }

            const int size = 32;
            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % size;
                var y = index / size;
                var inBand = x >= 6 && x <= 10 && y >= 4 && y <= 28;
                pixels[index] = inBand
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(40, 40, 40, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            staticGlyphTexture = texture;
            return texture;
        }

        private static Texture2D staticGlyphTexture;

        private static void PinRgbNormalConsumers(Material material)
        {
            // The RGB normal maps blend into the tangent space
            // normal, which the lighting reads only outside the flat
            // lighting mode of a fresh material.
            PinColorAlphaConsumers(material);
            material.DisableKeyword("_LIGHTINGMODE_FLAT");
            material.EnableKeyword("_LIGHTINGMODE_SHADEMAP");
        }

        private static void PinPackedMapConsumers(Material material)
        {
            // The packed metallic and smoothness values reach the
            // render under the PBR specular stack, so the fixture
            // holds the Mochie PBR keyword and one PBR enable (the
            // red channel) beside the color and alpha pins.
            PinColorAlphaConsumers(material);
            material.SetFloat("_RGBAPBRRedEnabled", 1f);
            material.EnableKeyword("MOCHIE_PBR");
        }

        private static void PinStochasticReader(Material material)
        {
            // The stochastic mode keywords move the sample positions
            // of a stochastic sampled texture only, so the fixture
            // pins one noisy texture to stochastic sampling. The
            // flat lighting mode of a fresh material ignores normal
            // maps, so the case holds the multilayer mode, which
            // reads the bump. The mode keywords share one enum, and
            // both together leave the hex tile macros as the last
            // macro definition, so the pin keeps the Deliot Heitz
            // mode beside the keyword of the family under test.
            material.SetTexture("_BumpMap", StaticNoiseTexture());
            material.SetFloat("_BumpMapUV", 0f);
            material.SetFloat("_BumpMapStochastic", 1f);
            material.DisableKeyword("_LIGHTINGMODE_FLAT");
            material.EnableKeyword("_LIGHTINGMODE_MULTILAYER_MATH");
        }

        private static void PinAudioLinkAvailability(Material material)
        {
            // The audio link decal and the volume color force their
            // values to zero while no audio link sits available. The
            // availability reads a global toggle and a global audio
            // texture wider than 16 pixels. The fixture pins both
            // globals and the band overrides, and the teardown
            // restores the toggle.
            Shader.SetGlobalFloat("_AudioLinkAnimToggle", 1f);
            Shader.SetGlobalTexture(
                "_AudioTexture", StaticAudioLinkTexture());
            material.SetFloat("_AudioLinkBandOverridesEnabled", 1f);
            material.SetVector(
                "_AudioLinkBandOverrideSliders",
                new Vector4(0.5f, 0.5f, 0.5f, 0.5f));
        }

        /// <summary>
        /// A deterministic audio link texture wide enough for the
        /// availability check. The texture is created once per run.
        /// </summary>
        private static Texture2D StaticAudioLinkTexture()
        {
            if (staticAudioLinkTexture != null)
            {
                return staticAudioLinkTexture;
            }

            const int size = 32;
            var texture = new Texture2D(
                size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % size;
                pixels[index] = new Color32(
                    (byte)(40 + x * 3), (byte)90, (byte)60, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            staticAudioLinkTexture = texture;
            return texture;
        }

        private static Texture2D staticAudioLinkTexture;

        private static void TurnEveryGroupKeywordOn(Material material)
        {
            foreach (var keyword in PoiyomiColorEffectsRoutes
                .AllowedKeywords)
            {
                material.EnableKeyword(keyword);
            }
        }

        /// <summary>
        /// Half A enables the other group keywords at the even list
        /// positions, half B the odd ones. The family switch sits in
        /// neither half, and every other keyword sits on in exactly
        /// one half, so the union of the two halves is the all-on
        /// fixture of the record design with roughly half the feature
        /// code per variant. The two stochastic mode keywords sit at
        /// adjacent positions, so each case holds at most one mode
        /// on and the variant compiles one sampling stack.
        /// </summary>
        private static void TurnHalfOfTheOtherGroupKeywordsOn(
            Material material,
            AtlasDeadValueFamily family,
            string half)
        {
            var index = 0;
            foreach (var keyword in PoiyomiColorEffectsRoutes
                .AllowedKeywords)
            {
                var inFamily = family.Switches.Contains(keyword);
                var thisHalf = (index % 2 == 0) == (half == "A");
                if (!inFamily && thisHalf)
                {
                    material.EnableKeyword(keyword);
                }

                index++;
            }
        }

        private static void TurnTheFamilyOff(
            Material material, AtlasDeadValueFamily family)
        {
            foreach (var name in family.Switches)
            {
                material.DisableKeyword(name);
            }
        }

        private static void TurnTheFamilyOn(
            Material material, AtlasDeadValueFamily family)
        {
            foreach (var name in family.Switches)
            {
                material.EnableKeyword(name);
            }
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
        /// The valid values of the exact value members that the
        /// shader compares with exact equality. A perturbed member
        /// cycles to the next value in its list. A member with a
        /// single value stays.
        /// </summary>
        private static readonly Dictionary<string, float[]>
            ExactValueTable =
                new Dictionary<string, float[]>
        {
            ["_TextFPSEnabled"] = new float[] { 1f },
            ["_TextPositionEnabled"] = new float[] { 1f },
            ["_TextTimeEnabled"] = new float[] { 1f },
            ["_TextNumericEnabled"] = new float[] { 1f },
            ["_UDIMDiscardMode"] = new float[] { 0f, 1f },
            ["_DissolveType"] = new float[] { 1f, 2f, 3f, 4f },
            ["_PathTypeR"] = new float[] { 0f, 1f, 2f, 3f },
            ["_PathTypeG"] = new float[] { 0f, 1f, 2f, 3f },
            ["_PathTypeB"] = new float[] { 0f, 1f, 2f, 3f },
            ["_PathTypeA"] = new float[] { 0f, 1f, 2f, 3f },
            ["_VideoMaskTextureChannel"] = new float[] { 0f },
            ["_VideoType"] = new float[] { 0f, 1f, 2f, 3f, 4f, 5f },
            ["_MainHueShiftSelectOrShift"] = new float[] { 0f, 1f },
            ["_BackFaceHueSelectOrShift"] = new float[] { 0f, 1f },
            ["_DissolveHueSelectOrShift"] = new float[] { 0f, 1f },
            ["_ppHueSelectOrShift"] = new float[] { 0f, 1f },
        };

        /// <summary>
        /// One value moves to another valid value: a color or vector
        /// gains 0.37 on every channel, a texture is replaced by the
        /// 4 by 4 noise texture, a continuous scalar gains 0.37, a
        /// toggle flips between 0 and 1, and an exact value member
        /// cycles through the values of its table. An exact value
        /// member without a second valid value stays, and the
        /// return value reports that. An implicit texture name is
        /// not a material float or vector: the scale and offset of
        /// a texture move through the texture setting methods, and
        /// Unity owns the texel size and HDR names, so those two
        /// stay unperturbed.
        /// </summary>
        private bool PerturbOneValue(Material material, string name)
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
                return true;
            }

            if (name.EndsWith("_TexelSize") || name.EndsWith("_HDR"))
            {
                return false;
            }

            if (name.EndsWith("UV"))
            {
                // The texture selectors of the shader compare exact
                // values and the fixture pins them. The member stays.
                return false;
            }

            if (ExactValueTable.TryGetValue(name, out var values))
            {
                if (values.Length < 2)
                {
                    return false;
                }

                var current = material.GetFloat(name);
                var position = System.Array.IndexOf(values, current);
                var next = values[(position + 1) % values.Length];
                material.SetFloat(name, next);
                return true;
            }

            var propertyIndex = material.shader.FindPropertyIndex(name);
            if (propertyIndex < 0)
            {
                // HasFloat stays silent for a name that the shader
                // does not declare. GetFloat would log an error.
                var saved = material.HasFloat(name)
                    ? material.GetFloat(name)
                    : 0f;
                material.SetFloat(name, saved + PerturbStep);
                return true;
            }

            switch (material.shader.GetPropertyType(propertyIndex))
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
                    return true;
                case ShaderPropertyType.Vector:
                    var vector = material.GetVector(name);
                    material.SetVector(
                        name,
                        new Vector4(
                            vector.x + PerturbStep,
                            vector.y + PerturbStep,
                            vector.z + PerturbStep,
                            vector.w + PerturbStep));
                    return true;
                case ShaderPropertyType.Texture:
                    PerturbTexture(material, name, propertyIndex);
                    return true;
                case ShaderPropertyType.Int:
                    // An integer member is an index or a step count.
                    // The next integer is another valid value.
                    material.SetFloat(
                        name, material.GetFloat(name) + 1f);
                    return true;
                default:
                    material.SetFloat(
                        name, material.GetFloat(name) + PerturbStep);
                    return true;
            }
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

            Assert.Fail(
                "fixture gap: the texture property '" + name +
                "' of the row has no noise texture of its dimension");
        }

        /// <summary>
        /// How many perturbed names the row declares as a property or
        /// the model declares as a uniform. A case with none is
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
            return PoiyomiColorEffectsRoutes.Families()
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
        /// A deterministic 4 by 4 noise pattern. The values sit far
        /// from the pattern texture, so a texture swap moves the
        /// render when the shader reads the texture.
        /// </summary>
        private static Texture2D NewNoiseTexture()
        {
            var texture = new Texture2D(
                NoiseSize, NoiseSize, TextureFormat.RGBA32, false)
            {
                name = "AMUSE poiyomi render oracle noise texture",
            };
            var pixels = new Color32[NoiseSize * NoiseSize];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % NoiseSize;
                var y = index / NoiseSize;
                pixels[index] = new Color32(
                    (byte)(30 + x * 50),
                    (byte)(200 + y * 10),
                    (byte)(20 + ((x + y) & 3) * 60),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
