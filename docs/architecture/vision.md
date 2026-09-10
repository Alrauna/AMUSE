# AMUSE Vision

## Purpose and scope

AMUSE aims to reduce the rendering and resource cost of VRChat avatars without destructive changes to your source assets. Material and shader understanding is its main purpose. Geometry and animation changes support that purpose. The goal is automatic optimization that keeps the intended appearance and avatar controls within stated compatibility limits. AMUSE must cooperate with other avatar tools rather than replace every type of avatar optimization.

This document defines the long-term direction, not a completed feature set or an implementation plan. In each section, the first paragraph explains the purpose for artists. The second paragraph states the engineering direction or current implementation. Current-status statements describe the code at `e4f281d`, with package version `0.1.0-pre.1`. Current code establishes what exists, but it does not determine the limits of the long-term vision. Historical plans and architecture diagrams do not authorize new subsystems or changes to the correctness contract.

## Useful optimization with a stated contract

The default goal is to keep your avatar looking and functioning as intended in supported VRChat use. This does not require every internal detail to stay identical. For example, a solid part of a transparent material can use an opaque material instead. This changes how Unity draws the part. Each optimization must state which differences it permits and which effects it preserves.

A transformation contract states required conditions and permitted changes. AMUSE must establish those conditions before it changes a candidate, which is a proposed optimization target. Exact proof is useful for bounded mathematical questions, but it is not a requirement to prove the entire Unity environment. Version-specific observations and explicit compatibility assumptions can also support a contract. An unsafe transformation violates that contract or lacks a required condition. An unnecessary refusal is a coverage defect, even when it is safer than an incorrect change.

## Policy, evidence, and accepted risk

A policy is a set of rules for permitted changes. The long-term default is practical preservation of appearance and avatar controls. A stricter compatibility policy can preserve additional behavior that unusual effects require. An experimental policy can permit stated quality or behavior changes only through an explicit user choice. The interface must explain these differences instead of using an undefined promise such as “lossless.”

Alpha controls transparency. The current product has individual controls, not these three named policies. A mip level is a smaller version of a texture. The default alpha policy limits evidence to mip level 4 and a minimum size of 128 texture pixels. Thus, the current proof does not cover every texture level that the graphics processor can sample. These are policy limits, not a measured guarantee of equal appearance at every distance, as [the component](../../Packages/com.alrauna.amuse/Runtime/AmuseAvatarOptimizer.cs) makes explicit.

## Local uncertainty and visible errors

If AMUSE cannot understand one material, it must keep that material unchanged where the proposed change depends on the missing information. This must not stop unrelated optimizations on the avatar. The same rule applies to an unsupported effect or animation. When the missing information affects several parts, AMUSE must keep those dependent parts unchanged together. The report must distinguish this choice from an error in AMUSE.

`Unknown` means that the available evidence does not establish a required fact. A known incompatible feature, a user exclusion, and an internal defect are different outcomes. Refusal scope must match the dependency: output, material slot, renderer, or avatar. A renderer is the Unity component that draws a mesh. Increased uncertainty must never permit a stronger transformation under the same policy. Programming errors must stop the build instead of becoming silent unsupported results.

## Simple use and useful explanations

The intended workflow is to add AMUSE to the avatar root and build the avatar. AMUSE must determine what it understands, what it can change, and which changes are worth their cost. You must be able to disable an optimization family or exclude an object, material, or branch of the hierarchy. Reports must explain both changed and preserved parts in language that supports a useful next action. Normal use must not require you to choose the order of internal algorithms.

The current [component inspector](../../Packages/com.alrauna.amuse/Editor/AmuseAvatarOptimizerEditor.cs) provides a global disable control, alpha policy controls, animation-slot tolerance, and the last build status. NDMF reports provide refusal messages and applied counts. Per-object exclusions, independent controls for several optimization families, and named compatibility policies remain future work. Future detailed reports must connect a decision to its required facts, policy assumptions, and generated output. Current reporting does not imply a completed evidence browser or a detailed explanation for every preserved triangle.

## The effective build avatar

Your source avatar is not always the avatar that Unity uploads. Modular Avatar, VRCFury, and other tools can add or replace objects, animations, meshes, and materials during the build. AMUSE must examine the result of those changes rather than predict it from the original scene. Source assets remain your editable inputs. AMUSE changes the build copy and its own generated assets, not those source assets.

