# Phase 25 - Hypercall Wait State Monitor

Status: monitor-only cadence for attributable VMCALL owner response. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, replacement intake, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `25_hv_wait_state_monitor.md`.
- Purpose: close the monitor-only cadence after Phase 24 so the repository can periodically check for an attributable neutral-runtime-owner response without converting waiting into activation.
- Status: cadence closure only; no attributable owner accept/reject/amend artifact is found in this repository revision.
- Scope: monitor trigger, artifact search result, wait-state invariants, escalation boundaries, and non-authority rules.
- No-goals: no RFC/ADR acceptance, no exact VMCALL leaf allocation, no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority, no replacement intake selection.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for the exact VMCALL path; this monitor, repository text, tests, scans, backlog rows, VMX/VMCS/`VmxCaps`, SecureCompute, compiler, migration, lane, and stream evidence are not authority.
- Required RFC/ADR: an attributable owner-specific RFC/ADR response remains required with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: if no attributable owner response is found, VMCALL remains `MissingNeutralOwner` / `ProjectionOnlyDenied` / wait/denied/future-gated.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, no overclaim wording, no production source diff, and explicit monitor-only wording.
- Risks: treating monitor cadence, repeated absence, elapsed time, clean scans, green tests, or backlog age as owner acceptance.
- Next-gate dependency: a later wait-state monitor, an attributable owner response audit, or a future process-only decision that explicitly abandons waiting and selects one replacement intake.

## ISE-HV-WAIT-STATE-MONITOR-25 - Closure Record

Closure date: 2026-06-18.

State: closed `MONITOR-ONLY-CADENCE / NO-ATTRIBUTABLE-OWNER-RESPONSE / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`.

Monitor result: no attributable external neutral-runtime-owner artifact was found that accepts, rejects, or amends `RFC-HV-VMCALL-NO-STATE-OWNER-0001`.

This closure records the wait-state monitor cadence only. It does not accept an RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, select a replacement intake, open release, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, tests, docs, scans, backlog wording, or monitor cadence.

## Monitor Cadence Matrix

| Monitor input | Repository result | Consequence |
| --- | --- | --- |
| accepted exact-leaf RFC/ADR | absent | Phase 06B/Phase 07 remain blocked |
| rejected RFC/ADR | absent | draft remains unresolved |
| amended owner packet | absent | Phase 20 packet remains pending |
| exact numeric VMCALL leaf | absent | all numeric VMCALL leaves remain denied |
| accepted argument/result ABI | absent | no runtime leaf-value, descriptor-value, or result contract |
| accepted neutral owner service | absent | `MissingNeutralOwner` remains production behavior |
| accepted capability/evidence/migration map | absent | no backend implementation review may open |
| accepted completion/retire rules | absent | Phase 08/Phase 09 remain separate future-gated gates |
| accepted secure-domain behavior | absent | non-secure/secure-no-effect remains only a requested classification, not accepted semantics |
| replacement intake decision | absent | Minimal VMCALL remains the selected wait target |

## Monitor Non-Authority Rules

- Monitor-only cadence is not owner acceptance.
- Repeated no-response is not owner acceptance.
- Backlog age is not owner acceptance.
- Elapsed time is not owner acceptance.
- Clean scans and green tests are not owner acceptance.
- Closing Phase 25 is not a replacement intake selection.
- Phase 20 packet, Phase 21 handoff, Phase 22 fallback, Phase 23 wait decision, Phase 24 late audit, and this Phase 25 monitor are not owner acceptance.
- Exact leaf class, operation class, opcode, exit reason, owner ID, register selector, SecureCompute fixture value, or VMFUNC leaf is not an exact numeric VMCALL leaf.
- SecureCompute `AllowedSecureOperation` is admission-only, and `AllowedProofOnlyNoExecution` is proof-only/no-execution.
- Backend success, if later authorized by a neutral owner, remains separate from completion publication.
- Completion publication, if later authorized by a completion owner, remains separate from retire publication.

## Current Wait State

Until an attributable accepted neutral-runtime-owner artifact exists for the VMCALL packet:

- `RFC-HV-VMCALL-NO-STATE-OWNER-0001` remains draft/pending;
- exact numeric VMCALL leaf remains not proven;
- Phase 06B remains blocked/future-gated;
- Phase 07 remains blocked/future-gated;
- production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`;
- route remains `ProjectionOnlyDenied`;
- completion and retire remain denied/future-gated;
- replacement intake remains not selected.

## Exit Criteria

- monitor-only cadence is recorded;
- no attributable owner response is found;
- wait/denied/future-gated status remains explicit;
- no production behavior changes are introduced.

## Dependency On Previous/Next Phase

Depends on Phase 24 late owner-response audit. The next valid transition is another monitor-only wait-state check, an attributable owner response audit if a real external artifact appears, or a later process-only decision that abandons waiting and selects one replacement intake from the backlog.
