using System;
using System.Collections.Generic;
using Alrauna.Amuse.Tests.Editor.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// One render oracle scene per test. The harness renders a lit
    /// sphere into a 64 by 64 ARGB32 texture. One directional light
    /// with soft shadows lands on a receiver plane, and one point
    /// light runs ForwardAdd, as in the lilToon live value render
    /// oracle. The scene sits far below the origin and every foreign
    /// light of the open scene stays off for the render, so a case
    /// never depends on the open scene.
    /// </summary>
    public sealed class PoiyomiRenderHarness : IDisposable
    {
        private const int TargetSize = 64;

        private readonly TestTransientScope _scope =
            new TestTransientScope(null);

        private GameObject root;
        private Camera camera;
        private MeshRenderer sphereRenderer;
        private RenderTexture target;
        private Texture2D readback;
        private bool previousAllowAsyncCompilation;
        private AmbientMode previousAmbientMode;
        private Color previousAmbientLight;
        private float previousAmbientIntensity;
        private SphericalHarmonicsL2 previousAmbientProbe;
        private readonly List<Light> disabledForeignLights =
            new List<Light>();

        public PoiyomiRenderHarness()
        {
            // A case must never compare placeholder frames of an
            // async shader compile.
            previousAllowAsyncCompilation =
                ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;

            // The ambient must sit at black. A bright ambient probe
            // washes out the light terms, and then no value change
            // can move the subject.
            previousAmbientMode = RenderSettings.ambientMode;
            previousAmbientLight = RenderSettings.ambientLight;
            previousAmbientIntensity = RenderSettings.ambientIntensity;
            previousAmbientProbe = RenderSettings.ambientProbe;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.ambientProbe = new SphericalHarmonicsL2();

            root = new GameObject("AMUSE poiyomi render oracle root");
            root.transform.position = new Vector3(0f, -500f, 0f);
            readback = _scope.Track(new Texture2D(
                TargetSize, TargetSize, TextureFormat.RGBA32, false));
            target = new RenderTexture(
                TargetSize, TargetSize, 24, RenderTextureFormat.ARGB32);

            var cameraObject = NewChild("oracle camera");
            camera = cameraObject.AddComponent<Camera>();
            camera.transform.localPosition =
                new Vector3(0f, 1.1f, -2.6f);
            camera.transform.localRotation =
                Quaternion.Euler(22f, 0f, 0f);
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

            var sunObject = NewChild("oracle sun");
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.6f;
            sun.shadows = LightShadows.Soft;
            sunObject.transform.localRotation =
                Quaternion.Euler(18f, 90f, 0f);

            var lampObject = NewChild("oracle point light");
            var lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.intensity = 0.6f;
            lamp.range = 12f;
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.ForcePixel;
            lampObject.transform.localPosition =
                new Vector3(1.2f, 1f, -1.5f);

            foreach (var sceneLight in UnityEngine.Object
                .FindObjectsOfType<Light>())
            {
                if (!sceneLight.isActiveAndEnabled ||
                    sceneLight.transform.IsChildOf(root.transform) ||
                    (sceneLight.cullingMask &
                     (1 << sphereRenderer.gameObject.layer)) == 0)
                {
                    continue;
                }

                sceneLight.enabled = false;
                disabledForeignLights.Add(sceneLight);
            }
        }

        public void Dispose()
        {
            foreach (var sceneLight in disabledForeignLights)
            {
                if (sceneLight != null)
                {
                    sceneLight.enabled = true;
                }
            }

            disabledForeignLights.Clear();
            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (root != null)
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            _scope.TearDown();
            if (target != null)
            {
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                target = null;
            }

            readback = null;
            camera = null;
            sphereRenderer = null;
            root = null;
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientLight = previousAmbientLight;
            RenderSettings.ambientIntensity = previousAmbientIntensity;
            RenderSettings.ambientProbe = previousAmbientProbe;
            ShaderUtil.allowAsyncCompilation =
                previousAllowAsyncCompilation;
        }

        /// <summary>
        /// The subject sphere with the material renders once. The
        /// first call of a case compiles the shader variants, so a
        /// caller that compares two frames renders once before the
        /// first frame it keeps.
        /// </summary>
        public Color32[] Render(Material material)
        {
            sphereRenderer.sharedMaterial = material;
            var active = RenderTexture.active;
            RenderTexture.active = target;
            camera.Render();
            readback.ReadPixels(
                new Rect(0, 0, TargetSize, TargetSize), 0, 0);
            readback.Apply(false, false);
            RenderTexture.active = active;
            return readback.GetPixels32();
        }

        private GameObject NewChild(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }
    }
}
