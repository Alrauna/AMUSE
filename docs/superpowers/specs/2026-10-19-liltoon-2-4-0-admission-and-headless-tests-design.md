# lilToon 2.4.0 admission and headless test runs design

Status as of 2026-10-19. Branch: `update-for-current-packages`. This version integrates six parallel adversarial reviews (accuracy, safety, security, architecture, ponytail, latent bugs) and two owner decisions.

Evidence base: `docs/superpowers/investigations/2026-10-19-liltoon-2-4-0-attestation-and-test-stall-investigation.md` (referenced as "the investigation").

## Owner decisions recorded 2026-10-19

1. Multi admission is version-aware (Option A). Multi keeps 2.3.4 attestation and gains 2.4.0 attestation. See A2.
2. The `dependencies` block of `Packages/vpm-manifest.json` aligns with the `locked` block. See M1.

## Goals

1. EditMode tests run and fail without ever blocking on a modal dialog.
2. lilToon 2.4.0 becomes an admitted version through measured data, with tests strengthened, never weakened.
3. The suite gains the five strengthening targets from the investigation (G1 through G5).

## Non-goals

1. No production consent behavior changes. The dialog keeps asking once per build. Decline stays a total no-op. Acceptance keeps the nearest-verified-version rules.
2. No Poiyomi attestation work.
3. No CI changes. No environment variables that gate product behavior.
4. No NDMF, VRChat SDK, AAO, or d4rkAvatarOptimizer package changes. M1 touches this project's own manifest, which is not a vendor package change.

## Decisions

### M1. Align the project manifest

Set the four `dependencies` entries in `Packages/vpm-manifest.json` to the locked values: lilToon 2.4.0, d4rkAvatarOptimizer 4.6.0, Avatar Optimizer 1.9.20, NDMF 1.14.8. Owner approved on 2026-10-19.

Rationale: the mismatch is the trigger condition for the VPM resolver's once-per-session dialog (`Packages/com.vrchat.core.vpm-resolver/Editor/Resolver/Resolver.cs:89`) and the revert hazard the investigation records in section 1. Alignment disarms both. Own commit, first in sequence.

### H1. Test-only presenter seam on the finish pass

Add to `AmusePlatformFinishPass`, next to the fallback chain it arms (co-location with the read it serves, matching the `ActiveBuildTargetForTests` topology at `Editor/Build/AmusePlatformFinishPlugin.cs:303-319`):

```csharp
internal static VersionConsentPresenter PresenterForTests = null;
```

The chain at `Editor/Build/AmusePlatformFinishPass.cs:321-325` becomes:

```csharp
consentPresenter ?? PresenterForTests ?? VersionConsentDialog.Present
```

Isolation is compile-time only, consistent with every internal seam in this package. It is not a security boundary. Reflection and unsigned-assembly name impersonation already reach all internals, so the seam grants no new capability. The fail-closed backstop is batch-mode refusal.

Lifecycle rule:

- The merged-consumption fixture installs a recording presenter in set-up: an internal sealed test class whose `Present` appends the subjects to `SubjectsSeen` and returns `false` always. Tests that exercise acceptance keep using the explicit presenter parameter of `Execute` (`Editor/Build/AmusePlatformFinishPass.cs:28-52`), as `AmusePlatformFinishPluginTests` does. `VersionConsentTests` pins the presenter contract through `ShouldProceed` directly.
- Tear-down restores null as the first statement, before `_filteringPin.Dispose()` and `_scope.TearDown()` (`Tests/Editor/Build/MergedConsumptionFixture.cs:129-140`). The restore must not depend on later teardown steps succeeding.
- Known limit, accepted: with an auto-decline default, a refusal-asserting test cannot distinguish an intended decline from a false attestation refusal. Unit-level coverage pins the distinction: the synthetic-subject test and the admitted-set enumeration test from T1.
- Residual, delegate-typed seam: the field type is the raw presenter delegate, so a future test could install a granting presenter. The decline-only recorder is the convention for every current user, and T3's re-ask pin plus the acceptance tests make a silent grant visible. The structural-typing alternative would put test machinery in production, which the architecture review rejected.

