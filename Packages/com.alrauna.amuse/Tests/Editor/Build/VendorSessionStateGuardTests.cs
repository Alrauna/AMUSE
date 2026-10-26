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
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(
                    Path.Combine(root, "AMUSE one"), string.Empty);
                File.WriteAllText(
                    Path.Combine(root, "_AMUSE two"), string.Empty);
                File.WriteAllText(
                    Path.Combine(root, "Keep Me"), string.Empty);

                var planned = AmuseGeneratedOutputCleanup.Plan(root);

                // The underscore form is the test witness for NDMF's
                // reserved-name filter. The filter prefixes an
                // underscore when a name holds a Windows reserved
                // fragment anywhere, case insensitively. Current
                // fixture names hit it, for example a container whose
                // name contains the fragment inside the word control.
                Assert.That(
                    planned,
                    Is.EquivalentTo(new[] { "AMUSE one", "_AMUSE two" }),
                    "the plan must name exactly the two prefixed"
                    + " containers");

                AmuseGeneratedOutputCleanup.Delete(root);

                Assert.That(
                    File.Exists(Path.Combine(root, "AMUSE one")),
                    Is.False,
                    "the plain prefixed container must be gone");
                Assert.That(
                    File.Exists(Path.Combine(root, "_AMUSE two")),
                    Is.False,
                    "the underscore prefixed container must be gone");
                Assert.That(
                    File.Exists(Path.Combine(root, "Keep Me")),
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
