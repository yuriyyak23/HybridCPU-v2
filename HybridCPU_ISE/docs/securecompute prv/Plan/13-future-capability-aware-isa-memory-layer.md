# Future Capability-Aware ISA Memory Layer

## Purpose

Import ban: current Layer 1/Layer 2 product code must not reference future capability-aware types, tags, capability registers, capability-bearing operands or provisional capability migration fields from this document.

Документ явно выносит capability-aware ISA/memory model за пределы текущего SecureCompute plan. Это future work после завершения Layer 1 и Layer 2.

## Scope

Future layer covers true per-pointer/per-access capability-aware semantics: capability registers, tagged memory, capability-bearing operands, capability-aware `LOAD` / `STORE` / `FETCH`, pointer provenance, sealed capabilities, compiler/ABI work, VLIW/EPIC operand metadata and formal ISA legality changes.

Current Layer 1/Layer 2 Lane6/Lane7 paths may carry only neutral descriptor/evidence envelopes. They must not carry capability metadata, tag state or per-pointer provenance under the current SecureCompute refactoring.

## Implementation status

- [x] Phase 10 release gate closed on 2026-05-31: doc/source conformance guards enforce this file as future quarantine, not Layer 1/Layer 2 product scope.
- [x] Future quarantine decisions transferred to `Plan2/14-securecompute-open-decision-backlog.md`: any capability-aware ISA/memory work remains outside current SecureCompute and must start from a new RFC / architecture decision record before code changes.

## Non-goals

Этот future layer не должен смешиваться с:

- Layer 1 HybridCPU Secure Domain;
- Layer 2 CHERI-like authority discipline without ISA expansion;
- VMX compatibility boundary hardening;
- disabled/no-effect descriptor skeleton;
- Stage B secure admission hooks.

## Architectural invariants

Capability-aware ISA/memory model is not transparent to ISA. It requires explicit architecture work and cannot be smuggled in as secure-domain descriptor policy.

Layer 1 and Layer 2 must remain valid without:

- capability registers;
- tagged memory;
- capability-bearing operands;
- capability-aware memory access instructions;
- per-pointer provenance;
- new operand formats;
- new instruction encodings;
- new addressing modes.

## Proposed descriptors / policies

Future topics:

- capability registers and register-file interactions;
- tagged memory and tag propagation;
- capability-bearing operands in instruction objects and typed `InstructionIR`;
- capability-aware `LOAD` / `STORE` / `FETCH`;
- pointer provenance and monotonic derivation per pointer;
- sealed capabilities and unsealing authority;
- compiler facade/helper ABI for capability values;
- decoder/encoder ABI changes and golden artifacts;
- VLIW/EPIC typed-slot metadata for capability operands;
- scheduling/bundling/lane binding impact;
- Lane6 descriptor sideband transport of capability metadata;
- Lane7 system/control-plane interactions;
- retire/writeback/side-effect publication for capability faults;
- replay/rollback/conformance for tag/provenance state;
- formal Stage A/Stage B legality changes.

## Integration points

Future integration would touch:

- ISA evidence chain;
- CloseToHSL instruction objects;
- compiler facade/helper ABI;
- decoder/encoder ABI;
- typed `InstructionIR` / projection;
- typed `MicroOp` publication and materialization;
- VLIW/EPIC slot legality;
- vector legality contours;
- memory subsystem and checkpoint/migration format;
- conformance/golden artifacts.

This future work must start from a separate architecture proposal and must not be introduced as an incidental implementation detail of secure descriptors.

Any capability-aware ISA or memory change requires a new RFC / architecture decision record before code changes, including explicit ISA, ABI, decoder, encoder, memory, migration, compiler and conformance impacts.

## No-regression requirements

Before any future capability-aware ISA work:

- Layer 1/2 tests must remain green;
- ordinary non-secure ISA behavior must have explicit compatibility strategy;
- decoder/encoder changes must have golden artifacts;
- compiler no-emission boundary must be redefined for capability operands;
- VMX compatibility projection must remain non-authoritative;
- migration/checkpoint must classify tag/provenance state explicitly.
- current SecureCompute migration descriptors must not add provisional tag/provenance checkpoint fields as temporary surrogates.

## Tests and conformance

Future tests would need:

- capability register encoding/decoding tests;
- tagged memory propagation tests;
- capability-aware load/store/fetch legality tests;
- pointer provenance monotonicity tests;
- sealed capability tests;
- compiler ABI/golden artifact tests;
- VLIW/EPIC slot legality tests;
- migration/replay rollback tests for tags and provenance;
- VMX non-authority tests to prove compatibility projection did not become capability owner.

These tests are not part of Layer 1/Layer 2.

## Closure criteria

Future layer can begin only after Layer 1 and Layer 2 are closed with conformance proof. A new proposal must state which ISA, ABI, decoder, encoder, memory and migration contracts change.

## Forbidden shortcuts

- Do not add tagged memory inside secure memory descriptor work.
- Do not add capability registers under Layer 2 secure grant work.
- Do not reinterpret ordinary pointers as capabilities in existing instructions.
- Do not change `LOAD` / `STORE` / `FETCH` semantics under the name of secure admission.
- Do not hide decoder/encoder ABI changes in VMX compatibility work.
- Do not use VMCS/VmxCaps to advertise or own capability-aware ISA state.
- Do not add provisional tag/provenance checkpoint fields inside current SecureCompute migration descriptors.
- Do not transport capability metadata through current Layer 1/Layer 2 Lane6/Lane7 sideband envelopes.
- Do not start capability-aware ISA/memory work without a new RFC / architecture decision record.

## Open questions

- Transferred 2026-05-31 to `Plan2/14-securecompute-open-decision-backlog.md`: future capability-aware ISA profile shape remains open.
- Transferred 2026-05-31 to `Plan2/14-securecompute-open-decision-backlog.md`: future 2048-bit bundle legality for capability operands remains open.
- Transferred 2026-05-31 to `Plan2/14-securecompute-open-decision-backlog.md`: future tag/provenance migration format remains open.
- Resolved for current Layer 1/Layer 2 baseline: no provisional tag/provenance checkpoint fields may be added to current SecureCompute migration descriptors.
