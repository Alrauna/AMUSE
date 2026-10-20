# lilToon 2.4.1 admission and preview cause note design

Status: 2026-10-20, second revision. This revision integrates five parallel adversarial reviews of the first draft (accuracy, safety and security, architectural purity, ponytail adherence, latent bugs). The reviews reported two CRITICAL, seven MAJOR, and ten MINOR findings. All are integrated. Branch: `feature/liltoon-2-4-1`. Basis: `docs/superpowers/investigations/2026-10-20-liltoon-2-4-1-admission-break-investigation.md` (the investigation). That record is the fact source for every claim repeated here. Product and test paths are relative to `Packages/com.alrauna.amuse/` unless prefixed `research:`.

Privacy note: this record covers a public upstream shader release and AMUSE's own pinned public constants. It contains no private data. The upstream tag trees live in a scratch directory outside the repository. They appear here as "the 2.4.0 tree" and "the 2.4.1 tree".

Decision-ID note: the letters A, D, H, M, R, S, T, and V are burned by earlier designs in the same files (A1-A5, D1-D7, T1-T3, M1, H1-H3, R1, S8-S11, V8). This document uses the B namespace. No earlier B decision exists as of 2026-10-20.

## Problem

lilToon 2.4.1 released 2026-10-20. It is the first in-range patch that changes a file inside the digested include tree. The D2 nearest-row rule pairs it with the 2.4.0 tree digest, so exact attestation refuses the source as modified. The effects, per the investigation section 3: the preview shows all-Unknown triangles for 2.4.1 materials, and the preview mislabels them. Its `RecordConsentFacts` walk emits the LockedMaterials note for them, whose sentence is false twice over: nothing is locked, and the build converts through the D3 approval arm without asking. Multi materials lose separation at build time. Regular materials lose their identity proof silently at build time.

AMUSE treats 2.4.1 as a supported version. The gap is one missing measured row, plus one mislabeled preview cause.

Owner decisions taken 2026-10-20:

1. The new row digest comes from the official 2.4.1 tag tree under the recorded include-tree recipe, licensed by an anchor reproduction. The pins flip before installed-shape measurement. Installed-shape confirmation is a recorded follow-up with a named work list. This decision accepts the ordering deviation from the opaque-target mandate, which asks for installed re-measurement before pins change (`Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:26-30`).
2. Pairing and fixture witnesses move to the static version `"2.4.2"`.
3. The preview gains a named-cause note for approved-but-unverified sources. The note replaces the false LockedMaterials note for those materials.

## Decisions

### B1. Admit 2.4.1 as the newest measured row

1. New digest value: `063f08cdc225ffc488d874c81860de376d5a9f9ad1c906ca84aa59f6193f0625`. Dated observation: computed on 2026-10-20 from the 2.4.1 tree's `Assets/lilToon/Shader/Includes` folder, 38 non-meta files, one run, recipe below.
2. License: the recipe is the production one. Per file, drop a leading UTF-8 BOM, convert CRLF and lone CR to LF, hash the UTF-8 bytes with SHA-256. Sort the `relativePath:hash` rows ordinally. Join with newlines. Hash again (`Editor/Semantics/NormalizedSourceHash.cs`, `Editor/Semantics/LilToon/LilToonSourceAttestation.cs:1765-1783`). The anchor reproduced the pinned 2.4.0 digest `549c3b552f8f001f2a2f6059acf4c8f0df6e9475f80b8d155784dabb68e7b670` from the 2.4.0 tree with the same recipe on 2026-10-20, 38 files. This is the recorded S11 practice for include-tree rows. The recipe never touches generated shader assets, which are the stale-upstream hazard the installed-shape rule guards.
3. `AdmittedPackageVersions` (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:413-427`) gains the 2.4.1 row. The 2.4.0 row becomes an explicit literal. The final row keeps the `(PackageVersion, IncludeTreeDigest)` shape, so it names 2.4.1.
4. `PackageVersion` moves to `"2.4.1"` (`:362`). `IncludeTreeDigest` moves to the new literal (`:393-394`). The comment above that constant (`:391-392`) is rewritten, because both its halves go false at the flip. The header comment (`:345`) says "2.3.0 through 2.4.1".
5. The row-block comment (`:396-412`) records the evidence: the four-file tag diff, the anchor reproduction with its date, and the outstanding installed-shape work. The block's closing ban sentence is restated scoped: never re-derive the generated shader and pass digests from the lilToon repository. Include-tree rows may cite the official tag tree when the anchor reproduces the previous row's digest. The outstanding follow-up names its full work list: include tree, shader, pass, extended family digests, Multi container digests, GUIDs, `_lilToonVersion` stamp, and the opaque-target tuple, measured from two installed 2.4.1 shapes per the mandate style at `Editor/Semantics/LilToon/LilToonOpaqueTarget.cs:26-30`.
6. No other pin moves at flip time. Shader and pass assets are byte-identical in the tag trees (investigation section 1). GUIDs live in `.meta` files and none changed. The `_lilToonVersion` stamp stays 45.

### B2. Multi rides the existing pairing for packaged installs, no new Multi row

The Multi row set stays exactly `2.3.4` and `2.4.0` (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2717-2730`).

