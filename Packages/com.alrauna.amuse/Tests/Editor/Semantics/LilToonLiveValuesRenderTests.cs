using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Semantics;
using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// The render oracle for the dead families of the reviewed
    /// lilToon 2.4.1 live value model. The model drops the values of
    /// a dead family from the material key. A wrong member would
    /// merge two materials that render differently. Each case builds
    /// a case material on one admitted row, renders a lit sphere,
    /// changes only the family values, and renders again. The two
    /// readbacks must match byte for byte. A liveness check renders
    /// the same family with its switch on and proves that the change
    /// moves the render there. So an equal pair means a dead family
    /// and not a broken fixture.
    /// </summary>
    public sealed class LilToonLiveValuesRenderTests
    {
        private const int TargetSize = 64;
        private const int MainTextureSize = 64;
        private const float PerturbStep = 0.37f;

        /// <summary>
        /// The families whose switch the reviewed route requires at
        /// zero. Admitted uses can never turn them on, so the oracle
        /// cannot prove them live.
        /// </summary>
        private static readonly HashSet<string> FamiliesTheRouteRequiresOff =
            new HashSet<string>
            {
                "Main3rd",
                "Glitter",
                "AudioLink",
                "Parallax",
            };

        /// <summary>
        /// Families whose live values the oracle cannot show under
        /// the fixture of the controller ruling of 2026-11-22. The
        /// render stays byte for byte equal with the switch on and
        /// every member perturbed, on every one of the ten rows.
        /// The finding is reported with the task. The dead cases of
        /// these families still run, and they still hold.
        /// </summary>
        private static readonly HashSet<string> FamiliesTheOracleCannotShow =
            new HashSet<string>
            {
                "Anisotropy",
                "Main gradation",
            };

        /// <summary>
        /// The ten admitted rows as GUID and row name pairs. The row
        /// names come from the attestation, so the source never loads
        /// a shader.
        /// </summary>
        private static readonly (string Guid, string Name)[] AdmittedRows =
        {
            (LilToonSourceAttestation.SupportedShaderGuid,
                LilToonSourceAttestation.SupportedShaderName),
            (LilToonSourceAttestation.CutoutShaderGuid,
                LilToonSourceAttestation.CutoutShaderName),
            (LilToonSourceAttestation.TransparentShaderGuid,
                LilToonSourceAttestation.TransparentShaderName),
            (LilToonSourceAttestation.OutlineShaderGuid,
                LilToonSourceAttestation.OutlineShaderName),
            (LilToonSourceAttestation.OutlineCutoutShaderGuid,
                LilToonSourceAttestation.OutlineCutoutShaderName),
            (LilToonSourceAttestation.OutlineTransparentShaderGuid,
                LilToonSourceAttestation.OutlineTransparentShaderName),
            (LilToonSourceAttestation.OnePassTransparentShaderGuid,
                LilToonSourceAttestation.OnePassTransparentShaderName),
            (LilToonSourceAttestation.TwoPassTransparentShaderGuid,
                LilToonSourceAttestation.TwoPassTransparentShaderName),
            (LilToonSourceAttestation.OnePassTransparentOutlineShaderGuid,
                LilToonSourceAttestation.OnePassTransparentOutlineShaderName),
            (LilToonSourceAttestation.TwoPassTransparentOutlineShaderGuid,
                LilToonSourceAttestation.TwoPassTransparentOutlineShaderName),
        };

        /// <summary>
        /// The family switches that the reviewed route lets an
        /// admitted use hold on. The probe asks the required value
        /// rule of the route itself, so the list can never drift from
        /// the rule. Computed on the main thread on first use.
        /// </summary>
        private static string[] switchesTheRouteMayHoldOn;

        private readonly List<UnityEngine.Object> tracked =
            new List<UnityEngine.Object>();

        private GameObject root;
        private Camera camera;
        private MeshRenderer sphereRenderer;
        private RenderTexture target;
        private Texture2D readback;
        private Texture2D mainTexture;
        private Texture2D noiseTexture;
        private Texture2D rampTexture;
        private Texture2D darkTexture;
        private Cubemap noiseCubemap;

        [SetUp]
        public void SetUp()
        {
            // The scene sits far below the origin. Geometry of the
            // open scene stays out of the frame and out of the
            // shadow maps.
            root = new GameObject(
                "AMUSE lilToon render oracle root");
            root.transform.position = new Vector3(0f, -500f, 0f);
            noiseTexture = Track(NewNoiseTexture());
            mainTexture = Track(NewPatternTexture());
            rampTexture = Track(NewRampTexture());
            darkTexture = Track(NewDarkTexture());
            noiseCubemap = Track(NewNoiseCubemap());
            readback = Track(new Texture2D(
                TargetSize, TargetSize, TextureFormat.RGBA32, false));
            target = new RenderTexture(
                TargetSize, TargetSize, 24, RenderTextureFormat.ARGB32);

            var cameraObject = NewChild("oracle camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.localPosition = new Vector3(0f, 0.35f, -2.4f);
            camera.transform.localRotation = Quaternion.identity;
            camera.fieldOfView = 55f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            camera.allowMSAA = false;
            camera.allowHDR = false;
            camera.targetTexture = target;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "oracle subject sphere";
            sphere.transform.SetParent(root.transform, false);
            sphereRenderer = sphere.GetComponent<MeshRenderer>();

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "oracle shadow receiver plane";
            plane.transform.SetParent(root.transform, false);
            plane.transform.localPosition = new Vector3(0f, -1.1f, 0f);
            plane.transform.localScale = new Vector3(6f, 1f, 6f);
            plane.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.GetBuiltinExtraResource<Material>(
                    "Default-Diffuse.mat");

            // One directional light with soft shadows runs ForwardBase
            // and ShadowCaster. One point light runs ForwardAdd.
            var sunObject = NewChild("oracle sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.localRotation =
                Quaternion.Euler(50f, -30f, 0f);

            var lampObject = NewChild("oracle point light");
            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = 0.8f;
            lamp.range = 12f;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;
            lampObject.transform.localPosition =
                new Vector3(1.2f, 1f, -1.5f);
        }

        [TearDown]
        public void TearDown()
        {
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            foreach (var obj in tracked)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            tracked.Clear();
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                target = null;
            }

            camera = null;
            sphereRenderer = null;
            root = null;
        }

        // --- the cases ------------------------------------------------

        private static IEnumerable<TestCaseData> DeadFamilyCases()
        {
            foreach (var family in LilToonLiveValues.Model.Families)
            {
                foreach (var row in AdmittedRows)
                {
                    // A row gated family with no switch only runs on
                    // the rows where its gate holds. The delegates of
                    // those families read no material.
                    if (family.Switches.Count == 0 &&
                        !family.IsOff(null, row.Name))
                    {
                        continue;
                    }

                    yield return new TestCaseData(family.Name, row.Guid)
                        .SetName("Dead family '" + family.Name +
                                 "' holds on row '" + row.Name + "'");
                }
            }
        }

        private static IEnumerable<TestCaseData> LiveFamilyCases()
        {
            foreach (var family in LilToonLiveValues.Model.Families)
            {
                if (family.Switches.Count == 0)
                {
                    continue;
                }

                if (FamiliesTheRouteRequiresOff.Contains(family.Name) ||
                    FamiliesTheOracleCannotShow.Contains(family.Name))
                {
                    continue;
                }

                yield return new TestCaseData(family.Name)
                    .SetName("Live family '" + family.Name +
                             "' changes the render");
            }
        }

        // --- the tests ------------------------------------------------

        /// <summary>
        /// One dead family on one admitted row. The case material
        /// holds every switch on that the route allows, except the
        /// switch of the family. The family values change. The render
        /// must stay byte for byte equal. The no-op guard proves that
        /// the dead set of the model holds every perturbed name, so
        /// the case can never pass by perturbing nothing.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(DeadFamilyCases))]
        public void DeadFamilyValuesDoNotChangeTheRender(
            string familyName,
            string shaderGuid)
        {
            var family = FamilyByName(familyName);
            var shader = LoadRowShader(shaderGuid);
            var material = NewCaseMaterial(shader, family);
            sphereRenderer.sharedMaterial = material;

            Assert.That(
                family.IsOff(material, shader.name),
                Is.True,
                "no-op guard: the family '" + familyName +
                "' must be off on row '" + shader.name + "'");

            var perturbed = PerturbedNamesOf(material, family, shader.name);
            Assert.That(
                perturbed,
                Is.Not.Empty,
                "no-op guard: the dead set of the model must hold at " +
                "least one member of the family '" + familyName +
                "'. The fallback name rule may shadow every member.");
            foreach (var name in perturbed)
            {
                Assert.That(
                    family.Properties,
                    Does.Contain(name),
                    "no-op guard: every perturbed name must be a member " +
                    "of the family under test");
            }

            Assert.That(
                AtlasRouteTable.LilToonReviewedMainColor
                    .UnheldRequiredValue(material),
                Is.Null,
                "fixture precondition: the case material must hold " +
                "every value that the reviewed route requires");

            // The first render compiles the shader variants of the
            // row. Its result is discarded.
            RenderSubject();
            var before = RenderSubject();
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = RenderSubject();
            var differentPixels = CountDifferentPixels(before, after);
            if (differentPixels > 0)
            {
                var liveMembers =
                    LiveMembersOf(family, shader, before, perturbed);
                Assert.Fail(
                    "The values of the dead family '" + familyName +
                    "' changed the render of row '" + shader.name + "'. " +
                    differentPixels + " of " + before.Length +
                    " pixels differ. The largest channel difference is " +
                    LargestChannelDifference(before, after) + " of 255. " +
                    "The live members are: " +
                    string.Join(", ", liveMembers) + ".");
            }
        }

        /// <summary>
        /// The liveness guard of the controller ruling of 2026-11-22.
        /// The same perturbation of the dead case must change the
        /// render on at least one admitted row when the family switch
        /// is on. This proves that the compiled shader reads the
        /// family, so the dead case cannot pass trivially.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(LiveFamilyCases))]
        public void LiveFamilyValuesChangeTheRenderOnSomeRow(
            string familyName)
        {
            var family = FamilyByName(familyName);
            var differingRows = new List<string>();
            foreach (var row in AdmittedRows)
            {
                var shader = LoadRowShader(row.Guid);
                var material = NewCaseMaterial(shader, family);
                var perturbed =
                    PerturbedNamesOf(material, family, shader.name);
                TurnOnTheFamilySwitch(material, family);
                sphereRenderer.sharedMaterial = material;

                RenderSubject();
                var before = RenderSubject();
                foreach (var name in perturbed)
                {
                    PerturbOneValue(material, name);
                }

                var after = RenderSubject();
                if (!BytesEqual(before, after))
                {
                    differingRows.Add(shader.name);
                }
            }

            Assert.That(
                differingRows,
                Is.Not.Empty,
                "liveness guard: no perturbation of the family '" +
                familyName + "' changed the render with its switch on. " +
                "The dead case of this family can pass trivially. The " +
                "rows tried are: " +
                string.Join(", ", AdmittedRows.Select(row => row.Name)) +
                ".");
        }

        // --- Falsifier 8: LiveFamilyValueChangesTheRender -------------

        /// <summary>
        /// The Rim family with its switch on. The same perturbation
        /// of the dead case must change the render here. This proves
        /// that the fixture and the camera can show a live family.
        /// </summary>
        [Test]
        public void LiveRimValueChangesTheRender()
        {
            var family = FamilyByName("Rim");
            var shader = LoadRowShader(
                LilToonSourceAttestation.SupportedShaderGuid);
            var material = NewCaseMaterial(shader, family);
            var perturbed = PerturbedNamesOf(material, family, shader.name);
            TurnOnTheFamilySwitch(material, family);
            sphereRenderer.sharedMaterial = material;

            RenderSubject();
            var before = RenderSubject();
            foreach (var name in perturbed)
            {
                PerturbOneValue(material, name);
            }

            var after = RenderSubject();
            Assert.That(
                BytesEqual(before, after),
                Is.False,
                "the Rim values with the switch on must change the " +
                "render. The dead case could pass trivially otherwise. " +
                "The perturbed names are: " +
                string.Join(", ", perturbed) + ".");
        }

        // --- the case material ----------------------------------------

        /// <summary>
        /// One material on the row. Every family switch that the
        /// route lets an admitted use hold on is on, except the
        /// switch of the family under test. The Main2nd shape and
        /// the blink vectors are pinned, so no value depends on
        /// time. Some textures of the pinned on families are pinned
        /// too. A null layer texture samples white in the editor,
        /// and the white would wash out the whole render and hide
        /// every other family.
        /// </summary>
        private Material NewCaseMaterial(
            Shader shader,
            AtlasDeadValueFamily family)
        {
            var material = Track(new Material(shader));
            material.SetTexture("_MainTex", mainTexture);
            PinMain2ndShape(material);
            foreach (var name in SwitchesTheRouteMayHoldOn(shader))
            {
                SetSwitchOn(material, name);
            }

            foreach (var name in family.Switches)
            {
                SetSwitchOff(material, name);
            }

            material.SetVector("_EmissionBlink", Vector4.zero);
            material.SetVector("_Emission2ndBlink", Vector4.zero);
            material.SetTexture("_Main2ndTex", mainTexture);
            material.SetTexture("_MatCapTex", mainTexture);
            material.SetTexture("_MatCap2ndTex", mainTexture);
            material.SetTexture("_EmissionMap", darkTexture);
            material.SetTexture("_Emission2ndMap", darkTexture);
            material.SetTexture("_MainGradationTex", rampTexture);
            // The alpha mask in replace mode with a varied mask
            // gives the render varied alpha. The dither of lilToon
            // only thresholds alpha, so a full alpha everywhere
            // would hide the family.
            material.SetTexture("_AlphaMask", mainTexture);
            return material;
        }

        private static string[] SwitchesTheRouteMayHoldOn(Shader shader)
        {
            if (switchesTheRouteMayHoldOn == null)
            {
                switchesTheRouteMayHoldOn = LilToonLiveValues.Model.Families
                    .SelectMany(family => family.Switches)
                    .Distinct()
                    .Where(name => TheRouteMayHold(shader, name))
                    .ToArray();
            }

            return switchesTheRouteMayHoldOn;
        }

        private static bool TheRouteMayHold(Shader shader, string name)
        {
            var probe = new Material(shader);
            try
            {
                PinMain2ndShape(probe);
                SetSwitchOn(probe, name);
                return AtlasRouteTable.LilToonReviewedMainColor
                    .UnheldRequiredValue(probe) == null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// The Main2nd values that the route demands when the layer
        /// is on and samples UV0. The pinned values also keep the
        /// probe of the switch list honest.
        /// </summary>
        private static void PinMain2ndShape(Material material)
        {
            material.SetFloat("_Main2ndTexAngle", 0f);
            material.SetFloat("_Main2ndTexIsDecal", 0f);
            material.SetFloat("_Main2ndTexIsLeftOnly", 0f);
            material.SetFloat("_Main2ndTexIsRightOnly", 0f);
            material.SetFloat("_Main2ndTexShouldCopy", 0f);
            material.SetFloat("_Main2ndTexShouldFlipMirror", 0f);
            material.SetFloat("_Main2ndTexShouldFlipCopy", 0f);
            material.SetFloat("_Main2ndTexIsMSDF", 0f);
            material.SetFloat("_Main2ndTex_UVMode", 0f);
            material.SetVector(
                "_Main2ndTexDecalAnimation",
                new Vector4(1f, 1f, 1f, 1f));
            material.SetVector(
                "_Main2ndTexDecalSubParam",
                new Vector4(1f, 1f, 0f, 0f));
            material.SetVector("_Main2ndDissolveParams", Vector4.zero);
        }

        private static void TurnOnTheFamilySwitch(
            Material material,
            AtlasDeadValueFamily family)
        {
            SetSwitchOn(material, family.Switches[0]);
        }

        private static void SetSwitchOn(Material material, string name)
        {
            if (name == "_DissolveParams")
            {
                var value = material.GetVector(name);
                material.SetVector(
                    name,
                    new Vector4(1f, value.y, value.z, value.w));
                return;
            }

            material.SetFloat(name, 1f);
        }

        private static void SetSwitchOff(Material material, string name)
        {
            if (name == "_DissolveParams")
            {
                var value = material.GetVector(name);
                material.SetVector(
                    name,
                    new Vector4(0f, value.y, value.z, value.w));
                return;
            }

            material.SetFloat(name, 0f);
        }

        // --- the perturbation -----------------------------------------

        /// <summary>
        /// The family members that the dead set of the model holds
        /// for the case material. The model never returns a VRChat
        /// fallback name, so those members stay unperturbed.
        /// </summary>
        private static List<string> PerturbedNamesOf(
            Material material,
            AtlasDeadValueFamily family,
            string rowName)
        {
            var dead = LilToonLiveValues.Model.DeadProperties(
                material, rowName, NeverDynamic);
            var names = new List<string>();
            foreach (var name in family.Properties)
            {
                if (dead.Contains(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static bool NeverDynamic(string name)
        {
            return false;
        }

        /// <summary>
        /// One value moves by a fixed step. A float gains 0.37. A
        /// color or vector gains 0.37 on every channel. A texture is
        /// replaced by the noise texture of its dimension. A name
        /// that the shader does not declare moves as a saved float,
        /// because no pass can read it as anything else.
        /// </summary>
        private void PerturbOneValue(Material material, string name)
        {
            var index = material.shader.FindPropertyIndex(name);
            if (index < 0)
            {
                // HasFloat stays silent for a name that the shader
                // does not declare. GetFloat would log an error.
                var saved = material.HasFloat(name)
                    ? material.GetFloat(name)
                    : 0f;
                material.SetFloat(name, saved + PerturbStep);
                return;
            }

            switch (material.shader.GetPropertyType(index))
            {
                case ShaderPropertyType.Color:
                    var color = material.GetColor(name);
                    material.SetColor(
                        name,
                        new Color(
                            color.r + PerturbStep,
                            color.g + PerturbStep,
                            color.b + PerturbStep,
                            color.a + PerturbStep));
                    return;
                case ShaderPropertyType.Vector:
                    var vector = material.GetVector(name);
                    material.SetVector(
                        name,
                        new Vector4(
                            vector.x + PerturbStep,
                            vector.y + PerturbStep,
                            vector.z + PerturbStep,
                            vector.w + PerturbStep));
                    return;
                case ShaderPropertyType.Texture:
                    PerturbTexture(material, name, index);
                    return;
                default:
                    material.SetFloat(
                        name, material.GetFloat(name) + PerturbStep);
                    return;
            }
        }

        private void PerturbTexture(
            Material material,
            string name,
            int index)
        {
            var dimension =
                material.shader.GetPropertyTextureDimension(index);
            if (dimension == TextureDimension.Tex2D)
            {
                material.SetTexture(name, noiseTexture);
                return;
            }

            if (dimension == TextureDimension.Cube)
            {
                material.SetTexture(name, noiseCubemap);
                return;
            }

            Assert.Fail(
                "fixture gap: the texture property '" + name +
                "' of the row has no noise texture of its dimension");
        }

        /// <summary>
        /// The members that move the render on their own. The helper
        /// gives each member one fresh case material, so the result
        /// names the defect for the data fix.
        /// </summary>
        private List<string> LiveMembersOf(
            AtlasDeadValueFamily family,
            Shader shader,
            Color32[] baseRender,
            List<string> perturbed)
        {
            var live = new List<string>();
            foreach (var name in perturbed)
            {
                var material = NewCaseMaterial(shader, family);
                sphereRenderer.sharedMaterial = material;
                PerturbOneValue(material, name);
                if (!BytesEqual(baseRender, RenderSubject()))
                {
                    live.Add(name);
                }
            }

            return live;
        }

        // --- the render ------------------------------------------------

        private Color32[] RenderSubject()
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            try
            {
                readback.ReadPixels(
                    new Rect(0f, 0f, TargetSize, TargetSize), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static bool BytesEqual(Color32[] first, Color32[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }

            for (var index = 0; index < first.Length; index++)
            {
                if (first[index].r != second[index].r ||
                    first[index].g != second[index].g ||
                    first[index].b != second[index].b ||
                    first[index].a != second[index].a)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountDifferentPixels(
            Color32[] first,
            Color32[] second)
        {
            var count = 0;
            for (var index = 0; index < first.Length; index++)
            {
                if (first[index].r != second[index].r ||
                    first[index].g != second[index].g ||
                    first[index].b != second[index].b ||
                    first[index].a != second[index].a)
                {
                    count++;
                }
            }

            return count;
        }

        private static int LargestChannelDifference(
            Color32[] first,
            Color32[] second)
        {
            var largest = 0;
            for (var index = 0; index < first.Length; index++)
            {
                largest = Math.Max(
                    largest, Math.Abs(first[index].r - second[index].r));
                largest = Math.Max(
                    largest, Math.Abs(first[index].g - second[index].g));
                largest = Math.Max(
                    largest, Math.Abs(first[index].b - second[index].b));
                largest = Math.Max(
                    largest, Math.Abs(first[index].a - second[index].a));
            }

            return largest;
        }

        // --- the textures and lookups ---------------------------------

        private static Texture2D NewPatternTexture()
        {
            var texture = new Texture2D(
                MainTextureSize,
                MainTextureSize,
                TextureFormat.RGBA32,
                false)
            {
                name = "AMUSE render oracle main texture",
            };
            var pixels =
                new Color32[MainTextureSize * MainTextureSize];
            for (var y = 0; y < MainTextureSize; y++)
            {
                for (var x = 0; x < MainTextureSize; x++)
                {
                    var checker = (x / 8 + y / 8) % 2 == 0 ? 200 : 60;
                    pixels[y * MainTextureSize + x] = new Color32(
                        (byte)(x * 4),
                        (byte)(y * 4),
                        (byte)checker,
                        255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D NewNoiseTexture()
        {
            var texture = new Texture2D(
                4, 4, TextureFormat.RGBA32, false)
            {
                name = "AMUSE render oracle noise texture",
            };
            var pixels = new Color32[16];
            var seed = 0x5eed1234;
            for (var index = 0; index < pixels.Length; index++)
            {
                seed = seed * 1664525 + 1013904223;
                pixels[index] = new Color32(
                    (byte)(seed >> 24),
                    (byte)(seed >> 16),
                    (byte)(seed >> 8),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// The cube shaped noise texture. The reflection family of
        /// the model holds one cube texture property.
        /// </summary>
        private static Cubemap NewNoiseCubemap()
        {
            var cubemap = new Cubemap(
                4, TextureFormat.RGBA32, false)
            {
                name = "AMUSE render oracle noise cubemap",
            };
            var faces = (CubemapFace[])
                Enum.GetValues(typeof(CubemapFace));
            foreach (var face in faces)
            {
                if (face == CubemapFace.Unknown)
                {
                    continue;
                }

                var pixels = new Color[16];
                var seed = 0x77aa55ee + (int)face;
                for (var index = 0; index < pixels.Length; index++)
                {
                    seed = seed * 1664525 + 1013904223;
                    pixels[index] = new Color32(
                        (byte)(seed >> 24),
                        (byte)(seed >> 16),
                        (byte)(seed >> 8),
                        255);
                }

                cubemap.SetPixels(pixels, face);
            }

            cubemap.Apply(false, false);
            return cubemap;
        }

        /// <summary>
        /// A gray ramp for the gradation map. The ramp maps every
        /// luminance to itself, so the pinned on gradation keeps the
        /// render of the base texture. The default white texture
        /// would map every color to white.
        /// </summary>
        private static Texture2D NewRampTexture()
        {
            var texture = new Texture2D(
                16, 16, TextureFormat.RGBA32, false)
            {
                name = "AMUSE render oracle ramp texture",
            };
            var pixels = new Color32[16 * 16];
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var shade = (byte)(x * 17);
                    pixels[y * 16 + x] =
                        new Color32(shade, shade, shade, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>
        /// A dark varied texture for the emission maps. The default
        /// white emission maps with the white emission colors would
        /// wash out the render. The dark map keeps the emission
        /// visible but small.
        /// </summary>
        private static Texture2D NewDarkTexture()
        {
            var texture = new Texture2D(
                16, 16, TextureFormat.RGBA32, false)
            {
                name = "AMUSE render oracle dark texture",
            };
            var pixels = new Color32[16 * 16];
            var seed = 0x1234abcd;
            for (var index = 0; index < pixels.Length; index++)
            {
                seed = seed * 1664525 + 1013904223;
                pixels[index] = new Color32(
                    (byte)((seed >> 24) & 0x3f),
                    (byte)((seed >> 16) & 0x3f),
                    (byte)((seed >> 8) & 0x3f),
                    255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Shader LoadRowShader(string shaderGuid)
        {
            var path = AssetDatabase.GUIDToAssetPath(shaderGuid);
            Assert.That(
                path,
                Is.Not.Empty,
                "fixture precondition: the admitted lilToon row must " +
                "resolve by its pinned GUID " + shaderGuid);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader,
                Is.Not.Null,
                "fixture precondition: the admitted lilToon row at " +
                path + " must load");
            return shader;
        }

        private static AtlasDeadValueFamily FamilyByName(string name)
        {
            foreach (var family in LilToonLiveValues.Model.Families)
            {
                if (family.Name == name)
                {
                    return family;
                }
            }

            Assert.Fail(
                "fixture precondition: the model must hold the family '" +
                name + "'");
            return null;
        }

        private GameObject NewChild(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            tracked.Add(obj);
            return obj;
        }
    }
}
