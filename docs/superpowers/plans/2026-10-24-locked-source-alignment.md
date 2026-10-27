# Locked Source Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

Status: 2026-10-24, second revision. Two parallel adversarial reviews of the first revision (spec coverage, feasibility against the code) reported one CRITICAL, eight MAJOR, and a set of MINOR findings. This revision integrates all of them, except one: the feasibility review asked the Finding 4 falsifier to write `thry_rename_suffix` only. thryeditor 2.74 writes `thry_locked_rename_suffix`, so the falsifier writes both.

**Goal:** Make the transient unlocked clone `U` of a locked Poiyomi material reproduce what the locked material `L` renders, from the generated locked shader text, and refuse when it cannot.

**Architecture:** Three pure units in `Editor/Semantics/Poiyomi/`: a condition evaluator, an aligner that emulates the closed list of vendor edits on the attested original and matches the generated text, and a reproduction map with consistency checks. The build reconstruction and the dry run call the same pure units. The swap-in adds per-material refusals for property blocks and for animations that `L` reads differently.

**Tech Stack:** C# for Unity 2022.3, NUnit EditMode tests through the Unity Test Runner (Unity MCP `run_tests`), no new dependencies.

**Spec:** `docs/superpowers/specs/2026-10-23-locked-source-alignment-design.md` (third revision). Decision IDs `A1` to `A12` and fact numbers refer to it. The investigation it closes is `docs/superpowers/investigations/2026-10-23-locked-reconstruction-fidelity-investigation.md`.

## Global Constraints

- All AMUSE paths below are relative to `Packages/com.alrauna.amuse/`.
- Simple English in every comment, test message, report string, and doc: short active sentences, one idea per sentence, no semicolons, no contractions.
- No absolute path, machine name, user name, port, GUID value, or Unity instance name in any file, comment, commit message, or test. Tests never contain vendor shader text or vendor source. Synthetic shader texts only.
- The aligner, the condition evaluator, and the reproduction map never touch a Unity object. They take strings and plain records.
- Fail closed. Every unexplained difference refuses with a fixed detail sentence. A detail sentence never names a property value or a path.
- One test class per production type, named `<Type>Tests`, in the folder that mirrors production. Method names are behavior sentences. Assertions use `Assert.That`.
- RED first: run each new test and observe the failure before the implementation. An assertion that passes on first run is recorded as characterization in the report.
- Falsifiers are marked `// --- Falsifier N: ... ---` and carry a no-op guard that proves the fixture reaches the code path.
- Code comments that implement a decision cite `A<n>` of the spec.
- Never edit, stage, or commit `Packages/manifest.json`, `Packages/packages-lock.json`, `Packages/vpm-manifest.json`, `ProjectSettings/*`, or `Assets/default.unity*`. Stage explicit paths only. Never push.
- Unity runs only in the dev editor instance. Verify `Application.dataPath` before any MCP write. Never run tests in the Census Lab project.
- After new files: refresh Unity and check the console for compile errors. A filtered run with 0 tests is a failure.
- Full validation runs both assemblies: `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor`. Baseline on 2026-10-24: 3043 total, 3039 passed, 0 failed, 4 ignored.

## Review Focus

1. A generated text that differs from the expected text only in whitespace inside a line must refuse (A3.2). Owner: Task 2, test `WhitespaceChangeInsideALineRefuses`.
2. A block whose body equals the text that follows it must not be recorded as `Kept` when the vendor removed it (A3.7). Owner: Task 3, test `BlockWhoseBodyRepeatsTheFollowingTextGrowsTheLookahead`.
3. A property that is baked in code and also read by a live ShaderLab bracket must refuse when the live value differs from the literal (A5.5). Owner: Task 4, test `BakedPropertyWithADifferentLiveBracketValueRefuses`.
4. A locked material shared by two renderers where only one renderer carries a property block must refuse on both renderers (A9). Owner: Task 6, test `PropertyBlockOnOneRendererRefusesEverySlotOfThatLockedMaterial`.
5. A dry-run row must not promise conversion for a locked material that the build refuses for any A5, A6, A9, or A10 reason (A8.5). Owner: Task 7, test `DryRunDoesNotDeferALockedSlotThatTheBuildRefuses`.

---

### Task 1: Lock condition evaluator

