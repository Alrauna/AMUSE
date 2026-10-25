# Locked source alignment design

Status: 2026-10-24, third revision. The first revision (2026-10-23) had three parallel adversarial reviews: vendor facts, soundness, and integration. They reported four CRITICAL, thirty MAJOR, and a set of MINOR findings. The second revision integrated all of them. A scoped re-review of the second revision reported five MAJOR and five MINOR findings. This revision integrates those. Decision IDs use the prefix `A` because `K8` and `K9` already name decisions of the per-shader attestation design.

Privacy note: this record covers AMUSE source, earlier AMUSE records, and public vendor packages. It contains no private data. Vendor paths use placeholders. `<optimizer-9.3.64>` is the ShaderOptimizer of the ThryEditor bundled with Poiyomi Toon 9.3.64. `<optimizer-2.74>` is the ShaderOptimizer of thryeditor 2.74.0. `<condition-9.3.64>` is the bundled `DefineableCondition.cs`. `<shader-9.3>` is the Poiyomi Toon 9.3 shader in that package. AMUSE paths are relative to `Packages/com.alrauna.amuse/`.

## Intent

AMUSE proves alpha on the transient unlocked clone `U` of a locked Poiyomi material `L`. `L` renders from its generated locked shader. The lock baked most values as literal numbers into that shader. The investigation `docs/superpowers/investigations/2026-10-23-locked-reconstruction-fidelity-investigation.md` found nine routes where `U` and `L` differ in a fact the proof reads. Seven of them can cause a false positive. This design makes `U` reproduce `L` from the generated shader text. It refuses when it cannot.

The user chose this approach on 2026-10-23 over three others: partial checks, refusing all locked materials, and refusing first and then aligning. The reason: it is the only approach that closes every route and keeps locked coverage.

## Goals

1. Close findings 1, 1a, 4, 5, 6, and 7 of the investigation, and the property-block route 4.8.3.
2. Make the shipped `O`, and a retained `U`, render like `L`. This closes finding 2 (queue and override tags) and 4.8.2 (animations that `L` ignores or reads under another name).
3. Keep locked coverage for every lock that the vendor tool made from the attested original without a later edit that changes what `L` renders.
4. Fail closed. Any text or state that this design does not explain refuses the locked material. A refusal is a coverage loss, never a false positive.

## Non-goals

- Admitting locked materials whose original is a bundled 9.3 row. `K8` of the 2026-10-23 per-shader attestation design stays.
- Other Poiyomi variants (World, Grab Pass, Early Outline, Lil Fur). They stay unsupported.
- A GPU observation of `L` (path 4 of the 2026-09-19 paths record).
- Rewriting float animation curves. A curve that would need a rewrite refuses (A10).
- Restoring stripped textures. A stripped texture belongs to a removed feature block. `L` cannot sample it.
- lilToon. It has no locked path.
- Light-mode pass removal. Neither admitted original declares `_LightModes`, so the vendor never removes a pass from them (fact 13). A missing pass refuses.

## Anti-goals

- A tolerance on any value the proof reads. The proof is exact.
- Trusting material tags, the material name, or the vendor suffix rule for a fact the generated text records.
- Copying vendor source or vendor shader text into the repository. Tests use synthetic shader texts.

## What "renders like `L`" means

The target is the build output as AMUSE writes it. `O` is an unlocked material on the original shader. Play mode uses that output as is. At upload, the vendor callback can lock `O` again. The vendor contract is that a locked material renders like its unlocked source. So a relock of `O` keeps the target. `O` keeps its current name rule. A relock then picks a new suffix, which is harmless because A10 refuses every slot whose animation reads a renamed name.

## Constraints

- The original text is the attested original file. `LockedMaterialIdentity.PassesPinnedPoiyomiIdentity` reads it as of 2026-10-24 (`Editor/Build/LockedMaterialIdentity.cs:304-356`). One shared reader serves attestation and alignment, so both see the same bytes.
- The generated text is the file at the asset path of `L.shader`.
- Production code stays in the two production assemblies. The aligner, the condition evaluator, and the reproduction map are pure C# with no Unity object access. EditMode tests drive them with strings and plain records.
- Simple English in every human-read text. No GUID values or machine paths in comments, tests, or docs.

