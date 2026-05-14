# HybridCPU ISE Instructions Refactor WhiteBook

Updated: 2026-05-14.

## Назначение

Этот WhiteBook является расширенным сводным описанием завершенного состояния
рефакторинга инструкций HybridCPU ISE по актуализированной документации из:

```text
C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE\Documentation\InstructionsRefactor
```

Он не заменяет фазовые файлы, runtime-код или тесты. Его задача: дать связную
архитектурную картину того, что уже закрыто, какие границы считаются
исполняемыми, какие контуры намеренно остаются неисполняемыми, и какие правила
нужно сохранить при продолжении работ.

## Источник истины

WhiteBook является производной документацией. Если он расходится с кодом или
runtime-owned тестами, корректным источником считается код плюс тесты. Фазовые
документы, metadata, compiler facts, typed-slot facts, enum/opcode rows и parser
acceptance не являются доказательством корректности выполнения.

Приоритет authority:

1. Runtime-owned legality, decode, materialization, execution, retire, replay.
2. Focused runtime-owned conformance tests.
3. Audit and phase documentation.
4. Compiler facts only when live compiler emission is explicitly in scope.
5. Metadata, typed-slot facts, registry rows and docs as evidence only.

## Состав WhiteBook

| File | Содержание |
|---|---|
| `00_README.md` | Индекс, назначение, чтение, статус закрытия. |
| `01_Authority_And_Phase_Map.md` | Архитектурные правила authority, support-status model, карта фаз. |
| `02_Runtime_Surface_Closure.md` | Закрытая runtime ISA-поверхность: scalar, branch, load/store, atomic, fence, vector, Lane6/Lane7. |
| `03_ABI_Decode_MicroOp_Retire_Contract.md` | Сквозной контракт от encoding ABI до retire и rollback/replay. |
| `04_Memory_Atomic_Fence_Model.md` | Phase 08/10 memory model: LR/SC, AMO W/D, aq/rl, FENCE, FENCE_I. |
| `05_NonExecutable_And_Future_Gates.md` | DescriptorOnly, CarrierOnly, ParserOnly, OptionalDisabled, Reserved и future enablement gates. |
| `06_Verification_And_Risk_Closure.md` | Снимок проверок, команды, TestAssemblerConsoleApps risk closure, expected drift. |

## Executive Summary

Текущий рефакторинг ISE закрывает обязательную runtime-поверхность до Phase 10
включительно для опубликованного ISA-контура:

- scalar integer64 repair закрыт до `ZEXT.W = 321`;
- compiler scalar tail для этого контура также подтвержден до `ZEXT.W = 321`;
- branch/control target transport нормализован через `Immediate`;
- scalar load/store имеют typed access semantics и retire-owned store commit;
- LR/SC и AMO W/D имеют retire-time semantics;
- atomic acquire/release ordering для опубликованных carriers закрыт runtime
  доказательствами, а не metadata/decoder facts;
- canonical zero-payload `FENCE` и `FENCE_I` закрыты как bounded lane7/system
  retire events;
- replay/rollback и SMT/VT ownership проверены для опубликованных путей;
- vector, Lane6, Lane7, matrix, cache/TLB/coherency non-executable contours
  имеют явные fail-closed или reserved границы.

## Главная граница Phase 10

Phase 10 закрыт только для текущего bounded runtime model. Это важно читать
буквально:

- `FENCE` не является общей cache/DMA/TLB/coherence теоремой;
- `FENCE_I` не является универсальным instruction-cache coherence механизмом;
- `SFENCE.VMA`, `DCACHE_CLEAN`, `DCACHE_INVAL`, `DCACHE_FLUSH`,
  `ICACHE_INVAL` остаются reserved или optional-disabled;
- compiler atomic/fence/cache/TLB lowering не открывается автоматически;
- существующий compiler coordinator barrier соответствует только canonical
  zero-payload `FENCE`.

## Current Open Entry Point

Обязательных открытых Phase 10 задач для текущей runtime ISA-поверхности не
осталось. Следующая работа должна начинаться только с явно выбранного
future-gated contour:

- full cache/TLB/coherence model;
- executable `VGATHER` / `VSCATTER`;
- production Lane6 execution;
- production Lane7 execution;
- future dot-product ABI variants;
- compiler emission for a proven runtime contour.

## Правило чтения

Этот WhiteBook полезен как архитектурный обзор перед внесением изменений. Перед
любой реализацией все равно нужно перечитать:

- `Documentation\InstructionsRefactor\00_README.md`;
- `Documentation\InstructionsRefactor\UNFINISHED_TASKS_AUDIT_2026-05-08.md`;
- фазовый файл выбранного контура;
- runtime code and focused tests для выбранного контура.

