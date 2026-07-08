# Phase 09 — Tests, Migration and Exit Criteria

## Назначение

Собрать обязательную отрицательную матрицу тестов, порядок миграции и критерии, по которым рефакторинг `HybridCPU_Compiler/Core` можно считать завершенным без нарушения модели HybridCPU.

## Strategy

Рефакторинг должен идти в режиме compatibility-first:

```text
observe -> wrap -> early negative gates -> type decisions -> migrate callers -> remove legacy ambiguity
```

Не допускается режим:

```text
rewrite -> hope behavior is equivalent
```

Negative gates are not only final cleanup. Minimal authority-boundary negative tests must exist before behavior migration begins.

## Migration phases

### Step 1 — Documentation and inventory

- Добавить `CURRENT_BEHAVIOR.md`.
- Заполнить таблицу API-точек from Phase 01 mandatory symbol list.
- Пометить ambiguous methods как legacy.
- Разделить same-contour structural fallback and forbidden cross-contour fallback.

### Step 2 — Authority taxonomy types

- Добавить enum/record из фазы 2.
- Не менять runtime behavior.
- Добавить compile-only tests на недопустимые названия/claims.
- Add quarantine wrappers for compiler-side `Legal*`/`IsLegal`/`HasLegalAssignment` vocabulary.

### Step 3 — Early negative gates

Before behavior migration, add tests proving:

- stale compiler contract rejects bridge package;
- `HasLegalAssignment` cannot map to runtime legality;
- helper success cannot map to production lowering;
- descriptor ABI success cannot map to execution authority;
- L7 descriptorless submit remains fail-closed;
- VMX/SecureCompute cannot emit backend carrier;
- bridge ingress accepted still requires runtime Legality A/B.

### Step 4 — Intent and contour classifier

- Ввести classifier рядом с текущими paths.
- Сначала запускать в diagnostic mode.
- Сравнивать classification с текущим behavior.
- Keep `CompilerSemanticIntent` and `CompilerExecutionContourSelection` separate.

### Step 5 — Lowering decision adapters

- Завернуть legacy results в `CompilerLoweringDecision`.
- Все новые callers используют typed decisions.
- Legacy callers временно сохраняются.
- Every adapter must fill `LegacyApiTranslation` and set `StrengthensAuthority == false`.

### Step 6 — Envelope separation

- Вынести carrier/sideband/descriptor/facts/agreement/evidence/bridge в разные envelope.
- Добавить validators.
- Запретить executable descriptor naming.
- Add compatibility adapter from `HybridCpuCompiledProgram`.

### Step 7 — Runtime bridge envelope

- Ввести `RuntimeBridgeEnvelope`.
- Проверить stale contract, facts mismatch, descriptor absence.
- Bridge statuses не должны называться runtime legality.
- Bridge ingress status must preserve Stage A/B runtime ownership.

### Step 8 — Provider registry

- Ввести contour analyzers and providers.
- Постепенно перенести logic из generic helpers.
- Registry fail-closed on unknown contour.
- Capability data is observation only unless explicit authority source/runtime dependency is recorded.

### Step 9 — Evidence and telemetry

- Добавить structured telemetry.
- Добавить audit snapshots.
- Сделать negative evidence обязательным для rejection paths.
- Add evidence ownership and host-owned isolation validators.

### Step 10 — Legacy cleanup

- Удалить или deprecated все ambiguous APIs.
- Удалить implicit fallback.
- Перенести документацию из RefactorPlan в постоянные ADR/Docs.

## Required negative tests

### 09A — Early authority-boundary gates

These tests must be added before any behavior-changing migration:

1. stale `CompilerContract.Version` -> `VersionRejected` / `BridgeIngressRejected`.
2. `HybridCpuSlotModel.SearchAssignments(...).HasLegalAssignment` cannot map directly to runtime legality.
3. `IrCandidateBundleAnalysis.IsLegal` cannot be consumed as runtime Legality A/B.
4. `IrBundleLegalityResult.Legal` cannot be exported as runtime `LegalityDecision`.
5. MatrixTile `TryRecoverFromInstruction == true` is helper ABI recognition only.
6. VectorTransfer `TryRecoverFromInstruction == true` is helper/transport recognition only.
7. Helper success cannot set `ProductionAllowedByExplicitCompilerGate`.
8. Descriptor ABI valid status cannot set execution/publication/commit/retire authority.
9. `ACCEL_SUBMIT` without descriptor remains fail-closed.
10. VMX backend emission request -> `VmxBackendEmissionForbidden`.
11. SecureCompute backend execution request -> `SecureComputeEmissionForbidden`.
12. Bridge ingress accepted -> runtime Legality A and B still required.

