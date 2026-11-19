# Alpha controls and UV islands

Privacy note: This record contains public source observations only. It contains no private asset data or machine identifiers.

Date: 2026-11-16.
Status on 2026-11-16: Source investigation complete. The user chose to retain the existing alpha features. This record does not authorize implementation.

## 1. Question and evidence

Which alpha-separator controls still earn their cost when AMUSE adds island-aware atlasing?

A UV island is a connected patch of texture coordinates. An island describes spatial relationships, not opacity or rendering cost. This investigation separates existing behavior from proposed cuts.

The parent inspected the component, inspector, presets, classifier, mip selection, slot decisions, mesh planning, and geometry capture. Two read-only investigations examined alpha-policy internals and the remaining compatibility controls. No tests, builds, benchmarks, or Unity operations ran. Referenced tests establish intended contracts, not passing results.

Related records:

- `docs/architecture/vision.md` defines the long-term product direction.
- `docs/superpowers/specs/2026-11-16-merge-ready-atlasing-design.md` records the unapproved atlas design.
- `docs/superpowers/investigations/2026-11-16-atlasing-foundation-investigation.md` records the earlier integration findings.

## 2. Main finding

Recommendation: Keep triangle-level alpha analysis. Add island analysis for atlas placement and sampling dependencies. Do not replace triangle classification with one verdict per island.

One connected island can contain a solid interior and a transparent edge. Keeping the whole island transparent loses useful separation. Making the whole island opaque loses the edge. Islands can also share image content without sharing mesh vertices. Mesh connectivity alone does not define a safe packing group.

Source evidence: `Editor/Analysis/MeshSeparationPlanner.cs:184-205` retains every non-opaque outcome on the original side. `Editor/Host/UnityRendererGeometryExtractor.cs:102-158` captures coordinates and indices. These paths do not establish island adjacency or atlas sampling dependencies.

All `Editor/`, `Runtime/`, `Presets/`, and `Tests/` paths below are relative to `Packages/com.alrauna.amuse/`.

## 3. Proposed cuts

| Existing control | Recommendation | Reason and cost |
|---|---|---|
| Minimum Opaque Coverage to Preserve, transparent and cutout | Remove both controls and the whole-slot fold behavior | It makes unproven triangles opaque to avoid a split. Removing it can retain more transparency and add splits. |
| Alpha Upper Clamp to Convert, per texture | Remove approximate alpha promotion | A lowered threshold makes partial transparency fully opaque. Islands cannot justify that change. |
| Alpha Upper Clamp and Minimum Opaque Coverage, per polygon | Remove the paired stray-texel policy | It adds density counting and special evidence values to several sampling paths. It is a separate lossy feature. |
| Accept Mobile Anisotropy Differences | Remove the unsupported-platform choice from the product | The product targets PC. A mobile acceptance choice does not enable mobile texture capture. |
| Advanced Settings reveal | Remove serialized presentation state | Opening a panel does not configure optimization. Reset and Undo behavior needs an explicit change. |

These are proposals, not approved removals. Removing a control means removing its behavior and plumbing, not hiding the same branch behind a preset.

### Whole-slot folding

Observation on 2026-11-16: The preserve threshold compares unproven triangle counts against the total for each submesh. A qualifying slot takes `Fold` before the split-benefit gate. `Folded` then moves both ordinal lists into the opaque output.

Evidence: `Editor/Analysis/SlotTransformDecision.cs:230-258,333-350`. `Editor/Analysis/MeshSeparationPlanner.cs:220-249`. `Tests/Editor/Analysis/SlotTransformDecisionTests.cs:61-93` explicitly distinguishes folding from a zero preserve threshold.

Normal uses five percent. Aggressive uses ten percent. This is shipped behavior, not an accidental violation of its configured contract. The control label does not clearly describe the permitted transparency loss.

Evidence: `Presets/normal.json:13-21`, `Presets/aggressive.json:13-21`, and `Runtime/AmuseAvatarOptimizer.cs:87-97`.

Recommendation: Remove the behavior rather than rename it or calculate the same allowance per island. A small triangle share does not bound lost screen area. One large transparent triangle can matter more than many small solid triangles.

### Alpha clamps and polygon strays

Observation on 2026-11-16: Alpha bounds change captured evidence. The polygon policy permits selected texels to lose their veto when their consulted share meets a configured limit. The classifier carries a special erased value and fallback counting paths.

Evidence: `Editor/Analysis/AlphaPolicyBounds.cs:3-29,50-63`. `Editor/Analysis/TriangleAlphaModels.cs:207-213`. `Editor/Analysis/TriangleAlphaClassifier.cs:38-60,453-494`.

Aggressive enables all three controls. Normal and Safe keep them inert. Removing them changes Aggressive and custom configurations even when the default result stays the same.

