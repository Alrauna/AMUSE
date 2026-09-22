using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// The per-slot decal inert proof. An enabled poiyomi decal slot
    /// writes the chain alpha only inside its override-alpha block (pinned
    /// 9.3.64 source, vendor lines 22958 to 22978), so a slot with the
    /// override-alpha mode proven zero is an alpha identity. The audio
    /// link decal module writes the chain alpha through its own weight
    /// (vendor line 24833), so the weight is proven exactly zero on every
    /// non-forced alpha path.
    /// </summary>
    public sealed class PoiyomiDecalSlotAlphaTests : PoiyomiFixtureTestBase
    {
        private static readonly string[] SlotOverrideAlphaProperties =
        {
            "_DecalOverrideAlpha",
            "_DecalOverrideAlpha1",
            "_DecalOverrideAlpha2",
            "_DecalOverrideAlpha3",
        };

        private static readonly int[] OverrideAlphaModes =
            { 1, 2, 3, 4, 5, 6 };

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        // A material on the non-forced alpha path with the mask mode off,
        // so alpha is proven from _MainTex.a and/or _Color.a.
        private Material NonForcedMaterial()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            return material;
        }

        private Material MaterialWithDecalSlot(int slot, float mode)
        {
            var material = NonForcedMaterial();
            material.SetFloat(
                slot == 0 ? "_DecalEnabled" : "_DecalEnabled" + slot, 1f);
            material.SetFloat(SlotOverrideAlphaProperties[slot], mode);
            return material;
        }

        [Test]
        public void
            EnabledDecalSlot_ProvenInertOverrideAlpha_KeepsAlphaComplete()
        {
            // --- Falsifier 1: a plausible wrong implementation keeps the
            // committed blanket gate and refuses every enabled decal by
            // name. It fails this case.
            var material = MaterialWithDecalSlot(0, 0f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            EnabledDecalSlot_WithAnyOverrideAlphaMode_RefusesNamingTheMode(
                [ValueSource(nameof(OverrideAlphaModes))] int mode)
        {
            // --- Falsifier 2: a plausible wrong implementation refuses the
            // slot but names the enable float instead of the mode float.
            var material = MaterialWithDecalSlot(0, mode);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_DecalOverrideAlpha");
        }

        [Test]
        public void
            EnabledHigherSlot_WithOverrideAlphaMode_RefusesNamingThatSlot(
                [ValueSource(nameof(SlotOverrideAlphaProperties))]
                string overrideProperty)
        {
            // --- Falsifier 3: a plausible wrong implementation checks slot
            // zero only, or names slot zero's property for every slot.
            var slot = System.Array.IndexOf(
                SlotOverrideAlphaProperties, overrideProperty);
            var material = MaterialWithDecalSlot(slot, 1f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                overrideProperty);
        }

        [Test]
        public void
            DisabledDecalSlot_WithAnOverrideAlphaMode_DoesNotRefuse()
        {
            // --- Falsifier 4: a plausible wrong implementation requires
            // the inert proof even for a slot whose enable float is zero,
            // refusing materials the committed gate already admitted.
            var material = NonForcedMaterial();
            material.SetFloat("_DecalEnabled", 0f);
            material.SetFloat("_DecalOverrideAlpha", 3f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }

        [Test]
        public void
            AudioLinkDecalControlsAlpha_NonZero_RefusesNamingTheWeight()
        {
            // --- Falsifier 5: a plausible wrong implementation admits the
            // decal slots but leaves the audio link decal module's weight
            // ungated, claiming an alpha the module scales.
            var material = NonForcedMaterial();
            material.SetFloat("_ALDecalControlsAlpha", 0.5f);

            AssertUnsupportedOutput(
                Interpret(material),
                PoiyomiSemanticOutput.Alpha,
                PoiyomiSemanticDiagnosticCode.UnsupportedFeature,
                "_ALDecalControlsAlpha");
        }

        [Test]
        public void
            ForcedOpaque_EnabledDecalSlot_StillClaimsConstantOne()
        {
            // No-op guard: the forced short-circuit precedes the decal
            // proof, matching the vendor order where the forcing runs
            // after the decal block.
            var material = MaterialWithDecalSlot(0, 2f);
            material.SetFloat("_AlphaForceOpaque", 1f);

            AssertOutputComplete(
                Interpret(material), PoiyomiSemanticOutput.Alpha);
        }
    }
}
