# Nested Secure Domain Plan

## Purpose

Design fence only: this plan does not implement nested secure backend entry, nested secure execution, or mutable nested secure runtime state.

Документ проектирует future-safe direction для nested secure domains. Цель - сохранить нейтральное владение nested state and policy, не возвращаясь к mutable Shadow VMCS / VMCS12 / VMCS02 as runtime authority.

## Scope

План покрывает neutral child-intent descriptor, parent/child monotonic policy, nested memory composition, evidence separation, completion mapping and migration checkpoint validation.

## Non-goals

Документ не реализует nested secure domains, не открывает Shadow VMCS state, не делает VMCS12/VMCS02 authority, не добавляет VMX nested runtime manager и не расширяет ISA.

## Architectural invariants

Nested secure direction must be descriptor-first. Shadow VMCS, VMCS12 and VMCS02 may exist only as compatibility projection vocabulary, not mutable source of truth. The only admissible design direction in this plan is neutral child-intent descriptor plus monotonic parent/child policy.

Parent secure domain authority bounds child secure domain authority. Child policy cannot exceed parent memory, I/O, hypercall, debug, migration, evidence or compatibility projection policy.

`SecureChildDomainIntentDescriptor` is an admission input and provenance object. It must not become a long-lived mutable execution-state store and must not become a hidden VMCS12-equivalent authority container.

Subset validation may pass as policy validation, but backend execution remains denied/design-fenced until a separate positive-owner phase defines neutral runtime owners, state materialization, memory composition authority and tests.

## Implementation status

- [x] Phase 9 nested secure domain design fence closed on 2026-05-31.
- [x] `SecureNestedDomainAdmissionPolicy` admits only a design-fence result: no backend success, no mutable nested secure runtime state and no implemented nested SecureCompute execution path.
- [x] Neutral child-intent admission denies missing child-intent owner, missing active parent secure descriptor, missing provenance, stale parent/child epoch and any parent/child authority widening.
- [x] Nested policy widening is fail-closed for I/O, hypercall, debug, migration payload and compatibility projection bounds.
- [x] Nested host-evidence leakage and nested projection expansion beyond the parent policy are denied.
- [x] Shadow VMCS remains compatibility bridge vocabulary only; mutable Shadow VMCS authority, VMCS12 authority and VMCS02 authority are denied.
- [x] Tests cover the required Phase 9 no-regression matrix in `HybridCPU_ISE.Tests/SecureComputeRefactoring` and `HybridCPU_ISE.Tests/VmxRefactoring`.
- [x] Phase 10 release gate closed on 2026-05-31: doc-lint, source guards, status-label audit and production-claim audit prove this plan remains design-fence-only.
- [x] Post-Phase10 owner/RFC proof gate closed on 2026-05-31: neutral backend owner proof, approved RFC/ADR requirement and negative tests are covered without nested backend entry.
- [x] Future runtime-execution decision transferred to `Plan2/14-securecompute-open-decision-backlog.md`: nested or other secure backend execution remains unopened pending a separate implementation phase and decision record.

## Proposed descriptors / policies

Future `SecureChildDomainIntentDescriptor` should describe:

- parent `DomainTag`;
- child requested `DomainTag`;
- requested `SecurityLevel`;
- requested memory classes and address-space composition;
- requested measurement/evidence/migration/debug/projection policy;
- requested I/O/hypercall authority;
- derivation provenance;
- parent policy digest and epoch;
- monotonicity proof status.

Nested policy rules:

- child secure domain cannot expand host inspection beyond parent;
- child cannot make private memory more host-visible than parent permits;
- child cannot serialize more evidence than parent migration policy allows;
- child cannot open more compatibility projection fields than parent;
- child cannot weaken debug policy;
- child cannot bypass parent revocation/epoch state.

## Integration points

`NestedDomainDescriptor`, `NestedMemoryDomainComposer`, `NestedMemoryCompositionService`, `NestedProjectionService` and compatibility nested projection code should remain neutral/projection-only.

Nested memory composition cannot materialize child runtime state until a separate positive-owner phase exists. Until then, any composed memory policy is validation evidence only, not child execution authority.

Runtime admission sequence:

1. receive neutral child-intent descriptor;
2. validate parent secure descriptor is materialized;
3. validate monotonic derivation;
4. compose neutral memory policy;
5. separate parent/child evidence classes;
6. map completion/retire publication;
7. classify migration payload;
8. only then expose optional compatibility projection.

## No-regression requirements

- No mutable Shadow VMCS state owner.
- No VMCS12/VMCS02 authority.
- No nested enablement from VMREAD projection values.
- No compatibility metadata as checkpoint authority.
- No host evidence leakage from parent to child or child to parent.
- Existing nested fail-closed readiness remains.

## Tests and conformance

Required tests:

- missing child-intent owner -> denied;
- missing parent secure descriptor -> denied for secure child;
- child policy > parent policy -> denied;
- child compatibility projection > parent allowed -> denied;
- child migration payload > parent allowed -> denied;
- host evidence leakage -> denied;
- nested projection cannot open more than parent allowed;
- Shadow VMCS remains compatibility bridge only;
- nested checkpoint rejects VMCS12/VMCS02 authority;
- stale parent/child epoch fails admission.

## Closure criteria

Nested secure planning closes when future nested support has a neutral descriptor path, monotonic policy model and fail-closed tests, without implementing mutable VMX nested state. Shell/design-only nested work must be labeled as design fence, not implemented nested SecureCompute.

## Forbidden shortcuts

- Do not use Shadow VMCS as state owner.
- Do not restore VMCS12/VMCS02 as authority.
- Do not derive child policy from VMREAD projection values.
- Do not reuse parent host evidence as child guest evidence.
- Do not bypass parent secure policy with VMX compatibility controls.

## Open questions

- Resolved 2026-05-31: Phase 9 closes only a design fence. Nested secure backend entry remains not implemented; admission can validate a neutral child-intent subset but does not authorize backend success.
- Resolved 2026-05-31: VMCS12/VMCS02 and mutable Shadow VMCS are rejected as checkpoint authority. Future restore should re-submit or rederive neutral child-intent descriptors rather than treating VMX nested state as authority.
