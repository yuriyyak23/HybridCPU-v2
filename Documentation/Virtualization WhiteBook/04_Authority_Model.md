# Authority Model

HybridCPU virtualization authority is descriptor-owned and runtime-admitted. A compatibility frontend can request projection or alias handling, but it cannot directly mutate authoritative state.

## Authority Components

Runtime boundary admission joins several gates:

- `DomainRuntimeContext`: the current neutral runtime context.
- `RootAuthorityDescriptor`: root-level authority evidence.
- `EvidencePolicyDescriptor`: policy for exposure and projection.
- `DomainRuntimeOperation`: the neutral operation being admitted.
- `DomainBoundaryDescriptor`: required domain ownership envelope.
- `CapabilityBoundaryRequirement`: typed capability requirement.
- `EvidenceBoundaryRequirement`: evidence visibility requirement.

The admission result is `RuntimeBoundaryAdmissionResult`. Its decision is neutral: allowed, missing context, domain boundary denied, capability denied, evidence denied, frontend mutation denied, or runtime authority denied.

## Frontend Mutation Fence

`RuntimeBoundaryAdmissionService` denies a compatibility frontend operation when it is a frontend-authored authoritative mutation. Projection-only operations such as `ReadCompatibilityProjection` and `ProjectCompatibilityTrap` can be admitted as projection requests. That admission still does not imply backend execution.

The compatibility frontend may request:

- activation/deactivation of the compatibility frontend;
- read-only compatibility projection;
- trap projection;
- denied aliases for unsupported or non-authoritative operations.

It may not directly write execution, memory, I/O, trap, completion, migration, or capability state.

## DomainRuntimeOperation

`DomainRuntimeOperation` provides the neutral operation vocabulary. Current relevant kinds include:

- `ActivateCompatibilityFrontend`
- `DeactivateCompatibilityFrontend`
- `EnterDomain`
- `ResumeDomain`
- `ReadCompatibilityProjection`
- `WriteCompatibilityProjection`
- `InvalidateTranslation`
- `InvokeCapability`
- `SaveDomainState`
- `RestoreDomainState`
- `ProjectCompatibilityTrap`

The operation records source and projection-only status. This is the mechanism that prevents VMX opcode names from becoming the runtime authority vocabulary.

## Projection Is Not Authority

A VMX-compatible result can exist only after the neutral owner has produced or admitted the underlying fact. For example:

- `VmExitReason.VmCall` can be projected from a neutral compatibility-operation trap.
- `ExitQualification` can encode a projected VMX-compatible result.
- `CompletionProjectionService` can expose a VMX projection for a compatibility completion record.

None of those projections decide whether the trap, completion, or retire publication is authorized.

## Authority Decision Pattern

```text
compatibility request
  -> decode/projection validation
  -> RuntimeBoundaryAdmissionService
  -> neutral owner evaluates policy
  -> neutral result/fence
  -> compatibility projection if allowed
  -> fail-closed if backend publication is not authorized
```

This pattern is the contract for every future VMX-compatible feature.
