# Console reporting hardening for build-copy objects

Privacy note: This design comes from two Census Lab investigations on 2026-09-27. The investigations name no avatar, renderer, material, or texture, and this document repeats none of them. Upstream tools are named by product, and code by repository-relative paths.

Date: 2026-09-27.
Branch: `feat/console-report-hardening`.
Base: `main` at `0052076`.
Investigations: `docs/superpowers/investigations/2026-09-27-maintex-build-copy-clone-investigation.md` and `docs/superpowers/investigations/2026-09-27-console-reporting-accuracy-investigation.md`.
Status on 2026-09-27: design awaiting implementation. No production code had changed.

## Problem

AMUSE's console reports are honest about captures and refusals, but four defects make them wrong or misleading on the avatars this repository aims to support, and two behaviors read as defects because nothing documents them.

Defect one: the avatar summary's third number undercounts. The sentence says "{2} renderers kept everything original", but the argument is the renderer-scope refusal counter. A renderer whose every material slot was refused at slot level counts as analyzed, moves zero triangles, and never appears in the summary. The 2026-09-26 avatar had exactly this shape and the summary read healthier than the run was.

Defect two: the last-build status store cannot surface. The store records under the processed build copy's instance ID. NDMF strips the `AmuseAvatarOptimizer` component, the only reader, from every processed copy, and play builds destroy the processed copy. The summary hint "Open this component to see the same status." cannot be fulfilled on play and upload builds.

Defect three: material lines name build-copy objects. Slot-refusal lines print the offender material's project path or name. Upstream tools this repository supports clone materials during the build, and the clones have no project path, so the report falls back to the clone's name and never points at the authoring asset.

Defect four: the no-identity capture hint gives false advice. When an upstream build step replaces a material texture with an in-memory copy, the report says the texture has no source identity and the hint says "Check that the texture is a real imported asset." The texture is fine; the build replaced it. The known producer is Avatar Optimizer's texture-atlas setting.

Documentation gap one: one material on several slots prints one identical capture-refusal line per slot. This is deliberate, but nothing says so. Documentation gap two: NDMF prints "changed outside of NDMF animator services; cloning a second time" lines inside AMUSE's pass window when an upstream writer has churned the descriptor's controllers. A reader attributes the noise to AMUSE. AMUSE cannot reword NDMF's log, but it can explain the lines.

## Approach decisions

### Decision one: the untouched-renderer count

Approach A, chosen: compute the invariant on the build state. Every renderer a build processes either receives an applied write or keeps everything original, so `untouched = analyzed + renderer-refused - applied`. One computed property, one changed argument at the call site, and the sentence's plain meaning becomes true.

Approach B, rejected: reword the sentence to name renderer-scope refusals only. The text gets longer and still hides every fully slot-refused renderer, which is the case the 2026-09-26 run exposed.

Approach C, rejected: add a fourth number for slot-refused renderers. The summary grows for a fact the per-slot lines already carry, and the reader must do the arithmetic.

### Decision two: the status store

Approach A, chosen: one session-scoped slot for the latest build, shown by every `AmuseAvatarOptimizer` inspector. The recorded text already names the build path, so the display is honest. The inspector surface survives on all build paths because the store no longer depends on object identity.

Approach B, rejected: key the store through NDMF's object registry, resolving the processed root to its source object, as the 2026-09-27 accuracy investigation suggested. Deeper analysis rejects it, and this corrects that suggestion: on a play build the processed root is the play-start clone that VRCFury made before NDMF's registry existed, and no producer registers it. The registry cannot key the exact path that motivated the fix. Registry resolution stays in this design for material naming, where producers do register.

Approach C, rejected: drop the store and keep only the NDMF error window. The window holds the summary, but the inspector surface costs almost nothing once the store is unkeyed, and the hint text can finally tell the truth.

### Decision three: registered-source naming for materials

Approach A, chosen: resolve at capture time through a delegate seam. The capture passes a lookup that maps a live material to its registered source object, and the captured record stores the source's path and name as plain strings. Evidence records stay free of live Unity objects, the report code is unchanged, and tests substitute the lookup, following the house seam pattern.

Approach B, rejected: resolve at report time. The report helpers receive the captured record, not the live material, so report-time resolution needs either a live reference inside the evidence or a new live-parameter thread through every report call site.

Approach C, rejected: leave the naming as it is. Reports would keep naming clones for exactly the upstream tools this repository targets, and Avatar Optimizer already registers its material replacements, so the registry holds the answer.

## Design

### The untouched-renderer invariant

`AmusePlatformFinishState` gains one computed property:

```csharp
internal int UntouchedRendererCount =>
    AnalyzedRendererCount + SemanticallyRefusedRendererCount
    - AppliedRendererCount;
```

`AlphaSeparationApply.Execute` passes that property as the summary's third argument instead of `SemanticallyRefusedRendererCount`. The `AvatarSummary` signature and the summary strings are unchanged. The property's doc comment states the invariant: analyzed plus renderer-refused renderers split exactly into applied and untouched, so the summary can no longer hide a refused renderer. `AppliedRendererCount` becomes load-bearing; it was already maintained by the apply pass.

Edges: a renderer refused at renderer scope never reaches preparation, so it cannot also be applied, and the two sets do not overlap. Renderers the barrier refused before analysis appear in neither analyzed nor untouched; their refusal line is their report, unchanged.

### The status store

`AmuseBuildStatusStore` becomes a single session slot:

```csharp
internal static void Record(string summary);
internal static bool TryGet(out string summary);
internal static void Forget();
```

