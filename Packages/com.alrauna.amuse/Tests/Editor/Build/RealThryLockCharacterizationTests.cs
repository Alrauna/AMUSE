using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Shared;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The real Thry lock characterization. The plain vendor type name
    /// inside this test assembly binds to the test stand-in while that
    /// stand-in exists, so every vendor member here resolves by
    /// assembly-qualified reflection against the installed vendor
    /// assembly only. A guard proves that binding before any call.
    /// <para>
    /// The suite runs the two cleanup-order tests always. It runs the
    /// real lock test only behind the opt-in gate, because the lock
    /// writes vendor-owned cache data under Assets/_LockedShaderCache
    /// and calls the vendor usage counter over the network. Set the
    /// gate only in a disposable project (V15).
    /// </para>
    /// <para>
    /// The teardown deletes the test's own locked materials before the
    /// test's own cache entries, because the vendor documents that a
    /// deleted cache entry breaks every other locked material that
    /// holds it (V13.2).
    /// </para>
    /// <para>
    /// The known vendor delta the comparison accepts is the bake. The
    /// real lock bakes every non-animated property into a constant.
    /// The emulated fixture partner keeps every property live. The
    /// aligner reads the bake as the Baked state, so the comparison
    /// accepts exactly Live-to-Baked and Unused-to-Baked and calls any
    /// other change a named difference. The bake value proof then
    /// parses the recorded literals of one float and one color back to
    /// the exact values the probe material carried before the lock,
    /// so a wrong-value bake cannot pass.
    /// </para>
    /// </summary>
    public sealed class RealThryLockCharacterizationTests
    {
        private const string VendorToolQualifiedName =
            "Thry.ThryEditor.ShaderOptimizer, ThryAssemblyDefinition";

        private const string VendorToolAssemblyName =
            "ThryAssemblyDefinition";

        private const string CacheRoot = "Assets/_LockedShaderCache";

        private const string ProbeFolderName = "AmuseTests_RealThryLock";

        private const string ProbeFragment = "RealThryLockProbe";

        private const string ProbeShaderName =
            "Hidden/Alrauna/AmuseTests/" + ProbeFragment;

        // The distinctive non-default values the probe material carries
        // before the lock. The bake value proof reads them back from
        // the recorded literals, so a wrong-value bake cannot pass.
        private const string BakedFloatPropertyName = "_ColorThemeIndex";

        private const float BakedFloatValue = 0.75f;

        private const string BakedColorPropertyName = "_Color";

        private static readonly Color BakedColorValue =
            new Color(0.25f, 0.5f, 0.75f, 1f);

        // The cleanup planner. It returns the deletion order. Every
        // material path precedes every cache entry path, because a
        // cache entry a still-referencing material holds breaks that
        // material when the entry is deleted (V13.2).
        internal static IReadOnlyList<string> ResidueCleanupPlan(
            IReadOnlyList<string> materialPaths,
            IReadOnlyList<string> cacheEntryPaths)
        {
            var plan = new List<string>(
                materialPaths.Count + cacheEntryPaths.Count);
            plan.AddRange(materialPaths);
            plan.AddRange(cacheEntryPaths);
            return plan;
        }

        // The cleanup apply. It computes the plan, invokes the deleter
        // once per path in plan order, and returns the plan. The
        // characterization teardown passes AssetDatabase.DeleteAsset.
        internal static IReadOnlyList<string> ResidueCleanupApply(
            IReadOnlyList<string> materialPaths,
            IReadOnlyList<string> cacheEntryPaths,
            Action<string> delete)
        {
            var plan = ResidueCleanupPlan(materialPaths, cacheEntryPaths);
            foreach (var path in plan)
            {
                delete(path);
            }

            return plan;
        }

        // --- Falsifier 8: a teardown that deletes a shared cache entry before the test's own materials breaks that material. ---
        [Test]
        public void ResidueCleanupPlanDeletesMaterialsBeforeCacheEntries()
        {
            var materials = ProbeMaterialPaths();
            var cacheEntries = ProbeCacheEntryPaths();

            var plan = ResidueCleanupPlan(materials, cacheEntries);

            // No-op guard: the plan must carry every input path exactly
            // once, so the order claim below is reached with real
            // content.
            Assert.That(
                plan.Count,
                Is.EqualTo(materials.Count + cacheEntries.Count),
                "fixture precondition: the plan holds every path once");
            var planned = new HashSet<string>(plan);
            Assert.That(
                planned.Count,
                Is.EqualTo(plan.Count),
                "fixture precondition: the plan holds no duplicate path");
            Assert.That(
                planned.SetEquals(materials.Concat(cacheEntries)),
                Is.True,
                "fixture precondition: the plan holds exactly the "
                + "planned paths");

            var lastMaterial = -1;
            var firstCacheEntry = int.MaxValue;
            for (var index = 0; index < plan.Count; index++)
            {
                if (materials.Contains(plan[index]))
                {
                    lastMaterial = Math.Max(lastMaterial, index);
                }

                if (cacheEntries.Contains(plan[index]))
                {
                    firstCacheEntry = Math.Min(firstCacheEntry, index);
                }
            }

            Assert.That(
                lastMaterial,
                Is.LessThan(firstCacheEntry),
                "every material path must precede every cache entry "
                + "path. A teardown that deletes a shared cache entry "
                + "first breaks the material that still holds it");
        }

        [Test]
        public void TeardownAppliesThePlannerOutputInOrder()
        {
            var materials = ProbeMaterialPaths();
            var cacheEntries = ProbeCacheEntryPaths();
            var deleted = new List<string>();

            var plan = ResidueCleanupApply(
                materials, cacheEntries, deleted.Add);

            Assert.That(
                deleted,
                Is.EqualTo(plan),
                "the teardown must invoke the deleter once per path in "
                + "plan order");
        }

        [Test]
        public void RealThryLockOutputAlignsThroughTheProductionAligner()
        {
            // The disposable-project guard: the real lock mutates
            // vendor-owned project state and calls the vendor usage
            // counter over the network, so it runs only when the caller
            // explicitly opted the project in.
            VendorOptInGate.Require(
                VendorOptInGate.RealThryLock,
                "Set " + VendorOptInGate.RealThryLock + "=1 in a"
                + " disposable project to run the real lock. The lock"
                + " writes vendor-owned cache data under " + CacheRoot
                + " and calls the vendor usage counter over the"
                + " network. It must never run in a project whose"
                + " state matters.");

            ResolveRealTool(
                out var lockMethod,
                out var unlockMethod,
                out var progressBarNone);
            var runDate = DateTime.UtcNow.ToString(
                "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // The emulated yardstick: the fixture pair for the shipped
            // original goes through the same aligner first, so the
            // comparison below measures the real lock against the
            // emulation through one aligner.
            _fixture = new LockedSourceFixture(ProbeFragment + "Emulated");
            var shippedText = ReadText(
                AssetDatabase.GetAssetPath(_fixture.Original));
            var partnerText = ReadText(
                AssetDatabase.GetAssetPath(_fixture.Partner));
            var shippedProperties =
                LockedSourceAlignmentMemo.PropertiesOf(
                    _fixture.Original, shippedText);
            var emulated = PoiyomiLockedSourceAlignment.Align(
                shippedText, partnerText, shippedProperties);
            Assert.That(
                emulated.Succeeded, Is.True,
                Dated("the emulated fixture pair refused the aligner",
                    runDate, emulated.RefusalDetail));
            Assert.That(
                emulated.Source, Is.Not.Null,
                Dated("the emulated alignment carries no source",
                    runDate, null));

            // The synthetic stand-in original carries the vendor lock
            // button attribute, because the vendor lock skips a shader
            // without it. Both assets land in the test temp folder, so
            // no generated asset lands in the vendor tree.
            EnsureFolder("Assets", ProbeFolderName);
            // The cleanup paths are armed before any write, so a throw
            // in the middle of the probe still leaves a teardown that
            // deletes every test asset.
            _materialPaths = ProbeMaterialPaths();
            var originalText = ProbeOriginalText(shippedText);
            var originalPath =
                "Assets/" + ProbeFolderName + "/"
                + ProbeFragment + "Original.shader";
            var originalShader = TestShaderWriter.WriteTestShader(
                originalPath, originalText + "\n");
            var material = new Material(originalShader)
            {
                name = ProbeFragment,
            };
            material.SetFloat(BakedFloatPropertyName, BakedFloatValue);
            material.SetColor(BakedColorPropertyName, BakedColorValue);
            var materialPath =
                "Assets/" + ProbeFolderName + "/"
                + ProbeFragment + ".mat";
            AssetDatabase.CreateAsset(material, materialPath);

            var properties = LockedSourceAlignmentMemo.PropertiesOf(
                originalShader, originalText);

            // The real lock. The reflection call passes both arguments
            // on every call, so the invocation reaches the vendor
            // overload the build path uses. The probe cache entry arms
            // before any assert, so a failed assert cannot strand it.
            var locked = (bool)lockMethod.Invoke(null, new object[]
            {
                new List<Material> { material },
                progressBarNone,
            });
            ArmProbeCacheEntryPaths();
            Assert.That(
                locked, Is.True,
                Dated("the vendor lock call returned false",
                    runDate, null));
            Assert.That(
                material.shader,
                Is.Not.EqualTo(originalShader),
                Dated("the lock left the material on its original "
                    + "shader. The lock must actually process the "
                    + "probe material", runDate, null));
            Assert.That(
                material.shader.name.StartsWith(
                    LockedMaterialIdentity.LockedShaderNamePrefix,
                    StringComparison.Ordinal),
                Is.True,
                Dated("the locked shader name does not carry the "
                    + "vendor locked prefix, got " + material.shader.name,
                    runDate, null));
            Assert.That(
                material.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(1f),
                Dated("the lock left the optimizer flag clear",
                    runDate, null));

            var lockedPath = AssetDatabase.GetAssetPath(material.shader);
            Assert.That(
                lockedPath.StartsWith(CacheRoot + "/",
                    StringComparison.Ordinal),
                Is.True,
                Dated("the locked shader asset does not live under the "
                    + "vendor cache root, got " + lockedPath,
                    runDate, null));
            var entryDirectory =
                Path.GetDirectoryName(lockedPath).Replace('\\', '/');
            var shaderDirectory =
                Path.GetDirectoryName(entryDirectory).Replace('\\', '/');
            // The test's own cache entries: the entry folder first, its
            // test-named parent folder second. Both die after every
            // material path. The armed scan must agree with the exact
            // entry path the locked shader names.
            Assert.That(
                _cacheEntryPaths,
                Does.Contain(entryDirectory),
                Dated("the armed cache entries miss the locked shader "
                    + "entry " + entryDirectory, runDate, null));
            Assert.That(
                _cacheEntryPaths,
                Does.Contain(shaderDirectory),
                Dated("the armed cache entries miss the probe shader "
                    + "folder " + shaderDirectory, runDate, null));

            var lockedText = ReadText(lockedPath);
            var real = PoiyomiLockedSourceAlignment.Align(
                originalText, lockedText, properties);
            Assert.That(
                real.Succeeded, Is.True,
                Dated("the real lock output refused the production "
                    + "aligner at original line " + real.OriginalLine
                    + " with text " + OriginalLineText(
                        originalText, real.OriginalLine),
                    runDate, real.RefusalDetail));
            Assert.That(
                real.Source, Is.Not.Null,
                Dated("the real alignment carries no source",
                    runDate, null));

            // The structural comparison through the aligner. The known
            // vendor delta is the bake: the real lock bakes every
            // non-animated property into a constant, while the
            // emulated partner keeps every property live. The aligner
            // reads that bake as the Baked state. Any other state
            // change is a named difference.
            Assert.That(
                real.Source.Era,
                Is.EqualTo(emulated.Source.Era),
                Dated("the real lock aligned in a different lock era",
                    runDate, null));
            Assert.That(
                real.Source.Suffix,
                Is.EqualTo(emulated.Source.Suffix),
                Dated("the real lock rename suffix differs from the "
                    + "emulation", runDate, null));
            Assert.That(
                real.Source.States.Count,
                Is.EqualTo(emulated.Source.States.Count),
                Dated("the real lock and the emulation disagree on the "
                    + "property count", runDate, null));
            var bakedProperties = 0;
            foreach (var pair in emulated.Source.States)
            {
                Assert.That(
                    real.Source.States.TryGetValue(
                        pair.Key, out var realState),
                    Is.True,
                    Dated("the real alignment carries no state for "
                        + "property " + pair.Key, runDate, null));
                var isKnownBake = realState == LockedPropertyState.Baked
                    && (pair.Value == LockedPropertyState.Live
                        || pair.Value == LockedPropertyState.Unused);
                if (isKnownBake)
                {
                    bakedProperties++;
                }

                Assert.That(
                    realState == pair.Value || isKnownBake,
                    Is.True,
                    Dated("the real lock and the emulation disagree on "
                        + "the state of property " + pair.Key
                        + ": emulation " + pair.Value + ", real "
                        + realState, runDate, null));
            }

            Assert.That(
                bakedProperties,
                Is.GreaterThan(0),
                Dated("the real lock baked no property, so the bake "
                    + "comparison proved nothing", runDate, null));
            Assert.That(
                real.Source.BakedLiterals.Count,
                Is.GreaterThanOrEqualTo(bakedProperties),
                Dated("the bake record count is below the baked state "
                    + "count", runDate, null));
            Assert.That(
                real.Source.RenamedNames.Count,
                Is.EqualTo(0),
                Dated("the real lock renamed properties on a probe "
                    + "without animated tags", runDate, null));

            // The bake value proof: the recorded literal must carry the
            // value the probe material held before the lock, never a
            // default and never another property's value.
            AssertBakedFloatLiteral(real.Source, runDate);
            AssertBakedColorLiteral(real.Source, runDate);

            // The real unlock. It must restore the original shader and
            // clear the flag, and it keeps the cache entry in place.
            var unlocked = (bool)unlockMethod.Invoke(null, new object[]
            {
                new List<Material> { material },
                progressBarNone,
            });
            Assert.That(
                unlocked, Is.True,
                Dated("the vendor unlock call returned false",
                    runDate, null));
            Assert.That(
                material.shader,
                Is.EqualTo(originalShader),
                Dated("the unlock did not restore the original shader",
                    runDate, null));
            Assert.That(
                material.GetFloat(
                    LockedMaterialIdentity.OptimizerEnabledPropertyName),
                Is.EqualTo(0f),
                Dated("the unlock left the optimizer flag set",
                    runDate, null));
        }

        [TearDown]
        public void TearDown()
        {
            // Delete exactly the planner output in order. Deletions run
            // before the asserts, so a failed assert cannot stop a
            // later deletion.
            if (_materialPaths != null && _cacheEntryPaths != null)
            {
                var plan = ResidueCleanupApply(
                    _materialPaths, _cacheEntryPaths,
                    path => AssetDatabase.DeleteAsset(path));
                AssertPlannedPathsGone(plan);
            }
            else if (_materialPaths != null)
            {
                foreach (var path in _materialPaths)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                AssertPlannedPathsGone(_materialPaths);
            }

            _materialPaths = null;
            _cacheEntryPaths = null;

            AssertNoProbeResidue();

            _fixture = null;
            LockedSourceFixture.DeleteFolder();
            LockedSourceAlignmentMemo.Clear();
        }

        private LockedSourceFixture _fixture;

        private IReadOnlyList<string> _materialPaths;

        private IReadOnlyList<string> _cacheEntryPaths;

        private static void AssertPlannedPathsGone(
            IReadOnlyList<string> plan)
        {
            foreach (var path in plan)
            {
                Assert.That(
                    Directory.Exists(path) || File.Exists(path),
                    Is.False,
                    "the teardown left residue at " + path);
            }
        }

        // The bake value proof for the float. The vendor prints float
        // literals in a fixed-decimal format, so the parse-back is
        // exact for the chosen value.
        private static void AssertBakedFloatLiteral(
            AlignedLockedSource source, string runDate)
        {
            Assert.That(
                source.BakedLiterals.ContainsKey(BakedFloatPropertyName),
                Is.True,
                Dated("the real lock baked no literal for "
                    + BakedFloatPropertyName, runDate, null));
            var literal = source.BakedLiterals[BakedFloatPropertyName];
            var baked = float.Parse(
                literal, NumberStyles.Float, CultureInfo.InvariantCulture);
            Assert.That(
                baked,
                Is.EqualTo(BakedFloatValue),
                Dated("the baked literal for " + BakedFloatPropertyName
                    + " is " + literal + ", which does not parse back to "
                    + BakedFloatValue.ToString(
                        CultureInfo.InvariantCulture), runDate, null));
        }

        // The bake value proof for the color. The vendor converts a
        // plain color to linear in linear color space and prints the
        // vector literal in a shortest float format, so the parse-back
        // uses a small tolerance. A default or a wrong value sits far
        // outside it.
        private static void AssertBakedColorLiteral(
            AlignedLockedSource source, string runDate)
        {
            Assert.That(
                source.BakedLiterals.ContainsKey(BakedColorPropertyName),
                Is.True,
                Dated("the real lock baked no literal for "
                    + BakedColorPropertyName, runDate, null));
            var literal = source.BakedLiterals[BakedColorPropertyName];
            var expected = BakedColorValue;
            if (PlayerSettings.colorSpace != ColorSpace.Gamma)
            {
                expected = expected.linear;
            }

            var prefix = "float4(";
            Assert.That(
                literal.StartsWith(prefix, StringComparison.Ordinal)
                    && literal.EndsWith(")", StringComparison.Ordinal),
                Is.True,
                Dated("the baked literal for " + BakedColorPropertyName
                    + " is " + literal, runDate, null));
            var inner = literal.Substring(
                prefix.Length, literal.Length - prefix.Length - 1);
            var parts = inner.Split(',');
            Assert.That(
                parts.Length,
                Is.EqualTo(4),
                Dated("the baked literal for " + BakedColorPropertyName
                    + " is " + literal, runDate, null));
            for (var index = 0; index < 4; index++)
            {
                var component = float.Parse(
                    parts[index], NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                Assert.That(
                    component,
                    Is.EqualTo(expected[index]).Within(1e-5f),
                    Dated("the baked literal for "
                        + BakedColorPropertyName + " is " + literal,
                        runDate, null));
            }
        }

        // Arms the probe cache entry for cleanup immediately after the
        // lock call returns, before any assert. The scan finds every
        // probe-named folder under the cache root and plans its child
        // entries first, the named folder last, so the teardown can
        // never strand the entry when an assert fails.
        private void ArmProbeCacheEntryPaths()
        {
            if (!Directory.Exists(CacheRoot))
            {
                return;
            }

            var paths = new List<string>();
            foreach (var shaderFolder in Directory.GetDirectories(
                CacheRoot, "*" + ProbeFragment + "*",
                SearchOption.TopDirectoryOnly))
            {
                paths.AddRange(Directory.GetDirectories(
                    shaderFolder).Select(
                    path => path.Replace('\\', '/')));
                paths.Add(shaderFolder.Replace('\\', '/'));
            }

            if (paths.Count > 0)
            {
                _cacheEntryPaths = paths;
            }
        }

        // The vendor binding rule: resolve the real tool by
        // assembly-qualified reflection. The guard asserts the resolved
        // type is non-null, its assembly is not this test assembly, and
        // its assembly name is the vendor editor assembly name, so a
        // resolution to the test stand-in can never pass.
        private static void ResolveRealTool(
            out MethodInfo lockMethod,
            out MethodInfo unlockMethod,
            out object progressBarNone)
        {
            var tool = Type.GetType(VendorToolQualifiedName, false);
            Assert.That(
                tool, Is.Not.Null,
                "the installed vendor editor assembly must expose the "
                + "lock tool type. A null resolution means the vendor "
                + "package is missing or its assembly name changed");

            Assert.That(
                tool.Assembly,
                Is.Not.EqualTo(typeof(RealThryLockCharacterizationTests)
                    .Assembly),
                "the resolution reached the test stand-in. The "
                + "characterization must bind the vendor assembly, "
                + "never the stand-in");

            Assert.That(
                tool.Assembly.GetName().Name,
                Is.EqualTo(VendorToolAssemblyName),
                "the resolved assembly must be the vendor editor "
                + "assembly, got " + tool.Assembly.GetName().Name);

            var progressBarType = tool.GetNestedType("ProgressBar");
            Assert.That(
                progressBarType, Is.Not.Null,
                "the vendor nested progress bar enum must exist");

            lockMethod = tool.GetMethod(
                "LockMaterials",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(IEnumerable<Material>), progressBarType },
                null);
            unlockMethod = tool.GetMethod(
                "UnlockMaterials",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new Type[] { typeof(IEnumerable<Material>), progressBarType },
                null);
            Assert.That(
                lockMethod, Is.Not.Null,
                "the vendor lock member must exist with the material "
                + "list first and the progress bar second");
            Assert.That(
                unlockMethod, Is.Not.Null,
                "the vendor unlock member must exist with the material "
                + "list first and the progress bar second");

            // The None member of the vendor nested enum, resolved
            // through Enum.Parse on the reflected type. A locally
            // declared enum would never match the vendor overload.
            progressBarNone = Enum.Parse(progressBarType, "None");
        }

        private static IReadOnlyList<string> ProbeMaterialPaths()
        {
            return new[]
            {
                "Assets/" + ProbeFolderName + "/" + ProbeFragment + ".mat",
                "Assets/" + ProbeFolderName + "/"
                    + ProbeFragment + "Original.shader",
                "Assets/" + ProbeFolderName,
            };
        }

        private static IReadOnlyList<string> ProbeCacheEntryPaths()
        {
            return new[]
            {
                CacheRoot + "/" + ProbeFolderName + "/entryhash",
                CacheRoot + "/" + ProbeFolderName,
            };
        }

        // The probe original text: the shipped fixture text with the
        // vendor lock button attribute on the lock flag property and a
        // probe shader name. The shipped text itself carries no
        // attribute, so the probe must add it before the real lock
        // accepts the material.
        private static string ProbeOriginalText(string shippedText)
        {
            var lines = shippedText
                .Replace("\r", string.Empty)
                .Split('\n')
                .ToList();
            var namedShader = false;
            var attributedFlag = false;
            for (var index = 0; index < lines.Count; index++)
            {
                if (!namedShader && lines[index].StartsWith(
                        "Shader \"", StringComparison.Ordinal))
                {
                    lines[index] = "Shader \"" + ProbeShaderName + "\"";
                    namedShader = true;
                    continue;
                }

                if (!attributedFlag && lines[index].TrimStart()
                        .StartsWith("_ShaderOptimizerEnabled",
                            StringComparison.Ordinal))
                {
                    lines[index] =
                        "        [ThryShaderOptimizerLockButton] "
                        + "_ShaderOptimizerEnabled (\"Locked\", Float)"
                        + " = 0";
                    attributedFlag = true;
                    continue;
                }

                if (lines[index].Trim() == "fixed4 _Color;")
                {
                    // The float property needs its own program
                    // declaration, because a property the program never
                    // declares is an undeclared identifier at its use.
                    lines.Insert(
                        index + 1,
                        "            float "
                        + BakedFloatPropertyName + ";");
                    continue;
                }

                if (lines[index].Trim().StartsWith(
                        "return tex2D(", StringComparison.Ordinal))
                {
                    // The probe program gains one float use on the
                    // return line. A declaration line keeps property
                    // identifiers (fact 4), so the float use must sit
                    // on a non-declaration line for the bake to align.
                    lines[index] =
                        "                return tex2D(_MainTex, i.uv)"
                        + " * _Color * " + BakedFloatPropertyName + ";";
                    break;
                }
            }

            Assert.That(
                namedShader, Is.True,
                "fixture precondition: the shipped text carries a "
                + "shader line");
            Assert.That(
                attributedFlag, Is.True,
                "fixture precondition: the shipped text carries the "
                + "lock flag property");
            return string.Join("\n", lines);
        }

        private static void EnsureFolder(string parent, string folder)
        {
            var combined = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(combined))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static string ReadText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8)
                .Replace("\r", string.Empty);
        }

        // The text of one original line for a refusal message. An out of
        // range index names itself instead of throwing.
        private static string OriginalLineText(
            string originalText, int originalLine)
        {
            var lines = originalText.Split('\n');
            if (originalLine < 0 || originalLine >= lines.Length)
            {
                return "<out of range " + originalLine + ">";
            }

            var text = lines[originalLine].Trim();
            return text.Length == 0
                ? "<empty line>"
                : text;
        }

        private static string Dated(
            string detail, string runDate, string refusalDetail)
        {
            var message = detail + " on " + runDate;
            if (!string.IsNullOrEmpty(refusalDetail))
            {
                message += ": " + refusalDetail;
            }

            return message;
        }

        private static void AssertNoProbeResidue()
        {
            if (!Directory.Exists(CacheRoot))
            {
                return;
            }

            foreach (var entry in Directory.GetFileSystemEntries(
                CacheRoot, "*", SearchOption.AllDirectories))
            {
                Assert.That(
                    entry.IndexOf(
                        ProbeFragment, StringComparison.OrdinalIgnoreCase),
                    Is.LessThan(0),
                    "the teardown left test residue under the vendor "
                    + "cache: " + entry);
            }
        }
    }
}
