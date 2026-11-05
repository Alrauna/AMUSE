# Swap-in property block writer diagnosis

Privacy note. This record names no machine, account, port, editor instance, or private avatar. Test names and package names are product content. Every run in this record ran in the dev editor instance of this repository on 2026-10-25. Product tests never run in the private Census Lab project.

## Question

The stage 1 baseline of 2026-10-25 lists `Alrauna.Amuse.Tests.Editor.Build.TransientUnlockSwapInTests.ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial` as failed. The first failing assertion is the guard:

```
fixture guard: the binding on the locked material's own path must refuse
Expected: 1
But was:  0
```

The guard counts `LockedMaterialAnimationMismatch` refusals. A count of zero means the admission never recorded that cause. The decision reads the property block fact first, so a live block on the renderer would answer with `LockedMaterialPropertyBlock` instead. This record names the writer of that block.

## Method

A throwaway EditMode fixture ran outside the committed suites on 2026-10-25 and was deleted after the runs. It mirrored the failing fixture exactly: the opted root with the descriptor, the two locked stand-in materials with the rename suffix, the two renderers on distinct paths, and the hand built clip with one object curve on the path A renderer and one float curve on the path C renderer. The build ran through `AvatarProcessor.ProcessAvatar` on the transient unlock test platform, exactly as the fixture under test does.

The throwaway file also registered two temporary NDMF probe plugins, both deleted after the runs. One plugin ran one extension-free pass per build phase after the vendors of that phase, plus one PlatformFinish pass that sat immediately before the window test plugin. The other plugin ran one PlatformFinish pass inside an animator extension scope and dumped the animation index bindings for both renderer paths. Each probe point recorded `Renderer.HasPropertyBlock()`, the two candidate float values, and the live slot materials.

Controls. One run disabled the index probe plugin, so no probe opened an animator scope before the swap in. One run disabled both Modular Avatar plugins. One run built the minimal fixture of the task brief through `AvatarProcessor.ProcessAvatar(root, AmbientPlatform.DefaultPlatform)` with every vendor enabled.

## Observed writer

The writer is the editor animator rebind that follows the animator controller reassignment in the NDMF animator extension commit. The chain, read from the locked vendor sources:

1. `VirtualControllerContext.OnDeactivate` commits every virtual controller.
2. The commit calls `IPlatformAnimatorBindings.CommitControllers`.
3. `GenericPlatformAnimatorBindings.CommitControllers` assigns `Animator.runtimeAnimatorController` on the root animator.
4. The controller assignment rebinds the editor animator. The rebind applies the bound curve values of the clip onto the live renderer. An object reference curve lands as the time zero material over the slot array, the form the finish plugin already documents at `Editor/Build/AmusePlatformFinishPlugin.cs:208-216`. A float curve on a `material.` name can land as a renderer `MaterialPropertyBlock`, within the boundary named below the chain.

Boundary of the block form, probe runs of 2026-10-25: a float curve lands as that block only when the curve was already part of the clip when the animator first received its controller. The sibling `AnimationMismatchRecordsTheRefusalOnEverySlot` writes its float curve after its swap helper assigned the controller, so no block lands there and its animation mismatch refusal stands, while the failing fixture writes every curve before the controller assignment, so the block lands. The engine path between the bound curve and the block was not determined further.

No installed package writes a renderer property block during the build. A search over the locked vendor sources found `SetPropertyBlock` callers only in the d4rk optimizer merge path, which runs outside NDMF, and in two runtime scripts that no build pass executes.

## Probe results, run of 2026-10-25

The mirror run reproduced the failure before any probe pass executed. The first run of the throwaway fixture answered `Incompatible` for every probe pass because NDMF runs unmarked plugins on the VRChat platform only. That run still recorded the swap summary `refusedBlock=1`, `refusedMismatch=0`, `clones=1`, `swapped=1`, `pairs=1`, and a live block on the path C renderer after the build. The probes were then marked to run on all platforms and the run repeated.

Mirror run, probes active:

