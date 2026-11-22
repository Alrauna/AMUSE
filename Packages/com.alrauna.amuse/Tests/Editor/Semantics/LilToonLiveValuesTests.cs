using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Data tests for the reviewed live value model of lilToon 2.4.1.
    /// The model drops dead families from the material key, so a
    /// missing or wrong name can merge two materials that render
    /// differently. Every test here checks the pinned data against
    /// the installed shaders rather than against the model itself.
    /// </summary>
    public sealed class LilToonLiveValuesTests
    {
        private static readonly string[] RowShaderGuids =
        {
            LilToonSourceAttestation.SupportedShaderGuid,
            LilToonSourceAttestation.CutoutShaderGuid,
            LilToonSourceAttestation.TransparentShaderGuid,
            LilToonSourceAttestation.OutlineShaderGuid,
            LilToonSourceAttestation.OutlineCutoutShaderGuid,
            LilToonSourceAttestation.OutlineTransparentShaderGuid,
            LilToonSourceAttestation.OnePassTransparentShaderGuid,
            LilToonSourceAttestation.TwoPassTransparentShaderGuid,
            LilToonSourceAttestation.OnePassTransparentOutlineShaderGuid,
            LilToonSourceAttestation.TwoPassTransparentOutlineShaderGuid,
        };

        private static readonly string[][] KeywordSets =
        {
            new string[0],
            new[] { "DIRECTIONAL", "SHADOWS_SCREEN", "LIGHTPROBE_SH" },
            new[] { "POINT" },
            new[] { "SHADOWS_DEPTH" },
        };

        /// <summary>
        /// The keyword set of one pass. Main agent ruling of
        /// 2026-11-22: one set per pass, chosen by the LightMode tag.
        /// A full cross product of every set and every pass costs
        /// more than 20 minutes of shader compiles for no extra
        /// declared name, so the test keeps one set per pass.
        /// </summary>
        private static string[] KeywordsOfPass(ShaderData.Pass pass)
        {
            var lightMode = pass.FindTagValue(
                new UnityEngine.Rendering.ShaderTagId("LightMode")).name;
            switch (lightMode)
            {
                case "ForwardBase":
                    return KeywordSets[1];
                case "ForwardAdd":
                    return KeywordSets[2];
                case "ShadowCaster":
                    return KeywordSets[3];
                default:
                    return KeywordSets[0];
            }
        }

        private static IEnumerable<Shader> RowShaders()
        {
            foreach (var guid in RowShaderGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.That(
                    path, Is.Not.Empty,
                    "fixture precondition: the admitted lilToon row " +
                    "must resolve by its pinned GUID " + guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                Assert.That(
                    shader, Is.Not.Null,
                    "fixture precondition: the admitted lilToon row at " +
                    path + " must load");
                yield return shader;
            }
        }

        private static IEnumerable<string> ShaderPropertyNames(
            Shader shader)
        {
            var count = shader.GetPropertyCount();
            for (var index = 0; index < count; index++)
            {
                yield return shader.GetPropertyName(index);
            }
        }

        /// <summary>
        /// Every switch must exist as a property of every admitted
        /// row. A switch that one row does not declare could never
        /// turn its family back on there, and the off test would
        /// read a value the row ignores.
        /// </summary>
        [Test]
        public void FamiliesNameOnlyDeclaredSwitches()
        {
            var offenders = new SortedSet<string>();
            foreach (var shader in RowShaders())
            {
                var rowProperties =
                    new HashSet<string>(ShaderPropertyNames(shader));
                foreach (var family in LilToonLiveValues.Model.Families)
                {
                    foreach (var name in family.Switches)
                    {
                        if (!rowProperties.Contains(name))
                        {
                            offenders.Add(shader.name + ": " + name);
                        }
                    }
                }
            }

            Assert.That(
                offenders, Is.Empty,
                "every switch must be a property of every admitted " +
                "row. Offenders: " + string.Join(", ", offenders));
        }

        /// <summary>
        /// Every field of every constant buffer that does not start
        /// with Unity, and every texture binding that does not start
        /// with unity_, of every compiled variant of every pass of
        /// every admitted row must be a declared name. A compiled
        /// name that the model does not declare could hide a live
        /// value behind a stale name.
        /// </summary>
        [Test]
        public void DeclaredUniformsCoverEveryCompiledName()
        {
            var declared = new HashSet<string>(
                LilToonLiveValues.Model.DeclaredUniforms);
            var missing = new SortedSet<string>();
            var failures = new List<string>();
            var compiled = 0;

            foreach (var shader in RowShaders())
            {
                var data = ShaderUtil.GetShaderData(shader);
                var subshader = data.GetSubshader(0);
                for (var passIndex = 0;
                     passIndex < subshader.PassCount;
                     passIndex++)
                {
                    var pass = subshader.GetPass(passIndex);
                    var keywords = KeywordsOfPass(pass);
                    {
                        foreach (var stage in new[]
                        {
                            UnityEditor.Rendering.ShaderType.Vertex,
                            UnityEditor.Rendering.ShaderType.Fragment,
                        })
                        {
                            var variant = pass.CompileVariant(
                                stage,
                                keywords,
                                UnityEditor.Rendering
                                    .ShaderCompilerPlatform.D3D,
                                BuildTarget.StandaloneWindows64);
                            if (!variant.Success)
                            {
                                failures.Add(
                                    shader.name + " pass " + pass.Name +
                                    " stage " + stage + " keywords " +
                                    string.Join(",", keywords) + ": " +
                                    string.Join(" | ", variant.Messages));
                                continue;
                            }

                            compiled++;
                            foreach (var buffer in variant.ConstantBuffers)
                            {
                                if (buffer.Name.StartsWith(
                                        "Unity",
                                        System.StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                foreach (var field in buffer.Fields)
                                {
                                    if (!declared.Contains(field.Name))
                                    {
                                        missing.Add(field.Name);
                                    }
                                }
                            }

                            foreach (var binding in variant.TextureBindings)
                            {
                                if (binding.Name.StartsWith(
                                        "unity_",
                                        System.StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                if (!declared.Contains(binding.Name))
                                {
                                    missing.Add(binding.Name);
                                }
                            }
                        }
                    }
                }
            }

            Assert.That(
                failures, Is.Empty,
                "CompileVariant must compile every lilToon variant");
            Assert.That(
                compiled, Is.GreaterThan(0),
                "the test must compile at least one variant");
            Assert.That(
                missing, Is.Empty,
                "DeclaredUniforms must cover every compiled name. " +
                "Missing: " + string.Join(", ", missing));
        }

        /// <summary>
        /// A family name that no admitted row declares as a property
        /// can never appear in a saved material. It would widen the
        /// model for no gain and hide a typo in the data.
        /// </summary>
        [Test]
        public void FamiliesNameOnlyDeclaredProperties()
        {
            var rowProperties = new HashSet<string>();
            foreach (var shader in RowShaders())
            {
                rowProperties.UnionWith(ShaderPropertyNames(shader));
            }

            var unknown = new SortedSet<string>();
            foreach (var family in LilToonLiveValues.Model.Families)
            {
                foreach (var property in family.Properties)
                {
                    if (!rowProperties.Contains(property))
                    {
                        unknown.Add(family.Name + ": " + property);
                    }
                }
            }

            Assert.That(
                unknown, Is.Empty,
                "every family name must be a property of an admitted " +
                "row. Unknown: " + string.Join(", ", unknown));
        }

        /// <summary>
        /// A family that holds its own switch would drop the switch
        /// from the key, so two materials with different switch
        /// values could share one page.
        /// </summary>
        [Test]
        public void FamiliesNeverHoldASwitch()
        {
            var switches = new HashSet<string>();
            foreach (var family in LilToonLiveValues.Model.Families)
            {
                switches.UnionWith(family.Switches);
            }

            var offenders = new SortedSet<string>();
            foreach (var family in LilToonLiveValues.Model.Families)
            {
                foreach (var property in family.Properties)
                {
                    if (switches.Contains(property))
                    {
                        offenders.Add(family.Name + ": " + property);
                    }
                }
            }

            Assert.That(
                offenders, Is.Empty,
                "no family may hold a switch. Offenders: " +
                string.Join(", ", offenders));
        }
    }
}
