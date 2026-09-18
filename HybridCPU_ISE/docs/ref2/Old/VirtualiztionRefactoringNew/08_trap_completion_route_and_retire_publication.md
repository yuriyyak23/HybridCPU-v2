# Phase 08 - Trap Completion Route And Retire Publication

## Goal

Keep trap completion route, completion publication, and retire publication as separate neutral decisions. No compatibility trap projection may publish completion or retire effects unless a backend owner authorizes execution and the route/fence rules allow publication.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `TrapCompletionRouteService.Authorize(...)` requires runtime admission, neutral trap result, runtime-owned route descriptor, domain validation when required, backend execution authorization, completion publication permission, and retire publication permission.
- `TrapCompletionRouteDescriptor.ProjectionOnlyDenied` denies completion and retire publication.
- Current VMX VMCALL flow uses projection-only denied route construction.
- `TrapCompletionPublicationFence.Evaluate(...)` denies publication without runtime admission, neutral trap, backend authorization, and retire publication permission.
- `CompletionRecord.FromCompatibilityExit(...)` and `TryFromCompatibilityExit(...)` require `TrapCompletionPublicationFenceResult.CompletionPublicationAllowed`.
- `VmxRetireEffect.InterceptExit(...)` returns a fault when `RetirePublicationAllowed` is false.
- Current VMX production callers materialize removed-front-end fail-closed fault effects, not VMCALL/intercept publication effects.

## Decision Record - Trap Completion Route And Retire Publication

Decision id: `ADR-VIRT-TRAP-PUBLICATION-2026-06-04`.

Status: accepted as a denial/readiness hardening decision only. It does not authorize backend execution, completion publication, retire publication, compatibility-exit completion record creation, VMCS mutation, SecureCompute backend execution, or runtime activation.

Current route owner: neutral runtime route service. The current VMX VMCALL frontend calls `TrapCompletionRouteRequest.ProjectionOnlyDenied(...)`; it does not call `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`.

Current completion publication owner: `TrapCompletionPublicationFence`. The current VMCALL path reaches `DeniedBackendExecution`; `CompletionPublicationAllowed` remains false and the completion record remains empty.

Current retire publication owner: none for VMCALL projection. `RetirePublicationAllowed` remains false, and `VmxRetireEffect.InterceptExit(...)` converts the denied fence to a typed fault rather than an exit-retire publication.

Current completion-owned VMREAD posture: completion-owned fields may be recomputed only from a neutral `CompletionRecord` that is already a compatibility projection source. The current admitted-denied VMCALL path does not create such a record.

## Current Route / Publication Matrix

| Surface | Current authority | Current VMCALL result | Publication state | Test anchor |
| --- | --- | --- | --- | --- |
| Route descriptor | `TrapCompletionRouteDescriptor.ProjectionOnlyDenied` | projection-only denied | no completion/retire permission | `VmxTrapCompletionRouteRetirePublicationHardeningTests` |
| Route service | `TrapCompletionRouteService.Authorize(...)` | `DeniedBackendExecution` | route not allowed | `VmxTrapCompletionRouteOwnerTests` |
| Publication fence | `TrapCompletionPublicationFence.Evaluate(...)` | `DeniedBackendExecution` | empty completion record | `VmxTrapProjectionPublicationFenceTests` |
| Compatibility completion record | `CompletionRecord.TryFromCompatibilityExit(...)` | returns false | no compatibility-exit record | `VmxTrapCompletionRouteRetirePublicationHardeningTests` |
| Retire effect | `VmxRetireEffect.InterceptExit(...)` | faulted effect | no exit-retire publication | `VmxTrapCompletionRouteRetirePublicationHardeningTests` |
| Production VMX callers | removed frontend fault path | fail-closed fault | no VMCALL/intercept publication effect | `VmxTrapCompletionRouteRetirePublicationHardeningTests` |

## Future Route-Publication Checklist

Any future route/publication path requires a prior neutral backend owner decision and all of the following:

- backend execution authorization from a neutral runtime owner;
- runtime-owned route descriptor with validated domain;
- explicit completion publication permission;
- explicit retire publication permission;
- source authority for the completion record;
- fence evidence proving runtime admission, neutral trap, backend authorization, completion permission, and retire permission;
- compatibility projection mapping only after the neutral completion record exists;
- migration/evidence classification for each published completion value;
- negative tests for missing backend, missing route descriptor, compatibility-owned route, stale domain, denied completion, denied retire, direct compatibility record construction, direct retire publication, SecureCompute shortcut, VMCS shortcut, and `RuntimeOwnedPublication` misuse from VMX frontend.

