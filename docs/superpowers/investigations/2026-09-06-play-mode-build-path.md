# Play mode execution: what runs, what refuses, what is silent

Date: 2026-09-06. Branch: `investigate/play-mode-build-path`. Base: `main`
at `74d3842`. Trigger: the user put the component on an avatar, entered
Play mode, and saw no effect on a mesh that carries known opaque faces on a
material whose shader is `Hidden/lilToonTransparent`. Other agents had
stated that AMUSE does not run on play mode builds.

Labels: `[SOURCE]` is a fact read at cited lines in this tree, or observed
in the live editor. `[MEASURED]` is a read-only editor observation made in
this investigation. `[INFERENCE]` is a conclusion. `[LIMIT]` is a known
instrument or evidence limit. `[DECISION NEEDED]` is a choice for the user.

## 1. Verdict

**AMUSE does not optimize in Play mode, by its own recorded decision, and
the refusal is silent.** NDMF executes every AMUSE pass in Play mode by
default. AMUSE's lifecycle gate then refuses with
`HostLifecycleRefusal.UnsupportedBuildPath` and returns before any
observation and before any report call. The user's observation is fully
explained. `[SOURCE]`

Play mode entry is the only refused invocation path. The two no-upload
testing paths that run in edit mode, NDMF manual bake and the SDK's
Build & Test local build, cross the same gate as an upload, so AMUSE runs
on both today. `[SOURCE]` `[INFERENCE]`

## 2. The full chain, from Play to silence

NDMF 1.14.4 (`Packages/nadena.dev.ndmf/package.json:4`):

1. Play mode entry runs the real VRChat SDK preprocess chain.
   `Editor/ApplyOnPlay.cs:62-66` subscribes to `playModeStateChanged` and
   arms the scene activator. For an avatar root with a
   `VRCAvatarDescriptor`, NDMF renames the root and calls
   `VRCBuildPipelineCallbacks.OnPreprocessAvatar` (`ApplyOnPlay.cs:88-98`).
   The only gate is `Config.ApplyOnPlay`, default true
   (`Runtime/NonPersistentConfig.cs:11`).
2. NDMF registers two hooks on that SDK chain. The second,
   `BuildFrameworkOptimizeHook` at callback order `-1025`, runs
   `ProcessAvatar(holder.context, BuildPhase.Optimizing, BuildPhase.Last)`
   (`Editor/VRChat/BuildFrameworkPreprocessHook.cs:63-84`). `BuildPhase.Last`
   is `PlatformFinish` (`Editor/API/Attributes/BuildPhase.cs:33-34`). The
   hook's play gate is the same `ApplyOnPlay` config (`:69-71`).
3. All three AMUSE passes therefore execute on Play mode entry. They
   register in `BuildPhase.PlatformFinish`
   (`AmusePlatformFinishPlugin.cs:146-170`).

AMUSE then refuses its own run:

4. The barrier pass evaluates the host lifecycle.
   `CaptureAndEvaluate` maps `EditorApplication.isPlayingOrWillChangePlaymode`
   to `AmuseBuildPath.ApplyOnPlay` (`HostLifecycleCapability.cs:210-212`).
   `Evaluate` refuses every path that is not `NonPlayNdmfBuild` with
   `UnsupportedBuildPath` (`:179-182`).
5. The refusal is silent. `Execute` returns when `MayUsePositiveMutation`
   is false, before the trigger check and before every report call
   (`AmusePlatformFinishPlugin.cs:339-344`). The avatar summary is the only
   writer of the inspector status store (`AmuseReports.cs:61-70`), and that
   code never runs. The apply pass no-ops on the same lifecycle gate
   (`AlphaSeparationApply.cs:36-43`).
6. Tests pin this behavior. `LifecycleRefusalNeverInvokesMutation` asserts
   `UnsupportedBuildPath` as the canonical refusal
   (`AmuseBuildOperationTests.cs:48-62`), and the pipeline test asserts the
   executed-but-zero-work state (`AmusePlatformFinishPluginTests.cs:42-46`).
7. The decision is recorded. Scope decision V11 says "Apply-on-Play stays
   refused" (`docs/superpowers/specs/2026-09-05-0.1.0-scope-design.md`).
   Horizon gap A9 recorded the same gate.

NDMF itself gates its hooks on the identical predicate
(`BuildFrameworkPreprocessHook.cs:33,69`). NDMF's only public build-path
signal is `RuntimeUtil.IsPlaying`, the same expression
(`Runtime/RuntimeUtil.cs:54-58`). `[SOURCE]`

## 3. Live editor observation `[MEASURED]`

Read-only, behind the identity gate on the repository project instance
(`Application.dataPath` matched `<repo-root>/Assets` exactly):

- The active build target is `StandaloneWindows64`. Horizon gap A5 admits
  only this target for texture-backed proof, so the alpha evidence route is
  available on this editor.
- The scene held no AMUSE component at check time. The user's play run was
  not instrumented.
- The console held no AMUSE entries. AMUSE reports go to the NDMF error
  report, not to the Unity console, so this is consistent with both "the
  pass ran and refused silently" and "nothing ran".

