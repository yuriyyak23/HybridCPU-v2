# Phase 3 — Authoritative native call ABI, allocation constraints and frame lowering

## Gate

Phase 2 produces a bounded acyclic graph with stable method identities/signatures.
Phase 0 has identified the authoritative HybridCPU native ABI contract and all blocker
target assumptions.

Before declaring that ISE changes are unnecessary, obtain direct ISE source/test evidence
for the existing call/link, return, scalar load/store, alignment, redirect/fault and
architectural-register semantics used by this phase. Until that proof exists, `ISE
source changes = zero` is `UNVERIFIED`; this is not evidence that a new ISA is needed.

## Goal

Use one authoritative HybridCPU native scalar call contract for managed-to-managed direct
calls. Extend/version the existing native ABI contract (for example the current
`HybridCpuNativeAbiContractV2` seam); do not define a competing
ScalarControlFlowV2-specific ABI.

## Required ABI contract

Specify and test exactly:

- argument registers/order and maximum register argument count;
- return register(s) and `void` behavior;
- scalar extension/truncation at call boundaries;
- reserved and allocatable architectural registers;
- stack-pointer register;
- return-address/link register or equivalent mechanism;
- caller-saved and callee-saved masks;
- stack growth direction;
- minimum stack and frame alignment;
- spill-slot alignment;
- final frame layout and save/restore ordering;
- outgoing/incoming stack-argument policy;
- prologue/epilogue responsibilities;
- leaf/non-leaf frame policy;
- call clobber semantics visible before allocation;
- maximum admitted frame bytes;
- stack overflow/execution-environment assumption;
- direct-call symbol/relocation carrier used by Phase 4.

For V1, stack-passed arguments are optional. If the existing ABI/profile does not already
require them, support the bounded register argument set and deterministically reject
larger signatures instead of adding an outgoing-argument-area mechanism solely for
completeness.

## Required implementation order

### 3A — Pre-RA call lowering

Before liveness/allocation finalization:

- classify arguments/results using the authoritative ABI;
- materialize ABI call pseudo/operation form;
- mark reserved registers;
- expose caller-saved clobbers and fixed argument/result locations;
- make live-across-call constraints visible to liveness/allocator;
- reject unsupported signatures before code emission.

A call is not just a CFG edge; it has register clobber/resource semantics.

### 3B — Analysis, schedule-for-allocation and register allocation

- recompute dependency/liveness/pressure/resource facts after call pseudos/moves;
- produce the schedule required by the existing schedule-aware allocator;
- allocate with call clobbers and reserved registers already known;
- insert required spill/reload/save/restore virtual operations according to allocator
  results.

The pre-RA schedule is not automatically the final schedule.

### 3C — Post-RA frame finalization

After actual spills and used callee-saved/link registers are known:

- compute final frame size and deterministic slot offsets;
- place saved return-address/callee registers;
- lower spill slots and stack addressing;
- insert prologue/epilogue and save/restore operations;
- resolve remaining physical parallel copies;
- invalidate every affected fact;
- recompute dependency/liveness/pressure/resource/loop facts as applicable;
- perform final scheduling/bundling before object emission.

Do not freeze the concrete frame before RA and then patch it after discovering spills.

## ISE/ISA sufficiency proof

Before Phase 3 closure, execute at least:

`caller -> call A -> A frame/save -> call B -> B return -> A restore -> A return`

with live caller values across calls and at least one stack spill/reload case.

The proof must establish the exact architectural return-address value/basis, branch/call
target addressing, stack load/store widths/alignment and fault behavior used by the ABI.
If existing ISA suffices, no ISA extension is added. If a gap is proven, propose only the
smallest targeted ISE/contract change required; do not move compiler metadata into
runtime authority.

## Non-goals

- GC stack maps;
- reference/object ABI;
- varargs;
- EH unwinding;
- tail-call optimization;
- virtual dispatch;
- unmanaged interop;
- recursion-depth machinery.

## Tests

Positive:

- zero, one and maximum admitted primitive register arguments;
- primitive return and `void` return;
- leaf call;
- non-leaf nested call chain;
- caller value live across caller-saved pressure;
- callee-saved preservation;
- helper with locals, spills and loop;
- deterministic leaf/non-leaf frame layout;
- executable nested-call/stack proof on ISE.

Negative:

- unsupported scalar/reference signature;
- too many arguments;
- frame-size overflow;
- invalid/unsupported calling convention;
- ABI/ISE contract mismatch.

Property/determinism:

- stable clobber and save order;
- frame layout independent of hash order;
- compute facts -> insert spill/prologue/call lowering -> stale fact cannot be consumed;
- repeated builds produce identical final machine/object bytes.

## Closure evidence

The authoritative ABI source, allocator constraints, final frame dumps, object/disassembly
evidence and ISE execution must agree on registers, offsets, clobbers, return addresses
and stack effects.

## Release disposition

Profile-gated until Phase 4 image/link closure.
