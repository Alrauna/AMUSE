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

        // Fixture for the sampling observations below. It differs from
        // BuildTwoSlotRenderer in two ways that the sampling tests depend on:
        // an Animator on the root (without which AnimationMode applies no
        // object-reference curve at all -- see
        // MaterialSlotObjectCurveSamplingRequiresAnAnimatorOnTheSampledRoot),
        // and two materials whose serialized _Cutoff values differ, so that
        // "slot 0 changed", "both changed", and "neither changed" are three
        // visibly different observations.
        private static GameObject BuildSampledTwoSlotRenderer(
            out Material a, out Material b, out Mesh mesh, bool withAnimator)
        {
            var root = new GameObject("sampling root");
            if (withAnimator)
            {
                root.AddComponent<Animator>();
            }

            var child = new GameObject("Body");
            child.transform.SetParent(root.transform);

            mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
            var renderer = child.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;

            a = new Material(Shader.Find("Standard"));
            b = new Material(Shader.Find("Standard"));
            a.SetFloat("_Cutoff", 0.10f);
            b.SetFloat("_Cutoff", 0.90f);
            renderer.sharedMaterials = new[] { a, b };
            return root;
        }

        private static void AddControlCurve(AnimationClip clip)
        {
            // Mandatory control. Without it a "nothing changed" material
            // observation is vacuous -- indistinguishable from sampling never
            // having run at all.
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve("Body", typeof(Transform), "m_LocalScale.x"),
                AnimationCurve.Constant(0f, 1f, 3.5f));
        }

        [Test]
        public void StructuralBindingCategoriesAreDiscovered()
        {
            var root = BuildTwoSlotRenderer(out var a, out var b, out var mesh);
            try
            {
                var child = root.transform.Find("Body").gameObject;

                var generated = AnimationUtility.GetAnimatableBindings(child, root);

                var structural = generated
                    .Where(binding =>
                        binding.propertyName.StartsWith(
                            "m_Materials", System.StringComparison.Ordinal) ||
                        binding.propertyName == "m_Mesh")
                    .Select(binding => binding.propertyName + " => " + binding.type.Name +
                        " isPPtr=" + binding.isPPtrCurve)
                    .OrderBy(text => text, System.StringComparer.Ordinal)
                    .ToArray();

                TestContext.WriteLine("generated structural bindings:");
                foreach (var text in structural)
                {
                    TestContext.WriteLine("  " + text);
                }

                // Observed verbatim on Unity 2022.3.22f1, SkinnedMeshRenderer with
                // two Standard-shader materials. GetAnimatableBindings generates
                // exactly one object-reference binding per existing material slot
                // and nothing else structural: no m_Materials.Array.size binding,
                // and no m_Mesh binding (a SkinnedMeshRenderer's mesh is not
                // offered by GetAnimatableBindings at all in this environment).
                Assert.That(structural, Is.EqualTo(new[]
                {
                    "m_Materials.Array.data[0] => SkinnedMeshRenderer isPPtr=True",
                    "m_Materials.Array.data[1] => SkinnedMeshRenderer isPPtr=True",
                }));

                // The only PPtr curves generated for this renderer at all are the
                // two material-slot bindings above. This is the observation that
                // answers obligation 3 (see the texture test below).
                Assert.That(
                    generated.Where(binding => binding.isPPtrCurve)
                        .Select(binding => binding.propertyName)
                        .OrderBy(name => name, System.StringComparer.Ordinal)
                        .ToArray(),
                    Is.EqualTo(new[]
                    {
                        "m_Materials.Array.data[0]",
                        "m_Materials.Array.data[1]",
                    }));
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
        public void NoTextureReferenceObjectCurvesAreGeneratedForMaterials()
        {
            var root = BuildTwoSlotRenderer(out var a, out var b, out var mesh);
            try
            {
                var child = root.transform.Find("Body").gameObject;

                var materialPPtrBindings = AnimationUtility
                    .GetAnimatableBindings(child, root)
                    .Where(binding => binding.isPPtrCurve &&
                        binding.propertyName.StartsWith(
                            "material", System.StringComparison.Ordinal))
                    .Select(binding => binding.propertyName)
                    .ToArray();

                TestContext.WriteLine(
                    "material.* PPtr bindings: " + materialPPtrBindings.Length);
                foreach (var name in materialPPtrBindings)
                {
                    TestContext.WriteLine("  " + name);
                }

                // Obligation 3, settled: Unity generates NO object-reference curve
                // for a material's texture references. Texture assignment is
                // therefore not an animatable dimension here, and the admitted
                // state construction does not have to cover it.
                Assert.That(materialPPtrBindings, Is.Empty,
                    "Unity now generates material texture-reference object " +
                    "curves; texture assignment would become an admitted-state " +
                    "dimension the approved design does not cover, which is a " +
                    "design change and must be raised before proceeding");
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
        public void AuthoredMaterialArraySizeFloatCurveDoesNotChangeSlotCountWhenSampled()
        {
            var root = BuildSampledTwoSlotRenderer(
                out var a, out var b, out var mesh, withAnimator: true);
            var clip = new AnimationClip { name = "array size sampling" };
            try
            {
                var renderer = root.transform.Find("Body")
                    .GetComponent<SkinnedMeshRenderer>();
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "Body", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.size"),
                    AnimationCurve.Constant(0f, 1f, 1f));
                AddControlCurve(clip);

                int sampledCount;
                float control;
                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    try
                    {
                        AnimationMode.SampleAnimationClip(root, clip, 0f);
                        sampledCount = renderer.sharedMaterials.Length;
                        control = root.transform.Find("Body").localScale.x;
                    }
                    finally
                    {
                        AnimationMode.EndSampling();
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                TestContext.WriteLine("control m_LocalScale.x = " + control);
                TestContext.WriteLine("initial slot count = 2");
                TestContext.WriteLine("sampled slot count = " + sampledCount);
                TestContext.WriteLine("restored slot count = " +
                    renderer.sharedMaterials.Length);

                Assert.That(control, Is.EqualTo(3.5f).Within(1e-5f),
                    "the control curve did not take its animated value; sampling " +
                    "did not run and the array-size observation is void");
                Assert.That(sampledCount, Is.EqualTo(2),
                    "the authored array-size float curve unexpectedly changed " +
                    "the two-slot renderer and must be re-characterized");
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));

                // Discovery offers no m_Materials.Array.size binding, and the
                // deliberately authored float curve produced no sampled effect.
                // This negative does not establish which curve category, if any,
                // can carry a working array-size animation; that remains unobserved.
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void MaterialSlotObjectCurveActuallySwapsTheSlot()
        {
            var root = BuildSampledTwoSlotRenderer(
                out var a, out var b, out var mesh, withAnimator: true);
            var replacement = new Material(Shader.Find("Standard"));
            var clip = new AnimationClip { name = "slot swap effect" };
            try
            {
                var renderer = root.transform.Find("Body")
                    .GetComponent<SkinnedMeshRenderer>();
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "Body", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe { time = 0f, value = replacement },
                    });
                AddControlCurve(clip);

                Material[] sampled;
                float control;
                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    try
                    {
                        AnimationMode.SampleAnimationClip(root, clip, 0f);

                        // sharedMaterials, never materials: the latter instantiates
                        // copies and would corrupt the observation.
                        sampled = renderer.sharedMaterials;
                        control = root.transform.Find("Body").localScale.x;
                    }
                    finally
                    {
                        AnimationMode.EndSampling();
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                TestContext.WriteLine("control m_LocalScale.x = " + control);
                TestContext.WriteLine("sampled slot count = " + sampled.Length);
                TestContext.WriteLine("slot0 == replacement: " + (sampled[0] == replacement));
                TestContext.WriteLine("slot0 == a: " + (sampled[0] == a));
                TestContext.WriteLine("slot1 == b: " + (sampled[1] == b));
                TestContext.WriteLine("slot1 == replacement: " + (sampled[1] == replacement));

                Assert.That(control, Is.EqualTo(3.5f).Within(1e-5f),
                    "the control curve did not take its animated value; sampling " +
                    "did not run and the slot observation below is void");

                // Observed: an m_Materials.Array.data[0] object curve targets
                // exactly slot 0. Slot 1 is untouched and the slot count is
                // unchanged. Slot targeting by index IS positively established
                // for object-reference curves.
                Assert.That(sampled.Length, Is.EqualTo(2));
                Assert.That(sampled[0], Is.SameAs(replacement));
                Assert.That(sampled[1], Is.SameAs(b));

                // And it is a sampling-scope override only: state reverts.
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(a));
                Assert.That(renderer.sharedMaterials[1], Is.SameAs(b));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(replacement);
            }
        }

        // Harness precondition, recorded because the first observation of the
        // slot swap above was a false negative caused by its absence. With no
        // Animator on the sampled root, AnimationMode applies float curves but
        // silently applies NO object-reference curve. This is a property of the
        // EditMode sampling harness; it is not evidence about runtime behavior.
        [Test]
        public void MaterialSlotObjectCurveSamplingRequiresAnAnimatorOnTheSampledRoot()
        {
            var root = BuildSampledTwoSlotRenderer(
                out var a, out var b, out var mesh, withAnimator: false);
            var replacement = new Material(Shader.Find("Standard"));
            var clip = new AnimationClip { name = "slot swap without animator" };
            try
            {
                var renderer = root.transform.Find("Body")
                    .GetComponent<SkinnedMeshRenderer>();
                AnimationUtility.SetObjectReferenceCurve(
                    clip,
                    EditorCurveBinding.PPtrCurve(
                        "Body", typeof(SkinnedMeshRenderer),
                        "m_Materials.Array.data[0]"),
                    new[]
                    {
                        new ObjectReferenceKeyframe { time = 0f, value = replacement },
                    });
                AddControlCurve(clip);

                Material[] sampled;
                float control;
                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    try
                    {
                        AnimationMode.SampleAnimationClip(root, clip, 0f);

                        sampled = renderer.sharedMaterials;
                        control = root.transform.Find("Body").localScale.x;
                    }
                    finally
                    {
                        AnimationMode.EndSampling();
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                TestContext.WriteLine("no-animator control m_LocalScale.x = " + control);
                TestContext.WriteLine("no-animator slot0 == a: " + (sampled[0] == a));

                Assert.That(control, Is.EqualTo(3.5f).Within(1e-5f),
                    "float curves are applied without an Animator");
                Assert.That(sampled[0], Is.SameAs(a),
                    "object-reference curves are NOT applied without an Animator");
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void BareMaterialBindingAppliesViaARendererWideMaterialPropertyBlock()
        {
            var root = BuildSampledTwoSlotRenderer(
                out var a, out var b, out var mesh, withAnimator: true);
            var clip = new AnimationClip { name = "bare material binding effect" };
            try
            {
                var renderer = root.transform.Find("Body")
                    .GetComponent<SkinnedMeshRenderer>();

                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(
                        "Body", typeof(SkinnedMeshRenderer), "material._Cutoff"),
                    AnimationCurve.Constant(0f, 1f, 0.42f));
                AddControlCurve(clip);

                float control;
                float sampledSlot0;
                float sampledSlot1;
                bool identityHeld;
                bool hasBlock;
                bool wideEmpty;
                bool wideHasCutoff;
                float wideCutoff;
                bool index0Empty;
                bool index0HasCutoff;
                float index0Cutoff;
                bool index1Empty;
                bool index1HasCutoff;
                float index1Cutoff;

                AnimationMode.StartAnimationMode();
                try
                {
                    AnimationMode.BeginSampling();
                    try
                    {
                        AnimationMode.SampleAnimationClip(root, clip, 0f);

                        // Everything below is read INSIDE the sampling scope,
                        // before EndSampling and StopAnimationMode restore state.
                        control = root.transform.Find("Body").localScale.x;

                        var sampled = renderer.sharedMaterials;
                        identityHeld = sampled[0] == a && sampled[1] == b;
                        sampledSlot0 = sampled[0].GetFloat("_Cutoff");
                        sampledSlot1 = sampled[1].GetFloat("_Cutoff");

                        hasBlock = renderer.HasPropertyBlock();

                        var wide = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(wide);
                        wideEmpty = wide.isEmpty;
                        wideHasCutoff = wide.HasFloat("_Cutoff");
                        wideCutoff = wide.GetFloat("_Cutoff");

                        var index0 = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(index0, 0);
                        index0Empty = index0.isEmpty;
                        index0HasCutoff = index0.HasFloat("_Cutoff");
                        index0Cutoff = index0.GetFloat("_Cutoff");

                        var index1 = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(index1, 1);
                        index1Empty = index1.isEmpty;
                        index1HasCutoff = index1.HasFloat("_Cutoff");
                        index1Cutoff = index1.GetFloat("_Cutoff");
                    }
                    finally
                    {
                        AnimationMode.EndSampling();
                    }
                }
                finally
                {
                    AnimationMode.StopAnimationMode();
                }

                TestContext.WriteLine("control m_LocalScale.x = " + control);
                TestContext.WriteLine("slot identity preserved = " + identityHeld);
                TestContext.WriteLine("sharedMaterials[0]._Cutoff = " + sampledSlot0);
                TestContext.WriteLine("sharedMaterials[1]._Cutoff = " + sampledSlot1);
                TestContext.WriteLine("HasPropertyBlock = " + hasBlock);
                TestContext.WriteLine("renderer-wide block: isEmpty=" + wideEmpty +
                    " has _Cutoff=" + wideHasCutoff + " value=" + wideCutoff);
                TestContext.WriteLine("block[0]: isEmpty=" + index0Empty +
                    " has _Cutoff=" + index0HasCutoff + " value=" + index0Cutoff);
                TestContext.WriteLine("block[1]: isEmpty=" + index1Empty +
                    " has _Cutoff=" + index1HasCutoff + " value=" + index1Cutoff);
                TestContext.WriteLine("after StopAnimationMode: scale.x=" +
                    root.transform.Find("Body").localScale.x +
                    " a._Cutoff=" + a.GetFloat("_Cutoff") +
                    " b._Cutoff=" + b.GetFloat("_Cutoff") +
                    " HasPropertyBlock=" + renderer.HasPropertyBlock());

                // The control is load-bearing: without it a "no material change"
                // reading would be indistinguishable from sampling never running.
                Assert.That(control, Is.EqualTo(3.5f).Within(1e-5f),
                    "the control curve did not take its animated value; sampling " +
                    "did not run and every material observation here is void");

                // OUTCOME 3, observed verbatim on Unity 2022.3.22f1.
                //
                // A bare "material._Cutoff" binding on a two-slot renderer does
                // NOT mutate either material object, and does NOT create any
                // per-material-index MaterialPropertyBlock. It is applied as
                // renderer-wide MaterialPropertyBlock state carrying the animated
                // value. A renderer-wide block is not slot-scoped, so the animated
                // value overrides the property for EVERY material slot the
                // renderer draws -- it is not a slot-0-only effect.
                Assert.That(identityHeld, Is.True,
                    "the sampled slots are no longer the fixture's materials");
                Assert.That(sampledSlot0, Is.EqualTo(0.10f).Within(1e-5f),
                    "slot 0's material object was mutated; the recorded " +
                    "property-block conclusion does not hold");
                Assert.That(sampledSlot1, Is.EqualTo(0.90f).Within(1e-5f),
                    "slot 1's material object was mutated; the recorded " +
                    "property-block conclusion does not hold");

                Assert.That(hasBlock, Is.True);
                Assert.That(wideEmpty, Is.False);
                Assert.That(wideHasCutoff, Is.True);
                Assert.That(wideCutoff, Is.EqualTo(0.42f).Within(1e-5f));

                // The per-material-index overload reports empty for both indices:
                // the animation system set no per-index block at all.
                Assert.That(index0Empty, Is.True);
                Assert.That(index0HasCutoff, Is.False);
                Assert.That(index0Cutoff, Is.EqualTo(0f).Within(1e-5f));
                Assert.That(index1Empty, Is.True);
                Assert.That(index1HasCutoff, Is.False);
                Assert.That(index1Cutoff, Is.EqualTo(0f).Within(1e-5f));

                // Nothing persists after StopAnimationMode: the materials keep
                // their serialized values and the property block is gone.
                Assert.That(a.GetFloat("_Cutoff"), Is.EqualTo(0.10f).Within(1e-5f));
                Assert.That(b.GetFloat("_Cutoff"), Is.EqualTo(0.90f).Within(1e-5f));
                Assert.That(renderer.HasPropertyBlock(), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(mesh);
            }
        }

        // Step 4c, recorded for Task 11 to implement. GetAnimatableBindings
        // establishes what Unity GENERATES in this fixture. It does not
        // establish that every clip in the ecosystem contains only those forms:
        // clips are authored, generated, and rewritten by many tools, and AMUSE
        // reads whatever the committed graph actually holds. During capture, a
        // renderer material-property binding whose syntax AMUSE does not
        // recognize, and which could name a proof-relevant material property,
        // MUST produce a named conservative refusal. It must never be silently
        // classified as irrelevant: silently ignoring an unparsed binding that
        // in fact drives a proof input is a false positive, which this project
        // treats as a correctness bug rather than a tradeoff.
    }
}
