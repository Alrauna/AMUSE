using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The reproduction map moves the aligned facts and the live values of
    /// the locked material into the values of the unlocked clone (A5). The
    /// consistency checks keep every removed block removed on the clone
    /// values (A6). Every fixture holds synthetic names and no vendor
    /// text.
    /// </summary>
    public sealed class LockedSourceReproductionTests
    {
        // One fixed detail sentence per rule (A5.9, A6.2).
        private const string LiteralShapeDetail =
            "A baked literal shape does not fit the property type.";

        private const string RoundTripDetail =
            "A baked literal does not print back to its own text.";

        private const string TexelSizeDetail =
            "A baked texel size does not match the texture. A generated shader shared between materials can cause this.";

        private const string BracketBakedDetail =
            "A baked property is also read by a live bracket with another value.";

        private const string EnumBakedDetail =
            "A render state enum text differs from a baked literal of the same property.";

        private const string EnumBracketDetail =
            "A render state enum text differs from a live bracket read of the same property.";

        private const string PropDefineDetail =
            "The texture defines of a program block do not match the textures of the material.";

        private const string KeywordPartDetail =
            "Program blocks hold different keyword defines.";

        private const string IfdefRemovedDetail =
            "A removed keyword block needs its keyword absent on the material.";

        private const string IfndefRemovedDetail =
            "A removed inverse keyword block needs its keyword present on the material.";

        private const string ConditionDetail =
            "A removed condition block does not hold on the material.";

        private const string ConditionUnsupportedDetail =
            "A removed condition block uses a condition outside the accepted forms.";

        /// <summary>
        /// Plain material values that the tests control. The applied view
        /// mirrors the clone write of the build (A8.1). No Unity object
        /// takes part.
        /// </summary>
        private sealed class FakeValues : ILockedMaterialValues
        {
            private readonly Dictionary<string, float> floats =
                new Dictionary<string, float>();

            private readonly Dictionary<string, Vector4> vectors =
                new Dictionary<string, Vector4>();

            private readonly Dictionary<string, Color> colors =
                new Dictionary<string, Color>();

            private readonly Dictionary<string, LockedTextureValue> textures =
                new Dictionary<string, LockedTextureValue>();

            internal FakeValues WithFloat(string name, float value)
            {
                floats[name] = value;
                return this;
            }

            internal FakeValues WithVector(string name, Vector4 value)
            {
                vectors[name] = value;
                return this;
            }

            internal FakeValues WithColor(string name, Color value)
            {
                colors[name] = value;
                return this;
            }

            internal FakeValues WithTexture(string name, LockedTextureValue value)
            {
                textures[name] = value;
                return this;
            }

            internal FakeValues WithQueue(int value)
            {
                RenderQueue = value;
                return this;
            }

            internal FakeValues WithRenderType(string value)
            {
                RenderTypeTag = value;
                return this;
            }

            internal FakeValues WithVrcFallback(string value)
            {
                VrcFallbackTag = value;
                return this;
            }

            internal FakeValues WithOriginalKeywords(params string[] names)
            {
                OriginalKeywordsTag = names;
                return this;
            }

            internal FakeValues WithKeywords(params string[] names)
            {
                Keywords = names;
                return this;
            }

            internal FakeValues WithProjectLinear(bool value)
            {
                ProjectIsLinear = value;
                return this;
            }

            public int RenderQueue { get; private set; } = 2000;

            public string RenderTypeTag { get; private set; } = "";

            public string VrcFallbackTag { get; private set; } = "";

            public IReadOnlyList<string> OriginalKeywordsTag { get; private set; } =
                Array.Empty<string>();

            public IReadOnlyList<string> Keywords { get; private set; } =
                Array.Empty<string>();

            public bool ProjectIsLinear { get; private set; } = true;

            public bool TryGetFloat(string propertyName, out float value)
            {
                return floats.TryGetValue(propertyName, out value);
            }

            public bool TryGetVector(string propertyName, out Vector4 value)
            {
                return vectors.TryGetValue(propertyName, out value);
            }

            public bool TryGetColor(string propertyName, out Color value)
            {
                return colors.TryGetValue(propertyName, out value);
            }

            public bool TryGetTexture(string propertyName, out LockedTextureValue value)
            {
                return textures.TryGetValue(propertyName, out value);
            }

            internal FakeValues WithoutTexture(string name)
            {
                textures.Remove(name);
                return this;
            }

            /// <summary>
            /// Applies the plan like the build applies it (A8.1). The
            /// vanished names are the saved names that the generated
            /// shader declares and the original shader does not declare.
            /// They are no slots after the swap.
            /// </summary>
            internal FakeValues Apply(
                LockedReproductionPlan plan,
                params string[] vanishedNames)
            {
                var view = new FakeValues();
                var gone = new HashSet<string>(vanishedNames);
                foreach (var entry in floats)
                {
                    if (!gone.Contains(entry.Key))
                    {
                        view.floats[entry.Key] = entry.Value;
                    }
                }

                foreach (var entry in vectors)
                {
                    if (!gone.Contains(entry.Key))
                    {
                        view.vectors[entry.Key] = entry.Value;
                    }
                }

                foreach (var entry in colors)
                {
                    if (!gone.Contains(entry.Key))
                    {
                        view.colors[entry.Key] = entry.Value;
                    }
                }

                foreach (var entry in textures)
                {
                    if (!gone.Contains(entry.Key))
                    {
                        view.textures[entry.Key] = entry.Value;
                    }
                }

                foreach (var entry in plan.FloatWrites)
                {
                    view.floats[entry.Key] = entry.Value;
                }

                foreach (var entry in plan.VectorWrites)
                {
                    view.vectors[entry.Key] = entry.Value;
                }

                foreach (var entry in plan.ColorWrites)
                {
                    view.colors[entry.Key] = entry.Value;
                }

                foreach (var entry in plan.TextureScaleOffsetWrites)
                {
                    view.vectors[entry.Key + "_ST"] = entry.Value;
                }

                foreach (var entry in plan.TextureWrites)
                {
                    view.textures[entry.Key] = entry.Value;
                }

                view.RenderQueue = plan.RenderQueue;
                view.RenderTypeTag = plan.RenderTypeTag;
                view.VrcFallbackTag = plan.VrcFallbackTag;
                view.Keywords = plan.Keywords;
                view.OriginalKeywordsTag = OriginalKeywordsTag;
                view.ProjectIsLinear = ProjectIsLinear;
                return view;
            }
        }

        private static LockedSourceProperty Prop(
            string name,
            ShaderPropertyType type,
            float defaultFloat = 0f,
            bool hdr = false,
            bool gamma = false)
        {
            return new LockedSourceProperty(name, type, hdr, gamma, defaultFloat);
        }

        private static IReadOnlyDictionary<string, LockedPropertyState> States(
            params (string Name, LockedPropertyState State)[] entries)
        {
            var result = new Dictionary<string, LockedPropertyState>();
            foreach (var entry in entries)
            {
                result.Add(entry.Name, entry.State);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string> Names(
            params (string From, string To)[] entries)
        {
            var result = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                result.Add(entry.From, entry.To);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, int> Enums(
            params (string Name, int Value)[] entries)
        {
            var result = new Dictionary<string, int>();
            foreach (var entry in entries)
            {
                result.Add(entry.Name, entry.Value);
            }

            return result;
        }

        private static IReadOnlyList<IReadOnlyList<string>> Runs(params string[][] runs)
        {
            return runs;
        }

        private static IReadOnlyList<LockedBlockRecord> Blocks(params LockedBlockRecord[] records)
        {
            return records;
        }

        private static LockedBlockRecord Block(
            LockedBlockKind kind,
            LockedBlockOutcome outcome,
            string conditionOrKeyword,
            int line)
        {
            return new LockedBlockRecord(kind, outcome, conditionOrKeyword, line);
        }

        /// <summary>
        /// Builds one aligned record by hand (A4). Defaults stand for the
        /// parts a fixture does not use.
        /// </summary>
        private static AlignedLockedSource Source(
            IReadOnlyDictionary<string, LockedPropertyState> states,
            IReadOnlyDictionary<string, string> renamed = null,
            IReadOnlyDictionary<string, string> baked = null,
            IReadOnlyDictionary<string, int> enumValues = null,
            IReadOnlyDictionary<string, string> brackets = null,
            IReadOnlyList<IReadOnlyList<string>> runs = null,
            IReadOnlyList<LockedBlockRecord> blocks = null,
            string suffix = null,
            string surviving = null)
        {
            return new AlignedLockedSource(
                PoiyomiLockEra.Bundled9364,
                suffix,
                states,
                renamed ?? Names(),
                baked ?? Names(),
                enumValues ?? Enums(),
                brackets ?? Names(),
                runs ?? Runs(new[] { "OPTIMIZER_ENABLED" }),
                blocks ?? Array.Empty<LockedBlockRecord>(),
                surviving ?? "");
        }

        /// <summary>Runs the build and fails on a refusal.</summary>
        private static LockedReproductionPlan BuiltPlan(
            AlignedLockedSource source,
            LockedSourceProperty[] properties,
            FakeValues locked)
        {
            var result = LockedSourceReproduction.Build(source, properties, locked);
            Assert.That(
                result.Succeeded,
                Is.True,
                result.RefusalDetail ?? "The build refused without a detail.");
            return result.Plan;
        }

        private static void AssertRefusal(LockedReproductionResult result, string detail)
        {
            Assert.That(result.Succeeded, Is.False, "The build must refuse.");
            Assert.That(result.RefusalDetail, Is.EqualTo(detail));
        }

        /// <summary>Checks the applied plan view for consistency (A6).</summary>
        private static void AssertConsistent(
            AlignedLockedSource source,
            LockedSourceProperty[] properties,
            FakeValues locked,
            LockedReproductionPlan plan,
            params string[] vanishedNames)
        {
            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                locked.Apply(plan, vanishedNames));
            Assert.That(
                detail,
                Is.Null,
                detail ?? "The applied plan must stay consistent.");
        }

        /// <summary>
        /// A renamed property reads the renamed name on the locked
        /// material and writes the plain name on the clone (A5.3).
        /// </summary>
        [Test]
        public void RenamedPropertyTakesTheSuffixedValue()
        {
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var source = Source(
                States(("_P", LockedPropertyState.Renamed)),
                renamed: Names(("_P", "S_P")));
            var locked = new FakeValues().WithFloat("S_P", 0.75f);

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.FloatWrites["_P"], Is.EqualTo(0.75f),
                "The renamed value moves to the plain name.");
            AssertConsistent(source, properties, locked, plan, "S_P");
        }

        /// <summary>
        /// An unsaved renamed name reads the generated shader default,
        /// which is the float default of the property table (A5.3).
        /// </summary>
        [Test]
        public void UnsavedRenamedNameReadsTheGeneratedDefault()
        {
            var properties = new[] { Prop("_P", ShaderPropertyType.Float, 0.5f) };
            var source = Source(
                States(("_P", LockedPropertyState.Renamed)),
                renamed: Names(("_P", "S_P")));
            var locked = new FakeValues();

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.FloatWrites["_P"], Is.EqualTo(0.5f),
                "The plain name takes the generated default.");
        }

        /// <summary>
        /// A renamed texture takes the texture and its scale and offset
        /// from the renamed names (A5.3).
        /// </summary>
        [Test]
        public void RenamedTextureTakesTextureScaleAndOffset()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(
                    ("_Tex", LockedPropertyState.Renamed),
                    ("_Tex_ST", LockedPropertyState.Renamed)),
                renamed: Names(("_Tex", "S_Tex"), ("_Tex_ST", "S_Tex_S_ST")));
            var locked = new FakeValues().WithTexture(
                "S_Tex",
                new LockedTextureValue("token-a", 4, 8, new Vector2(2f, 3f), new Vector2(0.25f, 0.5f)));

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.TextureWrites["_Tex"].IdentityToken, Is.EqualTo("token-a"),
                "The renamed texture moves to the plain name.");
            Assert.That(plan.TextureScaleOffsetWrites["_Tex"],
                Is.EqualTo(new Vector4(2f, 3f, 0.25f, 0.5f)),
                "The renamed scale and offset move to the plain name.");
            AssertConsistent(source, properties, locked, plan, "S_Tex", "S_Tex_S_ST");
        }

        // --- Falsifier 8: baked value edited after lock ---
        // An implementation that copies the saved value instead of the
        // parsed literal writes one and fails the plan value assertion.
        // The guard runs the same fixture with the saved value zero. Both
        // runs must write zero through the literal path. A shortcut that
        // skips the write when both values agree fails the guard.
        [Test]
        public void BakedFloatOverridesAnEditedSavedValue()
        {
            var properties = new[] { Prop("_P", ShaderPropertyType.Float, 0.25f) };
            var source = Source(
                States(("_P", LockedPropertyState.Baked)),
                baked: Names(("_P", "0.0")));

            var edited = new FakeValues().WithFloat("_P", 1f);
            var plan = BuiltPlan(source, properties, edited);
            Assert.That(plan.FloatWrites["_P"], Is.EqualTo(0f),
                "The baked literal wins over the edited saved value.");

            var agreeing = new FakeValues().WithFloat("_P", 0f);
            var guardedPlan = BuiltPlan(source, properties, agreeing);
            Assert.That(guardedPlan.FloatWrites.ContainsKey("_P"), Is.True,
                "The literal path performs the write.");
            Assert.That(guardedPlan.FloatWrites["_P"], Is.EqualTo(0f),
                "The literal path writes the parsed literal.");
        }

        /// <summary>
        /// A float literal that does not print back to its own text
        /// refuses (A5.4).
        /// </summary>
        [Test]
        public void FloatLiteralThatDoesNotRoundTripRefuses()
        {
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var source = Source(
                States(("_P", LockedPropertyState.Baked)),
                baked: Names(("_P", "0.10000000000000000001")));
            var locked = new FakeValues();

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, RoundTripDetail);
        }

        /// <summary>
        /// A baked vector parses its four components. Each component must
        /// print back to its own text (A5.4).
        /// </summary>
        [Test]
        public void BakedVectorRoundTripsAtMapLevel()
        {
            var properties = new[] { Prop("_V", ShaderPropertyType.Vector) };
            var source = Source(
                States(("_V", LockedPropertyState.Baked)),
                baked: Names(("_V", "float4(0.5,0.25,0.125,0)")));
            var locked = new FakeValues();

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.VectorWrites["_V"],
                Is.EqualTo(new Vector4(0.5f, 0.25f, 0.125f, 0f)),
                "The four parsed components reach the plan.");
            AssertConsistent(source, properties, locked, plan);
        }

        /// <summary>
        /// A baked ST sets the texture scale and offset (A5.4). The
        /// matching texel size of the same texture passes, so the texel
        /// refusal test pins both directions of that rule.
        /// </summary>
        [Test]
        public void BakedScaleOffsetSetsTextureScaleAndOffset()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(
                    ("_Tex", LockedPropertyState.Unused),
                    ("_Tex_ST", LockedPropertyState.Baked),
                    ("_Tex_TexelSize", LockedPropertyState.Baked)),
                baked: Names(
                    ("_Tex_ST", "float4(2,3,0.5,0.25)"),
                    ("_Tex_TexelSize", "float4(0.001953125,0.001953125,512,512)")));
            var locked = new FakeValues().WithTexture(
                "_Tex",
                new LockedTextureValue("token-b", 512, 512, Vector2.one, Vector2.zero));

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.TextureScaleOffsetWrites["_Tex"],
                Is.EqualTo(new Vector4(2f, 3f, 0.5f, 0.25f)),
                "The baked scale and offset reach the plan.");
            Assert.That(plan.TextureWrites.ContainsKey("_Tex"), Is.False,
                "An unused texture writes no texture.");
            AssertConsistent(source, properties, locked, plan);
        }

        /// <summary>
        /// A scalar literal of integer shape on a vector property does not
        /// fit the property type and refuses (A5.4).
        /// </summary>
        [Test]
        public void IntLiteralShapeOnAVectorPropertyRefuses()
        {
            var properties = new[] { Prop("_V", ShaderPropertyType.Vector) };
            var source = Source(
                States(("_V", LockedPropertyState.Baked)),
                baked: Names(("_V", "2")));
            var locked = new FakeValues();

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, LiteralShapeDetail);
        }

        /// <summary>
        /// A baked color takes the parsed alpha exactly. Its red green
        /// blue takes the inverse of the fact 6 conversion. Matching flags
        /// in a linear project convert. One flag set or a gamma project
        /// keeps the raw components (A5.4, fact 6).
        /// </summary>
        [Test]
        public void ColorTakesTheExactAlphaAndConvertedRgb()
        {
            var convertedProperties = new[] { Prop("_Color", ShaderPropertyType.Color) };
            var convertedSource = Source(
                States(("_Color", LockedPropertyState.Baked)),
                baked: Names(("_Color", "float4(0.5,0.25,0.375,0.75)")));
            var convertedLocked = new FakeValues().WithProjectLinear(true);
            var convertedPlan = BuiltPlan(convertedSource, convertedProperties, convertedLocked);

            var converted = new Color(0.5f, 0.25f, 0.375f).gamma;
            converted.a = 0.75f;
            Assert.That(convertedPlan.ColorWrites["_Color"], Is.EqualTo(converted),
                "The linear bake converts back to the stored color.");

            var rawProperties = new[] { Prop("_Color", ShaderPropertyType.Color, hdr: true) };
            var rawSource = Source(
                States(("_Color", LockedPropertyState.Baked)),
                baked: Names(("_Color", "float4(0.5,0.25,0.125,0.75)")));
            var rawLocked = new FakeValues().WithProjectLinear(true);
            var rawPlan = BuiltPlan(rawSource, rawProperties, rawLocked);
            Assert.That(rawPlan.ColorWrites["_Color"],
                Is.EqualTo(new Color(0.5f, 0.25f, 0.125f, 0.75f)),
                "An HDR only bake keeps the raw components.");

            var gammaProjectProperties = new[] { Prop("_Color", ShaderPropertyType.Color) };
            var gammaProjectSource = Source(
                States(("_Color", LockedPropertyState.Baked)),
                baked: Names(("_Color", "float4(0.5,0.25,0.125,0.75)")));
            var gammaProjectLocked = new FakeValues().WithProjectLinear(false);
            var gammaProjectPlan = BuiltPlan(
                gammaProjectSource,
                gammaProjectProperties,
                gammaProjectLocked);
            Assert.That(gammaProjectPlan.ColorWrites["_Color"],
                Is.EqualTo(new Color(0.5f, 0.25f, 0.125f, 0.75f)),
                "A gamma project keeps the raw components.");
        }

        /// <summary>
        /// A baked texel size must equal the fact 5 print of the texture
        /// facts on the clone. A mismatch refuses and names a shared
        /// generated shader as a possible cause (A5.4, fact 14).
        /// </summary>
        [Test]
        public void BakedTexelSizeThatDiffersFromTheTextureRefusesAndNamesSharing()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(
                    ("_Tex", LockedPropertyState.Unused),
                    ("_Tex_TexelSize", LockedPropertyState.Baked)),
                baked: Names(("_Tex_TexelSize", "float4(0.00390625,0.00390625,256,512)")));
            var locked = new FakeValues().WithTexture(
                "_Tex",
                new LockedTextureValue("token-c", 512, 512, Vector2.one, Vector2.zero));

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, TexelSizeDetail);
        }

        /// <summary>
        /// A baked property with a surviving bracket read refuses when the
        /// live value differs from the literal (A5.5, Review Focus 3).
        /// The guard proves the fixture reaches the check with the equal
        /// value and succeeds there.
        /// </summary>
        [Test]
        public void BakedPropertyWithADifferentLiveBracketValueRefuses()
        {
            var properties = new[] { Prop("_Cull", ShaderPropertyType.Float) };
            var source = Source(
                States(("_Cull", LockedPropertyState.Baked)),
                baked: Names(("_Cull", "1")),
                brackets: Names(("_Cull", "_Cull")));

            var differing = new FakeValues().WithFloat("_Cull", 2f);
            var result = LockedSourceReproduction.Build(source, properties, differing);
            AssertRefusal(result, BracketBakedDetail);

            var agreeing = new FakeValues().WithFloat("_Cull", 1f);
            var plan = BuiltPlan(source, properties, agreeing);
            Assert.That(plan.FloatWrites["_Cull"], Is.EqualTo(1f),
                "The equal live value passes and the literal wins.");
        }

        // --- Falsifier 9: enum-only property ---
        // An implementation that writes no value for an unused property
        // misses the enum text and fails the plan value assertion. The
        // no-op guards prove the fixture reaches the path. The state is
        // Unused and the enum value exists.
        [Test]
        public void EnumOnlyPropertyTakesTheEnumValue()
        {
            var properties = new[] { Prop("_Mask", ShaderPropertyType.Float) };
            var source = Source(
                States(("_Mask", LockedPropertyState.Unused)),
                enumValues: Enums(("_Mask", 2)));

            Assert.That(source.States["_Mask"], Is.EqualTo(LockedPropertyState.Unused),
                "The fixture state is Unused.");
            Assert.That(source.EnumValues.ContainsKey("_Mask"), Is.True,
                "The fixture holds the enum value.");

            var locked = new FakeValues();
            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.FloatWrites["_Mask"], Is.EqualTo(2f),
                "The enum text value reaches the plan.");
            AssertConsistent(source, properties, locked, plan);
        }

        /// <summary>
        /// Enum text that differs from a baked literal of the same
        /// property refuses (A5.6).
        /// </summary>
        [Test]
        public void EnumTextThatDiffersFromABakedLiteralRefuses()
        {
            var properties = new[] { Prop("_Mask", ShaderPropertyType.Float) };
            var source = Source(
                States(("_Mask", LockedPropertyState.Baked)),
                baked: Names(("_Mask", "1")),
                enumValues: Enums(("_Mask", 2)));

            var result = LockedSourceReproduction.Build(
                source,
                properties,
                new FakeValues());

            AssertRefusal(result, EnumBakedDetail);
        }

        /// <summary>
        /// Enum text that differs from the live value of a surviving
        /// bracket read refuses (A5.6).
        /// </summary>
        [Test]
        public void EnumTextThatDiffersFromALiveBracketRefuses()
        {
            var properties = new[] { Prop("_Mask", ShaderPropertyType.Float) };
            var source = Source(
                States(("_Mask", LockedPropertyState.Unused)),
                enumValues: Enums(("_Mask", 2)),
                brackets: Names(("_Mask", "_Mask")));
            var locked = new FakeValues().WithFloat("_Mask", 1f);

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, EnumBracketDetail);
        }

        /// <summary>
        /// An unused property with a surviving bracket read and no enum
        /// text keeps the locked value (A5.7).
        /// </summary>
        [Test]
        public void UnusedPropertyWithALiveBracketKeepsTheLockedValue()
        {
            var properties = new[] { Prop("_Mask", ShaderPropertyType.Float) };
            var source = Source(
                States(("_Mask", LockedPropertyState.Unused)),
                brackets: Names(("_Mask", "_Mask")));
            var locked = new FakeValues().WithFloat("_Mask", 0.25f);

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.FloatWrites["_Mask"], Is.EqualTo(0.25f),
                "The bracket read keeps the locked value.");
            AssertConsistent(source, properties, locked, plan);
        }

        /// <summary>
        /// An assigned texture expects its PROP define in the run when the
        /// define occurs in the surviving text. A missing define refuses
        /// (A5.9, finding 6).
        /// </summary>
        [Test]
        public void PropDefineMissingForAnAssignedTextureRefuses()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(("_Tex", LockedPropertyState.Unused)),
                surviving: "float4 PROP_TEX;");
            var locked = new FakeValues().WithTexture(
                "_Tex",
                new LockedTextureValue("token-d", 4, 4, Vector2.one, Vector2.zero));

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, PropDefineDetail);
        }

        /// <summary>
        /// A renamed texture write that the applier omits hides the PROP
        /// define of the run. The check reads the reproduced view by
        /// plain names only, so the renamed slot is no read, and the
        /// keyword part of the run must still be carried by the keywords
        /// of the view (A5.9).
        /// </summary>
        [Test]
        public void OmittedRenamedTextureWriteRefusesThePropCheck()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(("_Tex", LockedPropertyState.Renamed)),
                renamed: Names(("_Tex", "S_Tex")),
                runs: Runs(new[] { "PROP_TEX", "OPTIMIZER_ENABLED" }),
                surviving: "float4 PROP_TEX;");
            var locked = new FakeValues().WithTexture(
                "S_Tex",
                new LockedTextureValue("token-f", 4, 4, Vector2.one, Vector2.zero));

            var plan = BuiltPlan(source, properties, locked);
            Assert.That(plan.TextureWrites.ContainsKey("_Tex"), Is.True,
                "The build plans the texture write under the plain name.");

            // The faithful view carries the write under the plain name.
            // The renamed slot is no slot of the original shader.
            var faithful = locked.Apply(plan, "S_Tex");
            Assert.That(
                LockedSourceReproduction.CheckConsistency(source, properties, faithful),
                Is.Null,
                "The faithful view passes the check.");

            // The applier omits the texture write.
            var omitted = faithful.WithoutTexture("_Tex");
            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                omitted);
            Assert.That(detail, Is.EqualTo(PropDefineDetail),
                "The missing texture write refuses the check.");
        }

        // --- Falsifier 10: shared generated shader ---
        // An implementation that reads the keyword list from the
        // OriginalKeywords tag writes an empty list and fails. The tag of
        // the fixture is empty. The no-op guard is the non-empty define
        // run, so the keyword part can only come from the run.
        [Test]
        public void KeywordPartComesFromTheDefinesNotTheTag()
        {
            var properties = Array.Empty<LockedSourceProperty>();
            var source = Source(
                States(),
                runs: Runs(new[] { "_FEATURE_ON", "OPTIMIZER_ENABLED" }));
            var locked = new FakeValues();

            Assert.That(locked.OriginalKeywordsTag, Is.Empty,
                "The fixture tag holds no keyword.");

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.Keywords, Is.EqualTo(new[] { "_FEATURE_ON" }),
                "The keyword part comes from the define run.");
            AssertConsistent(source, properties, locked, plan);
        }

        /// <summary>
        /// A texture keyword that carries the PROP define name appears
        /// once in the keyword part and once in the PROP part (A5.9, fact
        /// 8).
        /// </summary>
        [Test]
        public void TextureKeywordNamedLikeItsPropDefineAppearsInBothParts()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(("_Tex", LockedPropertyState.Unused)),
                runs: Runs(new[] { "PROP_TEX", "PROP_TEX", "OPTIMIZER_ENABLED" }),
                surviving: "float4 PROP_TEX;");
            var locked = new FakeValues().WithTexture(
                "_Tex",
                new LockedTextureValue("token-e", 4, 4, Vector2.one, Vector2.zero));

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.Keywords, Is.EqualTo(new[] { "PROP_TEX" }),
                "The keyword part keeps the first entry.");
            AssertConsistent(source, properties, locked, plan);
        }

        // --- Falsifier 22: keyword define consumed as the PROP define ---
        // An implementation that reads the longest suffix takes the one
        // keyword define for the PROP define and drops the keyword. The
        // texture was empty at the lock and its texture keyword was
        // enabled, so the run holds the name once. The assignment after
        // the lock puts the name in the expected PROP list. The run text
        // alone cannot tell the two shapes apart, so the material must
        // refuse (A5.9 ruling of 2026-10-24). The no-op guard is the
        // non-empty define run with the keyword enabled on the material.
        [Test]
        public void KeywordDefineNamedLikeItsPropDefineRefusesWhenTheRunHoldsItOnce()
        {
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var source = Source(
                States(("_Tex", LockedPropertyState.Unused)),
                runs: Runs(new[] { "PROP_TEX", "OPTIMIZER_ENABLED" }),
                surviving: "float4 PROP_TEX;");
            var locked = new FakeValues()
                .WithTexture(
                    "_Tex",
                    new LockedTextureValue("token-g", 4, 4, Vector2.one, Vector2.zero))
                .WithKeywords("PROP_TEX");

            Assert.That(locked.Keywords, Does.Contain("PROP_TEX"),
                "The texture keyword is enabled on the material.");

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, PropDefineDetail);
        }

        /// <summary>
        /// Program blocks with different keyword parts refuse (A5.9).
        /// </summary>
        [Test]
        public void ProgramBlocksWithDifferentKeywordPartsRefuse()
        {
            var properties = Array.Empty<LockedSourceProperty>();
            var source = Source(
                States(),
                runs: Runs(
                    new[] { "_A_ON", "OPTIMIZER_ENABLED" },
                    new[] { "_B_ON", "OPTIMIZER_ENABLED" }));
            var locked = new FakeValues();

            var result = LockedSourceReproduction.Build(source, properties, locked);

            AssertRefusal(result, KeywordPartDetail);
        }

        /// <summary>
        /// A removed ifex block must hold on the clone values. A false
        /// condition refuses (A6.2).
        /// </summary>
        [Test]
        public void RemovedIfexWithAFalseConditionOnURefuses()
        {
            var properties = new[] { Prop("_X", ShaderPropertyType.Float) };
            var source = Source(
                States(("_X", LockedPropertyState.Live)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_X==1",
                    12)));
            var reproduced = new FakeValues().WithFloat("_X", 0f);

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.EqualTo(ConditionDetail),
                "The false condition refuses the check.");
        }

        // --- Falsifier 21: unsaved declared operand reads zero ---
        // An implementation that reads a missing saved entry as zero
        // accepts a removed block that the finished clone does not hold.
        // The saved entry was zero at the lock and a later edit cleared
        // it, so the clone renders the declared default. Reading zero
        // passes the check and the default fails it (A6.2). The no-op
        // guards prove the operand is a declared property with no saved
        // entry and a nonzero declared default.
        [Test]
        public void UnsavedDeclaredOperandReadsTheDeclaredDefault()
        {
            var properties = new[]
            {
                Prop("_X", ShaderPropertyType.Float, defaultFloat: 1f)
            };
            var source = Source(
                States(("_X", LockedPropertyState.Live)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_X==0",
                    12)));

            Assert.That(properties[0].DefaultFloat, Is.EqualTo(1f),
                "The declared default is nonzero.");

            var reproduced = new FakeValues();
            Assert.That(reproduced.TryGetFloat("_X", out _), Is.False,
                "The clone holds no saved entry.");

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.EqualTo(ConditionDetail),
                "The declared default fails the condition, so the check refuses.");
        }

        /// <summary>
        /// A never saved operand renders its declared default on the
        /// locked material and on the clone. That default made the
        /// removed condition true at the lock, so the check must read
        /// the default and hold (A6.2).
        /// </summary>
        [Test]
        public void NeverSavedOperandWithNonzeroDefaultKeepsARemovedBlockTrue()
        {
            var properties = new[]
            {
                Prop("_X", ShaderPropertyType.Float, defaultFloat: 1f)
            };
            var source = Source(
                States(("_X", LockedPropertyState.Live)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_X!=0",
                    12)));
            var reproduced = new FakeValues();

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.Null,
                "The declared default holds the removed condition.");
        }

        /// <summary>
        /// A removed ifex block with a condition outside the accepted
        /// forms refuses (A6.2, fact 10).
        /// </summary>
        [Test]
        public void RemovedIfexWithAnUnsupportedConditionRefuses()
        {
            var properties = Array.Empty<LockedSourceProperty>();
            var source = Source(
                States(),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "ThryEditor == 3",
                    15)));
            var reproduced = new FakeValues();

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.EqualTo(ConditionUnsupportedDetail),
                "The unsupported condition refuses the check.");
        }

        // --- Falsifier 11: outline set to 2 ---
        // An implementation that takes the compared value writes two and
        // fails the plan value assertion. An inequality fixes no value,
        // so the plan writes the declared default. The no-op guards prove
        // the fixture reaches the path. The operand state is Unused and
        // the block is Removed.
        [Test]
        public void UnusedOperandOfAnInequalityMustEqualTheDefault()
        {
            var properties = new[] { Prop("_Outline", ShaderPropertyType.Float) };
            var source = Source(
                States(("_Outline", LockedPropertyState.Unused)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_Outline!=2",
                    21)));

            Assert.That(source.States["_Outline"], Is.EqualTo(LockedPropertyState.Unused),
                "The operand state is Unused.");
            Assert.That(source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed),
                "The block is Removed.");

            var locked = new FakeValues();
            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.FloatWrites["_Outline"], Is.EqualTo(0f),
                "The inequality fixes no value, so the default applies.");
            AssertConsistent(source, properties, locked, plan);
        }

        /// <summary>
        /// An equality fixes the value of an unused operand (A6.3). The
        /// declared default of the fixture differs, so the compared value
        /// wins.
        /// </summary>
        [Test]
        public void UnusedOperandOfAnEqualityTakesTheComparedValue()
        {
            var properties = new[] { Prop("_A", ShaderPropertyType.Float, 9f) };
            var source = Source(
                States(("_A", LockedPropertyState.Unused)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_A==3",
                    30)));

            var locked = new FakeValues();
            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.FloatWrites["_A"], Is.EqualTo(3f),
                "The equality fixes the compared value.");
            AssertConsistent(source, properties, locked, plan);
        }

        // --- Falsifier 12: fixed value overrides enum text ---
        // An implementation that writes the fixed value of A6.3 without
        // comparing it to the recorded enum text hides the disagreement.
        // The guards prove the fixture reaches the path. The state is
        // Unused, the block is Removed, and the enum value exists.
        [Test]
        public void FixedValueThatDisagreesWithEnumTextRefuses()
        {
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var source = Source(
                States(("_P", LockedPropertyState.Unused)),
                enumValues: Enums(("_P", 2)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_P!=2",
                    35)));

            Assert.That(source.States["_P"], Is.EqualTo(LockedPropertyState.Unused),
                "The operand state is Unused.");
            Assert.That(source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed),
                "The block is Removed.");
            Assert.That(source.EnumValues.ContainsKey("_P"), Is.True,
                "The fixture holds the enum value.");

            var result = LockedSourceReproduction.Build(
                source,
                properties,
                new FakeValues());
            AssertRefusal(result, EnumBakedDetail);

            // No-op guard: the agreeing enum text takes the fixed value
            // and stays consistent.
            var agreeingSource = Source(
                States(("_P", LockedPropertyState.Unused)),
                enumValues: Enums(("_P", 0)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_P!=2",
                    35)));
            var agreeingLocked = new FakeValues();
            var agreeingPlan = BuiltPlan(agreeingSource, properties, agreeingLocked);
            Assert.That(agreeingPlan.FloatWrites["_P"], Is.EqualTo(0f),
                "The agreeing enum text keeps the fixed write.");
            AssertConsistent(agreeingSource, properties, agreeingLocked, agreeingPlan);
        }

        // --- Falsifier 13: fixed value overrides a live bracket ---
        // An implementation that writes the fixed value of A6.3 without
        // comparing it to the live bracket value changes what the
        // bracket renders. The guards prove the fixture reaches the
        // path. The bracket read survives, and the live value differs
        // from the fixed default of zero.
        [Test]
        public void FixedValueThatDisagreesWithALiveBracketRefuses()
        {
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var source = Source(
                States(("_P", LockedPropertyState.Unused)),
                brackets: Names(("_P", "_P")),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Removed,
                    "_P!=2",
                    40)));
            var locked = new FakeValues().WithFloat("_P", 1f);

            Assert.That(source.BracketReads.ContainsKey("_P"), Is.True,
                "The fixture holds the bracket read.");
            Assert.That(
                locked.TryGetFloat("_P", out var live) && live != 0f,
                Is.True,
                "The live value differs from the fixed default.");

            var result = LockedSourceReproduction.Build(source, properties, locked);
            AssertRefusal(result, BracketBakedDetail);

            // No-op guard: the agreeing live value takes the fixed value
            // and stays consistent.
            var agreeingLocked = new FakeValues().WithFloat("_P", 0f);
            var agreeingPlan = BuiltPlan(source, properties, agreeingLocked);
            Assert.That(agreeingPlan.FloatWrites["_P"], Is.EqualTo(0f),
                "The agreeing live value keeps the fixed write.");
            AssertConsistent(source, properties, agreeingLocked, agreeingPlan);
        }

        /// <summary>
        /// A removed ifdef block needs its keyword absent on the clone
        /// values (A6.1).
        /// </summary>
        [Test]
        public void RemovedIfdefWhoseKeywordIsOnRefuses()
        {
            var properties = Array.Empty<LockedSourceProperty>();
            var source = Source(
                States(),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifdef,
                    LockedBlockOutcome.Removed,
                    "_FEATURE_ON",
                    44)));
            var reproduced = new FakeValues().WithKeywords("_FEATURE_ON");

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.EqualTo(IfdefRemovedDetail),
                "The keyword on the clone refuses the removal.");
        }

        /// <summary>
        /// A removed ifndef block needs its keyword present on the clone
        /// values (A6.1).
        /// </summary>
        [Test]
        public void RemovedIfndefWhoseKeywordIsOffRefuses()
        {
            var properties = Array.Empty<LockedSourceProperty>();
            var source = Source(
                States(),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifndef,
                    LockedBlockOutcome.Removed,
                    "_FEATURE_ON",
                    47)));
            var reproduced = new FakeValues();

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.EqualTo(IfndefRemovedDetail),
                "The missing keyword refuses the removal.");
        }

        /// <summary>An unobservable block gets no check (A6.4).</summary>
        [Test]
        public void UnobservableBlockGetsNoCheck()
        {
            var properties = new[] { Prop("_X", ShaderPropertyType.Float) };
            var source = Source(
                States(("_X", LockedPropertyState.Live)),
                blocks: Blocks(Block(
                    LockedBlockKind.Ifex,
                    LockedBlockOutcome.Unobservable,
                    "_X==1",
                    5)));
            var reproduced = new FakeValues().WithFloat("_X", 0f);

            var detail = LockedSourceReproduction.CheckConsistency(
                source,
                properties,
                reproduced);

            Assert.That(detail, Is.Null, "The unobservable block checks nothing.");
        }

        /// <summary>
        /// The render queue and the override tags come from the locked
        /// material (A5.10).
        /// </summary>
        [Test]
        public void RenderQueueAndTagsComeFromTheLockedMaterial()
        {
            var properties = Array.Empty<LockedSourceProperty>();
            var source = Source(States());
            var locked = new FakeValues()
                .WithQueue(2450)
                .WithRenderType("Custom")
                .WithVrcFallback("Fallback");

            var plan = BuiltPlan(source, properties, locked);

            Assert.That(plan.RenderQueue, Is.EqualTo(2450),
                "The render queue moves to the plan.");
            Assert.That(plan.RenderTypeTag, Is.EqualTo("Custom"),
                "The override tag moves to the plan.");
            Assert.That(plan.VrcFallbackTag, Is.EqualTo("Fallback"),
                "The fallback tag moves to the plan.");
        }
    }
}
