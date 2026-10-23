# Poiyomi bundled 9.3 source identity investigation

Status as of 2026-10-23. Branch at investigation time: `feat/per-shader-attestation`, cut from `main` at `e8ce3d6`.

Privacy note: this investigation read product code in this repository and public vendor packages. It read private avatar data only as aggregate counts. Private avatar, material, mesh, and texture names do not appear. The installed-copy probe ran in the characterization editor and appears by role only.

This document records facts. It decides nothing by itself. Evidence labels:

- `[SOURCE]` A fact read in this repository at the base commit. The citation gives a repository-relative path and line numbers.
- `[MEASURED]` A fact computed from public vendor packages, or from the installed package copy in the characterization editor, on 2026-10-23. The recipe in section 2 reproduces the vendor values.
- `[INFERENCE]` A deduction from listed facts. The deduction is named.

Product paths are relative to `Packages/com.alrauna.amuse/` unless a path carries another root.

## 1. Purpose

A private characterization avatar failed to move triangles in a build on 2026-10-23. Its dominant material family sits on the old 9.3 shader that ships inside the Poiyomi Toon 10.0.25 package. AMUSE refused every one of those materials in that build. More than four fifths of that avatar's material slot uses sit on this family. The owner asked whether AMUSE can attest shader files directly, instead of requiring the installed package version to equal the pinned version. The owner asked for a comparison of the 9.3 sources inside the latest 9 package and the 10 package. The pins that define the attested source live at `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:48-61`. `[SOURCE]`

## 2. Method

Three parallel investigations ran on 2026-10-23.

| Investigation | Scope | Mode |
|---|---|---|
| Package comparison | Download official 9.3.63, 9.3.64, 10.0.25 zips, diff the bundled 9.3 trees | Read-only, scratch folder outside both projects |
| Attestation mechanics | Poiyomi gate order, canonical digest algorithm, lilToon precedent, pinning tests | Read-only repo analysis |
| Installed copy probe | Hash the 9.3 folder inside the installed package in the characterization editor | Read-only execute-code hashing |

Vendor downloads came from the official VPM index at `https://poiyomi.github.io/vpm/index.json` and the release assets it points to. The 9.3.64 and 10.0.25 zips matched the index hashes. The 9.3.63 zip was downloaded as a control and was not checked against the index. The official index lists no 9.x release of `com.poiyomi.toon` after 9.3.64. `[MEASURED]`

Canonical hash recipe, restated for reproduction. The decode step lives at the call site: `File.ReadAllText(assetPath, Encoding.UTF8)` at `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:84`. That call itself removes a UTF-8 BOM and detects UTF-16 and UTF-32 BOMs. The shared rule then drops one more leading U+FEFF character, replaces CRLF with LF, replaces lone CR with LF, encodes UTF-8 without BOM, and hashes with SHA-256 as lowercase hex (`Editor/Semantics/NormalizedSourceHash.cs:21-53`). None of the four compared shader files carries a BOM, so the two BOM steps change nothing here. `[SOURCE]`

## 3. Layout facts

`[MEASURED]` Both packages use the same layout. The bundled 9.3 shader set lives at `_PoiyomiShaders/Shaders/9.3/Toon/` with siblings `Shaders/10.0/` and `Shaders/Extras/`. The set holds 15 files: 7 monolithic `.shader` files of 2.7 to 4.2 megabytes, 7 `.shader.meta` files, and one folder meta. The string `Old Versions/9.3` is the internal display path of the shaders, for example `.poiyomi/Old Versions/9.3/Poiyomi Toon`. It is not a disk path.

`[MEASURED]` The installed package in the characterization editor matches the official 10.0.25 zip byte for byte. All 15 files in the 9.3 folder matched.

`[MEASURED]` The 9.3 shaders are self-contained. The only active include lines are Unity built-in includes. All vendor include markers are comments around inlined code. No include resolves outside the shader file into the package. A single-file digest therefore covers the whole semantic surface.

## 4. The bundled sources are not the attested release

`[MEASURED]` The canonical hashes of the real 9.3.64 shaders match the pinned constants exactly. This validates both the pins and the recipe. The bundled 10.0.25 copies fail those pins. The comparison ran line by line on the main shader and the Two Pass shader. Bundled-copy canonical digests:

