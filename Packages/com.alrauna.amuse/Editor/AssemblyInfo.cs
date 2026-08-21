using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Alrauna.Amuse.Tests.Editor")]

// The census collector reads AMUSE's internal analysis results directly rather
// than through reflection: it is first-party, lives in this repository, is
// versioned and compiled together, and gains nothing from a run-time surface
// probe that only re-creates what the compiler already checks. It changes no
// public API and adds no production code. See
// docs/superpowers/specs/2026-08-20-avatar-census-harness-preparation-design.md §4.2.
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Editor")]

// The census tests construct MaterialSemantics values to drive AMUSE's existing
// BaseMaterialSemanticsProvider seam, because the public development project
// installs no vendor shader and therefore cannot reach ProvenOpaque any other
// way. The alternative was a permanent calibration class inside the collector's
// production assembly - a hidden extension point whose only caller is a test.
// A test assembly ships in no build, so this is the narrower of the two. See
// docs/superpowers/specs/2026-08-20-census-collector-design.md §3.1.1.
[assembly: InternalsVisibleTo("Alrauna.Amuse.Research.Tests.Editor")]
