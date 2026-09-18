# 172 Legacy Shadow VMCS Block Removed Without Replacement

Дата: 2026-05-24

Статус: closed

## Правило / основание

VMX is a frozen compatibility frontend, not the virtualization architecture. Shadow VMCS/VMCS12/VMCS02 vocabulary is allowed as compatibility/conformance vocabulary, but the runtime state block must not own nested-domain authority, migration/checkpoint authority, evidence policy, or nested composition legality. Under removal-without-replacement, the old block is deleted and compatibility paths fail closed until a generic nested-domain projection/checkpoint service exists.

## Что изменено

- Removed `Legacy/VMX/Compatibility/Generated/VmcsProjection/ShadowVmcsBlock.cs`.
- Updated `ShadowVmcsNestedProjectionService` to fail closed instead of calling `VmcsV2Descriptor.ShadowVmcs`.
- Removed active `.ShadowVmcs.` dereferences from `VmcsV2Descriptor`, quarantined `VmcsManager`, and `VmxCheckpointImage`.
- Nested VMREAD/VMWRITE and Shadow VMCS checkpoint restore now require a future generic nested-domain projection/checkpoint service.
- Tightened `CoreVmxAuthorityBoundaryContract` so `.ShadowVmcs.` has no allowed Core path.
- Extended `LegacyVmxQuarantineManifest` with removed-without-replacement disposition.
- Added `LegacyShadowVmcsBlockRemovalContract` and static conformance coverage.

## Как проверено

- Legacy path is absent from `Legacy/VMX`.
- No Core return path was introduced.
- `ShadowVmcsNestedProjectionService` has no `.ShadowVmcs.`, `IShadowVmcsCompatibilityBridge`, or `ShadowVmcsCompatibilityBridge` markers.
- `VmcsV2Descriptor`, `VmcsManager`, and `VmxCheckpointImage` no longer dereference `.ShadowVmcs.`.
- Manifest marks the legacy Shadow VMCS block as removed without replacement.
- General Core VMX authority conformance now treats `.ShadowVmcs.` as forbidden without exceptions.

## Результат сборки

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 54 existing warnings, 0 errors.
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
  Result: succeeded, 93 existing warnings, 0 errors.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
  Result: passed 12/12.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`
  Result: passed 1/1.
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~RemovedLegacyShadowVmcsBlock"`
  Result: passed 1/1.
- Final `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
  Result: succeeded, 0 warnings, 0 errors.

## Остаточный риск

- Nested VMX compatibility is intentionally fail-closed until a generic nested-domain projection/checkpoint service is implemented.
- Historical ShadowVmcs-named conformance contracts remain as compatibility vocabulary and should be retired or renamed once the generic nested service exists.
- Remaining heavy legacy topics: `VmxExecutionUnit`, `VmcsManager`, and the legacy V1/V2 execution adapter partials.
