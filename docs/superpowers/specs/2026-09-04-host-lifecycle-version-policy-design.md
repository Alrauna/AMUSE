# Host lifecycle version policy — design

Date: 2026-09-04. Gap: A1, `docs/superpowers/investigations/2026-09-04-0.1.0-horizon-assessment.md` section 4.1.
Base: `main` at `f903f28`, branch `feature/host-lifecycle-version-policy`.
Status: design only. A separate task implements this file. This file is the contract that task follows.

Labels: `[SOURCE]` is a fact read in the tree or in pinned upstream source. `[MEASURED]` is a fact from a run.
`[INFERENCE]` is a conclusion. `[DECISION]` is a choice this spec makes.

## 1. The defect

`package.json:8` publishes the dependency `nadena.dev.ndmf` at `>=1.14.4 <2.0.0-a`. `[SOURCE]`
The code pins NDMF `1.14.4` by ordinal equality (`Editor/Build/HostLifecycleCapability.cs:67`, `:98-101`).
The Unity pin and both SDK pins use the same equality (`:66-69`, `:93-111`). `[SOURCE]`
Current upstream NDMF is 1.14.8 (horizon note section 2, upstream clone `89c8f6d1`). `[SOURCE]`

A user can satisfy the published dependency and still get a refusal.
The refusal returns before any renderer is observed (`Editor/Build/AmusePlatformFinishPlugin.cs:331-334`),
and nothing reports it (horizon note section 5.4). `[SOURCE]`
The result is a total silent no-op. The code is stricter than the contract the package publishes.
One Unity patch release, one NDMF release, or one SDK release turns the whole feature off. `[INFERENCE]`

## 2. What each pin protects

**Unity.** The pin protects two characterized host facts.
First, the Windows default texture format tables: the capture admits only `StandaloneWindows64`,
with the format-table reason recorded at the gate (`Editor/Host/UnityAlphaFieldEvidence.cs:508-517`,
horizon sections 4.5 and 4.6). `[SOURCE]`
Second, the GPU blit and asynchronous readback route (horizon section 4.7, `UnityAlphaFieldEvidence.cs:26-37`).
The readback half already re-measures itself: a per-domain latch (`UnityAlphaFieldEvidence.cs:358-412`)
builds a 4x2 fixture (`:372-376`), runs the production route, and checks readback orientation and
binary R8 encoding on the live host (`:378-401`, `:414-464`). `[SOURCE]`
So the Unity pin carries the import-side format tables, and the latch carries the readback facts per Editor domain. `[SOURCE]`

**NDMF.** The pin protects the consumed public API set: `BuildPhase` and `Sequence` in the plugin
declaration (`AmusePlatformFinishPlugin.cs:147-165`), `AnimatorServicesContext` as the required
extension that exposes `IPlatformAnimatorBindings` and the `AnimationIndex`
(`Editor/Build/AmuseAnimatorBindingsCapture.cs:36-38`, `Editor/Build/AlphaSeparationApply.cs:72-74`),
and `IAssetSaver`, `ObjectRegistry`, `ErrorReport` as required services
(`HostLifecycleCapability.cs:141-153`). `[SOURCE]`
These types are NDMF public semver surface, and `package.json:8` already declares the range the package accepts for them. `[SOURCE]`

**VRChat SDK.** The pins protect the scope of the task-6 re-entry theorem.
Verdict A holds for SDK Base and Avatars exactly 3.10.4: the retained post-commit
`IPlatformAnimatorBindings.GetInnateControllers(root)` re-entry re-creates `AvatarDescriptorEditor3`
and runs its `OnEnable`, and it is semantics-preserving only for that version
(`docs/task-6-vrchat-sdk-3.10.4-source-audit.md` sections 5, 6, 11, and 14; section 14 re-affirms the pin).
The proof rests on the `OnEnable` fixed point and on the write-set classification of section 6,
which records that the expression and collider initializers are empty in 3.10.4. `[SOURCE]`

