# Phase 07 - Future-Gated VMCALL Backend Success Path Plan

Status: historical E3/E4 implementation plan. E2-E7 are now closed for the exact probe; compatibility VMX remains fault-only, while the separately closed development-local exact profile remains default-disabled until explicit activation.

Current-state precedence: statements below that E2-E7, completion, or retire are blocked describe dated checkpoints. Current status is governed only by `VirtualizationActivationStatusV1.json`; historical text is retained as the implementation journal.

## 2026-08-09 PR-F Canonical Composition Closure

The attributable D2/O1 and PR-D/PR-E prerequisites are now consumed only by a
neutral production-compiled composition object. The scheduler has no default
binding. When an exact live binding is explicitly configured, the existing
lane-7 E1 plus one-time operand seam asks the canonical SafetyVerifier for E2
and attaches a private carrier-bound dispatch. Only `VmxMicroOp.Execute` can use
that dispatch to reach the exact executor once. `InvokeHypercall` is a distinct
neutral runtime operation and `ProjectCompatibilityTrap` remains projection-only.

Disable/replay/capability revocation before execute denies E3. Zero, adjacent and
high-bit leaves never prepare E2. No compatibility handler calls the composition
or executor, and no `CompletionRecord`, E5, E6 or successful retire is produced.

## 2026-06-11 Audit Contract

- File name: `07_vmcall_success_path_activation_plan.md`.
- Purpose: describe a future-gated path from VMCALL admitted-denied to backend execution for exactly one owner-specific leaf after accepted Phase 06 RFC/ADR.
- Status: `future-gated`; current VMCALL remains admitted-denied, not backend success.
- Scope: materialized backend owner, leaf executor, backend result, downstream route request, derived compatibility projection.
- No-goals: no current success path, no completion publication, no retire publication, no SecureCompute/nested/VMWRITE/lane/compiler activation.
- Code anchors: `VmxCompatibilityAdmissionService.Traps.cs`, `HypercallBackendAdmissionPolicy.cs`, future neutral hypercall executor, `TrapCompletionRoutePolicy.cs`.
- Authority owner: owner-approved neutral hypercall backend owner from D2/Phase 06, independent from the compatibility authority plane; VMX frontend only decodes and projects after neutral success.
- Required RFC/ADR: accepted Phase 06 RFC/ADR with exact leaf ID, neutral backend owner service, backend executor result type, and full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reasons, adjacent denials.
- Acceptance criteria: backend execution authorized only for exact leaf; all adjacent leaves and missing capability/evidence/domain cases remain denied; admitted-denied is not backend success.
- Tests/static scans: missing owner, unknown leaf, missing capability/evidence/domain, VMX projection non-authority, route denied without backend authorization, no frontend `RuntimeOwnedPublication`.
- Risks: confusing `TrapDecision`, `ProjectedDecision.ShouldExit`, or `AllowedAdmittedDenied` with execution.
- Next-gate dependency: Phase 08 completion publication gate and Phase 09 retire publication gate.

## Phase Goal

Describe how a single VMCALL path may become a backend-executed runtime path only after a neutral hypercall backend owner is materialized and all downstream gates are satisfied. This phase does not approve activation.

## 2026-08-09 Exact-Slice Decision Addendum

Phase 38 accepts the first-slice architecture/ABI decision: `DomainHypercallRuntimeOwner`, `HybridCPU.VMCALL.Runtime.v1`, 16-bit leaf `0x0001`, operation `PROBE_NO_STATE_V1`, `Rs1` full value, `Rs2=x0`, `Rd=x0`, no-state/no-payload and `DrainOnly`. This resolves the architecture choice but does not materialize machine D2 or open production execution.

The exact policy is `HCOWNR`/policy 1/epoch 1; typed DomainGranted capability bit 41 with NonDelegable/RuntimeRevocable/DomainLocal/HostOnly/NeverProject; evidence `None`; execution-domain-only with non-zero tag and no memory/I/O/address-space requirement; SecureCompute denied; `DenyBeforeExecution`; `DenyAttemptReplay`; and host-owned/nonmigratable/nonprojected completion. These are accepted D2 inputs, not current runtime facts.

The readiness order is normative: machine-validated SpecV2 plus AcceptanceRecordV2 -> O1 owner-policy snapshot, while E0/E1 implementation readiness is established independently. At runtime the canonical E1-bound operand snapshot precedes D2/O1 exact resolution, runtime admission, neutral trap evaluation, E2 and E3. E4 is proof that this is the only canonical composition to E3, not a runtime stage after E3. Separate atomic completion plus E5, canonical E6 retire and E7 follow. The compatibility handler, `ProjectCompatibilityTrap`, `VmxExitQualification`, route booleans and `VmxRetireEffect` cannot skip any dependency.

