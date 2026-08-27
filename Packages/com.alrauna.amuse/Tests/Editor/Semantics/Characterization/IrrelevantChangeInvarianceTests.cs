using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using Alrauna.Amuse.Tests.Editor.Semantics.LilToon;
using Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Characterization
{
    /// <summary>
    /// Architectural question — does irrelevant material state leak into
    /// semantic values?
    ///
    /// The property is stated over the whole <see cref="MaterialSemantics"/>
    /// using the semantic core's own structural equality, so a leak into any of
    /// the four outputs is caught, not just the one a hand-written assertion
    /// happened to look at. It also doubles as a gate-list coverage check: if a
    /// mutation below ever changes the result, either the property is genuinely
    /// read — in which case it does not belong on this list — or a gate list is
    /// reading something it should not.
    ///
    /// The property lists are short, hand-picked, and explicit. No generated
    /// sweep over the fixture shader is performed; that would be the
    /// combinatorial harness this milestone declines to build.
    /// </summary>
    public sealed class PoiyomiIrrelevantChangeInvarianceTests
        : PoiyomiFixtureTestBase
    {
        /// <summary>
        /// The render-state and masking properties the Poiyomi fixture exposes
        /// and that the interpreter never names: a UI preset selector, the
        /// cutout threshold, the two blend factors, and the alpha-mask texture
        /// slot (only its <c>_MainAlphaMaskMode</c> selector is read).
        ///
        /// PoiyomiBaseColorAlphaTests.RenderStateProperties_DoNotChangeAlpha
        /// already pins four of these against the Alpha equation alone. This
        /// generalizes the claim to every output at once, and adds the mask slot.
        /// </summary>
        private static readonly string[] IrrelevantFloats =
        {
            "_Mode",
            "_Cutoff",
            "_SrcBlend",
            "_DstBlend",

            // The remaining render state the fixture declares. The
            // opaque-conversion capability reads most of these, and none of
            // them may reach any semantic output: unknown or gate-failing
            // conversion state must refuse conversion only, never widen or
            // narrow ordinary alpha analysis.
            "_BlendOp",
            "_BlendOpAlpha",
            "_SrcBlendAlpha",
            "_DstBlendAlpha",
            "_AddBlendOp",
            "_AddSrcBlend",
            "_AddDstBlend",
            "_AddBlendOpAlpha",
            "_AddSrcBlendAlpha",
            "_AddDstBlendAlpha",
            "_ZWrite",
            "_ZTest",
            "_EnableOutlines",
            "_OutlineBlendOp",
            "_OutlineSrcBlend",
            "_OutlineDstBlend",
            "_OutlineBlendOpAlpha",
            "_OutlineSrcBlendAlpha",
            "_OutlineDstBlendAlpha",
        };

        private static PoiyomiSemanticResult Interpret(Material material)
        {
            return PoiyomiMaterialSemantics.InterpretVerifiedMaterial(
                material, ColorSpace.Linear);
        }

        private Material FullyProvenMaterial()
        {
            var material = NewFixtureMaterial();

            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_MainAlphaMaskMode", 0f);
            material.SetTexture("_MainTex", ImportTexture("inv_main"));
            material.SetTexture(
                "_BumpMap",
                ImportTexture(
                    "inv_bump",
                    i => i.textureType = TextureImporterType.NormalMap));
            material.SetFloat("_EnableEmission", 1f);
            material.SetTexture(
                "_EmissionMap",
                ImportTexture(
                    "inv_emis",
                    i => i.alphaSource = TextureImporterAlphaSource.None,
                    sourceHasAlpha: false));

            return material;
        }

        [Test]
        public void IrrelevantRenderState_LeavesEverySemanticOutputIdentical(
            [ValueSource(nameof(IrrelevantFloats))] string property)
        {
            var material = FullyProvenMaterial();
            var baseline = Interpret(material).Semantics;

            material.SetFloat(property, 3f);

            Assert.That(
                Interpret(material).Semantics,
                Is.EqualTo(baseline),
                $"Changing '{property}' altered the normalized semantics. It is "
                    + "either genuinely read — and does not belong on this list "
                    + "— or a gate list is reading it and should not.");
        }

        [Test]
        public void UnreadAlphaMaskSlot_LeavesEverySemanticOutputIdentical()
        {
            var material = FullyProvenMaterial();
            var baseline = Interpret(material).Semantics;

            // Only the _MainAlphaMaskMode selector is part of the alpha proof;
            // the mask texture slot itself is never sampled while the mode is 0.
            material.SetTexture("_AlphaMask", ImportTexture("inv_alphamask"));

            Assert.That(
                Interpret(material).Semantics,
                Is.EqualTo(baseline),
                "Assigning the unread _AlphaMask slot altered the semantics.");
        }
    }

    /// <summary>
    /// The lilToon fixture shader exposes <b>only</b> the property contract the
    /// interpreter consumes — every one of its 44 properties is named in
    /// production — so there is no unread property to mutate. The equivalent
    /// invariant for this frontend is therefore an irrelevant <em>change</em>
    /// rather than an irrelevant property: UV state must not be read when no
    /// texture is sampled through it.
    /// </summary>
    public sealed class LilToonIrrelevantChangeInvarianceTests
        : LilToonFixtureTestBase
    {
        [Test]
        public void MainUvState_WithNoMainTexture_LeavesSemanticsIdentical()
        {
            var material = NewFixtureMaterial();
            var baseline = Interpret(material).Semantics;

            // No _MainTex is assigned, so BaseColor is a constant and Normal is
            // Unmodified. Neither may consult the main transform.
            material.SetTextureScale("_MainTex", new Vector2(7f, 11f));
            material.SetTextureOffset("_MainTex", new Vector2(0.3f, 0.9f));

            Assert.That(
                Interpret(material).Semantics,
                Is.EqualTo(baseline),
                "Main UV state was read even though nothing samples through it.");
        }

        [Test]
        public void EmissionUvState_WithNoEmissionMap_LeavesSemanticsIdentical()
        {
            var material = NewFixtureMaterial();
            material.SetFloat("_UseEmission", 1f);
            var baseline = Interpret(material).Semantics;

            // Emission is enabled but unmapped, so it is a scaled constant and
            // must not consult the emission transform or channel selector.
            material.SetTextureScale("_EmissionMap", new Vector2(5f, 2f));
            material.SetTextureOffset("_EmissionMap", new Vector2(0.1f, 0.2f));
            material.SetFloat("_EmissionMap_UVMode", 3f);

            Assert.That(
                Interpret(material).Semantics,
                Is.EqualTo(baseline),
                "Emission UV state was read even though no map is sampled.");
        }

        [Test]
        public void BumpUvState_WithBumpMapDisabled_LeavesSemanticsIdentical()
        {
            var material = NewFixtureMaterial();
            var baseline = Interpret(material).Semantics;

            // _UseBumpMap is off, so the composed bump transform is never built.
            material.SetTextureScale("_BumpMap", new Vector2(4f, 4f));
            material.SetTextureOffset("_BumpMap", new Vector2(0.6f, 0.7f));

            Assert.That(
                Interpret(material).Semantics,
                Is.EqualTo(baseline),
                "Bump UV state was read even though no normal map is sampled.");
        }
    }
}
