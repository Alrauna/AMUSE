# AMUSE build caching - pipeline interaction and prerequisites

## Status and scope

Investigation record. Dated 2026-09-19. Branch `investigate/poiyomi-locked-materials`, based on `main` at `a3f9dca`. This note makes no production change. Per the owner's instruction it is deliberately separate from the two Poiyomi lock notes of the same date. It cites the working tree as of 2026-09-19.

Question. How does AMUSE interact with the NDMF and Unity build pipeline on 2026-09-19? What does it generate per build? Where does build time go? And what must change so AMUSE-generated results can be cached, reused, and validated or invalidated across builds?

Method. Source reading only, at the pins named in section 10. No live editor ran. No benchmark ran. Every cost statement in section 4 is structural and qualitative, not measured. Measured timings are an open question.

## 1. Pipeline position

The gate facts first, because they bound every cache key later. `HostLifecycleCapability.Evaluate` reads the Unity version, the NDMF version, the VRChat SDK Base and Avatars versions, the platform qualified name, the build path, and four build-context services (`HostLifecycleCapability.cs:126-213`). The D8 consent layer adds per-build consent above the last re-attested maxima.

AMUSE registers one plugin in the NDMF `PlatformFinish` phase (`AmusePlatformFinishPlugin.cs:141-190`). The pass order:

1. `AMUSE structural graph check`, extension-free, reads real authored clips before virtualization.
2. Under `AnimatorServicesContext`: `AMUSE animator bindings capture`, then the semantic barrier (`AmusePlatformFinishPass.Execute`), then `AlphaSeparationApply.Execute`.

PlatformFinish is NDMF's last phase. Upstream tools such as Modular Avatar, VRCFury, and Avatar Optimizer have already run. Two ordering facts are load-bearing for caching:

- The mesh and materials AMUSE reads are the effective post-upstream build copy. Avatar Optimizer's own documentation path in this repository states its passes run before AMUSE (`AmuseAvatarOptimizerInformation.cs`, class comment).
- Upstream outputs can be NDMF-generated objects with no stable project identity. Avatar Optimizer texture atlases arrive as sub-assets of an NDMF `SubAssetContainer` under the temporary generated-asset root (`GeneratedTextureAttestation.cs:51-65`). NDMF deletes that root after every build (`AvatarProcessor.TemporaryAssetRoot`, `CleanTemporaryAssets` at `AvatarProcessor.cs:64-82`).

So a cache key built from project GUIDs alone is unsound for upstream-generated inputs. Their identity survives only as content.

## 2. What AMUSE generates per build

| Artifact | Created at | Lifetime | Persisted |
|---|---|---|---|
| Mesh clone, later the separated mesh | `Object.Instantiate` at `AlphaSeparationPreparation.cs:495`; layout surgery in `FinalizeClone` at `AlphaSeparationApply.cs:676-749` | Build only | No |
| Canonical opaque materials | `new Material(source)` plus the attested target shader, `LilToonOpaqueTarget.cs:270-273` and the Poiyomi counterpart in `PoiyomiOpaqueConversion.cs` | Build only | No |
| Effective materials under animation divergence | `EffectiveMaterialMaterialization.cs:72,136` | Build only | No |
| GPU evidence transients | `RenderTexture.GetTemporary` and a predicate material, `UnityAlphaFieldEvidence.cs:567,586` | Milliseconds | No |
| Curve-edit records | `AlphaSeparationCurveEdit`, written at apply | Build only | No |

No production file calls an AssetSaver save method. The only `AssetSaver` use in AMUSE is the gate's service-availability check (`HostLifecycleCapability.cs:245-255`). The source-scanning test `ProductionEditorCodePersistsOnlyThroughTheNdmfAssetSaver` (`Tests/Editor/Build/AlphaSeparationApplyTests.cs:98-142`) forbids direct `AssetDatabase.CreateAsset` and `AddObjectToAsset` in production code. Generated objects ride the build avatar's serialization into the output, the sweep destroys unreferenced clones (`AlphaSeparationApply.cs:859-907`), and NDMF removes its temporary container root after the build.

