# Q-A2 - Poiyomi lock timing and locked-shader identity

## 1. Question and bounded scope

H1 section 4.2 and section 10 item 1 left one question open:

> When AMUSE observes a build avatar in the NDMF PlatformFinish phase, are the
> avatar's Poiyomi materials locked, and does AMUSE's Poiyomi frontend see an
> identity it can attest?

The question splits in two, and this note answers both parts.

- Q-A timing. Does Poiyomi lock materials during the upload, through what
  mechanism, and at what order relative to the two NDMF callbacks at -11000 and
  -1025? Is the lock default or opt-in?
- Q-B identity. Which of the five AMUSE attestation gates fail on a locked
  material, and in what order?

The note makes no production change and writes no test. It records options and
costs for the controller. It does not answer the other H1 gaps. One sentence
about A4 appears in section 4, because the amplifier changes the size of the
finding.

Labels: `[SOURCE]` is a fact read at a pinned revision. `[INFERENCE]` is a
conclusion or a corroboration. `[DECISION NEEDED]` is a choice for the
controller.

## 2. Repository, base state, and method

| Axis | Value |
|---|---|
| Branch | `investigate/poiyomi-lock-timing` |
| Base | `main` at `b6dfc88` (merge of PR #47) |
| Working tree | `Packages/manifest.json` and `Packages/packages-lock.json` modified and unstaged. They carry pre-existing user-owned Unity toolchain and sysroot changes. This investigation did not touch them |
| Poiyomi in this project | none. `grep -c poiyomi Packages/manifest.json` returns 0. Nothing was installed |

Method: one task agent and two read-only scout agents read pinned upstream
source in parallel. The controller verified every load-bearing line in person
before this note was written. No Unity MCP call was issued. No Census Lab data
and no private avatar data was used, inspected, or modified.

Pinned revisions:

| Source | Pin | Archive SHA-256 |
|---|---|---|
| `com.poiyomi.toon` 9.3.64 | VPM release zip for tag `v9.3.64`. The tag equals commit `e125e1c33cbfb860f59330799dd4d10a1097242d`, the commit AMUSE pins at `PoiyomiMaterialSemantics.cs:24-26` | `42217aa158ea685b8c0f3d9599229aca4a0d5b72ef46a980d8a76f70f0b5a7f6` |
| `com.poiyomi.thryeditor` 2.73.9 | VPM release zip, drift check | `a47ff221450b5958d949a26d6901680bccf15efd655b20e227aaadea12fad599` |
| `Thryrallo/ThryEditor` upstream | commit `28ecf9d42f337ed27d9173eca32f13d7c9c5cb14`, newest on `master` | git clone. The commit hash is the pin |
| `bdunderscore/ndmf` | commit `89c8f6d1`, tag `Release 1.14.8`, the commit H1 pinned | git clone. The commit hash is the pin |

Acquisition notes:

- The listing at `https://vcc.poiyomi.com/releases/index.json` is dead. The
  working official listing is `https://poiyomi.github.io/vpm/index.json`. It
  carries a `zipSHA256` field for 9.3.64, and the downloaded zip matches it.
- The zip member cited below equals the tag checkout modulo line endings, so a
  line citation names the zip and the tag at once.
- All downloads and clones were made outside the repository and were deleted
  after the evidence was verified.

Citation shorthand:

- `AMUSE` = `Packages/com.alrauna.amuse/Editor/` in this repository.
- `9.3.64` = the member `_PoiyomiShaders/Scripts/ThryEditor/Editor/ShaderOptimizer.cs`
  of the `com.poiyomi.toon` 9.3.64 zip.
- `2.73.9` = a member of the `com.poiyomi.thryeditor` 2.73.9 zip, named per claim.
- `ndmf` = `bdunderscore/ndmf` at `89c8f6d1`.

## 3. Q-A - the lock mechanism, its trigger, and its order

### 3.1 Mechanism

Poiyomi 9.3.64 embeds the Thry shader optimizer. The lock class is
`ShaderOptimizer.LockMaterialsOnUpload` (9.3.64:2566-2569):

```csharp
#if VRC_SDK_VRCSDK2 || VRC_SDK_VRCSDK3
public class LockMaterialsOnUpload : IVRCSDKPreprocessAvatarCallback
{
    public int callbackOrder => 100;
```

The callback collects every material on every `Renderer` (9.3.64:2574) plus
every material that an animation clip of the avatar can swap in
(9.3.64:2579-2585). It then locks the set with `allowCancel: false`
(9.3.64:2589).

The lock per material works in six steps:

1. It generates a new shader. The new name embeds the original shader name and
   the GUID of the material: `"Hidden/Locked/" + shader.name + "/" + guid`
   (9.3.64:1057, 1065). For `.poiyomi/Poiyomi Toon` the locked name is
   `Hidden/Locked/.poiyomi/Poiyomi Toon/<materialGUID>`.
2. It writes the generated source next to the material, under
   `OptimizedShaders/`, and imports it (9.3.64:1075, 1463-1469). A fresh import
   makes a new asset with a new GUID.
3. It saves the origin identity in the material override tags `OriginalShader`
   and `OriginalShaderGUID` (9.3.64:1547-1549).
4. It retargets the material: `material.shader = newShader`
   (9.3.64:1583-1591). It then removes all shader keywords from the material
   (9.3.64:1597-1600).
5. It sets the material float of the optimizer property to 1 (9.3.64:941-946).
   In Poiyomi that property is `_ShaderOptimizerEnabled`.
6. It bakes every property value that no animation drives into the generated
   source as a literal constant (section 5).

Manual locking exists (inspector button, Poiyomi menus, Thry menus) and runs
the same core, so a manually locked material carries the same identity shape.
Vendor documentation describes the same behavior ([INFERENCE],
`https://www.poiyomi.com/general/locking`).

The vendor code names the locked identity a second time. The build-time
stripper drops unlocked Thry shader variants from a build, and its comment
defines the locked shape as a name that starts with `Hidden/Locked/`
(9.3.64:2624-2639).

### 3.2 Not an NDMF plugin

The lock is a VRChat SDK preprocess callback. A search over the whole 9.3.64
package finds zero matches for `ExportsPlugin`, `nadena.dev.ndmf`, or any NDMF
interface. Poiyomi 9.3.64 does not take part in NDMF.

### 3.3 Order relative to NDMF

NDMF registers the two VRChat preprocess callbacks
(ndmf `Editor/VRChat/BuildFrameworkPreprocessHook.cs`):

- `callbackOrder => -11000` at line 29, with the comment
  `// Must run before -10000 (VRCFury)` at line 28. It runs `BuildPhase.First`
  through `Transforming` (line 50).
- `callbackOrder => -1025` at line 65, with the comment
  `// just before RemoveAvatarEditorOnly`. It runs `BuildPhase.Optimizing`
  through `BuildPhase.Last`, which is `PlatformFinish` (line 84). AMUSE runs
  inside this callback, in the `PlatformFinish` phase
  (AMUSE `Build/AmusePlatformFinishPlugin.cs:144`).

The VRChat SDK runs preprocess callbacks in ascending `callbackOrder` order.
The VRCSDK source was not read here, so the rule itself carries an
[INFERENCE] label. The pinned ndmf comment at line 28 supports it: ndmf chose a
value below -10000 in order to run before VRCFury.

The resulting order:

| Order | Callback | Work |
|---|---|---|
| -11000 | ndmf `BuildFrameworkPreprocessHook` | NDMF FirstChance through Transforming |
| -1025 | ndmf `BuildFrameworkOptimizeHook` | NDMF Optimizing through PlatformFinish. AMUSE observes and mutates here |
| 100 | Poiyomi `LockMaterialsOnUpload` | locks every still-unlocked Thry material of the final avatar |

The lock at 100 runs after every NDMF phase. NDMF never observes the lock. A
material that is unlocked in the project is still unlocked when AMUSE sees it
at -1025, and gets locked after AMUSE, at 100.

### 3.4 Default or opt-in?

Default-on and unconditional. No `lockOnUpload` identifier exists anywhere in
9.3.64 or in 2.73.9 (search over all `.cs` files in both: zero hits). The
callback body has exactly two gates. It does nothing in play mode
(9.3.64:2573). It shows a one-time information dialog on first use
(9.3.64:2589). The dialog cannot cancel the lock, because the call passes
`allowCancel: false`. Any project with the VRChat SDK present gets the lock on
every avatar upload.

### 3.5 Drift: 9.3.64 vs current Poiyomi

- The lock engine moves out of the toon package. `com.poiyomi.toon` master
  (package version 9.3.67, commit `daede88edb58abc8c5dcc095ac8276af0b08e5ca`)
  deletes the embedded ThryEditor tree and declares a VPM dependency on
  `com.poiyomi.thryeditor >= 2.73.2`. The VPM index still serves 9.3.64 as the
  newest toon release today.
- Mechanism, trigger, and default are unchanged in `com.poiyomi.thryeditor`
  2.73.9. The avatar lock callback keeps `callbackOrder => 100`
  (2.73.9 `Editor/ShaderOptimizer.cs:3156-3158`).
- The generated-shader storage changed. 2.73.9 keys locked shaders by content
  hash under a central cache `Assets/_LockedShaderCache`
  (2.73.9 `Editor/LockedShaderCache.cs:24-25`, 51). The name still starts with
  `Hidden/Locked/` and still embeds the original shader name. No pinned digest
  becomes possible, so the identity problem stays the same.
- Upstream ThryEditor `master` agrees with both at
  `Editor/ShaderOptimizer.cs:2600-2605`.

### 3.6 What the locker sees

The lock runs at 100, after both NDMF hooks, so it works on the final build
avatar. The collection at 9.3.64:2574 takes every `sharedMaterials` entry of
every `Renderer` on that avatar. When alpha separation ran earlier in the
build, that set includes the AMUSE-generated canonical opaque clone on the
appended submesh (H1 section 1). [INFERENCE] NDMF serializes referenced
temporaries at the end of its -1025 hook (H1 section 5.6, ndmf
`BuildContext.cs:205-268`), so the clones are persisted assets before order
100 runs.

The apply step of the lock records and preserves state around the shader
swap:

- It saves the `RenderType` tag and `material.renderQueue` before the swap
  and restores both after it (9.3.64:1552-1555, `:1593-1594`). The vendor
  comment gives the reason: a shader swap deletes the `RenderType` tag and
  sets the queue back to -1 (9.3.64:1552-1553). AMUSE writes queue 2000 and
  `RenderType` `Opaque` on its canonical clone
  (AMUSE `Semantics/Poiyomi/PoiyomiOpaqueConversion.cs:173-175`, `:542-543`),
  so both survive the lock. This is a safety fact for the conversion path.
- It writes the `OriginalShader` and `OriginalShaderGUID` override tags
  (9.3.64:1547-1549), the same tags option 3 depends on.
- It clears every enabled shader keyword after the swap (9.3.64:1597-1600)
  and removes stripped textures from the serialized texture list
  (9.3.64:1558-1580). Both happen after the constant bake, so neither
  contradicts an AMUSE proof of the baked state.
- The locked name embeds the material GUID and, for a sub-asset, a local
  file ID (9.3.64:1056, `:1065`). [INFERENCE] An AMUSE clone inside an NDMF
  generated container is a sub-asset, so its locked shader name changes
  every build.

## 4. Q-B - what a locked material looks like to AMUSE

The AMUSE identity conjunction runs in a fixed order and stops at the first
failure (AMUSE `Semantics/Poiyomi/PoiyomiMaterialSemantics.cs:1303-1395`). On
a locked material the first check already fails, so the later checks never run.

| # | Gate in AMUSE check order | Locked material | Deciding citation |
|---|---|---|---|
| 1 | Exact shader name `.poiyomi/Poiyomi Toon` (`:1308-1317`) | FAILS. The locked shader name is `Hidden/Locked/.poiyomi/Poiyomi Toon/<materialGUID>` | AMUSE `:27`, `:1308-1317`. 9.3.64 `:1057`, `:1065` |
| 2 | Not locked. Material `_ShaderOptimizerEnabled` must be 0 (`:1319-1325`, read at `:1420-1422`) | Not reached. The lock sets the float to 1, so this gate would fail too | 9.3.64 `:941-946` |
| 3 | Shader asset GUID `9444ce77bf4418748b1e8591b9d97f85` (`:1336-1345`) | Not reached. The generated shader is a new imported asset with a new GUID | 9.3.64 `:1463-1469` |
| 4 | Package `com.poiyomi.toon` at `9.3.64` (`:1347-1370`) | Not reached. The generated asset sits beside the material, outside any package, so `HasPackage` is false and these gates skip | AMUSE `:1457-1458`, `:1466`, `:1347` |
| 5 | Normalized source SHA-256 (`:1373-1382`) | Not reached. The generated source depends on the material values, so no fixed digest can exist | 9.3.64 `:1065`, `:1099`, `:2207-2213` |

The controller expectation is confirmed. Locking generates a new per-material
shader asset with a different name and a different GUID. The name gate fails
first, and the `_ShaderOptimizerEnabled` gate is never reached.

This finding corrects H1 section 4.2 in two ways. H1 locates the refusal at
the lock flag (`:1319-1325`). The real refusal happens one check earlier, at
the shader name, and the lock flag plays no part. And the timing worry inverts:
upload-time locking runs after AMUSE, so it never produces a locked material
for AMUSE to see. The coverage loss comes from materials that were locked
before the build, in the project. Their locked identity fails the name gate,
so AMUSE cannot attest them at all.

With the A4 amplifier, one pre-locked material on a renderer fails the
attestation closure for the whole renderer (H1 section 4.4). The user sees the
diagnostic `UnsupportedShader` with the `Hidden/Locked/...` name
(AMUSE `:1313-1315`).

## 5. What a locked shader gives AMUSE that an unlocked one does not

The lock resolves branch state at generation time. Every property that no
animation drives stops being a material value and becomes a literal in the
shader source.

- The optimizer collects the values of all non-animated properties from the
  material: colors, vectors, floats, ints, and per-texture scale and offset
  (9.3.64:1150-1235). Properties tagged animated are exempt. They get renamed
  with a suffix, so clips keep driving them (9.3.64:1150-1171).
- It rewrites each reference in the shader source into that literal. The
  comment at the substitution site says `// Replace with constant!`
  (9.3.64:2207-2213). The literal formats are `float4(x,y,z,w)` and invariant
  float text (9.3.64:336-357).
- It injects the active keywords as `#define` lines and then removes all
  shader keywords from the material (9.3.64:1089-1095, `:1597-1600`).

For an unlocked material, AMUSE must prove alpha safety from property values,
and the proof must hold for every value a property can take. For a locked
material, the alpha-relevant state is written down in the generated source.
Nothing is left to vary. In principle a locked material is easier to reason
about than an unlocked one, because the branch state is resolved at generation
time instead of at material read time.

The same fact breaks today's attestation model. The generated source differs
per material, because it embeds the material values, and its name embeds the
material GUID. No pinned digest can describe that family. The AMUSE identity
scheme pins one source and one hash, and that scheme cannot stretch over
generated sources.

Section 3.6 gives the baked-constant argument a second consumer. The material
the locker bakes can be AMUSE's own output: the canonical opaque clone on the
appended submesh. The save-and-restore of `RenderType` and render queue
(9.3.64:1552-1555, `:1593-1594`) is what keeps that safe. The clone carries
the render state AMUSE gave it into the locked shader, so the bake changes
the property plumbing but not the render state the conversion relies on.

## 6. Options

The note records options, costs, and the correctness question each one raises.
The controller chooses.

**Option 1 - require unlocked and refuse otherwise. This is today's behavior.**
No code change. The refusal is conservative and correct. Cost: every pre-locked
material gets no optimization, and the A4 amplifier turns each one into a dead
renderer. On a sold avatar, that is plausibly most of the Poiyomi surface
([INFERENCE], carried over from H1 section 4.2). Correctness question: none.

**Option 2 - move AMUSE so it runs before the lock.** Moot, and the inverse is
not credible. AMUSE runs at -1025. The lock runs at 100. AMUSE already runs
before the lock, so this option asks for a state that exists. Running after the
lock would mean leaving NDMF for a raw VRChat callback at an order above 100.
Cost of that: abandonment of the NDMF lifecycle the package is built on.
Correctness question for a post-lock mutation: it must not disagree with baked
shader constants. The option buys nothing, because a pre-lock AMUSE already
sees the unlocked truth.

**Option 3 - attest generated locked source by another means.** The identity
inputs exist and are stable. The override tags `OriginalShader` and
`OriginalShaderGUID` name the origin shader and its GUID (9.3.64:1547-1549).
The `Hidden/Locked/` prefix names the family. A credible shape: branch on the
tags, check the original shader against the existing pinned digest, then
attest properties of the generated source itself. Cost: a second attestation
model, new pins for the lock engine, and proof obligations for source AMUSE
did not pin. Correctness question: does the transformation contract allow
attesting source AMUSE did not pin, and which lock-engine versions does it
accept? The contract today says no. This is the only option that recovers the
locked part of the market.
Facts 1 and 3 of section 3.6 strengthen this option: the vendor already
preserves the render state AMUSE depends on (9.3.64:1552-1555, `:1593-1594`)
and already records the original identity (9.3.64:1547-1549).

**Option 4 - ask the user to disable lock-on-upload.** Not possible as asked.
No setting exists. The lock is unconditional (section 3.4). The reachable
variant is to ask users to unlock their materials in the project. Cost:
documentation and support burden, and it works against the vendor pipeline,
because the stripper removes unlocked Thry shader variants from builds with a
warning dialog (9.3.64:2624-2650). Note the upload still locks an unlocked
material at order 100, so an unlock-and-upload flow functions. Correctness
question: none, but the product cost is real.

## 7. `[DECISION NEEDED]` items

1. Accept the pre-locked coverage gap for a first release (option 1), or fund
   locked-source attestation research (option 3). This note recommends
   recording option 3 as the follow-up investigation. Option 1 plus the A4
   amplifier plausibly zeroes Poiyomi coverage on the common sold-avatar shape.
2. If option 3 gets funded, decide the attestation shape before any code:
   tag-based identity branch, property checks on generated source, or
   re-derivation of the locked source from the pinned original.
3. Record the packaging drift as input to the version-pin policy. Current
   Poiyomi moves the lock engine into `com.poiyomi.thryeditor`, so a future
   AMUSE that pins `com.poiyomi.toon` versions must decide what a thryeditor
   dependency means for attestation.
4. `[DECISION NEEDED]` The generated-shader churn of section 3.6. A locked
   name that changes every build for a sub-asset clone is either a product
   concern AMUSE must address or a vendor behavior AMUSE only documents. The
   controller chooses.

## 8. Stop conditions and their status

1. "The lock mechanism cannot be established from pinned source." Not
   triggered. Every mechanism claim cites the 9.3.64 zip at the pinned tag.
2. "Poiyomi 9.3.64 cannot be obtained read-only." Not triggered. The official
   VPM zip was downloaded and hash-checked. One dead listing was found and
   worked around (section 2).
3. "The answer requires observing a real upload." Not triggered. The order
   facts come from pinned callback registrations.
4. "The evidence shows AMUSE's Poiyomi frontend is unsound for a reason
   unrelated to locking." Not triggered. This investigation found no defect
   outside the lock question.

## 9. What this note proves and does not prove

Proves, from pinned source: the lock class, its callback order 100, its
unconditional default, the generated shader naming, the new-asset and new-GUID
behavior, the retarget, the flag set, and the baked constants, all in 9.3.64
and by spot-check in 2.73.9. Proves, from the ndmf pin: the two callback
orders and the phases each one runs. Proves, from AMUSE source: the gate order
and the five outcomes of section 4.

Does not prove: how many sold avatars ship pre-locked. That stays
[INFERENCE]. Does not prove: the VRChat callback-order rule from SDK source.
The rule is [INFERENCE], supported by the ndmf comment and by the observed
vendor designs. Does not prove: that the baked values always equal the values
an unlocked analysis would read. The bake happens after AMUSE, on the same
material, so equality is expected. This note contains no test of it.
Does not prove: that a locked AMUSE clone has been observed end to end. No
run has observed one. The sub-asset name churn of section 3.6 is inferred,
not measured.

## 10. Citations

Repository, branch `investigate/poiyomi-lock-timing`, base `b6dfc88`. Paths
relative to `<repo-root>/Packages/com.alrauna.amuse/`:

- `Editor/Semantics/Poiyomi/PoiyomiMaterialSemantics.cs`. Pins at `:24-33`.
  Lock read at `:1420-1422`. Package evidence at `:1457-1458` and `:1466`.
  Gate order at `:1303-1395`: name `:1308-1317`, lock `:1319-1325`, source
  `:1328-1334`, GUID `:1336-1345`, package `:1347-1370`, hash `:1373-1382`,
  schema `:1385-1391`. Name diagnostic at `:1313-1315`.
- `Editor/Build/AmusePlatformFinishPlugin.cs`. `PlatformFinish` registration
  at `:144`.
- `Editor/Semantics/Poiyomi/PoiyomiOpaqueConversion.cs`. Canonical queue and
  `RenderType` constants at `:173-175`. Clone writes at `:542-543`.

Upstream `com.poiyomi.toon` 9.3.64. Zip SHA-256
`42217aa158ea685b8c0f3d9599229aca4a0d5b72ef46a980d8a76f70f0b5a7f6`. Tag
`v9.3.64` equals commit `e125e1c33cbfb860f59330799dd4d10a1097242d`. Member
`_PoiyomiShaders/Scripts/ThryEditor/Editor/ShaderOptimizer.cs`:

- `:2573` play-mode gate. `:2574-2585` material collection. `:2589` dialog and
  `allowCancel: false`.
- `:1057` and `:1065` generated name from the material GUID. `:1075` and
  `:1078-1081` generated directory.
- `:1089-1095` keywords as defines. `:1099` constant list. `:1150-1171`
  animated exemption and rename. `:1178-1235` value collection.
- `:1463-1469` write and import. `:1478` refresh.
- `:1547-1549` origin tags. `:1583-1591` retarget. `:1597-1600` keyword
  removal.
- `:1056` sub-asset flag. `:1552-1555` render state save. `:1558-1580`
  texture strip. `:1593-1594` render state restore.
- `:941-946` optimizer float set to 1. `:2727-2734` locked test reads the
  shader property default.
- `:336-357` literal formats. `:2207-2213` constant substitution.
  `:2624-2639` stripper and the `Hidden/Locked/` comment.

Upstream `com.poiyomi.thryeditor` 2.73.9. Zip SHA-256
`a47ff221450b5958d949a26d6901680bccf15efd655b20e227aaadea12fad599`:

- `Editor/ShaderOptimizer.cs:3156-3158` avatar lock callback, order 100.
- `Editor/LockedShaderCache.cs:24-25` cache root and prefix. `:51`
  hash-keyed path.

Upstream `Thryrallo/ThryEditor` at `28ecf9d42f337ed27d9173eca32f13d7c9c5cb14`:

- `Editor/ShaderOptimizer.cs:2600-2605` same hook and order upstream.

Upstream `bdunderscore/ndmf` at `89c8f6d1`, tag `Release 1.14.8`:

- `Editor/VRChat/BuildFrameworkPreprocessHook.cs`. Order -11000 at `:28-29`,
  phases at `:50`. Order -1025 at `:65`, phases at `:84`.

Corroboration, never load-bearing: `https://www.poiyomi.com/general/locking`
describes automatic lock on upload and a one-time notice. The VPM listing
`https://poiyomi.github.io/vpm/index.json` carries the `zipSHA256` values
quoted above.

## 11. Privacy statement

No Census Lab data and no private avatar data was used, inspected, or
modified. No Unity MCP call was issued. No private name, path, GUID, host
name, port, or instance identifier appears in this note. Every digest and GUID
cited belongs to a public vendor package or to a tracked file in this
repository. All downloads and clones were made outside the repository and were
deleted after the evidence was verified.
