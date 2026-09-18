# Phase 22 - Hypercall No-Response Fallback

Status: no-response fallback and rollback record only. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `22_hv_no_response_fallback.md`.
- Purpose: define the fallback after Phase 21 found no attributable neutral-runtime-owner response for `RFC-HV-VMCALL-NO-STATE-OWNER-0001`.
- Status: process/readiness closure only; VMCALL backend path remains denied/future-gated.
- Scope: no-response fallback choices, rollback-to-wait state, replacement intake selection discipline, blocked VMCALL consequences, and non-authority rules.
- No-goals: no RFC/ADR acceptance, no exact VMCALL leaf allocation, no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for any exact positive path; fallback choice, backlog priority, docs, tests, scans, VMX/VMCS/`VmxCaps`, SecureCompute, compiler, migration, lane, and stream evidence are not authority.
- Required RFC/ADR: an attributable owner-specific RFC/ADR response remains required for VMCALL, or a replacement owner-specific RFC/ADR intake must be selected from the backlog with its own owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: absent an attributable owner response, VMCALL remains `MissingNeutralOwner` / `ProjectionOnlyDenied` / blocked; selecting a replacement intake is only a new process queue decision and grants no implementation permission.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, no overclaim wording, and no production source diff.
- Risks: treating no-response fallback, replacement selection, backlog reprioritization, or rollback wording as activation, owner acceptance, or backend implementation permission.
- Next-gate dependency: either an attributable VMCALL owner response, or a separately selected replacement owner-specific intake packet that remains denied/future-gated until accepted by its own neutral owner.

## ISE-HV-NO-RESPONSE-ROLLBACK-22 - Closure Record

Closure date: 2026-06-18.

State: closed `NO-RESPONSE FALLBACK / WAIT-OR-RESELECT / VMCALL-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`.

Fallback result:

- default action: wait for an attributable external neutral-runtime-owner response to `RFC-HV-VMCALL-NO-STATE-OWNER-0001`;
- optional action: choose a replacement owner-specific intake from Phase 19 backlog in a later process-only selection;
- prohibited action: treat no-response, fallback, replacement discussion, or backlog priority as implementation permission.

This closure does not accept an RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, open release, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, tests, docs, or backlog wording.

## Fallback Decision Matrix

| Fallback option | Current decision | Required external artifact | Runtime consequence |
| --- | --- | --- | --- |
| wait for VMCALL owner response | selected default | attributable neutral-runtime-owner accept/reject/amend response for `RFC-HV-VMCALL-NO-STATE-OWNER-0001` | VMCALL remains denied/future-gated until received |
| re-audit for late VMCALL owner response | allowed as process work | new attributable artifact in repository or external evidence record | no production change unless accepted exact-leaf RFC/ADR later exists |
| choose replacement backlog intake | allowed only as future process-only selection | new Phase 19-style intake selection plus owner-facing packet for that replacement | replacement also remains denied/future-gated until its owner accepts |
| implement VMCALL backend despite no response | prohibited | none can satisfy this path | forbidden shortcut |
| infer acceptance from silence or elapsed time | prohibited | none can satisfy this path | forbidden shortcut |
| use Phase 20/21 documents as authority | prohibited | none can satisfy this path | forbidden shortcut |

## Replacement Intake Discipline

If a replacement intake is chosen later, it must:

- come from an explicit backlog row;
- name one candidate owner-specific RFC/ADR intake;
- keep every non-selected backlog item denied/future-gated;
- prepare a new owner-facing packet before any implementation;
- require an attributable external owner accept/reject/amend artifact;
- preserve the same semantic ladder: admission != execution; backend success != completion publication; completion publication != retire publication; proof-only != activation approval; admitted-denied != backend success.

Replacement selection must not reuse VMCALL packet evidence as authority for GuestCr0/GuestCr4 widening, VMWRITE, nested, SecureCompute, Lane6/Lane7/Stream, compiler emission, migration payloads, release claims, memory/I/O/IOMMU passthrough, host aliases, or compatibility controls.

## VMCALL Blocked State

Until an attributable accepted neutral-runtime-owner artifact exists for the VMCALL packet:

- `RFC-HV-VMCALL-NO-STATE-OWNER-0001` remains draft/pending;
- exact numeric VMCALL leaf remains not proven;
- Phase 06B remains blocked/future-gated;
- Phase 07 remains blocked/future-gated;
- production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`;
- route remains `ProjectionOnlyDenied`;
- completion and retire remain denied/future-gated;
- all adjacent numeric VMCALL leaves remain denied.

## Non-Authority Rules

- No-response fallback is not owner acceptance.
- Rollback-to-wait is not owner acceptance.
- Replacement intake discussion is not owner acceptance.
- Backlog reprioritization is not implementation permission.
- Waiting does not allocate an exact leaf.
- Re-auditing does not allocate an exact leaf.
- Choosing a replacement intake does not open that replacement path.
- Green tests and clean scans do not authorize backend execution.
- SecureCompute `AllowedSecureOperation` and `AllowedProofOnlyNoExecution` do not satisfy VMCALL backend execution.
- Completion publication, if ever allowed, remains separate from retire publication.

## Exit Criteria

- no-response fallback is explicit;
- default action is wait for attributable owner response;
- replacement intake remains a future process-only option;
- VMCALL backend path remains denied/future-gated;
- no production behavior changes are introduced.

## Dependency On Previous/Next Phase

Depends on Phase 21 no-response handoff audit. The next valid transition is either a late attributable VMCALL owner response audit or a new replacement intake selection from the backlog, both process-only until accepted by the relevant neutral owner.