## Verified facts

These facts come from a source read of the vendor packages and scans of the two admitted shader files, on 2026-10-23 and 2026-10-24. No vendor lock ran.

1. The lock keeps line order. It never splits or merges a kept line. It inserts lines only for duplicate Properties definitions (fact 7) and for defines (fact 8). `[SOURCE]` `<optimizer-9.3.64>:1277-1477`.
2. The parse splits on `\r` and `\n` and drops empty entries. It deletes every line whose trimmed start is `//`, so `//ifex` and `//endex` markers leave nothing. A line that starts with `/*` opens a dropped block that ends at the next line that ends with `*/`. Output uses `AppendLine`, so the line ending depends on the editor platform. `[SOURCE]` `<optimizer-9.3.64>:1456-1458, 1646, 1669-1746`.
3. Every vendor edit is a string replacement. The vendor never changes other whitespace. `[SOURCE]` `<optimizer-9.3.64>:1281-1310, 2170-2237, 2250-2251`.
4. Baking replaces a property identifier with a literal, only inside a program block (`CGPROGRAM` or `CGINCLUDE` to `ENDCG` under the built-in pipeline). A candidate occurrence needs a line token equal to the name, using the 15-character split set, and both neighbor characters in the 25-character separator set. The occurrence on a declaration line, after a type keyword and before `;`, keeps the identifier. Properties lines and ShaderLab bracket references keep the identifier. `[SOURCE]` `<optimizer-9.3.64>:231, 289-308, 1360-1379, 1938, 2170-2237`.
5. A baked float prints with `"0.0####################"`, invariant culture. A property whose first declaration is `int` prints with `"F0"` from then on. A vector, color, `_ST`, or `_TexelSize` prints as `float4(a,b,c,d)`, each component with the parameterless invariant format. 2.74 wraps a negative float in parentheses. 9.3.64 does not, so `-_P` with a negative value prints `--0.5`, which does not compile. Developer mode inserts ` /*<name>*/` before a literal. `[SOURCE]` `<optimizer-9.3.64>:326-357, 2196-2206`, `<optimizer-2.74>:370-385`.
6. A color bakes as follows. HDR and Gamma flags: `Color.linear`. HDR only: raw. Gamma only: raw. Neither: `Color.linear`. A gamma project at lock time: raw for all. Alpha is never converted. `[SOURCE]` `<optimizer-9.3.64>:1178-1193`.
7. A property with an `Animated` tag `1` stays live under its plain name. A tag `2` property is renamed by a regex on every kept line, Properties lines, strings, and comments included. 9.3.64 uses `P(?!\w)` with no left boundary, so it also rewrites identifiers that end in `P`, such as `sampler_MainTex` or `s_start_AlphaToCoverage`. 2.74 uses `(?<!\w)P(?!\w)`. A renamed texture also renames `P_ST` to `P_S_ST` and `P_TexelSize` to `P_S_TexelSize`. Only 2.74.3 also renames `samplerP`. A tag `2` property in `IllegalPropertyRenames` keeps a plain Properties line followed by the renamed line, when its definition is one line with a one-word type. `Range` types and multi-line definitions keep only the renamed line. `[SOURCE]` `<optimizer-9.3.64>:253-268, 1150-1171, 1281-1310`, `<optimizer-2.74>:1636-1668`, 2.74.3 lines 1383-1389.
8. After each `CGPROGRAM` line, the lock inserts ` #define <name> ` lines. First come the enabled keywords whose name occurs in the surviving text. Then come `PROP` plus the uppercase texture name, for each non-null texture in property order whose define occurs in the surviving text. Then ` #define OPTIMIZER_ENABLED `. Nothing deduplicates them. A `[TextureKeyword]` texture has a keyword with the same name as its `PROP` define. `[SOURCE]` `<optimizer-9.3.64>:1088-1114, 1257-1269`.
9. An `//ifex <condition>` block is removed when the condition is true with the lock-time values. A block inside a removed block is never evaluated. The condition grammar splits at the first top-level `&&` or `||` with no precedence. A comparison operand that is not a declared property and not a number reads 0. A texture operand makes a numeric comparison false. `RenderQueue` reads the render queue. `isNotAnimated(P)` is false when `P` has an `Animated` tag other than `0`. `[SOURCE]` `<optimizer-9.3.64>:651-654, 1671-1699`, `<condition-9.3.64>:78-139, 210-229, 331-364, 452-547`.
10. The Toon shader has 148 distinct condition texts. They use `&&` joins of `isNotAnimated(P)` and comparisons, `P==n`, `P!=n`, `RenderQueue>3000`, the constant `0==0`, and spaces around operators. One condition carries trailing text after the number: `//ifex _GlobalMaskDistanceEnable_0==0// [Vector2] ...`. By fact 9 it means `_GlobalMaskDistanceEnable_0==0`. The Two Pass shader adds two `_Stencil2Type` conditions. `[MEASURED]` scan of both files.
11. An `#ifdef K` or `#ifndef K` line without `&` or `|` is removed with its body, its `#else` line, and its `#endif` when `K` was harvested from an earlier surviving `#pragma shader_feature` line, `K` is not in `DontRemoveIfBranchesKeywords`, and the keyword state disables the branch. The `#else` body stays. `#elif` is never special. `[SOURCE]` `<optimizer-9.3.64>:233, 1749-1835`.
12. Every line that starts with `#pragma shader_feature` is deleted. `[SOURCE]` `<optimizer-9.3.64>:1837-1849`.
13. Other edits: the `Shader "..."` name line becomes `Shader "Hidden/Locked/<original name>/<id>"`. The `[ThryShaderOptimizerLockButton]` line default becomes `1`. `Cull [P]` and `ColorMask [P]` with a baked `P` become enum text. Other brackets stay `[P]` or `[P_S]`. `UNITY_BRANCH` and `[branch]` substrings are deleted on program lines. 166 Toon lines hold only one of them and become whitespace-only. Neither admitted original declares `_LightModes`, `_IgnoreProjector`, `_ForceNoShadowCasting`, or `_LODCrossfade` with a matching commented line. Neither contains `//KSOEvaluateMacro`, `#pragma geometry`, hull or domain pragmas, `GrabPass`, `[maxtessfactor(`, or a non-Unity active include. `[SOURCE]` `<optimizer-9.3.64>:1321-1347, 1380-1446, 2250-2251`. `[MEASURED]` scan of both files.
14. 9.3.64 groups materials by a property hash and reuses the first material's generated shader. The hash ignores keywords, texture presence, render queue, and texel size, and prints colors and vectors at low precision. A later member gets its `OriginalKeywords` tag from an already cleared keyword set. `[SOURCE]` `<optimizer-9.3.64>:865-899, 979-1041, 1597`.
15. The Properties block keeps `shader_master_label` with the release label. The 9.3.63 and 9.3.64 files differ only in that label. `[SOURCE]` `<shader-9.3>:5`.
16. The Poiyomi alpha proof reads only the alpha channel of `_Color` and `_LineColor`. `[SOURCE]` `Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:550-560, 667-670`.
17. Size: Toon has 66,805 lines, 1,475 `//ifex` blocks with depth up to 4, and 1,192 `#ifdef` or `#ifndef` lines. Two Pass has 104,560 lines. `[MEASURED]`

