# Poiyomi per-shader attestation design

Status: 2026-10-23, third revision. The second revision integrated four parallel adversarial reviews of the first draft (accuracy, safety, minimalism, feasibility) and three follow-up measurements. Those reviews reported two CRITICAL, seventeen MAJOR, and a set of MINOR findings. The third revision integrates two adversarial plan reviews: K9.1 limits the float texture check to textures with scale-offset evidence, and test duty 7 names a falsifier that catches an ignored tag. All findings are integrated. Branch: `feat/poi-per-shader-attestation`. Basis: `docs/superpowers/investigations/2026-10-23-poiyomi-bundled-9-3-source-identity-investigation.md` (the investigation) and its second verification of 2026-10-23. Product and test paths are relative to `Packages/com.alrauna.amuse/` unless prefixed `research:`, which means `Packages/com.alrauna.amuse.research/`.

Privacy note: this record covers public vendor packages and AMUSE's own public constants. It contains no private data. A private characterization avatar motivated the work. It appears by role only. Vendor packages were measured in a scratch folder outside the repository. Shader asset GUIDs appear by role only, never by value.

Decision-ID note: earlier designs burn the letters A, B, C, D, E, F, G, H, L, M, P, R, S, T, V, and X in code or in specs. This document uses the K namespace. No K decision exists in code or docs as of 2026-10-23.

## Problem

Poiyomi Toon 10.x packages ship the old 9.3 shader generation in the folder `_PoiyomiShaders/Shaders/9.3/Toon/`. Its display names carry an `Old Versions/9.3` segment, for example `.poiyomi/Old Versions/9.3/Poiyomi Toon`. Many avatars keep their materials on this bundled 9.3 shader after a package update. AMUSE refuses all of these materials as of 2026-10-23. The name routing in `ClassifyShaderName` (`Editor/Semantics/UnityMaterialSemantics.cs:820-943`, Poiyomi branches at `:825-843`) does not know the bundled names. The user sees "The shader '.poiyomi/Old Versions/9.3/Poiyomi Toon' is not one AMUSE supports."