### Contract and bridge

1. stale `CompilerContract.Version` -> `VersionRejected`.
2. unknown sideband envelope version -> `SidebandRejected`.
3. typed-slot facts mismatch -> `TypedSlotFactsRejected` or `Quarantined`.
4. missing typed-slot facts in compatibility mode -> accepted as compatibility path, not stronger authority.
5. bridge accepted -> runtime legality still required.
6. bridge accepted -> no execution ready, commit, retire or architectural publication status.
7. runtime policy mode is observed/reference-only, not compiler-owned.

### Typed-slot and structural admission

1. structurally admissible bundle -> not automatically runtime legal.
2. Stage A dynamic gate absent -> compile package remains non-authoritative.
3. Stage B lane materialization not performed by compiler.
4. hazard summary success -> evidence only.
5. `TypedSlotFactsEnvelope.StructuralEvidenceOnly == true` for compiler-produced facts.
6. missing typed-slot facts under compatibility mode are weaker than validated facts.

### Carrier / sideband / descriptor

1. `HybridCpuBundleLowerer.LowerBundle` returns carrier artifact only; no execution/publication/commit/retire authority.
2. `HybridCpuBundleLowerer.EmitFactsForBundle` produces typed-slot facts evidence only; no legality authority.
3. `HybridCpuBundleLowerer.EmitAnnotationsForBundle` produces sideband only; no authority.
4. `HybridCpuCompiledProgram.EmitVliwBundleImage` writes/emits an image but does not claim execution, publication, commit or retire.
5. `VliwBundleAnnotations.Empty` compatibility path never strengthens authority.
6. `IrAdmissibilityAgreement.TotalBundleCount` match is structural agreement only.
7. carrier emitted without sideband where sideband optional -> accepted as carrier-only.
8. carrier emitted without required sideband -> reject.
9. descriptor parser success -> no execution claim.
10. descriptor ABI success -> no memory/register publication.
11. descriptor envelope cannot contain `Executable`, `CanExecute`, `IsLegal`, `Commit`, `Retire` fields.

### MatrixTile

1. supported helper op -> `HelperOnlyDecision` or `HelperAbiOnly` production status.
2. unsupported op -> reject.
3. unsupported dtype -> reject.
4. unsupported shape/layout -> reject.
5. unsupported accumulator policy -> reject.
6. rejected MatrixTile -> no scalar/stream fallback.
7. helper success -> no general matrix compiler claim.
8. helper success -> no production lowering claim.

### Stream / vector

1. supported scoped helper/transport path -> not broad vector compiler.
2. unsupported vector shape -> reject/no-emission.
3. unsupported vector dtype -> reject/no-emission.
4. unsupported predicate/stride -> reject/no-emission.
5. rejected Stream/vector -> no scalar fallback.
6. vector recovery false -> typed reject/parser/no-emission decision, not silent null semantics.

### DSC / lane6

1. DSC1 scoped descriptor -> transport descriptor only until runtime.
2. DSC2 parser-only -> no executable carrier.
3. DSC missing commit gate -> no publication claim.
4. DSC rejected -> no L7/Stream/scalar fallback.
5. lane6 `DmaStreamComputeDescriptor` on non-DSC opcode is rejected.
6. lane6 and lane7 descriptors on same instruction are rejected.
7. descriptor ABI valid status does not allow memory/register publication.

### L7-SDC / lane7

1. `ACCEL_SUBMIT` without descriptor -> `L7DescriptorMissing` or `L7DescriptorlessSubmitForbidden`.
2. rejected submit -> no fallback to DSC.
3. token evidence -> evidence only, not authority.
4. capability observation -> not capability authority.
5. descriptorless submit remains fail-closed after refactor.
6. descriptor success does not imply authority.

### VMX

1. VMX compatibility operation -> `VmxProjectionOnly`.
2. VMX backend emission request -> `VmxBackendEmissionForbidden`.
3. VMCS state mutation from compiler -> forbidden.
4. VmxCaps treated as authority -> forbidden.
5. VMX cannot emit backend carrier.
6. VMX projection cannot mutate VMCS ownership.

