# Design: depth-test policy setting for converted triangles

Date: 2026-09-17. Status: draft specification.

## Privacy note

This document names public product code only. It contains no private avatar,
renderer, material, animation clip, or controller names. It contains no
machine paths, network ports, or instance identifiers.

## Objective

Add one component setting that lets AMUSE move proven-opaque triangles of
materials whose authored depth test is `Less` (value 2) onto the canonical
opaque material, whose depth test is `LEqual` (value 4). The setting sits in
the Advanced Settings foldout of the AMUSE Avatar Optimizer component and is
on by default. With the setting off, today's refusal stays.

The controlling records are
`docs/superpowers/investigations/2026-09-17-liltoon-twopass-transparent-analysis.md`
and `docs/superpowers/investigations/2026-09-17-ztest-treatment-options.md`.
This specification implements Path C from the options note, reshaped by one
owner decision: the consent is the component setting itself, not a
per-build dialog.

## Evidence summary

The two investigations establish:

1. The refusal is identical cross-frontend policy. `LilToonCutoutSourceEligibility`,
   `LilToonTransparentSourceEligibility`, and `PoiyomiOpaqueConversion` all
   refuse any `_ZTest` other than `LEqual`, and all three recipes write
   `LEqual`.
2. `Less` and `LEqual` differ at exactly one pixel class: exact equality
   against stored depth. Equality collisions are a depth buffer population
   property, so a per-pixel equivalence proof is unreachable. The change is a
   stated, consented divergence, not a proven identity.
3. `Less` is a deliberate authored state: lilToon's own converter preserves
   it for TwoPass materials, the maintainer ships `Less` defaults for
   outlines, and community products depend on it.
4. The divergence touches only the forward pass. The lilToon additive pass
   hardcodes `LEqual`, and the canonical recipe writes `LEqual`.

## Decisions

| ID | Decision | Source |
|---|---|---|
| D1 | One new boolean setting, default on, in Advanced Settings | Owner decision, 2026-09-17 |
| D2 | The policy admits exactly `_ZTest == 2` (Less). Every other non-`LEqual` value still refuses | Options note, bounded to the characterized value |
| D3 | All three eligibility sites take the policy. One policy, no second convention | Options note, cross-frontend rule |
| D4 | The recipe still writes `LEqual`. The convertible outcome carries a `DepthTestDivergence` flag when gate 5 admitted through the policy | New |
| D5 | A divergent material whose slot plan is a mixed split refuses with a new named refusal. Wholly opaque slots proceed. Mixed-slot admission is future work | Options note first-slice restriction |
| D6 | The build report names the divergence for every affected slot | Vision reporting rule |
| D7 | The setting is the explicit user choice. Default on is the owner's stated product policy. No per-build dialog exists for this divergence | Owner decision, 2026-09-17 |
| D8 | The FORWARD_BACK pre-pass proof stays a separate, required correctness slice. This feature neither gates nor replaces it | TwoPass investigation Finding B |

## The setting

Runtime field, in `Runtime/AmuseAvatarOptimizer.cs`, beside
`_ignoreOutOfRangeMaterialSlots`:

```csharp
        [SerializeField]
        private bool _allowDepthTestChange = true;
```

Public property:

```csharp
        /// <summary>
        /// When true (default), AMUSE may move proven-opaque triangles of a
        /// material whose depth test is Less onto the canonical opaque
        /// material, whose depth test is LessEqual. The moved triangles then
        /// draw at exactly equal depth too. When false, such materials keep
        /// every triangle and refuse with the named depth-comparison
        /// refusal.
        /// </summary>
        public bool AllowDepthTestChange => _allowDepthTestChange;
```

Inspector entry, inside `DrawAdvancedSettings` in
`Editor/AmuseAvatarOptimizerEditor.cs`, after the existing out-of-range
entry:

```csharp
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("_allowDepthTestChange"),
                new GUIContent(
                    "Allow Depth Test Change on Moved Triangles",
                    "Some materials set a special depth rule: draw a " +
                    "pixel only when it is strictly closer than " +
                    "everything already drawn. When AMUSE moves solid " +
                    "triangles of such a material onto an opaque copy, " +
                    "those triangles use the normal depth rule, which " +
                    "also draws pixels at the same distance. On rare " +
                    "layered parts, surfaces at exactly the same " +
                    "distance can swap their draw order or flicker." +
                    "\n\n" +
                    "By default, AMUSE accepts this stated change and " +
                    "moves the triangles. Turn this setting off to keep " +
                    "materials with a special depth rule on their " +
                    "original material."));
```

An absent component behaves as the default: the build reads
`optimizer == null || optimizer.AllowDepthTestChange`, matching the
existing out-of-range read.

## Detailed design

### 1. The eligibility seam

Each of the three eligibility functions gains one optional parameter, so
every existing call and test stays valid and fails closed:

```csharp
            bool allowDepthTestChange = false
```

Gate 5 changes from a single comparison to an admitted set:

```csharp
            var depthComparison = Read(values, "_ZTest");
            if (depthComparison !=
                    LilToonOpaqueConversionFactors.LEqualDepthComparison &&
                !(allowDepthTestChange &&
                  depthComparison ==
                  LilToonOpaqueConversionFactors.LessDepthComparison))
            {
                return ...Refused(
                    ...UnsupportedDepthComparison);
            }
```

`LilToonOpaqueConversionFactors` gains `LessDepthComparison = 2f` beside the
existing `LEqualDepthComparison`. The Poiyomi site uses its own local
constant the same way.

The convertible factory gains the flag:

```csharp
        internal static LilToonOpaqueConversionEligibility Convertible(
            bool depthTestDivergence = false)
```

The struct gains:

```csharp
        internal bool DepthTestDivergence { get; }
```

`Convertible()` with no argument stays valid and means no divergence. The
Poiyomi eligibility struct receives the identical shape.

### 2. The mixed-split guard

`AlphaSeparationPreparation` computes each slot's separation plan. When the
eligibility result carries `DepthTestDivergence` and the slot's plan is a
mixed split, the slot refuses with a new closed-vocabulary value:

```csharp
        DepthTestDivergenceMixedSplit
```

in `AlphaSeparationSlotRefusal`, with the named-cause guard the other values
use. A wholly opaque slot has no appended submesh, so every moved triangle
lands on one material and the guard does not fire.

### 3. The report

The slot's success report gains one fixed sentence when the flag is set:

    Some moved triangles now use the normal depth rule because their source
    material set a special one.

### 4. Preserved invariants

- Animated `_ZTest` state refuses at slot resolution today. This feature
  does not change that. The animation closure already captures the property.
- The recipe writes `LEqual` unchanged, so the generated material is
  self-consistent for downstream tools.
- The FORWARD_BACK pre-pass proof is independent required work. This
  setting neither delays it nor replaces it.

## Out of scope

- Mixed-split admission for divergent sources (D5 refusal is the boundary).
- Any comparison value other than `Less`.
- The Poiyomi second-pass `_ZTest2` property family.
- A per-build consent dialog.

## Validation shape

RED first for every behavior change: a test that fails against a named
plausible wrong implementation, then the minimal change. The existing
policy-off refusal tests stay and stay green. Full suite before completion.