| Shader | Bundled canonical digest (full) | Matches pin |
|---|---|---|
| `.poiyomi/Old Versions/9.3/Poiyomi Toon` | `c812d4d39ce3352b6198fdfcb5de87eb07678739a4bce7e4afd18cea28be19f4` | No |
| `.poiyomi/Old Versions/9.3/Poiyomi Toon Two Pass` | `4fd1d8c1e35bd4e81a21c1dd6545ceb022542b67aa9996cb8341da7f3b64b65f` | No |

The bundled files are LF-only, so these values equal their raw SHA-256 digests. The installed-copy probe produced the same values independently. The pinned values live at `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:48-61`. The 9.3.64 release reproduces them.

`[MEASURED]` The bundled copies carry real content edits, not line-ending noise. The main shader and the Two Pass shader each differ by 98 normalized lines: 44 removed and 54 added. Every differing line sits in the `Properties` block. The text from `SubShader` onward is identical. The edits are:

1. The declaration line renamed to the `Old Versions/9.3` display path.
2. The master label changed from `Poiyomi 9.3.64` to `Poiyomi 9.3.67`. No release with that label exists in the official index.
3. One property renamed: `_LTCGI_Attribution` to `LTCGI_Attribution`.
4. Four properties added: `_BackfaceReminder`, `_EmissionFallbackHelp`, `_FaceDiscardHeader`, `_UDIMDiscardHeader`.
5. A UDIM tile-discard drawer rework, one inserted backface reminder helpbox, an inserted `[sRGBWarning]` on `_EmissionMask`, and attribute changes on several helpboxes.

No declared property type or default changed. The `_EnableUDIMDiscardOptions` declaration is identical across the two sources.

`[MEASURED]` The bundled copies also differ from 9.3.63. They are their own source generation, not a copy of either release.

`[MEASURED]` All 7 shader assets carry identical GUIDs in the 9.3.64 release and in the 10.0.25 bundle. One asset identity covers two different byte streams. This is a vendor packaging fact. It is not a claim about product behavior.

## 5. What the product does with these materials

`[SOURCE]` AMUSE routes a shader to a family by exact name first. `ClassifyShaderName` compares against the pinned display names and returns `CapturedAlphaMaterialFamily.Unsupported` for every other name (`Editor/Semantics/UnityMaterialSemantics.cs:822-942`).

`[SOURCE]` Inside the Poiyomi attestation, the exact-name gate is also first. A shader whose name matches neither pinned name refuses as `UnsupportedShader` before any other gate runs (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:100-114`).

`[INFERENCE]` The bundled materials carry the display name with the `Old Versions/9.3` segment. The product therefore refuses them at the name gate. The product never compares their GUID, their package version, or their source hash. No pinned value is ever consulted for these materials.

`[SOURCE]` Tests pin this routing shape. A shader named with an `Old Versions/9.0` segment classifies as an unsupported family and produces zero consent subjects (`Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:1149-1186`).

`[SOURCE]` For a shader that does carry an attested name, the gate order inside `TryVerifyPoiyomiIdentity` is: name, lock state, readable source, asset GUID, package name and exact version inside the `HasPackage` block (`:146-169`, version check at `:159-168`), canonical source hash (`:171-183`), required schema (`:185-191`). A version mismatch returns `UnsupportedVersion` before the hash gate runs.

`[SOURCE]` The version gate runs only when the shader lives inside a package. A legacy install under `Assets/` reports no package. That path skips the version gate, so GUID plus hash carry the whole proof (`Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:339-349`).

`[MEASURED]` The bundled copies declare all four properties of the required schema (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:126-132`).

The refusal code that the private build on 2026-10-23 showed for these slots was not recorded. The console renders refusal prose, not refusal codes. The routing analysis above predicts an unsupported-family refusal, not a version refusal.

## 6. Precedent: lilToon

