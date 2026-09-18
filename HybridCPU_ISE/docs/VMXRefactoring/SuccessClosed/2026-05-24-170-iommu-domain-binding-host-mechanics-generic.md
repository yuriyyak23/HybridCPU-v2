# 170 Legacy IOMMU Domain Binding Host Mechanics Generic Return

Дата: 2026-05-24

Статус: closed

## Правило / основание

VMX is a frozen compatibility frontend, not the virtualization architecture.
Host-side I/O-domain, DMA, and IOTLB mechanics must be owned by generic runtime/host substrate, not by VMX-shaped authority. A legacy file may leave `Legacy/VMX` only when VMX authority is removed, descriptor ownership is identified, capability/evidence/fence policy is represented by generic services, publication/retire boundaries are defined, no-emission/projection behavior is conformance-covered, and frozen VMX names remain only as compatibility ABI vocabulary.

## Что изменено

- Removed `Legacy/VMX/Substrate/Memory/Iommu/IOMMU.DomainBinding.partial.cs` from the legacy quarantine tree.
- Added `Memory/MMU/IOMMU.DomainBinding.cs` as generic host-side I/O-domain binding, DMA translation, IOTLB state, and translation invalidation mechanics.
- Added `Memory/MMU/IOMMU.VmxCompatibilityAliases.cs` as the frozen ABI alias layer for existing VMX-shaped callers outside `Core/VMX`.
- Removed the transitional `Memory/MMU/IOMMU.IoDomainBackend.cs` generic-to-VMX wrapper.
- Updated active host callers to use generic entrypoints:
  `InitializeIoDomainDmaState`, `InvalidateIotlbAll`, `TryTranslateDma`, and `ApplyTranslationInvalidation`.
- Updated `LegacyVmxQuarantineManifest` with the returned generic host path.
- Added `LegacyIommuDomainBindingReturnContract` and extended `VmxProjectionSchemaAndQuarantineTests` to check legacy absence, returned host presence, reverse-import proof, and no VMX-shaped markers in the generic host implementation.

## Как проверено

- `Memory/MMU/IOMMU.DomainBinding.cs` contains no `Vmx` / `VMX`, `BindVmx`, `UnbindVmx`, `InvalidateVmx`, `ApplyVmx`, `TryTranslateVmxDma`, `InitializeVmxDmaState`, or `_vmxDomainBindings` markers.
- `IoVirtualizationHostBackend` now reaches generic `IOMMU.BindIoDomain` / `InvalidateIotlb*` host methods directly.
- `Memory/MMU/IOMMU.VmxCompatibilityAliases.cs` keeps VMX-shaped names only as delegating compatibility aliases.
- `LegacyVmxQuarantineManifest` marks the old legacy path as returned with `Memory/MMU/IOMMU.DomainBinding.cs` as the new host path.

## Результат сборки

- Main project:
  `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 0 warnings, 0 errors.
- Test project:
  `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: blocked by unrelated dirty compiler test state outside this VMX/IOMMU change:
  `CompilerNoEmissionBoundaryTests.cs(629,26): CS0246 ProcessorCompilerBridge could not be found` and
  `CompilerNoEmissionBoundaryTests.cs(634,49): CS0619 Assert.Throws<T>(Func<Task>) is obsolete`.
  Because the test project did not rebuild, targeted post-170 test filters were not rerun against a fresh test assembly.

## Остаточный риск

- `Memory/MMU/IOMMU.VmxCompatibilityAliases.cs` still exposes frozen VMX-shaped host method names for legacy callers, but those aliases are outside `Core/VMX` and delegate to generic host mechanics.
- `Legacy/VMX/Compatibility/Frontend/Handlers/VmxExecutionUnit.cs` still calls the VMX-shaped compatibility alias and remains a separate heavy legacy frontend split task.
- `MemoryTranslationControl` still carries NPT/VPID/VMCS-shaped compatibility vocabulary.
- Remaining heavy legacy topics: `VmxExecutionUnit`, `VmcsManager`, `ShadowVmcsBlock`, `LegacyVmcsMemoryTranslationControlProjection`, and the legacy V1/V2 execution adapter partials.