## Historical Baseline (2026-06-11)

`VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection(...)` currently creates backend admission through `CreateHypercallBackendAdmission(...)`, which passes `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`. The final result is `TrapProjectionDeniedBackend`; route and publication fence deny backend execution/publication.

## Owner Of Authority

The neutral hypercall backend owner from Phase 06. VMX frontend owns only decode, compatibility alias validation, and final projection after neutral success.

## What Can Be Implemented

After accepted D2 RFC/ADR and live E2 operation-specific admission:

- pass a materialized neutral backend descriptor to backend admission;
- evaluate the approved leaf and arguments in a neutral executor;
- return an opaque, attempt-bound neutral backend execution receipt that cannot grant completion or retire;
- construct a route request only from neutral result and backend authorization;
- produce VMX-compatible projection only after completion/retire gates.
- backend success is not completion publication, and completion publication is not retire publication.

## What Remains Denied/Future-Gated

- All non-RFC leaves.
- Any path with missing capability/evidence/domain validation.
- Backend success without route authorization.
- Completion publication without fence.
- Retire publication without explicit retire rule.
- SecureCompute, nested, VMWRITE, lanes, and compiler emission.

## Forbidden Shortcuts

- Replacing `MissingNeutralOwner` with a descriptor that has no real owner semantics.
- Publishing a VMX completion before neutral backend result exists.
- Returning success because neutral trap policy chose to trap.
- Treating `ProjectedDecision.ShouldExit` as backend success.
- Treating `AllowedProofOnlyNoExecution` or `AllowedAdmittedDenied` as backend success.

## Required RFC/ADR

Phase 06 must be accepted and referenced by commit, ADR ID, and test names. A draft RFC is insufficient for this phase.

D2 acceptance is governance evidence, not a runtime certificate. Before E3, canonical SafetyVerifier must issue a live E2 certificate bound to the accepted exact leaf value, operand capture, operation-required identities, attempt/replay/restore generations, capability grant and evidence policy. For `PROBE_NO_STATE_V1`, `AddressSpaceIdentity` is absent by contract. For a future memory operation it is mandatory, owner-bound and attempt-bound. The E3 executor consumes E2 exactly once and returns an opaque receipt; it must not consume compatibility booleans, a manifest, `TrapDecision`, `VmExitReason` or `VmxRetireEffect` as authority.

PR-C closes the common-domain blocker: `DomainBoundaryDescriptor.ExecutionOnly` is propagated through `DomainValidationResult`, `DomainLegalityService`, `RuntimeBoundaryAdmissionService` and `DomainRuntimeAuthority`. `NoStateExecution` requires RuntimeRoot plus the exact typed capability, never `AllowAuthoritativeStateMutation`, and is denied to CompatibilityFrontend and SecureCompute.

Operand capture means an immutable canonical snapshot after live E1 materialization: source register identities plus actual values, attempt, VT/domain/context, source/working slot, bundle/replay epoch and restore generation. Current `VmxExitQualification(rs1, ..., rs2)` carries selector numbers only. E2/E3 must not infer the leaf from that compatibility object, truncate an independently selected runtime value to `ushort`, or re-read the register file.

## RFC/ADR Consumption Contract

Phase 07 may consume `RFC-HV-VMCALL-NO-STATE-OWNER-0001` only after the RFC/ADR state changes from draft to accepted by neutral runtime owners. Until then, the current production behavior must remain:

- `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` in the VMX compatibility VMCALL path;
- Phase 06A `NeutralHypercallBackendOwnerDescriptor` is a draft-only runtime skeleton and is not accepted owner semantics;
- no `HypercallBackendAdmissionDecision.Allowed`;
- no `BackendExecutionAuthorized: true`;
- no `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` in VMX frontend;
- no VMX frontend `CompletionRecord` construction;
- no retire publication through `VmxRetireEffect.VmCall` or `VmxRetireEffect.InterceptExit` in production callers.

Exact-leaf acceptance rules:

