# Legacy adapter boundaries returned to Core compatibility frontend

Дата: 2026-05-24

Статус: closed

## Основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: Legacy допускается только на frozen ABI / compatibility frontend boundary.
- `audit.md`: legacy quarantine может возвращать файлы в `Core/VMX` только после доказательства descriptor owner, capability policy, evidence policy, retire/publication boundary, projection tests and no authoritative VMX state.
- `LegacyVmxQuarantineManifest`: legacy adapter boundary can return only as frozen ABI adapter with descriptor owner and projection tests.

## Что изменено

- Перенесены из `Legacy/VMX` в `Core/VMX/Compatibility/Adapters`:
  - `Core/VMX/Compatibility/Adapters/LegacyVmxV1/LegacyVmxV1AdapterBoundary.cs`
  - `Core/VMX/Compatibility/Adapters/LegacyVmxV2/LegacyVmxV2AdapterBoundary.cs`
- Добавлены `LegacyOriginPath`, `CoreReturnPath` and `RequiredCoreReturnProof` для каждого adapter boundary.
- `LegacyVmxQuarantineManifest` теперь различает:
  - files that must remain quarantined;
  - files already returned to `Core/VMX` with proof.
- `VmxProjectionSchemaAndQuarantineTests` проверяет, что returned adapter files:
  - physically absent from `Legacy/VMX`;
  - present in `Core/VMX`;
  - pass reverse-import proof;
  - stay frozen-ABI / compatibility-frontend / projection-only / fail-closed.

## Проверка

- `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`
- `dotnet build "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~VmxProjectionSchemaAndQuarantineTests"`
- `dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-build --filter "FullyQualifiedName~CoreVmxAuthorityBoundaryTests"`

## Результат

- Основной проект: succeeded, 0 errors.
- Тестовый проект: succeeded, 0 errors.
- `VmxProjectionSchemaAndQuarantineTests`: Passed 6/6.
- `CoreVmxAuthorityBoundaryTests`: Passed 1/1.

## Остаточный риск

Это возвращает только safe adapter boundary contracts. Legacy execution partials, `VmxExecutionUnit`, `VmcsManager`, CSR fallback, Shadow VMCS block and VMX-shaped IOMMU/translation backends remain quarantined.
