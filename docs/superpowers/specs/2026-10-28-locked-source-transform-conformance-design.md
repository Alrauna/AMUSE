# Locked source transform conformance design

Privacy note. This record uses public product source and public vendor behavior. It contains no private observations, vendor shader copies, machine paths, or generated asset identifiers.

Status: 2026-10-28. Proposed implementation contract. The owner approved writing this design, reviewing it, then writing and reviewing its plan. That approval does not authorize implementation, asset writes, staging, or commits. No code or test changed for this record.

## Intent and basis

AMUSE must explain generated locked shader text from attested original text before it reconstructs a material. Real bundled locks exposed three mismatches in that explanation. The approved investigation identifies those mismatches and a later unexplained refusal. This design corrects the known rules without converting that later refusal into permission to guess.

Basis:

- `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md`, decisions A1–A12.
- `docs/superpowers/specs/2026-10-25-bundled-locked-original-admission-design.md`, decisions Y1–Y13.
- `docs/superpowers/investigations/2026-10-25-bundled-locked-original-swap-in-measurement.md`.
- `.superpowers/sdd/2026-10-25-bundled-locked-original-admission/problem-shape.md`, observed 2026-10-28.

The investigation is evidence, not a decision source. The owner approved its recommended narrow next step. The specific choices below are design calls. Decision IDs use `LA1` through `LA10`.

## Goals

1. Preserve compiler guards when vendor non-removal is proved.
2. Read valid multiline Properties strings without losing later definitions, rename artifacts, or Gamma attributes.
3. Match the installed vendor's property occurrence and declaration exception rules.
4. Retain exact line ownership, generated-text consumption, literal agreement, and era agreement.
5. Locate the first inner contradiction at the stochastic frontier before proposing another widening.
6. Produce a bounded completion result for these changes, distinct from bundled admission.

## Non-goals and anti-goals

- Do not flip either bundled `LockedOriginalAdmitted` flag. Y13 stays a separate admission gate.
- Do not promise that W1–W3 alone make every real lock align.
- Do not implement the later stochastic widening without a reviewed amendment that identifies its cause.
- Do not replace the aligner with a shader compiler, general parser framework, or full vendor emulator.
- Do not add live material values, tags, or material names to the aligner inputs.
- Do not add dependencies, assemblies, global caches, telemetry, permanent tracing, or a new public interface.
- Do not change A6, the seventh fact, animation refusal rules, source attestation pins, or vendor code.
- Do not force a kept result after both trials fail. Do not resolve ambiguity by time, step count, local spelling, or a default value.

## Global constraints

- Unity 2022.3 and C# in the existing production assemblies only. No new dependencies.
- The aligner stays pure. Its input remains original text, generated text, and the original property table.
- Original shader bytes must pass attestation before product use. Generated text and live locked values remain the truth for reconstruction.
- Unknown information refuses only the conclusion that depends on it. Programming and invariant failures still throw.
- Preserve exact kept text, literal round trips, mixed-use refusals, full generated consumption, and agreement among successful eras.
- Keep both bundled admission flags false throughout this work.
- No production or test execution is authorized by this document alone. Never stage, commit, push, publish, or deploy without explicit authorization.
- Never mutate source assets or vendor packages. Unity asset writes and tests may run only in the verified dev project, never in the Census Lab project.
- Human-readable text uses simple English. Records contain no private identifiers, machine paths, instance identities, ports, or vendor shader copies.

## Approaches

| Approach | Benefit | Risk or cost | Decision |
|---|---|---|---|
| Patch each reported shader site or adopt a kept trial after failure | Small local edits | No general proof. Repeated text and nested failures can produce wrong records | Reject |
| Model exact vendor state for every parse path, including actual pragma harvest | Largest direct coverage | Adds candidate-local state across exclusions, passes, and includes before that coverage is needed | Defer |
| Prove non-removal from a conservative source prefix, fix Properties state, and correct installed-era occurrence matching | Small changes at existing methods. Each rule has a local proof and falsifier | Some guards remain speculative because the prefix overestimates harvest | Choose |

The chosen prefix is not a claim that every earlier pragma survives. It is an upper bound on what could have been harvested. A false positive in that upper bound costs coverage, not soundness. If later evidence requires exact harvest, that is a separate reviewed widening.

## LA1. Proof obligations and the check boundary

A successful alignment must identify one permitted explanation of every generated line and preserve the facts needed to reproduce that explanation. Any competing successful era must produce the same facts. Genuine unresolved mappings still refuse.

A6 and the seventh fact remain residual checks. `LockedSourceReproduction.CheckConsistency` checks define runs and recorded removed-block conditions. `TransientUnlockSwapIn.RestoreMismatchReason` retrieves the memoized facts and repeats that check on the clone. It does not realign text or compare every restored scalar with its literal.

For each new acceptance, the aligner must prove text ownership and value association first. Later checks cannot make an incorrectly waived guard or wrongly recorded bake safe. The prior scratch claim that the seventh fact compares all baked writes is explicitly rejected.

## LA2. Conservative guard eligibility

