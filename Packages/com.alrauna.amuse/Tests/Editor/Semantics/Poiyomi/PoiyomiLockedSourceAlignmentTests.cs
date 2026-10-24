using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The locked source aligner emulates the closed list of vendor edits on
    /// a synthetic original and matches the result against a hand built
    /// generated text (A3). Every fixture holds synthetic property names and
    /// no vendor text. The generated text of each case is written by hand so
    /// the expected bytes stay visible in the test.
    /// </summary>
    public sealed class PoiyomiLockedSourceAlignmentTests
    {
        private const string LineMismatchDetail =
            "A generated line does not match the expected line.";

        private const string UnconsumedLineDetail =
            "The generated text holds a line that no original edit produces.";

        private const string TwoLiteralsDetail =
            "One property shows two different baked literals.";

        private const string MixedUsesDetail =
            "One property mixes live and baked uses.";

        private const string SuffixAmbiguousDetail =
            "The rename suffix of the generated names is ambiguous.";

        private const string InlinedMarkerDetail =
            "The generated text holds an inlined marker.";

        private const string IncludeDetail =
            "The original text includes a file outside the Unity include set.";

        private const string BlockTieDetail =
            "A block outcome stays undecided.";

        private const string OptimizerMissingDetail =
            "A program block misses the optimizer define.";

        // --- Falsifier 1: an implementation that folds inner whitespace ---
        // An implementation that trims or folds runs of spaces inside a line
        // before the compare aligns the mutated pair and fails the refusal
        // assertion (A3.2, Review Focus 1). The guard before the mutation
        // proves the fixture reaches the line matcher.

        // --- Falsifier 2: an implementation that keeps the last literal ---
        // An implementation that overwrites the recorded literal on every
        // use aligns the two-literal pair and fails the refusal assertion.
        // The float bake test proves the hole path records one literal.

        // --- Falsifier 3: an implementation that decides a state per use ---
        // An implementation that lets one use stay live while another use
        // bakes aligns the mixed pair and fails the refusal assertion. The
        // float bake test proves the hole path reaches the state decision.

        // --- Falsifier 4: an implementation that takes the first split ---
        // An implementation that reads the suffix from the first candidate
        // of the first artifact misses the disagreement between artifacts
        // and aligns the ambiguous pair. The underscore intersection test
        // proves the intersection path finds one suffix.

        // --- Falsifier 5: an implementation that reuses one rename regex ---
        // An implementation that runs the 2.74 left boundary rule in the
        // bundled era refuses the bundled fixture, because the collateral
        // line then never matches. The bundled success assertion is the
        // guard and fails the wrong regex choice.

        private static readonly string[] BaseOriginalLines =
        {
            "Shader \"Custom/Test\"",
            "{",
            "    Properties",
            "    {",
            "        _Color (\"Color\", Color) = (1, 1, 1, 1)",
            "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 0",
            "        _P (\"P\", Float) = 0.5",
            "        _Tex (\"Tex\", 2D) = \"white\" {}",
            "        _V (\"V\", Vector) = (0, 0, 0, 0)",
            "        _I (\"I\", Integer) = 0",
            "        _Cull (\"Cull\", Float) = 0",
            "        _Unused (\"Unused\", Float) = 0",
            "    }",
            "    SubShader",
            "    {",
            "        Tags { \"RenderType\" = \"Opaque\" }",
            "        Cull [_Cull]",
            "        Pass",
            "        {",
            "            CGPROGRAM",
            "            #pragma surface surf Standard",
            "            #pragma shader_feature _FEATURE_ON",
            "            #pragma vertex vert",
            "            sampler2D _Tex;",
            "            float _P;",
            "            float4 _V;",
            "            int _I;",
            "            struct Input",
            "            {",
            "                float2 uv;",
            "            };",
            "            void vert (inout appdata_full v)",
            "            {",
            "            }",
            "            void surf (Input IN, inout SurfaceOutput o)",
            "            {",
            "                o.Albedo = _P;",
            "                o.Albedo += _V;",
            "                o.Alpha = _I;",
            "                o.Albedo *= tex2D(_Tex, IN.uv).rgb;",
            "                // o.Albedo = _Unused;",
            "            }",
            "            ENDCG",
            "        }",
            "    }",
            "}"
        };

        private static string Join(IEnumerable<string> lines)
        {
            return string.Join("\n", lines);
        }

        private static string BaseOriginal()
        {
            return Join(BaseOriginalLines);
        }

        private static string BaseGenerated()
        {
            // The vendor deletes the comment line and the shader feature
            // pragma. It writes the locked name, the lock button default 1,
            // and the define run after CGPROGRAM (A3.3, fact 8, fact 12).
            var lines = new List<string>
            {
                "Shader \"Hidden/Locked/Custom/Test/d4\""
            };
            lines.AddRange(BaseOriginalLines.Skip(1).Take(18));
            lines.Add("            CGPROGRAM");
            lines.Add(" #define OPTIMIZER_ENABLED ");
            lines.Add("            #pragma surface surf Standard");
            lines.AddRange(BaseOriginalLines.Skip(22).Take(18));
            lines.AddRange(BaseOriginalLines.Skip(41).Take(5));
            // The lock button default becomes 1 (fact 13).
            return Join(lines).Replace(
                "[ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 0",
                "[ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 1");
        }

        private static (string Original, string Generated) BasePair()
        {
            return (BaseOriginal(), BaseGenerated());
        }

        private static (string Original, string Generated) SmallPair(
            string propertiesLines,
            string shaderLabLines,
            string programLines,
            params (string From, string To)[] generatedEdits)
        {
            var originalLines = new List<string>
            {
                "Shader \"Custom/Small\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 0"
            };
            if (!string.IsNullOrEmpty(propertiesLines))
            {
                originalLines.AddRange(propertiesLines.Split('\n'));
            }

            originalLines.Add("    }");
            originalLines.Add("    SubShader");
            originalLines.Add("    {");
            originalLines.Add("        Tags { \"RenderType\" = \"Opaque\" }");
            if (!string.IsNullOrEmpty(shaderLabLines))
            {
                originalLines.AddRange(shaderLabLines.Split('\n'));
            }

            originalLines.Add("        Pass");
            originalLines.Add("        {");
            originalLines.Add("            CGPROGRAM");
            originalLines.Add("            #pragma vertex vert");
            if (!string.IsNullOrEmpty(programLines))
            {
                originalLines.AddRange(programLines.Split('\n'));
            }

            originalLines.Add("            ENDCG");
            originalLines.Add("        }");
            originalLines.Add("    }");
            originalLines.Add("}");

            var original = Join(originalLines);
            var generatedLines = new List<string>
            {
                "Shader \"Hidden/Locked/Custom/Small/k1\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 1"
            };
            if (!string.IsNullOrEmpty(propertiesLines))
            {
                generatedLines.AddRange(propertiesLines.Split('\n'));
            }

            generatedLines.Add("    }");
            generatedLines.Add("    SubShader");
            generatedLines.Add("    {");
            generatedLines.Add("        Tags { \"RenderType\" = \"Opaque\" }");
            if (!string.IsNullOrEmpty(shaderLabLines))
            {
                generatedLines.AddRange(shaderLabLines.Split('\n'));
            }

            generatedLines.Add("        Pass");
            generatedLines.Add("        {");
            generatedLines.Add("            CGPROGRAM");
            generatedLines.Add(" #define OPTIMIZER_ENABLED ");
            generatedLines.Add("            #pragma vertex vert");
            if (!string.IsNullOrEmpty(programLines))
            {
                generatedLines.AddRange(programLines.Split('\n'));
            }

            generatedLines.Add("            ENDCG");
            generatedLines.Add("        }");
            generatedLines.Add("    }");
            generatedLines.Add("}");

            var generated = Join(generatedLines);
            foreach (var edit in generatedEdits)
            {
                generated = generated.Replace(edit.From, edit.To);
            }

            return (original, generated);
        }

        /// <summary>
        /// Builds a one pass pair where the program body of the generated
        /// text is written by hand. The block markers are comment lines, so
        /// a removed or kept block only changes which body lines survive.
        /// </summary>
        private static (string Original, string Generated) ProgramPair(
            string propertiesLines,
            string programLines,
            string generatedProgramLines)
        {
            var originalLines = new List<string>
            {
                "Shader \"Custom/Small\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 0"
            };
            if (!string.IsNullOrEmpty(propertiesLines))
            {
                originalLines.AddRange(propertiesLines.Split('\n'));
            }

            originalLines.Add("    }");
            originalLines.Add("    SubShader");
            originalLines.Add("    {");
            originalLines.Add("        Tags { \"RenderType\" = \"Opaque\" }");
            originalLines.Add("        Pass");
            originalLines.Add("        {");
            originalLines.Add("            CGPROGRAM");
            originalLines.Add("            #pragma vertex vert");
            if (!string.IsNullOrEmpty(programLines))
            {
                originalLines.AddRange(programLines.Split('\n'));
            }

            originalLines.Add("            ENDCG");
            originalLines.Add("        }");
            originalLines.Add("    }");
            originalLines.Add("}");

            var generatedLines = new List<string>
            {
                "Shader \"Hidden/Locked/Custom/Small/k1\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 1"
            };
            if (!string.IsNullOrEmpty(propertiesLines))
            {
                generatedLines.AddRange(propertiesLines.Split('\n'));
            }

            generatedLines.Add("    }");
            generatedLines.Add("    SubShader");
            generatedLines.Add("    {");
            generatedLines.Add("        Tags { \"RenderType\" = \"Opaque\" }");
            generatedLines.Add("        Pass");
            generatedLines.Add("        {");
            generatedLines.Add("            CGPROGRAM");
            generatedLines.Add(" #define OPTIMIZER_ENABLED ");
            generatedLines.Add("            #pragma vertex vert");
            if (!string.IsNullOrEmpty(generatedProgramLines))
            {
                generatedLines.AddRange(generatedProgramLines.Split('\n'));
            }

            generatedLines.Add("            ENDCG");
            generatedLines.Add("        }");
            generatedLines.Add("    }");
            generatedLines.Add("}");
            return (Join(originalLines), Join(generatedLines));
        }

        private static LockedSourceProperty Prop(string name, ShaderPropertyType type)
        {
            return new LockedSourceProperty(name, type, false, false, 0f);
        }

        private static IReadOnlyList<LockedSourceProperty> BaseProperties()
        {
            return new[]
            {
                Prop("_Color", ShaderPropertyType.Color),
                Prop("_Lock", ShaderPropertyType.Float),
                new LockedSourceProperty("_P", ShaderPropertyType.Float, false, false, 0.5f),
                Prop("_Tex", ShaderPropertyType.Texture),
                Prop("_V", ShaderPropertyType.Vector),
                Prop("_I", ShaderPropertyType.Float),
                Prop("_Cull", ShaderPropertyType.Float),
                Prop("_Unused", ShaderPropertyType.Float)
            };
        }

        private static LockedSourceAlignmentResult Align(
            string original,
            string generated,
            IReadOnlyList<LockedSourceProperty> properties)
        {
            return PoiyomiLockedSourceAlignment.Align(original, generated, properties);
        }

        private static int LineOf(string text, string needle)
        {
            var index = text.IndexOf(needle, System.StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "fixture needle missing");
            var number = 1;
            for (var i = 0; i < index; i++)
            {
                if (text[i] == '\n')
                {
                    number++;
                }
            }

            return number;
        }

        private static bool SameFacts(AlignedLockedSource left, AlignedLockedSource right)
        {
            if (!string.Equals(left.Suffix, right.Suffix, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (!string.Equals(left.SurvivingText, right.SurvivingText, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (!SamePairs(left.States, right.States))
            {
                return false;
            }

            if (!SamePairs(left.RenamedNames, right.RenamedNames))
            {
                return false;
            }

            if (!SamePairs(left.BakedLiterals, right.BakedLiterals))
            {
                return false;
            }

            if (!SamePairs(left.EnumValues, right.EnumValues))
            {
                return false;
            }

            if (!SamePairs(left.BracketReads, right.BracketReads))
            {
                return false;
            }

            if (left.DefineRuns.Count != right.DefineRuns.Count)
            {
                return false;
            }

            for (var i = 0; i < left.DefineRuns.Count; i++)
            {
                if (!left.DefineRuns[i].SequenceEqual(right.DefineRuns[i]))
                {
                    return false;
                }
            }

            if (left.Blocks.Count != right.Blocks.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Blocks.Count; i++)
            {
                if (left.Blocks[i].Kind != right.Blocks[i].Kind
                    || left.Blocks[i].Outcome != right.Blocks[i].Outcome
                    || left.Blocks[i].OriginalLine != right.Blocks[i].OriginalLine
                    || !string.Equals(
                        left.Blocks[i].ConditionOrKeyword,
                        right.Blocks[i].ConditionOrKeyword,
                        System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SamePairs<T>(
            IReadOnlyDictionary<string, T> left,
            IReadOnlyDictionary<string, T> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var value))
                {
                    return false;
                }

                if (!EqualityComparer<T>.Default.Equals(pair.Value, value))
                {
                    return false;
                }
            }

            return true;
        }

        [Test]
        public void UnchangedTextWithDefinesAlignsAsLive()
        {
            var (original, generated) = BasePair();
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            var source = result.Source;
            Assert.That(source.Era, Is.EqualTo(PoiyomiLockEra.Bundled9364));
            Assert.That(source.Suffix, Is.Null);
            Assert.That(source.States["_P"], Is.EqualTo(LockedPropertyState.Live));
            Assert.That(source.States["_V"], Is.EqualTo(LockedPropertyState.Live));
            Assert.That(source.States["_I"], Is.EqualTo(LockedPropertyState.Live));
            Assert.That(source.States["_Tex"], Is.EqualTo(LockedPropertyState.Live));
            Assert.That(source.States["_Tex_ST"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(source.States["_Tex_TexelSize"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(source.States["_Unused"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(source.RenamedNames, Is.Empty);
            Assert.That(source.BakedLiterals, Is.Empty);
            Assert.That(source.EnumValues, Is.Empty);
            Assert.That(source.BracketReads["_Cull"], Is.EqualTo("_Cull"));
            Assert.That(source.Blocks, Is.Empty);
            Assert.That(source.DefineRuns.Count, Is.EqualTo(1));
            Assert.That(source.DefineRuns[0], Is.EqualTo(new[] { "OPTIMIZER_ENABLED" }));
            Assert.That(source.SurvivingText, Does.Contain("o.Albedo = _P;"));
            Assert.That(source.SurvivingText, Does.Not.Contain("shader_feature"));
            Assert.That(source.SurvivingText, Does.Not.Contain("// o.Albedo"));
        }

        [Test]
        public void BakedFloatRecordsTheLiteral()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = 0.5;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_P"], Is.EqualTo("0.5"));
            Assert.That(result.Source.States["_V"], Is.EqualTo(LockedPropertyState.Live));
        }

        [Test]
        public void BakedIntRecordsTheF0Literal()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("o.Alpha = _I;", "o.Alpha = 3;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_I"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_I"], Is.EqualTo("3"));
        }

        [Test]
        public void BakedVectorRecordsTheFloat4Literal()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("o.Albedo += _V;", "o.Albedo += float4(1,2,0.5,0);");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_V"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_V"], Is.EqualTo("float4(1,2,0.5,0)"));
        }

        [Test]
        public void DeveloperModeCommentBeforeALiteralIsAccepted()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = /*_P*/0.5;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_P"], Is.EqualTo("0.5"));
        }

        [Test]
        public void TwoDifferentLiteralsForOnePropertyRefuse()
        {
            var (original, generated) = BasePair();
            original = original.Replace("o.Alpha = _I;", "o.Alpha = _P;");
            generated = generated.Replace("o.Alpha = _I;", "o.Alpha = _P;");
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = 0.5;");
            generated = generated.Replace("o.Alpha = _P;", "o.Alpha = 0.75;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(TwoLiteralsDetail));
        }

        [Test]
        public void MixedLiveAndBakedUsesRefuse()
        {
            var (original, generated) = BasePair();
            original = original.Replace("o.Alpha = _I;", "o.Alpha = _P;");
            generated = generated.Replace("o.Alpha = _I;", "o.Alpha = _P;");
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = 0.5;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(MixedUsesDetail));
        }

        [Test]
        public void DeclarationLineKeepsTheIdentifierWhenBaked()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = 0.5;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.SurvivingText, Does.Contain("float _P;"));
        }

        [Test]
        public void RenameOnEveryLineIncludingStringsAligns()
        {
            var (original, generated) = SmallPair(
                "        _Body (\"_Body info\", Float) = 0.5",
                "        ColorMask [_Body]",
                "        float _Body;\n        o.Albedo = _Body;",
                ("_Body", "_Body_Mat"));
            var result = Align(original, generated, new[] { Prop("_Body", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Mat"));
            Assert.That(result.Source.States["_Body"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_Body"], Is.EqualTo("_Body_Mat"));
            Assert.That(result.Source.BracketReads["_Body"], Is.EqualTo("_Body_Mat"));
        }

        [Test]
        public void SuffixWithAnUnderscoreIsFoundByIntersection()
        {
            var (original, generated) = SmallPair(
                "        _Hi (\"H\", Float) = 0\n        _Hi_Tex (\"HT\", 2D) = \"white\" {}",
                null,
                "        sampler2D _Hi_Tex;\n        float4 _Hi_Tex_ST;"
                    + "\n        o.Albedo *= tex2D(_Hi_Tex, IN.uv).rgb * _Hi_Tex_ST;",
                ("_Hi_Tex", "_Hi_Tex_A_B"));
            var properties = new[]
            {
                Prop("_Hi", ShaderPropertyType.Float),
                Prop("_Hi_Tex", ShaderPropertyType.Texture)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_A_B"));
            Assert.That(result.Source.States["_Hi_Tex"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.States["_Hi_Tex_ST"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.States["_Hi"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(result.Source.RenamedNames["_Hi_Tex"], Is.EqualTo("_Hi_Tex_A_B"));
            Assert.That(result.Source.RenamedNames["_Hi_Tex_ST"], Is.EqualTo("_Hi_Tex_A_B_ST"));
        }

        [Test]
        public void CollateralRenameAlignsOnlyInTheBundledEra()
        {
            var (original, generated) = SmallPair(
                "        _P (\"P\", Float) = 0.5",
                null,
                "        float _P;\n        o.Albedo = _P;\n        o.Emission = _X_P;",
                ("o.Emission = _X_P;", "o.Emission = _X_P_S;"),
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""));
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var bundled = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Bundled9364);
            Assert.That(bundled.Succeeded, Is.True, bundled.RefusalDetail ?? "refused");
            Assert.That(bundled.Source.States["_P"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(bundled.Source.RenamedNames["_P"], Is.EqualTo("_P_S"));
            var thry274 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Thry274To2742);
            Assert.That(thry274.Succeeded, Is.False);
            Assert.That(thry274.Source, Is.Null);
        }

        [Test]
        public void RenamedTextureRenamesScaleOffsetBeforeTheSuffix()
        {
            var (original, generated) = SmallPair(
                "        _T (\"T\", 2D) = \"white\" {}",
                null,
                "        sampler2D _T;\n        float4 _T_ST;\n        o.Albedo *= _T_ST;",
                ("_T", "_T_S"));
            var result = Align(original, generated, new[] { Prop("_T", ShaderPropertyType.Texture) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.RenamedNames["_T"], Is.EqualTo("_T_S"));
            Assert.That(result.Source.RenamedNames["_T_ST"], Is.EqualTo("_T_S_ST"));
            Assert.That(result.Source.States["_T_ST"], Is.EqualTo(LockedPropertyState.Renamed));
        }

        [Test]
        public void SamplerRenameAlignsOnlyIn2743()
        {
            var (original, generated) = SmallPair(
                "        _Tex (\"T\", 2D) = \"white\" {}",
                null,
                "        sampler2D sampler_Tex;\n        float _X_Tex;"
                    + "\n        o.Albedo *= tex2D(_Tex, IN.uv).rgb;",
                ("sampler2D sampler_Tex;", "sampler2D sampler_Tex_S;"),
                ("tex2D(_Tex,", "tex2D(_Tex_S,"),
                ("_Tex (\"T\"", "_Tex_S (\"T\""));
            var properties = new[] { Prop("_Tex", ShaderPropertyType.Texture) };
            var thry2743 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Thry2743);
            Assert.That(thry2743.Succeeded, Is.True, thry2743.RefusalDetail ?? "refused");
            Assert.That(thry2743.Source.RenamedNames["_Tex"], Is.EqualTo("_Tex_S"));
            var thry274 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Thry274To2742);
            Assert.That(thry274.Succeeded, Is.False);
            var bundled = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Bundled9364);
            Assert.That(bundled.Succeeded, Is.False);
        }

        [Test]
        public void NegativeWrapAlignsOnlyInThe274Eras()
        {
            var (original, generated) = SmallPair(
                "        _P (\"P\", Float) = 0.5",
                null,
                "        float _P;\n        o.Albedo = -_P;",
                ("o.Albedo = -_P;", "o.Albedo = -(-0.5);"));
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var thry274 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Thry274To2742);
            Assert.That(thry274.Succeeded, Is.True, thry274.RefusalDetail ?? "refused");
            Assert.That(thry274.Source.BakedLiterals["_P"], Is.EqualTo("-0.5"));
            var thry2743 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Thry2743);
            Assert.That(thry2743.Succeeded, Is.True, thry2743.RefusalDetail ?? "refused");
            var bundled = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, properties, PoiyomiLockEra.Bundled9364);
            Assert.That(bundled.Succeeded, Is.False);
        }

        [Test]
        public void CullBracketEnumTextRecordsTheValue()
        {
            var (original, generated) = SmallPair(
                "        _Cull (\"Cull\", Float) = 0",
                "        Cull [_Cull]",
                "        float _Cull;\n        o.Test = _Cull;",
                ("Cull [_Cull]", "Cull Front"),
                ("o.Test = _Cull;", "o.Test = 1.0;"));
            var result = Align(original, generated, new[] { Prop("_Cull", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.EnumValues["_Cull"], Is.EqualTo(1));
            Assert.That(result.Source.BracketReads.ContainsKey("_Cull"), Is.False);
            Assert.That(result.Source.States["_Cull"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_Cull"], Is.EqualTo("1.0"));
        }

        [Test]
        public void ShaderNameHoleAcceptsAnyId()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("Custom/Test/d4", "Custom/Test/any-id-42");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
        }

        [Test]
        public void WhitespaceChangeInsideALineRefuses()
        {
            var (original, generated) = BasePair();
            // Guard: the unmutated pair aligns, so this fixture reaches the
            // exact line compare.
            Assert.That(Align(original, generated, BaseProperties()).Succeeded, Is.True);
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo =  _P;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(LineMismatchDetail));
            Assert.That(result.OriginalLine, Is.EqualTo(LineOf(original, "o.Albedo = _P;")));
        }

        [Test]
        public void TrailingWhitespaceChangeRefuses()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("Cull [_Cull]", "Cull [_Cull] ");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RefusalDetail, Is.EqualTo(LineMismatchDetail));
            Assert.That(result.OriginalLine, Is.EqualTo(LineOf(original, "Cull [_Cull]")));
        }

        [Test]
        public void WhitespaceOnlyLinesAreDroppedOnBothSides()
        {
            var (original, generated) = BasePair();
            original = original.Replace("        Cull [_Cull]", "    \n        Cull [_Cull]");
            generated = generated.Replace("        ENDCG", "        ENDCG\n   ");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
        }

        [Test]
        public void UnconsumedGeneratedLineRefuses()
        {
            var (original, generated) = BasePair();
            generated += "\nleftover();";
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(UnconsumedLineDetail));
        }

        [Test]
        public void InlinedMarkerRefuses()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace("        Cull [_Cull]", "        Cull [_Cull]\n        // [Inlined]");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(InlinedMarkerDetail));
        }

        [Test]
        public void NonUnityIncludeRefuses()
        {
            var (original, generated) = BasePair();
            original = original.Replace("    SubShader", "    #include \"Custom/Extra.cginc\"\n    SubShader");
            generated = generated.Replace("    SubShader", "    #include \"Custom/Extra.cginc\"\n    SubShader");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(IncludeDetail));
        }

        [Test]
        public void UnityBranchDeletionThatEmptiesALineDropsIt()
        {
            var (original, generated) = BasePair();
            original = original.Replace(
                "                o.Albedo = _P;",
                "                UNITY_BRANCH\n                o.Albedo = _P;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
        }

        [Test]
        public void PropertyWithNoSurvivingUseRecordsUnused()
        {
            var (original, generated) = BasePair();
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_Unused"], Is.EqualTo(LockedPropertyState.Unused));
        }

        [Test]
        public void AmbiguousSuffixIntersectionRefuses()
        {
            var (original, generated) = SmallPair(
                "        _Hi (\"H\", Float) = 0\n        _Hi_Tex (\"HT\", Float) = 0"
                    + "\n        _Zed (\"Z\", Float) = 0",
                null,
                "        float _Hi;\n        o.A = _Hi;\n        float _Zed;\n        o.B = _Zed;",
                ("_Hi (\"H\"", "_Hi_Tex_C (\"H\""),
                ("float _Hi;", "float _Hi_Tex_C;"),
                ("o.A = _Hi;", "o.A = _Hi_Tex_C;"),
                ("_Zed (\"Z\"", "_Zed_Tex_C (\"Z\""),
                ("float _Zed;", "float _Zed_Tex_C;"),
                ("o.B = _Zed;", "o.B = _Zed_Tex_C;"));
            var properties = new[]
            {
                Prop("_Hi", ShaderPropertyType.Float),
                Prop("_Hi_Tex", ShaderPropertyType.Float),
                Prop("_Zed", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(SuffixAmbiguousDetail));
        }

        [Test]
        public void GammaAttributeIsReadFromThePropertiesLine()
        {
            var original = BaseOriginal().Replace(
                "        _P (\"P\", Float) = 0.5",
                "        [Gamma] _G (\"G\", Float) = 0.5\n        _P (\"P\", Float) = 0.5");
            var names = PoiyomiLockedSourceAlignment.GammaPropertyNames(original);
            Assert.That(names, Is.EqualTo(new[] { "_G" }));
        }

        [Test]
        public void ErasThatAlignMustAgree()
        {
            var (original, generated) = BasePair();
            var bundled = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, BaseProperties(), PoiyomiLockEra.Bundled9364);
            var thry274 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, BaseProperties(), PoiyomiLockEra.Thry274To2742);
            var thry2743 = PoiyomiLockedSourceAlignment.AlignForEra(
                original, generated, BaseProperties(), PoiyomiLockEra.Thry2743);
            Assert.That(bundled.Succeeded, Is.True, bundled.RefusalDetail ?? "refused");
            Assert.That(thry274.Succeeded, Is.True, thry274.RefusalDetail ?? "refused");
            Assert.That(thry2743.Succeeded, Is.True, thry2743.RefusalDetail ?? "refused");
            Assert.That(SameFacts(bundled.Source, thry274.Source), Is.True);
            Assert.That(SameFacts(bundled.Source, thry2743.Source), Is.True);
            var all = Align(original, generated, BaseProperties());
            Assert.That(all.Succeeded, Is.True, all.RefusalDetail ?? "refused");
            Assert.That(SameFacts(all.Source, bundled.Source), Is.True);
        }

        [Test]
        public void NoAligningEraRefuses()
        {
            var (original, generated) = BasePair();
            original = original.Replace("o.Alpha = _I;", "o.Alpha = _P;");
            generated = generated.Replace("o.Alpha = _I;", "o.Alpha = _P;");
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = 0.5;");
            generated = generated.Replace("o.Alpha = _P;", "o.Alpha = 0.75;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(TwoLiteralsDetail));
        }

        [Test]
        public void TruncatedGeneratedTextRefusesWithoutThrowing()
        {
            var (original, generated) = BasePair();
            generated = string.Join("\n", generated.Split('\n').Take(20));
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(LineMismatchDetail));
        }

        [Test]
        public void EnumTextWithoutASurvivingUseRecordsUnusedState()
        {
            var (original, generated) = SmallPair(
                "        _Cull (\"Cull\", Float) = 0",
                "        Cull [_Cull]",
                "        o.Albedo = 0.5;",
                ("Cull [_Cull]", "Cull Back"));
            var result = Align(original, generated, new[] { Prop("_Cull", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.EnumValues["_Cull"], Is.EqualTo(2));
            Assert.That(result.Source.States["_Cull"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(result.Source.BracketReads.ContainsKey("_Cull"), Is.False);
        }

        [Test]
        public void DigitSuffixWithoutSeparatorRefuses()
        {
            var (original, generated) = SmallPair(
                "        _P (\"P\", Float) = 0.5",
                null,
                "        float _P;\n        o.Albedo = _P;",
                ("float _P;", "float _P9;"),
                ("o.Albedo = _P;", "o.Albedo = _P9;"),
                ("_P (\"P\"", "_P9 (\"P\""));
            var properties = new[] { Prop("_P", ShaderPropertyType.Float) };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(SuffixAmbiguousDetail));
        }

        [Test]
        public void KeptIfexBlockIsRecordedKept()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //endex\n            o.Alpha = 1;",
                "            o.Albedo = _A;\n            o.Alpha = 1;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(1));
            var block = result.Source.Blocks[0];
            Assert.That(block.Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(block.Outcome, Is.EqualTo(LockedBlockOutcome.Kept));
            Assert.That(block.ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(block.OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
            Assert.That(result.Source.States["_A"], Is.EqualTo(LockedPropertyState.Live));
        }

        [Test]
        public void RemovedIfexBlockIsRecordedRemoved()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //endex\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(1));
            var block = result.Source.Blocks[0];
            Assert.That(block.Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(block.Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(block.ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(block.OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
            Assert.That(result.Source.States["_A"], Is.EqualTo(LockedPropertyState.Unused));
        }

        [Test]
        public void IfexBlockWithOnlyCommentsIsUnobservable()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            // just a note\n            //endex\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(1));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Unobservable));
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_A==0"));
        }

        [Test]
        public void NestedBlockInsideARemovedBlockIsNotEvaluated()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0\n        _B (\"B\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //ifex _B==0"
                    + "\n            o.Albedo = _B;\n            //endex\n            //endex\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var properties = new[]
            {
                Prop("_A", ShaderPropertyType.Float),
                Prop("_B", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(result.Source.Blocks[0].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("_B==0"));
            Assert.That(result.Source.Blocks[1].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _B==0")));
            Assert.That(result.Source.States["_A"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(result.Source.States["_B"], Is.EqualTo(LockedPropertyState.Unused));
        }

        [Test]
        public void BlockWhoseBodyRepeatsTheFollowingTextGrowsTheLookahead()
        {
            // The body line equals the next 10 surviving lines and then the
            // text differs. A fixed 8 line window ties or picks Kept. The
            // grown window finds the difference and records Removed
            // (A3.7, Review Focus 2).
            var repeat = "            o.Albedo = _A;";
            var program = new List<string> { "            //ifex _A==0", repeat, "            //endex" };
            var generatedProgram = new List<string>();
            for (var i = 0; i < 10; i++)
            {
                program.Add(repeat);
                generatedProgram.Add(repeat);
            }

            program.Add("            o.Alpha = 1;");
            generatedProgram.Add("            o.Alpha = 1;");
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                Join(program),
                Join(generatedProgram));
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(1));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.States["_A"], Is.EqualTo(LockedPropertyState.Live));
        }

        [Test]
        public void TieAtTheEndOfTheFileRefuses()
        {
            // Both outcomes consume the same surviving lines and both stay
            // consistent until the text ends. The outcome never follows
            // from the text, so the aligner refuses (A3.7). The texts end
            // inside the program on purpose, so no line after the repeated
            // lines breaks the tie.
            var repeat = "            o.Albedo = _A;";
            var head = Join(new[]
            {
                "Shader \"Custom/Small\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 0",
                "        _A (\"A\", Float) = 0",
                "    }",
                "    SubShader",
                "    {",
                "        Pass",
                "        {",
                "            CGPROGRAM",
                "            //ifex _A==0",
                repeat,
                "            //endex",
                repeat,
                repeat,
                repeat
            });
            var generatedHead = Join(new[]
            {
                "Shader \"Hidden/Locked/Custom/Small/k1\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 1",
                "        _A (\"A\", Float) = 0",
                "    }",
                "    SubShader",
                "    {",
                "        Pass",
                "        {",
                "            CGPROGRAM",
                " #define OPTIMIZER_ENABLED ",
                repeat,
                repeat,
                repeat,
                repeat
            });
            var result = Align(head, generatedHead, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockTieDetail));
        }

        [Test]
        public void RemovedIfdefKeepsItsElseBody()
        {
            var (original, generated) = ProgramPair(
                "        _P (\"P\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            #ifndef _K\n            o.Albedo = _P;\n            #else"
                    + "\n            //ifex _A==0\n            o.Alpha = _A;\n            //endex"
                    + "\n            o.Albedo = _A;\n            #endif\n            o.Alpha = 1;",
                "            o.Alpha = _A;\n            o.Albedo = _A;\n            o.Alpha = 1;");
            var properties = new[]
            {
                Prop("_P", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifndef));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_K"));
            Assert.That(result.Source.Blocks[1].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.Kept));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Unused));
            Assert.That(result.Source.States["_A"], Is.EqualTo(LockedPropertyState.Live));
        }

        [Test]
        public void IfdefWithAnAmpersandIsNeverStripped()
        {
            var program = "            #ifdef _K1 & _K2\n            o.Albedo = _A;\n            #endif\n            o.Alpha = 1;";
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                program,
                program);
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks, Is.Empty);
            Assert.That(result.Source.SurvivingText, Does.Contain("#ifdef _K1 & _K2"));
            Assert.That(result.Source.States["_A"], Is.EqualTo(LockedPropertyState.Live));
        }

        [Test]
        public void DefineRunIsRecordedPerProgramBlock()
        {
            var original = Join(new[]
            {
                "Shader \"Custom/Small\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 0",
                "        _A (\"A\", Float) = 0",
                "    }",
                "    SubShader",
                "    {",
                "        Tags { \"RenderType\" = \"Opaque\" }",
                "        Pass",
                "        {",
                "            CGPROGRAM",
                "            #pragma vertex vert",
                "            o.Albedo = _A;",
                "            ENDCG",
                "        }",
                "        Pass",
                "        {",
                "            CGPROGRAM",
                "            #pragma vertex vert",
                "            o.Alpha = _A;",
                "            ENDCG",
                "        }",
                "    }",
                "}"
            });
            var generated = Join(new[]
            {
                "Shader \"Hidden/Locked/Custom/Small/k1\"",
                "{",
                "    Properties",
                "    {",
                "        [ThryShaderOptimizerLockButton] _Lock (\"Lock\", Float) = 1",
                "        _A (\"A\", Float) = 0",
                "    }",
                "    SubShader",
                "    {",
                "        Tags { \"RenderType\" = \"Opaque\" }",
                "        Pass",
                "        {",
                "            CGPROGRAM",
                " #define _FEATURE_ON ",
                " #define OPTIMIZER_ENABLED ",
                "            #pragma vertex vert",
                "            o.Albedo = _A;",
                "            ENDCG",
                "        }",
                "        Pass",
                "        {",
                "            CGPROGRAM",
                " #define OPTIMIZER_ENABLED ",
                "            #pragma vertex vert",
                "            o.Alpha = _A;",
                "            ENDCG",
                "        }",
                "    }",
                "}"
            });
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.DefineRuns.Count, Is.EqualTo(2));
            Assert.That(result.Source.DefineRuns[0], Is.EqualTo(new[] { "_FEATURE_ON", "OPTIMIZER_ENABLED" }));
            Assert.That(result.Source.DefineRuns[1], Is.EqualTo(new[] { "OPTIMIZER_ENABLED" }));
        }

        [Test]
        public void DuplicatePropertyLineMustBeTheRenamedCopy()
        {
            var propertiesLines = "        _D (\"D\", Float) = 0.5\n        _E (\"E\", Float) = 0";
            var (original, generated) = SmallPair(
                propertiesLines,
                null,
                "        float _D;\n        o.Albedo = _D;",
                ("o.Albedo = _D;", "o.Albedo = _D_S;"),
                ("float _D;", "float _D_S;"),
                ("        _D (\"D\", Float) = 0.5\n        _E",
                    "        _D (\"D\", Float) = 0.5\n        _D_S (\"D\", Float) = 0.5\n        _E"));
            var properties = new[]
            {
                Prop("_D", ShaderPropertyType.Float),
                Prop("_E", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_D"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_D"], Is.EqualTo("_D_S"));

            // Any other line after the plain line refuses. The renamed copy
            // must repeat the plain line byte for byte under the new name.
            var (mutatedOriginal, mutatedGenerated) = SmallPair(
                propertiesLines,
                null,
                "        float _D;\n        o.Albedo = _D;",
                ("o.Albedo = _D;", "o.Albedo = _D_S;"),
                ("float _D;", "float _D_S;"),
                ("        _D (\"D\", Float) = 0.5\n        _E",
                    "        _D (\"D\", Float) = 0.5\n        _D_S (\"D\", Float) = 0.9\n        _E"));
            var mutated = Align(mutatedOriginal, mutatedGenerated, properties);
            Assert.That(mutated.Succeeded, Is.False);
            Assert.That(mutated.Source, Is.Null);
            Assert.That(mutated.RefusalDetail, Is.EqualTo(LineMismatchDetail));
        }

        [Test]
        public void RangePropertyInTheDuplicateSetKeepsOnlyTheRenamedLine()
        {
            var (original, generated) = SmallPair(
                "        _R (\"R\", Range(0, 1)) = 0.5",
                null,
                "        float _R;\n        o.Albedo = _R;",
                ("o.Albedo = _R;", "o.Albedo = _R_S;"),
                ("float _R;", "float _R_S;"),
                ("_R (\"R\"", "_R_S (\"R\""));
            var result = Align(original, generated, new[] { Prop("_R", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_R"], Is.EqualTo(LockedPropertyState.Renamed));

            // A Range definition never keeps the plain line, so a plain
            // line followed by the renamed copy refuses.
            var (duplicatedOriginal, duplicatedGenerated) = SmallPair(
                "        _R (\"R\", Range(0, 1)) = 0.5",
                null,
                "        float _R;\n        o.Albedo = _R;",
                ("o.Albedo = _R;", "o.Albedo = _R_S;"),
                ("float _R;", "float _R_S;"),
                ("        _R (\"R\", Range(0, 1)) = 0.5",
                    "        _R (\"R\", Range(0, 1)) = 0.5\n        _R_S (\"R\", Range(0, 1)) = 0.5"));
            var duplicated = Align(duplicatedOriginal, duplicatedGenerated, new[] { Prop("_R", ShaderPropertyType.Float) });
            Assert.That(duplicated.Succeeded, Is.False);
            Assert.That(duplicated.Source, Is.Null);
        }

        // --- Falsifier 7: Properties-region rename ---
        // An implementation that reads the suffix only from program
        // identifiers and brackets misses the renamed Properties name and
        // refuses the fixture. The renamed Properties line and the renamed
        // program uses share one suffix, so the intersection finds it. The
        // guard aligns the same texts without the rename and proves the
        // fixture reaches the suffix search.

        [Test]
        public void PropertiesRegionRenameWithSurvivingProgramUsesFindsTheSuffix()
        {
            var (guardOriginal, guardGenerated) = SmallPair(
                "        _Q (\"Q\", Float) = 0",
                null,
                "        float _Q;\n        o.Albedo = _Q;\n        o.Emission = _Q;");
            var guard = Align(guardOriginal, guardGenerated, new[] { Prop("_Q", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");
            Assert.That(guard.Source.Suffix, Is.Null);
            Assert.That(guard.Source.States["_Q"], Is.EqualTo(LockedPropertyState.Live));

            var (original, generated) = SmallPair(
                "        _Q (\"Q\", Float) = 0",
                null,
                "        float _Q;\n        o.Albedo = _Q;\n        o.Emission = _Q;",
                ("_Q (\"Q\"", "_Q_Mat (\"Q\""),
                ("float _Q;", "float _Q_Mat;"),
                ("o.Albedo = _Q;", "o.Albedo = _Q_Mat;"),
                ("o.Emission = _Q;", "o.Emission = _Q_Mat;"));
            var result = Align(original, generated, new[] { Prop("_Q", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Mat"));
            Assert.That(result.Source.States["_Q"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_Q"], Is.EqualTo("_Q_Mat"));
        }

        [Test]
        public void ProgramBlockWithoutOptimizerEnabledRefuses()
        {
            var (original, generated) = BasePair();
            generated = generated.Replace(" #define OPTIMIZER_ENABLED \n", string.Empty);
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(OptimizerMissingDetail));
        }
    }
}
