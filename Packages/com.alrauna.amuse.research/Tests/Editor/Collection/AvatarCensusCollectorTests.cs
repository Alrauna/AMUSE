using System;
using System.Collections.Generic;
using System.Reflection;
using Alrauna.Amuse.Research.Census;
using Alrauna.Amuse.Research.Collection;
using NUnit.Framework;
using UnityEngine;

namespace Alrauna.Amuse.Research.Tests.Editor.Collection
{
    public sealed class AvatarCensusCollectorTests
    {
        private CollectorTestScene _scene;

        [SetUp]
        public void SetUp() => _scene = new CollectorTestScene();

        [TearDown]
        public void TearDown() => _scene.Destroy();

        [Test]
        public void CollectsEveryRendererUnderTheRootIncludingInactive()
        {
            // An inactive renderer still ships with the avatar and an animation
            // can re-enable it, so excluding it would understate the avatar.
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Active", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());
            var hidden = _scene.NewMeshRenderer(
                root, "Inactive", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());
            hidden.SetActive(false);

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(observed.Renderers.Count, Is.EqualTo(2));
        }

        [Test]
        public void NeverCollectsRenderersOutsideTheGivenRoot()
        {
            // Scope containment: the collector observes what the caller named
            // and nothing else in the scene.
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mine", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var other = _scene.NewRoot("SomeoneElse");
            _scene.NewMeshRenderer(
                other, "NotMine", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(observed.Renderers.Count, Is.EqualTo(1));
            Assert.That(
                observed.Renderers[0].GameObjectName, Is.EqualTo("Mine"));
        }

        [Test]
        public void HierarchyPathIsRelativeToTheCollectionRoot()
        {
            // An absolute scene path would leak the structure above the avatar,
            // which is the operator's project rather than the observation.
            var root = _scene.NewRoot("Avatar");
            var body = _scene.NewChild(root, "Body");
            _scene.NewMeshRenderer(
                body, "Face", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(
                observed.Renderers[0].HierarchyPath, Is.EqualTo("Body/Face"));
        }

        [Test]
        public void RendererOnTheRootItselfHasAnEmptyPath()
        {
            var root = _scene.NewRoot("Avatar");
            root.AddComponent<MeshFilter>().sharedMesh =
                _scene.NewTriangleMesh(1);
            root.AddComponent<MeshRenderer>().sharedMaterials =
                new[] { _scene.NewStandardMaterial() };

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(
                observed.Renderers[0].HierarchyPath, Is.EqualTo(string.Empty));
        }

        [Test]
        public void RecordsAvatarNameAndSuppliedCreator()
        {
            var root = _scene.NewRoot("Avatar");

            var observed = AvatarCensusCollector.Collect(root, "Someone");

            Assert.That(observed.AvatarName, Is.EqualTo("Avatar"));
            Assert.That(observed.CreatorName, Is.EqualTo("Someone"));
        }

        [Test]
        public void SceneObjectHasNoAssetIdentity()
        {
            // A scene instance is not an asset. Null is the honest record, and
            // the anonymizer reads no avatar identity field, so it costs nothing
            // downstream.
            var root = _scene.NewRoot("Avatar");

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.That(observed.AssetPath, Is.Null);
            Assert.That(observed.AssetGuid, Is.Null);
        }

        [Test]
        public void RuntimeMaterialHasNameButNoAssetIdentity()
        {
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var submesh = AvatarCensusCollector
                .Collect(root, null).Renderers[0].Submeshes[0];

            Assert.That(
                submesh.MaterialName, Is.EqualTo("CensusTestStandard"));
            Assert.That(submesh.MaterialAssetPath, Is.Null);
            Assert.That(submesh.MaterialAssetGuid, Is.Null);
            Assert.That(submesh.ShaderName, Is.EqualTo("Standard"));
        }

        [Test]
        public void NullRootIsRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => AvatarCensusCollector.Collect(null, null));
        }

        [Test]
        public void ObservedListsAreReadOnly()
        {
            var root = _scene.NewRoot("Avatar");
            _scene.NewMeshRenderer(
                root, "Mesh", _scene.NewTriangleMesh(1),
                _scene.NewStandardMaterial());

            var observed = AvatarCensusCollector.Collect(root, null);

            Assert.Throws<NotSupportedException>(
                () => ((IList<ObservedRenderer>)observed.Renderers)
                    .Add(observed.Renderers[0]));
        }

        [Test]
        public void ThePublicSurfaceIsExactlyOneTypeWithOneMethod()
        {
            // Review change 4, asserted rather than promised. A configuration
            // object, a provider parameter, or a second entry point would all
            // show up here.
            var exported = typeof(AvatarCensusCollector).Assembly
                .GetExportedTypes();

            CollectionAssert.AreEqual(
                new[] { typeof(AvatarCensusCollector) }, exported);

            var methods = new List<string>();
            foreach (var method in typeof(AvatarCensusCollector).GetMethods(
                         BindingFlags.Public | BindingFlags.Static |
                         BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                methods.Add(method.Name);
            }

            CollectionAssert.AreEqual(new[] { "Collect" }, methods);
        }

        [Test]
        public void NoPublicEntryPointCanCollectWithoutACallerSuppliedRoot()
        {
            // The privacy requirement expressed as a signature: there is no
            // discovery, no scene scan, and no project search. Every public way
            // to produce an ObservedAvatar demands a GameObject the caller named.
            var offenders = new List<string>();
            foreach (var type in typeof(AvatarCensusCollector).Assembly
                         .GetExportedTypes())
            {
                foreach (var method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.Static |
                             BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (method.ReturnType != typeof(ObservedAvatar) &&
                        method.ReturnType != typeof(CensusObservationSet))
                    {
                        continue;
                    }

                    var takesRoot = false;
                    foreach (var parameter in method.GetParameters())
                    {
                        if (parameter.ParameterType == typeof(GameObject))
                        {
                            takesRoot = true;
                        }
                    }

                    if (!takesRoot)
                    {
                        offenders.Add(type.FullName + "." + method.Name);
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders);
        }
    }
}
