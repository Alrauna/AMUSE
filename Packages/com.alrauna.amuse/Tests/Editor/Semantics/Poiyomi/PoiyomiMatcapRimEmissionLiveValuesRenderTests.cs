using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The render oracle for the dead value families of the 'Matcap
    /// rim emission masks' group of the reviewed Poiyomi 9.3 route.
    /// The families come from the record at
    /// docs/superpowers/investigations/2026-11-23-poiyomi-reviewed-route-
    /// keywords.md, section 6. The model drops the values of a dead
    /// family from the material key. A wrong member would merge two
    /// materials that render differently. Each case builds a case
    /// material with every other group keyword on, renders a lit
    /// sphere, perturbs every family member, and renders again. The
    /// two readbacks must match byte for byte. The liveness case
    /// turns the family keyword on and proves that the same
    /// perturbation moves the render. So an equal pair means a dead
    /// family and not a broken fixture.
    /// </summary>
    public sealed class PoiyomiMatcapRimEmissionLiveValuesRenderTests
    {
        private const float PerturbStep = 0.37f;
        private const string GroupName = "Matcap rim emission masks";

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

        // --- the cases ------------------------------------------------

        private static string[] GroupKeywordNames()
        {
            return PoiyomiReviewedInventory.Keywords
                .Where(row => row.Group == GroupName)
                .Select(row => row.Keyword)
                .ToArray();
        }

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var keyword in GroupKeywordNames())
            {
                foreach (var twoPass in new[] { false, true })
                {
                    yield return new TestCaseData(keyword, twoPass)
                        .SetName("Dead family '" + keyword +
                                 "' holds on the " +
                                 (twoPass ? "two pass" : "one pass") +
                                 " row");
                }
            }
        }

        private static IEnumerable<TestCaseData> LiveFamilyCases()
        {
            foreach (var keyword in GroupKeywordNames())
            {
                yield return new TestCaseData(keyword)
                    .SetName("Live family '" + keyword +
                             "' changes the render");
            }
        }

        // --- the tests ------------------------------------------------

        /// <summary>
        /// One dead family on one bundled row. The case material
        /// holds every other group keyword of the record on. The
        /// keyword of the family is off. The family values change by
        /// a fixed step or to a noise texture. The render must stay
        /// byte for byte equal. The no-op guards prove that the dead
        /// set of the model holds every perturbed name, so the case
        /// can never pass by perturbing nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(DeadFamilyCases))]
        public void DeadFamilyValuesDoNotChangeTheRender(
            string familyKeyword, bool twoPass)
        {
            RunDeadCaseAssertions(familyKeyword, twoPass);
        }

        /// <summary>
        /// The second dead case of the slot 0 custom normal family.
        /// The parent slot keyword stays on and the mixed pin keeps
        /// the member color visible. This is the production state
        /// that drops the custom normal members.
        /// </summary>
        [Test]
        public void Matcap0CustomNormalDeadCaseHoldsWithParentOn(
            [Values(false, true)] bool twoPass)
        {
            RunDeadCaseAssertions("POI_MATCAP0_CUSTOM_NORMAL", twoPass);
        }

        private void RunDeadCaseAssertions(
            string familyKeyword, bool twoPass)
        {
            var family = FamilyByName(familyKeyword);
            var shader = LoadRowShader(twoPass);
            var material = NewCaseMaterial(shader, familyKeyword);

            Assert.That(
                family.IsOff(material, shader.name),
                Is.True,
                "no-op guard: the family '" + familyKeyword +
                "' must be off on row '" + shader.name + "'");
            if (familyKeyword == "POI_MATCAP0_CUSTOM_NORMAL")
            {
                Assert.That(
                    material.IsKeywordEnabled("POI_MATCAP0"),
                    Is.True,
                    "no-op guard: the parent slot keyword of the" +
                    " custom normal family must be on");
            }

            var perturbed =
                PerturbedNamesOf(material, family, shader.name);
            Assert.That(
                perturbed, Is.Not.Empty,
                "no-op guard: the dead set of the model must hold at" +
                " least one member of the family '" + familyKeyword +
                "'. The family may cover no name of the row.");
            foreach (var name in perturbed)
            {
                Assert.That(
                    family.Properties, Does.Contain(name),
                    "no-op guard: every perturbed name must be a" +
                    " member of the family under test");
            }

            if (DeclaredPerturbedCount(shader, perturbed) == 0)
            {
                Assert.Ignore(
                    "no perturbed name of the family '" +
                    familyKeyword + "' is a property or a declared" +
                    " uniform of row '" + shader.name +
                    "'. The case would pass vacuously.");
            }

            Assert.That(
                PoiyomiMatcapRimEmissionRoutes.UnheldRequiredValue(
                    material),
                Is.Null,
                "fixture precondition: the case material must hold" +
                " every value that the reviewed route requires");

            using var harness = new PoiyomiRenderHarness();
            // The first render compiles the shader variants of the
            // keyword set. Its result is discarded.
            harness.Render(material);
            var before = harness.Render(material);
            AssertFrameLooksRendered(
                before,
                "row '" + shader.name + "', family '" +
                familyKeyword + "'");

            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = harness.Render(material);
            var differentPixels = CountDifferentPixels(before, after);
            if (differentPixels > 0)
            {
                var liveMembers =
                    LiveMembersOf(shader, familyKeyword, perturbed);
                Assert.Fail(
                    "The values of the dead family '" + familyKeyword +
                    "' changed the render of row '" + shader.name +
                    "'. " + differentPixels + " of " + before.Length +
                    " pixels differ. The largest channel difference" +
                    " is " + LargestChannelDifference(before, after) +
                    " of 255. The members that move the render are: " +
                    string.Join(", ", liveMembers) +
                    ". Move them out of the family in the data.");
            }

            // The row control proves the case is not vacuous. The
            // member color is a live value and must move the render
            // of the same material.
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
                BytesEqual(before, control), Is.False,
                "row control: the member color must change the" +
                " render of row '" + shader.name + "'. The row" +
                " shader may not have compiled or the subject may" +
                " be out of frame.");
        }

        /// <summary>
        /// One liveness case per family. The case material is the
        /// dead case material. The keyword of the family turns on
        /// before the first kept frame. The same perturbation must
        /// change the render. This proves that the compiled shader
        /// reads the family, so the dead case cannot pass trivially.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(LiveFamilyCases))]
        public void LiveFamilyValueChangesTheRender(string familyKeyword)
        {
            var family = FamilyByName(familyKeyword);
            var shader = LoadRowShader(false);
            var material = NewCaseMaterial(shader, familyKeyword);
            var perturbed =
                PerturbedNamesOf(material, family, shader.name);
            Assert.That(
                perturbed, Is.Not.Empty,
                "liveness guard: the dead set of the model holds no" +
                " member of the family '" + familyKeyword +
                "'. The liveness case cannot run.");

            material.EnableKeyword(family.Switches[0]);
            // An exclusive keyword group holds one variant per
            // member. The variant of the family keyword exists only
            // while its siblings stay off. The live case swaps the
            // siblings for the family keyword.
            var group = ExclusiveGroupOf(family.Switches[0]);
            if (group != null)
            {
                foreach (var sibling in group)
                {
                    if (sibling != family.Switches[0])
                    {
                        material.DisableKeyword(sibling);
                    }
                }
            }

            Assert.That(
                PoiyomiMatcapRimEmissionRoutes.UnheldRequiredValue(
                    material),
                Is.Null,
                "fixture precondition: the live material must hold" +
                " every value that the reviewed route requires");

            using var harness = new PoiyomiRenderHarness();
            harness.Render(material);
            var before = harness.Render(material);
            AssertFrameLooksRendered(
                before,
                "row '" + shader.name + "', family '" +
                familyKeyword + "' with its keyword on");

            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = harness.Render(material);
            Assert.That(
                BytesEqual(before, after), Is.False,
                "liveness guard: the family '" + familyKeyword +
                "' with its keyword on must change the render. The" +
                " dead case of this family can pass trivially" +
                " otherwise. The perturbed names are: " +
                string.Join(", ", perturbed) + ".");
        }

        // --- the family lookup ----------------------------------------

        private static AtlasDeadValueFamily FamilyByName(
            string familyKeyword)
        {
            var family = PoiyomiMatcapRimEmissionRoutes.Families()
                .FirstOrDefault(
                    candidate => candidate.Name == familyKeyword);
            Assert.That(
                family, Is.Not.Null,
                "the group must carry the dead value family '" +
                familyKeyword + "' of the record, section 6");
            return family;
        }

        // --- the case material ----------------------------------------

        /// <summary>
        /// The exclusive keyword groups of the row. Each group is one
        /// shader feature line with a leading underscore, so only
        /// one member of the group can hold a variant. The case
        /// material holds exactly one member of each group. The
        /// live case of a family swaps the members, because the
        /// variant of the family keyword exists only alone.
        /// </summary>
        private static readonly string[][] ExclusiveKeywordGroups =
        {
            new[]
            {
                "_RIMSTYLE_POIYOMI",
                "_RIMSTYLE_UTS2",
                "_RIMSTYLE_LILTOON",
            },
            new[]
            {
                "_RIM2STYLE_POIYOMI",
                "_RIM2STYLE_UTS2",
                "_RIM2STYLE_LILTOON",
            },
        };

        /// <summary>
        /// The group of the exclusive keyword, or null.
        /// </summary>
        private static string[] ExclusiveGroupOf(string keyword)
        {
            foreach (var group in ExclusiveKeywordGroups)
            {
                if (group.Contains(keyword))
                {
                    return group;
                }
            }

            return null;
        }

        /// <summary>
        /// One material on one row. Every group keyword of the record
        /// is on, except the keyword of the family under test. The
        /// vendor default of the matcap slot 0 replaces the base
        /// color with the matcap sample. The replaced surface shows
        /// no member color, so the green run of 2026-11-23 proved
        /// the row control blind while the sample replaced. Every
        /// case that holds the slot keyword on therefore pins the
        /// slot sample to the mixed blend, which keeps the base
        /// color in the output. The case of the slot 0 family itself
        /// keeps the vendor default, because the family keyword
        /// holds the slot off there. Each exclusive keyword group
        /// holds exactly one member, because one variant exists per
        /// member. The vendor defaults hold every rule value of the
        /// group, so the case material holds the route values. The
        /// fixture override of the family pins the values that make
        /// the family visible to the camera.
        /// </summary>
        private Material NewCaseMaterial(
            Shader shader, string familyKeyword)
        {
            var material = Track(new Material(shader));
            material.SetTexture("_MainTex", patternTexture);
            foreach (var group in ExclusiveKeywordGroups)
            {
                foreach (var keyword in group)
                {
                    material.DisableKeyword(keyword);
                }
            }

            material.DisableKeyword(familyKeyword);
            foreach (var keyword in GroupKeywordNames())
            {
                if (keyword == familyKeyword ||
                    ExclusiveGroupOf(keyword) != null)
                {
                    continue;
                }

                material.EnableKeyword(keyword);
            }

            foreach (var group in ExclusiveKeywordGroups)
            {
                foreach (var keyword in group)
                {
                    if (keyword == familyKeyword)
                    {
                        continue;
                    }

                    material.EnableKeyword(keyword);
                    break;
                }
            }

            if (familyKeyword != "POI_MATCAP0")
            {
                material.SetTexture("_Matcap", patternTexture);
                material.SetFloat("_MatcapReplace", 0f);
                material.SetFloat("_MatcapMixed", 1f);
            }

            if (FixtureOverrides.TryGetValue(
                    familyKeyword, out var overrideAction))
            {
                overrideAction(material);
            }

            return material;
        }

        /// <summary>
        /// The fixture overrides per family. A family that the
        /// default material hides needs one pin. The matcap of slot
        /// 0 replaces the base color by default. The matcaps of the
        /// slots 1 to 3 carry no blend mode by default, so the mixed
        /// mode pins them visible. The emission strengths start at
        /// zero. The rim of the Poiyomi style is an emission and
        /// starts at zero. The global masks feed the RGB masks of the
        /// color effects group, so the case pins one consumer on.
        /// The override runs after the keyword list, so it can enable
        /// a keyword of another group.
        /// </summary>
        private Dictionary<string, Action<Material>> FixtureOverrides =>
            new Dictionary<string, Action<Material>>
            {
                ["POI_MATCAP0"] = material =>
                {
                    material.SetTexture("_Matcap", patternTexture);
                },
                ["POI_MATCAP0_CUSTOM_NORMAL"] = material =>
                {
                    // The base fixture of every other case assigns
                    // the slot 0 matcap with the mixed pin. The
                    // custom normal map joins that state here.
                    material.SetTexture(
                        "_Matcap0NormalMap", patternTexture);
                },
                ["COLOR_GRADING_HDR_3D"] = material =>
                {
                    material.SetTexture("_Matcap2", patternTexture);
                    material.SetFloat("_Matcap2Mixed", 1f);
                },
                ["POI_MATCAP1_CUSTOM_NORMAL"] = material =>
                {
                    material.SetTexture("_Matcap2", patternTexture);
                    material.SetFloat("_Matcap2Mixed", 1f);
                    material.SetTexture(
                        "_Matcap1NormalMap", patternTexture);
                },
                ["POI_MATCAP2"] = material =>
                {
                    material.SetTexture("_Matcap3", patternTexture);
                    material.SetFloat("_Matcap3Mixed", 1f);
                },
                ["POI_MATCAP2_CUSTOM_NORMAL"] = material =>
                {
                    material.SetTexture("_Matcap3", patternTexture);
                    material.SetFloat("_Matcap3Mixed", 1f);
                    material.SetTexture(
                        "_Matcap2NormalMap", patternTexture);
                },
                ["POI_MATCAP3"] = material =>
                {
                    material.SetTexture("_Matcap4", patternTexture);
                    material.SetFloat("_Matcap4Mixed", 1f);
                },
                ["POI_MATCAP3_CUSTOM_NORMAL"] = material =>
                {
                    material.SetTexture("_Matcap4", patternTexture);
                    material.SetFloat("_Matcap4Mixed", 1f);
                    material.SetTexture(
                        "_Matcap3NormalMap", patternTexture);
                },
                ["_EMISSION"] = material =>
                {
                    material.SetFloat("_EmissionStrength", 1f);
                },
                ["POI_EMISSION_1"] = material =>
                {
                    material.SetFloat("_EmissionStrength1", 1f);
                },
                ["POI_EMISSION_2"] = material =>
                {
                    material.SetFloat("_EmissionStrength2", 1f);
                },
                ["POI_EMISSION_3"] = material =>
                {
                    material.SetFloat("_EmissionStrength3", 1f);
                },
                ["_GLOSSYREFLECTIONS_OFF"] = material =>
                {
                    material.SetFloat("_RimStrength", 1f);
                },
                ["_RIMSTYLE_POIYOMI"] = material =>
                {
                    material.SetFloat("_RimStrength", 1f);
                },
                ["_RIMSTYLE_UTS2"] = material =>
                {
                    // A wide rim power makes the UTS rim band broad,
                    // so the perturbation moves many pixels.
                    material.SetFloat("_RimLight_Power", 0.6f);
                },
                ["_RIMSTYLE_LILTOON"] = material =>
                {
                    // A high border widens the lilToon style rim band
                    // over most of the subject.
                    material.SetFloat("_RimBorder", 0.9f);
                },
                ["POI_RIM2"] = material =>
                {
                    material.SetFloat("_Rim2Strength", 1f);
                },
                ["_RIM2STYLE_POIYOMI"] = material =>
                {
                    material.SetFloat("_Rim2Strength", 1f);
                },
                ["_RIM2STYLE_UTS2"] = material =>
                {
                    material.SetFloat("_Rim2Light_Power", 0.6f);
                },
                ["_RIM2STYLE_LILTOON"] = material =>
                {
                    material.SetFloat("_Rim2Border", 0.9f);
                },
                ["POI_GLOBALMASK_TEXTURES"] = material =>
                {
                    // The RGB mask red layer carries the global mask
                    // change through its emission term. The layer
                    // blend pins to Multiply, so the base color path
                    // of the layer stays multiplicative. The red
                    // color pins dark, so the added emission stays
                    // below the output clamp and the row control
                    // keeps its view of the member color. The green
                    // round 2 run proved that a white red color at
                    // full emission strength clamps the frame white.
                    material.EnableKeyword("VIGNETTE");
                    material.SetTexture(
                        "_GlobalMaskTexture0", patternTexture);
                    material.SetColor("_RedColor", new Color(0.5f, 0.3f, 0.1f, 1f));
                    material.SetFloat("_RGBARedEnable", 1f);
                    material.SetFloat("_RgbRedGlobalMaskChannel", 1f);
                    material.SetFloat("_RGBARedBlendType", 2f);
                    material.SetFloat("_RGBARedEmissionStrength", 1f);
                },
            };

        // --- the perturbation -----------------------------------------

        /// <summary>
        /// The family members that the dead set of the model holds
        /// for the case material. The dead set is the union of every
        /// family that is off. The intersection with the members of
        /// the family under test keeps the perturbation inside that
        /// family, even when another off family shares a member.
        /// </summary>
        private static List<string> PerturbedNamesOf(
            Material material,
            AtlasDeadValueFamily family,
            string rowName)
        {
            var dead = PoiyomiLiveValues.Model.DeadProperties(
                material, rowName, NeverDynamic);
            return family.Properties
                .Where(dead.Contains)
                .ToList();
        }

        private static bool NeverDynamic(string name)
        {
            return false;
        }

        /// <summary>
        /// The members that move the render on their own. The helper
        /// gives each member one fresh case material, so the result
        /// names the defect for the data fix.
        /// </summary>
        private List<string> LiveMembersOf(
            Shader shader,
            string familyKeyword,
            List<string> perturbed)
        {
            var live = new List<string>();
            foreach (var name in perturbed)
            {
                using var harness = new PoiyomiRenderHarness();
                var probe = NewCaseMaterial(shader, familyKeyword);
                harness.Render(probe);
                var baseFrame = harness.Render(probe);
                PerturbOneValue(probe, name);
                if (!BytesEqual(baseFrame, harness.Render(probe)))
                {
                    live.Add(name);
                }
            }

            return live;
        }

        /// <summary>
        /// One value moves by a fixed step. A float gains 0.37. A
        /// color or vector gains 0.37 on every channel. A texture is
        /// replaced by the noise texture of its dimension. An
        /// implicit texture name of the form <c>&lt;T&gt;_ST</c>
        /// moves through <c>SetTextureScale</c> and
        /// <c>SetTextureOffset</c> on <c>&lt;T&gt;</c>, because the
        /// name is no material vector. A
        /// <c>&lt;T&gt;_TexelSize</c> or <c>&lt;T&gt;_HDR</c> name
        /// stays untouched, because Unity sets it with the texture.
        /// A name that the shader does not declare moves as a saved
        /// float, because no pass can read it as anything else.
        /// </summary>
        private void PerturbOneValue(Material material, string name)
        {
            var index = material.shader.FindPropertyIndex(name);
            if (index < 0)
            {
                if (name.EndsWith("_ST") &&
                    DeclaredTextureName(material.shader, name) != null)
                {
                    var texture = DeclaredTextureName(
                        material.shader, name);
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
                    // Unity sets these names with the texture. A
                    // perturbation cannot reach them through a
                    // material value.
                    return;
                }

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
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    var color = material.GetColor(name);
                    material.SetColor(
                        name,
                        new Color(
                            color.r + PerturbStep,
                            color.g + PerturbStep,
                            color.b + PerturbStep,
                            color.a + PerturbStep));
                    return;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    var vector = material.GetVector(name);
                    material.SetVector(
                        name,
                        new Vector4(
                            vector.x + PerturbStep,
                            vector.y + PerturbStep,
                            vector.z + PerturbStep,
                            vector.w + PerturbStep));
                    return;
                case UnityEngine.Rendering.ShaderPropertyType.Texture:
                    PerturbTexture(material, name, index);
                    return;
                default:
                    material.SetFloat(
                        name, material.GetFloat(name) + PerturbStep);
                    return;
            }
        }

        /// <summary>
        /// The texture property name behind an implicit name of the
        /// form <c>&lt;T&gt;_ST</c>, or null when <c>&lt;T&gt;</c>
        /// is no declared texture property of the row.
        /// </summary>
        private static string DeclaredTextureName(
            Shader shader, string implicitName)
        {
            if (!implicitName.EndsWith("_ST"))
            {
                return null;
            }

            var texture = implicitName.Substring(
                0, implicitName.Length - "_ST".Length);
            var index = shader.FindPropertyIndex(texture);
            if (index < 0 || shader.GetPropertyTextureDimension(index)
                    != UnityEngine.Rendering.TextureDimension.Tex2D)
            {
                return null;
            }

            return texture;
        }

        private void PerturbTexture(
            Material material, string name, int index)
        {
            var dimension = material.shader
                .GetPropertyTextureDimension(index);
            if (dimension == UnityEngine.Rendering.TextureDimension.Tex2D)
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

        // --- the render helpers ---------------------------------------

        /// <summary>
        /// Two cheap checks on the frame. The frame must hold more
        /// than one color, and it must hold no pure magenta pixel.
        /// The checks cannot prove that the subject is drawn. The
        /// row control of each dead case carries that proof.
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
                distinct.Count, Is.GreaterThan(1),
                context + ": the frame must not be uniform. The" +
                " subject may be out of frame.");
            Assert.That(
                magenta, Is.EqualTo(0),
                context + ": the frame must not hold the magenta of" +
                " the error shader.");
        }

        private static bool BytesEqual(
            Color32[] first, Color32[] second)
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

        /// <summary>
        /// A pattern texture with varied channels. The variety keeps
        /// the subject away from a uniform frame.
        /// </summary>
        private static Texture2D NewPatternTexture()
        {
            var texture = new Texture2D(
                64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color32[64 * 64];
            for (var index = 0; index < pixels.Length; index++)
            {
                var x = index % 64;
                var y = index / 64;
                pixels[index] = new Color32(
                    (byte)(90 + x * 2),
                    (byte)(110 + y),
                    (byte)(70 + ((x + y) & 15) * 8),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// A four by four noise texture. The perturbation swaps every
        /// texture member of a family to this texture.
        /// </summary>
        private static Texture2D NewNoiseTexture()
        {
            var texture = new Texture2D(
                4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color32[16];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color32(
                    (byte)(30 + index * 13),
                    (byte)(200 - index * 11),
                    (byte)(60 + index * 7),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            tracked.Add(obj);
            return obj;
        }

        private static Shader LoadRowShader(bool twoPass)
        {
            var guid = twoPass
                ? PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid
                : PoiyomiSourceAttestation.CanonicalShaderGuid;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the installed bundled Poiyomi" +
                " shader asset must resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the installed bundled Poiyomi" +
                " shader asset must load");
            return shader;
        }
    }
}
