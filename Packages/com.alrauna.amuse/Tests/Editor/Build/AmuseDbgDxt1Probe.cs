using System;
using System.Collections.Generic;
using System.IO;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Temporary AMUSE-DBG diagnostics for the 2026-09-18 zero-proof
    /// investigation. Removal is tracked by the investigation note.
    /// </summary>
    internal sealed class AmuseDbgDxt1Probe
    {
        private const string Path = "Assets/AmuseTests_DbgDxt1Mask.png";

        [Test]
        public void Dbg_Dxt1Mask_FactorTrace()
        {
            try
            {
                RunProbe();
            }
            catch (System.Exception e)
            {
                Debug.Log(
                    "[AMUSE-DBG] FAILED " + e.GetType().Name + "\n" +
                    e.StackTrace);
                throw;
            }
            finally
            {
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Path)
                    != null)
                {
                    AssetDatabase.DeleteAsset(Path);
                }
            }
        }

        private static void RunProbe()
        {
            var staging = new Texture2D(128, 128, TextureFormat.RGBA32, true);
            var pixels = new Color32[128 * 128];
            for (var y = 0; y < 128; y++)
            {
                for (var x = 0; x < 128; x++)
                {
                    pixels[y * 128 + x] = x < 64
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(0, 0, 0, 255);
                }
            }

            staging.SetPixels32(pixels);
            staging.Apply();
            File.WriteAllBytes(Path, staging.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(staging);
            AssetDatabase.ImportAsset(
                Path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
            importer.mipmapEnabled = true;
            importer.sRGBTexture = true;
            importer.textureCompression =
                TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
            Assert.That(mask.format, Is.EqualTo(TextureFormat.DXT1));

            var material = LilToonFixtureTestBase
                .CreateTransparentConversionMaterial();
            material.SetTexture("_MainTex", mask);
            material.SetTexture("_AlphaMask", mask);
            material.SetFloat("_AlphaMaskMode", 1f);
            material.SetFloat("_Cutoff", 0.001f);

            var selected = VerifiedLilToonTestSeams
                .SelectVerifiedFixtureRequest(
                    material,
                    out var family,
                    out var alphaRelevance,
                    out var captureSchema);
            Debug.Log(
                $"[AMUSE-DBG] selected={selected} family={family} " +
                $"captureSchemaNull={captureSchema == null}");

            var capturedList = VerifiedLilToonTestSeams
                .CaptureVerifiedFixtureMaterials(
                    new[] { material },
                    new[] { family },
                    captureSchema,
                    AlphaPolicyBounds.Inert,
                    out var capturedRaw);
            Debug.Log(
                "[AMUSE-DBG] capturedOk=" + capturedList +
                " count=" + capturedRaw.Count);
            Assert.That(capturedRaw.Count, Is.EqualTo(1));
            var material0 = capturedRaw[0];

            var fields = UnityRendererAlphaAnalysis.GatherAlphaFields(
                capturedRaw, 4, 128);
            var setFlags = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;
            foreach (var field in typeof(AlphaFieldSet).GetFields(setFlags))
            {
                var value = field.GetValue(fields);
                if (value is System.Collections.IDictionary dict)
                {
                    Debug.Log(
                        $"[AMUSE-DBG] fields.{field.Name} ({dict.Count})");
                    foreach (var key in dict.Keys)
                    {
                        var keyFlags = System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance;
                        foreach (var kp in key.GetType().GetProperties(keyFlags))
                        {
                            if (kp.GetIndexParameters().Length > 0) continue;
                            try
                            {
                                Debug.Log(
                                    $"[AMUSE-DBG]   key.{kp.Name} = " +
                                    kp.GetValue(key, null));
                            }
                            catch (System.Exception) { }
                        }
                    }
                }
            }

            var analysisFlags = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static;
            var resolveFor = typeof(UnityRendererAlphaAnalysis).GetMethod(
                "ResolveFor", analysisFlags);
            var memoType = resolveFor.GetParameters()[3].ParameterType;
            var memo = Activator.CreateInstance(memoType);
            var resolution = resolveFor.Invoke(null, new[]
            {
                material0,
                (CapturedAlphaMaterialSemanticsResolver)(
                    UnityMaterialSemantics.AnalyzeAlphaMaterial),
                fields,
                memo,
                0,
            });
            DumpResolution(resolution, "resolution", 0);

            var extraUvSets = new[]
            {
                new List<Vector2>(),
                new List<Vector2>(),
                new List<Vector2>(),
            };
            var outcome = UnityRendererAlphaAnalysis.Classify(
                new List<int> { 0, 1, 2 },
                new List<Vector3>
                {
                    new Vector3(0f, 0f, 0f),
                    new Vector3(1f, 0f, 0f),
                    new Vector3(0f, 1f, 0f),
                },
                new List<Vector2>
                {
                    new Vector2(0.05f, 0.05f),
                    new Vector2(0.2f, 0.05f),
                    new Vector2(0.05f, 0.2f),
                },
                resolution,
                extraUvSets);

            Debug.Log(
                "[AMUSE-DBG] classifyOutcome=" + outcome[0]);
        }

        private static void DumpResolution(
            object resolution,
            string path,
            int depth)
        {
            if (depth > 4)
            {
                return;
            }

            var flags = System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance;
            var type = resolution.GetType();
            var isUniform = (bool)type
                .GetField("_isUniform", flags).GetValue(resolution);
            var uniformOutcome = type
                .GetField("_uniformOutcome", flags).GetValue(resolution);
            var isProduct = (bool)type
                .GetField("_isProduct", flags).GetValue(resolution);
            var isDisjunction = (bool)type
                .GetField("_isDisjunction", flags).GetValue(resolution);
            Debug.Log(
                $"[AMUSE-DBG] {path}: isUniform={isUniform} " +
                $"outcome={uniformOutcome} product={isProduct} " +
                $"or={isDisjunction}");
            var chain = type.GetField("_chain", flags)
                .GetValue(resolution);
            if (chain is AlphaMipChain mipChain)
            {
                var levels = (Array)typeof(AlphaMipChain)
                    .GetField("_levels", flags).GetValue(mipChain);
                for (var index = 0; index < levels.Length; index++)
                {
                    var level = levels.GetValue(index);
                    if (level == null)
                    {
                        continue;
                    }

                    var levelType = level.GetType();
                    var alpha8 = (byte[])levelType
                        .GetField("_alpha8", flags).GetValue(level);
                    var exact = 0;
                    foreach (var b in alpha8)
                    {
                        if (b == 255)
                        {
                            exact++;
                        }
                    }

                    Debug.Log(
                        $"[AMUSE-DBG] {path}.chain[{index}] " +
                        $"exact255={exact}/{alpha8.Length}");
                }
            }

            var mapping = type.GetField("_mapping", flags)
                .GetValue(resolution);
            if (mapping != null)
            {
                Debug.Log(
                    $"[AMUSE-DBG] {path}.mapping={mapping}");
            }

            var first = type.GetField("_firstFactor", flags)
                .GetValue(resolution);
            var second = type.GetField("_secondFactor", flags)
                .GetValue(resolution);
            if (first != null)
            {
                DumpResolution(first, path + ".first", depth + 1);
            }

            if (second != null)
            {
                DumpResolution(second, path + ".second", depth + 1);
            }
        }
    }
}