## Approaches

1. **Full alignment.** Chosen by the user. AMUSE emulates the closed list of vendor edits on the attested original and matches the generated text against it. It recovers every baked literal, renamed name, define, and removed block. It reproduces those facts in `U`, then checks the consistency rules. Closes every route. Large.
2. **Partial checks.** Derive renames from declarations, check `PROP` defines and property blocks. Leaves findings 5 and 7 open. Medium.
3. **Refuse every locked material.** Closes every route. Loses all locked coverage. Small.
4. **Refuse, then align.** Approach 3 first, approach 1 later. Most total work.

## Decisions

### A1. Truth source

1. The generated text of `L.shader` plus the live values of `L` are the truth for what `L` renders. No material tag is a truth source for a fact that the text records.
2. Alignment success proves that `L` was generated from the attested original bytes by the listed edits. This closes finding 7. A lock made from 9.3.63 fails on the label line and refuses. That is a coverage loss, by design.

### A2. Scope

1. Alignment runs for every locked material that passes `LockedMaterialIdentity.OriginalShaderAttested`. As of 2026-10-24 those are the two 9.3.64 release rows.
2. The aligner knows three vendor eras: the ThryEditor bundled with 9.3.64, thryeditor 2.74.0 to 2.74.2, and thryeditor 2.74.3. The eras differ in the rename regex, the sampler rename, and the negative wrap. The aligner runs each era. Every era that aligns must give the same facts. No era aligning refuses.
3. The generated shader must compile. `ShaderUtil.ShaderHasError(L.shader)` true refuses. This covers the 9.3.64 `--` case of fact 5.
4. Every program block must carry ` #define OPTIMIZER_ENABLED `. A lock made under another render pipeline bakes nothing and refuses.

