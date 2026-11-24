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
    /// The render oracle for the dead value families of the 'Always
    /// on' group of the reviewed Poiyomi 9.3 route. The model drops
    /// the values of a dead family from the material key. A wrong
    /// member would merge two materials that render differently.
    /// Each case renders a lit sphere with every group keyword on
    /// except the switch of the family, changes only the family
    /// values, and renders again. The two readbacks must match byte
    /// for byte. The liveness case turns the family switch on and
    /// proves that the same change moves the render there. So an
    /// equal pair means a dead family and not a broken fixture.
    /// </summary>
    public sealed class PoiyomiAlwaysOnLiveValuesRenderTests
    {
        private const float PerturbStep = 0.37f;
        private const int PatternSize = 16;
        private const int NoiseSize = 4;

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private Texture2D patternTexture;
        private Texture2D noiseTexture;

        [SetUp]
        public void SetUp()
        {
            patternTexture = Track(NewPatternTexture());
            noiseTexture = Track(NewNoiseTexture());
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
        }

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var family in PoiyomiAlwaysOnRoutes.Families())
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
            foreach (var family in PoiyomiAlwaysOnRoutes.Families())
            {
                yield return new TestCaseData(family.Name)
                    .SetName("Live family '" + family.Name +
                             "' changes the render");
            }
        }

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
                    shader, family, before, perturbed);
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
            TurnEveryGroupKeywordOn(material);
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
        /// One material on the row with the pattern texture, one
        /// lighting mode, and the family fixture overrides of the
        /// case. The vendor default mode multiplies the light data
        /// factors into the indirect term only, and the harness
        /// ambient sits at black, so the unpinned fixture is blind
        /// to the group families.
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
        /// The mode keywords of the row. They form one enum, and a
        /// fresh material carries the flat mode. A material with two
        /// mode keywords holds no compiled variant, and the variant
        /// fallback then picks an arbitrary mode. The case turns
        /// every mode off before it turns its one mode on.
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
        /// The base pin holds the shade map lighting mode, whose
        /// masks consume the shadow masks, the detail shadows, and
        /// the light map. An override replaces the mode or adds the
        /// values that the family needs to show, in the convention
        /// of the lilToon render oracle.
        /// </summary>
        private static readonly
            Dictionary<string, Action<Material>> FamilyFixtureOverrides =
            new Dictionary<string, Action<Material>>
            {
                // The texture ramp mode is the lighting mode whose
                // final lighting multiplies the occlusion into the
                // shadow term directly.
                ["PROP_LIGHTINGAOMAPS"] = material =>
                    TurnLightingModeOn(
                        material, "_LIGHTINGMODE_TEXTURERAMP"),

                // The light data map feeds the light map only in
                // light map mode 4, because the map sample branch
                // sits behind the mode compare.
                ["PROP_LIGHTDATASDFMAP"] = material =>
                    material.SetFloat("_LightingMapMode", 4f),
            };

        private static void TurnLightingModeOn(
            Material material, string mode)
        {
            foreach (var keyword in LightingModeKeywords)
            {
                material.DisableKeyword(keyword);
            }

            material.EnableKeyword(mode);
        }

        private static void PinTheCaseFixture(
            Material material, AtlasDeadValueFamily family)
        {
            TurnLightingModeOn(material, "_LIGHTINGMODE_SHADEMAP");
            if (FamilyFixtureOverrides.TryGetValue(
                    family.Name, out var overrideAction))
            {
                overrideAction(material);
            }
        }

        private static void TurnEveryGroupKeywordOn(Material material)
        {
            foreach (var keyword in PoiyomiAlwaysOnRoutes
                .AllowedKeywords)
            {
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

        /// <summary>
        /// The members that move the render on their own. The helper
        /// gives each member one fresh case material, so the result
        /// names the defect for the data fix.
        /// </summary>
        private List<string> LiveMembersOf(
            Shader shader,
            AtlasDeadValueFamily family,
            Color32[] baseRender,
            List<string> perturbed)
        {
            using var harness = new PoiyomiRenderHarness();
            PinAmbientLight();
            var live = new List<string>();
            foreach (var name in perturbed)
            {
                var probe = NewCaseMaterial(shader, family);
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
        /// One value moves by a fixed step. A float gains 0.37. A
        /// color or vector gains 0.37 on every channel. A texture is
        /// replaced by the 4 by 4 noise texture of its dimension.
        /// </summary>
        private void PerturbOneValue(Material material, string name)
        {
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
            return PoiyomiAlwaysOnRoutes.Families()
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
