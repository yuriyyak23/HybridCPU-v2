# 2026-05-25-205 - Lane7 host evidence substrate extracted

Date: 2026-05-25

Status: closed

## Rule / basis

VMX is a frozen compatibility frontend, not the owner of Lane7 execution, native accelerator handles, backend binding evidence, scheduler pressure evidence, checkpoint evidence, or host-owned runtime facts. Host-owned evidence must be cleared on restore and rebuilt from host runtime state.

## Closed slice

Removed Lane7 host-owned evidence tables from the VMX-shaped state block:

- `_hostTokenByVirtualValue`
- `_virtualTokenByHostHandle`
- `_backendBindings`
- `_submitPollCount` scheduler pressure cache

`Lane7StateBlock` now delegates native token bindings, backend binding cache/epoch, and scheduler pressure cache/epoch to `Core/Runtime/Lanes/Lane7/HostOwnedEvidence/Lane7HostOwnedEvidenceStore`.

## Denied / read-only projection

`Lane7Checkpoint.ContainsNativeTokenHandle(...)` now returns false and no longer scans virtual tokens for native-handle-looking values. `Lane7VirtualToken.ExposesHostTokenHandle(...)` is an explicit denied compatibility helper. Checkpoint restore calls `HostEvidence.PrepareForRestore(EvidencePolicyDescriptor.FailClosed)`, clearing host-owned token/backend/scheduler evidence and requiring rebuild.

## Not introduced

No `VmcsManager`, `VmxExecutionUnit`, `VmcsV2Runtime`, VMCS field store, VMX accelerator manager, or renamed runtime owner was introduced. No VMCS region dictionary, active VMCS pointer, VMCS scalar field store, VMREAD/VMWRITE helper, VMFUNC bypass, VMX IOTLB runtime owner, or native accelerator handle serializer was added.

## Remaining vocabulary

`MemoryTranslationControl`, VMCS field aliases, VMX IOTLB/INVVPID aliases, and VMFUNC leaf names remain only frozen compatibility/schema/projection vocabulary. They do not own the extracted Lane7 host evidence.

## Live generic responsibilities

- Lane7 virtual handle/token ABI remains in `Lane7StateBlock`.
- Lane7 helper namespaces remain neutral under `Core/Runtime/Lanes/Lane7/Accelerators`.
- Native token/backend/scheduler evidence is owned by `Lane7HostOwnedEvidenceStore`.
- Nested projection/checkpoint admission remains owned by `NestedDomainProjectionCheckpointService`.

## Verification

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`: passed.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`: passed with existing warnings.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~Lane7HostOwnedEvidence"`: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed 47/47.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed 15/15.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed 4/4.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmcsV2"`: passed 5/5.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~Checkpoint|FullyQualifiedName~Migration|FullyQualifiedName~VectorStream|FullyQualifiedName~Lane7|FullyQualifiedName~Nested"`: passed 105/105.
- Static scans: `Legacy/VMX` empty; removed Lane7 VMX-substrate host-evidence markers absent; neutral Lane7 host-evidence store has no VMX/VMCS/INVVPID/VMFUNC/MemoryTranslationControl vocabulary.

## Residual risk

Generic substrate still physically lives mostly under `Core/VMX/Substrate`; this task extracts only the Lane7 host-owned evidence tables. Compatibility `MemoryTranslationControl`, VMCS field aliases, VMX IOTLB/INVVPID aliases, and VMFUNC vocabulary still need continued frozen-vocabulary conformance. VMX freeze is not declared.

## Next heavy step

Continue staged substrate extraction: move remaining generic runtime descriptors/services out of `Core/VMX/Substrate` or quarantine them as explicit compatibility projections, then wire a low-risk VMX compatibility admission path through `RuntimeBoundaryAdmissionService` without enabling a VMX-owned runtime.
