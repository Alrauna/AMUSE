using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Layer 2 of mutation safety, as a test rather than a promise: the research
    /// package's production source may not name a mutating, importing,
    /// persisting, or transmitting API.
    /// <para>
    /// Tests/ is deliberately out of scope. A calibration case has to set a
    /// property block to construct the very refusal it measures, and a test has
    /// to destroy the objects it created.
    /// </para>
    /// <para>
    /// Synthetic cases in this class are plain source strings. The check reads
    /// each string. No string is compiled or run.
    /// </para>
    /// </summary>
    public sealed class ResearchSourceApiBanTests
    {
        /// <summary>
        /// Read-only AssetDatabase lookups are permitted; everything else on
        /// AssetDatabase is not. The blanket ban the harness design first wrote
        /// was broader than the mutation-safety concern that motivated it, and
        /// tier 1 loses its entire purpose without asset identity.
        /// </summary>
        private static readonly string[] AllowedAssetDatabaseMembers =
        {
            "AssetDatabase.GetAssetPath",
            "AssetDatabase.AssetPathToGUID",
        };

        /// <summary>
        /// Banned names as literal substrings. A literal match is
        /// case-sensitive and reads raw source text with no parsing. A literal
        /// names an API. It does not judge behavior. An indirect, wrapped, or
        /// renamed call passes unseen. These checks form a tripwire for
        /// accidental misuse. They are not a sandbox and not a proof against
        /// hostile code.
        /// </summary>
        private static readonly string[] BannedLiterals =
        {
            // Unity mutation and import.
            "AssetImporter",
            "TextureImporter",
            "ModelImporter",
            "EditorUtility.SetDirty",
            "Undo.",
            "PrefabUtility.",
            "EditorSceneManager.Save",
            "SetPropertyBlock",
            ".isReadable =",
            "Texture2D.Apply",
            "Object.Destroy",

            // Direct persistence. The FileAccess and FileMode names cover a
            // stream opened for writing in every spelled-out form.
            // FileMode.Create also matches FileMode.CreateNew. File.WriteAll
            // and File.AppendAll cover their method families. File.Copy and
            // File.Move relocate file content. The one stream form that stays
            // unseen is FileMode.Open with no FileAccess argument.
            "File.WriteAll",
            "File.AppendAll",
            "File.AppendText",
            "File.Move",
            "File.Copy",
            "new StreamWriter",
            "FileAccess.Write",
            "FileAccess.ReadWrite",
            "FileMode.Create",
            "FileMode.Truncate",
            "FileMode.Append",
            "FileMode.OpenOrCreate",
            "File.Create(",
            "File.OpenWrite",

            // Preference and session writes. The bare Set name covers every
            // value type.
            "PlayerPrefs.Set",
            "EditorPrefs.Set",
            "SessionState.Set",

            // Transmission.
            "PostAsync",
            "PutAsync",
            "SendAsync",
            "SendWebRequest",
            ".Send(",
            ".SendTo(",
        };

        /// <summary>
        /// The instantiating property reads, matched on a word boundary. As a
        /// bare substring ".material" also matches ".materialSlotIndex", and a
        /// scan that cries wolf gets weakened or deleted; the boundary makes it
        /// match the accident and not the field. It correspondingly does not
        /// match ".sharedMaterials" or ".sharedMesh", which is exactly the
        /// distinction this layer exists to draw.
        /// </summary>
        private static readonly string[] BannedPatterns =
        {
            @"\.material\b",
            @"\.materials\b",
            @"\.mesh\b",
        };

        /// <summary>
        /// The one source check behind both scans. It reports the banned
        /// literals, the banned patterns, and every AssetDatabase member
        /// outside the read-only allowlist. The production scan and the
        /// synthetic snippet cases run the same list.
        /// </summary>
        private static IReadOnlyList<string> FindOffences(string source)
        {
            var offences = new List<string>();

            foreach (var banned in BannedLiterals)
            {
                if (source.Contains(banned))
                {
                    offences.Add(banned);
                }
            }

            foreach (var pattern in BannedPatterns)
            {
                if (Regex.IsMatch(source, pattern))
                {
                    offences.Add(pattern);
                }
            }

            foreach (Match match in Regex.Matches(source, @"AssetDatabase\.\w+"))
            {
                var allowed = false;
                foreach (var permitted in AllowedAssetDatabaseMembers)
                {
                    if (match.Value == permitted)
                    {
                        allowed = true;
                    }
                }

                if (!allowed)
                {
                    offences.Add(match.Value);
                }
            }

            return offences;
        }

        private static string ProductionRoot()
        {
            return Path.GetFullPath(
                "Packages/com.alrauna.amuse.research/Editor");
        }

        [Test]
        public void ProductionSourceNamesNoMutatingApi()
        {
            var root = ProductionRoot();
            Assert.That(
                Directory.Exists(root), Is.True,
                "Research package Editor source not found at " + root);

            var files =
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

            // A mis-globbed path must fail rather than pass vacuously.
            Assert.That(
                files.Length, Is.GreaterThan(0),
                "No source files scanned; the scan proved nothing.");

            var offences = new List<string>();
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var name = Path.GetFileName(file);

                foreach (var offence in FindOffences(text))
                {
                    offences.Add(name + ": " + offence);
                }
            }

            CollectionAssert.IsEmpty(offences);
        }

        [Test]
        public void ProductionSourceHoldsNoCalibrationOrSeamType()
        {
            // Review change 2, asserted rather than promised. The semantics seam
            // is one internal pass-through parameter on
            // RendererObservationBuilder.Build and nothing else; a type that
            // exists only to be called by a test does not belong in production.
            var root = ProductionRoot();

            var offences = new List<string>();
            var carriers = new List<string>();
            foreach (var file in Directory.GetFiles(
                         root, "*.cs", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.IndexOf(
                        "Calibration",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    offences.Add(name);
                }

                if (File.ReadAllText(file)
                    .Contains("ResearchMaterialSemanticsProvider"))
                {
                    carriers.Add(Path.GetFileName(file));
                }
            }

            CollectionAssert.IsEmpty(offences);
            CollectionAssert.AreEqual(
                new[] { "RendererObservationBuilder.cs" }, carriers);
        }

        /// <summary>
        /// One source snippet with the verdict it must earn. A snippet is a
        /// plain string. No snippet is compiled or run.
        /// </summary>
        private readonly struct SourceCase
        {
            public SourceCase(
                string name, string source, string expectedOffence)
            {
                Name = name;
                Source = source;
                ExpectedOffence = expectedOffence;
            }

            public string Name { get; }

            public string Source { get; }

            public string ExpectedOffence { get; }
        }

        /// <summary>
        /// Direct persistence and transmission forms that break the read-only
        /// promise. ExpectedOffence names the fixed check that must report the
        /// form. In the red phase no fixed check knows these forms yet, so each
        /// case fails until the checks learn it.
        /// </summary>
        private static readonly SourceCase[] ForbiddenCases =
        {
            new SourceCase(
                "file write all text",
                "File.WriteAllText(destinationPath, body);",
                "File.WriteAll"),
            new SourceCase(
                "file append all text",
                "File.AppendAllText(logPath, line);",
                "File.AppendAll"),
            new SourceCase(
                "file append text",
                "var log = File.AppendText(logPath);",
                "File.AppendText"),
            new SourceCase(
                "file move",
                "File.Move(sourcePath, destinationPath);",
                "File.Move"),
            new SourceCase(
                "file copy",
                "File.Copy(sourcePath, destinationPath);",
                "File.Copy"),
            new SourceCase(
                "stream writer",
                "var writer = new StreamWriter(destinationPath, false);",
                "new StreamWriter"),
            new SourceCase(
                "writable file stream",
                "var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write);",
                "FileAccess.Write"),
            new SourceCase(
                "file stream create mode",
                "var stream = new FileStream(destinationPath, FileMode.Create);",
                "FileMode.Create"),
            new SourceCase(
                "file stream read write",
                "var stream = new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite);",
                "FileAccess.ReadWrite"),
            new SourceCase(
                "player preference write",
                "PlayerPrefs.SetString(preferencesKey, storedValue);",
                "PlayerPrefs.Set"),
            new SourceCase(
                "editor preference write",
                "EditorPrefs.SetString(preferencesKey, storedValue);",
                "EditorPrefs.Set"),
            new SourceCase(
                "session state write",
                "SessionState.SetString(stateKey, storedValue);",
                "SessionState.Set"),
            new SourceCase(
                "http post",
                "var response = await httpClient.PostAsync(requestUri, content);",
                "PostAsync"),
            new SourceCase(
                "http put",
                "var response = await httpClient.PutAsync(requestUri, content);",
                "PutAsync"),
            new SourceCase(
                "http send",
                "var response = await httpClient.SendAsync(request, cancellationToken);",
                "SendAsync"),
            new SourceCase(
                "unity web request send",
                "var operation = webRequest.SendWebRequest();",
                "SendWebRequest"),
            new SourceCase(
                "socket send",
                "socket.Send(payload, payload.Length, SocketFlags.None);",
                ".Send("),
            new SourceCase(
                "socket send to",
                "socket.SendTo(payload, remoteEndPoint);",
                ".SendTo("),
        };

        /// <summary>
        /// Read-only and lookup forms the promise permits. They must stay
        /// offence-free as the fixed checks grow. The explicit read-only file
        /// stream guards against an over-broad writable-stream check.
        /// </summary>
        private static readonly SourceCase[] AllowedCases =
        {
            new SourceCase(
                "file read all text",
                "var body = File.ReadAllText(sourcePath);",
                null),
            new SourceCase(
                "file open read",
                "var stream = File.OpenRead(sourcePath);",
                null),
            new SourceCase(
                "path combine",
                "var combinedPath = Path.Combine(directoryName, fileName);",
                null),
            new SourceCase(
                "read-only file stream",
                "var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);",
                null),
            new SourceCase(
                "asset path lookup",
                "var assetPath = AssetDatabase.GetAssetPath(sourceObject);",
                null),
            new SourceCase(
                "asset guid lookup",
                "var guid = AssetDatabase.AssetPathToGUID(assetPath);",
                null),
        };

        [Test]
        public void EachForbiddenSnippetEarnsItsNamedOffence()
        {
            var misses = new List<string>();
            foreach (var forbiddenCase in ForbiddenCases)
            {
                var reported = FindOffences(forbiddenCase.Source);
                var found = false;
                foreach (var offence in reported)
                {
                    if (offence == forbiddenCase.ExpectedOffence)
                    {
                        found = true;
                    }
                }

                if (!found)
                {
                    misses.Add(
                        forbiddenCase.Name + " expected " +
                        forbiddenCase.ExpectedOffence + " but found [" +
                        string.Join(", ", reported) + "]");
                }
            }

            Assert.That(misses, Is.Empty);
        }

        [Test]
        public void EachAllowedSnippetStaysOffenceFree()
        {
            var offences = new List<string>();
            foreach (var allowedCase in AllowedCases)
            {
                foreach (var offence in FindOffences(allowedCase.Source))
                {
                    offences.Add(allowedCase.Name + ": " + offence);
                }
            }

            Assert.That(offences, Is.Empty);
        }
    }
}
