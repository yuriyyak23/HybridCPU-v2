# Phase 09 — Tests, Migration and Exit Criteria

## Назначение

Собрать обязательную отрицательную матрицу тестов, порядок миграции и критерии, по которым рефакторинг `HybridCPU_Compiler/Core` можно считать завершенным без нарушения модели HybridCPU.

## Strategy

Рефакторинг должен идти в режиме compatibility-first:

```text
observe -> wrap -> type decisions -> add negative tests -> migrate callers -> remove legacy ambiguity
```

Не допускается режим:

```text
rewrite -> hope behavior is equivalent
```

## Migration phases

### Step 1 — Documentation and inventory

- Добавить `CURRENT_BEHAVIOR.md`.
- Заполнить таблицу API-точек.
- Пометить ambiguous methods как legacy.

### Step 2 — Authority taxonomy types

- Добавить enum/record из фазы 2.
- Не менять runtime behavior.
- Добавить compile-only tests на недопустимые названия/claims.

### Step 3 — Intent and contour classifier

- Ввести classifier рядом с текущими paths.
- Сначала запускать в diagnostic mode.
- Сравнивать classification с текущим behavior.

### Step 4 — Lowering decision adapters

- Завернуть legacy results в `CompilerLoweringDecision`.
- Все новые callers используют typed decisions.
- Legacy callers временно сохраняются.

### Step 5 — Envelope separation

- Вынести carrier/sideband/descriptor/facts в разные envelope.
- Добавить validators.
- Запретить executable descriptor naming.

### Step 6 — Runtime bridge envelope

- Ввести `RuntimeBridgeEnvelope`.
- Проверить stale contract, facts mismatch, descriptor absence.
- Bridge statuses не должны называться runtime legality.

### Step 7 — Provider registry

- Ввести contour providers.
- Постепенно перенести logic из generic helpers.
- Registry fail-closed on unknown contour.

### Step 8 — Evidence and telemetry

- Добавить structured telemetry.
- Добавить audit snapshots.
- Сделать negative evidence обязательным для rejection paths.

### Step 9 — Legacy cleanup

- Удалить или deprecated все ambiguous APIs.
- Удалить implicit fallback.
- Перенести документацию из RefactorPlan в постоянные ADR/Docs.

## Required negative tests

### Contract and bridge

1. stale `CompilerContract.Version` -> `VersionRejected`.
2. unknown sideband envelope version -> `SidebandRejected`.
3. typed-slot facts mismatch -> `TypedSlotFactsRejected` or `Quarantined`.
4. missing typed-slot facts in compatibility mode -> accepted as compatibility path, not stronger authority.
5. bridge accepted -> runtime legality still required.

### Typed-slot and legality

1. structurally admissible bundle -> not automatically runtime legal.
2. Stage A dynamic gate absent -> compile package remains non-authoritative.
3. Stage B lane materialization not performed by compiler.
4. hazard summary success -> evidence only.

### Carrier / sideband / descriptor

1. carrier emitted without sideband where sideband optional -> accepted as carrier-only.
2. carrier emitted without required sideband -> reject.
3. descriptor parser success -> no execution claim.
4. descriptor ABI success -> no memory/register publication.

### MatrixTile

1. supported helper op -> `HelperOnlyDecision`.
2. unsupported op -> reject.
3. unsupported dtype -> reject.
4. unsupported shape/layout -> reject.
5. unsupported accumulator policy -> reject.
6. rejected MatrixTile -> no scalar/stream fallback.

### DSC / lane6

1. DSC1 scoped descriptor -> transport descriptor only until runtime.
2. DSC2 parser-only -> no executable carrier.
3. DSC missing commit gate -> no publication claim.
4. DSC rejected -> no L7/Stream/scalar fallback.

### L7-SDC / lane7

1. `ACCEL_SUBMIT` without descriptor -> `L7DescriptorMissing` or `L7DescriptorlessSubmitForbidden`.
2. rejected submit -> no fallback to DSC.
3. token evidence -> evidence only, not authority.
4. capability observation -> not capability authority.

### VMX

1. VMX compatibility operation -> `VmxProjectionOnly`.
2. VMX backend emission request -> `VmxBackendEmissionForbidden`.
3. VMCS state mutation from compiler -> forbidden.
4. VmxCaps treated as authority -> forbidden.

### SecureCompute

1. SecureCompute admission descriptor -> policy/admission/evidence only.
2. secure backend execution request -> `SecureComputeEmissionForbidden`.
3. guest/domain architectural state must not receive host-owned evidence.
4. nested/retire/publication claim from compiler -> forbidden.

### Fallback

1. unknown contour -> reject/future-gated, no default scalar fallback.
2. provider failure -> no second provider unless explicit policy allows and evidence records it.
3. diagnostic fallback -> must not emit executable package.

## Exit criteria

Рефакторинг считается завершенным, если выполнены все условия:

- все public lowering paths возвращают `CompilerLoweringDecision`;
- все compiler outputs проходят через `CompilerEmissionPackage` или explicit no-emission decision;
- carrier, sideband, descriptor и typed-slot facts представлены отдельными envelope;
- runtime bridge statuses не содержат runtime legality/execution/commit/retire claims;
- negative test matrix проходит;
- telemetry содержит structured decision path;
- legacy ambiguous APIs удалены или помечены `[Obsolete]`;
- documentation clearly states production-lowering gates;
- VMX и SecureCompute остаются projection/admission-only в compiler layer;
- DSC/L7/MatrixTile не имеют скрытого fallback.

## PR slicing

Рекомендуемая нарезка PR:

1. `docs: document compiler core current behavior`.
2. `refactor(compiler): add authority taxonomy result headers`.
3. `refactor(compiler): add intent and contour classification`.
4. `refactor(compiler): introduce lowering decision hierarchy`.
5. `refactor(compiler): split carrier sideband descriptor envelopes`.
6. `refactor(compiler): add typed-slot runtime bridge envelope`.
7. `refactor(compiler): add contour provider registry`.
8. `test(compiler): add negative contour and no-fallback matrix`.
9. `docs(compiler): promote refactor plan to ADRs`.

## Final non-regression rule

Любой новый compiler feature должен отвечать на вопрос:

```text
What exactly is being produced, and what authority does it not have?
```

Если ответ не указан в типах, telemetry и тестах, feature не готова к merge.