```
point:pre-build renderer=AMUSE path A host block=False slots=PathACape
point:pre-build renderer=AMUSE path C host block=False slots=PathCCape
phase:FirstChance renderer=AMUSE path C host block=False
phase:PlatformInit renderer=AMUSE path C host block=False
phase:Resolving renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=1
phase:Transforming renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=1
phase:Optimizing renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=1
point:finish renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=1
summary refusedMismatch=0
summary refusedBlock=1
point:post-build renderer=AMUSE path A host block=False slots=PathACape
point:post-build renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=1
```

The block first appears inside the Resolving phase. It carries the time zero value of the fixture float curve under the renamed alpha name. It is present at the finish probe, which runs immediately before the window test plugin, so the swap in reads a live block and refuses with `LockedMaterialPropertyBlock`.

The index probe run confirms the animation index sees the whole clip. The float binding sits on the path C renderer and the object curve sits on the path A renderer. The binding collection is not the failure.

Control run with both Modular Avatar plugins disabled:

```
phase:Resolving renderer=AMUSE path C host block=False
phase:Optimizing renderer=AMUSE path C host block=False
point:finish renderer=AMUSE path C host block=False
summary refusedMismatch=1
summary refusedBlock=0
point:post-build renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=1
```

With Modular Avatar disabled, no phase writes the block, the refusal returns to the animation mismatch, and the whole original expectation of the fixture holds. The block that appears after the build comes from the animator scope commit of the window test plugin itself, which runs after the barrier. Modular Avatar 1.18.7 of the locked vendor set runs on the transient unlock test platform, and its Resolving phase animator pass is the first commit in the build. That commit, through the chain above, is the mid build writer.

Default platform run per the task brief, minimal fixture, vendors enabled:

```
point:pre-build renderer=AMUSE path C host block=False slots=ProbeCape
phase:PlatformInit renderer=AMUSE path C host block=False
phase:Resolving renderer=AMUSE path C host block=True _AlphaForceOpaque=0 _AlphaForceOpaque_S=0
phase:Optimizing renderer=AMUSE path C host block=True
point:post-build renderer=AMUSE path C host block=True
```

The default platform build shows the same phase shape. The block appears during Resolving. The probe names read zero there because that stand-in material carries no rename suffix, so no shader property exists under the suffixed name and the value lands nowhere the probe reads.

Second product of the same rebind. The mirror runs show the path A slot holding a `PathACape` material after the build while the recorded swap summary reports `swapped=1`. The swap in wrote the clone into the slot during the barrier, and its closure remap moved every object curve keyframe of that material onto the clone with times preserved. The window test plugin scope commit after the barrier reassigned the controller, and the rebind re-applied the remapped time zero value, which is the clone, over the slot. The rebuilt fixture read the live slot after the build and confirmed the instance there is not the original locked material. The live slot and the recorded summary agree in this fixture because the remap ran before the commit. The recorded summary stays the authoritative proof of the write, exactly as the finish plugin records its writes against the same rebind.

## Invariant outcome

The load bearing invariant of design item V9.1 asked whether the writer can carry an alpha relevant value on a proven slot. It can. The mirror run observed `_AlphaForceOpaque_S=1`, the renamed alpha property, in a live block on a renderer whose slot holds a recognized locked material. The check order at `Editor/Build/LockedMaterialAdmission.cs:44-47` therefore stays. Any reorder that reports the animation mismatch first, or that admits the blocked locked slot, would ship the observed alpha value past the gate.

The pure decision already refuses this shape. The new guard `LockedMaterialAdmissionTests.DecideRefusesABlockedLockedSlotWithCleanBindings` passed on its first run on 2026-10-25 and is recorded as characterization.

## Fixture consequence

The task brief branch for a block that exists before the build does not apply. The pre build probes show no block on any renderer. The block is written inside the build, during the Resolving phase, so a clear before the build changes nothing.

The branch for a block the build itself writes applies. The fixture `ABindingOnAnotherRendererPathDoesNotRefuseThisLockedMaterial` now expects `LockedMaterialPropertyBlock` for the locked material on the path of the float curve, asserts the planted block after the build, and cites this record. The locked material on the untouched path still swaps in, proven by the recorded swap summary and by the live slot, which holds the clone because the closure remap moved the curve onto the clone before the rebind ran.