The effective build avatar is the avatar after relevant build transformations. AMUSE must capture evidence close to the operation that consumes it. Immutable evidence cannot change after capture, but the live Unity objects can still change. Before application, AMUSE must make sure that the live target still satisfies the required conditions. The current [build plugin](../../Packages/com.alrauna.amuse/Editor/Build/AmusePlatformFinishPlugin.cs) captures committed animation state and prepares alpha separation in `PlatformFinish`. This placement serves the current feature, not a rule that every future feature must use one final phase.

## Animation and runtime state

An optimization must remain valid when you change an outfit, move a slider, or activate an avatar effect. A material that looks solid in the editor can become transparent through animation. Different materials can also occupy the same material slot at different times. AMUSE must account for the relevant possibilities before it changes that slot. Unknown animation behavior must stop the changes that depend on it.

The long-term model includes material swaps, animated properties, visibility, and supported systems that change effective rendering behavior. Current [state admission](../../Packages/com.alrauna.amuse/Editor/Analysis/AdmittedMaterialStates.cs) combines captured defaults and relevant animation evidence per material slot. It supports bounded cases and requires exact agreement for relevant numeric values. It does not solve exact Animator reachability or model arbitrary control from a world. Future analysis can distinguish simultaneous states from alternatives when a real optimization needs that distinction. Two outfits that never appear together still need separate texture storage unless an explicit runtime selection mechanism changes that requirement.

## Material meaning instead of property names

Two materials can use different controls yet produce the same visible result. Two controls with the same name can also do different things in different shaders. AMUSE must understand the effect of a property, not treat its name as proof. This understanding can let one description support several optimizations. A shader feature that AMUSE does not understand must remain outside claims that depend on it.

Semantics are facts about the behavior of an input. The current [MaterialSemantics](../../Packages/com.alrauna.amuse/Editor/Semantics/MaterialSemantics.cs) describes bounded forms of base color, alpha, emission, and normals. It is not a database of shader properties or a universal shader program. Structural facts and shader-specific conversion rules can support a transformation directly when this is sufficient. A separate shared domain is justified only when real consumers require it. Facts must remain separate from instructions to bake, combine, or otherwise change a material.

## Shader support and version assumptions

The goal is useful support for common shaders and effects, with clear limits for each supported configuration. A familiar shader name alone does not establish compatible behavior. A new version, generated variant, or modifier can change what the shader does. AMUSE must explain when it relies on a tested source and when you accept an additional assumption. Permission to try an untested version does not make that version tested.

The current [frontend selection](../../Packages/com.alrauna.amuse/Editor/Semantics/UnityMaterialSemantics.cs) serves Poiyomi and lilToon through separate implementations. Each frontend, which interprets one shader family, owns its equations, feature conditions, and source identity rules. Current support includes Poiyomi Toon 9.3.64 and lilToon 2.3.0 through 2.3.4, with configuration-specific limits. The build also offers per-build consent for certain untested host versions and recognized shader sources, then applies existing family rules. Unknown shader families remain unsupported, and batch builds refuse when consent is required. Future support must preserve this distinction between established behavior and an explicitly accepted assumption.

## Alpha separation as the first working optimization

AMUSE can move visually solid triangles from supported cutout or transparent materials to generated opaque materials. Triangles without the required evidence keep their original material. A mixed part gains an additional material slot, while a fully opaque part can replace its material without a split. This can reduce transparent rendering work, but an extra draw call also has a cost. A draw call is a command to draw geometry.

The current implementation connects texture evidence, exact triangle classification, per-slot state analysis, shader-specific opaque conversion, and nondestructive application. Cutout and blended transparency remain distinct source modes with different conditions. Mixed slots retain original indices and gain appended slots, while supported material-swap curves receive corresponding opaque values. [AlphaSeparationApply](../../Packages/com.alrauna.amuse/Editor/Build/AlphaSeparationApply.cs) also handles wholly opaque slots without unnecessary mesh replacement. The minimum opaque coverage control suppresses small mixed splits and defaults to 25 percent by triangle count. Alpha separation demonstrates the architecture, but it does not define the full product.

## Texture evidence and sampling behavior

A source image does not always describe the texture that the avatar draws. Compression, filtering, wrapping, and smaller texture levels can change the sampled result. AMUSE must account for the texture behavior that matters to the proposed change. It must not alter your texture import configuration to obtain a favorable result. A texture that AMUSE cannot measure through a supported route must not become assumed evidence.