New guard behavior applies only to `Thry2743`. Both historical eras retain their existing guard resolver. This is the same evidence limit as LA4, not a claim that older vendor rules differ.

### Prefix index

Build an immutable ordinal map once per original input. Its key is a potential shader-feature keyword. Its value is the earliest original line index at which the vendor could harvest that spelling.

Scan all original lines in order, including lines inside comments and excluded blocks. Use the vendor's pragma recognition and token extraction, not a general identifier scanner:

1. Trim leading whitespace.
2. Recognize the prefix `#pragma shader_feature` as the vendor does.
3. Remove the vendor's local and non-local shader-feature prefixes in the same order.
4. Trim leading whitespace, split on the space character, then trim each resulting token.
5. Record the earliest line index for each nonempty token.

Scanning unreachable lines deliberately overestimates harvest. Do not narrow the map by guessed exclusion outcomes. Share the immutable map between trial clones. Do not copy a set on each line or rebuild the map during lookahead.

Before enabling the optimization for an original, prove include closure for that input. Scan every vendor-recognized include line conservatively, including unreachable lines. Extract the literal quoted operand without case folding. The initial proved non-inlined operand set is exactly `UnityCG.cginc` and `AutoLight.cginc`. Both appear in the installed vendor's default-include table and are the only active include operands found in both installed bundled originals on 2026-10-28. An absent include also satisfies closure. Any other operand or malformed include disables this optimization for the whole original and retains the existing resolver. It does not extend or change the global include allowlist. This handles the existing allowlist entries whose bare spellings are not vendor default includes.

### Decision at a guard

Use the vendor keyword extraction for a recognized simple guard. Trim the substring after `#ifdef` or `#ifndef`, then take its first space-delimited token. The exact directive line still goes through the text matcher. Exclude only a remainder separated by an actual space. Do not strip a tab-joined comment or replace this split with a general whitespace parser. A line with `&` or `|` retains its existing ordinary-line treatment.

A guard is proved non-removable when either fact holds:

- The era's documented exception set contains its extracted keyword.
- The prefix map has no occurrence of that keyword at an index strictly before the guard.

The installed exception set is `UNITY_SINGLE_PASS_STEREO`, `FORWARD_BASE_PASS`, `FORWARD_ADD_PASS`, `POINT`, and `SPOT`. Its source basis is the installed optimizer's exception table. Do not generalize a new exception from a Unity-looking name. An unproved include disables this optimization even for an exempt keyword, so there is one simple per-input eligibility rule.

When non-removal is proved, match the start directive, walk both branches and their nested edits, match the frame delimiters, then record the outer block as `Kept`. Use the existing extent finder. Do not perform the removed trial or its tie lookahead for that outer frame. Preserve the enclosing lookahead counter and growth boundary just as a resolved block must. A removed enclosing block still records nested frames as `NotEvaluated` through the existing skipped-body path.

When non-removal is not proved, retain the existing two-hypothesis resolver. This includes a pragma inside a removed wrapper: the conservative map can still make a later guard speculative. This design does not claim to fix that coverage corner.

### Soundness

Every keyword the vendor actually harvested before a guard is in the prefix upper bound. Absence therefore proves that removal cannot occur. An exception independently prohibits removal. Keeping the frame introduces no guessed keyword value. Both surviving branches still receive exact line and bake matching. A6 continues to check removed nested blocks. A6 does not check the outer kept frame because the compiler sees it on both materials.

## LA3. Properties lexical state

One shared Properties scan must serve original routing, generated rename artifacts, and original Gamma extraction. Keep the existing public-to-assembly entry points. The scanner implementation stays private to the aligner.

Track brace depth, quoted-string state, and block-comment state across physical lines. Line-comment state ends at each physical newline. Inside a quoted string, comment markers and braces are data. A backslash escapes the next character on the same line. A terminal backslash does not escape the first character of the next line. The string remains open across that newline until an unescaped quote closes it.

Recognize `Properties` and its structural opening brace only outside strings and comments. Support the keyword and brace on the same line or on separate lines. Close the window only on the real structural brace that returns its depth to zero. Do not reopen a window from a keyword inside a quoted display string.

At end of input, an open Properties string, comment, or block is malformed. Use the fixed detail `The Properties text is not well formed.` and the original opening line where available. A generated-side failure maps to the related original Properties opening line, not a generated cache path. Do not silently fall back to a line-local string rule.

The shared scan returns validity as well as the window. `GammaPropertyNames` throws `InvalidOperationException` with that fixed detail on invalid original lexical state. Its attested-original caller must propagate this invariant failure. Do not catch it into an empty attribute set. `AlignForEra` instead converts invalid original or generated scan results into its named alignment refusal. Use the scan result directly in each path rather than leaking a private exception between them. Arbitrary text passed to alignment is unsupported. Property-table capture must not guess flags for a supposedly valid original.

Gamma attributes must be read outside quoted strings and comments on actual property declarations. A continuation line inside a display string cannot add a fake Gamma flag or a fake property declaration. Preserve the original spelling used for rename matching. This is not permission to normalize display strings or whitespace during alignment.

