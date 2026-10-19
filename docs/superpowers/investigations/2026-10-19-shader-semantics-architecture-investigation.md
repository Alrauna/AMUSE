# Shader Semantics Architecture Investigation

Status as of 2026-10-19.

## 1. Summary and Verdict

This investigation evaluates the architectural design of shader semantics in AMUSE.
The review evaluates whether AMUSE must store shader semantics as compiled C# code, as JSON data files, or as a hybrid model.
The evaluation ran three parallel investigation tracks.
Track 1 audited the existing C# implementation and code volume in `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/`.
Track 2 researched compiler engineering practices, game engine representations, and VRChat avatar optimization tools.
Track 3 conducted an adversarial review of failure modes, mathematical proofs, performance, and Ponytail minimalism.

### The Verdict

**Retain compiled C# for shader semantics. Reject runtime JSON interpretation.**

The investigation confirms that compiled C# is the correct architecture for AMUSE.
Shader semantics in AMUSE is not passive data.
Shader semantics in AMUSE is an active mathematical proof system.
Storing procedural semantics in JSON introduces Greenspun's Tenth Rule.
It forces AMUSE to build an untyped, ad-hoc interpreter inside the Unity Editor.
This interpreter creates runtime memory allocations, adds garbage collection pauses during avatar builds, and risks floating-point rounding errors.
It weakens compile-time guarantees and violates Ponytail minimalism.

---

## 2. Codebase Audit of Existing Semantics

On 2026-10-19, the Semantics module in `<repo-root>/Packages/com.alrauna.amuse/Editor/Semantics/` contains 44 C# source files.
The module contains 15,616 total lines of code.
This total represents 34.61% of the complete Editor codebase (45,125 lines).

### Code Distribution by Functional Responsibility

The investigation measured and classified every line of code in the module into six functional categories:

| Functional Category | Line Count | Share of Module | Primary Responsibilities |
| :--- | :--- | :--- | :--- |
| **Control-Flow / Gate Checks** | 5,434 | 34.80% | Feature flags, zero-gates, mode gates, resolution logic, refusal classification |
| **Static Attestation** | 3,949 | 25.29% | SHA-256 hashes, GUIDs, version matrices, include tree digests, source normalization |
| **Opaque Conversion Recipes** | 2,537 | 16.25% | Target property tuples, clone normalization, render-state conversion eligibility |
| **Declarative Schema** | 1,911 | 12.24% | Material evidence requests, property name constants, texture channel specifications |
| **Algorithmic Value Algebra** | 1,477 | 9.46% | Mathematical AST (`ScalarSemanticValue`), affine maps (`AffineAlphaMap`), product folds |
| **Shared Models & Diagnostics** | 308 | 1.97% | Reason taxonomies (`AlphaUnknownReason`), capture outcomes, render state records |
| **Total** | **15,616** | **100.00%** | **44 C# source files** |

### Key Findings from Code Volume Audit

1. **Procedural Logic Dominates Pure Data**:
   Control-flow gates, conversion recipes, and algebraic math account for 60.51% of the code (9,448 lines).
   Only 37.53% of the code (5,860 lines) consists of static attestation tables and declarative property schemas.
   JSON can serialize static tables.
   JSON cannot serialize procedural logic without an expression engine.

2. **Module Depth and Hidden Implementation**:
   The module declares 89 types (87 top-level types and 2 nested types).
   44 types (49.44%) are internal to `Semantics/`.
   20 files (5,305 lines) have zero external references outside `Semantics/`.
   9,873 lines out of 15,616 lines (63.22%) represent private implementation details.
   The interface exposed to `<repo-root>/Packages/com.alrauna.amuse/Editor/Analysis/` is small and deep.
   Analysis consumes only domain models (`ScalarSemanticValue`, `UvMapping`, `TextureSampling`) and one entry method (`AnalyzeAlphaMaterial`).

3. **Pain Points in the Status Quo**:
   Large static tables in `LilToonSourceAttestation.cs` (2,878 lines) increase C# source text size.
   Modifying a hash string forces a C# compilation and a Unity domain reload.
   Multiple files repeat vendor property names.
   Adding support for a new vendor shader requires authoring new C# classes.

---

## 3. Adversarial Analysis of the JSON Alternative

The investigation tested the hypothesis: "Can AMUSE store shader semantics in JSON files?"

### 3.1 Greenspun's Tenth Rule

Shader alpha in AMUSE is an algebraic expression tree.
For example, lilToon layer blending evaluates four different blend modes:
- Mode 1 replaces base alpha.
- Mode 2 folds products through `ScalarProductFold`.
- Mode 3 builds a saturating sum (`saturate(base + layer)`).
- Mode 4 builds a saturating difference (`saturate(base - layer)`).