**Files:**
- Create: `Editor/Semantics/Poiyomi/PoiyomiLockCondition.cs`
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockConditionTests.cs`

**Interfaces:**
- Produces:
  - `internal enum PoiyomiLockConditionOutcome { True, False, Unsupported }`
  - `internal interface IPoiyomiLockConditionValues { bool TryGetNumber(string propertyName, out float value); bool IsTexture(string propertyName); int RenderQueue { get; } }`
  - `internal static PoiyomiLockConditionOutcome PoiyomiLockCondition.Evaluate(string conditionText, IPoiyomiLockConditionValues values)`
  - `internal static IReadOnlyList<string> PoiyomiLockCondition.ComparisonOperands(string conditionText)` (property names used in comparisons, for A6.3)

The evaluator implements fact 9 and fact 10 of the spec. `isNotAnimated(P)` evaluates to `True` (A6.2). An operand that is neither a known property nor a number reads 0, as fact 9 says. A texture operand makes a numeric comparison `False`. The vendor-version operands `ThryEditor` and `VRCSDK`, any function other than `isNotAnimated`, and a comparison operator outside `== != > < >= <=` give `Unsupported`, because they are outside the fact 10 forms (A6.2).

- [ ] **Step 1: Write the failing tests.** One test per form, with synthetic property names:
  - `EqualityComparisonIsTrueWhenTheValueMatches`: `_A==1` with `_A` = 1 → `True`. With `_A` = 0 → `False`.
  - `InequalityComparisonUsesTheValue`: `_A!=1` with `_A` = 2 → `True`.
  - `SpacesAroundOperatorsAreAccepted`: `_A == 0 && _B == 0` with both 0 → `True`.
  - `IsNotAnimatedCountsAsTrue`: `_A==0 && isNotAnimated(_A)` with `_A` = 0 → `True`.
  - `RenderQueueReadsTheQueue`: `RenderQueue>3000` with queue 2000 → `False`, with 3001 → `True`.
  - `ConstantComparisonIsTrue`: `0==0` → `True`.
  - `TrailingTextAfterTheNumberReadsAsAMissingProperty`: `_A==0// [Vector2] _B ("x", Vector) = (1,2,0,0)` with `_A` = 0 → `True`, with `_A` = 1 → `False`.
  - `FirstTopLevelOperatorSplitsWithoutPrecedence`: `_A==1 || _B==1 && _C==1` with `_A` = 1, `_B` = 0, `_C` = 0 → the vendor right-nested result `True`.
  - `TextureOperandComparesFalse`: `_T==0` where `_T` is a texture → `False`.
  - `VendorVersionOperandIsUnsupported`: `ThryEditor>1` → `Unsupported`.
  - `UnknownIdentifierReadsAsZero`: `_Missing==0` where `_Missing` is not a property → `True`.
  - `ComparisonOperandsListsOnlyComparedProperties`: `_A==0 && isNotAnimated(_B)` → `["_A"]`.
- [ ] **Step 2: Run** `run_tests` EditMode, `test_names: ["Alrauna.Amuse.Tests.Editor.Semantics.Poiyomi.PoiyomiLockConditionTests"]`. Expected: compile failure, then FAIL.
- [ ] **Step 3: Implement `PoiyomiLockCondition`.** Port the split and comparison rules of fact 9. Do not copy vendor code. Write it from the spec rules.
- [ ] **Step 4: Run** the same filter. Expected: all PASS.
- [ ] **Step 5: Commit** `feat(poiyomi): evaluate lock conditions for alignment checks`.

### Task 2: Aligner line emulation and holes

