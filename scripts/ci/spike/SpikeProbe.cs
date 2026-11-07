// THROWAWAY spike probe. The spike script copies this file into the CI
// checkout under Assets. It is never part of a package. It answers S3 and S4:
// which graphics device does the editor get, and does the R8_UNorm blit plus
// AsyncGPUReadback path work on it.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public static class SpikeProbe
{
    [Serializable]
    private sealed class Result
    {
        public string deviceType;
        public string deviceName;
        public string deviceVersion;
        public bool asyncReadback;
        public bool r8Render;
        public bool r8ReadPixels;
        public bool blitReadbackOk;
        public int blitReadbackValue = -1;
        public string error = "";
    }

    public static void Run()
    {
        var result = new Result
        {
            deviceType = SystemInfo.graphicsDeviceType.ToString(),
            deviceName = SystemInfo.graphicsDeviceName,
            deviceVersion = SystemInfo.graphicsDeviceVersion,
            asyncReadback = SystemInfo.supportsAsyncGPUReadback,
            r8Render = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.Render),
            r8ReadPixels = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, FormatUsage.ReadPixels),
        };

        try
        {
            var source = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = new Color32(200, 200, 200, 200);
            source.SetPixels32(pixels);
            source.Apply(false);

            var descriptor = new RenderTextureDescriptor(4, 4, GraphicsFormat.R8_UNorm, 0) { sRGB = false };
            var target = RenderTexture.GetTemporary(descriptor);
            try
            {
                Graphics.Blit(source, target);
                var request = AsyncGPUReadback.Request(target, 0, GraphicsFormat.R8_UNorm);
                request.WaitForCompletion();
                if (request.hasError)
                {
                    result.error = "readback reported an error";
                }
                else
                {
                    result.blitReadbackValue = request.GetData<byte>()[0];
                    result.blitReadbackOk = Mathf.Abs(result.blitReadbackValue - 200) <= 1;
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }
        catch (Exception exception)
        {
            result.error = exception.GetType().Name + ": " + exception.Message;
        }

        Debug.Log("SPIKE-PROBE " + JsonUtility.ToJson(result));
        EditorApplication.Exit(0);
    }
}
