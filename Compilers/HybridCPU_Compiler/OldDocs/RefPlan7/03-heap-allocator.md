# Phase 3 — Heap allocator and runtime allocation ABI

## Goal

Introduce deterministic managed allocation on top of the Phase 1 bootstrap/kernel memory contract, without GC complexity.

## Prerequisites

Managed image/runtime helper imports work; object/type layout and static storage are stable; kernel can provide or reserve the heap region without object knowledge.

## Ownership

- Managed Runtime owns heap structure, allocation policy, object initialization and OOM semantics.
- Compiler lowers admitted allocation CIL to runtime helpers and constructors.
- RuntimeKernel owns generic VM/region reservation only.
- ISE changes: none.

## V1 allocator

Use a deterministic arena/bump allocator first:

```text
size = checked_aligned_object_size(type)
if bump + size <= limit:
    zero/init header
    return object
else:
    allocation_slow_path / OOM
```

Freeze heap base/limit/alignment, zeroing contract, header initialization, maximum object size, overflow behavior and deterministic exhaustion policy. Reserve the slow path for Phase 5 GC.

## Compiler/runtime helper flow

`newobj` must lower to a versioned helper such as `RhpNewObject(TypeDesc*)`, followed by the normal constructor call using the managed call ABI. Reference liveness across allocation/constructor calls must already be correct.

Constructors requiring unsupported instance-method or type-initialization behavior remain fail-closed.

## Tests

Allocation alignment/non-overlap, zeroing, constructor field initialization, allocation exhaustion/OOM, checked size overflow, deterministic allocation trace, malformed TypeDescriptor, heap-region permission failure, and end-to-end `.hcexe -> bootstrap -> helper -> object` execution.

## Closure

Single-context allocation is usable through the real image/bootstrap/runtime path. No reclamation or multithread allocation claim yet.