The attestation also binds identity to the package. It requires package name `com.poiyomi.toon` and the exact package version `9.3.64` (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:146-169`). A 10.x package can never pass this gate, even with correct bytes.

The reviews also found a pre-existing false-positive risk. Thry renames animated properties. Clips then animate names such as `material._Color_<suffix>`. AMUSE classifies such a curve as irrelevant (`Editor/Host/AnimationBindingResolution.cs:291-310`), so an animated alpha can be proven constant. Admitting the bundled shader widens exposure to this risk. K9 closes it.

## Owner decisions, 2026-10-23

1. True Poiyomi 10.0 support is deferred. The 10.0 shaders keep their refusal.
2. The goal is support for the 9.3 shader that 10.0 and later packages bundle. The support must be easy to maintain.
3. Attestation is by shader file, not by package.
4. This branch adds a fail-closed guard for rename-suffixed animation bindings (K9). The gaps in locked-material reconstruction get their own follow-up branch.

## Verified facts

These facts were measured or read on 2026-10-23. Independent verifiers checked each one against official packages from the Poiyomi VPM index. Every downloaded zip matched its index hash.

1. All eight published 10.x releases, 10.0.20 through 10.0.27, ship the same bundled 9.3 folder byte for byte. That is 14 files plus one folder meta. Path, names, GUIDs, and digests never changed.
2. The bundled main shader has canonical digest `c812d4d39ce3352b6198fdfcb5de87eb07678739a4bce7e4afd18cea28be19f4`. The bundled Two Pass shader has canonical digest `4fd1d8c1e35bd4e81a21c1dd6545ceb022542b67aa9996cb8341da7f3b64b65f`. The files are LF-only and carry no BOM. The production recipe in `Editor/Semantics/NormalizedSourceHash.cs` reproduces both values. The same recipe reproduces the pinned 9.3.64 digests from the 9.3.64 release.
3. The bundled main and Two Pass shaders carry the same asset GUIDs as the pinned 9.3.64 main and Two Pass shaders. One GUID covers two byte streams.
4. Each bundled shader is self-contained. The only active includes are Unity built-in includes. A single-file digest covers the whole semantic surface.
5. The bundled files differ from 9.3.64 by 98 normalized lines each: 44 removed and 54 added. The text from `SubShader` onward is identical, including the `shader_feature` and `multi_compile` sets. The edits are the declaration line, the master label (`Poiyomi 9.3.67`), one renamed UI property (`_LTCGI_Attribution` to `LTCGI_Attribution`, Float to Int), four added UI properties (`_BackfaceReminder`, `_EmissionFallbackHelp`, `_FaceDiscardHeader`, `_UDIMDiscardHeader`), a UDIM drawer rework, and helpbox text and attribute rewrites.
6. 44 declarations differ per shader: 38 changed, 5 added, 1 removed. The changed attributes are only `Helpbox`, `ThryDescription`, `ThryMultiFloatButtons`, `ThryMultiFloatHeaderDrawer`, `ThryHideInInspector`, `DoNotAnimate`, and `HideInInspector`. No changed, added, or removed declaration carries a keyword-binding drawer or `on_value_actions` metadata. `_EnableUDIMDiscardOptions` keeps its declaration and its `ThryToggle(POI_UDIMDISCARD)` binding. The 32 UDIM grid properties changed only non-keyword attributes and display text.
7. No property present in both sources changed its type or default. None of the five changed property names occurs in the product.
8. The full declarations of `_Mode` and `_ModeTwoPass` are byte-identical after normalization. This includes the multi-line ThryWideEnum display string with the preset `on_value_actions` metadata. The opaque-conversion recipe derives from that metadata (`Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:132-138`).
9. The 23 conversion-recipe properties and `_ShaderOptimizerEnabled` keep their full declarations, attributes included.
10. Both bundled shaders declare the 4-property alpha schema (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:126-132`) and the 10-property full-material schema (`Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:53-65`).
11. The 10.0 shaders use the exact names AMUSE pins as of 2026-10-23, `.poiyomi/Poiyomi Toon` and `.poiyomi/Poiyomi Toon Two Pass`. They carry different GUIDs. They pass the name gate and refuse at the GUID gate as `MissingSourceEvidence`. This is a code-read inference, not a build observation.
12. The Poiyomi identity diagnostic code is not user-visible. `AnalyzeAlphaMaterialCore` discards it and reports `AlphaUnknownKind.UnattestedShader` (`Editor/Semantics/UnityMaterialSemantics.cs:1198-1223`). The code reaches only the conversion refusal detail `SourceIdentity.<code>` (`Editor/Build/AlphaSeparationMaterialConverter.cs:216-218`) and the test-visible `PoiyomiSemanticResult.Diagnostics`.
13. Poiyomi 10.x packages depend on `com.poiyomi.thryeditor` 2.74.0 or later. The 9.3.64 package embeds Thry 2.69.3. The thryeditor lock writes a new tag, `thry_locked_rename_suffix`, and its unlock reads that tag first. AMUSE reconstruction does not read it (`Editor/Build/LockedMaterialReconstruction.cs:178-179`). The other lock-format facts that reconstruction uses match between the two Thry copies.
14. Thry writes animation keyframes for a rename-marked property under the suffixed name, for example `material._Color_<suffix>`. Neither Thry copy rewrites clips at lock or unlock. AMUSE resolves such a binding as `Irrelevant`: no generated-property split matches, and `CouldAddressAny` accepts only `.` or `[` after a requested name (`Editor/Host/AnimationBindingResolution.cs:155-167, 291-310`). The proof then treats the plain property as constant. No test animates a suffixed alpha input through the proof.

## Goals

- Unlocked materials on the bundled 9.3 main and Two Pass shaders, inside any 10.x or later package, get the same proof and conversion as materials on the 9.3.64 release.
- A future package that ships the same bundled bytes needs no code change.
- A future package that changes the bundled bytes fails closed until a maintainer measures and admits one new row.
- Identity is the shader file: name, GUID, and canonical digest.
- A rename-suffixed binding of a proof-relevant property never proves anything.

## Non-goals

- True Poiyomi 10.0 support, including its new alpha paths.
- Locked materials whose original is a bundled shader. K8 refuses them.
- Repairs to locked-material reconstruction. These are the follow-up branch.
- The other five bundled 9.3 variants (World, Grab Pass, Outline Early, Lil Fur, Lil Fur Two Pass). The 9.3.64 release support does not admit them either.
- Poiyomi 9.3.63 or any other 9.x release.
- A better refusal message for 10.0 materials.
- Changes to lilToon attestation. The verification of 2026-10-23 found no lilToon gain from per-file identity.

