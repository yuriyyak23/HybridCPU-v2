# SecureCompute Open Decision Backlog

## Purpose

This file is the single open-decision backlog after Phase 10 and the Post-Phase10 owner/RFC proof-gate closure. Items listed here are intentionally not closed by the current SecureCompute baseline.

Phase 14 is an open-decision backlog, not an implementation phase.
No item in this file authorizes code changes without a separate RFC/ADR and phase plan.

SecureCompute remains a neutral runtime descriptor/admission discipline. This backlog must not be read as permission to add VMX mode support, secure VMCS state, `VmxCaps` authority, CHERI-like ISA, tagged memory, capability registers or positive secure backend runtime execution.

`SecureCompute RFC HybridCPU-v2.md` is advisory RFC input only. It does not authorize implementation, publication, retire effects or backend-visible success.

## Closed Baseline

- [x] Phase 0-10 are closed only as their stated status classes: docs-only, shell/no-effect, fail-closed policy admission, positive policy admission, negative conformance, design fence or release gate.
- [x] Phase 7 is closed as runtime descriptor/grant monotonicity baseline, not CHERI ISA.
- [x] Phase 8 is closed as VMX compatibility deny/projection matrix, not secure VMCS.
- [x] Phase 9 is closed as nested secure domain design fence, not nested SecureCompute execution.
- [x] Phase 10 is closed as conformance/doc/source release gate, not feature-complete SecureCompute.
- [x] Post-Phase10 owner/RFC proof gate is closed as policy-evidence admission only; `AllowedProofOnlyNoExecution` does not authorize runtime execution.
- [x] Phase 14 backlog hardening closed on 2026-05-31: Plan2 is a quarantine for unresolved decisions and RFC inputs, not a product implementation phase.

## Open Decisions

- [ ] Positive secure backend runtime execution owner/RFC: unopened. Requires a separate implementation phase, approved RFC/ADR, neutral runtime owner implementation beyond proof-only admission, end-to-end proof chain, new denied tests, production-claim audit and explicit proof that no VMX/VMCS/`VmxCaps` path can authorize execution. `AllowedProofOnlyNoExecution` must remain the maximum result until a separate runtime-execution phase exists. No policy-evidence admission may publish completion, retire effects or backend-visible success.
- [ ] SecureCompute compatibility advertisement policy: decide whether Layer 1/2 keeps zero VMX exposure permanently or permits a future read-only compatibility advertisement. Default decision: zero VMX exposure. Any advertisement requires explicit neutral evidence owner, read-only projection, migration class and conformance proof. Any future advertisement must be a projection of neutral publication/evidence only; `VmxCapsProjection` must not grant, activate or own SecureCompute.
- [ ] Secure visibility alias placement: if a future secure visibility alias exists, decide whether it belongs in VMX schema or a separate neutral debug/attestation API. Preferred default: separate neutral debug/attestation API. VMX schema alias is allowed only as generated projection and must remain denied by default. Default remains no VMX state ownership and denial unless neutral evidence admission explicitly allows projection.
- [ ] Future capability-aware ISA profile shape: decide between a separate opt-in ISA profile, a domain mode or a new operand class with explicit decoder support. This is Plan 13 future work, not Layer 1/Layer 2 product code. These decisions require a new repository-level architecture proposal. They must not create placeholder product types under current SecureCompute Layer 1/2 namespaces.
- [ ] Future 2048-bit bundle legality for capability operands: define how capability operands would be encoded without breaking existing typed-slot guarantees, VLIW/EPIC legality or ordinary instruction behavior. No provisional capability operand metadata may be added to current typed slots, bundle legality or compiler helper ABI. These decisions require a new repository-level architecture proposal.
- [ ] Future tag/provenance migration format: define how tags/provenance could be migrated without treating host evidence as guest state and without adding provisional tag/provenance checkpoint fields to current SecureCompute migration descriptors. Current SecureMigrationDescriptor must not grow provisional tag/provenance payload classes. Future tag/provenance migration requires separate format, restore validation and host-evidence separation proof.

## Transfer Notes

- Plan 09 open VMX advertisement questions are tracked here; the default answer remains zero VMX authority.
- Plan 11 future production runtime-execution coverage is tracked here; current tests prove denial and policy evidence only.
- Plan 13 capability-aware ISA/memory questions are tracked here; current Layer 1/2 code has an import ban.
- Phase 14/Plan2 remains a decision backlog requiring separate RFC/ADR, new phase plans, negative tests and proof chains before any implementation work.