Pinned tests for the seam:

1. After fixture tear-down, `PresenterForTests` is null. This pins the restore discipline. It cannot observe a fresh process.
2. A synthetic subject run through the seam records the subject text and returns `false`.
3. The re-ask pin from T3: a granted build followed by a second build with the same subjects asks twice.

Rejected alternatives, with reasons, from the reviews: batch mode changes the harness, not the hazard, and the practice is interactive test runs. A fixture pre-flight attestation gate converts real decline-path executions into a skip, which fails the owner requirement that tests run and fail. Injection through `Execute`'s parameter is unreachable because the real plugin wires the one-argument `Execute(BuildContext)` and the AAO suite drives `AvatarProcessor.ProcessAvatar`.

### H2. No-modal source-scan test for the product package

A test in the product test assembly scans product `Editor` sources and fails when a banned literal appears outside the exemption.

1. Banned literals, a closed array in the `ResearchSourceApiBanTests` style (`Packages/com.alrauna.amuse.research/Tests/Editor/Collection/ResearchSourceApiBanTests.cs:38-58`): `EditorUtility.DisplayDialog`, `EditorUtility.DisplayDialogComplex`, `EditorUtility.DisplayCancelableProgressBar`.
2. Exemption: `Editor/Build/VersionConsentDialog.cs` exactly, with a doc comment stating it is the one sanctioned modal.
3. Anti-vacuous-pass guard: a mis-globbed or empty scan root fails the test rather than passing vacuously (precedent `:73-78`).
4. Scope: product package only. Vendor content is a mix of compiled DLLs out of a scan's reach and source we do not own. The resolver's source-visible session dialog is real but outside the product package's authority to ban. The risk register carries the residual.
5. The scan walker is duplicated from the research precedent on purpose. No shared home exists without a product-to-research test dependency or production test machinery. State this in the test's doc comment.

### H3. Temp-folder creation hygiene

The `AmuseTests_Temp N` residue under `Assets/` comes from the creation side, not deletion. The deleters already comply (`Tests/Editor/Shared/TestTransientScope.cs:52-56`, `Tests/Editor/Build/MergedConsumptionFixture.cs:136-139`). The Poiyomi memo fixtures create the folder with raw `Directory.CreateDirectory("Assets/AmuseTests_Temp")` and their finally blocks delete only an inner file, never the folder (`Tests/Editor/Semantics/Poiyomi/PoiyomiAttestationTests.cs:18,49`, `Tests/Editor/Semantics/Poiyomi/PoiyomiMaterialSemanticsTests.cs:238,244`). A later `AssetDatabase.CreateFolder` from the shared scope mints a suffixed duplicate (`AmuseTests_Temp 1`, `2`, and so on) that no teardown deletes. `INFERENCE` on the exact collision order. The raw-create sites, the suffixed folders, and the exact-path-only deletions are observed.

1. The memo fixtures create the temp folder through `TestTransientScope.EnsureTempFolder()` or `AssetDatabase.CreateFolder`, so the AssetDatabase sees what it will delete. The plan re-greps for all raw-create sites before the change.
2. Delete the eight existing suffixed folders together with their metas through `AssetDatabase.DeleteAsset`.
3. This is hygiene, not an owner ask. Own commit, separate from the admission work. Verification: after a full product-suite run, `Assets/` holds no `AmuseTests_Temp*` files.

### A1. Admit lilToon 2.4.0 as measured rows

One landing commit with A2 and A3, because the constants-derived row and the Multi gates rewire together.

The mechanical facts that shape the edit (all three latent-bug and safety findings converge here):

