using System;
using System.Collections.Generic;
using System.Linq;
using Alrauna.Amuse.Editor.Analysis;
using NUnit.Framework;

namespace Alrauna.Amuse.Tests.Editor.Analysis
{
    public sealed class MeshSeparationPlannerTests
    {
        private static MeshSeparationInput OneSubmesh(
            params TriangleAlphaOutcome[] outcomes)
        {
            return OneSubmeshWithBinding(0, outcomes);
        }

        private static MeshSeparationInput OneSubmeshWithBinding(
            int sourceMaterialBindingIndex,
            params TriangleAlphaOutcome[] outcomes)
        {
            var indices = new int[outcomes.Length * 3];
            for (var index = 0; index < indices.Length; index++)
                indices[index] = index;

            return new MeshSeparationInput(
                indices.Length,
                new[]
                {
                    new SubmeshSeparationInput(
                        sourceMaterialBindingIndex,
                        indices,
                        outcomes)
                });
        }

        private static void AssertOrdinals(
            IReadOnlyList<int> actual,
            params int[] expected)
        {
            CollectionAssert.AreEqual(expected, actual);
        }

        [Test]
        public void EmptyMeshIsAValidNoOp()
        {
            var input = new MeshSeparationInput(
                0,
                Array.Empty<SubmeshSeparationInput>());

            var plan = MeshSeparationPlanner.Create(input);

            Assert.That(plan.Submeshes, Is.Empty);
            Assert.That(plan.HasAnyOpaqueCandidates, Is.False);
            Assert.That(plan.RequiresAnySplit, Is.False);
            Assert.That(plan.OpaqueTriangleCount, Is.Zero);
            Assert.That(plan.TransparentTriangleCount, Is.Zero);
        }

        [Test]
        public void InputCopiesCallerCollections()
        {
            var indices = new[] { 0, 1, 2 };
            var outcomes = new[] { TriangleAlphaOutcome.Unknown };
            var submeshes = new[]
            {
                new SubmeshSeparationInput(4, indices, outcomes)
            };
            var input = new MeshSeparationInput(3, submeshes);

            indices[0] = 2;
            outcomes[0] = TriangleAlphaOutcome.ProvenOpaque;
            submeshes[0] = new SubmeshSeparationInput(
                9,
                Array.Empty<int>(),
                Array.Empty<TriangleAlphaOutcome>());

            Assert.That(input.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(4));
            Assert.That(input.Submeshes[0].Indices[0], Is.Zero);
            Assert.That(
                input.Submeshes[0].Outcomes[0],
                Is.EqualTo(TriangleAlphaOutcome.Unknown));
        }

