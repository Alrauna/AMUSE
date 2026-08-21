using System.Collections.Generic;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    /// <summary>
    /// Synthetic Unity objects for collector tests: built in code, tracked, and
    /// destroyed in teardown. Nothing here is imported, saved, or written to the
    /// project, so the calibration cases need no fixture asset and no avatar.
    /// </summary>
    internal sealed class CollectorTestScene
    {
        private readonly List<Object> _created = new List<Object>();

        internal GameObject NewRoot(string name)
        {
            var root = new GameObject(name);
            _created.Add(root);
            return root;
        }

        internal GameObject NewChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            // Parented objects are destroyed with their root; tracking the root
            // alone is enough and double-tracking would double-destroy.
            return child;
        }

        /// <summary>
        /// A mesh of <paramref name="submeshCount"/> triangle submeshes, one
        /// triangle each, with UV0 present. Vertices are distinct per submesh so
        /// no submesh shares an index range with another.
        /// </summary>
        internal Mesh NewTriangleMesh(int submeshCount)
        {
            var mesh = new Mesh { name = "CensusTestTriangles" };
            var vertices = new Vector3[submeshCount * 3];
            var uv = new Vector2[submeshCount * 3];
            for (var i = 0; i < submeshCount; i++)
            {
                vertices[i * 3] = new Vector3(i, 0f, 0f);
                vertices[i * 3 + 1] = new Vector3(i, 1f, 0f);
                vertices[i * 3 + 2] = new Vector3(i + 1f, 0f, 0f);
                uv[i * 3] = new Vector2(0f, 0f);
                uv[i * 3 + 1] = new Vector2(0f, 1f);
                uv[i * 3 + 2] = new Vector2(1f, 0f);
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.subMeshCount = submeshCount;
            for (var i = 0; i < submeshCount; i++)
            {
                mesh.SetTriangles(
                    new[] { i * 3, i * 3 + 1, i * 3 + 2 }, i, false);
            }

            _created.Add(mesh);
            return mesh;
        }

        /// <summary>One submesh of quad topology, which AMUSE refuses.</summary>
        internal Mesh NewQuadMesh()
        {
            var mesh = new Mesh { name = "CensusTestQuad" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f),
                new Vector3(1f, 1f, 0f), new Vector3(1f, 0f, 0f),
            };
            mesh.subMeshCount = 1;
            mesh.SetIndices(
                new[] { 0, 1, 2, 3 }, MeshTopology.Quads, 0, false);
            _created.Add(mesh);
            return mesh;
        }

        internal Material NewStandardMaterial()
        {
            var material = new Material(Shader.Find("Standard"))
            {
                name = "CensusTestStandard",
            };
            _created.Add(material);
            return material;
        }

        /// <summary>
        /// A tracked material on a caller-supplied shader, for the gate cases,
        /// which need vendor shaders rather than Standard. Tracked and
        /// destroyed exactly as every other object here, so a gate run leaves
        /// no material behind in the Lab.
        /// </summary>
        internal Material NewMaterial(Shader shader, string name)
        {
            var material = new Material(shader) { name = name };
            _created.Add(material);
            return material;
        }

        internal GameObject NewMeshRenderer(
            GameObject parent, string name, Mesh mesh, params Material[] materials)
        {
            var go = NewChild(parent, name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        internal void Destroy()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }
    }
}
