# Changelog

All notable changes to AMUSE are documented in this file.

## [Unreleased]

### Added
- Cutout Separator and Transparent Separator sub-switches below the Alpha Separator switch. Presets store both switches.

### Changed
- Preset schema version moves from 4 to 5. The new feature keys are `cutoutSeparator` and `transparentSeparator`. A schema 4 file refuses with the schema version message.
- lilToon 2.3.0 through any version below 2.5 are approved without a consent prompt. The build asks once per build for versions outside that range.

## [0.1.0-pre.8] - 2026-10-19

### Added
- Exact rational polygon alpha classification proving opacity across UV coordinates.
- Non-destructive mesh splitting separating proven-opaque triangles to canonical opaque materials.
- Shader frontends for lilToon (versions 2.3.0 to 2.4.0, with version-aware Multi container rows) and Poiyomi Toon Shader (version 9.3.64, including Two Pass).
- EditMode DryRun preview inspecting avatar materials and expected polygon splits.
- PlayMode build record store caching transformation results for inspection.
- Optimization presets: Safe, Normal, and Aggressive.
- Consent gate prompting before processing unverified shader versions.

### Requirements
- Unity 2022.3.22f1 or compatible 2022.3 LTS release.
- NDMF (Non-Destructive Modular Framework) version 1.14.4 or higher.
- VRChat Avatar 3.0 SDK version 3.10.4 or higher.

### Known Limitations
- Finding 4: Deep material property revalidation is deferred during the Apply pass.
- Finding 9: Large texture dry-runs execute synchronous GPU readbacks that cause brief editor hitches.
