using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Build;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    /// <summary>
    /// The cleanup ruling of the atlas write: an owned object that a
    /// committed object reference curve names stays alive even when no
    /// renderer references it, because a destroyed keyframe would
    /// serialize as a missing reference. The fixtures build the owned
    /// set, the renderer output, and the curve reference set directly,
    /// because authoring a curve keyframe that names a clone built
    /// during the same build needs the whole alpha path around it.
    /// The end to end suite covers the real pass order.
    /// </summary>
    public sealed class AtlasWriteCleanupTests
    {
        private readonly List<UnityEngine.Object> _created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    UnityEngine.Object.DestroyImmediate(created);
                }
            }
        }

        private T Track<T>(T obj) where T : UnityEngine.Object
        {
            _created.Add(obj);
            return obj;
        }

        /// <summary>
        /// One state whose owned set holds the slot material, one
        /// renderer whose slot the write replaces, and a curve
        /// reference set the caller controls.
        /// </summary>
        private AmusePlatformFinishState NewWriteFixture(
            out AtlasRendererOutput output,
            out Material clone)
        {
            var root = Track(new GameObject(
                "AMUSE atlas write cleanup")).transform;
            var mesh = Track(new Mesh());
            mesh.vertices = new[]
            {
                Vector3.zero, Vector3.right, Vector3.up,
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            clone = Track(new Material(Shader.Find("Standard")));
            clone.name = "AMUSE atlas write clone";
            var child = Track(new GameObject("body"));
            child.transform.SetParent(root, false);
            var renderer = child.AddComponent<MeshRenderer>();
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer.sharedMaterial = clone;

            var state = new AmusePlatformFinishState();
            state.AmuseOwnedObjects.Add(clone);
            output = new AtlasRendererOutput(
                root.gameObject, renderer);
            var pageMesh = Track(new Mesh());
            pageMesh.vertices = mesh.vertices;
            pageMesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            var pageMaterial = Track(new Material(clone));
            pageMaterial.name = "AMUSE atlas write page";
            output.NewMesh = pageMesh;
            output.NewSlots = new[] { pageMaterial };
            return state;
        }

        [Test]
        public void CurveReferencedOwnedCloneSurvivesCleanup()
        {
            var state = NewWriteFixture(
                out var output, out var clone);

            AtlasWrite.Apply(
                state,
                new[] { output },
                new UnityEngine.Object[] { clone });

            Assert.That(clone != null, Is.True,
                "an owned object a curve keyframe names must stay" +
                " alive after the cleanup");
        }

        [Test]
        public void UnreferencedOwnedCloneIsDestroyedByCleanup()
        {
            var state = NewWriteFixture(
                out var output, out var clone);

            AtlasWrite.Apply(
                state,
                new[] { output },
                Array.Empty<UnityEngine.Object>());

            Assert.That(clone == null, Is.True,
                "an owned object nothing references must be destroyed" +
                " by the cleanup");
        }
    }
}