**Files:**
- Create: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`, `Editor/Semantics/Poiyomi/AlignedLockedSource.cs`, `Editor/Semantics/Poiyomi/LockedSourceProperty.cs`, `Editor/Semantics/Poiyomi/PoiyomiLockEra.cs`, `Editor/Semantics/Poiyomi/LockedBlockRecord.cs` (the block types below, filled in Task 3)
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Produces:
  - `internal enum PoiyomiLockEra { Bundled9364, Thry274To2742, Thry2743 }`
  - `internal readonly struct LockedSourceProperty { string Name; UnityEngine.Rendering.ShaderPropertyType Type; bool Hdr; bool Gamma; float DefaultFloat; }` (constructor with all fields). The spec A3.1 list has no texture default. No rule reads one.
  - `internal enum LockedBlockKind { Ifex, Ifdef, Ifndef }`, `internal enum LockedBlockOutcome { Kept, Removed, Unobservable, NotEvaluated }`, `internal readonly struct LockedBlockRecord { LockedBlockKind Kind; LockedBlockOutcome Outcome; string ConditionOrKeyword; int OriginalLine; }`. Task 2 always returns an empty `Blocks` list.
  - `internal enum LockedPropertyState { Live, Renamed, Baked, Unused }`
  - `internal sealed class AlignedLockedSource` with: `PoiyomiLockEra Era`, `string Suffix` (null when no rename), `IReadOnlyDictionary<string, LockedPropertyState> States`, `IReadOnlyDictionary<string, string> RenamedNames`, `IReadOnlyDictionary<string, string> BakedLiterals`, `IReadOnlyDictionary<string, int> EnumValues`, `IReadOnlyDictionary<string, string> BracketReads` (property to the name read), `IReadOnlyList<IReadOnlyList<string>> DefineRuns`, `IReadOnlyList<LockedBlockRecord> Blocks`, `string SurvivingText`. It has one internal constructor that takes every field, so Task 4 tests build instances by hand.
  - `internal static IReadOnlyCollection<string> PoiyomiLockedSourceAlignment.GammaPropertyNames(string originalText)` (names whose Properties line carries `[Gamma]`, because Unity 2022.3 `ShaderPropertyFlags` has no Gamma member)
  - `internal readonly struct LockedSourceAlignmentResult { bool Succeeded; AlignedLockedSource Source; string RefusalDetail; int OriginalLine; }`
  - `internal static LockedSourceAlignmentResult PoiyomiLockedSourceAlignment.Align(string originalText, string generatedText, IReadOnlyList<LockedSourceProperty> properties)` (runs every era, A2.2)
  - `internal static LockedSourceAlignmentResult PoiyomiLockedSourceAlignment.AlignForEra(string originalText, string generatedText, IReadOnlyList<LockedSourceProperty> properties, PoiyomiLockEra era)`

This task covers A3.1 to A3.6 except blocks, A3.8, A3.9, A4.1, A4.2, and A4.3. A synthetic original in the tests holds a Properties block, one `CGPROGRAM` block with declarations and uses, ShaderLab brackets, a `Cull [P]` line, comment lines, and a `#pragma shader_feature` line. A test helper builds the matching generated text by hand for each case.

- [ ] **Step 1: Write the failing tests.**
  - `UnchangedTextWithDefinesAlignsAsLive`: generated text equals the original minus comments and pragmas, with the shader name hole and the lock button default `1`, plus ` #define OPTIMIZER_ENABLED ` after `CGPROGRAM`. Every program property state is `Live`.
  - `BakedFloatRecordsTheLiteral`: one use replaced by `0.5` → state `Baked`, literal `0.5`.
  - `BakedIntRecordsTheF0Literal`: a property declared `int` replaced by `3` → `Baked`, literal `3`.
  - `BakedVectorRecordsTheFloat4Literal`: `float4(1,2,0.5,0)`.
  - `DeveloperModeCommentBeforeALiteralIsAccepted`.
  - `TwoDifferentLiteralsForOnePropertyRefuse`.
  - `MixedLiveAndBakedUsesRefuse`.
  - `DeclarationLineKeepsTheIdentifierWhenBaked`.
  - `RenameOnEveryLineIncludingStringsAligns`: suffix `Body_Mat`, a Properties string naming the property, a bracket, and a program use. State `Renamed`, suffix `Body_Mat`.
  - `SuffixWithAnUnderscoreIsFoundByIntersection`: original declares `_Hi` and `_Hi_Tex`, rename of `_Hi_Tex` with suffix `A_B`.
  - `CollateralRenameAlignsOnlyInTheBundledEra`: identifier `_X_P` renamed with `_P` → aligns for `Bundled9364`, refuses for `Thry274To2742`.
  - `RenamedTextureRenamesScaleOffsetBeforeTheSuffix`: `_T_ST` → `_T_S_ST`.
  - `SamplerRenameAlignsOnlyIn2743`.
  - `NegativeWrapAlignsOnlyInThe274Eras`: `-(-0.5)` versus `--0.5`.
  - `CullBracketEnumTextRecordsTheValue`: `Cull [_Cull]` → `Cull Front` records 1.
  - `ShaderNameHoleAcceptsAnyId`.
  - `WhitespaceChangeInsideALineRefuses` (Review Focus 1).
  - `TrailingWhitespaceChangeRefuses`.
  - `WhitespaceOnlyLinesAreDroppedOnBothSides`.
  - `UnconsumedGeneratedLineRefuses`.
  - `InlinedMarkerRefuses`, `NonUnityIncludeRefuses`.
  - `UnityBranchDeletionThatEmptiesALineDropsIt`.
  - `PropertyWithNoSurvivingUseRecordsUnused`.
  - `AmbiguousSuffixIntersectionRefuses`.
  - `GammaAttributeIsReadFromThePropertiesLine`.
  - `ErasThatAlignMustAgree`: `Align` returns success only when all aligning eras give equal facts.
  - `NoAligningEraRefuses`. Every refusal test in this class calls `Align`, except the three era tests, which call `AlignForEra`.
