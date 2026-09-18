# Runtime epoch legacy API fail-closed

Дата: 2026-05-24

## Правило / основание

- `ОСНОВЫ и ПРАВИЛА VMX.md`: TLB/IOTLB epoch overflow должен иметь explicit fail-closed policy.
- `audit.md`, пункт 18: старый `AdvanceRuntimeEpoch()` всё ещё возвращал `1` при wraparound.

## Что изменено

- `AdvanceRuntimeEpoch()` при `ulong.MaxValue` больше не возвращает `1`.
- При wraparound он возвращает текущий epoch и выставляет `wrapped = true`.
- Добавлен `RuntimeEpochAdvanceFailClosedContract`.

## Как проверено

- Выполнено: `dotnet build "\HybridCPU ISE\HybridCPU_ISE\HybridCPU_ISE.csproj" --no-restore`

## Результат сборки

- `Build succeeded`
- `54 Warning(s)`
- `0 Error(s)`