- Phase 07 accepts only one exact numeric VMCALL leaf ID named by an accepted owner-specific RFC/ADR and backed by a production neutral-runtime ABI symbol or table entry.
- The accepted packet must bind that exact ID to purpose, argument ABI, neutral owner, runtime leaf-value source, backend result source, capability policy, evidence class, migration class, deterministic no-state/no-payload behavior, denial reasons, adjacent denied leaves, and non-secure or secure-no-effect behavior.
- A leaf class, operation class, trap class, compatibility opcode, register selector, exit reason, owner ID, test fixture, or policy-supplied SecureCompute ID is not an exact VMCALL leaf.
- `VMCALL == 259`, `VmExitReason.VmCall == 18`, VMFUNC leaves `1/7/8`, test register selector `2`, test owner ID `0x060A`, and SecureCompute test fixture `0x10` cannot satisfy the exact-leaf gate.
- The current decode path copies the encoded `rs1`/`rs2` register selectors into compatibility qualification. Phase 07 cannot infer the runtime leaf value or descriptor value from those selectors without an accepted value-source contract.
- `NeutralHypercallBackendOperationClass.NoStateNoPayloadDomainLocal` and `NeutralHypercallBackendLeafSelection.CandidateOnlyNoNumericLeaf` are readiness classifications only. Neither is consumable as an exact leaf.
- SecureCompute `AllowedSecureOperation` is admission only and is not backend success.
- SecureCompute `AllowedProofOnlyNoExecution` is proof-only and is not execution.
- Backend success is not completion publication.
- Completion publication is not retire publication.

Minimum accepted inputs from Phase 06:

| required input | current state | Phase 07 handling |
| --- | --- | --- |
| RFC/ADR identifier | `RFC-HV-VMCALL-NO-STATE-OWNER-0001` draft | future-gated until accepted |
| exact leaf ID | attributable D2 lookup and immutable O1 for `0x0001` exist | adjacent/high-bit values remain denied; E2/backend still absent |
| neutral backend owner service | exact PR-E executor and PR-F canonical composition exist under accepted Phase 38 policy | compatibility and direct noncanonical callers remain denied |
| backend executor result type | opaque owner-bound E3 implemented | not completion or retire authority |
| capability policy | not proven for the candidate leaf | requires owner-specific RFC/ADR |
| evidence class | `NoPayload` candidate only | non-empty payload requires owner-specific RFC/ADR |
| migration class | `NoPayload` candidate only | checkpoint/restore authority remains denied |
| completion route | Phase 08 future-gated | backend success is not completion publication |
| retire rule | Phase 09 future-gated | completion publication is not retire publication |

The phrase "no-state, domain-local hypercall" remains a candidate class, not an exact leaf ID. The VRT semantic candidate `HCPU_HV_PROBE_V1` is likewise a candidate name, not an accepted leaf. Phase 07 cannot consume a class name, an example ABI, an illustrative numeric value, or a SecureCompute operation class as the selected leaf.

`ISE-HV-LEAF-DECISION-04` is therefore decision-ready but non-consumable: it proves that no exact numeric VMCALL leaf is currently frozen, and it rejects numeric lookalikes from opcode, exit-reason, VMFUNC, owner-ID, register-selector, and SecureCompute test namespaces.

`ISE-HV-RFC-OWNER-DECISION-05` is closed `NO-GO` on 2026-06-12 because owner acceptance is not proven. Phase 07 must not interpret closure of the decision task as closure of Phase 06B or permission to implement backend execution.

`ISE-HV-OWNER-ACCEPTANCE-HANDOFF-06` is closed `NO-DECISION / RETURNED-BLOCKED` on 2026-06-12. The handoff closure records the absence of an external neutral-owner response; it is not an accepted RFC/ADR and is not consumable by Phase 07.

Phase 07 remains closed until a later repository revision contains an attributable accepted neutral-runtime-owner artifact, independent from the compatibility authority plane, that accepts one exact numeric leaf and the complete owner map. The owner may be repository-local; this plan cannot appoint it. A handoff record, readiness matrix, operation class, or lack of rejection cannot satisfy this gate.

`ISE-HV-OWNER-RESPONSE-07` is closed `NO-RESPONSE / EXTERNAL-BLOCKED` on 2026-06-12. Closing the response audit does not close the Phase 07 dependency: no owner response was found, the draft remains unresolved, and production VMCALL remains admitted-denied.

Phase 07 must reject any transition argument based on owner silence, absence of rejection, elapsed time, repository-local readiness, or completion of a response audit. Only an attributable accepted exact-leaf RFC/ADR is consumable.

## ISE-HV-PHASE07-BLOCKED-BASELINE-08 - Closure Record

Baseline date: 2026-06-12.

Baseline state: closed `BLOCKED-BASELINE / NO-ACTIVATION`.

This closure freezes the verified denied production behavior for Phase 07 while the external owner dependency remains unresolved. It closes the baseline audit only; it does not close Phase 07 as implemented and does not permit Phase 08 consumption.

Verified production baseline:

| boundary | frozen production state | required invariant |
| --- | --- | --- |
| exact VMCALL leaf | not proven | no leaf selected, allocated, reserved, or inferred |
| neutral owner | absent from VMX frontend path | `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` remains |
| backend admission | denial-only | no `HypercallBackendAdmissionDecision.Allowed` |
| backend execution | unauthorized | no `BackendExecutionAuthorized: true` |
| completion route | projection-only denied | `TrapCompletionRouteRequest.ProjectionOnlyDenied(...)` remains |
| publication fence | evaluated on denied route | completion and retire publication remain false |
| frontend completion construction | absent from admission handlers | no handler-side `CompletionRecord` |
| Phase 08 descriptors | runtime scaffold only | neither positive descriptor is connected to VMX frontend |
| Phase 09 retire | closed | no VMCALL retire publication |