Current [texture evidence capture](../../Packages/com.alrauna.amuse/Editor/Host/UnityAlphaFieldEvidence.cs) uses distinct routes for resident imported textures, streaming textures, and recognized generated textures. Internal evidence records the property that classification needs rather than reproducing the original image format. The evidence route, sampling model, and selected policy jointly limit the claim. Future texture operations must account for color interpretation, channels, compression, filtering, wrapping, and relevant mip levels. Shared analysis must consume these facts without depending on mutable editor objects. Support for one generated-texture pattern does not establish support for every tool-generated texture.

## Material normalization and combining

The long-term goal is to combine materials that can share a result, even when their original values differ. For example, one material can color a shirt red while another colors trousers blue. If the shader equations permit it, AMUSE can put those colors into generated textures and use one neutral tint. This moves a difference from material values into texture data. It is more useful than combining only materials that are already identical.

Normalization converts different representations to a supported common form. Future combining must distinguish constant differences, texture-bakeable differences, animated differences, and material-wide or render-state constraints. Baking stores a calculated result in a texture. Depth, stencil, pass structure, view-dependent effects, and time-dependent effects cannot automatically become static texture data. A merge requires compatible behavior across the relevant runtime states and an explicit policy for permitted differences. General material equivalence, combining, and tint baking are not current product features.

## Texture atlases from actual use

An atlas is a texture that contains several image regions. AMUSE aims to build atlases from the regions that geometry actually uses, not only pack rectangular source images. This can reduce unused texture space and support material combining. Color, normal, emission, and mask textures must remain aligned where their uses depend on the same mapping. Edges must stay correct at the texture detail levels covered by the policy.

UV coordinates map mesh positions to texture positions. Future atlas analysis must account for UV islands, which are connected regions of that mapping. Packing must preserve relevant sampling relationships across textures, material states, and generated UV changes. Different outputs can require different sizes, formats, color interpretation, and compression despite shared spatial mappings. Padding and edge extension must derive from the supported filtering and mip requirements rather than an unexplained fixed border. General UV-island analysis, atlas packing, texture generation, and atlas application remain future work.

## Shader simplification and modifier support

A shader can contain features that a particular avatar never uses. AMUSE aims to remove redundant work where it understands the effect and the relevant avatar states. It must also account for supported effects that other tools add to materials or shaders. It cannot treat an unfamiliar modifier as harmless. Shader simplification must preserve the behavior covered by the selected policy.

Future analysis can identify unused features, redundant configuration, and dependencies between shader operations. Shader-specific knowledge must remain with the relevant frontend unless independent consumers establish a shared concept. A richer feature model can describe inputs, outputs, coordinate spaces, operation order, passes, and side effects when a concrete transformation requires them. The current narrow output model does not provide general feature elimination or modifier composition. Automatic HLSL analysis, where HLSL is a shader programming language, remains a possible later method rather than a prerequisite. New analysis must first demonstrate useful results against supported implementations.

## Coordinated choices and actual benefit

Fewer materials do not always make an avatar faster. An atlas can increase texture memory, and an opaque split can increase draw calls. One optimization can also enable or prevent another. AMUSE must choose compatible changes with useful combined results rather than apply every available change. If a valid change costs more than it saves, AMUSE must be able to leave it unapplied.

Candidate discovery and candidate selection are separate responsibilities, not mandatory frameworks. The current product has local selection, including a mixed-split threshold, rather than a general cost model. Future planning can consider draw calls, shader passes, overdraw, texture memory, vertex growth, animation cost, and platform limits. Overdraw is repeated drawing over the same screen area. A shared planner becomes necessary when real transformations conflict or enable one another. Cost estimates can reject an allowed transformation, but they cannot establish missing correctness conditions or prove a universal performance gain.

## Controlled application through NDMF

Optimization must preserve your source assets and leave the built avatar internally consistent. A material change must still match its mesh and animations. If another build tool changes a required input, AMUSE must not blindly apply an old plan. Expected incompatibility must preserve the affected target. An internal application error must not produce an apparently successful build.

The application sequence is capture, analyze, prepare, revalidate, then minimal apply. Current application checks surviving candidates, removes unused AMUSE-generated objects, and writes animation curves before mesh and material references. NDMF supplies build state, animation services, generated-asset handling, and replacement relationships. AMUSE must use those facilities instead of constructing a parallel build framework. The boundary limits mutation, but it does not promise rollback after an unexpected error. Pure reasoning remains separate from live mutation without a speculative standalone library or second-host framework.

