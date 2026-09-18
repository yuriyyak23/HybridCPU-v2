# 2026-05-25-202 Lane6 Host Evidence Rebuild Boundary Removed

Date: 2026-05-25

Status: closed

## Rule / Basis

VMX is a frozen compatibility frontend. Lane6 native token bindings and epoch legality are host-owned runtime evidence and must not be owned, restored, or serialized through VMX-shaped compatibility state.

This closure follows `audit2.md` and the VMX principles requirement that Lane6 use a neutral descriptor/token/fence/queue model, expose no native handle to compatibility projection, and rebuild backend/native evidence after restore.

## Closed Slice

- Deleted active `NonRTL/Core/Execution/DmaStreamCompute/VmxLane6QueueVirtualizer.cs` without replacement.
- Added neutral `Core/Runtime/Lanes/Lane6/Lane6QueueRuntime.cs` for queue, fence, identity, and epoch mechanics.
- Added neutral `Core/Runtime/Lanes/Lane6/HostOwnedEvidence/Lane6HostOwnedEvidenceStore.cs` for host-local `DmaStreamComputeTokenHandle` bindings.
- Changed `Lane6StateBlock` and DMA validation to consume the neutral runtime/evidence boundary.
- Made queue and fence epoch exhaustion fail closed with `DmaFaultKind.EpochExhausted`; no wrap-to-1 token reuse survives.
- Made restore preparation clear native-token bindings and require an explicit post-restore rebuild binding.

## Intentionally Not Moved

No `VmcsManager`, `VmxExecutionUnit`, VMCS field store, VMX lane manager, adapter, or renamed VMX runtime owner was introduced. Native handle binding was not copied into a checkpoint image, compatibility projection, or serializer.

## Live Generic Responsibilities

- Lane6 queue/fence identity and exhaustion policy: `Lane6QueueRuntime`.
- Host-local native token evidence: `Lane6HostOwnedEvidenceStore`.
- Migration/checkpoint rejection of native token evidence: `MigrationDescriptor`, `DomainCheckpointImage`, and the existing evidence policy boundary.
- Native DMA execution: the existing `DmaStreamComputeRuntime` path.

## Conformance

Added `Lane6HostOwnedEvidenceBoundaryRemovalContract` and tests proving:

- the VMX-shaped carrier and `_hostTokens` owner are absent;
- neutral Lane6 runtime/evidence sources contain no VMCS/VMX owner vocabulary;
- restore clears host token mappings and a new mapping appears only after explicit rebuild;
- migration/checkpoint policy rejects native-token evidence;
- exhausted queue/fence epochs fail closed rather than wrapping.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed; projection lineage verifier ran.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings; projection lineage verifier ran.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Lane6HostOwnedEvidence|FullyQualifiedName~EventTrapDomainIdentity|FullyQualifiedName~NestedDomainProjectionCheckpoint"`: passed, 3/3.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed, 45/45.
- `rg --files HybridCPU_ISE/Legacy/VMX`: no files.

## Residual Risk

This closes the active Lane6 native-token evidence carrier and rebuild proof only. VMCS compatibility vocabulary, remaining non-Lane6 backend/scheduler evidence review, neutral substrate directory extraction, and separately admitted nested execution remain before freeze.

## Next Heavy Step

Neutralize remaining event/trap VMID/VPID routing aliases and establish a generic nested projection/checkpoint admission owner without enabling a VMCS-owned runtime.
