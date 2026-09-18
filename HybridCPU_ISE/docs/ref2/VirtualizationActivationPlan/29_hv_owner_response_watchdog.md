# Phase 29 - Hypercall Owner Response Watchdog

Status: watchdog-only repeat check for attributable VMCALL owner response after the denied-chain refresh. No owner acceptance, exact leaf allocation, backend execution, completion publication, retire publication, replacement intake, watchdog timer activation, or production behavior change is approved by this document.

## 2026-06-11 Audit Contract

- File name: `29_hv_owner_response_watchdog.md`.
- Purpose: close the owner-response watchdog check after Phase 28 by recording that a repeated response check found no attributable external neutral-runtime-owner artifact and therefore leaves the denied chain unchanged.
- Status: watchdog closure only; no attributable owner accept/reject/amend artifact is found, no explicit replacement intake is selected, and no production chain step is activated in this repository revision.
- Scope: watchdog trigger, repeated owner response search, silence/no-timeout semantics, denied-chain preservation, and non-authority rules.
- No-goals: no RFC/ADR acceptance, no exact VMCALL leaf allocation, no `HypercallBackendAdmissionDecision.Allowed`, no `BackendExecutionAuthorized: true`, no backend executor, no VMX frontend wiring, no completion record, no completion publication, no retire publication, no VMCS mutation, no VMWRITE, no SecureCompute activation, no compiler emission, no lane/stream passthrough, no migration payload authority, no replacement intake selection, no watchdog timer as activation authority.
- Code anchors: `HypercallBackendAdmissionPolicy.cs`, `NeutralHypercallBackendOwnerDescriptor.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`.
- Authority owner: external neutral runtime owner for the exact VMCALL path; this watchdog, repository text, tests, scans, backlog rows, VMX/VMCS/`VmxCaps`, SecureCompute, compiler, migration, lane, stream, and timer/cadence evidence are not authority.
- Required RFC/ADR: an attributable owner-specific RFC/ADR response remains required with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: if no attributable owner artifact and no explicit process reselect decision are found, the watchdog does not move state and VMCALL remains `MissingNeutralOwner` / backend denied / `ProjectionOnlyDenied` / completion publication denied / retire publication denied / wait/denied/future-gated.
- Tests/static scans: keep missing-owner denial, draft-owner denial, exact-leaf absence, no `Allowed`, no `BackendExecutionAuthorized: true`, no VMX frontend `RuntimeOwnedPublication`, no handler-side `CompletionRecord`, no overclaim wording, no production source diff, and explicit watchdog-only wording.
- Risks: treating watchdog tick, repeated silence, timeout, elapsed time, stable denied chain, clean scans, green tests, or closed audits as an attributable owner response.
- Next-gate dependency: a later artifact-triggered recheck if a real external artifact appears, another watchdog-only wait-state check, or a separate future process-only decision that explicitly abandons waiting and selects exactly one replacement intake.

## ISE-HV-OWNER-RESPONSE-WATCHDOG-29 - Closure Record

Closure date: 2026-06-18.

State: closed `WATCHDOG-ONLY / NO-ATTRIBUTABLE-OWNER-RESPONSE / DENIED-CHAIN-PRESERVED / WAIT-DENIED-FUTURE-GATED / NO-PRODUCTION-CHANGE`.

Watchdog result: no attributable external neutral-runtime-owner artifact was found that accepts, rejects, or amends `RFC-HV-VMCALL-NO-STATE-OWNER-0001`; the denied production chain remains unchanged.

This closure records the watchdog-only response check. It does not accept an RFC/ADR, allocate an exact numeric VMCALL leaf, approve backend admission, authorize backend execution, publish completion, publish retire, replace `MissingNeutralOwner`, create a backend executor, construct a frontend completion record, select a replacement intake, start an activation timer, open release, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, memory, I/O, IOMMU, nested, host-alias, compatibility-control, tests, docs, scans, backlog wording, monitor cadence, artifact recheck, process checkpoint, denied-chain refresh, or watchdog cadence.

## Watchdog Evidence Matrix

| Watchdog input | Repository result | Watchdog result | Consequence |
| --- | --- | --- | --- |
| attributable accepted exact-leaf RFC/ADR | absent | no state move | Phase 06B/Phase 07 remain blocked |
| attributable rejected RFC/ADR | absent | no owner closure | draft remains unresolved |
| attributable amended owner packet | absent | no amended intake | Phase 20 packet remains pending |
| exact numeric VMCALL leaf in accepted owner artifact | absent | no leaf | all numeric VMCALL leaves remain denied |
| accepted neutral owner service | absent | no backend owner | `MissingNeutralOwner` remains production behavior |
| accepted completion/retire rules | absent | no publication path | completion and retire remain denied |
| explicit process reselect decision | absent | no replacement selected | Minimal VMCALL remains the selected wait target |
| watchdog tick or timeout | local process signal only | non-authority | no state transition |
| stable denied chain | present | non-authority | does not become implementation permission |

## Watchdog Non-Authority Rules

- Watchdog closure is not owner acceptance.
- Watchdog tick is not owner acceptance.
- Watchdog timeout is not owner acceptance.
- Repeated silence is not owner acceptance.
- Stable denied behavior is not owner acceptance.
- Stable denied behavior is not implementation permission.
- Preserving `MissingNeutralOwner` is not owner acceptance.
- Preserving `ProjectionOnlyDenied` is not backend success.
- Backend denial is not completion publication.
- Completion publication denial is not retire publication.
- Closing Phase 29 is not an accepted RFC/ADR, rejection, amendment, or replacement intake selection.
- Phase 20 packet, Phase 21 handoff, Phase 22 fallback, Phase 23 wait decision, Phase 24 late audit, Phase 25 monitor, Phase 26 artifact recheck, Phase 27 checkpoint, Phase 28 refresh, and this Phase 29 watchdog are not owner acceptance.
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

- watchdog-only response check is recorded;
- no attributable owner response is found;
- no replacement intake is selected;
- denied production chain remains explicit;
- no production behavior changes are introduced.

## Dependency On Previous/Next Phase

Depends on Phase 28 denied-chain refresh. The next valid transition is another artifact-triggered recheck if a real external owner artifact appears, another watchdog-only wait-state check, or a separate future process-only decision that abandons waiting and selects exactly one replacement intake from the backlog.
