# Phase 10 — Architectural fault/trap integration

## Goal

Consume the already-established generic RuntimeKernel trap substrate and selectively map proven architectural synchronous faults to managed exceptions without merging CPU and managed EH authority.

## Prerequisites

Phase 1 kernel trap entry/return contract is executable; Phase 0 has precise synchronous memory-fault evidence; Phase 9 managed EH is stable.

## Ownership

- ISE: fault generation, architectural trap frame, precise register/PC/retire state.
- RuntimeKernel: trap entry/return, context association, generic classification and integrity/process failure policy.
- Managed Runtime: optional mapping of approved trap records to managed exception objects/types.
- Compiler: implicit-fault site marking only when a specific mapping policy is qualified; explicit null checks remain V1 baseline.

## Required TrapAbi

For each synchronous memory fault prove and expose, where applicable:

```text
ExecutionContext / VT identity
exact faulting architectural PC
fault virtual address
read / write / execute access kind
privilege/execution mode
trap class + subcause
architecturally committed register state
faulting instruction retired? (must be false for precise fault)
no younger architectural publication
return/resume semantics
nested-trap behavior
```

Use current trap/dispatch/MMU tests and source paths from Phase 0; do not reference stale legacy filenames.

## Mapping policy

Only deliberate mappings such as a later null/protection policy may become managed exceptions. Illegal instruction, corrupted return state, privilege/integrity failures and unclassified traps remain kernel/process failures unless explicitly specified otherwise.

## ISA/ISE change policy

No managed exception opcode. If the existing generic trap record lacks a required architectural field, first prove the gap with a failing conformance test; only then permit a minimal generic ISE/ISA extension.

## Tests

Load page/protection fault, store protection fault, execute fault where supported, exact PC/VA/access kind/VT, no younger write/store publication, nested trap, trap return, selected mapping -> managed catch, non-mappable trap -> fail-fast/process failure, deterministic replay sequence.

## Closure

Implicit null checks remain disabled until their exact trap mapping is independently proven safe and useful.