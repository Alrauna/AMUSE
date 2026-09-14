using System.Collections.Generic;
using Alrauna.Amuse.Editor.Host;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    /// <summary>
    /// Pins the Host materialization seam that turns a renderer's property
    /// blocks into per-slot effective materials. The copy schema is taken only
    /// from each shader's declared properties: renderer-wide entries land on
    /// every slot, per-material-index entries replace them per slot, undeclared
    /// keys never copy, and originals are read-only inputs.
    /// </summary>
    public sealed class EffectiveMaterialMaterializationTests
    {
        private const string FixtureShaderName =
            "Hidden/Alrauna/AmuseTests/PoiyomiSemanticTest";

        private readonly List<Object> _transient = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _transient)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _transient.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            _transient.Add(obj);
            return obj;
        }

        /// <summary>
        /// The stand-in Host fixture shader with two alpha-relevant serialized
        /// values moved off their declared defaults, so every case can tell a
        /// copied block entry from an inherited default.
        /// </summary>
        private Material NewFixtureMaterial()
        {
            var shader = Shader.Find(FixtureShaderName);
            Assert.That(
                shader, Is.Not.Null, $"'{FixtureShaderName}' must import.");
            var material = Track(new Material(shader));
            material.SetFloat("_AlphaForceOpaque", 0f);
            material.SetFloat("_Cutoff", 0.5f);
            return material;
        }

        private Renderer NewTwoSlotRenderer(Material first, Material second)
        {
            var gameObject = Track(new GameObject("amuse-materialization"));
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { first, second };
            return renderer;
        }

        private Material[] Materialize(Renderer renderer, out List<Material> clones)
        {
            var slots = renderer.sharedMaterials;
            var result = EffectiveMaterialMaterialization.Materialize(
                renderer, slots, out clones);
            foreach (var clone in clones)
            {
                Track(clone);
            }

            return result;
        }

        [Test]
        public void NoBlockReturnsTheSameArrayWithNoClones()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;

            var result = EffectiveMaterialMaterialization.Materialize(
                renderer, slots, out var clones);

            Assert.That(result, Is.SameAs(slots));
            Assert.That(clones, Is.Empty);
        }

        [Test]
        public void RendererWideFloatOverrideLandsOnEverySlotClone()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_AlphaForceOpaque", 1f);
            renderer.SetPropertyBlock(block);

            var result = EffectiveMaterialMaterialization.Materialize(
                renderer, slots, out var clones);

            Assert.That(clones.Count, Is.EqualTo(2));
            Assert.That(
                result[0], Is.Not.SameAs(slots[0]), "Slot 0 must materialize.");
            Assert.That(
                result[1], Is.Not.SameAs(slots[1]), "Slot 1 must materialize.");
            Assert.That(
                slots[0].GetFloat("_AlphaForceOpaque"), Is.EqualTo(0f),
                "The original material must keep its serialized value.");
            Assert.That(
                slots[1].GetFloat("_AlphaForceOpaque"), Is.EqualTo(0f));
            Assert.That(
                result[0].GetFloat("_AlphaForceOpaque"), Is.EqualTo(1f));
            Assert.That(
                result[1].GetFloat("_AlphaForceOpaque"), Is.EqualTo(1f));
        }

        [Test]
        public void PerIndexBlockOverridesRendererWide()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;
            var wide = new MaterialPropertyBlock();
            wide.SetFloat("_Cutoff", 0.5f);
            renderer.SetPropertyBlock(wide);
            var perIndex = new MaterialPropertyBlock();
            perIndex.SetFloat("_Cutoff", 0.25f);
            renderer.SetPropertyBlock(perIndex, 0);

            var result = Materialize(renderer, out var clones);

            Assert.That(clones.Count, Is.EqualTo(2));
            Assert.That(
                result[0].GetFloat("_Cutoff"), Is.EqualTo(0.25f),
                "The per-index entry must replace the renderer-wide one.");
            Assert.That(
                result[1].GetFloat("_Cutoff"), Is.EqualTo(0.5f),
                "Slot 1 must still see the renderer-wide entry.");
        }

        [Test]
        public void IntAndColorAndVectorAndTextureEntriesCopy()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;
            var overrideTexture = Track(
                new Texture2D(2, 2, TextureFormat.RGBA32, false));
            var block = new MaterialPropertyBlock();
            block.SetInt("_MainAlphaMaskMode", 1);
            block.SetColor("_Color", new Color(0.25f, 0.5f, 0.75f, 0.5f));
            block.SetVector("_MainTex_ST", new Vector4(2f, 3f, 0.1f, 0.2f));
            block.SetTexture("_MainTex", overrideTexture);
            renderer.SetPropertyBlock(block);

            var result = Materialize(renderer, out var clones);

            Assert.That(clones.Count, Is.EqualTo(2));
            foreach (var clone in result)
            {
                Assert.That(
                    clone.GetFloat("_MainAlphaMaskMode"), Is.EqualTo(1f));
                // A block color reaches the GPU in the working color space
                // and Material reads it back through the same conversion, so
                // the component round trip carries single-ULP error (measured
                // ~3e-8 on this editor; rendered output of block and clone
                // was pixel-identical). Assert within storage precision.
                var copied = clone.GetColor("_Color");
                Assert.That(copied.r, Is.EqualTo(0.25f).Within(1e-4f));
                Assert.That(copied.g, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(copied.b, Is.EqualTo(0.75f).Within(1e-4f));
                Assert.That(copied.a, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(
                    clone.GetTextureScale("_MainTex"), Is.EqualTo(new Vector2(2f, 3f)));
                Assert.That(
                    clone.GetTextureOffset("_MainTex"),
                    Is.EqualTo(new Vector2(0.1f, 0.2f)));
                Assert.That(
                    clone.GetTexture("_MainTex"), Is.SameAs(overrideTexture),
                    "The block's texture override must reach the clone by " +
                    "identity, not by a re-imported or rebuilt asset.");
            }
        }

        [Test]
        public void UndeclaredKeysChangeNothing()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;
            var block = new MaterialPropertyBlock();
            block.SetFloat("AMUSE_Undeclared_Key", 3f);
            renderer.SetPropertyBlock(block);

            var result = EffectiveMaterialMaterialization.Materialize(
                renderer, slots, out var clones);

            Assert.That(result, Is.SameAs(slots));
            Assert.That(clones, Is.Empty);
            Assert.That(
                slots[0].GetFloat("_AlphaForceOpaque"), Is.EqualTo(0f));
            Assert.That(slots[0].GetFloat("_Cutoff"), Is.EqualTo(0.5f));
            Assert.That(
                slots[1].GetFloat("_AlphaForceOpaque"), Is.EqualTo(0f));
            Assert.That(slots[1].GetFloat("_Cutoff"), Is.EqualTo(0.5f));
        }

        [Test]
        public void OriginalsAreNeverMutated()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;
            var beforeMainTex0 = slots[0].GetTexture("_MainTex");
            var beforeMainTex1 = slots[1].GetTexture("_MainTex");
            var wide = new MaterialPropertyBlock();
            wide.SetFloat("_AlphaForceOpaque", 1f);
            wide.SetColor("_Color", new Color(0.1f, 0.2f, 0.3f, 0.4f));
            wide.SetVector("_MainTex_ST", new Vector4(2f, 2f, 0.5f, 0.5f));
            wide.SetTexture("_MainTex", Track(
                new Texture2D(2, 2, TextureFormat.RGBA32, false)));
            renderer.SetPropertyBlock(wide);
            var perIndex = new MaterialPropertyBlock();
            perIndex.SetFloat("_Cutoff", 0.25f);
            perIndex.SetInt("_MainAlphaMaskMode", 0);
            renderer.SetPropertyBlock(perIndex, 1);

            Materialize(renderer, out _);

            foreach (var original in new[] { slots[0], slots[1] })
            {
                Assert.That(
                    original.GetFloat("_AlphaForceOpaque"), Is.EqualTo(0f));
                Assert.That(original.GetFloat("_Cutoff"), Is.EqualTo(0.5f));
                Assert.That(
                    original.GetFloat("_MainAlphaMaskMode"), Is.EqualTo(2f));
                Assert.That(
                    original.GetColor("_Color"), Is.EqualTo(Color.white));
                Assert.That(
                    original.GetTextureScale("_MainTex"),
                    Is.EqualTo(Vector2.one));
                Assert.That(
                    original.GetTextureOffset("_MainTex"),
                    Is.EqualTo(Vector2.zero));
            }

            Assert.That(
                slots[0].GetTexture("_MainTex"), Is.SameAs(beforeMainTex0));
            Assert.That(
                slots[1].GetTexture("_MainTex"), Is.SameAs(beforeMainTex1));
        }

        /// <summary>
        /// ShaderLab cannot declare a matrix property, so no declared-schema
        /// entry can ever carry one; a matrix block entry is ignored exactly
        /// like an undeclared key. This records that limitation by name.
        /// </summary>
        [Test]
        public void MatrixEntriesAreIgnoredBecauseNoShaderCanDeclareAMatrixProperty()
        {
            var first = NewFixtureMaterial();
            var second = NewFixtureMaterial();
            var renderer = NewTwoSlotRenderer(first, second);
            var slots = renderer.sharedMaterials;
            var block = new MaterialPropertyBlock();
            block.SetMatrix("AMUSE_Undeclared_Matrix", Matrix4x4.identity);
            renderer.SetPropertyBlock(block);

            var result = EffectiveMaterialMaterialization.Materialize(
                renderer, slots, out var clones);

            Assert.That(result, Is.SameAs(slots));
            Assert.That(clones, Is.Empty);
        }
    }
}