1. Precedent: 2.3.0 through 2.3.3 carry no Multi rows either. Rows exist only for versions whose Multi container bytes differ. The 2.4.1 tag diff touches no Multi template, so its container bytes equal the 2.4.0 bytes.
2. Packaged installs: for version `2.4.1` the packaged Multi path D2-pairs the nearest Multi row at or below it, which is 2.4.0 (`:2819-2833`), and the new include-tree row satisfies the tree step. A 2.4.1 Multi material with the shipped bytes attests through the pairing.
3. Loose installs refuse, fail-closed, and this is stated and pinned: `TryIdentifyMultiVersionByTree` scans only the Multi rows' trees (`:2771-2790`), so the 2.4.1 tree identifies no row loosely. A loose 2.4.1 Multi install refuses. The existing test `MultiContainer_LooseInstall_TreeIdentifiesTheRow` (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:588-598`) pins a case with the `IncludeTreeDigest` constant, which becomes the 2.4.1 digest at the flip. That case moves to the literal 2.4.0 tree digest, and a new case pins that the 2.4.1 tree digest identifies no Multi row.
4. Cross-combination discipline stays intact: the tree binding is exact per row, so a 2.4.1 version with a foreign tree refuses (`Editor/Semantics/LilToon/LilToonSourceAttestation.cs:2546-2560`).
5. Test duty: new Multi cases pin the packaged 2.4.1 pairing verifies, and a 2.4.1 version with a foreign tree refuses, built on the `MultiEvidence` fixture (`Tests/Editor/Semantics/LilToonSourceAttestationTests.cs:246-289`). The Multi row audits (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1788-1795`, `:2257-2262`) stay unchanged, which is the point.

### B3. Witnesses move to the static version 2.4.2

Every site that uses `"2.4.1"` as the approved, non-row witness moves to `"2.4.2"`.

1. `Tests/Editor/Build/VerifiedLilToonTestSeams.cs:476` `ApprovedRangeVendorVersion` to `"2.4.2"`, with its comment updated. Nothing else in the fixture changes. Its stand-in digests mismatch every pin by construction. Drift marker: when upstream ships a real 2.4.2, or when a later admission makes 2.4.2 a row, the witness must be re-swept to a version outside the rows. Runtime derivation from the row list is the escape hatch. The comment at `:469-476` carries this obligation.
2. `ApprovedRangePatchWithTheNearestRowTreeVerifies` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1838-1858`): the `"2.4.1"` TestCase becomes `"2.4.2"`. Its comment now says 2.4.2 pairs the 2.4.1 row. `"2.4.9"` stays and pairs 2.4.1.
3. `ApprovedRangeVersionWithAForeignTreeReportsModifiedSource` (`:1853-1870`): version `"2.4.1"` becomes `"2.4.2"`. The comment names the 2.4.1 row as the pairing target. The foreign tree stays the 2.3.4 digest.
4. `Verify_AcceptsApprovedNonRowVersionWithTheNearestRowTree` and its doc comment: the test body moves automatically, because the evidence helper coalesces to the `IncludeTreeDigest` constant. The comment's "(2.4.0)" pairing target becomes the 2.4.1 row, or the comment goes version-generic.
5. The consent-core approved witnesses in `Tests/Editor/Semantics/UnityMaterialSemanticsTests.cs` (`:965`, `:992`, `:1089`, `:1112`) keep version `"2.4.1"`. Approval is range-keyed, not row-keyed (`:460-468`), so they stay valid. This is the documented reason they do not move.
6. `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:3332-3333` and `:3673-3674` consume the fixture constant and move with it. The explicit `"2.5.0"` boundary at `:3487-3488` stays.

### B4. Preview note for approved-but-unverified sources

The preview names the cause once per avatar. It uses the existing note machinery with zero new row shapes.