- [ ] **Step 2: Run** the class filter. Expected: FAIL.
- [ ] **Step 3: Implement** the line walk, the era rename regex emulation, the holes, the state collection, and the suffix intersection.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat(poiyomi): align generated locked shader lines with the attested original`.

### Task 3: Aligner blocks, defines, and duplicates

**Files:**
- Modify: `Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignment.cs`, `Editor/Semantics/Poiyomi/AlignedLockedSource.cs`
- Modify: `Editor/Semantics/Poiyomi/LockedBlockRecord.cs` (only if a field is missing)
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiLockedSourceAlignmentTests.cs`

**Interfaces:**
- Consumes: Task 2 types, including `LockedBlockRecord`.
- Produces: filled `AlignedLockedSource.Blocks` and `DefineRuns`.

This task covers A3.6 and A3.7 and A4.4.

- [ ] **Step 1: Write the failing tests.**
  - `KeptIfexBlockIsRecordedKept`.
  - `RemovedIfexBlockIsRecordedRemoved`.
  - `IfexBlockWithOnlyCommentsIsUnobservable`.
  - `NestedBlockInsideARemovedBlockIsNotEvaluated`.
  - `BlockWhoseBodyRepeatsTheFollowingTextGrowsTheLookahead` (Review Focus 2): the removed block's body equals the next 10 surviving lines after the block, then differs. That is past the base window of 8. Expected outcome `Removed`. A fixed 8-line window records `Kept` or ties and fails this test.
  - `TieAtTheEndOfTheFileRefuses`.
  - `RemovedIfdefKeepsItsElseBody`, with a nested `//ifex` inside the `#else` body.
  - `IfdefWithAnAmpersandIsNeverStripped`.
  - `DefineRunIsRecordedPerProgramBlock`.
  - `DuplicatePropertyLineMustBeTheRenamedCopy`: an exact renamed copy aligns, any other next line refuses.
  - `RangePropertyInTheDuplicateSetKeepsOnlyTheRenamedLine`.
  - `// --- Falsifier 7: Properties-region rename ---` `PropertiesRegionRenameWithSurvivingProgramUsesFindsTheSuffix`. No-op guard: the same text without the rename aligns with `Suffix == null`.
  - `ProgramBlockWithoutOptimizerEnabledRefuses` (A2.4).