## Anti-goals

- Admitting any byte stream that no maintainer measured.
- A digest that skips the `Properties` block or any other part of the file.
- A version range or a consent prompt for Poiyomi.
- Mapping a suffixed binding back to its plain property. That needs the vendor suffix rule, which the follow-up branch owns. K9 refuses instead.

## Approaches

| Approach | What it does | Trade-off |
|---|---|---|
| Version-scoped rows | One row per package version that ships the bundle | Eight rows as of 2026-10-23 and one more per weekly release. Fails the maintenance goal. |
| Owner-approved version range | Admit a 10.x version band, as lilToon does | Version labels carry no proof for Poiyomi. The bytes still need a pin, so the range adds a gate without adding safety. |
| Per-file rows keyed by name, GUID, and digest | Admit the measured file wherever it is installed | Chosen. Matches the owner framing. One row per measured byte stream. Zero edits while the vendor keeps the bytes. |
| Keep the refusal | Record a coverage gap | Fails the goal. |

The owner chose per-file attestation on 2026-10-23.

## Decisions

### K1. Admitted source rows replace the scalar pins

1. `PoiyomiSourceAttestation` gains one internal row type, `PoiyomiAdmittedSource`, with five fields: `ShaderName`, `IsTwoPass`, `AssetGuid`, `NormalizedSourceHash`, and `LockedOriginalAdmitted`. It gains one accessor, `AdmittedSources()`, that returns the rows in a fixed order.
2. The rows as of 2026-10-23, in this order:

   | Row | Shader name | Two Pass | GUID | Digest | Locked original admitted |
   |---|---|---|---|---|---|
   | 9.3.64 main | `.poiyomi/Poiyomi Toon` | no | pinned main GUID constant | `31f2ff15615c5e2ac9b05fea08b6310731394d1b5a928b16048e7bde8f8b1755` | yes |
   | 9.3.64 Two Pass | `.poiyomi/Poiyomi Toon Two Pass` | yes | pinned Two Pass GUID constant | `b1d9ecd3072d21db97001dd23f88d089996b809e4039891a05f64d2ffcd4df67` | yes |
   | Bundled main | `.poiyomi/Old Versions/9.3/Poiyomi Toon` | no | pinned main GUID constant | `c812d4d39ce3352b6198fdfcb5de87eb07678739a4bce7e4afd18cea28be19f4` | no |
   | Bundled Two Pass | `.poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass` | yes | pinned Two Pass GUID constant | `4fd1d8c1e35bd4e81a21c1dd6545ceb022542b67aa9996cb8341da7f3b64b65f` | no |

3. Invariant: rows that share a name share `IsTwoPass`. A test asserts it.
4. One internal predicate does all row matching: `TryMatchAdmittedSource(string shaderName, string assetGuid, string normalizedSourceHash, out PoiyomiAdmittedSource row)`. It returns true only when one row equals all three values (ordinal).
5. The GUID constants `CanonicalShaderGuid` and `TwoPassCanonicalShaderGuid` stay. All four rows reference them. The name constants `PoiyomiToonShaderName` and `PoiyomiTwoPassShaderName` stay as the 9.3.64 row names. More than 20 test sites build stand-in shaders with them.
6. Removed: `PoiyomiPackageName`, `PoiyomiPackageVersion`, `CanonicalNormalizedSourceHash`, and `TwoPassCanonicalNormalizedSourceHash` in `PoiyomiSourceAttestation`. Their aliases at `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:29`, `:30`, and `:32` go too. The aliases at `:28`, `:31`, `:33`, and `:34` stay. The header comment at `:25-27` names the row set. This is a clean cutover. No alias or deprecated path stays.
7. The row block comment records the evidence for each row: the source release, the measurement date, and the admission rule from K5.

### K2. The identity proof is the shader file

