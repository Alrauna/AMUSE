# MainTex slot refusals in the Census Lab

Privacy note: This record uses a private avatar in the Census Lab project as test input. It names no avatar, scene, renderer, material, texture, or asset path. It holds no per-renderer table. It describes the two refused slots by shader family and state, and it records only observed session counts. It writes no instance names, hashes, ports, or machine paths.

Date: 2026-09-26.
Branch: `feat/maintex-alpha-blend-support`.
Base: `main` at `ef51424`.
Investigation status on 2026-09-26: No production code had changed at the end of the investigation. The evidence came from the pinned Census Lab editor instance and from repository source. One out-of-build control experiment ran the production capture through reflection. It created no lasting state.

## Question

On 2026-09-26, the NDMF console of the most recent play-mode build reported two slot refusals that name the Main texture feature, property `_MainTex`, and one texture capture refusal on the same property. This record answers two questions. What state causes each refusal? Which part is a support gap that the `feat/maintex-support` branch should close?

## Console evidence

On 2026-09-26, the Census Lab editor console held three AMUSE warnings from the latest play-mode build. The build path ran through the VRCFury play trigger, the VRChat SDK preprocess hook, and the NDMF PlatformFinish pass.

The two slot reports share one shape. Each says that AMUSE proved nothing for a slot on slot index 1 of a renderer. Each gives the reason `AdmittedMaterialSemanticsUnknown` and the sentence that AMUSE has no proven rule for the Main texture feature, property `_MainTex`.

The capture report says that AMUSE could not read a material texture on slot 1. Its description names texture property `_MainTex` and channel Alpha. The report family is `UnavailableCapture`.

The report order identifies the owner of the capture refusal. Each renderer reports its slot refusals first and its capture refusals after classification. See `Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs:647-705`. The capture refusal sits between the two slot refusals, so the first-processed renderer owns it. An unassigned texture never produces a capture refusal. See `Packages/com.alrauna.amuse/Editor/Host/UnityMaterialEvidenceCapture.cs:1365-1400`. The capture refusal therefore belongs to the slot whose texture is assigned, and the other slot refused at the frontend rule level.

The console also held noise. An Intel-only audio plugin bundle in the VRChat SDK package fails to load on this host and repeats one error. A third slot refusal on the same avatar names a different feature and is out of scope for this record.

## Live state of the refused slots

On 2026-09-26, read-only queries against the pinned Census Lab editor instance measured both slots. Before the queries, the instance identity was confirmed. `Application.dataPath` matched the Census Lab project and not the dev repository.

One refused slot holds a lilToon Transparent outline variant. Its `_MainTex` property exists and holds no texture. Its `_Color` alpha is about 0.05. The material carries no optimizer lock flag and no property block.

The other refused slot holds a lilToon Cutout outline variant. Its `_MainTex` holds an imported 512 by 512 PNG. The import is BC7 in sRGB with ten mip levels. Streaming is on. The effective mip limit is zero. The CPU copy is not readable. Its `_Cutoff` is 0.5 and its `_Color` alpha is 1. This material also carries no lock flag and no property block.

A clarification from the same queries on 2026-09-26: both materials do have a Main Color. That property is `_Color`, and both slots assign it. On the transparent slot its alpha is about 0.05, and that alpha is the translucency of the material. The refused property is the Main Texture, `_MainTex`, which sits beside the Main Color in the same lilToon section and is a different property. The transparent slot's Main Texture holds no texture in the scene material and in its material asset on disk. The cutout slot's Main Texture is assigned, and its refusal is the separate capture failure in the finding below. The same queries found exactly one renderer and one material asset for each refused material, so no name collision confused the measurements.

The lilToon UI never labels a field with the raw name `_MainTex`. The installed vendor inspector draws the texture box inside the combined Main Color row, under the label that the language table gives the Main Color. See `Editor/lilInspector/lilMainInspectorGUI.cs:87` in the vendor package, with the property registration at `Editor/lilInspector/lilMaterialProperties.cs:54`. An empty `_MainTex` is therefore the state of an empty texture box in the Main Color row, and an author reaches it by leaving that box empty. The vendor compatibility alias `_BaseMap` was also measured on 2026-09-26. The transparent slot holds no texture there either, and the cutout slot mirrors the same texture.

## Code trace

Both lilToon families guard the texture-backed alpha claim the same way. If the captured evidence holds no assignment for `_MainTex`, the frontend records Unknown with the `UnsupportedFeature` code and the property name. See `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonTransparentMaterialSemantics.cs:486-492` and `Packages/com.alrauna.amuse/Editor/Semantics/LilToon/LilToonCutoutMaterialSemantics.cs:405-413`. The report layer formats that diagnostic as the Main texture sentence. See `Packages/com.alrauna.amuse/Editor/Build/AmuseReports.cs:100-114` and the strings in `Packages/com.alrauna.amuse/Editor/Build/AmuseReportStrings.cs:361-366`.

The transparent slot refuses at this guard. Its `_MainTex` holds no texture, so no captured assignment exists. At playback Unity binds the shader's declared default texture in this case. On 2026-09-26, a live query measured the declared default name of that shader property: `white`. The sampled alpha is therefore exactly 1, and the runtime alpha is the constant `_Color` alpha, about 0.05. That constant is provable must-stay-transparent material state. The frontend has no arm for the unassigned case, so the slot resolves Unknown. The declared default is a measured fact. That playback binds the declared default is standard Unity material behavior and was not separately measured here.