- [ ] **Step 2: Run** the class filter. Expected: the new tests FAIL.
- [ ] **Step 3: Implement** the two-hypothesis block walk with the 8-line lookahead that grows on a tie, the define run reader, and the duplicate line rule.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat(poiyomi): decide locked shader blocks by two-hypothesis alignment`.

### Task 4: Reproduction map and consistency checks

**Files:**
- Create: `Editor/Semantics/Poiyomi/LockedSourceReproduction.cs`, `Editor/Semantics/Poiyomi/LockedReproductionPlan.cs`, `Editor/Semantics/Poiyomi/ILockedMaterialValues.cs` (holds the interface and `LockedTextureValue`)
- Test: `Tests/Editor/Semantics/Poiyomi/LockedSourceReproductionTests.cs`

**Interfaces:**
- Consumes: `AlignedLockedSource`, `LockedSourceProperty`, `PoiyomiLockCondition`.
- Produces:
  - `internal readonly struct LockedTextureValue { string IdentityToken; int Width; int Height; Vector2 Scale; Vector2 Offset; }` in `ILockedMaterialValues.cs`. `IdentityToken` is null for no texture. Otherwise it is an opaque string minted by the Unity view (Task 5) from the texture asset GUID plus local file id, or from the instance id for a texture without an asset. Only the Unity view turns a token back into a `Texture`.
  - `internal interface ILockedMaterialValues` with typed reads by name: `bool TryGetFloat(string, out float)`, `bool TryGetVector(string, out Vector4)`, `bool TryGetColor(string, out Color)`, `bool TryGetTexture(string, out LockedTextureValue)`, and `int RenderQueue`, `string RenderTypeTag`, `string VrcFallbackTag`, `IReadOnlyList<string> OriginalKeywordsTag`, `IReadOnlyList<string> Keywords`, `bool ProjectIsLinear`.
  - `internal sealed class LockedReproductionPlan` with float, vector, color, texture-scale-offset, and texture writes by name, `IReadOnlyList<string> Keywords`, `int RenderQueue`, `string RenderTypeTag`, `string VrcFallbackTag`.
  - `internal readonly struct LockedReproductionResult { bool Succeeded; LockedReproductionPlan Plan; string RefusalDetail; }`
  - `internal static LockedReproductionResult LockedSourceReproduction.Build(AlignedLockedSource source, IReadOnlyList<LockedSourceProperty> properties, ILockedMaterialValues locked)` (A5)
  - `internal static string LockedSourceReproduction.CheckConsistency(AlignedLockedSource source, IReadOnlyList<LockedSourceProperty> properties, ILockedMaterialValues reproduced)` returns null when consistent, else a fixed sentence (A5.9 PROP part, A6). `reproduced` reads the values of `U` after the plan. The build passes the finished clone. The dry run passes `LockedPlanView` (Task 7), a view of `L` with the plan applied.

- [ ] **Step 1: Write the failing tests** with a fake `ILockedMaterialValues`.
  - `RenamedPropertyTakesTheSuffixedValue` (finding 1 at map level).
  - `UnsavedRenamedNameReadsTheGeneratedDefault`.
  - `RenamedTextureTakesTextureScaleAndOffset`.
  - `// --- Falsifier 8: baked value edited after lock ---` `BakedFloatOverridesAnEditedSavedValue`: saved 1, literal `0.0` → plan writes 0. No-op guard: with saved 0 the plan writes 0 too and the test asserts the literal path was taken.
  - `FloatLiteralThatDoesNotRoundTripRefuses`.
  - `BakedVectorRoundTripsAtMapLevel`.
  - `BakedScaleOffsetSetsTextureScaleAndOffset`.
  - `IntLiteralShapeOnAVectorPropertyRefuses`.
  - `ColorTakesTheExactAlphaAndConvertedRgb`.
  - `BakedTexelSizeThatDiffersFromTheTextureRefusesAndNamesSharing`.
  - `BakedPropertyWithADifferentLiveBracketValueRefuses` (Review Focus 3).
  - `// --- Falsifier 9: enum-only property ---` `EnumOnlyPropertyTakesTheEnumValue`: `L._ColorMask` edited after lock, enum text records the lock value. No-op guard: the state is `Unused` and the enum value exists.
  - `EnumTextThatDiffersFromABakedLiteralRefuses`.
  - `EnumTextThatDiffersFromALiveBracketRefuses`.
  - `UnusedPropertyWithALiveBracketKeepsTheLockedValue`.
  - `PropDefineMissingForAnAssignedTextureRefuses` (finding 6).
  - `// --- Falsifier 10: shared generated shader ---` `KeywordPartComesFromTheDefinesNotTheTag`: the tag is empty, the define run holds a keyword. No-op guard: the define run is non-empty.
  - `TextureKeywordNamedLikeItsPropDefineAppearsInBothParts`.
  - `ProgramBlocksWithDifferentKeywordPartsRefuse`.
  - `RemovedIfexWithAFalseConditionOnURefuses`.
  - `RemovedIfexWithAnUnsupportedConditionRefuses`.
  - `// --- Falsifier 11: outline set to 2 ---` `UnusedOperandOfAnInequalityMustEqualTheDefault`. No-op guard: the operand is `Unused` and the block is `Removed`.
  - `UnusedOperandOfAnEqualityTakesTheComparedValue`.
  - `RemovedIfdefWhoseKeywordIsOnRefuses`.
  - `RemovedIfndefWhoseKeywordIsOffRefuses`.
  - `UnobservableBlockGetsNoCheck`.
  - `RenderQueueAndTagsComeFromTheLockedMaterial`.
