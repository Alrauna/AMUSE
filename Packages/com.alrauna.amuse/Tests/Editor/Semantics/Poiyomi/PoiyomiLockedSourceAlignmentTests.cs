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
    internal sealed class PoiyomiLockedSourceAlignmentTests
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

        private const string OutsideSetDetail =
            "The original text holds a construct outside the accepted set.";

        private const string MissingPassDetail =
            "The original text holds no pass.";

        private const string EnumWithoutBakeDetail =
            "A render state enum text names a property that is not baked.";

        private const string BlockAlignmentDetail =
            "Neither outcome of a block matches the generated text.";

        private const string BlockUnclosedDetail =
            "A block in the original text never closes.";

        // --- Falsifier locked-source-alignment 1: an implementation that folds inner whitespace ---
        // An implementation that trims or folds runs of spaces inside a line
        // before the compare aligns the mutated pair and fails the refusal
        // assertion (A3.2, Review Focus 1). The guard before the mutation
        // proves the fixture reaches the line matcher.

        // --- Falsifier locked-source-alignment 2: an implementation that keeps the last literal ---
        // An implementation that overwrites the recorded literal on every
        // use aligns the two-literal pair and fails the refusal assertion.
        // The float bake test proves the hole path records one literal.

        // --- Falsifier locked-source-alignment 3: an implementation that decides a state per use ---
        // An implementation that lets one use stay live while another use
        // bakes aligns the mixed pair and fails the refusal assertion. The
        // float bake test proves the hole path reaches the state decision.

        // --- Falsifier locked-source-alignment 4: an implementation that takes the first split ---
        // An implementation that reads the suffix from the first candidate
        // of the first artifact misses the disagreement between artifacts
        // and aligns the ambiguous pair. The underscore intersection test
        // proves the intersection path finds one suffix.

        // --- Falsifier locked-source-alignment 5: an implementation that reuses one rename regex ---
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
                    || left.Blocks[i].ZeroAndZero != right.Blocks[i].ZeroAndZero
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

        // --- Falsifier locked-source-alignment 23: storage class before the type keyword ---
        // An implementation that reads only the first token misses the
        // declaration on a storage class line. The aligner then opens a
        // hole for the declared name, the hole records an identifier use
        // beside the baked literal uses, and a genuine lock refuses. The
        // no-op guard is the baked use in the program body, so the
        // declaration line is the only remaining identifier use.
        [Test]
        public void StorageClassDeclarationLineKeepsTheIdentifierWhenBaked()
        {
            var (original, generated) = BasePair();
            original = original.Replace("float _P;", "uniform float _P;");
            generated = generated.Replace("float _P;", "uniform float _P;");
            generated = generated.Replace("o.Albedo = _P;", "o.Albedo = 0.5;");
            var result = Align(original, generated, BaseProperties());
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_P"], Is.EqualTo("0.5"));
            Assert.That(result.Source.SurvivingText, Does.Contain("uniform float _P;"));
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

        // Characterization pins for five aligner refusal sentences. Each
        // test holds one near pair that aligns as its no-op guard and one
        // pair that refuses with the pinned sentence. They pin existing
        // behavior. The spec stage 1 test list asks for one refusal test
        // per retained sentence. The guard pairs passed on their first
        // run on 2026-10-27.

        // The fixture of this pin needed two corrections before it
        // reached the sentence. The first shape refused in the suffix
        // search, the second shape let the removed trial adopt the walk.
        // Both refusals were existing behavior too. The final shape
        // reached the block sentence and passed on 2026-10-27.
        [Test]
        public void ABlockWhoseBothTrialsDieRefusesWithTheBlockAlignmentSentence()
        {
            var (guardOriginal, guardGenerated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //endex\n            o.Specular = _A;",
                "            o.Albedo = _A;\n            o.Specular = _A;");
            var guard = Align(guardOriginal, guardGenerated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");

            // The generated text after the block kills both outcomes. The
            // kept trial needs the specular line as the second generated
            // line. The removed trial needs it as the first one. The
            // fixture repeats the albedo line, so neither trial finds its
            // line and the lookahead kills both. Both generated words
            // also stand in the original, so the suffix search stays out
            // of the way.
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //endex\n            o.Specular = _A;",
                "            o.Albedo = _A;\n            o.Albedo = _A;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockAlignmentDetail));
            Assert.That(result.OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
        }

        [Test]
        public void AnUnclosedIfexBlockRefusesWithTheBlockUnclosedSentence()
        {
            var (guardOriginal, guardGenerated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //endex",
                "            o.Albedo = _A;");
            var guard = Align(guardOriginal, guardGenerated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");

            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;",
                "            o.Albedo = _A;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockUnclosedDetail));
            Assert.That(result.OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
        }

        [Test]
        public void AnOriginalWithoutAPassRefusesWithTheMissingPassSentence()
        {
            var (guardOriginal, guardGenerated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            o.Albedo = _A;",
                "            o.Albedo = _A;");
            var guard = Align(guardOriginal, guardGenerated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");

            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            o.Albedo = _A;",
                "            o.Albedo = _A;");
            original = original.Replace("        Pass\n", string.Empty);
            generated = generated.Replace("        Pass\n", string.Empty);
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(MissingPassDetail));
        }

        [Test]
        public void AGrabPassLineRefusesWithTheOutsideSetSentence()
        {
            var (guardOriginal, guardGenerated) = SmallPair(
                "        _A (\"A\", Float) = 0",
                null,
                "            o.Albedo = _A;");
            var guard = Align(guardOriginal, guardGenerated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");

            var (original, generated) = SmallPair(
                "        _A (\"A\", Float) = 0",
                "        GrabPass\n        {\n        }",
                "            o.Albedo = _A;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(OutsideSetDetail));
            Assert.That(result.OriginalLine, Is.EqualTo(LineOf(original, "GrabPass")));
        }

        [Test]
        public void EnumTextForAPropertyWithASurvivingUseRefusesWithTheEnumSentence()
        {
            // The guard holds the same enum text for a property with no
            // surviving use. The enum text alone is fine (A5.6), so the
            // guard aligns and proves the fixture reaches the enum check.
            var (guardOriginal, guardGenerated) = SmallPair(
                "        _C (\"C\", Float) = 0",
                "        Cull [_C]",
                "        float _C;",
                ("Cull [_C]", "Cull Front"));
            var guard = Align(guardOriginal, guardGenerated, new[] { Prop("_C", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");

            // The live program use stays unbaked. The enum text then
            // names a property that is not baked, and the walk refuses.
            var (original, generated) = SmallPair(
                "        _C (\"C\", Float) = 0",
                "        Cull [_C]",
                "        float _C;\n        o.Emission = _C;",
                ("Cull [_C]", "Cull Front"));
            var result = Align(original, generated, new[] { Prop("_C", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(EnumWithoutBakeDetail));
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

        // --- Falsifier 6: a run scanner that starts runs only at letters and underscores. ---
        // A scanner that starts a run only at a letter or an underscore
        // splits the literal 0x7ffffff into the artifact x7ffffff. The
        // original never holds that form, so the artifact has no declared
        // name inside and the empty candidate set refuses before the walk
        // (Y5 of the bundled locked original admission design). The guard
        // pair without the literal aligns before and after the fix.

        [Test]
        public void DigitLedHexLiteralDoesNotBlockTheSuffix()
        {
            var guardProperties = "        _P (\"P\", Float) = 0.5\n        _F (\"F\", Float) = 0";
            var (guardOriginal, guardGenerated) = SmallPair(
                guardProperties,
                null,
                "        float _P;\n        #ifdef _F\n        o.Emission = _F;\n        #endif"
                    + "\n        o.Albedo = _P;",
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""));
            var properties = new[]
            {
                Prop("_P", ShaderPropertyType.Float),
                Prop("_F", ShaderPropertyType.Float)
            };
            var guard = Align(guardOriginal, guardGenerated, properties);
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");
            Assert.That(guard.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(guard.Source.States["_F"], Is.EqualTo(LockedPropertyState.Live));

            var (original, generated) = SmallPair(
                guardProperties,
                null,
                "        float _P;\n        #ifdef _F\n        o.Emission = _F;"
                    + "\n        o.Albedo *= 0x7ffffff;\n        #endif"
                    + "\n        o.Albedo = _P;",
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""));
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_P"], Is.EqualTo("_P_S"));
            Assert.That(result.Source.States["_F"], Is.EqualTo(LockedPropertyState.Live));
        }

        // --- Falsifier 12: a run drop rule that relies on whole-token presence only. ---
        // After the run start widening, the baked literals 0.975 and
        // 1E-06 yield the digit-led runs 0, 975, 1E, and 06. The original
        // holds only 0 as a whole token. A drop rule without the
        // digit-led check keeps 975, 1E, and 06 as artifacts and refuses
        // on the empty intersection before the walk (Y5 of the bundled
        // locked original admission design). The float4 form is the fact
        // 5 print of a small vector component. The guard pairs without
        // the baked values align before and after the fix.

        [Test]
        public void BakedDecimalAndExponentLiteralsDoNotBlockTheSuffix()
        {
            var decimalProperties = "        _P (\"P\", Float) = 0.5\n        _Q (\"Q\", Float) = 0";
            var decimalProgram = "        float _P;\n        float _Q;\n        o.Albedo = _P;\n        o.Alpha = _Q;";
            var decimalGuardEdits = new[]
            {
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\"")
            };
            var decimalEdits = new[]
            {
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""),
                ("o.Alpha = _Q;", "o.Alpha = 0.975;")
            };
            var decimalNames = new[]
            {
                Prop("_P", ShaderPropertyType.Float),
                Prop("_Q", ShaderPropertyType.Float)
            };
            var (guardOriginal, guardGenerated) = SmallPair(decimalProperties, null, decimalProgram, decimalGuardEdits);
            var guard = Align(guardOriginal, guardGenerated, decimalNames);
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");
            Assert.That(guard.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(guard.Source.States["_Q"], Is.EqualTo(LockedPropertyState.Live));

            var (decimalOriginal, decimalGenerated) = SmallPair(decimalProperties, null, decimalProgram, decimalEdits);
            var decimalResult = Align(decimalOriginal, decimalGenerated, decimalNames);
            Assert.That(decimalResult.Succeeded, Is.True, decimalResult.RefusalDetail ?? "refused");
            Assert.That(decimalResult.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(decimalResult.Source.States["_P"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(decimalResult.Source.States["_Q"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(decimalResult.Source.BakedLiterals["_Q"], Is.EqualTo("0.975"));

            var vectorProperties = "        _P (\"P\", Float) = 0.5\n        _V (\"V\", Vector) = (0, 0, 0, 0)";
            var vectorProgram = "        float _P;\n        float4 _V;\n        o.Albedo = _P;\n        o.Albedo += _V;";
            var vectorGuardEdits = new[]
            {
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\"")
            };
            var vectorEdits = new[]
            {
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""),
                ("o.Albedo += _V;", "o.Albedo += float4(0,1E-06,0,0);")
            };
            var vectorNames = new[]
            {
                Prop("_P", ShaderPropertyType.Float),
                Prop("_V", ShaderPropertyType.Vector)
            };
            var (vectorGuardOriginal, vectorGuardGenerated) = SmallPair(vectorProperties, null, vectorProgram, vectorGuardEdits);
            var vectorGuard = Align(vectorGuardOriginal, vectorGuardGenerated, vectorNames);
            Assert.That(vectorGuard.Succeeded, Is.True, vectorGuard.RefusalDetail ?? "refused");
            Assert.That(vectorGuard.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(vectorGuard.Source.States["_V"], Is.EqualTo(LockedPropertyState.Live));

            var (vectorOriginal, vectorGenerated) = SmallPair(vectorProperties, null, vectorProgram, vectorEdits);
            var vectorResult = Align(vectorOriginal, vectorGenerated, vectorNames);
            Assert.That(vectorResult.Succeeded, Is.True, vectorResult.RefusalDetail ?? "refused");
            Assert.That(vectorResult.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(vectorResult.Source.States["_P"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(vectorResult.Source.States["_V"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(vectorResult.Source.BakedLiterals["_V"], Is.EqualTo("float4(0,1E-06,0,0)"));
        }

        // A letter-led artifact the original never holds and that carries
        // no declared name still refuses. Y5 drops only whole-token runs
        // and digit-led runs, so an unknown identifier stays fail closed
        // (Y5 of the bundled locked original admission design). This pins
        // existing behavior and passes on its first possible run.

        [Test]
        public void NonDigitArtifactWithNoDeclaredNameStillRefuses()
        {
            var (original, generated) = SmallPair(
                "        _P (\"P\", Float) = 0.5",
                null,
                "        float _P;\n        o.Albedo = _P;\n        o.Specular = 0.5;",
                ("o.Specular = 0.5;", "o.Specular = Zed;"),
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""));
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

        // --- Falsifier 8: an extent finder that counts directive shaped lines inside block comments. ---
        // The vendor deletes block comments wholesale, so the generated
        // text never shows a commented directive. An extent finder with no
        // comment tracking counts the commented //ifex line, mis-nests the
        // wrapper, and refuses with the unclosed block sentence. The Y4
        // comment tracking drops the span first, so the commented line
        // records nothing and the real nested block records its own
        // outcome.
        [Test]
        public void CommentedDirectiveInsideABlockNeverOpensABlock()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0\n        _B (\"B\", Float) = 0\n        _C (\"C\", Float) = 0",
                "            //ifex _A==0\n            /*\n            //ifex _C==0\n            */"
                    + "\n            //ifex _B==0\n            o.Albedo = _B;\n            //endex\n            //endex"
                    + "\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var properties = new[]
            {
                Prop("_A", ShaderPropertyType.Float),
                Prop("_B", ShaderPropertyType.Float),
                Prop("_C", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(result.Source.Blocks[0].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("_B==0"));
            Assert.That(result.Source.Blocks[1].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _B==0")));
            Assert.That(result.Source.States["_C"], Is.EqualTo(LockedPropertyState.Unused));
        }

        // The no-op guard of Falsifier 8 above. The same wrapper holds the
        // comment span with no commented directive inside. The old count
        // was already right on this shape, so the pair aligns before and
        // after the fix. The guard proves the fixture reaches the extent
        // scan and the block records.
        [Test]
        public void CommentSpanWithoutADirectiveAlignsTheSame()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0\n        _B (\"B\", Float) = 0",
                "            //ifex _A==0\n            /*\n            */"
                    + "\n            //ifex _B==0\n            o.Albedo = _B;\n            //endex\n            //endex"
                    + "\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var properties = new[]
            {
                Prop("_A", ShaderPropertyType.Float),
                Prop("_B", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.True);
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("_B==0"));
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

        // --- Falsifier locked-source-alignment 8: an implementation that never counts the window ---
        // An implementation that walks each candidate to the end of the
        // walk range in one step kills both hypotheses in one round and
        // refuses with the wrong sentence. The bounded window adopts the
        // surviving hypothesis first, and the main walk then refuses at
        // the first line past the window. The guard keeps the text
        // consistent past the window and aligns as Removed.

        [Test]
        public void LookaheadWindowStopsAtEightSurvivingLines()
        {
            var (guardOriginal, guardGenerated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            o.Albedo = _A;\n            //endex"
                    + "\n            o.Albedo = _A;\n            o.Emission = _A;\n            o.Emission = _A;"
                    + "\n            o.Emission = _A;\n            o.Emission = _A;\n            o.Emission = _A;"
                    + "\n            o.Emission = _A;\n            o.Emission = _A;\n            o.Specular = _A;"
                    + "\n            o.Emission = _A;\n            o.Alpha = 1;",
                "            o.Albedo = _A;\n            o.Emission = _A;\n            o.Emission = _A;"
                    + "\n            o.Emission = _A;\n            o.Emission = _A;\n            o.Emission = _A;"
                    + "\n            o.Emission = _A;\n            o.Emission = _A;\n            o.Specular = _A;"
                    + "\n            o.Emission = _A;\n            o.Alpha = 1;");
            var properties = new[] { Prop("_A", ShaderPropertyType.Float) };
            var guard = Align(guardOriginal, guardGenerated, properties);
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");
            Assert.That(guard.Source.Blocks.Count, Is.EqualTo(1));
            Assert.That(guard.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));

            // The generated line past the window breaks only the removed
            // interpretation. The window adopts the removed hypothesis at
            // eight surviving lines, and the main walk refuses at the
            // ninth one. The break reuses a known identifier, so the
            // suffix search stays out of the way.
            var generated = guardGenerated.Replace("o.Specular = _A;", "o.Emission = _A;");
            var result = Align(guardOriginal, generated, properties);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(LineMismatchDetail));
            Assert.That(result.OriginalLine, Is.EqualTo(LineOf(guardOriginal, "o.Specular = _A;")));
        }

        // --- Falsifier locked-source-alignment 9: an implementation that treats an undecided nested block as a dead hypothesis ---
        // An implementation that swallows the tie refusal of a nested
        // block records Removed for the outer block on text that grounds
        // no outcome. The tie must refuse the whole alignment. The guard
        // decides the nested block, and the outer block then records
        // Removed.

        [Test]
        public void NestedTieRefusesTheWholeAlignment()
        {
            var guardProgram = "            //ifex _A==0\n            //ifex _B==0\n            o.Emission = _B;"
                + "\n            //endex"
                + "\n            o.Albedo = _B;\n            o.Albedo = _B;\n            o.Albedo = _B;"
                + "\n            //endex"
                + "\n            o.Albedo = _B;\n            o.Albedo = _B;\n            o.Albedo = _B;";
            var (guardOriginal, guardGenerated) = ProgramPair(
                "        _A (\"A\", Float) = 0\n        _B (\"B\", Float) = 0",
                guardProgram,
                "            o.Albedo = _B;\n            o.Albedo = _B;\n            o.Albedo = _B;");
            var properties = new[]
            {
                Prop("_A", ShaderPropertyType.Float),
                Prop("_B", ShaderPropertyType.Float)
            };
            var guard = Align(guardOriginal, guardGenerated, properties);
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");
            Assert.That(guard.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(guard.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(guard.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));

            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0\n        _B (\"B\", Float) = 0",
                "            //ifex _A==0\n            //ifex _B==0\n            o.Albedo = _B;\n            o.Alpha = _B;\n            //endex"
                    + "\n            o.Albedo = _B;\n            o.Alpha = _B;\n            o.Albedo = _B;\n            o.Alpha = _B;"
                    + "\n            //endex"
                    + "\n            o.Albedo = _B;\n            o.Alpha = _B;\n            o.Albedo = _B;\n            o.Alpha = _B;",
                "            o.Albedo = _B;\n            o.Alpha = _B;\n            o.Albedo = _B;\n            o.Alpha = _B;"
                    + "\n            o.Albedo = _B;\n            o.Alpha = _B;");
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockTieDetail));
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

        // The measured M1 shape of the bundled locked original admission
        // design. The inner block ties at the wrapper boundary, because no
        // surviving line sits between the inner block end and the wrapper
        // end. This test is the no-op guard of Falsifier 1 and Falsifier 9
        // below. It proves the fixture reaches the tie path.
        [Test]
        public void TieWithAKeptGuardAndNoDuplicateAlignsAndRecordsKept()
        {
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            //ifex _W==0\n            //ifex _A==0\n            o.Albedo = _A;\n            o.Alpha = _A;\n            //endex\n            //endex\n            o.Specular = _W;",
                "            o.Albedo = _A;\n            o.Alpha = _A;\n            o.Specular = _W;");
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Kept));
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_W==0"));
            Assert.That(result.Source.Blocks[0].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _W==0")));
            Assert.That(result.Source.Blocks[1].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.Kept));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(result.Source.Blocks[1].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
        }

        // --- Falsifier 1: a tie rule that decides kept whenever the kept trial consumed text, with no catch-up probe. ---
        // The kept world and the removed world of the inner block both
        // reproduce the generated text from the duplicate after the wrapper
        // end. The catch-up probe must catch that and refuse undecided.
        // The no-op guard is the pair without the duplicate above. It
        // aligns and records both blocks Kept.
        [Test]
        public void TieWhoseBodyIsDuplicatedAfterTheBlockRefusesUndecided()
        {
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            //ifex _W==0\n            //ifex _A==0\n            o.Albedo = _A;\n            o.Alpha = _A;\n            o.Specular = _A;\n            //endex\n            //endex\n            o.Albedo = _A;\n            o.Alpha = _A;\n            o.Specular = _A;",
                "            o.Albedo = _A;\n            o.Alpha = _A;\n            o.Specular = _A;");
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockTieDetail));
        }

        // --- Falsifier 9: a catch-up probe capped by a constant step or line count instead of the end of the file. ---
        // The probe must walk all 61 duplicated lines before its generated
        // index reaches the rival index. A probe capped below 61 steps
        // finds no death and no catch up and wrongly decides kept. The
        // no-op guard is the Falsifier 1 guard pair above. It aligns.
        [Test]
        public void CatchUpProbeWalksPastAnyConstantCapAndRefusesUndecided()
        {
            var body = new List<string>();
            for (var i = 0; i < 61; i++)
            {
                body.Add("            o.Albedo = _A;");
            }

            var program = "            //ifex _W==0\n            //ifex _A==0\n" + Join(body)
                + "\n            //endex\n            //endex\n" + Join(body);
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                program,
                Join(body));
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockTieDetail));
        }

        // --- Falsifier 2: an M2 resolution that runs the old lookahead or decides kept. ---
        // The vendor dropped the whole wrapper body, so both trials of the
        // wrapper consume zero generated lines. Y3 of the bundled locked
        // original admission design adopts the removed trial with no
        // lookahead. The code as of 2026-10-25 ran the lookahead, found no
        // difference, and refused undecided. The no-op guard below keeps
        // one surviving body line, so the kept trial consumes text and the
        // tie takes the Y2 catch-up path.

        [Test]
        public void ZeroAndZeroWrapperAlignsAndRecordsTheWrapperRemoved()
        {
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            //ifex _W==0\n            #ifndef K\n            o.Albedo = _A;\n            #endif"
                    + "\n            //endex\n            o.Specular = _W;",
                "            o.Specular = _W;");
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.True);
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_W==0"));
            Assert.That(result.Source.Blocks[0].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _W==0")));
            Assert.That(result.Source.Blocks[1].Kind, Is.EqualTo(LockedBlockKind.Ifndef));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("K"));
            Assert.That(result.Source.Blocks[1].OriginalLine, Is.EqualTo(LineOf(original, "#ifndef K")));
        }

        // The no-op guard of Falsifier 2 above. One surviving wrapper body
        // line gives the kept trial a nonzero count, so the shape is no
        // longer both zero. The tie then runs the Y2 catch-up path and
        // records Kept. The guard proves the fixture reaches the tie path.

        [Test]
        public void ZeroAndZeroWrapperWithOneSurvivingBodyLineRecordsKept()
        {
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            //ifex _W==0\n            #ifndef K\n            o.Albedo = _A;\n            #endif"
                    + "\n            o.Alpha = _W;\n            //endex\n            o.Specular = _W;",
                "            o.Alpha = _W;\n            o.Specular = _W;");
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Kept));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.False);
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
        }

        // The measured case e shape of the bundled locked original
        // admission design. The zero-and-zero wrapper runs no lookahead
        // (Y3), so a later tie cannot throw through it (Y2 case e note).
        // The kept guard after the wrapper then resolves on its own. The
        // plain line after the guard is load bearing. Without it the guard
        // probe reaches the end of the original file and the shape refuses
        // undecided.

        [Test]
        public void CaseEWrapperResolvesAndTheLaterKeptGuardRecordsKept()
        {
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            //ifex _W==0\n            #ifndef K\n            o.Albedo = _A;\n            #endif"
                    + "\n            //endex\n            #ifdef _A\n            o.Emission = _A;\n            #endif"
                    + "\n            o.Specular = _W;",
                "            #ifdef _A\n            o.Emission = _A;\n            #endif\n            o.Specular = _W;");
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(3));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.True);
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_W==0"));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[2].Kind, Is.EqualTo(LockedBlockKind.Ifdef));
            Assert.That(result.Source.Blocks[2].Outcome, Is.EqualTo(LockedBlockOutcome.Kept));
            Assert.That(result.Source.Blocks[2].ConditionOrKeyword, Is.EqualTo("_A"));
        }

        // Y3 of the bundled locked original admission design keeps the
        // Unobservable branch unchanged. A wrapper whose body holds only
        // comments has no surviving line, so the tie path never runs and
        // the record carries no marker. This test pins existing behavior
        // and passes on its first possible run.

        [Test]
        public void CommentOnlyWrapperStaysUnobservable()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            //ifex _A==0\n            // just a note\n            //endex\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(1));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Unobservable));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.False);
        }

        // Y3 states the implication of the marker in reverse. A kept
        // ifdef or ifndef trial always consumes its directive line, so a
        // removed ifdef never meets the both zero shape. Its record
        // carries no marker. This test pins existing behavior and passes
        // on its first possible run.

        [Test]
        public void RemovedIfdefRecordIsNeverZeroAndZero()
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0",
                "            #ifdef K\n            o.Albedo = _A;\n            #endif\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var result = Align(original, generated, new[] { Prop("_A", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(1));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifdef));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.False);
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

        // --- Falsifier locked-source-alignment 7: Properties-region rename ---
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

        // --- Falsifier 4: a suffix split that tries every occurrence of a longest declared name. ---
        // An implementation that splits at every occurrence of the longest
        // declared name ties on the second occurrence and refuses the
        // aligned pair (Y6 of the bundled locked original admission
        // design). PlainMaterialNameSuffixIsUnchanged is the no-op guard
        // and proves the fixture reaches the suffix search. The suffix
        // string plays the role of the cleaned material name.
        [Test]
        public void EmbeddedDeclaredNameSuffixDerivesFromTheEarliestBoundary()
        {
            var (original, generated) = SmallPair(
                "        _Color (\"Color\", Color) = (1, 1, 1, 1)",
                null,
                "        float4 _Color;\n        o.Albedo = _Color;",
                ("_Color", "_Color_Body_Color_1"));
            var result = Align(original, generated, new[] { Prop("_Color", ShaderPropertyType.Color) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Body_Color_1"));
            Assert.That(result.Source.States["_Color"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_Color"], Is.EqualTo("_Color_Body_Color_1"));
        }

        // --- Falsifier 5: a head rule that anchors anywhere but the earliest boundary. ---
        // An implementation that anchors at a later declared name derives
        // the wrong suffix and the walk refuses the aligned pair (Y6 of
        // the bundled locked original admission design). The unrenamed
        // live float is the later name. PlainMaterialNameSuffixIsUnchanged
        // is the no-op guard and proves the fixture reaches the suffix
        // search.
        [Test]
        public void HeadAnchorsAtTheEarliestBoundaryNotALaterName()
        {
            var (original, generated) = SmallPair(
                "        _Color (\"Color\", Color) = (1, 1, 1, 1)\n        _Cutoff (\"Cutoff\", Float) = 0",
                null,
                "        float4 _Color;\n        float _Cutoff;\n        o.Albedo = _Color;\n        o.Alpha = _Cutoff;",
                ("_Color", "_Color_Cutoff_X"));
            var properties = new[]
            {
                Prop("_Color", ShaderPropertyType.Color),
                Prop("_Cutoff", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Cutoff_X"));
            Assert.That(result.Source.States["_Color"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.States["_Cutoff"], Is.EqualTo(LockedPropertyState.Live));
        }

        // Two renamed names embed one longer declared name. A split that
        // scans the longest name gives the two artifacts disjoint
        // candidate sets and refuses on the empty intersection (Y6 of the
        // bundled locked original admission design). The earliest boundary
        // gives one shared suffix.
        [Test]
        public void LongerEmbeddedNameStraddleDerivesOneSuffix()
        {
            var (original, generated) = SmallPair(
                "        _Color (\"Color\", Color) = (1, 1, 1, 1)"
                    + "\n        _EmissionColor (\"Emission\", Color) = (0, 0, 0, 0)"
                    + "\n        _Cutoff (\"Cutoff\", Float) = 0",
                null,
                "        float4 _Color;\n        float4 _EmissionColor;\n        float _Cutoff;"
                    + "\n        o.Albedo = _Color;\n        o.Emission = _EmissionColor;\n        o.Alpha = _Cutoff;",
                ("_Color", "_Color_Cut_Cutoff_On"),
                ("_EmissionColor", "_EmissionColor_Cut_Cutoff_On"));
            var properties = new[]
            {
                Prop("_Color", ShaderPropertyType.Color),
                Prop("_EmissionColor", ShaderPropertyType.Color),
                Prop("_Cutoff", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Cut_Cutoff_On"));
            Assert.That(result.Source.States["_Color"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.States["_EmissionColor"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.States["_Cutoff"], Is.EqualTo(LockedPropertyState.Live));
        }

        // Two declared names of one length sit in one artifact. A split
        // that reads both ties and refuses the aligned pair (Y6 of the
        // bundled locked original admission design). The earliest boundary
        // breaks the tie.
        [Test]
        public void EmbeddedNameNoLongerTiesTheIntersection()
        {
            var (original, generated) = SmallPair(
                "        _Cutoff (\"Cutoff\", Float) = 0\n        _Matcap (\"Matcap\", Float) = 0",
                null,
                "        float _Cutoff;\n        float _Matcap;\n        o.Alpha = _Cutoff;\n        o.Emission = _Matcap;",
                ("_Cutoff", "_Cutoff_Body_Matcap_1"));
            var properties = new[]
            {
                Prop("_Cutoff", ShaderPropertyType.Float),
                Prop("_Matcap", ShaderPropertyType.Float)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Body_Matcap_1"));
            Assert.That(result.Source.States["_Cutoff"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.States["_Matcap"], Is.EqualTo(LockedPropertyState.Live));
        }

        // A plain material name with no embedded declared name keeps its
        // suffix. The test passes on its first run and is recorded as
        // characterization. It is the no-op guard for Falsifier 4 and
        // Falsifier 5 (Y6 of the bundled locked original admission
        // design).
        [Test]
        public void PlainMaterialNameSuffixIsUnchanged()
        {
            var (original, generated) = SmallPair(
                "        _Color (\"Color\", Color) = (1, 1, 1, 1)",
                null,
                "        float4 _Color;\n        o.Albedo = _Color;",
                ("_Color", "_Color_Body"));
            var result = Align(original, generated, new[] { Prop("_Color", ShaderPropertyType.Color) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Body"));
            Assert.That(result.Source.States["_Color"], Is.EqualTo(LockedPropertyState.Renamed));

            var (textureOriginal, textureGenerated) = SmallPair(
                "        _MainTex (\"Main\", 2D) = \"white\" {}",
                null,
                "        sampler2D _MainTex;\n        o.Albedo *= tex2D(_MainTex, IN.uv).rgb;",
                ("_MainTex", "_MainTex_Body"));
            var texture = Align(
                textureOriginal, textureGenerated, new[] { Prop("_MainTex", ShaderPropertyType.Texture) });
            Assert.That(texture.Succeeded, Is.True, texture.RefusalDetail ?? "refused");
            Assert.That(texture.Source.Suffix, Is.EqualTo("_Body"));
            Assert.That(texture.Source.States["_MainTex"], Is.EqualTo(LockedPropertyState.Renamed));
        }

        // A renamed texture name embeds the longer declared name. A split
        // that scans the longest name derives the wrong tail and the walk
        // refuses (Y6 of the bundled locked original admission design).
        // The earliest boundary at the renamed head keeps the suffix, and
        // the tail strip still admits the derived forms.
        [Test]
        public void RenamedTextureWithAnEmbeddedNameKeepsTheSuffix()
        {
            var (original, generated) = SmallPair(
                "        _T (\"T\", 2D) = \"white\" {}\n        _Color (\"Color\", Color) = (1, 1, 1, 1)",
                null,
                "        sampler2D _T;\n        float4 _T_ST;\n        float4 _T_TexelSize;\n        o.Albedo *= _T_ST;",
                // The texel size form holds the substring _T twice, once in
                // the head and once behind _Texel. One wide replace renames
                // it twice, so each renamed form gets an explicit edit.
                ("_T (\"T\"", "_T_Body_Color_1 (\"T\""),
                ("sampler2D _T;", "sampler2D _T_Body_Color_1;"),
                ("float4 _T_ST;", "float4 _T_Body_Color_1_ST;"),
                ("float4 _T_TexelSize;", "float4 _T_Body_Color_1_TexelSize;"),
                ("o.Albedo *= _T_ST;", "o.Albedo *= _T_Body_Color_1_ST;"));
            var properties = new[]
            {
                Prop("_T", ShaderPropertyType.Texture),
                Prop("_Color", ShaderPropertyType.Color)
            };
            var result = Align(original, generated, properties);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Body_Color_1"));
            Assert.That(result.Source.States["_T"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_T"], Is.EqualTo("_T_Body_Color_1"));
            Assert.That(result.Source.States["_T_ST"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_T_ST"], Is.EqualTo("_T_Body_Color_1_ST"));
            Assert.That(result.Source.States["_T_TexelSize"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_T_TexelSize"], Is.EqualTo("_T_Body_Color_1_TexelSize"));
        }

        // Y7 of the bundled locked original admission design: the
        // Properties window counts braces outside strings and comments.
        // A display string with an unbalanced brace drove the whole line
        // count to zero and closed the window before the later property
        // lines. Each fixture pairs one untouched property line with one
        // renamed property line after it. The program never references
        // either property, so the renamed artifact can come only from the
        // Properties window. A closed window then leaves no suffix
        // candidate and the aligner refuses.

        // --- Falsifier 7: a Properties window that counts braces inside display strings. ---
        // An implementation that counts braces inside display strings lets
        // one unbalanced } drive the depth to zero. The window then closes
        // before the renamed property line, and the aligner refuses or
        // derives a wrong suffix. The guard aligns the same block without
        // the brace string and proves the fixture reaches the window.

        [Test]
        public void DisplayStringWithUnbalancedBraceKeepsLaterPropertiesLines()
        {
            var (guardOriginal, guardGenerated) = SmallPair(
                "        _Q (\"Q\", Float) = 0\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var guard = Align(
                guardOriginal,
                guardGenerated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");
            Assert.That(guard.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));

            var (original, generated) = SmallPair(
                "        _Q (\"Q }\", Float) = 0\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var result = Align(
                original,
                generated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_W"], Is.EqualTo("_W_S"));
        }

        [Test]
        public void EscapedQuoteInADisplayStringIsAccepted()
        {
            // The display string is "Q \" }". The backslash escapes the
            // quote. The string skip ends at the real closing quote. The }
            // inside the string does not count, so the window stays open
            // and the renamed line still yields its artifact.
            var (original, generated) = SmallPair(
                "        _Q (\"Q \\\" }\", Float) = 0\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var result = Align(
                original,
                generated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_W"], Is.EqualTo("_W_S"));
        }

        [Test]
        public void UnclosedDisplayStringIsAccepted()
        {
            // The display string never closes before the line end. The
            // string skip ends at the line end. The } inside the string
            // does not count, so the later Properties lines still count.
            var (original, generated) = SmallPair(
                "        _Q (\"Q } open, Float) = 0\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var result = Align(
                original,
                generated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_W"], Is.EqualTo("_W_S"));
        }

        // Characterization for Y7. A [Gamma] mark inside a quoted display
        // string records no gamma name. This test passed on its first run
        // on 2026-10-27. Skipping string contents can only remove gamma
        // marks, and the vendor bakes with the real Unity flags, so the
        // direction is safe.
        [Test]
        public void GammaMarkInsideAQuotedDisplayStringStopsCounting()
        {
            var original = BaseOriginal().Replace(
                "        _P (\"P\", Float) = 0.5",
                "        _G (\"G [Gamma]\", Float) = 0.5\n        [Gamma] _H (\"H\", Float) = 0.5");
            var names = PoiyomiLockedSourceAlignment.GammaPropertyNames(original);
            Assert.That(names, Is.EqualTo(new[] { "_H" }));
        }

        [Test]
        public void LineCommentBraceDoesNotCloseThePropertiesWindow()
        {
            // The comment is a true line comment inside the block. Its }
            // must not close the window. The later renamed line still
            // yields its artifact.
            var (original, generated) = SmallPair(
                "        _Q (\"Q\", Float) = 0 // } close\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var result = Align(
                original,
                generated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_W"], Is.EqualTo("_W_S"));
        }

        // Characterization for the Y7 block comment reading. The brace
        // counter resumes after the closer on the same line, so a }
        // after */ counts. The window then closes at the closer line and
        // the gamma line after it leaves the window. The guard keeps the
        // closer line clean and proves the fixture reaches the window.
        // This test passed on its first run on 2026-10-27.
        [Test]
        public void BraceAfterTheBlockCommentCloserClosesThePropertiesWindow()
        {
            var guarded = BaseOriginal().Replace(
                "        _P (\"P\", Float) = 0.5",
                "        /*\n        a comment line\n        */\n        [Gamma] _H (\"H\", Float) = 0.5");
            Assert.That(
                PoiyomiLockedSourceAlignment.GammaPropertyNames(guarded),
                Is.EqualTo(new[] { "_H" }));

            var original = BaseOriginal().Replace(
                "        _P (\"P\", Float) = 0.5",
                "        /*\n        a comment line\n        */ }\n        [Gamma] _H (\"H\", Float) = 0.5");
            Assert.That(PoiyomiLockedSourceAlignment.GammaPropertyNames(original), Is.Empty);
        }

        // Characterization for the Y7 commented block reading. A
        // commented out Properties block before the real one contributes
        // no window lines. The real block keeps the window, so its
        // renamed lines still yield the artifact. The vendor deletes the
        // block comment, so the generated text holds only the real
        // block. This test passed on its first run on 2026-10-27.
        [Test]
        public void ACommentedOutPropertiesBlockBeforeTheRealOneYieldsNoWindowLines()
        {
            var (plainOriginal, plainGenerated) = SmallPair(
                "        _Q (\"Q\", Float) = 0\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var guard = Align(
                plainOriginal,
                plainGenerated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(guard.Succeeded, Is.True, guard.RefusalDetail ?? "refused");

            var commentedHead = Join(new[]
            {
                "/*",
                "Shader \"Custom/Old\"",
                "{",
                "    Properties",
                "    {",
                "        _Old (\"Old\", Float) = 0",
                "    }",
                "}",
                "*/"
            });
            var original = commentedHead + "\n" + plainOriginal;
            var result = Align(
                original,
                plainGenerated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) });
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_W"], Is.EqualTo("_W_S"));
        }

        // --- Falsifier 1 and Falsifier 9, era twins: the duplicated body tie. ---
        // Y8 item 2 of the bundled locked original admission design says
        // the tie rules key on consumption and text, not on era. The pair
        // duplicates the wrapper body after the wrapper end and carries no
        // era specific form. Both outcomes reproduce the generated text
        // from the duplicate. The catch-up probe must walk to the end of
        // the file and refuse undecided. The no-op guard is
        // TieWithAKeptGuardAndNoDuplicateAlignsAndRecordsKept in the
        // all-era tests above. It proves the fixture reaches the tie path.
        [TestCase(PoiyomiLockEra.Bundled9364)]
        [TestCase(PoiyomiLockEra.Thry274To2742)]
        [TestCase(PoiyomiLockEra.Thry2743)]
        public void TheDuplicatedBodyTieRefusesInEveryEra(PoiyomiLockEra era)
        {
            var body = new List<string>();
            for (var i = 0; i < 61; i++)
            {
                body.Add("            o.Albedo = _A;");
            }

            var program = "            //ifex _W==0\n            //ifex _A==0\n" + Join(body)
                + "\n            //endex\n            //endex\n" + Join(body);
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                program,
                Join(body));
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = PoiyomiLockedSourceAlignment.AlignForEra(original, generated, properties, era);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Source, Is.Null);
            Assert.That(result.RefusalDetail, Is.EqualTo(BlockTieDetail));
        }

        // --- Falsifier 2, Falsifier 3, and Falsifier 11 record halves, era twins: the zero-and-zero wrapper. ---
        // Y3 adopts the removed trial of a wrapper whose two trials both
        // consume zero generated lines. The adoption writes the record
        // marker, and the later checks read that marker, so the record
        // shape must hold in every era (Y8 item 2). The pair carries no
        // era specific form. The no-op guard is
        // ZeroAndZeroWrapperWithOneSurvivingBodyLineRecordsKept in the
        // all-era tests above. It proves the fixture reaches the tie path.
        [TestCase(PoiyomiLockEra.Bundled9364)]
        [TestCase(PoiyomiLockEra.Thry274To2742)]
        [TestCase(PoiyomiLockEra.Thry2743)]
        public void TheZeroAndZeroWrapperResolvesInEveryEra(PoiyomiLockEra era)
        {
            var (original, generated) = ProgramPair(
                "        _W (\"W\", Float) = 0\n        _A (\"A\", Float) = 0",
                "            //ifex _W==0\n            #ifndef K\n            o.Albedo = _A;\n            #endif"
                    + "\n            //endex\n            o.Specular = _W;",
                "            o.Specular = _W;");
            var properties = new[]
            {
                Prop("_W", ShaderPropertyType.Float),
                Prop("_A", ShaderPropertyType.Float)
            };
            var result = PoiyomiLockedSourceAlignment.AlignForEra(original, generated, properties, era);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Kind, Is.EqualTo(LockedBlockKind.Ifex));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ZeroAndZero, Is.True);
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_W==0"));
            Assert.That(result.Source.Blocks[0].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _W==0")));
            Assert.That(result.Source.Blocks[1].Kind, Is.EqualTo(LockedBlockKind.Ifndef));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("K"));
            Assert.That(result.Source.Blocks[1].OriginalLine, Is.EqualTo(LineOf(original, "#ifndef K")));
        }

        // --- Falsifier 6 and Falsifier 12, era twins: digit-led literals. ---
        // Y5 widens the collected runs to full maximal runs and drops the
        // digit-led ones. The rule keys on text, not on era, so the
        // literals must not block the suffix in any era (Y8 item 2). The
        // pair carries no era specific form. It bakes one hex literal,
        // one decimal literal, and one exponent component. The no-op
        // guards are the guard pairs of DigitLedHexLiteralDoesNotBlockTheSuffix
        // and BakedDecimalAndExponentLiteralsDoNotBlockTheSuffix in the
        // all-era tests above. They prove the fixture reaches the suffix
        // search.
        [TestCase(PoiyomiLockEra.Bundled9364)]
        [TestCase(PoiyomiLockEra.Thry274To2742)]
        [TestCase(PoiyomiLockEra.Thry2743)]
        public void TheDigitLedLiteralsDoNotBlockTheSuffixInEveryEra(PoiyomiLockEra era)
        {
            var (original, generated) = SmallPair(
                "        _P (\"P\", Float) = 0.5\n        _F (\"F\", Float) = 0"
                    + "\n        _Q (\"Q\", Float) = 0\n        _V (\"V\", Vector) = (0, 0, 0, 0)",
                null,
                "        float _P;\n        float _Q;\n        float4 _V;"
                    + "\n        #ifdef _F\n        o.Emission = _F;\n        o.Albedo *= 0x7ffffff;\n        #endif"
                    + "\n        o.Albedo = _P;\n        o.Alpha = _Q;\n        o.Albedo += _V;",
                ("float _P;", "float _P_S;"),
                ("o.Albedo = _P;", "o.Albedo = _P_S;"),
                ("_P (\"P\"", "_P_S (\"P\""),
                ("o.Alpha = _Q;", "o.Alpha = 0.975;"),
                ("o.Albedo += _V;", "o.Albedo += float4(0,1E-06,0,0);"));
            var properties = new[]
            {
                Prop("_P", ShaderPropertyType.Float),
                Prop("_F", ShaderPropertyType.Float),
                Prop("_Q", ShaderPropertyType.Float),
                Prop("_V", ShaderPropertyType.Vector)
            };
            var result = PoiyomiLockedSourceAlignment.AlignForEra(original, generated, properties, era);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_P"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_P"], Is.EqualTo("_P_S"));
            Assert.That(result.Source.States["_F"], Is.EqualTo(LockedPropertyState.Live));
            Assert.That(result.Source.States["_Q"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_Q"], Is.EqualTo("0.975"));
            Assert.That(result.Source.States["_V"], Is.EqualTo(LockedPropertyState.Baked));
            Assert.That(result.Source.BakedLiterals["_V"], Is.EqualTo("float4(0,1E-06,0,0)"));
        }

        // --- Falsifier 4 and Falsifier 5, era twins: the earliest head boundary. ---
        // Y6 splits only at the earliest start position, and the longest
        // qualifying declared name decides at that position. The rule
        // keys on text, not on era, so the suffix must derive in every
        // era (Y8 item 2). The pair carries no era specific form. The
        // later declared name _Cutoff is the trap. The no-op guard is
        // PlainMaterialNameSuffixIsUnchanged in the all-era tests above.
        // It proves the fixture reaches the suffix search.
        [TestCase(PoiyomiLockEra.Bundled9364)]
        [TestCase(PoiyomiLockEra.Thry274To2742)]
        [TestCase(PoiyomiLockEra.Thry2743)]
        public void TheEarliestHeadBoundarySuffixDerivesInEveryEra(PoiyomiLockEra era)
        {
            var (original, generated) = SmallPair(
                "        _Color (\"Color\", Color) = (1, 1, 1, 1)\n        _Cutoff (\"Cutoff\", Float) = 0",
                null,
                "        float4 _Color;\n        float _Cutoff;\n        o.Albedo = _Color;\n        o.Alpha = _Cutoff;",
                ("_Color", "_Color_Cutoff_X"));
            var properties = new[]
            {
                Prop("_Color", ShaderPropertyType.Color),
                Prop("_Cutoff", ShaderPropertyType.Float)
            };
            var result = PoiyomiLockedSourceAlignment.AlignForEra(original, generated, properties, era);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_Cutoff_X"));
            Assert.That(result.Source.States["_Color"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_Color"], Is.EqualTo("_Color_Cutoff_X"));
            Assert.That(result.Source.States["_Cutoff"], Is.EqualTo(LockedPropertyState.Live));
        }

        // --- Falsifier 7, era twin: the braced display string window. ---
        // Y7 counts braces outside strings and comments, so one unbalanced
        // brace inside a display string cannot close the Properties
        // window. The rule keys on text, not on era (Y8 item 2). The pair
        // carries no era specific form. The no-op guard is the guard pair
        // of DisplayStringWithUnbalancedBraceKeepsLaterPropertiesLines in
        // the all-era tests above. It proves the fixture reaches the
        // window.
        [TestCase(PoiyomiLockEra.Bundled9364)]
        [TestCase(PoiyomiLockEra.Thry274To2742)]
        [TestCase(PoiyomiLockEra.Thry2743)]
        public void TheBracedDisplayStringWindowYieldsTheArtifactInEveryEra(PoiyomiLockEra era)
        {
            var (original, generated) = SmallPair(
                "        _Q (\"Q }\", Float) = 0\n        _W (\"W\", Float) = 0",
                null,
                null,
                ("_W (\"W\"", "_W_S (\"W\""));
            var result = PoiyomiLockedSourceAlignment.AlignForEra(
                original,
                generated,
                new[] { Prop("_Q", ShaderPropertyType.Float), Prop("_W", ShaderPropertyType.Float) },
                era);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Suffix, Is.EqualTo("_S"));
            Assert.That(result.Source.States["_W"], Is.EqualTo(LockedPropertyState.Renamed));
            Assert.That(result.Source.RenamedNames["_W"], Is.EqualTo("_W_S"));
        }

        // --- Falsifier 8, era twin: the commented directive extent. ---
        // Y4 tracks block comments in the extent finder, so a commented
        // directive inside a comment span never opens a block. The rule
        // keys on text, not on era (Y8 item 2). The pair carries no era
        // specific form. The no-op guard is
        // CommentSpanWithoutADirectiveAlignsTheSame in the all-era tests
        // above. It proves the fixture reaches the extent finder.
        [TestCase(PoiyomiLockEra.Bundled9364)]
        [TestCase(PoiyomiLockEra.Thry274To2742)]
        [TestCase(PoiyomiLockEra.Thry2743)]
        public void TheCommentedDirectiveNeverOpensABlockInEveryEra(PoiyomiLockEra era)
        {
            var (original, generated) = ProgramPair(
                "        _A (\"A\", Float) = 0\n        _B (\"B\", Float) = 0\n        _C (\"C\", Float) = 0",
                "            //ifex _A==0\n            /*\n            //ifex _C==0\n            */"
                    + "\n            //ifex _B==0\n            o.Albedo = _B;\n            //endex\n            //endex"
                    + "\n            o.Alpha = 1;",
                "            o.Alpha = 1;");
            var properties = new[]
            {
                Prop("_A", ShaderPropertyType.Float),
                Prop("_B", ShaderPropertyType.Float),
                Prop("_C", ShaderPropertyType.Float)
            };
            var result = PoiyomiLockedSourceAlignment.AlignForEra(original, generated, properties, era);
            Assert.That(result.Succeeded, Is.True, result.RefusalDetail ?? "refused");
            Assert.That(result.Source.Blocks.Count, Is.EqualTo(2));
            Assert.That(result.Source.Blocks[0].Outcome, Is.EqualTo(LockedBlockOutcome.Removed));
            Assert.That(result.Source.Blocks[0].ConditionOrKeyword, Is.EqualTo("_A==0"));
            Assert.That(result.Source.Blocks[0].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _A==0")));
            Assert.That(result.Source.Blocks[1].Outcome, Is.EqualTo(LockedBlockOutcome.NotEvaluated));
            Assert.That(result.Source.Blocks[1].ConditionOrKeyword, Is.EqualTo("_B==0"));
            Assert.That(result.Source.Blocks[1].OriginalLine, Is.EqualTo(LineOf(original, "//ifex _B==0")));
            Assert.That(result.Source.States["_C"], Is.EqualTo(LockedPropertyState.Unused));
        }
    }
}
