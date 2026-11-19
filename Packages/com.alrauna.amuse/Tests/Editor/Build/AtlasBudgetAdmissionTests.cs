using System;
using System.Collections.Generic;
using Alrauna.Amuse.Editor.Analysis;
using Alrauna.Amuse.Editor.Build;
using Alrauna.Amuse.Editor.Host;
using Alrauna.Amuse.Editor.Semantics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Alrauna.Amuse.Tests.Editor.Build
{
    public sealed class AtlasBudgetAdmissionTests
    {
        private enum BudgetNumber
        {
            Group,
            Avatar,
            Peak
        }

        private static readonly AtlasSamplerFacts Sampler =
            new AtlasSamplerFacts(
                FilterMode.Bilinear,
                UnityEngine.TextureWrapMode.Repeat,
                UnityEngine.TextureWrapMode.Repeat,
                0,
                0f);

        // The boundary group holds one 81 by 1 R8 source on one 82 by 1
        // R8 page. The source bytes are 81. The retained bytes are 82.
        private const long BoundarySourceBytes = 81;
        private const long BoundaryRetainedBytes = 82;

        // The hand-computed page prediction of the boundary group at
        // P = 1: compose buffer 328, page bytes 82, raw levels 81,
        // Color32 array 324, largest clone 81, check decode 328 + 324.
        private const long BoundaryPeakBytes = 1548;

        private static CapturedAtlasUse R8Use(
            int instanceId,
            int width,
            int height,
            int captureOrder)
        {
            return new CapturedAtlasUse(
                0,
                0,
                0,
                "Poiyomi 9.3 release main",
                instanceId,
                GraphicsFormat.R8_UNorm,
                width,
                height,
                1,
                Sampler,
                new AtlasUvRect(0.0, 0.0, 1.0, 1.0),
                "material",
                Array.Empty<string>(),
                captureOrder);
        }

        private static AtlasGroupPlan R8Group(
            string materialKey,
            int sourceInstanceId,
            int sourceWidth,
            int pageWidth,
            int useIndex)
        {
            var page = new AtlasPagePlanEntry(
                pageWidth,
                1,
                GraphicsFormat.R8_UNorm,
                1,
                new[]
                {
                    new AtlasPlacement(
                        sourceInstanceId,
                        Sampler,
                        0,
                        0,
                        1,
                        1,
                        0,
                        0,
                        0,
                        new[] { useIndex }),
                });
            var key = new AtlasGroupKey(
                "Poiyomi 9.3 release main",
                AtlasGroupKey.MainColorRouteName,
                GraphicsFormat.R8_UNorm,
                Sampler,
                materialKey,
                Array.Empty<string>());
            return new AtlasGroupPlan(
                key,
                new[] { page },
                new[] { useIndex },
                Array.Empty<AtlasRefusedUse>());
        }

        private static IReadOnlyList<CapturedAtlasUse> BoundaryUses()
        {
            return new[] { R8Use(7, 81, 1, 0) };
        }

        private static AtlasGroupPlan BoundaryPlan()
        {
            return R8Group("boundary", 7, 81, 82, 0);
        }

        private static AtlasBudgetLimits LimitsAt(
            BudgetNumber number,
            long shortBy)
        {
            switch (number)
            {
                case BudgetNumber.Group:
                    // The factor 82 / 81 sits exactly at the limit,
                    // because 82 * 81 equals 81 * 82. The factor 1 / 1
                    // lands one byte over, because 82 * 1 exceeds
                    // 81 * 1 by one.
                    return shortBy == 0
                        ? new AtlasBudgetLimits(
                            BoundaryRetainedBytes,
                            BoundarySourceBytes,
                            long.MaxValue,
                            long.MaxValue)
                        : new AtlasBudgetLimits(
                            1,
                            1,
                            long.MaxValue,
                            long.MaxValue);
                case BudgetNumber.Avatar:
                    return new AtlasBudgetLimits(
                        1000,
                        1,
                        BoundaryRetainedBytes - shortBy,
                        long.MaxValue);
                case BudgetNumber.Peak:
                    return new AtlasBudgetLimits(
                        1000,
                        1,
                        long.MaxValue,
                        BoundaryPeakBytes - shortBy);
                default:
                    throw new ArgumentOutOfRangeException(nameof(number));
            }
        }

        [Test]
        public void ProviderReturnsUnlimitedByDefault()
        {
            Assert.That(
                AtlasBudgetLimits.Provider().IsUnlimited,
                Is.True,
                "the production provider must return Unlimited"
                + " before Milestone 5 sets the measured values");
        }

        [TestCase((int)BudgetNumber.Group)]
        [TestCase((int)BudgetNumber.Avatar)]
        [TestCase((int)BudgetNumber.Peak)]
        public void GroupExactlyAtEachLimitIsAdmitted(int number)
        {
            var plans = new[] { BoundaryPlan() };

            var results = AtlasBudgetAdmission.Admit(
                plans,
                BoundaryUses(),
                1,
                LimitsAt((BudgetNumber)number, 0));

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(AtlasUseRefusal.None));
        }

        [TestCase((int)BudgetNumber.Group, (int)AtlasUseRefusal.GroupBudget)]
        [TestCase((int)BudgetNumber.Avatar, (int)AtlasUseRefusal.AvatarBudget)]
        [TestCase((int)BudgetNumber.Peak, (int)AtlasUseRefusal.PeakBudget)]
        public void OneByteMoreRefusesIt(int number, int expectedRefusal)
        {
            var plans = new[] { BoundaryPlan() };

            var results = AtlasBudgetAdmission.Admit(
                plans,
                BoundaryUses(),
                1,
                LimitsAt((BudgetNumber)number, 1));

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(
                results[0],
                Is.EqualTo((AtlasUseRefusal)expectedRefusal));
        }

        [Test]
        public void GroupThatTheAvatarLimitRefusesDoesNotEnterTheSum()
        {
            var plans = new[]
            {
                R8Group("large", 900, 200, 200, 0),
                R8Group("small", 901, 50, 50, 1),
            };
            var uses = new[]
            {
                R8Use(900, 200, 1, 0),
                R8Use(901, 50, 1, 1),
            };
            var limits = new AtlasBudgetLimits(1000, 1, 100, long.MaxValue);

            var results = AtlasBudgetAdmission.Admit(plans, uses, 1, limits);

            Assert.That(
                results[0],
                Is.EqualTo(AtlasUseRefusal.AvatarBudget),
                "the large group passes the limit of 100 bytes");
            Assert.That(
                results[1],
                Is.EqualTo(AtlasUseRefusal.None),
                "the refused bytes stay out of the sum, so the later"
                + " smaller group still fits");
        }

        [Test]
        public void AGroupThatFailsTwoChecksNamesTheFirstCheck()
        {
            var plans = new[] { BoundaryPlan() };

            var allThreeFail = AtlasBudgetAdmission.Admit(
                plans,
                BoundaryUses(),
                1,
                new AtlasBudgetLimits(1, 1, 1, 1));
            Assert.That(
                allThreeFail[0],
                Is.EqualTo(AtlasUseRefusal.GroupBudget),
                "the group check runs first");

            var peakAndAvatarFail = AtlasBudgetAdmission.Admit(
                plans,
                BoundaryUses(),
                1,
                new AtlasBudgetLimits(1000, 1, 1, 1));
            Assert.That(
                peakAndAvatarFail[0],
                Is.EqualTo(AtlasUseRefusal.PeakBudget),
                "the peak check runs before the avatar check");
        }

        [Test]
        public void UnlimitedAdmitsAnEightKilotexelGroup()
        {
            var format = GraphicsFormat.RGBA_BC7_SRGB;
            var uses = new[]
            {
                new CapturedAtlasUse(
                    0,
                    0,
                    0,
                    "Poiyomi 9.3 release main",
                    9001,
                    format,
                    4096,
                    4096,
                    13,
                    Sampler,
                    new AtlasUvRect(0.0, 0.0, 1.0, 1.0),
                    "material",
                    Array.Empty<string>(),
                    0),
                new CapturedAtlasUse(
                    0,
                    1,
                    1,
                    "Poiyomi 9.3 release main",
                    9002,
                    format,
                    4096,
                    4096,
                    13,
                    Sampler,
                    new AtlasUvRect(0.0, 0.0, 1.0, 1.0),
                    "material",
                    Array.Empty<string>(),
                    1),
            };
            var page = new AtlasPagePlanEntry(
                8192,
                4096,
                format,
                14,
                new[]
                {
                    new AtlasPlacement(
                        9001, Sampler, 0, 0, 4096, 4096, 0, 0, 0,
                        new[] { 0 }),
                    new AtlasPlacement(
                        9002, Sampler, 0, 0, 4096, 4096, 0, 4096, 0,
                        new[] { 1 }),
                });
            var key = new AtlasGroupKey(
                "Poiyomi 9.3 release main",
                AtlasGroupKey.MainColorRouteName,
                format,
                Sampler,
                "material",
                Array.Empty<string>());
            var plans = new[]
            {
                new AtlasGroupPlan(
                    key,
                    new[] { page },
                    new[] { 0, 1 },
                    Array.Empty<AtlasRefusedUse>()),
            };

            var results = AtlasBudgetAdmission.Admit(
                plans,
                uses,
                1,
                AtlasBudgetLimits.Provider());

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0], Is.EqualTo(AtlasUseRefusal.None));
        }
    }
}