This decision replaces Y7's line-local unclosed-string acceptance rule. The existing `UnclosedDisplayStringIsAccepted` test pins that old rule. Replace that obsolete contract case with malformed-input refusal and a separate valid multiline continuation case. Do not remove valid escaped-quote or brace tests. This is an explicit contract correction, not a test weakened to make an implementation pass.

### Soundness

A valid complete window makes all three consumers agree about the same structural region. Exact Properties matching, suffix intersection, and duplicate-copy checks remain mandatory. A6 and the seventh fact still check defines and removed conditions. They do not verify suffix inference or Gamma flags. Those facts must come from the validated window and property declarations.

## LA4. Occurrence-level baking

### Scope by era

The installed `Thry2743` era gets the corrected occurrence rule. The two historical eras retain their existing matching behavior in this change. Only the installed optimizer source is available in the inspected project on 2026-10-28. The earlier broad declaration statement is not enough evidence to widen those historical eras.

This does not remove historical-era support or the two release rows. It adds no new acceptance to historical eras. Existing historical-era tests must still pass. A later historical widening needs its own source evidence. The shared all-era `Align` entry still refuses disagreement between successful eras.

### Installed-era rule

Port the relevant installed rule from `ShaderOptimizer.ReplaceShaderValues`. Do not use a general HLSL parser or a rule based on the first equals sign.

Tentative hole inference is not proof. Before adopting any installed-era program-line facts, replay the vendor occurrence edits on a separate mutable copy of the renamed original line. Tokenize the renamed original before deleting branch hints. Keep that original token array through all replay edits. Replay declaration visits, repeated-token searches, inline-comment skips, and literal writes in vendor order. Delete `UNITY_BRANCH` and `[branch]` substrings last. Require exact equality with the generated line. Only then adopt both inferred facts and declaration-format state. A failed replay refuses and must not leak tentative state into another trial.

- Tokenize that pre-hint-removal program line once with the vendor's exact split set: space, tab, parentheses, square brackets, plus, minus, asterisk, slash, dot, comma, semicolon, equals, and exclamation mark.
- A candidate property must occur as an exact token. Both occurrence neighbors must belong to the vendor separator set. Use a space for a missing neighbor.
- Preserve the vendor's recognized inline-comment skip shape.
- For each property token in token order, search occurrences in the current line as the vendor does. Reset `declarationFound` for that token's search.
- Exempt at most the occurrence allowed by the immediately preceding token, recognized type set, following trimmed semicolon, and `declarationFound` state. The recognized type set is `int`, `float`, `float2`, `float3`, `float4`, `half`, `half2`, `half3`, `half4`, `fixed`, `fixed2`, `fixed3`, and `fixed4`.
- Every other eligible occurrence is a use. Match an identifier or literal under the existing live, renamed, and baked rules. Record literals under their original property. Preserve exact repeated-literal agreement and mixed-use refusal.
- Preserve mutable-line and repeated-token effects. If the matcher cannot explain an unusual repeated-occurrence form faithfully, refuse it. Do not create a broader declaration exception as a fallback.

The current `WalkOrdinaryLine` deletes hints before `MatchProgramLine`. Change that order for the installed era. For example, an original initializer whose token joins `UNITY_BRANCH` directly to `_P` does not contain a property token `_P`. The vendor leaves it unbaked, then hint deletion exposes the identifier. Alleged generated text that bakes that newly exposed identifier must refuse. This is an explicit installed-era amendment of A3.3's edit order.

Declaration type affects integer printing from the point the vendor observes that declaration. The installed-era format state must follow the surviving walked declarations, not declarations inside removed regions or a whole-file first-declaration scan. Trial clones own that mutable state. An unused removed declaration cannot change a later literal format. A later observed type change affects later prints as the vendor does. If one property would require distinct literal texts, the existing single-literal contract still refuses it.

Delete the broad line-level declaration helper where no historical-era caller needs it. Do not remove helpers that historical-era behavior still needs. Keep the era distinction explicit rather than routing new behavior through a hidden fallback.

### LA4 sampler-declaration amendment

Status: 2026-10-29. The owner directed continuation after the reproduced texture-declaration regression. This amendment restores one existing invariant. An unrenamed declared texture whose only program occurrence is its sampler declaration is Unused in every era. A separate real texture read remains Live. A proved renamed declaration keeps its existing Renamed fact.

The installed vendor's texture branch creates only `_ST` and `_TexelSize` constants. The texture sampler name is not a key in its constant dictionary. See `ShaderOptimizer.cs` L1561–L1578 and L1623. The vendor rename still applies to declarations. Applying the thirteen constant declaration types to the sampler name invented a Live use and broke shared alignment.

For the installed era, recognize a single sampler declarator with optional existing storage prefixes, an existing sampler entry from `TypeKeywords`, the declared texture identifier, and a following semicolon. Verify the exact generated identifier or proved rename. Record rename touches, but no program use and no declaration-format entry. Recognition is occurrence-specific. A later read of that texture must not become exempt because the line also holds a declaration. Repeated token visits must not turn the same non-constant declarator into a constant use.

