using System.Globalization;
using Alrauna.Amuse.Tests.Editor.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The GPU reach fixture of
    /// `docs/superpowers/investigations/2026-11-19-atlasing-page-fixtures.md`.
    /// The border rule of design section 7 draws its reach numbers from
    /// that record. This suite re-measures the reach on the live editor
    /// GPU with the method of the record: a probe shader samples one
    /// black texture with one white texel on its base level through
    /// explicit gradients, the sample spacing is one eighth of a texel,
    /// the sweep covers six angles, several gradient lengths, and
    /// anisotropic ratios up to 64, and the quality setting pins
    /// anisotropic filtering to forced on. A row fails when any nonzero
    /// output lies its border reach plus one half texel or more from
    /// the white texel.
    /// </summary>
    public sealed class AtlasGpuReachTests
    {
        private const string TempFolder = "Assets/AmuseTests_AtlasGpuReach";

        private const int ProbePixels = 512;

        private const float TexelsPerProbeStep = 0.125f;

        private const int TextureSize = 64;

        private const int WhiteTexelX = 32;

        private const int WhiteTexelY = 32;

        private static readonly int[] SweepLengths = { 1, 2, 4, 8, 16, 32 };

        private static readonly int[] SweepRatios = { 1, 2, 4, 8, 16, 32, 64 };

        private static readonly float[] SweepAngles =
            { 0f, 30f, 60f, 90f, 120f, 150f };

        private readonly TestTransientScope _scope =
            new TestTransientScope(TempFolder);

        [SetUp]
        public void SetUp()
        {
            _scope.EnsureTempFolder();
        }

        [TearDown]
        public void TearDown()
        {
            _scope.TearDown();
            if (AssetDatabase.IsValidFolder(TempFolder))
            {
                AssetDatabase.DeleteAsset(TempFolder);
                AssetDatabase.Refresh();
            }
        }

        [TestCase(0, 1)]
        [TestCase(16, 16)]
        public void GpuReachStaysInsideTheBorder(
            int anisoLevel,
            int borderReachTexels)
        {
            var shader = TestShaderWriter.WriteTestShader(
                TempFolder + "/AtlasGpuReachProbe.shader",
                ProbeShaderSource);
            using (new AnisotropicFilteringPin(
                AnisotropicFiltering.ForceEnable))
            {
                var texture = NewProbeTexture(anisoLevel);
                try
                {
                    var reach = MeasureReach(texture, shader);
                    TestContext.Out.WriteLine(
                        "Observed reach for anisotropy level " + anisoLevel +
                        " is " + reach.MaxDistance.ToString(
                            "0.###", CultureInfo.InvariantCulture) +
                        " texels. The farthest config was " + reach.Cause +
                        ".");
                    Assert.That(
                        reach.MaxDistance,
                        Is.LessThan(borderReachTexels + 0.5f),
                        "The border reach of " + borderReachTexels +
                        " texels must cover the measured reach at" +
                        " anisotropy level " + anisoLevel + ". The" +
                        " farthest config was " + reach.Cause + ".");
                }
                finally
                {
                    Destroy(texture);
                }
            }
        }

        /// <summary>
        /// The probe texture of the record: black on every level, with
        /// one white texel on the base level, a full manual mip chain,
        /// trilinear filtering, and the row's anisotropy level.
        /// </summary>
        private static Texture2D NewProbeTexture(int anisoLevel)
        {
            const int mipLevels = 7;
            var texture = new Texture2D(
                TextureSize,
                TextureSize,
                TextureFormat.RGBA32,
                mipLevels,
                linear: true);
            for (var level = 0; level < mipLevels; level++)
            {
                var width = TextureSize >> level;
                var pixels = new Color32[width * width];
                if (level == 0)
                {
                    pixels[WhiteTexelY * TextureSize + WhiteTexelX] =
                        new Color32(byte.MaxValue, byte.MaxValue,
                            byte.MaxValue, byte.MaxValue);
                }

                texture.SetPixels32(pixels, level);
            }

            texture.filterMode = FilterMode.Trilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.anisoLevel = anisoLevel;
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return texture;
        }

        private readonly struct ReachResult
        {
            internal ReachResult(float maxDistance, string cause)
            {
                MaxDistance = maxDistance;
                Cause = cause;
            }

            internal float MaxDistance { get; }

            internal string Cause { get; }
        }

        /// <summary>
        /// Sweeps the method of the record over the probe texture and
        /// returns the largest distance from the white texel at which
        /// any probe output was nonzero.
        /// </summary>
        private static ReachResult MeasureReach(Texture2D texture, Shader shader)
        {
            var material = new Material(shader);
            RenderTexture target = null;
            Texture2D readback = null;
            try
            {
                target = RenderTexture.GetTemporary(
                    ProbePixels,
                    ProbePixels,
                    0,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                readback = new Texture2D(
                    ProbePixels,
                    ProbePixels,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: true);
                material.SetVector(ShaderIds.Grid, new Vector4(
                    ProbePixels,
                    TexelsPerProbeStep,
                    (WhiteTexelX + 0.5f) / TextureSize,
                    (WhiteTexelY + 0.5f) / TextureSize));
                material.SetVector(
                    ShaderIds.Texel,
                    new Vector2(1f / TextureSize, 1f / TextureSize));

                var best = new ReachResult(0f, "none");
                foreach (var angle in SweepAngles)
                {
                    var radians = angle * Mathf.PI / 180f;
                    var axis = new Vector2(
                        Mathf.Cos(radians), Mathf.Sin(radians));
                    var cross = new Vector2(-axis.y, axis.x);
                    foreach (var length in SweepLengths)
                    {
                        foreach (var ratio in SweepRatios)
                        {
                            material.SetVector(
                                ShaderIds.AxisA,
                                new Vector4(axis.x, axis.y, length, 0f));
                            material.SetVector(
                                ShaderIds.AxisB,
                                new Vector4(
                                    cross.x, cross.y, length / (float)ratio,
                                    0f));
                            var distance = RenderAndMeasure(
                                texture, material, target, readback);
                            if (distance > best.MaxDistance)
                            {
                                best = new ReachResult(
                                    distance,
                                    "angle " + angle.ToString(
                                        CultureInfo.InvariantCulture) +
                                    " degrees, gradient length " + length +
                                    " texels, ratio " + ratio);
                            }
                        }
                    }
                }

                return best;
            }
            finally
            {
                Destroy(material);
                if (readback != null)
                {
                    Destroy(readback);
                }

                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                }
            }
        }

        private static float RenderAndMeasure(
            Texture2D texture,
            Material material,
            RenderTexture target,
            Texture2D readback)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            try
            {
                Graphics.Blit(texture, target, material);
                readback.ReadPixels(
                    new Rect(0f, 0f, ProbePixels, ProbePixels), 0, 0);
                readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            var pixels = readback.GetPixels32();
            var half = (ProbePixels - 1) * 0.5f;
            var best = 0f;
            for (var y = 0; y < ProbePixels; y++)
            {
                for (var x = 0; x < ProbePixels; x++)
                {
                    if (pixels[y * ProbePixels + x].r == 0)
                    {
                        continue;
                    }

                    var dx = (x - half) * TexelsPerProbeStep;
                    var dy = (y - half) * TexelsPerProbeStep;
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > best)
                    {
                        best = distance;
                    }
                }
            }

            return best;
        }

        private static void Destroy(Object obj)
        {
            if (obj != null)
            {
                Object.DestroyImmediate(obj);
            }
        }

        private static class ShaderIds
        {
            internal static readonly int Grid = Shader.PropertyToID("_Grid");

            internal static readonly int Texel =
                Shader.PropertyToID("_Texel");

            internal static readonly int AxisA =
                Shader.PropertyToID("_AxisA");

            internal static readonly int AxisB =
                Shader.PropertyToID("_AxisB");
        }

        /// <summary>
        /// One probe pass maps every render target pixel to one sample
        /// point around the white texel, at the record's spacing, and
        /// samples the probe texture at that point through explicit
        /// gradients of the swept length, angle, and ratio.
        /// </summary>
        private const string ProbeShaderSource = @"
Shader ""Hidden/Alrauna/AmuseTests/AtlasGpuReachProbe""
{
    Properties
    {
        _MainTex (""Source"", 2D) = ""black"" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include ""UnityCG.cginc""

            sampler2D _MainTex;
            float4 _Grid;
            float2 _Texel;
            float4 _AxisA;
            float4 _AxisB;

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 grid : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f output;
                output.position = UnityObjectToClipPos(v.vertex);
                output.grid = v.texcoord.xy * _Grid.x;
                return output;
            }

            float4 frag(v2f input) : SV_Target
            {
                float2 center = _Grid.xx * 0.5 - 0.5;
                float2 offsetTexels = (input.grid - center) * _Grid.y;
                float2 uv = _Grid.zw + offsetTexels * _Texel;
                float2 ddx = _AxisA.xy * _AxisA.z * _Texel;
                float2 ddy = _AxisB.xy * _AxisB.z * _Texel;
                return tex2Dgrad(_MainTex, uv, ddx, ddy);
            }
            ENDCG
        }
    }
}
";
    }
}
