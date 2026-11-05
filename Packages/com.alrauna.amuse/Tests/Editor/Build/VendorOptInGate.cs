using System;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The named-skip gate for vendor integration tests. A test that
    /// mutates vendor-owned project state calls <see cref="Require"/>
    /// with its opt-in variable and its disclosure. The gate stops the
    /// test through a named skip unless the variable equals 1, so an
    /// unset gate reads as a skip in the report instead of an
    /// inconclusive assumption.
    /// </summary>
    internal static class VendorOptInGate
    {
        internal const string RealThryLock = "AMUSE_REAL_THRY_LOCK";
        internal const string DaoIntegration = "AMUSE_DAO_INTEGRATION";
        internal const string ProductionOrderDispatch =
            "AMUSE_PRODUCTION_ORDER_DISPATCH";

        internal static void Require(string variable, string disclosure)
        {
            if (Environment.GetEnvironmentVariable(variable) == "1")
            {
                return;
            }

            Assert.Ignore(
                "The opt-in variable " + variable + " is not set to 1. "
                + disclosure);
        }
    }
}
