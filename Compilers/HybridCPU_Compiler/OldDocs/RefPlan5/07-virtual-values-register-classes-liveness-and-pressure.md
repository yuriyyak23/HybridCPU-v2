# 07 — Virtual values, register classes, liveness and pressure

## Goal

Add the pre-allocation value model required by multi-frontend SSA, region scheduling and modulo scheduling without prematurely binding physical registers or inventing architectural register files that HybridCPU-v2 does not expose.

## Dependency correction

This phase depends on Phase 03 resource/topology evidence, Phase 06 Canonical IR, and the **early Phase 08A TargetMachine/Register/primitive-ABI contract**. Allocation classes and pressure budgets must not be defined before target DataLayout, the architectural register inventory and primitive ABI constraints are fixed.

Conceptually the order is:

```text
06 Canonical IR
   -> 08A TargetMachine core contract
      -> 07 virtual values / liveness / pressure
         -> 08B full memory/object/link/platform contract
```

## Required model

The following concepts are separate and must not be collapsed into one `RegisterClass` enum:

```text
ValueKind
VirtualValueClass
AllocationConstraint
ArchitecturalRegisterClass
RegisterGroup
SpecialStateClass
```

- `ValueKind` describes semantic IR values (integer, floating, pointer, aggregate, vector-like semantic values where a verified lowering exists).
- `VirtualValueClass` describes compiler scheduling/liveness properties before physical allocation.
- `AllocationConstraint` describes width, alignment, pairing, fixed/precolored operands, VT locality, contour/lane restrictions and legal physical/group sets.
- `ArchitecturalRegisterClass` describes only architecturally allocatable register storage actually defined by HybridCPU-v2.
- `RegisterGroup` describes the Phase 03 grouping/port-conflict domain used by allocation and scheduling.
- `SpecialStateClass` describes non-general-register architectural/special state that is not allocator-owned generic register storage.

The architectural register inventory and widths are taken only from the pinned live HybridCPU-v2 target contract. Shared backend PRF/rename/commit/free-list state remains runtime/backend-owned. Stream/vector/matrix execution contours and lane6/lane7 special behavior must **not** be interpreted as proof of a generic allocatable architectural vector register file.

A semantic `Vector` value is permitted only when a verified lowering contract exists; it must not automatically become an allocatable `VectorRegClass`.

Additional required state:

- Stable virtual value IDs with defs/uses, PHI/edge uses and optional fixed/precolored ISA operands.
- Region/loop live-in, live-out and interval/lifetime information.
- Pressure by allocatable architectural class, register group and predicted PRF read/write demand using Phase 03 topology contracts.
- Explicit distinctions between virtual value, encoded architectural register, compiler physical assignment, non-register special state and runtime rename/PRF state.

## Current-code integration

Extend the existing Canonical IR/dependency infrastructure; do not replace precise RAW/WAR/WAW analysis for already physical native inputs. Native precolored programs remain supported. Physical allocation/spills/frame mutation are deferred to Phase 20.

## Safety / determinism

Runtime rename/PRF occupancy is not modeled as compiler-owned state. Unknown architectural register inventory, class mapping or special-state semantics fail closed for allocation-capable paths. All value numbering and liveness ordering are deterministic.

## Tests / acceptance

Cover PHI edges, loops, calls, fixed registers, VT separation, register groups, special state, lane6/lane7 constraints and mixed precolored/virtual values. Add negative tests proving that a vector-like IR value cannot create an allocatable vector register class without an explicit target contract. Pressure must reconcile with generated uses/defs and remain stable under collection-order perturbation.

**Acceptance:** region and loop schedulers can reason about lifetimes/pressure without requiring final physical allocation, and every allocatable class is traceable to the versioned HybridCPU-v2 target-register contract rather than inferred from execution-contour names.
