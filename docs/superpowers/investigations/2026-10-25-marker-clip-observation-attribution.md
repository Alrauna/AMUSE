# Marker-clip observation attribution

Status as of 2026-10-26. Task 11 of the vendor-integrated test plan, stage 1b, V9 item 2. All runs ran in the dev editor instance of this repository. No test code changed. No product code changed.

## 1. Question

The vendor-integrated test rework record, section 4.3, third row, observed `AlphaSeparationApplyTests.MarkerClipRefusalIsSlotLocalAndWritesNothingForIt` fail with marker refusal count 0 against 1, and left the exact vendor pass open. This record names the pass that removed the marker-clip observation, and states the scoping consequence for the Task 12 survival audit.

## 2. Method, and the change of premise

The task brief planned a bisect: disable one vendor plugin at a time through `VendorSessionStateGuard.DisablePluginForFixture` and record which disabling restores the refusal count of 1. The premise no longer holds. Task 9 repaired the measurement site on 2026-10-25, so the loss does not reproduce: the count is 1 with every locked vendor active, first in the Task 9 focused runs and then in the full product-assembly run of 2026-10-26, 3063 completed, 0 failed (`docs/superpowers/investigations/2026-10-25-vendor-integrated-test-run-records.md`, block "Run of 2026-10-26, Task 10 stage 1 exit validation").

This record therefore attributes the pre-repair loss from the measured evidence of Task 9, and adds one confirming probe of 2026-10-26 that runs the brief's rows against the repaired checkout. Reverting the repair to re-run the literal bisect would weaken a valid test, so it was not done.

The probe set the NDMF plugin-disable `SessionState` key of one vendor plugin at a time, ran the marker test once per key in EditMode, and restored each key. NDMF's own `PluginDisablePrefs.IsPluginDisabled` read confirmed each key live before its run and clear after its restore. The keys are the exact keys the guard owns, built from its pinned prefix (`Tests/Editor/Build/VendorSessionStateGuard.cs:27-28, 44-47`). A guard scope itself cannot span an external run by design: the suite-end tripwire fails a run that ends with a guard-opened key still set (`Tests/Editor/RunEndSceneHygiene.cs:89-93`), and the suite-start check fails a run entered with a known key set (`Tests/Editor/Build/VendorSessionStateGuard.cs:71-85`). The AAO row of this probe observes that refusal directly.

## 3. The vendor set that can act in this build

The marker test builds on the dedicated apply test platform of `Tests/Editor/Build/AlphaSeparationApplyTests.cs`. Its qualified name is `com.alrauna.amuse.tests.alpha-separation-apply`. Only plugins without a platform restriction run there:

- Modular Avatar main plugin, id `nadena.dev.modular-avatar`, `RunsOnAllPlatforms` (`Packages/nadena.dev.modular-avatar/Editor/PluginDefinition/PluginDefinition.cs:29-33`).
- Modular Avatar late plugin, id `nadena.dev.modular-avatar.late-transform-stages`, `RunsOnAllPlatforms` (same file, lines 168-172).
- Avatar Optimizer, id `com.anatawa12.avatar-optimizer`, `RunsOnAllPlatforms` (`Packages/com.anatawa12.avatar-optimizer/Editor/OptimizerPlugin.cs:30-36`).

No other installed vendor can act in this build. NDMF's own VRChat plugin runs only on the VRChat platform (`Packages/nadena.dev.ndmf/Editor/VRChat/VRChatPasses.cs:8-15`), and the run log of 2026-10-26 lists its only pass as incompatible. The rework record section 4.4 already found no NDMF plugin declaration in the LTCGI, Light Volumes, Poiyomi, and Thry packages, and d4rk registers none. A grep of 2026-10-26 over the package folders found `ExportsPlugin` only in NDMF, Modular Avatar, Avatar Optimizer, and AMUSE's own product and test code. VRCFury acts only through the SDK callback, which `ProcessAvatar` does not call. The clean baseline run's pass log agrees: `Clone animators` in Resolving, the late MA passes in Transforming, the Avatar Optimizer passes in Optimizing, and no VRCFury pass anywhere.

## 4. The attribution

The refusing pass is Modular Avatar's main plugin Resolving pass `Clone animators`, acting through NDMF 1.14.8's controller reuse. The chain, measured by Task 9 on 2026-10-25 and verified in the locked sources:

1. `Clone animators` runs in Resolving on every platform under an `AnimatorServicesContext` (`PluginDefinition.cs:50-55`). Activating that context clones and commits every innate controller, so the live controller on the avatar becomes an NDMF commit.
2. When a later animator scope activates and the committed controller equals the last commit, NDMF reuses the virtual controller and empties the live committed controller's layers to reduce dirty-callback overhead (`Packages/nadena.dev.ndmf/Editor/API/AnimatorServices/VirtualControllerContext.cs:104-126`, the layer assignment at line 112, reached from scope activation at line 218).
3. The pre-repair test plugin enumerated the live committed controller inline inside its own active animator scope, so it read a controller whose layers NDMF had just emptied. It saw no clips, so the marker stub's special-motion flag never reached the evidence. The flag reaches evidence only through the committed-graph walk. The AnimationIndex path carries NDMF's `IsMarkerClip` answer, which does not consult the stub. The refusal count read 0.
4. Production never reads the live graph in that window. A reviewer verified this in the locked vendor sources on 2026-10-26, recorded in the Task 9 report (`.superpowers/sdd/2026-10-25-vendor-integrated-tests/task-9-report.md`). The loss was a test-plugin read shape, not a product false negative.