### A3. The aligner: emulate, then match

1. A pure type `PoiyomiLockedSourceAlignment` in `Editor/Semantics/Poiyomi/`. Input: the original text, the generated text, and the original property table (name, type, HDR flag, Gamma flag, float default). The aligner runs each era of A2.2. Output: a refusal with one fixed detail sentence and the 1-based line number of the original file, or an `AlignedLockedSource` record (A4).
2. Lines. Split both texts on `\r` and `\n`, drop empty entries, and drop whitespace-only lines. Compare lines exactly, including inner whitespace. Trailing whitespace counts, because the vendor keeps it.
3. The walk goes forward over the original. For each original line, the expected generated text comes from the vendor edits, emulated in the vendor order: comment and pragma deletion, the rename regex of the era over the whole line, the shader name and lock button edits, the `Cull` and `ColorMask` edits, `UNITY_BRANCH` and `[branch]` deletion on program lines, and baking. An original line that is whitespace-only after these edits is dropped.
4. Holes. A hole is a span of the expected line that may vary. The rest of the line must match exactly.
   - Baking: on a program line, each occurrence that fact 4 would replace becomes a hole for that property. It matches one literal of fact 5 for the era, with an optional ` /*<name>*/` prefix, or the identifier itself when the property is live.
   - Enum: in `Cull [P]` and `ColorMask [P]`, the bracket becomes a hole. It matches the bracket itself, or one `CullMode` name or one mask text of the vendor (`<optimizer-9.3.64>:402-412, 1424-1446`). The record keeps the parsed enum value.
   - Shader name: on the `Shader "..."` line, the segment after `Hidden/Locked/<original name>/` up to the closing quote is a hole. It records nothing. A sub-asset suffix rides inside it.
5. The renamed set and the suffix `S` come first (A4.2). The rename emulation then needs no guess.
6. Insertions. After a `CGPROGRAM` line, the aligner reads the run of ` #define <name> ` lines and records it. After a plain Properties line of a duplicate-set property, it accepts exactly the renamed copy of that line.
7. Blocks. For every `//ifex` block, and for every `#ifdef` or `#ifndef` line without `&` or `|`, the aligner tries two hypotheses. No harvest simulation is needed, because A6 checks only removed outcomes and the text grounds each outcome.
   - Kept: the whole body aligns, nested blocks included.
   - Removed: the block is skipped. The `#else` body of a removed `#ifdef` block is walked like any kept body, nested blocks included. Then the next 8 surviving original lines after the block align.

   The record states one of four outcomes per block:
   - `Kept`: only the kept hypothesis aligned.
   - `Removed`: only the removed hypothesis aligned.
   - `Unobservable`: the body has no surviving line, so both hypotheses consume nothing.
   - `NotEvaluated`: the block sits inside a removed block.

   When both hypotheses align and the body has a surviving line, the lookahead grows until one hypothesis fails or the file ends. A tie at the end of the file refuses. Neither hypothesis aligning refuses. The bounded lookahead keeps the cost near linear, with a factor of the nesting depth.