`[SOURCE]` lilToon separates two mechanisms. `AdmittedPackageVersions` maps exact versions to include-tree digests (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:432-448`). Versions 2.3.1, 2.3.2, and 2.3.3 have three rows with the same digest. Separately, `ApprovedVersionFloor` and `ApprovedVersionUpperBound` bound an approved range (`:374-375`, check at `:481-501`). The range bounds approval, not the admitted rows.

`[SOURCE]` An owner decision of 2026-10-19 defines approval. The build path skips the identity checks for an approved member with unattested bytes. The attestation itself still fails closed on changed bytes (`:366-373`).

`[SOURCE]` An admitted or approved version whose bytes do not match refuses as `ModifiedShaderSource`, never as `UnsupportedVersion` (`:2390-2401`, tests at `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1866-1936`). A version outside both the admitted rows and the approved range returns `UnsupportedVersion` early (`:2347-2356`). The Multi path identifies the version from the tree digest alone, so bytes can carry identity (`:2807-2818`).

## 7. Feasibility of per-shader attestation for the bundled generation

`[INFERENCE]` Name aliases are the first required change. Every routing site must learn the bundled display names before any digest row can matter. The sites are the family classifier (`Editor/Semantics/UnityMaterialSemantics.cs:822-942`), the attestation name gate and its Two Pass choice (`Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:100-114`), the Two Pass choice in the semantics facade (`Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:121-125`), the dry-run original-shader tag (`Editor/DryRun/AlphaSeparationDryRun.cs:1225-1232`), and the unlock-window identity resolution (`Editor/Build/LockedMaterialIdentity.cs:270-307`).

`[INFERENCE]` After routing, three admitted-set shapes exist. They follow lilToon precedent to different degrees.

1. Version-scoped rows. Admitted rows keyed by exact package version carry the bundled names and measured digests. A version with no row keeps the early `UnsupportedVersion`. This is the nearest match to the admitted-rows precedent.
2. Owner-approved range. An approved band of 10.x versions with consent semantics, following the 2026-10-19 owner decision. The risk is wider: analysis runs on unmeasured future releases inside the band until they are characterized.
3. Bytes-keyed digest set. A digest set independent of the version label, following the Multi shape. Any 10.x release that ships identical bundled bytes is admitted without a per-release row. Whether 10.0.20 through 10.0.27 ship the same bytes is unmeasured, so the maintenance claim is conditional.

`[INFERENCE]` The safety argument rests on two measured parts, not on the diff alone. First, the shader text from `SubShader` onward is identical, and AMUSE never parses source text for semantics. The Poiyomi proof is a hand model of the pinned source, and the pinned hash ties that model to the bytes. Second, the declared property set differs only by five names, and no property that AMUSE requests changed in name, type, or default. The capture reads properties through `material.HasProperty` and `shader.FindPropertyIndex` (`Editor/Host/UnityMaterialEvidenceCapture.cs:250-266`). The reworked UDIM drawer sits beside `_EnableUDIMDiscardOptions`, which is an input to an alpha coverage gate (`Editor/Semantics/Poiyomi/PoiyomiAlphaInterpreter.cs:38-45`). That declaration is identical across the two sources. None of the five changed names occurs anywhere in the product.

`[INFERENCE]` The cost is recurring characterization for shapes 1 and 3. Poiyomi can rewrite the bundled copy in any 10.x release under the same GUID. Each new byte stream needs a measured row or a new digest. Unmeasured releases fail closed in the meantime.

`[INFERENCE]` The fourth option is to keep the refusal. That refusal is honest as of 2026-10-23: the installed bytes are not the source AMUSE analyzed. Keeping it records a coverage gap for the dominant Poiyomi-10-onwards configuration rather than a defect.

`[INFERENCE]` A fifth shape, comparing only the body and ignoring the Properties block, is not recommended. It weakens the pinned-digest discipline and adds a normalization rule that fails closed only on paper.

## 8. Change surface if adopted

`[INFERENCE]` These rows are a deduction from the routing analysis. Adoption stays a decision for the owner. The lilToon order tests are the nearest precedent for the target semantics.

| Surface | File and lines |
|---|---|
| Name routing sites (all five) | Section 7, first paragraph |
| Admitted rows and gate order | `Editor/Semantics/Poiyomi/PoiyomiSourceAttestation.cs:96-194` |
| Shared hash rule | `Editor/Semantics/NormalizedSourceHash.cs` reuses as-is |
| Family routing tests | `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:1149-1186` |
| Two Pass routing tests | `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:352-379` |
| Build refusal detail | `Editor/Build/AlphaSeparationMaterialConverter.cs:211-220` |
| Report strings | `Editor/Build/AmuseReportStrings.cs:118-119`, `:311-319` |
| Changelog | `CHANGELOG.md` per repo discipline |

The test that pins the current version refusal changes legitimately with the behavior: `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:418-423`. The build-path refusal detail contract starts with `SourceIdentity.` and stays (`Tests/Editor/Build/AlphaSeparationPreparationTests.cs:12789-12790`).

## 9. What this investigation does not cover

- The comparison was static. No build ran the alpha proof over the bundled sources. Body-equality by diff stands in for compiled equivalence.
- Only releases 9.3.63, 9.3.64, and 10.0.25 were measured. The other published 10.x releases were not. Whether they ship the same bundled bytes is unknown.
- The 9.3.63 GUIDs were not compared, and the 9.3.63 zip was not checked against the index hash.
- The refusal code the private build showed was not recorded.
- Exact private material counts appear as ranges only.

## 10. Open questions for the owner

1. Which admitted-set shape, if any: version-scoped rows, an owner-approved range, a bytes-keyed digest set, or continued refusal as a recorded coverage gap?
2. If adopting: is attesting an unpublished vendor snapshot acceptable as policy, given the vendor can change it under the same GUID in any release?
3. If adopting: which 10.x releases should the first characterization cover?

## Second verification, 2026-10-23

Five independent verifiers re-checked this record on 2026-10-23. They re-read every citation and re-measured the official packages. No conclusion of this record changed. The corrections below supersede the sentences they name.

1. Section 3. The `9.3/Toon/` folder holds 14 files: 7 shader files and 7 shader meta files. The fifteenth file is the folder meta `Shaders/9.3/Toon.meta`, which sits in the parent folder.
2. Section 3. The sibling folder `Shaders/10.0/` and the size range of 2.7 to 4.2 megabytes describe the 10.x bundle only. The 9.3.x packages have no `Shaders/10.0/` folder. Their largest shader is 4.30 megabytes.
3. Section 4. Every differing line sits in the `Properties` block except line 1, the `Shader` declaration. Edit 1 names that line.
4. Section 4, edit 3. The rename of `_LTCGI_Attribution` to `LTCGI_Attribution` also changes the type from Float to Int and the drawer from `[Helpbox(1)]` to `[ThryDescription]`.
5. Section 4, edit 5. The `[sRGBWarning]` attribute on `_EmissionMask` is not an edit. It is present in 9.3.63 and in 9.3.64.
6. Section 4. No property present in both sources changed its type or default. The renamed LTCGI property is not present in both sources under one name.
7. Section 2 and section 9. The 9.3.63 zip matches its index hash. The 9.3.63 GUIDs equal the 9.3.64 GUIDs. 9.3.63 and 9.3.64 differ only in the master label line.
8. Section 9. All eight published 10.x releases, 10.0.20 through 10.0.27, ship the same bundled 9.3 folder byte for byte.
9. Section 7 and section 8. Line ranges drift. The dry-run site is `Editor/DryRun/AlphaSeparationDryRun.cs:1226-1233`. The locked identity check is `Editor/Build/LockedMaterialIdentity.cs:265-307`, with GUID resolution at `:238-263`. The legacy routing tests are `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs:1148-1182`.
10. Section 6. lilToon emits `ModifiedShaderSource` in `TryMatchDigest` at `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2419-2444`, not at `:2390-2401`. The lilToon tests span `Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1866-1937`.
11. Section 8. The change surface also includes `Tests/Editor/Build/LockedMaterialIdentityTests.cs:136`, `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:837-859`, and the 10-property full-material schema at `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:53-65`. Both bundled shaders declare all ten properties.
12. Section 5. The predicted console text for a bundled material is the slot refusal `AdmittedMaterialSemanticsUnknown` with the sentence "The shader '.poiyomi/Old Versions/9.3/Poiyomi Toon' is not one AMUSE supports." This is traced through the code, not observed in a build.

## Verification record

2026-10-23. An adversarial review re-opened every cited file and range, reproduced the measured values from the downloaded packages, and reported 17 defects. This amendment applies all of them. The review found no identifier-rule violations in this record.

## Amendment log

- 2026-10-23: amended after an adversarial review. The refusal narrative moved from the version gate to exact-name family routing. The safety argument was recast around the property capture and the hash-tied proof model. The change surface gained the five name-routing sites and their tests. The lilToon precedent was corrected to separate admitted rows from the approved range. The recipe gained its decode-site citation and BOM caveat. Citations were corrected. Open questions and this verification record were added.
- 2026-10-23: second verification. Twelve corrections recorded in section "Second verification". The design `docs/superpowers/specs/2026-10-23-poiyomi-per-shader-attestation-design.md` uses the verified facts.