Baseline invalidation requires all of:

- an attributable accepted neutral-runtime-owner RFC/ADR;
- one exact numeric leaf backed by a production neutral-runtime ABI symbol or table;
- an accepted runtime leaf-value and descriptor-value source contract;
- complete capability, evidence, migration, adjacent-denial, and secure-domain policy;
- a separately reviewed Phase 06B/07 implementation that preserves Phase 08 and Phase 09 separation.

Until those inputs exist, any production diff that replaces `MissingNeutralOwner(...)`, introduces positive backend admission, connects a positive completion route, constructs a completion in the frontend handler, or publishes retire violates this baseline.

First allowed implementation shape, after acceptance only:

- replace `MissingNeutralOwner(...)` only with an accepted neutral owner descriptor for the exact leaf;
- add a neutral executor that returns no host evidence, native token, scheduler evidence, lane/stream evidence, SecureCompute evidence, VMCS state, or migration authority;
- make all adjacent leaves and missing capability/evidence/domain cases explicit denial results;
- pass backend authorization downstream without publishing completion or retire;
- keep VMX compatibility projection as downstream vocabulary only.

Required negative matrix for the first Phase 07 implementation PR:

| case | required result |
| --- | --- |
| draft RFC only | must remain denied |
| unknown leaf | must remain denied |
| missing neutral owner | must remain denied |
| compatibility-owned backend descriptor | must remain denied |
| missing domain validation | must remain denied |
| missing typed grant when required | must remain denied |
| missing evidence policy when required | must remain denied |
| backend authorized but route denied | no completion publication |
| completion authorized but retire denied | no retire publication |
| VMX/VMCS/VmxCaps/migration/lane/SecureCompute source used as owner | must remain denied |
| `AllowedSecureOperation` or `AllowedProofOnlyNoExecution` reused as backend success | must remain denied |

## Phase 06B Dependency Closure

Phase 07 remains blocked because Phase 06B is closed as blocked/future-gated under the current draft-only RFC facts. The draft `NeutralHypercallBackendOwnerDescriptor` is not accepted owner semantics, and it cannot be consumed by Phase 07 as backend success.

The next transition is external to code: neutral runtime owners must accept or reject an exact-leaf RFC/ADR. Until an accepted artifact exists, there is no valid Phase 06B implementation iteration.

Until Phase 06B is reopened by an accepted neutral runtime owner RFC/ADR:

- exact-leaf positive backend admission must not be added;
- VMX frontend must continue using `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`;
- `NeutralHypercallBackendOwnerRfcAdrState` must not grow an accepted state without the accepted RFC/ADR and adjacent denial tests in the same reviewed change;
- `BackendExecutionAuthorized: true` must not appear in runtime or VMX frontend code;
- `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` must not appear in VMX frontend code.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- Future neutral backend executor under `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/07_hypercall_backend_owner_and_vmcall_decision.md`
- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/08_trap_completion_route_and_retire_publication.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`

## Required Tests

Negative:

- missing owner remains denied;
- unknown leaf denied;
- missing capability denied;
- missing evidence denied;
- invalid domain denied;
- VMX projection cannot own backend authority;
- route denied when backend execution is not authorized.

Positive, after RFC:

- exact leaf backend owner admits;
- executor returns neutral result;
- VMX projection is produced only after route/fence/retire permissions;
- denied adjacent leaves remain denied.

## Required Static/Source Scans

```powershell
rg -n "MissingNeutralOwner|NeutralBackendOwnerMaterialized|BackendExecutionAuthorized|ProjectedDecision|VmExitReason|TrapDecision" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime/Events
```

The scan must prove projection vocabulary is downstream only.

## Migration/Evidence Classification

The first VMCALL path should avoid persistent state. If it produces data, classify it as guest-visible completion payload only after fence approval, not checkpoint authority. No backend handles or host evidence migrate.

## Completion/Retire Implications

This phase must produce backend execution authorization but still defer publication to Phase 08 and retire to Phase 09. If either downstream gate denies, the operation must not retire as success.

## Exit Criteria

- Backend owner path exists for exactly one leaf.
- Backend denial remains default.
- No VMX/VMCS state store introduced.
- Downstream publication remains gated.

## Dependency On Previous/Next Phase

Depends on Phase 06. Phase 08 authorizes completion route/publication for the same path.
