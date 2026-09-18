# 08 — TargetMachine, DataLayout, ABI, memory model and object/link contract

## Goal

Define the target-platform facts that LLVM, register allocation and NativeAOT require before any of them can be considered correct. This phase is deliberately split into an early compiler-core contract and a later platform/object contract to avoid the Phase 07 ↔ Phase 08 dependency cycle.

## Current-code reality and reuse

The compiler already has contour-specific `Compiler*AbiContract` types, symbol/section models and control-flow relocation support. Consolidate these facts; do not claim ABI/object support is wholly absent and do not invent incompatible parallel rules.

## 08A — TargetMachine core contract (must precede Phase 07)

Freeze a versioned `HybridCpuTargetMachineContract` containing only facts required to type values, build register constraints and calculate target layout:

1. endianness, pointer width and address-space identities;
2. legal scalar widths, alignment and aggregate layout rules;
3. architectural register inventory and encodable register namespace;
4. architecturally allocatable register classes, fixed/special registers and call-clobber primitives;
5. register groups and mappings that are architectural contracts rather than live PRF/rename state;
6. primitive native calling-convention locations sufficient for liveness/call-boundary analysis;
7. target triple/DataLayout identity and a deterministic contract digest.

**HybridCPU-v2 constraint:** do not infer a generic vector register file from stream/vector/matrix execution contours. Current architectural register assumptions must be traceable to live HybridCPU-v2 code/tests. Unknown or undocumented register storage remains `Unsupported`.

Phase 07 consumes 08A. 08A must not depend on Phase 07.

## 08B — Full ABI, memory and object/platform contract (after Phase 07)

Complete the remaining target/platform facts:

1. full native calling conventions: argument/return locations, caller/callee-saved sets, aggregate passing, varargs policy, tail-call constraints and special-contour calls;
2. stack-frame/frame-index contract, stack alignment, red-zone policy, dynamic stack allocation policy and stack probing policy;
3. atomics/memory model: supported widths/orderings, fences, volatile semantics, alignment requirements and unsupported cases;
4. TLS/thread-local addressing models and relocation requirements;
5. symbol binding/visibility, sections, relocation kinds/addends, object format, COMDAT/duplicate policy and linker responsibilities;
6. executable entry/startup contract and host/OS assumptions;
7. versioned runtime-helper ABI and reserved symbols;
8. debug/source mapping format and deterministic line/origin mapping;
9. unwind/EH object-section and relocation placeholders with explicit `Unsupported` until Phase 25 implements semantics;
10. object-writer feature matrix: each relocation/section capability is independently versioned and fail-closed.

## Required ownership rules

- Target contracts describe architectural/compiler-visible facts only; they never expose runtime rename/PRF/free-list/scoreboard state as compiler allocation authority.
- A target contract version is bound to the HybridCPU-v2 architecture/runtime revision and machine-model digest.
- LLVM host defaults, host C ABI rules or LLVM target defaults cannot fill unknown HybridCPU facts.
- `Unknown` legality-relevant target facts block the affected frontend/lowering path; they are not converted to convenient defaults.

## Dependency contract

```text
05 schemas/capabilities + 06 Canonical IR
        -> 08A TargetMachine core
        -> 07 virtual values/liveness/pressure
        -> 08B full ABI/memory/object/platform
        -> 09 LLVM importer, 20 final RA/frame, 21+ .NET
```

This is an intentional split inside one numbered phase; acceptance of 08A does not imply acceptance of 08B.

## Tests / acceptance

### 08A gates

- Golden pointer/layout/register-namespace cases against the pinned HybridCPU-v2 contract.
- Static negative tests preventing inferred vector register classes or runtime PRF/rename fields.
- Target digest and version-skew rejection.
- Primitive call-boundary tests sufficient for Phase 07 liveness.

### 08B gates

- Golden aggregate layout/call/relocation cases.
- Cross-module symbol and COMDAT/duplicate tests.
- Stack alignment/probe tests and unsupported dynamic-stack cases.
- Atomic ordering/width/alignment negative matrix.
- TLS relocation/addressing matrix.
- Deterministic object metadata/section ordering and malformed-relocation rejection.
- Debug origin-chain round-trip where supported.

**Acceptance:** 08A provides a single target-correct register/DataLayout source for Phase 07; 08B provides a single versioned native ABI/object/platform contract for LLVM/.NET and final lowering, with every unknown target fact failing closed rather than inheriting host/LLVM defaults.
