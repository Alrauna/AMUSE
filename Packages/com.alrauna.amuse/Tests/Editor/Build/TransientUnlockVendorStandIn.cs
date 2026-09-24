using System.Collections.Generic;
using UnityEngine;

namespace Thry.ThryEditor
{
    /// <summary>
    /// Stand-in for the vendor lock tool type. The declaring full name
    /// matches the verified vendor type on purpose. A reintroduced vendor
    /// call resolves this type and counts here instead of failing
    /// silently. The suite asserts <c>LockCallCount</c> and
    /// <c>UnlockCallCount</c> stay zero. Those counters are the tripwire.
    /// No behavior is scripted. The method signatures mirror the
    /// live-verified vendor shapes: public static boolean, an
    /// IEnumerable of Material first. The removed scripting is
    /// recoverable from git history before the 2026-09-24 handover.
    /// </summary>
    internal static class ShaderOptimizer
    {
        public static bool LockMaterials(IEnumerable<Material> materials)
        {
            LockCallCount++;
            return true;
        }

        public static bool UnlockMaterials(IEnumerable<Material> materials)
        {
            UnlockCallCount++;
            return true;
        }

        internal static int LockCallCount;

        internal static int UnlockCallCount;

        internal static void Reset()
        {
            LockCallCount = 0;
            UnlockCallCount = 0;
        }
    }
}
