# HybridCPU Compiler Core Refactor Plan

Статус: planning-only.

Этот каталог фиксирует пофазовый план рефакторинга `HybridCPU_Compiler/Core` без немедленного изменения исполняемого кода. План исходит из текущей философии HybridCPU-v2:

- compiler не является источником runtime authority;
- `carrier != execution != publication != authority != commit != retire != evidence != production lowering`;
- двухстадийный допуск Legality A|B остается архитектурной осью;
- sideband, descriptor, token, certificate и typed-slot facts являются переносимыми данными/доказательствами, но не правом исполнения;
- VMX и SecureCompute не должны превращаться в backend source of truth;
- отсутствие production lowering должно быть выражено явно, а не скрыто за helper/parser success.

## Почему план расположен под Core

Текущий `Core` уже разделен на `IR` и `Support`, а `IR` содержит самостоятельные зоны для admission, analysis, bundling, construction, decomposition, hazards, model, scheduling и telemetry. Поэтому рефакторинг предлагается проводить поверх существующего разбиения: сначала документировать границы полномочий, затем вводить API-типы, затем переносить реализации за новые контракты.

## Фазы

1. [`01_phase_inventory_and_freeze.md`](01_phase_inventory_and_freeze.md) — инвентаризация текущего поведения и заморозка публичных смыслов.
2. [`02_phase_authority_taxonomy.md`](02_phase_authority_taxonomy.md) — единая таксономия полномочий, доказательств и запретов.
3. [`03_phase_ir_intent_and_contours.md`](03_phase_ir_intent_and_contours.md) — классификация IR intent и execution contours.
4. [`04_phase_lowering_decision_api.md`](04_phase_lowering_decision_api.md) — API решений lowering без скрытого fallback.
5. [`05_phase_carrier_sideband_descriptor_abi.md`](05_phase_carrier_sideband_descriptor_abi.md) — carrier, sideband и descriptor ABI как разные продукты компиляции.
6. [`06_phase_typed_slot_and_legality_bridge.md`](06_phase_typed_slot_and_legality_bridge.md) — typed-slot facts и bridge к runtime legality.
7. [`07_phase_contour_providers.md`](07_phase_contour_providers.md) — контурные lowering providers: scalar, stream, MatrixTile, DSC, L7-SDC, VMX, SecureCompute.
8. [`08_phase_evidence_and_telemetry.md`](08_phase_evidence_and_telemetry.md) — evidence envelope, telemetry и auditability.
9. [`09_phase_tests_migration_and_exit.md`](09_phase_tests_migration_and_exit.md) — отрицательные тесты, миграция и критерии завершения.
10. [`10_architectural_audit_addendum.md`](10_architectural_audit_addendum.md) — audit hardening addendum: текущие Core-соответствия, обязательные усиления API, revised phase structure и расширенная negative matrix.

## Глобальная цель

После завершения плана `Core` должен предоставлять не набор удобных helpers, а архитектурно строгий слой handoff:

```text
source/IR intent
  -> semantic classification
  -> contour selection
  -> lowering decision
  -> carrier/sideband/descriptor/facts package
  -> runtime bridge acceptance
  -> runtime-owned LegalityDecision
```

Ключевая граница: compiler может создать переносимый пакет, но не может объявить его исполненным, опубликованным, committed, retired или production-lowered.

## Глобальные запреты

- Не добавлять универсальный fallback между DSC, L7-SDC, Stream, MatrixTile и scalar VLIW.
- Не трактовать descriptor parser success как право исполнения.
- Не трактовать helper success как production lowering.
- Не трактовать typed-slot facts как runtime legality.
- Не превращать VMX vocabulary в state owner.
- Не превращать SecureCompute admission/policy в secure backend execution.
- Не добавлять descriptorless submit для L7-SDC.

## Рекомендуемая форма изменений

Каждая фаза должна завершаться одним PR или одним логически изолированным набором коммитов. До фазы 4 допускаются в основном типы, обертки и документация. Исполняемое поведение должно меняться только после появления отрицательных тестов из фазы 9.

## Audit hardening note

`10_architectural_audit_addendum.md` является обязательным дополнением к плану: он фиксирует найденные в текущем `master` legacy ambiguity surfaces, уточняет границы `carrier/sideband/descriptor/facts/evidence/bridge`, требует ранние negative gates до поведенческой миграции и вводит machine-checkable exit checklist.
