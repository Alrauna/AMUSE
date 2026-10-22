# Changelog

All notable changes to AMUSE are documented in this file.

## [Unreleased] - 2026-10-21

### Fixed
- Play mode results are keyed by the proved global identity of each avatar root. Same-named roots, twins of one prefab, and roots in scenes with duplicate names keep separate results. A rename or a reorder keeps the record. A root without a proved identity, such as an avatar in an unsaved scene or a copy made during Play, holds no record.

### Changed
- Capture tests for RGB24 and ARGB32 no longer pin a specific backend's sampling capability. RGB24 keeps its source-format Sample support exemption, and its test checks full-chain opaque alpha. ARGB32 follows the exact sampling gate. Without exact support, its test asserts the real capture refusal with no evidence. With exact support, it checks 255, which passes the opaque predicate, and 128 and 254, which do not.

## [0.1.0-pre.10] - 2026-10-21

### Added
- Dry run note for a shader version inside the supported range that does not verify against its measured source. The note names the cause and says the triangles stay unproven.

### Changed
- lilToon 2.4.1 is a measured version. Exact attestation rows now cover 2.3.0 through 2.4.1. Multi container rows cover it too.

## [0.1.0-pre.9] - 2026-10-20

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
