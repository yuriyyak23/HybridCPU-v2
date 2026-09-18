# Phase 07 - Hypercall Backend Owner And VMCALL Decision

## Goal

Define the decision gate for any future VMCALL backend path. Current VMCALL remains a neutral trap projection with backend admission denial because no neutral hypercall backend owner is materialized.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Current Code Baseline

- `VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection(...)` performs decode, projection validation, runtime admission, neutral trap policy evaluation, backend admission, route evaluation, and publication-fence evaluation.
- `CreateHypercallBackendAdmission(...)` uses `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)`.
- `HypercallBackendAdmissionService` denies missing backend descriptor and missing neutral backend owner.
- Current result can be admitted-denied trap projection, not backend completion.
- `VmxTrapProjectionMapper` maps neutral trap result to compatibility projection after neutral trap evaluation.
- `TrapCompletionRouteRequest.ProjectionOnlyDenied(...)` is used for the current VMCALL projection chain.
- `TrapCompletionRouteService` and `TrapCompletionPublicationFence` deny completion and retire publication when backend execution is not authorized.

## Decision Record - Hypercall Backend Owner And VMCALL Decision

Decision id: `ADR-VIRT-HYPERCALL-BACKEND-2026-06-04`.

Status: accepted as a denial/readiness hardening decision only. It does not implement a hypercall backend owner, does not authorize VMCALL backend execution, does not route through runtime-owned publication, does not publish completion records, and does not publish retire effects.

Current hypercall backend owner: none. `HypercallBackendAdmissionRequest.MissingNeutralOwner(...)` is the current production request shape for VMCALL backend admission.

Current backend decision authority: `HypercallBackendAdmissionService`. It denies missing runtime admission, missing neutral trap result, missing descriptor, non-runtime backend authority, missing domain validation, missing typed capability, missing evidence, and missing materialized neutral backend owner. The current service has no positive backend execution result.

Current VMCALL projection authority: compatibility frontend decode/projection plus runtime boundary admission plus neutral trap policy. This is sufficient to project a compatibility trap exit reason, but it is not backend execution authority.

Current route/publication authority: `TrapCompletionRouteService` and `TrapCompletionPublicationFence`. Both remain denied for the current VMCALL path because backend execution is denied before routing.

## Current VMCALL Chain Matrix

| Stage | Current authority | Current result | Denial / boundary | Test anchor |
| --- | --- | --- | --- | --- |
| Decode | `VmxCompatDecodeBoundary` | allowed vocabulary | frozen compatibility frontend only | `VmxHypercallBackendOwnerDecisionReadinessTests` |
| Projection validation | `VmxCompatProjectionService` | allowed projection alias | no authoritative mutation | `VmxAdmittedDeniedVmCallTrapPathTests` |
| Runtime admission | `RuntimeBoundaryAdmissionService` | projection-only admission | admission is not backend execution | `VmxHypercallBackendOwnerDecisionReadinessTests` |
| Neutral trap policy | `TrapPolicyDescriptor` / `TrapPolicyBitmap` | neutral compatibility-operation intercept | trap policy is not backend owner | `VmxAdmittedDeniedVmCallTrapPathTests` |
| Compatibility trap projection | `VmxTrapProjectionMapper` | `VmExitReason.VmCall` projection | exit reason is not backend authorization | `VmxHypercallBackendOwnerDecisionReadinessTests` |
| Backend admission | `HypercallBackendAdmissionService` | denied | `MissingBackendDescriptor` / missing neutral owner | `VmxHypercallBackendOwnerDecisionReadinessTests` |
| Completion route | `TrapCompletionRouteService` | denied | `DeniedBackendExecution` | `VmxTrapCompletionRouteOwnerTests` |
| Completion publication | `TrapCompletionPublicationFence` | denied | no `CompletionRecord` | `VmxTrapProjectionPublicationFenceTests` |
| Retire publication | VMX retire publication boundary | denied/faulted | no VMCALL retire effect publication from current path | `VmxHypercallBackendOwnerDecisionReadinessTests` |

## Future Neutral Hypercall Backend Owner Preconditions

Any future backend owner requires a separate RFC/ADR and must satisfy all of the following before a positive path can be considered:

- named neutral runtime owner, distinct from compatibility projection and VMX frontend vocabulary;
- typed capability grant and explicit capability scope;
- evidence policy for hypercall arguments and guest-visible state;
- domain validation and stale-domain denial;
- typed argument model for hypercall leaf, descriptor, inputs, outputs, and failure modes;
- backend scheduling/admission policy separate from trap projection;
- completion fence with route authority, source authority, and no compatibility-owned route;
- retire publication rule separate from completion publication;
- negative tests for missing owner, compatibility-owned owner, missing capability, missing evidence, invalid domain, missing completion fence, missing retire rule, SecureCompute shortcut, VMCS shortcut, and `VmExitReason.VmCall` shortcut.

