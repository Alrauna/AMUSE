using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The outcome of one reproduction build. A refusal carries one fixed
    /// detail sentence (A5). A detail sentence never names a property
    /// value or a path.
    /// </summary>
    internal readonly struct LockedReproductionResult
    {
        internal LockedReproductionResult(
            bool succeeded,
            LockedReproductionPlan plan,
            string refusalDetail)
        {
            Succeeded = succeeded;
            Plan = plan;
            RefusalDetail = refusalDetail;
        }

        internal bool Succeeded { get; }

        internal LockedReproductionPlan Plan { get; }

        internal string RefusalDetail { get; }
    }

    /// <summary>
    /// The pure reproduction map from the aligned facts and the live
    /// values of the locked material to the values of the unlocked clone
    /// (A5). The type never touches a Unity object. The build calls
    /// <see cref="Build"/> while the clone still has the generated shader
    /// and applies the plan after the shader swap. The build and the dry
    /// run both call <see cref="CheckConsistency"/> on the values after
    /// the plan (A8.1).
    /// </summary>
    internal static class LockedSourceReproduction
    {
        // One fixed detail sentence per rule (A5, A6). No sentence names
        // a property value or a path.
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

        // The print formats of fact 5. A scalar with a decimal point
        // prints with the vendor float format. A scalar without one
        // prints with F0. A float4 component prints with the
        // parameterless invariant format.
        private const string ScalarDecimalFormat = "0.0####################";
        private const string ScalarIntegerFormat = "F0";
        private const string Float4Head = "float4(";
        private const string Float4Tail = ")";

        private const string OptimizerDefine = "OPTIMIZER_ENABLED";
        private const string PropDefinePrefix = "PROP";
        private const string ScaleSuffix = "_ST";
        private const string TexelSizeSuffix = "_TexelSize";

        /// <summary>
        /// Builds the plan for one aligned lock (A5). The method refuses
        /// with a fixed sentence when a rule of A5 fails. The method
        /// throws on a broken aligned record, because that record breaks
        /// the A4 invariant.
        /// </summary>
        internal static LockedReproductionResult Build(
            AlignedLockedSource source,
            IReadOnlyList<LockedSourceProperty> properties,
            ILockedMaterialValues locked)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            if (locked == null)
            {
                throw new ArgumentNullException(nameof(locked));
            }

            var propsByName = new Dictionary<string, LockedSourceProperty>();
            foreach (var property in properties)
            {
                propsByName[property.Name] = property;
            }

            var floatWrites = new Dictionary<string, float>();
            var vectorWrites = new Dictionary<string, Vector4>();
            var colorWrites = new Dictionary<string, Color>();
            var scaleOffsetWrites = new Dictionary<string, Vector4>();
            var textureWrites = new Dictionary<string, LockedTextureValue>();

            // The unused operand writes of A6.3 come first. The removed
            // block contract fixes those values, so they win over the
            // keep rules of A5.6 and A5.7. The evaluator must accept a
            // condition before its operand list is valid (A6.2).
            var fixedValues = new Dictionary<string, float>();
            foreach (var block in source.Blocks)
            {
                if (block.Kind != LockedBlockKind.Ifex
                    || block.Outcome != LockedBlockOutcome.Removed)
                {
                    continue;
                }

                var outcome = PoiyomiLockCondition.Evaluate(
                    block.ConditionOrKeyword,
                    new LockValuesAdapter(locked, properties));
                if (outcome == PoiyomiLockConditionOutcome.Unsupported)
                {
                    return Refuse(ConditionUnsupportedDetail);
                }

                foreach (var name in PoiyomiLockCondition.ComparisonOperands(
                    block.ConditionOrKeyword))
                {
                    if (fixedValues.ContainsKey(name)
                        || !source.States.TryGetValue(name, out var state)
                        || state != LockedPropertyState.Unused
                        || !propsByName.TryGetValue(name, out var operand))
                    {
                        continue;
                    }

                    // An equality fixes the compared value. Any other
                    // comparison fixes no value, so the declared default
                    // applies (A6.3).
                    var compared = FixedEqualityValue(block.ConditionOrKeyword, name);
                    fixedValues[name] = compared ?? operand.DefaultFloat;
                }
            }

            foreach (var property in properties)
            {
                if (!source.States.TryGetValue(property.Name, out var state))
                {
                    // The aligner records a state for every declared
                    // property (A4.1). A missing record breaks that
                    // invariant and blocks the build.
                    throw new InvalidOperationException(
                        "The aligned record misses a property state.");
                }

                switch (state)
                {
                    case LockedPropertyState.Live:
                        WriteLive(property, locked, vectorWrites, colorWrites, textureWrites, scaleOffsetWrites, floatWrites);
                        break;
                    case LockedPropertyState.Renamed:
                        if (!source.RenamedNames.TryGetValue(property.Name, out var renamedName))
                        {
                            throw new InvalidOperationException(
                                "The aligned record misses a renamed name.");
                        }

                        WriteRenamed(property, renamedName, locked, vectorWrites, colorWrites, textureWrites, scaleOffsetWrites, floatWrites);
                        break;
                    case LockedPropertyState.Baked:
                        if (!source.BakedLiterals.TryGetValue(property.Name, out var literal))
                        {
                            throw new InvalidOperationException(
                                "The aligned record misses a baked literal.");
                        }

                        var bakedDetail = WriteBaked(
                            source, property, literal, locked, floatWrites, vectorWrites, colorWrites);
                        if (bakedDetail != null)
                        {
                            return Refuse(bakedDetail);
                        }

                        break;
                    case LockedPropertyState.Unused:
                        if (fixedValues.TryGetValue(property.Name, out var fixedValue))
                        {
                            // The removed block contract fixes the value
                            // (A6.3). The locked material still renders
                            // its recorded enum text and its surviving
                            // bracket read (A5.6, A5.7). A disagreement
                            // means the material would read two values.
                            if (source.EnumValues.TryGetValue(property.Name, out var fixedEnum)
                                && fixedEnum != fixedValue)
                            {
                                return Refuse(EnumBakedDetail);
                            }

                            if (source.BracketReads.ContainsKey(property.Name)
                                && (!locked.TryGetFloat(property.Name, out var fixedLive)
                                    || fixedLive != fixedValue))
                            {
                                return Refuse(BracketBakedDetail);
                            }

                            floatWrites[property.Name] = fixedValue;
                        }
                        else if (source.EnumValues.TryGetValue(property.Name, out var enumValue))
                        {
                            // Enum text with no program use takes the
                            // enum value (A5.6).
                            floatWrites[property.Name] = enumValue;
                            if (source.BracketReads.ContainsKey(property.Name)
                                && (!locked.TryGetFloat(property.Name, out var bracketValue)
                                    || bracketValue != enumValue))
                            {
                                return Refuse(EnumBracketDetail);
                            }
                        }
                        else if (source.BracketReads.ContainsKey(property.Name))
                        {
                            // A program unused property with a surviving
                            // bracket read keeps the locked value (A5.7).
                            if (locked.TryGetFloat(property.Name, out var live))
                            {
                                floatWrites[property.Name] = live;
                            }
                        }

                        break;
                }

                // The derived texture entries of A5.4. A baked ST writes
                // the scale and offset and wins over the texture read,
                // because the text is the truth (A1.1). A baked texel
                // size writes nothing. It must equal the print of the
                // texture facts on the clone.
                if (property.Type == ShaderPropertyType.Texture)
                {
                    var stName = property.Name + ScaleSuffix;
                    if (source.States.TryGetValue(stName, out var stState)
                        && stState == LockedPropertyState.Baked)
                    {
                        var stDetail = RoundTripFloat4(
                            source.BakedLiterals[stName], out var scaleOffset);
                        if (stDetail != null)
                        {
                            return Refuse(stDetail);
                        }

                        scaleOffsetWrites[property.Name] = scaleOffset;
                    }

                    var texelName = property.Name + TexelSizeSuffix;
                    if (source.States.TryGetValue(texelName, out var texelState)
                        && texelState == LockedPropertyState.Baked)
                    {
                        var texelLiteral = source.BakedLiterals[texelName];
                        if (texelLiteral != ExpectedTexelSize(source, locked, property.Name))
                        {
                            return Refuse(TexelSizeDetail);
                        }
                    }
                }
            }

            // The keyword and PROP split of the define runs (A5.9). The
            // PROP part must match the textures. All keyword parts must
            // agree. The locked view reads renamed texture names.
            var expectedProps = ExpectedPropDefines(source, properties, locked, true);
            List<string> keywordPart = null;
            foreach (var run in source.DefineRuns)
            {
                if (!TrySplitRun(run, expectedProps, locked.Keywords, out var runKeywords))
                {
                    return Refuse(PropDefineDetail);
                }

                if (keywordPart == null)
                {
                    keywordPart = runKeywords;
                }
                else if (!SameNames(keywordPart, runKeywords))
                {
                    return Refuse(KeywordPartDetail);
                }
            }

            var keywords = keywordPart ?? new List<string>();
            foreach (var extra in locked.OriginalKeywordsTag)
            {
                // A tag keyword outside the surviving text has no define,
                // so it cannot change rendering (A5.9).
                if (!source.SurvivingText.Contains(extra) && !keywords.Contains(extra))
                {
                    keywords.Add(extra);
                }
            }

            var plan = new LockedReproductionPlan(
                floatWrites,
                vectorWrites,
                colorWrites,
                scaleOffsetWrites,
                textureWrites,
                keywords,
                locked.RenderQueue,
                locked.RenderTypeTag,
                locked.VrcFallbackTag);
            return new LockedReproductionResult(true, plan, null);
        }

        /// <summary>
        /// Checks the values after the plan against the aligned facts
        /// (A5.9, A6). The method returns null when the values stay
        /// consistent. Otherwise it returns one fixed detail sentence.
        /// The build passes the finished clone. The dry run passes its
        /// plan view (A8.1). The reproduced view reads the plain names
        /// only, because the renamed names are no slots of the original
        /// shader after the swap.
        /// </summary>
        internal static string CheckConsistency(
            AlignedLockedSource source,
            IReadOnlyList<LockedSourceProperty> properties,
            ILockedMaterialValues reproduced)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (properties == null)
            {
                throw new ArgumentNullException(nameof(properties));
            }

            if (reproduced == null)
            {
                throw new ArgumentNullException(nameof(reproduced));
            }

            // The PROP part must match the textures after the plan
            // (A5.9). The keyword part must agree across the runs. The
            // view side reads plain names only.
            var expectedProps = ExpectedPropDefines(source, properties, reproduced, false);
            List<string> keywordPart = null;
            foreach (var run in source.DefineRuns)
            {
                if (!TrySplitRun(run, expectedProps, reproduced.Keywords, out var runKeywords))
                {
                    return PropDefineDetail;
                }

                if (keywordPart == null)
                {
                    keywordPart = runKeywords;
                }
                else if (!SameNames(keywordPart, runKeywords))
                {
                    return KeywordPartDetail;
                }

                // The clone carries the keyword part of every run
                // (A5.9). A PROP define that slides into the keyword
                // part marks a texture that the view lost, because the
                // build split the run with the full texture list.
                foreach (var name in runKeywords)
                {
                    if (!ContainsName(reproduced.Keywords, name))
                    {
                        return PropDefineDetail;
                    }
                }
            }

            // Only removed blocks need checks. A kept block compiles on
            // both materials (A6).
            foreach (var block in source.Blocks)
            {
                if (block.Outcome != LockedBlockOutcome.Removed)
                {
                    // Unobservable and not evaluated blocks get no check
                    // (A6.4).
                    continue;
                }

                if (block.Kind == LockedBlockKind.Ifdef)
                {
                    // A removed ifdef block needs its keyword off (A6.1).
                    if (ContainsName(reproduced.Keywords, block.ConditionOrKeyword))
                    {
                        return IfdefRemovedDetail;
                    }
                }
                else if (block.Kind == LockedBlockKind.Ifndef)
                {
                    // A removed ifndef block needs its keyword present.
                    if (!ContainsName(reproduced.Keywords, block.ConditionOrKeyword))
                    {
                        return IfndefRemovedDetail;
                    }
                }
                else
                {
                    // A removed ifex block needs its comparisons true on
                    // the clone (A6.2). The evaluator ports the vendor
                    // grammar, and isNotAnimated counts as true, because
                    // the removal proves it held at lock time.
                    var outcome = PoiyomiLockCondition.Evaluate(
                        block.ConditionOrKeyword,
                        new LockValuesAdapter(reproduced, properties));
                    if (outcome == PoiyomiLockConditionOutcome.Unsupported)
                    {
                        return ConditionUnsupportedDetail;
                    }

                    if (outcome == PoiyomiLockConditionOutcome.False)
                    {
                        return ConditionDetail;
                    }
                }
            }

            return null;
        }

        private static LockedReproductionResult Refuse(string detail)
        {
            return new LockedReproductionResult(false, null, detail);
        }

        private static void WriteLive(
            LockedSourceProperty property,
            ILockedMaterialValues locked,
            Dictionary<string, Vector4> vectorWrites,
            Dictionary<string, Color> colorWrites,
            Dictionary<string, LockedTextureValue> textureWrites,
            Dictionary<string, Vector4> scaleOffsetWrites,
            Dictionary<string, float> floatWrites)
        {
            // A live property keeps the locked value (A5.7).
            if (property.Type == ShaderPropertyType.Texture)
            {
                if (locked.TryGetTexture(property.Name, out var texture)
                    && texture.IdentityToken != null)
                {
                    textureWrites[property.Name] = texture;
                    scaleOffsetWrites[property.Name] = ScaleOffsetOf(texture);
                }
            }
            else if (property.Type == ShaderPropertyType.Vector)
            {
                if (locked.TryGetVector(property.Name, out var vector))
                {
                    vectorWrites[property.Name] = vector;
                }
            }
            else if (property.Type == ShaderPropertyType.Color)
            {
                if (locked.TryGetColor(property.Name, out var color))
                {
                    colorWrites[property.Name] = color;
                }
            }
            else
            {
                if (locked.TryGetFloat(property.Name, out var value))
                {
                    floatWrites[property.Name] = value;
                }
            }
        }

        private static void WriteRenamed(
            LockedSourceProperty property,
            string renamedName,
            ILockedMaterialValues locked,
            Dictionary<string, Vector4> vectorWrites,
            Dictionary<string, Color> colorWrites,
            Dictionary<string, LockedTextureValue> textureWrites,
            Dictionary<string, Vector4> scaleOffsetWrites,
            Dictionary<string, float> floatWrites)
        {
            // The clone reads the renamed name and writes the plain name
            // (A5.3). An unsaved renamed float reads the generated shader
            // default, which is the property table default. An unsaved
            // slot of another type stays unset, so the clone renders its
            // shader default the same way.
            if (property.Type == ShaderPropertyType.Texture)
            {
                if (locked.TryGetTexture(renamedName, out var texture)
                    && texture.IdentityToken != null)
                {
                    textureWrites[property.Name] = texture;
                    scaleOffsetWrites[property.Name] = ScaleOffsetOf(texture);
                }
            }
            else if (property.Type == ShaderPropertyType.Vector)
            {
                if (locked.TryGetVector(renamedName, out var vector))
                {
                    vectorWrites[property.Name] = vector;
                }
            }
            else if (property.Type == ShaderPropertyType.Color)
            {
                if (locked.TryGetColor(renamedName, out var color))
                {
                    colorWrites[property.Name] = color;
                }
            }
            else
            {
                floatWrites[property.Name] = locked.TryGetFloat(renamedName, out var value)
                    ? value
                    : property.DefaultFloat;
            }
        }

        /// <summary>
        /// Writes one baked property (A5.4). The parsed literal is the
        /// value. The literal must print back to its own text. A
        /// surviving bracket read or an enum text must agree with the
        /// literal (A5.5, A5.6). The method returns null on success and
        /// one fixed detail sentence otherwise.
        /// </summary>
        private static string WriteBaked(
            AlignedLockedSource source,
            LockedSourceProperty property,
            string literal,
            ILockedMaterialValues locked,
            Dictionary<string, float> floatWrites,
            Dictionary<string, Vector4> vectorWrites,
            Dictionary<string, Color> colorWrites)
        {
            if (property.Type == ShaderPropertyType.Vector)
            {
                var vectorDetail = RoundTripFloat4(literal, out var vector);
                if (vectorDetail != null)
                {
                    return vectorDetail;
                }

                vectorWrites[property.Name] = vector;
                return null;
            }

            if (property.Type == ShaderPropertyType.Color)
            {
                var colorDetail = RoundTripFloat4(literal, out var baked);
                if (colorDetail != null)
                {
                    return colorDetail;
                }

                colorWrites[property.Name] = InverseBake(property, baked, locked);
                return null;
            }

            if (property.Type == ShaderPropertyType.Texture)
            {
                // No literal shape fits a texture slot.
                return LiteralShapeDetail;
            }

            var scalarDetail = RoundTripScalar(literal, out var parsed);
            if (scalarDetail != null)
            {
                return scalarDetail;
            }

            if (source.EnumValues.TryGetValue(property.Name, out var enumValue)
                && enumValue != parsed)
            {
                return EnumBakedDetail;
            }

            if (source.BracketReads.ContainsKey(property.Name)
                && (!locked.TryGetFloat(property.Name, out var live) || live != parsed))
            {
                // The locked material reads two values (A5.5).
                return BracketBakedDetail;
            }

            floatWrites[property.Name] = parsed;
            return null;
        }

        /// <summary>
        /// Inverts the fact 6 bake conversion (A5.4). The bake applied one
        /// linear conversion when the HDR and Gamma flags agree and the
        /// project is linear. The inverse of that conversion is the gamma
        /// conversion. The alpha never converts.
        /// </summary>
        private static Color InverseBake(
            LockedSourceProperty property,
            Vector4 baked,
            ILockedMaterialValues locked)
        {
            var wasLinear = property.Hdr == property.Gamma && locked.ProjectIsLinear;
            var rgb = wasLinear
                ? new Color(baked.x, baked.y, baked.z).gamma
                : new Color(baked.x, baked.y, baked.z);
            rgb.a = baked.w;
            return rgb;
        }

        /// <summary>
        /// Parses one scalar literal of fact 5. The shape comes from the
        /// decimal point. A literal with a point prints with the float
        /// format. A literal without one prints with F0. The parsed value
        /// must print back to the literal text (A5.4). The method returns
        /// null on success and the round trip sentence otherwise.
        /// </summary>
        private static string RoundTripScalar(string literal, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(literal)
                || !float.TryParse(
                    literal,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var parsed))
            {
                return RoundTripDetail;
            }

            var format = literal.IndexOf('.') < 0
                ? ScalarIntegerFormat
                : ScalarDecimalFormat;
            if (parsed.ToString(format, CultureInfo.InvariantCulture) != literal)
            {
                return RoundTripDetail;
            }

            value = parsed;
            return null;
        }

        /// <summary>
        /// Parses one float4 literal of fact 5. Every component prints
        /// with the parameterless invariant format and must give back its
        /// own text (A5.4). The method returns null on success.
        /// </summary>
        private static string RoundTripFloat4(string literal, out Vector4 value)
        {
            value = default;
            if (literal == null
                || !literal.StartsWith(Float4Head, StringComparison.Ordinal)
                || !literal.EndsWith(Float4Tail, StringComparison.Ordinal))
            {
                return LiteralShapeDetail;
            }

            var inner = literal.Substring(
                Float4Head.Length,
                literal.Length - Float4Head.Length - Float4Tail.Length);
            var parts = inner.Split(',');
            if (parts.Length != 4)
            {
                return LiteralShapeDetail;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(
                        parts[i],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var component)
                    || component.ToString(CultureInfo.InvariantCulture) != parts[i])
                {
                    return RoundTripDetail;
                }

                value[i] = component;
            }

            return null;
        }

        /// <summary>
        /// Packs the scale and offset of one texture read into the Unity
        /// ST order (A5.4).
        /// </summary>
        private static Vector4 ScaleOffsetOf(LockedTextureValue texture)
        {
            return new Vector4(
                texture.Scale.x,
                texture.Scale.y,
                texture.Offset.x,
                texture.Offset.y);
        }

        /// <summary>
        /// Reads the texture that the plan puts on the clone for one
        /// texture property (A5.4). A renamed texture sits under its
        /// renamed name. A null token means no texture.
        /// </summary>
        private static bool TryTextureOnU(
            AlignedLockedSource source,
            ILockedMaterialValues values,
            string name,
            out LockedTextureValue texture)
        {
            if (source.States.TryGetValue(name, out var state)
                && state == LockedPropertyState.Renamed
                && source.RenamedNames.TryGetValue(name, out var renamedName))
            {
                return values.TryGetTexture(renamedName, out texture);
            }

            return values.TryGetTexture(name, out texture);
        }

        /// <summary>
        /// Builds the fact 5 print of (1/w, 1/h, w, h) for the texture on
        /// the clone. No texture expects the print of (1, 1, 1, 1)
        /// (A5.4).
        /// </summary>
        private static string ExpectedTexelSize(
            AlignedLockedSource source,
            ILockedMaterialValues values,
            string name)
        {
            if (!TryTextureOnU(source, values, name, out var texture)
                || texture.IdentityToken == null)
            {
                return Float4Head + "1,1,1,1" + Float4Tail;
            }

            return Float4Head
                + InvertFloat(texture.Width) + ","
                + InvertFloat(texture.Height) + ","
                + texture.Width.ToString(CultureInfo.InvariantCulture) + ","
                + texture.Height.ToString(CultureInfo.InvariantCulture)
                + Float4Tail;
        }

        private static string InvertFloat(int size)
        {
            return (1f / size).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Lists the PROP defines that the textures expect (A5.9). The
        /// list walks the property order. Only an assigned texture
        /// counts, and only when its define occurs in the surviving text
        /// (fact 8). The locked view reads the renamed name. The
        /// reproduced view reads the plain name only, because the
        /// renamed names are no slots of the original shader after the
        /// swap.
        /// </summary>
        private static List<string> ExpectedPropDefines(
            AlignedLockedSource source,
            IReadOnlyList<LockedSourceProperty> properties,
            ILockedMaterialValues values,
            bool allowRenamedName)
        {
            var expected = new List<string>();
            foreach (var property in properties)
            {
                if (property.Type != ShaderPropertyType.Texture)
                {
                    continue;
                }

                var readName = property.Name;
                if (allowRenamedName
                    && source.States.TryGetValue(property.Name, out var state)
                    && state == LockedPropertyState.Renamed
                    && source.RenamedNames.TryGetValue(property.Name, out var renamedName))
                {
                    readName = renamedName;
                }

                if (!values.TryGetTexture(readName, out var texture)
                    || texture.IdentityToken == null)
                {
                    continue;
                }

                var define = PropDefinePrefix + property.Name.ToUpperInvariant();
                if (source.SurvivingText.Contains(define))
                {
                    expected.Add(define);
                }
            }

            return expected;
        }

        /// <summary>
        /// Splits one define run into its keyword part and its PROP part
        /// (A5.9, fact 8). The run ends with the optimizer define. The
        /// PROP part is the suffix before it that must equal the expected
        /// list. Everything before the suffix is the keyword part.
        /// A5.9 ruling of 2026-10-24: a texture keyword can carry the
        /// same name as its PROP define. When the expected PROP part
        /// holds such a name, the keyword is enabled on the material,
        /// and the run holds the name only once, the text cannot tell
        /// the keyword define from the PROP define. The suffix split
        /// would read the keyword occurrence and drop the keyword, so
        /// the ambiguous shape refuses.
        /// </summary>
        private static bool TrySplitRun(
            IReadOnlyList<string> run,
            IReadOnlyList<string> expectedProps,
            IReadOnlyList<string> viewKeywords,
            out List<string> keywordPart)
        {
            keywordPart = null;
            if (run.Count == 0 || run[run.Count - 1] != OptimizerDefine)
            {
                return false;
            }

            foreach (var define in expectedProps)
            {
                if (!ContainsName(viewKeywords, define))
                {
                    continue;
                }

                var occurrences = 0;
                foreach (var entry in run)
                {
                    if (entry == define)
                    {
                        occurrences++;
                    }
                }

                if (occurrences == 1)
                {
                    return false;
                }
            }

            var propStart = run.Count - 1 - expectedProps.Count;
            if (propStart < 0)
            {
                return false;
            }

            for (var i = 0; i < expectedProps.Count; i++)
            {
                if (run[propStart + i] != expectedProps[i])
                {
                    return false;
                }
            }

            keywordPart = new List<string>(propStart);
            for (var i = 0; i < propStart; i++)
            {
                keywordPart.Add(run[i]);
            }

            return true;
        }

        private static bool SameNames(IReadOnlyList<string> left, IReadOnlyList<string> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsName(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (candidate == name)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Reads the number that one equality fixes for one unused
        /// operand (A6.3). The leaf must hold the property name on one
        /// side and one plain number on the other side. Any other shape
        /// fixes no value, so the caller takes the declared default.
        /// </summary>
        private static float? FixedEqualityValue(string conditionText, string name)
        {
            if (conditionText == null)
            {
                return null;
            }

            foreach (var leaf in SplitLeaves(conditionText, 0, conditionText.Length))
            {
                // A function call is no plain comparison (fact 10).
                if (leaf.IndexOf('(') >= 0)
                {
                    continue;
                }

                var operatorIndex = LeafOperatorIndex(leaf, out var operatorLength);
                if (operatorIndex < 0
                    || operatorLength != 2
                    || leaf[operatorIndex] != '=')
                {
                    continue;
                }

                var left = leaf.Substring(0, operatorIndex).Trim();
                var right = leaf.Substring(operatorIndex + operatorLength).Trim();
                if (left == name && TryConditionNumber(right, out var value))
                {
                    return value;
                }

                if (right == name && TryConditionNumber(left, out value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Splits one condition at the top level joins of fact 9. The
        /// first join at depth zero wins, and no precedence applies.
        /// </summary>
        private static IEnumerable<string> SplitLeaves(string text, int start, int end)
        {
            var depth = 0;
            for (var i = start; i < end - 1; i++)
            {
                var c = text[i];
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                }
                else if (depth == 0 && (c == '&' || c == '|') && text[i + 1] == c)
                {
                    foreach (var left in SplitLeaves(text, start, i))
                    {
                        yield return left;
                    }

                    foreach (var right in SplitLeaves(text, i + 2, end))
                    {
                        yield return right;
                    }

                    yield break;
                }
            }

            yield return text.Substring(start, end - start);
        }

        /// <summary>
        /// Finds the first comparison operator of one leaf. The two
        /// character forms win over the one character forms at the same
        /// position. A lone equals or exclamation mark is outside the
        /// grammar and ends the search.
        /// </summary>
        private static int LeafOperatorIndex(string leaf, out int operatorLength)
        {
            for (var i = 0; i < leaf.Length; i++)
            {
                var c = leaf[i];
                if (c == '=' || c == '!')
                {
                    if (i + 1 < leaf.Length && leaf[i + 1] == '=')
                    {
                        operatorLength = 2;
                        return i;
                    }

                    break;
                }

                if (c == '>' || c == '<')
                {
                    if (i + 1 < leaf.Length && leaf[i + 1] == '=')
                    {
                        operatorLength = 2;
                        return i;
                    }

                    operatorLength = 1;
                    return i;
                }
            }

            operatorLength = 0;
            return -1;
        }

        private static bool TryConditionNumber(string text, out float value)
        {
            return float.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        /// <summary>
        /// Bridges the material value surface to the lock condition reader
        /// (A6.2). A texture slot stays a texture slot with no texture
        /// assigned, because the vendor asks for the slot. A declared
        /// name with no saved entry reads the declared default, because
        /// that is the value both materials render. An unknown name
        /// still reads zero (fact 9).
        /// </summary>
        private sealed class LockValuesAdapter : IPoiyomiLockConditionValues
        {
            private readonly ILockedMaterialValues values;
            private readonly Dictionary<string, LockedSourceProperty> declared;

            internal LockValuesAdapter(
                ILockedMaterialValues values,
                IReadOnlyList<LockedSourceProperty> properties)
            {
                this.values = values;
                declared = new Dictionary<string, LockedSourceProperty>();
                foreach (var property in properties)
                {
                    declared[property.Name] = property;
                }
            }

            public int RenderQueue => values.RenderQueue;

            public bool IsTexture(string propertyName)
            {
                return values.TryGetTexture(propertyName, out _);
            }

            public bool TryGetNumber(string propertyName, out float value)
            {
                if (values.TryGetFloat(propertyName, out value))
                {
                    return true;
                }

                // The vendor reads the live typed value. A property with
                // no saved entry renders its declared default (A6.2).
                if (declared.TryGetValue(propertyName, out var property))
                {
                    value = property.DefaultFloat;
                    return true;
                }

                return false;
            }
        }
    }
}