## Ecosystem compatibility

AMUSE must remain useful on avatars assembled with common nondestructive tools. Compatibility means predictable build order, correct references, and clear exclusions where tools affect the same data. It does not mean that two optimizers can freely change the same materials in every order. The long-term targets include Modular Avatar, VRCFury, and AAO: Avatar Optimizer. Known conflicts must produce practical guidance rather than a general claim that an avatar is unsupported.

Compatibility claims require evidence for the relevant tool versions, build path, and affected domain. Existing NDMF integration and recognized AAO texture patterns are narrower than a completed compatibility matrix. The [README](../../README.md) explicitly states that combined use with d4rkAvatarOptimizer is not validated for 0.1.0. Current code admits supported upload and NDMF Play mode paths, with PC as the product target. Future cooperation can use explicit ordering, local exclusions, or existing ecosystem APIs when a real conflict requires them. Another host or platform is not a current architectural requirement.

## Progress and development direction

AMUSE is beyond analysis-only experiments: the current package contains a working path from avatar opt-in to build-copy alpha separation and reports. It does not yet provide general material combining, atlases, shader simplification, or coordinated selection across optimization families. The next stage must expand useful results without turning every new case into a new framework. Measured avatar benefit must determine which additional optimization comes next. The long-term material-understanding goal remains broader than the current alpha feature.

The sequence is evidence-driven rather than a fixed release schedule. Strengthen the current transformation and compatibility limits, then select another concrete consumer of material or texture understanding. Add materially different shader support where it exposes a real missing capability or improves useful coverage. Introduce shared planning only when implemented transformations require coordination. During `0.x`, internal interfaces can change as real consumers establish their requirements. A public extension API requires stable evidence and actual use, not only a planned version number.

## Future shader-author integration

A shader author can eventually describe supported behavior once instead of writing a separate integration for each optimizer. Basic descriptions must be useful without requiring a full model of every shader operation. More detailed descriptions can enable deeper optimization when necessary. This gives shader authors a way to expand AMUSE support while keeping optimization decisions inside AMUSE.

First-party frontends must establish the contract before AMUSE publishes a stable third-party API. A simple shader can use declarative facts, while a complex shader can require code-backed interpretation. The format remains undecided because current consumers do not establish a public contract. Semantic APIs must describe behavior, supported inputs, source assumptions, and diagnostics rather than authorize individual optimizations. Richer capabilities must not force basic integrations to implement an entire feature graph. Public stability must preserve useful boundaries rather than freeze the current four-output model without sufficient evidence.

## Possible shader reconstruction and portability

The same understanding can eventually help compare shader features or transfer an effect between shader families. Similar feature names do not guarantee the same appearance. A rim-light effect can depend on normal processing, masks, lighting, and where the shader adds its result. These are possible future uses, not promised AMUSE features or equal goals beside avatar optimization. They must not delay useful optimization or dictate its current architecture.

Future reconstruction must account for the full relevant dependency set and distinguish equivalent, approximate, incompatible, and unknown results. A feature description and a particular source implementation are separate identities. Copying source code also requires permission under its license, independently of whether AMUSE understands the behavior. Image comparisons can expose incorrect mappings but cannot prove equality for every possible input. If a richer internal representation becomes necessary, it must serve AMUSE analysis rather than assume Unity Shader Graph is the required model. Cross-shader reconstruction and feature transfer remain conditional on demonstrated user value and technical feasibility.

## Evidence, privacy, and success

Success means useful savings on real avatars, predictable behavior within the stated policy, and explanations that help you resolve unsupported cases. Build time and memory use also matter because optimization is part of your normal workflow. AMUSE must not publish private avatar content to demonstrate its results. Public examples must use assets that their authors permit others to use.

Each transformation needs evidence at the narrowest layer that can expose an incorrect result. The repository contains EditMode tests for classification, state handling, host behavior, and build application, but their existence is not a current passing result. Public deterministic fixtures must protect important contracts, while authorized private observations help identify missing real-world cases. Visual comparisons and benchmarks support bounded claims, not universal equivalence or universal speed improvements. Only privacy-reviewed aggregate results can leave private research by default, and the research package must never enter the product package or VPM listing. Development succeeds when evidence improves useful coverage without weakening the stated contract or adding architecture with no present consumer.