Keep the thirteen `InstalledDeclarationTypes` unchanged. Keep scalar and vector occurrence replay unchanged. Derived texture names `_ST` and `_TexelSize` remain vector constant candidates. Do not skip all texture tokens or copy the historical broad initializer exemption into the constant path. Other sampler syntax gains no new exemption from this amendment.

Exact replay must verify the pass-through as well as all actual edits before any fact commits. An edited sampler name or an alleged literal in its declaration refuses with no source record. All-era fact agreement, A6, the seventh fact, historical matching, and both admission flags stay unchanged. The later checks do not repair a wrong use fact.

Falsifiers: `SamplerDeclarationRemainsUnusedAcrossEras`, `SamplerReadRemainsLiveAcrossEras`, `RenamedSamplerDeclarationStaysVerified`, and `TextureDerivedInitializerStillRecordsBothLiterals`. Preserve the unchanged `MaskAssignedAfterLockWithoutItsDefineRefuses` as the integration regression. It must pass its alignment guard and still refuse the missing texture define after assignment.

The `SamplerReadRemainsLiveAcrossEras` falsifier must also include a read on the same line after the sampler declaration's semicolon and require the direct installed era to record Live. The same-line form does not assert historical agreement, because those eras retain their existing broad declaration-line behavior.

Approaches: adding sampler types to the constant list is wrong because those names are not constants. Skipping textures is wrong because real reads need Live evidence. The chosen correction is the occurrence-specific verified no-use declaration pass-through. It adds no vendor edit primitive, public interface, helper framework, or product check.

Review status: 2026-10-29. Adversarial review accepted the amendment after requiring the explicit same-line read falsifier. The saved amendment includes that correction. No new vendor edit primitive or flag change is authorized.

### LA4 recorded-print amendment

Status: 2026-10-31. A real vendor lock on the bundled row refused at a kept feature frame. A structure-aware trace of the installed walk found the exact cause. A later occurrence of one baked property can sit behind an unprocessed occurrence of another property whose token the vendor visits later in the same line. The generated position map then names that other property's literal, so the positional literal read fails and the whole line refused.

The vendor replaces every eligible occurrence of a baked property with the one recorded print. The installed inference learns that print from the first occurrence of the same line. The amendment adds one fallback for the installed era. When the positional read of a later occurrence fails and the line proposal already holds the recorded literal print, splice the recorded print at the occurrence and let the exact replay of the line verify the full prediction. A wrong print refuses at the replay compare, so the fallback stays fail closed. The single-literal contract and the mixed-use refusal stay unchanged. A first occurrence never uses the fallback, because no print is recorded yet.

Falsifier: `SwizzledColorBakeWithSiblingScalarAligns` pins the shape under the direct installed era entry. A swizzled color use prints the whole color and keeps the swizzle on the literal, with a baked sibling scalar between the two uses. The pair mirrors a real locked line where a matcap color bakes twice beside a baked theme index on one line. The shape aligns under both historical eras, so the test must pin the installed era directly and not use the shared all-era entry.

### LA4 recorded-kind amendment

Status: 2026-11-01. The mirror of the recorded-print amendment for the identifier kind. A real vendor lock on the bundled row refused at a texture sampling line inside a guard that the retention rule keeps. The line uses one texture name twice and three sibling properties once each. The per-line replay visits tokens in property-name order, so both name occurrences are read before any sibling literal is spliced. The second name occurrence sits behind the not-yet-applied sibling bake, the position map points inside shifted text, and the identifier read fails. A texture property has no literal shape, so no fallback existed and the line refused. The refusal killed the kept trial of the enclosing outline wrapper, the wrapper resolved removed, and the whole family refused with the sentence of the outermost diverging frame.

The vendor keeps every occurrence of a live property under its identifier spelling. The amendment adds one fallback for the installed era. When the positional identifier read of a later occurrence fails and the line proposal already recorded the property as an identifier, keep the spelling and let the exact replay of the line verify the full prediction. A wrong keep refuses at the replay compare, so the fallback stays fail closed. A first occurrence never uses the fallback, because no kind is recorded yet, and the mixed-use refusal stays unchanged.

Falsifier: `PrunedMacroGuardBetweenSiblingFramesAlignsAndRecordsKeptAndRemoved` pins the shape inside a mode-sibling region that mirrors the real stochastic sampling layout, with the sampling line carrying two name uses and three sibling bakes. Its era twin keeps the fallback guards and aligns in every era. The mutation half refuses a changed macro body or a changed tail line, so the fixture is not vacuously green.

Left unchanged: the literal path, the declaration exemptions, LA2 guards, and every existing protection. The joined defined-directive frontier named below stayed open until this date and closed as a mislabel: those directives align verbatim and the real killer was this line shape.

Left unchanged: historical eras, the all-era entry, LA2 guards, and declaration exemptions. The joined defined-directive frontier closed on 2026-11-01 as a mislabel of the recorded-kind shape above.



### Soundness

A true property declaration remains exact text. An initializer read can now record its literal. The property-to-literal association is established by the vendor occurrence rule and exact generated comparison. Reproduction parses and round-trips that literal through the existing plan. A6 catches a value that makes a recorded removed condition false. Neither A6 nor the seventh fact independently verifies every scalar write.

