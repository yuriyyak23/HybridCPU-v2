# Продолжение VirtualizationActivationPlan — PR-A

## Роль

Ты — ведущий архитектор CPU/ISA/compiler/runtime и аудитор виртуализации HybridCPU-v2. VMX/VMCS для тебя — замороженный compatibility ABI и словарь проекции, но не источник полномочий. Полномочия принадлежат только нейтральным runtime owners и существующим общим domain/capability/admission/completion/retire контурам.

## Вводные

- Работай только локально в `C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE`. Не выполняй fetch/pull/push и не подменяй локальные файлы внешними версиями.
- Код ISE: `HybridCPU_ISE`; каноническое дерево — только `CloseToHSL`. `CloseToRTL` устарел.
- Диагностический стенд: `VirtualizationDiagnosticsConsole`. Он является testing/model evidence и не доказывает production execution или activation.
- Перед изменениями проверь текущие HEAD/status, рабочее дерево, вызывающие связи, production pipeline и тесты. Сохраняй пользовательские изменения и не создавай commit/stage без прямого запроса.
- Обязательно изучи ключевые WhiteBook-разделы: `02_Principles_And_Non_Goals.md`, `04_Authority_Model.md`, `05_Runtime_Domain_Owners.md`, `06_Capabilities_And_Evidence.md`, `09_VMX_Compatibility_Frontend.md`, `11_Admission_Boundaries.md`, `12_Trap_Intercept_Completion_Retire.md`, `15_Security_Invariants.md`, `16_Current_State_And_Closure_Matrix.md`, `17_Roadmap_And_Residual_Risk.md` и `19_Source_References_And_Check_Commands.md` в `Documentation\Documentation\Virtualization WhiteBook`.
- Нормативно сопоставь `VirtualizationActivationPlan`: фазы `00`, `01`, `02`, `03`, `06`–`09`, `15`–`19`, `34`, `35` и прежде всего `38_first_production_vmcall_slice_repository_owner_adr.md`. При расхождении сначала проверь код, затем синхронизируй документы.
- Phase 38 уже фиксирует exact architecture/policy profile: `D2-HV-VMCALL-RUNTIME-V1-PROBE-0001`, namespace `HybridCPU.VMCALL.Runtime.v1`, leaf `0x0001`, operation `PROBE_NO_STATE_V1`, owner allocation `HCOWNR`, capability bit 41, no-state/no-payload, execution-only domain, SecureCompute deny, exact cancellation/replay/completion/retire/migration policies. Эти значения ещё не являются machine D2 или runtime authority.
- Production VMX остаётся fault-only. P1/P2 остаются `TESTING`-only; P3 не разрешён.

## Задача

Закрой только PR-A — D2 v2 governance/negative substrate. Работай подряд, итеративно по пунктам ниже. Для каждой итерации сначала перепроверь фактическую основу, затем внеси минимальное изменение, выполни focused-тесты и запретительные scans, при необходимости синхронизируй `VirtualizationDiagnosticsConsole` и документацию, после чего переходи к следующему пункту. Не расширяй scope при зелёных тестах.

1. Введи отдельные immutable-типы `VirtualizationDecisionSpecV2` и `VirtualizationDecisionAcceptanceRecordV2` со всеми обязательными полями Phase 38. AcceptanceRecord ссылается на уже существующие exact bytes spec через `SpecCommitSha` и `SpecDigest`; он не содержит SHA собственного будущего containing commit. Revocation/supersession оформляются новыми immutable records.
2. Реализуй `VirtualizationDecisionCanonicalEncoderV2` с фиксированными field order, типами, endian/length representation и versioning. Вычисляй SHA-256 только от canonical bytes без соответствующего digest-поля. Raw serializer JSON, whitespace и property ordering не могут определять digest.
3. Реализуй fail-closed `VirtualizationDecisionValidatorV2`. Он проверяет canonical bytes/digests, SHA и exact spec bytes, DecisionId, owner/class/policy, namespace/width/leaf uniqueness и cross-namespace rules, ABI, полный policy/owner map, acceptance state, review roles/CODEOWNERS evidence, revocation/supersession и adjacent-leaf deny. Положительный результат — `AcceptedVirtualizationDecision`, который является immutable policy object, а не capability.
4. Реализуй role-separated review/CODEOWNERS gates. `CompatibilityFrontend` не может удовлетворить `OwnerReviewRole` или `ArchitectureReviewRole`; разные логические роли не требуют искусственно разных людей. Если добавляется repository-level `CODEOWNERS`, используй только scopes и principal, уже принятые Phase 38 (`@yuriyyak23`), но не фабрикуй completed review evidence и не объявляй D2 принятым.
5. Закрой полную отрицательную матрицу: wrong digest/SHA/DecisionId, malformed и self-referential SHA, noncanonical bytes, zero/wrong owner, zero/duplicate/adjacent/cross-namespace leaf, missing/unknown policy, incomplete owner map, missing CODEOWNERS, reviewer-role mismatch, compatibility-only review, draft/revoked/superseded record и invalid lineage. Positive structural fixture допустима только как validator test; она не должна попадать в production registry или становиться activation proof.
6. Добавь или обнови в `VirtualizationDiagnosticsConsole` отдельный PR-A diagnostic scenario только если он исполняет реальные новые schema/encoder/validator contracts. Сценарий обязан явно маркироваться как governance/negative evidence, выдавать структурированные counters/trace и не создавать owner registry, O1, E2, executor, completion или retire. Сохрани существующие `vmcall-denied` и `e1-fault-transport` инварианты.
7. Синхронизируй Phase 16/17/19/34/38, статические plan guards, README диагностики и evidence aggregate. Зафиксируй разницу между: architecture/policy values decided; machine D2 structurally validated; attributable accepted instance; runtime authority. Закрытая PR-A не открывает PR-B автоматически.

Запрещено в PR-A:

- создавать populated production AcceptanceRecord или generated accepted-operation registry;
- материализовать production owner/capability registry, O1 или operand snapshot;
- выдавать positive E2, добавлять executor/E3, `InvokeHypercall`, backend allow или `BackendExecutionAuthorized: true`;
- подключать compatibility frontend к production dispatcher, completion или retire;
- создавать `CompletionRecord` в frontend/admission либо добавлять E5/E6/E7;
- трактовать schema, validator success, CODEOWNERS, review workflow, тест, diagnostics, manifest или ADR как execution authority;
- расширять VMREAD/GuestCr0/GuestCr4, включать VMWRITE, nested, SecureCompute, memory/IOMMU/device/lane/stream/compiler или broad virtualization activation.

Обязательная проверка после каждой существенной итерации и итогом:

- focused D2/plan/diagnostics tests;
- весь `FullyQualifiedName~HybridCPU_ISE.Tests.VmxRefactoring`;
- build без `TESTING`/internal test hooks в конфигурации, допускаемой проектными guards;
- `VirtualizationDiagnosticsConsole`: новые PR-A проверки плюс `vmcall-denied` и `e1-fault-transport` на разумном числе итераций;
- `git diff --check` и scans на exact-profile production registry, accepted instance, executor/`InvokeHypercall`, allowed backend, `BackendExecutionAuthorized: true`, frontend completion и positive compatibility-retire shortcuts.

В финале выдай: закрытый scope PR-A; вердикты по каждому пункту; изменённые файлы; тесты и diagnostics; доказанные, недоказанные и blocked факты; подтверждение отсутствия activation/backend/completion/retire shortcuts; актуальные блокеры и только следующий допустимый пул. Не создавай commit/stage без прямого запроса.