`[LIMIT]` The user's exact play run was not observed. Section 2 is verified
from source, not from that run.

## 4. What Play mode execution would mean

- **In-place mutation.** NDMF does not clone for play mode. It fakes the
  SDK's `(Clone)` naming on the live scene avatar
  (`ApplyOnPlay.cs:88-103`) and mutates it in place. Unity discards scene
  changes on play exit, so authoring assets stay safe. The "NDMF build
  copy" and the play avatar are one object. `[SOURCE]`
- **Transient assets.** Generated assets persist under NDMF's temporary
  root, which NDMF deletes on returning to edit mode
  (`ApplyOnPlay.cs:195-199`). The `IAssetSaver` and its container exist in
  play mode, so the AMUSE services gate would pass. `[SOURCE]`
  `[INFERENCE]`
- **Reports work.** `BuildContext.Finish` auto-opens the NDMF console when
  a build reported anything, in every invocation path
  (`BuildContext.cs:579-582`). `[SOURCE]`
- **Coverage limits stay.** The user's `Hidden/lilToonTransparent` mesh
  moves only when texture formats (A6), sampling states (A7: mirror wrap
  still refused), and the UV transform (A8: identity only) pass. The GPU
  alpha readback route has never been characterized under play mode.
  `[LIMIT]`
- **Consent dialog.** Unattested host or shader versions would open the
  consolidated dialog during the play transition. Batch mode refuses. Play
  mode would prompt. `[INFERENCE]`

## 5. Ways Play processing never reaches PlatformFinish

These matter after any gate change. None of them changes the verdict today.

- `Tools/NDM Framework/Apply on Play` is off. The config is
  non-persistent and resets each session. `[SOURCE]`
- Av3Emulator is active. NDMF stands down and the emulator drives the same
  SDK chain (`ApplyOnPlay.cs:71`). `[SOURCE]`
- VRCFury drives play mode. NDMF detects the invocation and truncates at
  Transforming, so `Optimizing` and `PlatformFinish` never run
  (`AvatarProcessor.cs:201-220`). `[SOURCE]`
- Dedup: an avatar already processed this play session is skipped
  (`ApplyOnPlay.cs:78`). `[SOURCE]`

## 6. How peers behave

| Tool | Play mode | Mechanism |
|---|---|---|
| Modular Avatar | Applies, since 1.8.0 (2023-10) | NDMF Apply on Play |
| Avatar Optimizer | Applies; play mode is the norm | NDMF Apply on Play |
| d4rkAvatarOptimizer | Applies; `Optimize in Play Mode` setting | Own SDK hook, not NDMF |

NDMF documents play mode application as a headline feature and no upstream
document excludes `PlatformFinish` from it. Avatar Optimizer ships
play-specific user messages, so late-phase tools already carry play-mode
behavior in production. `[SOURCE]`

- https://github.com/bdunderscore/ndmf (README)
- https://modular-avatar.nadena.dev/docs/manual-processing
- https://github.com/anatawa12/AvatarOptimizer (README, changelog)
- https://github.com/d4rkc0d3r/d4rkAvatarOptimizer (README, changelog)

## 7. Testing paths that already work

- **Build & Test.** The SDK's local build runs the same preprocess chain in
  edit mode. `isPlayingOrWillChangePlaymode` is false, so AMUSE admits the
  run and reports normally. The upload and the VRChat API network calls are
  skipped. `[INFERENCE]` from the hook gates. The SDK-side local-build
  wiring is outside this checkout. `[LIMIT]`
- **NDMF manual bake.** `Tools/NDM Framework/Manual bake avatar` runs all
  phases in one shot in edit mode and keeps the baked clone with persistent
  assets under `Assets/ZZZ_GeneratedAssets`. AMUSE runs and its output is
  inspectable. `[SOURCE]`

## 8. Finding beyond the question

**Every lifecycle refusal is silent, not just play mode.** Unsupported
Unity, NDMF, and SDK versions, wrong platform, and missing services all
take the same silent return (`AmusePlatformFinishPlugin.cs:339-344`).
Scope decision V7 promises that "a silent no-op becomes impossible for
opted-in avatars". The lifecycle path breaks that promise today. One
report call per refusal would have made the user's play run
self-explanatory. `[SOURCE]` `[DECISION NEEDED]`

## 9. Options

**Option A: admit Play mode.** Reverses V11. `CaptureAndEvaluate` admits
`ApplyOnPlay`, the `SupportedAssumption` text changes, and the tests in
section 2 step 6 flip RED first. The cost is characterizing the GPU
readback route under play mode before trusting texture-backed proof there.
Unknowns stay fail-closed by construction: unproven triangles keep their
source material.

**Option B: keep V11, report the refusal.** Keep the play refusal. Add the
lifecycle report line from section 8 so any refusal is visible. Test today
through Build & Test and manual bake.

**Option C: A and B together.** Admit Play mode and report every refusal.
The report work is required by V7 in both worlds.

Recommendation: C. The mechanism is small, every peer tool runs in play
mode, and the report work stands on its own. The decision is the user's:
V11 is a recorded user decision.
