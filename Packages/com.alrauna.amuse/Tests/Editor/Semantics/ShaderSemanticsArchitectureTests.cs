using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    [TestFixture]
    internal sealed class ShaderSemanticsArchitectureTests
    {
        private static readonly Regex HexGuidRegex =
            new("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);
        private static readonly Regex HexSha256Regex =
            new("^[0-9a-fA-F]{64}$", RegexOptions.Compiled);

        [Test]
        public void SemanticsDirectoryContainsZeroRuntimeDataFiles()
        {
            var packageInfo = PackageInfo.FindForAssembly(
                typeof(UnityMaterialSemantics).Assembly);
            var packageRoot = packageInfo != null
                ? packageInfo.resolvedPath
                : Path.GetFullPath("Packages/com.alrauna.amuse");
            var semanticsDir = Path.Combine(packageRoot, "Editor", "Semantics");
            Assert.That(Directory.Exists(semanticsDir), Is.True,
                "Semantics directory must exist.");

            var forbiddenExtensions = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase)
            {
                ".json", ".yaml", ".yml", ".xml", ".txt"
            };

            var discoveredFiles = Directory.GetFiles(
                semanticsDir, "*.*", SearchOption.AllDirectories)
                .Where(p => forbiddenExtensions.Contains(Path.GetExtension(p)))
                .ToArray();

            Assert.That(discoveredFiles, Is.Empty,
                "Semantics directory must not contain runtime data files.");
        }

        [Test]
        public void PoiyomiPropertiesMatchShaderConventions()
        {
            var fields = typeof(PoiyomiProperties)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToArray();

            Assert.That(fields, Is.Not.Empty);

            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var value = (string)field.GetValue(null);
                Assert.That(value, Is.Not.Null.And.Not.Empty);
                Assert.That(value.StartsWith("_", StringComparison.Ordinal), Is.True,
                    $"Property '{field.Name}' value '{value}' must start with an underscore.");
                Assert.That(values.Add(value), Is.True,
                    $"Duplicate property value detected: '{value}' in field '{field.Name}'.");
            }
        }

        [Test]
        public void LilToonPropertiesMatchShaderConventions()
        {
            var fields = typeof(LilToonProperties)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
                .ToArray();

            Assert.That(fields.Length, Is.EqualTo(48),
                "LilToonProperties must contain exactly 48 centralized constants.");

            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                var value = (string)field.GetValue(null);
                Assert.That(value, Is.Not.Null.And.Not.Empty);
                Assert.That(value.StartsWith("_", StringComparison.Ordinal), Is.True,
                    $"Property '{field.Name}' value '{value}' must start with an underscore.");
                Assert.That(values.Add(value), Is.True,
                    $"Duplicate property value detected: '{value}' in field '{field.Name}'.");
            }
        }

        [Test]
        public void AttestationProfilesContainValidGuidsAndHashes()
        {
            // Verify LilToon source attestation constants
            Assert.That(HexGuidRegex.IsMatch(LilToonSourceAttestation.SupportedShaderGuid),
                Is.True, "LilToon SupportedShaderGuid must be a 32-character hex string.");
            Assert.That(HexGuidRegex.IsMatch(LilToonSourceAttestation.PassShaderGuid),
                Is.True, "LilToon PassShaderGuid must be a 32-character hex string.");

            // Verify Poiyomi source attestation constants
            Assert.That(HexGuidRegex.IsMatch(PoiyomiSourceAttestation.CanonicalShaderGuid),
                Is.True, "Poiyomi CanonicalShaderGuid must be a 32-character hex string.");
            Assert.That(HexGuidRegex.IsMatch(PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid),
                Is.True, "Poiyomi TwoPassCanonicalShaderGuid must be a 32-character hex string.");
            Assert.That(HexSha256Regex.IsMatch(PoiyomiSourceAttestation.CanonicalNormalizedSourceHash),
                Is.True, "Poiyomi CanonicalNormalizedSourceHash must be a 64-character hex string.");
            Assert.That(HexSha256Regex.IsMatch(PoiyomiSourceAttestation.TwoPassCanonicalNormalizedSourceHash),
                Is.True, "Poiyomi TwoPassCanonicalNormalizedSourceHash must be a 64-character hex string.");
        }
    }
}