- [ ] **Step 2: Run** the class filter. Expected: FAIL.
- [ ] **Step 3: Implement** `Build` and `CheckConsistency` from A5 and A6.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat(poiyomi): reproduce locked facts and check removed blocks`.

### Task 5: Build reconstruction cutover

**Files:**
- Modify: `Editor/Build/LockedMaterialReconstruction.cs` (replace `TryApply` and delete the tag-driven move), `Editor/Build/TransientUnlockAvailability.cs` (delegate returns `TransientUnlockRestoreResult`), `Editor/Build/TransientUnlockSwapIn.cs` (prefer the restore detail, seventh fact), `Editor/Build/LockedMaterialIdentity.cs` (shared original text reader), `Editor/Build/AmuseSessionCaches.cs` (memo clear, comment count from seven to eight)
- Create: `Editor/Build/TransientUnlockRestoreResult.cs`, `Editor/Build/LockedSourceAlignmentMemo.cs`, `Editor/Build/UnityLockedMaterialValues.cs` (the `ILockedMaterialValues` view over a `Material`, which mints and resolves texture tokens)
- Modify tests: `Tests/Editor/Build/LockedMaterialReconstructionTests.cs`, `Tests/Editor/Build/TransientUnlockTestLifecycle.cs`, `Tests/Editor/Build/TransientUnlockSwapInTests.cs`, `Tests/Editor/Build/TransientUnlockAvailabilityTests.cs`, `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs` (its lifecycle locked materials), `Tests/Editor/Build/AmuseSessionCachesTests.cs` or the existing test that pins the clear list
- Create test helper: `Tests/Editor/Build/LockedSourceFixture.cs`. It reads the text of the shipped original `Tests/Editor/Semantics/Poiyomi/PoiyomiSemanticTest.shader`, so the fixture seams that match that shader by identity keep working. It writes only the generated partner through `TestShaderWriter.WriteTestShader`, named `Hidden/Locked/<original name>/<id>`. Its default partner drops comment, pragma, and blank lines, sets the lock button default to 1, adds ` #define OPTIMIZER_ENABLED ` after each `CGPROGRAM`, and keeps every property live, with no rename. Options let a test bake one property, rename with a given suffix, add a define, or change one line. The helper is hand-written from the spec rules. It is never a copy of vendor code.

**Interfaces:**
- Consumes: Tasks 2 to 4.
- Produces:
  - `internal readonly struct TransientUnlockRestoreResult { TransientUnlockRestoreOutcome Outcome; string Detail; }`
  - `internal delegate TransientUnlockRestoreResult TransientUnlockRestoreDelegate(Material lockedClone)`
  - `internal static TransientUnlockRestoreResult LockedMaterialReconstruction.TryApply(Material clone, Func<Shader, bool> originalShaderAttested)`
  - `internal static string LockedMaterialIdentity.ReadOriginalText(Shader original)` (one reader for attestation and alignment)
  - `internal static LockedSourceAlignmentResult LockedSourceAlignmentMemo.GetOrAlign(Shader generated, Shader original, IReadOnlyList<LockedSourceProperty> properties)` and `internal static void LockedSourceAlignmentMemo.Clear()`
  - `internal static IReadOnlyList<LockedSourceProperty> LockedSourceAlignmentMemo.PropertiesOf(Shader original, string originalText)` (names, types, float defaults, and HDR from `Shader.GetPropertyFlags`, Gamma from `GammaPropertyNames`)

Order inside `TryApply` (A5.2, A8.1): tags and original resolution as in the code on 2026-10-24, `ShaderUtil.ShaderHasError(clone.shader)` refuses, read both texts, align through the memo, read every value of the generated-shader view into `UnityLockedMaterialValues` while the clone still has the generated shader, build the plan, swap the shader, write the plan, set the lock flag to 0, run `CheckConsistency` on the finished clone. Every refusal returns its sentence in `Detail`.

The seventh fact in `RestoreMismatchReason` (A8.3) takes the memoized `AlignedLockedSource` for `locked.shader` and runs `CheckConsistency` against the finished clone through `UnityLockedMaterialValues`. It does not rebuild the plan.

- [ ] **Step 1: Write the failing tests** in `LockedMaterialReconstructionTests` with `LockedSourceFixture`:
  - `--- Falsifier 1 ---` `UnderscoreNameWithAnEditedRenamedAlphaInputReproducesTheLockedValue`.
  - `--- Falsifier 2 ---` `MissingAnimatedTagsDoNotStopTheRenamedMove`.
  - `--- Falsifier 3 ---` `LockedRenameSuffixTagThatDisagreesWithTheNameIsIgnored`: the fixture writes both `thry_locked_rename_suffix` and `thry_rename_suffix` with values that disagree with the real suffix in the generated text.
  - `--- Falsifier 4 ---` `BakedForceOpaqueEditedAfterLockTakesTheBakedValue`.
  - `--- Falsifier 5 ---` `MaskAssignedAfterLockWithoutItsDefineRefuses`.
  - `--- Falsifier 6 ---` `DifferentLabelLineRefuses`.
  - `GeneratedShaderWithCompileErrorRefuses`.
  - `QueueAndOverrideTagsAreCarriedFromTheLockedMaterial`.
  - `ClonedNameStaysTheLockedName`.
  - `RelockToTheSamePathInvalidatesTheMemo`.
  - The clear-list test adds the memo, following the existing source-hash memo pattern.
  Each falsifier carries a no-op guard: the fixture aligns and the plan is non-empty before the edit.
