# Separation Refusal Renderer Name Design

Date: 2026-09-28.
Branch: `fix/separation-refusal-renderer-name`.
Base: `main` at `58a17fb` (the merge of `feat/alpha-mask-value-support`).
Investigation: `docs/superpowers/investigations/2026-09-28-alpha-mask-console-followup-investigation.md`.
Status on 2026-09-28: design awaiting implementation. No production code has changed on this branch.

Privacy note: the triggering evidence comes from the play-mode console of the Census Lab project and one private avatar slot. This document names no avatar, scene, renderer, material, texture, or asset path, and no instance names, hashes, ports, or machine paths. The refused slot is described by role, following the sanitized investigation record.

## Problem

A slot-separation refusal report can render its renderer as the literal text `<missing>`. The Census Lab console showed exactly this after the alpha-mask sync: the coverage-gate report for the transparent-family lower-body clothing slot read "AMUSE left material slot 0 of renderer '<missing>' unchanged." The report's reason, slot index, and outcome were correct. Only the renderer name was gone.

The mechanism, from the investigation record:

1. Three preparation-pass emission sites omit the `rendererName` argument: the coverage gate at `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationPreparation.cs:270-274`, the marker-clip refusal at `:330-334`, and the conversion refusal at `:438-441`. The apply pass omits it at `Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs:164-168`, where the live renderer may itself be null. The parameter's declared default is null (`Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:145-155`).
2. NDMF renders a null substitution as the literal `<missing>` (`Packages/nadena.dev.ndmf/Editor/ErrorReporting/InlineError.cs:30-33`), in both the title and the description templates (`Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:456-464`).
3. The placeholder was always wrong-looking, but it became user-visible now because the alpha-mask branch let previously refused slots classify, plan, and reach preparation. On main the flagged slot refused at slot analysis, whose emission site passes the name (`AmusePlatformFinishPlugin.cs:681-687`).

The placeholder matters after play mode ends. NDMF report references point at the build-copy renderer, which is destroyed when play mode ends, so the console's object link is dead. The report text is then the only place that still names the renderer. A report that says `renderer '<missing>'` fails its one identification job exactly when the reader needs it.

## Goal

Every slot-separation refusal report names the renderer it refused whenever that renderer's name is available at emission time, through every current and future emission site, with no call-site changes.

## Non-goals

- No change to report strings, keys, refusal enums, or emission sites.
- No change to the slot-analysis report family: its only call site passes the name today (`AmusePlatformFinishPlugin.cs:681-687`), so it has no reachable gap. Widening the fallback there is speculative armor.
- No change to `SlotSeparationDivergence` and `SlotSeparationPremultiplyNormalization`: their only call sites already pass a null-guarded name (`AlphaSeparationPreparation.cs:474-477`, `:489-492`). Same reasoning.
- No path-level test for the preparation coverage gate: the wiring at the emission sites is unchanged by this fix, and the helper is the single choke point every emission site flows through. The unit seam is the narrowest layer that can disprove the behavior.

## Decision

The fix lives inside the one helper every affected site already flows through, not at the call sites.

In `AmuseReports.SlotSeparationRefusal`, resolve the name once: when the `rendererName` argument is null, use the `renderer` argument's game object name; when the renderer is also null, keep null, which NDMF renders as the honest `<missing>` placeholder.

```csharp
rendererName = rendererName
    ?? (renderer != null ? renderer.gameObject.name : null);
```

Alternatives considered:

- Passing a guarded name at each of the four omission sites, mirroring the existing pattern at `AlphaSeparationPreparation.cs:567-573`. Rejected: four edits with one repeated ternary, and the class of defect survives the next omitted argument. The helper owns the report's readability contract; resolving the name there makes the contract true by construction.
- Falling back to the prepared renderer path when the renderer is null. Rejected for now: no consumer needs it, the report template names renderers by game object name everywhere else, and a path in a name slot would read inconsistently. If a future site needs path identity for a destroyed renderer, that is a separate design.

Properties of the chosen shape:

- An explicit name keeps winning, because `??` reads the argument first. The apply pass's `RuntimeMaterialValueNotMapped` and validation sites (`AlphaSeparationApply.cs:202-207`, `:275-280`) and both transient-unlock sites (`TransientUnlockSwapIn.cs:553-555`, `TransientUnlockWindowClose.cs:139-147`) pass names that stay authoritative.
- The apply pass's renderer-changed site (`:164-168`) needs no edit: whenever its renderer is alive the helper now finds the name; when the renderer is gone the placeholder is the truthful rendering.
- A destroyed renderer is safe: Unity's null semantics make `renderer != null` false for destroyed objects, so the fallback yields null and never throws on `gameObject`.
- An empty game object name renders as an empty substitution, never as `<missing>`, because `""` is not null.

## Testing

Product tests run in the dev editor instance through the Test Runner, EditMode mode. Tests never run in the Census Lab. A filtered run that reports zero tests is a failure, and observed counts are recorded.

The tests live in the existing `AmuseReportsSlotTests` class, which already owns this report family and captures through NDMF's `ErrorReport.CaptureErrors`. Three tests:

1. RED: a refusal without an explicit name, over the fixture's named renderer, must carry that name in the formatted message and must not carry the `<missing>` placeholder. This fails on current code with the exact text observed in the Census Lab console.
2. RED: the same refusal over a renderer whose name is empty must render without the `<missing>` placeholder. This fails on current code for the same null argument, and pins the empty-string boundary of the fallback.
3. Guard: a refusal with a null renderer keeps the `<missing>` placeholder and does not throw. This passes before and after; it stops a later change from turning the fallback into a throw or from naming a renderer that does not exist.

The explicit-name precedence is already pinned by the neighboring `SlotSeparationRefusalReportsRendererAndMaterial` test, which must keep passing unchanged. The rest of the class pins slot index, cause, and string-table completeness, and must keep passing: the fix adds no argument and changes no string.

After the focused run, the full `Alrauna.Amuse.Tests.Editor` assembly runs, and the `Alrauna.Amuse.Research.Tests.Editor` assembly runs because it consumes the same internals.

## Expected effect

Every current and future slot-separation refusal emitted while the renderer is alive carries the renderer's name in title and description. The four omission sites heal without edits. Reports emitted with a genuinely absent renderer keep the placeholder, which stays truthful.

## Risks

The behavior change is report text only, and only where the text was already wrong for the reader. No refusal decision, count, or severity moves. The one semantic subtlety is precedence, which the existing named-renderer test pins. The falsifier for the empty name guards the `??`-on-empty-string boundary, which is the only plausible wrong implementation of the fallback (treating empty as missing).
