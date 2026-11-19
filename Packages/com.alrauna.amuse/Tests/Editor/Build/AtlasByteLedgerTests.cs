using System;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class AtlasByteLedgerTests
    {
        [Test]
        public void InjectedAllocationAbovePredictionThrows()
        {
            var ledger = new AtlasByteLedger(100);

            ledger.Add(60);
            Assert.That(ledger.Current, Is.EqualTo(60));

            ledger.Add(40);
            Assert.That(
                ledger.Current,
                Is.EqualTo(100),
                "an allocation that reaches the prediction exactly"
                + " stays legal");

            ledger.Release(40);
            Assert.That(ledger.Current, Is.EqualTo(60));

            Assert.Throws<InvalidOperationException>(() => ledger.Add(41));
            Assert.That(
                ledger.Current,
                Is.EqualTo(60),
                "a refused allocation must not move the counter");
        }
    }
}