### SecureCompute

1. SecureCompute admission descriptor -> policy/admission/evidence only.
2. secure backend execution request -> `SecureComputeEmissionForbidden`.
3. guest/domain architectural state must not receive host-owned evidence.
4. nested/retire/publication claim from compiler -> forbidden.
5. SecureCompute cannot emit secure backend execution carrier.

### Fallback

1. unknown contour -> reject/future-gated, no default scalar fallback.
2. provider failure -> no second provider unless explicit policy allows and evidence records it.
3. diagnostic fallback -> must not emit executable package.
4. same-contour placement fallback from global search to local materialization is recorded as structural placement fallback, not lowering fallback.
5. any cross-contour fallback without explicit policy fails.

### Evidence and telemetry

1. every decision logs `intent.kind` and `contour.kind`.
2. every decision logs `decision.kind`, `emission.class`, `production_lowering.status`.
3. every decision logs `authority.class`, `authority.source_kind`, `evidence.class`.
4. every decision logs `fallback.policy` and `fallback.proof_id`.
5. negative decisions produce evidence, not only free-form string.
6. host-owned evidence isolation validator rejects guest/domain state projection.

## Exit criteria

Рефакторинг считается завершенным, если выполнены все условия:

```text
[ ] Every public compiler lowering entrypoint returns CompilerLoweringDecision or explicit NoEmission decision.
[ ] No public compiler API exposes raw Success/Valid/Accepted/IsLegal/CanExecute without typed authority class.
[ ] All legacy bool/Try/HasLegalAssignment APIs have LegacyApiTranslation adapters or are obsolete/internal.
[ ] CompilerStructuralAdmissionResult cannot be assigned to runtime LegalityDecision.
[ ] Carrier, sideband, descriptor, typed-slot facts, agreement, bridge and evidence are separate envelope fields.
[ ] Descriptor ABI valid status never implies execution, publication, commit, retire or runtime legality.
[ ] Helper success never implies production lowering.
[ ] Parser success never implies production lowering.
[ ] L7 ACCEL_SUBMIT without descriptor sideband rejects fail-closed.
[ ] MatrixTile unsupported op/dtype/shape/layout/accumulator rejects with no fallback.
[ ] DSC rejected path has no L7/Stream/scalar fallback.
[ ] Unknown contour rejects or future-gates with no scalar fallback.
[ ] Same-contour placement fallback is recorded separately from lowering fallback.
[ ] Runtime bridge accepted status still requires runtime Legality A/B.
[ ] VMX remains projection/no-emission and cannot own VMCS state.
[ ] SecureCompute remains policy/admission/evidence-only and cannot emit secure backend execution.
[ ] Capability observation cannot become authority without explicit source and runtime dependency.
[ ] Host-owned evidence is blocked from guest/domain architectural state.
[ ] Telemetry contains decision.kind, contour.kind, authority.class, evidence.class, emission.class, production_lowering.status and fallback proof id.
[ ] Early negative gates pass before behavior migration starts.
[ ] Final ADRs document what each compiler product is and what authority it does not have.
```

## PR slicing

Рекомендуемая нарезка PR:

1. `docs: document compiler core current behavior`.
2. `test(compiler): add early authority-boundary negative gates`.
3. `refactor(compiler): add authority taxonomy result headers`.
4. `refactor(compiler): add intent and contour classification`.
5. `refactor(compiler): introduce lowering decision hierarchy`.
6. `refactor(compiler): split carrier sideband descriptor envelopes`.
7. `refactor(compiler): add typed-slot runtime bridge envelope`.
8. `refactor(compiler): add contour analyzer/provider registry`.
9. `refactor(compiler): add evidence ownership and telemetry snapshots`.
10. `test(compiler): add full negative contour and no-fallback matrix`.
11. `docs(compiler): promote refactor plan to ADRs`.

## Final non-regression rule

Любой новый compiler feature должен отвечать на вопрос:

```text
What exactly is being produced, and what authority does it not have?
```

Если ответ не указан в типах, telemetry и тестах, feature не готова к merge.

## Non-goals

- Не добавлять production backend lowering как часть test migration.
- Не смягчать fail-closed модель ради совместимости.
- Не заменять runtime Stage A/B compiler-side checks.
- Не превращать descriptor/token/capability/evidence в authority.
