# Phase 08 - Future-Gated Trap Completion Route Publication Plan

Status: exact PR-G completion-owner contour closed; legacy caller-boolean fence remains scaffolding and current VMX frontend paths keep projection-only denial.

## 2026-08-10 PR-G Closure Addendum

Committed PR-F `7f529cd4f9699701b0b2bfdcc8bd90eaf82af781`
preserved exclusive canonical E4 and fault-only retire. PR-G introduces a pure
owner-policy evaluation alongside, but does not flip or promote, the existing
caller-boolean `Evaluate` seam. A configured neutral completion owner alone may
consume one live exact E3 after real common admission and the split
`RuntimeOwnedCompletionPublication` route, then atomically create one
`CompletionRecordClass.Event` and one sealed E5. E5 binds attempt, E3 digest,
decision/owner, VT/domain, execution/completion sequence, effect/completion
digests, host-owned evidence, non-migratable class and restore generation.
Missing owner, duplicate consume, wrong route/policy and restore mismatch deny.
No E6, retire permission, compatibility projection or guest-visible effect is
created.

## 2026-06-11 Audit Contract

- File name: `08_trap_completion_route_publication_plan.md`.
- Purpose: define the future completion publication gate after backend execution authorization exists.
- Status: `future-gated`; current VMX frontend paths use projection-only denial.
- Scope: route request, runtime-owned route descriptor, publication fence, completion record creation, downstream VMX projection, and explicit separation from retire publication.
- No-goals: no completion publication from denied backend paths, no retire publication, no direct `CompletionRecord` construction in VMX admission/frontend handler; compatibility projection helpers may create compatibility records only after neutral fence permit.
- Code anchors: `TrapCompletionRoutePolicy.cs`, `TrapCompletionPublicationFence.cs`, `VmxCompatibilityAdmissionService.Traps.cs`, `CompletionProjectionService.cs`.
- Authority owner: neutral completion owner after route-service and publication-fence policy decisions; VMX exit vocabulary, public booleans and fence DTOs are not authority.
- Required RFC/ADR: Phase 06/07 RFC/ADR must include completion payload/route map with full owner map: field/operation, owner, value source, capability policy, evidence class, migration class, denial reason.
- Acceptance criteria: backend success != completion publication; completion is allowed only through neutral fence for the exact approved path.
- Tests/static scans: `RuntimeOwnedCompletionPublication` split-route cases, `RuntimeOwnedPublication` rejection cases, no frontend use before owner, no handler-side `CompletionRecord`, fence-only publication, no Phase 08-only use of a descriptor that also grants retire.
- Risks: treating route descriptor flags, trap projection, backend success, or the current coupled `RuntimeOwnedPublication` descriptor as publication permission.
- Next-gate dependency: Phase 09 explicit retire rule.

## Phase Goal

Define the future completion publication gate for a single neutral backend-owned trap path only after backend execution authorization and route policy approval. This phase does not approve current completion publication.

## Historical Baseline (2026-06-11)

`TrapCompletionRouteDescriptor.ProjectionOnlyDenied` is used by VMCALL compatibility paths. `TrapCompletionRouteDescriptor.RuntimeOwnedCompletionPublication` exists as the split completion-only route descriptor. The neutral fence can materialize `CompletionRecordClass.Trap` with `CompletionPublishedRetireDenied`, `CompletionPublicationAllowed == true`, and `RetirePublicationAllowed == false`. `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` remains the coupled completion+retire descriptor; neither positive descriptor may be used by VMX frontend before real backend owner authorization and, for the coupled path, explicit Phase 09 retire approval.

These positive shapes are compatibility/policy scaffolding, not the future E5 authority contract. Current `TrapCompletionPublicationFence.Evaluate` accepts caller-provided authorization booleans and directly constructs `CompletionRecord`. Therefore `CompletionPublicationAllowed == true`, a positive route descriptor or a directly constructible fence result cannot prove that E3 executed or authorize a future positive publication.

The future positive lifecycle replaces that shape with an owner-bound exact-once seam:

```text
E3
  -> TrapCompletionRouteService
  -> TrapCompletionPublicationFence policy decision
  -> neutral completion owner
  -> atomic publication:
       CompletionRecord
       + opaque E5 CompletionPublicationToken
```

