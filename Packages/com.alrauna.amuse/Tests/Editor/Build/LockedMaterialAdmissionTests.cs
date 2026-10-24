using System;
using System.Collections.Generic;
using NUnit.Framework;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The pure admission decision for one locked material (A9, A10).
    /// Every test builds the aligned record by hand and passes raw
    /// material binding names. No Unity object takes part. Each
    /// falsifier carries a no-op guard that proves the same source
    /// without the binding admits.
    /// </summary>
    public sealed class LockedMaterialAdmissionTests
    {
        // --- Falsifier 12: a decision that reads only the plain property
        // states misses a binding on a renamed name ---

        /// <summary>
        /// A float binding on a renamed name refuses, because the locked
        /// material reads the property under the renamed spelling only
        /// (A10.2).
        /// </summary>
        [Test]
        public void FloatBindingOfARenamedNameRefuses()
        {
            var source = Source(
                States(("_P", LockedPropertyState.Renamed)),
                renamed: Names(("_P", "_P_S")),
                suffix: "S");

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, Array.Empty<string>()),
                Is.EqualTo(AlphaSeparationSlotRefusal.None),
                "fixture guard: the same source without the binding " +
                "must admit");

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, new[] { "material._P_S" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialAnimationMismatch),
                "a float binding on a renamed name must refuse");
        }

        // --- Falsifier 13: a decision that trusts the saved value of a
        // baked property admits a binding on its plain name ---

        /// <summary>
        /// A float binding on the plain name of a baked property
        /// refuses, because the locked material ignores that name
        /// (A10.2).
        /// </summary>
        [Test]
        public void FloatBindingOfABakedPlainNameRefuses()
        {
            var source = Source(
                States(("_B", LockedPropertyState.Baked)));

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, Array.Empty<string>()),
                Is.EqualTo(AlphaSeparationSlotRefusal.None),
                "fixture guard: the same source without the binding " +
                "must admit");

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, new[] { "material._B" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialAnimationMismatch),
                "a float binding on a baked plain name must refuse");
        }

        // --- Falsifier 14: a decision that reads only float curves
        // misses an object reference curve on a renamed texture ---

        /// <summary>
        /// An object reference binding on the plain name of a renamed
        /// texture refuses. The raw binding name has the one shape the
        /// decision reads, so the rule covers object curves with the
        /// same strip and compare (A10.2).
        /// </summary>
        [Test]
        public void ObjectBindingOfARenamedTexturePlainNameRefuses()
        {
            var source = Source(
                States(("_T", LockedPropertyState.Renamed)),
                renamed: Names(("_T", "_T_S")),
                suffix: "S");

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, Array.Empty<string>()),
                Is.EqualTo(AlphaSeparationSlotRefusal.None),
                "fixture guard: the same source without the binding " +
                "must admit");

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, new[] { "material._T" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialAnimationMismatch),
                "an object reference binding on a renamed texture " +
                "plain name must refuse");
        }

        [Test]
        public void IndexedMaterialBindingIsRead()
        {
            var renamed = Source(
                States(("_P", LockedPropertyState.Renamed)),
                renamed: Names(("_P", "_P_S")),
                suffix: "S");
            Assert.That(
                LockedMaterialAdmission.Decide(
                    renamed, false, new[] { "material[1]._P_S" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialAnimationMismatch),
                "the indexed material form must strip and refuse like " +
                "the plain form");

            var live = Source(
                States(("_P", LockedPropertyState.Live)));
            Assert.That(
                LockedMaterialAdmission.Decide(
                    live, false, new[] { "material[1]._P" }),
                Is.EqualTo(AlphaSeparationSlotRefusal.None),
                "an indexed binding on a live property must pass, so " +
                "the refusal above comes from the strip and the state");
        }

        [Test]
        public void FloatBindingOfARenamedTextureScaleOffsetRefuses()
        {
            var source = Source(
                States(("_T", LockedPropertyState.Renamed)),
                renamed: Names(("_T", "_T_S")),
                suffix: "S");

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, new[] { "material._T_ST.x" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialAnimationMismatch),
                "the scale-offset name follows the renamed texture, so " +
                "the component suffix must strip first");
        }

        [Test]
        public void FloatBindingOfALivePropertyPasses()
        {
            var source = Source(
                States(
                    ("_L", LockedPropertyState.Live),
                    ("_LT", LockedPropertyState.Live)));

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source,
                    false,
                    new[]
                    {
                        "material._L",
                        "material._L.r",
                        "material._LT_ST.y",
                    }),
                Is.EqualTo(AlphaSeparationSlotRefusal.None),
                "a live property and the derived names of a live " +
                "texture must pass");
        }

        [Test]
        public void FloatBindingOfAnUnusedPlainNameRefuses()
        {
            var source = Source(
                States(("_U", LockedPropertyState.Unused)));

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, false, new[] { "material._U" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialAnimationMismatch),
                "the locked material ignores an unused property, so " +
                "the binding must refuse");
        }

        [Test]
        public void PropertyBlockWinsOverAnAnimationMismatch()
        {
            var source = Source(
                States(("_B", LockedPropertyState.Baked)));

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source, true, new[] { "material._B" }),
                Is.EqualTo(AlphaSeparationSlotRefusal
                    .LockedMaterialPropertyBlock),
                "the property block must win over the animation " +
                "mismatch");
        }

        [Test]
        public void ANameWithoutTheMaterialPrefixPasses()
        {
            var source = Source(
                States(("_B", LockedPropertyState.Baked)));

            Assert.That(
                LockedMaterialAdmission.Decide(
                    source,
                    false,
                    new[]
                    {
                        "m_Materials.Array.data[0]",
                        "_B",
                        "material",
                    }),
                Is.EqualTo(AlphaSeparationSlotRefusal.None),
                "only a material binding names a property of the " +
                "locked material");
        }

        private static AlignedLockedSource Source(
            IReadOnlyDictionary<string, LockedPropertyState> states,
            IReadOnlyDictionary<string, string> renamed = null,
            string suffix = null)
        {
            return new AlignedLockedSource(
                PoiyomiLockEra.Bundled9364,
                suffix,
                states,
                renamed ?? new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, int>(),
                new Dictionary<string, string>(),
                new IReadOnlyList<string>[]
                {
                    new[] { "OPTIMIZER_ENABLED" },
                },
                Array.Empty<LockedBlockRecord>(),
                "");
        }

        private static IReadOnlyDictionary<string, LockedPropertyState>
            States(params (string Name, LockedPropertyState State)[] entries)
        {
            var result = new Dictionary<string, LockedPropertyState>();
            foreach (var entry in entries)
            {
                result.Add(entry.Name, entry.State);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, string> Names(
            params (string From, string To)[] entries)
        {
            var result = new Dictionary<string, string>();
            foreach (var entry in entries)
            {
                result.Add(entry.From, entry.To);
            }

            return result;
        }
    }
}