Poiyomi alpha mask evaluation computes:
`alpha = invert ? (1.0 - saturate(blendStrength + (invert ? -value : value))) : saturate(...)`.

A JSON file cannot directly execute these equations.
To store this logic in JSON, the schema must encode an Abstract Syntax Tree (AST).
The JSON format must define objects for variables, operators, conditional branches, clamps, and diagnostic fallbacks.
This recreates Greenspun's Tenth Rule:
Any complex data format recreates an ad-hoc, informally-specified, slow implementation of a programming language.

AMUSE must write and maintain an expression interpreter in C#.
The developer must debug logic errors in JSON without compiler syntax checks, without type safety, and without IDE breakpoint support.

### 3.2 Floating-Point Precision and Proof Soundness

AMUSE enforces strict mathematical proofs.
Cutout classification requires exact IEEE-754 binary32 comparisons (for example, `cutoff <= 0.9999f`).
Runtime JSON parsers frequently parse decimal text into binary64 (`double`) before casting to binary32 (`float`).
This two-step conversion creates double-rounding errors on boundary thresholds.
Runtime parsers also risk culture-dependent separator bugs.
In compiled C#, the compiler encodes exact binary32 IEEE-754 bit representations directly into IL opcodes.
C# guarantees identical numerical evaluation on every execution.
### 3.3 Build Performance and Garbage Collection

AMUSE executes during Non-Destructive Modular Framework (NDMF) avatar builds.
Avatar export occurs in the Unity Editor before upload.
Users expect fast build times.
Reading JSON from disk and building dynamic AST trees allocates heap memory and boxed dictionaries.
These allocations trigger Unity garbage collection spikes during avatar builds.
In contrast, compiled C# minimizes allocations.
The C# implementation uses value structs (`AffineAlphaMap`, `UvMapping`) and static arrays.
Compiled C# evaluates expressions via direct method calls with zero parsing overhead.
### 3.4 Strict Attestation and Fail-Closed Invariants

AMUSE enforces Invariant 1: Any unproven state must fail closed as `Unknown`.
AMUSE does not support unverified user shaders.
AMUSE strictly attests vendor shaders against pinned GUIDs, package versions, and SHA-256 digests.
AMUSE engineers must vet shaders before AMUSE adds support. Dynamic end-user JSON editing provides no user value.
Users cannot safely add custom shader semantics without mathematical proof audits.
A corrupted, malformed, or out-of-date JSON file throws runtime exceptions or silently fails closed to `Unknown`.
This disables optimizations without informing the user why.

---

## 4. Industry Architectures and External Evidence

The investigation researched how external compilers, material standards, and avatar tools represent shader semantics:

### 4.1 Production Material Standards and Compilers

1. **MaterialX (Lucasfilm / Academy Software Foundation)**:
   MaterialX uses XML exclusively to describe node graph connections and parameter values.
   MaterialX does not evaluate math inside XML.
   All semantic validation, topological sorting, and code generation run in compiled C++ (`MaterialXCore`, `MaterialXGenShader`).

2. **glTF 2.0 (Khronos Group)**:
   glTF stores material properties in declarative JSON.
   The JSON schema contains zero procedural logic and zero dynamic expressions.
   Written specification documents define the mathematical semantics.
   Game engines implement the math in hardcoded HLSL or GLSL shaders.

3. **Unity ShaderGraph**:
   Unity serializes graph assets to disk as JSON.
   When loaded into memory, the JSON deserializes into compiled C# node classes (`AbstractMaterialNode`).
   Compiled C# implements type propagation, precision validation, and HLSL generation.

4. **SPIR-V Machine-Readable Grammar**:
   Khronos publishes `spirv.core.grammar.json` to define instruction syntax.
   Khronos explicitly states that semantic validation rules remain in C++ code.
   A build script parses the JSON at build time to generate C++ header files.
   No production SPIR-V tool parses grammar JSON at runtime.

5. **LLVM TableGen vs. C++**:
   LLVM uses TableGen (`.td` files) for instruction syntax and simple pattern matching.
   LLVM translates TableGen into C++ source files at build time.
   All complex instruction selection, semantic analysis, and legalizations remain in compiled C++.
   LLVM documentation notes that forcing complex semantics into TableGen increases maintenance complexity.

### 4.2 VRChat Avatar Optimization Tools

1. **Anatawa12's Avatar Optimizer (AAO)**:
   AAO uses a compiled C# API (`ShaderInformation`, `MaterialInformationCallback`).
   Compiled C# classes implement third-party shader support.
   AAO evaluated the avatar optimization problem and selected compiled C# over JSON.