1. `TryVerifyPoiyomiIdentity` (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:96-195`) checks, in this order:

   | Gate | Passes when | Refusal code |
   |---|---|---|
   | Name | At least one row has this exact name | `UnsupportedShader` |
   | Lock | The material is not locked | `UnsupportedShader` |
   | Readable source | The shader file was read | `MissingSourceEvidence` |
   | GUID | A row with this name has this GUID | `MissingSourceEvidence` |
   | Digest | `TryMatchAdmittedSource` matches name, GUID, and digest | `ModifiedShaderSource` |
   | Schema | The required schema is present | `ModifiedShaderSource` |

2. The package name gate and the package version gate are removed. The bytes are the whole semantic surface (verified fact 4). A package label adds no proof. A file with admitted bytes is admitted in a 10.x package, in a 9.3.64 package, and in a legacy `Assets/` install alike.
3. The digest gate binds name, GUID, and digest to one row. The binding does real work. The name comes from the captured material. The digest comes from a memo keyed by path and write time (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:71-87, 210-237`). Only the build passes clear that memo. The 9.3.64 file and the bundled file live at the same path. So a package update can pair a fresh name with a stale digest. The binding refuses that pair.
4. `PoiyomiSourceEvidence` loses `HasPackage`, `PackageName`, and `PackageVersion`. Both production builders stop calling `PackageInfo.FindForAssetPath`: `GatherSourceEvidence` (`:250-262`) and `LockedMaterialIdentity.PassesPinnedPoiyomiIdentity` (`Editor/Build/LockedMaterialIdentity.cs:289-303`). The two test builders drop the package arguments: the `Evidence` helper (`Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:185-207`) and `TwoPassIdentityVerifiesAgainstItsOwnPins` (`Tests/Editor/Semantics/Poiyomi/PoiyomiMultipassRuleTests.cs:130-150`).
5. `PoiyomiSemanticDiagnosticCode.UnsupportedVersion` loses its only producer. The member is removed. Three tests that test removed behavior are deleted: `Identity_LegacyInstallWithoutPackage_IsSupported` (`PoiyomiMaterialSemanticsTests.cs:339-347`), `Identity_WrongPackageName_IsMissingSourceEvidence` (`:409-416`), and `Identity_WrongPackageVersion_IsUnsupportedVersion` (`:418-423`).
6. An old 9.x file that shares the pinned GUID but carries other bytes now refuses as `ModifiedShaderSource` instead of `UnsupportedVersion`. Old files with another GUID refuse as `MissingSourceEvidence` before and after. The user sees `UnattestedShader` in every case (verified fact 12).

### K3. One name-routing predicate

1. `PoiyomiSourceAttestation` gains `TryClassifyShaderName(string shaderName, out bool isTwoPass)`. It returns true when some row has this exact name. `isTwoPass` is that name's flag (K1.3).
2. These sites call it:
   - `ClassifyShaderName` (`Editor/Semantics/UnityMaterialSemantics.cs:825-843`). A false `isTwoPass` routes to `CapturedAlphaMaterialFamily.Poiyomi`. A true one routes to `CapturedAlphaMaterialFamily.PoiyomiTwoPass`. The requests do not change.
   - The Two Pass choice in `AnalyzeBaseMaterial` (`Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:121-125`). This is a structural change. No stand-in reaches a supported `AnalyzeBaseMaterial` result, so the predicate's own test covers it.
   - The name gate in `TryVerifyPoiyomiIdentity`.
3. No new family member is added. Consent collection is family-keyed and skips both Poiyomi families (`Editor/Semantics/UnityMaterialSemantics.cs:575-580`).

### K4. The 9.3.64 proof model covers the bundled rows

1. The alpha interpreter is a hand model of the 9.3.64 source. Verified facts 5 through 10 show that every input the model reads is unchanged in the bundled source, keyword bindings included. The interpreter and its requests do not change.
2. The opaque-conversion recipe derives from the `_Mode` preset metadata. Verified facts 8 and 9 show that the metadata and all recipe declarations are identical. The recipe does not change.
3. These comments stop naming the scalar pins or the package gate. They name the row set and the K5 rule: the conversion class comment (`Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:122-145`), the comment at `:583-586`, the comment at `Editor/Semantics/UnityMaterialSemantics.cs:1121-1123`, and the `LockedMaterialIdentity` comment at `:256-264`. Comments that name 9.3.64 as the source of the proof model stay. They stay true.
4. One test class owns the row-set data pin. It asserts the exact four rows, the K1.3 invariant, and the hex format of every GUID and digest. It replaces `ShaderSemanticsArchitectureTests.cs:103-111` (the two removed hash constants) and `TwoPassPins_AreTheMeasuredValues` (`PoiyomiMultipassRuleTests.cs:157-168`), which are deleted. The conversion tripwire (`Tests/Editor/Semantics/Poiyomi/PoiyomiOpaqueConversionTests.cs:1437-1445`) asserts the row set and keeps its message that a new row needs a re-derivation or a recorded K5 argument first.

