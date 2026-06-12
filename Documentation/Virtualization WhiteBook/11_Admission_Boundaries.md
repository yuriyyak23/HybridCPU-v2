# Admission Boundaries

Runtime boundary admission is the line between compatibility vocabulary and production authority.

## RuntimeBoundaryAdmissionService

`RuntimeBoundaryAdmissionService` validates:

- domain runtime context presence;
- domain boundary requirements;
- capability requirements;
- evidence requirements;
- frontend mutation restrictions;
- root/domain runtime authority.

The decision enum is neutral. It does not use VMX exit reasons as authority.

## VMREAD Admission

`VmxCompatibilityAdmissionService.AdmitVmReadProjection` performs:

1. VMREAD decode validation.
2. Compatibility alias projection validation for `VMREAD`.
3. Runtime admission with `DomainRuntimeOperationKind.ReadCompatibilityProjection`.
4. Evidence check for `CompatibilityAlias`.
5. Generated `VmcsFieldProjectionSchema` owner lookup.
6. `VmcsReadOnlyValueProjectionService` dispatch to a neutral value source.
7. Read-only value projection or explicit denied/fail-closed result.

The result can be `ReadOnlyValueProjected`, `ReadOnlyProjectionDenied`, `DecodeDenied`, `ProjectionDenied`, or `RuntimeAdmissionDenied`. Historical scalar-projection vocabulary remains compatibility ABI, but the current admitted value path does not fall back to a VMCS field store.

The important detail: runtime admission is for projection. It is not a VMCS execution backend.

Current admitted VMREAD values come only from:

- neutral `CompletionRecord` for completion-owned fields;
- neutral `MemoryDomainReadOnlyTranslationView` for the admitted memory-owned slice;
- neutral `ExecutionDomainReadOnlyStateView` for `GuestPc`, `GuestSp`, and `GuestFlags`.

## VMCALL Trap Admission

`VmxCompatibilityAdmissionService.AdmitVmCallTrapProjection` performs:

1. Creates a neutral `TrapRequest` for a compatibility operation.
2. Decodes `VMCALL`.
3. Validates the frozen `VMCALL` alias projection.
4. Calls `RuntimeBoundaryAdmissionService` with `ProjectCompatibilityTrap`.
5. Requires a runtime-authoritative `TrapPolicyDescriptor`.
6. Evaluates `TrapPolicyBitmap`.
7. Produces `NeutralTrapResult`.
8. Projects through `VmxTrapProjectionMapper`.
9. Evaluates neutral hypercall backend admission; production uses missing backend owner.
10. Evaluates neutral trap completion route; production uses projection-only denied route.
11. Denies backend execution through `TrapCompletionPublicationFence`.

The final admitted-denied decision is `TrapProjectionDeniedBackend`.

The existence of `RuntimeOwnedCompletionPublication` does not change this production chain. It is a future-gated runtime route descriptor and is not selected by the VMX frontend.

## Runtime-Owned Policy Requirements

For trap projection, the runtime policy descriptor must:

- exist;
- be runtime-authoritative;
- allow compatibility projection;
- require/receive a validated domain when configured;
- allow the `CompatibilityOperation` trap class;
- produce a neutral trap result.

If any condition fails, projection is denied.

## Projection-Only Operations

The following operation kinds are currently used as projection-only compatibility requests:

- `ReadCompatibilityProjection`
- `ProjectCompatibilityTrap`

Projection-only status prevents the compatibility frontend from being classified as an authoritative mutation source.

## Admission Does Not Mean Publication

Admission can prove that a projection request is legitimate. It does not prove:

- backend execution;
- completion publication;
- retire publication;
- architectural state mutation.

Those later gates have their own neutral owners.