2. **d4rkAvatarOptimizer**:
   d4rkAvatarOptimizer includes a custom HLSL parser and AST analyzer in C#.
   The tool parses HLSL macros and include trees.
   This approach provides flexibility but requires high maintenance when vendor shaders change formatting or macros.

3. **Poiyomi Shader Optimizer (ThryEditor)**:
   ThryEditor combines C# editor scripts with custom comment annotations (`//if`, `//end`) inside HLSL templates.
   Template comments and C# classes divide the logic.

---

## 5. Architectural Options Comparison

The investigation evaluated four distinct implementation approaches:

| Evaluation Criteria | Option 1: Pure C# (Status Quo) | Option 2: Pure JSON | Option 3: Hybrid (JSON Data + C# Code) | Option 4: Source-Generated C# |
| :--- | :--- | :--- | :--- | :--- |
| **Proof Soundness** | **Highest.** Compiler verified, exact IEEE-754 binary32 literals. | **Poor.** Ad-hoc interpreter invites operator and rounding errors. | **Moderate.** Tables separated from logic, potential synchronization drift. | **High.** Generates typed C# code before compilation. |
| **Fail-Closed Safety** | **Highest.** Type system prevents unhandled state leaks. | **Poor.** Schema mismatches risk runtime crashes or silent refusals. | **Moderate.** Missing asset files can cause runtime failures. | **High.** Missing symbols fail at compile time. |
| **Refactoring and IDE Tooling** | **Highest.** Full IDE symbol tracking, renames, and call hierarchy. | **Poor.** Untyped string keys, IDE tools cannot trace call sites. | **Moderate.** Two disconnected files per shader family. | **Moderate.** Generated files add friction during navigation. |
| **Build-Time Performance** | **Highest.** Zero file IO, zero runtime parsing, zero heap allocations. | **Poor.** File IO and JSON parsing allocate memory during avatar builds. | **Moderate.** File IO and table loading add build latency. | **Highest.** Zero file IO, zero runtime parsing, compiled bytecode. |
| **Maintenance Burden** | **Moderate.** High line counts for static attestation tables. | **High.** Must maintain both JSON files and a custom interpreter. | **Moderate.** Must maintain two representations for every shader. | **Moderate.** Must maintain generator scripts and schemas. |
| **Ponytail Score** | **Passes.** Boring design, zero extra plumbing, deletes dead weight. | **Fails.** High accidental complexity, reinvents an untyped language. | **Fails.** Two representations for one concept, fails the deletion test. | **Fails.** Over-engineered for two supported shader families. |

---

## 6. Codebase Design and Ponytail Principles

### 6.1 The Deletion Test
The codebase design skill requires the deletion test:
Imagine deleting the proposed layer.
If we delete a JSON loader and retain compiled C# arrays, what happens?
We delete a file parser, an asset reader, schema validation logic, and file IO exception handlers.
We eliminate potential runtime failures.
What capability did we lose?
Nothing.
End users do not write shader semantics.
AMUSE engineers write shader semantics only after rigorous verification of vendor HLSL code.
Therefore, a JSON data layer fails the deletion test.

### 6.2 Module Depth and Locality
In the compiled C# design, locality is high.
In `LilToonTransparentMaterialSemantics.cs`, the gate property names, the numeric threshold constants, the proof citations, and the gate checks live together in one file.
Moving property names to JSON isolates those names from the mathematical proof comments that justify them.
A developer must consult two different files to understand a single shader gate.
Compiled C# keeps decisions local, cohesive, and easy to review.

---

## 7. Conclusions and Recommended Actions

1. **Retain Compiled C# as the Source of Truth**:
   AMUSE must keep shader semantic interpretation, evidence requests, and value algebra in compiled C#.
   This preserves fail-closed safety, eliminates build-time memory allocations, and guarantees mathematical precision.

2. **Reject a Runtime JSON Interpreter**:
   AMUSE must not build a JSON expression engine.
   A JSON interpreter introduces accidental complexity, maintenance overhead, and latent proof defects.

3. **Manage Static Attestation Tables within C#**:
   The primary defect of the status quo is line count bloat in `LilToonSourceAttestation.cs` (2,878 lines).
   AMUSE must not solve this by introducing runtime JSON files.
   If table size becomes problematic, AMUSE can compact static arrays using standard C# syntax or compact binary byte spans.
   If developers require external tooling in the future, use offline build-time scripts to generate plain `.cs` files.
   Never parse semantics at runtime during avatar builds.
