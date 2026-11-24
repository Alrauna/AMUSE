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
    /// The render oracle for the dead value families of the
    /// Lighting group of the reviewed Poiyomi 9.3 route. The model
    /// drops the values of a dead family from the material key. A
    /// wrong member would merge two materials that render
    /// differently. Each case builds a case material on the bundled
    /// row, renders a lit sphere, changes only the family values,
    /// and renders again. The two readbacks must match byte for
    /// byte. A liveness case renders the same family with its
    /// switch on and proves that the same perturbation moves the
    /// render there. So an equal pair means a dead family and not a
    /// broken fixture.
    /// </summary>
    public sealed class PoiyomiLightingLiveValuesRenderTests
    {
        private const float PerturbStep = 0.37f;

        /// <summary>
        /// Fixture overrides per family. The override runs after the
        /// keyword state is set, so it can pin a visibility value of
        /// the family. Ruling 3 of 2026-11-23: a pin makes the
        /// family show in this scene. The pins name the observation
        /// that needed them.
        /// <para>
        /// SKIN and SDF: the default of `_LightingIgnoreAmbientColor`
        /// is 1, and then the shadow term of both modes reads
        /// `_LightingShadowColor` (1, 1, 1) and clamps at the direct
        /// color, so no member of the family can move the frame.
        /// Observed on 2026-11-23 in the round 1 run. The pin sets
        /// the value to 0, and the shadow term then follows the
        /// family values.
        /// </para>
        /// <para>
        /// POI_SUBSURFACESCATTERING and
        /// `_STYLIZEDREFLECTIONMODE_LILTOON` carry no entry. Their
        /// liveness is unproven in this scene, and their dated
        /// reasons sit in `CannotShowReasons`.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, Action<Material>>
            FamilyFixtureOverrides =
                new Dictionary<string, Action<Material>>
                {
                    ["_LIGHTINGMODE_SKIN"] = material =>
                        material.SetFloat(
                            "_LightingIgnoreAmbientColor", 0f),
                    ["_LIGHTINGMODE_SDF"] = material =>
                        material.SetFloat(
                            "_LightingIgnoreAmbientColor", 0f),
                };

        /// <summary>
        /// Families whose liveness the oracle cannot show. The
        /// render stays byte for byte equal with the switch on and
        /// every member perturbed, even with the visibility pins of
        /// the override map. A family joins only with the dated
        /// observation and the reason in `CannotShowReasons`. A
        /// fixture that shows the family must remove the entry. The
        /// dead case of the family still runs, and the report says
        /// that the liveness of the family is unproven. Ruling 3 of
        /// 2026-11-23.
        /// </summary>
        private static readonly HashSet<string>
            FamiliesTheOracleCannotShow = new HashSet<string>
            {
                "POI_SUBSURFACESCATTERING",
                "_STYLIZEDREFLECTIONMODE_LILTOON",
            };

        /// <summary>
        /// Why a family of `FamiliesTheOracleCannotShow` cannot
        /// show. The key is the family name.
        /// </summary>
        private static readonly Dictionary<string, string>
            CannotShowReasons = new Dictionary<string, string>
            {
                ["POI_SUBSURFACESCATTERING"] =
                    "The scattering term adds a backlight glow " +
                    "where the view direction points through the " +
                    "object toward the light. The sun of the oracle " +
                    "shines across the view direction, so the glow " +
                    "faces away from the camera on every visible " +
                    "pixel, and no material value can turn the " +
                    "camera. Observed on 2026-11-23: the perturbation " +
                    "of every member left the frame byte for byte " +
                    "equal in the rounds 4 and 6, with the spread at " +
                    "1 and the distortion at 0.",
                ["_STYLIZEDREFLECTIONMODE_LILTOON"] =
                    "The specular and reflection body sits behind " +
                    "the runtime gate `_UseReflection`, and the " +
                    "toon specular edge, the reflectance, and the " +
                    "reflection color were all pinned to visible " +
                    "values, and the probe dump of the failing " +
                    "cases confirmed the pins on the material. The " +
                    "perturbation of every member still left the " +
                    "frame byte for byte equal in the rounds 4, 6, " +
                    "8, and 9 of 2026-11-23. The oracle scene cannot " +
                    "show this family; the cause needs a vendor " +
                    "session of its own.",
            };

        /// <summary>
        /// The lighting mode keywords of the base pass pragma, line
        /// 10074. The six mode keywords of the group sit here, plus
        /// the three KeyOnly modes of the record. The mode blocks
        /// overwrite each other's final lighting in code order, so
        /// more than one mode on can never show one family. The
        /// vendor UI keeps one mode on. Observed on 2026-11-23: a
        /// liveness case with every other mode on showed no member
        /// of the family under test.
        /// </summary>
        private static readonly HashSet<string> ModeKeywords =
            new HashSet<string>
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
        /// The keywords a case leaves out, with the dated reason.
        /// Ruling 2 of 2026-11-23 allows a left out keyword only
        /// when the frame proves it, and the reason must name the
        /// observation. The dead case of the family turns every
        /// other group keyword on except these entries.
        /// </summary>
        private static readonly Dictionary<string,
            (string[] Keywords, string Reason)>
            KeywordsLeftOutPerFamily =
                new Dictionary<string, (string[], string)>
                {
                    ["MOCHIE_PBR"] =
                        (new[]
                        {
                            "GGX_ANISOTROPICS",
                        },
                        "The dead case with every other keyword on " +
                        "rendered 382 magenta pixels on 2026-11-23. " +
                        "The vendor UI pairs the GGX specular helpers " +
                        "with the Mochie block, so GGX_ANISOTROPICS " +
                        "is the first keyword left out."),
                };

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private Texture2D noiseTexture;
        private Cubemap noiseCubemap;

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
            noiseTexture = null;
            noiseCubemap = null;
        }

        // --- the cases ------------------------------------------------

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var family in PoiyomiLightingRoutes.Families())
            {
                yield return new TestCaseData(family.Name)
                    .SetName("Dead family '" + family.Name +
                             "' holds on the Lighting group");
            }
        }

        private static IEnumerable<TestCaseData> LiveFamilyCases()
        {
            foreach (var family in PoiyomiLightingRoutes.Families())
            {
                if (FamiliesTheOracleCannotShow.Contains(family.Name))
                {
                    continue;
                }

                yield return new TestCaseData(family.Name)
                    .SetName("Live family '" + family.Name +
                             "' changes the render");
            }
        }

        // --- the tests ------------------------------------------------

        /// <summary>
        /// One dead family of the group. The case material holds
        /// every other keyword of the group on and the switch of the
        /// family off. The family values change. The render must
        /// stay byte for byte equal. The no-op guard proves that the
        /// dead set of the model holds every perturbed name, so the
        /// case can never pass by perturbing nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(DeadFamilyCases))]
        public void DeadFamilyValuesDoNotChangeTheRender(
            string familyName)
        {
            var family = FamilyByName(familyName);
            using var harness = new PoiyomiRenderHarness();
            PreparePerturbationTextures();
            var material = NewCaseMaterial(family);

            Assert.That(
                family.IsOff(material, material.shader.name),
                Is.True,
                "no-op guard: the family '" + familyName +
                "' must be off on the case material");

            var dead = PoiyomiLiveValues.Model.DeadProperties(
                material, material.shader.name, NeverDynamic);
            var perturbed = PerturbedNamesOf(material, family, dead);
            Assert.That(
                perturbed, Is.Not.Empty,
                "no-op guard: the dead set of the model must hold" +
                " at least one member of the family '" + familyName +
                "'");
            foreach (var name in perturbed)
            {
                Assert.That(
                    family.Properties, Does.Contain(name),
                    "no-op guard: the perturbed name '" + name +
                    "' must be a member of the family '" +
                    familyName + "'");
                Assert.That(
                    dead, Does.Contain(name),
                    "no-op guard: DeadProperties must hold the" +
                    " perturbed name '" + name + "'");
            }

            if (DeclaredPerturbedCount(material, perturbed) == 0)
            {
                Assert.Ignore(
                    "no perturbed name of the family '" + familyName +
                    "' is a property or a declared uniform. The case" +
                    " would pass vacuously.");
            }

            Assert.That(
                AtlasRouteTable.PoiyomiReviewedMainColor
                    .UnheldRequiredValue(material),
                Is.Null,
                "fixture precondition: the case material must hold" +
                " every value that the reviewed route requires");

            // The first render compiles the shader variants. Its
            // result is discarded.
            var context = "family '" + familyName + "', keywords on:" +
                " " + EnabledKeywordList(material);
            harness.Render(material);
            var before = harness.Render(material);
            AssertFrameLooksRendered(before, context);
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
                    "' changed the render. " + differentPixels +
                    " of " + before.Length + " pixels differ. The" +
                    " largest channel difference is " +
                    LargestChannelDifference(before, after) +
                    " of 255. The live members are: " +
                    LiveMembersOf(
                        harness, material, family, dead, before, false) +
                    ".");
            }

            // The row control proves the case is not vacuous. The
            // main color is live in every lighting mode.
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
                "row control: the live value '_Color' must change" +
                " the render of the case material");
        }

        /// <summary>
        /// The liveness guard of the group. The same perturbation of
        /// the dead case must change the render when the switch of
        /// the family is on. This proves that the compiled shader
        /// reads the family, so the dead case cannot pass trivially.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(LiveFamilyCases))]
        public void LiveFamilyValueChangesTheRender(string familyName)
        {
            var family = FamilyByName(familyName);
            using var harness = new PoiyomiRenderHarness();
            PreparePerturbationTextures();

            // The perturbed names come from the dead set, so the
            // family switch sits off at this point. The switch then
            // turns on.
            var material = NewCaseMaterial(family);
            var dead = PoiyomiLiveValues.Model.DeadProperties(
                material, material.shader.name, NeverDynamic);
            var perturbed = PerturbedNamesOf(material, family, dead);
            Assert.That(
                perturbed, Is.Not.Empty,
                "fixture precondition: the dead set of the model" +
                " must hold at least one member of the family '" +
                familyName + "'");
            TurnOnTheFamilySwitch(material, family);
            IsolateTheModeOf(material, family);
            if (FamilyFixtureOverrides.TryGetValue(
                    familyName, out var prepare))
            {
                prepare(material);
            }

            harness.Render(material);
            var before = harness.Render(material);
            AssertFrameLooksRendered(
                before,
                "family '" + familyName + "' with its switch on");
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = harness.Render(material);
            if (BytesEqual(before, after))
            {
                // The diagnostics of ruling 3. The per member run
                // names the members that move the render on their
                // own. The row control proves whether the frame can
                // show a live value at all.
                var liveMembers = LiveMembersOf(
                    harness, material, family, dead, before, true);
                var color = material.GetColor("_Color");
                material.SetColor(
                    "_Color",
                    new Color(
                        color.r + PerturbStep,
                        color.g + PerturbStep,
                        color.b + PerturbStep,
                        color.a));
                var control = harness.Render(material);
                Assert.Fail(
                    "liveness guard: no perturbation of the family '" +
                    familyName + "' changed the render with its" +
                    " switch on. The perturbed members are: " +
                    string.Join(", ", perturbed) + ". The members" +
                    " that move the render on their own are: " +
                    string.Join(", ", liveMembers) + ". The row" +
                    " control changed the frame: " +
                    !BytesEqual(before, control) + ". The probe" +
                    " values are: " + ProbeValues(material) + ".");
            }
        }

        /// <summary>
        /// The saved values of the properties that the pins of the
        /// fixture overrides touch. The dump proves whether a pin
        /// applied when a family still refuses to show.
        /// </summary>
        private static string ProbeValues(Material material)
        {
            var names = new[]
            {
                "_UseReflection",
                "_Reflectance",
                "_ApplySpecular",
                "_Smoothness",
                "_SSSSpread",
                "_SSSDistortion",
                "_LightingIgnoreAmbientColor",
            };
            var parts = new List<string>();
            foreach (var name in names)
            {
                parts.Add(name + "=" + material.GetFloat(name)
                    .ToString("0.##"));
            }

            return string.Join(", ", parts);
        }

        /// <summary>
        /// The local keywords the material enables, in shader order.
        /// The failure messages name the keyword set, so a blind or
        /// magenta frame reports the set. Ruling 4 of 2026-11-23.
        /// </summary>
        private static string EnabledKeywordList(Material material)
        {
            return string.Join(
                ", ", material.shaderKeywords
                    .OrderBy(name => name, StringComparer.Ordinal));
        }

        /// <summary>
        /// The lighting mode keywords overwrite each other's final
        /// lighting in code order, so a mode family shows only when
        /// its mode is the only mode on. The vendor UI keeps one
        /// mode on, so the liveness case of a mode family turns the
        /// other eight modes off. Ruling 2 and 3 of 2026-11-23.
        /// Observed on 2026-11-23: a liveness case with every other
        /// group keyword on showed no member of the mode family.
        /// </summary>
        private static void IsolateTheModeOf(
            Material material,
            AtlasDeadValueFamily family)
        {
            var familyKeyword = family.Switches[0];
            if (!familyKeyword.StartsWith(
                    "_LIGHTINGMODE_", StringComparison.Ordinal))
            {
                return;
            }

            foreach (var keyword in ModeKeywords)
            {
                if (keyword != familyKeyword)
                {
                    material.DisableKeyword(keyword);
                }
            }
        }

        // --- the case material ----------------------------------------

        /// <summary>
        /// One case material on the bundled row. The material starts
        /// from the vendor defaults. Every other keyword of the
        /// group turns on, and the switch of the family turns off.
        /// The override of the family runs last.
        /// </summary>
        private Material NewCaseMaterial(AtlasDeadValueFamily family)
        {
            var material = Track(new Material(LoadCanonicalShader()));
            var switches = family.Switches.ToList();
            var leftOut = KeywordsLeftOutPerFamily.TryGetValue(
                family.Name, out var entry)
                ? entry.Keywords
                : Array.Empty<string>();
            foreach (var keyword in
                     PoiyomiLightingRoutes.AllowedKeywords)
            {
                if (switches.Contains(keyword) ||
                    leftOut.Contains(keyword))
                {
                    continue;
                }

                material.EnableKeyword(keyword);
            }

            foreach (var keyword in switches)
            {
                material.DisableKeyword(keyword);
            }

            if (FamilyFixtureOverrides.TryGetValue(
                    family.Name, out var prepare))
            {
                prepare(material);
            }

            return material;
        }

        private static void TurnOnTheFamilySwitch(
            Material material,
            AtlasDeadValueFamily family)
        {
            foreach (var keyword in family.Switches)
            {
                material.EnableKeyword(keyword);
            }
        }

        private static Shader LoadCanonicalShader()
        {
            var path = AssetDatabase.GUIDToAssetPath(
                PoiyomiSourceAttestation.CanonicalShaderGuid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed bundled" +
                " Poiyomi shader asset must resolve by its pinned" +
                " GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed bundled" +
                " Poiyomi shader asset must load");
            return shader;
        }

        // --- the perturbation -----------------------------------------

        /// <summary>
        /// The family members that the dead set of the model holds
        /// for the case material. The model never returns a VRChat
        /// fallback name, so those members stay unperturbed. Unity
        /// sets the texel size and the HDR decode values of a
        /// texture property, so those implicit names stay
        /// unperturbed too and stay in the family.
        /// </summary>
        private static List<string> PerturbedNamesOf(
            Material material,
            AtlasDeadValueFamily family,
            ISet<string> dead)
        {
            var names = new List<string>();
            foreach (var name in family.Properties)
            {
                if (dead.Contains(name) &&
                    !IsUnityManagedName(material, name))
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
        /// replaced by the noise texture of its dimension. The
        /// tiling and offset of a texture moves through the texture
        /// scale and offset API, because the transform name is an
        /// implicit name of the texture. A name that the shader
        /// does not declare moves as a saved float, because no pass
        /// can read it as anything else.
        /// </summary>
        private void PerturbOneValue(Material material, string name)
        {
            var transformTexture = TextureTransformOf(material, name);
            if (transformTexture != null)
            {
                var scale =
                    material.GetTextureScale(transformTexture);
                material.SetTextureScale(
                    transformTexture,
                    new Vector2(
                        scale.x + PerturbStep,
                        scale.y + PerturbStep));
                var offset =
                    material.GetTextureOffset(transformTexture);
                material.SetTextureOffset(
                    transformTexture,
                    new Vector2(
                        offset.x + PerturbStep,
                        offset.y + PerturbStep));
                return;
            }

            var index = material.shader.FindPropertyIndex(name);
            if (index < 0)
            {
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

        /// <summary>
        /// The texture property of an implicit transform name, or
        /// null. The name <c>&lt;T&gt;_ST</c> is the Unity managed
        /// tiling and offset of the texture property <c>&lt;T&gt;</c>.
        /// It holds no material float or vector, so the test moves
        /// it through the texture API of <c>&lt;T&gt;</c>.
        /// </summary>
        private static string TextureTransformOf(
            Material material,
            string name)
        {
            if (!name.EndsWith("_ST", StringComparison.Ordinal))
            {
                return null;
            }

            var texture = name.Substring(0, name.Length - 3);
            var index = material.shader.FindPropertyIndex(texture);
            if (index < 0 ||
                material.shader.GetPropertyType(index) !=
                ShaderPropertyType.Texture)
            {
                return null;
            }

            return texture;
        }

        /// <summary>
        /// Whether Unity sets the name. The texel size and the HDR
        /// decode values of a texture property are implicit names
        /// that Unity fills. No test may write them, so they stay
        /// unperturbed and stay in the family.
        /// </summary>
        private static bool IsUnityManagedName(
            Material material,
            string name)
        {
            foreach (var suffix in new[] { "_TexelSize", "_HDR" })
            {
                if (!name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    continue;
                }

                var index = material.shader.FindPropertyIndex(
                    name.Substring(0, name.Length - suffix.Length));
                if (index >= 0 &&
                    material.shader.GetPropertyType(index) ==
                    ShaderPropertyType.Texture)
                {
                    return true;
                }
            }

            return false;
        }

        private void PerturbTexture(
            Material material,
            string name,
            int index)
        {
            var dimension =
                material.shader.GetPropertyTextureDimension(index);
            if (dimension == TextureDimension.Tex2D)
            {
                material.SetTexture(name, noiseTexture);
                return;
            }

            if (dimension == TextureDimension.Cube)
            {
                material.SetTexture(name, noiseCubemap);
                return;
            }

            Assert.Fail(
                "fixture gap: the texture property '" + name +
                "' of the row has no noise texture of its dimension");
        }

        /// <summary>
        /// The members that move the render on their own. The helper
        /// renders every member on the harness of the caller, so no
        /// second oracle scene overlaps the first one. The row
        /// material only carries the shader of the row for the name
        /// checks.
        /// </summary>
        private List<string> LiveMembersOf(
            PoiyomiRenderHarness harness,
            Material rowMaterial,
            AtlasDeadValueFamily family,
            ISet<string> dead,
            Color32[] baseRender,
            bool isolateMode)
        {
            var live = new List<string>();
            foreach (var name in PerturbedNamesOf(rowMaterial, family, dead))
            {
                var material = NewCaseMaterial(family);
                Track(material);
                TurnOnTheFamilySwitch(material, family);
                if (isolateMode)
                {
                    IsolateTheModeOf(material, family);
                }

                PerturbOneValue(material, name);
                if (!BytesEqual(baseRender, harness.Render(material)))
                {
                    live.Add(name);
                }
            }

            return live;
        }

        /// <summary>
        /// How many perturbed names the row declares as a property
        /// or the model declares as a uniform. A case with none is
        /// vacuous, because no shader can read the perturbed values.
        /// </summary>
        private static int DeclaredPerturbedCount(
            Material material,
            List<string> perturbed)
        {
            var count = 0;
            foreach (var name in perturbed)
            {
                if (material.shader.FindPropertyIndex(name) >= 0 ||
                    PoiyomiLiveValues.Model.Declares(name))
                {
                    count++;
                }
            }

            return count;
        }

        // --- the comparison -------------------------------------------

        private static void AssertFrameLooksRendered(
            Color32[] render,
            string context)
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
                distinct.Count, Is.GreaterThan(1),
                context + ": the frame must not be uniform. The" +
                " subject may be out of frame.");
            Assert.That(
                magenta, Is.EqualTo(0),
                context + ": the frame must not hold the magenta of" +
                " the error shader. Magenta pixels: " + magenta +
                " of " + render.Length + ". A magenta frame means" +
                " a shader variant failed. Ruling 4 of 2026-11-23.");
        }

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
            Color32[] first,
            Color32[] second)
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
            Color32[] first,
            Color32[] second)
        {
            var largest = 0;
            for (var index = 0; index < first.Length; index++)
            {
                largest = Math.Max(
                    largest, Math.Abs(first[index].r - second[index].r));
                largest = Math.Max(
                    largest, Math.Abs(first[index].g - second[index].g));
                largest = Math.Max(
                    largest, Math.Abs(first[index].b - second[index].b));
                largest = Math.Max(
                    largest, Math.Abs(first[index].a - second[index].a));
            }

            return largest;
        }

        // --- the textures and lookups ---------------------------------

        private void PreparePerturbationTextures()
        {
            noiseTexture = Track(NewNoiseTexture());
            noiseCubemap = Track(NewNoiseCubemap());
        }

        /// <summary>
        /// A 4 by 4 noise texture of the perturbation step. The
        /// values stay deterministic, so a rerun compares the same
        /// frames.
        /// </summary>
        private static Texture2D NewNoiseTexture()
        {
            var texture = new Texture2D(
                4, 4, TextureFormat.RGBA32, false)
            {
                name = "AMUSE poiyomi render oracle noise texture",
            };
            var pixels = new Color32[16];
            var seed = 0x5eed1234;
            for (var index = 0; index < pixels.Length; index++)
            {
                seed = seed * 1664525 + 1013904223;
                pixels[index] = new Color32(
                    (byte)(seed >> 24),
                    (byte)(seed >> 16),
                    (byte)(seed >> 8),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// The cube shaped noise texture. The cube map family of the
        /// group holds cube texture members.
        /// </summary>
        private static Cubemap NewNoiseCubemap()
        {
            var cubemap = new Cubemap(
                4, TextureFormat.RGBA32, false)
            {
                name = "AMUSE poiyomi render oracle noise cubemap",
            };
            var faces = (CubemapFace[])
                Enum.GetValues(typeof(CubemapFace));
            foreach (var face in faces)
            {
                if (face == CubemapFace.Unknown)
                {
                    continue;
                }

                var pixels = new Color[16];
                var seed = 0x77aa55ee + (int)face;
                for (var index = 0; index < pixels.Length; index++)
                {
                    seed = seed * 1664525 + 1013904223;
                    pixels[index] = new Color32(
                        (byte)(seed >> 24),
                        (byte)(seed >> 16),
                        (byte)(seed >> 8),
                        255);
                }

                cubemap.SetPixels(pixels, face);
            }

            cubemap.Apply(false, false);
            return cubemap;
        }

        private static AtlasDeadValueFamily FamilyByName(string name)
        {
            foreach (var family in PoiyomiLightingRoutes.Families())
            {
                if (family.Name == name)
                {
                    return family;
                }
            }

            Assert.Fail(
                "fixture precondition: the group must hold the" +
                " family '" + name + "'");
            return null;
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            tracked.Add(obj);
            return obj;
        }
    }
}
