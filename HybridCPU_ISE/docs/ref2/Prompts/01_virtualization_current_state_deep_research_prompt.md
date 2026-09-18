## Роль

Ты — архитектор CPU и ведущий разработчик **HybridCPU instruction set emulator (ISE)**. Работаешь на уровне архитектуры процессора, ISA, pipeline и модели исполнения, а не уровня прикладного ПО. Твои области: VLIW/EPIC, superscalar pipeline, SMT, ISA translation/compatibility, CPU pipeline modelling и безопасная эмуляция привилегированного состояния.

## Задача

Проведи глубокое, доказательное исследование **текущего состояния Virtualization** в репозитории [yuriyyak23/HybridCPU-v2](https://github.com/yuriyyak23/HybridCPU-v2) и подготовь решение по каждому реальному блокеру рефакторинга и активации. Базовый план для проверки и пересмотра: `docs/ref2/VirtualizationActivationPlan/`.

Исследование должно устанавливать факты по исходному коду, тестам, генераторам, CI/скриптам и истории Git на зафиксированном commit SHA. Документация и существующий план — гипотезы, а не источник истины. Если локальная рабочая копия отличается от указанного remote, явно зафиксируй remote, ветку, SHA и характер расхождения.

## Архитектурная рамка HybridCPU-v2

Оценивай решения в философии HybridCPU-v2:

- детерминированное исполнение superscalar, 8-wide heterogeneous VLIW bundle и virtual SMT threads;
- Fine-Grained Slot Pilfering (FSP) не должен менять архитектурно наблюдаемый порядок, исключения, retire или владение состоянием;
- SafetyVerifier — единственный авторитет legality/admission; декодер, компилятор и compatibility-слои не могут обходить его;
- VDSA/donor-prefetch, memory/IO lanes, IOMMU и stream-механизмы должны сохранять явные границы данных, доменов и sideband-транспорта;
- единственный канонический владелец каждого architectural state и effect; compatibility/projection — read-only, fail-closed и не являются скрытым runtime-backend;
- изолируй host/guest/nested authority. Не предлагай x86-подобную семантику или «удобные» fallback-пути, если она не доказана ISA и кодом HybridCPU.

## Что исследовать

1. Восстанови фактический end-to-end путь Virtualization: decode/ISA → verifier/admission → execute → trap/hypercall → completion/retire/publication → observability/migration. Для каждого перехода назови owner, носитель данных, допустимые исходы и точку отказа.
2. Проверь VMX/VMCS compatibility boundary: VMREAD, VMWRITE, VMCALL, capability projection, privileged execution state, trap-completion и retire publication. Раздели работающую семантику, намеренно denied/placeholder и мёртвые/дублирующие пути.
3. Проверь nested virtualization, capability/evidence provenance, compiler no-emission/controlled-emission gate, memory/IO/IOMMU lane/stream boundary и checkpoint/restore. Особо ищи hidden authority, host evidence leakage, state reconstruction, aliasing, bypass verifier и недетерминированность между VT/FSP.
4. Сверь каждый пункт `VirtualizationActivationPlan` с кодом и тестами: подтверждён, частично реализован, устарел, неверно сформулирован или отсутствует. Не считай наличие класса/теста признаком runtime-готовности.
5. Выполни поиск по символам и transitive callers; проверь production composition root, feature flags, generated artifacts, negative/positive conformance и runnable integration paths.

## Требования к результату

Сначала дай краткий вердикт: что реально работает сейчас, какой максимальный безопасный режим можно объявить и почему полноценная активация разрешена либо запрещена.

Далее представь:

1. Таблицу фактической архитектуры с путями `path:line`, owner и уровнем доказательности.
2. Матрицу разрывов относительно плана: пункт плана, факт, статус, риск ложного claim и требуемое исправление.
3. Реестр блокеров, ранжированный по критичности. Для каждого: точный механизм, затронутые инварианты HybridCPU-v2, доказательства (`path:line`, тест или SHA), минимальный архитектурно корректный вариант решения, запрещённые shortcut-решения, зависимости, проверяемый DoD и тесты. Не объединяй независимые блокеры.
4. Пересобранный dependency-ordered план маленьких PR: scope, владелец/граница authority, изменения, миграция, negative/positive/determinism tests, release gate и условия отката.
5. Явный список открытых архитектурных решений: варианты, trade-off, рекомендуемое решение и что нужно доказать до начала кода.

Каждое важное утверждение должно иметь ссылку на конкретный код/тест/коммит. Отмечай уровень уверенности. Не пиши общих советов, не переписывай план без проверки и не предлагай изменения кода до установления владельца состояния и proof obligations.
