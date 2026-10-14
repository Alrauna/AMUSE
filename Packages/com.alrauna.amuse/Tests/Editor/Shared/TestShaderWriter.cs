using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Shared
{
    /// <summary>
    /// Writes a stand-in shader source, imports it with a forced
    /// synchronous import, and resolves it by asset path with a null
    /// assert. Sites that pin a .meta GUID by hand cannot use this: their
    /// import must observe the hand-written meta, so their staging stays
    /// at the call site.
    /// </summary>
    internal static class TestShaderWriter
    {
        internal static Shader WriteTestShader(string path, string shaderText)
        {
            File.WriteAllText(path, shaderText);
            AssetDatabase.ImportAsset(
                path, ImportAssetOptions.ForceSynchronousImport);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null, path);
            return shader;
        }

        /// <summary>
        /// Writes and imports the shared stand-in shape: the named shader
        /// with one empty pass and, when <paramref name="properties"/> is
        /// not null, a properties block carrying that fragment verbatim
        /// between the block's braces. A null properties writes the
        /// blockless form.
        /// </summary>
        internal static Shader WriteStandInShader(
            string path,
            string shaderName,
            string properties)
        {
            return WriteTestShader(
                path,
                "Shader \"" + shaderName + "\"\n" +
                "{\n" +
                (properties == null
                    ? ""
                    : "    Properties\n    {" + properties + "\n    }\n") +
                "    SubShader { Pass {} }\n}\n");
        }

        /// <summary>
        /// Mints a material of the named imported fixture shader,
        /// asserting the shader resolves. The caller owns destruction.
        /// </summary>
        internal static Material CreateMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            Assert.That(
                shader,
                Is.Not.Null,
                $"Test fixture shader '{shaderName}' must import.");
            return new Material(shader);
        }
    }
}
