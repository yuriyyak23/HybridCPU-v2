# 2026-05-25-203 Event/Trap and Nested Checkpoint Admission Neutralized

Date: 2026-05-25

Status: closed

## Rule / Basis

VMID/VPID and Shadow VMCS terms may remain only as frozen compatibility vocabulary. Executable event/trap identity and nested projection/checkpoint restore authority belong to neutral domain descriptors and evidence/migration policy.

This closure follows `audit2.md` and the VMX principles rules for generic address-space tags, nested domain composition, checkpoint images without host evidence, and fail-closed compatibility frontend behavior.

## Closed Slice

- Replaced executable event/trap routing identity fields and parameters with `ExecutionDomainTag` / `AddressSpaceTag` in event descriptors, interrupt remap, posted queues, interrupt fabric, trap requests, and virtual timers.
- Removed the `vmid` routing alias from the compatibility `EventInjectionBlock.TryDeliver(...)` helper; it now delegates using `executionDomainTag`.
- Added neutral `Core/Runtime/Nested/NestedDomainProjectionCheckpointService.cs`, which validates `NestedProjectionRequest` together with domain-owned checkpoint restore.
- Kept `ShadowVmcsNestedProjectionService` denied; it cannot bypass the neutral projection/checkpoint validation owner.
- Denied nested restore whenever the domain checkpoint contains host-owned/native-token evidence.

## Intentionally Not Moved

No Shadow VMCS block, VMCS field store, nested VMCS manager, VMX-owned checkpoint service, VM-entry/VM-exit owner, or compatibility nested success path was introduced. The new service does not depend on `VmcsV2Descriptor`, `VmcsField`, VMCS blocks, or native token handles.

## Live Generic Responsibilities

- Event/trap identity and queue routing: neutral event/trap substrate descriptors using execution-domain and address-space tags.
- Nested projection validation: `NestedProjectionService` and `NestedDomainProjectionCheckpointService`.
- Checkpoint restore policy: `DomainCheckpointImage`, `RestoreValidationService`, and `MigrationValidationPolicy`.
- Compatibility nested frontend: generated `ShadowVmcsNestedProjectionService`, still fail-closed.

## Conformance

Added:

- `EventTrapDomainIdentityAuthorityRemovalContract`, proving no executable event/trap surface or compatibility event delivery helper routes on VMID/VPID aliases.
- `NestedDomainProjectionCheckpointOwnerContract`, proving the neutral owner contains no VMX/VMCS state dependency, allows domain-owned checkpoint restore, rejects native-token evidence, and leaves Shadow VMCS compatibility admission denied.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed; projection lineage verifier ran.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings; projection lineage verifier ran.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Lane6HostOwnedEvidence|FullyQualifiedName~EventTrapDomainIdentity|FullyQualifiedName~NestedDomainProjectionCheckpoint"`: passed, 3/3.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 45/45.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests|FullyQualifiedName~RemovedLegacyVmxExecutionUnit|FullyQualifiedName~LegacyVmcsManager"`: passed, 20/20.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2|FullyQualifiedName~Checkpoint|FullyQualifiedName~Migration|FullyQualifiedName~VectorStream|FullyQualifiedName~Nested"`: passed, 26/26.

## Residual Risk

Nested compatibility execution is still intentionally denied; this closure supplies a neutral admission owner rather than a success path. Remaining blockers include frozen VMCS translation/field/vector vocabulary, accelerator/backend evidence review, and physical extraction of generic substrate from `Core/VMX/Substrate`.

## Next Heavy Step

Extract the next neutral runtime/domain family from `Core/VMX/Substrate` and audit remaining compiled VMCS/vector/accelerator compatibility aliases for executable authority pressure.