- [ ] **Step 2: Write the failing tests** in `TransientUnlockSwapInTests`: `AlignmentRefusalDetailReachesTheSlotRefusal` (the recorded detail equals the reconstruction sentence) and `SeventhFactCatchesAConsistencyBreakAfterRestore` (a test restore delegate returns success with a clone that breaks A6).
- [ ] **Step 3: Run** both classes. Expected: FAIL.
- [ ] **Step 4: Implement.** Delete `MoveSuffixedSavedValues`, `CleanNameSuffix`, `IsRenameAnimated`, `TryPlainName`, `TryTextureBaseName`, `ReadSavedProperties`, `ElementKeyName`, and `DeclaredProperties` when unused, and the comment at the old lines 43-45 (A12). Move all delegate callers to the new result. Switch `TransientUnlockTestLifecycle` and the lifecycle uses in `AmusePlatformFinishPluginTests` to `LockedSourceFixture` so the suites that are not about alignment keep running (A11.5). The default fixture has no rename, so the existing suffixed-binding tests keep their meaning: their binding names are not renamed names. Delete the saved-entry writers and the tests that pin the removed move.
- [ ] **Step 5: Run** `LockedMaterialReconstructionTests`, `TransientUnlockSwapInTests`, `TransientUnlockAvailabilityTests`, `TransientUnlockWindowCloseTests`, `TransientUnlockTransformationTests`, `LockedMaterialIdentityTests`, `AmusePlatformFinishPluginTests`. Expected: PASS.
- [ ] **Step 6: Run** the full two-assembly suite. Expected: 0 failed. Record counts.
- [ ] **Step 7: Commit** `feat(build): reconstruct locked materials from the aligned generated shader`.

### Task 6: Property block and animation refusals

**Files:**
- Modify: `Editor/Build/TransientUnlockSwapIn.cs` (promote the private nested `LockedSlots` to an internal nested type, add the adapters), `Editor/Build/AlphaSeparationApply.cs` (`ValidateCandidateSlot`), `Editor/Build/AlphaSeparationRecords.cs` (two new tail members and the writers comment), `Editor/Build/AmuseReportStrings.cs` (two string triples), `Editor/Host/AnimationBindingResolution.cs` (make `TryStripPrefix`, `TrySplitComponent`, `TryStripIndexedMaterialPrefix`, and `MaterialPrefix` internal)
- Create: `Editor/Build/LockedMaterialAdmission.cs` (the pure per-`L` decision)
- Test: `Tests/Editor/Build/TransientUnlockSwapInTests.cs`, `Tests/Editor/Build/LockedMaterialAdmissionTests.cs`, `Tests/Editor/Build/AlphaSeparationApplyTests.cs`, `Tests/Editor/Build/AmuseReportStringsTests.cs`

**Interfaces:**
- Consumes: `AlignedLockedSource` from `LockedSourceAlignmentMemo` (Task 5).
- Produces:
  - `AlphaSeparationSlotRefusal.LockedMaterialPropertyBlock`, `AlphaSeparationSlotRefusal.LockedMaterialAnimationMismatch`.
  - `internal static AlphaSeparationSlotRefusal LockedMaterialAdmission.Decide(AlignedLockedSource source, bool anyRendererHasPropertyBlock, IEnumerable<string> materialBindingPropertyNames)`. `materialBindingPropertyNames` are raw `EditorCurveBinding.propertyName` values of float and object reference bindings on the renderer paths of the slots. The method strips `material.` and `material[i].` with the three helpers, strips a component suffix, and applies A10.2. It returns `None` when the material may open a pair. A property block wins over an animation mismatch.
  - Swap-in adapter: `TransientUnlockSwapIn` collects the names from the `AnimationIndex` (`context.Extension<AnimatorServicesContext>().AnimationIndex`), using `VirtualClip.GetFloatCurveBindings()` and `GetObjectCurveBindings()` for each renderer path of the entry's slots, closure slots included.
  - Dry-run adapter (Task 7) collects the names with `AnimationUtility.GetCurveBindings` and `GetObjectReferenceCurveBindings` over the clips that the dry run already enumerates (`Editor/DryRun/AlphaSeparationDryRun.cs` around line 342).

The decision runs per `L` before the clone, over every collected slot, closure slots included (A9, A10). A refusal opens no pair and records the refusal on every slot of that `L`, like the restore mismatch path.

