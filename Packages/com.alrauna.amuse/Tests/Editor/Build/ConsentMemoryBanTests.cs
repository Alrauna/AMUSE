using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins decision V8 (T3): consent is never persisted. The consent-
    /// owned files may not touch a persistence store or any raw
    /// persistence API. EditorPrefs and PlayerPrefs had zero uses in
    /// product Editor source as of 2026-10-19, so those tokens are
    /// banned across the whole Editor tree. PlayModeBuildStore writes
    /// SessionState for build caching, including the declined-consent
    /// row, and is the named, justified carve-out outside the consent-
    /// owned scope. The scan asserts it saw the consent-owned files, so
    /// a rename or a drifted root fails instead of passing vacuously.
    /// </summary>
    public sealed class ConsentMemoryBanTests
    {
        private static readonly string[] ConsentOwnedFiles =
        {
            "VersionConsentDialog.cs",
            "AmusePlatformFinishPass.cs",
        };

        private static readonly string[] StoreTokens =
        {
            "SessionState.",
        };

        private static readonly string[] PrefsTokens =
        {
            "EditorPrefs.",
            "PlayerPrefs.",
        };

        private static readonly string[] RawPersistenceTokens =
        {
            "File.WriteAllText",
            "File.AppendAllText",
            "new FileStream",
        };

        [Test]
        public void ConsentOwnedFiles_NeverTouchAPersistenceStore()
        {
            var root = PackageInfo.FindForAssembly(typeof(ConsentMemoryBanTests).Assembly)
                .assetPath + "/Editor";
            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var seen = new HashSet<string>();
            var violations = new List<string>();

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                seen.Add(name);
                var text = File.ReadAllText(file);
                var isConsentOwned = ConsentOwnedFiles.Contains(name);

                foreach (var token in StoreTokens.Concat(PrefsTokens)
                             .Concat(RawPersistenceTokens))
                {
                    var bannedHere = isConsentOwned
                        || PrefsTokens.Contains(token);
                    if (bannedHere && text.Contains(token))
                    {
                        violations.Add(name + " -> " + token);
                    }
                }
            }

            Assert.That(seen, Is.SupersetOf(ConsentOwnedFiles),
                "the scan must have covered the consent-owned files, or " +
                "it proved nothing");
            Assert.That(violations, Is.Empty,
                "consent must never persist anywhere (decision V8)");
        }
    }
}
