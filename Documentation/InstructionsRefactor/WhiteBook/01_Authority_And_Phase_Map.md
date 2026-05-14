# Authority And Phase Map

Updated: 2026-05-14.

## Архитектурный принцип

HybridCPU ISE после рефакторинга строится вокруг одного жесткого правила:
исполняемость инструкции доказывается только runtime-owned цепочкой. Наличие
opcode, enum value, metadata row, parser recognition, compiler helper или
typed-slot fact не создает ISA-поддержку.

Корректная цепочка для executable contour должна закрывать:

1. inventory and support status;
2. encoding ABI;
3. decoder projection;
4. IR and MicroOp materialization;
5. execution capture without early publication;
6. retire-owned publication;
7. rollback/replay behavior;
8. focused positive and negative tests;
9. compiler emission only when explicitly in scope.

Если хотя бы один runtime слой отсутствует, контур должен быть описан как
non-executable, descriptor-only, carrier-only, parser-only, optional-disabled
или reserved.

## Authority Hierarchy

| Layer | Authority role |
|---|---|
| Runtime legality | Финальное решение, допустима ли форма инструкции. |
| Decoder/materializer | Обязательный runtime gate, но не самостоятельное доказательство исполнения. |
| Execution/retire | Место, где возникает архитектурная правда регистров, памяти и PC. |
| Focused tests | Доказательство, что runtime chain работает и fail-closed paths закрыты. |
| Compiler facts | Scope input только для live-emitted forms, не runtime authority. |
| Metadata/typed-slot facts/docs | ValidationOnly или evidence, не authority. |

## Support Status Model

Фазовая документация закрепляет следующие категории:

| Status | Смысл |
|---|---|
| `Executable` | Полная runtime цепочка и тесты существуют для конкретного контура. |
| `OptionalEnabled` | Узкий optional contour доступен ровно в описанных рамках. |
| `DescriptorOnly` | Есть descriptor transport, но нет прямой execution authority. |
| `CarrierOnly` | Runtime carrier существует, backend execution/commit отключены. |
| `ParserOnly` | Parser/model awareness без runtime исполнения. |
| `OptionalDisabled` | Row существует, но decoder/runtime отвергают форму. |
| `Reserved` | Нет поддержанной runtime allocation или execution surface. |

Эти статусы являются не косметикой, а частью безопасности ISA: они не дают
документации или compiler metadata случайно открыть исполнение.

## Карта закрытых фаз

| Phase | State | Основной результат |
|---:|---|---|
| 00 | Closed discipline | Inventory freeze, запрет stale support claims и opcode reuse. |
| 01 | Closed | ISA surface нормализована по executable/non-executable категориям. |
| 02 | Closed | Scalar repair opcodes закреплены без renumbering. |
| 03 | Closed | Mandatory scalar integer64 runtime/compiler tail закрыт до `ZEXT.W`. |
| 04 | Closed | Encoding ABI закрыт для scalar, branch/control, atomics, FENCE/FENCE_I, descriptor carriers. |
| 05 | Closed | Decoder projection закрыт для audited contours и fail-closed rows. |
| 06 | Closed | Metadata синхронизирована как evidence, не authority. |
| 07 | Closed | MicroOp materialization закрыта для executable contours и fail-closed boundaries. |
| 08 | Closed | Execution/retire model закрыт для scalar, memory, branch, atomic, fence, replay. |
| 09 | Closed | Vector scoped contours и Lane6/Lane7 fail-closed contracts закрыты. |
| 10 | Closed | Atomic acquire/release и bounded `FENCE` / `FENCE_I` закрыты. |
| 11 | Deferred | Matrix остается optional-disabled / decoder-rejected. |
| 12 | Runtime-linked | Compiler facts вынесены в compiler plan; runtime не выводит truth из compiler. |
| 13 | Ongoing | Documentation cleanup остается дисциплиной сопровождения. |

## Неприкосновенные constraints

- Не перенумеровывать существующие opcodes.
- Не ломать VLIW instruction/bundle encoding.
- Не добавлять hidden compatibility fallback.
- Не открывать compiler lowering без runtime proof и явного scope.
- Не считать descriptor-only/carrier-only контуры executable ISA.
- Не продвигать metadata или typed-slot facts выше runtime-owned legality.
- Не расширять Phase 10 до cache/TLB/DMA/coherence claim без отдельной модели.

## Практический вывод

Для любого будущего изменения правильный вопрос не "есть ли opcode?", а
"закрыта ли runtime chain от ABI до retire и rollback/replay?". Если ответ
неполный, безопасное состояние: fail closed.

