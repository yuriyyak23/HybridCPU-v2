# Phase 27 - Hypercall Wait Or Reselect Checkpoint

Status: process-only checkpoint for continuing wait-state or explicitly selecting a future replacement intake. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, replacement intake, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `27_hv_wait_or_reselect_checkpoint.md`.
- Purpose: close the wait-or-reselect checkpoint after Phase 26 by recording that no attributable owner artifact exists and no explicit process reselect decision is made.
- Status: checkpoint closure only; no attributable owner accept/reject/amend artifact is found, and no replacement intake is selected in this repository revision.
- Scope: current wait-state, replacement-intake decision boundary, absence of owner artifact, explicit no-reselect result, and non-authority rules.
- No-goals: no RFC/ADR acceptance, no exact VMCALL leaf allocation, no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority, no replacement intake selection.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for the exact VMCALL path; this checkpoint, repository text, tests, scans, backlog rows, VMX/VMCS/`VmxCaps`, SecureCompute, compiler, migration, lane, and stream evidence are not authority.
- Required RFC/ADR: an attributable owner-specific RFC/ADR response remains required with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: if no attributable owner artifact and no explicit process reselect decision are found, the selected path remains wait-state and VMCALL remains `MissingNeutralOwner` / `ProjectionOnlyDenied` / wait/denied/future-gated.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, no overclaim wording, no production source diff, and explicit no-reselect wording.
- Risks: treating checkpoint closure, candidate availability, backlog priority, clean scans, green tests, elapsed time, or no-response fatigue as replacement intake selection or owner acceptance.
- Next-gate dependency: a later artifact-triggered recheck if a real external artifact appears, another monitor-only wait-state check, or a separate future process-only reselect decision that explicitly abandons waiting and selects exactly one replacement intake.

## ISE-HV-WAIT-OR-RESELECT-CHECKPOINT-27 - Closure Record

Closure date: 2026-06-18.

State: closed `PROCESS-CHECKPOINT / WAIT-CONTINUES / NO-REPLACEMENT-SELECTED / NO-ATTRIBUTABLE-OWNER-ARTIFACT / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`.

Checkpoint result: continue waiting for an attributable external neutral-runtime-owner artifact for `RFC-HV-VMCALL-NO-STATE-OWNER-0001`; no replacement intake is selected.

This closure records the wait-or-reselect checkpoint only. It does not accept an RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, select a replacement intake, open release, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, tests, docs, scans, backlog wording, monitor cadence, artifact recheck, or process checkpoint.

## Checkpoint Decision Matrix

| Decision input | Repository result | Checkpoint result | Consequence |
| --- | --- | --- | --- |
| attributable accepted exact-leaf RFC/ADR | absent | no activation | Phase 06B/Phase 07 remain blocked |
| attributable rejected RFC/ADR | absent | no closure by owner | draft remains unresolved |
| attributable amended owner packet | absent | no amended intake | Phase 20 packet remains pending |
| exact numeric VMCALL leaf in accepted owner artifact | absent | no leaf | all numeric VMCALL leaves remain denied |
| accepted neutral owner service | absent | no backend owner | `MissingNeutralOwner` remains production behavior |
| accepted capability/evidence/migration map | absent | no backend review | no backend implementation review may open |
| accepted completion/retire rules | absent | no publication path | Phase 08/Phase 09 remain separate future-gated gates |
| explicit process reselect decision | absent | no replacement selected | Minimal VMCALL remains the selected wait target |
| replacement candidate ranking | present in backlog only | non-authority | ranking does not choose a replacement |

## Checkpoint Non-Authority Rules

- Wait-or-reselect checkpoint closure is not owner acceptance.
- Continuing to wait is not owner acceptance.
- Not selecting a replacement is not owner acceptance.
- Not selecting a replacement is not implementation permission.
- Replacement candidate availability is not replacement intake selection.
- Backlog priority and candidate ranking are not replacement intake selection.
- Clean scans, green tests, backlog age, and elapsed time are not replacement intake selection.
- Closing Phase 27 is not an accepted RFC/ADR, rejection, amendment, or replacement intake selection.
- Phase 20 packet, Phase 21 handoff, Phase 22 fallback, Phase 23 wait decision, Phase 24 late audit, Phase 25 monitor, Phase 26 artifact recheck, and this Phase 27 checkpoint are not owner acceptance.
- Exact leaf class, operation class, opcode, exit reason, owner ID, register selector, SecureCompute fixture value, or VMFUNC leaf is not an exact numeric VMCALL leaf.
- SecureCompute `AllowedSecureOperation` is admission-only, and `AllowedProofOnlyNoExecution` is proof-only/no-execution.
- Backend success, if later authorized by a neutral owner, remains separate from completion publication.
- Completion publication, if later authorized by a completion owner, remains separate from retire publication.

## Current Wait State

Until an attributable accepted neutral-runtime-owner artifact exists for the VMCALL packet, or a separate future process-only decision explicitly abandons waiting and selects exactly one replacement intake:

- `RFC-HV-VMCALL-NO-STATE-OWNER-0001` remains draft/pending;
- exact numeric VMCALL leaf remains not proven;
- Phase 06B remains blocked/future-gated;
- Phase 07 remains blocked/future-gated;
- production VMCALL remains `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`;
- route remains `ProjectionOnlyDenied`;
- completion and retire remain denied/future-gated;
- replacement intake remains not selected.

## Exit Criteria

- wait-or-reselect checkpoint is recorded;
- no attributable owner artifact is found;
- no replacement intake is selected;
- wait/denied/future-gated status remains explicit;
- no production behavior changes are introduced.

## Dependency On Previous/Next Phase

Depends on Phase 26 owner artifact recheck. The next valid transition is another artifact-triggered recheck if a real external owner artifact appears, another monitor-only wait-state check, or a separate future process-only decision that abandons waiting and selects exactly one replacement intake from the backlog.
