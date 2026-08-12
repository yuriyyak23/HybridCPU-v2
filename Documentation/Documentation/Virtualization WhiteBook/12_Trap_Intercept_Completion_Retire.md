# Trap, Intercept, Completion, And Retire

Trap/intercept authority has been split from VMX-compatible exit vocabulary. This is the current hardening center of the VMX refactor.

PR-I adds no trap/completion/retire shortcut. Its E7 drain owner only closes new
E2, observes or cancels the issuing-owner E2/E3/E5/E6 registries and checkpoints
policy identity after all are zero. Restore advances generation and invalidates
the older chain; compatibility projection remains non-authoritative.

## Neutral Trap Types

Neutral trap types live under:

- `CloseToHSL/Core/Runtime/Events/Traps/TrapRequest.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/TrapPolicyBitmap.cs`
- `CloseToHSL/Core/Runtime/Domains/Descriptors/TrapPolicy/TrapPolicyDescriptor.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/NeutralTrapResult.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/DomainTrapRecord.cs`
- `CloseToHSL/Core/Runtime/Events/Traps/SchedulingBudgetTimer.cs`
- `CloseToHSL/Core/Runtime/Events/Hypercalls/HypercallBackendAdmissionPolicy.cs`
- `CloseToHSL/Core/Runtime/Completion/Routing/TrapCompletionRoutePolicy.cs`

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

- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/TrapDecision.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/Frontend/Projection/Events/VmxTrapProjectionMapper.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/FrozenAbi/VmcsFieldAliases/VmExitReason.cs`
- `CloseToHSL/Core/Virtualization/Compatibility/FrozenAbi/VmcsFieldAliases/VmxExitQualification.cs`

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

`TrapCompletionPublicationFence` lives under `CloseToHSL/Core/Runtime/Completion/Records`. It evaluates:

PR-G adds a separate internal pure owner-policy evaluation and
`DomainHypercallCompletionOwner`. It does not promote the legacy caller-boolean
`Evaluate` result to authority. Only the neutral owner can consume one live exact
E3 and atomically create a neutral event `CompletionRecord` plus opaque E5. E5 is
record/attempt/VT/domain/restore-bound and explicitly grants no retire. The
compatibility frontend remains disconnected and the existing VMX retire path
remains fault-only.

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

`CompletionRecord` is neutral runtime data. `CompletionProjectionService` maps compatible completion records into `VmxCompletionProjection`. The current compatibility file exposes `CompletionRecord.FromCompatibilityExit` and `TryFromCompatibilityExit`, which accept a publication-fence result and are called only by tests in the inspected worktree. Because the fence DTO and route booleans are not owner-bound tokens, these factories are isolated scaffolding, not a production publication path. No admission handler, dispatcher, pipeline or retire caller may use them; a future positive path must move record creation behind the neutral completion owner and an attempt-bound backend-result token.

## Retire Fence

`VmxRetireEffect.InterceptExit` accepts a `TrapDecision` plus a `TrapCompletionPublicationFenceResult`. It returns a successful VMX exit effect only if `publicationFence.RetirePublicationAllowed` is true. Otherwise it returns a fail-closed security fault.

Even a positive-shaped `VmxRetireEffect` is not evidence that the canonical instruction executed successfully: the current CPU retire path applies `ApplyRemovedFrontendFailClosedEffect` and maps every valid VMX compatibility effect to a fault. A future success path requires a separate owner-bound retire grant tied to the live attempt identity.

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
PR-H adds the distinct per-CPU `DomainHypercallRetireOwner`. It may consume a
live E5 only after the existing WB stable-prefix/fault ordering and complete
batch prevalidation identify the canonical head. Its opaque E6 binds the exact
attempt, VT/domain, post-Stage-B and retire-window/order/restore identities and
is consumed once by the ordinary WB finalizer. The operation is no-state: no
register, memory, VM-state or redirect publication is fabricated.
`VmxRetireEffect` remains compatibility data and fault-only without E6.