8. Any generated line that the walk does not consume refuses. Any construct outside the closed list refuses: a `// [Inlined]` marker, a non-Unity `#include`, `GrabPass`, `#pragma geometry`, hull or domain pragmas, `[maxtessfactor(`, or a missing pass.
9. Each rule has a fixed detail sentence. No sentence names a property value or a machine path.

### A4. Aligned facts

1. A use is an occurrence inside a program block that is not on a declaration line. For each original property `P`, and for each derived name `P_ST` and `P_TexelSize` of a texture, the record holds one program state:
   - `Live`: every use matched the identifier.
   - `Renamed(N)`: every use matched the renamed identifier `N`. Collateral renames of 9.3.64 count here.
   - `Baked(literal)`: every use matched one literal text. Two different literal texts refuse.
   - `Unused`: no use survives.
   Mixed program uses refuse.
2. The suffix. Collect every surviving renamed artifact: generated Properties names that the original does not declare, generated program identifiers, and generated bracket names. For each artifact, collect every split into a declared original name, `_`, and a remainder. Intersect the remainders across all artifacts. The intersection must hold exactly one value `S`. No artifact at all means no rename. Otherwise refuse. The walk then refuses any artifact that the rename of the era with `S` does not produce.
3. Bracket reads. ShaderLab `[P]` references are recorded separately as live reads of `P` or of its renamed name. A `Cull` or `ColorMask` bracket that became enum text records the enum value instead.
4. The record also holds: the define run per program block, the outcome of each `//ifex` block with its condition text, the outcome of each `#ifdef` and `#ifndef` block with its keyword, the surviving original text (for occurrence tests), and the era.

### A5. Reproduction map

1. Reproduction is a pure map from the aligned facts and the live values of `L` to the values of `U`. The build and the dry run both call it (A8).
2. The build reads every value it needs from the clone while the clone still has the generated shader: renamed values with their typed getters, textures with scale and offset, the render queue, and the `RenderType` and `VRCFallback` override tags. It then swaps the shader and writes.
3. `Renamed(N)`: `U.P` takes the value of `L.N`. A renamed texture takes texture, scale, and offset. An unsaved `N` reads the generated shader default, which is what `L` renders.
4. `Baked(literal)`:
   - A float or int takes the parsed literal. Printing the value with the same format must give back the literal text. Otherwise refuse. The format comes from the literal shape: `F0` has no decimal point. A literal shape that does not fit the property type refuses.
   - A vector takes the four parsed components, with the same round-trip rule.
   - A color takes the parsed alpha exactly. Its RGB takes the inverse of the fact 6 conversion, chosen by the property flags and the project color space at alignment. The design assumes that space equals the space at lock. A change corrupts only color RGB. The proof does not read color RGB (fact 16), so this is output fidelity only.
   - A baked `_ST` sets texture scale and offset with the round-trip rule.
   - A baked `_TexelSize` must equal, as text, the fact 5 print of `(1/w, 1/h, w, h)` for the texture on `U`, or `(1,1,1,1)` for no texture. Otherwise refuse. The detail sentence names a shared generated shader as a possible cause (fact 14).
5. Bracket reads of a `Baked` property: the live value of `L.P` must equal the parsed literal exactly. Otherwise refuse, because `L` reads two values.
6. Enum text. A property with enum text and program state `Unused` takes the enum value. Enum text that differs from a `Baked` literal of the same property refuses. Enum text that differs from the live value of a surviving bracket read of the same property refuses.
7. `Live`: `U.P` keeps the value of `L.P`. A program-`Unused` property with a surviving bracket read and no enum text also keeps the value of `L.P`.
8. `Unused` operands of removed blocks: see A6.3.
9. Keywords. The `PROP` part of the define run is the longest run suffix before ` #define OPTIMIZER_ENABLED ` that equals the expected list: `PROP` plus the uppercase name of each texture of `L` that is not null, in property order, whose define occurs in the surviving text. A mismatch refuses. This closes finding 6. The rest of the run is the keyword part. A name that is both an enabled keyword and a `PROP` define appears once in each part. All program blocks must give the same keyword part. `U` takes the keyword part, plus the `OriginalKeywords` tag keywords that do not occur in the surviving text. Those extra keywords cannot change rendering.
   - Amendment, 2026-10-24: the longest-suffix rule has one ambiguous shape. A texture keyword can carry the same name as its `PROP` define. When a name that the expected PROP list derives is also an enabled keyword of the material, and the run holds that name only once, the text cannot tell the keyword define from the PROP define. The split then refuses the material. The refusal reuses the texture define sentence.
