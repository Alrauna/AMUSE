using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi
{
    /// <summary>
    /// Builds materials on the installed bundled Poiyomi shaders at run
    /// time. Each shader loads by its pinned canonical GUID from
    /// <c>PoiyomiSourceAttestation</c>. Name lookup is never used, because
    /// a stand-in can mimic a vendor name and answer first. The material
    /// stays in memory. The caller destroys it in teardown, normally
    /// through the fixture Track helper.
    /// </summary>
    internal static class RealPoiyomiMaterial
    {
        /// <summary>
        /// Loads one installed bundled Poiyomi shader by its pinned GUID
        /// and mints one in-memory material on it. The GUID load is the
        /// only reliable resolve. A name lookup can answer with a stand-in
        /// or a foreign shader that shares the vendor name.
        /// </summary>
        internal static Material Create(string shaderGuid, string label)
        {
            var shader = LoadShader(shaderGuid, label);
            return new Material(shader);
        }

        /// <summary>Material on the installed bundled main shader.</summary>
        internal static Material Main()
        {
            return Create(
                PoiyomiSourceAttestation.CanonicalShaderGuid,
                "installed bundled Poiyomi Toon shader");
        }

        /// <summary>Material on the installed bundled Two Pass shader.</summary>
        internal static Material TwoPass()
        {
            return Create(
                PoiyomiSourceAttestation.TwoPassCanonicalShaderGuid,
                "installed bundled Poiyomi Toon Two Pass shader");
        }

        /// <summary>The vendor mask texture slot on the main shader.</summary>
        internal const string MaskSlot = "_AlphaMask";

        /// <summary>
        /// Binds a staged mask texture in the vendor mask slot of a real
        /// material. The vendor mask block samples the mask through the
        /// main sampler, so a mask row assigns the main texture too when
        /// its route reads the mask.
        /// </summary>
        internal static void BindMask(Material material, Texture mask)
        {
            material.SetTexture(MaskSlot, mask);
        }

        private static Shader LoadShader(string shaderGuid, string label)
        {
            var path = AssetDatabase.GUIDToAssetPath(shaderGuid);
            Assert.That(
                path, Is.Not.Empty,
                "fixture precondition: the " + label +
                " must resolve by its pinned GUID");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(
                shader, Is.Not.Null,
                "fixture precondition: the " + label + " must load");
            return shader;
        }
    }
}
