# Phase 28 - Hypercall Denied Chain Refresh

Status: readiness-only refresh of the current VMCALL denied production chain. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, replacement intake, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `28_hv_denied_chain_refresh.md`.
- Purpose: close the denied-chain refresh after Phase 27 by recording the current production VMCALL chain as `MissingNeutralOwner` -> backend denied -> `ProjectionOnlyDenied` -> completion publication denied -> retire publication denied.
- Status: denied-chain refresh closure only; no attributable owner accept/reject/amend artifact is found, no explicit replacement intake is selected, and no production chain step is activated in this repository revision.
- Scope: VMX frontend admission request, backend denial, trap completion route denial, completion publication fence denial, retire publication denial, and non-authority rules.
- No-goals: no RFC/ADR acceptance, no exact VMCALL leaf allocation, no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority, no replacement intake selection.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for the exact VMCALL path; this refresh, repository text, tests, scans, backlog rows, VMX/VMCS/`VmxCaps`, SecureCompute, compiler, migration, lane, and stream evidence are not authority.
- Required RFC/ADR: an attributable owner-specific RFC/ADR response remains required with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: if no attributable owner artifact and no explicit process reselect decision are found, the production chain remains `MissingNeutralOwner` / backend denied / `ProjectionOnlyDenied` / completion publication denied / retire publication denied / wait/denied/future-gated.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, no overclaim wording, no production source diff, and explicit denied-chain wording.
- Risks: treating denied-chain refresh, clean scans, green tests, existing route scaffolding, completion fence scaffolding, or closed audits as permission to replace any denied link with activation.
- Next-gate dependency: a later artifact-triggered recheck if a real external artifact appears, another monitor-only wait-state check, or a separate future process-only decision that explicitly abandons waiting and selects exactly one replacement intake.

## ISE-HV-DENIED-CHAIN-REFRESH-28 - Closure Record

Closure date: 2026-06-18.

State: closed `DENIED-CHAIN-REFRESH / MISSING-OWNER-PRESERVED / PROJECTION-ONLY-DENIED / COMPLETION-RETIRE-DENIED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`.

Refresh result: current production VMCALL remains denied from admission through completion and retire because no attributable external neutral-runtime-owner artifact exists for `RFC-HV-VMCALL-NO-STATE-OWNER-0001`, and no replacement intake is selected.

This closure records the denied production chain only. It does not accept an RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, select a replacement intake, open release, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, tests, docs, scans, backlog wording, monitor cadence, artifact recheck, process checkpoint, route scaffolding, or completion fence scaffolding.

## Denied Chain Matrix

| Chain link | Current evidence | Required owner artifact to change | Consequence |
| --- | --- | --- | --- |
| VMCALL owner | `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` | accepted neutral runtime owner for exact VMCALL leaf | backend admission remains denied |
| backend admission | no `HypercallBackendAdmissionDecision.Allowed` | accepted exact-leaf RFC/ADR with full owner map | backend execution remains unauthorized |
| backend execution | no `BackendExecutionAuthorized: true` | backend owner execution contract and capability policy | no backend executor may run |
| completion route | `TrapCompletionRouteRequest.ProjectionOnlyDenied(...)` | completion owner route contract after backend success | route remains admitted-denied/projection-only |
| completion publication | fence result has `CompletionPublicationAllowed == false` | neutral completion publication owner | no completion record may be constructed in frontend |
| retire publication | fence result has `RetirePublicationAllowed == false` | separate retire owner and evidence contract | no retire publication may occur |
| replacement intake | absent | separate process-only reselect decision | Minimal VMCALL remains the selected wait target |

## Refresh Non-Authority Rules

- Denied-chain refresh is not owner acceptance.
- Preserving `MissingNeutralOwner` is not owner acceptance.
- Preserving `ProjectionOnlyDenied` is not backend success.
- Backend denial is not completion publication.
- Completion publication denial is not retire publication.
- Route scaffolding is not VMX frontend runtime publication.
- Completion fence scaffolding is not frontend completion construction.
- Clean scans, green tests, closed audits, and current denied behavior are not implementation permission.
- Closing Phase 28 is not an accepted RFC/ADR, rejection, amendment, or replacement intake selection.
- Phase 20 packet, Phase 21 handoff, Phase 22 fallback, Phase 23 wait decision, Phase 24 late audit, Phase 25 monitor, Phase 26 artifact recheck, Phase 27 checkpoint, and this Phase 28 refresh are not owner acceptance.
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
- backend execution remains unauthorized;
- route remains `ProjectionOnlyDenied`;
- completion publication remains denied;
- retire publication remains denied;
- replacement intake remains not selected.

## Exit Criteria

- denied-chain refresh is recorded;
- no attributable owner artifact is found;
- no replacement intake is selected;
- missing-owner admission, projection-only route, completion denial, and retire denial remain explicit;
- no production behavior changes are introduced.

## Dependency On Previous/Next Phase

Depends on Phase 27 wait-or-reselect checkpoint. The next valid transition is another artifact-triggered recheck if a real external owner artifact appears, another monitor-only wait-state check, or a separate future process-only decision that abandons waiting and selects exactly one replacement intake from the backlog.
