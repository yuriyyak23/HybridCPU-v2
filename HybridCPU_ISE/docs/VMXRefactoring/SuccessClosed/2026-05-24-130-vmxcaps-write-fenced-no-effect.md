# VmxCaps compatibility write no-effect fence

Дата: 2026-05-24

## Правило / основание

- `VmxCaps` должен быть compatibility alias/projection поверх `CapabilityDescriptorSet`, а не источником истины.
- Запись в capability projection не должна становиться authority path.
- Compatibility no-effect допустим только как явно fenced compatibility режим.

## Что изменено

- `VmxCapsProjection` получил явный `compatibilityWriteFenceEnabled`.
- `EvaluateWrite()` теперь возвращает `CompatibilityNoEffect` только если publication policy разрешает no-effect и включен explicit compatibility write fence.
- Без fence запись fail-closed через `Rejected`.
- Актуализирован boundary test `VmxCapsProjectionBoundaryTests`.

## Как проверено

```text
dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore
dotnet test "\HybridCPU ISE\HybridCPU_ISE.Tests\HybridCPU_ISE.Tests.csproj" --no-restore --filter "FullyQualifiedName~VmxCapsProjectionBoundaryTests"
```

## Результат сборки

Build succeeded. 54 Warning(s), 0 Error(s).

Targeted tests passed: Failed: 0, Passed: 3, Skipped: 0, Total: 3.
