# Release 0.1.0-pre.9 and separator button order investigation

Status as of 2026-10-20. Branch at investigation time: `feature/pre9-version-and-separator-order`, cut from `main` at `382bde7`.

Privacy note: this investigation read only product code in this repository. It read no private avatar data and no Census data. Machines appear by role only.

This document records facts about the code at the base commit. It decides nothing by itself. Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[INFERENCE]` A deduction from listed facts. The deduction is named.

Product and test paths are relative to `Packages/com.alrauna.amuse/` unless a path carries another root.

## 1. Purpose

The owner asked for two changes on 2026-10-20.

1. Bump the package release version to `0.1.0-pre.9`.
2. Flip the order of the Cutout Separator and Transparent Separator toggles that draw below the Alpha Separator switch.

## 2. Method

Two parallel read-only investigations ran on 2026-10-20.

| Investigation | Scope | Mode |
|---|---|---|
| Version surface | Every declaration, pin, prose mention, and workflow read of the package version. Bump commit precedent | Read-only repo analysis |
| UI order surface | Draw site of the two separator sub-switch toggles, test hooks, tests, presets, docs that name the order | Read-only repo analysis |

The controller then re-read every load-bearing citation in this record before the design round.

## 3. Version surface facts

### 3.1 Declaration and changelog

- `[SOURCE]` `package.json:4` holds the one semver declaration of the released product package: `"version": "0.1.0-pre.8"`.
- `[SOURCE]` The research package manifest declares its own independent version `0.0.1` (`../com.alrauna.amuse.research/package.json:4`). No past bump commit moved it. Its description marks it as never released. The design round must not bump it.
- `[SOURCE]` The package changelog carries an `## [Unreleased]` block (`CHANGELOG.md:5-12`). It holds the separator sub-switch feature and the lilToon range approval.
- `[SOURCE]` The last release header is `## [0.1.0-pre.8] - 2026-10-19` (`CHANGELOG.md:14`).

### 3.2 Pins and workflow reads

- `[SOURCE]` No file pins the version. `Packages/packages-lock.json` records `com.alrauna.amuse` as a `file:` reference, not a semver pin. `Packages/vpm-manifest.json` has no row for this package. No test or editor script asserts the package version.
- `[SOURCE]` The release workflow reads the version from `package.json` at run time (`.github/workflows/release.yml:46-59`, property path `version`). The release tag and artifact names follow that value. A version bump needs no change there.
- `[SOURCE]` The listing workflow takes no version input in this repository (`.github/workflows/build-listing.yml:59-63`). It delegates to an external listing action. A version bump needs no change there either.

### 3.3 Historical prose

- `[SOURCE]` Records under `docs/superpowers/` and `docs/architecture/vision.md` cite past versions such as `0.1.0-pre.1` through `0.1.0-pre.7`. A no-op workflow file carries prose about the `0.1.0` release-candidate series (`.github/workflows/pr.yml:1-3,22`). Dated records speak for their date. They must not change.
- `[SOURCE]` Scratch records under `.superpowers/` also carry version mentions. Git ignores that directory. It sits outside this inventory of tracked content.

### 3.4 Bump precedent

- `[SOURCE]` The last bump commit is `b0fe7d9b`, subject `chore(pkg): update version to 0.1.0-pre.8`. It changed exactly two files, one line each: `package.json` and the package changelog header line.
- `[SOURCE]` That commit renamed an already dated header. It did not promote an `Unreleased` block. No `Unreleased` block existed at that commit.
- `[SOURCE]` Earlier bumps used subjects of the form `chore: bump package version to <version>` and `chore: bump version to <version>`. The newest subject form is `chore(pkg): update version to <version>`.

## 4. UI order surface facts

### 4.1 Draw site

- `[SOURCE]` `Editor/AmuseAvatarOptimizerEditor.cs` `OnInspectorGUI()` draws the master row over `_alphaSeparatorEnabled` with label `Alpha Separator` (`:169-172`).
- `[SOURCE]` While the master switch is on, one `EditorGUI.IndentLevelScope()` block draws two toggles in this order:
  1. `_cutoutSeparatorEnabled`, label `Cutout Separator`, at `:177-182`. Its test hook `OnCutoutSeparatorToggleDrawnForTest` fires right after, at `:183-185`.
  2. `_transparentSeparatorEnabled`, label `Transparent Separator`, at `:186-191`. Its test hook `OnTransparentSeparatorToggleDrawnForTest` fires right after, at `:192-194`.
- `[SOURCE]` The current visual order is therefore Cutout Separator above, Transparent Separator below, both one indent level under the master row.
- `[SOURCE]` The order is hard-coded GUI call order. No array, enum, or collection drives it.
- `[INFERENCE]` A flip swaps the two statement pairs. Each hook call sits inside its own toggle's pair, so a block swap moves each hook with its toggle.

