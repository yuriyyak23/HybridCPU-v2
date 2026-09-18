# Nested proof authority source

Дата: 2026-05-24

## Правило / основание

- Наличие compatibility mask bit не должно автоматически доказывать descriptor-backed и evidence-bound authority.
- Proof authority должна приходить из runtime/evidence policy source.
- Основание: `ОСНОВЫ и ПРАВИЛА VMX.md`; audit.md, пункт 8.

## Что изменено

- Введен `NestedProofAuthoritySource`.
- `NestedEnablementProof.FromCompatibilityMasks(...)` получил overload с явным authority source.
- Двухаргументный compatibility overload теперь fail-closed и не выдает authority из одного mask.
- `CreateGrant(...)` выставляет `DescriptorBacked` и `EvidencePolicyBound` только при `CanAuthorizeCapabilityProof`.
- Добавлен conformance contract `NestedProofAuthoritySourceContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`.

## Результат сборки

- Build succeeded.
- 54 warnings, 0 errors.

