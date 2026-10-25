using System;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// Pins the vendor opt-in gate contract: the two documented
    /// variable names, the named skip for a variable that is not 1,
    /// and the pass-through when the variable equals 1. Both tests
    /// that touch the environment save the variable first and
    /// restore it in a finally block.
    /// </summary>
    public sealed class VendorOptInGateTests
    {
        [Test]
        public void GateConstantsMatchTheDocumentedVariableNames()
        {
            Assert.That(
                VendorOptInGate.RealThryLock,
                Is.EqualTo("AMUSE_REAL_THRY_LOCK"));
            Assert.That(
                VendorOptInGate.DaoIntegration,
                Is.EqualTo("AMUSE_DAO_INTEGRATION"));
        }

        [Test]
        public void RequireIgnoresWithANamedReasonWhenTheVariableIsUnset()
        {
            var variable = VendorOptInGate.DaoIntegration;
            var saved = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
            try
            {
                const string disclosure =
                    "Set the variable to 1 in a disposable public"
                    + " integration project to run the gated test.";
                var thrown = Assert.Throws<IgnoreException>(
                    () => VendorOptInGate.Require(variable, disclosure));
                Assert.That(
                    thrown.Message,
                    Does.Contain(variable),
                    "the skip must name the opt-in variable");
                Assert.That(
                    thrown.Message,
                    Does.Contain(disclosure),
                    "the skip must carry the site disclosure");
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, saved);
            }
        }

        [Test]
        public void RequireReturnsWhenTheVariableIsOne()
        {
            var variable = VendorOptInGate.RealThryLock;
            var saved = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, "1");
            try
            {
                Assert.DoesNotThrow(
                    () => VendorOptInGate.Require(
                        variable, "a disclosure the pass-through never uses"));
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, saved);
            }
        }
    }
}