- The newest admitted row is the symbolic pair `(PackageVersion, IncludeTreeDigest)` at `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:404`, not a literal. `IncludeDigestForVersion` (`:422-436`) returns the first match, so a naive const flip pairs `2.4.0` with the 2.3.4 tree: a modified vendor package attests, a genuine one refuses. Implemented as insert-before instead, 2.3.4 silently leaves the set.
- The Multi gate compares evidence against the `PackageVersion` and `IncludeTreeDigest` constants directly (`:2216-2230`). A const flip alone rewires Multi to demand 2.4.0 and drops 2.3.4 Multi attestation with no failing test.

Edits, all in the same commit:

1. Materialize the 2.3.4 row as the literal `("2.3.4", "6e2dce6cb3073d5e04b569a14df8e0944c93ca408999fb42d7c717050c48fd46")`, replacing the symbolic pair.
2. Add the measured row `("2.4.0", <new tree digest>)`.
3. Move the `PackageVersion` constant to `"2.4.0"`.
4. Repoint the `IncludeTreeDigest` constant to the measured 2.4.0 tree digest.
5. Provenance comments in the established format (date, installed shapes, method, why the new digest differs from the 2.3.4 row: the VRC Light Volumes include rewrite), on every new or re-bound pin. Amend adjacent comments whose claims the flip falsifies: the inline comment on `PackageVersion` (`:361-362`), the row block comment (`:386-392`), and the Multi block (`:2314-2322`).
6. Mirror literals in the same commit: the audit literal in `PinnedDigests_AreTheTask0Measurements` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1893-1907`), the S11 TestCase rows (`:2391-2412`), the shared-tree fixture constants (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:190-195`), the helper default version literals (`:220-221`, `:245-246`, `:1924-1925`, `:2009-2010`, or make them symbolic off `PackageVersion`), the Multi assertion message text (`:350-360`), and the supported-version strings in `README.md` and the package `CHANGELOG.md`.

Measurement protocol, per the recorded procedure (investigation section 5):

- Measure from installed 2.4.0 packages, never from the upstream tag.
- Name the reproduction source per anchor: the 2.3.4 tree digest reproduces from the 2.3.4 tree or install. Regular profile digests reproduce from installed 2.4.0 shapes, where they are expected to reproduce exactly because the shader and pass assets are byte-identical in the tag trees.
- Use at least two installed shapes where obtainable, one digest per file across shapes.
- Confirm from installed bytes, before the flip: the `_lilToonVersion` stamp (expected `45f`), the asset and pass GUIDs (expected unchanged), and a byte comparison of the alpha-cited includes (`lil_common_frag_alpha.hlsl`, `lil_pass_forward_normal.hlsl`, `lil_pass_depthonly.hlsl`) between an installed 2.3.4 shape and an installed 2.4.0 shape. Record the comparison result in the new row's comment, in the style of the 2.3.x row block.

### A2. Version-aware Multi admission

The Multi path gains the same per-version binding the regular path has had since the admitted rows landed. Version, tree, and container digest bind as a unit, so no cross combination attests.