Conclusion. Nothing AMUSE creates survives a build. Every build redoes all evidence capture, classification, planning, and generation. Cross-build reuse would be a new capability, not a growth of an existing store.

## 3. Caches that exist on 2026-09-19

Three deliberate session-scoped caches exist.

- `UnityGeneratedTextureEvidence.SessionCache`, keyed by texture instance id, channel, cutoff, policy bounds, and active mip limit (`UnityGeneratedTextureEvidence.cs:21-33`). Cleared at pass start (`AmusePlatformFinishPlugin.cs:354`) and in the sweep (`AlphaSeparationApply.cs:906`).
- `UnityStreamingTextureEvidence.Cache`, keyed by asset GUID plus source and `.meta` write times (`UnityStreamingTextureEvidence.cs:45-54`). Any re-import re-clones instead of serving stale evidence. Editor-session scope only.
- A host capability flag keyed by nothing, with a doc comment that names it explicitly not a texture evidence cache and bars growing it into one (`UnityAlphaFieldEvidence.cs:668-672`). Domain reload clears it.

Two design stances are already encoded here. Identity-plus-write-time is the accepted validity signal for project assets. And session caches are deliberate, with growth guarded by tests (`UnityGeneratedTextureEvidenceTests.cs:320-459`, `UnityStreamingTextureEvidenceTests.cs:141-179`). Cross-build reuse was never built and would be a new contract.

## 4. Where build time goes

Structural shape only, per the method note.

- Per-texture evidence is the dominant per-material cost. The GPU route gates formats, blits per mip level, and reads back asynchronously (`UnityAlphaFieldEvidence.cs:567-586`). The CPU route clones whole textures and reads pixels (`UnityStreamingTextureEvidence.cs`). Both are already session-cached by identity, so their repeat cost within one build is near zero and their first-build cost is the target of any cross-build cache.
- Classification runs exact rational interval arithmetic per triangle over UV envelopes (`Analysis/TriangleAlphaClassifier.cs`, invoked per resolution at `AmusePlatformFinishPlugin.cs:1213-1222`). This is pure CPU work on small structs.
- Mesh surgery reads and rewrites index arrays of split slots (`AlphaSeparationApply.cs:690-718`).
- Material conversion runs attestation, schema extraction, and per-family recipes (`EffectiveMaterialMaterialization.cs`, `PoiyomiOpaqueConversion.cs`, `LilToonOpaqueTarget.cs`).
- The transient-unlock path from the lock notes adds per-build costs: clone, restore through a reflection seam, and revalidation probes. Its revalidation is the piece a cache could legitimately shorten, because its inputs are the material serialization, the Thry version pin, and the original shader digest.

None of these numbers is measured. A live profiling pass is a prerequisite before choosing what to cache first.

## 5. Cacheable results and their key inputs

A cache entry is sound only when its key fully determines its payload. Candidates, in expected value order.

| Candidate | Key must include | Payload |
|---|---|---|
| Texture alpha evidence chain | Texture content identity (GUID plus write times for project assets, content hash for generated ones), format, wrap and filter state, color space, platform, cutoff, mip limit, policy bounds, capture route | Mip chain bytes |
| Per-slot classification outcome | Submesh geometry hash, UV set hash, material evidence digest, policy version, mip and size policy | Proven-opaque, must-stay, unknown triangle ordinals |
| Separation plan and final layout | Classification outcome and slot set | Plan, appended layout, bounds recipe inputs |
| Canonical material recipe | Source material serialization digest, attested target digest, frontend version, policy | Property recipe, not the Material object |
| Curve-edit set | Clip content hash, binding identity, mapping | Keyframe payloads |
| Unlock revalidation fingerprint (path 3) | Material serialization digest including tags, Thry version pin, original shader digest | Restore transcript and fingerprint verdict |