## LA5. The stochastic frontier gate

Before choosing any fourth widening, obtain a read-only trace of the first inner contradiction. The 2026-10-28 diagnostic inputs stopped at outer original lines 18889 and 18987. Those are not proved deepest cursors. Do not rerun the known ten-case failure simply to confirm it.

Use a disposable development-only trace or a debugger. Do not add a permanent product diagnostic interface. The trace must distinguish:

- Original and generated cursors before the first failed comparison.
- The kind of comparison and fixed refusal reason.
- The owning block, trial, enclosing growth boundary, and lookahead state.
- Whether trial cloning and adoption preserve cursor, literal, declaration-format, and frame state.
- Whether a failure is a real vendor transform mismatch, a genuine ambiguous mapping, or a resolver implementation defect.

Raw texts and detailed traces stay in memory. A durable note contains only sanitized source locations, comparison categories, and conclusions. Do not commit vendor excerpts or generated assets.

A resolver implementation defect that restores an existing stated invariant may be fixed only after a reproduced falsifier and a reviewed amendment that names the invariant. A new acceptance rule likewise requires an amendment with its proof and falsifier. A genuine ambiguous mapping keeps refusing. None is an implicit W1–W3 task.

If a trace cannot be obtained with the available tooling, report the missing prerequisite. W1–W3 work may proceed after its own approval. No implementation or record may then claim the stochastic cause is known or that bundled admission is achieved.

## LA6. File and interface ownership

Production changes should stay in these existing modules:

- `Packages/com.alrauna.amuse/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`: prefix index, retained-frame walk, shared Properties scan, installed-era occurrence matching, and candidate-local declaration format state.
- `Packages/com.alrauna.amuse/Editor/Build/LockedSourceAlignmentMemo.cs`: only the invalid attested-original lexical-state handling needed for Gamma capture. Preserve its memo key and lifetime.

No change is planned to the aligner's `Align` or `AlignForEra` parameters, `AlignedLockedSource`, reproduction interfaces, refusal enums, attestation flags, or package dependencies. Private helpers may be split inside the existing aligner if that improves clarity. Do not add a new production file merely to carry a small helper.

Tests belong in the existing `PoiyomiLockedSourceAlignmentTests`, `LockedSourceReproductionTests`, `LockedMaterialReconstructionTests`, and `BundledLockedOriginalRealLockTests` suites. Use public synthetic text for precise falsifiers and the installed vendor for real-lock measurements. Tests must exercise behavior, facts, or errors rather than private helper wiring or source text.

## LA7. Falsifiers and characterization

Each permanent falsifier needs a positive guard that reaches the relevant path. Observe the pre-change result. A case that already passes is characterization, not RED.

| ID | Plausible wrong implementation | Required observable result |
|---|---|---|
| LA-F1 | Treat every installed-era simple guard as removable | A guard before any matching pragma keeps its frame and exact body without a fabricated keyword demand |
| LA-F2 | Derive non-removal from current cursor spelling or property names | An installed-era harvested non-property keyword can still be removed. Repeated text with different facts does not authorize a guessed retained mapping |
| LA-F3 | Forget outer frame ownership while passing through | An installed-era retained outer frame with a removed inner guard and an else branch has exact frame text, inner records, bakes, and final consumption |
| LA-F4 | Treat the prefix upper bound as actual harvest | In the installed era, a pragma in an excluded wrapper does not become evidence of actual harvest. An unproved or case-changed include disables forced retention. Neither case authorizes a forced removal |
| LA-F5 | Reset quote state on every line | A valid multiline display string followed by a plain-plus-renamed duplicate aligns with the exact suffix |
| LA-F6 | Fix routing but leave Gamma and artifacts on a different scan | A real Gamma attribute after the string affects the plan. Fake attributes and declarations inside the string do not |
| LA-F7 | Let an open quote swallow the file | Malformed original or generated Properties text refuses. A valid multiline and escaped-quote control still works |
| LA-F8 | Exempt every property on a declaration line | A true declaration stays while a local initializer reads and records the baked scalar |
| LA-F9 | Use non-word boundaries, reorder hint deletion, or ignore repeated tokens | Vendor-ineligible punctuation stays exact. A property name exposed only by final hint deletion cannot bake. A later token visit that revisits an exempt declaration follows exact mutable replay or refuses |
| LA-F10 | Precompute format from stripped declarations | Removing an integer declaration does not force later integer printing. Surviving declaration format state is cloned and adopted correctly |
| LA-F11 | Assume the seventh fact validates every bake | An unconstrained changed scalar is not claimed as detected. A false removed condition or missing required keyword still refuses |
| LA-F12 | Run a widened guard or occurrence rule in historical eras without evidence | Historical-era cases preserve their prior guard and occurrence contracts. Installed-era coverage improves. Cross-era fact disagreement still refuses |

Preserve the existing duplicate-body ambiguity, both-nonzero tie, zero-and-zero strictness, suffix ambiguity, texture-define, enum, mixed-use, and two-literal protections. Mutating one retained line or adding one generated line must still refuse. Do not pin incidental wording or a private helper layout in new tests.