1. The Multi version check routes through `IsAdmittedPackageVersion` instead of `PackageVersion` const equality.
2. The Multi tree check routes through `TryVerifyIncludeTreeForVersion`, so each admitted version pairs with its own tree.
3. `MultiContainerProfiles` rows carry per-version digests: a `(name, guid)` row with a version-to-digest map, resolved by version-filtered lookup. The 2.3.4 outline digest stays for 2.3.4. The re-measured 2.4.0 outline digest lands for 2.4.0. The container digest for `lilToonMulti` is expected unchanged across versions and still requires confirmation.
4. Cross-combination refusal tests at the container verify level: a 2.4.0 version with the 2.3.4 outline digest refuses, a 2.3.4 version with the 2.4.0 outline digest refuses, and the correct pairings verify for both versions.
5. The `MultiEvidence` fixture gains both versions as data. The current single default (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:216-247`) stops encoding an impossible combo.
6. Multi digests re-measure from installed 2.4.0 shapes under the two-shape rule. Unmeasured means unadmitted, and unadmitted means all-Unknown, which is fail-closed.

Safety statement for the owner's "if safe" condition: no cross product of version, tree, and digest attests, because all three bind as a unit per version. The admitted set stays closed. Anything unmeasured refuses. The cross-combination refusal tests are acceptance criteria, not optional coverage.

### A3. Re-measure the canonical opaque tuple

Per the mandate at `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:26-30`, re-measure `CanonicalOpaqueTuple`, queue, and render type from the installed 2.4.0 package. Expected: exact reproduction of all eighteen writes, because `lts.shader` and `ltspass_opaque.shader` are byte-identical in the tag trees. On exact reproduction, update the provenance comment with the date and source shape and change nothing else. The no-op branch is the success branch, and it must leave a record.

### A4. Canonicalization vocabularies

Seven editor generator files changed upstream, including the `mainTexCheckWords` typo fix. The investigation requires vocabulary confirmation against the 2.4.0 generator output. Confirmation vehicles: the pinned `OfficialSettingIdentifiers` count of 109 (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1242,1250`), the closed-domain and uniqueness audit of the skip vocabulary, and the post-flip real gathers through the AAO suite. No literal changes are expected. A count drift or an unmapped token halts.

### T1. Boundary and admitted-set tests, on one enumeration surface

1. Enumeration surface: an internal read accessor on `LilToonSourceAttestation` exposing the admitted rows as an immutable snapshot, plus the Multi rows and the Multi gate's accepted version-tree-digest contract. Doctrine rejects reflection for first-party consumers (`Editor/AssemblyInfo.cs:6-9`). One accessor serves the product pin test and the research gate. `INFERENCE` avoided: no reflection probe duplicates the accessor.
2. `AdmittedVersionRows_AreTheMeasuredSet` pins every row: exact version strings, per-version tree pairings, no duplicate version strings, the Multi rows with their per-version digests. This is G5 for both admission surfaces.
3. Refusal TestCases for non-admitted forms: `"2.4.1"`, `"2.5.0"`, `"2.3.10"`, `"2.4"`, `"02.4.0"`. This is G1 with the upper edge pinned as data. The existing `2.3.5` witnesses stay (`LilToonAttestationTests.cs:1762`, `:2374`).
4. Upper-edge and mirror literals move in the same commit as the flip they encode. The full mirror list lives in A1 step 6.

### T2. Drift tests

Two deliberate, overlapping surfaces, both asserting `ModifiedShaderSource`:

1. Real-gather pairing: a stand-in tree matching an older admitted row while the package version reads newer verifies as `ModifiedShaderSource`, not `UnsupportedVersion`. Pins `TryVerifyIncludeTreeForVersion` (`:2122-2153`).
2. Per-profile digest mutation asserts the `ModifiedShaderSource` diagnostic, so a changed vendor file never reports as a version mismatch. This is G2.

### T3. Consent memory: source ban plus behavioral pin

1. Source ban within the consent-owned files `Editor/Build/VersionConsentDialog.cs` and `Editor/Build/AmusePlatformFinishPass.cs`: no `SessionState.Set*`, `EditorPrefs.Set*`, `PlayerPrefs.Set*`, or raw persistence APIs. Product `EditorPrefs` and `PlayerPrefs` uses were zero as of 2026-10-19, so those tokens may ban product-wide. `PlayModeBuildStore.cs` writes `SessionState` for build caching, including the declined-consent row, and is the named, justified carve-out outside the consent-owned scope. The ban list is closed literals. Scope and carve-out live in the test's doc comment.
2. Behavioral re-ask pin, the true form of V8: a build with subjects where the presenter grants, followed by a second build with the same subjects, must ask twice. The presenter records each ask. No store can satisfy this pin, which is the property the comment-only rule has lacked. This is G4.

