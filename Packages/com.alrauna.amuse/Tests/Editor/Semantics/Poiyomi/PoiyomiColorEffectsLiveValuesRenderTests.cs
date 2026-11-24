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
            RenderSettings.skybox = previousSkybox;
            RenderSettings.defaultReflectionMode =
                previousDefaultReflectionMode;
        }

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var family in PoiyomiColorEffectsRoutes.Families())
            {
                yield return new TestCaseData(family.Name, false)
                    .SetName("Dead family '" + family.Name +
                             "' holds on the bundled row");
                yield return new TestCaseData(family.Name, true)
                    .SetName("Dead family '" + family.Name +
                             "' holds on the two pass row");
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
        /// The families whose live values this scene cannot show. The
        /// dead cases of these families still run and still hold. The
        /// liveness of each one stays unproven, and the reason sits
        /// here with its date. Observed on 2026-11-23.
        /// </summary>
        private static readonly Dictionary<string, string>
            FamiliesTheOracleCannotShow =
                new Dictionary<string, string>
        {
            ["POI_UDIMDISCARD"] =
                "The perturbation moves the discard mode selector to " +
                "0.37, which is neither exact branch value 0 nor 1, " +
                "so no discard branch can run. Observed 2026-11-23.",
            ["POI_DEPTHBULGE"] =
                "The depth bulge returns before it reads its values " +
                "when the scene holds no camera depth texture. The " +
                "oracle scene renders none. Observed 2026-11-23.",
            ["POI_BACKFACE"] =
                "The subject rasterizes its front faces only, so no " +
                "fragment reaches the not front face branch. " +
                "Observed 2026-11-23.",
            ["POI_AL_DECAL"] =
                "The decal forces its value to zero when no audio " +
                "link is available. The harness scene has none. " +
                "Observed 2026-11-23.",
            ["POI_AL_VOLUMECOLOR"] =
                "The volume color multiplies its blend with the audio " +
                "link availability, which reads zero in the harness " +
                "scene. Observed 2026-11-23.",
            ["POI_UZUMORE"] =
                "The push applies only to vertices near the camera " +
                "near plane. The subject sits far in front of the " +
                "camera. Observed 2026-11-23.",
            ["PROP_RGBMASK"] =
                "The perturbation moves the texture selector to 0.37, " +
                "and the shader reads the mesh UV array at that " +
                "fractional index, which yields no sample, so no " +
                "member reaches the render. Observed 2026-11-23.",
            ["PROP_RGBAMETALLICMAPS"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_RGBASMOOTHNESSMAPS"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_REDTEXTURE"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_RGBNORMALR"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_GREENTEXTURE"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_RGBNORMALG"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_BLUETEXTURE"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_RGBNORMALB"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_ALPHATEXTURE"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["PROP_RGBNORMALA"] =
                "See PROP_RGBMASK. The fractional selector kills the " +
                "sample. Observed 2026-11-23.",
            ["POI_PATHING"] =
                "White path colors, the assigned map, and the tex " +
                "gradient source still leave the per color path alphas " +
                "at zero in the harness state, so no member reaches " +
                "the render. Observed 2026-11-23.",
            ["DISTORT"] =
                "The perturbation moves the dissolve type selector to " +
                "1.37, which matches no dissolve mode branch, so no " +
                "member reaches the render. Observed 2026-11-23.",
            ["EFFECT_BUMP"] =
                "The perturbation moves the overlay enable flags to " +
                "0.37, and the shader compares them with exact " +
                "equality against 1, so no overlay draws. Observed " +
                "2026-11-23.",
        };

        /// <summary>
        /// One dead family of the group on one bundled row. The case
        /// material holds every group keyword of the record on,
        /// except the switch of the family. The family values change.
        /// The render must stay byte for byte equal. The no-op guard
        /// proves that the dead set of the model holds every
        /// perturbed name, so the case can never pass by perturbing
        /// nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(DeadFamilyCases))]
        public void DeadFamilyValuesDoNotChangeTheRender(
            string familyName, bool twoPass)
        {
            var family = FamilyByName(familyName);
            var shader = LoadShader(twoPass);
            var material = NewCaseMaterial(shader, family);
            TurnEveryGroupKeywordOn(material);
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
            // main color is a live value of every admitted row. The
            // control drops the color to a dark grey, so the change
            // survives an overlit frame.
            material.SetColor(
                "_Color",
                new Color(0.3f, 0.3f, 0.3f, material.GetColor("_Color").a));
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
            // computes them with the switch off.
            var perturbed = PerturbedNamesOf(
                material, family, shader.name);
            TurnTheFamilyOn(material, family);
            LetTheShaderReadTheFamily(material, family);

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
        /// One material on the row with the pattern texture. The
        /// safe values of the group let every conditional keyword of
        /// the group go on. The family fixture override runs last.
        /// </summary>
        private Material NewCaseMaterial(
            Shader shader, AtlasDeadValueFamily family)
        {
            var material = Track(new Material(shader));
            material.SetTexture("_MainTex", patternTexture);
            PinSafeGroupValues(material);
            ApplyFamilyFixtureOverride(material, family);
            return material;
        }

        /// <summary>
        /// The values that the table rules of the record need when
        /// the conditional keywords of the group go on. Each entry
        /// mirrors one rule of record section 5. The safe defaults
        /// that need no entry stay untouched.
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
        /// The fixture override of the two stochastic mode families.
        /// The mode keywords move the coordinates of a stochastic
        /// sampled texture only. The fixture pins one texture to
        /// stochastic sampling, so the mode keywords have a reader.
        /// The dead cases of the two families stay dead, because the
        /// mode keywords of the other family stay on there and the
        /// other family members are not perturbed.
        /// </summary>
        private void ApplyFamilyFixtureOverride(
            Material material, AtlasDeadValueFamily family)
        {
            if (family.Name != "_STOCHASTICMODE_DELIOT_HEITZ" &&
                family.Name != "_STOCHASTICMODE_HEXTILE")
            {
                return;
            }

            material.SetTexture("_BumpMap", noiseTexture);
            material.SetFloat("_BumpMapUV", 0f);
            material.SetFloat("_BumpMapStochastic", 1f);
        }

        private static void TurnEveryGroupKeywordOn(Material material)
        {
            foreach (var keyword in PoiyomiColorEffectsRoutes
                .AllowedKeywords)
            {
                // The stochastic mode keywords share one enum. The
                // vendor default carries the Deliot Heitz mode, so the
                // fixture never adds the hex tile mode beside it. The
                // cases of the two stochastic families manage the enum
                // themselves.
                if (keyword == "_STOCHASTICMODE_HEXTILE")
                {
                    continue;
                }

                material.EnableKeyword(keyword);
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

        /// <summary>
        /// The state the compiled shader needs to read a family whose
        /// reader another state shadows. The mirror texture block
        /// compiles under the mirror options keyword of the always on
        /// group, and the distortion writes the coordinate slot 7,
        /// which the main texture reads in this case. Ruling 3 of
        /// GREEN round 1.
        /// </summary>
        private void LetTheShaderReadTheFamily(
            Material material, AtlasDeadValueFamily family)
        {
            switch (family.Name)
            {
                case "USER_LUT":
                    material.SetFloat("_MainTexUV", 7f);
                    return;
                case "POI_MIRROR_TEXTURE":
                    material.EnableKeyword("POI_MIRROR");
                    return;
                case "EFFECT_BUMP":
                    // The text overlays draw the glyph font array. The
                    // font texture is a neutral of the route, so the
                    // fixture pins it here. The overlay reads mesh UV0
                    // on the subject.
                    material.SetTexture("_TextGlyphs", noiseTexture);
                    material.SetFloat("_TextFPSUV", 0f);
                    return;
                case "PROP_PPMASK":
                    // The post process mask mixes two colors that sit
                    // equal at the neutral post process settings. A
                    // brightness value makes them differ, so the mask
                    // mix shows.
                    material.SetFloat("_PPBrightness", 1.5f);
                    return;
                case "DISTORT":
                    // The dissolve shows through its edge emission on
                    // an opaque subject. The edge needs a bright edge
                    // color and an emission strength beside the
                    // perturbed width.
                    material.SetColor("_DissolveEdgeColor", Color.white);
                    material.SetFloat("_DissolveEdgeEmission", 2f);
                    material.SetFloat("_DissolveToEmissionStrength", 2f);
                    return;
                case "_STOCHASTICMODE_DELIOT_HEITZ":
                case "_STOCHASTICMODE_HEXTILE":
                    // The stochastic mode keywords move the sample
                    // positions of a stochastic sampled texture only,
                    // so the fixture pins one noisy texture to
                    // stochastic sampling. The flat lighting mode of a
                    // fresh material ignores normal maps, so the case
                    // holds the multilayer mode, which reads the bump.
                    // The mode keywords share one enum, so the case
                    // holds exactly one of them on.
                    material.SetTexture("_BumpMap", noiseTexture);
                    material.SetFloat("_BumpMapUV", 0f);
                    material.SetFloat("_BumpMapStochastic", 1f);
                    material.DisableKeyword("_LIGHTINGMODE_FLAT");
                    material.EnableKeyword(
                        "_LIGHTINGMODE_MULTILAYER_MATH");
                    material.DisableKeyword("_STOCHASTICMODE_HEXTILE");
                    material.DisableKeyword(
                        "_STOCHASTICMODE_DELIOT_HEITZ");
                    if (family.Name == "_STOCHASTICMODE_HEXTILE")
                    {
                        material.EnableKeyword("_STOCHASTICMODE_HEXTILE");
                    }
                    else
                    {
                        material.EnableKeyword(
                            "_STOCHASTICMODE_DELIOT_HEITZ");
                    }

                    return;
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
        /// One value moves by a fixed step. A float gains 0.37. A
        /// color or vector gains 0.37 on every channel. A texture is
        /// replaced by the 4 by 4 noise texture of its dimension. An
        /// implicit texture name is not a material float or vector:
        /// the scale and offset of a texture move through the texture
        /// setting methods, and Unity owns the texel size and HDR
        /// names, so those two stay unperturbed.
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
                    material.SetFloat(
                        name, material.GetFloat(name) + PerturbStep);
                    return;
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