## LA8. Measurement and bounded completion

Three milestones must not be confused:

1. Rule completion: W1–W3 are implemented, each required regression has evidence, and the relevant full test assemblies have observed zero failures. Historical behavior remains supported. The prefix overapproximation corner remains documented.
2. Frontier characterization: the first inner stochastic contradiction is traced and classified. If no approved rule covers it, record that fact without forcing a result.
3. Admission: Y13's full swap-in and surface gates hold. This design does not authorize that milestone or the flag change.

For measurements after rule completion, retain the existing ten real-lock cases. Add two assigned-texture cases, one per bundled row, behind the existing real-lock gate. Use one disposable linear 4-by-2 RGBA texture with alternating zero and one red and alpha values. Assign it to both `_MainTex` and `_AlphaMask`. Set both scales to `(2, 0.5)` and offsets to `(0.25, 0.125)`. Set `_MainPixelMode` to 1, `_MainAlphaMaskMode` to 2, and `_AlphaForceOpaque` to 0. These binary-exact values avoid incidental float rounding. Require a surviving `PROP_ALPHAMASK` define, non-declaration scale-and-offset uses, and the main texture texel-size use. Record those observations even when alignment later refuses. On alignment success, assert the corresponding baked facts and reproduction values. An unused assignment is not coverage. Do not copy a vendor or private texture.

For each distinct measured generated shader, record lock time, alignment time to success or refusal, reported source location, and the phases actually reached. On success, exercise clone, restore, seventh fact, consistency, production swap-in, and cold dry-run alignment. A skipped or unreached phase is not a pass. Do not infer complete alignment time from a prefix walk.

There is no promise that all twelve cases pass within W1–W3. A remaining refusal is a measured coverage limit. Rule completion is valid only if every observed new refusal is understood as either an unchanged supported refusal or the explicitly traced frontier. A new unexplained failure requires investigation before claiming rule completion. Do not silently expand implementation to make the matrix green.

The two release originals are not installed as real vendor fixtures. Keep release support and existing synthetic era coverage. No new release-package installation or real historical lock gate is required by this design.

## LA9. Unity safety and cleanup

Before any runtime work, enumerate instances read-only, pin the dev editor instance, and verify its exact `Application.dataPath` against the repository Assets folder. Stop on mismatch. Do not query private scene state.

Real locks use the existing explicit opt-in gate and network disclosure. Vendor usage counters may make network calls. Do not bypass consent, edit editor preferences, or silently enable the gate for a whole session.

Before the first vendor lock, require the cache root to be absent or to contain only `README.txt` and its metadata file, with no subdirectories or other entries. Otherwise stop before locking. Do not back up, rewrite, or borrow an existing entry. The installed vendor updates cache sidecars on reuse and can reconcile tags on sibling materials. Deleting only new entries cannot undo those writes. Serialize this suite and ensure no other process or callback locks materials during its run. Stop if unrelated persistent assets are dirty, because the vendor can call the global asset save. If exclusive ownership cannot be established, stop.

Snapshot cache existence, root metadata existence and bytes, the relative file and directory sets, and file digests before the first lock. Snapshot fixture path absence before creating it. Never reuse an unrelated existing fixture path. Wait for vendor verification, queued save, and cache collection callbacks to finish before cleanup. Observe callback removal, not just an empty verification list or a delay. The installed verification list is not cleared when verification finishes. Do not manually clear vendor queues or change vendor settings.

Require complete asset and metadata pairs at preflight. Refuse an orphan cache-root metadata file, a cache directory without its metadata, and either an existing fixture directory or orphan fixture metadata. Place temporary build output under the owned fixture directory. Do not create or unconditionally delete the unrelated shared test output directory.

The test runner can time out a teardown iterator without disposing it. Use a ten-second editor-time drain watchdog that is strictly below the effective teardown timeout. Refuse runtime work if an inherited timeout does not allow that margin. Expiration is a blocked test operation, not an aligner refusal or an accepted outcome. Keep sticky ownership and prohibit later locks from replacing it. Do not delete assets still referenced by pending vendor work. Resume only after observed callback completion, then perform explicit cleanup and snapshot verification. No rule or runtime completion claim is allowed while that ownership remains blocked.

In a finally block, delete created materials and textures first, then their fixture folder, then only cache entries absent from the snapshot. If the cache root was absent initially, delete the created root and its metadata too. Delete assets through Unity and keep each asset with its metadata. When the root existed, preserve its metadata without GUID churn.

Prove final file bytes, directory set, and cache existence equal the snapshot. Cleanup by a name pattern is not enough. A timeout leaves the outcome unknown until observed state resolves it. Never rerun a timed-out lock without checking whether it completed. Remove development-only trace artifacts after the measurement. Do not leave a background callback that can write after the final snapshot.

## LA10. Documentation, review, and delivery

This design explicitly amends A3.7 for proved non-removable installed-era guards, A3.3 for installed-era hint deletion order, Y7 for multiline strings, and the declaration description of fact 4 for the installed era. It clarifies A6 and A8.3 without strengthening their implementation. All other A and Y decisions stand. The admission plan cannot use this document as evidence that Y13 has passed.

