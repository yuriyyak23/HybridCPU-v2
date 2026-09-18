# Phase 10 - Memory I/O IOMMU Lanes And Stream Boundary

## Goal

Classify memory, I/O, IOMMU, lanes, DmaStreamCompute, StreamEngine, assist, and L7-SDC surfaces as runtime/helper/model surfaces unless an explicit neutral owner and virtualization publication gate exists.

## Activation Authority

This phase creates review material only. It does not authorize runtime activation, VMX backend execution, VMCS mutation, SecureCompute backend execution, or production publication behavior.

## Closure Decision - ADR-VIRT-MEM-IO-LANE-STREAM-2026-06-05

Phase 10 is closed as owner-first denial/readiness only. The current codebase contains bounded neutral runtime/helper contours for memory-domain read-only projection, I/O/IOMMU descriptors, Lane6 DSC1 execution, Lane6 descriptor validation evidence, L7-SDC command execution, and stream/helper models. None of those contours is a VMX compatibility backend owner, SecureCompute authority, mutable VMCS state, completion-publication authority, or VMX retire-publication authority.

The closure decision is intentionally asymmetric:

- Existing neutral MemoryDomainDescriptor VMREAD projection vocabulary remains read-only and field-scoped.
- Existing I/O/IOMMU VMX aliases remain frozen denied compatibility vocabulary returning no binding, translation, invalidation, dirty-log, or mutation result.
- Existing Lane6/Lane7 production contours remain owned by their native runtime domains and continue to reject guest compatibility execution under the frozen VMX frontend.
- Existing stream/helper/telemetry/replay evidence remains helper/model evidence only.
- Future VMX memory/I/O/IOMMU/Lane6/Lane7/Stream activation still requires a neutral owner, capability contract, evidence policy, runtime admission, backend authority, completion fence, retire publication rule, negative tests, static gates, and documentation.

## Current Code Baseline

- The lane topology remains fixed at W=8: lanes 0-3 ALU, lanes 4-5 LSU, lane 6 DMA/stream, lane 7 branch/system.
- Stream WhiteBook current contract records a bounded Phase 06 DSC1 lane6 contour through `DmaStreamComputeMicroOp` and `DmaStreamComputeRuntime`.
- `DmaStreamComputeDescriptorParser.ExecutionEnabled` is `true` for the current DSC1 contour, while unsupported shapes and later descriptor classes remain fail-closed.
- The runtime helper path stages token data and publishes memory only through explicit token commit.
- Guest Lane6 compatibility execution remains fail-closed for the frozen VMX frontend.
- L7-SDC has bounded command contours through `SystemDeviceCommandMicroOp` and `ExternalAcceleratorRuntime`, with guard-backed descriptor requirements and no legacy accelerator fallback.

## Current-State Matrix

