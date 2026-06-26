# Trap, Intercept, Completion, And Retire

Trap/intercept authority has been split from VMX-compatible exit vocabulary. This is the current hardening center of the VMX refactor.

## Neutral Trap Types

Neutral trap types live under:

- `CloseToRTL/Core/Runtime/Events/Traps/TrapRequest.cs`
- `CloseToRTL/Core/Runtime/Events/Traps/TrapPolicyBitmap.cs`
- `CloseToRTL/Core/Runtime/Domains/Descriptors/TrapPolicy/TrapPolicyDescriptor.cs`
- `CloseToRTL/Core/Runtime/Events/Traps/NeutralTrapResult.cs`
- `CloseToRTL/Core/Runtime/Events/Traps/DomainTrapRecord.cs`
- `CloseToRTL/Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs`
- `CloseToRTL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `CloseToRTL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`

The neutral vocabulary is:

- `TrapTargetKind`
- `TrapAccessType`
- `TrapAccessMask`
- `TrapPolicyClass`
- `TrapPolicyAuthority`
- `NeutralTrapResultKind`
- `NeutralTrapResult`

No neutral trap result needs `VmExitReason`.

## VMX Projection Types

VMX-facing projection types live under:

- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Events/TrapDecision.cs`
- `CloseToRTL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs`
- `CloseToRTL/Core/Virtualization/Compatibility/FrozenAbi/VmcsFieldAliases/VmExitReason.cs`
- `CloseToRTL/Core/Virtualization/Compatibility/FrozenAbi/VmcsFieldAliases/VmxExitQualification.cs`

`TrapDecision` contains `VmExitReason` and `VmxExitQualification`, so it is compatibility projection vocabulary. It must not feed runtime trap policy.

## VMCALL Projection Chain

```text
VMCALL
  -> TrapRequest.ForVmxOperation(VmCall)
  -> VMX decode / alias projection validation
  -> RuntimeBoundaryAdmissionService(ProjectCompatibilityTrap)
  -> TrapPolicyDescriptor + TrapPolicyBitmap
  -> NeutralTrapResult(CompatibilityOperationIntercept)
  -> HypercallBackendAdmissionService(MissingNeutralOwner in production)
  -> TrapCompletionRouteService(ProjectionOnlyDenied in production)
  -> VmxTrapProjectionMapper
  -> TrapDecision(VmExitReason.VmCall, VmxExitQualification)
  -> TrapCompletionPublicationFence
  -> admitted-denied result
```

This chain proves that the frontend can project an exit reason only after neutral trap policy has produced a neutral result. It also proves that projected exit reason does not imply backend success.

## Hypercall Backend Admission

`HypercallBackendAdmissionService` lives under neutral runtime events. It validates runtime admission, neutral trap result, backend descriptor authority, domain validation, typed capability, and evidence policy.

Current production VMCALL passes `HypercallBackendAdmissionRequest.MissingNeutralOwner`, so backend execution remains denied before route publication or retire publication can become successful.

## Trap Completion Route

`TrapCompletionRouteService` lives under neutral completion routing. It can represent a runtime-owned publication route, but the VMX compatibility frontend currently uses `TrapCompletionRouteDescriptor.ProjectionOnlyDenied`.

The runtime route descriptors are intentionally distinct:

- `ProjectionOnlyDenied`: completion false, retire false; current VMX frontend path.
- `RuntimeOwnedCompletionPublication`: completion route flag true, retire false; future-gated split descriptor introduced by `ISE-COMP-ROUTE-01`.
- `RuntimeOwnedPublication`: completion true, retire true; coupled future descriptor that also requires an explicit retire-publication gate.

`TrapCompletionRouteResult.CompletionPublicationAuthorizedOnly` identifies the split route result. `IsFullyRetirable` identifies the coupled completion+retire result. `IsAllowed` remains equivalent to fully retirable and must not be used to describe completion-only route authorization.

Neither runtime-owned positive descriptor is used by the current VMX frontend. A descriptor is route policy, not backend success and not publication by itself.

## Completion Publication Fence

`TrapCompletionPublicationFence` lives under `CloseToRTL/Core/Runtime/Completion/Records`. It evaluates:

- runtime admission allowed;
- neutral trap exists;
- backend execution authorized;
- completion publication authorized;
- retire publication authorized;
- completion evidence visibility;
- explicit completion migration classification.

Denied decisions include:

- `DeniedRuntimeAdmission`
- `DeniedNoNeutralTrap`
- `DeniedBackendExecution`
- `CompletionPublishedRetireDenied` for a published neutral completion whose retire gate remains closed

`Allowed` and `CompletionPublishedRetireDenied` both contain a neutral `CompletionRecord`. Only `Allowed` can make `RetirePublicationAllowed` true. The admitted VMCALL path intentionally uses `DeniedBackendExecution` and still produces no completion.

`ISE-COMP-FENCE-02` is closed as future-gated neutral scaffolding. A completion-only route can produce `CompletionRecordClass.Trap` with `CompletionPublicationAllowed == true` and `RetirePublicationAllowed == false`. Host-owned evidence, an `Unclassified` migration class, or `HostOwnedNonMigratable` cannot grant retire. Defaults are fail-closed and do not imply migration approval.

## Completion Projection

`CompletionRecord` is neutral runtime data. `CompletionProjectionService` maps compatible completion records into `VmxCompletionProjection`. `CompletionRecord.FromCompatibilityExit` and `TryFromCompatibilityExit` require a publication fence result. This prevents arbitrary VMX exit records from bypassing the neutral fence.

## Retire Fence

`VmxRetireEffect.InterceptExit` accepts a `TrapDecision` plus a `TrapCompletionPublicationFenceResult`. It returns a successful VMX exit effect only if `publicationFence.RetirePublicationAllowed` is true. Otherwise it returns a fail-closed security fault.

This is the critical retire rule:

```text
VMX projection exists
  does not imply
retire publication allowed
```

Likewise:

```text
completion route flag authorized
  does not imply
completion record published
```

And:

```text
completion record published
  does not imply
retire publication allowed
```

## Forbidden Leakage

The following are forbidden as runtime authority:

- branching runtime trap policy on `VmExitReason`;
- storing trap authority in `TrapDecision`;
- creating completion records from VMX exit values without the neutral publication fence;
- using `VmxRetireEffect.InterceptExit` as success evidence without publication authorization;
- treating `RuntimeOwnedCompletionPublication` by itself as completion-record or retire publication;
- treating admitted-denied VMCALL projection as backend execution.
