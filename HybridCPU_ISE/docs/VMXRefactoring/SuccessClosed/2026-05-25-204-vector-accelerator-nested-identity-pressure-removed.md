# 2026-05-25-204 - vector/accelerator/nested identity pressure removed

Date: 2026-05-25

Status: closed

## Rule / basis

VMX is a frozen compatibility frontend, not the runtime owner. Vector-stream faults, Lane7 accelerator handles/tokens/completions, and nested child intent/checkpoint admission must use neutral runtime/domain names. VMCS/VMX names may remain only as frozen ABI, generated projection, explicit legacy quarantine, or conformance vocabulary.

## Closed slice

- Removed `VmxVectorExceptionInfo` and `VmxStreamDescriptorFaultInfo` without replacement.
- Added neutral `VectorStreamExceptionInfo` and `VectorStreamDescriptorFaultInfo` with `ExecutionDomainTag` and `AddressSpaceTag`.
- Changed VMCSv2 exit-info projection to encode compatibility qualifications from neutral vector-stream DTOs and `addressSpaceTag`, not `vpid`.
- Removed `VmxAcceleratorHandleMap`, `VmxLane7TokenVirtualizer`, `VmxAcceleratorCompletionRouter`, and `VmxAcceleratorPolicy` without replacement.
- Added neutral Lane7 helper namespaces under `Core/Runtime/Lanes/Lane7/Accelerators`.
- Removed `TryVmRead`, `TryVmWrite`, `Vmcs12Pointer`, and `VmcsField.Vpid` from `ChildDomainIntentDescriptor` substrate identity.
- Kept `NestedDomainProjectionCheckpointService` as the neutral projection/checkpoint admission owner and proved host-owned evidence restore remains denied.

## What was intentionally not moved

No `VmcsManager`, `VmxExecutionUnit`, VMCS field store, active VMCS pointer, VMCS12 runtime manager, or renamed VMX runtime owner was introduced. The compatibility VMCS projection still only publishes compatibility-facing qualifications and remains non-authoritative.

## Remaining live generic responsibilities

- Vector-stream execution remains with `VectorStreamDomainRuntime`.
- Lane7 virtual handles/tokens/completions remain with `Lane7StateBlock` plus neutral Lane7 accelerator namespaces.
- Nested projection/checkpoint admission remains with `NestedDomainProjectionCheckpointService`.
- Domain checkpoint/restore legality remains with `DomainCheckpointImage`, `MigrationValidationPolicy`, and `RestoreValidationService`.

## Conformance

Added `VectorAcceleratorNestedIdentityPressureRemovalContract` and `VectorAcceleratorNestedIdentityPressure_UsesNeutralRuntimeBoundary`.

The contract verifies:

- old VMX-shaped vector DTO and accelerator wrapper files are absent;
- neutral vector DTOs do not carry VMID/VPID or VMX-shaped names;
- VMCS replay qualification uses `addressSpaceTag`;
- neutral Lane7 accelerator namespaces replace VMX-shaped wrappers;
- child intent no longer exposes VMCS12/VMREAD/VMWRITE/VPID identity aliases;
- nested projection/checkpoint restore rejects host-owned evidence.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VectorAcceleratorNestedIdentityPressure"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, `46/46`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed, `1/1`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed, `15/15`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed, `4/4`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmcsV2"`: passed, `5/5`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VectorStream|FullyQualifiedName~Lane7|FullyQualifiedName~Nested"`: passed, `94/94`.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Checkpoint|FullyQualifiedName~Migration|FullyQualifiedName~VectorStream|FullyQualifiedName~Lane7|FullyQualifiedName~Nested"`: passed, `104/104`.

## Residual risk

VMX freeze is still not declared. `MemoryTranslationControl`, frozen VMCS field aliases, explicit VMX IOTLB aliases, `INVVPID` opcode vocabulary, VMFUNC leaf compatibility vocabulary, Lane7 backend host-handle rebuild proof, and physical substrate placement under `Core/VMX/Substrate` remain separate freeze blockers.

## Next heavy step

Continue substrate extraction and host-owned evidence proof: move or quarantine remaining generic `Core/VMX/Substrate` domain services into neutral `Core/Runtime`, `Core/Memory`, `Core/IO`, and capability/evidence homes, with end-to-end checks for Lane7 backend handles and scheduler/cache evidence restore.
