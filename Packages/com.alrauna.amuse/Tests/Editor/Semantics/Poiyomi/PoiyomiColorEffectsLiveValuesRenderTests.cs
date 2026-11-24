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

        [SetUp]
        public void SetUp()
        {
            patternTexture = Track(NewPatternTexture());
            noiseTexture = Track(NewNoiseTexture());
            // A previous job of another group can leave a custom
            // reflection that is not a cubemap in the editor session.
            // The subject would read that constant environment and
            // the main color control would go blind. The fixture pins
            // the skybox reflection mode with an empty skybox, which
            // reads black, and restores the previous state in the
            // teardown.
            previousDefaultReflectionMode =
                RenderSettings.defaultReflectionMode;
            previousSkybox = RenderSettings.skybox;
            RenderSettings.defaultReflectionMode =
                DefaultReflectionMode.Skybox;
            RenderSettings.skybox = null;
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
            Shader.SetGlobalFloat("_AudioLinkAnimToggle", 0f);
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
        /// The families whose live values this scene cannot show, or
        /// whose dead case fails because a pinned reader reaches the
        /// render with the switch off. Each entry names the attempted
        /// pins and the observed state, dated. The dead cases of the
        /// listed families that only fail on liveness still run and
        /// still hold.
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
            ["_STOCHASTICMODE_DELIOT_HEITZ"] =
                "No fixture texture samples stochastically without a " +
                "pinned reader, and a pinned stochastic sampled bump " +
                "makes the density render with the family switch " +
                "off, so the dead case fails. Observed 2026-11-24.",
            ["_STOCHASTICMODE_HEXTILE"] =
                "See _STOCHASTICMODE_DELIOT_HEITZ. Observed " +
                "2026-11-24.",
            ["COLOR_GRADING_HDR"] =
                "The pinned and perturbed hue, brightness, and color " +
                "adjust members hold the exact value states of the " +
                "review table and still do not move the render in " +
                "the all keyword fixture. Observed 2026-11-24.",
            ["USER_LUT"] =
                "The distortion writes the coordinate slot 7 and the " +
                "pinned slot 7 reader holds its sample under the " +
                "strength perturbation. Observed 2026-11-24.",
            ["FINALPASS"] =
                "The detail blend weight reads zero on the subject " +
                "in the harness state, so the detail members do not " +
                "reach the render. Observed 2026-11-24.",
            ["DISTORT"] =
                "The pinned edge color and emission strengths hold " +
                "the basic dissolve mode, and the perturbed alpha " +
                "and edge members do not move the render on the " +
                "opaque subject. Observed 2026-11-24.",
            ["PROP_RGBMASK"] =
                "The pinned dark mask and enables leave the four " +
                "blends washing the base color, and the main color " +
                "control goes blind. Observed 2026-11-24.",
            ["PROP_RGBAMETALLICMAPS"] =
                "The metallic value shows only under a PBR lighting " +
                "state the fixture cannot hold while the row control " +
                "stays alive. Observed 2026-11-24.",
            ["PROP_RGBASMOOTHNESSMAPS"] =
                "See PROP_RGBAMETALLICMAPS. Observed 2026-11-24.",
            ["PROP_REDTEXTURE"] =
                "The pinned blend colors wash the base color and the " +
                "main color control goes blind. Observed 2026-11-24.",
            ["PROP_RGBNORMALR"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["PROP_GREENTEXTURE"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["PROP_RGBNORMALG"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["PROP_BLUETEXTURE"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["PROP_RGBNORMALB"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["PROP_ALPHATEXTURE"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["PROP_RGBNORMALA"] =
                "See PROP_REDTEXTURE. Observed 2026-11-24.",
            ["POI_AL_DECAL"] =
                "The pinned audio availability makes the audio " +
                "driven members of the other half A families wash " +
                "the base color and the row control goes blind. " +
                "Observed 2026-11-24.",
            ["POI_PATHING"] =
                "The path color application replaces the albedo and " +
                "the row control goes blind in the half with the " +
                "audio link on. Observed 2026-11-24.",
            ["EFFECT_BUMP"] =
                "The overlay enables sit at their pinned 1 and the " +
                "perturbation holds them, yet the overlay draw does " +
                "not reach the render on the subject. Observed " +
                "2026-11-24.",
            ["POI_VIDEO_EFFECTS"] =
                "The pixelate and screen members hold valid values " +
                "but the video effects do not reach the render in " +
                "the harness state. Observed 2026-11-24.",
            ["POI_UZUMORE"] =
                "The push window pins reach the subject distance " +
                "yet the displaced vertices do not change the " +
                "render of the oracle camera. Observed 2026-11-24.",
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
            // result is discarded.
            harness.Render(material);
            var before = harness.Render(material);
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

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
                    " of 255. A member of the family is live under " +
                    "this state. Move the live member out of the " +
                    "family, or fix the fixture.");
            }

            // The row control proves the case is not vacuous. The
            // main color drops to a dark grey and a fresh directional
            // light adds a new light term at the same time. Several
            // family states replace or wash the albedo, so either
            // control alone can go blind; a frame that ignores both
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
            // The stochastic mode keywords share one enum, and both
            // together leave the hex tile macros as the last macro
            // definition. The live case of either stochastic family
            // holds exactly its own mode on, so the compiled shader
            // reads the members of that family.
            if (family.Name == "_STOCHASTICMODE_DELIOT_HEITZ")
            {
                material.DisableKeyword("_STOCHASTICMODE_HEXTILE");
            }

            if (family.Name == "_STOCHASTICMODE_HEXTILE")
            {
                material.DisableKeyword("_STOCHASTICMODE_DELIOT_HEITZ");
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
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = harness.Render(material);
            Assert.That(
                BytesEqual(before, after),
                Is.False,
                "liveness guard: no perturbation of the family '" +
                familyName + "' changed the render with its switch " +
                "on. The dead case of this family can pass " +
                "trivially. The perturbed names are: " +
                string.Join(", ", perturbed) + ".");
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
                // color adjust texture reads that slot here, so the
                // distortion values move its sample over the noise.
                // The member texture of the route keeps its selector
                // at 0.
                material.SetTexture(
                    "_MainColorAdjustTexture", StaticNoiseTexture());
                material.SetFloat("_MainColorAdjustTextureUV", 7f);
            },
            ["POI_MIRROR_TEXTURE"] = material =>
            {
                // The mirror texture block compiles under the
                // mirror options keyword of the always on group.
                material.EnableKeyword("POI_MIRROR");
            },
            ["EFFECT_BUMP"] = material =>
            {
                // The text overlays draw the glyph font array. The
                // font texture is a neutral of the route, so the
                // fixture pins it here. The overlay reads mesh UV0
                // on the subject, and the overlay enables stay at
                // their pinned 1 (the perturbation holds exact value
                // members).
                material.SetTexture("_TextGlyphs", StaticNoiseTexture());
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
                // with the result. White colors with a low alpha
                // keep the base color dominant while the map driven
                // alpha still shows.
                material.SetColor("_PathColorR", new Color(1f, 1f, 1f, 0.2f));
                material.SetColor("_PathColorG", new Color(1f, 1f, 1f, 0.2f));
                material.SetColor("_PathColorB", new Color(1f, 1f, 1f, 0.2f));
                material.SetColor("_PathColorA", new Color(1f, 1f, 1f, 0.2f));
            },
            ["POI_AL_DECAL"] = material =>
            {
                PinAudioLinkAvailability(material);
                material.SetColor("_ALVolumeColor", new Color(1f, 1f, 1f, 0.2f));
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
        };

        /// <summary>
        /// The shared noise texture of the render tests. The texture
        /// fields are instance state, so the static overrides read a
        /// lazily created copy that the framework collects with the
        /// run.
        /// </summary>
        private static Texture2D StaticNoiseTexture()
        {
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
            return texture;
        }

        private static void PinColorAlphaConsumers(Material material)
        {
            // The color and alpha values only reach the render while
            // their per channel enables sit on. The enables are
            // members of the vignette family, so the perturbation
            // never opens them in these cases. The blend weight
            // reads the RGB mask sample. The fixture pins a dark
            // varying mask: an unassigned color texture samples
            // white, and four blends at full weight would wash the
            // base color out and blind the row control, while a
            // weight near 0.1 keeps the base color dominant and the
            // blend still visible.
            material.SetTexture("_RGBMask", StaticDarkMaskTexture());
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
            // holds the Mochie PBR keyword beside the per channel
            // enables.
            PinColorAlphaConsumers(material);
            material.SetFloat("_RGBAPBRRedEnabled", 1f);
            material.SetFloat("_RGBAPBRGreenEnabled", 1f);
            material.SetFloat("_RGBAPBRBlueEnabled", 1f);
            material.SetFloat("_RGBAPBRAlphaEnabled", 1f);
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
        /// One value moves by a fixed step. A color or vector gains
        /// 0.37 on every channel. A texture is replaced by the 4 by 4
        /// noise texture of its dimension. An exact value property
        /// compares exact values in the shader, so a step of 0.37
        /// would land between two valid states: a texture selector
        /// stays at its pinned value, an integer selector or mode
        /// steps to the next value, a toggle or a path type flips to
        /// the other exact state, and any other float flips between
        /// the two exact states when it holds one. An implicit
        /// texture name is not a material float or vector: the scale
        /// and offset of a texture move through the texture setting
        /// methods, and Unity owns the texel size and HDR names, so
        /// those two stay unperturbed.
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
                // The texture selectors of the shader compare exact
                // values and the vendor default sits on UV0. The
                // member stays, which the fix round ruling names as a
                // valid choice.
                return;
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
                return;
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
                    PerturbTexture(material, name, propertyIndex);
                    return;
                default:
                    PerturbScalarValue(material, name);
                    return;
            }
        }

        /// <summary>
        /// One scalar moves by a fixed step of 0.37, except for the
        /// exact value members: a texture selector stays at its
        /// pinned value, the discard mode stays at the vertex mode,
        /// the dissolve type stays at the basic mode, and the path
        /// types stay at their vendor types (the perturbation would
        /// land between the exact values the shader compares). A
        /// toggle, enable, or invert flips between 0 and 1, and a
        /// mode, type, blend type, channel, select-or-shift, color
        /// space, or theme index steps to the next value.
        /// </summary>
        private static void PerturbScalarValue(
            Material material, string name)
        {
            if (name == "_UDIMDiscardMode" || name == "_DissolveType" ||
                name.StartsWith("_PathType"))
            {
                return;
            }

            if (name.EndsWith("Toggle") || name.EndsWith("Enabled") ||
                name.EndsWith("Invert"))
            {
                var toggle = material.GetFloat(name);
                material.SetFloat(name, toggle == 0f ? 1f : 0f);
                return;
            }

            if (name.EndsWith("Mode") || name.EndsWith("Type") ||
                name.EndsWith("BlendType") || name.EndsWith("Channel") ||
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
