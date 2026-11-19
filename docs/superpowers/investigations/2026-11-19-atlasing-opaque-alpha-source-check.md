# Source check: does the alpha separation opaque output read main texture alpha

Privacy note: This record contains public product research only. It names vendor shader properties and passes. It quotes no vendor source and contains no private asset observations or machine identifiers.

Date: 2026-11-19.
Status on 2026-11-19: Read-only source review of the installed vendor packages in the dev project. No Unity run, build, or test happened. The user approved this check on 2026-11-19. It closes Gate 0 item 2 of `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md` with no blocking finding.

Correction on 2026-11-19, later: An earlier draft of this record reported a Poiyomi Two Pass finding and a new atlas admission rule. That draft was wrong. It assumed that alpha separation converts Two Pass materials. Product code does not convert them. Section "Poiyomi Toon 9.3 Two Pass" below records the correct state. The admission rule was removed from the design.

## Question

The minimal design runs atlasing after alpha separation. An atlas page keeps the alpha of its source, but compression and the mip tail can change alpha values. Can such a change break the opacity proof of an alpha separation opaque output? Two separate questions follow:

1. Opacity. Does any pass of the opaque output use main texture alpha for clip, coverage, or blend?
2. Color. Does any pass of the opaque output use main texture alpha to compute color?

## Method

The check read the shaders that the opaque recipes target:

- The Poiyomi Toon 9.3 shader and the Poiyomi Toon 9.3 Two Pass shader that the installed Poiyomi package bundles.
- The lilToon opaque target shader, its opaque pass shader, and the shared forward, shadow caster, and keyword include files of the installed lilToon package.

The check compared each alpha read against the property values that the opaque recipes in `PoiyomiOpaqueConversion.cs` and `LilToonOpaqueTarget.cs` write.

## Results

### Poiyomi Toon 9.3

- Opacity: safe. Each fragment pass sets the output alpha to one when `_AlphaForceOpaque` is one. The recipe writes one. This covers the Base, Add, Outline, and ShadowCaster passes. The clip, alpha-to-coverage, and dithering steps run after that write. The recipe writes `_Cutoff` 0 and `_AlphaToCoverage` 0. The EarlyZ pass does not sample the main texture.
- Color: main texture alpha can reach color before the force step. The premultiply path needs `_AlphaPremultiply`, and the recipe writes 0. The reflection path with `_ReflectionApplyTransparency` scales the reflection weight by alpha. That property defaults to on. The video path copies alpha into a value that never reaches color.

### Poiyomi Toon 9.3 Two Pass

- Product state on 2026-11-19: alpha separation never makes an opaque output for this shader. `AlphaSeparationMaterialConverter.ConvertAdmittedMaterial` has no case for the Two Pass family, so the slot refuses with `OpaqueConversionUnsupportedFamily`. The test `AlphaSeparationPreparationTests.TwoPass_ConversionRefusesByName` pins this. It refuses even when both force-opaque properties are one. Its comment states the reason: the opaque recipe comes from the preset data of the plain shader, and nobody derived it for Two Pass.
- The shader source confirms that this refusal is needed. The plain recipe writes only first pass properties. The second pass sets alpha to one only when `_AlphaForceOpaque2` is one or `_ModeTwoPass` is opaque. Otherwise it uses main texture alpha for its own clip, alpha power, and blend.
- Prerequisite for later versions: a Two Pass opaque conversion must write the second pass to opaque too. Then this check applies to it unchanged.

### lilToon

- Opacity: safe. The opaque target compiles with the opaque render mode. The forward passes, including the outline passes, set alpha to one before output. The shadow caster passes do not sample the main texture in the opaque render mode. The multi shader selects the opaque render mode for the mode-0 keyword set that the recipe writes.
- Color: safe. Every path that reads alpha into color is compiled only in the transparent render mode. These are premultiply, emission blend, rim, matcap, reflection, and glitter.

## Consequences for the minimal design

The page output check includes the alpha channel. That bounds the Poiyomi reflection color change at the protected levels. Beyond the protected levels, the mip tail already changes color for every use by design. No new admission rule is needed.