        [Test]
        public void NullAndNegativeMeshInputsThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
                MeshSeparationPlanner.Create(null));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeshSeparationInput(
                    -1,
                    Array.Empty<SubmeshSeparationInput>()));
            Assert.Throws<ArgumentNullException>(() =>
                new MeshSeparationInput(0, null));
            Assert.Throws<ArgumentNullException>(() =>
                new MeshSeparationInput(
                    0,
                    new SubmeshSeparationInput[] { null }));
        }

        [Test]
        public void MalformedSubmeshInputsThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SubmeshSeparationInput(
                    -1,
                    Array.Empty<int>(),
                    Array.Empty<TriangleAlphaOutcome>()));
            Assert.Throws<ArgumentNullException>(() =>
                new SubmeshSeparationInput(
                    0,
                    null,
                    Array.Empty<TriangleAlphaOutcome>()));
            Assert.Throws<ArgumentNullException>(() =>
                new SubmeshSeparationInput(
                    0,
                    Array.Empty<int>(),
                    null));
            Assert.Throws<ArgumentException>(() =>
                new SubmeshSeparationInput(
                    0,
                    new[] { 0, 1 },
                    Array.Empty<TriangleAlphaOutcome>()));
            Assert.Throws<ArgumentException>(() =>
                new SubmeshSeparationInput(
                    0,
                    new[] { 0, 1, 2 },
                    Array.Empty<TriangleAlphaOutcome>()));
            Assert.Throws<ArgumentException>(() =>
                new SubmeshSeparationInput(
                    0,
                    new[] { 0, 1, 2 },
                    new[]
                    {
                        TriangleAlphaOutcome.Unknown,
                        TriangleAlphaOutcome.Unknown
                    }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SubmeshSeparationInput(
                    0,
                    new[] { 0, 1, 2 },
                    new[] { (TriangleAlphaOutcome)999 }));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void OutOfRangeVertexIndicesThrow(int invalidIndex)
        {
            var submesh = new SubmeshSeparationInput(
                0,
                new[] { 0, 1, invalidIndex },
                new[] { TriangleAlphaOutcome.Unknown });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new MeshSeparationInput(3, new[] { submesh }));
        }

        [Test]
        public void MustRemainTransparentTrianglesKeepSubmeshUnchanged()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.MustRemainTransparent));

            var submesh = plan.Submeshes[0];
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            AssertOrdinals(submesh.OpaqueTriangleOrdinals);
            AssertOrdinals(submesh.TransparentTriangleOrdinals, 0, 1, 2, 3);
        }

        [Test]
        public void UnknownTrianglesKeepSubmeshUnchanged()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.Unknown));

            Assert.That(plan.HasAnyOpaqueCandidates, Is.False);
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 0, 1, 2, 3);
        }

        [Test]
        public void MixedTransparentAndUnknownOutcomesStayInSourceOrder()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.Unknown));

            Assert.That(
                plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 0, 1, 2, 3);
        }

        [Test]
        public void EmptySubmeshRemainsRepresentedWithItsBinding()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmeshWithBinding(7));

            Assert.That(plan.Submeshes, Has.Count.EqualTo(1));
            Assert.That(plan.Submeshes[0].SourceSubmeshIndex, Is.Zero);
            Assert.That(plan.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(7));
            Assert.That(
                plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals);
        }

        [Test]
        public void AllProvenOpaqueTrianglesBecomeWhollyOpaqueCandidate()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque));

            var submesh = plan.Submeshes[0];
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.WhollyOpaqueCandidate));
            AssertOrdinals(submesh.OpaqueTriangleOrdinals, 0, 1, 2, 3);
            AssertOrdinals(submesh.TransparentTriangleOrdinals);
            Assert.That(plan.HasAnyOpaqueCandidates, Is.True);
            Assert.That(plan.RequiresAnySplit, Is.False);
        }

        [Test]
        public void ProvenOpaqueAndTransparentTrianglesRequireSplit()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.MustRemainTransparent));

            var submesh = plan.Submeshes[0];
            Assert.That(
                submesh.Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Split));
            AssertOrdinals(submesh.OpaqueTriangleOrdinals, 0, 1);
            AssertOrdinals(submesh.TransparentTriangleOrdinals, 2, 3);
            Assert.That(plan.RequiresAnySplit, Is.True);
        }

        [Test]
        public void UnknownNeverEntersOpaqueMembership()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.MustRemainTransparent));

            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0, 2);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1, 3);
        }

        [Test]
        public void MultipleSubmeshesPreserveExplicitBindingsAndSourceOrder()
        {
            var indices0 = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            var indices1 = new[] { 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23 };
            var indices2 = new[] { 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35 };
            var input = new MeshSeparationInput(
                36,
                new[]
                {
                    new SubmeshSeparationInput(4, indices0, new[]
                    {
                        TriangleAlphaOutcome.ProvenOpaque,
                        TriangleAlphaOutcome.ProvenOpaque,
                        TriangleAlphaOutcome.ProvenOpaque,
                        TriangleAlphaOutcome.MustRemainTransparent
                    }),
                    new SubmeshSeparationInput(2, indices1, new[]
                    {
                        TriangleAlphaOutcome.MustRemainTransparent,
                        TriangleAlphaOutcome.MustRemainTransparent,
                        TriangleAlphaOutcome.Unknown,
                        TriangleAlphaOutcome.MustRemainTransparent
                    }),
                    new SubmeshSeparationInput(4, indices2, new[]
                    {
                        TriangleAlphaOutcome.ProvenOpaque,
                        TriangleAlphaOutcome.ProvenOpaque,
                        TriangleAlphaOutcome.ProvenOpaque,
                        TriangleAlphaOutcome.ProvenOpaque
                    })
                });

            var plan = MeshSeparationPlanner.Create(input);

            CollectionAssert.AreEqual(
                new[]
                {
                    SubmeshSeparationDisposition.Split,
                    SubmeshSeparationDisposition.Unchanged,
                    SubmeshSeparationDisposition.WhollyOpaqueCandidate
                },
                plan.Submeshes.Select(item => item.Disposition));
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                plan.Submeshes.Select(item => item.SourceSubmeshIndex));
            CollectionAssert.AreEqual(
                new[] { 4, 2, 4 },
                plan.Submeshes.Select(item => item.SourceMaterialBindingIndex));
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0, 1, 2);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 3);
            AssertOrdinals(plan.Submeshes[1].TransparentTriangleOrdinals, 0, 1, 2, 3);
            AssertOrdinals(plan.Submeshes[2].OpaqueTriangleOrdinals, 0, 1, 2, 3);
        }

        [Test]
        public void EmptyMiddleSubmeshDoesNotShiftBindingProvenance()
        {
            var input = new MeshSeparationInput(
                3,
                new[]
                {
                    new SubmeshSeparationInput(
                        8,
                        new[] { 0, 1, 2 },
                        new[] { TriangleAlphaOutcome.ProvenOpaque }),
                    new SubmeshSeparationInput(
                        3,
                        Array.Empty<int>(),
                        Array.Empty<TriangleAlphaOutcome>()),
                    new SubmeshSeparationInput(
                        8,
                        new[] { 0, 2, 1 },
                        new[] { TriangleAlphaOutcome.Unknown })
                });

            var plan = MeshSeparationPlanner.Create(input);

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                plan.Submeshes.Select(item => item.SourceSubmeshIndex));
            CollectionAssert.AreEqual(
                new[] { 8, 3, 8 },
                plan.Submeshes.Select(item => item.SourceMaterialBindingIndex));
            Assert.That(
                plan.Submeshes[1].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Unchanged));
            AssertOrdinals(plan.Submeshes[1].OpaqueTriangleOrdinals);
            AssertOrdinals(plan.Submeshes[1].TransparentTriangleOrdinals);
        }

        [Test]
        public void RepeatedVertexIndicesAndDuplicateTrianglesRemainDistinctOccurrences()
        {
            var input = new MeshSeparationInput(
                2,
                new[]
                {
                    new SubmeshSeparationInput(
                        5,
                        new[] { 0, 1, 1, 0, 1, 1 },
                        new[]
                        {
                            TriangleAlphaOutcome.ProvenOpaque,
                            TriangleAlphaOutcome.Unknown
                        })
                });

            var plan = MeshSeparationPlanner.Create(input);

            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 1, 0, 1, 1 },
                plan.Source.Submeshes[0].Indices);
        }

        [Test]
        public void SourceIndexTriplesPreserveWindingAndOrder()
        {
            var input = new MeshSeparationInput(
                3,
                new[]
                {
                    new SubmeshSeparationInput(
                        0,
                        new[] { 0, 1, 2, 2, 1, 0 },
                        new[]
                        {
                            TriangleAlphaOutcome.ProvenOpaque,
                            TriangleAlphaOutcome.MustRemainTransparent
                        })
                });

            var plan = MeshSeparationPlanner.Create(input);

            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 2, 1, 0 },
                plan.Source.Submeshes[0].Indices);
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1);
        }

        [Test]
        public void TriangleOrdinalsRestartWithinEachSourceSubmesh()
        {
            var input = new MeshSeparationInput(
                4,
                new[]
                {
                    new SubmeshSeparationInput(
                        6,
                        new[] { 0, 1, 2 },
                        new[] { TriangleAlphaOutcome.ProvenOpaque }),
                    new SubmeshSeparationInput(
                        6,
                        new[] { 2, 3, 0 },
                        new[] { TriangleAlphaOutcome.Unknown })
                });

            var plan = MeshSeparationPlanner.Create(input);

            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
            AssertOrdinals(plan.Submeshes[1].TransparentTriangleOrdinals, 0);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                plan.Source.Submeshes[0].Indices);
            CollectionAssert.AreEqual(
                new[] { 2, 3, 0 },
                plan.Source.Submeshes[1].Indices);
        }

        [TestCase((int)TriangleAlphaOutcome.Unknown)]
        [TestCase((int)TriangleAlphaOutcome.MustRemainTransparent)]
        public void ReplacingProvenOpaqueCannotIncreaseOpaqueCount(
            int replacementValue)
        {
            var replacement = (TriangleAlphaOutcome)replacementValue;
            var baseline = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.Unknown));
            var uncertain = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.ProvenOpaque,
                replacement,
                TriangleAlphaOutcome.MustRemainTransparent,
                TriangleAlphaOutcome.Unknown));

            Assert.That(
                uncertain.OpaqueTriangleCount,
                Is.LessThanOrEqualTo(baseline.OpaqueTriangleCount));
            AssertOrdinals(uncertain.Submeshes[0].OpaqueTriangleOrdinals, 0);
            AssertOrdinals(uncertain.Submeshes[0].TransparentTriangleOrdinals, 1, 2, 3);
        }

        [Test]
        public void AlternatingOpaqueAndUnknownPreservesBothSourceOrders()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmesh(
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.Unknown,
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.Unknown));

            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0, 2);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1, 3);
        }

        [Test]
        public void OneOpaqueTriangleAmongOneThousandStillProducesAStructuralSplit()
        {
            var outcomes = Enumerable.Repeat(
                    TriangleAlphaOutcome.MustRemainTransparent,
                    1000)
                .ToArray();
            outcomes[731] = TriangleAlphaOutcome.ProvenOpaque;

            var plan = MeshSeparationPlanner.Create(OneSubmesh(outcomes));

            Assert.That(
                plan.Submeshes[0].Disposition,
                Is.EqualTo(SubmeshSeparationDisposition.Split));
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 731);
            Assert.That(
                plan.Submeshes[0].TransparentTriangleOrdinals,
                Has.Count.EqualTo(999));
        }

        [Test]
        public void CallerMutationAfterPlanCreationCannotChangePlan()
        {
            var indices = new[] { 0, 1, 2, 2, 3, 0 };
            var outcomes = new[]
            {
                TriangleAlphaOutcome.ProvenOpaque,
                TriangleAlphaOutcome.Unknown
            };
            var input = new MeshSeparationInput(
                4,
                new[]
                {
                    new SubmeshSeparationInput(11, indices, outcomes)
                });
            var plan = MeshSeparationPlanner.Create(input);

            indices[0] = 3;
            outcomes[0] = TriangleAlphaOutcome.Unknown;

            Assert.That(plan.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(11));
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
            AssertOrdinals(plan.Submeshes[0].TransparentTriangleOrdinals, 1);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2, 2, 3, 0 },
                plan.Source.Submeshes[0].Indices);
        }

        [Test]
        public void InputAndPlanViewsCannotBeMutatedByCallers()
        {
            var plan = MeshSeparationPlanner.Create(OneSubmeshWithBinding(
                12,
                TriangleAlphaOutcome.ProvenOpaque));
            var mutableMembership = plan.Submeshes[0].OpaqueTriangleOrdinals
                as IList<int>;
            var mutableIndices = plan.Source.Submeshes[0].Indices
                as IList<int>;
            var mutableOutcomes = plan.Source.Submeshes[0].Outcomes
                as IList<TriangleAlphaOutcome>;

            if (mutableMembership != null)
                Assert.Throws<NotSupportedException>(() => mutableMembership[0] = 99);
            if (mutableIndices != null)
                Assert.Throws<NotSupportedException>(() => mutableIndices[0] = 99);
            if (mutableOutcomes != null)
            {
                Assert.Throws<NotSupportedException>(() =>
                    mutableOutcomes[0] = TriangleAlphaOutcome.Unknown);
            }

            Assert.That(plan.Submeshes[0].SourceMaterialBindingIndex, Is.EqualTo(12));
            AssertOrdinals(plan.Submeshes[0].OpaqueTriangleOrdinals, 0);
            Assert.That(plan.Source.Submeshes[0].Indices[0], Is.Zero);
            Assert.That(
                plan.Source.Submeshes[0].Outcomes[0],
                Is.EqualTo(TriangleAlphaOutcome.ProvenOpaque));
        }
    }
}