10. `U` takes the render queue, the `RenderType` override tag, and the `VRCFallback` override tag that the build read from the clone before the swap.

### A6. Consistency checks

Only removed blocks need checks. A kept block is compiled by both `L` and `U`.

1. A `Removed` `#ifdef K` needs `K` absent from the keywords of `U`. A `Removed` `#ifndef K` needs `K` present.
2. A `Removed` `//ifex` block needs its comparisons true on `U`. Its `isNotAnimated` clauses count as true, because the removal shows that the vendor saw them true. The evaluator ports the vendor grammar of fact 9: first top-level `&&` or `||` split, no precedence, a non-property non-number operand reads 0, texture operands compare false, `RenderQueue` reads the queue of `U`. A condition outside the forms of fact 10 refuses.
3. An `Unused` property that is a comparison operand of a removed block. Its value in `U` is not recorded in the text. An `==` comparison fixes the value. Any other comparison does not, so `U.P` must equal the declared default of the original, and the condition must be true at that value. Otherwise refuse.
4. `Unobservable` and `NotEvaluated` blocks get no check.

### A7. Trust boundary

1. AMUSE trusts the vendor contract that a removed `//ifex` or `#ifdef` block is inert at the values that remove it. A6 checks that the values of `U` still remove it.
2. AMUSE trusts that Unity renders a baked literal and a live property with the same value the same way.
3. AMUSE trusts that a vendor relock of `O` renders like `O`.

### A8. Where it runs

1. A new reconstruction replaces `LockedMaterialReconstruction.TryApply`. It reads the original and generated texts, runs the aligner, reads values from the clone, swaps the shader, applies the reproduction map, and runs A6.
2. `TransientUnlockRestoreDelegate` returns a result with an optional detail sentence. `ClonePersistRestoreVerify` prefers that sentence over the generic one. All callers move to the new signature.
3. `RestoreMismatchReason` keeps its six facts. A seventh fact re-runs A5.9 and A6 on the finished `U` from the memoized aligned facts. A failure records the existing slot refusal `TransientUnlockRestoreMismatch` for every slot of that `L`, with the fixed detail sentence.
4. Memo. The aligned facts are memoized per generated path with its write time, plus the original path with its digest. Reproduction and checks are never memoized. The memo joins `AmuseSessionCaches.Clear()`, and that comment updates its count. Tests get an internal clear call.
5. The dry run. It runs the aligner and the reproduction map on `L` without a clone. A locked slot whose alignment, map, or checks refuse is not shown as deferred. A new note names the cause. The dry run also mirrors A9 and A10. The dry-run cache fingerprint adds the identity of `L.shader` and of the original shader. The name-tag-only deferral arm stays limited to display, as it is as of 2026-10-24.

### A9. Property blocks

The swap-in decides per `L`, over every slot it collected for that `L`, closure slots included. When any renderer of those slots returns true from `Renderer.HasPropertyBlock()`, the swap-in opens no pair for that `L`. Every slot of that `L` records the new slot refusal `LockedMaterialPropertyBlock`. Apply re-runs the check in `ValidateCandidateSlot` for a slot whose live material is a window clone, because a later pass can add a block. This closes 4.8.3.

### A10. Animations that `L` reads differently

1. The swap-in decides per `L`, over the animation closure of every renderer of its slots. It reads float curve bindings and object reference curve bindings through the existing animation index, with the existing helpers `TryStripPrefix` and `TrySplitComponent` of `Editor/Host/AnimationBindingResolution.cs`.
2. A binding `material.<name>` or `material[i].<name>`, with or without a component suffix, refuses when `<name>` is a renamed name, or names a property that is `Baked`, `Unused`, or `Renamed` in `L`, or is a derived `_ST` or `_TexelSize` name of such a texture. `L` ignores the binding or reads it under another name, while `O` would honor it or miss it. Object reference curves on texture names follow the same rule.
3. Refusal: no pair for that `L`, and every slot records the new slot refusal `LockedMaterialAnimationMismatch`. The existing `K9` rule stays for unlocked materials.
4. Coverage cost: the vendor inspector and VRCFury author bindings on renamed names for animated properties. A10 refuses those locked materials until a curve rewrite exists. This is a recorded follow-up.

