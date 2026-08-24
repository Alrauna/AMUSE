using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Host
{
    public sealed class UnityAnimationCharacterizationTests
    {
        private static GameObject BuildTwoSlotRenderer(out Material a, out Material b, out Mesh mesh)
        {
            var root = new GameObject("binding discovery root");
            var child = new GameObject("Body");
            child.transform.SetParent(root.transform);

            mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;

            a = new Material(Shader.Find("Standard"));
            b = new Material(Shader.Find("Standard"));
            renderer.sharedMaterials = new[] { a, b };
            return root;
        }

        private static GameObject BuildOneSlotRenderer(out Material a, out Mesh mesh)
        {
            var root = new GameObject("binding discovery root (one slot)");
            var child = new GameObject("Body");
            child.transform.SetParent(root.transform);

            mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;

            a = new Material(Shader.Find("Standard"));
            renderer.sharedMaterials = new[] { a };
            return root;
        }

        private static string[] GetSortedMaterialBindings(GameObject root, GameObject child)
        {
            return AnimationUtility
                .GetAnimatableBindings(child, root)
                .Select(binding => binding.propertyName)
                .Where(name => name.StartsWith("material", System.StringComparison.Ordinal))
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToArray();
        }

        [Test]
        public void UnityGeneratesTheMaterialBindingsWeParse()
        {
            var root = BuildTwoSlotRenderer(out var a, out var b, out var mesh);
            try
            {
                var child = root.transform.Find("Body").gameObject;

                var generated = AnimationUtility
                    .GetAnimatableBindings(child, root)
                    .Select(binding => binding.propertyName)
                    .ToArray();

                Assert.That(generated, Is.Not.Empty,
                    "GetAnimatableBindings returned nothing; discovery is " +
                    "unavailable and obligation 2 must stay unknown");

                var materialBindings = generated
                    .Where(name => name.StartsWith("material", System.StringComparison.Ordinal))
                    .OrderBy(name => name, System.StringComparer.Ordinal)
                    .ToArray();

                TestContext.WriteLine("generated material bindings:");
                foreach (var name in materialBindings)
                {
                    TestContext.WriteLine("  " + name);
                }

                Assert.That(materialBindings, Is.Not.Empty,
                    "Unity generated no material bindings; obligation 2 cannot be " +
                    "closed from this environment");

                // Observed on Unity 2022.3.22f1 (Standard shader, SkinnedMeshRenderer,
                // two shared materials): GetAnimatableBindings generates 132 distinct
                // "material.*" propertyName forms. Every one of these strings was read
                // verbatim from the TestContext output above.

                // Scalar form: "material.<PropertyName>", no suffix.
                Assert.That(materialBindings, Contains.Item("material._Cutoff"));
                Assert.That(materialBindings, Contains.Item("material._Metallic"));
                Assert.That(materialBindings, Contains.Item("material._Glossiness"));
                Assert.That(materialBindings, Contains.Item("material._BumpScale"));

                // Colour-component form: "material.<PropertyName>.<r|g|b|a>".
                Assert.That(materialBindings, Contains.Item("material._Color.r"));
                Assert.That(materialBindings, Contains.Item("material._Color.g"));
                Assert.That(materialBindings, Contains.Item("material._Color.b"));
                Assert.That(materialBindings, Contains.Item("material._Color.a"));
                Assert.That(materialBindings, Contains.Item("material._EmissionColor.r"));
                Assert.That(materialBindings, Contains.Item("material._EmissionColor.g"));
                Assert.That(materialBindings, Contains.Item("material._EmissionColor.b"));
                Assert.That(materialBindings, Contains.Item("material._EmissionColor.a"));

                // Vector-component form: "material.<PropertyName>.<x|y|z|w>", generated
                // for the Vector4-valued ST (tiling/offset), HDR, and TexelSize
                // properties Unity derives per texture property.
                Assert.That(materialBindings, Contains.Item("material._MainTex_ST.x"));
                Assert.That(materialBindings, Contains.Item("material._MainTex_ST.y"));
                Assert.That(materialBindings, Contains.Item("material._MainTex_ST.z"));
                Assert.That(materialBindings, Contains.Item("material._MainTex_ST.w"));
                Assert.That(materialBindings, Contains.Item("material._MainTex_TexelSize.x"));
                Assert.That(materialBindings, Contains.Item("material._MainTex_HDR.x"));

                // Slot targeting: NOT expressed at all. GetAnimatableBindings produced
                // no "material[1]." prefixed form, no separate binding set for the
                // second material slot, and — pinned by the committed comparison in
                // TheGeneratedMaterialBindingSetDoesNotVaryWithSlotCount below — the
                // identical 132-entry set is produced for a renderer carrying only one
                // material. The generated propertyName alone therefore carries no
                // information that lets a caller positively determine which material
                // slot a "material.*" binding targets.
                Assert.That(materialBindings.Any(name => name.StartsWith("material[", System.StringComparison.Ordinal)),
                    Is.False,
                    "expected no indexed material[n]. slot form; if this fails, " +
                    "Unity now expresses per-slot material bindings and obligation 2 " +
                    "must be re-evaluated");
                Assert.That(materialBindings.Length, Is.EqualTo(132));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void TheGeneratedMaterialBindingSetDoesNotVaryWithSlotCount()
        {
            var twoSlotRoot = BuildTwoSlotRenderer(out var a, out var b, out var twoSlotMesh);
            var oneSlotRoot = BuildOneSlotRenderer(out var c, out var oneSlotMesh);
            try
            {
                var twoSlotChild = twoSlotRoot.transform.Find("Body").gameObject;
                var oneSlotChild = oneSlotRoot.transform.Find("Body").gameObject;

                var twoSlotBindings = GetSortedMaterialBindings(twoSlotRoot, twoSlotChild);
                var oneSlotBindings = GetSortedMaterialBindings(oneSlotRoot, oneSlotChild);

                TestContext.WriteLine("one-slot material bindings (" + oneSlotBindings.Length + "):");
                foreach (var name in oneSlotBindings)
                {
                    TestContext.WriteLine("  " + name);
                }

                // This is the observation that grounds the conservative slot-targeting
                // branch above: if Unity generated a different binding set depending on
                // how many material slots the renderer has, that would be a distinct,
                // reportable finding, not something to paper over. It does not here —
                // the one-slot and two-slot sets are identical, confirming that the
                // "material.*" propertyName form carries no per-slot information.
                Assert.That(oneSlotBindings, Is.EqualTo(twoSlotBindings),
                    "the one-slot and two-slot material binding sets differ; the " +
                    "conservative slot-targeting conclusion above does not hold as " +
                    "recorded and obligation 2 must be re-evaluated");
            }
            finally
            {
                Object.DestroyImmediate(twoSlotRoot);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(twoSlotMesh);
                Object.DestroyImmediate(oneSlotRoot);
                Object.DestroyImmediate(c);
                Object.DestroyImmediate(oneSlotMesh);
            }
        }

        // STORAGE TEST ONLY. This pins that AnimationUtility round-trips the
        // property names we construct. It does NOT establish that Unity generates
        // or applies these forms -- UnityGeneratesTheMaterialBindingsWeParse does
        // that, and it is the only test that may close obligation 2.
        [Test]
        public void AnimationUtilityRoundTripsAConstructedMaterialBinding()
        {
            var clip = new AnimationClip();
            try
            {
                var binding = EditorCurveBinding.FloatCurve("Body", typeof(SkinnedMeshRenderer), "material._Color.r");
                var curve = new AnimationCurve(new Keyframe(0f, 1f));

                AnimationUtility.SetEditorCurve(clip, binding, curve);
                var roundTripped = AnimationUtility.GetEditorCurve(clip, binding);

                Assert.That(roundTripped, Is.Not.Null);
                Assert.That(roundTripped.keys.Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }
    }
}
