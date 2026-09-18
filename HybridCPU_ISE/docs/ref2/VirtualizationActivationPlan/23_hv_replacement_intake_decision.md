# Phase 23 - Hypercall Replacement Intake Decision

Status: process-only replacement-intake decision. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `23_hv_replacement_intake_decision.md`.
- Purpose: choose whether to keep waiting for the VMCALL owner response or select one replacement owner-specific intake from the Phase 19 backlog after Phase 22 fallback.
- Status: process/readiness decision only; selected decision is to continue waiting for an attributable VMCALL owner response.
- Scope: wait-versus-replacement decision, replacement-candidate matrix, non-selected backlog items, blocked VMCALL consequences, and non-authority rules.
- No-goals: no RFC/ADR acceptance, no exact VMCALL leaf allocation, no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for any exact positive path; this process decision, backlog priority, docs, tests, scans, VMX/VMCS/`VmxCaps`, SecureCompute, compiler, migration, lane, and stream evidence are not authority.
- Required RFC/ADR: VMCALL still requires an attributable owner-specific RFC/ADR response; any future replacement intake would require its own owner-specific RFC/ADR packet with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: replacement is not selected in this phase; every replacement candidate remains denied/future-gated; VMCALL remains `MissingNeutralOwner` / `ProjectionOnlyDenied` / blocked until attributable owner response.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, no overclaim wording, and no production source diff.
- Risks: treating the wait decision, replacement non-selection, candidate ranking, or backlog matrix as owner acceptance, activation, or backend implementation permission.
- Next-gate dependency: late attributable VMCALL owner response audit; a replacement intake may be selected only by a later process-only decision if waiting is abandoned.

## ISE-HV-REPLACEMENT-INTAKE-DECISION-23 - Closure Record

Closure date: 2026-06-18.

State: closed `PROCESS-ONLY WAIT DECISION / NO-REPLACEMENT-SELECTED / VMCALL-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`.

Decision: continue waiting for an attributable external neutral-runtime-owner response to `RFC-HV-VMCALL-NO-STATE-OWNER-0001`.

Replacement intake selected: none.

This closure does not accept an RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, open release, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, tests, docs, or backlog wording.

## Decision Matrix

| Option | Phase 23 decision | Required future artifact | Runtime consequence |
| --- | --- | --- | --- |
| continue waiting for VMCALL owner response | selected | attributable neutral-runtime-owner accept/reject/amend response for `RFC-HV-VMCALL-NO-STATE-OWNER-0001` | VMCALL remains denied/future-gated until received |
| choose `GuestCr0`/`GuestCr4` projection widening | not selected | separate owner-specific RFC/ADR for any additional field, mutation, backend, completion, retire, or migration behavior | current guarded read-only projection only |
| choose VMWRITE | not selected | neutral write owner per state class | all writes remain denied |
| choose nested child intent | not selected | neutral child-intent owner RFC/ADR | nested execution remains denied/future-gated |
| choose SecureCompute VMX visibility/backend | not selected | secure runtime owner RFC/ADR plus virtualization boundary proof | SecureCompute activation through VMX/VMCS/`VmxCaps` remains denied |
| choose Lane6/Lane7/Stream passthrough | not selected | memory/I/O/lane owner RFC/ADR with evidence non-leak | passthrough and payload authority remain denied |
| choose compiler controlled emission | not selected | compiler emission RFC tied to accepted runtime owner RFC/ADR | VMX/SecureCompute/lane/stream emission remains denied/future-gated |
| choose migration payloads for active path | not selected | neutral migration descriptor only after an accepted active path exists | checkpoint/restore payload authority remains denied |
| choose release claim wording | not selected | Phase 18 release review only after exact implementation chain exists | broad release claim remains denied |

## Non-Authority Rules

- Choosing to wait is not owner acceptance.
- Not selecting a replacement is not owner acceptance.
- Replacement candidate ranking is not implementation permission.
- Backlog priority is not implementation permission.
- Waiting does not allocate an exact VMCALL leaf.
- Waiting does not reopen Phase 06B or Phase 07.
- A future replacement intake selection would still be process-only and would not open implementation.
- Clean scans and green tests do not authorize backend execution.
- SecureCompute `AllowedSecureOperation` and `AllowedProofOnlyNoExecution` do not satisfy VMCALL backend execution.
- Backend success, if later authorized by a neutral owner, remains separate from completion publication.
- Completion publication, if later authorized by a completion owner, remains separate from retire publication.

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

## Exit Criteria

- wait-vs-replacement decision is explicit;
- decision is to keep waiting for attributable VMCALL owner response;
- no replacement intake is selected;
- every replacement candidate remains denied/future-gated;
- no production behavior changes are introduced.

## Dependency On Previous/Next Phase

Depends on Phase 22 no-response fallback. The next valid transition is a late attributable VMCALL owner response audit or a later process-only decision that explicitly abandons waiting and selects one replacement intake from the backlog.