Evidence: `Presets/aggressive.json:15-17`, `Presets/normal.json:15-17`, and `Presets/safe.json:15-17`.

Recommendation: Remove this lossy feature instead of moving it to island coverage. Keep the shader's authored cutout threshold. Removing approximate alpha promotion does not mean testing cutout materials against alpha one.

An island-wide allowance introduces another dependence on region size and topology. It does not establish that an ignored texel has no visible effect. Retain exact geometry, filtering, wrapping, and mapped-alpha logic that still serves the remaining contract.

## 4. Controls that still have distinct jobs

| Existing control | Recommendation | Why islands do not replace it |
|---|---|---|
| Disable AMUSE and Alpha Separator | Keep | Global disable and feature disable need distinct meanings once atlasing exists. |
| Transparent Separator and Cutout Separator | Keep as advanced exclusions | They permit one render family to remain unchanged without disabling the other. |
| Minimum Opaque Coverage to Convert, both families | Retain provisionally and describe as a triangle-share heuristic | It suppresses mixed splits. Islands do not predict draw cost or screen area. |
| Smallest Tested Mipmap and Smallest Tested Texture, both families | Rework sampling policy before atlas integration | They limit different aspects of the consulted mip chain. Island extraction does not remove distant filtering. |
| Desktop anisotropy acceptance | Keep the independent acceptance | Removing a footprint from the proof permits a specific rendering difference. |
| Allow Depth Test Change | Keep the independent acceptance | Equal-depth behavior is unrelated to UV connectivity. |
| Allow Out-of-Range Material Slots | Keep provisionally | It controls animation admission, not texture layout. Removing it needs a separate host-behavior decision. |

The retained split threshold is not a measured performance model. It counts triangles per split submesh. A change in tessellation can change its result without changing the visible surface. Do not replace it with island count or add several new cost sliders.

Evidence: `Editor/Analysis/SlotTransformDecision.cs:261-276,307-314`. The inspector describes draw-call tradeoffs in `Editor/AmuseAvatarOptimizerEditor.cs:761-775`.

Atlasing in 0.2.0 does not combine material slots. It therefore does not remove the extra draw from alpha separation. Retain this gate until an approved replacement has useful evidence. Material combining in 0.3.0 can change its economics without changing alpha facts.

Cutout and transparent eligibility remain distinct even if some controls disappear. Their enable switches do not require duplicated implementations. Evidence: `Editor/Analysis/SlotTransformDecision.cs:168-227`.

## 5. Sampling needs a narrower redesign

Observation on 2026-11-16: The proof uses the smaller limit from the mip cap and minimum texture size. A texture below the minimum size supplies no consulted levels. Its dependent result stays unknown.

Evidence: `Editor/Host/UnityRendererAlphaAnalysis.cs:346-379`. The component documents the two limits in `Runtime/AmuseAvatarOptimizer.cs:50-72,150-169`.

These are related controls, but they are not equivalent. One limits relative reduction. The other limits absolute dimensions. Removing either can change which source textures convert.

An atlas changes texture dimensions and region positions. The same numeric mip limit or minimum size can cover a different source footprint after packing. Do not apply existing source limits to generated pages and call that equivalent evidence.

Recommendation: Resolve one alpha sampling scope before packing. Check atlas output against that established scope. Keep atlas quality acceptance separate from alpha acceptance. A named compatibility policy is an alternative to raw controls, not an approved replacement in this record.

The existing baseline is the result under the selected alpha policy. It is not a claim of exact preservation at every distance. Normal permits whole-slot flattening, limits mips, and accepts anisotropy differences. The atlas path must not add new transparency changes beyond its approved contract.

Evidence: `Presets/normal.json:11-25`. `Editor/Analysis/AlphaSamplingPolicy.cs:6-19,37-57`. The atlas design records separate sampling conditions in sections 6 and 10.

Recommendation: Remove the mobile choice as part of the PC-only cutover. Do not remove unsupported-target refusals or make a non-PC target use desktop acceptance. Mobile support requires its own future evidence.

## 6. What to rebuild and what to reuse

Recommendation: Rebuild the output composition that assumes one opaque result per source material. Atlas results depend on the surface use, visibility group, coordinate route, and page. Keep one authority for final mesh, material, and animation assignments.

The earlier foundation investigation supplies the output-ownership and lifecycle evidence. This record does not propose a new build framework or a full rewrite of alpha separation.

Add bounded region information for the atlas consumer. A region needs links to its source triangles and texture uses. It also needs declared dependencies for overlap, wrapping, filtering, and admitted material states. Do not make every alpha call require island extraction.

Preserve the classifier, exact UV geometry, animation closure, shader attestation, conversion eligibility, and live revalidation where their contracts remain valid. Remove stray-specific evidence and counting only after their consumers disappear. Keep load-bearing error handling and source-asset protection.

Maintain three distinct decisions:

1. Establish which triangles qualify under the selected alpha contract.
2. Decide which alpha transformations to accept before atlas packing.
3. Pack compatible uses and compose final output without changing the alpha decision.

Only the last decision needs atlas placements. Island layout must not grant permission to make unknown triangles opaque. A failed atlas candidate keeps its ordinary alpha result. It does not start another split and pack cycle.

Feature disable needs a written contract. Recommendation: Alpha Separator off prevents render-mode conversion but can still permit appearance-preserving atlasing. A disabled cutout or transparent arm prevents that arm's alpha conversion. It does not automatically ban all texture optimization.

This distinction needs clear UI text and tests. If the user expects a switch to exclude every change to that material family, that is a different product choice.

## 7. Alternatives

Retaining every control avoids an immediate behavior cut. It also makes atlas analysis carry the current lossy policy combinations and their migration burden. Moving them behind a simpler panel does not remove that cost.

Replacing triangles with island verdicts simplifies one description but weakens either coverage or appearance preservation. It also couples alpha separation to atlas topology. This is not recommended.

Recommendation: Keep triangle analysis and remove deliberate alpha-flattening features. Add islands beside that analysis for the texture operations that need them. Rework sampling and final output composition without introducing a general optimizer planner.

Decision on 2026-11-16: The user rejected the proposed alpha-feature cuts. The existing controls smooth over authoring mistakes and remain in scope. Earlier removal recommendations are not the selected direction.

## 8. Migration and required evidence

A new version must not silently reinterpret stored sliders. Normal and Aggressive change meaning if folding disappears. Aggressive also loses approximate alpha admission. More preserved transparency can mean more draws or fewer conversions.

Recommendation: Use one explicit configuration migration, not a hidden legacy execution mode. New presets describe the new contract. Old imports need an actionable incompatibility message when removed values cannot be represented. Do not silently discard those values or auto-enable atlasing as compensation.

A scene upgrade needs a visible explanation of changed alpha behavior. Exact migration and consent remain user decisions. Removing the mobile choice also needs an explicit preset-schema change, even though the supported PC path does not read it.

Preset changes must reach parsing, application, matching, and result fingerprints together. The parser accepts schema five only and rejects unknown keys. Evidence: `Editor/Presets/PresetParser.cs:18-43,70-99` and `Editor/Presets/PresetApplier.cs:19-66`.

Out-of-range animation admission also protects future output slots. An ignored binding can become relevant when a split appends a slot at that index. Preserve the collision guard in `Editor/Build/AlphaSeparationApply.cs:855-872` when output composition changes.

Before implementation approval, settle the following:

- Preserve the existing alpha controls and their configured behavior. No control removal is approved.
- Define the alpha sampling scope and its relationship to generated atlas sampling.
- Define feature-off behavior and migration for older components and presets.

Later implementation must exercise mixed-alpha islands, overlapping uses, alpha-changing material swaps, distant mips, and occupied UV channels. It must also exercise disabled features, retained source references, and final output ownership. Any changed contract needs updated behavioral tests without weakening unrelated safety tests.

## 9. Independent findings and rejected shortcuts

The compatibility investigation recommended merging the cutout and transparent sampling controls because most shipped values match. This record does not accept that conclusion. Different mip defaults and custom values have real effects. Shared implementation does not require identical user policy.

The same investigation called minimum texture size inert because shipped presets do not vary it. This record rejects that description. The value actively excludes small sources and limits consulted mips. Preset agreement does not establish that custom values lack a useful purpose.

Removing the mobile acceptance control does not authorize deleting every conservative fallback. Unsupported targets still need explicit refusal and safe policy selection. The supported-target gate is `Editor/Host/UnityAlphaFieldEvidence.cs:907-915`.

The mobile control can change a reported policy label even when texture capture refuses. Its removal simplifies the supported product surface. It is not a claim that the field has no observable effect anywhere.

The classifier investigation recommended retaining the lossy controls because they define existing user consent. That establishes a migration cost, not a reason to retain every feature. Removing the behavior through an approved versioned cutover remains a valid simplification.

Its summary also placed every alpha control before classification. The polygon density gate actually runs inside the classifier. Evidence: `Editor/Analysis/TriangleAlphaClassifier.cs:43-60,453-494`. Sampling acceptance and transparency flattening are contract choices, not merely performance decisions.

The same investigation proposed never running the classifier on atlas output. This record rejects that absolute rule. A one-time output check can protect the fixed baseline. It must not discover extra conversions or trigger repacking.

The fixed baseline does not require generated color textures to contain the classifier's internal evidence bytes. Those bytes encode predicates, not source appearance. Atlas generation needs its own image and sampling contract.

## 10. Limits

This investigation establishes source behavior and design tradeoffs as of 2026-11-16. It does not establish performance savings, atlas quality, visual equivalence, or coverage on private avatars. No product source, presets, scenes, or assets changed.
