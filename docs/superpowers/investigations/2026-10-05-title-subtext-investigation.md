# Title subtext with the full product name: current-state characterization

Date: 2026-10-05. Branch: `feature/optimizer-ui-rework`, base `main` at
`f319186`.

Labels: `[SOURCE]` is a fact read in this repository or in public
optimizer source. `[INFERENCE]` is a deduction. `[RECOMMENDATION]` is a
proposed next step.

## 1. Purpose

The product owner asked for a subtext line under the "AMUSE" title in
the component inspector. The line reads "Alrauna's Material
Understanding and Simplification Engine". The owner named
d4rkAvatarOptimizer's version line as the visual reference. This note
records the state the design must touch. It changes nothing. A second
note from the same date covers the quality preset selector.

## 2. The header today

`[SOURCE]`
`Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs` is the
only file that draws this component's inspector. The file has grown from
the 319 lines the inspector rework note recorded earlier on 2026-10-05
to 413 lines after the advanced settings slice. `DrawHeader` runs first
in `OnInspectorGUI`.

`[SOURCE]` `DrawHeader` draws one control rect 26 pixels tall. A bold
label style with alignment `TextAnchor.MiddleCenter` and font size 18
draws the text "AMUSE" across the whole rect. The package version comes
from `UnityEditor.PackageManager.PackageInfo.FindForAssembly` on the
editor assembly. A right-aligned `EditorStyles.miniLabel` draws
"v" plus the version in the same rect when the version is not empty.

`[SOURCE]` Below the rect, `DrawHeader` draws a "Report a bug" button
that opens the public issue tracker. It then adds
`EditorGUILayout.Space(4)`.

`[SOURCE]` No other code draws a product name. `package.json` holds
`"displayName": "AMUSE"` at version `0.1.0-pre.5`. The repository
overview names the product "AMUSE (Alrauna's Material Understanding &
Simplification Engine)".

## 3. The d4rkAvatarOptimizer reference

`[SOURCE]` The installed VPM dependency
`Packages/d4rkpl4y3r.d4rkavataroptimizer` holds version 4.5.4. The
public repository at `https://github.com/d4rkc0d3r/d4rkAvatarOptimizer`
holds version 4.6.1 on its main branch. Both copies agree in the parts
below. Citations use the installed paths and line numbers.

`[SOURCE]` `Editor/d4rkAvatarOptimizerEditor.cs` draws two lines at
lines 53 to 58:

- A title label with rich text `<size=20>` and alignment
  `TextAnchor.LowerCenter`. The text reads "d4rkpl4y3r's Avatar
  Optimizer" and shortens to "d4rk Avatar Optimizer" when the inspector
  is narrower than about 370 pixels.
- A second label `EditorGUILayout.LabelField($"v{packageInfo.version}",
  EditorStyles.centeredGreyMiniLabel)`. This line is the subtext under
  the title. It is centered, grey, and small.

`[SOURCE]` d4rk reads its version with
`PackageInfo.FindForAssetPath` on its own script asset path. AMUSE reads
it with `FindForAssembly`. Both read the installed package metadata, so
neither needs an inspector edit per release.

`[INFERENCE]` In d4rk the version is the subtext. In AMUSE the version
already sits at the right of the title row. The request asks for the
motto as subtext, not the version. The design can keep the right-aligned
version and add one centered subtext line. Nothing else in the header
needs to move.

## 4. Shape of the change

`[RECOMMENDATION]` Add one label between the title rect and the "Report
a bug" button. Derive the style from
`EditorStyles.centeredGreyMiniLabel` and turn `wordWrap` on, so a narrow
inspector wraps the motto instead of clipping it. Hold the text in one
internal constant so a future wording change is one edit. The package
metadata stays unchanged, because the subtext is a product name, not a
package field.

`[INFERENCE]` A smoke check on the dev editor instance is the right
verification. No EditMode test draws the inspector. The only editor test
covers `NormalizePolygonClamp`. An assertion that pins the constant
would test source text, which the repository conventions reject. The
2026-09-10 settings plan used the same smoke-check pattern for foldout
layout.

## 5. Constraints the design must keep

`[SOURCE]` The session rules require plain technical English in labels
and tooltips. The motto is a proper product name, so it passes as a
proper noun. Any new tooltip text stays short and active.

`[SOURCE]` `DrawHeader` is private and static. The design touches one
method in one file. The version label, the button, and the placement
help box keep their behavior.

## 6. Open question for the design

The owner wrote the motto with "and". The repository overview writes
"Alrauna's Material Understanding & Simplification Engine" with an
ampersand. The design picks one wording for the subtext.

Decision, 2026-10-05: the owner picked the "and" wording. The design
lives in
`docs/superpowers/specs/2026-10-05-title-subtext-design.md`.

## 7. Verification record

`[SOURCE]` On 2026-10-05 this note was checked against the working
tree, the installed d4rk package, and the public d4rk repository. The
d4rk facts were read from the installed copy at version 4.5.4 and from
the public main branch at version 4.6.1. Both agree on the title and
subtext drawing. No claim needed a correction.
