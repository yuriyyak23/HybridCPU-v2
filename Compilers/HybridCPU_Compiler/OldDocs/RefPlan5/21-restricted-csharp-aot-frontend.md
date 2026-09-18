# 21 — Restricted C# AOT frontend

## Goal

Establish the first .NET milestone without pretending that full NativeAOT runtime support already exists.

## Scope

Compile a deliberately restricted CIL subset into Canonical HybridCPU IR: primitive arithmetic/control flow, selected value types, static methods and explicit supported calls. Roslyn may produce CIL from C# but is not a Core dependency; a CIL reader/importer is the compiler seam.

Unsupported managed semantics—GC references beyond the qualified subset, EH, reflection/dynamic code, unsupported generics, threading/TLS, P/Invoke and runtime helpers—must produce deterministic diagnostics rather than host fallback.

This phase is a **restricted compiler/codegen milestone**, not a managed runtime milestone. It must work with an explicit closed-world feature manifest and must not silently execute unsupported portions on CoreCLR/host architecture.

## Contracts

Use Phase 08A/08B DataLayout/native ABI and Phase 07 values/classes. Preserve IL method/token/source provenance without storing Roslyn objects in Core. Define a restricted-runtime helper allowlist.

Define a machine-readable supported-CIL matrix covering:

- accepted IL opcodes and exact stack/type semantics;
- supported primitive/value types and layout restrictions;
- method/call forms, generics policy and metadata forms;
- allowed static initialization model;
- managed-reference policy for this phase (prefer none/minimal explicitly proven forms until Phase 22/25);
- helper calls and their ABI/effect descriptions;
- unsupported EH/GC/reflection/threading/TLS/interop features.

CIL verification/import performs deterministic stack-state and type-flow validation before Canonical IR creation. Invalid/unverifiable input is distinct from valid-but-unsupported managed semantics.

## Frontend boundary

Roslyn is optional source-to-CIL tooling only. `HybridCPU.Compiler.Core` sees neither Roslyn symbols nor CIL tokens; the adapter translates valid supported CIL into frontend-neutral Canonical IR plus source/provenance records.

LLVMSharp is not part of this path and is not a CIL→native compiler. No CIL is routed through LLVM IR merely to avoid implementing the managed ABI/runtime contracts.

## Runtime boundary

Until Phases 22–25 qualify managed semantics, the restricted profile must not claim GC, EH, object model, reflection, TLS, P/Invoke or managed/unmanaged transitions. Helpers are allowed only from a versioned allowlist whose side effects/calling convention are known to Core.

## Determinism / fallback

Unsupported methods/features fail with stable diagnostics before emission. Production behavior is bounded by IL/method/type/count limits rather than elapsed time. There is no silent JIT/CoreCLR/host-code fallback.

## Tests / acceptance

Compile small C# -> CIL fixtures and equivalent hand-built/native Canonical IR; compare functional results and deterministic codegen. Negative matrix must reject every non-qualified IL opcode/metadata/runtime feature.

Add malformed IL/type-stack tests, unsupported generic instantiations, hidden managed-reference flows, static-constructor edge cases and helper-side-effect tests. Verify Roslyn and LLVMSharp packages can be absent when compiling pre-produced supported CIL through the restricted adapter.

**Acceptance:** restricted C# AOT is a deterministic, explicitly bounded CIL→Canonical-IR→HybridCPU path with a closed feature manifest; it is useful for bring-up while remaining explicitly smaller than NativeAOT and independent of LLVMSharp/full managed runtime support.