### K5. Admission rule for a future byte stream

When a vendor release changes the bundled bytes, materials on that release refuse. The refusal is fail-closed and needs no code. A maintainer admits a new stream only through this procedure:

1. Download the release from the official index and check its zip hash.
2. Compute the canonical digest of the changed shader file with the production recipe.
3. Diff the normalized file against the 9.3.64 release.
4. Admit a row only when all of these hold:
   - The active includes are still Unity built-ins only.
   - The text from `SubShader` onward is identical.
   - Every property present in both files keeps its type, its default, and its full attribute list. The exceptions are a named list of UI-only properties: helpboxes, headers, descriptions, and the UDIM grid buttons.
   - No added property carries a keyword-binding attribute (`Toggle`, `ToggleOff`, `ToggleUI`, `ThryToggle`, `ThryToggleUI`, `KeywordEnum`, `Enum` with keywords, `ThryWideEnum`, `ThryRGBAPacker`) or `on_value_actions` metadata.
   - The `_Mode` and `_ModeTwoPass` declarations are identical.
   - The properties the product reads keep their declarations. The read set is the union of the alpha, Two Pass, full-material, and conversion requests.
5. Otherwise the stream needs a new proof model, which is a new design.
6. Record the measurement in a dated investigation and cite it in the row comment.

This rule lives in the row block comment and in this record. No tool automates it as of 2026-10-23.

### K6. Research probe follows the rows

1. `research:Tests/Editor/Calibration/CensusVendorProbe.cs:72-103` finds the Poiyomi shader through `Shader.Find` with the 9.3.64 name. In a 10.x install that name finds the 10.0 shader. The probe then reports version 10.x, and the gate test `AnInstalledFamilyMatchesTheVersionAmuseAttests` fails.
2. The Poiyomi probe uses only rows with `IsTwoPass` false. The reachability tests set main-family properties only (`research:Tests/Editor/Calibration/VendorReachabilityTests.cs:216-222`).
3. A pure selection function takes the candidates as (name, shader, GUID, digest) in row order and matches each through the product `TryMatchAdmittedSource`. It returns the first matching candidate. When none matches, it returns the last candidate that resolved, so a 10.x install reports the bundled shader, not the 10.0 shader. When no row name resolves, it reports absence. A thin shell feeds it from `Shader.Find`, `AssetDatabase.GetAssetPath`, `AssetDatabase.TryGetGUIDAndLocalFileIdentifier`, and `PoiyomiSourceAttestation.TryReadNormalizedSourceHashCached`.
4. `CensusVendorPresence` gains `MatchesAdmittedSource`. For Poiyomi, `ExpectedPackageVersion` and `InstalledPackageVersion` are null. The Poiyomi branch of the gate test asserts `MatchesAdmittedSource` and names the reported shader in its message. The family label `com.poiyomi.toon` becomes a research-owned constant. The probe class comment states that the label is a family label, not a pin. The gate-case comment at `VendorReachabilityTests.cs:59-66` drops "pins 9.3.64 exactly".
5. The probe stays read-only by review. It calls only the read APIs in K6.3. `ResearchSourceApiBanTests` scans only `research:Editor`, so it does not guard the probe.

### K7. Documentation

1. `README.md` and `Packages/com.alrauna.amuse/README.md` (line 42 in both) state support for Poiyomi Toon 9.3.64 and for the 9.3 shader that Poiyomi 10.x packages bundle, main and Two Pass, unlocked. Locked bundled materials keep their original slots.
2. `docs/architecture/vision.md:55` says the same.
3. `Packages/com.alrauna.amuse/CHANGELOG.md` gains entries under `[Unreleased]` for K1 through K9.
4. The repository `AGENTS.md` overview line on supported sources says the same as the README.

### K8. Locked originals stay on the 9.3.64 rows

