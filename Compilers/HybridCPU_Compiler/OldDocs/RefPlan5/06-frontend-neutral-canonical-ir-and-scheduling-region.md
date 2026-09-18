# 06 — Frontend-neutral Canonical IR and SchedulingRegion contracts

## Goal

Turn the existing Canonical IR into the stable seam for native, LLVM and .NET frontends, and introduce a first-class scheduling-region abstraction without changing scheduling behavior yet.

## Current-code reality and reuse

Reuse `IrProgram`, `IrFunction`, `IrBasicBlock`, `IrInstruction`, CFG/source-symbol/span types, dependency graphs and `HybridCpuBasicBlockSchedulingDagBuilder`. Existing `IrParallelRegion`/`ParallelRegionDetector` are decomposition concepts and must not be reused as scheduling-region authority.

## Required work

1. Define frontend-neutral type/value/source semantics and stable instruction identities.
2. Ensure frontend handles (`LLVMValueRef`, CIL tokens, Roslyn symbols) never enter Core scheduling/bundling objects.
3. Preserve source mapping through transforms with versioned origin chains for later debug mapping.
4. Add `SchedulingRegion` with blocks, entries/exits, side-exit/control predicates, dependence view, capability requirements and deterministic region ID.
5. First implementation is BB-only and must produce byte-parity with the existing local scheduler.
6. Define frontend adapter result/diagnostic boundary; a frontend may emit Canonical IR only after semantic validation.
7. Make side effects first-class: memory read/write/atomic/volatile, fences, calls, traps/faulting operations, control effects and special architectural state must be represented explicitly enough for dependency and speculation legality.
8. Define a frontend-neutral memory-effect lattice and address-space identity. `Unknown` must conservatively participate in dependencies; it cannot be erased because LLVM/CIL supplied an optimistic analysis fact.
9. Define Canonical IR semantics for integer overflow/wrapping, shifts, pointer arithmetic, poison/undef-like source values, conversions and exceptional/faulting behavior. Frontends must lower source-specific semantics into these contracts or reject unsupported cases.
10. Add mutation/version stamps for dependency, liveness, pressure, MII and placement views. Schedule-changing transforms must invalidate affected derived facts rather than retaining stale proof objects.

## Canonical IR boundary rules

- Canonical IR is not LLVM IR and not CIL. It carries only semantics the HybridCPU compiler core can validate and lower.
- Frontend analysis results are tagged evidence with provenance/trust class; they are not embedded as unconditional legality truth.
- Canonical IR capability requirements are explicit and versioned. A target lacking a required capability rejects before final lowering.
- EH regions, GC state, TLS, atomics or managed reference semantics may be represented only once their corresponding target/managed contracts exist; otherwise the importer/frontend reports `Unsupported` rather than approximating them.
- Source-origin chains are diagnostic/debug provenance only; they cannot affect deterministic scheduling tie-breaks except through a separately versioned stable source identity contract.

## Tests / fallback

BB-region mode must be bit-for-bit equivalent to legacy BB scheduling. Unknown frontend semantics reject before scheduling. Region expansion stays default-off until Phase 12.

Add negative tests for unknown memory effects, volatile/atomic reordering, potentially faulting instructions, unsupported address spaces, stale derived-fact versions and accidental frontend-object retention. Property tests must prove that frontend insertion/enumeration order cannot change stable Canonical IR identities.

**Acceptance:** native frontend passes through the new seam with zero schedule delta; Core contains no frontend-specific handles; debug/source origin mapping survives round-trip tests; every legality-relevant side effect needed by dependency/region/loop scheduling is explicit or conservatively `Unknown`.
