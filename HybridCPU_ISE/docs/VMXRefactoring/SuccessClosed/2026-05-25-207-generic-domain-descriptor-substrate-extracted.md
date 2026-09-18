# 2026-05-25-207 - generic domain descriptor substrate extracted

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the virtualization architecture.
- Generic descriptors and admission services must not remain physically owned by `Core/VMX/Substrate` once they are not VMX ABI/projection vocabulary.
- VMX, VMCS, VMCSv2, `MemoryTranslationControl`, VMX IOTLB/INVVPID, VMFUNC, and VmxCaps names may remain only as frozen compatibility/projection vocabulary.

## Closed slice

This staged extraction moved the next generic substrate pack out of `Core/VMX/Substrate`:

- execution, memory, I/O, Lane6, Lane7, completion-route, event-queue, evidence-policy, observability, and trap-policy descriptors now live under `Core/Runtime/Domains/Descriptors`;
- execution, memory, I/O, Lane6, Lane7, nested, and vector-stream domain admission runtimes now live under `Core/Runtime/Domains/Admission`;
- `NestedDomainDescriptor`, `INestedProjectionService`, `NestedProjectionService`, and `NestedValidationResult` now live under `Core/Runtime/Nested`.

No `VmcsManager`, `VmxExecutionUnit`, active VMCS pointer, VMCS field store, `VmcsV2RuntimeManager`, projection runtime manager, or renamed VMX-owned runtime owner was introduced.

## What intentionally stayed frozen or residual

Frozen compatibility/projection vocabulary remains compiled in its compatibility locations:

- `MemoryTranslationControl`
- VMCS field projection schema
- VMX IOTLB/INVVPID aliases
- VMFUNC vocabulary
- `VmxCapsProjection`

`CapabilityDescriptorSet` and generated `CapabilityDescriptorSetSchema.VmxCompatibility` intentionally remain a separate capability generated-projection placement slice. They were not moved in this task because that surface still carries explicit VmxCaps compatibility vocabulary and needs its own extraction/quarantine proof.

## Conformance

Added `GenericDomainSubstrateExtractionContract` and the test `GenericDomainSubstrate_MovesDescriptorsAndAdmissionToNeutralRuntime`.

The proof covers:

- old descriptor/admission/nested projection paths absent under `Core/VMX/Substrate`;
- new neutral paths present under `Core/Runtime/Domains/*` and `Core/Runtime/Nested/*`;
- no VMX/VMCS/`MemoryTranslationControl`/VMCS field/INVVPID/VMFUNC/VmxCaps/Shadow VMCS markers in moved neutral runtime files;
- frozen aliases remain only in compatibility/projection locations;
- live admission semantics still work through neutral descriptors and domain runtimes;
- `Legacy/VMX` remains empty.

Updated existing path contracts:

- `AddressSpaceCanonicalIdentityAuthorityRemovalContract`
- `TranslationIoLaneIdentityAuthorityRemovalContract`
- `VmcsV2VectorStreamProjectionAuthorityRemovalContract`
- `VmcsV2BlocksDirtyVectorCheckpointAuthorityRemovalContract`
- `NestedDomainProjectionCheckpointOwnerContract`

## Verification

Passed:

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~GenericDomainSubstrate"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~DomainRuntimeSubstrate"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"` - 49/49
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~NestedDomainProjectionCheckpoint"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"` - 15/15
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmcsManager"` - 4/4
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmcsV2"` - 5/5
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~TranslationIoLaneIdentity"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~AddressSpaceCanonicalIdentity"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VectorStream"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~Nested"` - 13/13

Static checks:

- `Legacy/VMX` is empty.
- `Core/VMX/Substrate/Descriptors` now only contains the capability descriptor slice.
- `Core/Runtime/Domains/*` and `Core/Runtime/Nested/*` contain no VMX/VMCS/`MemoryTranslationControl`/INVVPID/VMFUNC/VmxCaps/Shadow VMCS markers.

## Residual risk

This closes only the generic descriptor/admission/nested projection primitive extraction slice. Remaining freeze blockers include:

- capability generated projection/schema placement;
- memory translation and checkpoint/migration services;
- I/O/DMA/IOTLB services;
- completion/event services;
- Lane6/Lane7 state block placement;
- explicit frozen compatibility aliases and generated projection surfaces.

Known unrelated broad-filter failures were not rerun here. The recorded `DmaStreamCompute` and `Retire` repository-shape/documentation failures remain unrelated baseline context.

## Next heavy step

Extract or quarantine the next service-oriented substrate pack: capability generated projection placement plus memory/I/O/migration/completion/event/lane services, keeping VMX-shaped names only as frozen compatibility projections.