1. A locked material whose original resolves to a row with `LockedOriginalAdmitted` false refuses as `LockedPoiyomiOriginalShaderUnattested`. Reason: such locks come from thryeditor 2.74 or later (verified fact 13). Reconstruction does not read its suffix tag, and the restore verification checks identity facts, not values (`Editor/Build/TransientUnlockSwapIn.cs:285-348`). Re-admission needs the follow-up branch and a dated characterization.
2. The precheck applies the swap-in's name rule. The `OriginalShader` tag must be present and must equal the resolved shader name (`TransientUnlockSwapIn.cs:295-305`). Without this rule, a material locked on 9.3.64 in a project that later moved to 10.x resolves by GUID to the bundled shader. The dry run would then promise a recovery that the swap-in refuses. The orphan case, where both tags are absent, keeps its 2026-09-19 behavior (`LockedMaterialIdentity.cs:146-152`).
3. A pure internal seam carries both rules: `LockedMaterialIdentity.AttestsLockedOriginal(string originalShaderTag, in PoiyomiSourceEvidence resolved)`. It returns true only when the tag equals `resolved.ShaderName`, `TryVerifyPoiyomiIdentity` passes, and the matched row has `LockedOriginalAdmitted` true. `ResolvesToAttestedPoiyomi` gathers the evidence and calls it. `PreCheckRefusal` and `OriginalShaderAttested` pass the tag through.
4. The dry-run tag check `IsAttestedOriginalShader` (`Editor/DryRun/AlphaSeparationDryRun.cs:1226-1232`) asks the rows. It accepts a tag only when a row with that name has `LockedOriginalAdmitted` true. Its accepted set stays the two 9.3.64 names as of 2026-10-23. The site moves to the rows so that it cannot drift from K8.1.

### K9. Rename-suffixed bindings refuse

1. `CouldAddressAny` (`Editor/Host/AnimationBindingResolution.cs:291-310`) also answers true when the text starts with a relevant property name followed by `_`. For material float bindings, the relevant names also include each requested texture whose request includes scale-offset evidence, so a renamed texture's scale-offset binding such as `material._MainTex_<suffix>_ST.x` is caught. A texture requested without scale-offset evidence stays out of the float check, because the proof never reads its scale-offset. A material binding of that shape on an analyzed renderer then resolves as `UnrecognizedMaterialBinding`. The renderer refuses as `UnrecognizedAnimatedMaterialBinding`, through the existing path at `Editor/Analysis/RuntimeStateResolution.cs:203-218`.
2. The rule runs after generated-property resolution (`:155-162`). A binding that resolves as a requested property, a color or vector component, or a texture scale-offset component keeps its resolution. A requested name that itself extends another requested name with `_` stays resolved.
3. The rule also reaches object bindings through `IsUnrecognizedObjectMaterialBinding` (`:187-222`), because that method calls the same `CouldAddressAny`. The object check keeps all requested texture names, as it does as of 2026-10-23.
4. The rule is shader-independent. It covers lilToon requests too. The over-refusal surface was measured on 2026-10-23 for both frontends (section "Over-refusal surface").
5. The refusal is coverage-only. It never proves anything. Mapping the suffix back to its plain property is the follow-up branch.
6. Existing tests that animate suffixed names through a renderer with a relevant property may change outcome. The builder lists each one and decides with evidence whether its expectation was a valid contract or a product of the defect. A test whose expectation relied on the defect changes with the behavior. A valid test is never weakened.

## Over-refusal surface

Measured on 2026-10-23 from official packages. The scan listed every declared property that extends a requested name with `_` and is not requested itself.

| Source | Requested names | Pairs |
|---|---|---|
| Poiyomi 9.3.64 main and Two Pass | 93, plus the three texture `_ST` names | none |
| Poiyomi 10.x bundled main and Two Pass | same | none |
| lilToon 2.4.1 regular, cutout, transparent | 12 to 71 | none |
| lilToon 2.4.1 Multi and Multi outline | 128 | `_EmissionBlendMask` to `_EmissionBlendMask_ScrollRotate` (Vector), `_Emission2ndBlendMask` to `_Emission2ndBlendMask_ScrollRotate` (Vector) |

The two Multi pairs extend texture names that the Multi gate requests for assignment only, without scale-offset evidence (`Editor/Semantics/LilToon/LilToonMultiModeGate.cs:177-181`). Under K9.1 the float check leaves those texture names out, so K9 does not refuse those pairs. Unity also generates `_TexelSize` and `_HDR` names for every texture. A binding to one of those on a texture requested with scale-offset evidence now refuses. That loss is accepted as coverage-only. The texture `_ST` component bindings keep their resolution, because generated-property resolution runs first (K9.2).