1. New `NoteKind` member `ApprovedUnverifiedSource` in `Editor/DryRun/MaterialTransformationRow.cs:42-49`.
2. New fixed verbatim sentence in `RowText.NoteSentence` (`:253-276`): "A shader version inside the supported range did not verify against its measured source. Its triangles stay unproven." The sentence is fixed because the cache-hit note rebuild keys on exact sentence equality (`Editor/DryRun/AlphaSeparationDryRun.cs:266-277`). The note never names a material or version.
3. Routing inside `RecordConsentFacts` (`Editor/DryRun/AlphaSeparationDryRun.cs:1216-1249`): a new first branch inside the loop. A sentinel record (`Family == Unsupported`) whose `CapturedVersionApproved` is true emits `AddNoteOnce(NoteKind.ApprovedUnverifiedSource)` and continues. The branch precedes the existing `Unsupported` fallback, so the false LockedMaterials note stops firing for these records. The streaming disclosure is not lost: the probe re-derives it independently at `:944-946`. Out-of-range and unsupported sources keep the sentinel and the consent-subject note they earn as of 2026-10-20. At most one note per avatar.
4. Data plumbing, the smallest carrier that survives record-less capturers:
   - `ClosedAlphaCaptureOutcome` (`Editor/Semantics/ClosedAlphaCaptureOutcome.cs:15-30`) gains one defaulted list, `UnattestedApprovedFlags`, an `IReadOnlyList<bool>`. Empty by default. When non-empty it mirrors `UnattestedOrdinals` one to one. It is a mirror, not a partition member. The partition doc stays as is, with one added sentence naming the mirror. The constructor's new parameter defaults to empty, so all existing construction sites stand: the production probe capturer (`Editor/DryRun/AlphaSeparationDryRun.cs:966-985`), the plugin test capturer, and the test seams, all of which fail ordinals without records.
   - The shared capture middle computes each flag at the single `failed.Add` site (`Editor/Semantics/UnityMaterialSemantics.cs:396-403`, the one append both entries route through; the strict entry at `:288` delegates to the transferred entry). The flag is the D3 suppression conjunct over the failed record: `CapturedVersionIsApproved(result[index])` and, for a `LilToonMulti` record, `MultiResolution != null` and `Refusal == LilToonMultiResolutionRefusal.AttestationFailed`. For the regular lilToon families the strict failure is itself the identity verify, so approval plus failure suffices. The conjunct is load-bearing: without it, an approved 2.4.0 Multi material refused on state, for example the clipping canceller, would earn a false "did not verify" sentence.
   - `CapturedAlphaMaterial` gains one bool, `CapturedVersionApproved`, defaulted false. Only `UnattestedMaterial` (`:1289-1322`) sets it true, through a new defaulted parameter fed from the outcome's flag mirror.
   - The Host substitution (`Editor/Host/UnityAnimationEvidenceCapture.cs:711-724`) is a touched site: the k-th failed ordinal reads the k-th flag, empty means false, and passes it to `UnattestedMaterial`. The count invariant at `:703-710` stays unchanged and never grows a flags-count clause, which record-less capturers can never satisfy.
5. Scope: the note fires only for an approved version whose identity failed. Out-of-range, state-refused, and unsupported sources keep the sentinel and the consent-subject note they earn as of 2026-10-20. The build path keeps its existing named-cause sentence for Multi (`AmuseReportStrings.cs:460-462`).
6. Known loss path, accepted: `RecordConsentFacts` runs on the renderer compute path only. An open interrupted after renderer rows stored leaves renderer entries in the cache and nothing under the avatar key. The next open serves renderers from cache, never captures, and stores note-less avatar rows. The note is absent until the fingerprint or the domain changes. This matches the existing consent-fact limitation and is accepted for this design. Acceptance criterion 4 applies to a completed compute run.
7. Test duty: sentence pin in `Tests/Editor/DryRun/MaterialTransformationRowTests.cs`; the new note appears exactly once for an approved-unverified source and `LockedMaterials` is absent for that avatar; an approved, state-refused Multi record sets no flag and fires no note; an out-of-range source still earns `LockedMaterials`; attested avatars earn neither; no duplicate across a cache hit; the flipped fixture in `Tests/Editor/Build/AlphaSeparationPreparationTests` and `Tests/Editor/DryRun/AlphaSeparationDryRunTests.cs:1336-1410`, which moves to `"2.4.2"` and gains the companion note expectation.

### B5. Boundary and row-set tests move with the flip

1. `AdmittedVersionRows_AreTheMeasuredSet` (`Tests/Editor/Semantics/LilToon/LilToonAttestationTests.cs:1760-1795`): version list gains `"2.4.1"`. Multi tuple list stays the two rows.
2. `PinnedDigests_AreTheTask0Measurements` (`:2228-2263`): the `IncludeTreeDigest` audit moves to the new literal. Its comment names the 2.4.1 row and the anchor license.
3. `Verify_AcceptsAdmittedPatchVersionWithItsOwnTree` (`:2737-2772`): gains the 2.4.1 case with its own tree. Its stale "bytes identical across 2.3.0-2.4.0" comment updates.
4. New exact-row drift case: version `"2.4.1"` presenting the 2.4.0 tree digest refuses as `ModifiedShaderSource`. This pins the row binding the flip creates. It mirrors `:1853-1870`.
5. New Multi pairing cases per B2 item 5, including the loose-install case moves at `:588-598`.
6. Implementers re-sweep for `"2.4.1"` literals and for consumers of the two moving constants before the flip commit. The investigation section 5 table lists the known sites as of 2026-10-20. The sweep is the authority.

