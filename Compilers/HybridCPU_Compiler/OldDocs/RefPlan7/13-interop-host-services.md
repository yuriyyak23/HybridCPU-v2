# Phase 13 — Managed interop and host services

## Goal

Add P/Invoke/native interop and richer host services on top of the generic host transition established in Phase 1, without leaking services or managed semantics into ISA.

## Prerequisites

Stable managed/native ABI, GC/handles/pinning, threads/TLS, EH, kernel host boundary and synchronization needed by transition state.

## Important split

The **early platform host boundary** is already present from Phase 1 for boot, memory/VM, process exit, trap transition and minimal diagnostics. This phase adds **managed interop**, not the kernel substrate itself.

## Compiler responsibilities

- P/Invoke signature admission;
- native-call stub/thunk generation;
- calling-convention lowering and deterministic symbol imports;
- fail-closed unsupported marshalling/signatures.

## Managed Runtime responsibilities

- native library/symbol resolution policy;
- blittable marshalling first;
- pinned handles/object lifetime across native transitions;
- GC transition state and native-call root policy;
- callbacks/reentrancy policy;
- exception boundary: managed exceptions must not accidentally unwind through an unsupported native frame.

## RuntimeKernel/host responsibilities

Generic external service transition, address/privilege validation, host provider dispatch and explicit result/error model.

## V1 scope

Integers, supported FP, raw pointers, fixed-layout qualified structs, explicit buffers + lengths. Complex string/object marshalling and broad compatibility layers remain deferred.

Console/files/network/time are runtime libraries over service IDs/contracts, never opcodes.

## ISA policy

`ECALL`/existing generic trap/call machinery must be qualified first. Add no new service-specific instruction. A generic ISE extension is allowed only after a failed host-transition conformance test proves the existing path insufficient.

## Tests

Valid/missing symbol, supported/unsupported signature, pinning and native-call roots, GC requested during transition, invalid pointer/buffer, callback/reentrancy cases if admitted, exception containment, deterministic mock host provider, no direct host API calls from ISE execution code.

## Closure

The blittable subset works through compiler thunk -> runtime transition state -> generic kernel host boundary with explicit GC/exception semantics.