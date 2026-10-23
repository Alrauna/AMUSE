using Alrauna.Amuse.Editor.Semantics.Poiyomi;
using NUnit.Framework;

namespace Alrauna.Amuse.Research.Tests.Editor.Calibration
{
    /// <summary>
    /// The Poiyomi probe follows the admitted rows (K6,
    /// docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-
    /// design.md). These tests drive the pure selection seam. The candidates
    /// use the real row strings with a null Shader, so no vendor file is
    /// needed.
    /// </summary>
    public sealed class CensusVendorProbeTests
    {
        [Test]
        public void ProbeRowsAreTheMainRowsInRowOrder()
        {
            var rows = CensusVendorProbe.PoiyomiProbeRows();

            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(
                rows[0].ShaderName,
                Is.EqualTo(".poiyomi/Poiyomi Toon"));
            Assert.That(rows[0].IsTwoPass, Is.False);
            Assert.That(
                rows[1].ShaderName,
                Is.EqualTo(".poiyomi/Old Versions/9.3/Poiyomi Toon"));
            Assert.That(rows[1].IsTwoPass, Is.False);
        }

        [Test]
        public void SelectionPrefersTheCandidateThatMatchesARow()
        {
            var bundled = BundledMainRow();

            var candidates = new[]
            {
                // A 10.0-like shader. It carries the 9.3.64 name with
                // another GUID and another digest.
                new CensusPoiyomiCandidate(
                    ".poiyomi/Poiyomi Toon",
                    null,
                    new string('0', 32),
                    new string('a', 64)),
                new CensusPoiyomiCandidate(
                    bundled.ShaderName,
                    null,
                    bundled.AssetGuid,
                    bundled.NormalizedSourceHash),
            };

            var selected = CensusVendorProbe.SelectPoiyomi(candidates);

            Assert.That(
                selected.HasValue,
                Is.True,
                "A candidate matches an admitted row, so the selection "
                + "must report it.");
            Assert.That(
                selected.Value.ShaderName,
                Is.EqualTo(bundled.ShaderName),
                "The selection must report the candidate whose file "
                + "matches an admitted row, not the first resolved "
                + "candidate.");
        }

        [Test]
        public void SelectionReportsTheLastResolvedCandidateWhenNoneMatches()
        {
            var candidates = new[]
            {
                new CensusPoiyomiCandidate(
                    ".poiyomi/Poiyomi Toon",
                    null,
                    new string('0', 32),
                    new string('a', 64)),
                new CensusPoiyomiCandidate(
                    BundledMainRow().ShaderName,
                    null,
                    new string('0', 32),
                    new string('b', 64)),
            };

            var selected = CensusVendorProbe.SelectPoiyomi(candidates);

            Assert.That(
                selected.HasValue,
                Is.True,
                "Both candidates resolved, so the selection must report "
                + "one of them.");
            Assert.That(
                selected.Value.ShaderName,
                Is.EqualTo(".poiyomi/Old Versions/9.3/Poiyomi Toon"),
                "When no candidate matches an admitted row the selection "
                + "must report the last resolved candidate, so a 10.x "
                + "install reports the bundled shader.");
        }

        [Test]
        public void SelectionOfNoCandidatesReportsAbsence()
        {
            Assert.That(
                CensusVendorProbe.SelectPoiyomi(
                    new CensusPoiyomiCandidate[0]),
                Is.Null);
        }

        private static PoiyomiAdmittedSource BundledMainRow()
        {
            foreach (var row in PoiyomiSourceAttestation.AdmittedSources())
            {
                if (row.ShaderName == ".poiyomi/Old Versions/9.3/Poiyomi Toon")
                {
                    return row;
                }
            }

            throw new System.InvalidOperationException(
                "The admitted rows have no bundled main row. The probe "
                + "tests cannot build their candidates.");
        }
    }
}