### B6. Documentation moves with the flip

1. Package `CHANGELOG.md` gains an entry for 2.4.1 support and the preview note. Its attested-maximum wording says 2.4.1.
2. The repository agent guide's supported-version sentence says "exact rows through 2.4.1". Its DryRun sentence names the new note.
3. `README.md:42` stays as written. The approved-range sentence is already version-generic.
4. Code comments that name the attested maximum or the moving pairing target move in the same commit as the flip: the attestation header (`:345`), the `PackageVersion` comment (`:362`), the include-digest comment (`:391-392`), the row-block comment (`:396-412`), and the pairing-target comments in the attestation tests (B3 item 4). No comment references the burned ID namespaces of earlier designs.

## Risk register

| Risk | Mitigation |
|---|---|
| Tag-tree bytes differ from a real installed 2.4.1 | The anchor reproduces the recipe, not the install. On divergence, Multi refuses and the preview shows Unknown, fail-closed. The regular build path absorbs the divergence through the D3 approval arm and analyzes under the current pins without a refusal. The installed-shape follow-up in B1 item 5 bounds that window. A false admit is not reachable: the row digest matches only the tag bytes |
| The digest recipe replication diverges from the C# one | Impossible to miss: the anchor must reproduce `549c3b55…` from the 2.4.0 tree first. Any divergence halts before the flip |
| The note fires without an identity failure | The flag conjunct in B4 item 4 mirrors the D3 suppression: approval alone is not enough. A state-refused approved Multi record sets no flag. Dedicated test |
| Note duplicates after a cache hit | Fixed sentence, exact-match rebuild at `AlphaSeparationDryRun.cs:266-277`, and a dedicated cache-hit test |
| Note lost after an interrupted first open | Known cache limitation, accepted and stated in B4 item 6 |
| Witness flips break fixture premises beyond the named sites | The B5 item 6 re-sweep gates the flip commit. The 2.4.2 witness carries a drift marker and a named re-sweep obligation |
| Loose 2.4.1 Multi installs newly refuse | Fail-closed and pinned by B2 item 3. Packaged installs, the norm, attest through the pairing |
| Consent behavior drift | No consent surface changes. 2.4.1 becomes attested, so the D3 arms engage less often, which is the intended effect |

## Halt points for the owner

1. The anchor fails to reproduce the 2.4.0 digest from the 2.4.0 tag tree.
2. The computed 2.4.1 digest equals any existing row digest.
3. Any existing test fails after the flip in a way the plan does not anticipate.
4. The preview note appears for an attested avatar, for a state-refused approved Multi record, or twice across a cache hit.
5. A reviewer finds a production behavior change beyond the note, the row, and the Loose-Install Multi refusal that B2 pins.

## Acceptance criteria

1. The anchor reproduction and the 2.4.1 computation are both observed and dated before the flip lands. Observed 2026-10-20: anchor reproduced, 38 files, digest `549c…` matched. 2.4.1 computed from the 2.4.1 tree, 38 files, digest `063f…`.
2. After the flip: `2.4.1` verifies with its own tree. `2.4.1` with the 2.4.0 tree refuses as modified. `2.4.2` pairs the 2.4.1 row in the pairing tests and the D3 fixture. Packaged Multi `2.4.1` verifies through the pairing. Multi `2.4.1` with a foreign tree refuses. A loose 2.4.1 Multi install refuses.
3. The row-set and digest audits pass with the new row and the moved constants.
4. On a completed compute run: the preview shows the note exactly once for an approved-unverified source, `LockedMaterials` is absent for that avatar, an approved state-refused Multi earns no new note, out-of-range sources still earn `LockedMaterials`, attested avatars earn neither, and no note duplicates across a cache hit.
5. A full EditMode product and research run passes with zero modal blocks. The installed 2.4.0 stays attested throughout.
6. The documentation surfaces say 2.4.1.
7. `git diff --check` is clean at every commit.

## Decision IDs

Code comments implementing these decisions reference B1 through B6 and point to this document. The B namespace is fresh as of 2026-10-20. Earlier designs own A1-A5, D1-D7, T1-T3, M1, H1-H3, R1, S8-S11, and V8 in the same files. No code comment may reuse those letters for this change.