At implementation completion, update the product changelog and the dated measurement record with only observed outcomes. Do not claim bundled admission in user-facing docs. Add load-bearing code comments that cite the LA decision and this spec. Existing historical records need not be rewritten to hide their earlier assumptions.

The implementation plan must map each LA decision and falsifier to an owning task. It must specify test filters, runtime smoke steps, and cleanup proofs. No implementation task may contain an unreviewed fourth widening. The plan is delivered for owner approval before implementation.

## LA11. Crossing control span ownership

Status: 2026-10-29. The owner directed continuation after the LA5 trace. This amendment names one resolver defect and restores one existing invariant. The invariant is that the walk consumes each original line at most once. The record `docs/superpowers/investigations/2026-10-28-locked-source-transform-conformance-measurement.md`, section "Single-case inner walk capture", holds the evidence. The reproduced case is the kept-frame double walk only. This amendment does not widen any matching, tie, or fact rule. It adds one refusal for a shape that the saved walk cannot express.

### Defect

The vendor processes exclusion markers and preprocessor directives as two separate states in one forward loop. It skips excluded lines before the preprocessor logic sees them. See `ShaderOptimizer.cs:2053-2098`, `2121-2139`, and `2156-2242`. The bundled Toon original uses this on purpose. A pair of wrappers with one shared condition surrounds the opening and the closing of a preprocessor frame. Observed on 2026-10-29: the bundled Toon original holds five such frames. The first wrapper holds the frame opener and the else line. The second wrapper holds the frame close.

The saved matcher finds each frame extent by counting directives over the whole original. It finds each wrapper extent by counting wrapper markers alone. A wrapper that starts inside a frame body can therefore end after the frame close. When that wrapper keeps, its walk consumes the frame close as an ordinary line. `WalkKeptFrame` then walks the same close again as the frame delimiter. The generated cursor has moved on, so the second walk fails. The kept frame dies and the refusal spreads to the enclosing wrapper.

### Scope by era

The rules below apply to `Thry2743` frame walks, speculative and proved retained alike. They apply to one delimiter only when a wrapper that opened after the frame start is still open at that delimiter line. A static scan of the wrapper markers decides this. It runs only when the walk cursor has already passed the delimiter. The first run of a broader rule on 2026-10-30 broke four existing LA2 and LA-F4 cases. Those cases need a refusal when retention is not proved, and they contain no wrapper. A tie lookahead can pass the else line there, and no approved design allows an acceptance for that shape. The static crossing test keeps those cases unchanged. On 2026-10-30 a review also found that every crossing frame in both bundled originals is speculative. An early pragma in a wrapper puts the frame keyword in the conservative prefix map before every guard. A rule limited to proved retention would never run on the traced case. Both historical eras keep their existing frame walk. This is the same evidence limit as LA2 and LA4. It is not a claim that older vendor rules differ.

### Rule

A frame walk owns a crossed delimiter by stream order. The delimiters are the else line and the close line. When a wrapper crosses a delimiter and the cursor is beyond it, the walk applies the steps below. Otherwise the saved delimiter step runs.

The cursor alone cannot tell a consumed delimiter from a jumped one. A kept crossing wrapper consumes lines by generated-line comparison. A removed crossing wrapper moves the cursor past its whole body and consumes nothing. So the walk state must answer one question: did a generated-line comparison consume the delimiter line? The existing last consumed original line number answers it. A delimiter counts as consumed when that number is at or beyond the delimiter line number. A removed wrapper leaves that number unchanged. Any later surviving line in a kept wrapper pushes it past the delimiter. Comment lines, dropped pragma lines, and blank lines never hold a delimiter.

1. After the body walk, look at a cursor beyond the else index. If the else line counts as consumed, do not walk it again. If a removed wrapper jumped over it, the vendor dropped it with the wrapper body, so do not walk it either. In both cases walk the rest of the else range from the cursor up to the close index. Walk nothing when the cursor is at or beyond the close index.
2. Before the close step, look at a cursor beyond the close index. If the close line counts as consumed, do not walk it again. If it does not, a removed wrapper jumped over it. The vendor stream then has no close for this frame. The saved frame extent has no other close. The kept trial dies with the existing block alignment refusal. The walk never moves the cursor backward to walk the close.
3. A delimiter that counts as consumed was usually consumed once, by exact text comparison, as an ordinary line inside the wrapper trial. The number proves only that the walk is at or past the delimiter in stream order. One shape differs. A removed wrapper nested inside a kept crossing wrapper can hold the close. The close is then dropped with that body, and later lines of the kept wrapper push the number past it. The walk stays sound, because the cursor drives every comparison and the vendor dropped the same close. A close that is still in the generated text fails at the next comparison. No rule skips a comparison for a surviving line. The frame close and the else line still need their generated lines when they survive.
4. A delimiter that no wrapper crosses keeps its saved step. This includes a cursor that a tie lookahead moved past the else line.

