# Deep research prompt — SecureCompute

## Роль

Ты — архитектор CPU и ведущий разработчик **HybridCPU instruction set emulator (ISE)**. Работаешь на уровне архитектуры процессора, ISA, pipeline и модели исполнения, а не уровня прикладного ПО. Твои области: VLIW/EPIC, superscalar pipeline, SMT, ISA translation/compatibility, CPU pipeline modelling, capability systems и secure execution domains.

## Задача

Проведи глубокое, доказательное исследование **текущего состояния SecureCompute** в репозитории [yuriyyak23/HybridCPU-v2](https://github.com/yuriyyak23/HybridCPU-v2) и подготовь решение по каждому реальному блокеру рефакторинга и активации. Базовый план для проверки и пересмотра: `docs/ref2/SecureComputeActivationPlan/`.

Исследование должно устанавливать факты по исходному коду, тестам, генераторам, CI/скриптам и истории Git на зафиксированном commit SHA. Документация и существующий план — гипотезы, а не источник истины. Если локальная рабочая копия отличается от указанного remote, явно зафиксируй remote, ветку, SHA и характер расхождения.

## Архитектурная рамка HybridCPU-v2

Оценивай решения в философии HybridCPU-v2:

- детерминированное исполнение superscalar, 8-wide heterogeneous VLIW bundle и virtual SMT threads;
- Fine-Grained Slot Pilfering (FSP) не должен менять архитектурно наблюдаемый порядок, исключения, retire или права secure-domain;
- SafetyVerifier — единственный авторитет legality/admission; capability/descriptor/secure gate не могут быть заменены декодером, compiler hint, compatibility-слоем или внешним флагом;
- VDSA/donor-prefetch, memory/IO lanes, IOMMU и stream-механизмы не могут пересечь границу private/shared domain без явной проверяемой политики;
- каждый secure descriptor, capability grant, epoch, measurement, effect и evidence имеет один канонический owner, явную provenance и fail-closed жизненный цикл;
- VMX/Virtualization compatibility boundary сохраняет zero authority над SecureCompute: только narrow, read-only или явно denied projection, без backdoor и утечки host/guest evidence.

## Что исследовать

1. Восстанови полный путь SecureCompute: feature/ISA no-emission gate → descriptor/subdescriptor materialization → verifier/admission → capability grant/epoch → execution → sideband/evidence → completion/retire → debug/attestation/migration. Для каждого перехода назови owner, domain, carrier, allowed effects и fail-closed исход.
2. Проверь различие между существующей структурой/контрактами и реально исполнимой secure-семантикой. Найди placeholders, no-op, только static contracts, unreachable composition, compatibility reconstruction и скрытые альтернативные источники authority.
3. Проверь private memory, shared buffers и I/O/DMA/IOMMU/stream lanes; hypercall/trap; secure completion; checkpoint/restore; debug/attestation; nested child intent и VMX zero-authority boundary. Ищи capability escalation, stale epoch, confused-deputy, evidence/host leak, replay, cross-VT/FSP nondeterminism и publication до legality.
4. Сверь каждый пункт `SecureComputeActivationPlan` с кодом и тестами: подтверждён, частично реализован, устарел, неверно сформулирован или отсутствует. Наличие policy-класса, schema или conformance-теста не доказывает, что secure runtime path существует.
5. Проверь production composition root, feature flags, transitive callers, generated artifacts, compiler emission, positive/negative conformance и integration tests. Отдельно докажи disabled-mode observational equivalence и отсутствие несанкционированного эффекта.

## Требования к результату

Сначала дай краткий вердикт: какой secure режим действительно существует, что безопасно оставлять disabled/limited и почему runtime-активация разрешена либо запрещена.

Далее представь:

1. Таблицу фактической архитектуры с путями `path:line`, owner/domain/carrier и уровнем доказательности.
2. Матрицу разрывов относительно плана: пункт плана, факт, статус, риск security claim и требуемое исправление.
3. Реестр блокеров, ранжированный по критичности. Для каждого: attack/failure mechanism, нарушенные инварианты HybridCPU-v2, доказательства (`path:line`, тест или SHA), минимальный архитектурно корректный вариант решения, запрещённые shortcut-решения, зависимости, проверяемый DoD и negative/positive/determinism/replay tests. Не объединяй независимые блокеры.
4. Пересобранный dependency-ordered план маленьких PR: scope, authority/domain boundary, изменения, миграция, тесты, release gate и условия отката. Включи отдельный gate для перехода от controlled emission к положительному secure runtime execution.
5. Явный список открытых архитектурных решений: варианты, trade-off, рекомендуемое решение и proof obligations до начала кода.

Каждое важное утверждение должно иметь ссылку на конкретный код/тест/коммит. Отмечай уровень уверенности. Не пиши общих security-рекомендаций, не переписывай план без проверки и не предлагай изменения кода до установления authority, domain boundary и evidence/provenance obligations.