E5 is emitted atomically with one already-published completion and proves exactly that publication. It does not grant the completion owner permission to publish, cannot be issued before the record, and may be consumed only by the canonical retire owner.

`ISE-HV-PHASE07-BLOCKED-BASELINE-08` closes the Phase 07 baseline audit as `BLOCKED-BASELINE / NO-ACTIVATION`. Phase 08 cannot consume that closure as backend success: the VMCALL frontend still uses `MissingNeutralOwner(...)` and `ProjectionOnlyDenied(...)`, and both positive route descriptors remain disconnected from it.

## ISE-COMP-ROUTE-01 / ISE-COMP-FENCE-02 - Closure Record

Closure date: 2026-06-18.

State: closed `FUTURE-GATED SCAFFOLDING / COMPLETION-ONLY-ROUTE-SEPARATED / RETIRE-DENIED / NO-VMX-FRONTEND-PUBLICATION / NO-PRODUCTION-CHANGE`.

Closure result: `RuntimeOwnedCompletionPublication` exists only as split completion-route scaffolding, and `CompletionPublishedRetireDenied` exists only as neutral fence scaffolding where completion can be represented while retire remains denied.

This closure does not accept an RFC/ADR, authorize backend execution, publish completion from the VMX frontend, construct a handler-side `CompletionRecord`, publish retire, consume Phase 07 blocked baseline as backend success, connect `RuntimeOwnedCompletionPublication` or `RuntimeOwnedPublication` to VMCALL, or grant authority to VMX, VMCS, `VmxCaps`, SecureCompute, compiler, migration, lane, stream, tests, docs, scans, route descriptor flags, or fence shape.

Closure invariants:

- `RuntimeOwnedCompletionPublication` remains completion-only route scaffolding.
- `RuntimeOwnedPublication` remains coupled completion+retire scaffolding and cannot be used as Phase 08-only approval.
- `CompletionPublishedRetireDenied` may represent neutral completion while `RetirePublicationAllowed == false`.
- VMX frontend remains `ProjectionOnlyDenied`.
- VMCALL remains `MissingNeutralOwner`.
- Handler-side completion construction remains forbidden.
- Completion publication remains separate from retire publication.

## Owner Of Authority

The neutral completion owner owns exact-once publication. `TrapCompletionRouteService` and `TrapCompletionPublicationFence` provide preceding route and policy decisions driven by a live E3/neutral trap result; they do not self-issue publication authority. VMX exit vocabulary is downstream projection only.

## What Can Be Implemented

After accepted Phases 06 and 07, and only with a completion route RFC/ADR, the future positive implementation may:

- construct a route request with backend execution authorized only from neutral backend result;
- use `RuntimeOwnedCompletionPublication` for the split completion-only path or `RuntimeOwnedPublication` only when the coupled completion+retire path is explicitly approved;
- pass neutral reason code and payload fields through route/fence policy evaluation;
- replace the current boolean/direct-construction positive seam with an owner-bound atomic publication of exactly one `CompletionRecord` and one opaque E5 token;
- keep VMX projection as a derived result.
- completion publication is not retire publication.
- if the current coupled `RuntimeOwnedPublication` descriptor is reused, the RFC/ADR must also satisfy Phase 09.

## What Remains Denied/Future-Gated

- Completion publication for any denied backend path.
- Completion publication from VMX frontend without neutral backend result.
- Completion publication through a descriptor that also grants retire unless Phase 09 is explicitly satisfied.
- Retire publication until Phase 09.
- All non-RFC hypercall leaves.

## Forbidden Shortcuts

- Directly constructing `CompletionRecord` in VMX frontend.
- Using `RuntimeOwnedCompletionPublication` or `RuntimeOwnedPublication` with `BackendExecutionAuthorized: false`.
- Treating route descriptor flags as sufficient without runtime admission and backend owner.
- Treating `RuntimeOwnedCompletionPublication` as retire authorization.
- Treating the current `RuntimeOwnedPublication` descriptor as Phase 08-only approval.
- Publishing completion from `TrapDecision`.
- Publishing completion as if it were retire authorization.
- Having the completion owner issue a grant to itself and then consume that grant to create the record.
- Issuing E5 before, separately from, or without the matching atomic `CompletionRecord` publication.

