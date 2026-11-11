using Alrauna.Amuse.Editor.Semantics.LilToon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Semantics
{
    /// <summary>
    /// Builds materials on the installed lilToon shaders at run time.
    /// Each shader loads by its production GUID pin from
    /// <c>LilToonSourceAttestation</c>. Name lookup is never used, because
    /// a stand-in can mimic a vendor name and answer first. The material
    /// stays in memory. The caller destroys it in teardown, normally
    /// through the fixture Track helper.
    /// </summary>
    internal static class RealLilToonMaterial
    {
        /// <summary>
        /// Loads one installed lilToon shader by its pinned GUID and mints
        /// one in-memory material on it. The GUID load is the only
        /// reliable resolve. A name lookup can answer with a stand-in or
        /// a foreign shader that shares the vendor name.
        /// </summary>
        internal static Material Create(string shaderGuid, string label)
        {
            var shader = LoadShader(shaderGuid, label);
            return new Material(shader);
        }

        /// <summary>Material on the installed opaque shader.</summary>
        internal static Material Opaque()
        {
            return Create(
                LilToonSourceAttestation.SupportedShaderGuid,
                "installed lilToon opaque shader");
        }

        /// <summary>Material on the installed cutout shader.</summary>
        internal static Material Cutout()
        {
            return Create(
                LilToonSourceAttestation.CutoutShaderGuid,
                "installed lilToon cutout shader");
        }

        /// <summary>Material on the installed transparent shader.</summary>
        internal static Material Transparent()
        {
            return Create(
                LilToonSourceAttestation.TransparentShaderGuid,
                "installed lilToon transparent shader");
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
