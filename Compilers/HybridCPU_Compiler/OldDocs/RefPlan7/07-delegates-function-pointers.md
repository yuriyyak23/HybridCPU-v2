# Phase 7 — Delegates and managed function pointers

## Goal

Add delegate/function-pointer semantics over the same generic managed call ABI and indirect control transfer proven in Phase 6.

## Prerequisites

Managed objects/GC, instance/direct/indirect calls, type identity and stable call signatures.

## Delegate contract

Freeze a GC-visible delegate representation containing the required combination of target object, code pointer/invocation thunk and optional context. Define signature identity/compatibility and open/closed instance/static invocation forms admitted by V1.

Compiler emits ordinary object construction plus invocation through a versioned thunk/helper and generic indirect call. Runtime owns delegate object semantics and target/context resolution.

Multicast delegates may be deferred, but the unsupported state must be explicit.

## Function pointers

Managed function pointers use machine code addresses plus compiler/runtime signature compatibility rules. Native/interop function pointers that require transition state belong to Phase 13.

## ISA policy

No delegate/fptr opcode. `JALR` or the proven generic indirect-transfer contract is sufficient unless concrete evidence disproves it.

## Tests

Static, closed-instance and admitted open-instance delegates; target surviving GC; signature mismatch negative; null target semantics; thunk recursion/nested calls; code-pointer relocation; function-pointer invocation; deterministic delegate/thunk metadata.

## Closure

Delegate roots appear correctly in GC evidence and all invocation paths use the normal call/indirect-transfer authority.