## Required RFC/ADR

Phase 06/07 RFC/ADR must include completion payload class, route policy, evidence class, migration class, denial reasons, and either the split completion-only route descriptor or explicit Phase 09 retire approval. If not, this phase remains blocked.

## Code Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Completion/CompletionProjectionService.cs`

## Documentation Anchors

- `HybridCPU_ISE/docs/ref2/Old/VirtualiztionRefactoringNew/08_trap_completion_route_and_retire_publication.md`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`

## Required Tests

- `RuntimeOwnedCompletionPublication` authorizes the completion route flag while retire remains denied.
- The publication fence returns `CompletionPublishedRetireDenied` with a neutral completion record for the completion-only route.
- Host-owned evidence, `Unclassified`, and `HostOwnedNonMigratable` migration classes keep retire denied.
- Positive retire classification must be explicit; fail-closed defaults do not grant retire.
- `RuntimeOwnedPublication` rejects missing runtime admission.
- It rejects non-trap neutral results.
- It rejects missing backend execution authorization.
- It rejects non-runtime route authority.
- It rejects Phase 08-only approval through a route descriptor that also grants retire.
- It creates completion only when all neutral gates pass.
- Future positive-path tests deny boolean-only authorization, direct fence construction, E5-before-record, record-without-E5, duplicate atomic publication and any non-retire consumer of E5.
- Compatibility projection helpers create `CompletionRecordClass.CompatibilityExit` only after `publicationFence.CompletionPublicationAllowed`.
- VMX frontend cannot use `RuntimeOwnedCompletionPublication` or `RuntimeOwnedPublication` before Phase 07 owner path.

## Required Static/Source Scans

```powershell
rg -n "RuntimeOwnedCompletionPublication|RuntimeOwnedPublication|new CompletionRecord|CompletionPublicationAuthorized|EvaluateFence" HybridCPU_ISE/CloseToHSL/Core/Virtualization HybridCPU_ISE/CloseToHSL/Core/Runtime
```

Any VMX frontend match must be downstream projection or denial unless the RFC ID is present. Current admitted-denied handlers must not construct `CompletionRecord`; `CompletionRecordCompatibilityProjection` may construct a compatibility record only after the neutral fence permits completion publication.

## Migration/Evidence Classification

Completion record payload is not migration authority by default. `OperationMigrationPolicy = DrainOnly` and `CompletionMigrationClass = HostOwnedNonMigratable` are distinct accepted D2 fields and are not interchangeable. `CompletionEvidenceClass = HostOwnedRuntimeEvidence` and `CompletionProjectionPolicy = NeverProject`. The current fence separately classifies completion migration with `TrapCompletionMigrationClass`; it directly constructs a record from caller booleans and denies host-owned evidence at retire, so it must not be flipped positive. For the first slice checkpoint occurs only after drain, so live E5 is non-serializable and does not migrate. Completion-owned VMREAD fields remain `RecomputedCompletion`; checkpoint images must not serialize completion projection values as authoritative state.

## Completion/Retire Implications

The current neutral fence scaffolding can represent a completion record while retire remains denied, but its public booleans and direct construction are not the future positive contract. The split route descriptor and fence policy decision do not publish by themselves: the neutral completion owner performs the atomic `CompletionRecord + E5` publication. The coupled completion+retire descriptor still requires Phase 09. E5 proves published completion and is consumable only by the retire owner; completion success remains separate from retire success.

## Exit Criteria

- Future positive completion publication happens only after neutral route/fence policy and through the owner-bound atomic publication seam.
- Denied backend paths still produce empty completion.
- VMX projection cannot create completion by itself.
- `RuntimeOwnedCompletionPublication` can authorize the completion route flag without retire.
- `ISE-COMP-FENCE-02` is closed as future-gated neutral fence scaffolding only; it is not positive E5 authority.
- Missing/unsafe evidence or migration classification cannot grant retire.
- Any descriptor that grants retire is covered by explicit Phase 09 approval.

## Dependency On Previous/Next Phase

Depends on Phase 07. Phase 09 grants or denies retire publication.
