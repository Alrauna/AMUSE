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

        private static CapturedAtlasUse UseOf(
            int instanceId,
            GraphicsFormat format,
            int width,
            int height,
            int mipCount,
            int captureOrder)
        {
            return new CapturedAtlasUse(
                0,
                captureOrder,
                captureOrder,
                "Poiyomi 9.3 release main",
                instanceId,
                format,
                width,
                height,
                mipCount,
                Sampler,
                new AtlasUvRect(0.0, 0.0, 1.0, 1.0),
                "material",
                Array.Empty<string>(),
                captureOrder);
        }

        // One page that places each listed use as its own source. The
        // placements may overlap, because admission reads only the
        // page facts and the distinct sources.
        private static AtlasGroupPlan PageOf(
            string materialKey,
            GraphicsFormat format,
            int pageWidth,
            int pageHeight,
            int mipCount,
            IReadOnlyList<CapturedAtlasUse> uses,
            int firstUse,
            int useCount)
        {
            var placements = new AtlasPlacement[useCount];
            var useIndices = new int[useCount];
            for (var i = 0; i < useCount; i++)
            {
                var use = uses[firstUse + i];
                useIndices[i] = firstUse + i;
                placements[i] = new AtlasPlacement(
                    use.SourceTextureInstanceId,
                    Sampler,
                    0,
                    0,
                    use.SourceWidth,
                    use.SourceHeight,
                    0,
                    0,
                    0,
                    new[] { firstUse + i });
            }

            var page = new AtlasPagePlanEntry(
                pageWidth,
                pageHeight,
                format,
                mipCount,
                placements);
            var key = new AtlasGroupKey(
                "Poiyomi 9.3 release main",
                AtlasGroupKey.MainColorRouteName,
                format,
                Sampler,
                materialKey,
                Array.Empty<string>());
            return new AtlasGroupPlan(
                key,
                new[] { page },
                useIndices,
                Array.Empty<AtlasRefusedUse>());
        }

        /// <summary>
        /// --- Falsifier 3: a group factor of 1 refuses the 100 byte
        /// page, and a factor of 3/2 admits the 101 byte page. The
        /// production factor 5/4 limits an 80 byte source to exactly
        /// 100 page bytes. ---
        /// </summary>
        [Test]
        public void ProductionGroupFactorAdmitsAQuarterOfBorder()
        {
            var uses = new[] { R8Use(11, 80, 1, 0) };

            var atLimit = AtlasBudgetAdmission.Admit(
                new[] { R8Group("factor", 11, 80, 100, 0) },
                uses,
                1,
                AtlasBudgetLimits.Provider());
            var overLimit = AtlasBudgetAdmission.Admit(
                new[] { R8Group("factor", 11, 80, 101, 0) },
                uses,
                1,
                AtlasBudgetLimits.Provider());

            Assert.That(
                atLimit[0].Refusal,
                Is.EqualTo(AtlasUseRefusal.None),
                "a page of exactly 5/4 of its source bytes is admitted");
            Assert.That(
                overLimit[0].Refusal,
                Is.EqualTo(AtlasUseRefusal.GroupBudget));
            Assert.That(overLimit[0].ValueBytes, Is.EqualTo(101));
            Assert.That(overLimit[0].LimitBytes, Is.EqualTo(100));
        }

        /// <summary>
        /// --- Falsifier 4: a limit of 500,000,000 bytes, the decimal
        /// reading of 500 MB, refuses the eighth group. The VRChat SDK
        /// limit is 500 MiB. Eight groups of 65,536,000 bytes fill it
        /// exactly, and one more byte passes it. ---
        /// </summary>
        [Test]
        public void ProductionAvatarLimitAdmitsExactlyFiveHundredMebibytes()
        {
            var uses = new List<CapturedAtlasUse>();
            var plans = new List<AtlasGroupPlan>();
            for (var i = 0; i < 8; i++)
            {
                uses.Add(UseOf(100 + i, GraphicsFormat.R8_UNorm, 8192, 8000, 1, i));
                plans.Add(PageOf(
                    "avatar" + i, GraphicsFormat.R8_UNorm, 8192, 8000, 1, uses, i, 1));
            }

            uses.Add(UseOf(200, GraphicsFormat.R8_UNorm, 1, 1, 1, 8));
            plans.Add(PageOf("avatar8", GraphicsFormat.R8_UNorm, 1, 1, 1, uses, 8, 1));

            var results = AtlasBudgetAdmission.Admit(
                plans,
                uses,
                1,
                AtlasBudgetLimits.Provider());

            for (var i = 0; i < 8; i++)
            {
                Assert.That(
                    results[i].Refusal,
                    Is.EqualTo(AtlasUseRefusal.None),
                    "group " + i + " fits under 500 MiB");
            }

            Assert.That(results[8].Refusal, Is.EqualTo(AtlasUseRefusal.AvatarBudget));
            Assert.That(results[8].ValueBytes, Is.EqualTo(524288001L));
            Assert.That(results[8].LimitBytes, Is.EqualTo(524288000L));
        }

        /// <summary>
        /// --- Falsifier 5: a peak limit of 2 GiB refuses five full size
        /// sources, and no peak limit admits six. The prediction for
        /// five sources is 4,294,967,292 bytes, four bytes under 4 GiB.
        /// Each further source adds 536,870,912 bytes. ---
        /// </summary>
        [Test]
        public void ProductionPeakLimitAdmitsFiveFullSizeSources()
        {
            var format = GraphicsFormat.R8G8B8A8_SRGB;
            var uses = new List<CapturedAtlasUse>();
            for (var i = 0; i < 6; i++)
            {
                uses.Add(UseOf(300 + i, format, 8192, 8192, 14, i));
            }

            var five = AtlasBudgetAdmission.Admit(
                new[] { PageOf("peak", format, 8192, 8192, 14, uses, 0, 5) },
                uses,
                1,
                AtlasBudgetLimits.Provider());
            var six = AtlasBudgetAdmission.Admit(
                new[] { PageOf("peak", format, 8192, 8192, 14, uses, 0, 6) },
                uses,
                1,
                AtlasBudgetLimits.Provider());

            Assert.That(five[0].Refusal, Is.EqualTo(AtlasUseRefusal.None));
            Assert.That(six[0].Refusal, Is.EqualTo(AtlasUseRefusal.PeakBudget));
            Assert.That(six[0].ValueBytes, Is.EqualTo(4831838204L));
            Assert.That(six[0].LimitBytes, Is.EqualTo(4294967296L));
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
            Assert.That(results[0].Refusal, Is.EqualTo(AtlasUseRefusal.None));
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
                results[0].Refusal,
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
                results[0].Refusal,
                Is.EqualTo(AtlasUseRefusal.AvatarBudget),
                "the large group passes the limit of 100 bytes");
            Assert.That(
                results[1].Refusal,
                Is.EqualTo(AtlasUseRefusal.None),
                "the refused bytes stay out of the sum, so the later"
                + " smaller group still fits");
        }

        [Test]
        public void AvatarLimitComparesTheRunningSumOfAdmittedGroups()
        {
            var plans = new[]
            {
                R8Group("sixty", 910, 60, 60, 0),
                R8Group("fifty", 911, 50, 50, 1),
                R8Group("forty", 912, 40, 40, 2),
            };
            var uses = new[]
            {
                R8Use(910, 60, 1, 0),
                R8Use(911, 50, 1, 1),
                R8Use(912, 40, 1, 2),
            };
            var limits = new AtlasBudgetLimits(1000, 1, 100, long.MaxValue);

            var results = AtlasBudgetAdmission.Admit(plans, uses, 1, limits);

            Assert.That(
                results[0].Refusal,
                Is.EqualTo(AtlasUseRefusal.None),
                "the first group of 60 bytes fits the limit of 100"
                + " bytes on its own");
            Assert.That(
                results[1].Refusal,
                Is.EqualTo(AtlasUseRefusal.AvatarBudget),
                "the second group would raise the running sum to 110"
                + " bytes, above the limit");
            Assert.That(
                results[2].Refusal,
                Is.EqualTo(AtlasUseRefusal.None),
                "the refused 50 bytes stayed out of the sum, so 60"
                + " plus 40 reaches the limit exactly and the third"
                + " group stays admitted");
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
                allThreeFail[0].Refusal,
                Is.EqualTo(AtlasUseRefusal.GroupBudget),
                "the group check runs first");

            var peakAndAvatarFail = AtlasBudgetAdmission.Admit(
                plans,
                BoundaryUses(),
                1,
                new AtlasBudgetLimits(1000, 1, 1, 1));
            Assert.That(
                peakAndAvatarFail[0].Refusal,
                Is.EqualTo(AtlasUseRefusal.PeakBudget),
                "the peak check runs before the avatar check");
        }

        /// <summary>
        /// The shape of the upload fixture: two 4096 by 4096 RGB24
        /// sources on one 8192 by 4096 page. The production limits
        /// admit it. This test also passed with unlimited budgets, so it
        /// is a characterization test.
        /// </summary>
        [Test]
        public void ProductionLimitsAdmitTheUploadFixtureShape()
        {
            var format = GraphicsFormat.R8G8B8_SRGB;
            var uses = new[]
            {
                UseOf(9001, format, 4096, 4096, 13, 0),
                UseOf(9002, format, 4096, 4096, 13, 1),
            };

            var results = AtlasBudgetAdmission.Admit(
                new[] { PageOf("material", format, 8192, 4096, 14, uses, 0, 2) },
                uses,
                1,
                AtlasBudgetLimits.Provider());

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Refusal, Is.EqualTo(AtlasUseRefusal.None));
        }

        /// <summary>
        /// --- Falsifier 1: a verdict that states the unrounded or the
        /// rounded-up group limit. The source bytes 81 times 3 / 4 give
        /// 60.75. The trip rule compares integers, so the exact limit
        /// is the floor 60. ---
        /// </summary>
        [Test]
        public void GroupBudgetVerdictStatesTheFlooredLimit()
        {
            var results = AtlasBudgetAdmission.Admit(
                new[] { BoundaryPlan() },
                BoundaryUses(),
                1,
                new AtlasBudgetLimits(3, 4, long.MaxValue, long.MaxValue));

            Assert.That(
                results[0].Refusal, Is.EqualTo(AtlasUseRefusal.GroupBudget));
            Assert.That(results[0].ValueBytes,
                Is.EqualTo(BoundaryRetainedBytes));
            Assert.That(results[0].LimitBytes, Is.EqualTo(60L));
        }

        [Test]
        public void PeakBudgetVerdictStatesThePrediction()
        {
            var results = AtlasBudgetAdmission.Admit(
                new[] { BoundaryPlan() },
                BoundaryUses(),
                1,
                LimitsAt(BudgetNumber.Peak, 1));

            Assert.That(
                results[0].Refusal, Is.EqualTo(AtlasUseRefusal.PeakBudget));
            Assert.That(results[0].ValueBytes, Is.EqualTo(BoundaryPeakBytes));
            Assert.That(results[0].LimitBytes,
                Is.EqualTo(BoundaryPeakBytes - 1));
        }

        /// <summary>
        /// --- Falsifier 2: a verdict that states only the bytes of the
        /// refused group. The avatar number compares the running sum,
        /// so the value is the sum the group would make. ---
        /// </summary>
        [Test]
        public void AvatarBudgetVerdictStatesTheRunningSum()
        {
            var plans = new[]
            {
                R8Group("sixty", 910, 60, 60, 0),
                R8Group("fifty", 911, 50, 50, 1),
            };
            var uses = new[]
            {
                R8Use(910, 60, 1, 0),
                R8Use(911, 50, 1, 1),
            };

            var results = AtlasBudgetAdmission.Admit(
                plans, uses, 1,
                new AtlasBudgetLimits(1000, 1, 100, long.MaxValue));

            Assert.That(
                results[1].Refusal, Is.EqualTo(AtlasUseRefusal.AvatarBudget));
            Assert.That(results[1].ValueBytes, Is.EqualTo(110L));
            Assert.That(results[1].LimitBytes, Is.EqualTo(100L));
        }
    }
}