### A11. Tests

1. The aligner, the condition evaluator, and the reproduction map get focused tests on synthetic texts and records. One positive test and one refusal test per rule.
2. The condition evaluator gets one synthetic test per form of fact 10, including the trailing-text form and the `0==0` constant.
3. Falsifiers, each a plausible wrong implementation that must fail:
   - Finding 1: a material name with an underscore and a renamed alpha input edited after lock.
   - Finding 1a: no `Animated` tags on `L`.
   - Finding 4: a `thry_locked_rename_suffix` tag that disagrees with the material name.
   - Finding 5: a baked `_AlphaForceOpaque` whose saved value changed after lock.
   - Finding 6: an `_AlphaMask` assigned after lock with no `PROP_ALPHAMASK` define.
   - Finding 7: a label line that differs.
   - A shared generated shader: a material whose `OriginalKeywords` tag is empty while the text carries a keyword define.
   - An outline removal with `_EnableOutlines` set to 2 after lock.
   - A9 and A10: a property block, a float binding of a renamed name, a float binding of a baked plain name, and an object reference binding of a renamed texture's plain name.
   - An enum-only property: `L._ColorMask` edited after lock while the generated text holds the enum text.
   - A Properties-region rename: a renamed property whose declaration sits in a removed block while its program uses survive.
   - Each refusal also changes the dry-run row.
4. Integration tests import a synthetic generated shader and a synthetic original into the test temp folder through the existing shader-writing helper. They use the existing seams: the `originalShaderAttested` parameter of `SwapIn`, the attestation function of the reconstruction, and the finish-pass attestation parameter. A proof-level test also uses `VerifiedPoiyomiTestSeams`.
5. The shared transient-unlock test lifecycle builds an alignable synthetic pair, so the suites that are not about alignment keep running.
6. A characterization test keeps fact 16 true: the alpha interpreter result does not change when `_Color` RGB changes.

### A12. Clean cutover

1. Deleted: the tag-driven move, `CleanNameSuffix`, `IsRenameAnimated`, `TryPlainName`, `TryTextureBaseName`, `ReadSavedProperties`, `ElementKeyName`, `DeclaredProperties` where unused, the saved-entry move loops, the saved-entry test writers, and the tests that pin the removed behavior.
2. The comment at `Editor/Build/LockedMaterialReconstruction.cs:43-45` that misstates the duplicate case goes.
3. The comment in `Editor/Build/AlphaSeparationRecords.cs` that names the only writers of the unlock-window refusals adds the A9 and A10 writers.
4. Kept invariant: `U` keeps the name of `L`.
5. Docs: both READMEs (the locked-material refusal sentence), `CHANGELOG.md`, and the investigation record's open questions.

## Left to the builder

1. The tokenizer for occurrence tests, as long as fact 4 holds.
2. The fixed detail sentences, in simple English.
3. The file split between the aligner, the record, the evaluator, the map, and the reconstruction.
4. The internal shape of the restore result.

## Risks

1. A vendor behavior that the source read missed refuses every lock. That is a coverage loss. No vendor lock ran for this design, because vendor packages are never installed in this repository and the Census Lab project must not be mutated. A live lock check in a separate project with the vendor package is a release gate outside this branch.
2. The aligner is large. Its tests are the main protection. Each rule has a refusal test.
3. A10 refuses many locked avatars that animate renamed properties. That cost is visible in the dry run.
4. Vendor session statics can make two locks of one material differ. Each lock still aligns or refuses on its own.

## Validation

1. Focused EditMode runs for each new test, observed RED and then GREEN.
2. The full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies in the dev editor instance.
3. No run in the Census Lab project.

Code comments that implement these decisions reference A1 through A12 and point to this document.