Two hard rules follow from section 1. Keys derive from the effective build state, never from project paths alone. And upstream-generated inputs key on content, because their GUIDs die with the NDMF temporary root.

## 6. Validation and invalidation requirements

- Soundness contract. A hit must be indistinguishable from a recompute in every downstream observable. Any doubt degrades to a miss. A cache miss is the behavior of 2026-09-19, so the failure direction is always toward recompute, never toward a wider transformation.
- Live revalidation stays. The apply pass already refuses candidates whose mesh or slot identity drifted (`AlphaSeparationApply.cs:142-155`). A hit may skip only the keyed recomputation. The gate, the per-build consent, and the apply's live checks run on every build regardless of hits.
- Invalidation set. AMUSE policy and payload schema versions, the attested digest set, the pinned frontend versions, and the host versions the gate already reads. A lock-path entry additionally dies on any Thry or Poiyomi pin change.
- Payload validation. Each entry carries a schema version and a digest. A reader that fails validation treats the entry as absent. An unreadable cache never fails a build and never refuses; it recompute.
- Ownership and cleanup. The store is AMUSE-owned, content-addressed, and self-evicting by age or capacity. It never touches non-AMUSE paths. The per-build sweep keeps its current scope.

## 7. Storage location

Three options, with the decision deferred to design.

- Library-local cache directory. Machine-local, never committed, wiped with the Library folder, which only costs a cold rebuild. It does not conflict with the persistence guard test, because that test bans `AssetDatabase` persistence in production code and a raw-bytes store performs none. This is the boring default.
- An Assets folder, as Thry's own `_LockedShaderCache` precedent does. Survives Library wipes and is visible to users, but it pollutes projects, needs ignore-list hygiene, and contradicts the current posture that AMUSE persists nothing into the project. Choosing it is a deliberate contract change that must update the guard test.
- The NDMF container. Per build only. Verified unsuitable for cross-build reuse, because NDMF deletes the root after the build (`AvatarProcessor.cs:64-82`).

## 8. What AMUSE needs

Requirement level, not design.

1. A versioned cache service seam owned by `Build/`: typed get and put, key computation, digest validation, and hit metrics. No call sites reach storage directly.
2. Key derivation at capture time from effective state: mesh data hashes, the texture identity tuple, material serialization digests, and the policy, pin, and host versions.
3. Versioned payload schemas. Readers validate and treat mismatches as misses.
4. The hit-path rule of section 6, stated once and tested: skip only keyed recomputation.
5. Report integration: hit, miss, and stale counts per artifact class in the existing report surface.
6. Tests: hit-parity characterization against recompute, staleness falsifiers for upstream mesh edits, texture re-imports, and policy bumps, and an updated persistence guard if the storage contract changes.
7. Sequencing with the lock work. Land the seam with or before the transient-unlock path, so unlock revalidation is cache-covered from its first release.

## 9. Open questions

- Measured stage timings. Needs a live project. Decides the caching order of section 5.
- Whether Unity's dependency hash (`Hash128`) can replace write-time identity for project textures on Unity 2022.3. Unverified.
- Whether NDMF will offer a sanctioned persistent cache API upstream. Watch only.
- Whether classification entries should store ordinals or recompute plans from cheaper keys. Payload size against CPU.

## 10. Sources

- Working tree of branch `investigate/poiyomi-locked-materials` as of 2026-09-19, at every `file:line` cited above.
- Vendored NDMF 1.14.4 under `Packages/nadena.dev.ndmf/`, files `Editor/AvatarProcessor.cs` and `Editor/API/Serialization/AssetSaver.cs`.
- The lock notes of the same date for the transient-unlock cost shape: `2026-09-19-poiyomi-locked-materials-characterization.md` and `2026-09-19-poiyomi-locked-materials-paths.md`.
