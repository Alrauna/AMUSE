using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the session-state guard of the vendor suite. A fixture that
    /// disables an NDMF plugin must restore the prior key value even
    /// when its own assertions throw, and the suite teardown must name
    /// every key a fixture left off its entry value. The cleanup test
    /// pins the generated-output delete set: only the two pinned name
    /// prefixes go, and every other file survives.
    /// </summary>
    public sealed class VendorSessionStateGuardTests
    {
        private static string DisableKeyOf(string pluginId)
        {
            // The pinned key format of NDMF's plugin disable mechanism.
            return "nadena.dev.ndmf.plugin-disabled." + pluginId;
        }

        // --- Falsifier 9: a fixture that restores its plugin-disable key after the assertions leaves the key set when an assertion throws. ---
        [Test]
        public void DisablePluginForFixtureRestoresTheKeyAfterTheActionThrows()
        {
            const string pluginId =
                "com.alrauna.amuse.tests.guard-restore-probe";
            var key = DisableKeyOf(pluginId);
            SessionState.SetBool(key, false);

            // The no-op guard: the probe must reach the inside of the
            // scope, so the restored key below proves the scope cleaned
            // up after the throw.
            var reachedTheScopeBody = false;
            Assert.That(
                () =>
                {
                    using (VendorSessionStateGuard
                        .DisablePluginForFixture(pluginId))
                    {
                        reachedTheScopeBody = true;
                        throw new InvalidOperationException(
                            "probe failure inside the fixture scope");
                    }
                },
                Throws.InvalidOperationException);

            Assert.That(
                reachedTheScopeBody, Is.True,
                "the probe must reach the code path inside the guard");
            Assert.That(
                SessionState.GetBool(key, false), Is.False,
                "the guard must restore the key after the scope ended"
                + " in a throw");
            Assert.DoesNotThrow(
                VendorSessionStateGuard.AssertAllTouchedKeysAtEntryValues,
                "a restored key must not fail the suite-end assertion");
        }

        [Test]
        public void AssertAllTouchedKeysAtEntryValuesNamesAnOffender()
        {
            const string pluginId =
                "com.alrauna.amuse.tests.guard-offender-probe";
            var key = DisableKeyOf(pluginId);
            SessionState.SetBool(key, false);

            using (VendorSessionStateGuard.DisablePluginForFixture(pluginId))
            {
                // The guard disabled the plugin and saved the entry
                // value false. The scope ends restored.
            }

            try
            {
                // Flip the key outside the guard, the way a fixture with
                // a broken restore would leave it.
                SessionState.SetBool(key, true);

                var thrown = Assert.Throws<AssertionException>(
                    VendorSessionStateGuard
                        .AssertAllTouchedKeysAtEntryValues);

                Assert.That(
                    thrown.Message, Does.Contain(key),
                    "the failure must name the touched key");
                Assert.That(
                    thrown.Message, Does.Contain("current value True"),
                    "the failure must name the current value");
                Assert.That(
                    thrown.Message, Does.Contain("entry value False"),
                    "the failure must name the entry value");
            }
            finally
            {
                SessionState.SetBool(key, false);
            }
        }

        [Test]
        public void GeneratedOutputCleanupRemovesOnlyAmuseContainers()
        {
            var root = Path.Combine(
                Path.GetTempPath(),
                "AmuseGeneratedOutputCleanupTests-"
                + Guid.NewGuid().ToString("N"));

            // The real NDMF layout. The asset saver writes one directory
            // per avatar directly under the generated root, named with
            // the filtered avatar name, one _assets folder inside it,
            // and the container asset files inside that. The suite
            // names its fixture avatars with the AMUSE prefix, so the
            // pinned prefixes land on the directory names. The
            // underscore form is the test witness for NDMF's
            // reserved-name filter. The filter prefixes an underscore
            // when a name holds a Windows reserved fragment anywhere,
            // case insensitively. Fixture names hit it, for example a
            // container whose name contains the fragment inside the
            // word control.
            Directory.CreateDirectory(
                Path.Combine(root, "AMUSE one", "_assets"));
            File.WriteAllText(
                Path.Combine(root, "AMUSE one", "_assets", "assets.asset"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(
                    root, "AMUSE one", "_assets", "assets.asset.meta"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(root, "AMUSE one", "AMUSE one.asset"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(root, "AMUSE one.meta"), string.Empty);
            Directory.CreateDirectory(
                Path.Combine(root, "_AMUSE two", "_assets"));
            File.WriteAllText(
                Path.Combine(root, "_AMUSE two", "_assets", "assets.asset"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(root, "_AMUSE two.meta"), string.Empty);
            Directory.CreateDirectory(
                Path.Combine(root, "Keep Me", "_assets"));
            File.WriteAllText(
                Path.Combine(root, "Keep Me", "_assets", "assets.asset"),
                string.Empty);
            File.WriteAllText(
                Path.Combine(root, "Keep Me.meta"), string.Empty);
            File.WriteAllText(
                Path.Combine(root, "Keep Standalone.asset"), string.Empty);

            try
            {
                var planned = AmuseGeneratedOutputCleanup.Plan(root);

                Assert.That(
                    planned,
                    Is.EquivalentTo(new[]
                    {
                        "AMUSE one",
                        "AMUSE one.meta",
                        "_AMUSE two",
                        "_AMUSE two.meta",
                    }),
                    "the plan must name exactly the prefixed container"
                    + " directories and their meta companions. The root"
                    + " asset inside a prefixed container dies with its"
                    + " container and needs no plan entry of its own.");

                AmuseGeneratedOutputCleanup.Delete(root);

                Assert.That(
                    Directory.Exists(Path.Combine(root, "AMUSE one")),
                    Is.False,
                    "the plain prefixed container directory must be"
                    + " gone");
                Assert.That(
                    Directory.Exists(Path.Combine(root, "_AMUSE two")),
                    Is.False,
                    "the underscore prefixed container directory must"
                    + " be gone");
                Assert.That(
                    File.Exists(Path.Combine(root, "AMUSE one.meta")),
                    Is.False,
                    "the meta companion of a deleted container must be"
                    + " gone");
                Assert.That(
                    File.Exists(Path.Combine(root, "_AMUSE two.meta")),
                    Is.False,
                    "the meta companion of an underscore container must"
                    + " be gone");
                Assert.That(
                    Directory.Exists(Path.Combine(root, "Keep Me")),
                    Is.True,
                    "a container directory outside the pinned prefixes"
                    + " must survive");
                Assert.That(
                    File.Exists(
                        Path.Combine(root, "Keep Me", "_assets",
                            "assets.asset")),
                    Is.True,
                    "the container asset of a surviving container must"
                    + " stay on disk");
                Assert.That(
                    File.Exists(Path.Combine(root, "Keep Me.meta")),
                    Is.True,
                    "the meta companion of a surviving container must"
                    + " stay on disk");
                Assert.That(
                    File.Exists(Path.Combine(root, "Keep Standalone.asset")),
                    Is.True,
                    "a file outside the pinned prefixes must survive");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