The cutout slot refuses during capture. Its texture passes the early gates of `UnityAlphaFieldEvidence.TryCapture`: it is a Texture2D with source identity, the build target is admitted, BC7 is on the format allowlist, and the mip gate passes. See `Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs:100-350`. The admitted build target is only `StandaloneWindows64`, and the Census Lab active target matched it on 2026-09-26. The refusal family `UnavailableCapture` confirms that the format gate and the mip gate did not refuse. The texture streams, so capture takes the streaming route in `Packages/com.alrauna.amuse/Editor/Host/UnityStreamingTextureEvidence.cs`. That route first reads the source image straight from disk in `Packages/com.alrauna.amuse/Editor/Host/SourceImageAlphaReader.cs`. A PNG passes its extension gate. If the reader refuses, the route falls back to a readable clone re-import.

The identity gate matters for the build case. `UnityTextureEvidence.TryGetSourceId` fails for scene-only textures that no producer attests. See `Packages/com.alrauna.amuse/Editor/Semantics/UnityTextureEvidence.cs:26-59`. A fail there yields `UnavailableCapture`.

## Out-of-build control experiment

On 2026-09-26, the production `TryCapture` overload with seven parameters ran through reflection inside the pinned Census Lab editor instance. The input was the exact live texture of the cutout slot. The bounds were inert and the cutoff was 1.0.

The capture succeeded. The refusal was `None` and the mip chain was produced. The route's temporary folder did not exist after the run. The same code, the same texture, and the same editor process therefore capture cleanly outside a build.

Code dating supports the comparison. The capture-path files in the Census Lab embedded package copy match repository HEAD byte for byte. The direct reader landed between 2026-09-08 and 2026-09-11, and the Lab copy synced on 2026-09-15. The report strings match HEAD. The refusing build ran this capture code.

## Finding and limit

Cause one is established. One refused slot holds an unassigned `_MainTex` on a lilToon Transparent family. The frontend refuses because no rule covers the unassigned case. The runtime value is provable: the declared `white` default makes the alpha the constant `_Color` alpha. A proof arm for the unassigned case is the support gap.

Cause two is partially established. The streamed BC7 texture of the other slot refused capture inside the play-mode build and captures cleanly outside it. Two mechanisms remain possible, and the evidence does not yet separate them.

The first mechanism is a build-copy difference. The pass captures the texture of the build copy. An upstream tool on this avatar can clone a material or texture during the build. A scene-only clone fails the identity gate and yields `UnavailableCapture`. The source scene keeps the original asset, so a post-build query cannot see this state.

The second mechanism is a mid-build `AssetDatabase` limit. The clone fallback writes a temporary asset, re-imports it, and deletes it. A build pipeline can restrict these operations. This mechanism requires the direct reader to refuse first. The reader is deterministic file decode plus memory work, so a refusal there is unlikely but not excluded.

The refusal record already carries the reason family and the identity flag. The report description does not print them. One report-line addition turns the next play-mode build into the separating experiment.

## Decision request

On 2026-09-26, production code had not changed. The branch `feat/maintex-support` proposes two bounded features.

Feature one: prove an unassigned `_MainTex` as its family's declared default. Only a measured `white` default qualifies. The proof composes the constant 1 sample with `_Color` alpha through the existing constant-term path. The arm can prove opaque state only when the composed value reaches the family's opaque test at every gate. Product tests cover it in the dev editor instance with the stand-in shaders. Tests never run in the Census Lab.

Feature two: print the capture reason family and the identity flag in the `amuse.texture.UnavailableCapture` description. This change carries no correctness risk and resolves the open cause-two branch on the next Lab build.

Out of scope: the third slot refusal on the same avatar, and the host plugin noise.

## Follow-up on 2026-09-27

On 2026-09-27, a play-mode build ran in the Census Lab with the `feat/maintex-support` code synced into the Lab's embedded package copy. Both shipped changes were live: the declared-default arm, and the report fields this design added.

The first cause closed. The transparent slot with the unassigned Main Texture no longer refuses. It resolves under the declared-default arm and keeps its original material, so its refusal report is gone from the console.

The second cause remains, and the new report field answered the open question. The cutout slot's capture refusal still fires, and its description now reads: the refusal reason is `UnavailableCapture`, and the texture has no source identity. That resolves the investigation's two candidate mechanisms in favor of the first. The texture the capturer sees on the build copy is an object no project asset backs, so the identity gate refuses before any capture route runs. The mid-build `AssetDatabase` limit mechanism is excluded for this texture, because the run never reached the clone route.

The build copy is the in-memory avatar the play-mode build hands to NDMF, and some pass in that chain upstream of AMUSE replaces the slot's assigned imported texture with a clone. Which pass makes the clone is unproven; what is measured is the clone's absence of source identity.

Scope: this refusal is outside `feat/maintex-support` by design. The spec declared the build-time capture refusal a non-goal and shipped the report change precisely to produce this measurement. A follow-up design must decide how to treat build-copy textures without source identity: reconstruct identity from the authoring asset the clone came from, or keep the refusal with sharper guidance. That is a new dated spec, not a change to this branch.

The avatar's other refused slots name other features and remain out of scope.