Until this checklist is satisfied by a later owner-specific RFC/ADR, `RuntimeOwnedPublication` remains future-gated from VMX frontend paths.

## Completion Record Projection Boundary

`CompletionRecordClass.Trap` is neutral runtime completion data. It is not directly projected to VMX-compatible completion fields. VMX-compatible completion projection requires a `CompletionRecordClass.CompatibilityExit` record, and that record can only be created after the neutral publication fence allows completion publication.

The current VMCALL chain never reaches that state. Completion-owned VMREAD fields remain recomputed compatibility projections only for an already-published compatible completion source; they are not completion publication authority.

## Already Closed / Must Not Reopen

- Do not use compatibility exit projection as completion publication.
- Do not use retire as an implied effect of trap projection.
- Do not bypass `TrapCompletionPublicationFence`.
- Do not route through `RuntimeOwnedPublication` before backend owner and publication policy are real.
- Do not use completion records as migration authority unless neutral policy classifies them that way.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Completion/Records/TrapCompletionPublicationFence.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/Virtualization WhiteBook/16_Current_State_And_Closure_Matrix.md`

## Work Items

- Document the current route decisions and publication-fence decisions. Done in the route/publication matrix.
- Define the route-publication checklist for any future backend owner. Done as future checklist above.
- Define completion record classes that can be projected back to VMX-compatible fields. Done in the completion projection boundary.
- Define retire-publication denial tests for admitted-denied paths. Done in `VmxTrapCompletionRouteRetirePublicationHardeningTests`.
- Require migration/evidence classification for any published completion value. Done as future checklist above.

## Explicit Non-Goals

- Do not authorize publication in this docs phase.
- Do not change trap result mapping.
- Do not treat completion route design as current publication.
- Do not create a retire side channel through tests or diagnostics.

## Done Criteria

- Route authorization, completion publication, and retire publication are documented as separate gates.
- The current VMCALL projection-only denied route is explicit.
- `RuntimeOwnedPublication` is documented only as future-gated.
- Completion-owned VMREAD fields are described as recomputed compatibility projections from neutral completion records.
- Focused tests prove projection-only route denial, runtime-owned publication descriptor does not bypass backend denial, completion and retire publication are separate denied gates, completion record projection requires fence-published compatibility source, and VMX frontend/callers do not use publication shortcuts.

## Required Tests / Static Checks

- Existing trap completion route tests.
- Existing publication-fence tests.
- `FullyQualifiedName~VmxTrapCompletionRouteRetirePublicationHardeningTests`
- `FullyQualifiedName~VmxHypercallBackendOwnerDecisionReadinessTests`
- Static scan for `RuntimeOwnedPublication` in VMX frontend code.
- Static scan for completion record construction outside the fence.

Owner-specific static gates:

- Source anchor scan for `ProjectionOnlyDenied`, `RuntimeOwnedPublication`, `DeniedBackendExecution`, `DeniedCompletionPublication`, `DeniedRetirePublication`, `CompletionPublicationAllowed`, `RetirePublicationAllowed`, and `VmxTrapCompletionRouteRetirePublicationHardeningTests`.
- Forbidden VMX frontend/caller scan for `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`, compatibility completion record construction, and direct VMCALL/intercept retire effects in current production call paths.
- Forbidden production scan for compatibility completion record helper calls outside `CompletionRecordCompatibilityProjection`.
- Forbidden production scan for raw `CompletionRecord` construction outside the neutral completion fence and compatibility completion projection boundary.
- Documentation overclaim scan for current `RuntimeOwnedPublication` use, completion publication allowed, retire publication allowed, compatibility completion publication from admitted-denied VMCALL, or VMCALL backend-success language outside static-gate definitions.

## Residual Risk

Route and fence classes exist in production code, so documentation must keep reminding readers that class existence is not current permission to publish.

## External Audit Risk Update

Route/fence class existence is not publication permission. `RuntimeOwnedPublication` must remain statically guarded from VMX frontend paths until a real neutral backend owner and publication policy exist. Completion publication and retire publication remain separate gates.

## Next Phase Dependency

Phase 09 depends on the same publication discipline for nested virtualization intent and child-domain projection.