| Surface | Model/helper surface | Evidence surface | Compatibility projection vocabulary | Runtime admission | Backend execution authority | Completion publication | Retire publication | Future owner requirements |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Memory composition / memory-domain translation | `MemoryDomainDescriptor`, `MemoryDomainTranslationControl`, read-only translation view | Guest architectural and compatibility-alias evidence for selected memory-owned VMCS fields | `GuestCr3`, `EptPointer`, `Vpid`, and `Cr3TargetCount` may be read-only projected from neutral memory state; writes remain denied | `MemoryDomainRuntime` requires a memory descriptor and requested neutral sub-descriptors | Denied for VMX compatibility; memory projection is not a backend owner | Denied | Denied | Neutral host-address-space, dirty-tracking, second-stage, and publication owners before any expansion |
| I/O / IOMMU / DMA authority | `IoDomainDescriptor`, `DmaWindowDescriptor`, `IommuDomainBinding`, `DmaAuthorityService`, `IotlbInvalidationService` | Domain, IOTLB, queue, fence, and DMA-window evidence | `IommuVmxCompatibilityAliases` are retained as denied read-only compatibility vocabulary | `IoDomainRuntime` requires DMA/IOMMU authority, virtualization block, DMA window, and compatibility permission | Denied for VMX aliases; neutral `IoVirtualizationBlock` authority is not a VMX backend | Denied | Denied | Neutral I/O owner, DMA-window policy, IOTLB invalidation owner, fence policy, and negative VMX alias gates |
| Lane6 DSC1 / DMA stream | `DmaStreamComputeMicroOp`, `DmaStreamComputeRuntime`, `Lane6DomainRuntime`, queue/fence/token runtime | Replay evidence, token lifecycle, owner/domain guard, optional `VmxDmaDescriptorValidationEvidence` | Guest Lane6 compatibility execution is fail-closed; validation evidence is host-owned Lane6 evidence, not frontend authority | `Lane6DomainRuntime` requires runtime-authoritative Lane6 descriptor, namespace binding, queue binding, fence domain, and optional compatibility projection permission | Current native Lane6 DSC1 contour only; denied as VMX compatibility backend authority | Denied for VMX compatibility | Native token commit remains Lane6 runtime behavior; denied as VMX retire effect publication | Neutral DMA-domain owner, queue/fence owner, compatibility policy, completion fence, retire rule, and no-emission proof |
| Lane7 / L7-SDC | `SystemDeviceCommandMicroOp`, `ExternalAcceleratorRuntime`, `Lane7DomainRuntime`, `Lane7CompletionPolicy` | Guard-backed descriptor evidence, virtual tokens/handles, host-owned Lane7 evidence | Guest Lane7 compatibility execution is fail-closed; reserved taxonomy rows grant no execution or compiler-emission authority | `Lane7DomainRuntime` requires runtime-authoritative Lane7 descriptor, backend binding, handle/token namespace, completion route, and compatibility permission | Current native L7-SDC contour only; denied as VMX or SecureCompute authority | Native Lane7 completion policy remains domain-local; denied as VMX completion publication | Native register writeback/fence behavior remains domain-local; denied as VMX retire publication | Neutral accelerator owner, descriptor taxonomy acceptance, backend binding, route policy, completion fence, retire rule, and no-emission proof |
| StreamEngine / assist / cache / conflict / telemetry / replay | Helper/model surfaces only | Helper evidence, telemetry, conflict/cache observations | No VMX projection authority | No virtualization runtime admission | Denied | Denied | Denied | Neutral owner and evidence policy before any virtualization use |
| Nested Lane6/Lane7 passthrough | Nested descriptors require lane passthrough blocked | Nested evidence excludes host-owned lane evidence | Shadow VMCS bridge remains fail-closed | Nested runtime admission remains denied without neutral nested owner | Denied | Denied | Denied | Separate nested owner decision after Phase 09 conditions remain closed |

## Already Closed / Must Not Reopen

- Do not use stream, lane6, assist, L7, telemetry, token, replay, conflict, or cache observations as virtualization authority.
- Do not use `DmaStreamComputeRuntime` as a VMX backend owner.
- Do not use L7 command runtime as SecureCompute authority.
- Do not treat compiler sideband preservation as production lowering permission.
- Do not treat IOMMU/cache/conflict models as current virtualization memory publication.
- Do not treat `VmxDmaDescriptorValidationEvidence` as VMX backend authority, completion publication, or retire publication.
- Do not treat Lane6/Lane7 guest compatibility fail-closed checks as activation paths.
- Do not treat VMCS/VMREAD memory-owned projection as I/O, IOMMU, Lane6, Lane7, Stream, or SecureCompute authority.

## Required Code/Doc Anchors

