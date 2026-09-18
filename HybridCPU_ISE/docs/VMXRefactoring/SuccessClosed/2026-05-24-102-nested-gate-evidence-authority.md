# Nested gate evidence authority

Дата: 2026-05-24

## Правило / основание

- `CompatibilityAlias` не должен выступать authoritative evidence.
- Gate proof должен опираться на runtime/evidence policy source.
- Основание: `ОСНОВЫ и ПРАВИЛА VMX.md`; audit.md, пункт 9.

## Что изменено

- `NestedEnablementGateDescriptor.IsAuthoritative` теперь принимает только `EvidenceVisibilityClass.GuestArchitecturalState`.
- `CreateGate(...)` получает evidence class из `NestedProofAuthoritySource`.
- Compatibility path без runtime validation остается fail-closed.
- `NestedProofAuthoritySourceContract` фиксирует запрет `CompatibilityAlias` как authority.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

## Результат сборки

- Build succeeded.
- 54 warnings, 0 errors.