Until this owner exists, VMCALL remains admitted-denied.

## Future Route / Publication Preconditions

Route and publication cannot be inferred from backend admission. A future route requires:

- backend execution authorization from a neutral owner;
- runtime-owned route descriptor;
- completion publication permission;
- retire publication permission;
- explicit compatibility projection mapping after neutral completion publication;
- static gates preventing `RuntimeOwnedPublication` from being used in VMX frontend paths before the backend owner and publication policy are materialized.

## Already Closed / Must Not Reopen

- Do not use `VmExitReason.VmCall` as backend authorization.
- Do not use `TrapDecision` as neutral runtime policy.
- Do not use compatibility projection as backend owner.
- Do not publish completion or retire effects from admitted-denied VMCALL projection.
- Do not use `TrapCompletionRouteDescriptor.RuntimeOwnedPublication` before a real neutral backend owner exists.

## Required Code/Doc Anchors

- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Handlers/VmxCompatibilityAdmissionService.Traps.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Events/Traps/NeutralTrapResult.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs`
- `Documentation/Virtualization WhiteBook/12_Trap_Intercept_Completion_Retire.md`
- `Documentation/Virtualization WhiteBook/17_Roadmap_And_Residual_Risk.md`

## Work Items

- Specify neutral hypercall backend owner requirements: runtime owner, typed capability, evidence policy, domain validation, argument model, completion fence, retire rule, and tests. Done as future preconditions above.
- Define negative cases: missing owner, compatibility owner, missing capability, missing evidence, stale/invalid domain, and no publication permission. Done in tests and preconditions.
- Document the current admitted-denied VMCALL chain. Done in the chain matrix.
- Separate backend admission from completion route and retire publication. Done in decision record and preconditions.
- Require future RFC/ADR before any backend owner can be considered materialized. Done in this phase.

## Explicit Non-Goals

- Do not implement a backend owner.
- Do not open backend execution in this phase.
- Do not change trap policy.
- Do not publish completion records from current VMCALL projection.

## Done Criteria

- Current VMCALL state is classified as projection/admitted-denied.
- Future owner preconditions are explicit and testable.
- Route/publication preconditions are not collapsed into backend admission.
- All forbidden shortcuts are documented.
- Focused tests prove missing-owner backend denial, no positive backend path, route denial, completion publication denial, retire publication denial, and source/static shortcut denial.

## Required Tests / Static Checks

- `FullyQualifiedName~VmxHypercallBackendAdmissionPolicyTests`
- `FullyQualifiedName~VmxAdmittedDeniedVmCallTrapPathTests`
- `FullyQualifiedName~VmxTrapCompletionRouteOwnerTests`
- `FullyQualifiedName~VmxTrapProjectionPublicationFenceTests`
- `FullyQualifiedName~VmxHypercallBackendOwnerDecisionReadinessTests`
- Tests that `MissingNeutralOwner` denies backend execution.
- Tests that route and publication remain denied without backend authorization.
- Static scan for `RuntimeOwnedPublication` usage in VMX frontend paths.

Owner-specific static gates:

- Source anchor scan for `HypercallBackendAdmissionRequest.MissingNeutralOwner`, `DeniedNeutralBackendOwnerMissing`, `TrapCompletionRouteRequest.ProjectionOnlyDenied`, `DeniedBackendExecution`, and `VmxHypercallBackendOwnerDecisionReadinessTests`.
- Forbidden VMX frontend/caller scan for `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`, positive backend authorization literals, direct backend descriptor creation, compatibility completion publication, and direct `VmxRetireEffect` VMCALL/intercept publication in current production call paths.
- Documentation overclaim scan for `VmExitReason.VmCall` as backend authorization, `TrapDecision` as runtime policy, compatibility projection as backend owner, current `RuntimeOwnedPublication`, or positive VMCALL backend-success language outside static-gate definitions.

## Residual Risk

Admitted-denied projection has enough structure to look like progress. The plan must keep saying that the missing owner is the current boundary, not a TODO that can be bypassed.

## External Audit Risk Update

VMCALL remains admitted-denied, not backend success. `VmExitReason.VmCall` is not backend authorization, `TrapDecision` is not neutral runtime policy, and compatibility projection is not backend owner. A future backend-success path requires a neutral hypercall backend owner RFC/ADR with typed capability, evidence policy, domain validation, argument model, completion fence, and retire rule.

## Next Phase Dependency

Phase 08 depends on a real backend-owner decision before allowing route or retire publication.