- `Documentation/Stream WhiteBook/DmaStreamCompute/01_Current_Contract.md`
- `Documentation/Stream WhiteBook/ExternalAccelerators/11_DmaStreamCompute_And_Assist_Separation.md`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Memory/MemoryDomainRuntime.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/IO/IoDomainRuntime.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/IO/Dma/DmaAuthorityService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/IO/Iotlb/IotlbInvalidationService.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Virtualization/Compatibility/Adapters/IO/IommuVmxCompatibilityAliases.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane6DmaStream/DmaStreamComputeMicroOp.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Lane6/Lane6DomainRuntime.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeDescriptorParser.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/DmaStreamCompute/VmxDmaDescriptorValidator.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Pipeline/MicroOps/Lane7Accelerator/SystemDeviceCommandMicroOp.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Domains/Admission/Lane7/Lane7DomainRuntime.cs`
- `HybridCPU_ISE/CloseToHSL/Core/Runtime/Lanes/Lane7/Completion/Lane7CompletionPolicy.cs`
- `HybridCPU_ISE/NonRTL/Core/Execution/ExternalAccelerators/*`
- `HybridCPU_ISE.Tests/VmxRefactoring/VmxMemoryIoLaneStreamBoundaryHardeningTests.cs`
- Relevant `HybridCPU_ISE.Tests/tests/DmaStreamCompute*.cs` and `HybridCPU_ISE.Tests/tests/L7Sdc*.cs`

## Work Items

- Boundary table for memory, I/O/IOMMU, Lane6 DSC1, `VmxDmaDescriptorValidator`, Lane7/L7-SDC, StreamEngine/helper, and nested passthrough surfaces is recorded above.
- Current bounded contours are documented as implemented in their own runtime domains, not virtualization authority.
- DSC2 runtime execution, async/queue VMX ownership, coherent DMA/cache, broad compiler lowering, global memory-conflict authority, and VMX Lane6/Lane7 passthrough remain future-gated or denied.
- VMX-compatible paths must request neutral memory/I/O/lane owners before any projection, backend execution, completion publication, or retire publication.
- Documentation must avoid stale "all fail-closed" claims while also avoiding claims that bounded native contours authorize virtualization.

## Explicit Non-Goals

- Do not change Stream or L7 implementation.
- Do not make DmaStreamCompute a virtualization backend.
- Do not make ACCEL command contours virtualization authority.
- Do not add VMX projection for stream or L7 state.
- Do not turn helper telemetry into evidence authority.

## Done Criteria

- Current DSC1 lane6 contour is documented accurately and narrowly.
- Future DSC2/async/IOMMU/cache/coherent/broad lowering items remain gated.
- L7-SDC current command contour is separated from VMX/SecureCompute authority.
- Tests and docs agree on helper/model/authority classification.
- `VmxMemoryIoLaneStreamBoundaryHardeningTests` passes as the focused Phase 10 guard.
- Phase 13 records executable static gates for source anchors, shortcut denial, documentation overclaim denial, and compiler no-emission.

## Required Tests / Static Checks

- `FullyQualifiedName~VmxMemoryIoLaneStreamBoundaryHardeningTests`
- DmaStreamCompute parser/runtime/token/commit tests.
- L7-SDC guard, status, submit, fence, conflict, and no-legacy-fallback tests.
- Compiler DmaStreamCompute contract tests.
- Static scan for stream/L7 claim language in documentation.
- Static scan that VMX frontend and SecureCompute projection code do not reference `DmaStreamComputeRuntime`, `VmxDmaDescriptorValidator`, `ExternalAcceleratorRuntime`, `Lane6DomainRuntime`, `Lane7DomainRuntime`, `Lane7CompletionPolicy`, `DmaStreamComputeRetirePublication`, or `SystemDeviceCommandMicroOp`.
- Static scan that memory/I/O/lane boundary sources do not introduce `VmcsManager`, `IVmcsManager`, `VmxExecutionUnit`, `TrapCompletionRouteDescriptor.RuntimeOwnedPublication`, compatibility completion records, or VMX retire effects.
- Static scan that `HybridCPU_Compiler/Core` and Non-VMX ISA metadata do not emit VMX activation or mutation opcodes for Phase 10.

## Residual Risk

The live stream baseline has moved beyond the old prompt text. The risk is two-sided: preserving stale fail-closed statements would be inaccurate, while broadening the bounded contour into virtualization authority would be unsafe.

## External Audit Risk Update

Stream, Lane6, L7, I/O, IOMMU, telemetry, tokens, and replay evidence are runtime/helper/model surfaces, not virtualization authority. No stream or accelerator evidence may satisfy VMX owner, SecureCompute owner, completion publication, or retire publication requirements.

## Next Phase Dependency

Phase 11 depends on these boundaries to keep capabilities, evidence, and SecureCompute ownership neutral.
