# Gate 0 fixtures: pass position, appended slots, and protected mip levels

Date: 2026-11-19.
Status on 2026-11-19: Throwaway EditMode probes ran in the dev editor instance. They close Gate 0 items 3, 4, and 5 of `docs/superpowers/specs/2026-11-19-minimal-atlasing-design.md`. Gate 0 item 6 closed from source review on the same date. This record changes no product code.

## Method

One throwaway test class held two probes. It was not a product test.

The build probe used a dedicated test platform. A test plugin on that platform ran the five production pass bodies in production order. It then ran one probe pass in the same sequence after `TransientUnlockWindowClose`. That is the position of the atlas pass in the minimal design. The barrier step used the existing test seam barrier of `AlphaSeparationApplyTests`. That seam supplies supported host facts and the verified fixture seams. The test platform is not the VRChat platform, so the production lifecycle gate refuses it without the seam.

The avatar was the fixture of `AlphaSeparationPersistenceTests.AssignedGeneratedObjectsPersistThroughNdmfSerialization`. It has one renderer with three slots after alpha separation:

- Slot 0 has a material swap curve. Alpha separation converts it whole.
- Slot 1 keeps its transparent source material.
- Slot 2 is the opaque slot that the split appended.

NDMF persistence pointed at a test-owned folder. The probe pass made these objects:

- a new mesh copy
- one new material per slot
- one new 4 by 4 mipmapped texture, assigned to the slot 2 material

It assigned the mesh with `sharedMesh` and the materials with `sharedMaterials`. It did not call `SaveAsset`.

The mip probe made a 64 by 64 RGBA32 texture with mipmaps. It filled level 0, then let Unity generate the mips. It marked levels 2 and lower with a red marker, so a later regeneration would show. It then overwrote levels 0 and 1 with blue and applied without mip generation. Then it compressed the texture to BC7 with `EditorUtility.CompressTexture`.

## Observations

The control test `AssignedGeneratedObjectsPersistThroughNdmfSerialization` passed in the same environment. Vendor lock check: every package matched its lock on 2026-11-19.

Item 3, writes after the window close:

- After the build, the renderer held the probe mesh and all three probe materials. This includes slot 0, the slot with the swap curve.
- The probe mesh, the three probe materials, and the probe texture were persistent assets inside the test-owned folder. NDMF serialization found the texture through the material reference.

Item 5, appended slots:

- The probe pass saw 3 material slots and 3 submeshes.
- Slot 2 held the opaque material that alpha separation appended.

Item 4, protected levels:

- Before compression, level 0 read back blue and level 3 read back the red marker.
- After BC7 compression, levels 0 and 1 read back blue and level 3 read back the red marker.
- Unity did not regenerate the mip tail on apply without mip generation or on compression.

Counts: the build probe ran 1 test with 1 passed. The mip probe ran 1 test with 1 passed. A combined run of the build probe and the control ran 2 tests with 2 passed. Earlier runs of the build probe failed during probe setup, before any observation. The probe plugin was first missing its `ExportsPlugin` attribute. Before that, the probe used the production barrier, which refuses a test platform.

## Limits

- The probes ran on a test platform. Vendor passes that select only on the VRChat platform did not run. A vendor pass that runs after AMUSE in the PlatformFinish phase can still change the result.
- The barrier used the verified fixture seams, not production attestation.
- Slot 0 kept the probe material after the build. Its committed swap curve still names the alpha separation clones. An atlas write to a slot with a swap curve does not change what the curve plays. The minimal design defers material swaps.
- The mip probe used the CPU texture path and BC7 only. It did not test other formats, sRGB sampling, or GPU upload.

## Consequences for the minimal design

- The atlas pass position after `TransientUnlockWindowClose` holds. No later step in the probe build undid its writes.
- The atlas pass reads final slot indices from the live renderer. Appended slots are already present at that point.
- A page can keep Unity's generated mip tail while it overwrites only the protected levels.