The Avatar Optimizer and both late MA passes are outside the mechanism. Nothing in the Task 9 evidence or this probe implicates them.

## 5. Confirming probe of 2026-10-26

Every row ran `AlphaSeparationApplyTests.MarkerClipRefusalIsSlotLocalAndWritesNothingForIt` once in EditMode. The test asserts the refusal count equals 1, so a pass means the count was 1.

| Disable key set | Key state verified | Discovered, completed, passed, failed | Duration | Observation |
|---|---|---|---|---|
| None, clean baseline | All three keys read false before the run | 1, 1, 1, 0 | 5.9 s | Refusal fired. The test output names reason `MarkerClipCarriesSlotBinding`. The pass log lists `Clone animators`, the late MA passes, and the Avatar Optimizer passes. |
| `nadena.dev.modular-avatar` | NDMF read the key live before the run, clear after the restore | 1, 1, 1, 0 | 1.4 s | The count stayed 1 without Modular Avatar. The repaired test neither depends on nor is disturbed by the main MA plugin. |
| `nadena.dev.modular-avatar.late-transform-stages` | NDMF read the key live before the run, clear after the restore | 1, 1, 1, 0 | 0.6 s | No effect. |
| `com.anatawa12.avatar-optimizer` | NDMF read the key live before the run | Refused at suite start | 2.2 s | No test body ran. Three `OneTimeSetUp` failures named the key with the suite-start message. The marker test never executed. |

The AAO refusal is the suite's own design, not a vendor effect: the suite-start check fails any run entered with a known plugin-disable key set, because a disabled vendor plugin changes what the suite measures (`VendorSessionStateGuard.cs:71-85`, reached from `RunEndSceneHygiene.cs:56-63`). An external AAO disable is therefore not measurable by design. AAO scoping is measurable only from inside a fixture through the guard, the discipline the transient unlock suites already use (`Tests/Editor/Build/TransientUnlockTestLifecycle.cs:87-111`).

After the last restore, NDMF read all three keys clear, and one final clean run gave 1 discovered, 1 completed, 1 passed, 0 failed, 0.6 s. Durations are not evidence of which passes ran: the first run of the session paid a one-time warm-up, and `Clone animators` alone logged 760 ms there. The pass log of the baseline row is the evidence of pass presence.

One inference stays labeled. `[INFERENCE]` Before the Task 9 repair, disabling the main MA plugin would have restored the count to 1, because without the `Clone animators` commit there is no commit to reuse, so the revalidation arm cannot empty any layers and the live graph keeps its clips. This was not measured, because it would require reverting the repair.

## 6. Scoping consequence for Task 12

1. The marker-clip falsifier needs no scoping verdict. Task 9's repair is the survival shape: the test plugin reads the stored pre-scope graph first and extension-free, exactly as the production plugin runs the structural check. The count assertion of 1 is the survival assertion. It is green against the full locked vendor set, and it stayed green under each single-vendor disable of this probe.
2. The pass that destroys authored-controller state by design is the main MA plugin's `Clone animators` in Resolving, on every platform. Where the Task 12 audit finds a falsifier whose planted state dies in that virtualization, for example the animation-event row of section 4.3, the scoping verdict must disable `nadena.dev.modular-avatar` through the guard and add that plugin id to `VendorSessionStateGuard.KnownPluginIds`, whose comment requires one verdict per id (`VendorSessionStateGuard.cs:30-37`). The late MA plugin id is a separate key, and this record gives no verdict for it.
3. The disable key acts per plugin, not per pass: NDMF filters disabled plugins when it builds the pass list (`Packages/nadena.dev.ndmf/Editor/API/Solver/PluginResolver.cs:213`). A scoping verdict therefore names a vendor plugin and never a single pass. There is no key for `Clone animators` alone.
4. External key sets are not a Task 12 mechanism. The suite-start check and the suite-end tripwire both refuse them, and the AAO row of this probe observed the first refusal. Every Task 12 scope must open and close the guard scope inside the fixture, in the same restore discipline the transient unlock suites use.
5. AAO already sits in `KnownPluginIds` with its recorded verdict from the transient unlock suites. Nothing here asks for an AAO scope.

## 7. Restore evidence

All three keys ended clear, read back through NDMF's own disable reader after the final restore. The final clean run passed. The probe touched `SessionState` keys only. It wrote no file and changed no script.