## Test duties

The builder writes RED tests first. A test that passes on its first run is recorded as characterization. Each falsifier carries a `--- Falsifier N: ... ---` marker that names the wrong implementation it catches.

1. RED: the bundled main and the bundled Two Pass evidence attest through `TryVerifyPoiyomiIdentity`. Built through the `Evidence` helper.
2. RED falsifier: a bundled name with the 9.3.64 digest refuses as `ModifiedShaderSource`. It catches a digest lookup that ignores the name. Characterization: a 9.3.64 name with the bundled digest refuses the same way.
3. RED falsifier: a bundled main name with the Two Pass GUID refuses as `MissingSourceEvidence`.
4. The 10.0 deferral is pinned by the existing `Identity_WrongGuid_IsMissingSourceEvidence`. No new test.
5. RED: `TryClassifyShaderName` answers true with the right flag for each bundled name. `ClassifyShaderName`, reached through `TrySelectAlphaMaterialRequests`, routes each bundled name to its family. Characterization: an `Old Versions/9.0` name and a bundled World name stay unsupported.
6. Characterization: the row-set pin and the K1.3 invariant (K4.4).
7. RED: `AttestsLockedOriginal` accepts the 9.3.64 main row with a matching tag. Falsifier: it refuses a bundled row with a matching tag (an implementation that reuses `TryVerifyPoiyomiIdentity` alone admits it once the bundled rows attest). Falsifier: it refuses resolved 9.3.64 main evidence whose tag names another admitted shader, such as the 9.3.64 Two Pass name (an implementation that ignores the tag admits it). It refuses a missing or empty tag with resolved 9.3.64 main evidence.
8. Characterization: `IsSlotDeferred` does not defer a locked material whose tag names a bundled shader.
9. RED: the research selection function picks the matching bundled candidate over an unmatched first candidate. It returns the last resolved candidate when none matches. It never receives a Two Pass row.
10. RED falsifier: a float binding `material._Color_<suffix>.a` on an analyzed renderer refuses as `UnrecognizedAnimatedMaterialBinding`. It catches the `Irrelevant` classification. A scalar `material._Cutoff_<suffix>`, a float binding `material._MainTex_<suffix>_ST.x`, and an object binding `material._MainTex_<suffix>` refuse the same way. Characterization: `material._MainTex_ST.x` still resolves as a scale-offset component.

## What is left to the builder

- Exact test file placement, following the one-test-class-per-type rule.
- Identity duties use the `Evidence` helper. Name-routing duties use stand-in shaders through `NewMaterial`. K8 and K6 duties use the pure seams in K8.3 and K6.3.
- The list in K9.6.

## Risks

1. The vendor can change the bundled bytes in any release under the same GUID. K5 handles this. Materials fail closed in the meantime.
2. Locked bundled materials refuse (K8). Avatars that build with locked materials gain less until the follow-up branch.
3. The two GUIDs now cover two admitted byte streams each. The digest gate keeps them apart.
4. K9 refuses some renderers that AMUSE separated before. That loss is the cost of closing a false positive.

## Recorded findings for the follow-up branch

These pre-existing defects in locked-material reconstruction were found on 2026-10-23. They affect the 9.3.64 locked path. This branch does not fix them.

1. `CleanNameSuffix` (`Editor/Build/LockedMaterialReconstruction.cs:550-573`) writes an underscore as `5F`. The vendor cleaner keeps the underscore. A material name with an underscore gets a suffix the lock never used. Nothing moves, and the values roll back to lock time. The restore verification still passes.
2. Reconstruction does not restore the render queue, `RenderType`, or stripped textures. Both vendor unlocks restore them.
3. Reconstruction moves duplicated properties that the vendor unlock leaves alone.
4. Reconstruction does not read `thry_locked_rename_suffix` (verified fact 13).

## Validation

1. Focused EditMode runs for each new test, observed RED and then GREEN.
2. The full `Alrauna.Amuse.Tests.Editor` and `Alrauna.Amuse.Research.Tests.Editor` assemblies in the dev editor instance.
3. A recorded count for each run. A filtered run with 0 tests is a failure.

Code comments that implement these decisions reference K1 through K9 and point to this document.
