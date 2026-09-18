# Phase 12 — Synchronization and managed memory model

## Goal

Enable `volatile`, `Interlocked` and monitors only after a normative compiler/runtime mapping to HybridCPU atomic/order primitives is written and cross-VT tested.

## Prerequisites

Phase 11 threads/TLS/blocking and Phase 0 executable LR/SC/AMO/fence evidence.

## Normative mapping contract

Document explicitly:

- ordinary load/store guarantees;
- naturally atomic widths/alignment;
- CIL volatile read/write lowering;
- Interlocked RMW/CAS lowering;
- acquire, release and full-fence semantics;
- successful and failed CAS ordering;
- cross-VT visibility/publication;
- effect of speculation, flush and replay on observable ordering.

Existing acquire/release tests are evidence, not a substitute for this normative mapping.

## Ownership

Compiler chooses architecture primitives/helpers consistent with the mapping. Managed Runtime owns Monitor recursion/ownership/object semantics. RuntimeKernel supplies only language-neutral contention blocking/wakeup. ISE owns atomicity/order, never `Monitor`.

## Monitor V1

```text
fast path: atomic CAS/RMW on runtime lock state
slow path: RuntimeKernel.wait_on_address(address, expected, deadline)
unlock: release publication + RuntimeKernel.wake_address(address, count)
```

The kernel wait/wake contract must define no-lost-wakeup races, timeout and cancellation/termination interaction.

## ISA policy

Reuse existing atomics/fences unless a normative litmus test proves one concrete missing generic primitive. No managed synchronization opcode.

## Tests

Cross-VT message passing, store/load buffering where applicable, acquire/release publication, CAS success/failure, full fence, volatile lowering golden tests, monitor recursion/contention/handoff, lost-wakeup races, GC while blocked, deterministic/replay plus adversarial interleavings.

## Closure

Do not claim `volatile`, `Interlocked` or `Monitor` support until the normative contract and litmus suite are checked in and green.