### 4.2 Serialization and presets are unaffected

- `[SOURCE]` Unity matches serialized fields by name. `EditorGUILayout.PropertyField(serializedObject.FindProperty("_cutoutSeparatorEnabled"))` and its transparent twin bind by field name (`Editor/AmuseAvatarOptimizerEditor.cs:177-191`).
- `[SOURCE]` Presets parse JSON keys by name (`Editor/Presets/PresetParser.cs:23-24`, `:105-113`) and apply by `FindProperty` name (`Editor/Presets/PresetApplier.cs:19-24`). Preset schema 5 keys are `alphaSeparator`, `cutoutSeparator`, `transparentSeparator`.
- `[INFERENCE]` The flip touches draw order only. No serialized field, preset file, or schema version changes.

### 4.3 A second cutout and transparent pair exists

- `[SOURCE]` The Settings foldout draws an `Alpha Separator Settings` view with a Transparent submenu and a Cutout submenu (`Editor/AmuseAvatarOptimizerEditor.cs:787-952`). The Transparent submenu method draws first (`:850-851`, method at `:889-919`). The Cutout submenu method follows (`:921-952`).
- `[INFERENCE]` The owner's words name the pair below the Alpha Separator switch. That is the sub-switch pair in section 4.1. The submenu pair is not the target of this change.

### 4.4 Tests

- `[SOURCE]` Two Unity tests exercise the pair (`Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:237-309`). `SubSwitchHooksFireWithStoredValuesWhenMasterSwitchIsOn` records each hook into its own list and asserts distinct `(value, indent)` tuples of `(true, 1)` and `(false, 1)`. `SubSwitchHooksDoNotFireWhenMasterSwitchIsOff` asserts both lists stay empty.
- `[INFERENCE]` No existing assertion pins the relative draw order of the two toggles. The flip keeps every existing assertion true. The indent stays 1 for both rows.
- `[SOURCE]` The host window paints several IMGUI events per repaint, so the hooks fire several times per repaint (`Tests/Editor/AmuseAvatarOptimizerEditorTests.cs:224-233`).

### 4.5 Copy that names the two controls

- `[SOURCE]` The master row tooltip names both controls in cutout-first word order. It states no positional order between the two controls. It does state their placement below the master row: the `Cutout Separator and Transparent Separator switches draw below it, one indent level down` (`Editor/AmuseAvatarOptimizerEditor.cs:519-525`).
- `[SOURCE]` The runtime field doc comments name the two sub-switches without stating an order between them (`Runtime/AmuseAvatarOptimizer.cs:23-48`).
- `[SOURCE]` The unreleased changelog bullet names both in cutout-first word order. It states no positional order between the two controls. It does state their placement below the Alpha Separator switch (`CHANGELOG.md:8`).
- `[SOURCE]` The report hint names both with an `or`, order-neutral (`Editor/Build/AmuseReportStrings.cs:590`).
- `[SOURCE]` The root `README.md` and its package mirror name the Alpha Separator switch and the schema 5 keys. Neither states an order between the two sub-switch toggles.
- `[INFERENCE]` No user-facing sentence states a positional order between the two toggles. Placement sentences describe position relative to the master switch. The flip preserves that placement. It breaks no documented promise. The design round may still choose to update prose that lists the controls in the old word order.

## 5. Summary of change surface

| Change | Files | Kind |
|---|---|---|
| Version bump to `0.1.0-pre.9` | `package.json`, `CHANGELOG.md` header | Data only |
| Sub-switch order flip | `Editor/AmuseAvatarOptimizerEditor.cs:177-194` | Draw order only |

The bump commit historically lands alone. `[INFERENCE]` The changelog `Unreleased` block becomes the `0.1.0-pre.9` section at bump time. This promotion is a new choice for this bump. The pre.8 commit renamed an already dated header instead (`[SOURCE]`, section 3.4).

## 6. Open questions for the design round

1. Does the design round update prose that lists the two controls in cutout-first word order to match the new draw order?
2. The repository RED/GREEN policy requires a failing test before a behavior change. A draw order is user-visible behavior. Which test pins the new order, and what named plausible wrong implementation does it fail?
3. Does the changelog gain a new `Changed` bullet for the flip? Or does the flip amend the existing unreleased bullets for the not-yet-released sub-switch feature?

## Record changes

- 2026-10-20: three parallel adversarial reviews ran against the first draft. Their axes were citation accuracy, completeness, and document rule compliance. Fixes applied: the research manifest fact and the listing workflow correction in section 3, the untracked scratch scope in section 3.3, the header-rename precedent fact in section 3.4, corrected citations in sections 4.2 and 4.3, and separated placement and order claims in sections 4.5 and 5.
