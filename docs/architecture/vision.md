# AMUSE Architecture Vision

## Purpose

AMUSE — Alrauna's Material Understanding & Simplification Engine — is intended to optimize material, texture, geometry, and rendering usage in Unity avatars without changing observable behavior. It is hosted by Unity and NDMF, but its core responsibility is broader than any single alpha-material optimization.

This document describes the long-term direction. It does not claim that the full pipeline exists today.

## Safety and optimization policy

AMUSE is proof-first:

- optimize when all relevant supported states prove a transformation preserves behavior;
- preserve the original behavior when evidence shows the transformation is unsafe; and
- return an explicit unknown or unsupported result when the available model is incomplete.

More uncertainty must never make optimization more aggressive. False negatives reduce optimization coverage; false positives are correctness defects.

## Architectural direction

The intended high-level flow is:

1. Unity/NDMF host integration gathers immutable, normalized inputs from the built avatar.
2. Semantic analyzers describe geometry, materials, textures, shaders, animation, and other rendering state without mutating source assets.
3. Analysis results are combined across every reachable state.
4. A deterministic planner proposes transformations and records the proof, refusal, or unsupported reason for each decision.
5. A host-side executor applies only the approved plan to generated build artifacts.
6. Validation and diagnostics make both applied and skipped work inspectable.

These are responsibility boundaries, not a requirement to create one class, assembly, or abstraction per step.

## Semantic understanding

AMUSE should model the behavior that determines whether a rendering change is equivalent: mesh topology and UVs, material and shader properties, texture sampling semantics, animation bindings, material swaps, and other state that can alter the result. Shader-specific adapters may eventually translate implementation details into normalized material semantics, while recognized external modifiers may contribute additional semantics to the effective material. Each analyzer should state the domain it supports. Unknown modifiers, unmodeled shader behavior, or incomplete reachable-state information must fail closed.

## Analysis and combined planning

Individual facts are useful only when combined over the full relevant state space, including reachable animation, material-swap, renderer, and property relationships when those analyzers exist. The planning layer should consume normalized analysis results, produce the same plan for the same input, and remain separable from mutation. Plans should identify what can change, what must remain unchanged, and why.

Planning should also consider whether a proven transformation is worthwhile, but profitability can only suppress safe work; it cannot turn an unproven transformation into an allowed one.

## Unity/NDMF host integration

Unity and NDMF provide asset access, build lifecycle integration, generated-object ownership, and final execution. Host-facing code should translate mutable Unity objects into stable analysis inputs and apply plans nondestructively to build outputs. Product correctness must not depend on a live editor automation session or the private avatar testbed.

## Alpha subsystem

The existing exact UV geometry, triangle alpha classification, and mesh-separation planning form an early semantic subsystem. They demonstrate the proof-first model for supported texture wrap, filtering, and alpha data. They should remain reusable inputs to combined material planning rather than becoming a special-case architecture for the whole engine.

## Portability

Pure analysis and planning should avoid unnecessary dependencies on Unity object identity, editor state, NDMF orchestration, or private fixtures. Normalized immutable inputs make deterministic tests possible and leave room for reuse in tooling or hosts beyond the initial Unity integration. Portability is a design pressure, not a promise of a separate runtime or platform today.

## Future policy levels

Future releases may expose explicit optimization policies, such as conservative defaults and separately selected broader transformations. Every policy must preserve the same safety invariant, describe any additional assumptions, and remain deterministic. A policy setting must never silently reinterpret unknown evidence as proof.

## Current implementation versus future direction

Implemented now:

- exact triangle UV and alpha reasoning for a defined input domain;
- conservative classification outcomes;
- deterministic mesh-separation planning; and
- synthetic fixtures and EditMode tests for that behavior.

Future work includes shader-specific and modifier-aware material semantics; reachable animation and material-state analysis; texture-use, UV-island, and atlas planning; material normalization and combining; alpha and overdraw optimization; combined optimization planning; nondestructive transformation execution; NDMF pass integration; compatibility handling; diagnostics; and profitability policy. These directions describe possible analysis responsibilities, not implemented features or a commitment to scaffold them now.

## Non-goals for current development

Current development does not attempt to understand arbitrary shaders, optimize on incomplete evidence, modify avatar source assets, make the private testbed a product dependency, or scaffold the entire future architecture before a narrow vertical increment requires it.
