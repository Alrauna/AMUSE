using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The session-state guard of the vendor suite. NDMF stores one
    /// SessionState bool per disabled plugin under the pinned key
    /// format. A test that disables a vendor plugin changes what every
    /// later test measures, so the guard owns the disable lifecycle:
    /// it saves the prior value when a fixture opens the scope, it
    /// restores that value on dispose, and it remembers every touched
    /// key with its entry value. The suite teardown asks the guard
    /// whether every touched key ended at its entry value, so a broken
    /// restore names its key while the offender still sits in the
    /// runner output.
    /// </summary>
    internal static class VendorSessionStateGuard
    {
        /// <summary>
        /// The pinned key prefix of NDMF's plugin disable mechanism.
        /// A key is this prefix followed by the plugin id.
        /// </summary>
        internal const string DisableKeyPrefix =
            "nadena.dev.ndmf.plugin-disabled.";

        /// <summary>
        /// The exact set of NDMF plugins the suite disables through the
        /// guard. The suite start fails when any of them is disabled
        /// before the first test. A new id joins this set only with its
        /// own scoping verdict, so the set stays exact.
        /// </summary>
        internal static readonly IReadOnlyList<string> KnownPluginIds =
            new[] { TransientUnlockTestKnobs.AaoPluginId };

        // Every key the suite touched this session, with the value the
        // key held when the guard first opened it.
        private static readonly Dictionary<string, bool> TouchedKeys =
            new Dictionary<string, bool>();

        internal static string DisableKeyOf(string pluginId)
        {
            return DisableKeyPrefix + pluginId;
        }

        internal static IEnumerable<string> KnownDisableKeys()
        {
            return KnownPluginIds.Select(DisableKeyOf);
        }

        /// <summary>
        /// Saves the prior value of the plugin-disable key, sets the
        /// key to true, and returns a scope whose dispose restores the
        /// saved value. Dispose is idempotent, so a teardown that
        /// restores twice stays a no-op the second time.
        /// </summary>
        internal static IDisposable DisablePluginForFixture(
            string pluginId)
        {
            return new DisableScope(DisableKeyOf(pluginId));
        }

        /// <summary>
        /// The suite-start check. It fails when any known plugin is
        /// disabled before the first test, because a disabled vendor
        /// plugin changes what the suite measures.
        /// </summary>
        internal static void AssertNoKnownPluginDisabled()
        {
            foreach (var key in VendorSuiteStartCheck.KnownDisableKeys())
            {
                if (SessionState.GetBool(key, false))
                {
                    Assert.Fail(
                        "The suite-start check found the NDMF"
                        + " plugin-disable key " + key + " set to true."
                        + " A disabled vendor plugin changes what the"
                        + " suite measures. Clear the key before the"
                        + " run.");
                }
            }
        }

        /// <summary>
        /// The suite-end tripwire. It fails naming the key, the current
        /// value, and the entry value when any touched key ended off
        /// its entry value.
        /// </summary>
        internal static void AssertAllTouchedKeysAtEntryValues()
        {
            foreach (var touched in TouchedKeys)
            {
                var current = SessionState.GetBool(touched.Key, false);
                if (current != touched.Value)
                {
                    Assert.Fail(
                        "The suite touched the NDMF plugin-disable key "
                        + touched.Key + ". Its current value " + current
                        + " differs from its entry value " + touched.Value
                        + ". A fixture left the key set. Restore the key"
                        + " in the same finally block that opened it.");
                }
            }
        }

        private sealed class DisableScope : IDisposable
        {
            private readonly string key;
            private readonly bool priorValue;
            private bool disposed;

            internal DisableScope(string key)
            {
                this.key = key;
                priorValue = SessionState.GetBool(key, false);
                if (!TouchedKeys.ContainsKey(key))
                {
                    TouchedKeys[key] = priorValue;
                }

                SessionState.SetBool(key, true);
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                SessionState.SetBool(key, priorValue);
            }
        }
    }
}