- [ ] **Step 1: Write the failing tests.**
  - `PropertyBlockOnOneRendererRefusesEverySlotOfThatLockedMaterial` (Review Focus 4) in `TransientUnlockSwapInTests`: two renderers share `L`, one carries a block, both slots record `LockedMaterialPropertyBlock`.
  - In `LockedMaterialAdmissionTests`, each a falsifier with a no-op guard that the same source without the binding returns `None`:
    - `// --- Falsifier 12 ---` `FloatBindingOfARenamedNameRefuses`.
    - `// --- Falsifier 13 ---` `FloatBindingOfABakedPlainNameRefuses`.
    - `// --- Falsifier 14 ---` `ObjectBindingOfARenamedTexturePlainNameRefuses`.
    - `IndexedMaterialBindingIsRead`: `material[1]._X_S`.
    - `FloatBindingOfARenamedTextureScaleOffsetRefuses`: `material._T_ST.x` for a renamed `_T`.
    - `FloatBindingOfALivePropertyPasses`.
  - `AnimationMismatchRecordsTheRefusalOnEverySlot` in `TransientUnlockSwapInTests`.
  - `PropertyBlockAddedAfterSwapInRefusesAtApply` in `AlphaSeparationApplyTests`.
  - String triple tests for both members in `AmuseReportStringsTests`, matching the existing pattern.
- [ ] **Step 2: Run** the four classes. Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the four classes. Expected: PASS.
- [ ] **Step 5: Commit** `feat(build): refuse locked materials with property blocks or mismatched animations`.

### Task 7: Dry-run mirror

**Files:**
- Modify: `Editor/DryRun/AlphaSeparationDryRun.cs` (`IsSlotDeferred`, the binding adapter, the cache fingerprint in `BuildRendererBody` with `AssetIdentity` of `L.shader` and of the original), `Editor/DryRun/MaterialTransformationRow.cs` (`NoteKind` member and the `RowText.NoteSentence` sentence)
- Create: `Editor/DryRun/LockedPlanView.cs` (an `ILockedMaterialValues` view of `L` with a `LockedReproductionPlan` applied)
- Test: `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs`

**Interfaces:**
- Consumes: `LockedSourceAlignmentMemo.GetOrAlign`, `LockedSourceReproduction.Build`, `LockedSourceReproduction.CheckConsistency` with `LockedPlanView`, `LockedMaterialAdmission.Decide`, `UnityLockedMaterialValues`.
- Produces: `NoteKind.LockedMaterialNotReproducible`.

- [ ] **Step 1: Write the failing tests.**
  - `// --- Falsifier 15: dry-run promise ---` `DryRunDoesNotDeferALockedSlotThatTheBuildRefuses` (Review Focus 5), one case each for an alignment refusal, a reproduction refusal, a consistency refusal, a property block, and an animation mismatch. No-op guard: the same scene without the defect defers the slot.
  - `DryRunDefersAnAlignableLockedSlot`.
  - `RelockToTheSamePathChangesTheCacheFingerprint`.
- [ ] **Step 2: Run** the class filter. Expected: FAIL.
- [ ] **Step 3: Implement.** The name-tag-only deferral arm stays display-only.
- [ ] **Step 4: Run** the class filter. Expected: PASS.
- [ ] **Step 5: Commit** `feat(dryrun): mirror locked source alignment refusals`.

### Task 8: Characterization, docs, and full validation

**Files:**
- Test: `Tests/Editor/Semantics/Poiyomi/PoiyomiBaseColorAlphaTests.cs` (class `PoiyomiAlphaTests`)
- Modify: `README.md`, `Packages/com.alrauna.amuse/README.md` (locked-material refusal sentence, kept identical), `Packages/com.alrauna.amuse/CHANGELOG.md` (`[Unreleased]`), `docs/superpowers/investigations/2026-10-23-locked-reconstruction-fidelity-investigation.md` (an amendment log line stating that this spec answers its open questions and its vendor-free check options)

- [ ] **Step 1: Write** `AlphaResultDoesNotChangeWithColorRgb` (A11.6). Expected on first run: PASS. Record it as characterization.
- [ ] **Step 2: Update docs** in simple English. The changelog states: locked materials are rebuilt from the generated shader, edits after the lock that change what the locked material renders are reproduced or refused, and locked materials with a property block or with animations that the locked shader reads differently are refused. It also states the coverage cost of A10.
- [ ] **Step 3: Run** the full two-assembly suite. Expected: 0 failed. Record counts.
- [ ] **Step 4: Run** the identifier sweep over changed files: an at sign joined to hexadecimal, drive-letter paths, home-directory paths, four-digit ports, and private names. Expected: no hits.
- [ ] **Step 5: Commit** `docs: record locked source alignment`.
