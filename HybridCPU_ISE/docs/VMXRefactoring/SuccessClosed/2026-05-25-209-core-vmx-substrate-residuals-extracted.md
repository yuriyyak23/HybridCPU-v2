# Core VMX Substrate Residuals Extracted

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend, not the virtualization/runtime architecture. Generic capability grants, vector-stream state, host/guest/debug evidence policy, Lane7 token/handle/completion helper state, event trap DTOs, and nested composition/filter policy belong to neutral runtime/domain owners. VMX-shaped names may remain only as ABI/projection/conformance/quarantine vocabulary.

## What Changed

- Moved typed capability runtime services from `Core/VMX/Substrate` to `Core/Runtime/Capabilities/*`:
  - `CapabilityGrantCollection`
  - `CapabilityDescriptorSet`
  - `CapabilityNegotiationService`
  - `CapabilityPublicationPolicy`
- Isolated compatibility mask ingress in `Core/VMX/Compatibility/Frontend/Projection/CapabilityCompatibilityProjection.cs`.
- Moved neutral residuals from `Core/VMX/Substrate` to `Core/Runtime/*`:
  - host-owned, guest-visible, and debug trace evidence policy;
  - vector-stream state and save/restore projection;
  - Lane7 token, handle, backend-binding, completion, checkpoint, and virtual-token evidence helper surfaces that do not carry VMX/VMCS vocabulary;
  - neutral virtual timer/domain trap DTOs;
  - nested capability filter, nested evidence policy, and nested memory-composition services.
- Removed `Core\VMX\Substrate\` folder includes from `HybridCPU_ISE.csproj`.

## What Was Not Moved

No `VmcsManager`, `VmxExecutionUnit`, VMCS field store, active VMCS pointer, VMCS runtime manager, or renamed projection-runtime owner was created.

The remaining `Core/VMX/Substrate` sources are explicitly quarantined compatibility vocabulary for this step:

- `MemoryTranslationControl` and VMX invalidation aliases;
- completion/VM-exit projection and record vocabulary;
- VMX trap/intercept vocabulary and preemption-timer projection;
- vector-stream VMCS host-evidence compatibility helper;
- Lane7 VMFUNC/VM-exit compatibility state and VMCS host-evidence checkpoint helper;
- nested compatibility completion/intent/translation mappers;
- VMX retire-evidence observers.

## Live Generic Responsibilities

- Capability authority: `Core/Runtime/Capabilities/*`.
- Vector-stream state/save-restore: `Core/Runtime/Lanes/VectorStream/*`.
- Evidence policy: `Core/Runtime/Evidence/*`.
- Lane7 token/handle/completion helper policy: `Core/Runtime/Lanes/Lane7/*`.
- Event trap DTO/timer state: `Core/Runtime/Events/Traps/*`.
- Nested capability/evidence/composition services: `Core/Runtime/Nested/*`.

## Conformance Added

- `CapabilityRuntimeSubstrateExtractionContract`
- `CoreVmxSubstrateResidualExtractionContract`
- `VmxProjectionSchemaAndQuarantineTests` coverage for:
  - old VMX substrate paths absent;
  - neutral runtime paths present and free of VMX/VMCS authority markers;
  - compatibility mask ingress isolated as projection-only;
  - remaining VMX substrate sources listed as frozen quarantine;
  - `Legacy/VMX` empty;
  - project file has no `Core\VMX\Substrate\` folder ownership.

## Verification

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore` passed with existing warnings.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore` passed with existing warnings.
- `dotnet test ... --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"` passed 52/52.
- `dotnet test ... --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"` passed 1/1.
- `dotnet test ... --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"` passed 15/15.
- `dotnet test ... --filter "FullyQualifiedName~LegacyVmcsManager"` passed 4/4.
- `dotnet test ... --filter "FullyQualifiedName~CapabilityRuntimeSubstrate|FullyQualifiedName~CoreVmxSubstrateResiduals"` passed 2/2.
- `dotnet test ... --filter "FullyQualifiedName~VmcsV2"` passed 5/5.
- `dotnet test ... --filter "FullyQualifiedName~Capability"` passed 45/45.
- `dotnet test ... --filter "FullyQualifiedName~VectorStream"` passed 1/1.
- `dotnet test ... --filter "FullyQualifiedName~Lane7"` passed 81/81.
- `dotnet test ... --filter "FullyQualifiedName~Nested"` passed 13/13.
- `dotnet test ... --filter "FullyQualifiedName~Checkpoint"` passed 3/3.
- `dotnet test ... --filter "FullyQualifiedName~Migration"` passed 8/8.
- `dotnet test ... --filter "FullyQualifiedName~DirtyLog"` and `RetireEvidence` found no matching tests.
- `rg --files "...\\Legacy\\VMX"` produced no files.

Known unrelated broad-filter result:

- `dotnet test ... --filter "FullyQualifiedName~Evidence"` failed 3 tests due missing repository documentation surfaces:
  - `Documentation\validation-baseline.md`
  - `Documentation\paper-claim-evidence-map.md`

## Residual Risk

This is still not VMX freeze. Remaining compiled compatibility surfaces must be semantically reviewed or removed: retire evidence, VMX completion/trap projection, nested compatibility mappers, `MemoryTranslationControl`, VMX invalidation aliases, and Lane7 VMFUNC/VM-exit state.

## Next Heavy Step

Remove or read-only quarantine the remaining VMX completion/trap/nested/retire compatibility surfaces, then prove that any live retire publication, completion routing, nested mapping, and event/trap authority is owned by neutral runtime descriptors rather than VMX vocabulary.
