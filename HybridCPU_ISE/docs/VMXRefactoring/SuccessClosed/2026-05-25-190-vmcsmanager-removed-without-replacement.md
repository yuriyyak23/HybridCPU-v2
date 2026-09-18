# 190 VmcsManager removed without replacement

Date: 2026-05-25

Status: closed

## Rule / basis

- VMX is a frozen compatibility frontend, not the authority owner for execution, memory, I/O, lane, evidence, observability, or migration state.
- Live generic behavior must have a neutral runtime owner or fail closed; dead VMCS authority is removed without replacement.
- `Legacy/VMX` can be declared empty only after production no longer constructs or calls the manager/interface carrier.

## Chosen disposition

- Full physical removal was selected.
- Native Lane6 and Lane7 already execute through neutral runtimes: `DmaStreamComputeRuntime` and `ExternalAcceleratorRuntime`.
- The VMCS-dependent guest augmentation has no admitted runtime-owned domain binding after the frozen frontend removal. It now rejects before token, backend, result, completion, or dirty-state effects rather than being copied to a new adapter.

## Removed

- Deleted `Legacy/VMX/Substrate/Runtime/Binding/VmcsManager.cs`.
- Deleted `NonRTL/Core/System/IVmcsManager.cs`.
- Removed VMCS-owned lane-completion routing and state binding from `CPU_Core`.
- Removed VMCS dirty and vector-dirty publication from memory, retire, DMA, and accelerator commit paths.
- Removed guest VMCS descriptor/evidence/completion helpers from Lane6 and Lane7 micro-ops.
- Removed global `VmxDirtyLogManager` / `IVmxDirtyWriteSink`; retained projection DTO vocabulary in `VmxDirtyLogProjectionTypes.cs`.
- Marked the manifest carrier entry `RemovedWithoutReplacement`; `Legacy/VMX` has no remaining source file.

## Intentionally not moved into Core

- No `VmcsManager` adapter, VMCS projection runtime manager, VMCS field store, active pointer, lane completion router, dirty sink, Lane7 descriptor authority, or DMA evidence provider was introduced.
- VMCSv2 generated/projection/checkpoint vocabulary that remains compiled outside the removed carrier is not claimed as a neutral runtime owner.

## Live behavior disposition

- Native Lane6 continues through `DmaStreamComputeRuntime` outside compatibility guest execution.
- Native Lane7 continues through `ExternalAcceleratorRuntime` outside compatibility guest execution.
- Frozen compatibility guest attempts to enter Lane6 or Lane7 now fail closed before token, backend, completion, result, or dirty effects.
- VMCS-based memory/vector/Lane6/Lane7 dirty augmentation and completion publication have no production binding.

## Conformance

- Added `LegacyVmcsManagerRemovalContract`.
- Updated `LegacyVmxQuarantineManifest` and `VmxProjectionSchemaAndQuarantineTests`.
- Static conformance proves absent carrier/interface, an empty `Legacy/VMX`, no forbidden VMCS owner markers in former live production paths, and no global VMCS dirty sink.
- Runtime conformance proves Lane6 rejection before token/write and Lane7 rejection before runtime result; task `188` frozen opcode routing remains typed fail closed.

## Verification

- `dotnet build HybridCPU_ISE.csproj --no-restore`: passed with existing warnings and no errors.
- `dotnet build HybridCPU_ISE.Tests.csproj --no-restore`: passed with existing warnings and no errors.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~LegacyVmcsManager"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~RemovedLegacyVmxExecutionUnit"`: passed.
- `dotnet test HybridCPU_ISE.Tests.csproj --no-build --filter "FullyQualifiedName~Phase04_DirectCompatRetireTransactionTests"`: passed after converting the remaining `VMREAD` / `VMXOFF` success assertions to fail-closed assertions.
- `LaneCompletion`, `VectorStream`, and `DirtyLog` name filters matched no executable tests; their removed manager binding is covered by the static full-removal contract and Lane6/Lane7 runtime denial tests.
- The broad `DmaStreamCompute` filter fails in five untouched repository-shape tests that expect a missing `Core/Execution/DmaStreamCompute/DmaStreamComputeRuntime.cs` path or reject existing native `NonRTL` runtime sources; the new manager-removal Lane6 test passes under `LegacyVmcsManager`.
- The broad `Retire` filter now passes the updated compatibility retire assertions but still fails in four unrelated repository-wide checks: a missing operational-semantics document and existing stream/compat/native-DMA boundary scans.

## Residual risk

- VMCSv2 generated/projection/checkpoint/vector compatibility model types remain compiled outside the deleted carrier and require a separate read-only/denied projection audit.
- Generic nested-domain projection/checkpoint work, build-time regeneration, and namespace extraction remain open architecture work.

## Next heavy step

- Audit remaining VMCSv2 projection/model/checkpoint and vector compatibility types; remove executable mutable compatibility authority or make it explicitly read-only/denied without reintroducing a manager.