### R1. Research gate measures the admitted set

1. Convert both research gates: `AnInstalledFamilyMatchesTheVersionAmuseAttests` to membership in the admitted set, and the `LilToonReachesProvenOpaqueThroughTheProductionPath` precondition (`Packages/com.alrauna.amuse.research/Tests/Editor/Calibration/VendorReachabilityTests.cs:393-397`) to membership as well, so acceptance criterion 5 holds for any admitted version.
2. A second assertion pins the probe constant to the newest-admitted constant, so the probe keeps moving with the pin. The probe already reads constants, never literals (`CensusVendorProbe.cs:57-59,86-88`).
3. The accessor from T1 serves both gates and returns an immutable snapshot. The closest precedent returns a live list that tests mutate (`Editor/Semantics/AdmittedProducerVersions.cs:48-73`). Do not copy that shape.
4. Prose updates in the gate doc comments that still say `2.3.4` (`:59`, `:372-374`).
5. Rationale: production admits the whole set. Membership measures AMUSE. Equality with the newest pin measures only the pin. Own commit after the flip.

## Risk register

| Risk | Mitigation |
|---|---|
| `PresenterForTests` leaks past a crashed teardown into an interactive build | Restore is the first teardown statement. A stale seam degrades to decline, which is fail-closed. A domain reload clears statics |
| Delegate-typed seam could carry a granting presenter | Convention plus pins. The recorder is the only current user. The re-ask pin and acceptance tests make a grant visible |
| Auto-decline masks the difference between intended and false refusals in refusal-asserting tests | Accepted in H1. Unit-level distinction is pinned by the synthetic-subject test and the admitted-set enumeration |
| Measurement surprises | Extended halt list below. Fail closed |
| The flip lands without mirrors or provenance | A1 steps 5 and 6 enumerate them. Each mirror is a plan task |
| Vendor dialogs stall a run | M1 removes the project-side trigger. Compiled vendor DLLs remain a documented residual that no source scan can reach |
| Next version bump friction | F9 mirror list is enumerated in A1 step 6. Each surface fails loudly |

## Halt points for the owner

1. Any regular-profile digest, GUID, or the `_lilToonVersion` stamp fails to confirm on installed 2.4.0 shapes.
2. The measured 2.4.0 tree digest equals an existing row's digest, which would indicate a measurement error, since the tag diff proves the tree changed.
3. A second installed shape is unobtainable and a digest would ship single-shape.
4. The opaque tuple differs from the pinned values on re-measurement.
5. A gather reports an unmapped vocabulary token, or the 109-count pin drifts.
6. Any change would alter production consent behavior beyond the H1 seam.

## Acceptance criteria

1. A full EditMode product and research run completes with zero modal blocks, whether or not the installed vendor version is attested.
2. After the flip: the 2.3.x rows and the 2.4.0 row verify with their own trees. An installed 2.4.0 real material passes attestation. The AAO real-build suite passes without consent subjects. 2.3.4 Multi and 2.4.0 Multi both verify. Every cross combination refuses.
3. The boundary tests, drift tests, admitted-set and Multi-row enumeration tests, source ban, and re-ask pin pass before the flip commit lands.
4. `LilToonOpaqueTargetTests` pass, with the provenance comment updated to record the 2.4.0 re-measurement.
5. Both research gates pass with any admitted version installed, and the probe-constant pin holds.
6. The supported-version strings in `README.md` and the package `CHANGELOG.md` say 2.3.0 through 2.4.0.
7. After a full product-suite run, `Assets/` holds no `AmuseTests_Temp*` files or folders.
8. Every halt point fires as specified and stops the run for the owner.
9. The manifest `dependencies` block matches the `locked` block.

## Decision IDs

Code comments implementing these decisions reference M1, H1, H2, H3, A1, A2, A3, A4, T1, T2, T3, R1, and point to this document.