**Prior attestation.** The SDK build-environment contract investigation characterized this exact
triple — Unity `2022.3.22f1` revision `887be4894c44`, NDMF 1.14.4, SDK Base and Avatars 3.10.4 with
verified archive hashes (`docs/superpowers/investigations/2026-08-22-sdk-build-environment-contract.md`,
"Exact environment"). `[SOURCE]`
It returned Outcome D, "required guarantee not enforceable", for that stock host, and it pinned the
callback ordering facts: the SDK dispatcher orders preprocess callbacks by ascending `callbackOrder`,
NDMF's `PlatformFinish` hook sits at order `-1025`, and Apply-on-Play enters the preprocess chain
without the request event and ignores the failure result (same file, "Reconstructed lifecycle",
"Callback inventory", and "NDMF ordering"). `[SOURCE]`
The current `HostLifecycleCapability` is the fail-closed gate that grew from that result, so a version
range that outgrows the characterized hosts widens the theorem's exposure, which is why section 3 records residuals.

**Checks that stay.** The platform check compares NDMF's platform qualified name
(`HostLifecycleCapability.cs:70`, `:113-116`). The build-path check reads Unity editor state,
`EditorApplication.isPlayingOrWillChangePlaymode` (`:118-121`, `:138-140`), so it is a Unity check, not an SDK check.
The service checks read build-context capabilities (`:123-127`). `[SOURCE]`
None of the three reads a version string.

## 3. The decisions

`[DECISION] D1 — Unity admits 2022.3.x with x >= 22, release type f only.`
Refuse any other stream. Refuse release types `a`, `b`, `c`, `p`, and any unrecognized type.
Refuse unparseable input. Refuse null or absent input.
Reason: the characterized host facts are the format tables and the readback path, and the latch
re-measures the live-host readback facts once per Editor domain (`UnityAlphaFieldEvidence.cs:358-412`).

`[DECISION] D2 — NDMF admits >= 1.14.4 and < 2.0.0, no prerelease suffix.`
Refuse unparseable and null.
Reason: the consumed APIs are NDMF public semver surface. This is exactly the range
`package.json:8` publishes. Code and manifest must agree.

`[DECISION] D3 — VRChat SDK Base and Avatars admit >= 3.10.4 and < 4.0.0, no prerelease suffix.`
Refuse unparseable and null.
Reason: the build-path check reads Unity state (`HostLifecycleCapability.cs:138-140`) and the
platform check reads NDMF's qualified name (`:113-116`). Neither reads an SDK version.
The SDK pins guard only the task-6 theorem scope (section 2).

### D3-RESIDUAL — closed on 2026-09-04

The REQUIRED re-attestation ran the same day and shipped in PR #50. See
`docs/superpowers/investigations/2026-09-04-host-range-reattestation.md`.
Result: the task-6 fixed-point theorem HOLDS for every version the gate admits.
Admitted and attested: NDMF 1.14.4 through 1.14.8, SDK Base and Avatars
3.10.4 and 3.10.5. The 1.14.7 `VirtualControllerContext` hunk and the
3.10.5 Colliders inspector change each required a new class (a) argument.
The commit path and the OnEnable write set stayed byte-identical or
equivalent in every admitted version. No release blocker remains from this
section.

Recorded residuals:

- `[INFERENCE]` Unity within-stream patch stability of `Editor.CreateEditor` — accepted residual for D1.
  Unity does not publish the internals of `Editor.CreateEditor` (task-6 audit section 10).
  The theorem holds for any repetition of the explicitly invoked `OnEnable`, so the residual stays bounded.
- Task-6 pinned NDMF's VRChat bindings source by SHA-256 at 1.14.4 (task-6 audit section 2). `[SOURCE]`
  Accepted residual for D2: the widened NDMF range admits bindings sources the audit never hashed.