`Record` replaces the slot, so the inspector always shows the latest build. `AvatarSummary` keeps its signature and stops deriving a key from `avatarRoot.GetInstanceID()`. The inspector calls `TryGet` unconditionally and shows the stored text. The hint string becomes: "This status shows in every AMUSE Avatar Optimizer inspector in this editor session." The recorded text keeps its build-path prefix, so a reader can tell a play run from an upload. The latest-only rule is deliberate: the store describes the editor session's most recent AMUSE build, not a per-avatar history. The old manual-bake open question from the investigation dissolves, because the display no longer depends on which object survived.

### The no-identity capture hint

The `amuse.texture.UnavailableCapture:hint` string becomes:

> When the report says the texture has no source identity, an upstream build step replaced the material texture with an in-memory copy. The known producer is the texture-atlas setting of the Trace and Optimize component of Avatar Optimizer. AMUSE kept the affected triangles on the original material. When the report says the texture has a source identity, make sure that the texture is a real imported asset and that the project supports texture capture.

The `NonResidentMips` and `UnsupportedFormat` hints keep their current text. Their identity conditionals are dead text today, because the identity gate refuses before those gates run, but retouching them is out of scope here.

### Registered-source naming for materials

A small Build-module helper wraps the read-only registry lookup:

```csharp
internal static class RegisteredSourceIdentity
{
    internal static UnityEngine.Object Resolve(
        UnityEngine.Object source)
    {
        var registry = ObjectRegistry.ActiveRegistry;
        var reference = (registry as IObjectRegistry)?
            .GetReference(source, false);
        if (reference == null)
        {
            return null;
        }
        var resolved = reference.Object;
        return resolved != null && resolved != source ? resolved : null;
    }
}
```

The rule it enforces: resolve only replacements a producer registered. The `create:false` lookup returns null for an unregistered object instead of creating an entry. This matters beyond tidiness: a created entry blocks a later `RegisterReplacedObject` for the same object, which is the exact failure class the 2026-09-08 registry design fixed inside AMUSE. The static `GetReference` lookup always creates, so the design never uses it for this check.

The Semantics capture grows one delegate, following the house seam pattern:

```csharp
internal delegate UnityEngine.Object RegisteredSourceLookup(
    UnityEngine.Object source);
```

`TryCaptureClosedAlphaMaterials` and the unsupported-material construction site take it as an optional trailing parameter that defaults to null. At both sites, the live material resolves through the lookup before `AssetDatabase.GetAssetPath` and `name` are read: when the lookup returns a different object, that object's path and name fill `MaterialPath` and `MaterialName`; otherwise today's behavior holds. The captured record stays strings-only. The Build call sites pass `RegisteredSourceIdentity.Resolve`. The apply pass resolves the live `offendingMaterial` through the same helper before `SlotSeparationRefusal` reports it. Because the parameters are optional, the verified test seams compile unchanged.

Effect: when a producer registered the replacement, a refused slot names the authoring asset's path; when nothing registered it, the report says exactly what it says today. AMUSE never fabricates identity from names, and this design keeps that rule.

### Documentation

The README gains a short "Reading the console reports" section with three facts: one material on several slots prints one line per slot by design; NDMF "cloning a second time" lines next to AMUSE passes are NDMF re-clones of controllers an upstream tool changed, not AMUSE failures; the last-build status shows in every AMUSE Avatar Optimizer inspector for the editor session.

## Testing

Product tests run in the dev editor instance through the Test Runner, EditMode mode. Tests never run in the Census Lab.

- The untouched invariant gets a RED state-level test: analyzed two, applied one, renderer-refused one must read two untouched, and the falsifier forgets the refusal term and reads one. A summary-message test through `ErrorReport.CaptureErrors` pins the printed number.
- The status store gets rewritten contract tests: a play record followed by an upload record leaves the upload text readable, `Forget` clears, and the inspector test reads the unkeyed store. The falsifier records an older build second and asserts the store still returns the latest, guarding a keyed-per-path leftover.
- The hint string gets a RED string-table test: the text names the in-memory replacement and the Trace and Optimize atlas setting, and no longer tells the reader to check whether the texture is imported.
- Registered-source naming gets two tests in the semantics capture tests through an ambient object registry: a registered clone reports the source asset's path, and the falsifier, an unregistered clone, keeps its own identity and never gains the source's path. The falsifier fails an implementation that resolves through the creating lookup or copies identity from names.

After the focused runs, the full `Alrauna.Amuse.Tests.Editor` assembly runs, and observed counts are recorded. A filtered run that reports zero tests is a failure.

## Non-goals

- Renderer-name resolution. The context-object link is the renderer's attribution; the text stays as it is.
- Capturing atlas textures or other unsaved build objects. The 2026-09-27 atlas record keeps that boundary, and registration belongs to the producers.
- Deduplicating shared-material report lines. Documented as intended instead.
- Naming the upstream writer behind the animator re-clone noise. That needs one traced build with NDMF's animator debugging and is an operator action outside this design.
- Retouching the identity conditionals in the `NonResidentMips` and `UnsupportedFormat` hints.
- Any change to classification, capture, or the mutation path. This design changes only what the console says.

## Risks and limits

The registry helper depends on `IObjectRegistry.GetReference(obj, false)` returning null for an unregistered object, which is read and pinned in the installed NDMF 1.14.4 source. A future NDMF that changes that contract would surface as the falsifier test failing, not as a wrong report. The lookup happens at capture time inside AMUSE's PlatformFinish passes; producers that register later than that cannot be resolved, and the report falls back instead of guessing. The status store shows one latest build, so an editor session that builds two avatars shows the second; the recorded text names the build path, and a per-avatar history stays a non-goal. The summary invariant assumes `AppliedRendererCount` only counts renderers that `AnalyzedRendererCount` also counts, which the pass structure guarantees today; the property's doc comment states the assumption so a future pass reorder re-reads it.
