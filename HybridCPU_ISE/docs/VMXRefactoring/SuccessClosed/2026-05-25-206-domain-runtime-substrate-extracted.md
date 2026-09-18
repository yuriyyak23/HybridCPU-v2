# 2026-05-25-206 - domain-runtime substrate extracted

Status: closed

## Rule / basis

- VMX is not the virtualization architecture; VMX is a frozen compatibility frontend.
- Generic domain runtime services must not physically live under VMX substrate ownership when they are not VMX ABI/projection vocabulary.
- Frozen `MemoryTranslationControl`, VMCS field aliases, VMX IOTLB/INVVPID aliases, and VMFUNC names may remain compiled only as compatibility/projection vocabulary.

## Closed slice

The staged substrate extraction now moves the active generic domain-runtime service set out of `Core/VMX/Substrate/Runtime` and into neutral `Core/Runtime/Domains/*`:

- `DomainRuntimeContext`
- `DomainRuntimeOperation`
- `DomainRuntimeAuthority`
- `RootAuthorityDescriptor`
- `DomainLegalityService`
- `DomainValidationResult`
- `DomainSchedulingAdmission`
- `DomainBindingTable`

The old `Core/VMX/Substrate/Runtime/*` copies are absent. No `VmcsManager`, `VmxExecutionUnit`, VMCS field store, active VMCS pointer, VMCS projection runtime manager, or renamed VMX runtime owner was introduced.

## What intentionally stayed frozen

These names remain only as compatibility/projection vocabulary:

- `MemoryTranslationControl`
- `VmcsFieldProjectionSchema`
- `InvalidateVmxIotlbByVmid`
- `VMFUNC`
- VMCS field aliases and VMX IOTLB/INVVPID compatibility names

They are not used by the extracted neutral domain-runtime files as source-of-truth runtime state.

## Live generic responsibilities

Live domain runtime responsibility now sits under neutral runtime paths:

- runtime/root authority validation: `Core/Runtime/Domains/Authority`
- domain operation/context records: `Core/Runtime/Domains/Services`
- legality validation: `Core/Runtime/Domains/Legality`
- scheduling admission: `Core/Runtime/Domains/Scheduling`
- binding validation: `Core/Runtime/Domains/Binding`
- validation result vocabulary: `Core/Runtime/Domains/Validation`

Existing `RuntimeBoundaryAdmissionService` remains the neutral admission boundary and consumes typed capability/evidence/root authority requirements. This closure did not create a new runtime owner.

## Conformance

Added `DomainRuntimeSubstrateExtractionContract`:

- requires the old VMX substrate runtime paths to be absent;
- requires the new neutral `Core/Runtime/Domains/*` paths to exist;
- rejects `Vmcs`, `Vmx`, `VMCS`, `VMX`, `MemoryTranslationControl`, `VmcsField`, `INVVPID`, `VMFUNC`, `VmxRetireEffect`, and `IOMMU.VmxCompatibilityAliases` markers inside the extracted neutral runtime files;
- verifies runtime admission still routes through typed `DomainRuntimeAuthority`, `CapabilityBoundaryRequirement`, `EvidenceBoundaryRequirement`, and mutation denial;
- keeps frozen compatibility aliases visible only in their compatibility/projection locations.

Updated `VmxProjectionSchemaAndQuarantineTests` with `DomainRuntimeSubstrate_MovesToNeutralRuntimeDomainsAndKeepsCompatAliasesFrozen`.

## Verification

Passed:

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~DomainRuntimeSubstrate"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RuntimeBoundaryAdmissionTests"` - 4/4
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CapabilityCallerMaskIngress"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"` - 48/48
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"` - 1/1
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"` - 15/15
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~LegacyVmcsManager"` - 4/4
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmcsV2"` - 5/5

Static checks:

- `Legacy/VMX` remains empty.
- `Core/VMX/Substrate/Runtime/*` no longer hosts the extracted domain-runtime files.
- `Core/Runtime/Domains/*` has no VMX/VMCS/MemoryTranslationControl/INVVPID/VMFUNC vocabulary markers.

## Residual risk

This closure reduces physical substrate ownership risk but does not complete VMX freeze. Remaining work includes staged extraction/classification for descriptor, capability, memory, I/O, nested, projection-only compatibility, and any future generated artifacts that still physically live under `Core/VMX/Substrate`.

Known unrelated broad-filter failures were not rerun here. The recorded `DmaStreamCompute` and `Retire` repository-shape/documentation failures remain unrelated baseline context.

## Next heavy step

Continue staged substrate extraction: classify and move the next neutral descriptor/capability/memory/I/O/nested substrate slice out of `Core/VMX/Substrate`, leaving only explicit frozen compatibility projections and conformance vocabulary under `Core/VMX`.
