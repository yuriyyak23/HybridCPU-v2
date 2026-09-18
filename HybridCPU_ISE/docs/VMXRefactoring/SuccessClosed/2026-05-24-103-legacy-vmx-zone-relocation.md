# 103. Legacy VMX zone relocation

Date: 2026-05-24

## Rule / basis

- VMX must no longer be the architectural axis of virtualization.
- VMX/VMCS/VMXCSR surfaces that remain frozen compatibility ABI or transitional state carriers should be explicitly separated from active generic substrate work.
- Legacy VMX-owned behavior, VMCS runtime carriers, Shadow VMCS compatibility vocabulary, and VMX-shaped invalidation bridges should not be mixed with the current `Core/VMX` substrate while they are still mostly legacy-shaped.
- Files moved to `Legacy/VMX` keep their relative folder structure so each unit can later be refactored and promoted back into `Core/VMX` through descriptor/capability/evidence/runtime boundaries.

## What changed

- Moved the following legacy-heavy `.cs` files from `Core/VMX` to `Legacy/VMX`, preserving subfolder structure:
  - `Compatibility/Adapters/LegacyVmxV1/CPU_Core.PipelineExecution.Vmx.cs`
  - `Compatibility/Adapters/LegacyVmxV1/ExecutionDispatcherV4.Vmx.cs`
  - `Compatibility/Adapters/LegacyVmxV1/LegacyVmxV1AdapterBoundary.cs`
  - `Compatibility/Adapters/LegacyVmxV2/LegacyVmxV2AdapterBoundary.cs`
  - `Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`
  - `Compatibility/Generated/VmcsProjection/ShadowVmcsBlock.cs`
  - `Substrate/Memory/Iommu/IOMMU.DomainBinding.partial.cs`
  - `Substrate/Runtime/Binding/VmcsManager.cs`
- Updated `HybridCPU_ISE.Tests/VmxRefactoring/VmxCapsProjectionBoundaryTests.cs` so the static no-regression check now inspects the relocated `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs`.
- Left smaller follow-up seams in `Core/VMX` in place:
  - `Substrate/Memory/Translation/MemoryTranslationControl.cs` still has a transitional `FromVmcs` adapter.
  - `Substrate/Memory/Invalidation/TranslationInvalidationService.cs` still bridges to VMX-shaped IOMMU invalidation.

## How verified

Commands:

```powershell
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --filter "FullyQualifiedName~VmxCapsProjectionBoundaryTests"
```

Static scan:

- Confirmed the selected files are absent from `Core/VMX` and present under `Legacy/VMX`.
- Confirmed the remaining legacy markers in `Core/VMX` are limited follow-up seams, not full legacy VMX carriers.

## Build / test result

- Main project build after relocation: succeeded, 0 errors. The first full post-move build reported 54 pre-existing warnings; the final control build reported 0 warnings and 0 errors.
- Focused conformance tests: passed, 3/3.