No other resolver rule changes. The wrapper extent rule, the frame extent rule, the lookahead window, the growth boundary, both tie rules, the removed-frame trial, and the Y2 catch-up probe stay as they are. The Y2 probe keeps its boundary bypass. The removed-frame trial keeps its cursor reset to the close index plus one.

Recorded, not changed: a removed-frame trial could in theory let a kept crossing wrapper consume the frame close, and the reset would hide that. No trace or reproduced case shows it. A fix needs its own dated amendment with a reproduced falsifier. This amendment does not add that refusal.

### Soundness

When a wrapper opened after the frame start is still open at a delimiter, a cursor beyond that delimiter has one source in the proved retained walk. The wrapper walked past it. The retained body lookahead stops at the body end, and its else body judges to the exclusive close index, so no tie lookahead consumes either delimiter. In a speculative walk a tie lookahead can also move the cursor. The consumed test then still applies. A line that the lookahead consumed counts as consumed only by exact text comparison. A line that nothing consumed refuses at the close and never moves the cursor backward at the else line. Frame extents balance directives, so no nested frame can cross a delimiter of its parent. The wrapper either consumed the delimiter line by exact text comparison or jumped over it as a removed wrapper. Rule 2 and the last consumed line test separate the two. A consumed delimiter has the same text proof that the frame walk would have made. A jumped else line is stream-correct, because the vendor drops the else line and its body part with the removed wrapper. A jumped close leaves the frame unbalanced, so the trial refuses. This is the fail-closed answer. The vendor never emits that shape when both wrappers share one condition.

The rules add no hypothesis and no fact. Trial clones still isolate every cursor, record, bake, and literal. Only the winner is adopted. A6 and the seventh fact are unchanged. They do not prove the new rule. The exact text comparison does.

### Falsifiers

Each falsifier needs a positive guard that reaches the crossing path. Each hand-written generated text follows the vendor rules applied to the reduced original. No new lock produces any witness.

| ID | Plausible wrong implementation | Required observable result |
|---|---|---|
| LA-F13 | Skip every close step without checking for consumption | A crossing pair with both wrappers kept aligns, and each surviving directive line is consumed exactly once. A kept frame with no crossing and a missing, changed, or duplicated generated close still refuses |
| LA-F14 | Accept a crossing close that the generated text lacks or changes | The same case with one generated close removed, changed, or duplicated refuses |
| LA-F15 | Consume the else line twice or skip it | A crossing wrapper that holds the else line aligns with the else line consumed once. A changed else line refuses |
| LA-F16 | Treat the crossing wrapper as removed to avoid the conflict | Both wrappers removed aligns and keeps the middle lines only. Their records read Removed. The both-kept case of LA-F13 still aligns with Kept records |
| LA-F17 | Treat a cursor beyond the close as a consumed close | The first wrapper kept and the second removed refuses, because the frame has no close. The cursor never moves backward |
| LA-F18 | Let a losing crossing trial commit facts | Characterization only. A bake in the frame body appears in the winning alignment. The refusal case returns no source, which every refusal does, so it cannot catch shared trial state. Isolation stays the existing clone and adopt behavior |
| LA-F19 | Apply the new walk to the historical eras | Each historical era keeps its prior result on the same crossing input |
| LA-F20 | Widen frame rules to hide a mismatch | A retained and a removable keyword frame with no crossing keep their prior results. The removed-frame cursor reset is unchanged |

Preserve the existing duplicate-body ambiguity, both-nonzero tie, zero-and-zero strictness, suffix ambiguity, texture-define, enum, mixed-use, and two-literal protections. A case that passes before the change is characterization, not RED.

### Measurement

After the rules are green, run only the default Toon real-lock case again behind the existing gate. Use a temporary in-memory capture only if it refuses again. Do not repeat the full matrix until that case reaches a new, understood result. Every other LA8 milestone stays as written. Record the next frontier as unknown until it is traced. A new refusal needs its own trace and amendment. Do not silently expand this rule to make the matrix green.

### Left unchanged

A6, the seventh fact, shared-era agreement, all attestation rows, and both `LockedOriginalAdmitted` flags stay unchanged. Y13 admission, the teardown watchdog, and the real-lock harness stay unchanged. The two earlier watchdog failures stay unexplained until a failing teardown is observed with the existing pending-work observation.

## Left to the builder

Private helper names, local data representation, and exact fixture spelling are free choices where they do not change the rules above. Prefer existing methods and test helpers. Allocation must be proportional to distinct source facts and necessary trial state, not repeated whole-file scans in lookahead. Performance work beyond that is outside this design.

## Review record

Status: 2026-10-28. Two independent adversarial reviews found three distinct blocking gaps: cache reuse could mutate pre-existing data, include closure was not proved by the existing allowlist, and the new bake rule needed vendor-order replay before acceptance. The corrected revision received two scoped acceptances. Plan review then found that shared temporary output and iterator timeouts needed stronger ownership rules. This revision adds owned child output, orphan-metadata preflight, a bounded callback-drain watchdog, and sticky blocked ownership. Final scoped proof and safety re-reviews accepted the saved document corrections with no remaining blocking findings. This is design acceptance only. No implementation or admission result is claimed.