`[DECISION] D4 — the platform check, the build-path check, and the service-capability checks stay
byte-for-byte as they are` (`HostLifecycleCapability.cs:113-127`).
Apply-on-Play stays refused (`:118-121`). This change is version policy only.

`[DECISION] D5 — no new dependency and no third-party version-parsing library.`
The implementation adds a small internal comparator in `HostLifecycleCapability.cs`.
Reason: the grammar in section 4 is three short rules. A library adds surface for no correctness gain.

`[DECISION] D6 — null or absent version keeps its present meaning: refuse.`
`PackageVersion` returns null on a package miss (`HostLifecycleCapability.cs:161-180`), and the
null-refusal test stays (`HostLifecycleCapabilityTests.cs:71-79`).
A missing package is not a permissive case.

`[DECISION] D8 — the declared upper bounds stay. NDMF admits any 1.x below 2.0.0, and the
SDK admits any 3.x below 4.0.0. The bounds do not narrow to the attested maxima.`
The controller first chose option 2 of the re-attestation note: narrow the ceilings to
`{1,14,9}` and `{3,10,6}`. The user overruled on 2026-09-04. Reason: a minor or patch
bump declares no API change, and AMUSE must not add brittleness against semver without
evidence of breakage in practice.
Recorded counter-argument: semver covers API surface, not the write set. NDMF 1.14.7
changed a pinned file's behavior in a patch bump (re-attestation note section 5).
Rejected option: narrow ceilings to `{1,14,9}` and `{3,10,6}` (re-attestation note section 7).
Reversing falsifier: a patch or minor release inside the bounds changes a pinned file's
write set in a semantic class. That event re-opens this bounds decision.
Consent layer, added 2026-09-05: every build warns and asks before it classifies against
an unattested version of any integration (host or attested shader family), and a major
beyond the declared bounds also asks. Decline means refuse. Batch mode refuses without
asking. The per-release re-attestation obligation moves versions out of the warning set.

## 4. Comparison rules

Precise enough to implement without judgment. Every numeric comparison is per component as an integer.
No code path compares numeric parts as strings. `EqualsOrdinal` stays for the platform qualified name
and the package-name lookup (`HostLifecycleCapability.cs:156-159`, `:168`); the four version checks stop using it.

### 4.1 Common rules

- A null input refuses (D6).
- An unparseable input refuses with the family's named cause (section 5).
- A component is a run of one or more ASCII digits, parsed as an integer.
  Any other character makes the input unparseable.
- Sources: Unity from `Application.unityVersion` (`HostLifecycleCapability.cs:144`),
  the three packages from `PackageInfo.version` through `PackageVersion` (`:145-147`, `:161-180`).

### 4.2 Unity

Format `M.m.p<type><n>`: three digit runs, then one release-type letter from `f`, `c`, `a`, `b`, `p`,
then a digit run.
Admit iff `M.m` equals `2022.3`, `p >= 22`, and the type is `f`.
Everything else refuses. Specifically:

- A missing type suffix is unparseable and refuses.
- A type letter with no digits after it is unparseable and refuses.
- Fewer or more than three numeric components is unparseable and refuses.

### 4.3 NDMF and SDK (one rule, applied twice)

Grammar: `M.m.p` with an optional `-` prerelease suffix.

1. A `-` prerelease suffix refuses: `1.15.0-beta.1` and `3.11.0-a` refuse.
   Any other foreign character, including `+`, is unparseable and refuses.
