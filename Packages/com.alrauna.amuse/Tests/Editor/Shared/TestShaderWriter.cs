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
    }
}
