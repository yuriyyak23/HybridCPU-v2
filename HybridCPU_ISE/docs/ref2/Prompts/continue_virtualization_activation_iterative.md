# Продолжение VirtualizationActivationPlan

## Роль

Ты — ведущий архитектор CPU/ISA/compiler/runtime и аудитор виртуализации HybridCPU-v2. VMX/VMCS остаются замороженным compatibility ABI и словарём проекции; полномочия принадлежат только нейтральным runtime owners, независимым от compatibility plane.

## Вводные

- Работай только локально в `C:\Users\Yuriy Kurnosov\Desktop\HybridCPU ISE`; не выполняй fetch/pull/push и не используй удалённый репозиторий как источник файлов.
- Код ISE: `HybridCPU_ISE`; канонический контур — только `CloseToHSL`, `CloseToRTL` устарел.
- Нормативные документы: `docs/ref2/VirtualizationActivationPlan/17_phase_rollout_and_pr_order.md`, `19_open_decision_backlog.md`, `34_d2_schema_attribution_and_e2_negative_substrate.md`, `37_testing_only_canonical_issue_materialization_composition.md`, `38_first_production_vmcall_slice_repository_owner_adr.md`.
- Аудит-рекомендация: `docs/ref2/1/VRT/HybridCPU-v2 Virtualization Activation 3.md`; каждое утверждение заново проверяй по текущему HEAD/worktree, callers, production pipeline, тестам, WhiteBook и `Intenal Docs/ISE`.
- P1 и P2 закрыты только как `TESTING`-only model evidence. Production VMX остаётся fault-only. P3 не разрешён.
- Phase 38 принимает архитектурную роль `DomainHypercallRuntimeOwner` и exact ABI `HybridCPU.VMCALL.Runtime.v1` / 16-bit / `0x0001` / `PROBE_NO_STATE_V1` / `Rs1` full value / `Rs2=x0` / `Rd=x0` / no-state/no-payload / `DrainOnly`. Машинный D2 не материализован: SpecV2/AcceptanceRecordV2, stable non-zero OwnerId, reviewer/CODEOWNERS mapping и validated accepted instance отсутствуют. Production O1/operand/E2-E9 заблокированы.

## Задача

Работай строго по одному допустимому пулу. Перед изменениями проверь HEAD/status, сохрани пользовательские изменения и актуализируй фактические вызывающие связи.

Следующий допустимый пул — только D2 v2 governance/negative substrate:

1. Подготовь раздельные схемы `VirtualizationDecisionSpecV2` и `VirtualizationDecisionAcceptanceRecordV2` и fail-closed validator. Spec должен быть immutable и иметь детерминированный digest; AcceptanceRecord должна ссылаться на SHA+digest spec и reviewer/CODEOWNERS evidence, не пытаясь содержать SHA собственного будущего commit.
2. Кодируй только принятый Phase 38 exact vocabulary; не добавляй populated accepted record, runtime OwnerId, CODEOWNERS appointment, O1, E2 issuer или executor и не объявляй машинный D2 закрытым.
3. Добавь только отрицательные проверки: malformed/missing spec SHA, digest mismatch, reviewer mismatch, compatibility self-approval, absent/duplicate leaf, incomplete owner map, invalid withdrawal/replacement lineage, unknown schema version. Структурная валидность не является runtime authority.
4. Зафиксируй, что `VmxInstructionPayload.FromDecodedRegisters` и `VmxExitQualification` содержат `rs1`/`rs2` selectors, а не runtime operand values. После machine D2 O1 означает immutable owner-policy snapshot; отдельный `VirtualizationOperandSnapshot` один раз читает full `Rs1` value и проверяет high bits zero, `Rs2=x0`, `Rd=x0`. Текущий пул их не реализует.
5. Не выводи ABI из frozen compatibility `ushort`: 16-bit/`0x0001` уже приняты Phase 38 как namespace-scoped ADR, но становятся machine/runtime input только через validated SpecV2/AcceptanceRecordV2. VMFUNC leaf 1 остаётся отдельным namespace.
6. Сохрани раздельные полномочия: E3 opaque execution receipt → consume-once E5 completion token → consume-once canonical E6 retire grant. `CompletionPublicationAllowed`, route/fence DTO, `CompletionRecord` factory и `VmxRetireEffect` — только данные/compatibility vocabulary.
7. Обнови `VirtualizationDiagnosticsConsole` только если пул добавляет исполняемый отрицательный validator contract; diagnostics остаётся testing evidence и не подключается к production VMX.
8. Обнови Phase 17/19/33/34 и статические guard-тесты. Не создавай новую положительную research-фазу.

Обязательные проверки: focused D2/plan guards, полный `FullyQualifiedName~VmxRefactoring`, Release без TESTING, `git diff --check`, а также scans на numeric leaf, allowed backend, `BackendExecutionAuthorized: true`, production callers research-типа, `CompletionRecord` во frontend/admission и completion/retire shortcuts.

Запрещено назначать runtime OwnerId/reviewer вместо governance, создавать populated accepted record, повторно выбирать или расширять leaf/namespace, добавлять production O1/operand/E2/backend/executor/Allowed, подключать compatibility frontend к execution/completion/retire, создавать `CompletionRecord` во frontend/admission, трактовать ADR/projection/admission/P1/P2/DTO/тесты как исполнение, расширять GuestCr0/GuestCr4, активировать VMWRITE/nested/SecureCompute/lane/stream/compiler или объявлять broad virtualization activation.

По завершении выдай: закрытый scope, изменённые файлы, доказанные и недоказанные факты, тесты/scans, подтверждение отсутствия shortcuts, актуальные блокеры и следующий допустимый пул. Не создавай commit/stage без прямого запроса.