2. Split the numeric part on `.`. Exactly three components are valid.
   One component, two components, four or more, or an empty part is
   unparseable and refuses. A registered package always carries three
   components. The Unity 2022.3 package manifest makes `version` a required
   property and states its format as "MAJOR.MINOR.PATCH", which "must
   respect Semantic Versioning". Without the required properties, "either
   the registry refuses the package when it's published, or the Package
   Manager can't fetch or load the package"
   (https://docs.unity3d.com/2022.3/Documentation/Manual/upm-manifestPkg.html).
   The Unity 2022.3 versioning page states the same contract: "Packages
   must follow Semantic Versioning (SemVer)", and SemVer "expresses
   versions as MAJOR.MINOR.PATCH"
   (https://docs.unity3d.com/2022.3/Documentation/Manual/upm-semver.html).
3. Compare against the floor `F` and the exclusive bound `U` — NDMF: 1.14.4 and 2.0.0; SDK: 3.10.4 and 4.0.0.
   Both sides carry exactly three components, so admit iff `F <= input`
   component-wise and `input < U` component-wise.
4. `[DECISION]` D7 — short input is unparseable and refuses: `1.14` and
   `3.10` refuse with the family's named cause. The earlier truncation
   rule failed open. It admitted `1.14`, which sits below the attested
   1.14.4 floor, while it refused `1.14.0`, the three-component form of
   the same version. The fail-closed invariant decides this rule: more
   uncertainty must never make optimization more aggressive. The evidence
   for the three-component grammar is the Unity SemVer contract (item 2).
   A rule may not take its justification from a falsifier table written in
   the same run — the table is not evidence.

### 4.4 Numeric proof

`1.9.0` against floor `1.14.4` compares 9 < 14 per component and refuses.
An ordinal text sort admits it, because `"1.9.0"` sorts above `"1.14.4"`.
The falsifier table carries this row, and its test proves numeric per-component comparison.

## 5. Refusal vocabulary

The `HostLifecycleRefusal` member names stay: `UnsupportedUnityVersion`, `UnsupportedNdmfVersion`,
`UnsupportedVrchatSdkBaseVersion`, `UnsupportedVrchatSdkAvatarsVersion`, plus the untouched platform,
build-path, and service members (`HostLifecycleCapability.cs:14-24`). `[DECISION]`
A range refusal and an equality refusal use the same named cause. No new member.

The `SupportedAssumption` string at `:132-133` describes the new ranges honestly. Replacement:

```
"Unity 2022.3.22f1 or newer 2022.3 f-release; NDMF 1.14.4 to any 1.x below 2.0.0, no prerelease; VRChat SDK Base/Avatars 3.10.4 to any 3.x below 4.0.0, no prerelease; NDMF platform nadena.dev.ndmf.vrchat.avatar3; non-Play NDMF build."
```

The upper bounds are exclusive majors, so the string names them instead of the word "latest".
The assert at `HostLifecycleCapabilityTests.cs:41` checks the prefix `Unity 2022.3.22f1` and still passes.
The implementation may extend that assert to pin the NDMF fragment.

## 6. Falsifier table

One row per input version. "Admit" means `MayUsePositiveMutation == true`.
"Refuse" means the named cause fires. Each row names the plausible wrong implementation it catches.
Rows 18 to 23 run twice, once for `com.vrchat.base` and once for `com.vrchat.avatars`.

### Unity, cause `UnsupportedUnityVersion`

| # | Input | Expected | Catches |
|---|---|---|---|
| 1 | `2022.3.22f1` | admit | floor placed above 22, or a leftover exact-equality branch |
| 2 | `2022.3.21f1` | refuse | floor compare done on major.minor only, so any `2022.3` patch admits |
| 3 | `2022.3.23f1` | admit | leftover `patch == 22` equality beside the range check |
| 4 | `2023.1.0f1` | refuse | ordinal string compare against the floor, which orders `2023` above `2022.3.22f1` with no stream equality check |
| 5 | `2022.3.22f2` | admit | the old equality refusal case kept in the tests (`HostLifecycleCapabilityTests.cs:48`), or residual full-string equality |
| 6 | `2022.3.22b` | refuse | release type not checked against `f`, or a suffix accepted without its digit |
| 7 | `not-a-version` | refuse | parse failure coalesced to admit |
| 8 | null | refuse | null coalesced to the floor before compare |

### NDMF, cause `UnsupportedNdmfVersion`

| # | Input | Expected | Catches |
|---|---|---|---|
| 9 | `1.14.4` | admit | floor set above the published floor |
| 10 | `1.14.3` | refuse | patch dropped from the floor compare |
| 11 | `1.13.9` | refuse | floor placed one minor low, or an "any 1.x admits" rule |
| 12 | `1.15.0-beta.1` | refuse | prerelease suffix stripped before compare |
| 13 | `2.0.0` | refuse | missing exclusive upper bound |
| 14 | `2.0.0-a` | refuse | suffix stripped, or missing upper bound |
| 15 | `1.9.0` | refuse | text sort of numeric parts, because `"1.9.0"` sorts above `"1.14.4"`; this row proves numeric per-component comparison |
| 16 | `1.14` | refuse | the fail-open truncation rule that admits the two-component short form below the attested floor |
| 17 | `1.15.0` | admit | residual exact equality at the floor |

### SDK Base and Avatars, causes `UnsupportedVrchatSdkBaseVersion` / `UnsupportedVrchatSdkAvatarsVersion`

| # | Input | Expected | Catches |
|---|---|---|---|
| 18 | `3.10.4` | admit | floor above 3.10.4 |
| 19 | `3.10.3` | refuse | major.minor-only floor |
| 20 | `3.10.5` | admit | the old equality refusal cases kept in the tests (`HostLifecycleCapabilityTests.cs:92`, `:104`) |
| 21 | `4.0.0` | refuse | missing exclusive upper bound |
| 22 | `3.11.0-beta` | refuse | prerelease suffix stripped |
| 23 | null | refuse | null coalesced to admit |
| 24 | `3.10` | refuse | the fail-open truncation rule, as row 16, on both SDK causes (Base and Avatars) |

Row 17 is an addition beyond the required set. It exists because the current test file uses `1.14.5`
as a refusal case (`HostLifecycleCapabilityTests.cs:58`), and that case flips to admit under this policy.

A controller review on 2026-09-04 found the earlier short-input rows unsound.

## 7. Scope and non-goals

### Files the implementation may change

- `Packages/com.alrauna.amuse/Editor/Build/HostLifecycleCapability.cs` — the four constants, the comparator, the `SupportedAssumption` string.
- `Packages/com.alrauna.amuse/Tests/Editor/Build/HostLifecycleCapabilityTests.cs` — flip the four old refusal cases that now admit (`:48`, `:58`, `:92`, `:104`), keep or extend `:41`, add the falsifier rows of section 6.

### Files that stay untouched

Four other test files construct supported facts with the pinned strings and keep their values,
because those values sit inside the new admitted ranges (`2022.3.22f1`, `1.14.4`, `3.10.4`, `3.10.4`): `[SOURCE]`

- `Tests/Editor/Build/AlphaSeparationApplyTests.cs:335-339`
- `Tests/Editor/Build/AlphaSeparationPreparationTests.cs:5039-5043`
- `Tests/Editor/Build/AmuseBuildOperationTests.cs:486-490`
- `Tests/Editor/Build/AmusePlatformFinishPluginTests.cs:3228-3235`

### Non-goals

- No component, no report channel, no `ObjectRegistry` call, no exclusion mechanism, no settings object.
  Gaps B1 to B6 of the horizon note stay closed. `[DECISION]`
- No shader attestation widening. The lilToon 2.3.4 and Poiyomi 9.3.64 pins stay exact (horizon section 4.3). `[DECISION]`
- Apply-on-Play stays refused (D4).

### TDD obligations

Every admission-direction behavior change gets a test observed RED against the current equality code
and GREEN after the change. The admission-direction rows are 3, 5, 16, 17, and 20.
The refusal-direction rows are comparator guards. Their defense target is not today's code but the
plausible wrong implementation named in each row, so a guard can pass against old and new code and
still earn its place.
